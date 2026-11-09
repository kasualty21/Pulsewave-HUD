using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Pulsewave.Themes;
using Point = System.Windows.Point;
using Size = System.Windows.Size;

namespace Pulsewave.Regression;

// Other screen sizes, tap actions and the details page. Taps are checked by hit-testing an
// off-screen theme render; real touch input and media players are left to the running app.
internal static class TouchChecks
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("TOUCH: " + message);
        Console.WriteLine("PASS TOUCH: " + message);
    }

    private static IEnumerable<System.Windows.Forms.Control> AllControls(System.Windows.Forms.Control parent) =>
        parent.Controls.Cast<System.Windows.Forms.Control>().SelectMany(c => AllControls(c).Prepend(c));

    /// <param name="shots">When set, the details page is also saved there as PNGs at three screen shapes.</param>
    public static void Run(string outputRoot, string? shots = null)
    {
        // Shortcut icons: padding trimmed, solid icons told from shaped ones, Steam ids read for cover art.
        var padded = new byte[64 * 64 * 4];
        for (int y = 16; y < 48; y++) for (int x = 16; x < 48; x++) { int i = (y * 64 + x) * 4; padded[i] = padded[i + 1] = padded[i + 2] = padded[i + 3] = 255; }
        var paddedIcon = BitmapSource.Create(64, 64, 96, 96, PixelFormats.Pbgra32, null, padded, 64 * 4);
        var trimmed = ShortcutIcons.Trim(paddedIcon);
        var full = BitmapSource.Create(8, 8, 96, 96, PixelFormats.Bgr32, null, new byte[8 * 8 * 4], 32);
        Check(trimmed.PixelWidth is >= 32 and <= 36 && trimmed.PixelHeight == trimmed.PixelWidth && ShortcutIcons.IsSolid(trimmed) && !ShortcutIcons.IsSolid(paddedIcon) &&
              ShortcutIcons.IsSolid(full) && SteamLibrary.AppIdOf("steam://rungameid/3259780") == "3259780" && SteamLibrary.AppIdOf("steam://rungameid/") == null &&
              SteamLibrary.AppIdOf(@"C:\Games\x.exe") == null && SteamLibrary.CoverArt("..\\x") == null,
            "Shortcut icons lose their transparent padding, solid icons are told from shaped ones, and Steam links give the game's id for its cover");
        Check(DisplaySizes.SameShape(1920, 480, 1920, 480) && DisplaySizes.SameShape(682, 2560, 480, 1920) &&
              DisplaySizes.SameShape(1920, 1200, 1920, 1080) && !DisplaySizes.SameShape(1920, 480, 2560, 1600) &&
              !DisplaySizes.SameShape(2560, 682, 682, 2560) && !DisplaySizes.SameShape(0, 480, 1920, 480),
            "Screen shapes match within 15%: Y70 upright fits a 480x1920 bar, 16:10 fits 16:9, a bar does not fit a tablet");
        Check(DisplaySizes.IsHudPanel(2560, 682) && DisplaySizes.IsHudPanel(682, 2560) && DisplaySizes.IsHudPanel(1920, 480) &&
              !DisplaySizes.IsHudPanel(1920, 1080) && DisplaySizes.Describe(2560, 682).Contains("HYTE") && DisplaySizes.Describe(1366, 768) == "1366×768",
            "HUD panels (8.8\" bar, HYTE Y70 either way up) are recognised and presets are named");

        (string, string, int, int)[] themes = [("", "Classic", 1920, 480), ("bar", "Bar", 1920, 480), ("y70", "Infinity", 2560, 682),
                                               ("tower", "Tower", 682, 2560), ("tablet", "Tablet", 1920, 1200)];
        Check(ThemeCycle.Fitting(themes, 682, 2560).SequenceEqual(["tower"]) && ThemeCycle.Fitting(themes, 1920, 1080).SequenceEqual(["tablet"]) &&
              ThemeCycle.Fitting(themes, 1920, 480).SequenceEqual(["", "bar", "y70"]) && ThemeCycle.Fitting(themes, 1000, 3000).Count == 5,
            "Rotation without a playlist keeps to themes shaped like the display (all of them when none fits)");
        Check(themes.Count(t => DisplaySizes.Matches("vertical", t.Item3, t.Item4, 1920, 480)) == 1 &&
              themes.Count(t => DisplaySizes.Matches("horizontal", t.Item3, t.Item4, 1920, 480)) == 4 &&
              themes.Count(t => DisplaySizes.Matches("fits", t.Item3, t.Item4, 682, 2560)) == 1 &&
              themes.All(t => DisplaySizes.Matches("fits", t.Item3, t.Item4, 0, 0)) && themes.All(t => DisplaySizes.Matches("", t.Item3, t.Item4, 682, 2560)) &&
              DisplaySizes.Matches("vertical", 800, 800, 0, 0) && DisplaySizes.Matches("horizontal", 800, 800, 0, 0) &&
              !DisplaySizes.Matches("vertical", 0, 0, 0, 0) && DisplaySizes.Matches("from-a-newer-version", 1920, 480, 682, 2560),
            "The gallery filter keeps vertical, horizontal or display-shaped themes (square ones count as both; unknown filters show all)");
        Exception? galleryFailure = null;
        var galleryThread = new Thread(() =>
        {
            try
            {
                string root = Path.Combine(outputRoot, "gallery-" + Guid.NewGuid().ToString("N"));
                foreach (var (id, w, h) in new[] { ("tower", 682, 2560), ("wide", 2560, 682) })
                {
                    Directory.CreateDirectory(Path.Combine(root, id));
                    ThemeStore.Save(Path.Combine(root, id), new ThemeDefinition { Name = id, Width = w, Height = h, BackgroundColor = "#FF000000" });
                }
                Directory.CreateDirectory(Path.Combine(root, "broken"));
                File.WriteAllText(Path.Combine(root, "broken", "theme.json"), "{ not json");
                int Shown(string filter)
                {
                    var settings = new MonitorSettings { ThemeGalleryFilter = filter };
                    using var form = new ThemesForm(settings, root, _ => { }, displaySize: () => (682, 2560));
                    var list = AllControls(form).OfType<System.Windows.Forms.ListView>().Single();
                    return list.Items.Cast<System.Windows.Forms.ListViewItem>().Count(i => (string)i.Tag! is "tower" or "wide" or "broken" or "");
                }
                int all = Shown(""), vertical = Shown("vertical"), horizontal = Shown("horizontal"), fits = Shown("fits");
                Check(all == 4 && vertical == 1 && horizontal == 2 && fits == 1,
                    $"The Themes gallery opens on the saved shape filter (all {all}, vertical {vertical}, horizontal {horizontal}, fits {fits})");

                // Playlist: y70 then tower are ticked. Filtering hides themes without unticking or reordering them.
                string? savedFilter = null;
                using var playlist = new PlaylistForm(new MonitorSettings { ThemePlaylist = ["y70", "tower"], ThemeGalleryFilter = "vertical" }, themes, _ => [],
                    displaySize: () => (1920, 480), filterChanged: key => savedFilter = key);
                var verticalRows = playlist.ShownIds.ToList();
                var keptHidden = playlist.Playlist;
                playlist.SetFilter("horizontal");
                var horizontalRows = playlist.ShownIds.ToList();
                playlist.SelectRow(1); // Classic, listed after y70
                playlist.MoveSelected(-1);
                var moved = playlist.ShownIds.ToList();
                playlist.Tick(moved.IndexOf("bar"), true);
                playlist.SetFilter("");
                Check(verticalRows.SequenceEqual(["tower"]) && keptHidden.SequenceEqual(["y70", "tower"]) && savedFilter == "" &&
                      horizontalRows.SequenceEqual(["y70", "", "bar", "tablet"]) && moved.SequenceEqual(["", "y70", "bar", "tablet"]) &&
                      playlist.Playlist.SequenceEqual(["tower", "y70", "bar"]) && playlist.ShownIds.Count == 5,
                    "The playlist window filters by shape too: hidden themes stay ticked, moving skips them, and the filter is saved");
            }
            catch (Exception ex) { galleryFailure = ex; }
        });
        galleryThread.SetApartmentState(ApartmentState.STA); galleryThread.Start(); galleryThread.Join();
        if (galleryFailure != null) throw galleryFailure;
        Check(ThemeCycle.Order(["y70"], ["", "bar", "y70"], ["", "bar"]).SequenceEqual(["y70"]) &&
              ThemeCycle.Order([], ["", "bar", "y70"], ["", "bar"]).SequenceEqual(["", "bar"]),
            "A playlist plays the themes picked for it whatever their shape");
        var sizes = BuiltInTemplates.Names.Select(n => JsonSerializer.Deserialize<ThemeDefinition>(BuiltInTemplates.Read(n), ThemeStore.Json)!).ToList();
        Check(sizes.Any(t => (t.Width, t.Height) == (2560, 682)) && sizes.Any(t => t.Height > t.Width) && sizes.Any(t => (t.Width, t.Height) == (1920, 1200)) &&
              sizes.Count(t => (t.Width, t.Height) == (1920, 480)) == 24,
            "Built-in templates include the HYTE Y70, a vertical and tablet layouts beside the twenty-four 8.8\" ones");

        var art = new ThemeElement { Kind = ThemeElementKind.AlbumArt };
        var title = new ThemeElement { Kind = ThemeElementKind.Data, DataName = "MEDIA_TITLE" };
        var temp = new ThemeElement { Kind = ThemeElementKind.Data, DataName = "CPUTEMP" };
        var gauge = new ThemeElement { Kind = ThemeElementKind.Arc, DataName = "GPULOAD" };
        var clock = new ThemeElement { Kind = ThemeElementKind.Data, DataName = "TIME" };
        var label = new ThemeElement { Kind = ThemeElementKind.Text, Text = "CPU" };
        var button = new ThemeElement { Kind = ThemeElementKind.Panel, Action = ThemeAction.NextTheme };
        var off = new ThemeElement { Kind = ThemeElementKind.Data, DataName = "CPULOAD", Action = ThemeAction.None };
        Check(ThemeActions.Effective(art) == ThemeAction.PlayPause && ThemeActions.Effective(title) == ThemeAction.PlayPause &&
              ThemeActions.Effective(temp) == ThemeAction.Details && ThemeActions.Effective(gauge) == ThemeAction.Details &&
              ThemeActions.Effective(clock) == ThemeAction.None && ThemeActions.Effective(label) == ThemeAction.None &&
              ThemeActions.Effective(button) == ThemeAction.NextTheme && ThemeActions.Effective(off) == ThemeAction.None,
            "Taps default to play/pause on now playing and details on sensor readouts; a theme's own action wins");
        Check(ThemeActions.SensorFor("RAM_GB") == "MemoryLoad" && ThemeActions.SensorFor("GPUMODEL") == "GpuLoad" &&
              ThemeActions.SensorFor("CPUCLOCK_G") == "CpuClock" && ThemeActions.SensorFor("TIME") == null,
            "Readouts open details on their own sensor (memory, hardware names and clocks included)");

        string json = JsonSerializer.Serialize(new ThemeDefinition { Elements = [button, temp] }, ThemeStore.Json);
        var back = JsonSerializer.Deserialize<ThemeDefinition>(json, ThemeStore.Json)!;
        var old = JsonSerializer.Deserialize<ThemeDefinition>("{\"Elements\":[{\"Kind\":\"Data\",\"DataName\":\"CPUTEMP\"}]}", ThemeStore.Json)!;
        Check(json.Contains("\"NextTheme\"") && back.Elements[0].Action == ThemeAction.NextTheme && back.Elements[1].Action == null &&
              !json.Contains("\"Action\": null") && old.Elements[0].Action == null && old.Width == 1920 && old.Height == 480,
            "Tap actions are saved by name, left out when automatic, and older themes load as 1920x480 with automatic taps");

        var playing = new ThemeDataContext { Media = new NowPlaying(MediaPlayback.Playing, "t", "a", "app", null) };
        var paused = new ThemeDataContext { Media = new NowPlaying(MediaPlayback.Paused, "t", "a", "app", null) };
        Check(ThemeData.Read("MEDIA_PLAYPAUSE", null, false, playing).Text == ThemeData.PauseGlyph &&
              ThemeData.Read("MEDIA_PLAYPAUSE", null, false, paused).Text == ThemeData.PlayGlyph &&
              ThemeData.Read("MEDIA_PLAYPAUSE", null, false, new ThemeDataContext()).Text == ThemeData.PlayGlyph,
            "The play/pause icon shows pause while playing and play otherwise");
        Check(PlexampReader.CommandPath(MediaCommand.PlayPause, MediaPlayback.Playing) == "/player/playback/pause?type=music" &&
              PlexampReader.CommandPath(MediaCommand.PlayPause, MediaPlayback.Paused) == "/player/playback/play?type=music" &&
              PlexampReader.CommandPath(MediaCommand.Next, MediaPlayback.Playing) == "/player/playback/skipNext?type=music" &&
              PlexampReader.CommandPath(MediaCommand.Previous, MediaPlayback.Paused) == "/player/playback/skipPrevious?type=music",
            "Plexamp gets pause/play by its state, and skip next/previous, on its local player address");

        Check(SensorDetails.For("CpuTemp") == (0, "CpuTemp") && SensorDetails.For("GpuFps") == (1, "GpuFps") &&
              SensorDetails.For("CpuClock") == (0, "CpuLoad") && SensorDetails.For("DriveTemp") == (2, "MemoryLoad") && SensorDetails.For(null) == (2, "MemoryLoad"),
            "Details open on the tapped reading's page, or the first reading of its group when it has no session statistics");
        Check(SensorDetails.Format("CpuTemp", 64.4) == "64°C" && SensorDetails.Format("GpuVram", 8192) == 8.0.ToString("F1") + " GB" &&
              SensorDetails.Format("NetDownload", double.NaN) == "--" && SensorDetails.Format("CpuFan", null) == "--",
            "Details format readings with units and show -- for missing ones");
        Check(SensorDetails.ChartMaximum("CpuLoad", [300], null) == 100 && Math.Abs(SensorDetails.ChartMaximum("CpuTemp", [60, 70], 90) - 99) < 1e-9 &&
              SensorDetails.ChartMaximum("CpuPower", [], null) == 1,
            "Detail charts scale percentages to 100 and keep the alert limit in view");
        Check(SensorDetails.LogicalSize(1920, 480) == new Size(1920, 480) && SensorDetails.LogicalSize(682, 2560) == new Size(480, 1802) &&
              SensorDetails.LogicalSize(1920, 1200) == new Size(768, 480),
            "The details page is laid out for the display's shape with a 480-unit short side");

        Exception? failure = null;
        var thread = new Thread(() => { try { Thumbnails(outputRoot, shots); Rendered(outputRoot, shots); } catch (Exception ex) { failure = ex; } });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
        if (failure != null) throw failure;
    }

    // Gallery tiles: how much of the 2:1 tile a theme's preview covers, and a contact sheet of all of them.
    private static void Thumbnails(string outputRoot, string? shots)
    {
        var tiles = new List<(string Name, System.Drawing.Bitmap Image)> { ("Classic", (System.Drawing.Bitmap)ThemesForm.ClassicThumbnail()) };
        // As the gallery lists them, with previews rendered off-screen.
        foreach (var info in BuiltInTemplates.List(Path.Combine(outputRoot, "thumb-templates")))
            tiles.Add((info.Id, (System.Drawing.Bitmap)ThemesForm.Thumbnail(info)));
        double Covered(System.Drawing.Bitmap b)
        {
            var back = b.GetPixel(0, 0);
            int across = Enumerable.Range(0, b.Width).Count(x => b.GetPixel(x, b.Height / 2) != back);
            int down = Enumerable.Range(0, b.Height).Count(y => b.GetPixel(b.Width / 2, y) != back);
            return (double)across / b.Width * down / b.Height;
        }
        double bar = Covered(tiles.First(t => t.Name.Contains("01-")).Image), tower = Covered(tiles.First(t => t.Name.Contains("tower")).Image),
               tablet = Covered(tiles.First(t => t.Name.Contains("tablet")).Image);
        Check(tiles.All(t => t.Image.Size == ThemesForm.ThumbSize) && tower > 0.45 && bar > 0.45 && tablet > 0.7,
            $"Gallery tiles give every shape a fair share: bar {bar:P0}, vertical {tower:P0} (turned on its side), tablet {tablet:P0}");

        if (shots != null)
        {
            Directory.CreateDirectory(shots);
            int columns = 4, w = ThemesForm.ThumbSize.Width + 12, h = ThemesForm.ThumbSize.Height + 12;
            using var sheet = new System.Drawing.Bitmap(columns * w, (tiles.Count + columns - 1) / columns * h);
            using (var g = System.Drawing.Graphics.FromImage(sheet))
            {
                g.Clear(System.Drawing.Color.FromArgb(23, 32, 43));
                for (int i = 0; i < tiles.Count; i++) g.DrawImage(tiles[i].Image, i % columns * w + 6, i / columns * h + 6);
            }
            sheet.Save(Path.Combine(shots, "gallery-thumbnails.png"), System.Drawing.Imaging.ImageFormat.Png);
        }
        foreach (var tile in tiles) tile.Image.Dispose();
    }

    private static void Rendered(string outputRoot, string? shots)
    {
        string dir = BuiltInTemplates.Ensure(BuiltInTemplates.Prefix + "18-desk-tablet", Path.Combine(outputRoot, "touch-templates"))!;
        var theme = ThemeStore.Load(dir);
        var view = new ThemeView(theme, dir, playVideo: false);
        view.Measure(new Size(theme.Width, theme.Height));
        view.Arrange(new Rect(0, 0, theme.Width, theme.Height));
        view.Update(ThemeSampleData.Uniform());
        view.UpdateLayout();
        var buttons = theme.Elements.Where(e => e.Kind == ThemeElementKind.Panel && e.Action is ThemeAction.PlayPause or ThemeAction.NextTrack).ToList();
        var nextTrack = buttons.First(b => b.Action == ThemeAction.NextTrack);
        var cpuTemp = theme.Elements.First(e => e.DataName == "CPUTEMP" && e.Y > 600);
        Check(view.ElementAt(new Point(nextTrack.X + nextTrack.Width / 2, nextTrack.Y + nextTrack.Height / 2)) is { } hit &&
              ThemeActions.Effective(hit) == ThemeAction.NextTrack &&
              view.ElementAt(new Point(cpuTemp.X + 10, cpuTemp.Y + 20)) is { DataName: "CPUTEMP" } &&
              view.ElementAt(new Point(theme.Width - 6, 6)) is null,
            "A tap finds the button or readout under it (topmost first) and nothing on bare background");
        var small = new ThemeDefinition { Elements = [new ThemeElement { Kind = ThemeElementKind.Bar, DataName = "CPULOAD", X = 100, Y = 100, Width = 40, Height = 6 }] };
        var smallView = new ThemeView(small, dir, playVideo: false);
        smallView.Measure(new Size(1920, 480)); smallView.Arrange(new Rect(0, 0, 1920, 480));
        Check(smallView.ElementAt(new Point(120, 128)) != null && smallView.ElementAt(new Point(120, 140)) == null,
            "Small readouts get a finger-sized (64 px) tap target");

        var telemetry = new TelemetryEngine();
        var settings = new MonitorSettings();
        var start = DateTime.Now.AddMinutes(-4);
        for (int i = 0; i < 240; i++)
        {
            var snapshot = new SensorSnapshot();
            foreach (var metric in TelemetryEngine.MetricOrder) snapshot.Values[metric] = i is > 100 and < 110 ? null : 40 + 30 * Math.Sin(i / 15.0);
            snapshot.Values["CpuTemp"] = i is > 100 and < 110 ? null : 62 + 20 * Math.Sin(i / 20.0);
            telemetry.Ingest(start.AddSeconds(i), snapshot, settings);
        }
        foreach (var (name, w, h) in new[] { ("bar", 1920, 480), ("y70-vertical", 682, 2560), ("tablet", 1920, 1200) })
        {
            var size = SensorDetails.LogicalSize(w, h);
            var page = new SensorDetailView(telemetry, settings, size, g => g == 0 ? "Intel Core Ultra 7 265K" : "");
            page.Open("CpuTemp");
            page.Measure(size); page.Arrange(new Rect(size)); page.UpdateLayout();
            page.Update(start.AddSeconds(240));
            page.UpdateLayout();
            var bitmap = new RenderTargetBitmap((int)size.Width, (int)size.Height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(page);
            page.Close();
            Check(page.Group == 0 && page.Metric == "CpuTemp" && bitmap.PixelWidth == (int)size.Width,
                $"The details page draws at the {name} shape ({size.Width:0}x{size.Height:0}) on the tapped reading");
            if (shots != null) { Directory.CreateDirectory(shots); ThemeMedia.SavePng(bitmap, Path.Combine(shots, "details-" + name + ".png")); }
        }
    }
}
