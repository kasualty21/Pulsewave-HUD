using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Size = System.Windows.Size;
using Point = System.Windows.Point;

namespace Pulsewave.Regression;

// Per-app volume: naming and merging sessions, and the HUD's volume page at both screen shapes
// (an off-screen render with made-up apps; real levels are only read by --app-volume-probe).
internal static class MixerChecks
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("VOLUME: " + message);
        Console.WriteLine("PASS VOLUME: " + message);
    }

    public static void Run(string outputRoot, string? shots = null)
    {
        var system = AppVolume.Identify(true, "@%SystemRoot%\\System32\\AudioSrv.Dll,-202", null, null, null);
        var spotify = AppVolume.Identify(false, "", @"C:\Apps\Spotify.exe", "Spotify", "Spotify");
        var resource = AppVolume.Identify(false, "@%SystemRoot%\\foo.dll,-1", @"C:\Games\game.exe", "game", null);
        var named = AppVolume.Identify(false, "Discord voice", @"C:\Apps\Discord.exe", "Discord", "Discord");
        Check(system == (AppVolume.SystemKey, "System sounds", null) && spotify == (@"C:\Apps\Spotify.exe", "Spotify", @"C:\Apps\Spotify.exe") &&
              resource.Name == "Game" && named.Name == "Discord voice",
            "Apps are named by the session, the program's description, or its process name; system sounds are one entry");

        var merged = AppVolume.Group([
            new AppAudio("chrome", "Chrome", null, 40, true, false), new AppAudio("chrome", "Chrome", null, 70, false, true),
            new AppAudio(AppVolume.SystemKey, "System sounds", null, 100, false, false), new AppAudio("zoom", "Zoom", null, 50, true, false),
            new AppAudio("arc", "Arc", null, 80, false, false)]);
        Check(merged.Select(a => a.Key).SequenceEqual(["chrome", "arc", "zoom", AppVolume.SystemKey]) && merged[0] is { Level: 70, Muted: false, Playing: true } &&
              merged.Single(a => a.Key == "zoom").Muted,
            "An app's sessions show once (loudest level, muted only if all are); playing apps come first and system sounds last");

        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var apps = new List<AppAudio>
                {
                    new("game", "Cyberpunk 2077", null, 80, false, true), new("spotify", "Spotify", null, 45, false, true),
                    new("discord", "Discord", null, 100, true, false), new(AppVolume.SystemKey, "System sounds", null, 60, false, false)
                };
                foreach (var (w, h) in new[] { (1920, 480), (480, 1802) })
                {
                    var view = new MixerView(new Size(w, h));
                    view.Open();
                    view.Show((35, false), apps);
                    view.Measure(new Size(w, h)); view.Arrange(new Rect(0, 0, w, h)); view.UpdateLayout();
                    view.Show((35, false), apps); // the bars use the laid-out widths
                    var controls = Visuals<FrameworkElement>(view).Where(b => b.Cursor == System.Windows.Input.Cursors.Hand)
                        .Select(b => (b, Bounds: b.TransformToAncestor(view).TransformBounds(new Rect(0, 0, b.ActualWidth, b.ActualHeight)))).ToList();
                    var sliders = controls.Where(c => c.b is PillSlider).ToList();
                    bool fits = controls.Count == 1 + 5 * 2 && sliders.Count == 5 &&
                                controls.All(t => t.Bounds.Right <= w + 0.5 && t.Bounds.Bottom <= h + 0.5 && t.Bounds.Height >= 56 && t.Bounds.Width >= 56) &&
                                sliders.All(s => (w > h ? s.Bounds.Height : s.Bounds.Width) >= 200);
                    string? changed = null; bool? muted = null;
                    view.LevelRequested += (key, level) => changed = $"{key}={level:0}";
                    view.MuteRequested += (key, mute) => muted = mute;
                    // A tap three-quarters of the way along the second slider (the game) sets about 75.
                    var game = (PillSlider)sliders[1].b;
                    view.PositionOf = (_, element) => w > h
                        ? new Point(10, element.RenderSize.Height - (52 + (element.RenderSize.Height - 104) * 0.75))
                        : new Point(44 + (element.RenderSize.Width - 88) * 0.75, 10);
                    Tap(game);
                    string? tapped = changed;
                    // A finger dragging the slider: the level follows it and is sent when it lifts.
                    var size = game.RenderSize;
                    game.DragTo(w > h ? new Point(10, size.Height - (52 + (size.Height - 104) * 0.3)) : new Point(44 + (size.Width - 88) * 0.3, 10), final: true);
                    bool dragged = changed == "game=30" && game.Level == 30;
                    changed = tapped;
                    Tap(Borders(view).First(b => b.Tag as string == "mute"));
                    changed = dragged ? changed : "drag failed";
                    if (shots != null)
                    {
                        var bitmap = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
                        bitmap.Render(view);
                        var png = new PngBitmapEncoder();
                        png.Frames.Add(BitmapFrame.Create(bitmap));
                        Directory.CreateDirectory(shots);
                        using var output = File.Create(Path.Combine(shots, $"volume-{w}x{h}.png"));
                        png.Save(output);
                    }
                    view.Close();
                    Check(fits && changed == "game=75" && muted == true,
                        $"The {w}x{h} volume page shows all sound and each app as a One UI pill slider (tap or drag to set) with a round mute button" + (fits && changed == "game=75" && muted == true ? "" : $" [fits {fits}, {controls.Count} controls, {sliders.Count} sliders, changed {changed}, muted {muted}]"));
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

    private static IEnumerable<T> Visuals<T>(DependencyObject node) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
        {
            var child = VisualTreeHelper.GetChild(node, i);
            if (child is T t) yield return t;
            foreach (var inner in Visuals<T>(child)) yield return inner;
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
