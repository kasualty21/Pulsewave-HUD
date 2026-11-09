using System.IO;
using System.Windows;
using System.Windows.Media;
using FontFamily = System.Windows.Media.FontFamily;
using FontStyle = System.Windows.FontStyle;

namespace Pulsewave.Themes;

public sealed record FontMatch(FontFamily Family, FontWeight Weight, FontStyle Style, FontStretch Stretch, bool Found);

/// <summary>
/// Finds theme fonts by name: installed Windows fonts, then Pulsewave's own fonts folder,
/// then fonts bundled with an installed L-Connect (read in place, never copied or installed).
/// </summary>
public static class ThemeFonts
{
    private static readonly object Gate = new();
    private static Dictionary<string, FontMatch>? _index;
    private static readonly string[] StyleWords =
        ["Bold", "Italic", "Regular", "Rg", "Medium", "Light", "SemiLight", "Thin", "Condensed", "SemiCondensed", "Black", "Semibold", "It"];

    public static string UserFontsPath => Path.Combine(SettingsStore.DirectoryPath, "fonts");
    internal static Func<IEnumerable<string>> SearchDirectories { get; set; } = DefaultDirectories;

    private static IEnumerable<string> DefaultDirectories()
    {
        yield return UserFontsPath;
        string lconnect = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Lian-Li", "L-Connect 3", "Assets");
        if (Directory.Exists(lconnect))
            foreach (var dir in Directory.EnumerateDirectories(lconnect, "*", SearchOption.AllDirectories))
                if (Path.GetFileName(dir) is "fonts" or "assets") yield return dir;
    }

    public static void Invalidate() { lock (Gate) _index = null; }

    public static FontMatch Resolve(string? name)
    {
        var fallback = new FontMatch(new FontFamily("Segoe UI"), FontWeights.Normal, FontStyles.Normal, FontStretches.Normal, false);
        if (string.IsNullOrWhiteSpace(name)) return fallback;
        var index = Index();
        if (index.TryGetValue(Key(name), out var exact)) return exact;
        // Theme files often name a face ("Agency Gothic CT Bold", "Good Times Rg"); retry with
        // the family name and apply the style words it carried.
        var words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
        FontWeight? weight = null; FontStretch? stretch = null; bool italic = false;
        while (words.Count > 1 && StyleWords.Contains(words[^1], StringComparer.OrdinalIgnoreCase))
        {
            switch (words[^1].ToLowerInvariant())
            {
                case "bold": weight ??= FontWeights.Bold; break;
                case "black": weight ??= FontWeights.Black; break;
                case "semibold": weight ??= FontWeights.SemiBold; break;
                case "medium": weight ??= FontWeights.Medium; break;
                case "light": weight ??= FontWeights.Light; break;
                case "semilight": weight ??= FontWeights.Light; break;
                case "thin": weight ??= FontWeights.Thin; break;
                case "condensed": stretch ??= FontStretches.Condensed; break;
                case "semicondensed": stretch ??= FontStretches.SemiCondensed; break;
                case "italic" or "it": italic = true; break;
            }
            words.RemoveAt(words.Count - 1);
            if (index.TryGetValue(Key(string.Join(' ', words)), out var family))
                return family with
                {
                    Weight = weight ?? family.Weight, Stretch = stretch ?? family.Stretch, Style = italic ? FontStyles.Italic : family.Style
                };
        }
        return fallback;
    }

    /// <summary>Copies a font into Pulsewave's fonts folder after checking that it loads.</summary>
    public static string Add(string sourceFile, string? fileName = null)
    {
        var face = new GlyphTypeface(new Uri(Path.GetFullPath(sourceFile))); // throws on invalid font files
        _ = face.FamilyNames;
        Directory.CreateDirectory(UserFontsPath);
        string target = Path.Combine(UserFontsPath, Path.GetFileName(fileName ?? sourceFile));
        File.Copy(sourceFile, target, overwrite: true);
        Invalidate();
        return target;
    }

    /// <summary>Font names usable in themes, for the editor's picker.</summary>
    public static IReadOnlyList<string> AvailableFamilies()
    {
        var names = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var family in Fonts.SystemFontFamilies) names.Add(family.Source);
        foreach (var match in Index().Values.Distinct())
            if (match.Family.FamilyNames.Values.FirstOrDefault() is { } name) names.Add(name);
            else names.Add(match.Family.Source.Split('#').Last());
        return names.ToList();
    }

    private static string Key(string name) => string.Join(' ', name.Split(' ', StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant();

    private static Dictionary<string, FontMatch> Index()
    {
        lock (Gate)
        {
            if (_index != null) return _index;
            var index = new Dictionary<string, FontMatch>();
            foreach (var family in Fonts.SystemFontFamilies)
                foreach (var name in family.FamilyNames.Values)
                    index.TryAdd(Key(name), new FontMatch(family, FontWeights.Normal, FontStyles.Normal, FontStretches.Normal, true));
            foreach (var dir in SearchDirectories())
            {
                if (!Directory.Exists(dir)) continue;
                foreach (var file in Directory.EnumerateFiles(dir).Where(f => f.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase) ||
                                                                              f.EndsWith(".otf", StringComparison.OrdinalIgnoreCase)))
                {
                    try
                    {
                        var face = new GlyphTypeface(new Uri(file));
                        string familyName = face.Win32FamilyNames.Values.FirstOrDefault() ?? face.FamilyNames.Values.First();
                        var family = new FontFamily(new Uri(Path.GetDirectoryName(file)! + Path.DirectorySeparatorChar),
                                                    "./" + Path.GetFileName(file) + "#" + familyName);
                        var match = new FontMatch(family, face.Weight, face.Style, face.Stretch, true);
                        foreach (var name in face.Win32FamilyNames.Values.Concat(face.FamilyNames.Values)
                                     .Concat(face.FamilyNames.Values.SelectMany(f => face.FaceNames.Values.Select(n => f + " " + n))))
                            index.TryAdd(Key(name), match);
                    }
                    catch (Exception ex) when (ex is FileFormatException or IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
                    {
                        // Skip unreadable font files.
                    }
                }
            }
            return _index = index;
        }
    }
}
