using System.IO;
using Pulsewave.Themes;

namespace Pulsewave.Regression;

// Style directions to choose from, built as real themes for the HYTE Y70 column so they render with
// the HUD's own engine (sample data). `--style-mockups <folder>` writes a PNG and theme.json for each
// and runs the template quality gate. Not shipped: a picked direction becomes a built-in template.
internal static class StyleMockups
{
    private sealed record Style(
        string Name, string Background, string BackgroundEnd, (string Color, double X, double Y, double R)[] Glows,
        bool Cards, string CardFill, string CardFillEnd, string CardBorder, double BorderWidth, double Radius, string? CardGlow,
        string Text, string Secondary, string Accent, string Accent2, string Track, string Fill, string FillEnd,
        string ClockFont, bool ClockBold, string ButtonFill, string ButtonText, string PlayFill, string PlayText, string Gauges, string WeatherIcon);

    private static readonly Style[] Styles =
    [
        // Nocturne: the purple reference grown up. Plum depth, hairline glowing magenta outlines, warm accent.
        new("Nocturne", "#FF1A0F2E", "#FF0A0614",
            [("#66B03CFF", 0.2, 0.18, 0.55), ("#447A1FA8", 0.9, 0.55, 0.6), ("#33FF4FA3", 0.1, 0.95, 0.5)],
            true, "#2A2A1450", "#1A12082A", "#AAE05CFF", 1.5, 34, "#88C04DFF",
            "#FFF7F1FF", "#FFCDB8E8", "#FFE07CFF", "#FFFFB86B", "#33FFFFFF", "#FFB55CFF", "#FFFF7AD9",
            "Segoe UI Light", false, "#33E07CFF", "#FFF7F1FF", "#FFE9D7FF", "#FF2A0F45", "bars", "#FFFFB86B"),
        // Linen: the light reference, calmer. Warm paper, frosted white cards, dark ink, amber accent.
        new("Linen", "#FFE9E4DC", "#FFCFD6DF",
            [("#66FFB27A", 0.85, 0.08, 0.5), ("#5590A8C8", 0.1, 0.7, 0.6)],
            true, "#C8FFFFFF", "#A8FFFFFF", "#80FFFFFF", 1.5, 36, null,
            "#FF1E2430", "#FF4A515C", "#FF8F4205", "#FF2F5F96", "#26182030", "#FFE07A1F", "#FFF2A65A",
            "Segoe UI Light", false, "#FFEFEAE3", "#FF1E2430", "#FF1E2430", "#FFFFFFFF", "bars", "#FF9C4C08"),
        // Hairline: typographic minimalism. No cards, thin rules, big numerals, one mint accent, segmented meters.
        new("Hairline", "#FF07090B", "#FF07090B", [],
            false, "#00000000", "#00000000", "#00000000", 0, 0, null,
            "#FFF2F4F5", "#FF8D969E", "#FF62F0C8", "#FF62F0C8", "#22FFFFFF", "#FF62F0C8", "#FF62F0C8",
            "Segoe UI Semilight", false, "#00000000", "#FFF2F4F5", "#FF62F0C8", "#FF07090B", "segments", "#FFF2F4F5"),
        // Halo: One UI pushed further. Colorful glows under heavier glass, ring gauges.
        new("Halo", "#FF0E1424", "#FF0A0E1A",
            [("#6636C2FF", 0.1, 0.15, 0.6), ("#55FF5E8A", 0.95, 0.42, 0.55), ("#4444E0A0", 0.2, 0.85, 0.6)],
            true, "#8C141A2C", "#A60E1322", "#40FFFFFF", 1, 44, null,
            "#FFFFFFFF", "#FFD5DCE6", "#FF7FD8FF", "#FFFF8FB1", "#2EFFFFFF", "#FF7FD8FF", "#FFC3A6FF",
            "Segoe UI Light", false, "#40FFFFFF", "#FFFFFFFF", "#FFFFFFFF", "#FF101626", "rings", "#FFFFD27A"),
    ];

    private const string Ui = "Segoe UI", Icons = "Segoe Fluent Icons", Symbols = "Segoe UI Symbol";
    private const string PinGlyph = "", PrevGlyph = "", NextGlyph = "", LightGlyph = "", PaletteGlyph = "",
        AppsGlyph = "", MixerGlyph = "", AestheticGlyph = "", MusicGlyph = "";

    public static void Run(string folder)
    {
        Directory.CreateDirectory(folder);
        foreach (var style in Styles)
        {
            var theme = Column(style);
            string dir = Path.Combine(folder, style.Name);
            Directory.CreateDirectory(dir);
            ThemeStore.Save(dir, theme);
            var problems = TemplateChecks.Problems(theme, dir, Path.Combine(folder, style.Name + ".png"));
            Console.WriteLine($"{style.Name}: " + (problems.Count == 0 ? "OK" : string.Join("; ", problems)));
        }
    }

    private static ThemeDefinition Column(Style s)
    {
        var t = new ThemeDefinition { Name = s.Name, Source = "Pulsewave style mockup", Width = 682, Height = 2560, BackgroundColor = s.BackgroundEnd };
        var e = t.Elements;
        e.Add(new ThemeElement { Kind = ThemeElementKind.Panel, Width = 682, Height = 2560, FillColor = s.Background, GradientColor = s.BackgroundEnd, GradientAngle = 90 });
        foreach (var (color, gx, gy, gr) in s.Glows)
        {
            double r = 2560 * gr * 0.5;
            e.Add(new ThemeElement { Kind = ThemeElementKind.Panel, X = 682 * gx - r, Y = 2560 * gy - r, Width = 2 * r, Height = 2 * r, CornerRadius = r,
                FillColor = color, GradientColor = "#00" + color[3..], GradientStyle = "Radial" });
        }
        const double x = 32, w = 618, mid = 341;
        bool light = s.Name == "Linen", bare = !s.Cards;

        // Clock: Hairline goes huge and left-aligned; the others centre it.
        if (bare)
        {
            e.Add(Value(40, 40, "TIME", Font(128, s.Text, family: s.ClockFont), format: "h:m"));
            e.Add(Value(48, 238, "DAY", Font(18, s.Accent, bold: true), unit: false));
            e.Add(Value(120, 238, "DATE", Font(18, s.Secondary), format: "Y-M-D"));
            e.Add(Rule(48, 300, 586, s));
        }
        else
        {
            e.Add(Value(mid, 52, "TIME", Font(118, s.Text, s.ClockBold, ThemeTextAlignment.Center, s.ClockFont), format: "h:m"));
            e.Add(Value(mid - 10, 240, "DAY", Font(19, s.Accent, bold: true, ThemeTextAlignment.Right)));
            e.Add(Value(mid + 10, 240, "DATE", Font(19, s.Secondary), format: "Y-M-D"));
            if (s.CardGlow != null)
                e.Add(new ThemeElement { Kind = ThemeElementKind.Line, X = 241, Y = 292, Width = 200, Height = 0, LineColor = s.Accent, LineWidth = 2, GlowColor = s.CardGlow, GlowSize = 10 });
        }

        // Weather.
        Card(e, s, x, 330, w, 390);
        e.Add(Text(68, 366, PinGlyph, Font(14, s.Secondary, family: Icons)));
        e.Add(Value(96, 362, "WEATHER_CITY", Font(16, s.Secondary, bold: true), unit: false, width: 400));
        e.Add(Value(66, 398, "WEATHER_TEMP", Font(84, s.Text, family: s.ClockFont)));
        e.Add(Value(612, 396, "WEATHER_ICON", Font(80, s.WeatherIcon, align: ThemeTextAlignment.Right, family: Symbols), unit: false));
        e.Add(Value(68, 546, "WEATHER_TEXT", Font(21, s.Text, bold: true), unit: false, width: 540));
        e.Add(Small(68, 598, "H", s)); e.Add(Value(92, 594, "WEATHER_HIGH", Font(16, s.Text)));
        e.Add(Small(160, 598, "L", s)); e.Add(Value(182, 594, "WEATHER_LOW", Font(16, s.Text)));
        e.Add(Small(612, 598, "SUNSET", s, ThemeTextAlignment.Right));
        e.Add(Value(612, 620, "WEATHER_SUNSET", Font(16, s.Text, align: ThemeTextAlignment.Right), unit: false));
        e.Add(Value(68, 650, "WEATHER_D1_ICON", Font(20, s.WeatherIcon, family: Symbols), unit: false));
        e.Add(Value(108, 655, "WEATHER_D1_DAY", Font(14, s.Secondary, bold: true), unit: false));
        e.Add(Value(160, 653, "WEATHER_D1_HIGH", Font(15, s.Text)));
        e.Add(Value(250, 650, "WEATHER_D2_ICON", Font(20, s.WeatherIcon, family: Symbols), unit: false));
        e.Add(Value(290, 655, "WEATHER_D2_DAY", Font(14, s.Secondary, bold: true), unit: false));
        e.Add(Value(342, 653, "WEATHER_D2_HIGH", Font(15, s.Text)));
        if (bare) e.Add(Rule(48, 740, 586, s));

        // Now playing.
        Card(e, s, x, 760, w, 560);
        double art = bare ? 586 : 180;
        if (bare)
        {
            // Hairline: a wide letterboxed cover strip, then type.
            e.Add(new ThemeElement { Kind = ThemeElementKind.AlbumArt, X = 48, Y = 780, Width = 586, Height = 220, CornerRadius = 6, FillColor = "#FF15191D", LineColor = s.Secondary, Action = ThemeAction.MediaPage });
            e.Add(Value(48, 1024, "MEDIA_TITLE", Font(24, s.Text, bold: true), unit: false, width: 586, action: ThemeAction.MediaPage));
            e.Add(Value(48, 1066, "MEDIA_ARTIST", Font(17, s.Secondary), unit: false, width: 586, action: ThemeAction.MediaPage));
        }
        else
        {
            e.Add(new ThemeElement { Kind = ThemeElementKind.AlbumArt, X = 68, Y = 796, Width = art, Height = art, CornerRadius = s.Radius * 0.7,
                FillColor = s.ButtonFill, LineColor = s.Secondary, Action = ThemeAction.MediaPage, GlowColor = s.CardGlow, GlowSize = 18 });
            e.Add(Value(276, 824, "MEDIA_TITLE", Font(22, s.Text, bold: true), unit: false, width: 330, action: ThemeAction.MediaPage));
            e.Add(Value(276, 868, "MEDIA_ARTIST", Font(17, s.Secondary), unit: false, width: 330, action: ThemeAction.MediaPage));
            e.Add(Value(276, 920, "MEDIA_STATE", Font(13, s.Accent, bold: true), unit: false, width: 330, action: ThemeAction.MediaPage));
        }
        double by = bare ? 1110 : 1010;
        e.AddRange(Round(mid - 170 - 40, by + 12, 80, PrevGlyph, ThemeAction.PreviousTrack, s.ButtonFill, s.ButtonText, s));
        e.AddRange(Play(mid, by, 104, s));
        e.AddRange(Round(mid + 170 - 40, by + 12, 80, NextGlyph, ThemeAction.NextTrack, s.ButtonFill, s.ButtonText, s));
        double vy = bare ? 1250 : 1196;
        e.Add(Small(68, vy - 30, "VOLUME", s));
        e.Add(Value(612, vy - 34, "VOLUME", Font(14, s.Secondary, bold: true, ThemeTextAlignment.Right)));
        e.Add(Bar(68, vy, 544, bare ? 10 : 56, "VOLUME", s, ThemeAction.VolumeMixer));
        if (!bare)
        {
            e.Add(new ThemeElement { Kind = ThemeElementKind.Panel, X = 68, Y = vy, Width = 56, Height = 56, CornerRadius = 28, FillColor = s.FillEnd, Action = ThemeAction.Mute });
            e.Add(Value(96, vy + 13, "VOLUME_ICON", Font(16, "#FF161A24", align: ThemeTextAlignment.Center, family: Icons), unit: false, action: ThemeAction.Mute));
        }
        if (bare) e.Add(Rule(48, 1340, 586, s));

        // System: bars, rings or segmented meters.
        Card(e, s, x, 1360, w, 500);
        e.Add(Small(68, 1392, bare ? "SYSTEM" : "System", s, size: bare ? 13 : 18, color: bare ? s.Secondary : s.Text));
        var stats = new (string Name, string Load, string Detail)[] { ("CPU", "CPULOAD", "CPUTEMP"), ("GPU", "GPULOAD", "GPUTEMP"), ("MEMORY", "RAMLOAD", "RAM_GB") };
        if (s.Gauges == "rings")
        {
            for (int i = 0; i < 3; i++)
            {
                double cx = 136 + i * 205;
                e.Add(new ThemeElement { Kind = ThemeElementKind.Arc, DataName = stats[i].Load, X = cx - 82, Y = 1460, Width = 164, Height = 164, Diameter = 164, Thickness = 16,
                    StartAngle = 135, SweepAngle = 270, RoundCaps = true, ForeColor = i == 1 ? s.Accent2 : s.Accent, GradientColor = s.FillEnd, BackColor = s.Track, Action = ThemeAction.Details });
                e.Add(Value(cx, 1514, stats[i].Load, Font(24, s.Text, bold: true, ThemeTextAlignment.Center), action: ThemeAction.Details));
                e.Add(Text(cx, 1650, stats[i].Name, Font(13, s.Secondary, bold: true, ThemeTextAlignment.Center)));
                e.Add(Value(cx, 1678, stats[i].Detail, Font(16, s.Text, align: ThemeTextAlignment.Center)));
            }
            e.Add(new ThemeElement { Kind = ThemeElementKind.Chart, DataName = "CPULOAD", X = 68, Y = 1740, Width = 544, Height = 90, LineColor = s.Accent, FillColor = "#22" + s.Accent[3..], LineWidth = 2.5, Samples = 60 });
        }
        else
        {
            for (int i = 0; i < 3; i++)
            {
                double y = 1446 + i * 130;
                e.Add(Small(68, y + 6, stats[i].Name, s));
                e.Add(Value(612, y - 6, stats[i].Load, Font(bare ? 34 : 28, s.Text, bold: !bare, ThemeTextAlignment.Right, bare ? s.ClockFont : Ui)));
                e.Add(Value(bare ? 496 : 500, y + 4, stats[i].Detail, Font(16, s.Secondary, align: ThemeTextAlignment.Right)));
                e.Add(bare
                    ? new ThemeElement { Kind = ThemeElementKind.Bar, X = 68, Y = y + 62, Width = 544, Height = 16, DataName = stats[i].Load, SegmentWidth = 8, SegmentGap = 4,
                        CornerRadius = 2, ForeColor = s.Accent, BackColor = s.Track, Action = ThemeAction.Details }
                    : Bar(68, y + 56, 544, 30, stats[i].Load, s, ThemeAction.Details));
            }
        }
        if (bare) e.Add(Rule(48, 1880, 586, s));

        // Quick toggles.
        Card(e, s, x, 1900, w, 330);
        var toggles = new (string Glyph, string Label, ThemeAction Action)[]
        {
            (LightGlyph, "Lights", ThemeAction.LightsPage), (PaletteGlyph, "Colors", ThemeAction.PalettePage), (MixerGlyph, "Volume", ThemeAction.VolumeMixer),
            (AppsGlyph, "Apps", ThemeAction.AppDrawer), (MusicGlyph, "Media", ThemeAction.MediaPage), (AestheticGlyph, "Wallpaper", ThemeAction.AestheticToggle),
        };
        for (int i = 0; i < toggles.Length; i++)
        {
            double cx = 141 + i % 3 * 200, cy = 1932 + i / 3 * 148;
            bool on = i == 0;
            e.AddRange(Round(cx - 42, cy, 84, toggles[i].Glyph, toggles[i].Action, on ? s.PlayFill : s.ButtonFill, on ? s.PlayText : s.ButtonText, s));
            e.Add(Text(cx, cy + 92, toggles[i].Label, Font(13, s.Secondary, bold: true, ThemeTextAlignment.Center), toggles[i].Action));
        }
        if (bare) e.Add(Rule(48, 2250, 586, s));

        // Shortcuts.
        Card(e, s, x, 2270, w, 250);
        for (int i = 0; i < 4; i++)
            e.Add(new ThemeElement { Kind = ThemeElementKind.Shortcut, Slot = i + 1, X = 62 + i * 142, Y = 2304, Width = 132, Height = 180, CornerRadius = bare ? 8 : s.Radius * 0.8,
                FillColor = bare ? "#FF111417" : s.ButtonFill, BorderColor = s.CardBorder, BorderWidth = s.Cards ? s.BorderWidth : 0, LineColor = s.Secondary });
        return t;
    }

    private static void Card(List<ThemeElement> e, Style s, double x, double y, double w, double h)
    {
        if (!s.Cards) return;
        e.Add(new ThemeElement { Kind = ThemeElementKind.Panel, X = x, Y = y, Width = w, Height = h, CornerRadius = s.Radius, FillColor = s.CardFill, GradientColor = s.CardFillEnd,
            GradientAngle = 90, BorderColor = s.CardBorder, BorderWidth = s.BorderWidth, GlowColor = s.CardGlow, GlowSize = 16 });
    }

    private static ThemeElement Rule(double x, double y, double w, Style s) =>
        new() { Kind = ThemeElementKind.Line, X = x, Y = y, Width = w, Height = 0, LineColor = "#26FFFFFF", LineWidth = 1 };

    private static ThemeElement Bar(double x, double y, double w, double h, string data, Style s, ThemeAction action) =>
        new() { Kind = ThemeElementKind.Bar, X = x, Y = y, Width = w, Height = h, CornerRadius = h / 2, DataName = data, BackColor = s.Track, ForeColor = s.Fill,
            GradientColor = s.FillEnd, Action = action };

    private static IEnumerable<ThemeElement> Round(double x, double y, double size, string glyph, ThemeAction action, string fill, string text, Style s)
    {
        yield return new ThemeElement { Kind = ThemeElementKind.Panel, X = x, Y = y, Width = size, Height = size, CornerRadius = s.Cards ? size / 2 : 10, FillColor = fill,
            BorderColor = s.Cards ? "#00000000" : "#40FFFFFF", BorderWidth = s.Cards ? 0 : 1, Action = action };
        double pt = size * 0.3 * 0.75;
        yield return Text(x + size / 2, y + (size - pt * 4 / 3 * 1.33) / 2, glyph, Font(pt, text, align: ThemeTextAlignment.Center, family: Icons), action);
    }

    private static IEnumerable<ThemeElement> Play(double cx, double y, double size, Style s)
    {
        yield return new ThemeElement { Kind = ThemeElementKind.Panel, X = cx - size / 2, Y = y, Width = size, Height = size, CornerRadius = s.Cards ? size / 2 : 12,
            FillColor = s.PlayFill, Action = ThemeAction.PlayPause, GlowColor = s.CardGlow, GlowSize = 20 };
        double pt = size * 0.3 * 0.75;
        yield return Value(cx, y + (size - pt * 4 / 3 * 1.33) / 2, "MEDIA_PLAYPAUSE", Font(pt, s.PlayText, align: ThemeTextAlignment.Center, family: Icons), unit: false, action: ThemeAction.PlayPause);
    }

    private static ThemeElement Small(double x, double y, string text, Style s, ThemeTextAlignment align = ThemeTextAlignment.Left, double size = 12, string? color = null) =>
        Text(x, y, text, Font(size, color ?? s.Secondary, bold: true, align));

    private static ThemeFont Font(double size, string color, bool bold = false, ThemeTextAlignment align = ThemeTextAlignment.Left, string family = Ui) =>
        new() { Family = family, Size = size, Color = color, Bold = bold, Alignment = align };

    private static ThemeElement Text(double x, double y, string text, ThemeFont font, ThemeAction? action = null) =>
        new() { Kind = ThemeElementKind.Text, X = x, Y = y, Text = text, Font = font, Action = action };

    private static ThemeElement Value(double x, double y, string data, ThemeFont font, bool unit = true, double width = 0, string? format = null, ThemeAction? action = null) =>
        new() { Kind = ThemeElementKind.Data, X = x, Y = y, DataName = data, ShowUnit = unit, Width = width, DataFormat = format, Font = font, Action = action };
}
