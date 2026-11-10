using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using Pulsewave.Themes;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Orientation = System.Windows.Controls.Orientation;
using Size = System.Windows.Size;
using VerticalAlignment = System.Windows.VerticalAlignment;

namespace Pulsewave;

/// <summary>
/// The HUD's Colors page: the current theme in its own colors or any of the palettes, each tile a small
/// preview of the palette (its background with the accents as dots). Tap one to recolor the theme;
/// the choice is remembered for that theme. ✕ or 30 seconds without a tap closes it.
/// </summary>
internal sealed class PaletteView : Grid
{
    private readonly DispatcherTimer _idle = new() { Interval = TimeSpan.FromSeconds(30) };
    private readonly UniformGrid _tiles;
    private readonly TextBlock _subtitle = OneUi.Label("", 18, OneUi.Secondary);
    private readonly double _tileHeight;

    public event Action? CloseRequested;
    /// <summary>A palette's name, or "" for the theme's own colors.</summary>
    public event Action<string>? PaletteChosen;

    private readonly bool _wide;

    public PaletteView(Size size)
    {
        Width = size.Width;
        Height = size.Height;
        Background = OneUi.Background();
        bool wide = _wide = size.Width >= size.Height;
        _tiles = new UniformGrid { Columns = wide ? 5 : 2, VerticalAlignment = VerticalAlignment.Top };
        _tileHeight = wide ? 70 : 96;
        _idle.Tick += (_, _) => { _idle.Stop(); CloseRequested?.Invoke(); };
        MouseLeftButtonUp += (_, e) => { e.Handled = true; if (_idle.IsEnabled) { _idle.Stop(); _idle.Start(); } }; // not after ✕ closed it

        var layout = new DockPanel { Margin = new Thickness(20, 14, 20, 12) };
        var header = new DockPanel { Margin = new Thickness(8, 0, 0, 4) };
        var close = OneUi.Circle("", 60, () => CloseRequested?.Invoke());
        DockPanel.SetDock(close, Dock.Right);
        header.Children.Add(close);
        var title = OneUi.Title("Colors");
        title.VerticalAlignment = VerticalAlignment.Center;
        DockPanel.SetDock(title, Dock.Left);
        header.Children.Add(title);
        _subtitle.VerticalAlignment = VerticalAlignment.Center;
        _subtitle.Margin = new Thickness(24, 0, 12, 0);
        header.Children.Add(_subtitle);
        DockPanel.SetDock(header, Dock.Top);
        layout.Children.Add(header);
        // Scrolls with a drag (or the mouse wheel) when the palettes don't all fit.
        var list = new ScrollViewer { Content = _tiles, VerticalScrollBarVisibility = ScrollBarVisibility.Hidden, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Focusable = false, PanningMode = PanningMode.None };
        layout.Children.Add(OneUi.Card(list, radius: 40, padding: 10));
        Children.Add(layout);
    }

    /// <summary>Shows the palettes with <paramref name="current"/> ("" = the theme's own) marked; <paramref name="theme"/> is null for Classic.</summary>
    public void Open(string current, string? theme)
    {
        _subtitle.Text = theme == null ? "Palettes recolor custom themes; Classic keeps its own look." : theme;
        _tiles.Children.Clear();
        _tiles.Children.Add(Tile("", _wide ? "Theme's own colors" : "Theme's own", ["#FF2A3346", "#FF7FD8FF", "#FFFF8FB1", "#FFC3A6FF"], ThemePalettes.Find(current) == null, theme != null));
        foreach (var palette in ThemePalettes.All)
            _tiles.Children.Add(Tile(palette.Name, palette.Name, [palette.Surface, .. palette.Accents], ThemePalettes.Find(current) == palette, theme != null));
        _idle.Stop();
        _idle.Start();
    }

    public void Close() => _idle.Stop();

    private Border Tile(string key, string name, string[] colors, bool chosen, bool enabled)
    {
        Color C(string hex) => (Color)ColorConverter.ConvertFromString(hex.Length == 7 ? "#FF" + hex[1..] : hex);
        var dots = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 14, 0) };
        foreach (var hex in colors.Skip(1))
            dots.Children.Add(new Border { Width = 22, Height = 22, CornerRadius = new CornerRadius(11), Margin = new Thickness(0, 0, 6, 0), Background = new SolidColorBrush(C(hex)),
                BorderBrush = OneUi.GlassEdge, BorderThickness = new Thickness(1) });
        // On a narrow screen the name goes under the dots, so it is not cut short beside them.
        var row = new DockPanel { VerticalAlignment = VerticalAlignment.Center };
        DockPanel.SetDock(dots, _wide ? Dock.Left : Dock.Top);
        if (!_wide) dots.Margin = new Thickness(0, 0, 0, 6);
        row.Children.Add(dots);
        var label = OneUi.Label(name, _wide ? 19 : 18, bold: true);
        label.VerticalAlignment = VerticalAlignment.Center;
        row.Children.Add(label);
        var background = new LinearGradientBrush(C(colors[0]), Color.FromArgb(0xCC, C(colors[0]).R, C(colors[0]).G, C(colors[0]).B), 0);
        var tile = new Border
        {
            Height = _tileHeight, CornerRadius = new CornerRadius(_tileHeight / 2), Margin = new Thickness(5), Padding = new Thickness(22, 0, 18, 0),
            Background = background, BorderBrush = chosen ? OneUi.OnFill : OneUi.GlassEdge, BorderThickness = new Thickness(chosen ? 3 : 1),
            Cursor = System.Windows.Input.Cursors.Hand, Child = row, Tag = "palette:" + key, Opacity = enabled ? 1 : 0.45
        };
        if (enabled) tile.MouseLeftButtonUp += (_, _) => PaletteChosen?.Invoke(key);
        return tile;
    }
}
