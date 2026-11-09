using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Pulsewave.Themes;
using Brushes = System.Windows.Media.Brushes;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Image = System.Windows.Controls.Image;
using Point = System.Windows.Point;
using Size = System.Windows.Size;
using VerticalAlignment = System.Windows.VerticalAlignment;

namespace Pulsewave;

/// <summary>
/// The HUD's media page in the One UI style: art, title and artist, a slim seek bar to tap, round
/// transport buttons (a big light play button; shuffle and repeat fill light when on), and with
/// Plexamp its own volume as a pill slider and the play queue as frosted rows (tap one to play it).
/// Laid out across a bar or down a tall screen. ✕ or 60 seconds without a tap closes it.
/// </summary>
internal sealed class MediaView : Grid
{
    private const string ShuffleGlyph = "", RepeatGlyph = "", RepeatOneGlyph = "", MusicGlyph = "", SpeakerGlyph = "";

    private readonly DispatcherTimer _idle = new() { Interval = TimeSpan.FromSeconds(60) };
    private readonly bool _wide;
    private readonly int _maxQueue;
    private readonly Border _art = new() { CornerRadius = new CornerRadius(28), Background = OneUi.Track, ClipToBounds = true };
    private readonly TextBlock _title, _artist, _elapsed, _remaining, _note, _status;
    private readonly SeekBar _seek = new() { Height = 56, Background = Brushes.Transparent, Cursor = System.Windows.Input.Cursors.Hand };
    private double? _seeking; // where a dragging finger holds the seek bar
    private readonly Border _fill = new() { Background = OneUi.OnFill, CornerRadius = new CornerRadius(5), HorizontalAlignment = HorizontalAlignment.Left, Width = 0 };
    private readonly Border _shuffle, _repeat, _playPause;
    private readonly PillSlider _volume;
    private readonly StackPanel _queue = new();
    private readonly FrameworkElement _volumeArea;
    private PlexampTimeline? _timeline;
    private string _queueKey = "\0";

    public event Action? CloseRequested;
    public event Action<MediaCommand>? CommandRequested;
    /// <summary>A Plexamp control and its value (seek position in ms, 0/1 shuffle, 0–2 repeat, volume, or queue item id).</summary>
    public event Action<PlexampControl, long>? ControlRequested;
    private Func<System.Windows.Input.MouseEventArgs, UIElement, Point> _positionOf = (e, element) => e.GetPosition(element);
    /// <summary>Where a tap landed on an element (touch is replayed at the finger, not the mouse cursor).</summary>
    public Func<System.Windows.Input.MouseEventArgs, UIElement, Point> PositionOf
    {
        get => _positionOf;
        set { _positionOf = value; _volume.PositionOf = value; }
    }

    public MediaView(Size size)
    {
        Width = size.Width;
        Height = size.Height;
        Background = OneUi.Background();
        _wide = size.Width >= size.Height;
        _maxQueue = 30; // Plexamp queues at most 30 tracks ahead; the list scrolls
        _idle.Tick += (_, _) => { _idle.Stop(); CloseRequested?.Invoke(); };
        MouseLeftButtonUp += (_, e) => { e.Handled = true; if (_idle.IsEnabled) { _idle.Stop(); _idle.Start(); } }; // not after ✕ closed it

        _title = OneUi.Label("", _wide ? 30 : 34, bold: true);
        _artist = OneUi.Label("", _wide ? 21 : 24, OneUi.Secondary);
        _elapsed = OneUi.Label("", 17, OneUi.Secondary);
        _remaining = OneUi.Label("", 17, OneUi.Secondary);
        _note = OneUi.Label("", 19, OneUi.Secondary);
        _note.TextWrapping = TextWrapping.Wrap;
        _status = OneUi.Label("", 18, OneUi.Secondary);

        // Sized to the room the card leaves (the page's logical width shrinks with Windows display scaling).
        double room = _wide ? (size.Width - 40 - 120) * 1.3 / 2.3 - 16 - 36 : size.Width - 2 * (20 + 8 + 24);
        double scale = Math.Min(1, room / ((_wide ? 62 * 2 + 74 * 2 + 92 : 76 * 2 + 90 * 2 + 112) + 4 * 12));
        double small = (_wide ? 62 : 76) * scale, middle = (_wide ? 74 : 90) * scale, big = (_wide ? 92 : 112) * scale;
        _shuffle = OneUi.Circle(ShuffleGlyph, small, () => Control(PlexampControl.Shuffle, _timeline?.Shuffle == true ? 0 : 1));
        _repeat = OneUi.Circle(RepeatGlyph, small, () => Control(PlexampControl.Repeat, ((_timeline?.Repeat ?? 0) + 1) % 3));
        _playPause = OneUi.Circle(ThemeData.PlayGlyph, big, () => CommandRequested?.Invoke(MediaCommand.PlayPause), on: true);
        var previous = OneUi.Circle(ThemeData.PreviousGlyph, middle, () => CommandRequested?.Invoke(MediaCommand.Previous));
        var next = OneUi.Circle(ThemeData.NextGlyph, middle, () => CommandRequested?.Invoke(MediaCommand.Next));
        var transport = new Grid { Margin = new Thickness(0, _wide ? 2 : 12, 0, 0) };
        var buttons = new[] { _shuffle, previous, _playPause, next, _repeat };
        // Each button gets a column of its own size, with even flexible gaps, so none is ever cut by its neighbour's share.
        for (int i = 0; i < buttons.Length; i++)
        {
            transport.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            transport.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            SetColumn(buttons[i], 2 * i + 1);
            transport.Children.Add(buttons[i]);
        }
        transport.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // Seek: a slim bar in a tall tap area; tap where to jump.
        var bar = new Border { Background = OneUi.Track, CornerRadius = new CornerRadius(5), Height = 10, VerticalAlignment = VerticalAlignment.Center, Child = _fill };
        _seek.Child = bar;
        _seek.MouseLeftButtonUp += (_, e) =>
        {
            if (_timeline is not { DurationMs: > 0 } t || !t.Can("seekTo") || _seek.ActualWidth <= 0) return;
            double x = Math.Clamp(PositionOf(e, _seek).X / _seek.ActualWidth, 0, 1);
            ControlRequested?.Invoke(PlexampControl.Seek, (long)(t.DurationMs * x));
        };
        bar.SizeChanged += (_, _) => ShowProgress();
        // A drag moves the bar with the finger and seeks once, where it lifts.
        _seek.Dragged = (fraction, final) =>
        {
            if (_timeline is not { DurationMs: > 0 } t || !t.Can("seekTo")) return;
            _seeking = final ? null : fraction;
            _elapsed.Text = Clock((long)(t.DurationMs * fraction));
            ShowProgress(fraction);
            if (final) ControlRequested?.Invoke(PlexampControl.Seek, (long)(t.DurationMs * fraction));
        };
        var times = new DockPanel { Margin = new Thickness(2, -8, 2, 0) };
        DockPanel.SetDock(_remaining, Dock.Right);
        times.Children.Add(_remaining);
        times.Children.Add(_elapsed);

        // Plexamp's own volume as a pill slider.
        _volume = new PillSlider(vertical: _wide, _wide ? 96 : 84, "slider:volume");
        _volume.SetIcon(OneUi.Glyph(SpeakerGlyph, _wide ? 32 : 28, OneUi.OnText));
        _volume.LevelChosen += level => Control(PlexampControl.Volume, (long)level);

        var info = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(_wide ? 22 : 0, _wide ? 0 : 18, 0, 0) };
        info.Children.Add(_title);
        info.Children.Add(_artist);
        _art.Width = _art.Height = _wide ? 148 : Math.Min(440, room);
        var heading = new DockPanel();
        if (_wide) { DockPanel.SetDock(_art, Dock.Left); heading.Children.Add(_art); heading.Children.Add(info); }
        else
        {
            var stack = new StackPanel();
            _art.HorizontalAlignment = HorizontalAlignment.Center;
            stack.Children.Add(_art); stack.Children.Add(info);
            heading.Children.Add(stack);
        }
        var player = new StackPanel();
        foreach (var part in new FrameworkElement[] { heading, _seek, times, transport }) player.Children.Add(part);
        _seek.Margin = new Thickness(0, _wide ? 4 : 16, 0, 0);

        var queueCard = new DockPanel();
        var queueTitle = OneUi.Label("Up next", 22, bold: true);
        queueTitle.Margin = new Thickness(6, 0, 0, 10);
        DockPanel.SetDock(queueTitle, Dock.Top);
        queueCard.Children.Add(queueTitle);
        DockPanel.SetDock(_note, Dock.Top);
        queueCard.Children.Add(_note);
        // Scrolls with a vertical swipe (see MainWindow.OnTouch) or the mouse wheel; no scroll bar.
        queueCard.Children.Add(new ScrollViewer { Content = _queue, VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Focusable = false, PanningMode = PanningMode.None });

        var layout = new DockPanel { Margin = new Thickness(20, 14, 20, 12) };
        var header = new DockPanel { Margin = new Thickness(8, 0, 0, 4) };
        var close = OneUi.Circle("", 60, () => CloseRequested?.Invoke());
        DockPanel.SetDock(close, Dock.Right);
        header.Children.Add(close);
        var title = OneUi.Label("Now playing", 32, bold: true);
        title.VerticalAlignment = VerticalAlignment.Center;
        DockPanel.SetDock(title, Dock.Left);
        header.Children.Add(title);
        _status.Margin = new Thickness(24, 0, 12, 0);
        _status.VerticalAlignment = VerticalAlignment.Center;
        header.Children.Add(_status);
        DockPanel.SetDock(header, Dock.Top);
        layout.Children.Add(header);

        var body = new Grid();
        var left = OneUi.Card(player, radius: 40, padding: _wide ? 18 : 24);
        var right = OneUi.Card(queueCard, radius: 40, padding: 18);
        if (_wide)
        {
            var volumeColumn = new Grid { Margin = new Thickness(8) };
            _volume.HorizontalAlignment = HorizontalAlignment.Center;
            volumeColumn.Children.Add(_volume);
            _volumeArea = volumeColumn;
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.3, GridUnitType.Star) });
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            SetColumn(volumeColumn, 1);
            SetColumn(right, 2);
            body.Children.Add(volumeColumn);
        }
        else
        {
            _volume.Margin = new Thickness(0, 22, 0, 0);
            player.Children.Add(_volume);
            _volumeArea = _volume;
            body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            SetRow(right, 1);
        }
        body.Children.Add(left);
        body.Children.Add(right);
        layout.Children.Add(body);
        Children.Add(layout);
    }

    public void Open() { _idle.Stop(); _idle.Start(); }

    public void Close() => _idle.Stop();

    public void ShowStatus(string text) => _status.Text = text;

    /// <summary>Shows what is playing; <paramref name="timeline"/> is Plexamp's when it is the player, else null.</summary>
    public void Show(NowPlaying playing, PlexampTimeline? timeline)
    {
        _timeline = timeline;
        bool nothing = playing.State == MediaPlayback.None;
        _title.Text = nothing ? "Nothing playing" : playing.Title;
        _artist.Text = nothing ? "" : playing.Artist;
        _art.Child = playing.Art is { } art ? new Image { Source = art, Stretch = Stretch.UniformToFill } : OneUi.Glyph(MusicGlyph, _wide ? 54 : 120, OneUi.Secondary);
        OneUi.SetGlyph(_playPause, playing.State == MediaPlayback.Playing ? ThemeData.PauseGlyph : ThemeData.PlayGlyph);
        bool plex = timeline != null;
        _volumeArea.Visibility = plex && timeline!.Can("volume") ? Visibility.Visible : Visibility.Hidden;
        _shuffle.Visibility = plex && timeline!.Can("shuffle") ? Visibility.Visible : Visibility.Hidden;
        _repeat.Visibility = plex && timeline!.Can("repeat") ? Visibility.Visible : Visibility.Hidden;
        OneUi.SetOn(_shuffle, timeline?.Shuffle == true);
        OneUi.SetOn(_repeat, timeline?.Repeat is 1 or 2);
        OneUi.SetGlyph(_repeat, timeline?.Repeat == 1 ? RepeatOneGlyph : RepeatGlyph);
        if (timeline?.Volume is { } v) _volume.Show(v); // not while the finger is on it: Plexamp reports a beat behind
        _seek.Visibility = plex && timeline!.DurationMs > 0 ? Visibility.Visible : Visibility.Hidden;
        _elapsed.Text = plex ? Clock(_seeking is { } held ? (long)(timeline!.DurationMs * held) : timeline!.TimeMs) : "";
        _remaining.Text = plex && timeline!.DurationMs > 0 ? "−" + Clock(Math.Max(0, timeline.DurationMs - timeline.TimeMs)) : "";
        ShowProgress();
        if (!plex) ShowQueue(null, nothing ? "Nothing playing." : "Queue, seeking, shuffle, repeat and volume work with Plexamp.");
    }

    /// <summary>The play queue: tracks after the current one. Rebuilt only when it changes, so a tap never lands on a moving row.</summary>
    public void ShowQueue(IReadOnlyList<PlexampQueueItem>? queue, string? note = null)
    {
        int current = queue?.ToList().FindIndex(q => q.Current) ?? -1;
        var upNext = queue == null ? [] : queue.Skip(current + 1).Take(_maxQueue).ToList();
        string key = note + "|" + string.Join(",", upNext.Select(q => q.ItemId));
        if (key == _queueKey) return;
        _queueKey = key;
        _note.Text = note ?? (queue == null ? "Plexamp's queue couldn't be read (check the Plex token in the tray)." : upNext.Count == 0 ? "Nothing after this track." : "");
        _note.Visibility = _note.Text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        _queue.Children.Clear();
        foreach (var item in upNext)
        {
            var row = new DockPanel();
            var length = OneUi.Label(Clock(item.DurationMs), 17, OneUi.Secondary);
            length.VerticalAlignment = VerticalAlignment.Center;
            DockPanel.SetDock(length, Dock.Right);
            row.Children.Add(length);
            var play = OneUi.Glyph(ThemeData.PlayGlyph, 18, OneUi.Secondary);
            play.Margin = new Thickness(0, 0, 14, 0);
            DockPanel.SetDock(play, Dock.Left);
            row.Children.Add(play);
            var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            text.Children.Add(OneUi.Label(item.Title, 19, bold: true));
            text.Children.Add(OneUi.Label(item.Artist, 15, OneUi.Secondary));
            row.Children.Add(text);
            var tile = new Border { Background = OneUi.Glass, CornerRadius = new CornerRadius(30), Margin = new Thickness(0, 0, 0, 8), Padding = new Thickness(20, 6, 22, 6),
                MinHeight = 60, Cursor = System.Windows.Input.Cursors.Hand, Child = row, Tag = item.ItemId };
            tile.MouseLeftButtonUp += (_, _) => { if (long.TryParse(item.ItemId, out long id)) Control(PlexampControl.SkipTo, id); };
            _queue.Children.Add(tile);
        }
    }

    private void Control(PlexampControl control, long value)
    {
        if (_timeline == null) return;
        ControlRequested?.Invoke(control, value);
    }

    private void ShowProgress(double? at = null)
    {
        if (_fill.Parent is not FrameworkElement bar || bar.ActualWidth <= 0) return;
        double fraction = at ?? _seeking ?? (_timeline is { DurationMs: > 0 } t ? Math.Clamp((double)t.TimeMs / t.DurationMs, 0, 1) : 0);
        _fill.Width = bar.ActualWidth * fraction;
    }

    /// <summary>The seek bar's tap area, which a finger can also drag.</summary>
    private sealed class SeekBar : Border, ITouchDrag
    {
        public Action<double, bool>? Dragged { get; set; }
        public bool Vertical => false;
        public void DragTo(Point at, bool final) { if (ActualWidth > 0) Dragged?.Invoke(Math.Clamp(at.X / ActualWidth, 0, 1), final); }
    }

    internal static string Clock(long ms)
    {
        var t = TimeSpan.FromMilliseconds(Math.Max(0, ms));
        return t.TotalHours >= 1 ? t.ToString(@"h\:mm\:ss", CultureInfo.InvariantCulture) : t.ToString(@"m\:ss", CultureInfo.InvariantCulture);
    }
}
