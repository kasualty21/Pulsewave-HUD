using System.IO;
using System.Reflection;

namespace Pulsewave.Themes;

/// <summary>
/// Templates that ship inside Lumen HUD (embedded Templates/*.json): the six Lumen themes. They are read-only: each is
/// written to %LOCALAPPDATA%\Lumen HUD\templates\&lt;name&gt; when used, refreshed whenever a new version changes it,
/// and customized by duplicating it into the user's themes. The older templates (01–39, embedded from Templates/Legacy)
/// are not built-ins any more: see <see cref="LegacyThemes"/>.
/// </summary>
public static class BuiltInTemplates
{
    public const string Prefix = "builtin:";
    private const string ResourcePrefix = "Pulsewave.Templates.", LegacyResourcePrefix = "Pulsewave.Legacy.";

    public static string RootPath => Path.Combine(SettingsStore.DirectoryPath, "templates");

    public static bool IsBuiltIn(string? id) => id?.StartsWith(Prefix, StringComparison.Ordinal) == true;

    /// <summary>Built-in template names, in gallery order (file names sort them: "40-lumen" ...).</summary>
    public static IReadOnlyList<string> Names => Embedded(ResourcePrefix);

    /// <summary>Legacy template names ("01-glass-cards" … "39-noir-column"): importable, not in the gallery.</summary>
    public static IReadOnlyList<string> LegacyNames => Embedded(LegacyResourcePrefix);

    public static bool IsLegacy(string? name) => name != null && LegacyNames.Contains(name);

    /// <summary>The base theme: Lumen Lifestyle for the bar, its column for a tall screen.</summary>
    public const string Default = Prefix + "40-lumen", DefaultColumn = Prefix + "41-lumen-column";

    private static List<string> Embedded(string prefix) => typeof(BuiltInTemplates).Assembly.GetManifestResourceNames()
        .Where(n => n.StartsWith(prefix, StringComparison.Ordinal) && n.EndsWith(".json", StringComparison.Ordinal))
        .Select(n => n[prefix.Length..^".json".Length]).OrderBy(n => n, StringComparer.Ordinal).ToList();

    /// <summary>
    /// Folder for a built-in theme id, written from the embedded copy if missing or out of date.
    /// Returns null if no such template ships with this version.
    /// </summary>
    /// <param name="legacy">Also write a legacy template (checks and previews only; the app imports those instead).</param>
    public static string? Ensure(string id, string? root = null, bool legacy = false)
    {
        if (!IsBuiltIn(id)) return null;
        string name = id[Prefix.Length..];
        if (name != Path.GetFileName(name) || !(Names.Contains(name) || legacy && LegacyNames.Contains(name))) return null;
        string json = Read(name);
        string dir = Path.Combine(root ?? RootPath, name);
        string file = Path.Combine(dir, "theme.json");
        Directory.CreateDirectory(dir);
        if (!File.Exists(file) || File.ReadAllText(file) != json)
        {
            File.WriteAllText(file, json);
            File.Delete(Path.Combine(dir, "preview.png")); // re-rendered for the new version
        }
        return dir;
    }

    /// <summary>All built-ins as gallery entries; previews are rendered on first use (call on the UI thread).</summary>
    public static IReadOnlyList<ThemeInfo> List(string? root = null)
    {
        var themes = new List<ThemeInfo>();
        foreach (var name in Names)
        {
            string id = Prefix + name;
            try
            {
                string dir = Ensure(id, root)!;
                var theme = ThemeStore.Load(dir);
                if (!File.Exists(Path.Combine(dir, "preview.png"))) ThemeMedia.SavePreview(dir, theme);
                theme.Preview = "preview.png";
                themes.Add(new ThemeInfo(id, dir, theme, null));
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or System.Text.Json.JsonException or UnauthorizedAccessException)
            {
                themes.Add(new ThemeInfo(id, Path.Combine(root ?? RootPath, name), null, ex.Message));
            }
        }
        return themes;
    }

    /// <summary>A built-in or legacy template's theme.json.</summary>
    public static string Read(string name)
    {
        var assembly = typeof(BuiltInTemplates).Assembly;
        using var stream = assembly.GetManifestResourceStream(ResourcePrefix + name + ".json")
                           ?? assembly.GetManifestResourceStream(LegacyResourcePrefix + name + ".json")
                           ?? throw new FileNotFoundException("No built-in template " + name);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
