using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using Color = System.Windows.Media.Color;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Point = System.Windows.Point;
using Size = System.Windows.Size;
using VerticalAlignment = System.Windows.VerticalAlignment;

namespace Pulsewave;

/// <summary>
/// The HUD's Lights page in the One UI style: round power buttons, a brightness pill slider (tap
/// where it should be), round color swatches, and OpenRGB profiles and effects as pills, on frosted
/// cards laid out for the display's shape. ✕ or 30 seconds without a tap closes it.
/// </summary>
internal sealed class LightsView : Grid
{
    /// <summary>The swatches offered on the HUD (any color can be set in tray → Lights).</summary>
    internal static readonly string[] Swatches =
        ["#FFFFFF", "#FF2020", "#FF7A00", "#FFD000", "#20FF40", "#00E5FF", "#2060FF", "#8A2BFF", "#FF20C0", "#FFB070"];
    private const string PowerGlyph = "", SunGlyph = "";

    private readonly DispatcherTimer _idle = new() { Interval = TimeSpan.FromSeconds(30) };
    private readonly TextBlock _status = OneUi.Label("", 18, OneUi.Secondary);
    private readonly UniformGrid _colors = new() { Columns = 5 }, _profiles = new() { Columns = 2, VerticalAlignment = VerticalAlignment.Top },
        _effects = new() { Columns = 2, VerticalAlignment = VerticalAlignment.Top };
    private readonly PillSlider _brightness = new(vertical: false, 72, "slider:brightness");
    private readonly Border _off, _on;
    private readonly int _maxTiles;
    private LightsSettings _settings = new();
    private string? _profile, _effect;

    public event Action? CloseRequested;
    /// <summary>The "Open OpenRGB" pill: show or start the OpenRGB program.</summary>
    public event Action? OpenAppRequested;
    public event Action<LightsCommand>? Requested;
    public event Action<string>? ColorChosen;
    public event Action<int>? BrightnessChosen;
    /// <summary>Where a tap landed (touch is replayed at the finger, not the mouse cursor).</summary>
    public Func<System.Windows.Input.MouseEventArgs, UIElement, Point> PositionOf
    {
        get => _brightness.PositionOf;
        set => _brightness.PositionOf = value;
    }

    public LightsView(Size size)
    {
        Width = size.Width;
        Height = size.Height;
        Background = OneUi.Background();
        bool wide = size.Width >= size.Height;
        _maxTiles = 40; // lists scroll
        _idle.Tick += (_, _) => { _idle.Stop(); CloseRequested?.Invoke(); };
        MouseLeftButtonUp += (_, e) => { e.Handled = true; if (_idle.IsEnabled) { _idle.Stop(); _idle.Start(); } }; // not after ✕ closed it

        var layout = new DockPanel { Margin = new Thickness(20, 14, 20, 12) };
        var header = new DockPanel { Margin = new Thickness(8, 0, 0, 4) };
        var close = OneUi.Circle("", 60, () => CloseRequested?.Invoke());
        DockPanel.SetDock(close, Dock.Right);
        header.Children.Add(close);
        var openApp = OneUi.Pill("Open OpenRGB", 60, () => OpenAppRequested?.Invoke(), glyph: "", size: 19);
        DockPanel.SetDock(openApp, Dock.Right);
        header.Children.Add(openApp);
        var title = OneUi.Title("Lights");
        title.VerticalAlignment = VerticalAlignment.Center;
        DockPanel.SetDock(title, Dock.Left);
        header.Children.Add(title);
        _status.VerticalAlignment = VerticalAlignment.Center;
        DockPanel.SetDock(header, Dock.Top);
        if (wide)
        {
            _status.Margin = new Thickness(24, 0, 12, 0);
            header.Children.Add(_status);
            layout.Children.Add(header);
        }
        else
        {
            // A tall screen has no room beside the buttons: the status gets its own line.
            header.Children.Add(new Border());
            layout.Children.Add(header);
            _status.Margin = new Thickness(10, 0, 10, 4);
            DockPanel.SetDock(_status, Dock.Top);
            layout.Children.Add(_status);
        }

        // Power and brightness.
        _off = OneUi.Circle(PowerGlyph, 76, () => Request(new LightsCommand(LightsKind.Off)), tag: "Off");
        _on = OneUi.Circle(PowerGlyph, 76, () => Request(new LightsCommand(LightsKind.On)), tag: "On");
        var power = new UniformGrid { Columns = 2 };
        foreach (var (button, label) in new[] { (_off, "Off"), (_on, "On") })
        {
            var stack = new StackPanel();
            stack.Children.Add(button);
            var caption = OneUi.Label(label, 17, OneUi.Secondary, bold: true);
            caption.HorizontalAlignment = HorizontalAlignment.Center;
            caption.Margin = new Thickness(0, 6, 0, 0);
            stack.Children.Add(caption);
            power.Children.Add(stack);
        }
        _brightness.SetIcon(OneUi.Glyph(SunGlyph, 28, OneUi.OnText));
        _brightness.Margin = new Thickness(0, 16, 0, 0);
        _brightness.LevelChosen += level =>
        {
            int value = (int)Math.Round(level / 5) * 5;
            if (value == _settings.Brightness) return;
            BrightnessChosen?.Invoke(value);
            _brightness.Set(value, text: $"{value}%");
        };
        var powerStack = new StackPanel();
        powerStack.Children.Add(power);
        powerStack.Children.Add(_brightness);

        var sections = new Grid { ClipToBounds = true };
        var parts = new (string Title, UIElement Body, double Share)[]
        {
            ("Power", powerStack, 1), ("Color", _colors, 1.25), ("Profiles", Scroll(_profiles, wide), 1.3), ("Effects", Scroll(_effects, wide), 1.3)
        };
        for (int i = 0; i < parts.Length; i++)
        {
            var card = Section(parts[i].Title, parts[i].Body);
            if (wide) { sections.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(parts[i].Share, GridUnitType.Star) }); SetColumn(card, i); }
            else { sections.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); SetRow(card, i); }
            sections.Children.Add(card);
        }
        layout.Children.Add(sections);
        Children.Add(layout);
    }

    /// <summary>Shows the page while OpenRGB is asked what it has; call <see cref="ShowState"/> with the answer.</summary>
    public void Open(LightsSettings settings)
    {
        _settings = settings;
        _profiles.Children.Clear();
        _effects.Children.Clear();
        _profiles.Children.Add(Note("Asking OpenRGB…"));
        ShowStatus("Connecting to OpenRGB…");
        Refresh();
        _idle.Stop();
        _idle.Start();
    }

    public void Close() => _idle.Stop();

    public void ShowStatus(string text) => _status.Text = text;

    /// <summary>Fills in profiles and effects (or explains why there are none).</summary>
    public void ShowState(LightsState? state, string status)
    {
        ShowStatus(status);
        _profiles.Children.Clear();
        _effects.Children.Clear();
        if (state == null)
        {
            _profiles.Children.Add(Note("OpenRGB isn't connected."));
            return;
        }
        if (state.Profiles.Count == 0)
            _profiles.Children.Add(Note(state.Protocol < 2 ? "Update OpenRGB for profiles." : "No profiles saved in OpenRGB yet."));
        foreach (var name in state.Profiles.Take(_maxTiles))
            _profiles.Children.Add(OneUi.Pill(name, 60, () => Request(new LightsCommand(LightsKind.Profile, name)), selected: name == _profile));
        var effects = Lights.Effects(state, _settings);
        if (effects.Count == 0) _effects.Children.Add(Note("No hardware effects on these devices."));
        foreach (var (name, _) in effects.Take(_maxTiles))
            _effects.Children.Add(OneUi.Pill(name, 60, () => Request(new LightsCommand(LightsKind.Effect, name)), selected: name == _effect));
    }

    private void Refresh()
    {
        _profile = _settings.LastKind == "Profile" && !_settings.Off ? _settings.LastValue : null;
        _effect = _settings.LastKind == "Effect" && !_settings.Off ? _settings.LastValue : null;
        _brightness.Set(_settings.Brightness, text: $"{_settings.Brightness}%");
        OneUi.SetOn(_off, _settings.Off);
        OneUi.SetOn(_on, !_settings.Off);
        _colors.Children.Clear();
        var swatches = Swatches.ToList();
        string current = "#" + _settings.Color.TrimStart('#')[^6..].ToUpperInvariant();
        // A color chosen in the tray stays one tap away, in place of the last swatch unless one is close to it.
        Lights.TryColor(current, out var mine);
        if (!swatches.Any(hex => Lights.TryColor(hex, out var c) && Math.Abs(c.R - mine.R) + Math.Abs(c.G - mine.G) + Math.Abs(c.B - mine.B) < 48))
            swatches[^1] = current;
        foreach (var hex in swatches)
        {
            Lights.TryColor(hex, out var color);
            bool on = Close(hex, current) && _settings.LastKind is "Color" or "" && !_settings.Off;
            var swatch = new Border
            {
                Width = 60, Height = 60, CornerRadius = new CornerRadius(30), Margin = new Thickness(6), Background = new SolidColorBrush(color),
                BorderBrush = on ? OneUi.OnFill : OneUi.GlassEdge, BorderThickness = new Thickness(on ? 4 : 1), Cursor = System.Windows.Input.Cursors.Hand,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Tag = "swatch"
            };
            // A ring with a gap, like One UI's selected color.
            if (on) swatch.Child = new Border { CornerRadius = new CornerRadius(26), BorderBrush = new SolidColorBrush(Color.FromArgb(0xCC, 0x10, 0x14, 0x1E)), BorderThickness = new Thickness(3) };
            swatch.MouseLeftButtonUp += (_, _) => { ColorChosen?.Invoke(hex); Refresh(); };
            _colors.Children.Add(swatch);
        }
    }

    private static bool Close(string a, string b) =>
        Lights.TryColor(a, out var x) && Lights.TryColor(b, out var y) && Math.Abs(x.R - y.R) + Math.Abs(x.G - y.G) + Math.Abs(x.B - y.B) < 48;

    private void Request(LightsCommand command)
    {
        ShowStatus("Working…");
        Requested?.Invoke(command);
    }

    /// <summary>Called after a command ran, so the chosen power button, swatch, profile or effect is marked.</summary>
    public void Applied(LightsCommand command, string status)
    {
        ShowStatus(status);
        Refresh();
        foreach (var grid in new[] { _profiles, _effects })
            foreach (var tile in grid.Children.OfType<Border>())
                if (tile.Tag is string name)
                    OneUi.SetOn(tile, grid == _profiles ? name == _profile : name == _effect);
    }

    private static Border Section(string title, UIElement body)
    {
        var dock = new DockPanel();
        var heading = OneUi.Caption(title);
        heading.Margin = new Thickness(6, 0, 0, 10);
        DockPanel.SetDock(heading, Dock.Top);
        dock.Children.Add(heading);
        dock.Children.Add(body);
        return OneUi.Card(dock, radius: 36, padding: 16);
    }

    /// <summary>On a bar the profile and effect lists scroll with a drag (or the mouse wheel) when they don't fit.</summary>
    private static UIElement Scroll(UIElement list, bool wide) => !wide ? list : new ScrollViewer { Content = list, VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Focusable = false, PanningMode = PanningMode.None };

    private static TextBlock Note(string text)
    {
        var note = OneUi.Label(text, 19, OneUi.Secondary);
        note.TextWrapping = TextWrapping.Wrap;
        note.Margin = new Thickness(6);
        return note;
    }
}
