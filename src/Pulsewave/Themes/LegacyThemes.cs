using System.IO;

namespace Pulsewave.Themes;

/// <summary>
/// The templates from before Lumen (01–39: Glass Cards to Noir, One UI, Halo, Graphite, Paper). They ship inside the app
/// but are not built-ins: Themes → Import legacy theme copies one into the user's themes, where it works like any theme
/// they made. Settings that still point at one (from an earlier version) get it imported once, so nothing in use
/// disappears.
/// </summary>
public static class LegacyThemes
{
    /// <summary>The legacy templates as (name, theme name, width, height), for the import list.</summary>
    public static IReadOnlyList<(string Name, string Title, int Width, int Height)> List()
    {
        var list = new List<(string, string, int, int)>();
        foreach (var name in BuiltInTemplates.LegacyNames)
        {
            try
            {
                var theme = Parse(name);
                list.Add((name, theme.Name.Length > 0 ? theme.Name : name, theme.Width, theme.Height));
            }
            catch (Exception ex) when (ex is System.Text.Json.JsonException or InvalidDataException) { }
        }
        return list;
    }

    private static ThemeDefinition Parse(string name) =>
        System.Text.Json.JsonSerializer.Deserialize<ThemeDefinition>(BuiltInTemplates.Read(name), ThemeStore.Json)
        ?? throw new InvalidDataException("Empty legacy template " + name);

    /// <summary>Copies a legacy template into the user's themes (<paramref name="themesRoot"/>); returns the new theme's id.</summary>
    public static string Import(string name, string themesRoot)
    {
        if (!BuiltInTemplates.IsLegacy(name)) throw new ArgumentException("No legacy template " + name, nameof(name));
        var theme = Parse(name);
        Directory.CreateDirectory(themesRoot);
        string id = ThemeStore.NewId(themesRoot, theme.Name.Length > 0 ? theme.Name : name);
        theme.Source = "Legacy template";
        Directory.CreateDirectory(Path.Combine(themesRoot, id));
        ThemeStore.Save(Path.Combine(themesRoot, id), theme);
        return id;
    }

    /// <summary>
    /// Imports every legacy template the settings use (the shown theme, the playlist, the gaming and schedule themes,
    /// theme colors, hotkeys and the tray double-click) and points them at the imported copies. Classic, which no longer
    /// exists, leaves the playlist. Returns the names of the themes imported (none when there was nothing to do).
    /// </summary>
    public static List<string> Migrate(MonitorSettings s, string themesRoot)
    {
        var imported = new Dictionary<string, string>(StringComparer.Ordinal); // legacy id → user theme id
        var names = new List<string>();
        string Map(string id)
        {
            if (!BuiltInTemplates.IsBuiltIn(id)) return id;
            string name = id[BuiltInTemplates.Prefix.Length..];
            if (!BuiltInTemplates.IsLegacy(name)) return id;
            if (!imported.TryGetValue(id, out var copy))
            {
                copy = Import(name, themesRoot);
                imported[id] = copy;
                names.Add(Parse(name).Name);
            }
            return copy;
        }
        string MapAction(string action) => action.StartsWith("theme:", StringComparison.Ordinal) ? "theme:" + Map(action[6..]) : action;

        s.ActiveTheme = Map(s.ActiveTheme ?? "");
        s.ThemePlaylist = s.ThemePlaylist.Where(id => id.Length > 0).Select(Map).Distinct(StringComparer.Ordinal).ToList();
        s.Gaming.Theme = Map(s.Gaming.Theme);
        foreach (var key in s.Gaming.Themes.Keys.ToList()) s.Gaming.Themes[key] = Map(s.Gaming.Themes[key]);
        foreach (var rule in s.Schedule) rule.Theme = Map(rule.Theme);
        foreach (var (id, palette) in s.ThemePalettes.ToList())
            if (Map(id) is var to && to != id) { s.ThemePalettes.Remove(id); s.ThemePalettes[to] = palette; }
        foreach (var hotkey in s.Hotkeys ?? []) hotkey.Action = MapAction(hotkey.Action);
        s.TrayDoubleClick = MapAction(s.TrayDoubleClick);
        return names;
    }
}
