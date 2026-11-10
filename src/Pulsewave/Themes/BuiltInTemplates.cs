using System.IO;
using System.Reflection;

namespace Pulsewave.Themes;

/// <summary>
/// Templates that ship inside Pulsewave (embedded Templates/*.json). They are read-only: each is
/// written to %LOCALAPPDATA%\Pulsewave\templates\&lt;name&gt; when used, refreshed whenever a new
/// version changes it, and customized by duplicating it into the user's themes.
/// </summary>
public static class BuiltInTemplates
{
    public const string Prefix = "builtin:";
    private const string ResourcePrefix = "Pulsewave.Templates.";

    public static string RootPath => Path.Combine(SettingsStore.DirectoryPath, "templates");

    public static bool IsBuiltIn(string? id) => id?.StartsWith(Prefix, StringComparison.Ordinal) == true;

    /// <summary>
    /// Template names, in gallery order: the Lumen family (the baseline look) first, then the rest; within each, file
    /// names sort them ("01-glass-cards" ...).
    /// </summary>
    public static IReadOnlyList<string> Names => typeof(BuiltInTemplates).Assembly.GetManifestResourceNames()
        .Where(n => n.StartsWith(ResourcePrefix, StringComparison.Ordinal) && n.EndsWith(".json", StringComparison.Ordinal))
        .Select(n => n[ResourcePrefix.Length..^".json".Length]).OrderBy(n => Lumen.IsLumen(n) ? 0 : 1).ThenBy(n => n, StringComparer.Ordinal).ToList();

    /// <summary>
    /// Folder for a built-in theme id, written from the embedded copy if missing or out of date.
    /// Returns null if no such template ships with this version.
    /// </summary>
    public static string? Ensure(string id, string? root = null)
    {
        if (!IsBuiltIn(id)) return null;
        string name = id[Prefix.Length..];
        if (name != Path.GetFileName(name) || !Names.Contains(name)) return null;
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

    public static string Read(string name)
    {
        using var stream = typeof(BuiltInTemplates).Assembly.GetManifestResourceStream(ResourcePrefix + name + ".json")
                           ?? throw new FileNotFoundException("No built-in template " + name);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
