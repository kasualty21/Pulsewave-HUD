using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Pulsewave.Themes;
using Point = System.Windows.Point;
using Size = System.Windows.Size;

namespace Pulsewave.Regression;

// Custom themes: safe template reading, import, storage rules, video wrapping, data
// formatting and rendering. No check needs Lian Li's files; local templates are optional.
internal static class ThemeChecks
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("THEMES: " + message);
        Console.WriteLine("PASS THEMES: " + message);
    }

    private static bool Throws<T>(Action action) where T : Exception
    {
        try { action(); return false; } catch (T) { return true; }
    }

    public static void Run(string outputRoot)
    {
        string root = Path.Combine(outputRoot, "themes-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        Reader();
        Import(root);
        Store(root);
        Video();
        Data();
        NewData();
        Exception? failure = null;
        var thread = new Thread(() => { try { Render(); Alerts(); Tiles(); Feed(); BackgroundPlacement(root); } catch (Exception ex) { failure = ex; } });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
        if (failure != null) throw new Exception("Theme render checks failed", failure);
        Sensors();
        LocalTemplates(root);
    }

    private static void Reader()
    {
        var root = (NrbfObject)NrbfReader.Read(new MemoryStream(NrbfFixture.Theme()))!;
        var items = LConnectImporter.Items(root.Get("GraphList")).ToList();
        Check(root.String("name") == "Fixture Theme" && root.Number("width") == 1920 && items.Count == 2,
            "Template reader returns the theme's fields and element list as plain data");
        Check(ReferenceEquals(items[0].Object("fontConfig"), items[1].Object("fontConfig")) && items[1].String("TypeName") == "Text",
            "Shared objects and reused class layouts resolve like the original stream");

        var good = NrbfFixture.Theme();
        Check(Throws<InvalidDataException>(() => NrbfReader.Read(new MemoryStream(good[..(good.Length / 2)]))), "Truncated template is rejected");
        Check(Throws<InvalidDataException>(() => NrbfReader.Read(new MemoryStream([1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17]))),
            "Non-template data is rejected");
        var bomb = new NrbfFixture();
        bomb.Raw([16]); bomb.Int(1); bomb.Int(int.MaxValue); // array claiming more elements than the file has
        Check(Throws<InvalidDataException>(() => NrbfReader.Read(new MemoryStream(bomb.Finish()))), "Lengths larger than the file are rejected before allocating");
        var unknown = new NrbfFixture();
        unknown.Raw([21]);
        Check(Throws<InvalidDataException>(() => NrbfReader.Read(new MemoryStream(unknown.Finish()))), "Unsupported record types (method calls) are rejected");
        var deep = new NrbfFixture();
        deep.ClassWithTypes(1, "Nest", [("child", 2, null)]);
        for (int i = 0; i < 100; i++) deep.ClassWithId(1000 + i, 1);
        deep.Null();
        Check(Throws<InvalidDataException>(() => NrbfReader.Read(new MemoryStream(deep.Finish()))), "Excessively nested templates are rejected");
    }

    private static void Import(string root)
    {
        string source = Path.Combine(root, "..", "fixture-" + Guid.NewGuid().ToString("N") + ".template");
        File.WriteAllBytes(source, NrbfFixture.Theme());
        var result = LConnectImporter.Import(source, root);
        var data = result.Theme.Elements[0];
        Check(result.Theme.Name == "Fixture Theme" && data.Kind == ThemeElementKind.Data && data.DataName == "CPUTEMP" &&
              data.X == 207 && data.Y == 218 && data.ShowUnit && data.Font is { Family: "Digital-7", Size: 68, Alignment: ThemeTextAlignment.Center, Color: "#FFFFAA5F" },
            "Imported Data keeps source, position, unit, font, ARGB color and center alignment");
        Check(result.Theme.Elements[1] is { Kind: ThemeElementKind.Text, Text: "CPU", DataName: null } && result.Theme.Elements[1].Font?.Family == "Digital-7",
            "Imported Text keeps its label and the shared font");
        string dir = Path.Combine(root, result.ThemeId);
        Check(File.Exists(Path.Combine(dir, "theme.json")) && ThemeStore.Load(dir).Elements.Count == 2 &&
              !Directory.EnumerateDirectories(root, ".import-*").Any(), "Import writes a Pulsewave theme folder and leaves no staging folder");
        Check(ThemeStore.List(root).Any(t => t.Id == result.ThemeId && t.Definition != null), "Imported theme appears in the gallery list");

        string broken = Path.Combine(root, "..", "broken-" + Guid.NewGuid().ToString("N") + ".template");
        File.WriteAllBytes(broken, [0, 1, 0, 0, 0, 255, 255]);
        int before = Directory.GetDirectories(root).Length;
        Check(Throws<InvalidDataException>(() => LConnectImporter.Import(broken, root)) && Directory.GetDirectories(root).Length == before,
            "A failed import leaves no partial theme behind");
        string second = LConnectImporter.Import(source, root).ThemeId;
        Check(second != result.ThemeId && second.StartsWith("Fixture Theme"), "Importing the same theme twice keeps both under distinct names");
    }

    private static void Store(string root)
    {
        string dir = Path.Combine(root, "store-check");
        Directory.CreateDirectory(dir);
        Check(Throws<InvalidDataException>(() => ThemeStore.ResolveFile(dir, @"..\settings.json")) &&
              Throws<InvalidDataException>(() => ThemeStore.ResolveFile(dir, @"C:\Windows\win.ini")) &&
              ThemeStore.ResolveFile(dir, "images/a.png").StartsWith(dir, StringComparison.OrdinalIgnoreCase),
            "Theme file references cannot leave the theme folder");
        var escaping = new ThemeDefinition { Name = "x", BackgroundVideo = @"..\..\other.mp4" };
        Check(Throws<InvalidDataException>(() => ThemeStore.Save(dir, escaping)), "A theme.json pointing outside its folder is refused");
        var bad = new ThemeDefinition { Elements = [new ThemeElement { X = double.NaN }] };
        Check(Throws<InvalidDataException>(() => ThemeStore.Validate(bad, dir)), "Non-finite element geometry is refused");
        Check(ThemeStore.NewId(root, @"..\a:b*c?") is var id && id == Path.GetFileName(id) && !id.Contains(':') && !id.Contains('*'),
            "Theme folder names are sanitized");
        Check(Throws<ArgumentException>(() => ThemeStore.Delete(root, "..")) && Throws<ArgumentException>(() => ThemeStore.Delete(root, @"a\b")),
            "Delete only accepts a single theme folder name");
    }

    // Parameter sets taken from an x264 stream (encoder settings only, no picture data).
    private static readonly byte[] Sps = [0x67, 0x42, 0xC0, 0x1F, 0xDA, 0x07, 0x80, 0xF1, 0xB0, 0x16, 0xC8, 0x00, 0x00, 0x03, 0x00, 0x08, 0x00, 0x00, 0x03, 0x01, 0x84, 0x78, 0xC1, 0x95];
    private static readonly byte[] Pps = [0x68, 0xCE, 0x0F, 0xC8];

    private static void Video()
    {
        var sps = H264Mp4Writer.ParseSps(Sps);
        Check(sps.Width == 480 && sps.Height == 1920 && Math.Abs(sps.Fps - 24) < 0.01, "SPS parsing reads size and frame rate (480x1920, 24 fps)");

        // Synthetic stream: SPS/PPS + key frame, then two predicted frames, with 3- and 4-byte start codes.
        var stream = new MemoryStream();
        void Nal(byte[] nal, bool longCode) { stream.Write(longCode ? [0, 0, 0, 1] : [0, 0, 1]); stream.Write(nal); }
        Nal(Sps, true); Nal(Pps, true);
        Nal([0x65, 0x88, 0x84, 0x21, 0x00], true); // IDR slice, first_mb = 0 (trailing zero belongs to the next start code)
        Nal([0x41, 0x9A, 0x22], false);             // P slice, new frame
        Nal([0x41, 0x9A, 0x33], false);
        var nals = H264Mp4Writer.SplitNalUnits(stream.ToArray());
        Check(nals.Count == 5 && nals[2].Length == 4, "NAL splitting handles both start codes and strips trailing zero bytes");

        var mp4 = new MemoryStream();
        var info = H264Mp4Writer.Convert(stream.ToArray(), mp4);
        var bytes = mp4.ToArray();
        Check(info.Frames == 3 && info.Width == 480 && System.Text.Encoding.ASCII.GetString(bytes, 4, 4) == "ftyp" &&
              H264Mp4Writer.ReadMp4Size(bytes) == (480, 1920), "Raw H.264 is wrapped as an MP4 with the right frame count and size");
        var noKey = new MemoryStream();
        foreach (var nal in new[] { Sps, Pps, new byte[] { 0x41, 0x9A, 0x22 } }) { noKey.Write([0, 0, 1]); noKey.Write(nal); }
        Check(Throws<InvalidDataException>(() => H264Mp4Writer.Convert(noKey.ToArray(), new MemoryStream())), "A stream without a key frame is refused");
    }

    private static void Data()
    {
        var c = ThemeSamples.Context();
        string Show(string name, bool unit = false, string? format = null, bool f = false) =>
            ThemeData.Display(ThemeData.Read(name, format, f, c), unit);
        Check(Show("CPUCLOCK_G") == "4.80" && Show("CPUCLOCK_G", unit: true) == "4.80GHz" && Show("CPUCLOCK") == "4800",
            "Clock reads in MHz or GHz as the theme asks");
        Check(Show("CPUTEMP", true) == "88°C" && Show("CPUTEMP", true, f: true) == "190°F", "Temperatures honor the theme's Fahrenheit option");
        Check(Show("DOWNDSPEED", true) == "888KB/s" && Math.Abs(ThemeData.Read("DOWNDSPEED", null, false, c).Number!.Value - 888) < 1e-6, "Network shows KB/s below 1 MB/s");
        Check(Show("TIME", format: "h:m:s") == "12:00:00" && Show("TIME", format: "h:m") == "12:00" &&
              Show("DATE", format: "Y.M.D") == "2020.12.31" && Show("DAY") == "Thu", "Time and date follow the theme's format tokens");
        Check(Show("RAM_GB", true) == "10.8GB" && Show("FPS_AVG") == "118" && Show("GPUMODEL") == "RTX 5070 Ti",
            "Memory, average FPS and model names come from the context");
        var empty = new ThemeDataContext();
        Check(ThemeData.Display(ThemeData.Read("GPUTEMP", null, false, empty), true) == "--" && Show("RAMMODEL") == "--" && Show("NOT_A_SOURCE") == "--",
            "Missing and unsupported data show -- without a dangling unit");
    }

    private static void NewData()
    {
        var c = ThemeSampleData.Context();
        string Show(string name, string? format = null, bool unit = true, bool spaced = false) => ThemeData.Display(ThemeData.Read(name, format, false, c), unit, spaced);
        Check(Show("WEATHER_RAIN") == "Rain from 18:00" && Show("WEATHER_H1_TIME") == "13:00" && Show("WEATHER_H3_TIME") == "19:00" &&
              ThemeData.Read("WEATHER_H3_KIND", null, false, c).Number == 2 && ThemeData.Read("WEATHER_KIND", null, false, c).Number == 1 &&
              Show("WEATHER_FEELS_NOTE") == "Feels 16°",
            "Weather: the next hours three apart, when rain starts, a kind number for coloring symbols, and Feels only when it differs by 2° or more");
        var close = ThemeSampleData.Context() is var s && s.Weather is { } w ? new ThemeDataContext { Weather = w with { FeelsC = 17.2 }, Now = s.Now } : null;
        bool feelsHidden = ThemeData.Read("WEATHER_FEELS_NOTE", null, false, close!).Text == "";
        Check(feelsHidden && Show("DRIVE_FREE", spaced: true) == "412 GB free" && Show("DRIVE_USED_PCT") == "78%" && Show("MEDIA_ALBUM") == "Hurry Up, We're Dreaming" &&
              Show("CLAUDE_5H_RESET", "resets") == "resets in 2h 14m" && Show("GAME_SESSION", "short") == "1h 24m" && Show("GAME_SESSION") == "1:24:10" &&
              Show("GPURAM_GB_OF") == "9.6 / 16 GB" && Show("CPUPWR", spaced: true) == "142 W" && Show("CPUTEMP", spaced: true) == "71°C",
            "Free drive space, album, resets-in wording, short session time, VRAM used of total, and spaced units (never before a degree sign)");

        var timers = new TouchTimers();
        var t0 = new DateTime(2026, 1, 1, 9, 0, 0);
        timers.StartPomodoro(t0, 25, 5);
        bool focus = timers.Phase == "FOCUS" && timers.TimerRunning && timers.TimerLeft(t0.AddMinutes(1)) == TimeSpan.FromMinutes(24);
        bool toBreak = timers.Finished(t0.AddMinutes(25)) && timers.Phase == "BREAK" && timers.TimerRunning && timers.TimerLeft(t0.AddMinutes(26)) == TimeSpan.FromMinutes(4);
        bool toFocus = timers.Finished(t0.AddMinutes(30)) && timers.Phase == "FOCUS";
        timers.SwitchShown(); timers.ToggleShown(t0.AddMinutes(31));
        bool shown = timers.StopwatchShown && timers.StopwatchRunning && timers.TimerRunning;
        timers.SwitchShown(); timers.AddMinute();
        Check(focus && toBreak && toFocus && shown && timers.Phase == "",
            "Pomodoro: focus then break start by themselves; the card's switch shows the stopwatch; +1 min turns it back into a plain countdown");
        var ctx = new ThemeDataContext { TimerRunning = true, StopwatchShown = true, TimerPhase = "FOCUS", Stopwatch = TimeSpan.FromSeconds(75), Timer = TimeSpan.FromMinutes(3) };
        Check(ThemeData.Read("FOCUS_MODE", null, false, ctx).Text == "STOPWATCH" && ThemeData.Read("FOCUS_CLOCK", null, false, ctx).Text == "1:15" &&
              ThemeData.Read("FOCUS_ACTION", null, false, ctx).Text == "Start" && ThemeData.Read("FOCUS_SWITCH", null, false, ctx).Text == "Timer" &&
              ThemeData.Read("TIMER_ACTION", null, false, ctx).Text == "Pause",
            "The timer card shows whichever clock is chosen, and its buttons say Start or Pause for it");
    }

    private static void BackgroundPlacement(string root)
    {
        var theme = new ThemeDefinition { BackgroundFit = "Cover" };
        var cover = ThemeView.BackgroundRect(theme, 1000, 1000, 1, 0, 0);
        Check(cover == new Rect(0, -720, 1920, 1920), "A Cover background fills the width, centred");
        theme.BackgroundFit = "Fit";
        Check(ThemeView.BackgroundRect(theme, 1000, 1000, 1, 0, 0) == new Rect(720, 0, 480, 480), "A Fit background shows the whole picture, centred");
        theme.BackgroundFit = "Fill";
        Check(ThemeView.BackgroundRect(theme, 1000, 1000, 0.5, 100, -20) == new Rect(580, 100, 960, 240), "Zoom scales about the centre and the shift moves it");
        var (x, y) = Pulsewave.Themes.Editor.EditorGeometry.ZoomAbout(new Point(1500, 100), new Point(960, 240), 1, 2, 30, 10);
        var before = ThemeView.BackgroundRect(theme, 1920, 480, 1, 30, 10);
        var after = ThemeView.BackgroundRect(theme, 1920, 480, 2, x, y);
        double u = (1500 - before.X) / before.Width, v = (100 - before.Y) / before.Height;
        Check(Math.Abs(after.X + u * after.Width - 1500) < 1e-6 && Math.Abs(after.Y + v * after.Height - 100) < 1e-6,
            "Scrolling to zoom keeps the background point under the pointer in place");

        var old = System.Text.Json.JsonSerializer.Deserialize<ThemeDefinition>("{\"Name\":\"old\",\"BackgroundFit\":\"Cover\"}", ThemeStore.Json)!;
        Check(old.BackgroundZoom == 1 && old.BackgroundOffsetX == 0 && old.BackgroundOffsetY == 0, "Themes saved before placement existed keep their background as it was");
        string dir = Path.Combine(root, "background-check");
        Directory.CreateDirectory(dir);
        var wild = new ThemeDefinition { BackgroundZoom = double.PositiveInfinity, BackgroundOffsetX = double.NaN, BackgroundOffsetY = 1e9 };
        ThemeStore.Validate(wild, dir);
        Check(wild.BackgroundZoom == 1 && wild.BackgroundOffsetX == 0 && wild.BackgroundOffsetY == 8192, "Out-of-range background placement falls back safely");

        // Render: a white picture shifted right by half the screen leaves the left half the background color.
        var white = System.Windows.Media.Imaging.BitmapSource.Create(4, 1, 96, 96, PixelFormats.Bgra32, null, Enumerable.Repeat((byte)255, 16).ToArray(), 16);
        ThemeMedia.SavePng(white, Path.Combine(dir, "bg.png"));
        var shifted = new ThemeDefinition { BackgroundColor = "#FF000000", BackgroundImage = "bg.png", BackgroundFit = "Fill", BackgroundOffsetX = 960 };
        var view = new ThemeView(shifted, dir, playVideo: false);
        view.Measure(new Size(1920, 480)); view.Arrange(new Rect(0, 0, 1920, 480)); view.UpdateLayout();
        var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(1920, 480, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(view);
        byte Red(int px, int py) { var pixel = new byte[4]; bitmap.CopyPixels(new Int32Rect(px, py, 1, 1), pixel, 4, 0); return pixel[2]; }
        Check(Red(400, 240) < 10 && Red(1400, 240) > 245, "A shifted background renders where it was placed");
    }

    private static void Render()
    {
        var theme = new ThemeDefinition
        {
            Name = "Render check",
            Elements =
            [
                new ThemeElement { Kind = ThemeElementKind.Data, DataName = "CPULOAD", X = 960, Y = 100,
                    Font = new ThemeFont { Size = 40, Alignment = ThemeTextAlignment.Center } },
                new ThemeElement { Kind = ThemeElementKind.Bar, DataName = "CPULOAD", X = 10, Y = 10, Width = 200, Height = 20,
                    ForeColor = "#FF00FFFF", BackColor = "#FF333333" },
                new ThemeElement { Kind = ThemeElementKind.Arc, DataName = "CPULOAD", X = 300, Y = 10, Diameter = 100, Thickness = 10, SweepAngle = 280, StartAngle = 90 },
                new ThemeElement { Kind = ThemeElementKind.Chart, DataName = "CPULOAD", X = 500, Y = 10, Width = 100, Height = 50, Samples = 10 },
                new ThemeElement { Kind = ThemeElementKind.Text, Text = "HIDDEN", Hidden = true, X = 0, Y = 0, Font = new ThemeFont() }
            ]
        };
        var view = new ThemeView(theme, Path.GetTempPath(), playVideo: false);
        var sensors = new SensorSnapshot(); sensors.Values["CpuLoad"] = 50;
        for (int i = 0; i < 3; i++) view.Update(new ThemeDataContext { Sensors = sensors });
        view.Measure(new Size(1920, 480)); view.Arrange(new Rect(0, 0, 1920, 480)); view.UpdateLayout();

        var canvas = view.Children.OfType<Canvas>().Single();
        var text = canvas.Children.OfType<TextBlock>().Single();
        var box = text.TransformToAncestor(view).TransformBounds(new Rect(0, 0, text.ActualWidth, text.ActualHeight));
        Check(text.Text == "50" && Math.Abs(box.Left + box.Width / 2 - 960) < 1 && Math.Abs(box.Top - 100) < 1,
            "Centered Data text is centered on X with its top at Y, and hidden elements are not drawn");
        var fill = canvas.Children.OfType<System.Windows.Shapes.Rectangle>().Single(r => r.Height == 20 && r.Fill is SolidColorBrush { Color: { G: 255, B: 255, R: 0 } });
        Check(fill.Width == 100, "Bar fill follows value / maximum (50 of 100 on a 200 px bar)");
        var arcs = canvas.Children.OfType<System.Windows.Shapes.Path>().ToList();
        Check(arcs.Count == 2 && arcs[1].Data is PathGeometry { Figures: [{ StartPoint: var start }] } &&
              Math.Abs(start.X - 350) < 0.5 && Math.Abs(start.Y - 110) < 0.5,
            "Arc value starts at its start angle on the ring (6 o'clock for 90°)");
        var chart = canvas.Children.OfType<Canvas>().Single();
        var line = chart.Children.OfType<System.Windows.Shapes.Polyline>().Single();
        Check(line.Points.Count == 3 && line.Points[^1] == new Point(100, 25), "Chart keeps recent samples with the newest at the right edge");
        view.Dispose();
    }

    private static void Alerts()
    {
        var hot = new ThemeElement { Kind = ThemeElementKind.Data, DataName = "CPUTEMP", Fahrenheit = true, WarnAt = 80, CriticalAt = 90,
            X = 10, Y = 200, Font = new ThemeFont { Color = "#FFFFFFFF" } };
        var bar = new ThemeElement { Kind = ThemeElementKind.Bar, DataName = "CPULOAD", WarnAt = 85, CriticalAt = 95, X = 10, Y = 10, Width = 200, Height = 20, ForeColor = "#FF00FFFF" };
        var ring = new ThemeElement { Kind = ThemeElementKind.Arc, DataName = "CPULOAD", WarnAt = 85, X = 300, Y = 10, Diameter = 100, Thickness = 10, ForeColor = "#FF00FFFF" };
        var fps = new ThemeElement { Kind = ThemeElementKind.Data, DataName = "FPS", LowIsBad = true, WarnAt = 60, CriticalAt = 30 };
        var plain = new ThemeElement { Kind = ThemeElementKind.Data, DataName = "CPUTEMP" };
        ThemeDataContext At(double temp, double load, double fpsValue)
        {
            var s = new SensorSnapshot(); s.Values["CpuTemp"] = temp; s.Values["CpuLoad"] = load; s.Values["GpuFps"] = fpsValue;
            return new ThemeDataContext { Sensors = s };
        }
        Check(ThemeView.AlertLevel(hot, At(70, 0, 0)) == 0 && ThemeView.AlertLevel(hot, At(82, 0, 0)) == 1 && ThemeView.AlertLevel(hot, At(91, 0, 0)) == 2 &&
              ThemeView.AlertLevel(fps, At(0, 0, 120)) == 0 && ThemeView.AlertLevel(fps, At(0, 0, 45)) == 1 && ThemeView.AlertLevel(fps, At(0, 0, 20)) == 2 &&
              ThemeView.AlertLevel(plain, At(99, 0, 0)) == 0 && ThemeView.AlertLevel(hot, new ThemeDataContext()) == 0,
            "Warning and critical thresholds compare in °C (even on a °F element), count down when low is bad, and change nothing when unset or missing");

        var view = new ThemeView(new ThemeDefinition { Elements = [hot, bar, ring] }, Path.GetTempPath(), playVideo: false);
        System.Windows.Media.Color TextColor() => ((SolidColorBrush)view.Children.OfType<Canvas>().Single().Children.OfType<TextBlock>().Single().Foreground).Color;
        System.Windows.Media.Color BarColor() => ((SolidColorBrush)view.Children.OfType<Canvas>().Single().Children.OfType<System.Windows.Shapes.Rectangle>().Last().Fill).Color;
        System.Windows.Media.Color RingColor() => ((SolidColorBrush)view.Children.OfType<Canvas>().Single().Children.OfType<System.Windows.Shapes.Path>().Last().Stroke).Color;
        view.Update(At(60, 50, 0));
        bool calm = TextColor() == System.Windows.Media.Colors.White && BarColor() == System.Windows.Media.Color.FromRgb(0, 255, 255) && RingColor() == System.Windows.Media.Color.FromRgb(0, 255, 255);
        view.Update(At(85, 90, 0));
        bool warm = TextColor() == System.Windows.Media.Color.FromRgb(0xFF, 0xB5, 0x47) && BarColor() == System.Windows.Media.Color.FromRgb(0xFF, 0xB5, 0x47) && RingColor() == System.Windows.Media.Color.FromRgb(0xFF, 0xB5, 0x47);
        view.Update(At(95, 99, 0));
        bool red = TextColor() == System.Windows.Media.Color.FromRgb(0xFF, 0x5A, 0x6E) && BarColor() == System.Windows.Media.Color.FromRgb(0xFF, 0x5A, 0x6E);
        view.Update(At(60, 50, 0));
        bool back = TextColor() == System.Windows.Media.Colors.White && BarColor() == System.Windows.Media.Color.FromRgb(0, 255, 255);
        Check(calm && warm && red && back, "Text, bars and rings turn amber at the warning level, red when critical, and back again");
        view.Dispose();
    }

    private static void Tiles()
    {
        System.Windows.Media.Imaging.BitmapSource Icon(byte r, byte g, byte b)
        {
            var px = new byte[32 * 32 * 4];
            for (int i = 0; i < px.Length; i += 4) { px[i] = b; px[i + 1] = g; px[i + 2] = r; px[i + 3] = 255; }
            return System.Windows.Media.Imaging.BitmapSource.Create(32, 32, 96, 96, PixelFormats.Bgra32, null, px, 32 * 4);
        }
        var red = ShortcutIcons.Accent(Icon(220, 40, 40));
        Check(red is { R: > 200, G: < 60, B: < 60 } && ShortcutIcons.Accent(Icon(128, 128, 128)) == null,
            "A tile's backdrop color comes from its icon's main hue; grey icons have none");

        ThemeElement Tile(double x, string? style, bool label = false) => new()
            { Kind = ThemeElementKind.Shortcut, X = x, Y = 0, Width = 120, Height = 120, CornerRadius = 24, FillColor = "#FF202020", TileStyle = style, ShowLabel = label };
        var context = new ThemeDataContext { Shortcuts =
            [new ShortcutView("Game", Icon(220, 40, 40)) { Accent = red }, new ShortcutView("Lock", null, "") { IsAction = true }, new ShortcutView("Game", Icon(220, 40, 40)) { Accent = red }] };
        var tinted = Tile(0, "Tinted", label: true); tinted.Slot = 1;
        var action = Tile(200, "Tinted"); action.Slot = 2;
        var plain = Tile(400, null); plain.Slot = 3;
        var view = new ThemeView(new ThemeDefinition { Elements = [tinted, action, plain] }, Path.GetTempPath(), playVideo: false);
        view.Update(context);
        view.Measure(new Size(1920, 480)); view.Arrange(new Rect(0, 0, 1920, 480)); view.UpdateLayout();
        var tiles = view.Children.OfType<Canvas>().Single().Children.OfType<Border>().Where(b => b.Width == 120 && b.Child is Grid).ToList();
        bool RoundShown(Border tile) => ((Grid)((Grid)tile.Child).Children[0]).Children.OfType<Border>().First().Visibility == Visibility.Visible;
        var label = ((Grid)tiles[0].Child).Children.OfType<TextBlock>().SingleOrDefault();
        Check(tiles.Count == 3 && tiles[0].Background is LinearGradientBrush && label?.Text == "Game" &&
              tiles[1].Background == System.Windows.Media.Brushes.Transparent && RoundShown(tiles[1]) && !RoundShown(tiles[0]) &&
              tiles[2].Background is SolidColorBrush { Color: { R: 0x20 } } && !RoundShown(tiles[2]),
            "Tinted tiles take their icon's color and can name the shortcut; system actions become round buttons; plain tiles are unchanged");
        view.Dispose();
    }

    private static void Feed()
    {
        SensorSnapshot Snap(double load, double clock, int expected = 2)
        {
            var s = new SensorSnapshot { ExpectedCount = expected };
            s.Values["CpuLoad"] = load; s.Values["CpuClock"] = clock;
            return s;
        }
        var feed = new SensorFeed();
        var t0 = new DateTime(2026, 1, 1, 12, 0, 0);
        bool live = feed.Update(Snap(40, 5000), t0) == SensorFeedState.Live && feed.Update(Snap(41, 5000), t0.AddSeconds(10)) == SensorFeedState.Live;
        bool briefPause = feed.Update(Snap(41, 5000), t0.AddSeconds(35)) == SensorFeedState.Live;      // unchanged for 25 s: still live
        bool frozen = feed.Update(Snap(41, 5000), t0.AddSeconds(41)) == SensorFeedState.Frozen;          // unchanged for 31 s
        bool back = feed.Update(Snap(42, 5000), t0.AddSeconds(42)) == SensorFeedState.Live;
        var offline = new SensorSnapshot { ExpectedCount = 12 };
        bool gone = feed.Update(offline, t0.AddSeconds(43)) == SensorFeedState.Offline;
        var single = new SensorFeed();
        var one = new SensorSnapshot { ExpectedCount = 1 }; one.Values["PumpRpm"] = 2650;
        single.Update(one, t0); bool oneStill = single.Update(one, t0.AddMinutes(5)) == SensorFeedState.Live; // one static reading is never stale
        Check(live && briefPause && frozen && back && gone && oneStill,
            "The sensor feed is frozen only when every reading has stood still for 30 s, offline when HWiNFO reports nothing, and live again on the next change");

        var temp = new ThemeElement { Kind = ThemeElementKind.Data, DataName = "CPUTEMP", X = 10, Y = 10, Font = new ThemeFont() };
        var clock = new ThemeElement { Kind = ThemeElementKind.Data, DataName = "TIME", X = 400, Y = 10, Font = new ThemeFont() };
        var status = new ThemeElement { Kind = ThemeElementKind.Data, DataName = "SENSOR_STATUS", X = 800, Y = 10, Font = new ThemeFont() };
        var view = new ThemeView(new ThemeDefinition { Elements = [temp, clock, status] }, Path.GetTempPath(), playVideo: false);
        var blocks = view.Children.OfType<Canvas>().Single().Children.OfType<TextBlock>().ToList();
        view.Update(new ThemeDataContext { SensorFeed = SensorFeedState.Frozen });
        bool dimmed = blocks[0].Opacity == ThemeView.StaleOpacity && blocks[1].Opacity == 1 && blocks[2].Text == "SENSORS PAUSED";
        view.Update(new ThemeDataContext());
        Check(dimmed && blocks[0].Opacity == 1 && blocks[2].Text == "",
            "While the feed is not live, sensor readings dim (the clock does not) and SENSOR_STATUS says so; both clear when it recovers");
        view.Dispose();

        var limits = new SensorSnapshot();
        limits.Values["GpuPowerLimit"] = SensorReader.ParseNumber("Yes"); limits.Values["GpuThermalLimit"] = SensorReader.ParseNumber("No");
        limits.Values["CpuThermalLimit"] = 0; limits.Values["CpuPowerLimit"] = 0;
        var lc = new ThemeDataContext { Sensors = limits };
        var both = new SensorSnapshot(); both.Values["GpuPowerLimit"] = 1; both.Values["GpuThermalLimit"] = 1;
        Check(ThemeData.Read("GPU_LIMIT", null, false, lc) is { Text: "POWER LIMIT", Number: 1 } && ThemeData.Read("CPU_LIMIT", null, false, lc) is { Text: "", Number: 0 } &&
              ThemeData.Read("GPU_LIMIT", null, false, new ThemeDataContext { Sensors = both }).Text == "THERMAL LIMIT" &&
              ThemeData.Read("CPU_LIMIT", null, false, new ThemeDataContext()) is { Text: "", Number: null } && SensorReader.ParseNumber("no") == 0,
            "Limit tags read HWiNFO's Yes / No flags: the first limit on (thermal before power before voltage), blank when none is or none are mapped");
    }

    private static void Sensors()
    {
        var settings = new MonitorSettings();
        settings.Sensors["CpuClock"] = new SensorMapping(); // optional theme sensor left unassigned
        IReadOnlyList<SensorCatalogEntry> Scan(string _) => [];
        var reader = new SensorReader(settings, Scan, (_, slots) => new SensorCatalogEntry?[slots.Count], () => 0);
        Check(reader.Read().ExpectedCount == 12, "Unassigned theme sensors are not counted as missing");
        settings.Sensors["CpuClock"] = new SensorMapping { RegistryValue = "ValueRaw90", MatchMode = "Slot" };
        Check(new SensorReader(settings, Scan, (_, slots) => new SensorCatalogEntry?[slots.Count], () => 0).Read().ExpectedCount == 13,
            "A mapped theme sensor is counted like the dashboard sensors");
    }

    // Optional: imports every L-Connect template installed on this PC. Reports SKIP elsewhere.
    private static void LocalTemplates(string root)
    {
        string folder = @"C:\ProgramData\Lian-Li\L-Connect 3\universal-screen-8.8-inch\template";
        if (!Directory.Exists(folder)) { Console.WriteLine("SKIP THEMES: no local L-Connect templates on this PC"); return; }
        var files = Directory.GetFiles(folder, "*.template");
        string local = Path.Combine(root, "local");
        var failures = new List<string>();
        foreach (var file in files)
        {
            try { LConnectImporter.Import(file, local); }
            catch (Exception ex) { failures.Add(Path.GetFileName(file) + ": " + ex.Message); }
        }
        Check(failures.Count == 0, $"All {files.Length} local L-Connect templates import" + (failures.Count > 0 ? ": " + string.Join("; ", failures) : ""));
    }
}
