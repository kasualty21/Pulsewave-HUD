using System.Text.Json;
using System.Text.Json.Nodes;
using Pulsewave.Themes;

namespace Pulsewave.Regression;

// The feature refresh for the built-in templates made before the Halo kit (01–25): each keeps its layout and look
// and gains what Pulsewave can do now. Run with `--refresh-templates <dir>`; the results are reviewed with the
// quality gate and copied into src/Pulsewave/Templates. Only the touched properties change in the files, and
// re-running on refreshed templates changes nothing.
//   Warning colors: temperatures, memory and AI limits turn amber / red (on light themes only bars and gauges do,
//     since amber text would be hard to read on a light card).
//   Media: the album art and track names open the Media page; the "PLAYING" line becomes the track time.
//   Dates: a numeric date beside a weekday reads "Dec 31"; on its own it reads "Thursday, Dec 31".
//   Shortcuts: tiles take their icons' color, and Lock / Sleep / keys show as round buttons.
internal static class TemplateRefresh
{
    public static string Apply(string json, IReadOnlyDictionary<string, string>? dateFormats = null)
    {
        var theme = JsonNode.Parse(json)!.AsObject();
        string name = (string?)theme["Name"] ?? "";
        bool light = Lightness((string?)theme["BackgroundColor"] ?? "#FF000000") > 0.6 || name.Contains("Daylight", StringComparison.OrdinalIgnoreCase);
        var elements = theme["Elements"]!.AsArray().Select(n => n!.AsObject()).ToList();
        bool hasDay = elements.Any(e => (string?)e["DataName"] == "DAY");
        foreach (var e in elements)
        {
            string kind = (string?)e["Kind"] ?? "", data = (string?)e["DataName"] ?? "";
            bool reading = kind is "Data" or "Bar" or "Arc";
            // Unset, or still at the first refresh's levels (raised since: gaming temperatures are normal, not alarming).
            bool replace = e["WarnAt"] == null && e["CriticalAt"] == null || Earlier(data) is { } was && (double?)e["WarnAt"] == was.Warn && (double?)e["CriticalAt"] == was.Critical;
            if (reading && replace && Limits(data) is { } limits && (!light || kind != "Data"))
            {
                e["WarnAt"] = limits.Warn;
                e["CriticalAt"] = limits.Critical;
            }
            if (kind == "AlbumArt" && e["Action"] == null) e["Action"] = "MediaPage";
            if (kind == "Data" && data is "MEDIA_TITLE" or "MEDIA_ARTIST" && e["Action"] == null) e["Action"] = "MediaPage";
            if (kind == "Data" && data == "MEDIA_STATE") { e["DataName"] = "MEDIA_TIME"; e["ShowUnit"] = false; }
            if (kind == "Data" && data == "DATE" && (string?)e["DataFormat"] is not ("long" or "short" or "month"))
                e["DataFormat"] = dateFormats?.GetValueOrDefault(name) ?? (hasDay ? "month" : "long");
            if (kind == "Shortcut" && ((int?)e["Slot"] ?? 0) > 0 && e["TileStyle"] == null) e["TileStyle"] = "Tinted";
        }
        string text = theme.ToJsonString(new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        // Icon glyphs (private-use characters) stay as the characters the files were written with.
        return System.Text.RegularExpressions.Regex.Replace(text, @"\\u([EF][0-9A-F]{3})", m => ((char)Convert.ToInt32(m.Groups[1].Value, 16)).ToString());
    }

    private static (double Warn, double Critical)? Limits(string data) => data switch
    {
        "CPUTEMP" => (90, 98),
        "GPUTEMP" => (83, 90),
        "HDDTEMP" => (70, 80),
        "RAMLOAD" or "GPURAMLOAD" => (85, 95),
        "CLAUDE_5H" or "CLAUDE_WEEK" or "CODEX_5H" or "CODEX_WEEK" => (75, 90),
        _ => null
    };

    private static (double Warn, double Critical)? Earlier(string data) => data switch
    {
        "CPUTEMP" => (80, 90),
        "GPUTEMP" => (75, 85),
        "HDDTEMP" => (55, 65),
        _ => null
    };

    private static double Lightness(string color)
    {
        var c = ThemeView.ParseColor(color, System.Windows.Media.Colors.Black);
        return c.A == 0 ? 0 : (Math.Max(c.R, Math.Max(c.G, c.B)) + Math.Min(c.R, Math.Min(c.G, c.B))) / 510.0;
    }
}
