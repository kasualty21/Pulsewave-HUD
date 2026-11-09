using Point = System.Windows.Point;

namespace Pulsewave.Themes.Editor;

/// <summary>A ready-made set of elements (a clock card, a CPU card…) the editor adds as one group.</summary>
public sealed record ThemeWidget(string Name, string Tip, Func<List<ThemeElement>> Build);

/// <summary>
/// The editor's + Widget menu. Each widget is laid out from 0,0 with a card (its first element)
/// that sets its size; <see cref="Place"/> centres it on the theme and shrinks it to fit when needed.
/// Font sizes are points (px = pt × 4/3).
/// </summary>
public static class ThemeWidgets
{
    // Halo: dark tinted glass with a faint white edge, translucent buttons and tracks, sky / pink / violet accents.
    private const string Border = "#33FFFFFF", Track = "#2EFFFFFF", Button = "#40FFFFFF";
    private const string White = "#FFFFFFFF", Dim = "#FFD5DCE6", Mint = "#FF7FD8FF", Blue = "#FFFF8FB1", Lavender = "#FFC3A6FF", Amber = "#FFFFD27A";
    private const string Icons = "Segoe Fluent Icons";

    public static readonly IReadOnlyList<ThemeWidget> All =
    [
        new("Clock", "Split clock with a glowing colon and the long date (Thursday, Dec 31)", Clock),
        new("CPU card", "Load ring, temperature (amber / red when hot), power and clock", () => Hardware("CPU", "CPU", Mint, ("CLOCK", "CPUCLOCK_G"), (90, 98))),
        new("GPU card", "Load ring, temperature (amber / red when hot), power and clock", () => Hardware("GPU", "GPU", Blue, ("CLOCK", "GPUCLOCK_G"), (83, 90))),
        new("Memory card", "Used of total, load and a bar that turns amber / red when nearly full", Memory),
        new("GPU gauge", "A ring gauge with the GPU load in the middle", Gauge),
        new("Now playing", "Round album art in a progress ring, title, artist, time and round play controls", NowPlaying),
        new("Network", "Download and upload speed with small charts", Network),
        new("Volume", "Speaker button (tap to mute) beside a volume track (tap for app volumes)", Volume),
        new("Timer", "Countdown (tap to start or pause), +1 minute and reset buttons", Timer),
        new("Stopwatch", "Stopwatch (tap to start or pause) and reset", Stopwatch),
        new("Claude usage", "Claude Code 5-hour and weekly limits with bars (amber / red near the limit) and reset times", () => Usage("CLAUDE CODE", "CLAUDE", Amber)),
        new("Codex usage", "Codex 5-hour and weekly limits with bars (amber / red near the limit) and reset times", () => Usage("CODEX", "CODEX", Mint)),
        new("Dock", "Four shortcut tiles tinted by their icons and named; Lock / Sleep / keys show as round buttons", Shortcuts),
        new("Quick toggles", "Round buttons for Lights, Theme colors, app volumes and the app drawer", Toggles),
        new("Lights", "OpenRGB lights: open the Lights page, and turn them off and on", LightsWidget),
        new("Gaming", "The game being played (or the last one) with its cover, session time, average and 1% low FPS (amber when it drops), and play time today", Gaming),
        new("Weather", "City, temperature and conditions, labelled high and low, the next sunrise or sunset, and the next two days", Weather),
    ];
    /// <summary>
    /// Copies of the widget's elements as a new group (named after the widget, numbered when the
    /// theme already has one), centred on the theme and scaled down if it would not fit.
    /// </summary>
    public static List<ThemeElement> Place(ThemeWidget widget, ThemeDefinition theme)
    {
        var elements = widget.Build();
        string group = UniqueGroup(widget.Name, theme.Elements);
        double w = elements[0].Width, h = elements[0].Height;
        double s = Math.Min(1, Math.Min(theme.Width * 0.9 / w, theme.Height * 0.9 / h));
        foreach (var e in elements)
        {
            e.Group = group;
            if (s < 1) EditorGeometry.Scale(e, new Point(0, 0), s);
            EditorGeometry.Move(e, (theme.Width - w * s) / 2, (theme.Height - h * s) / 2);
        }
        return elements;
    }

    /// <summary>"Clock", or "Clock 2", "Clock 3"… when the name is taken.</summary>
    public static string UniqueGroup(string name, IEnumerable<ThemeElement> existing)
    {
        var used = existing.Select(e => e.Group).Where(g => g != null).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!used.Contains(name)) return name;
        for (int n = 2; ; n++)
            if (!used.Contains($"{name} {n}")) return $"{name} {n}";
    }

    // --- Building blocks (the Halo kit: small-caps headers and labels, Semibold numbers, thin clock) -------------

    private const string Semi = "Segoe UI Semibold", Light = "Segoe UI Light", Symbols = "Segoe UI Symbol";

    private static ThemeElement Card(double w, double h, ThemeAction? action = null) => new()
    {
        Kind = ThemeElementKind.Panel, Width = w, Height = h, CornerRadius = 32, FillColor = "#D9141A2C", GradientColor = "#E60E1322", GradientAngle = 90,
        BorderColor = "#33FFFFFF", BorderWidth = 1, Action = action
    };

    private static ThemeFont Font(double size, string color, bool bold = false, ThemeTextAlignment align = ThemeTextAlignment.Left, string family = "Segoe UI") =>
        new() { Family = family, Size = size, Color = color, Bold = bold, Alignment = align };

    private static ThemeFont Num(double size, string color = White, ThemeTextAlignment align = ThemeTextAlignment.Left) => Font(size, color, align: align, family: Semi);

    private static ThemeElement Text(double x, double y, string text, ThemeFont font, ThemeAction? action = null) =>
        new() { Kind = ThemeElementKind.Text, X = x, Y = y, Text = text, Font = font, Action = action };

    private static ThemeElement Header(double x, double y, string text) => Text(x, y, text, Font(13, Dim, bold: true));

    private static ThemeElement Label(double x, double y, string text, ThemeTextAlignment align = ThemeTextAlignment.Left) => Text(x, y, text, Font(11, Dim, bold: true, align));

    private static ThemeElement Value(double x, double y, string data, ThemeFont font, bool unit = true, double width = 0, string? format = null, ThemeAction? action = null) =>
        new() { Kind = ThemeElementKind.Data, X = x, Y = y, DataName = data, ShowUnit = unit, Width = width, DataFormat = format, Font = font, Action = action };

    private static ThemeElement Bar(double x, double y, double w, double h, string data, string color, ThemeAction? action = null) =>
        new() { Kind = ThemeElementKind.Bar, X = x, Y = y, Width = w, Height = h, DataName = data, ForeColor = color, GradientColor = Lavender, BackColor = Track, CornerRadius = h / 2, Action = action };

    private static ThemeElement Warn(ThemeElement e, double warn, double critical) { e.WarnAt = warn; e.CriticalAt = critical; return e; }

    /// <summary>A pill button: its card and centred label share the tap action.</summary>
    private static IEnumerable<ThemeElement> PillButton(double x, double y, double w, double h, string label, ThemeAction action, double size = 16)
    {
        yield return new ThemeElement { Kind = ThemeElementKind.Panel, X = x, Y = y, Width = w, Height = h, CornerRadius = h / 2, FillColor = Button, Action = action };
        yield return Text(x + w / 2, y + (h - size * 4 / 3 * 1.33) / 2, label, Font(size, White, bold: true, ThemeTextAlignment.Center), action);
    }

    /// <summary>A round glass button with an icon glyph (or a glyph reading such as the speaker).</summary>
    private static IEnumerable<ThemeElement> Round(double x, double y, double size, string glyph, ThemeAction action, string? data = null, bool filled = false)
    {
        yield return new ThemeElement { Kind = ThemeElementKind.Panel, X = x, Y = y, Width = size, Height = size, CornerRadius = size / 2, FillColor = filled ? White : Button, Action = action, Bevel = true };
        // The icon font's ink sits 0.18 em above a centred text line; move it down so it is centred in the circle.
        double pt = size * 0.3 * 0.75, top = y + (size - pt * 4 / 3 * 1.33) / 2 + pt * 4 / 3 * 0.18;
        var font = Font(pt, filled ? "#FF101626" : White, align: ThemeTextAlignment.Center, family: Icons);
        yield return data != null ? Value(x + size / 2, top, data, font, unit: false, action: action) : Text(x + size / 2, top, glyph, font, action);
    }

    // --- Widgets ----------------------------------------------------------------------------

    private static List<ThemeElement> Gaming()
    {
        var low = Value(282, 198, "GAME_FPS_LOW", Num(18), unit: false, action: ThemeAction.GamingPage);
        low.ColorBy = "GAME_FPS_LOW_PCT"; low.LowIsBad = true; low.WarnAt = 60; low.CriticalAt = 40;
        return
        [
            Card(460, 280, ThemeAction.GamingPage),
            new() { Kind = ThemeElementKind.Shortcut, Slot = ThemeDataContext.GameSlot, X = 24, Y = 24, Width = 154, Height = 232, CornerRadius = 22,
                    FillColor = Button, BorderColor = Border, BorderWidth = 1, LineColor = Dim, Action = ThemeAction.GamingPage },
            Value(200, 30, "GAME_STATE", Font(12, Mint, bold: true), unit: false, action: ThemeAction.GamingPage),
            Value(200, 54, "GAME_NAME", Font(17, White, bold: true), unit: false, width: 236, action: ThemeAction.GamingPage),
            Value(200, 88, "GAME_SESSION", Num(36), unit: false, action: ThemeAction.GamingPage),
            Label(200, 180, "AVG FPS"),
            Value(200, 198, "GAME_FPS_AVG", Num(18), unit: false, action: ThemeAction.GamingPage),
            Label(282, 180, "1% LOW"),
            low,
            Label(364, 180, "TODAY"),
            Value(364, 198, "GAME_TODAY", Num(18), unit: false, action: ThemeAction.GamingPage),
        ];
    }

    private static List<ThemeElement> Weather() =>
    [
        Card(440, 330),
        Text(24, 25, "\uECAF", Font(13, Dim, family: Icons)),
        Value(46, 22, "WEATHER_CITY", Font(14, Dim, bold: true), unit: false, width: 370),
        Value(24, 52, "WEATHER_ICON", Font(48, Amber, family: Symbols), unit: false),
        Value(124, 52, "WEATHER_TEMP", Num(48)),
        Value(416, 74, "WEATHER_TEXT", Font(16, White, bold: true, ThemeTextAlignment.Right), unit: false, width: 180),
        Label(24, 150, "HIGH"), Value(24, 168, "WEATHER_HIGH", Num(18)),
        Label(108, 150, "LOW"), Value(108, 168, "WEATHER_LOW", Num(18)),
        Label(192, 150, "FEELS LIKE"), Value(192, 168, "WEATHER_FEELS", Num(18)),
        Value(416, 150, "WEATHER_SUN_NEXT_LABEL", Font(11, Dim, bold: true, ThemeTextAlignment.Right), unit: false),
        Value(416, 168, "WEATHER_SUN_NEXT", Num(18, White, ThemeTextAlignment.Right), unit: false),
        new() { Kind = ThemeElementKind.Line, X = 24, Y = 222, Width = 392, LineColor = "#1FFFFFFF", LineWidth = 1 },
        Value(24, 236, "WEATHER_D1_DAY", Font(13, Dim, bold: true), unit: false),
        Value(24, 258, "WEATHER_D1_ICON", Font(26, Amber, family: Symbols), unit: false),
        Value(76, 266, "WEATHER_D1_HIGH", Num(16)),
        Value(124, 266, "WEATHER_D1_LOW", Num(16, Dim)),
        Value(232, 236, "WEATHER_D2_DAY", Font(13, Dim, bold: true), unit: false),
        Value(232, 258, "WEATHER_D2_ICON", Font(26, Amber, family: Symbols), unit: false),
        Value(284, 266, "WEATHER_D2_HIGH", Num(16)),
        Value(332, 266, "WEATHER_D2_LOW", Num(16, Dim)),
    ];

    private static List<ThemeElement> Clock() =>
    [
        Card(440, 250),
        Value(204, 22, "TIME", Font(84, White, align: ThemeTextAlignment.Right, family: Light), unit: false, format: "h"),
        new() { Kind = ThemeElementKind.Text, X = 220, Y = 17, Text = ":", Font = Font(84, Mint, align: ThemeTextAlignment.Center, family: Light), GlowColor = "#887FD8FF", GlowSize = 12 },
        Value(236, 22, "TIME", Font(84, White, family: Light), unit: false, format: "m"),
        Value(220, 170, "DATE", Font(17, Dim, bold: true, ThemeTextAlignment.Center), unit: false, format: "long"),
    ];

    private static List<ThemeElement> Hardware(string prefix, string title, string accent, (string Label, string Data) clock, (double Warn, double Critical) hot) =>
    [
        Card(420, 250, ThemeAction.Details),
        Header(24, 20, title),
        Value(70, 18, prefix + "MODEL", Font(12, Dim), unit: false, width: 326),
        new() { Kind = ThemeElementKind.Arc, DataName = prefix + "LOAD", X = 24, Y = 56, Diameter = 160, Width = 160, Height = 160, Thickness = 14,
                StartAngle = 135, SweepAngle = 270, RoundCaps = true, ForeColor = accent, GradientColor = Lavender, BackColor = Track, Action = ThemeAction.Details },
        Value(104, 104, prefix + "LOAD", Num(28, White, ThemeTextAlignment.Center), action: ThemeAction.Details),
        Label(104, 150, "LOAD", ThemeTextAlignment.Center),
        Label(212, 70, "TEMP"), Warn(Value(396, 62, prefix + "TEMP", Num(18, White, ThemeTextAlignment.Right)), hot.Warn, hot.Critical),
        Label(212, 122, "POWER"), Value(396, 114, prefix + "PWR", Num(18, White, ThemeTextAlignment.Right)),
        Label(212, 174, clock.Label), Value(396, 166, clock.Data, Num(18, White, ThemeTextAlignment.Right)),
    ];

    private static List<ThemeElement> Memory() =>
    [
        Card(400, 200, ThemeAction.Details),
        Header(24, 20, "MEMORY"),
        Value(376, 16, "RAMLOAD", Num(16, Dim, ThemeTextAlignment.Right)),
        Value(24, 58, "RAM_GB_OF", Num(36), unit: false),
        Warn(Bar(24, 146, 352, 14, "RAMLOAD", Lavender, ThemeAction.Details), 85, 95),
    ];

    private static List<ThemeElement> Gauge() =>
    [
        Card(280, 320, ThemeAction.Details),
        new() { Kind = ThemeElementKind.Arc, DataName = "GPULOAD", X = 50, Y = 40, Diameter = 180, Width = 180, Height = 180, Thickness = 18,
                StartAngle = 130, SweepAngle = 280, RoundCaps = true, ForeColor = Blue, GradientColor = Lavender, BackColor = Track, Action = ThemeAction.Details },
        Value(140, 96, "GPULOAD", Num(36, White, ThemeTextAlignment.Center)),
        Label(140, 256, "GPU LOAD", ThemeTextAlignment.Center),
    ];

    private static List<ThemeElement> NowPlaying() =>
    [
        Card(620, 240),
        new() { Kind = ThemeElementKind.Arc, DataName = "MEDIA_PROGRESS", X = 24, Y = 24, Diameter = 192, Width = 192, Height = 192, Thickness = 6,
                StartAngle = -90, SweepAngle = 360, RoundCaps = true, ForeColor = Mint, GradientColor = Lavender, BackColor = Track, Action = ThemeAction.MediaPage },
        new() { Kind = ThemeElementKind.AlbumArt, X = 40, Y = 40, Width = 160, Height = 160, CornerRadius = 80, FillColor = Button, LineColor = Dim, Action = ThemeAction.MediaPage },
        Value(244, 28, "MEDIA_TITLE", Font(20, White, bold: true), unit: false, width: 352, action: ThemeAction.MediaPage),
        Value(244, 66, "MEDIA_ARTIST", Font(15, Dim), unit: false, width: 352, action: ThemeAction.MediaPage),
        Value(244, 98, "MEDIA_TIME", Num(13, Mint), unit: false),
        .. Round(272, 148, 56, ThemeData.PreviousGlyph, ThemeAction.PreviousTrack),
        .. Round(388, 140, 72, "", ThemeAction.PlayPause, "MEDIA_PLAYPAUSE", filled: true),
        .. Round(520, 148, 56, ThemeData.NextGlyph, ThemeAction.NextTrack),
    ];

    private static List<ThemeElement> Network() =>
    [
        Card(400, 220),
        Header(24, 20, "NETWORK"),
        Label(24, 62, "DOWNLOAD"),
        Label(212, 62, "UPLOAD"),
        Value(24, 80, "DOWNDSPEED", Num(22), width: 176),
        Value(212, 80, "UPSPEED", Num(22), width: 164),
        new() { Kind = ThemeElementKind.Chart, DataName = "DOWNDSPEED", X = 24, Y = 150, Width = 168, Height = 46, Maximum = 0, Samples = 60,
                LineColor = Mint, FillColor = "#337FD8FF", LineWidth = 2 },
        new() { Kind = ThemeElementKind.Chart, DataName = "UPSPEED", X = 212, Y = 150, Width = 164, Height = 46, Maximum = 0, Samples = 60,
                LineColor = Blue, FillColor = "#33FF8FB1", LineWidth = 2 },
    ];

    private static List<ThemeElement> Volume() =>
    [
        Card(440, 120),
        .. Round(24, 32, 56, "", ThemeAction.Mute, "VOLUME_ICON"),
        Label(100, 36, "VOLUME"),
        Value(416, 32, "VOLUME", Num(14, Dim, ThemeTextAlignment.Right)),
        Bar(100, 66, 316, 18, "VOLUME", Mint, ThemeAction.VolumeMixer),
    ];

    private static List<ThemeElement> Timer() =>
    [
        Card(400, 230),
        Header(24, 20, "TIMER"),
        Value(200, 44, "TIMER", Num(52, White, ThemeTextAlignment.Center), unit: false, action: ThemeAction.TimerToggle),
        .. PillButton(24, 158, 170, 50, "+1 min", ThemeAction.TimerAddMinute),
        .. PillButton(206, 158, 170, 50, "Reset", ThemeAction.TimerReset),
    ];

    private static List<ThemeElement> Stopwatch() =>
    [
        Card(400, 230),
        Header(24, 20, "STOPWATCH"),
        Value(200, 44, "STOPWATCH", Num(52, White, ThemeTextAlignment.Center), unit: false, action: ThemeAction.StopwatchToggle),
        .. PillButton(24, 158, 170, 50, "Start / Pause", ThemeAction.StopwatchToggle, 14),
        .. PillButton(206, 158, 170, 50, "Reset", ThemeAction.StopwatchReset),
    ];

    private static List<ThemeElement> Usage(string title, string prefix, string accent) =>
    [
        Card(420, 230),
        Header(24, 20, title),
        Label(24, 62, "5 HOURS"),
        Value(120, 59, prefix + "_5H_RESET", Font(12, Dim), unit: false, width: 200),
        Value(396, 56, prefix + "_5H", Num(16, White, ThemeTextAlignment.Right)),
        Warn(Bar(24, 90, 372, 12, prefix + "_5H", accent), 75, 90),
        Label(24, 136, "THIS WEEK"),
        Value(120, 133, prefix + "_WEEK_RESET", Font(12, Dim), unit: false, width: 200),
        Value(396, 130, prefix + "_WEEK", Num(16, White, ThemeTextAlignment.Right)),
        Warn(Bar(24, 164, 372, 12, prefix + "_WEEK", accent), 75, 90),
    ];

    private static List<ThemeElement> LightsWidget() =>
    [
        Card(400, 200),
        Header(24, 20, "LIGHTS"),
        .. PillButton(24, 58, 352, 56, "Colors & effects", ThemeAction.LightsPage),
        .. PillButton(24, 126, 352, 50, "Off / On", ThemeAction.LightsToggle),
    ];

    private static List<ThemeElement> Toggles()
    {
        var list = new (string Glyph, string Label, ThemeAction Action)[]
            { ("\uEA80", "Lights", ThemeAction.LightsPage), ("\uE790", "Theme colors", ThemeAction.PalettePage), ("\uE767", "Volume", ThemeAction.VolumeMixer), ("\uE71D", "Apps", ThemeAction.AppDrawer) };
        var elements = new List<ThemeElement> { Card(440, 150) };
        for (int i = 0; i < list.Length; i++)
        {
            double cx = 24 + 98 * (i + 0.5);
            elements.AddRange(Round(cx - 30, 24, 60, list[i].Glyph, list[i].Action));
            elements.Add(Text(cx, 92, list[i].Label, Font(11, Dim, bold: true, ThemeTextAlignment.Center), list[i].Action));
        }
        return elements;
    }

    private static List<ThemeElement> Shortcuts() =>
    [
        Card(560, 180),
        .. Enumerable.Range(0, 4).Select(i => new ThemeElement
        {
            Kind = ThemeElementKind.Shortcut, Slot = i + 1, X = 24 + i * 134, Y = 24, Width = 110, Height = 132,
            CornerRadius = 28, FillColor = Button, BorderColor = Border, BorderWidth = 1, LineColor = Dim, TileStyle = "Tinted", ShowLabel = true
        }),
    ];
}