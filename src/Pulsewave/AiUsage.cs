using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Pulsewave;

/// <summary>How much of a plan limit window is used, and when the window starts over.</summary>
public sealed record UsageWindow(double UsedPercent, DateTimeOffset? ResetsAt)
{
    /// <summary>The window as of <paramref name="now"/>: once its reset time passes, nothing of it is used.</summary>
    public UsageWindow At(DateTimeOffset now) => ResetsAt is { } reset && reset <= now ? new UsageWindow(0, null) : this;
}

/// <summary>Claude Code and Codex plan usage: the 5-hour session window and the weekly window of each.</summary>
public sealed record AiUsage(UsageWindow? ClaudeSession, UsageWindow? ClaudeWeek, UsageWindow? CodexSession, UsageWindow? CodexWeek);

/// <summary>
/// Reads plan usage from what the tools already keep on this PC. Codex writes its limits into
/// its session logs (~/.codex/sessions); Claude Code hands them to its status line command,
/// which Pulsewave can be (see <see cref="ClaudeStatusLine"/>). No accounts, tokens or web requests.
/// </summary>
public sealed class AiUsageReader
{
    private readonly string _codexHome, _claudeFile;
    private DateTime _readAt = DateTime.MinValue;
    private AiUsage? _last;

    public AiUsageReader(string? codexHome = null, string? claudeUsageFile = null)
    {
        _codexHome = codexHome ?? CodexHome;
        _claudeFile = claudeUsageFile ?? ClaudeStatusLine.UsageFile;
    }

    public static string CodexHome => Environment.GetEnvironmentVariable("CODEX_HOME") is { Length: > 0 } home
        ? home : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex");

    /// <summary>Reads the files again on the next <see cref="Read"/> (new usage was just saved).</summary>
    public void Invalidate() => _last = null;

    /// <summary>The latest usage; files are read at most every 15 seconds.</summary>
    public AiUsage Read(DateTime now)
    {
        if (_last != null && (now - _readAt).Duration() < TimeSpan.FromSeconds(15)) return _last;
        _readAt = now;
        var (claudeSession, claudeWeek) = ReadClaude(_claudeFile);
        var (codexSession, codexWeek) = ReadCodex(_codexHome, now);
        return _last = new AiUsage(claudeSession, claudeWeek, codexSession, codexWeek);
    }

    internal static (UsageWindow? Session, UsageWindow? Week) ReadClaude(string file)
    {
        try
        {
            if (!File.Exists(file)) return (null, null);
            var root = JsonNode.Parse(File.ReadAllText(file));
            return (Window(root?["five_hour"]), Window(root?["seven_day"]));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException) { return (null, null); }

        static UsageWindow? Window(JsonNode? node) =>
            Number(node?["used_percentage"]) is { } used ? new UsageWindow(used, Epoch(node?["resets_at"])) : null;
    }

    /// <summary>The limits in the newest Codex session log of the last week that has any.</summary>
    internal static (UsageWindow? Session, UsageWindow? Week) ReadCodex(string codexHome, DateTime now)
    {
        string sessions = Path.Combine(codexHome, "sessions");
        if (!Directory.Exists(sessions)) return (null, null);
        var files = new List<FileInfo>();
        for (int day = 0; day <= 8; day++)
        {
            var date = now.Date.AddDays(-day);
            var folder = new DirectoryInfo(Path.Combine(sessions, date.ToString("yyyy", CultureInfo.InvariantCulture),
                date.ToString("MM", CultureInfo.InvariantCulture), date.ToString("dd", CultureInfo.InvariantCulture)));
            try { if (folder.Exists) files.AddRange(folder.EnumerateFiles("*.jsonl")); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
        foreach (var file in files.OrderByDescending(f => f.LastWriteTimeUtc).Take(6))
        {
            foreach (var line in TailLines(file.FullName, 512 * 1024).Reverse())
                if (line.Contains("\"rate_limits\"", StringComparison.Ordinal) && ParseCodexLine(line) is { } limits) return limits;
        }
        return (null, null);
    }

    /// <summary>The 5-hour and weekly windows from a Codex token_count event, or null when it has none.</summary>
    internal static (UsageWindow? Session, UsageWindow? Week)? ParseCodexLine(string line)
    {
        try
        {
            var limits = JsonNode.Parse(line)?["payload"]?["rate_limits"];
            if (limits is null) return null;
            // Other limit ids are per-model extras; the plan's own limits are "codex" (or unnamed in older logs).
            if (limits["limit_id"]?.GetValue<string>() is { } id && !id.Equals("codex", StringComparison.OrdinalIgnoreCase)) return null;
            UsageWindow? session = null, week = null;
            foreach (var key in new[] { "primary", "secondary" })
            {
                var node = limits[key];
                if (Number(node?["used_percent"]) is not { } used) continue;
                var window = new UsageWindow(used, Epoch(node?["resets_at"]));
                if ((Number(node?["window_minutes"]) ?? 0) >= 24 * 60) week = window; else session = window;
            }
            return session is null && week is null ? null : (session, week);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException) { return null; }
    }

    private static IEnumerable<string> TailLines(string path, int bytes)
    {
        try
        {
            // Codex keeps writing the newest log; share it rather than lock it.
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            long start = Math.Max(0, stream.Length - bytes);
            stream.Seek(start, SeekOrigin.Begin);
            using var reader = new StreamReader(stream, Encoding.UTF8);
            var lines = reader.ReadToEnd().Split('\n');
            return start > 0 ? lines.Skip(1).ToArray() : lines; // the first line of a tail is cut
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return []; }
    }

    internal static double? Number(JsonNode? node)
    {
        try { return node is JsonValue value && value.TryGetValue<double>(out var n) && double.IsFinite(n) ? n : null; }
        catch (InvalidOperationException) { return null; }
    }

    internal static DateTimeOffset? Epoch(JsonNode? node) =>
        Number(node) is { } seconds and > 0 and < 32503680000 ? DateTimeOffset.FromUnixTimeSeconds((long)seconds) : null;

    /// <summary>"2h 14m", "3d 6h" or "42m" until a window resets; "--" when unknown.</summary>
    public static string Until(DateTimeOffset? reset, DateTimeOffset now)
    {
        if (reset is not { } r || r <= now) return "--";
        var left = r - now;
        return left.TotalDays >= 1 ? $"{(int)left.TotalDays}d {left.Hours}h"
            : left.TotalHours >= 1 ? $"{(int)left.TotalHours}h {left.Minutes:00}m"
            : $"{Math.Max(1, (int)Math.Ceiling(left.TotalMinutes))}m";
    }
}

/// <summary>
/// Opt-in (tray → Claude usage → Read from your Claude sign-in): asks Anthropic for the plan's
/// 5-hour and weekly usage with the sign-in the Claude desktop app and Claude Code keep in
/// ~/.claude/.credentials.json, so usage stays current without a terminal session. This is the
/// usage page Claude's own apps read; Anthropic does not document it, so it may change. The token
/// is read when needed, sent only to api.anthropic.com over HTTPS, and never saved, logged or
/// refreshed here (refreshing could sign Claude out); an expired one just waits for Claude to renew it.
/// </summary>
public static class ClaudeAccountUsage
{
    public const string Endpoint = "https://api.anthropic.com/api/oauth/usage";

    public static string CredentialsFile => Path.Combine(Path.GetDirectoryName(ClaudeStatusLine.ClaudeSettingsFile)!, ".credentials.json");

    /// <summary>The access token while it is valid for another minute, or null.</summary>
    internal static string? ReadToken(string credentialsFile, DateTimeOffset now)
    {
        try
        {
            if (!File.Exists(credentialsFile)) return null;
            var oauth = JsonNode.Parse(File.ReadAllText(credentialsFile))?["claudeAiOauth"];
            string? token = oauth?["accessToken"]?.GetValue<string>();
            double? expires = AiUsageReader.Number(oauth?["expiresAt"]);
            if (string.IsNullOrWhiteSpace(token)) return null;
            return expires is { } ms && DateTimeOffset.FromUnixTimeMilliseconds((long)ms) <= now.AddMinutes(1) ? null : token;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException or ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    /// <summary>The 5-hour and weekly windows of a usage response (utilization in percent, ISO reset times).</summary>
    internal static List<(string Key, double Used, double? ResetsAt)> Parse(string json)
    {
        var windows = new List<(string, double, double?)>();
        try
        {
            var root = JsonNode.Parse(json);
            foreach (var key in new[] { "five_hour", "seven_day" })
            {
                var node = root?[key];
                if (AiUsageReader.Number(node?["utilization"]) is not { } used) continue;
                double? resets = node?["resets_at"]?.GetValue<string>() is { } text &&
                                 DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var at) ? at.ToUnixTimeSeconds() : null;
                windows.Add((key, used, resets));
            }
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException) { }
        return windows;
    }

    /// <summary>Fetches usage and saves it for the HUD; returns false when there is no valid sign-in or the request failed.</summary>
    public static async Task<bool> FetchAsync(HttpClient http, string credentialsFile, string usageFile, CancellationToken cancel = default)
    {
        if (ReadToken(credentialsFile, DateTimeOffset.UtcNow) is not { } token) return false;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, Endpoint);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            request.Headers.Add("anthropic-beta", "oauth-2025-04-20");
            using var response = await http.SendAsync(request, cancel).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) return false;
            var windows = Parse(await response.Content.ReadAsStringAsync(cancel).ConfigureAwait(false));
            if (windows.Count == 0) return false;
            ClaudeStatusLine.Save(usageFile, windows);
            return true;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException) { return false; }
    }
}

/// <summary>
/// Claude Code passes its plan limits (5-hour and weekly) to a status line command as JSON on
/// standard input; this is the documented way to see them. "Pulsewave.exe --claude-statusline"
/// is that command: it saves the limits for the HUD and prints a short line for Claude Code.
/// Connecting adds it to ~/.claude/settings.json only when the user asks, and never replaces
/// another status line.
/// </summary>
public static class ClaudeStatusLine
{
    public const string Argument = "--claude-statusline";
    public static string UsageFile => Path.Combine(SettingsStore.DirectoryPath, "claude-usage.json");
    /// <summary>A one-line .cmd next to the settings that runs this Pulsewave.exe; its path has no spaces to quote for Git Bash or PowerShell.</summary>
    public static string LauncherFile => Path.Combine(SettingsStore.DirectoryPath, "claude-statusline.cmd");

    public static string ClaudeSettingsFile => Path.Combine(
        Environment.GetEnvironmentVariable("CLAUDE_CONFIG_DIR") is { Length: > 0 } dir ? dir
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude"), "settings.json");

    /// <summary>Handles one status line refresh: saves the limits in <paramref name="input"/> and returns the line to print.</summary>
    public static string Relay(string input, string usageFile)
    {
        JsonNode? root;
        try { root = JsonNode.Parse(input); }
        catch (JsonException) { return "Lumen HUD"; }
        var limits = root?["rate_limits"];
        var parts = new List<string>();
        if (root?["model"]?["display_name"]?.GetValue<string>() is { Length: > 0 } model) parts.Add(model);
        if (AiUsageReader.Number(root?["context_window"]?["used_percentage"]) is { } context) parts.Add($"ctx {context:0}%");
        if (limits != null)
        {
            var windows = new List<(string Key, double Used, double? ResetsAt)>();
            foreach (var (key, label) in new[] { ("five_hour", "5h"), ("seven_day", "wk") })
            {
                if (AiUsageReader.Number(limits[key]?["used_percentage"]) is not { } used) continue;
                windows.Add((key, used, AiUsageReader.Number(limits[key]?["resets_at"])));
                parts.Add($"{label} {used:0}%");
            }
            Save(usageFile, windows);
        }
        return parts.Count > 0 ? string.Join(" · ", parts) : "Lumen HUD";
    }

    /// <summary>Saves Claude usage windows (percent used, reset as Unix seconds) for the HUD, keeping windows not given.</summary>
    internal static void Save(string usageFile, IEnumerable<(string Key, double Used, double? ResetsAt)> windows)
    {
        JsonObject saved;
        try { saved = File.Exists(usageFile) ? JsonNode.Parse(File.ReadAllText(usageFile)) as JsonObject ?? new() : new(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { saved = new(); }
        foreach (var (key, used, resets) in windows)
            saved[key] = new JsonObject { ["used_percentage"] = used, ["resets_at"] = resets };
        saved["updated"] = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(usageFile)!);
            // Several Claude Code sessions may refresh at once; each replaces the file whole.
            string staging = usageFile + "." + Guid.NewGuid().ToString("N") + ".tmp";
            File.WriteAllText(staging, saved.ToJsonString());
            File.Move(staging, usageFile, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    /// <summary>The status line command as written to Claude Code's settings (forward slashes, as Claude Code asks for on Windows).</summary>
    public static string Command(string launcherFile)
    {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string path = launcherFile.StartsWith(home + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            ? "~/" + launcherFile[(home.Length + 1)..] : launcherFile;
        path = path.Replace('\\', '/');
        return path.Contains(' ') ? "\"" + path + "\"" : path;
    }

    public enum State { NotConnected, Connected, OtherStatusLine }

    /// <summary>Whether Claude Code's settings run Pulsewave as the status line, another command, or none.</summary>
    public static (State State, string? Command) Status(string claudeSettings, string launcherFile)
    {
        try
        {
            if (!File.Exists(claudeSettings)) return (State.NotConnected, null);
            var command = Parse(File.ReadAllText(claudeSettings))?["statusLine"]?["command"]?.GetValue<string>();
            return command is null ? (State.NotConnected, null)
                : command == Command(launcherFile) ? (State.Connected, command) : (State.OtherStatusLine, command);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException)
        {
            return (State.NotConnected, null);
        }
    }

    /// <summary>
    /// Writes the launcher for <paramref name="exePath"/> and sets it as Claude Code's status line,
    /// keeping every other setting and a backup of the file. Refuses when another status line is set.
    /// </summary>
    public static (State State, string? Command) Connect(string claudeSettings, string launcherFile, string exePath)
    {
        var (state, existing) = Status(claudeSettings, launcherFile);
        if (state == State.OtherStatusLine) return (state, existing);
        Directory.CreateDirectory(Path.GetDirectoryName(launcherFile)!);
        File.WriteAllText(launcherFile, $"@\"{exePath}\" {Argument}\r\n");
        var root = File.Exists(claudeSettings) ? Parse(File.ReadAllText(claudeSettings)) as JsonObject ?? new JsonObject() : new JsonObject();
        root["statusLine"] = new JsonObject { ["type"] = "command", ["command"] = Command(launcherFile) };
        Write(claudeSettings, root);
        return (State.Connected, Command(launcherFile));
    }

    /// <summary>Removes Pulsewave's status line; another command is left alone.</summary>
    public static bool Disconnect(string claudeSettings, string launcherFile)
    {
        if (Status(claudeSettings, launcherFile).State != State.Connected) return false;
        if (Parse(File.ReadAllText(claudeSettings)) is not JsonObject root) return false;
        root.Remove("statusLine");
        Write(claudeSettings, root);
        return true;
    }

    private static JsonNode? Parse(string json) =>
        JsonNode.Parse(json, documentOptions: new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });

    private static void Write(string claudeSettings, JsonObject root)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(claudeSettings)!);
        if (File.Exists(claudeSettings)) File.Copy(claudeSettings, claudeSettings + ".pulsewave-backup", overwrite: true);
        string staging = claudeSettings + "." + Guid.NewGuid().ToString("N") + ".tmp";
        File.WriteAllText(staging, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        File.Move(staging, claudeSettings, overwrite: true);
    }
}
