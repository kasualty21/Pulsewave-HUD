using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Image = System.Windows.Controls.Image;
using Point = System.Windows.Point;
using Size = System.Windows.Size;
using VerticalAlignment = System.Windows.VerticalAlignment;

namespace Pulsewave;

/// <summary>
/// The HUD's volume page in the One UI style: all sound and every app with sound as pill sliders
/// (side by side on a bar, stacked rows on a tall screen), the app's icon riding in the fill, the
/// level at the far end and a round mute button. Tap a slider where the level should be.
/// ✕ or 30 seconds without a tap closes it.
/// </summary>
internal sealed class MixerView : Grid
{
    internal const string MasterKey = "";
    private const string SpeakerGlyph = "", MuteGlyph = "", SystemGlyph = "", AppGlyph = "";

    private readonly DispatcherTimer _idle = new() { Interval = TimeSpan.FromSeconds(30) };
    private readonly Grid _cards = new();
    private readonly bool _wide;
    private readonly int _maxCards;
    private readonly Dictionary<string, ImageSource?> _icons = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, (PillSlider Slider, Border Mute)> _parts = new(StringComparer.OrdinalIgnoreCase);
    private string _shownKeys = "\0";

    public event Action? CloseRequested;
    /// <summary>A slider tap: the key (<see cref="MasterKey"/> for the device) and the level 0–100.</summary>
    public event Action<string, double>? LevelRequested;
    /// <summary>A mute button: the key and whether to mute.</summary>
    public event Action<string, bool>? MuteRequested;
    /// <summary>Where a tap landed (touch is replayed at the finger, not the mouse cursor).</summary>
    public Func<System.Windows.Input.MouseEventArgs, UIElement, Point> PositionOf { get; set; } = (e, element) => e.GetPosition(element);

    public MixerView(Size size)
    {
        Width = size.Width;
        Height = size.Height;
        Background = OneUi.Background();
        _wide = size.Width >= size.Height;
        _maxCards = _wide ? 7 : 30;
        _idle.Tick += (_, _) => { _idle.Stop(); CloseRequested?.Invoke(); };
        MouseLeftButtonUp += (_, e) => { e.Handled = true; if (_idle.IsEnabled) { _idle.Stop(); _idle.Start(); } }; // not after ✕ closed it

        var layout = new DockPanel { Margin = new Thickness(20, 14, 20, 12) };
        var header = new DockPanel { Margin = new Thickness(8, 0, 0, 4) };
        var close = OneUi.Circle("", 60, () => CloseRequested?.Invoke());
        DockPanel.SetDock(close, Dock.Right);
        header.Children.Add(close);
        var title = OneUi.Label("Volume", 32, bold: true);
        title.VerticalAlignment = VerticalAlignment.Center;
        header.Children.Add(title);
        DockPanel.SetDock(header, Dock.Top);
        layout.Children.Add(header);
        // On a tall screen many apps can be listed: the rows scroll with a drag (or the mouse wheel).
        UIElement content = _wide ? _cards : new ScrollViewer { Content = _cards, VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Focusable = false, PanningMode = PanningMode.None };
        var card = OneUi.Card(content, radius: 40, padding: _wide ? 18 : 16);
        if (!_wide) card.VerticalAlignment = VerticalAlignment.Top;
        layout.Children.Add(card);
        Children.Add(layout);
    }

    public void Open() { _idle.Stop(); _idle.Start(); }

    public void Close() => _idle.Stop();

    /// <summary>Shows the device and apps; rebuilt only when the set of apps changes, so a tap never lands on a moving control.</summary>
    public void Show((double Level, bool Muted)? master, IReadOnlyList<AppAudio> apps)
    {
        var rows = new List<(string Key, string Name, string? Path, double Level, bool Muted)>();
        if (master is { } m) rows.Add((MasterKey, "All sound", null, m.Level, m.Muted));
        rows.AddRange(apps.Take(_maxCards - rows.Count).Select(a => (a.Key, a.Name, a.ProgramPath, a.Level, a.Muted)));
        string keys = string.Join("\n", rows.Select(r => r.Key));
        if (keys != _shownKeys)
        {
            _shownKeys = keys;
            _cards.Children.Clear();
            _cards.ColumnDefinitions.Clear();
            _cards.RowDefinitions.Clear();
            _parts.Clear();
            for (int i = 0; i < rows.Count; i++)
            {
                var item = Build(rows[i].Key, rows[i].Name, rows[i].Path);
                if (_wide) { _cards.ColumnDefinitions.Add(new ColumnDefinition()); SetColumn(item, i); }
                else { _cards.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); SetRow(item, i); }
                _cards.Children.Add(item);
            }
            if (rows.Count == 0) _cards.Children.Add(OneUi.Label("No playback device.", 24, OneUi.Secondary));
        }
        foreach (var row in rows) Set(row.Key, row.Level, row.Muted, refresh: true);
    }

    /// <summary>After a tap: show the new level or mute straight away. A refresh leaves a slider the user is moving alone.</summary>
    public void Set(string key, double level, bool muted, bool refresh = false)
    {
        if (!_parts.TryGetValue(key, out var parts)) return;
        if (refresh) parts.Slider.Show(level, muted); else parts.Slider.Set(level, muted);
        OneUi.SetOn(parts.Mute, muted);
        OneUi.SetGlyph(parts.Mute, muted ? MuteGlyph : SpeakerGlyph);
        parts.Mute.Tag = muted ? "muted" : "mute";
    }

    private FrameworkElement Build(string key, string name, string? path)
    {
        double thickness = _wide ? 104 : 88;
        var slider = new PillSlider(vertical: _wide, thickness, "slider:" + key) { PositionOf = (e, element) => PositionOf(e, element) };
        slider.SetIcon(Icon(key, path, thickness));
        slider.LevelChosen += level => LevelRequested?.Invoke(key, level);
        Border mute = null!;
        mute = OneUi.Circle(SpeakerGlyph, _wide ? 58 : 72, () => MuteRequested?.Invoke(key, mute.Tag as string != "muted"), tag: "mute");
        _parts[key] = (slider, mute);
        var label = OneUi.Label(name, _wide ? 17 : 20, OneUi.Secondary, bold: true);

        if (_wide)
        {
            // A column: the slider, its name, the mute button.
            var column = new Grid { Margin = new Thickness(6, 0, 6, 0) };
            column.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            column.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            column.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            slider.HorizontalAlignment = HorizontalAlignment.Center;
            label.HorizontalAlignment = HorizontalAlignment.Center;
            label.Margin = new Thickness(0, 8, 0, 6);
            SetRow(label, 1); SetRow(mute, 2);
            column.Children.Add(slider); column.Children.Add(label); column.Children.Add(mute);
            return column;
        }
        // A row: the name over a long slider, the mute button at the end.
        var row = new Grid { Margin = new Thickness(0, 4, 0, 10) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        row.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        label.Margin = new Thickness(10, 0, 0, 6);
        mute.Margin = new Thickness(14, 0, 0, 0);
        SetRow(slider, 1); SetRow(mute, 1); SetColumn(mute, 1);
        row.Children.Add(label); row.Children.Add(slider); row.Children.Add(mute);
        return row;
    }

    private FrameworkElement Icon(string key, string? path, double thickness)
    {
        if (path != null && !_icons.ContainsKey(path)) _icons[path] = ShortcutIcons.Load(new AppShortcut { Target = path }, 96);
        if (path != null && _icons[path] is { } source)
        {
            var image = new Image { Source = source, Width = thickness * 0.44, Height = thickness * 0.44, Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
            return image;
        }
        return OneUi.Glyph(key == MasterKey ? SpeakerGlyph : key == AppVolume.SystemKey ? SystemGlyph : AppGlyph, thickness * 0.34, OneUi.OnText);
    }
}
