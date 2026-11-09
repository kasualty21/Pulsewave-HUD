using Color = System.Windows.Media.Color;

namespace Pulsewave.Themes.Editor;

/// <summary>
/// The colors elements actually show, by role (text, bar fill, border…), so the editor can set one
/// role on many elements at once or swap a color everywhere it is used. Fields an element's kind does
/// not draw (a panel's ForeColor, say) are left alone.
/// </summary>
internal static class ThemeColors
{
    public const string Text = "Text", Fill = "Bar & gauge fill", Track = "Bar & gauge track", Background = "Card & tile fill",
        ChartFill = "Chart fill", Border = "Border", Line = "Lines", Gradient = "Gradient end", Glow = "Glow";

    private sealed record Slot(string Role, Func<ThemeElement, bool> Applies, Func<ThemeElement, string> Get, Action<ThemeElement, string> Set);

    private static bool Is(ThemeElement e, params ThemeElementKind[] kinds) => kinds.Contains(e.Kind);

    private static readonly Slot[] Slots =
    [
        new(Text, e => e.Font != null, e => e.Font!.Color, (e, c) => e.Font!.Color = c),
        new(Fill, e => Is(e, ThemeElementKind.Bar, ThemeElementKind.Arc), e => e.ForeColor, (e, c) => e.ForeColor = c),
        new(Track, e => Is(e, ThemeElementKind.Bar, ThemeElementKind.Arc), e => e.BackColor, (e, c) => e.BackColor = c),
        new(Background, e => Is(e, ThemeElementKind.Panel, ThemeElementKind.Shortcut, ThemeElementKind.AlbumArt), e => e.FillColor, (e, c) => e.FillColor = c),
        new(ChartFill, e => Is(e, ThemeElementKind.Chart), e => e.FillColor, (e, c) => e.FillColor = c),
        new(Border, e => Is(e, ThemeElementKind.Panel, ThemeElementKind.Shortcut, ThemeElementKind.Chart) && e.BorderWidth > 0, e => e.BorderColor, (e, c) => e.BorderColor = c),
        new(Line, e => Is(e, ThemeElementKind.Line, ThemeElementKind.Chart, ThemeElementKind.Shortcut, ThemeElementKind.AlbumArt), e => e.LineColor, (e, c) => e.LineColor = c),
        new(Gradient, e => e.GradientColor != null && Is(e, ThemeElementKind.Bar, ThemeElementKind.Arc, ThemeElementKind.Panel, ThemeElementKind.Shortcut),
            e => e.GradientColor!, (e, c) => e.GradientColor = c),
        new(Glow, e => e.GlowColor != null && !Is(e, ThemeElementKind.Image, ThemeElementKind.Chart), e => e.GlowColor!, (e, c) => e.GlowColor = c),
    ];

    private static readonly Color NotAColor = Color.FromArgb(0, 1, 2, 3);

    private static Color? Parse(string? text)
    {
        var c = ThemeView.ParseColor(text, NotAColor);
        return c == NotAColor ? null : c;
    }

    public static string Hex(Color c) => $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";

    /// <summary>Changes every color the elements show through <paramref name="map"/> (given the color and its role); invisible colors stay.</summary>
    public static void Recolor(IEnumerable<ThemeElement> elements, Func<Color, string, Color> map)
    {
        foreach (var e in elements)
            foreach (var slot in Slots.Where(s => s.Applies(e)))
                if (Parse(slot.Get(e)) is { A: > 0 } c) slot.Set(e, Hex(map(c, slot.Role)));
    }

    /// <summary>The roles these elements show, in a fixed order.</summary>
    public static List<string> Roles(IEnumerable<ThemeElement> elements)
    {
        var list = elements.ToList();
        return Slots.Where(s => list.Any(s.Applies)).Select(s => s.Role).ToList();
    }

    /// <summary>The color most of these elements use for a role (for showing in the editor).</summary>
    public static string? Common(IEnumerable<ThemeElement> elements, string role)
    {
        var slot = Slots.First(s => s.Role == role);
        return elements.Where(slot.Applies).Select(slot.Get).GroupBy(c => c, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Count()).Select(g => g.Key).FirstOrDefault();
    }

    /// <summary>
    /// Sets a role on every element that shows it. When only the color changed (not the opacity of
    /// <paramref name="shown"/>), each element keeps its own opacity, so a faint border stays faint.
    /// Returns how many colors changed.
    /// </summary>
    public static int SetRole(IEnumerable<ThemeElement> elements, string role, string color, string? shown = null)
    {
        if (Parse(color) is not { } to) return 0;
        bool keepAlpha = Parse(shown) is { } was && was.A == to.A;
        var slot = Slots.First(s => s.Role == role);
        int changed = 0;
        foreach (var e in elements.Where(slot.Applies))
        {
            var target = keepAlpha && Parse(slot.Get(e)) is { } own ? Color.FromArgb(own.A, to.R, to.G, to.B) : to;
            string hex = Hex(target);
            if (!string.Equals(slot.Get(e), hex, StringComparison.OrdinalIgnoreCase)) { slot.Set(e, hex); changed++; }
        }
        return changed;
    }

    /// <summary>The visible colors these elements use (opacity aside), most used first, with how often.</summary>
    public static List<(string Color, int Uses)> Palette(IEnumerable<ThemeElement> elements) =>
        elements.SelectMany(e => Slots.Where(s => s.Applies(e)).Select(s => Parse(s.Get(e))))
            .Where(c => c is { A: > 0 })
            .GroupBy(c => Hex(Color.FromArgb(255, c!.Value.R, c.Value.G, c.Value.B)))
            .OrderByDescending(g => g.Count()).ThenBy(g => g.Key)
            .Select(g => (g.Key, g.Count())).ToList();

    /// <summary>
    /// Swaps one color for another wherever these elements show it, keeping each use's own opacity
    /// (a card at 80% stays at 80%). Returns how many colors changed.
    /// </summary>
    public static int Replace(IEnumerable<ThemeElement> elements, string from, string to)
    {
        if (Parse(from) is not { } a || Parse(to) is not { } b) return 0;
        int changed = 0;
        foreach (var e in elements)
            foreach (var slot in Slots.Where(s => s.Applies(e)))
                if (Parse(slot.Get(e)) is { A: > 0 } c && c.R == a.R && c.G == a.G && c.B == a.B)
                {
                    string hex = Hex(Color.FromArgb(c.A, b.R, b.G, b.B));
                    if (!string.Equals(slot.Get(e), hex, StringComparison.OrdinalIgnoreCase)) { slot.Set(e, hex); changed++; }
                }
        return changed;
    }

    /// <summary>Elements "like" this one for Select similar: same kind, and plates apart from data bars.</summary>
    public static bool Alike(ThemeElement a, ThemeElement b) =>
        a.Kind == b.Kind && (a.Kind != ThemeElementKind.Bar || (a.DataName == null) == (b.DataName == null));
}
