using System.IO;
using System.Text.Json;

namespace Pulsewave.Themes;

/// <summary>Which theme comes next for the playlist and the next/previous hotkeys.</summary>
public static class ThemeCycle
{
    /// <summary>Every theme that can be shown, in gallery order: the Lumen templates, then the user's themes.</summary>
    public static IReadOnlyList<(string Id, string Name, int Width, int Height)> Available(string themesRoot)
    {
        var themes = new List<(string, string, int, int)>();
        foreach (var name in BuiltInTemplates.Names)
        {
            string title = name;
            int width = 1920, height = 480;
            try
            {
                using var json = JsonDocument.Parse(BuiltInTemplates.Read(name));
                if (json.RootElement.TryGetProperty("Name", out var n) && n.GetString() is { Length: > 0 } s) title = s;
                if (json.RootElement.TryGetProperty("Width", out var w) && w.TryGetInt32(out int wi)) width = wi;
                if (json.RootElement.TryGetProperty("Height", out var h) && h.TryGetInt32(out int hi)) height = hi;
            }
            catch (JsonException) { }
            themes.Add((BuiltInTemplates.Prefix + name, title, width, height));
        }
        foreach (var theme in ThemeStore.List(themesRoot))
            if (theme.Definition is { } d) themes.Add((theme.Id, d.Name, d.Width, d.Height));
        return themes;
    }

    /// <summary>Themes shaped like the display (all of them when none is), for rotation without a playlist.</summary>
    public static IReadOnlyList<string> Fitting(IEnumerable<(string Id, string Name, int Width, int Height)> available, int displayWidth, int displayHeight)
    {
        var all = available.ToList();
        var fitting = all.Where(t => DisplaySizes.SameShape(t.Width, t.Height, displayWidth, displayHeight)).Select(t => t.Id).ToList();
        return fitting.Count > 0 ? fitting : all.Select(t => t.Id).ToList();
    }

    /// <summary>
    /// The playlist entries that still exist, in playlist order. An empty playlist cycles through
    /// <paramref name="fallback"/> (by default every theme).
    /// </summary>
    public static IReadOnlyList<string> Order(IEnumerable<string>? playlist, IReadOnlyList<string> available, IReadOnlyList<string>? fallback = null)
    {
        var known = new HashSet<string>(available, StringComparer.Ordinal);
        var order = (playlist ?? []).Where(known.Contains).Distinct(StringComparer.Ordinal).ToList();
        return order.Count > 0 ? order : fallback ?? available;
    }

    /// <summary>The theme <paramref name="step"/> places from <paramref name="current"/>, wrapping around; a theme not in the order starts from its edge.</summary>
    public static string Step(IReadOnlyList<string> order, string current, int step)
    {
        if (order.Count == 0) return current;
        int index = -1;
        for (int i = 0; i < order.Count; i++) if (order[i] == current) { index = i; break; }
        if (index < 0) return step >= 0 ? order[0] : order[^1];
        return order[((index + step) % order.Count + order.Count) % order.Count];
    }
}
