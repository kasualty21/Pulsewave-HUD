using Pulsewave.Themes.Editor;
using Color = System.Windows.Media.Color;
using Colors = System.Windows.Media.Colors;

namespace Pulsewave.Themes;

/// <summary>A color palette a theme can be shown in: dark background and surfaces, text, muted text and accents.</summary>
public sealed record ThemePalette(string Name, string Background, string Surface, string Text, string Muted, string[] Accents);

/// <summary>
/// On-the-fly recoloring: shows any theme in another palette without changing its files. Each color
/// the theme uses is sorted into dark (backgrounds, cards, tracks), light (text), muted (grey text)
/// or accent (anything colorful), and swapped for the palette's, keeping its own opacity and how
/// dark it is among the dark ones. Accents keep their order of use, so a theme's main accent gets
/// the palette's main accent. Pictures and videos are left as they are.
/// </summary>
public static class ThemePalettes
{
    public static readonly IReadOnlyList<ThemePalette> All =
    [
        new("Synthwave", "#120A24", "#24133F", "#FFF0FB", "#CBA9DA", ["#FF4FD8", "#7A5CFF", "#3DE1FF"]),
        new("Ember", "#160B08", "#2A1610", "#FFF4EC", "#D9B8A6", ["#FF7A2F", "#FFC14D", "#FF4545"]),
        new("Forest", "#0A1610", "#15281C", "#EEF8EF", "#A9C9B0", ["#5EDB8A", "#B6E35A", "#3FC1A8"]),
        new("Ocean", "#06141F", "#0F2536", "#EAF6FF", "#A5C4D8", ["#3DB8FF", "#4FF0D2", "#8AA6FF"]),
        new("Rose", "#1A0B12", "#2E1420", "#FFF0F4", "#D9AEBE", ["#FF6F9C", "#FFB3C7", "#C77DFF"]),
        new("Amber", "#0F0C05", "#1E180A", "#FFE9B0", "#C9AE6E", ["#FFB000", "#FFD966", "#FF8A00"]),
        new("Ice", "#0C1218", "#18222C", "#F4FAFF", "#B4C6D4", ["#9FE7FF", "#D6F3FF", "#7FB8FF"]),
        new("Mono", "#0E0E0E", "#1C1C1C", "#F5F5F5", "#A8A8A8", ["#FFFFFF", "#D4D4D4", "#9A9A9A"]),
        new("Toxic", "#0B1206", "#162410", "#F2FFE6", "#B5CCA0", ["#A6FF00", "#39FF88", "#FFE600"]),
        new("Sakura", "#1B1016", "#2B1A24", "#FFF5F8", "#D8B9C6", ["#FFB7D5", "#FF8FB8", "#B5E3FF"]),
        new("Sunset", "#1A0E14", "#2C1622", "#FFF3EA", "#DDB7AE", ["#FF8A5B", "#FFC46B", "#E86AA8"]),
        new("Arctic", "#1C222B", "#2B333F", "#ECEFF4", "#AEB7C5", ["#88C0D0", "#A3BE8C", "#EBCB8B"]),
        new("Nightshade", "#191A21", "#282A36", "#F8F8F2", "#B4B6C8", ["#BD93F9", "#FF79C6", "#50FA7B"]),
        new("Gold", "#0D0B07", "#1C1810", "#FFF7E0", "#CDBF96", ["#E8B84A", "#FFE08A", "#D09A3C"]),
        new("Terminal", "#030A05", "#0A1A0E", "#D9FFE0", "#8FBF97", ["#00FF66", "#00D45A", "#B2FF59"]),
        new("Neon city", "#0E0F12", "#1C1E24", "#FFFDF0", "#B9BCC6", ["#FCEE0A", "#00F0FF", "#FF2A6D"]),
        new("Crimson", "#140809", "#261012", "#FFF0F0", "#D4A9AB", ["#FF3B4E", "#FF8A65", "#FFD1D1"]),
        new("Lavender", "#141220", "#221E33", "#F6F2FF", "#BDB4D6", ["#B69CFF", "#8FD3FF", "#FF9ECF"]),
    ];

    /// <summary>The palette by name, or null for the theme's own colors.</summary>
    public static ThemePalette? Find(string? name) =>
        string.IsNullOrEmpty(name) ? null : All.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>The palette after <paramref name="current"/> ("" = the theme's own colors comes after the last).</summary>
    public static string Next(string? current, int step = 1)
    {
        var names = new List<string> { "" };
        names.AddRange(All.Select(p => p.Name));
        int index = names.FindIndex(n => string.Equals(n, current ?? "", StringComparison.OrdinalIgnoreCase));
        return names[((Math.Max(index, 0) + step) % names.Count + names.Count) % names.Count];
    }

    /// <summary>Recolors the theme in place (load a fresh copy to get its own colors back).</summary>
    public static void Apply(ThemeDefinition theme, ThemePalette palette)
    {
        Color P(string hex) => ThemeView.ParseColor(hex, Colors.Black);
        var background = P(palette.Background);
        var surface = P(palette.Surface);
        var text = P(palette.Text);
        var muted = P(palette.Muted);
        var accents = palette.Accents.Select(P).ToArray();
        var themeBack = ThemeView.ParseColor(theme.BackgroundColor, Colors.Black);
        var back = Color.FromRgb(themeBack.R, themeBack.G, themeBack.B); // what the theme's own text was read against
        // A light theme (dark text on light cards) is read with its lightness turned around, so its cards
        // become the palette's dark surfaces and its text the palette's light text. Inverting RGB flips
        // lightness and keeps saturation; hues move together, so the accents keep their order.
        bool light = themeBack.A > 0 && Lightness(themeBack) > 0.6;
        Color Read(Color c) => light ? Color.FromArgb(c.A, (byte)(255 - c.R), (byte)(255 - c.G), (byte)(255 - c.B)) : c;

        // The theme's colorful hues, most used first, so its main accent becomes the palette's main accent.
        // (Only accent uses count: dark cards often share the main accent's hue family without being one.)
        var accentUses = new List<Color>();
        ThemeColors.Recolor(theme.Elements, (c, _) => { if (Kind(Read(c)) == Tone.Accent) accentUses.Add(Read(c)); return c; });
        var hueOrder = accentUses.GroupBy(HueBucket).OrderByDescending(g => g.Count()).Select(g => g.Key).ToList();

        Color Map(Color original, string role)
        {
            var c = Read(original);
            var tone = Kind(c);
            Color to = tone switch
            {
                Tone.Dark => Lerp(background, surface, Math.Clamp(Lightness(c) / DarkLimit, 0, 1)),
                Tone.Light => text,
                Tone.Muted => role == ThemeColors.Text ? muted : Lerp(surface, muted, 0.5),
                _ => accents[Math.Max(0, hueOrder.IndexOf(HueBucket(c))) % accents.Length]
            };
            // Fills always end up dark: a colorful fill (a backdrop gradient, a bright tile) becomes a dark tint
            // of its accent, so it keeps its color without turning bright behind the text…
            bool fill = role is ThemeColors.Background or ThemeColors.Gradient or ThemeColors.ChartFill;
            if (fill && tone == Tone.Accent) to = Lerp(surface, to, Math.Clamp(Lightness(c) * 0.6, 0.15, 0.35));
            else if (fill && tone is Tone.Muted or Tone.Light) to = surface; // grey or light cards become the palette's surface
            // …and so text is always light (dark text on a bright tile becomes the palette's text color).
            if (role == ThemeColors.Text && tone == Tone.Dark) to = text;
            // Text stays as readable as it was in the theme (up to 7:1): it is eased toward the palette's
            // text color until it is, measured on the palette's lighter surface.
            if (role == ThemeColors.Text)
            {
                double target = Math.Min(7, Contrast(Over(original, back), back));
                for (double t = 0.1; t <= 1.0001 && Contrast(Over(Color.FromArgb(c.A, to.R, to.G, to.B), surface), surface) < target; t += 0.1)
                    to = Lerp(to, text, t);
            }
            return Color.FromArgb(c.A, to.R, to.G, to.B);
        }

        ThemeColors.Recolor(theme.Elements, Map);
        var ownBack = ThemeView.ParseColor(theme.BackgroundColor, Colors.Black);
        theme.BackgroundColor = ThemeColors.Hex(Color.FromArgb(ownBack.A, background.R, background.G, background.B));
    }

    /// <summary>A see-through color as it shows over <paramref name="under"/>.</summary>
    private static Color Over(Color c, Color under)
    {
        double a = c.A / 255.0;
        return Color.FromRgb((byte)Math.Round(c.R * a + under.R * (1 - a)), (byte)Math.Round(c.G * a + under.G * (1 - a)), (byte)Math.Round(c.B * a + under.B * (1 - a)));
    }

    /// <summary>WCAG contrast ratio of two opaque colors.</summary>
    private static double Contrast(Color a, Color b)
    {
        static double L(Color c)
        {
            static double Channel(byte v) { double s = v / 255.0; return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4); }
            return 0.2126 * Channel(c.R) + 0.7152 * Channel(c.G) + 0.0722 * Channel(c.B);
        }
        double x = L(a), y = L(b);
        return (Math.Max(x, y) + 0.05) / (Math.Min(x, y) + 0.05);
    }

    private enum Tone { Dark, Light, Muted, Accent }
    private const double DarkLimit = 0.24;

    private static Tone Kind(Color c)
    {
        double l = Lightness(c), s = Saturation(c);
        if (l < DarkLimit) return Tone.Dark;
        if (s < 0.22 || l > 0.9) return l > 0.72 ? Tone.Light : Tone.Muted;
        return Tone.Accent;
    }

    private static double Lightness(Color c) => (Math.Max(c.R, Math.Max(c.G, c.B)) + Math.Min(c.R, Math.Min(c.G, c.B))) / 510.0;

    private static double Saturation(Color c)
    {
        double max = Math.Max(c.R, Math.Max(c.G, c.B)) / 255.0, min = Math.Min(c.R, Math.Min(c.G, c.B)) / 255.0, l = (max + min) / 2;
        return max == min ? 0 : (max - min) / (1 - Math.Abs(2 * l - 1));
    }

    /// <summary>The hue in 30° steps, so near shades of one accent count as one.</summary>
    private static int HueBucket(Color c)
    {
        double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0, max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b)), d = max - min;
        if (d == 0) return -1;
        double h = max == r ? (g - b) / d % 6 : max == g ? (b - r) / d + 2 : (r - g) / d + 4;
        return (int)Math.Round((h * 60 + 360) % 360 / 30) % 12;
    }

    private static Color Lerp(Color a, Color b, double t) =>
        Color.FromRgb((byte)Math.Round(a.R + (b.R - a.R) * t), (byte)Math.Round(a.G + (b.G - a.G) * t), (byte)Math.Round(a.B + (b.B - a.B) * t));
}
