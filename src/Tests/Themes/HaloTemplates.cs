using Pulsewave.Themes;

namespace Pulsewave.Regression;

// Source of the Halo built-in templates, Pulsewave's signature style, in three focused versions, each for the
// 8.8" bar (1920x480) and the HYTE Y70 column (682x2560):
//   Lifestyle (28-halo, 29-halo-column): clock, weather with the next hours, now playing with the album, the dock.
//   Gaming (30-halo-gaming, 31-halo-gaming-column): live FPS first with its history, the session, CPU and GPU cards
//     with what is holding them back (thermal / power limits).
//   Productivity (32-halo-productivity, 33-halo-productivity-column): one timer card (timer or stopwatch, Pomodoro),
//     Claude Code and Codex limits with when they reset, system and network, a now-playing strip.
// One design kit for all six: a 24 px margin and gap, 28 px card padding, 36 px corners, small-caps grey headers and
// labels, Segoe UI Semibold numbers with spaced units (Light only for the clock), titles on up to two lines, calm
// glows under nearly opaque glass, warning colors where a reading can really be too high, and one dock-tile style.
// Regenerate with `--widget-shots <dir> --write-oneui-templates <dir>` (it writes these too).
internal static class HaloTemplates
{
    private const string Text = "#FFFFFFFF", Secondary = "#FFD5DCE6", Sky = "#FF7FD8FF", Pink = "#FFFF8FB1", Violet = "#FFC3A6FF", Warm = "#FFFFD27A",
        CardFill = "#D9141A2C", CardEnd = "#E60E1322", Edge = "#33FFFFFF", Track = "#2EFFFFFF", Button = "#40FFFFFF", OnFill = "#FFFFFFFF", OnText = "#FF101626";
    private const string Ui = "Segoe UI", Semi = "Segoe UI Semibold", Light = "Segoe UI Light", Icons = "Segoe Fluent Icons", Symbols = "Segoe UI Symbol";
    private const string PinGlyph = "", PrevGlyph = "", NextGlyph = "", LightGlyph = "", PaletteGlyph = "",
        AppsGlyph = "", MixerGlyph = "", AestheticGlyph = "", MusicGlyph = "";

    // The kit's spacing.
    private const double Margin = 24, Pad = 28, Radius = 36;

    private static readonly (string Glyph, string Label, ThemeAction Action)[] LifestyleToggles =
        [(LightGlyph, "Lights", ThemeAction.LightsPage), (PaletteGlyph, "Theme colors", ThemeAction.PalettePage), (AppsGlyph, "Apps", ThemeAction.AppDrawer), (AestheticGlyph, "Wallpaper", ThemeAction.AestheticToggle)];
    private static readonly (string Glyph, string Label, ThemeAction Action)[] GamingToggles =
        [(LightGlyph, "Lights", ThemeAction.LightsPage), (PaletteGlyph, "Theme colors", ThemeAction.PalettePage), (MixerGlyph, "Volume", ThemeAction.VolumeMixer), (MusicGlyph, "Media", ThemeAction.MediaPage)];
    private static readonly (string Glyph, string Label, ThemeAction Action)[] WorkToggles =
        [(LightGlyph, "Lights", ThemeAction.LightsPage), (PaletteGlyph, "Theme colors", ThemeAction.PalettePage), (MixerGlyph, "Volume", ThemeAction.VolumeMixer), (AppsGlyph, "Apps", ThemeAction.AppDrawer)];

    // --- Lifestyle ---------------------------------------------------------------------------

    public static ThemeDefinition Bar()
    {
        var t = New("Halo Lifestyle", 1920, 480);
        var e = t.Elements;

        // Clock and weather.
        Card(e, 24, 24, 500, 432);
        ClockBlock(e, 274, 36, 96);
        WeatherBlock(e, 52, 248, 444, 40);

        // Now playing: art in a progress ring, title on up to two lines, artist, album and time.
        Card(e, 548, 24, 760, 432);
        ArtRing(e, 580, 56, 232);
        e.Add(Header(840, 58, "NOW PLAYING"));
        e.Add(Lines(Value(840, 82, "MEDIA_TITLE", Font(22, Text, bold: true), unit: false, width: 440, action: ThemeAction.MediaPage), 2));
        e.Add(Value(840, 158, "MEDIA_ARTIST", Font(16, Secondary), unit: false, width: 440, action: ThemeAction.MediaPage));
        e.Add(Value(840, 186, "MEDIA_ALBUM", Font(14, Secondary), unit: false, width: 440, action: ThemeAction.MediaPage));
        e.Add(Value(840, 214, "MEDIA_TIME", Num(14, Sky), unit: false));
        Transport(e, 1060, 250, 56, 76, 110);
        Volume(e, 580, 352, 700, 56);

        // The next hours, quick toggles and the dock.
        Card(e, 1332, 24, 564, 432);
        e.Add(Header(1360, 52, "NEXT HOURS"));
        Hours(e, 1360, 78, 508, 26);
        e.Add(Value(1360, 182, "WEATHER_RAIN", Font(14, Secondary), unit: false, width: 508));
        Toggles(e, 1360, 214, 508, 56, LifestyleToggles);
        Dock(e, 1360, 316, 508, 112, 4, 1, labels: true);
        return t;
    }

    public static ThemeDefinition Column()
    {
        var t = New("Halo Lifestyle Column", 682, 2560);
        var e = t.Elements;
        const double x = Margin, w = 682 - 2 * Margin, ix = x + Pad, iw = w - 2 * Pad, mid = 341;

        ClockBlock(e, mid, 44, 128);

        // All the weather in one card: now, the next hours, and the next days.
        Card(e, x, 320, w, 670);
        WeatherBlock(e, ix, 348, iw, 64);
        e.Add(Rule(ix, 588, iw));
        Hours(e, ix, 604, iw, 30);
        e.Add(Value(ix, 724, "WEATHER_RAIN", Font(15, Secondary), unit: false, width: iw));
        e.Add(Rule(ix, 770, iw));
        e.Add(Header(ix, 790, "NEXT DAYS"));
        Day(e, ix, 824, 1);
        Day(e, ix + iw / 2, 824, 2);

        // Now playing.
        Card(e, x, 1014, w, 670);
        ArtRing(e, ix, 1046, 262);
        double tx = ix + 290, tw = iw - 290;
        e.Add(Header(tx, 1052, "NOW PLAYING"));
        e.Add(Lines(Value(tx, 1078, "MEDIA_TITLE", Font(21, Text, bold: true), unit: false, width: tw, action: ThemeAction.MediaPage), 2));
        e.Add(Value(tx, 1154, "MEDIA_ARTIST", Font(16, Secondary), unit: false, width: tw, action: ThemeAction.MediaPage));
        e.Add(Value(tx, 1184, "MEDIA_ALBUM", Font(14, Secondary), unit: false, width: tw, action: ThemeAction.MediaPage));
        e.Add(Value(tx, 1216, "MEDIA_TIME", Num(15, Sky), unit: false));
        Transport(e, mid, 1364, 84, 112, 170);
        Volume(e, ix, 1564, iw, 64);

        Card(e, x, 1708, w, 170);
        Toggles(e, ix, 1708 + (170 - 104) / 2.0, iw, 72, LifestyleToggles);

        // One row of favourites.
        Card(e, x, 1902, w, 204);
        Dock(e, ix, 1928, iw, 152, 4, 1, labels: true);

        // A light look at the PC, last: the least of what Lifestyle is for.
        Card(e, x, 2130, w, 406);
        e.Add(Header(ix, 2158, "SYSTEM"));
        var stats = new (string Name, string Load, string Detail, string Color)[] { ("CPU", "CPULOAD", "CPUTEMP", Sky), ("GPU", "GPULOAD", "GPUTEMP", Pink), ("MEMORY", "RAMLOAD", "RAM_GB_OF", Violet) };
        for (int i = 0; i < 3; i++) Ring(e, ix + iw / 6 + i * iw / 3, 2224, 164, stats[i].Load, stats[i].Name, stats[i].Detail, stats[i].Color);
        return t;
    }
    // --- Gaming ------------------------------------------------------------------------------

    public static ThemeDefinition GamingBar()
    {
        var t = New("Halo Gaming", 1920, 480);
        var e = t.Elements;

        // The game: live FPS first, with its history, then the session.
        Card(e, 24, 24, 560, 432, ThemeAction.GamingPage);
        e.Add(Cover(52, 52, 176, 264));
        e.Add(Value(256, 54, "GAME_STATE", Font(13, Secondary, bold: true), unit: false, action: ThemeAction.GamingPage));
        e.Add(Lines(Value(256, 76, "GAME_NAME", Font(18, Text, bold: true), unit: false, width: 300, action: ThemeAction.GamingPage), 2));
        Fps(e, 256, 140, 300, 50, 24, 180, 254, 64);
        SessionRow(e, 52, 346, 504, 18);

        Processor(e, 608, 24, 420, 432, "CPU", "CPUMODEL", "CPULOAD", "CPU_LIMIT", Sky, ("TEMP", "CPUTEMP"), ("POWER", "CPUPWR"), ("CLOCK", "CPUCLOCK_G"), ("FAN", "CPUFAN_STATE"), small: true);
        Processor(e, 1052, 24, 420, 432, "GPU", "GPUMODEL", "GPULOAD", "GPU_LIMIT", Pink, ("TEMP", "GPUTEMP"), ("POWER", "GPUPWR"), ("CLOCK", "GPUCLOCK_G"), ("VRAM", "GPURAM_GB_OF"), small: true);

        // Time, memory and toggles.
        Card(e, 1496, 24, 400, 432);
        ClockBlock(e, 1696, 36, 80);
        Memory(e, 1524, 214, 344);
        Toggles(e, 1524, 300, 344, 56, GamingToggles);
        return t;
    }

    public static ThemeDefinition GamingColumn()
    {
        var t = New("Halo Gaming Column", 682, 2560);
        var e = t.Elements;
        const double x = Margin, w = 682 - 2 * Margin, ix = x + Pad, iw = w - 2 * Pad, mid = 341;

        ClockBlock(e, mid, 36, 104);

        Card(e, x, 256, w, 448, ThemeAction.GamingPage);
        e.Add(Cover(ix, 284, 200, 300));
        double rx = ix + 228, rw = iw - 228;
        e.Add(Value(rx, 288, "GAME_STATE", Font(13, Secondary, bold: true), unit: false, action: ThemeAction.GamingPage));
        e.Add(Lines(Value(rx, 312, "GAME_NAME", Font(20, Text, bold: true), unit: false, width: rw, action: ThemeAction.GamingPage), 2));
        Fps(e, rx, 386, rw, 64, 24, 210, 520, 64);
        SessionRow(e, ix, 612, iw, 20);

        Processor(e, x, 728, w, 476, "CPU", "CPUMODEL", "CPULOAD", "CPU_LIMIT", Sky, ("TEMP", "CPUTEMP"), ("POWER", "CPUPWR"), ("CLOCK", "CPUCLOCK_G"), ("FAN", "CPUFAN_STATE"));
        Processor(e, x, 1228, w, 476, "GPU", "GPUMODEL", "GPULOAD", "GPU_LIMIT", Pink, ("TEMP", "GPUTEMP"), ("POWER", "GPUPWR"), ("CLOCK", "GPUCLOCK_G"), ("VRAM", "GPURAM_GB_OF"));

        Card(e, x, 1728, w, 236);
        e.Add(Header(ix, 1756, "SYSTEM"));
        Memory(e, ix, 1790, iw);
        Stat(e, ix, 1872, "DOWNLOAD", "DOWNDSPEED", 22);
        Stat(e, ix + iw / 2, 1872, "UPLOAD", "UPSPEED", 22);

        Card(e, x, 1988, w, 170);
        Toggles(e, ix, 1988 + (170 - 104) / 2.0, iw, 72, GamingToggles);

        // Two rows of games and apps.
        Card(e, x, 2182, w, 354);
        Dock(e, ix, 2208, iw, 302, 4, 1, labels: true, rows: 2);
        return t;
    }

    // --- Productivity ------------------------------------------------------------------------

    public static ThemeDefinition WorkBar()
    {
        var t = New("Halo Productivity", 1920, 480);
        var e = t.Elements;

        // Clock, weather and what is playing.
        Card(e, 24, 24, 440, 432);
        ClockBlock(e, 244, 36, 80);
        WeatherBlock(e, 52, 226, 384, 30, compact: true);
        MediaStrip(e, 52, 380, 384, 48);

        Card(e, 488, 24, 460, 432);
        TimerCard(e, 516, 52, 404, 376, 64);

        // Claude Code and Codex limits.
        Card(e, 972, 24, 480, 432);
        e.Add(Header(1000, 52, "AI USAGE"));
        Usage(e, 1000, 84, 424, "Claude Code", "CLAUDE");
        Usage(e, 1000, 262, 424, "Codex", "CODEX");

        // System and network.
        Card(e, 1476, 24, 420, 432);
        e.Add(Header(1504, 52, "SYSTEM"));
        Load(e, 1504, 82, 364, "CPU", "CPULOAD", "CPUTEMP");
        Load(e, 1504, 130, 364, "GPU", "GPULOAD", "GPUTEMP");
        Load(e, 1504, 178, 364, "MEMORY", "RAMLOAD", "RAM_GB_OF");
        Load(e, 1504, 226, 364, "DRIVE", "DRIVE_USED_PCT", "DRIVE_FREE");
        Stat(e, 1504, 276, "DOWNLOAD", "DOWNDSPEED", 17);
        Stat(e, 1686, 276, "UPLOAD", "UPSPEED", 17);
        Toggles(e, 1504, 338, 364, 48, WorkToggles);
        return t;
    }

    public static ThemeDefinition WorkColumn()
    {
        var t = New("Halo Productivity Column", 682, 2560);
        var e = t.Elements;
        const double x = Margin, w = 682 - 2 * Margin, ix = x + Pad, iw = w - 2 * Pad, mid = 341;

        ClockBlock(e, mid, 36, 104);

        Card(e, x, 256, w, 276);
        WeatherBlock(e, ix, 284, iw, 48);
        e.Add(Value(ix, 476, "WEATHER_RAIN", Font(15, Secondary), unit: false, width: iw));

        Card(e, x, 556, w, 120);
        MediaStrip(e, ix, 580, iw, 72);

        Card(e, x, 700, w, 420);
        TimerCard(e, ix, 728, iw, 364, 88);

        Card(e, x, 1144, w, 400);
        e.Add(Header(ix, 1172, "AI USAGE"));
        Usage(e, ix, 1204, iw, "Claude Code", "CLAUDE");
        Usage(e, ix, 1366, iw, "Codex", "CODEX");

        Card(e, x, 1568, w, 404);
        e.Add(Header(ix, 1596, "SYSTEM"));
        Load(e, ix, 1632, iw, "CPU", "CPULOAD", "CPUTEMP");
        Load(e, ix, 1692, iw, "GPU", "GPULOAD", "GPUTEMP");
        Load(e, ix, 1752, iw, "MEMORY", "RAMLOAD", "RAM_GB_OF");
        Load(e, ix, 1812, iw, "DRIVE", "DRIVE_USED_PCT", "DRIVE_FREE");
        Stat(e, ix, 1874, "DOWNLOAD", "DOWNDSPEED", 20);
        Stat(e, ix + iw / 2, 1874, "UPLOAD", "UPSPEED", 20);

        Card(e, x, 1996, w, 170);
        Toggles(e, ix, 1996 + (170 - 104) / 2.0, iw, 72, WorkToggles);

        Card(e, x, 2190, w, 346);
        Dock(e, ix, 2216, iw, 294, 4, 1, labels: true, rows: 2);
        return t;
    }

    // --- Kit ---------------------------------------------------------------------------------

    private static ThemeDefinition New(string name, int w, int h)
    {
        var t = new ThemeDefinition { Name = name, Source = "Pulsewave template", Width = w, Height = h, BackgroundColor = "#FF0A0E1A" };
        Backdrop(t.Elements, w, h);
        return t;
    }

    /// <summary>A deep navy base with two soft, low glows: color without competing with the cards.</summary>
    private static void Backdrop(List<ThemeElement> e, double w, double h)
    {
        e.Add(new ThemeElement { Kind = ThemeElementKind.Panel, Width = w, Height = h, FillColor = "#FF0E1424", GradientColor = "#FF0A0E1A", GradientAngle = 90 });
        bool tall = h > w;
        var glows = tall
            ? new (string, double, double, double)[] { ("#3336C2FF", 0.1, 0.12, 0.6), ("#2AFF5E8A", 0.95, 0.6, 0.55) }
            : [("#3336C2FF", 0.05, 0.1, 0.8), ("#2AFF5E8A", 0.7, 0.95, 0.7)];
        foreach (var (color, gx, gy, gr) in glows)
        {
            double r = Math.Max(w, h) * gr * (tall ? 0.5 : 0.35);
            e.Add(new ThemeElement { Kind = ThemeElementKind.Panel, X = w * gx - r, Y = h * gy - r, Width = 2 * r, Height = 2 * r, CornerRadius = r,
                FillColor = color, GradientColor = "#00" + color[3..], GradientStyle = "Radial" });
        }
    }

    private static void Card(List<ThemeElement> e, double x, double y, double w, double h, ThemeAction? action = null) =>
        e.Add(new ThemeElement { Kind = ThemeElementKind.Panel, X = x, Y = y, Width = w, Height = h, CornerRadius = Radius, FillColor = CardFill, GradientColor = CardEnd,
            GradientAngle = 90, BorderColor = Edge, BorderWidth = 1, Action = action });

    /// <summary>Hours, a glowing sky-blue colon and minutes, with the long date ("Thursday, Dec 31") clear of the digits below.</summary>
    private static void ClockBlock(List<ThemeElement> e, double cx, double y, double size)
    {
        double gap = size * 0.2;
        e.Add(Value(cx - gap, y, "TIME", Font(size, Text, align: ThemeTextAlignment.Right, family: Light), format: "h"));
        e.Add(new ThemeElement { Kind = ThemeElementKind.Text, X = cx, Y = y - size * 0.06, Text = ":", Font = Font(size, Sky, align: ThemeTextAlignment.Center, family: Light),
            GlowColor = "#887FD8FF", GlowSize = 12 });
        e.Add(Value(cx + gap, y, "TIME", Font(size, Text, family: Light), format: "m"));
        // Regular weight (not bold) beside the thin digits, and set clear of them.
        e.Add(Value(cx, y + size * 1.6, "DATE", Font(Math.Round(size * 0.2), Secondary, align: ThemeTextAlignment.Center), format: "long"));
    }

    /// <summary>City (with a pin), symbol and temperature, conditions with "feels" when it differs, then high, low and the next sunrise or sunset.</summary>
    private static void WeatherBlock(List<ThemeElement> e, double x, double y, double w, double size, bool compact = false)
    {
        e.Add(Txt(x, y + 3, PinGlyph, Font(13, Secondary, family: Icons)));
        e.Add(Value(x + 22, y, "WEATHER_CITY", Font(14, Secondary, bold: true), unit: false, width: w - 22));
        double row = y + 28, px = size * 4 / 3;
        e.Add(Symbol(Value(x, row, "WEATHER_ICON", Font(size, Warm, family: Symbols), unit: false), "WEATHER_KIND"));
        e.Add(Value(x + px * 1.5, row, "WEATHER_TEMP", Num(size, Text)));
        double textSize = Math.Max(15, size * 0.34);
        e.Add(Value(x + w, row + px * 0.18, "WEATHER_TEXT", Font(textSize, Text, bold: true, ThemeTextAlignment.Right), unit: false, width: w - px * 3.4));
        e.Add(Value(x + w, row + px * 0.18 + textSize * 1.75, "WEATHER_FEELS_NOTE", Font(13, Secondary, align: ThemeTextAlignment.Right), unit: false));
        double labels = row + px * 1.42 + (compact ? 0 : 6);
        e.Add(Label(x, labels, "HIGH")); e.Add(Value(x, labels + 18, "WEATHER_HIGH", Num(18, Text)));
        e.Add(Label(x + 84, labels, "LOW")); e.Add(Value(x + 84, labels + 18, "WEATHER_LOW", Num(18, Text)));
        e.Add(Value(x + w, labels, "WEATHER_SUN_NEXT_LABEL", Font(11, Secondary, bold: true, ThemeTextAlignment.Right), unit: false));
        e.Add(Value(x + w, labels + 18, "WEATHER_SUN_NEXT", Num(18, Text, ThemeTextAlignment.Right), unit: false));
    }

    /// <summary>The next four three-hour steps: time, a symbol colored by the weather (warm sun, grey cloud or night, blue rain), temperature.</summary>
    private static void Hours(List<ThemeElement> e, double x, double y, double w, double size)
    {
        double step = w / 4;
        for (int i = 1; i <= 4; i++)
        {
            double cx = x + step * (i - 0.5);
            string h = "WEATHER_H" + i + "_";
            e.Add(Value(cx, y, h + "TIME", Font(12, Secondary, bold: true, ThemeTextAlignment.Center), unit: false));
            e.Add(Symbol(Value(cx, y + 20, h + "ICON", Font(size, Warm, align: ThemeTextAlignment.Center, family: Symbols), unit: false), h + "KIND"));
            e.Add(Value(cx, y + 20 + size * 4 / 3 * 1.3, h + "TEMP", Num(18, Text, ThemeTextAlignment.Center)));
        }
    }

    /// <summary>One forecast day: weekday, a colored symbol, and its labelled high and low.</summary>
    private static void Day(List<ThemeElement> e, double x, double y, int day)
    {
        string d = "WEATHER_D" + day + "_";
        e.Add(Value(x, y, d + "DAY", Font(16, Text, bold: true), unit: false));
        e.Add(Symbol(Value(x, y + 34, d + "ICON", Font(40, Warm, family: Symbols), unit: false), d + "KIND"));
        e.Add(Label(x + 92, y + 40, "HIGH")); e.Add(Value(x + 92, y + 58, d + "HIGH", Num(20, Text)));
        e.Add(Label(x + 168, y + 40, "LOW")); e.Add(Value(x + 168, y + 58, d + "LOW", Num(20, Secondary)));
    }

    /// <summary>The weather symbol's color from the conditions: warm for sun, grey for cloud, fog or night, blue for rain and snow.</summary>
    private static ThemeElement Symbol(ThemeElement value, string kind)
    {
        value.ColorBy = kind;
        value.WarnAt = 1; value.WarnColor = Secondary;
        value.CriticalAt = 2; value.CriticalColor = Sky;
        return value;
    }

    /// <summary>Round album art inside a progress ring (Plexamp), both opening the media page.</summary>
    private static void ArtRing(List<ThemeElement> e, double x, double y, double ring)
    {
        e.Add(new ThemeElement { Kind = ThemeElementKind.Arc, DataName = "MEDIA_PROGRESS", X = x, Y = y, Width = ring, Height = ring, Diameter = ring, Thickness = 7,
            StartAngle = -90, SweepAngle = 360, RoundCaps = true, ForeColor = Sky, GradientColor = Violet, BackColor = Track, Action = ThemeAction.MediaPage,
            GlowColor = "#557FD8FF", GlowSize = 8 });
        double inset = 18, art = ring - inset * 2;
        e.Add(new ThemeElement { Kind = ThemeElementKind.AlbumArt, X = x + inset, Y = y + inset, Width = art, Height = art, CornerRadius = art / 2,
            FillColor = Button, LineColor = Secondary, Action = ThemeAction.MediaPage });
    }

    /// <summary>A one-line now-playing strip: small art, title and artist, and play / pause.</summary>
    private static void MediaStrip(List<ThemeElement> e, double x, double y, double w, double h)
    {
        e.Add(new ThemeElement { Kind = ThemeElementKind.AlbumArt, X = x, Y = y, Width = h, Height = h, CornerRadius = h * 0.22,
            FillColor = Button, LineColor = Secondary, Action = ThemeAction.MediaPage });
        double tx = x + h + 14, tw = w - h - 14 - h - 10;
        double title = Math.Round(h * 0.3), sub = Math.Round(h * 0.24);
        e.Add(Value(tx, y + h * 0.04, "MEDIA_TITLE", Font(title, Text, bold: true), unit: false, width: tw, action: ThemeAction.MediaPage));
        e.Add(Value(tx, y + h * 0.04 + title * 1.6, "MEDIA_ARTIST", Font(sub, Secondary), unit: false, width: tw, action: ThemeAction.MediaPage));
        e.AddRange(Play(x + w - h / 2, y, h));
    }

    private static void Transport(List<ThemeElement> e, double cx, double y, double side, double play, double spread)
    {
        double top = y + (play - side) / 2;
        e.AddRange(Round(cx - spread - side / 2, top, side, PrevGlyph, ThemeAction.PreviousTrack));
        e.AddRange(Play(cx, y, play));
        e.AddRange(Round(cx + spread - side / 2, top, side, NextGlyph, ThemeAction.NextTrack));
    }

    /// <summary>The speaker as its own round button (tap: mute) beside a slim gradient track (tap: app volumes).</summary>
    private static void Volume(List<ThemeElement> e, double x, double y, double w, double size)
    {
        e.AddRange(Round(x, y, size, null, ThemeAction.Mute, "VOLUME_ICON"));
        double bx = x + size + 20, bw = w - size - 20, track = Math.Round(size * 0.32);
        e.Add(Label(bx, y + 2, "VOLUME"));
        e.Add(Value(bx + bw, y - 2, "VOLUME", Num(14, Secondary, ThemeTextAlignment.Right)));
        e.Add(new ThemeElement { Kind = ThemeElementKind.Bar, X = bx, Y = y + size - track - 4, Width = bw, Height = track, CornerRadius = track / 2, DataName = "VOLUME",
            BackColor = Track, ForeColor = Sky, GradientColor = Violet, Action = ThemeAction.VolumeMixer });
    }

    /// <summary>A load ring with its reading inside, a label and one detail below.</summary>
    private static void Ring(List<ThemeElement> e, double cx, double y, double d, string load, string name, string detail, string color)
    {
        var ring = new ThemeElement { Kind = ThemeElementKind.Arc, DataName = load, X = cx - d / 2, Y = y, Width = d, Height = d, Diameter = d, Thickness = 14,
            StartAngle = 135, SweepAngle = 270, RoundCaps = true, ForeColor = color, GradientColor = Violet, BackColor = Track, Action = ThemeAction.Details };
        if (load == "RAMLOAD") Warn(ring, 85, 95);
        e.Add(ring);
        e.Add(Value(cx, y + d * 0.32, load, Num(24, Text, ThemeTextAlignment.Center), action: ThemeAction.Details));
        e.Add(Txt(cx, y + d + 4, name, Font(12, Secondary, bold: true, ThemeTextAlignment.Center)));
        e.Add(Limited(Value(cx, y + d + 24, detail, Num(16, Text, ThemeTextAlignment.Center), unit: detail != "RAM_GB_OF")));
    }

    /// <summary>
    /// A CPU or GPU card: name (small) with what is holding it back on the right, a load ring, temperature and power large
    /// beside it, two more readings under it, and the load history labelled with its span.
    /// </summary>
    private static void Processor(List<ThemeElement> e, double x, double y, double w, double h, string title, string model, string load, string limit, string color,
        (string Label, string Data) a, (string Label, string Data) b, (string Label, string Data) c, (string Label, string Data) d, bool small = false)
    {
        Card(e, x, y, w, h, ThemeAction.Details);
        double ix = x + Pad, iw = w - 2 * Pad;
        e.Add(Header(ix, y + 26, title));
        e.Add(Value(ix + 44, y + 27, model, Font(11, Secondary), unit: false, width: iw - 44 - 130));
        e.Add(Value(ix + iw, y + 25, limit, Font(12, Warm, bold: true, ThemeTextAlignment.Right), unit: false));
        double ring = small ? 150 : 200, ry = y + (small ? 58 : 64);
        e.Add(new ThemeElement { Kind = ThemeElementKind.Arc, DataName = load, X = ix, Y = ry, Width = ring, Height = ring, Diameter = ring, Thickness = small ? 13 : 17,
            StartAngle = 135, SweepAngle = 270, RoundCaps = true, ForeColor = color, GradientColor = Violet, BackColor = Track, Action = ThemeAction.Details });
        e.Add(Value(ix + ring / 2, ry + ring * 0.3, load, Num(small ? 28 : 38, Text, ThemeTextAlignment.Center), action: ThemeAction.Details));
        e.Add(Txt(ix + ring / 2, ry + ring * 0.3 + (small ? 44 : 60), "LOAD", Font(12, Secondary, bold: true, ThemeTextAlignment.Center)));
        // Temperature and power large beside the ring; the label sits right above its value, not across the card.
        double sx = ix + ring + (small ? 26 : 36), big = small ? 30 : 40, gap = small ? 74 : 98;
        Stat(e, sx, ry + 4, a.Label, a.Data, big, labelSize: 13);
        Stat(e, sx, ry + 4 + gap, b.Label, b.Data, big, labelSize: 13);
        double below = ry + ring + (small ? 14 : 22), half = iw / 2;
        Stat(e, ix, below, c.Label, c.Data, small ? 20 : 26, labelSize: 13);
        Stat(e, ix + half, below, d.Label, d.Data, small ? 20 : 26, labelSize: 13);
        double chartTop = below + (small ? 58 : 74), chartBottom = y + h - Pad;
        e.Add(Label(ix, chartTop, "LOAD · LAST 60 S"));
        e.Add(new ThemeElement { Kind = ThemeElementKind.Chart, DataName = load, X = ix, Y = chartTop + 26, Width = iw, Height = chartBottom - chartTop - 26, Samples = 60, Maximum = 100,
            LineColor = color, LineWidth = 2, FillColor = "#22" + color[3..], Action = ThemeAction.Details });
    }

    /// <summary>Live FPS as the hero number, average and 1% low (amber when it drops far below the average) beside it, and the FPS history.</summary>
    private static void Fps(List<ThemeElement> e, double x, double y, double w, double hero, double small, double side, double chartY, double chartH)
    {
        e.Add(Label(x, y, "FPS"));
        var live = Value(x, y + 16, "FPS", Num(hero, Text), unit: false);
        e.Add(live);
        Stat(e, x + side, y, "AVG", "GAME_FPS_AVG", small).ShowUnit = false;
        var low = Stat(e, x + side, y + small * 4 / 3 + 30, "1% LOW", "GAME_FPS_LOW", small);
        low.ShowUnit = false;
        low.ColorBy = "GAME_FPS_LOW_PCT"; low.LowIsBad = true; low.WarnAt = 60; low.CriticalAt = 40;
        e.Add(Label(x, chartY, "FPS · LAST 60 S"));
        e.Add(new ThemeElement { Kind = ThemeElementKind.Chart, DataName = "FPS", X = x, Y = chartY + 18, Width = w, Height = chartH - 18, Samples = 60, Maximum = 0,
            LineColor = Sky, LineWidth = 2, FillColor = "#227FD8FF", Action = ThemeAction.GamingPage });
    }

    /// <summary>The session in one format: its length, today, this week, and the session's peak temperatures.</summary>
    private static void SessionRow(List<ThemeElement> e, double x, double y, double w, double size)
    {
        double step = w / 5;
        Stat(e, x, y, "SESSION", "GAME_SESSION", size).DataFormat = "short";
        Stat(e, x + step, y, "TODAY", "GAME_TODAY", size);
        Stat(e, x + step * 2, y, "THIS WEEK", "GAME_WEEK", size);
        Stat(e, x + step * 3, y, "PEAK CPU", "GAME_PEAK_CPU", size);
        Stat(e, x + step * 4, y, "PEAK GPU", "GAME_PEAK_GPU", size);
    }

    private static void Memory(List<ThemeElement> e, double x, double y, double w)
    {
        e.Add(Label(x, y + 4, "MEMORY"));
        e.Add(Value(x + w, y, "RAM_GB_OF", Num(16, Text, ThemeTextAlignment.Right), unit: false, width: w - 90));
        e.Add(Warn(new ThemeElement { Kind = ThemeElementKind.Bar, DataName = "RAMLOAD", X = x, Y = y + 34, Width = w, Height = 14, CornerRadius = 7,
            ForeColor = Sky, GradientColor = Violet, BackColor = Track, Action = ThemeAction.Details }, 85, 95));
    }

    /// <summary>A system row: name and a detail on the left, the percentage on the right, a slim bar below. Only memory and the drive warn: busy is normal.</summary>
    private static void Load(List<ThemeElement> e, double x, double y, double w, string name, string load, string detail)
    {
        e.Add(Label(x, y + 3, name));
        e.Add(Limited(Value(x + 84, y, detail, Num(14, Secondary), unit: detail != "RAM_GB_OF")));
        e.Add(Value(x + w, y, load, Num(14, Text, ThemeTextAlignment.Right)));
        var bar = new ThemeElement { Kind = ThemeElementKind.Bar, DataName = load, X = x, Y = y + 26, Width = w, Height = 10, CornerRadius = 5,
            ForeColor = Sky, GradientColor = Violet, BackColor = Track, Action = ThemeAction.Details };
        e.Add(load is "RAMLOAD" or "DRIVE_USED_PCT" ? Warn(bar, 85, 95) : bar);
    }

    /// <summary>Claude Code or Codex: the 5-hour and weekly limits, each saying when it resets.</summary>
    private static void Usage(List<ThemeElement> e, double x, double y, double w, string title, string prefix)
    {
        e.Add(Txt(x, y, title, Font(16, Text, family: Semi)));
        for (int i = 0; i < 2; i++)
        {
            string window = i == 0 ? "_5H" : "_WEEK";
            double ry = y + 34 + i * 66;
            e.Add(Label(x, ry + 3, i == 0 ? "5 HOURS" : "THIS WEEK"));
            e.Add(Value(x + 96, ry + 1, prefix + window + "_RESET", Font(13, Secondary), unit: false, width: w - 96 - 70, format: "resets"));
            e.Add(Value(x + w, ry, prefix + window, Num(15, Text, ThemeTextAlignment.Right)));
            e.Add(Warn(new ThemeElement { Kind = ThemeElementKind.Bar, DataName = prefix + window, X = x, Y = ry + 28, Width = w, Height = 12, CornerRadius = 6,
                ForeColor = Sky, GradientColor = Violet, BackColor = Track }, 75, 90));
        }
    }

    /// <summary>
    /// One timer card, centred throughout: the mode (TIMER, FOCUS / BREAK during a Pomodoro, or STOPWATCH) with a switch, the clock,
    /// Start / Pause (it says which) and Reset, and the Pomodoro presets.
    /// </summary>
    private static void TimerCard(List<ThemeElement> e, double x, double y, double w, double h, double clock)
    {
        double cx = x + w / 2;
        e.Add(Value(x, y, "FOCUS_MODE", Font(13, Secondary, bold: true), unit: false, action: ThemeAction.FocusSwitch));
        e.AddRange(PillValue(x + w - 132, y - 10, 132, 36, "FOCUS_SWITCH", ThemeAction.FocusSwitch, 12));
        double clockY = y + h * 0.1;
        e.Add(Value(cx, clockY, "FOCUS_CLOCK", Num(clock, Text, ThemeTextAlignment.Center), unit: false, action: ThemeAction.FocusToggle));
        double buttons = clockY + clock * 4 / 3 * 1.36 + 8, bh = Math.Round(clock * 0.72);
        double main = 168, reset = 120;
        double bx = cx - (main + 12 + reset) / 2;
        e.AddRange(PillValue(bx, buttons, main, bh, "FOCUS_ACTION", ThemeAction.FocusToggle, Math.Round(bh * 0.32)));
        e.AddRange(Pills(bx + main + 12, buttons, bh, [("Reset", reset, ThemeAction.FocusReset)]));
        double presets = buttons + bh + 32;
        e.Add(Txt(cx, presets, "POMODORO", Font(11, Secondary, bold: true, ThemeTextAlignment.Center)));
        double ph = Math.Round(bh * 0.86), pw = 110;
        double px0 = cx - (pw * 3 + 24) / 2;
        e.AddRange(Pills(px0, presets + 22, ph, [("25 / 5", pw, ThemeAction.Pomodoro25), ("50 / 10", pw, ThemeAction.Pomodoro50), ("+1 min", pw, ThemeAction.TimerAddMinute)]));
    }

    /// <summary>Round quick toggles spread evenly across a width, each with a label under it.</summary>
    private static void Toggles(List<ThemeElement> e, double x, double y, double w, double size, (string Glyph, string Label, ThemeAction Action)[] toggles)
    {
        double step = w / toggles.Length;
        for (int i = 0; i < toggles.Length; i++)
        {
            double cx = x + step * (i + 0.5);
            e.AddRange(Round(cx - size / 2, y, size, toggles[i].Glyph, toggles[i].Action));
            e.Add(Txt(cx, y + size + 8, toggles[i].Label, Font(12, Secondary, bold: true, ThemeTextAlignment.Center), toggles[i].Action));
        }
    }

    /// <summary>The dock: shortcut tiles tinted by their icons, system actions drawn round, optionally named.</summary>
    private static void Dock(List<ThemeElement> e, double x, double y, double w, double h, int perRow, int firstSlot, bool labels, int rows = 1)
    {
        const double gap = 16;
        double tw = (w - gap * (perRow - 1)) / perRow, th = (h - gap * (rows - 1)) / rows;
        for (int r = 0; r < rows; r++)
            for (int i = 0; i < perRow; i++)
                e.Add(new ThemeElement { Kind = ThemeElementKind.Shortcut, Slot = firstSlot + r * perRow + i, X = x + i * (tw + gap), Y = y + r * (th + gap), Width = tw, Height = th,
                    CornerRadius = 28, FillColor = Button, BorderColor = Edge, BorderWidth = 1, LineColor = Secondary, TileStyle = "Tinted", ShowLabel = labels });
    }

    private static ThemeElement Cover(double x, double y, double w, double h) =>
        new() { Kind = ThemeElementKind.Shortcut, Slot = ThemeDataContext.GameSlot, X = x, Y = y, Width = w, Height = h, CornerRadius = 28,
            FillColor = Button, BorderColor = Edge, BorderWidth = 1, LineColor = Secondary, Action = ThemeAction.GamingPage };

    private static List<ThemeElement> Pills(double x, double y, double h, (string Label, double Width, ThemeAction Action)[] pills)
    {
        var list = new List<ThemeElement>();
        foreach (var (label, pw, action) in pills)
        {
            list.Add(new ThemeElement { Kind = ThemeElementKind.Panel, X = x, Y = y, Width = pw, Height = h, CornerRadius = h / 2, FillColor = Button, Action = action });
            double pt = Math.Round(h * 0.32);
            list.Add(Txt(x + pw / 2, y + (h - pt * 4 / 3 * 1.33) / 2, label, Font(pt, Text, bold: true, ThemeTextAlignment.Center), action));
            x += pw + 12;
        }
        return list;
    }

    /// <summary>A pill whose label is a reading (Start or Pause, Timer or Stopwatch).</summary>
    private static IEnumerable<ThemeElement> PillValue(double x, double y, double w, double h, string data, ThemeAction action, double pt)
    {
        yield return new ThemeElement { Kind = ThemeElementKind.Panel, X = x, Y = y, Width = w, Height = h, CornerRadius = h / 2, FillColor = Button, Action = action };
        yield return Value(x + w / 2, y + (h - pt * 4 / 3 * 1.33) / 2, data, Font(pt, Text, bold: true, ThemeTextAlignment.Center), unit: false, action: action);
    }

    private static IEnumerable<ThemeElement> Round(double x, double y, double size, string? glyph, ThemeAction action, string? data = null)
    {
        yield return new ThemeElement { Kind = ThemeElementKind.Panel, X = x, Y = y, Width = size, Height = size, CornerRadius = size / 2, FillColor = Button, Action = action, Bevel = true };
        double pt = size * 0.3 * 0.75, top = IconTop(y, size, pt);
        var font = Font(pt, Text, align: ThemeTextAlignment.Center, family: Icons);
        yield return data != null ? Value(x + size / 2, top, data, font, unit: false, action: action) : Txt(x + size / 2, top, glyph!, font, action);
    }

    private static IEnumerable<ThemeElement> Play(double cx, double y, double size)
    {
        yield return new ThemeElement { Kind = ThemeElementKind.Panel, X = cx - size / 2, Y = y, Width = size, Height = size, CornerRadius = size / 2, FillColor = OnFill,
            Action = ThemeAction.PlayPause, GlowColor = "#447FD8FF", GlowSize = 18, Bevel = true };
        double pt = size * 0.3 * 0.75;
        yield return Value(cx, IconTop(y, size, pt), "MEDIA_PLAYPAUSE", Font(pt, OnText, align: ThemeTextAlignment.Center, family: Icons), unit: false, action: ThemeAction.PlayPause);
    }

    /// <summary>Where an icon's text line starts so the icon's ink is centred in a box of <paramref name="size"/>: theme text lines are placed from their top as 1.33 em lines, and the icon's ink sits 0.18 em lower than that line's middle.</summary>
    private static double IconTop(double y, double size, double pt) => y + (size - pt * 4 / 3 * 1.33) / 2 + pt * 4 / 3 * 0.18;

    /// <summary>A labelled reading: small-caps label with the number right under it. Returns the number for extra settings.</summary>
    private static ThemeElement Stat(List<ThemeElement> e, double x, double y, string label, string data, double size, double labelSize = 11)
    {
        e.Add(Txt(x, y, label, Font(labelSize, Secondary, bold: true)));
        var value = Limited(Value(x, y + labelSize * 1.6, data, Num(size, Text)));
        e.Add(value);
        return value;
    }

    /// <summary>Warning levels where a reading can really be too high: CPU, GPU and drive temperatures (and session peaks).</summary>
    private static ThemeElement Limited(ThemeElement value)
    {
        (double, double)? limits = value.DataName switch
        {
            "CPUTEMP" or "GAME_PEAK_CPU" => (90, 98),
            "GPUTEMP" or "GAME_PEAK_GPU" => (83, 90),
            "HDDTEMP" => (70, 80),
            "CPUFAN_STATE" => (50, 100), // red only when the fan stops
            _ => null
        };
        return limits is { } l ? Warn(value, l.Item1, l.Item2) : value;
    }

    private static ThemeElement Warn(ThemeElement element, double warn, double critical)
    {
        element.WarnAt = warn;
        element.CriticalAt = critical;
        return element;
    }

    private static ThemeElement Lines(ThemeElement element, int lines) { element.MaxLines = lines; return element; }

    private static ThemeElement Rule(double x, double y, double w) =>
        new() { Kind = ThemeElementKind.Line, X = x, Y = y, Width = w, Height = 0, LineColor = "#1FFFFFFF", LineWidth = 1 };

    /// <summary>The one card header style: small caps, secondary.</summary>
    private static ThemeElement Header(double x, double y, string text) => Txt(x, y, text, Font(13, Secondary, bold: true));

    /// <summary>The one value label style, above its number.</summary>
    private static ThemeElement Label(double x, double y, string text) => Txt(x, y, text, Font(11, Secondary, bold: true));

    /// <summary>The one number style.</summary>
    private static ThemeFont Num(double size, string color, ThemeTextAlignment align = ThemeTextAlignment.Left) => Font(size, color, align: align, family: Semi);

    private static ThemeFont Font(double size, string color, bool bold = false, ThemeTextAlignment align = ThemeTextAlignment.Left, string family = Ui) =>
        new() { Family = family, Size = size, Color = color, Bold = bold, Alignment = align };

    private static ThemeElement Txt(double x, double y, string text, ThemeFont font, ThemeAction? action = null) =>
        new() { Kind = ThemeElementKind.Text, X = x, Y = y, Text = text, Font = font, Action = action };

    /// <summary>A reading; units are spaced ("188 W") throughout the kit.</summary>
    private static ThemeElement Value(double x, double y, string data, ThemeFont font, bool unit = true, double width = 0, string? format = null, ThemeAction? action = null) =>
        new() { Kind = ThemeElementKind.Data, X = x, Y = y, DataName = data, ShowUnit = unit, SpacedUnit = true, Width = width, DataFormat = format, Font = font, Action = action };
}
