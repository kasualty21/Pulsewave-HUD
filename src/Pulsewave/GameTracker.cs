using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Pulsewave.Themes;

namespace Pulsewave;

/// <summary>
/// A game Pulsewave can recognise: by its exact program (<see cref="Exe"/>) or anything in its install
/// folder; its Steam app id (cover art) or logo picture when it has one; and where it was found.
/// </summary>
internal sealed record GameEntry(string Key, string Name, string Folder, string? AppId, string? Exe = null, string? Logo = null, string Source = "Steam");

/// <summary>Games added by hand and programs never to count as games (tray → Gaming…).</summary>
public sealed class GamingSettings
{
    public List<UserGame> Games { get; set; } = new();
    /// <summary>Program paths (lower case) that are never games, and never suggested again.</summary>
    public List<string> Ignored { get; set; } = new();
    /// <summary>Ask "Track this as a game?" when an unknown program runs full-screen with the GPU busy.</summary>
    public bool Suggest { get; set; } = true;
    /// <summary>The theme shown while any game runs ("" = don't switch).</summary>
    public string Theme { get; set; } = "";
    /// <summary>Per-game themes by game key, overriding <see cref="Theme"/>; <see cref="NoTheme"/> = don't switch for this game.</summary>
    public Dictionary<string, string> Themes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public const string NoTheme = "none";

    public void Normalize()
    {
        Theme = (Theme ?? "").Trim();
        Themes = new Dictionary<string, string>((Themes ?? new()).Where(t => !string.IsNullOrWhiteSpace(t.Key) && !string.IsNullOrWhiteSpace(t.Value))
            .Take(500).ToDictionary(t => t.Key, t => t.Value.Trim()), StringComparer.OrdinalIgnoreCase);
        Games = (Games ?? new()).Where(g => g != null && !string.IsNullOrWhiteSpace(g.Exe)).GroupBy(g => g.Exe.ToLowerInvariant()).Select(g => g.First()).Take(200).ToList();
        foreach (var g in Games) g.Name = string.IsNullOrWhiteSpace(g.Name) ? Path.GetFileNameWithoutExtension(g.Exe) : g.Name.Trim();
        Ignored = (Ignored ?? new()).Where(i => !string.IsNullOrWhiteSpace(i)).Select(i => i.ToLowerInvariant()).Distinct().Take(500).ToList();
    }
}

public sealed class UserGame
{
    public string Name { get; set; } = "";
    public string Exe { get; set; } = "";
}

/// <summary>
/// The games on this PC, from: installed Steam games; GOG Galaxy's registry entries; Xbox / Game Pass
/// games (their MicrosoftGame.config); the games Windows itself has recognised (Game Bar's list, read
/// defensively because its format isn't documented); shortcuts into other stores' game folders; and
/// games added by hand. Store launchers, Wallpaper Engine and programs marked "never" don't count.
/// </summary>
internal static class GameCatalog
{
    private static readonly string[] StoreFolders =
        [@"\steamapps\common\", @"\Epic Games\", @"\GOG Galaxy\Games\", @"\GOG Games\", @"\XboxGames\", @"\Riot Games\",
         @"\Ubisoft Game Launcher\games\", @"\EA Games\", @"\Electronic Arts\", @"\Games\"];

    /// <summary>Steam apps that run alongside games rather than being played: Wallpaper Engine, Lossless Scaling, SteamVR, Soundpad.</summary>
    internal static readonly HashSet<string> NotGames = ["431960", "993090", "250820", "629520"];

    /// <summary>Folders too broad to stand for one game (a game whose program sits directly in one is matched by program only).</summary>
    private static readonly string[] Broad =
        [Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
         Environment.GetFolderPath(Environment.SpecialFolder.Windows), Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
         Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
         Environment.GetFolderPath(Environment.SpecialFolder.Desktop)];

    public static List<GameEntry> Build(IEnumerable<AppShortcut> shortcuts, GamingSettings? gaming = null)
    {
        gaming ??= new GamingSettings();
        var list = shortcuts.Where(s => !s.IsEmpty).ToList();
        var games = SteamLibrary.InstalledFolders().Where(g => !NotGames.Contains(g.AppId)).Select(g => new GameEntry("steam:" + g.AppId, g.Name, g.Folder, g.AppId)).ToList();
        foreach (var s in list)
            if (SteamLibrary.AppIdOf(s.Target) is { } id)
            {
                // A Steam shortcut names the game better than its manifest sometimes does; keep the user's name.
                int i = games.FindIndex(g => g.AppId == id);
                if (i >= 0 && !string.IsNullOrWhiteSpace(s.Name)) games[i] = games[i] with { Name = s.DisplayName };
            }
        void Add(GameEntry entry)
        {
            if (entry.Exe != null && IsIgnored(gaming, entry.Exe)) return;
            if (entry.Exe != null && games.Any(g => g.Exe != null && string.Equals(g.Exe, entry.Exe, StringComparison.OrdinalIgnoreCase))) return;
            if (entry.Folder.Length > 0 && games.Any(g => SameFolder(g.Folder, entry.Folder))) return;
            games.Add(entry);
        }
        foreach (var g in gaming.Games) Add(new GameEntry("user:" + g.Exe.ToLowerInvariant(), g.Name, "", null, g.Exe, null, "Added"));
        foreach (var g in Safe(Gog)) Add(g);
        foreach (var g in Safe(Xbox)) Add(g);
        foreach (var g in Safe(WindowsGames)) Add(g);
        foreach (var s in list)
        {
            string target = Environment.ExpandEnvironmentVariables(s.Target.Trim().Trim('"'));
            if (target.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && IsStoreGame(target))
                Add(new GameEntry("exe:" + target.ToLowerInvariant(), s.DisplayName, GameFolder(target), null, target, null, "Shortcut"));
        }
        return games;
    }

    private static IEnumerable<GameEntry> Safe(Func<IEnumerable<GameEntry>> source)
    {
        try { return source().ToList(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException or System.Xml.XmlException or ArgumentException) { return []; }
    }

    internal static bool IsIgnored(GamingSettings gaming, string program) => gaming.Ignored.Contains(program.ToLowerInvariant());

    /// <summary>GOG Galaxy writes each installed game's name, folder and program under HKLM\SOFTWARE\WOW6432Node\GOG.com\Games.</summary>
    private static IEnumerable<GameEntry> Gog()
    {
        using var root = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\GOG.com\Games");
        if (root == null) yield break;
        foreach (string id in root.GetSubKeyNames())
        {
            using var key = root.OpenSubKey(id);
            // DLC is registered too, pointing at its game's folder; only base games count.
            if (key?.GetValue("dependsOn") is string parent && parent.Trim().Length > 0) continue;
            if (key?.GetValue("path") is not string folder || !Directory.Exists(folder)) continue;
            string name = key.GetValue("gameName") as string ?? Path.GetFileName(folder.TrimEnd('\\'));
            string? exe = key.GetValue("exe") as string;
            yield return new GameEntry("gog:" + id, name, folder, null, exe != null && File.Exists(exe) ? exe : null, null, "GOG");
        }
    }

    /// <summary>Xbox / Game Pass games: each XboxGames\…\Content folder has a MicrosoftGame.config with the title and logo.</summary>
    private static IEnumerable<GameEntry> Xbox()
    {
        foreach (var drive in DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Fixed && d.IsReady))
        {
            string folder = Path.Combine(drive.RootDirectory.FullName, "XboxGames");
            if (!Directory.Exists(folder)) continue;
            foreach (var game in Directory.EnumerateDirectories(folder))
            {
                string content = Path.Combine(game, "Content"), config = Path.Combine(content, "MicrosoftGame.config");
                if (!File.Exists(config)) continue;
                var doc = System.Xml.Linq.XDocument.Load(config);
                var visuals = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "ShellVisuals");
                string name = (string?)visuals?.Attribute("DefaultDisplayName") ?? Path.GetFileName(game);
                string? logo = (string?)visuals?.Attribute("Square150x150Logo") ?? (string?)visuals?.Attribute("StoreLogo");
                string? logoPath = logo != null && File.Exists(Path.Combine(content, logo)) ? Path.Combine(content, logo) : null;
                yield return new GameEntry("xbox:" + Path.GetFileName(game).ToLowerInvariant(), name, content, null, null, logoPath, "Xbox");
            }
        }
    }

    /// <summary>
    /// The games Windows has recognised (Game Bar): HKCU\System\GameConfigStore\Children\*\MatchedExeFullPath.
    /// Undocumented, so anything unexpected is skipped; programs that no longer exist are left out.
    /// </summary>
    private static IEnumerable<GameEntry> WindowsGames()
    {
        using var root = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"System\GameConfigStore\Children");
        if (root == null) yield break;
        foreach (string child in root.GetSubKeyNames())
        {
            using var key = root.OpenSubKey(child);
            if (key?.GetValue("MatchedExeFullPath") is not string exe || !exe.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
                exe.Contains(@"\steamapps\", StringComparison.OrdinalIgnoreCase) || IsLauncher(exe) || !File.Exists(exe)) continue;
            yield return new GameEntry("exe:" + exe.ToLowerInvariant(), NameOf(exe), GameFolder(exe), null, exe, null, "Windows");
        }
    }

    /// <summary>A store launcher or helper rather than a game.</summary>
    internal static bool IsLauncher(string path)
    {
        string file = Path.GetFileName(path);
        return file.Contains("launcher", StringComparison.OrdinalIgnoreCase) || file.Contains("crashreport", StringComparison.OrdinalIgnoreCase) ||
               path.Contains(@"\Epic Games\Launcher\", StringComparison.OrdinalIgnoreCase) || file.Equals("Battle.net.exe", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The folder that stands for the game: above engine subfolders (Unreal's Binaries\Win64, a bin or x64
    /// folder), or empty when that is a broad folder like Program Files (then only the program itself matches).
    /// </summary>
    internal static string GameFolder(string exe)
    {
        string? folder = Path.GetDirectoryName(exe);
        while (folder != null && Path.GetFileName(folder).ToLowerInvariant() is "win64" or "win32" or "binaries" or "bin" or "x64" or "x86" or "wingdk")
            folder = Path.GetDirectoryName(folder);
        // Unreal Engine games keep the program in <game>\<Project>\Binaries\Win64, beside <game>\Engine: the game is one level up.
        if (folder != null && Path.GetDirectoryName(folder) is { } parent && Directory.Exists(Path.Combine(parent, "Engine")) && !Directory.Exists(Path.Combine(folder, "Engine")))
            folder = parent;
        if (folder == null || Path.GetPathRoot(folder) == folder || Broad.Any(b => b.Length > 0 && SameFolder(b, folder))) return "";
        return folder;
    }

    /// <summary>A readable game name: the program's description or product unless it is an engine's generic one, else its folder.</summary>
    internal static string NameOf(string exe)
    {
        string? product = null, description = null;
        try
        {
            var info = FileVersionInfo.GetVersionInfo(exe);
            product = info.ProductName?.Trim();
            description = info.FileDescription?.Trim();
        }
        catch (Exception ex) when (ex is IOException or ArgumentException) { }
        string folder = GameFolder(exe), folderName = folder.Length > 0 ? Path.GetFileName(folder) : "";
        foreach (var candidate in new[] { description, product })
        {
            if (string.IsNullOrWhiteSpace(candidate) || candidate.Length > 60 || IsGeneric(candidate)) continue;
            // A program's own code name ("Octopath_Traveler2"): the folder is usually the real title.
            if (candidate.Contains('_') && !candidate.Contains(' ') && folderName.Length > 0) break;
            // A name in another script than the folder's (a Japanese title in an English-named folder): the folder reads better here.
            if (!candidate.All(c => c < 0x2E80) && folderName.Length > 0 && folderName.All(c => c < 0x2E80)) break;
            return Clean(candidate);
        }
        return folderName.Length > 0 ? Clean(folderName) : Path.GetFileNameWithoutExtension(exe);
    }

    /// <summary>Drops trademark signs and build words ("Retail", "x64", "(64-bit)") from a name.</summary>
    internal static string Clean(string name)
    {
        string n = name.Replace("™", "").Replace("®", "").Trim();
        foreach (var suffix in new[] { " Retail", " (64-bit)", " 64-bit", " x64", " Win64", "-Win64", "_Win64", " Shipping", " Client" })
            if (n.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) n = n[..^suffix.Length].Trim();
        return n.Length > 0 ? n : name;
    }

    private static bool IsGeneric(string name) =>
        name.Contains("Unreal", StringComparison.OrdinalIgnoreCase) || name.Contains("Bootstrap", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("Game", StringComparison.OrdinalIgnoreCase) || name.Contains("Unity", StringComparison.OrdinalIgnoreCase) ||
        name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) || name.Contains("Shipping", StringComparison.OrdinalIgnoreCase);

    /// <summary>A program in a store's game folder, not a store launcher (the Epic or EA app, a "…Launcher.exe").</summary>
    internal static bool IsStoreGame(string path) => StoreFolders.Any(f => path.Contains(f, StringComparison.OrdinalIgnoreCase)) && !IsLauncher(path);

    /// <summary>The game a program belongs to: an exact program match first, else the deepest catalog folder that contains it.</summary>
    internal static GameEntry? Match(IEnumerable<GameEntry> games, string programPath)
    {
        var list = games as IReadOnlyCollection<GameEntry> ?? games.ToList();
        return list.FirstOrDefault(g => g.Exe != null && string.Equals(g.Exe, programPath, StringComparison.OrdinalIgnoreCase))
            ?? list.Where(g => g.Folder.Length > 3 && programPath.StartsWith(g.Folder.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(g => g.Folder.Length).FirstOrDefault();
    }

    private static bool SameFolder(string a, string b) => string.Equals(a.TrimEnd('\\'), b.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);
}


/// <summary>A finished play session as saved in gaming.json.</summary>
public sealed record GameSession(string Key, string Name, DateTime Start, DateTime End, double? AvgFps, double? LowFps, double? PeakCpuC, double? PeakGpuC)
{
    public TimeSpan Length => End - Start;
}

/// <summary>What themes and the Gaming page show: the game being played (or the last one), its session, and play time.</summary>
public sealed record GamingSnapshot(
    string? Game, bool Playing, TimeSpan? Session, double? AvgFps, double? LowFps, double? PeakCpuC, double? PeakGpuC,
    TimeSpan Today, TimeSpan Week, ShortcutView? Tile, IReadOnlyList<(string Name, TimeSpan Time)> WeekByGame, IReadOnlyList<GameSession> Recent);


/// <summary>
/// Which game is running: the programs behind visible windows (their paths only, through
/// QueryFullProcessImageName), matched to the catalog. The foreground window wins when several are games.
/// Nothing is captured or injected.
/// </summary>
internal static class GameDetector
{
    public static (GameEntry Game, string Program)? Running(IReadOnlyList<GameEntry> games)
    {
        if (games.Count == 0) return null;
        var pids = new HashSet<uint>();
        EnumWindows((window, _) =>
        {
            if (IsWindowVisible(window) && GetWindowTextLength(window) > 0 && GetWindowThreadProcessId(window, out uint pid) != 0) pids.Add(pid);
            return true;
        }, IntPtr.Zero);
        GetWindowThreadProcessId(GetForegroundWindow(), out uint foreground);
        (GameEntry, string)? best = null;
        foreach (uint pid in pids.OrderByDescending(p => p == foreground))
        {
            if (ProgramPath(pid) is not { } path || GameCatalog.Match(games, path) is not { } game) continue;
            if (pid == foreground) return (game, path);
            best ??= (game, path);
        }
        return best;
    }

    /// <summary>Programs that fill the screen with the GPU busy but are never games.</summary>
    private static readonly HashSet<string> NeverGames = new(StringComparer.OrdinalIgnoreCase)
    {
        "explorer.exe", "chrome.exe", "msedge.exe", "firefox.exe", "brave.exe", "opera.exe", "vivaldi.exe", "arc.exe", "vlc.exe", "mpv.exe", "mpc-hc64.exe",
        "PotPlayerMini64.exe", "obs64.exe", "Discord.exe", "Teams.exe", "ms-teams.exe", "Zoom.exe", "steamwebhelper.exe", "steam.exe", "Pulsewave.exe", "LumenHUD.exe",
        "Plex.exe", "Plexamp.exe", "Spotify.exe", "Netflix.exe", "ApplicationFrameHost.exe", "wallpaper64.exe", "wallpaper32.exe", "LockApp.exe",
        "devenv.exe", "Code.exe", "blender.exe", "Photoshop.exe", "Resolve.exe", "Premiere Pro.exe"
    };

    /// <summary>The foreground program's path when its window covers its whole screen (full-screen or borderless), else null.</summary>
    public static string? FullScreenProgram()
    {
        IntPtr window = GetForegroundWindow();
        if (window == IntPtr.Zero || !GetWindowRect(window, out var r)) return null;
        IntPtr monitor = MonitorFromWindow(window, 2);
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (monitor == IntPtr.Zero || !GetMonitorInfo(monitor, ref info)) return null;
        bool covers = r.Left <= info.Monitor.Left && r.Top <= info.Monitor.Top && r.Right >= info.Monitor.Right && r.Bottom >= info.Monitor.Bottom;
        if (!covers || GetWindowThreadProcessId(window, out uint pid) == 0) return null;
        string? path = ProgramPath(pid);
        return path != null && !NeverGames.Contains(Path.GetFileName(path)) && !path.Contains(@"\Windows\", StringComparison.OrdinalIgnoreCase) ? path : null;
    }

    [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct MonitorInfo { public int Size; public NativeRect Monitor, Work; public uint Flags; }
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr window, out NativeRect rect);
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr window, uint flags);
    [DllImport("user32.dll")] private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

    private static string? ProgramPath(uint pid)
    {
        IntPtr process = OpenProcess(0x1000, false, pid); // PROCESS_QUERY_LIMITED_INFORMATION
        if (process == IntPtr.Zero) return null;
        try
        {
            var text = new StringBuilder(1024);
            int size = text.Capacity;
            return QueryFullProcessImageName(process, 0, text, ref size) ? text.ToString() : null;
        }
        finally { CloseHandle(process); }
    }

    private delegate bool EnumWindowsProc(IntPtr window, IntPtr data);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr data);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")] private static extern int GetWindowTextLength(IntPtr window);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr OpenProcess(uint access, bool inherit, uint processId);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool QueryFullProcessImageName(IntPtr process, int flags, StringBuilder name, ref int size);
    [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr handle);
}

/// <summary>
/// Notices an unknown program that looks like a game: full-screen in the foreground with the GPU at
/// least half busy for two minutes in all (checked every 5 s; a pause just stops the count). It is
/// suggested once per run; "never" programs and known games are skipped.
/// </summary>
internal sealed class GameSuggester
{
    public static readonly TimeSpan Needed = TimeSpan.FromMinutes(2);
    private readonly Dictionary<string, TimeSpan> _seen = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _asked = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>One check: the full-screen program (or null) and the GPU load. Returns a program to suggest, at most once each.</summary>
    public string? Check(string? fullScreen, double? gpuLoad, TimeSpan interval, Func<string, bool> known)
    {
        if (fullScreen == null || gpuLoad is not >= 50 || known(fullScreen) || _asked.Contains(fullScreen)) return null;
        var total = _seen[fullScreen] = _seen.GetValueOrDefault(fullScreen) + interval;
        if (total < Needed) return null;
        _asked.Add(fullScreen);
        return fullScreen;
    }
}

/// <summary>
/// Play sessions: one starts when a known game is seen and ends 30 seconds after it was last seen.
/// While it runs it collects the FPS and temperatures Pulsewave already reads; sessions of a minute or
/// more are kept in %LOCALAPPDATA%\Pulsewave\gaming.json (180 days) for today's and the week's play time.
/// </summary>
public sealed class GameTracker
{
    public static readonly TimeSpan Grace = TimeSpan.FromSeconds(30), Shortest = TimeSpan.FromMinutes(1);
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    private readonly string? _file;
    private readonly List<GameSession> _history;
    private readonly List<double> _fps = new();
    // RTSS's own "Framerate 1% Low" readings this session (when paired in Sensor Manager).
    private readonly List<double> _rtssLow = new();
    private string? _key, _name;
    private DateTime _start, _lastSeen;
    private double? _peakCpu, _peakGpu;
    private ShortcutView? _tile;
    private string _tileKey = "";

    /// <param name="file">Where sessions are saved; null keeps them in memory (checks).</param>
    public GameTracker(string? file)
    {
        _file = file;
        _history = Load(file);
    }

    public static string DefaultFile => Path.Combine(SettingsStore.DirectoryPath, "gaming.json");

    public IReadOnlyList<GameSession> History => _history;
    public string? CurrentGame => _name;
    /// <summary>The running session's game key (it stays through the 30 s grace after the game was last seen), or null.</summary>
    public string? CurrentKey => _key;

    /// <summary>
    /// One look at what is running (null: no known game), with this second's FPS and temperatures, and RTSS's
    /// 1% low when it is paired. Returns the session that just ended, if any.
    /// </summary>
    public GameSession? Update(DateTime now, string? key, string? name, double? fps, double? cpuC, double? gpuC, double? rtssLow = null)
    {
        GameSession? ended = null;
        if (key != null && key != _key) ended = End();
        if (key != null)
        {
            if (_key == null) { _key = key; _name = name ?? key; _start = now; }
            _lastSeen = now;
            if (fps is > 0 and < 1000) _fps.Add(fps.Value);
            if (rtssLow is > 0 and < 1000) _rtssLow.Add(rtssLow.Value);
            if (cpuC is > 0 and < 150) _peakCpu = Math.Max(_peakCpu ?? 0, cpuC.Value);
            if (gpuC is > 0 and < 150) _peakGpu = Math.Max(_peakGpu ?? 0, gpuC.Value);
        }
        else if (_key != null && now - _lastSeen > Grace) ended = End();
        return ended;
    }

    /// <summary>Ends the current session (at the last time the game was seen), keeping it if it was long enough.</summary>
    public GameSession? End()
    {
        if (_key == null) return null;
        var session = new GameSession(_key, _name ?? _key, _start, _lastSeen, Average(_fps), SessionLow(), _peakCpu, _peakGpu);
        _key = _name = null;
        _fps.Clear();
        _rtssLow.Clear();
        _peakCpu = _peakGpu = null;
        if (session.Length < Shortest) return null;
        _history.Add(session);
        _history.RemoveAll(s => s.End < session.End.AddDays(-180));
        Save();
        return session;
    }

    /// <summary>The average FPS over the session's samples.</summary>
    internal static double? Average(IReadOnlyCollection<double> fps) => fps.Count > 0 ? Math.Round(fps.Average()) : null;

    /// <summary>"1% low": the FPS the slowest 1% of the session's seconds stayed under (needs a minute of samples).</summary>
    internal static double? Low(IReadOnlyCollection<double> fps)
    {
        if (fps.Count < 60) return null;
        var sorted = fps.OrderBy(v => v).ToList();
        return Math.Round(sorted[(int)Math.Floor((sorted.Count - 1) * 0.01)]);
    }

    /// <summary>
    /// The 1% low to show: RTSS's latest reading while playing (<paramref name="live"/>) or the median of its readings
    /// for the finished session, when RTSS is paired; else the slowest 1% of the session's seconds.
    /// </summary>
    private double? SessionLow(bool live = false)
    {
        if (_rtssLow.Count == 0) return Low(_fps);
        if (live) return Math.Round(_rtssLow[^1]);
        var sorted = _rtssLow.OrderBy(v => v).ToList();
        return Math.Round(sorted[sorted.Count / 2]);
    }

    /// <summary>What to show at <paramref name="now"/>; <paramref name="tileFor"/> makes the cover/icon tile for a game key (cached).</summary>
    public GamingSnapshot Snapshot(DateTime now, Func<string, ShortcutView?>? tileFor = null)
    {
        bool playing = _key != null;
        var last = _history.Count > 0 ? _history[^1] : null;
        string? key = _key ?? last?.Key;
        if (key != _tileKey) { _tileKey = key ?? ""; _tile = key != null ? tileFor?.Invoke(key) : null; }
        var current = playing ? new GameSession(_key!, _name!, _start, now, null, null, null, null) : null;
        var all = current != null ? _history.Append(current).ToList() : _history;
        TimeSpan Within(DateTime from) => TimeSpan.FromTicks(all.Sum(s => Math.Max(0, (Min(s.End, now) - Max(s.Start, from)).Ticks)));
        DateTime today = now.Date, week = now.Date.AddDays(-6);
        var byGame = all.Where(s => s.End > week).GroupBy(s => s.Name)
            .Select(g => (g.Key, TimeSpan.FromTicks(g.Sum(s => Math.Max(0, (Min(s.End, now) - Max(s.Start, week)).Ticks)))))
            .OrderByDescending(g => g.Item2).ToList();
        return new GamingSnapshot(playing ? _name : last?.Name, playing, playing ? now - _start : null,
            playing ? Average(_fps) : last?.AvgFps, playing ? SessionLow(live: true) : last?.LowFps,
            playing ? _peakCpu : last?.PeakCpuC, playing ? _peakGpu : last?.PeakGpuC,
            Within(today), Within(week), _tile, byGame, _history.AsEnumerable().Reverse().Take(30).ToList());
    }

    private static DateTime Min(DateTime a, DateTime b) => a < b ? a : b;
    private static DateTime Max(DateTime a, DateTime b) => a > b ? a : b;

    private static List<GameSession> Load(string? file)
    {
        try
        {
            if (file != null && File.Exists(file))
                return JsonSerializer.Deserialize<List<GameSession>>(File.ReadAllText(file), Json)?.Where(s => s.Key != null && s.End >= s.Start).OrderBy(s => s.End).ToList() ?? [];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { }
        return [];
    }

    private void Save()
    {
        if (_file == null) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
            string temp = _file + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(_history, Json));
            File.Move(temp, _file, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Debug.WriteLine("gaming.json not saved: " + ex.Message); }
    }
}
