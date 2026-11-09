using System.IO;
using Pulsewave.Themes;

namespace Pulsewave.Regression;

// Source of the monochrome built-in templates, each for the 8.8" bar (1920x480) and the HYTE Y70 column (682x2560):
//   Graphite (34, 35): Halo's glass in greys only: charcoal cards, white for what matters, ring gauges, soft bevels.
//   Paper (36, 37): e-ink calm: warm off-white, black ink, hairlines instead of cards, a serif clock, segmented meters.
//   Noir (38, 39): poster black and white: heavy type, square corners, thick flat bars, an inverted play button.
// Pictures (album art, shortcut icons, game covers) are shown in grey so nothing breaks the monochrome. Warnings can't
// use color here, so a hot reading steps up to the strongest ink. Regenerate with
// `--widget-shots <dir> --write-oneui-templates <dir>` (it writes these too); `--mono-mockups <dir>` renders and gates them.
internal static class MonoTemplates
{
    private sealed record Style(
        string Name, string Background, string BackgroundEnd, bool Cards, string CardFill, string CardEdge, double Radius,
        string Text, string Secondary, string Track, string Fill, string ClockFont, string NumberFont, string LabelFont, bool LabelBold,
        string ButtonFill, string ButtonEdge, string ButtonText, string PlayFill, string PlayText, string Meter, double ArtRadius, bool Bevel,
        string Rule, string Warn, double ClockScale);

    private static readonly Style Graphite = new("Graphite", "#FF17181B", "#FF0C0D0F", true, "#E01E2024", "#24FFFFFF", 32,
        "#FFF4F4F5", "#FFA3A6AC", "#26FFFFFF", "#FFE6E7EA", "Segoe UI Light", "Segoe UI Semibold", "Segoe UI", true,
        "#2EFFFFFF", "#00000000", "#FFF4F4F5", "#FFF4F4F5", "#FF141518", "rings", 999, true, "#1FFFFFFF", "#FFFFFFFF", 1);
    private static readonly Style Paper = new("Paper", "#FFF2F0EB", "#FFE9E6DF", false, "#00000000", "#00000000", 0,
        "#FF141414", "#FF55524C", "#1F000000", "#FF141414", "Georgia", "Bahnschrift", "Bahnschrift", false,
        "#00000000", "#FF141414", "#FF141414", "#FF141414", "#FFF2F0EB", "segments", 6, false, "#33000000", "#FF000000", 1);
    private static readonly Style Noir = new("Noir", "#FF000000", "#FF000000", false, "#00000000", "#00000000", 0,
        "#FFFFFFFF", "#FF9C9C9C", "#33FFFFFF", "#FFFFFFFF", "Segoe UI Black", "Segoe UI Black", "Segoe UI", true,
        "#00000000", "#FFFFFFFF", "#FFFFFFFF", "#FFFFFFFF", "#FF000000", "bars", 0, false, "#4DFFFFFF", "#FFFFFFFF", 0.88);

    public static IEnumerable<(string File, ThemeDefinition Theme)> All() =>
    [
        ("34-graphite", Bar(Graphite)), ("35-graphite-column", Column(Graphite)),
        ("36-paper", Bar(Paper)), ("37-paper-column", Column(Paper)),
        ("38-noir", Bar(Noir)), ("39-noir-column", Column(Noir)),
    ];

    private const string Icons = "Segoe Fluent Icons", Symbols = "Segoe UI Symbol";
    private const string LightGlyph = "", PaletteGlyph = "", AppsGlyph = "", AestheticGlyph = "", MixerGlyph = "";

    private static readonly (string Glyph, string Label, ThemeAction Action)[] Toggles =
        [(LightGlyph, "Lights", ThemeAction.LightsPage), (PaletteGlyph, "Theme colors", ThemeAction.PalettePage), (AppsGlyph, "Apps", ThemeAction.AppDrawer), (AestheticGlyph, "Wallpaper", ThemeAction.AestheticToggle)];

    /// <summary>Renders each template with sample data and runs the quality gate, worst-case values included.</summary>
    public static void Run(string folder)
    {
        Directory.CreateDirectory(folder);
        foreach (var (file, theme) in All())
        {
            string dir = Path.Combine(folder, file);
            Directory.CreateDirectory(dir);
            ThemeStore.Save(dir, theme);
            var problems = TemplateChecks.Problems(theme, dir, Path.Combine(folder, file + ".png"));
            problems.AddRange(TemplateChecks.StressProblems(theme, dir));
            Console.WriteLine($"{file}: " + (problems.Count == 0 ? "OK" : string.Join("; ", problems)));
        }
    }

    // --- Bar ---------------------------------------------------------------------------------

    private static ThemeDefinition Bar(Style s)
    {
        var t = New(s, s.Name, 1920, 480);
        var e = t.Elements;

        // Clock, date and weather.
        Card(e, s, 24, 24, 480, 432);
        ClockBlock(e, s, 264, 34, 100);
        Divider(e, s, 56, 248, 416);
        WeatherBlock(e, s, 56, 262, 416, 40);

        // Now playing.
        Card(e, s, 528, 24, 640, 432);
        NowPlaying(e, s, 560, 56, 576, 196, 330);

        // System.
        Card(e, s, 1192, 24, 540, 432);
        e.Add(Txt(1224, 56, "SYSTEM", Label(s)));
        if (s.Meter == "rings") Rings(e, s, 1224, 92, 476, 136);
        else Rows(e, s, 1224, 92, 476, 3, 70);
        Divider(e, s, 1224, 330, 476);
        Stat(e, s, 1224, 350, "DOWNLOAD", "DOWNDSPEED", 20);
        Stat(e, s, 1390, 350, "UPLOAD", "UPSPEED", 20);
        Stat(e, s, 1556, 350, "FPS", "FPS", 20).ShowUnit = false;

        // Quick actions, one above the other.
        Card(e, s, 1756, 24, 140, 432);
        for (int i = 0; i < Toggles.Length; i++)
            Toggle(e, s, 1826, 44 + i * 102, 54, Toggles[i]);
        return t;
    }

    // --- Column ------------------------------------------------------------------------------

    private static ThemeDefinition Column(Style s)
    {
        var t = New(s, s.Name + " Column", 682, 2560);
        var e = t.Elements;
        const double x = 24, w = 634, ix = 52, iw = 578, mid = 341;

        ClockBlock(e, s, mid, 48, 128);

        // Weather with the next hours.
        Card(e, s, x, 320, w, 448);
        WeatherBlock(e, s, ix, 352, iw, 56);
        Divider(e, s, ix, 570, iw);
        Hours(e, s, ix, 590, iw);
        e.Add(Value(ix, 712, "WEATHER_RAIN", Font(15, s.Secondary, family: s.LabelFont), unit: false, width: iw));
        if (!s.Cards) Divider(e, s, ix, 776, iw);

        // Now playing.
        Card(e, s, x, 792, w, 440);
        NowPlaying(e, s, ix, 824, iw, 220, 1116);
        if (!s.Cards) Divider(e, s, ix, 1240, iw);

        // System: CPU, GPU, memory and the drive, then the network.
        Card(e, s, x, 1256, w, 520);
        e.Add(Txt(ix, 1288, "SYSTEM", Label(s)));
        if (s.Meter == "rings")
        {
            Rings(e, s, ix, 1326, iw, 164);
            Row(e, s, ix, 1594, iw, "DRIVE", "DRIVE_USED_PCT", "DRIVE_FREE");
        }
        else Rows(e, s, ix, 1326, iw, 4, 84, drive: true);
        Divider(e, s, ix, 1666, iw);
        Stat(e, s, ix, 1686, "DOWNLOAD", "DOWNDSPEED", 22);
        Stat(e, s, ix + iw / 2, 1686, "UPLOAD", "UPSPEED", 22);
        if (!s.Cards) Divider(e, s, ix, 1784, iw);

        // Quick actions and the dock.
        Card(e, s, x, 1800, w, 170);
        double step = iw / Toggles.Length;
        for (int i = 0; i < Toggles.Length; i++)
            Toggle(e, s, ix + step * (i + 0.5), 1828, 68, Toggles[i]);
        if (!s.Cards) Divider(e, s, ix, 1978, iw);

        Card(e, s, x, 1994, w, 542);
        Dock(e, s, ix, 2022, iw, 486);
        return t;
    }

    // --- Pieces ------------------------------------------------------------------------------

    private static ThemeDefinition New(Style s, string name, int w, int h)
    {
        var t = new ThemeDefinition { Name = name, Source = "Pulsewave template", Width = w, Height = h, BackgroundColor = s.Background };
        t.Elements.Add(new ThemeElement { Kind = ThemeElementKind.Panel, Width = w, Height = h, FillColor = s.Background, GradientColor = s.BackgroundEnd, GradientAngle = 90 });
        return t;
    }

    private static void ClockBlock(List<ThemeElement> e, Style s, double cx, double y, double size)
    {
        size *= s.ClockScale;
        e.Add(Value(cx, y, "TIME", Font(size, s.Text, ThemeTextAlignment.Center, s.ClockFont), format: "h:m"));
        e.Add(Value(cx, y + size * 1.62, "DATE", Font(Math.Round(size * 0.18), s.Secondary, ThemeTextAlignment.Center, s.LabelFont), format: "long"));
    }

    private static void WeatherBlock(List<ThemeElement> e, Style s, double x, double y, double w, double size)
    {
        double px = size * 4 / 3;
        e.Add(Value(x, y + 6, "WEATHER_ICON", Font(size, s.Text, family: Symbols), unit: false));
        e.Add(Value(x + px * 1.5, y, "WEATHER_TEMP", Font(size, s.Text, family: s.NumberFont)));
        e.Add(Value(x + w, y + 10, "WEATHER_TEXT", Font(16, s.Text, ThemeTextAlignment.Right, s.LabelFont, bold: true), unit: false, width: w - px * 3.2));
        e.Add(Value(x + w, y + 40, "WEATHER_CITY", Font(13, s.Secondary, ThemeTextAlignment.Right, s.LabelFont), unit: false, width: w - px * 3.2));
        double labels = y + px * 1.5 + 10;
        Stat(e, s, x, labels, "HIGH", "WEATHER_HIGH", 20);
        Stat(e, s, x + 100, labels, "LOW", "WEATHER_LOW", 20);
        e.Add(Value(x + w, labels, "WEATHER_SUN_NEXT_LABEL", Label(s, ThemeTextAlignment.Right), unit: false));
        e.Add(Value(x + w, labels + 20, "WEATHER_SUN_NEXT", Font(20, s.Text, ThemeTextAlignment.Right, s.NumberFont), unit: false));
    }

    private static void Hours(List<ThemeElement> e, Style s, double x, double y, double w)
    {
        double step = w / 4;
        for (int i = 1; i <= 4; i++)
        {
            double cx = x + step * (i - 0.5);
            string h = "WEATHER_H" + i + "_";
            e.Add(Value(cx, y, h + "TIME", Label(s, ThemeTextAlignment.Center, 12), unit: false));
            e.Add(Value(cx, y + 22, h + "ICON", Font(28, s.Text, ThemeTextAlignment.Center, Symbols), unit: false));
            e.Add(Value(cx, y + 72, h + "TEMP", Font(18, s.Text, ThemeTextAlignment.Center, s.NumberFont)));
        }
    }

    /// <summary>Art, title (two lines), artist, album and time, a progress meter, then previous / play / next and the volume.</summary>
    private static void NowPlaying(List<ThemeElement> e, Style s, double x, double y, double w, double art, double controlsY)
    {
        e.Add(new ThemeElement { Kind = ThemeElementKind.AlbumArt, X = x, Y = y, Width = art, Height = art, CornerRadius = Math.Min(s.ArtRadius, art / 2),
            FillColor = s.Cards ? s.ButtonFill : s.Track, LineColor = s.Secondary, Action = ThemeAction.MediaPage, Grayscale = true });
        double tx = x + art + 30, tw = w - art - 30;
        e.Add(Txt(tx, y + 4, "NOW PLAYING", Label(s)));
        var title = Value(tx, y + 28, "MEDIA_TITLE", Font(22, s.Text, family: s.LabelFont, bold: true), unit: false, width: tw, action: ThemeAction.MediaPage);
        title.MaxLines = 2;
        e.Add(title);
        e.Add(Value(tx, y + 104, "MEDIA_ARTIST", Font(16, s.Secondary, family: s.LabelFont), unit: false, width: tw, action: ThemeAction.MediaPage));
        e.Add(Value(tx, y + 132, "MEDIA_ALBUM", Font(14, s.Secondary, family: s.LabelFont), unit: false, width: tw, action: ThemeAction.MediaPage));
        e.Add(Value(tx, y + 166, "MEDIA_TIME", Font(14, s.Text, family: s.NumberFont), unit: false));
        Meter(e, s, x, y + art + 30, w, 8, "MEDIA_PROGRESS", ThemeAction.MediaPage);
        double cx = x + w / 2 - 40;
        e.AddRange(Round(s, cx - 150 - 30, controlsY + 10, 60, "\uE892", ThemeAction.PreviousTrack));
        e.AddRange(Play(s, cx, controlsY, 80));
        e.AddRange(Round(s, cx + 150 - 30, controlsY + 10, 60, "\uE893", ThemeAction.NextTrack));
        e.Add(Txt(x + w, controlsY + 16, "VOLUME", Label(s, ThemeTextAlignment.Right)));
        e.Add(Value(x + w, controlsY + 36, "VOLUME", Font(18, s.Text, ThemeTextAlignment.Right, s.NumberFont), action: ThemeAction.VolumeMixer));
    }

    private static void Rings(List<ThemeElement> e, Style s, double x, double y, double w, double d)
    {
        var rows = new (string Name, string Load, string Detail)[] { ("CPU", "CPULOAD", "CPUTEMP"), ("GPU", "GPULOAD", "GPUTEMP"), ("MEMORY", "RAMLOAD", "RAM_GB_OF") };
        for (int i = 0; i < 3; i++)
        {
            double cx = x + w / 6 + i * w / 3;
            e.Add(new ThemeElement { Kind = ThemeElementKind.Arc, DataName = rows[i].Load, X = cx - d / 2, Y = y, Width = d, Height = d, Diameter = d, Thickness = d / 11,
                StartAngle = 135, SweepAngle = 270, RoundCaps = true, ForeColor = s.Fill, BackColor = s.Track, Action = ThemeAction.Details });
            e.Add(Value(cx, y + d * 0.32, rows[i].Load, Font(Math.Round(d * 0.17), s.Text, ThemeTextAlignment.Center, s.NumberFont)));
            e.Add(Txt(cx, y + d + 10, rows[i].Name, Label(s, ThemeTextAlignment.Center)));
            e.Add(Hot(s, Value(cx, y + d + 30, rows[i].Detail, Font(16, s.Text, ThemeTextAlignment.Center, s.NumberFont), unit: rows[i].Detail != "RAM_GB_OF")));
        }
    }

    private static void Rows(List<ThemeElement> e, Style s, double x, double y, double w, int count, double step, bool drive = false)
    {
        var rows = new (string Name, string Load, string Detail)[] { ("CPU", "CPULOAD", "CPUTEMP"), ("GPU", "GPULOAD", "GPUTEMP"), ("MEMORY", "RAMLOAD", "RAM_GB_OF"), ("DRIVE", "DRIVE_USED_PCT", "DRIVE_FREE") };
        for (int i = 0; i < count; i++) Row(e, s, x, y + i * step, w, rows[i].Name, rows[i].Load, rows[i].Detail);
    }

    /// <summary>Name, a detail (temperature, used of total, free space), the percentage large on the right, and a meter.</summary>
    private static void Row(List<ThemeElement> e, Style s, double x, double y, double w, string name, string load, string detail)
    {
        e.Add(Txt(x, y + 6, name, Label(s)));
        e.Add(Hot(s, Value(x + 116, y, detail, Font(18, s.Secondary, family: s.NumberFont), unit: detail != "RAM_GB_OF")));
        e.Add(Value(x + w, y - 6, load, Font(26, s.Text, ThemeTextAlignment.Right, s.NumberFont)));
        Meter(e, s, x, y + 38, w, s.Meter == "bars" ? 14 : 12, load, ThemeAction.Details);
    }

    private static void Dock(List<ThemeElement> e, Style s, double x, double y, double w, double h)
    {
        const double gap = 16;
        double tw = (w - gap * 3) / 4, th = (h - gap) / 2;
        for (int r = 0; r < 2; r++)
            for (int i = 0; i < 4; i++)
                e.Add(new ThemeElement { Kind = ThemeElementKind.Shortcut, Slot = 1 + r * 4 + i, X = x + i * (tw + gap), Y = y + r * (th + gap), Width = tw, Height = th,
                    CornerRadius = s.Cards ? 26 : s.ArtRadius, FillColor = s.Cards ? s.ButtonFill : "#00000000", BorderColor = s.Cards ? "#00000000" : s.ButtonEdge,
                    BorderWidth = s.Cards ? 0 : 1.5, LineColor = s.Secondary, ShowLabel = true, Grayscale = true });
    }

    private static void Toggle(List<ThemeElement> e, Style s, double cx, double y, double size, (string Glyph, string Label, ThemeAction Action) toggle)
    {
        e.AddRange(Round(s, cx - size / 2, y, size, toggle.Glyph, toggle.Action));
        e.Add(Txt(cx, y + size + 8, toggle.Label, Label(s, ThemeTextAlignment.Center), toggle.Action));
    }

    private static void Card(List<ThemeElement> e, Style s, double x, double y, double w, double h)
    {
        if (!s.Cards) return;
        e.Add(new ThemeElement { Kind = ThemeElementKind.Panel, X = x, Y = y, Width = w, Height = h, CornerRadius = s.Radius, FillColor = s.CardFill, BorderColor = s.CardEdge, BorderWidth = 1 });
    }

    private static void Divider(List<ThemeElement> e, Style s, double x, double y, double w) =>
        e.Add(new ThemeElement { Kind = ThemeElementKind.Line, X = x, Y = y, Width = w, LineColor = s.Rule, LineWidth = 1 });

    private static void Meter(List<ThemeElement> e, Style s, double x, double y, double w, double h, string data, ThemeAction action)
    {
        bool segments = s.Meter == "segments";
        var bar = new ThemeElement { Kind = ThemeElementKind.Bar, DataName = data, X = x, Y = y, Width = w, Height = h, ForeColor = s.Fill, BackColor = s.Track,
            CornerRadius = s.Meter == "bars" || segments ? 0 : h / 2, SegmentWidth = segments ? 10 : 0, SegmentGap = segments ? 4 : 0, Action = action };
        e.Add(bar);
    }

    private static IEnumerable<ThemeElement> Round(Style s, double x, double y, double size, string glyph, ThemeAction action)
    {
        yield return new ThemeElement { Kind = ThemeElementKind.Panel, X = x, Y = y, Width = size, Height = size, CornerRadius = s.Cards ? size / 2 : s.ArtRadius,
            FillColor = s.ButtonFill, BorderColor = s.ButtonEdge, BorderWidth = s.ButtonEdge.StartsWith("#00", StringComparison.Ordinal) ? 0 : 1.5, Action = action, Bevel = s.Bevel };
        double pt = size * 0.3 * 0.75;
        yield return Txt(x + size / 2, IconTop(y, size, pt), glyph, Font(pt, s.ButtonText, ThemeTextAlignment.Center, Icons), action);
    }

    private static IEnumerable<ThemeElement> Play(Style s, double cx, double y, double size)
    {
        yield return new ThemeElement { Kind = ThemeElementKind.Panel, X = cx - size / 2, Y = y, Width = size, Height = size, CornerRadius = s.Cards ? size / 2 : s.ArtRadius,
            FillColor = s.PlayFill, Action = ThemeAction.PlayPause, Bevel = s.Bevel };
        double pt = size * 0.3 * 0.75;
        yield return Value(cx, IconTop(y, size, pt), "MEDIA_PLAYPAUSE", Font(pt, s.PlayText, ThemeTextAlignment.Center, Icons), unit: false, action: ThemeAction.PlayPause);
    }

    /// <summary>Where an icon's text line starts so its ink is centred (the icon font sits 0.18 em high in its line).</summary>
    private static double IconTop(double y, double size, double pt) => y + (size - pt * 4 / 3 * 1.33) / 2 + pt * 4 / 3 * 0.18;

    private static ThemeElement Stat(List<ThemeElement> e, Style s, double x, double y, string label, string data, double size)
    {
        e.Add(Txt(x, y, label, Label(s)));
        var value = Hot(s, Value(x, y + 20, data, Font(size, s.Text, family: s.NumberFont)));
        e.Add(value);
        return value;
    }

    /// <summary>Hot temperatures step up to the strongest ink (white, or black on Paper); levels as in the Halo kit.</summary>
    private static ThemeElement Hot(Style s, ThemeElement value)
    {
        (double, double)? limits = value.DataName switch { "CPUTEMP" => (90, 98), "GPUTEMP" => (83, 90), _ => null };
        if (limits is { } l) { (value.WarnAt, value.CriticalAt) = l; value.WarnColor = value.CriticalColor = s.Warn; }
        return value;
    }

    private static ThemeFont Label(Style s, ThemeTextAlignment align = ThemeTextAlignment.Left, double size = 11) =>
        new() { Family = s.LabelFont, Size = size, Color = s.Secondary, Bold = s.LabelBold, Alignment = align };

    private static ThemeFont Font(double size, string color, ThemeTextAlignment align = ThemeTextAlignment.Left, string family = "Segoe UI", bool bold = false) =>
        new() { Family = family, Size = size, Color = color, Alignment = align, Bold = bold };

    private static ThemeElement Txt(double x, double y, string text, ThemeFont font, ThemeAction? action = null) =>
        new() { Kind = ThemeElementKind.Text, X = x, Y = y, Text = text, Font = font, Action = action };

    private static ThemeElement Value(double x, double y, string data, ThemeFont font, bool unit = true, double width = 0, string? format = null, ThemeAction? action = null) =>
        new() { Kind = ThemeElementKind.Data, X = x, Y = y, DataName = data, ShowUnit = unit, SpacedUnit = true, Width = width, DataFormat = format, Font = font, Action = action };
}
