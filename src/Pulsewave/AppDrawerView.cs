using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Pulsewave.Themes;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using FontFamily = System.Windows.Media.FontFamily;
using Image = System.Windows.Controls.Image;
using Size = System.Windows.Size;

namespace Pulsewave;

/// <summary>
/// The app drawer: every shortcut as a large tile, laid out for the display's shape like the
/// details page. Tapping a tile launches it and closes the drawer; ✕ or 30 seconds without a
/// tap closes it too.
/// </summary>
public sealed class AppDrawerView : Grid
{
    // Glyph shortcuts (web links, keys) in Halo's sky blue; messages in the page's secondary text.
    private static readonly Brush Dim = OneUi.Secondary, Accent = Frozen(0xFF, 0x7F, 0xD8, 0xFF);
    private static readonly FontFamily Icons = new("Segoe Fluent Icons, Segoe MDL2 Assets");

    /// <summary>Tiles on one page; more shortcuts than this get ‹ › page buttons in the header.</summary>
    public const int PerPage = 24;

    private readonly DispatcherTimer _idle = new() { Interval = TimeSpan.FromSeconds(30) };
    private readonly Grid _tiles = new();
    private readonly Size _area;
    private readonly StackPanel _pager = new() { Orientation = System.Windows.Controls.Orientation.Horizontal, Margin = new Thickness(0, 0, 12, 0) };
    private readonly TextBlock _pageLabel = new() { FontSize = 24, VerticalAlignment = System.Windows.VerticalAlignment.Center, Margin = new Thickness(14, 0, 14, 0) };
    private List<(ShortcutView View, int Slot)> _shortcuts = [];
    private int _page;

    /// <summary>The page shown (0-based) and how many there are, for checks.</summary>
    public (int Page, int Pages) Paging => (_page, Pages(_shortcuts.Count));
    public static int Pages(int count) => Math.Max(1, (count + PerPage - 1) / PerPage);

    public event Action? CloseRequested;
    /// <summary>A tile was tapped: the 1-based slot to launch.</summary>
    public event Action<int>? LaunchRequested;

    public AppDrawerView(Size size)
    {
        Width = size.Width;
        Height = size.Height;
        Background = OneUi.Background();
        _idle.Tick += (_, _) => { _idle.Stop(); CloseRequested?.Invoke(); };
        // Taps on the drawer never reach the HUD underneath.
        MouseLeftButtonUp += (_, e) => { e.Handled = true; if (_idle.IsEnabled) { _idle.Stop(); _idle.Start(); } }; // not after ✕ closed it

        var layout = new DockPanel { Margin = new Thickness(20, 14, 20, 12) };
        var header = new DockPanel { Margin = new Thickness(0, 0, 0, 12) };
        var close = OneUi.Circle("", 60, () => CloseRequested?.Invoke());
        DockPanel.SetDock(close, Dock.Right);
        header.Children.Add(close);
        _pageLabel.Foreground = Dim;
        _pageLabel.FontFamily = OneUi.Font;
        _pager.Children.Add(OneUi.Circle("", 60, () => ShowPage(_page - 1)));
        _pager.Children.Add(_pageLabel);
        _pager.Children.Add(OneUi.Circle("", 60, () => ShowPage(_page + 1)));
        DockPanel.SetDock(_pager, Dock.Right);
        header.Children.Add(_pager);
        var title = OneUi.Label("Apps", 32, bold: true);
        title.VerticalAlignment = System.Windows.VerticalAlignment.Center;
        title.Margin = new Thickness(8, 0, 0, 0);
        header.Children.Add(title);
        DockPanel.SetDock(header, Dock.Top);
        layout.Children.Add(header);
        layout.Children.Add(_tiles);
        Children.Add(layout);
        _area = new Size(Math.Max(1, size.Width - 32), Math.Max(1, size.Height - 12 - 14 - 60 - 12));
    }

    /// <summary>The tile grid for a number of shortcuts: as many columns as gives the largest square-ish tiles.</summary>
    public static (int Columns, int Rows, double Tile) Layout(int count, Size area)
    {
        count = Math.Max(1, count);
        (int, int, double) best = (1, count, 0);
        for (int columns = 1; columns <= count; columns++)
        {
            int rows = (count + columns - 1) / columns;
            double tile = Math.Min(Math.Min(area.Width / columns, area.Height / rows), 240);
            if (tile > best.Item3 + 0.5) best = (columns, rows, tile);
        }
        return best;
    }

    /// <summary>Fills the drawer with the shortcuts (slot order; empty slots are left out) and restarts the idle timer.</summary>
    public void Open(IReadOnlyList<ShortcutView?> slots)
    {
        _shortcuts = slots.Select((view, i) => (View: view!, Slot: i + 1)).Where(s => s.View != null).ToList();
        ShowPage(0);
    }

    /// <summary>Shows one page of tiles (clamped to the pages there are) and restarts the idle timer.</summary>
    public void ShowPage(int page)
    {
        int pages = Pages(_shortcuts.Count);
        _page = Math.Clamp(page, 0, pages - 1);
        _pager.Visibility = pages > 1 ? Visibility.Visible : Visibility.Collapsed;
        _pageLabel.Text = $"{_page + 1} / {pages}";
        var shortcuts = _shortcuts.Skip(_page * PerPage).Take(PerPage).ToList();
        _tiles.Children.Clear();
        _tiles.RowDefinitions.Clear();
        _tiles.ColumnDefinitions.Clear();
        if (shortcuts.Count == 0)
        {
            _tiles.Children.Add(new TextBlock
            {
                Text = "No shortcuts yet.\nAdd programs, games and links from the tray: Shortcuts…", FontSize = 26, Foreground = Dim,
                TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap, FontFamily = OneUi.Font,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center, VerticalAlignment = System.Windows.VerticalAlignment.Center
            });
        }
        else
        {
            // With several pages every page uses the full page's grid, so tiles keep their size and place from page to
            // page; a shorter last page leaves its remaining cells empty instead of reshaping.
            var (columns, rows, tile) = Layout(pages > 1 ? PerPage : shortcuts.Count, _area);
            _tiles.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
            _tiles.VerticalAlignment = System.Windows.VerticalAlignment.Center;
            for (int c = 0; c < columns; c++) _tiles.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(tile) });
            for (int r = 0; r < rows; r++) _tiles.RowDefinitions.Add(new RowDefinition { Height = new GridLength(tile) });
            for (int i = 0; i < shortcuts.Count; i++)
            {
                var card = Tile(shortcuts[i].View!, tile);
                int slot = shortcuts[i].Slot;
                card.MouseLeftButtonUp += (_, _) => LaunchRequested?.Invoke(slot);
                SetColumn(card, i % columns);
                SetRow(card, i / columns);
                _tiles.Children.Add(card);
            }
        }
        _idle.Stop();
        _idle.Start();
    }

    public void Close() => _idle.Stop();

    private static Border Tile(ShortcutView shortcut, double size)
    {
        double icon = size * 0.54, caption = Math.Clamp(size * 0.12, 15, 24);
        var stack = new StackPanel { VerticalAlignment = System.Windows.VerticalAlignment.Center };
        if (shortcut.Icon is { } source)
        {
            // Larger, with a soft shadow; a solid icon is rounded with a slight bevel, as on theme tiles.
            double radius = icon * 0.22;
            var image = new Image { Source = source, Width = icon, Height = icon, Stretch = Stretch.Uniform };
            RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
            var box = new Grid { Width = icon, Height = icon, HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = icon * 0.14, ShadowDepth = icon * 0.035, Direction = 270, Opacity = 0.4, Color = Colors.Black } };
            box.Children.Add(image);
            if (shortcut.IconSolid)
            {
                image.Clip = new RectangleGeometry(new Rect(0, 0, icon, icon), radius, radius);
                var bevel = new LinearGradientBrush { StartPoint = new System.Windows.Point(0, 0), EndPoint = new System.Windows.Point(0, 1) };
                bevel.GradientStops.Add(new GradientStop(Color.FromArgb(0x73, 0xFF, 0xFF, 0xFF), 0));
                bevel.GradientStops.Add(new GradientStop(Color.FromArgb(0x1A, 0xFF, 0xFF, 0xFF), 0.35));
                bevel.GradientStops.Add(new GradientStop(Color.FromArgb(0x10, 0x00, 0x00, 0x00), 0.65));
                bevel.GradientStops.Add(new GradientStop(Color.FromArgb(0x59, 0x00, 0x00, 0x00), 1));
                box.Children.Add(new Border { CornerRadius = new CornerRadius(radius), BorderBrush = bevel, BorderThickness = new Thickness(1.5) });
            }
            stack.Children.Add(box);
        }
        else
            stack.Children.Add(new TextBlock { Text = shortcut.Glyph, FontFamily = Icons, FontSize = icon * 0.62, VerticalAlignment = System.Windows.VerticalAlignment.Center, Padding = new Thickness(0, icon * 0.16, 0, 0), Foreground = Accent, Height = icon,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center, TextAlignment = TextAlignment.Center });
        stack.Children.Add(new TextBlock
        {
            Text = shortcut.Name, FontSize = caption, Foreground = OneUi.Text, FontWeight = FontWeights.SemiBold, FontFamily = OneUi.Font,
            TextTrimming = TextTrimming.CharacterEllipsis, TextAlignment = TextAlignment.Center, Margin = new Thickness(6, size * 0.06, 6, 0)
        });
        return new Border
        {
            Background = OneUi.Glass, BorderBrush = OneUi.GlassEdge, BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(Math.Min(40, size * 0.2)), Margin = new Thickness(Math.Max(5, size * 0.05)),
            Cursor = System.Windows.Input.Cursors.Hand, Child = stack
        };
    }

    private static SolidColorBrush Frozen(byte a, byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromArgb(a, r, g, b));
        brush.Freeze();
        return brush;
    }
}
