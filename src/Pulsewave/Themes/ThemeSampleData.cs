using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Pulsewave.Themes;

/// <summary>Fixed sample readings, like the "88" values L-Connect uses in its theme previews.</summary>
public static class ThemeSampleData
{
    // A violet-to-orange square standing in for album art in previews.
    private static readonly Lazy<BitmapSource> SampleCover = new(() =>
    {
        const int size = 96;
        var pixels = new byte[size * size * 4];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                double t = (x + y) / (2.0 * size);
                int i = (y * size + x) * 4;
                pixels[i] = (byte)(200 - 160 * t); pixels[i + 1] = (byte)(60 + 80 * t); pixels[i + 2] = (byte)(120 + 130 * t); pixels[i + 3] = 255;
            }
        var bitmap = BitmapSource.Create(size, size, 96, 96, PixelFormats.Bgra32, null, pixels, size * 4);
        bitmap.Freeze();
        return bitmap;
    });

    /// <summary>
    /// Preview readings <paramref name="ticksBeforeNow"/> refreshes ago: a realistic, consistent gaming PC (memory text
    /// matches its bar, session peaks are at or above the current temperatures, nothing is near a warning level), so
    /// previews read like a real HUD and mismatches show. Earlier ticks vary loads, FPS and network so charts show a
    /// plausible history (feed ticks from high down to 0).
    /// </summary>
    public static ThemeDataContext Context(int ticksBeforeNow = 0) => Build(ticksBeforeNow, new()
    {
        ["CpuTemp"] = 71, ["CpuLoad"] = Wave(ticksBeforeNow, 46, 30, 0.31), ["CpuPower"] = 142, ["CpuFan"] = 1240, ["CpuClock"] = 5200,
        ["GpuTemp"] = 68, ["GpuLoad"] = Wave(ticksBeforeNow, 94, 10, 0.23), ["GpuPower"] = 268, ["GpuFan"] = 1650, ["GpuClock"] = 2790,
        ["GpuFps"] = Wave(ticksBeforeNow, 142, 40, 0.37, 400), ["GpuVram"] = 9830, ["GpuVramLoad"] = 60, ["MemoryLoad"] = 34,
        ["NetDownload"] = Wave(ticksBeforeNow, 4.6, 6, 0.47), ["NetUpload"] = 0.32, ["PumpRpm"] = 2650, ["CaseFan1"] = 940, ["CaseFan2"] = 910,
        ["DriveTemp"] = 41, ["DriveLoad"] = 12, ["DriveUsed"] = 62, ["CpuVoltage"] = 1.212, ["GpuVoltage"] = 0.975
    }, averageFps: 138, memory: (10.8, 32), title: "Midnight City", artist: "M83", game: "FINAL FANTASY VII REBIRTH",
        gaming: (138, 96, 81, 76), city: "San Francisco", shortcuts: SampleShortcuts);

    /// <summary>
    /// The old uniform "88" readings (L-Connect's preview style), for checks that pin exact formatting.
    /// </summary>
    public static ThemeDataContext Uniform(int ticksBeforeNow = 0) => Build(ticksBeforeNow, new()
    {
        ["CpuTemp"] = 88, ["CpuLoad"] = Wave(ticksBeforeNow, 88, 60, 0.31), ["CpuPower"] = 188, ["CpuFan"] = 1888, ["CpuClock"] = 4800,
        ["GpuTemp"] = 88, ["GpuLoad"] = Wave(ticksBeforeNow, 88, 50, 0.23), ["GpuPower"] = 288, ["GpuFan"] = 1888, ["GpuClock"] = 2800,
        ["GpuFps"] = 120, ["GpuVram"] = 8800, ["GpuVramLoad"] = 88, ["MemoryLoad"] = 88,
        ["NetDownload"] = Wave(ticksBeforeNow, 88.8, 80, 0.47) / 100, ["NetUpload"] = 0.888, ["PumpRpm"] = 2888, ["CaseFan1"] = 888, ["CaseFan2"] = 888,
        ["DriveTemp"] = 88, ["DriveLoad"] = 88, ["DriveUsed"] = 88, ["CpuVoltage"] = 1.288, ["GpuVoltage"] = 0.988
    }, averageFps: 118, memory: (10.8, 32), title: "Midnight City (Extended Remix Version)", artist: "M83", game: "FINAL FANTASY VII REBIRTH",
        gaming: (118, 86, 71, 66), city: "San Francisco", shortcuts: SampleShortcuts, timerMinutes: 5);

    /// <summary>
    /// Worst-case readings for the quality gate: full loads, critical temperatures, 5-digit fan speeds, gigabytes per
    /// second, long titles and names, so text that grows still fits and does not collide.
    /// </summary>
    public static ThemeDataContext Stress(int ticksBeforeNow = 0) => Build(ticksBeforeNow, new()
    {
        ["CpuTemp"] = 99, ["CpuLoad"] = 100, ["CpuPower"] = 253, ["CpuFan"] = 12500, ["CpuClock"] = 5900,
        ["GpuTemp"] = 99, ["GpuLoad"] = 100, ["GpuPower"] = 600, ["GpuFan"] = 10500, ["GpuClock"] = 3100,
        ["GpuFps"] = 999, ["GpuVram"] = 32760, ["GpuVramLoad"] = 100, ["MemoryLoad"] = 100,
        ["NetDownload"] = 1250, ["NetUpload"] = 1180, ["PumpRpm"] = 10800, ["CaseFan1"] = 10900, ["CaseFan2"] = 10900,
        ["DriveTemp"] = 99, ["DriveLoad"] = 100, ["DriveUsed"] = 100, ["CpuVoltage"] = 1.588, ["GpuVoltage"] = 1.188
    }, averageFps: 999, memory: (127.9, 128), title: "Midnight City (Extended Remix Version) [Live at the Hollywood Bowl, 2012]",
        artist: "M83, Anthony Gonzalez and the Los Angeles Philharmonic Orchestra", game: "The Elder Scrolls V: Skyrim Special Edition Anniversary Upgrade",
        gaming: (999, 999, 105, 105), city: "Llanfairpwllgwyngyll, Isle of Anglesey", shortcuts: StressShortcuts);

    private static double Wave(int tick, double baseline, double swing, double rate, double max = 100) =>
        tick == 0 ? baseline : Math.Clamp(baseline - swing / 2 + swing * (0.5 + 0.35 * Math.Sin(tick * rate) + 0.15 * Math.Sin(tick * rate * 3.1)), 0, max);

    private static ThemeDataContext Build(int tick, Dictionary<string, double> values, double averageFps, (double UsedGb, double TotalGb) memory,
        string title, string artist, string game, (double Avg, double Low, double PeakCpu, double PeakGpu) gaming, string city, IReadOnlyList<ShortcutView?> shortcuts, int timerMinutes = 25)
    {
        var sensors = new SensorSnapshot();
        foreach (var (key, value) in values) sensors.Values[key] = value;
        return new ThemeDataContext
        {
            Sensors = sensors, Now = new DateTime(2020, 12, 31, 12, 0, 0),
            CpuName = "Intel Core Ultra 7 265K", GpuName = "NVIDIA GeForce RTX 5070 Ti", AverageFps = averageFps,
            Media = new NowPlaying(MediaPlayback.Playing, title, artist, "Sample", SampleCover.Value) { Album = title.Length > 40 ? "Hurry Up, We're Dreaming (Deluxe Remastered 10th Anniversary Edition)" : "Hurry Up, We're Dreaming" },
            DriveSpace = _ => title.Length > 40 ? (12.4, 8001) : (412, 1863),
            TimerRunning = true, TimerPhase = "FOCUS",
            Memory = (memory.UsedGb * 1024, (memory.TotalGb - memory.UsedGb) * 1024, memory.TotalGb * 1024),
            MediaPosition = (new TimeSpan(0, 2, 41), new TimeSpan(0, 4, 3)),
            Gaming = new GamingSnapshot(game, true, new TimeSpan(1, 24, 10), gaming.Avg, gaming.Low, gaming.PeakCpu, gaming.PeakGpu, new TimeSpan(2, 5, 0), new TimeSpan(9, 40, 0),
                shortcuts.Count > 0 ? shortcuts[0] : null,
                [(game, new TimeSpan(5, 10, 0)), ("Stellar Blade", new TimeSpan(2, 45, 0)), ("Lumines Remastered", new TimeSpan(1, 45, 0))],
                [new("steam:1", "Stellar Blade", new DateTime(2020, 12, 30, 21, 0, 0), new DateTime(2020, 12, 30, 22, 40, 0), 104, 79, 69, 64)]),
            Shortcuts = shortcuts,
            Volume = (64, false), Timer = TimeSpan.FromMinutes(timerMinutes), Stopwatch = new TimeSpan(0, 12, 34),
            Weather = new WeatherReport(city, 18, 16, 2, true, 21, 12, new DateTime(2020, 12, 31, 7, 25, 0), new DateTime(2020, 12, 31, 17, 1, 0),
                [new(new DateTime(2020, 12, 31), 2, 21, 12), new(new DateTime(2021, 1, 1), 61, 17, 11), new(new DateTime(2021, 1, 2), 0, 23, 13)], SampleNow.DateTime)
            {
                // Clearing this afternoon, showers from 18:00 (the sample clock reads 12:00).
                Hours = Enumerable.Range(0, 24).Select(h => new WeatherHour(new DateTime(2020, 12, 31, 12, 0, 0).AddHours(h),
                    h is >= 6 and < 10 ? 61 : h < 3 ? 2 : h < 6 ? 1 : 3, 18 + (h < 4 ? h * 0.5 : 2 - (h - 4) * 0.4), h is >= 6 and < 10 ? 70 : 10, h < 5 || h > 19)).ToList()
            },
            Usage = new AiUsage(new(42, SampleNow.AddMinutes(134)), new(61, SampleNow.AddDays(3).AddHours(6)),
                new(18, SampleNow.AddMinutes(212)), new(35, SampleNow.AddDays(5).AddHours(2)))
        };
    }
    private static readonly DateTimeOffset SampleNow = new(new DateTime(2020, 12, 31, 12, 0, 0));

    /// <summary>Stand-in apps: a glyph on a colorful gradient square each, like real app icons, so previews show tinted tiles.</summary>
    public static readonly IReadOnlyList<ShortcutView?> SampleShortcuts;

    /// <summary>The same apps with very long names, for the quality gate.</summary>
    public static readonly IReadOnlyList<ShortcutView?> StressShortcuts;

    private static readonly IReadOnlyList<ShortcutView> Glyphs =
    [
        new("Game", null, ""), new("Browser", null, ""), new("Music", null, ""), new("Editor", null, ""),
        new("Terminal", null, ""), new("Chat", null, ""), new("Photos", null, ""), new("Mail", null, ""),
        new("Files", null, ""), new("Videos", null, ""), new("Notes", null, ""), new("Settings", null, "")
    ];

    static ThemeSampleData()
    {
        // Drawing needs an STA thread; whoever asks first may not be on one.
        // The drawing runs in SampleIcons, which has no static state: a thread started from here must not wait on this class.
        var glyphs = Glyphs;
        List<ShortcutView?> Make() => glyphs.Select((g, i) => (ShortcutView?)SampleIcons.Colorful(g, i)).ToList();
        List<ShortcutView?> icons = [];
        if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA) icons = Make();
        else
        {
            var thread = new Thread(() => icons = Make());
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
        }
        SampleShortcuts = icons;
        StressShortcuts = icons.Select(s => (ShortcutView?)(s! with { Name = s.Name + " Ultimate Collector's Edition" })).ToList();
    }

}

internal static class SampleIcons
{
    /// <summary>A rounded square in one of twelve hues with the glyph in white, as a frozen 96 px icon.</summary>
    public static ShortcutView Colorful(ShortcutView glyph, int index)
    {
        var hue = System.Windows.Media.Color.FromRgb(0, 0, 0);
        string[] hues = ["#FF4F7BFF", "#FF22B8CF", "#FFFF5A7A", "#FFFFA62B", "#FF3CCB7F", "#FF8B5CF6", "#FFE84393", "#FF00A8E8", "#FFF6C12B", "#FFEF4444", "#FF14B8A6", "#FF64748B"];
        hue = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hues[index % hues.Length]);
        const int size = 96;
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            var light = System.Windows.Media.Color.FromRgb((byte)Math.Min(255, hue.R + 60), (byte)Math.Min(255, hue.G + 60), (byte)Math.Min(255, hue.B + 60));
            var fill = new LinearGradientBrush(light, hue, 90);
            dc.DrawRoundedRectangle(fill, null, new System.Windows.Rect(0, 0, size, size), 22, 22);
            var text = new FormattedText(glyph.Glyph, System.Globalization.CultureInfo.InvariantCulture, System.Windows.FlowDirection.LeftToRight,
                new Typeface("Segoe Fluent Icons"), 46, System.Windows.Media.Brushes.White, 1.0);
            dc.DrawText(text, new System.Windows.Point((size - text.Width) / 2, (size - text.Height) / 2));
        }
        var bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return glyph with { Icon = bitmap, IconSolid = true, Accent = hue };
    }
}