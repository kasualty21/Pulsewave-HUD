using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using Pulsewave.Themes;
using FontFamily = System.Windows.Media.FontFamily;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Image = System.Windows.Controls.Image;
using Size = System.Windows.Size;
using VerticalAlignment = System.Windows.VerticalAlignment;

namespace Pulsewave;

/// <summary>
/// The HUD's Gaming page: the game being played (or the last one) with its cover, the session's length,
/// average and 1% low FPS and peak temperatures; play time today and over the last 7 days with a bar
/// per game; and recent sessions. ✕ or 60 seconds without a tap closes it.
/// </summary>
internal sealed class GamingView : Grid
{
    private readonly DispatcherTimer _idle = new() { Interval = TimeSpan.FromSeconds(60) };
    private readonly bool _wide;
    private readonly Border _cover = new() { CornerRadius = new CornerRadius(24), Background = OneUi.Track, ClipToBounds = true };
    private readonly TextBlock _state, _name, _session, _today, _week;
    private readonly (TextBlock Value, TextBlock Label)[] _stats;
    private readonly StackPanel _games = new(), _recent = new();
    private string _gamesKey = "\0", _recentKey = "\0", _coverKey = "\0";

    public event Action? CloseRequested;

    public GamingView(Size size)
    {
        Width = size.Width;
        Height = size.Height;
        Background = OneUi.Background();
        _wide = size.Width >= size.Height;
        _idle.Tick += (_, _) => { _idle.Stop(); CloseRequested?.Invoke(); };
        MouseLeftButtonUp += (_, e) => { e.Handled = true; if (_idle.IsEnabled) { _idle.Stop(); _idle.Start(); } }; // not after ✕ closed it

        _state = OneUi.Label("", 15, OneUi.Secondary, bold: true);
        _name = OneUi.Label("", _wide ? 28 : 30, bold: true);
        _session = OneUi.Label("", _wide ? 64 : 72);
        _session.FontFamily = new FontFamily("Segoe UI Light");
        _today = OneUi.Label("", 40);
        _week = OneUi.Label("", 40);
        _today.FontFamily = _week.FontFamily = new FontFamily("Segoe UI Light");
        _stats = new[] { "AVG FPS", "1% LOW", "PEAK CPU", "PEAK GPU" }.Select(l => (OneUi.Label("--", 26, bold: true), OneUi.Label(l, 12, OneUi.Secondary, bold: true))).ToArray();

        // Session: cover beside (bar) or above (tall) the name, timer and stats.
        _cover.Width = _wide ? 200 : 260;
        _cover.Height = _cover.Width * 1.5;
        var statRow = new UniformGrid { Columns = 4, Margin = new Thickness(0, 14, 0, 0) };
        foreach (var (value, label) in _stats)
        {
            var cell = new StackPanel();
            cell.Children.Add(value);
            cell.Children.Add(label);
            statRow.Children.Add(cell);
        }
        var details = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(_wide ? 24 : 0, _wide ? 0 : 18, 0, 0) };
        foreach (var part in new UIElement[] { _state, _name, _session, statRow }) details.Children.Add(part);
        var session = new DockPanel();
        if (_wide) { DockPanel.SetDock(_cover, Dock.Left); session.Children.Add(_cover); session.Children.Add(details); }
        else
        {
            var stack = new StackPanel();
            _cover.HorizontalAlignment = HorizontalAlignment.Center;
            stack.Children.Add(_cover);
            stack.Children.Add(details);
            session.Children.Add(stack);
        }

        // Play time: today and 7 days, then a bar per game.
        var totals = new UniformGrid { Columns = 2 };
        foreach (var (value, label) in new[] { (_today, "TODAY"), (_week, "LAST 7 DAYS") })
        {
            var cell = new StackPanel();
            cell.Children.Add(OneUi.Label(label, 13, OneUi.Secondary, bold: true));
            cell.Children.Add(value);
            totals.Children.Add(cell);
        }
        var time = new DockPanel();
        DockPanel.SetDock(totals, Dock.Top);
        time.Children.Add(totals);
        _games.Margin = new Thickness(0, 16, 0, 0);
        time.Children.Add(_games);

        // Recent sessions: a list that scrolls with a drag (or the mouse wheel).
        var recent = new DockPanel();
        var recentTitle = OneUi.Label("Recent sessions", 20, bold: true);
        recentTitle.Margin = new Thickness(4, 0, 0, 10);
        DockPanel.SetDock(recentTitle, Dock.Top);
        recent.Children.Add(recentTitle);
        recent.Children.Add(new ScrollViewer { Content = _recent, VerticalScrollBarVisibility = ScrollBarVisibility.Hidden, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Focusable = false, PanningMode = PanningMode.None });

        var layout = new DockPanel { Margin = new Thickness(20, 14, 20, 12) };
        var header = new DockPanel { Margin = new Thickness(8, 0, 0, 4) };
        var close = OneUi.Circle("", 60, () => CloseRequested?.Invoke());
        DockPanel.SetDock(close, Dock.Right);
        header.Children.Add(close);
        var title = OneUi.Label("Gaming", 32, bold: true);
        title.VerticalAlignment = VerticalAlignment.Center;
        header.Children.Add(title);
        DockPanel.SetDock(header, Dock.Top);
        layout.Children.Add(header);

        var body = new Grid();
        var cards = new[] { OneUi.Card(session, radius: 40, padding: 20), OneUi.Card(time, radius: 40, padding: 20), OneUi.Card(recent, radius: 40, padding: 18) };
        if (_wide)
        {
            foreach (var share in new[] { 1.45, 1, 1 }) body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(share, GridUnitType.Star) });
            for (int i = 0; i < 3; i++) { SetColumn(cards[i], i); body.Children.Add(cards[i]); }
        }
        else
        {
            body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            for (int i = 0; i < 3; i++) { SetRow(cards[i], i); body.Children.Add(cards[i]); }
        }
        layout.Children.Add(body);
        Children.Add(layout);
    }

    public void Open() { _idle.Stop(); _idle.Start(); }

    public void Close() => _idle.Stop();

    /// <summary>Shows a snapshot; lists are rebuilt only when they change, so a tap never lands on a moving row.</summary>
    public void Show(GamingSnapshot g)
    {
        _state.Text = g.Game == null ? "NO GAME YET" : g.Playing ? "PLAYING" : "LAST PLAYED";
        _state.Foreground = g.Playing ? OneUi.Frozen(0xFF, 0x7F, 0xD8, 0xFF) : OneUi.Secondary;
        _name.Text = g.Game ?? "Play a game from your Steam library or shortcuts";
        _session.Text = g.Session is { } s ? TouchTimers.Format(s) : g.Recent.FirstOrDefault() is { } last ? ThemeData.PlayTime(last.Length) : "--";
        string unit = "°C";
        string[] values = [Num(g.AvgFps), Num(g.LowFps), g.PeakCpuC is { } c ? $"{c:0}{unit}" : "--", g.PeakGpuC is { } gp ? $"{gp:0}{unit}" : "--"];
        for (int i = 0; i < _stats.Length; i++) _stats[i].Value.Text = values[i];
        _today.Text = ThemeData.PlayTime(g.Today);
        _week.Text = ThemeData.PlayTime(g.Week);

        string coverKey = g.Tile?.Name ?? "";
        if (coverKey != _coverKey)
        {
            _coverKey = coverKey;
            var picture = g.Tile?.Cover ?? g.Tile?.Icon;
            _cover.Child = picture != null
                ? new Image { Source = picture, Stretch = g.Tile?.Cover != null ? Stretch.UniformToFill : Stretch.Uniform, Margin = new Thickness(g.Tile?.Cover != null ? 0 : 30) }
                : OneUi.Glyph("", 64, OneUi.Secondary);
        }

        var top = g.WeekByGame.Take(_wide ? 4 : 6).ToList();
        string gamesKey = string.Join("|", top.Select(t => t.Name + (int)t.Time.TotalMinutes));
        if (gamesKey != _gamesKey)
        {
            _gamesKey = gamesKey;
            _games.Children.Clear();
            double most = top.Count > 0 ? Math.Max(1, top.Max(t => t.Time.TotalMinutes)) : 1;
            foreach (var (name, played) in top)
            {
                var row = new DockPanel { Margin = new Thickness(0, 0, 0, 10) };
                var amount = OneUi.Label(ThemeData.PlayTime(played), 15, OneUi.Secondary, bold: true);
                DockPanel.SetDock(amount, Dock.Right);
                row.Children.Add(amount);
                row.Children.Add(OneUi.Label(name, 16, bold: true));
                var track = new Border { Background = OneUi.Track, CornerRadius = new CornerRadius(5), Height = 10, Margin = new Thickness(0, 0, 0, 12) };
                var fill = new Border { Background = OneUi.SliderFillAcross, CornerRadius = new CornerRadius(5), HorizontalAlignment = HorizontalAlignment.Left };
                double share = played.TotalMinutes / most;
                track.SizeChanged += (_, _) => fill.Width = Math.Max(10, track.ActualWidth * share);
                track.Child = fill;
                _games.Children.Add(row);
                _games.Children.Add(track);
            }
            if (top.Count == 0) _games.Children.Add(OneUi.Label("No play time in the last 7 days.", 16, OneUi.Secondary));
        }

        string recentKey = string.Join("|", g.Recent.Select(r => r.End.Ticks));
        if (recentKey != _recentKey)
        {
            _recentKey = recentKey;
            _recent.Children.Clear();
            foreach (var r in g.Recent)
            {
                var row = new DockPanel();
                var right = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                var length = OneUi.Label(ThemeData.PlayTime(r.Length), 16, bold: true);
                length.HorizontalAlignment = HorizontalAlignment.Right;
                right.Children.Add(length);
                var fps = OneUi.Label(r.AvgFps is { } a ? $"{a:0} FPS" : "", 13, OneUi.Secondary);
                fps.HorizontalAlignment = HorizontalAlignment.Right;
                right.Children.Add(fps);
                DockPanel.SetDock(right, Dock.Right);
                row.Children.Add(right);
                var left = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                left.Children.Add(OneUi.Label(r.Name, 17, bold: true));
                left.Children.Add(OneUi.Label(r.Start.ToString("ddd d MMM · HH:mm", CultureInfo.CurrentCulture), 13, OneUi.Secondary));
                row.Children.Add(left);
                _recent.Children.Add(new Border { Background = OneUi.Glass, CornerRadius = new CornerRadius(26), Margin = new Thickness(0, 0, 0, 8),
                    Padding = new Thickness(20, 8, 20, 8), MinHeight = 56, Child = row });
            }
            if (g.Recent.Count == 0) _recent.Children.Add(OneUi.Label("Sessions of a minute or more show here.", 16, OneUi.Secondary));
        }
    }

    private static string Num(double? v) => v is { } x ? x.ToString("0", CultureInfo.CurrentCulture) : "--";
}
