using Pulsewave.Themes;

namespace Pulsewave.Regression;

// Source of the two One UI built-in templates (26-one-ui, 27-one-ui-column). Run
// `--write-oneui-templates <folder>` to regenerate their theme.json files, then copy them into
// src/Pulsewave/Templates. Colors match the HUD's One UI pages (OneUi.cs).
internal static class OneUiTemplates
{
    private const string Text = "#FFF5F7FA", Secondary = "#FFC9D0DA", Glass = "#1FFFFFFF", Edge = "#1CFFFFFF", Off = "#33FFFFFF",
        OnFill = "#FFE9ECF0", OnText = "#FF1D2330", Track = "#2EFFFFFF", FillLow = "#FFC9CDD3", FillHigh = "#FFF6F7F9", Warm = "#FFFFC27A";
    private const string Ui = "Segoe UI", Light = "Segoe UI Light", Icons = "Segoe Fluent Icons", Symbols = "Segoe UI Symbol";
    private const string PinGlyph = "", PrevGlyph = "", NextGlyph = "", LightGlyph = "", PaletteGlyph = "",
        AppsGlyph = "", MixerGlyph = "", AestheticGlyph = "", MusicGlyph = "", PowerGlyph = "";

    public static ThemeDefinition Bar()
    {
        var t = new ThemeDefinition { Name = "One UI", Source = "Pulsewave template", Width = 1920, Height = 480, BackgroundColor = "#FF0D121E" };
        var e = t.Elements;
        Backdrop(e, 1920, 480);

        // Clock and weather.
        Card(e, 24, 24, 440, 432);
        e.Add(Value(244, 40, "TIME", Font(84, Text, align: ThemeTextAlignment.Center, family: Light), format: "h:m"));
        e.Add(Value(234, 170, "DAY", Font(17, Secondary, bold: true, ThemeTextAlignment.Right)));
        e.Add(Value(254, 170, "DATE", Font(17, Secondary), format: "Y-M-D"));
        e.Add(Divider(56, 222, 376));
        e.Add(Value(56, 248, "WEATHER_ICON", Font(44, Warm, family: Symbols), unit: false));
        e.Add(Value(140, 244, "WEATHER_TEMP", Font(44, Text, family: Light)));
        e.Add(Value(408, 256, "WEATHER_TEXT", Font(15, Text, bold: true, ThemeTextAlignment.Right), unit: false, width: 180));
        e.Add(Value(408, 288, "WEATHER_CITY", Font(13, Secondary, align: ThemeTextAlignment.Right), unit: false, width: 180));
        e.Add(Label(56, 352, "HIGH", 11));
        e.Add(Value(56, 372, "WEATHER_HIGH", Font(18, Text)));
        e.Add(Label(160, 352, "LOW", 11));
        e.Add(Value(160, 372, "WEATHER_LOW", Font(18, Text)));
        e.Add(Label(408, 352, "SUNSET", 11, ThemeTextAlignment.Right));
        e.Add(Value(408, 372, "WEATHER_SUNSET", Font(18, Text, align: ThemeTextAlignment.Right), unit: false));

        // Now playing with transport buttons and a volume pill.
        Card(e, 488, 24, 560, 432);
        e.Add(new ThemeElement { Kind = ThemeElementKind.AlbumArt, X = 520, Y = 56, Width = 136, Height = 136, CornerRadius = 28, FillColor = Off, LineColor = Secondary,
            Action = ThemeAction.MediaPage });
        e.Add(Value(680, 74, "MEDIA_TITLE", Font(22, Text, bold: true), unit: false, width: 336, action: ThemeAction.MediaPage));
        e.Add(Value(680, 116, "MEDIA_ARTIST", Font(16, Secondary), unit: false, width: 336, action: ThemeAction.MediaPage));
        e.AddRange(Circle(723 - 150 - 36, 238, 72, PrevGlyph, ThemeAction.PreviousTrack));
        e.AddRange(PlayButton(723, 224, 100));
        e.AddRange(Circle(723 + 150 - 36, 238, 72, NextGlyph, ThemeAction.NextTrack));
        e.AddRange(VolumePill(520, 362, 496, 66));

        // System: pill bars with labels and values outside the fill, so they read at any level.
        Card(e, 1072, 24, 520, 432);
        e.Add(Label(1104, 52, "System", 16, bold: true, color: Text));
        Stat(e, 1104, 100, 456, "CPU", "CPULOAD", "CPUTEMP");
        Stat(e, 1104, 210, 456, "GPU", "GPULOAD", "GPUTEMP");
        Stat(e, 1104, 320, 456, "MEMORY", "RAMLOAD", "RAM_GB");

        // Quick toggles, One UI's round buttons.
        Card(e, 1616, 24, 280, 432);
        var toggles = new (string Glyph, string Label, ThemeAction Action)[]
        {
            (LightGlyph, "Lights", ThemeAction.LightsPage), (PaletteGlyph, "Colors", ThemeAction.PalettePage),
            (MixerGlyph, "Volume", ThemeAction.VolumeMixer), (AppsGlyph, "Apps", ThemeAction.AppDrawer),
            (MusicGlyph, "Media", ThemeAction.MediaPage), (AestheticGlyph, "Wallpaper", ThemeAction.AestheticToggle),
        };
        for (int i = 0; i < toggles.Length; i++)
        {
            double cx = 1688 + i % 2 * 136, cy = 48 + i / 2 * 136;
            e.AddRange(Circle(cx - 36, cy, 72, toggles[i].Glyph, toggles[i].Action));
            e.Add(Text_(cx, cy + 82, toggles[i].Label, Font(12, Secondary, bold: true, ThemeTextAlignment.Center), toggles[i].Action));
        }
        return t;
    }

    public static ThemeDefinition Column()
    {
        var t = new ThemeDefinition { Name = "One UI Column", Source = "Pulsewave template", Width = 682, Height = 2560, BackgroundColor = "#FF0D121E" };
        var e = t.Elements;
        Backdrop(e, 682, 2560);
        const double x = 28, w = 626, mid = 341;

        // Clock.
        e.Add(Value(mid, 60, "TIME", Font(120, Text, align: ThemeTextAlignment.Center, family: Light), format: "h:m"));
        e.Add(Value(mid - 10, 250, "DAY", Font(20, Secondary, bold: true, ThemeTextAlignment.Right)));
        e.Add(Value(mid + 10, 250, "DATE", Font(20, Secondary), format: "Y-M-D"));

        // Weather.
        Card(e, x, 330, w, 420);
        e.Add(Text_(64, 368, PinGlyph, Font(14, Secondary, family: Icons)));
        e.Add(Value(92, 364, "WEATHER_CITY", Font(16, Secondary, bold: true), unit: false, width: 400));
        e.Add(Value(64, 404, "WEATHER_TEMP", Font(80, Text, family: Light)));
        e.Add(Value(618, 400, "WEATHER_ICON", Font(84, Warm, align: ThemeTextAlignment.Right, family: Symbols), unit: false));
        e.Add(Value(64, 548, "WEATHER_TEXT", Font(22, Text, bold: true), unit: false, width: 550));
        e.Add(Label(64, 600, "FEELS", 12));
        e.Add(Value(124, 596, "WEATHER_FEELS", Font(16, Text)));
        e.Add(Label(214, 600, "HIGH", 12));
        e.Add(Value(266, 596, "WEATHER_HIGH", Font(16, Text)));
        e.Add(Label(354, 600, "LOW", 12));
        e.Add(Value(398, 596, "WEATHER_LOW", Font(16, Text)));
        e.Add(Divider(64, 650, 554));
        e.Add(Value(64, 672, "WEATHER_D1_ICON", Font(22, Warm, family: Symbols), unit: false));
        e.Add(Value(108, 678, "WEATHER_D1_DAY", Font(15, Secondary, bold: true), unit: false));
        e.Add(Value(164, 676, "WEATHER_D1_HIGH", Font(16, Text)));
        e.Add(Value(214, 676, "WEATHER_D1_LOW", Font(16, Secondary)));
        e.Add(Value(360, 672, "WEATHER_D2_ICON", Font(22, Warm, family: Symbols), unit: false));
        e.Add(Value(404, 678, "WEATHER_D2_DAY", Font(15, Secondary, bold: true), unit: false));
        e.Add(Value(460, 676, "WEATHER_D2_HIGH", Font(16, Text)));
        e.Add(Value(510, 676, "WEATHER_D2_LOW", Font(16, Secondary)));

        // Now playing.
        Card(e, x, 790, w, 600);
        e.Add(new ThemeElement { Kind = ThemeElementKind.AlbumArt, X = 64, Y = 826, Width = 200, Height = 200, CornerRadius = 32, FillColor = Off, LineColor = Secondary,
            Action = ThemeAction.MediaPage });
        e.Add(Value(292, 860, "MEDIA_TITLE", Font(22, Text, bold: true), unit: false, width: 330, action: ThemeAction.MediaPage));
        e.Add(Value(292, 906, "MEDIA_ARTIST", Font(17, Secondary), unit: false, width: 330, action: ThemeAction.MediaPage));
        e.Add(Value(292, 960, "MEDIA_STATE", Font(13, Secondary, bold: true), unit: false, width: 330, action: ThemeAction.MediaPage));
        e.AddRange(Circle(110, 1076, 88, PrevGlyph, ThemeAction.PreviousTrack));
        e.AddRange(PlayButton(mid, 1060, 120));
        e.AddRange(Circle(484, 1076, 88, NextGlyph, ThemeAction.NextTrack));
        e.AddRange(VolumePill(64, 1250, 554, 84));

        // System.
        Card(e, x, 1430, w, 470);
        e.Add(Label(64, 1462, "System", 18, bold: true, color: Text));
        Stat(e, 64, 1516, 554, "CPU", "CPULOAD", "CPUTEMP", big: true);
        Stat(e, 64, 1644, 554, "GPU", "GPULOAD", "GPUTEMP", big: true);
        Stat(e, 64, 1772, 554, "MEMORY", "RAMLOAD", "RAM_GB", big: true);

        // Quick toggles.
        Card(e, x, 1940, w, 330);
        var toggles = new (string Glyph, string Label, ThemeAction Action)[]
        {
            (LightGlyph, "Lights", ThemeAction.LightsPage), (PaletteGlyph, "Colors", ThemeAction.PalettePage), (MixerGlyph, "Volume", ThemeAction.VolumeMixer),
            (AppsGlyph, "Apps", ThemeAction.AppDrawer), (MusicGlyph, "Media", ThemeAction.MediaPage), (AestheticGlyph, "Wallpaper", ThemeAction.AestheticToggle),
        };
        for (int i = 0; i < toggles.Length; i++)
        {
            double cx = 141 + i % 3 * 200, cy = 1972 + i / 3 * 148;
            e.AddRange(Circle(cx - 42, cy, 84, toggles[i].Glyph, toggles[i].Action));
            e.Add(Text_(cx, cy + 92, toggles[i].Label, Font(13, Secondary, bold: true, ThemeTextAlignment.Center), toggles[i].Action));
        }

        // App shortcuts as rounded tiles.
        Card(e, x, 2310, w, 220);
        for (int i = 0; i < 4; i++)
            e.Add(new ThemeElement { Kind = ThemeElementKind.Shortcut, Slot = i + 1, X = 60 + i * 144, Y = 2342, Width = 130, Height = 156, CornerRadius = 36,
                FillColor = Glass, BorderColor = Edge, BorderWidth = 1, LineColor = Secondary });
        return t;
    }

    // --- Building blocks --------------------------------------------------------------------

    /// <summary>Deep navy with a soft rose glow top-left and a blue glow bottom-right.</summary>
    private static void Backdrop(List<ThemeElement> e, double w, double h)
    {
        e.Add(new ThemeElement { Kind = ThemeElementKind.Panel, Width = w, Height = h, FillColor = "#FF171D30", GradientColor = "#FF0D121E", GradientAngle = 90 });
        double r = Math.Max(w, h) * 0.6;
        e.Add(new ThemeElement { Kind = ThemeElementKind.Panel, X = -r * 0.5, Y = -r * 0.5, Width = r * 1.4, Height = r * 1.2, CornerRadius = r * 0.6,
            FillColor = "#558A4A6E", GradientColor = "#008A4A6E", GradientStyle = "Radial" });
        e.Add(new ThemeElement { Kind = ThemeElementKind.Panel, X = w - r * 0.9, Y = h - r * 0.8, Width = r * 1.4, Height = r * 1.2, CornerRadius = r * 0.6,
            FillColor = "#402C4A8C", GradientColor = "#002C4A8C", GradientStyle = "Radial" });
    }

    private static void Card(List<ThemeElement> e, double x, double y, double w, double h) =>
        e.Add(new ThemeElement { Kind = ThemeElementKind.Panel, X = x, Y = y, Width = w, Height = h, CornerRadius = 40, FillColor = Glass, BorderColor = Edge, BorderWidth = 1 });

    private static ThemeElement Divider(double x, double y, double w) =>
        new() { Kind = ThemeElementKind.Line, X = x, Y = y, Width = w, Height = 0, LineColor = "#26FFFFFF", LineWidth = 1 };

    /// <summary>A round button: the circle and its glyph share the tap action.</summary>
    private static IEnumerable<ThemeElement> Circle(double x, double y, double size, string glyph, ThemeAction action, bool on = false)
    {
        yield return new ThemeElement { Kind = ThemeElementKind.Panel, X = x, Y = y, Width = size, Height = size, CornerRadius = size / 2,
            FillColor = on ? OnFill : Off, Action = action };
        double pt = size * 0.3 * 0.75;
        yield return Text_(x + size / 2, y + (size - pt * 4 / 3 * 1.33) / 2, glyph, Font(pt, on ? OnText : Text, align: ThemeTextAlignment.Center, family: Icons), action);
    }

    /// <summary>The big light play button, centred on <paramref name="cx"/>, showing play or pause.</summary>
    private static IEnumerable<ThemeElement> PlayButton(double cx, double y, double size)
    {
        yield return new ThemeElement { Kind = ThemeElementKind.Panel, X = cx - size / 2, Y = y, Width = size, Height = size, CornerRadius = size / 2,
            FillColor = OnFill, Action = ThemeAction.PlayPause };
        double pt = size * 0.3 * 0.75;
        yield return Value(cx, y + (size - pt * 4 / 3 * 1.33) / 2, "MEDIA_PLAYPAUSE", Font(pt, OnText, align: ThemeTextAlignment.Center, family: Icons),
            unit: false, action: ThemeAction.PlayPause);
    }

    /// <summary>One UI's volume slider: a pill track and light fill, a light round end with the speaker (tap: mute), the level at the far end.</summary>
    private static IEnumerable<ThemeElement> VolumePill(double x, double y, double w, double h)
    {
        yield return new ThemeElement { Kind = ThemeElementKind.Bar, X = x, Y = y, Width = w, Height = h, CornerRadius = h / 2, DataName = "VOLUME",
            BackColor = Track, ForeColor = FillLow, GradientColor = FillHigh, Action = ThemeAction.VolumeMixer };
        yield return new ThemeElement { Kind = ThemeElementKind.Panel, X = x, Y = y, Width = h, Height = h, CornerRadius = h / 2, FillColor = FillHigh, Action = ThemeAction.Mute };
        yield return Value(x + h / 2, y + h * 0.24, "VOLUME_ICON", Font(h * 0.28, OnText, align: ThemeTextAlignment.Center, family: Icons), unit: false, action: ThemeAction.Mute);
        yield return Label(x + w, y - 30, "VOLUME", 11, ThemeTextAlignment.Right);
        yield return Label(x, y - 30, "MEDIA", 11);
    }

    /// <summary>A labelled stat: name and readings above a slim pill bar.</summary>
    private static void Stat(List<ThemeElement> e, double x, double y, double w, string name, string load, string detail, bool big = false)
    {
        e.Add(Label(x, y + (big ? 6 : 4), name, big ? 14 : 12));
        e.Add(Value(x + w, y - 4, load, Font(big ? 28 : 22, Text, bold: true, ThemeTextAlignment.Right)));
        e.Add(Value(x + w - (big ? 110 : 90), y + (big ? 4 : 2), detail, Font(big ? 16 : 14, Secondary, align: ThemeTextAlignment.Right)));
        double barY = y + (big ? 56 : 44), barH = big ? 34 : 26;
        e.Add(new ThemeElement { Kind = ThemeElementKind.Bar, X = x, Y = barY, Width = w, Height = barH, CornerRadius = barH / 2, DataName = load,
            BackColor = Track, ForeColor = FillLow, GradientColor = FillHigh, Action = ThemeAction.Details });
    }

    private static ThemeFont Font(double size, string color, bool bold = false, ThemeTextAlignment align = ThemeTextAlignment.Left, string family = Ui) =>
        new() { Family = family, Size = size, Color = color, Bold = bold, Alignment = align };

    private static ThemeElement Label(double x, double y, string text, double size, ThemeTextAlignment align = ThemeTextAlignment.Left, bool bold = true, string color = Secondary) =>
        Text_(x, y, text, Font(size, color, bold, align));

    private static ThemeElement Text_(double x, double y, string text, ThemeFont font, ThemeAction? action = null) =>
        new() { Kind = ThemeElementKind.Text, X = x, Y = y, Text = text, Font = font, Action = action };

    private static ThemeElement Value(double x, double y, string data, ThemeFont font, bool unit = true, double width = 0, string? format = null, ThemeAction? action = null) =>
        new() { Kind = ThemeElementKind.Data, X = x, Y = y, DataName = data, ShowUnit = unit, Width = width, DataFormat = format, Font = font, Action = action };
}
