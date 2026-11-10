using Pulsewave.Themes;

namespace Pulsewave.Regression;

// Source of the Lumen built-in templates: light on near-black, for the 8.8" bar (40-lumen, 1920x480) and the HYTE Y70
// column (41-lumen-column, 682x2560), in three versions like Halo:
//   Lumen (40, 41): all-round. Clock and weather, CPU and GPU, memory and network, now playing, quick toggles and
//     (on the column) the dock and the last game.
//   Lumen Gaming (42, 43): live FPS as the hero with frame time, the game and its session, CPU and GPU with what is
//     holding them back, memory and VRAM; follows the Gaming shortcut list.
//   Lumen Productivity (44, 45): the timer on an instrument dial with Pomodoro, Claude Code and Codex limits, a
//     compact system card with the drive, a now-playing strip; follows the Productivity shortcut list.
// The kit: an aurora of three soft glows on black; see-through glass slabs with a hairline edge and a highlight along
// the top; instrument gauges (a ring of ticks, a glowing gradient arc, the reading in the middle); DIN numerals
// (Bahnschrift) with Segoe UI for running text; letter-spaced small-caps labels; charts that fade out downward;
// segmented meters. Cyan is the CPU and the main accent, magenta the GPU, violet memory.
// Regenerate with `--widget-shots <dir> --write-oneui-templates <dir>` (it writes these too).
internal static class LumenTemplates
{
    private const string Text = "#FFFFFFFF", Soft = "#FFC9D1E0", Dim = "#FFA3ADC2", Cyan = "#FF4DE8FF", Blue = "#FF5B8CFF", Violet = "#FFA78BFF",
        Magenta = "#FFFF5CCB", Warm = "#FFFFC56B", Track = "#1FFFFFFF", Edge = "#26FFFFFF", Button = "#1CFFFFFF", OnText = "#FF04121A";
    private const string Ui = "Segoe UI", Din = "Bahnschrift", DinLight = "Bahnschrift Light", DinSemi = "Bahnschrift SemiBold",
        Icons = "Segoe Fluent Icons", Symbols = "Segoe UI Symbol";
    private const string PinGlyph = "", PrevGlyph = "", NextGlyph = "", LightGlyph = "", PaletteGlyph = "",
        AppsGlyph = "", MixerGlyph = "", MusicGlyph = "";

    private const double Margin = 24, Pad = 28, Radius = 34;

    private static readonly (string Glyph, string Label, ThemeAction Action)[] GamingToggles =
        [(LightGlyph, "Lights", ThemeAction.LightsPage), (PaletteGlyph, "Colors", ThemeAction.PalettePage), (MixerGlyph, "Volume", ThemeAction.VolumeMixer), (MusicGlyph, "Media", ThemeAction.MediaPage)];
    private static readonly (string Glyph, string Label, ThemeAction Action)[] QuickToggles =
        [(LightGlyph, "Lights", ThemeAction.LightsPage), (PaletteGlyph, "Colors", ThemeAction.PalettePage), (MixerGlyph, "Volume", ThemeAction.VolumeMixer), (AppsGlyph, "Apps", ThemeAction.AppDrawer)];

    // --- Bar ---------------------------------------------------------------------------------

    public static ThemeDefinition Bar()
    {
        var t = New("Lumen", 1920, 480);
        var e = t.Elements;

        HeroBar(e, media: false);

        // CPU and GPU.
        Slab(e, 472, 24, 716, 432, ThemeAction.Details);
        Processor(e, 472 + 179, 42, 228, 302, "CPU", "CPUMODEL", "CPULOAD", "CPUTEMP", "CPU_LIMIT", Cyan, Blue, ("POWER", "CPUPWR"), ("CLOCK", "CPUCLOCK_G"), 456 - 22);
        e.Add(new ThemeElement { Kind = ThemeElementKind.Line, X = 830, Y = 60, Width = 0, Height = 360, LineColor = "#14FFFFFF", LineWidth = 1 });
        Processor(e, 472 + 537, 42, 228, 302, "GPU", "GPUMODEL", "GPULOAD", "GPUTEMP", "GPU_LIMIT", Magenta, Violet, ("POWER", "GPUPWR"), ("VRAM", "GPURAM_GB_OF"), 456 - 22);

        // Frames, memory and network.
        Slab(e, 1212, 24, 300, 432, ThemeAction.GamingPage);
        Frames(e, 1240, 48, 244, 58, 22, 56);
        e.Add(Hairline(1240, 280, 244));
        Meter(e, 1240, 296, 244, "MEMORY", "RAMLOAD", "RAM_GB_OF", Violet);
        NetworkRows(e, 1240, 370, 244);

        // Now playing and the quick toggles.
        Slab(e, 1536, 24, 360, 432);
        Art(e, 1564, 50, 112);
        e.Add(Label(1694, 54, "NOW PLAYING", Cyan));
        e.Add(Lines(Value(1694, 76, "MEDIA_TITLE", Font(16, Text, bold: true), unit: false, width: 174, action: ThemeAction.MediaPage), 2));
        e.Add(Value(1694, 134, "MEDIA_ARTIST", Font(13, Soft), unit: false, width: 174, action: ThemeAction.MediaPage));
        Progress(e, 1564, 178, 304);
        Transport(e, 1716, 218, 52, 68, 92);
        e.Add(Hairline(1564, 304, 304));
        Toggles(e, 1564, 320, 304, 56, QuickToggles);
        return t;
    }

    // --- Column ------------------------------------------------------------------------------

    public static ThemeDefinition Column()
    {
        var t = New("Lumen Column", 682, 2560);
        var e = t.Elements;
        const double x = Margin, w = 682 - 2 * Margin, ix = x + Pad, iw = w - 2 * Pad, right = 682 - Margin - Pad;

        HeroColumn(e);

        // The next hours.
        Slab(e, x, 560, w, 212);
        Hours(e, ix, 584, iw, 30);
        e.Add(Value(ix, 728, "WEATHER_RAIN", Font(14, Soft), unit: false, width: iw - 150));
        e.Add(Value(right, 728, "WEATHER_SUN_NEXT", Num(14, Soft, ThemeTextAlignment.Right), unit: false));
        e.Add(Value(right - 58, 731, "WEATHER_SUN_NEXT_LABEL", Font(11, Dim, align: ThemeTextAlignment.Right, family: DinSemi), unit: false));

        SystemColumn(e, 796, ("VRAM", "GPURAM_GB_OF"));

        // Now playing.
        Slab(e, x, 1488, w, 470);
        Art(e, ix, 1516, 196);
        double tx = ix + 196 + 24, tw = iw - 196 - 24;
        e.Add(Label(tx, 1524, "NOW PLAYING", Cyan));
        e.Add(Lines(Value(tx, 1550, "MEDIA_TITLE", Font(21, Text, bold: true), unit: false, width: tw, action: ThemeAction.MediaPage), 2));
        e.Add(Value(tx, 1626, "MEDIA_ARTIST", Font(16, Soft), unit: false, width: tw, action: ThemeAction.MediaPage));
        e.Add(Value(tx, 1656, "MEDIA_ALBUM", Font(14, Dim), unit: false, width: tw, action: ThemeAction.MediaPage));
        Progress(e, ix, 1736, iw);
        Transport(e, 341, 1784, 68, 88, 150);
        Volume(e, ix, 1894, iw);

        // Quick toggles and two rows of favourites.
        Slab(e, x, 1982, w, 408);
        Toggles(e, ix, 2006, iw, 64, QuickToggles);
        e.Add(Hairline(ix, 2116, iw));
        Dock(e, ix, 2134, iw, 232, 4, 2);

        // The game being played, or the last one.
        Slab(e, x, 2414, w, 122, ThemeAction.GamingPage);
        e.Add(Value(ix, 2436, "GAME_STATE", Font(12, Magenta, family: DinSemi), unit: false, action: ThemeAction.GamingPage));
        e.Add(Value(ix, 2460, "GAME_NAME", Font(18, Text, bold: true), unit: false, width: iw - 250, action: ThemeAction.GamingPage));
        e.Add(Value(ix, 2494, "GAME_SESSION", Font(13, Soft), unit: false, width: iw - 250, format: "short"));
        Stat(e, right - 110, 2440, "TODAY", "GAME_TODAY", 20, ThemeTextAlignment.Right);
        Stat(e, right, 2440, "THIS WEEK", "GAME_WEEK", 20, ThemeTextAlignment.Right);
        return t;
    }

    // --- Gaming ------------------------------------------------------------------------------

    public static ThemeDefinition GamingBar()
    {
        var t = New("Lumen Gaming", 1920, 480);
        t.ShortcutList = ShortcutLists.Gaming;
        var e = t.Elements;

        // The game: its cover, the session beside it, its name and the session's peak temperatures.
        Slab(e, 24, 24, 320, 432, ThemeAction.GamingPage);
        e.Add(Cover(48, 48, 132, 198));
        Stat(e, 200, 52, "SESSION", "GAME_SESSION", 20).DataFormat = "short";
        Stat(e, 200, 120, "TODAY", "GAME_TODAY", 20);
        Stat(e, 200, 188, "THIS WEEK", "GAME_WEEK", 20);
        e.Add(Value(48, 264, "GAME_STATE", Font(11, Magenta, family: DinSemi), unit: false, action: ThemeAction.GamingPage));
        e.Add(Lines(Value(48, 284, "GAME_NAME", Font(16, Text, bold: true), unit: false, width: 272, action: ThemeAction.GamingPage), 2));
        e.Add(Hairline(48, 354, 272));
        Stat(e, 48, 370, "PEAK CPU", "GAME_PEAK_CPU", 20);
        Stat(e, 184, 370, "PEAK GPU", "GAME_PEAK_GPU", 20);

        // Live FPS stands free on the aurora, with frame time and the clock under it.
        FpsHero(e, 376, 30, 800, 122, 26);
        FrameTime(e, 376, 240, 424, 68);
        e.Add(Hairline(376, 352, 424));
        e.Add(Value(376, 366, "TIME", Font(44, Text, family: DinLight)));
        e.Add(Value(800, 394, "DATE", Font(16, Soft, align: ThemeTextAlignment.Right), format: "long"));

        // CPU and GPU.
        Slab(e, 824, 24, 716, 432, ThemeAction.Details);
        Processor(e, 824 + 179, 42, 228, 302, "CPU", "CPUMODEL", "CPULOAD", "CPUTEMP", "CPU_LIMIT", Cyan, Blue, ("POWER", "CPUPWR"), ("CLOCK", "CPUCLOCK_G"), 456 - 22);
        e.Add(new ThemeElement { Kind = ThemeElementKind.Line, X = 1182, Y = 60, Width = 0, Height = 360, LineColor = "#14FFFFFF", LineWidth = 1 });
        Processor(e, 824 + 537, 42, 228, 302, "GPU", "GPUMODEL", "GPULOAD", "GPUTEMP", "GPU_LIMIT", Magenta, Violet, ("POWER", "GPUPWR"), ("CLOCK", "GPUCLOCK_G"), 456 - 22);

        // Memory, network, what is playing and the toggles.
        Slab(e, 1564, 24, 332, 432);
        Meter(e, 1592, 46, 276, "MEMORY", "RAMLOAD", "RAM_GB_OF", Violet);
        Meter(e, 1592, 106, 276, "VRAM", "GPURAMLOAD", "GPURAM_GB_OF", Magenta);
        NetworkRows(e, 1592, 168, 276);
        e.Add(Hairline(1592, 242, 276));
        MediaStrip(e, 1592, 256, 276, 52);
        e.Add(Hairline(1592, 322, 276));
        Toggles(e, 1592, 338, 276, 52, GamingToggles);
        return t;
    }

    public static ThemeDefinition GamingColumn()
    {
        var t = New("Lumen Gaming Column", 682, 2560);
        t.ShortcutList = ShortcutLists.Gaming;
        var e = t.Elements;
        const double x = Margin, w = 682 - 2 * Margin, ix = x + Pad, iw = w - 2 * Pad, right = 682 - Margin - Pad;

        // The clock on one line, small: the game is the hero here.
        e.Add(Value(323, 18, "TIME", Font(92, Text, align: ThemeTextAlignment.Right, family: DinLight), format: "h"));
        e.Add(Glowing(Txt(341, 10, ":", Font(92, Cyan, align: ThemeTextAlignment.Center, family: DinLight)), Cyan, 18));
        e.Add(Glowing(Value(359, 18, "TIME", Font(92, Cyan, family: DinLight), format: "m"), Cyan, 24));
        e.Add(Value(341, 164, "DATE", Font(16, Soft, align: ThemeTextAlignment.Center), format: "long"));

        // The game's cover with its name and live FPS beside it, free on the aurora.
        e.Add(Cover(40, 232, 200, 300));
        e.Add(Value(268, 236, "GAME_STATE", Font(12, Magenta, family: DinSemi), unit: false, action: ThemeAction.GamingPage));
        e.Add(Lines(Value(268, 258, "GAME_NAME", Font(19, Text, bold: true), unit: false, width: right - 268, action: ThemeAction.GamingPage), 2));
        FpsHero(e, 268, 344, right, 104, 24);

        // Frame time and the session.
        Slab(e, x, 560, w, 238, ThemeAction.GamingPage);
        FrameTime(e, ix, 584, iw, 84);
        e.Add(Hairline(ix, 710, iw));
        SessionRow(e, ix, 726, iw, 17);

        SystemColumn(e, 822, ("CLOCK", "GPUCLOCK_G"));

        // What is playing.
        Slab(e, x, 1514, w, 120);
        MediaStrip(e, ix, 1538, iw, 72);

        // Quick toggles and three rows of games.
        Slab(e, x, 1658, w, 590);
        Toggles(e, ix, 1682, iw, 64, GamingToggles);
        e.Add(Hairline(ix, 1792, iw));
        Dock(e, ix, 1810, iw, 414, 4, 3);

        // Video memory, the drive and the volume.
        Slab(e, x, 2272, w, 264);
        Meter(e, ix, 2296, iw, "VRAM", "GPURAMLOAD", "GPURAM_GB_OF", Magenta);
        Meter(e, ix, 2360, iw, "DRIVE", "DRIVE_USED_PCT", "DRIVE_USED_OF", Cyan);
        e.Add(Hairline(ix, 2430, iw));
        Volume(e, ix, 2450, iw);
        return t;
    }

    // --- Productivity ------------------------------------------------------------------------

    public static ThemeDefinition WorkBar()
    {
        var t = New("Lumen Productivity", 1920, 480);
        t.ShortcutList = ShortcutLists.Productivity;
        var e = t.Elements;

        HeroBar(e, media: true);

        // The timer on its dial, with its buttons beside and the Pomodoro presets under it.
        Slab(e, 476, 24, 424, 432);
        Dial(e, 618, 44, 236, 40);
        e.AddRange(Pill(500, 296, 236, 40, null, ThemeAction.FocusSwitch, "FOCUS_SWITCH"));
        e.Add(Label(618, 352, "POMODORO", Dim, ThemeTextAlignment.Center));
        e.AddRange(Pill(500, 376, 112, 44, "25 / 5", ThemeAction.Pomodoro25));
        e.AddRange(Pill(624, 376, 112, 44, "50 / 10", ThemeAction.Pomodoro50));
        e.AddRange(Pill(760, 64, 116, 76, null, ThemeAction.FocusToggle, "FOCUS_ACTION", accent: true));
        e.AddRange(Pill(760, 156, 116, 56, "Reset", ThemeAction.FocusReset));
        e.AddRange(Pill(760, 224, 116, 56, "+1 min", ThemeAction.TimerAddMinute));

        // Claude Code and Codex limits.
        Slab(e, 924, 24, 480, 432);
        e.Add(Label(952, 50, "AI USAGE", Dim));
        Usage(e, 952, 82, 424, "Claude Code", "CLAUDE", Cyan);
        e.Add(Hairline(952, 256, 424));
        Usage(e, 952, 272, 424, "Codex", "CODEX", Magenta);

        // System: two small gauges, memory and the drive, the network and the toggles.
        Slab(e, 1428, 24, 468, 432, ThemeAction.Details);
        Gauge(e, 1456 + 103, 40, 148, "CPU", "CPULOAD", "CPUTEMP", Cyan, Blue);
        Gauge(e, 1456 + 309, 40, 148, "GPU", "GPULOAD", "GPUTEMP", Magenta, Violet);
        Meter(e, 1456, 196, 412, "MEMORY", "RAMLOAD", "RAM_GB_OF", Violet);
        Meter(e, 1456, 254, 412, "DRIVE", "DRIVE_USED_PCT", "DRIVE_USED_OF", Cyan);
        Network(e, 1456, 316, 412, 18);
        Toggles(e, 1456, 384, 412, 48, QuickToggles, labels: false);
        return t;
    }

    public static ThemeDefinition WorkColumn()
    {
        var t = New("Lumen Productivity Column", 682, 2560);
        t.ShortcutList = ShortcutLists.Productivity;
        var e = t.Elements;
        const double x = Margin, w = 682 - 2 * Margin, ix = x + Pad, iw = w - 2 * Pad;

        HeroColumn(e);

        // The timer on its dial with its buttons and the Pomodoro presets.
        Slab(e, x, 560, w, 486);
        Dial(e, 341, 584, 300, 58);
        e.AddRange(Pill(84, 906, 190, 60, null, ThemeAction.FocusToggle, "FOCUS_ACTION", accent: true));
        e.AddRange(Pill(286, 906, 150, 60, "Reset", ThemeAction.FocusReset));
        e.AddRange(Pill(448, 906, 150, 60, "+1 min", ThemeAction.TimerAddMinute));
        e.AddRange(Pill(84, 978, 190, 46, null, ThemeAction.FocusSwitch, "FOCUS_SWITCH"));
        e.AddRange(Pill(286, 978, 150, 46, "25 / 5", ThemeAction.Pomodoro25));
        e.AddRange(Pill(448, 978, 150, 46, "50 / 10", ThemeAction.Pomodoro50));

        // Claude Code and Codex limits.
        Slab(e, x, 1070, w, 412);
        e.Add(Label(ix, 1096, "AI USAGE", Dim));
        Usage(e, ix, 1128, iw, "Claude Code", "CLAUDE", Cyan);
        e.Add(Hairline(ix, 1298, iw));
        Usage(e, ix, 1314, iw, "Codex", "CODEX", Magenta);

        // System: CPU and GPU gauges, memory and the drive, the network.
        Slab(e, x, 1506, w, 494, ThemeAction.Details);
        e.Add(Label(ix, 1532, "SYSTEM", Dim));
        e.Add(Value(682 - Margin - Pad, 1530, "SENSOR_STATUS", Font(12, Warm, align: ThemeTextAlignment.Right, family: DinSemi), unit: false));
        Gauge(e, ix + iw / 4, 1560, 220, "CPU", "CPULOAD", "CPUTEMP", Cyan, Blue);
        Gauge(e, ix + iw * 3 / 4, 1560, 220, "GPU", "GPULOAD", "GPUTEMP", Magenta, Violet);
        Meter(e, ix, 1796, iw, "MEMORY", "RAMLOAD", "RAM_GB_OF", Violet);
        Meter(e, ix, 1858, iw, "DRIVE", "DRIVE_USED_PCT", "DRIVE_USED_OF", Cyan);
        Network(e, ix, 1926, iw, 22);

        // What is playing.
        Slab(e, x, 2024, w, 116);
        MediaStrip(e, ix, 2046, iw, 72);

        // Quick toggles and two rows of apps.
        Slab(e, x, 2164, w, 372);
        Toggles(e, ix, 2186, iw, 64, QuickToggles);
        e.Add(Hairline(ix, 2290, iw));
        Dock(e, ix, 2306, iw, 206, 4, 2);
        return t;
    }

    // --- Kit ---------------------------------------------------------------------------------

    private static ThemeDefinition New(string name, int w, int h)
    {
        var t = new ThemeDefinition { Name = name, Source = "Pulsewave template", Width = w, Height = h, BackgroundColor = "#FF04050A" };
        var e = t.Elements;
        e.Add(new ThemeElement { Kind = ThemeElementKind.Panel, Width = w, Height = h, FillColor = "#FF070912", GradientColor = "#FF030408", GradientAngle = 90 });
        // The aurora: cyan where the clock is, violet through the middle, magenta at the far end.
        bool tall = h > w;
        var glows = tall
            ? new (string Color, double X, double Y, double R)[] { ("#3D12A8E0", 0.1, 0.07, 0.3), ("#336A45F0", 1.0, 0.36, 0.3), ("#2EE0409C", 0.0, 0.68, 0.3), ("#2412A8E0", 0.95, 0.97, 0.24) }
            : [("#3D12A8E0", 0.1, 0.1, 0.3), ("#336A45F0", 0.5, 1.1, 0.3), ("#2EE0409C", 0.94, 0.0, 0.28)];
        foreach (var (color, gx, gy, gr) in glows)
        {
            double r = Math.Max(w, h) * gr;
            e.Add(new ThemeElement { Kind = ThemeElementKind.Panel, X = w * gx - r, Y = h * gy - r, Width = 2 * r, Height = 2 * r, CornerRadius = r,
                FillColor = color, GradientColor = "#00" + color[3..], GradientStyle = "Radial" });
        }
        return t;
    }

    /// <summary>The bar's free-standing hero: the clock (minutes in glowing cyan), the date and the weather; with <paramref name="media"/> a now-playing strip takes the place of the city, high, low and sun.</summary>
    private static void HeroBar(List<ThemeElement> e, bool media)
    {
        e.Add(Value(214, 14, "TIME", Font(118, Text, align: ThemeTextAlignment.Right, family: DinLight), format: "h"));
        e.Add(Glowing(Txt(232, 4, ":", Font(118, Cyan, align: ThemeTextAlignment.Center, family: DinLight)), Cyan, 20));
        e.Add(Glowing(Value(250, 14, "TIME", Font(118, Cyan, family: DinLight), format: "m"), Cyan, 26));
        e.Add(Value(232, 196, "DATE", Font(18, Soft, align: ThemeTextAlignment.Center), format: "long"));
        e.Add(Hairline(92, 244, 280));
        e.Add(Symbol(Value(44, 262, "WEATHER_ICON", Font(38, Warm, family: Symbols), unit: false), "WEATHER_KIND"));
        e.Add(Value(124, 258, "WEATHER_TEMP", Num(42, Text)));
        e.Add(Value(428, 268, "WEATHER_TEXT", Font(16, Text, bold: true, ThemeTextAlignment.Right), unit: false, width: 196));
        e.Add(Value(428, 298, "WEATHER_FEELS_NOTE", Font(14, Soft, align: ThemeTextAlignment.Right), unit: false));
        if (media)
        {
            e.Add(Hairline(44, 350, 384));
            MediaStrip(e, 44, 374, 384, 56);
            return;
        }
        e.Add(Txt(44, 356, PinGlyph, Font(12, Dim, family: Icons)));
        e.Add(Value(66, 353, "WEATHER_CITY", Font(13, Dim, bold: true), unit: false, width: 362));
        Stat(e, 44, 392, "HIGH", "WEATHER_HIGH", 20);
        Stat(e, 144, 392, "LOW", "WEATHER_LOW", 20);
        e.Add(Value(428, 392, "WEATHER_SUN_NEXT_LABEL", Font(11, Dim, align: ThemeTextAlignment.Right, family: DinSemi), unit: false));
        e.Add(Value(428, 392 + 11 * 1.45, "WEATHER_SUN_NEXT", Num(20, Text, ThemeTextAlignment.Right), unit: false));
    }

    /// <summary>The column's hero: hours over minutes, large, on the left; the day and the weather beside them.</summary>
    private static void HeroColumn(List<ThemeElement> e)
    {
        const double right = 682 - Margin - Pad;
        e.Add(Value(40, 22, "TIME", Font(204, Text, family: DinLight), format: "h"));
        e.Add(Glowing(Value(40, 250, "TIME", Font(204, Cyan, family: DinLight), format: "m"), Cyan, 34));
        e.Add(Value(right, 78, "DATE", Font(17, Soft, align: ThemeTextAlignment.Right), format: "long"));
        e.Add(Hairline(right - 200, 122, 200, fadeLeft: true));
        e.Add(Symbol(Value(right, 144, "WEATHER_ICON", Font(46, Warm, align: ThemeTextAlignment.Right, family: Symbols), unit: false), "WEATHER_KIND"));
        e.Add(Value(right, 226, "WEATHER_TEMP", Num(60, Text, ThemeTextAlignment.Right)));
        e.Add(Value(right, 330, "WEATHER_TEXT", Font(17, Text, bold: true, ThemeTextAlignment.Right), unit: false, width: 250));
        e.Add(Value(right, 362, "WEATHER_FEELS_NOTE", Font(15, Soft, align: ThemeTextAlignment.Right), unit: false));
        Stat(e, right - 96, 412, "HIGH", "WEATHER_HIGH", 22, ThemeTextAlignment.Right);
        Stat(e, right, 412, "LOW", "WEATHER_LOW", 22, ThemeTextAlignment.Right);
        e.Add(Value(right, 488, "WEATHER_CITY", Font(13, Dim, bold: true, ThemeTextAlignment.Right), unit: false, width: 250));
    }

    /// <summary>The column's system slab (668 tall): CPU and GPU gauges with their readings and history, memory and network.</summary>
    private static void SystemColumn(List<ThemeElement> e, double y, (string Label, string Data) gpu)
    {
        const double x = Margin, w = 682 - 2 * Margin, ix = x + Pad, iw = w - 2 * Pad, right = 682 - Margin - Pad;
        Slab(e, x, y, w, 668, ThemeAction.Details);
        e.Add(Label(ix, y + 26, "SYSTEM", Dim));
        e.Add(Value(right, y + 24, "SENSOR_STATUS", Font(12, Warm, align: ThemeTextAlignment.Right, family: DinSemi), unit: false));
        Processor(e, ix + iw / 4 - 4, y + 60, 264, 278, "CPU", "CPUMODEL", "CPULOAD", "CPUTEMP", "CPU_LIMIT", Cyan, Blue, ("POWER", "CPUPWR"), ("CLOCK", "CPUCLOCK_G"), y + 504);
        Processor(e, ix + iw * 3 / 4 + 4, y + 60, 264, 278, "GPU", "GPUMODEL", "GPULOAD", "GPUTEMP", "GPU_LIMIT", Magenta, Violet, ("POWER", "GPUPWR"), gpu, y + 504);
        e.Add(Hairline(ix, y + 522, iw));
        Meter(e, ix, y + 540, iw, "MEMORY", "RAMLOAD", "RAM_GB_OF", Violet);
        Network(e, ix, y + 608, iw, 22);
    }

    /// <summary>The session: its length, today, this week, and the session's peak temperatures.</summary>
    private static void SessionRow(List<ThemeElement> e, double x, double y, double w, double size)
    {
        double step = w / 5;
        Stat(e, x, y, "SESSION", "GAME_SESSION", size).DataFormat = "short";
        Stat(e, x + step, y, "TODAY", "GAME_TODAY", size);
        Stat(e, x + step * 2, y, "THIS WEEK", "GAME_WEEK", size);
        Stat(e, x + step * 3, y, "PEAK CPU", "GAME_PEAK_CPU", size);
        Stat(e, x + step * 4, y, "PEAK GPU", "GAME_PEAK_GPU", size);
    }

    /// <summary>The game's cover (or its icon) as a tall tile with a magenta light behind it; opens the gaming page.</summary>
    private static ThemeElement Cover(double x, double y, double w, double h) =>
        new() { Kind = ThemeElementKind.Shortcut, Slot = ThemeDataContext.GameSlot, X = x, Y = y, Width = w, Height = h, CornerRadius = 24,
            FillColor = Button, BorderColor = Edge, BorderWidth = 1, LineColor = Soft, Action = ThemeAction.GamingPage, GlowColor = "#59FF5CCB", GlowSize = 40 };

    /// <summary>Frame time: the current value and its last minute, with a dashed 60 FPS line and amber above 33 ms.</summary>
    private static void FrameTime(List<ThemeElement> e, double x, double y, double w, double h)
    {
        e.Add(Label(x, y, "FRAME TIME", Dim));
        e.Add(Value(x + w, y - 3, "FRAMETIME", Num(13, Soft, ThemeTextAlignment.Right)));
        e.Add(new ThemeElement { Kind = ThemeElementKind.Chart, DataName = "FRAMETIME", X = x, Y = y + 24, Width = w, Height = h, Samples = 60, Maximum = 0,
            MinimumScale = 12, GuideAt = 16.7, GuideLabel = "60 FPS", WarnAt = 33, LineColor = Cyan, LineWidth = 2, FillColor = "#594DE8FF", GradientColor = "#004DE8FF",
            Action = ThemeAction.GamingPage });
    }

    /// <summary>
    /// The game's live FPS as the hero number (only while it runs: afterwards the sensor reads the desktop, not the
    /// game), with the session average and 1% low (amber when far below the average) right-aligned beside it.
    /// </summary>
    private static void FpsHero(List<ThemeElement> e, double x, double y, double right, double hero, double small)
    {
        e.Add(Label(x, y, "FPS", Cyan));
        var live = Value(x, y, "GAME_FPS", Font(hero, Text, family: DinLight), unit: false, action: ThemeAction.GamingPage);
        live.GlowColor = "#664DE8FF"; live.GlowSize = 30;
        e.Add(live);
        double step = 11 * 1.45 + small * 4 / 3 * 1.25 + 14;
        Stat(e, right, y + 22, "AVG", "GAME_FPS_AVG", small, ThemeTextAlignment.Right, unit: false);
        var low = Stat(e, right, y + 22 + step, "1% LOW", "GAME_FPS_LOW", small, ThemeTextAlignment.Right, unit: false);
        low.ColorBy = "GAME_FPS_LOW_PCT"; low.LowIsBad = true; low.WarnAt = 60; low.CriticalAt = 40;
    }

    /// <summary>
    /// The timer on an instrument dial: a full ring of ticks with a soft light inside, the mode (TIMER, FOCUS / BREAK or
    /// STOPWATCH), the clock (cyan while it runs; tap: start or pause) and the Pomodoro round.
    /// </summary>
    private static void Dial(List<ThemeElement> e, double cx, double top, double d, double clock)
    {
        double ticks = d - 8, length = Math.PI * ticks;
        e.Add(new ThemeElement { Kind = ThemeElementKind.Arc, X = cx - ticks / 2, Y = top + 4, Width = ticks, Height = ticks, Diameter = ticks, Thickness = 8,
            StartAngle = -90, SweepAngle = 360, SegmentWidth = 2, SegmentGap = length / 60 - 2, BackColor = "#47FFFFFF", ForeColor = "#00FFFFFF" });
        double ring = d - 40;
        e.Add(new ThemeElement { Kind = ThemeElementKind.Panel, X = cx - ring / 2, Y = top + 20, Width = ring, Height = ring, CornerRadius = ring / 2,
            FillColor = "#2E4DE8FF", GradientColor = "#004DE8FF", GradientStyle = "Radial", BorderColor = "#33FFFFFF", BorderWidth = 1.5, Action = ThemeAction.FocusToggle });
        double px = clock * 4 / 3;
        e.Add(Value(cx, top + d * 0.5 - px * 0.56 - 26, "FOCUS_MODE", Font(12, Cyan, align: ThemeTextAlignment.Center, family: DinSemi), unit: false, action: ThemeAction.FocusSwitch));
        var running = Value(cx, top + d * 0.5 - px * 0.56, "FOCUS_CLOCK", Num(clock, Text, ThemeTextAlignment.Center), unit: false, action: ThemeAction.FocusToggle);
        running.ColorBy = "FOCUS_RUNNING"; running.WarnAt = 1; running.WarnColor = Cyan;
        e.Add(running);
        e.Add(Value(cx, top + d * 0.5 + px * 0.6, "FOCUS_ROUND", Font(12, Soft, align: ThemeTextAlignment.Center), unit: false));
    }

    /// <summary>Claude Code or Codex: the 5-hour and weekly limits as segmented meters, each saying when it resets.</summary>
    private static void Usage(List<ThemeElement> e, double x, double y, double w, string title, string prefix, string color)
    {
        e.Add(new ThemeElement { Kind = ThemeElementKind.Panel, X = x, Y = y + 9, Width = 8, Height = 8, CornerRadius = 4, FillColor = color, GlowColor = "#99" + color[3..], GlowSize = 10 });
        e.Add(Txt(x + 18, y, title, Font(15, Text, family: "Segoe UI Semibold")));
        for (int i = 0; i < 2; i++)
        {
            string window = i == 0 ? "_5H" : "_WEEK";
            double ry = y + 34 + i * 62;
            e.Add(Label(x, ry + 4, i == 0 ? "5 HOURS" : "THIS WEEK", Dim));
            e.Add(Value(x + 104, ry, prefix + window + "_RESET", Font(13, Soft), unit: false, width: w - 104 - 64, format: "resets"));
            e.Add(Value(x + w, ry - 2, prefix + window, Num(16, Text, ThemeTextAlignment.Right)));
            e.Add(Warn(new ThemeElement { Kind = ThemeElementKind.Bar, DataName = prefix + window, X = x, Y = ry + 28, Width = w, Height = 12, CornerRadius = 2, SegmentWidth = 5, SegmentGap = 3,
                ForeColor = color, BackColor = Track }, 75, 90));
        }
    }

    /// <summary>A one-line now-playing strip: small art, title and artist, and play / pause.</summary>
    private static void MediaStrip(List<ThemeElement> e, double x, double y, double w, double h)
    {
        e.Add(new ThemeElement { Kind = ThemeElementKind.AlbumArt, X = x, Y = y, Width = h, Height = h, CornerRadius = Math.Round(h * 0.2),
            FillColor = Button, LineColor = Soft, Action = ThemeAction.MediaPage });
        double tx = x + h + 14, tw = w - h - 14 - h - 12;
        double title = Math.Round(h * 0.28), sub = Math.Round(h * 0.23);
        e.Add(Value(tx, y + h * 0.06, "MEDIA_TITLE", Font(title, Text, bold: true), unit: false, width: tw, action: ThemeAction.MediaPage));
        e.Add(Value(tx, y + h * 0.06 + title * 1.6, "MEDIA_ARTIST", Font(sub, Soft), unit: false, width: tw, action: ThemeAction.MediaPage));
        e.AddRange(Play(x + w - h / 2, y, h));
    }

    /// <summary>Play / pause: the one filled button, in the accent gradient with a glow.</summary>
    private static IEnumerable<ThemeElement> Play(double cx, double y, double size)
    {
        yield return new ThemeElement { Kind = ThemeElementKind.Panel, X = cx - size / 2, Y = y, Width = size, Height = size, CornerRadius = size / 2, FillColor = Cyan, GradientColor = Violet,
            GradientAngle = 45, Action = ThemeAction.PlayPause, GlowColor = "#804DE8FF", GlowSize = Math.Round(size * 0.32) };
        double pt = size * 0.3 * 0.75;
        yield return Value(cx, IconTop(y, size, pt), "MEDIA_PLAYPAUSE", Font(pt, OnText, align: ThemeTextAlignment.Center, family: Icons), unit: false, action: ThemeAction.PlayPause);
    }

    /// <summary>A glass pill button; with <paramref name="data"/> its label is a reading (Start or Pause), and <paramref name="accent"/> fills it with the accent gradient.</summary>
    private static IEnumerable<ThemeElement> Pill(double x, double y, double w, double h, string? label, ThemeAction action, string? data = null, bool accent = false)
    {
        yield return accent
            ? new ThemeElement { Kind = ThemeElementKind.Panel, X = x, Y = y, Width = w, Height = h, CornerRadius = h / 2, FillColor = Cyan, GradientColor = Violet, GradientAngle = 0,
                Action = action, GlowColor = "#664DE8FF", GlowSize = 20 }
            : new ThemeElement { Kind = ThemeElementKind.Panel, X = x, Y = y, Width = w, Height = h, CornerRadius = h / 2, FillColor = "#26FFFFFF", GradientColor = "#0DFFFFFF", GradientAngle = 90,
                BorderColor = "#40FFFFFF", BorderWidth = 1, Action = action };
        double pt = Math.Min(15, Math.Round(h * 0.3)), top = y + (h - pt * 4 / 3 * 1.33) / 2;
        var font = Font(pt, accent ? OnText : Text, bold: true, ThemeTextAlignment.Center);
        yield return data != null ? Value(x + w / 2, top, data, font, unit: false, action: action) : Txt(x + w / 2, top, label!, font, action);
    }

    /// <summary>A see-through glass slab: a faint fill, a hairline edge, and a highlight fading out along its top.</summary>
    private static void Slab(List<ThemeElement> e, double x, double y, double w, double h, ThemeAction? action = null)
    {
        e.Add(new ThemeElement { Kind = ThemeElementKind.Panel, X = x, Y = y, Width = w, Height = h, CornerRadius = Radius, FillColor = "#17FFFFFF", GradientColor = "#08FFFFFF",
            GradientAngle = 90, BorderColor = Edge, BorderWidth = 1, Action = action });
        double half = (w - 2 * Radius) / 2;
        e.Add(new ThemeElement { Kind = ThemeElementKind.Panel, X = x + Radius, Y = y, Width = half, Height = 1.5, FillColor = "#00FFFFFF", GradientColor = "#8CFFFFFF", GradientAngle = 0 });
        e.Add(new ThemeElement { Kind = ThemeElementKind.Panel, X = x + Radius + half, Y = y, Width = half, Height = 1.5, FillColor = "#8CFFFFFF", GradientColor = "#00FFFFFF", GradientAngle = 0 });
    }

    /// <summary>A rule that fades out at one end (or at the left).</summary>
    private static ThemeElement Hairline(double x, double y, double w, bool fadeLeft = false) =>
        new() { Kind = ThemeElementKind.Panel, X = x, Y = y, Width = w, Height = 1, FillColor = fadeLeft ? "#00FFFFFF" : "#33FFFFFF", GradientColor = fadeLeft ? "#40FFFFFF" : "#0AFFFFFF", GradientAngle = 0 };

    /// <summary>
    /// The instrument gauge: a ring of ticks, a glowing gradient arc for the load on a faint track, a soft light inside,
    /// and in the middle the name, the load and the temperature (amber and red when it is too hot).
    /// </summary>
    private static void Gauge(List<ThemeElement> e, double cx, double top, double d, string name, string load, string temp, string from, string to)
    {
        double ticks = d - 8, length = Math.PI * ticks * 0.75;
        e.Add(new ThemeElement { Kind = ThemeElementKind.Arc, X = cx - ticks / 2, Y = top + 4, Width = ticks, Height = ticks, Diameter = ticks, Thickness = 8,
            StartAngle = 135, SweepAngle = 270, SegmentWidth = 2, SegmentGap = (length - 2) / 45 - 2, BackColor = "#47FFFFFF", ForeColor = "#00FFFFFF" });
        double thick = Math.Round(d * 0.062), arc = d - 44 - thick;
        double glow = arc - thick - 10;
        e.Add(new ThemeElement { Kind = ThemeElementKind.Panel, X = cx - glow / 2, Y = top + (d - glow) / 2, Width = glow, Height = glow, CornerRadius = glow / 2,
            FillColor = "#30" + from[3..], GradientColor = "#00" + from[3..], GradientStyle = "Radial" });
        e.Add(new ThemeElement { Kind = ThemeElementKind.Arc, DataName = load, X = cx - arc / 2, Y = top + (d - arc) / 2, Width = arc, Height = arc, Diameter = arc, Thickness = thick,
            StartAngle = 135, SweepAngle = 270, RoundCaps = true, ForeColor = from, GradientColor = to, BackColor = Track, GlowColor = "#8C" + from[3..], GlowSize = 22,
            Action = ThemeAction.Details });
        double number = Math.Round(d * 0.2), px = number * 4 / 3;
        e.Add(Label(cx, top + d * 0.5 - px * 0.5 - (d >= 200 ? 30 : 13), name, from, ThemeTextAlignment.Center));
        e.Add(Value(cx, top + d * 0.5 - px * 0.56, load, Num(number, Text, ThemeTextAlignment.Center), action: ThemeAction.Details));
        e.Add(Limited(Value(cx, top + d * 0.5 + px * 0.52, temp, Num(Math.Round(d * 0.082), Soft, ThemeTextAlignment.Center))));
    }

    /// <summary>A gauge with the chip's name, two readings and the load's last minute under it, down to <paramref name="bottom"/>.</summary>
    private static void Processor(List<ThemeElement> e, double cx, double top, double d, double w, string name, string model, string load, string temp, string limit,
        string from, string to, (string Label, string Data) a, (string Label, string Data) b, double bottom)
    {
        Gauge(e, cx, top, d, name, load, temp, from, to);
        double x = cx - w / 2, y = top + d - 12;
        // What is holding it back takes the model's place while it does.
        e.Add(Value(cx, y, model, Font(11, Dim, align: ThemeTextAlignment.Center), unit: false, width: w));
        e.Add(Value(cx, y + 20, limit, Font(11, Warm, align: ThemeTextAlignment.Center, family: DinSemi), unit: false));
        double size = d >= 240 ? 22 : 19, row = y + 42;
        Stat(e, x + 6, row, a.Label, a.Data, size);
        Stat(e, x + w - 6, row, b.Label, b.Data, size, ThemeTextAlignment.Right, unit: b.Data != "GPURAM_GB_OF");
        double chart = row + 11 * 1.45 + size * 4 / 3 * 1.25 + 6;
        e.Add(new ThemeElement { Kind = ThemeElementKind.Chart, DataName = load, X = x, Y = chart, Width = w, Height = bottom - chart, Samples = 60, Maximum = 100,
            LineColor = from, LineWidth = 2, FillColor = "#40" + from[3..], GradientColor = "#00" + from[3..], Action = ThemeAction.Details });
    }

    /// <summary>Live FPS large, the average and 1% low beside it, and frame time with a dashed 60 FPS line.</summary>
    private static void Frames(List<ThemeElement> e, double x, double y, double w, double hero, double small, double chartH)
    {
        e.Add(Label(x, y, "FPS", Cyan));
        e.Add(Value(x, y + 14, "FPS", Num(hero, Text), unit: false));
        Stat(e, x + w, y, "AVG", "FPS_AVG", small, ThemeTextAlignment.Right, unit: false);
        var low = Stat(e, x + w, y + 11 * 1.45 + small * 4 / 3 * 1.25 + 8, "1% LOW", "GAME_FPS_LOW", small, ThemeTextAlignment.Right, unit: false);
        low.ColorBy = "GAME_FPS_LOW_PCT"; low.LowIsBad = true; low.WarnAt = 60; low.CriticalAt = 40;
        double chartY = y + 14 + hero * 4 / 3 * 1.25 + 14;
        e.Add(Label(x, chartY, "FRAME TIME", Dim));
        e.Add(Value(x + w, chartY - 3, "FRAMETIME", Num(13, Soft, ThemeTextAlignment.Right)));
        e.Add(new ThemeElement { Kind = ThemeElementKind.Chart, DataName = "FRAMETIME", X = x, Y = chartY + 24, Width = w, Height = chartH, Samples = 60, Maximum = 0,
            MinimumScale = 12, GuideAt = 16.7, GuideLabel = "60 FPS", WarnAt = 33, LineColor = Cyan, LineWidth = 2, FillColor = "#594DE8FF", GradientColor = "#004DE8FF",
            Action = ThemeAction.GamingPage });
    }

    /// <summary>A labelled segmented meter with its reading on the right; amber and red when nearly full.</summary>
    private static void Meter(List<ThemeElement> e, double x, double y, double w, string name, string load, string detail, string color)
    {
        e.Add(Label(x, y + 2, name, Dim));
        e.Add(Value(x + w, y - 2, detail, Num(15, Text, ThemeTextAlignment.Right), unit: false, width: w - 90));
        e.Add(Warn(new ThemeElement { Kind = ThemeElementKind.Bar, DataName = load, X = x, Y = y + 30, Width = w, Height = 16, CornerRadius = 2, SegmentWidth = 5, SegmentGap = 3,
            ForeColor = color, BackColor = Track, Action = ThemeAction.Details }, 85, 95));
    }

    private static void Network(List<ThemeElement> e, double x, double y, double w, double size)
    {
        Stat(e, x, y, "DOWN", "DOWNDSPEED", size);
        Stat(e, x + w, y, "UP", "UPSPEED", size, ThemeTextAlignment.Right);
    }

    /// <summary>Download and upload as two rows (label left, speed right), where a column is too narrow for both side by side.</summary>
    private static void NetworkRows(List<ThemeElement> e, double x, double y, double w)
    {
        e.Add(Label(x, y + 7, "DOWN", Dim)); e.Add(Value(x + w, y, "DOWNDSPEED", Num(17, Text, ThemeTextAlignment.Right)));
        e.Add(Label(x, y + 39, "UP", Dim)); e.Add(Value(x + w, y + 32, "UPSPEED", Num(17, Text, ThemeTextAlignment.Right)));
    }

    /// <summary>The next four three-hour steps: time, a symbol colored by the weather, temperature.</summary>
    private static void Hours(List<ThemeElement> e, double x, double y, double w, double size)
    {
        double step = w / 4;
        for (int i = 1; i <= 4; i++)
        {
            double cx = x + step * (i - 0.5);
            string h = "WEATHER_H" + i + "_";
            e.Add(Value(cx, y, h + "TIME", Font(12, Dim, align: ThemeTextAlignment.Center, family: DinSemi), unit: false));
            e.Add(Symbol(Value(cx, y + 22, h + "ICON", Font(size, Warm, align: ThemeTextAlignment.Center, family: Symbols), unit: false), h + "KIND"));
            e.Add(Value(cx, y + 22 + size * 4 / 3 * 1.3, h + "TEMP", Num(20, Text, ThemeTextAlignment.Center)));
        }
    }

    /// <summary>The weather symbol's color from the conditions: warm for sun, grey for cloud, fog or night, cyan for rain and snow.</summary>
    private static ThemeElement Symbol(ThemeElement value, string kind)
    {
        value.ColorBy = kind;
        value.WarnAt = 1; value.WarnColor = Soft;
        value.CriticalAt = 2; value.CriticalColor = Cyan;
        return value;
    }

    /// <summary>The album cover with rounded corners and a soft light behind it; opens the media page.</summary>
    private static void Art(List<ThemeElement> e, double x, double y, double size) =>
        e.Add(new ThemeElement { Kind = ThemeElementKind.AlbumArt, X = x, Y = y, Width = size, Height = size, CornerRadius = Math.Round(size * 0.16),
            FillColor = Button, LineColor = Soft, Action = ThemeAction.MediaPage, GlowColor = "#66A78BFF", GlowSize = 36 });

    /// <summary>A slim progress line with the time played and left under it (sources with a timeline).</summary>
    private static void Progress(List<ThemeElement> e, double x, double y, double w)
    {
        e.Add(new ThemeElement { Kind = ThemeElementKind.Bar, DataName = "MEDIA_PROGRESS", X = x, Y = y, Width = w, Height = 4, CornerRadius = 2, ForeColor = Cyan, GradientColor = Violet,
            BackColor = Track, Action = ThemeAction.MediaPage, ShowIf = "MEDIA_PROGRESS" });
        e.Add(Value(x, y + 8, "MEDIA_ELAPSED", Num(11, Dim), unit: false));
        e.Add(Value(x + w, y + 8, "MEDIA_REMAINING", Num(11, Dim, ThemeTextAlignment.Right), unit: false));
    }

    private static void Transport(List<ThemeElement> e, double cx, double y, double side, double play, double spread)
    {
        double top = y + (play - side) / 2;
        e.AddRange(Round(cx - spread - side / 2, top, side, PrevGlyph, ThemeAction.PreviousTrack));
        e.AddRange(Play(cx, y, play));
        e.AddRange(Round(cx + spread - side / 2, top, side, NextGlyph, ThemeAction.NextTrack));
    }

    /// <summary>The speaker as a round button (tap: mute) beside a slim gradient track (tap: app volumes).</summary>
    private static void Volume(List<ThemeElement> e, double x, double y, double w)
    {
        const double size = 44;
        e.AddRange(Round(x, y, size, null, ThemeAction.Mute, "VOLUME_ICON"));
        double bx = x + size + 18, bw = w - size - 18 - 62;
        e.Add(new ThemeElement { Kind = ThemeElementKind.Bar, X = bx, Y = y + size / 2 - 3, Width = bw, Height = 6, CornerRadius = 3, DataName = "VOLUME",
            BackColor = Track, ForeColor = Cyan, GradientColor = Violet, Action = ThemeAction.VolumeMixer });
        e.Add(Value(x + w, y + size / 2 - 13, "VOLUME", Num(15, Soft, ThemeTextAlignment.Right)));
    }

    /// <summary>Round glass toggles spread evenly across a width, each named under it.</summary>
    private static void Toggles(List<ThemeElement> e, double x, double y, double w, double size, (string Glyph, string Label, ThemeAction Action)[] toggles, bool labels = true)
    {
        double step = w / toggles.Length;
        for (int i = 0; i < toggles.Length; i++)
        {
            double cx = x + step * (i + 0.5);
            e.AddRange(Round(cx - size / 2, y, size, toggles[i].Glyph, toggles[i].Action));
            if (labels) e.Add(Txt(cx, y + size + 8, toggles[i].Label, Font(12, Soft, bold: true, ThemeTextAlignment.Center), toggles[i].Action));
        }
    }

    /// <summary>The dock: shortcut tiles tinted by their icons, system actions drawn round.</summary>
    private static void Dock(List<ThemeElement> e, double x, double y, double w, double h, int perRow, int rows)
    {
        const double gap = 16;
        double tw = (w - gap * (perRow - 1)) / perRow, th = (h - gap * (rows - 1)) / rows;
        for (int r = 0; r < rows; r++)
            for (int i = 0; i < perRow; i++)
                e.Add(new ThemeElement { Kind = ThemeElementKind.Shortcut, Slot = 1 + r * perRow + i, X = x + i * (tw + gap), Y = y + r * (th + gap), Width = tw, Height = th,
                    CornerRadius = 28, FillColor = Button, BorderColor = Edge, BorderWidth = 1, LineColor = Soft, TileStyle = "Tinted" });
    }

    private static IEnumerable<ThemeElement> Round(double x, double y, double size, string? glyph, ThemeAction action, string? data = null)
    {
        yield return new ThemeElement { Kind = ThemeElementKind.Panel, X = x, Y = y, Width = size, Height = size, CornerRadius = size / 2, FillColor = "#26FFFFFF", GradientColor = "#0DFFFFFF",
            GradientAngle = 90, BorderColor = "#40FFFFFF", BorderWidth = 1, Action = action };
        double pt = size * 0.3 * 0.75, top = IconTop(y, size, pt);
        var font = Font(pt, Text, align: ThemeTextAlignment.Center, family: Icons);
        yield return data != null ? Value(x + size / 2, top, data, font, unit: false, action: action) : Txt(x + size / 2, top, glyph!, font, action);
    }

    /// <summary>Where an icon's text line starts so the icon's ink is centred in a box of <paramref name="size"/> (see the Halo kit).</summary>
    private static double IconTop(double y, double size, double pt) => y + (size - pt * 4 / 3 * 1.33) / 2 + pt * 4 / 3 * 0.18;

    /// <summary>A labelled reading: a small letter-spaced label with the number right under it. Returns the number for extra settings.</summary>
    private static ThemeElement Stat(List<ThemeElement> e, double x, double y, string label, string data, double size, ThemeTextAlignment align = ThemeTextAlignment.Left, bool unit = true)
    {
        // A clock HWiNFO doesn't export is left out rather than shown as "--".
        string? showIf = data.Contains("CLOCK", StringComparison.Ordinal) ? data : null;
        var caption = Label(x, y, label, Dim, align);
        caption.ShowIf = showIf;
        e.Add(caption);
        var value = Limited(Value(x, y + 11 * 1.45, data, Num(size, Text, align), unit));
        value.ShowIf = showIf;
        e.Add(value);
        return value;
    }

    /// <summary>Warning levels where a reading can really be too high: CPU and GPU temperatures.</summary>
    private static ThemeElement Limited(ThemeElement value)
    {
        (double, double)? limits = value.DataName switch { "CPUTEMP" or "GAME_PEAK_CPU" => (90, 98), "GPUTEMP" or "GAME_PEAK_GPU" => (83, 90), _ => null };
        return limits is { } l ? Warn(value, l.Item1, l.Item2) : value;
    }

    private static ThemeElement Warn(ThemeElement element, double warn, double critical)
    {
        element.WarnAt = warn;
        element.CriticalAt = critical;
        return element;
    }

    private static ThemeElement Glowing(ThemeElement element, string color, double size)
    {
        element.GlowColor = "#73" + color[3..];
        element.GlowSize = size;
        return element;
    }

    private static ThemeElement Lines(ThemeElement element, int lines) { element.MaxLines = lines; return element; }

    /// <summary>The one label style: small DIN capitals, letter-spaced with hair spaces.</summary>
    private static ThemeElement Label(double x, double y, string text, string color, ThemeTextAlignment align = ThemeTextAlignment.Left) =>
        Txt(x, y, string.Join(' ', text.ToCharArray()), Font(11, color, align: align, family: DinSemi));

    /// <summary>The one number style.</summary>
    private static ThemeFont Num(double size, string color, ThemeTextAlignment align = ThemeTextAlignment.Left) => Font(size, color, align: align, family: Din);

    private static ThemeFont Font(double size, string color, bool bold = false, ThemeTextAlignment align = ThemeTextAlignment.Left, string family = Ui) =>
        new() { Family = family, Size = size, Color = color, Bold = bold, Alignment = align };

    private static ThemeElement Txt(double x, double y, string text, ThemeFont font, ThemeAction? action = null) =>
        new() { Kind = ThemeElementKind.Text, X = x, Y = y, Text = text, Font = font, Action = action };

    /// <summary>A reading; units are spaced ("188 W").</summary>
    private static ThemeElement Value(double x, double y, string data, ThemeFont font, bool unit = true, double width = 0, string? format = null, ThemeAction? action = null) =>
        new() { Kind = ThemeElementKind.Data, X = x, Y = y, DataName = data, ShowUnit = unit, SpacedUnit = true, Width = width, DataFormat = format, Font = font, Action = action };
}
