using System.IO;
using System.Text.RegularExpressions;

namespace Pulsewave;

/// <summary>An installed Steam game: launched through Steam (steam://rungameid) with its own .exe's icon.</summary>
public sealed record SteamGame(string AppId, string Name, string? Icon)
{
    public AppShortcut ToShortcut() => new() { Name = Name, Target = "steam://rungameid/" + AppId, IconFile = Icon };
}

/// <summary>
/// Lists installed Steam games from Steam's own files (libraryfolders.vdf and each library's
/// appmanifest_*.acf), read only. Tools such as redistributables and Proton are left out.
/// </summary>
public static partial class SteamLibrary
{
    private static readonly HashSet<string> Tools = ["228980", "1070560", "1391110", "1628350", "1493710", "2180100", "1826330", "1161040"];

    public static string? SteamPath()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
            return key?.GetValue("SteamPath") is string path && Directory.Exists(path) ? Path.GetFullPath(path) : null;
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException) { return null; }
    }

    public static List<SteamGame> Installed(string? steamPath = null)
    {
        var games = new List<SteamGame>();
        steamPath ??= SteamPath();
        if (steamPath is null) return games;
        var libraries = new List<string> { steamPath };
        try
        {
            string folders = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
            if (File.Exists(folders))
                libraries.AddRange(Values(File.ReadAllText(folders), "path"));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        foreach (var library in libraries.Select(l => Path.GetFullPath(l).TrimEnd('\\')).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            string apps = Path.Combine(library, "steamapps");
            if (!Directory.Exists(apps)) continue;
            foreach (var manifest in Directory.EnumerateFiles(apps, "appmanifest_*.acf"))
            {
                try
                {
                    string text = File.ReadAllText(manifest);
                    string? id = Values(text, "appid").FirstOrDefault(), name = Values(text, "name").FirstOrDefault(), dir = Values(text, "installdir").FirstOrDefault();
                    if (id is null || name is null || IsTool(id, name) || games.Any(g => g.AppId == id)) continue;
                    games.Add(new SteamGame(id, name, (dir is null ? null : GameExe(Path.Combine(apps, "common", dir))) ?? CachedIcon(steamPath, id)));
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            }
        }
        return games.OrderBy(g => g.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    /// <summary>
    /// Installed Steam games with their install folders (no program search, so it is quick), for telling
    /// which game is running from a process's path.
    /// </summary>
    public static List<(string AppId, string Name, string Folder)> InstalledFolders(string? steamPath = null)
    {
        var games = new List<(string, string, string)>();
        steamPath ??= SteamPath();
        if (steamPath is null) return games;
        var libraries = new List<string> { steamPath };
        try
        {
            string folders = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
            if (File.Exists(folders)) libraries.AddRange(Values(File.ReadAllText(folders), "path"));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        foreach (var library in libraries.Select(l => Path.GetFullPath(l).TrimEnd('\\')).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            string apps = Path.Combine(library, "steamapps");
            if (!Directory.Exists(apps)) continue;
            foreach (var manifest in Directory.EnumerateFiles(apps, "appmanifest_*.acf"))
            {
                try
                {
                    string text = File.ReadAllText(manifest);
                    string? id = Values(text, "appid").FirstOrDefault(), name = Values(text, "name").FirstOrDefault(), dir = Values(text, "installdir").FirstOrDefault();
                    if (id is null || name is null || dir is null || IsTool(id, name) || games.Any(g => g.Item1 == id)) continue;
                    games.Add((id, name, Path.Combine(apps, "common", dir)));
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            }
        }
        return games;
    }

    internal static bool IsTool(string id, string name) =>
        Tools.Contains(id) || name.StartsWith("Proton", StringComparison.OrdinalIgnoreCase) ||
        name.StartsWith("Steamworks", StringComparison.OrdinalIgnoreCase) || name.StartsWith("Steam Linux Runtime", StringComparison.OrdinalIgnoreCase);

    /// <summary>Every value of a key in Valve's KeyValues text ("key"  "value"), with \\ unescaped.</summary>
    internal static IEnumerable<string> Values(string vdf, string key) =>
        Pair().Matches(vdf).Where(m => m.Groups[1].Value.Equals(key, StringComparison.OrdinalIgnoreCase))
            .Select(m => m.Groups[2].Value.Replace(@"\\", @"\"));

    /// <summary>
    /// The game's own program, for its icon: crash reporters, installers and other helpers are skipped;
    /// a name like the install folder's wins, otherwise the largest. Looks three folders deep.
    /// </summary>
    internal static string? GameExe(string folder)
    {
        if (!Directory.Exists(folder)) return null;
        var found = new List<FileInfo>();
        try
        {
            var options = new EnumerationOptions { RecurseSubdirectories = true, MaxRecursionDepth = 3, IgnoreInaccessible = true };
            found.AddRange(new DirectoryInfo(folder).EnumerateFiles("*.exe", options).Take(200)
                .Where(f => !Helper().IsMatch(f.Name)));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return null; }
        string Letters(string s) => new(s.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
        string wanted = Letters(Path.GetFileName(folder));
        // Top-level programs first (Unreal and Unity games keep their launcher there), then a name like the folder's, then size.
        return found.OrderBy(f => Path.GetRelativePath(folder, f.FullName).Count(c => c == '\\'))
            .ThenByDescending(f => wanted.Length > 0 && Letters(Path.GetFileNameWithoutExtension(f.Name)) is var n && n.Length > 0 &&
                                   (wanted.Contains(n) || n.Contains(wanted)))
            .ThenByDescending(f => f.Length).FirstOrDefault()?.FullName;
    }

    /// <summary>The app id of a steam://rungameid/… target, or null.</summary>
    public static string? AppIdOf(string? target)
    {
        const string prefix = "steam://rungameid/";
        if (target is null || !target.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return null;
        string id = new(target[prefix.Length..].TakeWhile(char.IsDigit).ToArray());
        return id.Length > 0 ? id : null;
    }

    /// <summary>
    /// The game's portrait cover (library_capsule.jpg or library_600x900.jpg, 2:3) from Steam's local
    /// library cache, which Steam keeps either loose or in a hashed subfolder. Null when it has none.
    /// </summary>
    public static string? CoverArt(string appId, string? steamPath = null)
    {
        steamPath ??= SteamPath();
        if (steamPath is null || appId.Length == 0 || !appId.All(char.IsDigit)) return null;
        string folder = Path.Combine(steamPath, "appcache", "librarycache", appId);
        try
        {
            if (!Directory.Exists(folder)) return null;
            var options = new EnumerationOptions { RecurseSubdirectories = true, MaxRecursionDepth = 1, IgnoreInaccessible = true };
            return Directory.EnumerateFiles(folder, "library_capsule.jpg", options).Concat(Directory.EnumerateFiles(folder, "library_600x900*.jpg", options))
                .OrderByDescending(f => new FileInfo(f).Length).FirstOrDefault();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return null; }
    }

    /// <summary>Steam's own small icon for a game, used when the game's folder has no usable program.</summary>
    internal static string? CachedIcon(string steamPath, string appId)
    {
        string folder = Path.Combine(steamPath, "appcache", "librarycache", appId);
        try
        {
            return Directory.Exists(folder)
                ? Directory.EnumerateFiles(folder, "*.jpg").FirstOrDefault(f => Path.GetFileNameWithoutExtension(f).Length == 40)
                : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return null; }
    }

    [GeneratedRegex("\"([^\"]+)\"\\s+\"((?:[^\"\\\\]|\\\\.)*)\"")]
    private static partial Regex Pair();

    [GeneratedRegex("crash|crs-|unins|setup|redist|vcredist|dxsetup|prereq|install|update|report|upload|helper|handler|subprocess|chunk|easyanticheat|battleye|dotnet|cef|server",
        RegexOptions.IgnoreCase)]
    private static partial Regex Helper();
}
