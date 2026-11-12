using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Pulsewave.Themes;

public sealed record ThemeInfo(string Id, string Directory, ThemeDefinition? Definition, string? Error)
{
    public string Name => Definition?.Name is { Length: > 0 } name ? name : Id;
}

/// <summary>Installed themes, one folder each under %LOCALAPPDATA%\Pulsewave\themes.</summary>
public static class ThemeStore
{
    /// <summary>No theme chosen: the HUD shows its base theme, Lumen Lifestyle (Classic until 0.14).</summary>
    public const string ClassicId = "";
    private const int MaxElements = 4000;

    internal static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static string RootPath => Path.Combine(SettingsStore.DirectoryPath, "themes");

    public static IReadOnlyList<ThemeInfo> List(string root)
    {
        if (!System.IO.Directory.Exists(root)) return [];
        var themes = new List<ThemeInfo>();
        foreach (var dir in System.IO.Directory.GetDirectories(root).OrderBy(d => d, StringComparer.OrdinalIgnoreCase))
        {
            string id = Path.GetFileName(dir);
            if (id.StartsWith('.')) continue; // import staging folders
            try { themes.Add(new ThemeInfo(id, dir, Load(dir), null)); }
            catch (Exception ex) when (ex is IOException or InvalidDataException or JsonException or UnauthorizedAccessException)
            { themes.Add(new ThemeInfo(id, dir, null, ex.Message)); }
        }
        return themes;
    }

    public static ThemeDefinition Load(string themeDirectory)
    {
        var file = Path.Combine(themeDirectory, "theme.json");
        var theme = JsonSerializer.Deserialize<ThemeDefinition>(File.ReadAllText(file), Json)
                    ?? throw new InvalidDataException("theme.json is empty.");
        Validate(theme, themeDirectory);
        return theme;
    }

    public static void Save(string themeDirectory, ThemeDefinition theme)
    {
        Validate(theme, themeDirectory);
        File.WriteAllText(Path.Combine(themeDirectory, "theme.json"), JsonSerializer.Serialize(theme, Json));
    }

    public const double MinBackgroundZoom = 0.1, MaxBackgroundZoom = 10;

    internal static void Validate(ThemeDefinition theme, string themeDirectory)
    {
        if (theme.Format is < 1 or > ThemeDefinition.CurrentFormat)
            throw new InvalidDataException($"Theme format {theme.Format} needs a newer Lumen HUD.");
        if (theme.Width is < 1 or > 8192 || theme.Height is < 1 or > 8192)
            throw new InvalidDataException("Theme size is invalid.");
        theme.Elements ??= new();
        theme.BackgroundZoom = double.IsFinite(theme.BackgroundZoom) && theme.BackgroundZoom > 0 ? Math.Clamp(theme.BackgroundZoom, MinBackgroundZoom, MaxBackgroundZoom) : 1;
        theme.BackgroundOffsetX = double.IsFinite(theme.BackgroundOffsetX) ? Math.Clamp(theme.BackgroundOffsetX, -8192, 8192) : 0;
        theme.BackgroundOffsetY = double.IsFinite(theme.BackgroundOffsetY) ? Math.Clamp(theme.BackgroundOffsetY, -8192, 8192) : 0;
        if (theme.Elements.Count > MaxElements) throw new InvalidDataException("Theme has too many elements.");
        foreach (var file in new[] { theme.BackgroundVideo, theme.BackgroundImage, theme.Preview }
                     .Concat(theme.Elements.Select(e => e.ImageFile)))
            if (file is not null) ResolveFile(themeDirectory, file);
        foreach (var element in theme.Elements)
            element.Group = string.IsNullOrWhiteSpace(element.Group) ? null : element.Group.Trim() is var g && g.Length > 64 ? g[..64] : element.Group.Trim();
        foreach (var element in theme.Elements)
            if (!double.IsFinite(element.X) || !double.IsFinite(element.Y) || !double.IsFinite(element.Width) ||
                !double.IsFinite(element.Height) || element.Font is { Size: <= 0 or > 1000 })
                throw new InvalidDataException("Theme element has invalid geometry or font size.");
    }

    /// <summary>A theme file reference must stay inside the theme's own folder.</summary>
    public static string ResolveFile(string themeDirectory, string relative)
    {
        if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative))
            throw new InvalidDataException("Theme file references must be relative.");
        string root = Path.GetFullPath(themeDirectory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string full = Path.GetFullPath(Path.Combine(root, relative));
        if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Theme file reference leaves the theme folder.");
        return full;
    }

    /// <summary>Folder name for a new theme: readable, unique, and safe on Windows.</summary>
    internal static string NewId(string root, string name)
    {
        var chars = name.Trim().Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' or ' ' ? c : '_').ToArray();
        string baseId = new string(chars).Trim().Trim('.');
        if (baseId.Length == 0) baseId = "Theme";
        if (baseId.Length > 60) baseId = baseId[..60].Trim();
        string id = baseId;
        for (int n = 2; System.IO.Directory.Exists(Path.Combine(root, id)); n++) id = $"{baseId} ({n})";
        return id;
    }

    public static void Delete(string root, string id)
    {
        if (string.IsNullOrWhiteSpace(id) || id != Path.GetFileName(id) || id.StartsWith('.'))
            throw new ArgumentException("Invalid theme id.", nameof(id));
        string dir = Path.Combine(root, id);
        if (System.IO.Directory.Exists(dir)) System.IO.Directory.Delete(dir, recursive: true);
    }
}
