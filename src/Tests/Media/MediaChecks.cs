using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Point = System.Windows.Point;
using Size = System.Windows.Size;

namespace Pulsewave.Regression;

// Deeper Plexamp control: the timeline's position, volume, shuffle, repeat and queue fields, the
// play queue answer, the local control paths, and the HUD media page (off-screen, sample data).
// The live player is only touched by --plexamp-live.
internal static class MediaChecks
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("MEDIA: " + message);
        Console.WriteLine("PASS MEDIA: " + message);
    }

    private const string Timeline = """
        <MediaContainer commandID="1"><Timeline state="playing" duration="197546" time="30215" playQueueItemID="28734" key="/library/metadata/3238"
          playQueueID="94" type="music" volume="3" shuffle="0" repeat="2" controllable="volume,repeat,skipPrevious,seekTo,playPause,shuffle,skipNext"
          protocol="https" address="192-168-0-101.abc.plex.direct" port="32400"/><Timeline type="video" state="stopped"/></MediaContainer>
        """;

    private const string Queue = """
        <MediaContainer playQueueID="94" playQueueSelectedItemID="28734">
          <Track playQueueItemID="28733" title="Before" grandparentTitle="Band" duration="143000"/>
          <Track playQueueItemID="28734" title="Now" grandparentTitle="Band" duration="197546"/>
          <Track playQueueItemID="28735" title="Next one" grandparentTitle="Album Artist" originalTitle="Guest" duration="188000"/>
          <Track playQueueItemID="28736" title="After that" grandparentTitle="Band" duration="201000"/>
        </MediaContainer>
        """;

    public static void Run(string outputRoot, string? shots = null)
    {
        var t = PlexampReader.ParseTimeline(Timeline)!;
        Check(t is { TimeMs: 30215, DurationMs: 197546, Volume: 3, Shuffle: false, Repeat: 2, PlayQueueId: "94", PlayQueueItemId: "28734" } &&
              t.Can("seekTo") && t.Can("volume") && !t.Can("stepBack") &&
              PlexampReader.ParseTimeline(Timeline.Replace(" volume=\"3\" shuffle=\"0\" repeat=\"2\"", ""))! is { Volume: null, Shuffle: null, Repeat: null },
            "Plexamp's timeline gives position, length, its volume, shuffle, repeat, the queue and what it can be told");
        var at = new Pulsewave.Themes.ThemeDataContext { Now = new DateTime(2026, 11, 9, 7, 5, 0), MediaPosition = (TimeSpan.FromSeconds(161), TimeSpan.FromSeconds(243)) };
        string Show(string name, string? format = null) => Pulsewave.Themes.ThemeData.Display(Pulsewave.Themes.ThemeData.Read(name, format, false, at), true);
        var none = new Pulsewave.Themes.ThemeDataContext();
        Check(Show("MEDIA_PROGRESS") == "66%" && Show("MEDIA_ELAPSED") == "2:41" && Show("MEDIA_REMAINING") == "−1:22" &&
              Pulsewave.Themes.ThemeData.Read("MEDIA_PROGRESS", null, false, none).Number == null && Pulsewave.Themes.ThemeData.Read("MEDIA_ELAPSED", null, false, none).Text == "" &&
              Show("TIME", "h") == "07" && Show("TIME", "m") == "05" && Show("TIME", "h:m") == "07:05",
            "Themes can show track progress (for a ring), time played and left, and the clock's hours and minutes apart");
        var queue = PlexampReader.ParseQueue(Queue, "28734");
        Check(queue.Count == 4 && queue[1].Current && queue[2] is { Title: "Next one", Artist: "Guest", DurationMs: 188000 } &&
              PlexampReader.ParseQueue(Queue, "").Count(q => q.Current) == 1,
            "The play queue lists tracks with the current one marked (the timeline's item, or the server's selected one)");
        Check(PlexampReader.ControlPath(PlexampControl.Seek, 45000) == "/player/playback/seekTo?type=music&offset=45000" &&
              PlexampReader.ControlPath(PlexampControl.Shuffle, 1) == "/player/playback/setParameters?type=music&shuffle=1" &&
              PlexampReader.ControlPath(PlexampControl.Repeat, 7) == "/player/playback/setParameters?type=music&repeat=2" &&
              PlexampReader.ControlPath(PlexampControl.Volume, 140) == "/player/playback/setParameters?type=music&volume=100" &&
              PlexampReader.ControlPath(PlexampControl.SkipTo, 28735) == "/player/playback/skipTo?type=music&playQueueItemID=28735" &&
              PlexampReader.ControlPath(PlexampControl.SkipTo, 0) == null && MediaView.Clock(3_725_000) == "1:02:05" && MediaView.Clock(65_000) == "1:05",
            "Controls go to Plexamp's local player paths with values kept in range");

        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var playing = new NowPlaying(MediaPlayback.Playing, "Fleeting Moment", "Hideaki Kobayashi", PlexampReader.Source, null);
                foreach (var (w, h) in new[] { (1920, 480), (480, 1802) })
                {
                    var view = new MediaView(new Size(w, h));
                    var sent = new List<(PlexampControl, long)>();
                    view.ControlRequested += (c, v) => sent.Add((c, v));
                    view.Open();
                    view.Show(playing, t);
                    view.ShowQueue(queue);
                    view.Measure(new Size(w, h)); view.Arrange(new Rect(0, 0, w, h)); view.UpdateLayout();
                    view.Show(playing, t);
                    var tiles = Borders(view).Where(b => b.Cursor != null && b.IsVisible || b.Cursor != null && b.Visibility == Visibility.Visible)
                        .Select(b => (b, Bounds: b.TransformToAncestor(view).TransformBounds(new Rect(0, 0, b.ActualWidth, b.ActualHeight)))).ToList();
                    bool fits = tiles.All(x => x.Bounds.Right <= w + 0.5 && x.Bounds.Bottom <= h + 0.5 && x.Bounds.Height >= 54) &&
                                // No two tap targets overlap (the transport buttons once did on the Y70's narrow logical width).
                                tiles.All(a => tiles.All(b => a.b == b.b || !Rect.Intersect(a.Bounds, b.Bounds).IsEmpty == false ||
                                    Rect.Intersect(a.Bounds, b.Bounds).Width < 1 || Rect.Intersect(a.Bounds, b.Bounds).Height < 1));
                    // A tap three-quarters along the seek bar seeks to three-quarters of the track; the position comes from PositionOf (touch).
                    var seek = tiles.Select(x => x.b).First(b => b.Height == 56 && b.Tag == null);
                    view.PositionOf = (_, element) => new Point(((FrameworkElement)element).ActualWidth * 0.75, 10);
                    Tap(seek);
                    var next = tiles.Select(x => x.b).First(b => b.Tag as string == "28735");
                    Tap(next);
                    // Dragging the seek bar moves it with the finger and seeks once, where it lifts.
                    var drag = (ITouchDrag)seek;
                    drag.DragTo(new Point(seek.ActualWidth * 0.2, 10), final: false);
                    int whileDragging = sent.Count;
                    drag.DragTo(new Point(seek.ActualWidth * 0.5, 10), final: true);
                    bool seekDrag = whileDragging == 2 && sent.Count == 3 && sent[2] == (PlexampControl.Seek, (long)(197546 * 0.5));
                    if (!seekDrag) sent.Add((PlexampControl.Seek, -1));
                    sent.RemoveAt(sent.Count - 1);
                    if (shots != null)
                    {
                        var bitmap = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
                        bitmap.Render(view);
                        var png = new PngBitmapEncoder();
                        png.Frames.Add(BitmapFrame.Create(bitmap));
                        Directory.CreateDirectory(shots);
                        using var output = File.Create(Path.Combine(shots, $"media-{w}x{h}.png"));
                        png.Save(output);
                    }
                    // Icons centred in their round buttons (the ✕ glyph itself sits 0.7 px off); then paused, the play
                    // triangle is nudged right by OneUi.PlayNudge and still level.
                    var offsets = IconOffsets(view);
                    view.Show(playing with { State = MediaPlayback.Paused }, t);
                    view.UpdateLayout();
                    var play = IconOffsets(view).Where(o => o.Glyph == Pulsewave.Themes.ThemeData.PlayGlyph).ToList();
                    bool centred = offsets.Count >= 6 && offsets.All(o => Math.Abs(o.Dx) <= 1 && Math.Abs(o.Dy) <= 1) &&
                                   play.Count == 1 && play[0].Dx > 0.5 && play[0].Dx < play[0].Button * 0.1 && Math.Abs(play[0].Dy) <= 1;
                    Check(centred, $"The {w}x{h} media page's icons sit centred in their buttons (largest offset " +
                                   $"{offsets.Max(o => Math.Max(Math.Abs(o.Dx), Math.Abs(o.Dy))):0.0} px; play nudged {play.FirstOrDefault().Dx:0.0} px right by eye)");
                    view.Close();
                    int queueRows = tiles.Count(x => x.b.Tag is string id && id.StartsWith("287"));
                    Check(fits && queueRows == 2 && sent.Count == 2 && sent[0] == (PlexampControl.Seek, (long)(197546 * 0.75)) && sent[1] == (PlexampControl.SkipTo, 28735L),
                        $"The {w}x{h} media page fits, seeks where the bar is tapped and plays a tapped queue track ({tiles.Count} tiles)");
                }
            }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
        if (failure != null) throw failure;
    }

    private static void Tap(UIElement element) =>
        element.RaiseEvent(new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice, 0, System.Windows.Input.MouseButton.Left)
            { RoutedEvent = UIElement.MouseLeftButtonUpEvent });

    /// <summary>
    /// For every icon in a round button (or round knob) on a page: how far its ink's centre is from the button's centre,
    /// from the font's own glyph outline, the icon's place and its transforms. Positive is right / down.
    /// </summary>
    internal static List<(string Glyph, double Button, double Dx, double Dy)> IconOffsets(FrameworkElement page)
    {
        var found = new List<(string, double, double, double)>();
        foreach (var t in Descendants(page).OfType<System.Windows.Controls.TextBlock>())
        {
            if (t.Visibility != Visibility.Visible || t.ActualWidth <= 0 || t.Text.Length != 1 || t.FontFamily.Source != OneUi.Icons.Source) continue;
            DependencyObject? up = VisualTreeHelper.GetParent(t);
            while (up != null && !(up is System.Windows.Controls.Border round && round.ActualWidth > 0 &&
                                    Math.Abs(round.ActualWidth - round.ActualHeight) < 1 && round.CornerRadius.TopLeft >= round.ActualWidth / 2 - 1))
                up = VisualTreeHelper.GetParent(up);
            if (up is not System.Windows.Controls.Border button) continue;
            var text = new FormattedText(t.Text, System.Globalization.CultureInfo.InvariantCulture, System.Windows.FlowDirection.LeftToRight,
                new Typeface(t.FontFamily, t.FontStyle, t.FontWeight, t.FontStretch), t.FontSize, System.Windows.Media.Brushes.Black, 1.0);
            var ink = text.BuildGeometry(new Point(t.Padding.Left, t.Padding.Top)).Bounds;
            var box = t.TransformToAncestor(button).TransformBounds(ink);
            found.Add((t.Text, button.ActualWidth, box.X + box.Width / 2 - button.ActualWidth / 2, box.Y + box.Height / 2 - button.ActualHeight / 2));
        }
        return found;
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject node)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
        {
            var child = VisualTreeHelper.GetChild(node, i);
            yield return child;
            foreach (var inner in Descendants(child)) yield return inner;
        }
    }

    private static IEnumerable<System.Windows.Controls.Border> Borders(DependencyObject node)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
        {
            var child = VisualTreeHelper.GetChild(node, i);
            if (child is System.Windows.Controls.Border b) yield return b;
            foreach (var inner in Borders(child)) yield return inner;
        }
    }
}
