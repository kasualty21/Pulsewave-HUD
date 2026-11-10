using System.IO;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows.Media;

namespace Pulsewave.Themes;

/// <summary>Everything a theme can display for one refresh.</summary>
public sealed class ThemeDataContext
{
    public SensorSnapshot Sensors { get; init; } = new();
    /// <summary>Whether HWiNFO's feed is live, frozen (nothing has changed for 30 s) or offline; sensor readings dim when it is not live.</summary>
    public SensorFeedState SensorFeed { get; init; } = SensorFeedState.Live;
    public DateTime Now { get; init; } = DateTime.Now;
    public string CpuName { get; init; } = "";
    public string GpuName { get; init; } = "";
    /// <summary>What is playing (Windows media or Plexamp), when the theme shows it.</summary>
    public NowPlaying? Media { get; init; }
    /// <summary>Average FPS over the recent history window, when FPS is reporting.</summary>
    public double? AverageFps { get; init; }
    /// <summary>The user's shortcuts in slot order (slot 1 first); null is an empty slot.</summary>
    public IReadOnlyList<ShortcutView?> Shortcuts { get; init; } = [];
    /// <summary>Claude Code and Codex plan usage, when the theme shows it.</summary>
    public AiUsage? Usage { get; init; }
    /// <summary>Playback volume 0–100 and mute, when the theme shows it.</summary>
    public (double Level, bool Muted)? Volume { get; init; }
    /// <summary>The countdown timer's time left and the stopwatch's elapsed time.</summary>
    public TimeSpan? Timer { get; init; }
    public TimeSpan? Stopwatch { get; init; }
    public bool TimerRunning { get; init; }
    public bool StopwatchRunning { get; init; }
    /// <summary>"FOCUS" or "BREAK" during a Pomodoro, otherwise "".</summary>
    public string TimerPhase { get; init; } = "";
    /// <summary>The countdown is stopped partway (it resumes rather than starts).</summary>
    public bool TimerPaused { get; init; }
    /// <summary>The Pomodoro's focus round, counting from 1; 0 without a Pomodoro.</summary>
    public int PomodoroRound { get; init; }
    /// <summary>Whether a one-card theme shows the stopwatch rather than the timer.</summary>
    public bool StopwatchShown { get; init; }
    /// <summary>Free and total space of a drive ("" = the Windows drive); null reads Windows' own drive information.</summary>
    public Func<string, (double FreeGb, double TotalGb)?>? DriveSpace { get; init; }
    /// <summary>Where the track is (from Plexamp's timeline), when known: elapsed and length.</summary>
    public (TimeSpan Elapsed, TimeSpan Length)? MediaPosition { get; init; }
    /// <summary>The game being played (or the last one) and play time, when the theme shows it.</summary>
    public GamingSnapshot? Gaming { get; init; }
    /// <summary>The latest weather, when the theme shows it and a city is set.</summary>
    internal WeatherReport? Weather { get; init; }

    /// <summary>The shortcut in a 1-based slot, or null when the slot is empty.</summary>
    /// <summary>The shortcut in a 1-based slot; slot <see cref="GameSlot"/> is the game being played (or the last one).</summary>
    public ShortcutView? Shortcut(int slot) =>
        slot == GameSlot ? Gaming?.Tile : slot >= 1 && slot <= Shortcuts.Count ? Shortcuts[slot - 1] : null;

    /// <summary>A shortcut tile with this slot shows the current game's cover or icon.</summary>
    public const int GameSlot = -1;

    private (double UsedMb, double FreeMb, double TotalMb)? _memory;
    private bool _memoryRead;
    public (double UsedMb, double FreeMb, double TotalMb)? Memory
    {
        get
        {
            if (!_memoryRead) { _memoryRead = true; _memory = ThemeData.ReadMemory(); }
            return _memory;
        }
        init { _memory = value; _memoryRead = true; }
    }
}

/// <summary>A shortcut as themes draw it: its name and icon, or a Segoe Fluent Icons glyph when it has no icon.</summary>
public sealed record ShortcutView(string Name, ImageSource? Icon, string Glyph = ShortcutView.AppGlyph)
{
    public const string AppGlyph = "", WebGlyph = "", EmptyGlyph = "", DrawerGlyph = "";

    /// <summary>How a theme shows a shortcut; null for an empty slot.</summary>
    public static ShortcutView? From(AppShortcut shortcut) => shortcut.IsEmpty ? null : shortcut.Kind switch
    {
        AppShortcut.KeysKind => new(shortcut.DisplayName, null, "") { IsAction = true },
        AppShortcut.SystemKind => new(shortcut.DisplayName, null, shortcut.Target.Trim().Equals("sleep", StringComparison.OrdinalIgnoreCase) ? "" : "") { IsAction = true },
        _ => Picture(shortcut)
    };

    /// <summary>Portrait cover art (a Steam game's), which tall tiles show filling the tile instead of the icon.</summary>
    public ImageSource? Cover { get; init; }
    /// <summary>The icon fills its square (a picture or tile icon), so tiles round its corners and bevel it.</summary>
    public bool IconSolid { get; init; }
    /// <summary>The icon's main color, for tinted tiles; null for grey icons and glyphs.</summary>
    public System.Windows.Media.Color? Accent { get; init; }
    /// <summary>A system action or key press (Lock, Sleep, a key combination) rather than something it opens.</summary>
    public bool IsAction { get; init; }

    private static ShortcutView Picture(AppShortcut shortcut)
    {
        var icon = ShortcutIcons.Load(shortcut);
        return new(shortcut.DisplayName, icon, shortcut.Target.Contains("://", StringComparison.Ordinal) ? WebGlyph : AppGlyph)
        {
            Cover = ShortcutIcons.LoadCover(shortcut),
            IconSolid = icon is System.Windows.Media.Imaging.BitmapSource bitmap && ShortcutIcons.IsSolid(bitmap),
            Accent = icon is System.Windows.Media.Imaging.BitmapSource source ? ShortcutIcons.Accent(source) : null
        };
    }
}

/// <summary>A formatted reading: Number drives bars, arcs and charts; Text is what Data elements show.</summary>
public readonly record struct ThemeValue(string Text, double? Number, string Unit);

/// <summary>
/// L-Connect data names and how Pulsewave fills them: HWiNFO sensors (including the
/// optional theme sensors in Sensor Manager), Windows clock and memory, and hardware names.
/// </summary>
public static class ThemeData
{
    public const string Missing = "--";

    /// <summary>Data names a theme can use but Pulsewave cannot supply yet.</summary>
    public static readonly IReadOnlySet<string> Unsupported = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "GPUVALIDRAM", "RAMMODEL" };

    private static readonly Dictionary<string, string> Sensors = new(StringComparer.OrdinalIgnoreCase)
    {
        ["CPUTEMP"] = "CpuTemp", ["CPULOAD"] = "CpuLoad", ["CPUPWR"] = "CpuPower", ["CPUFAN"] = "CpuFan",
        ["CPUCLOCK"] = "CpuClock", ["CPUCLOCK_G"] = "CpuClock", ["CPUVOLTAGE"] = "CpuVoltage",
        ["GPUTEMP"] = "GpuTemp", ["GPULOAD"] = "GpuLoad", ["GPUPWR"] = "GpuPower", ["GPUFAN"] = "GpuFan",
        ["GPUCLOCK"] = "GpuClock", ["GPUCLOCK_G"] = "GpuClock", ["GPUVOLTAGE"] = "GpuVoltage",
        ["GPURAMLOAD"] = "GpuVramLoad", ["GPURAM"] = "GpuVram", ["FPS"] = "GpuFps", ["FPS_AVG"] = "GpuFps", ["FRAMETIME"] = "GpuFps",
        ["RAMLOAD"] = "MemoryLoad", ["WATERPUMP"] = "PumpRpm", ["CASEFAN1"] = "CaseFan1", ["CASEFAN2"] = "CaseFan2",
        ["DRVLOAD"] = "DriveLoad", ["HDDTEMP"] = "DriveTemp", ["HDDUSED"] = "DriveUsed",
        ["UPSPEED"] = "NetUpload", ["DOWNDSPEED"] = "NetDownload",
        ["CPU_LIMIT"] = "CpuThermalLimit", ["GPU_LIMIT"] = "GpuPowerLimit", ["CPUFAN_STATE"] = "CpuFan", ["CPUFAN_ALERT"] = "CpuFan"
    };

    /// <summary>The Pulsewave sensor a data name reads, or null for clock, memory and names.</summary>
    public static string? SensorFor(string? name) => name is not null && Sensors.TryGetValue(name, out var id) ? id : null;

    /// <summary>Data names the editor offers, with friendly labels.</summary>
    public static readonly IReadOnlyList<(string Name, string Label)> Catalog =
    [
        ("CPUTEMP", "CPU temperature"), ("CPULOAD", "CPU load %"), ("CPUPWR", "CPU power (W)"), ("CPUFAN", "CPU fan (RPM)"), ("CPUFAN_STATE", "CPU fan: OK / STOPPED"), ("CPUFAN_ALERT", "CPU fan: STOPPED, else blank (show the row only then)"),
        ("CPUCLOCK_G", "CPU clock (GHz)"), ("CPUCLOCK", "CPU clock (MHz)"), ("CPUVOLTAGE", "CPU voltage"), ("CPUMODEL", "CPU name"),
        ("GPUTEMP", "GPU temperature"), ("GPULOAD", "GPU load %"), ("GPUPWR", "GPU power (W)"), ("GPUFAN", "GPU fan (RPM)"),
        ("GPUCLOCK_G", "GPU clock (GHz)"), ("GPUCLOCK", "GPU clock (MHz)"), ("GPUVOLTAGE", "GPU voltage"),
        ("GPURAMLOAD", "GPU memory load %"), ("GPURAM", "GPU memory used (MB)"), ("GPURAM_GB_OF", "GPU memory used of total (8.6 / 16 GB)"), ("GPUMODEL", "GPU name"),
        ("FPS", "FPS"), ("FPS_AVG", "Average FPS (last minute)"), ("FRAMETIME", "Frame time (ms): RTSS's when paired, else from FPS"),
        ("RAMLOAD", "Memory load %"), ("RAM_GB", "Memory used (GB)"), ("RAMVALID_GB", "Memory free (GB)"), ("RAMTOTAL_GB", "Memory total (GB)"),
        ("WATERPUMP", "Pump (RPM)"), ("CASEFAN1", "Case fan 1 (RPM)"), ("CASEFAN2", "Case fan 2 (RPM)"),
        ("DRVLOAD", "Drive activity %"), ("HDDTEMP", "Drive temperature"), ("HDDUSED", "Drive used %"),
        ("DOWNDSPEED", "Download speed"), ("UPSPEED", "Upload speed"),
        ("SENSOR_STATUS", "Sensor feed: SENSORS PAUSED / OFFLINE (blank while live)"),
        ("MEDIA_ALBUM", "Now playing: album"), ("DRIVE_FREE", "Drive free space (format: drive letter; blank = Windows drive)"), ("DRIVE_USED_OF", "Drive used of total (1.5 / 2 TB)"),
        ("DRIVE_USED_PCT", "Drive used % (format: drive letter)"),
        ("TIMER_ACTION", "Timer: Start / Pause (for its button)"), ("STOPWATCH_ACTION", "Stopwatch: Start / Pause (for its button)"),
        ("TIMER_PHASE", "Timer: FOCUS / BREAK during a Pomodoro"),
        ("FOCUS_CLOCK", "Timer card: the timer or stopwatch, whichever is shown"), ("FOCUS_MODE", "Timer card: TIMER / FOCUS / BREAK / STOPWATCH"),
        ("FOCUS_ACTION", "Timer card: Start / Pause / Resume"), ("FOCUS_RUNNING", "Timer card: 1 while running (for colors)"), ("FOCUS_ROUND", "Timer card: Pomodoro round (Round 2 / 4)"), ("FOCUS_SWITCH", "Timer card: the other mode (Timer / Stopwatch)"),
        ("WEATHER_FEELS_NOTE", "Weather: \"Feels 16°\" (blank within 2°)"), ("WEATHER_RAIN", "Weather: Rain from 15:00 / until / none next 12 h"),
        ("WEATHER_KIND", "Weather: 0 clear, 1 cloud or night, 2 wet (for symbol colors)"),
        ("WEATHER_D1_KIND", "Weather tomorrow: kind (for symbol colors)"), ("WEATHER_D2_KIND", "Weather in 2 days: kind"),
        ("WEATHER_H1_TIME", "Weather next hour: time"), ("WEATHER_H1_ICON", "Weather next hour: symbol"), ("WEATHER_H1_TEMP", "Weather next hour: temperature"), ("WEATHER_H1_KIND", "Weather next hour: kind"),
        ("WEATHER_H2_TIME", "Weather in 3 h: time"), ("WEATHER_H2_ICON", "Weather in 3 h: symbol"), ("WEATHER_H2_TEMP", "Weather in 3 h: temperature"), ("WEATHER_H2_KIND", "Weather in 3 h: kind"),
        ("WEATHER_H3_TIME", "Weather in 6 h: time"), ("WEATHER_H3_ICON", "Weather in 6 h: symbol"), ("WEATHER_H3_TEMP", "Weather in 6 h: temperature"), ("WEATHER_H3_KIND", "Weather in 6 h: kind"),
        ("WEATHER_H4_TIME", "Weather in 9 h: time"), ("WEATHER_H4_ICON", "Weather in 9 h: symbol"), ("WEATHER_H4_TEMP", "Weather in 9 h: temperature"), ("WEATHER_H4_KIND", "Weather in 9 h: kind"),
        ("CPU_LIMIT", "CPU held back by: THERMAL / POWER LIMIT (blank when not)"), ("GPU_LIMIT", "GPU held back by: THERMAL / POWER / VOLTAGE LIMIT (blank when not)"),
        ("TIME", "Time"), ("DATE", "Date"), ("DAY", "Weekday"), ("APM", "AM/PM"),
        ("MEDIA_TITLE", "Now playing: title"), ("MEDIA_ARTIST", "Now playing: artist"), ("MEDIA_STATE", "Now playing: state"),
        ("MEDIA_PLAYPAUSE", "Now playing: play/pause icon"), ("MEDIA_TIME", "Now playing: time played / length (Plexamp)"),
        ("RAM_GB_OF", "Memory used of total (23.8 / 32 GB)"), ("GAME_FPS_LOW_PCT", "Gaming: 1% low as % of average (for warning colors)"),
        ("WEATHER_SUN_NEXT", "Weather: next sunrise or sunset"), ("WEATHER_SUN_NEXT_LABEL", "Weather: SUNRISE or SUNSET (whichever is next)"),
        ("MEDIA_PROGRESS", "Now playing: progress % (Plexamp; for a ring or bar)"), ("MEDIA_ELAPSED", "Now playing: time played (Plexamp)"),
        ("MEDIA_REMAINING", "Now playing: time left (Plexamp)"),
        ("APP_NAME", "Shortcut name (its slot)"),
        ("CLAUDE_5H", "Claude Code: 5-hour limit used %"), ("CLAUDE_WEEK", "Claude Code: weekly limit used %"),
        ("CLAUDE_5H_RESET", "Claude Code: 5-hour limit resets in"), ("CLAUDE_WEEK_RESET", "Claude Code: weekly limit resets in"),
        ("CODEX_5H", "Codex: 5-hour limit used %"), ("CODEX_WEEK", "Codex: weekly limit used %"),
        ("CODEX_5H_RESET", "Codex: 5-hour limit resets in"), ("CODEX_WEEK_RESET", "Codex: weekly limit resets in"),
        ("VOLUME", "Volume %"), ("VOLUME_ICON", "Volume: speaker/mute icon"),
        ("TIMER", "Timer: time left"), ("STOPWATCH", "Stopwatch: elapsed"),
        ("GAME_NAME", "Gaming: game being played (or the last one)"), ("GAME_STATE", "Gaming: PLAYING / LAST PLAYED"),
        ("GAME_SESSION", "Gaming: this session's length (the last one's after it)"), ("GAME_FPS", "Gaming: live FPS while a game runs (blank otherwise)"), ("GAME_FPS_AVG", "Gaming: session average FPS"), ("GAME_FPS_LOW", "Gaming: session 1% low FPS"),
        ("GAME_PEAK_CPU", "Gaming: session peak CPU temperature"), ("GAME_PEAK_GPU", "Gaming: session peak GPU temperature"),
        ("GAME_TODAY", "Gaming: play time today (minutes for bars)"), ("GAME_WEEK", "Gaming: play time, last 7 days"),
        ("WEATHER", "Weather: temperature and conditions"), ("WEATHER_TEMP", "Weather: temperature"), ("WEATHER_TEXT", "Weather: conditions"),
        ("WEATHER_ICON", "Weather: symbol (Segoe UI Symbol font)"), ("WEATHER_FEELS", "Weather: feels like"),
        ("WEATHER_HIGH", "Weather: today's high"), ("WEATHER_LOW", "Weather: today's low"), ("WEATHER_CITY", "Weather: city"),
        ("WEATHER_SUNRISE", "Weather: sunrise"), ("WEATHER_SUNSET", "Weather: sunset"),
        ("WEATHER_D1_DAY", "Weather tomorrow: weekday"), ("WEATHER_D1_ICON", "Weather tomorrow: symbol"),
        ("WEATHER_D1_HIGH", "Weather tomorrow: high"), ("WEATHER_D1_LOW", "Weather tomorrow: low"),
        ("WEATHER_D2_DAY", "Weather in 2 days: weekday"), ("WEATHER_D2_ICON", "Weather in 2 days: symbol"),
        ("WEATHER_D2_HIGH", "Weather in 2 days: high"), ("WEATHER_D2_LOW", "Weather in 2 days: low")
    ];

    /// <summary>Whether a data name shows the weather (fetched only while a theme shows it).</summary>
    public static bool IsWeather(string? name) => name?.StartsWith("WEATHER", StringComparison.OrdinalIgnoreCase) == true;

    public const string SpeakerGlyph = "", MutedGlyph = "";

    /// <summary>Whether a data name needs the playback volume read.</summary>
    public static bool IsVolume(string? name) => name?.StartsWith("VOLUME", StringComparison.OrdinalIgnoreCase) == true;

    /// <summary>Whether a data name shows Claude Code or Codex usage (read only while a theme shows it).</summary>
    public static bool IsUsage(string? name) =>
        name?.StartsWith("CLAUDE_", StringComparison.OrdinalIgnoreCase) == true || name?.StartsWith("CODEX_", StringComparison.OrdinalIgnoreCase) == true;

    /// <summary>Segoe Fluent Icons glyphs for media buttons.</summary>
    public const string PlayGlyph = "", PauseGlyph = "", NextGlyph = "\uE893", PreviousGlyph = "\uE892";
    // Play / pause solid (the main button); previous, next and the quick-action icons in the outline set.

    /// <summary>Whether a data name can show a value with these settings (its sensor is mapped, or it needs none).</summary>
    public static bool IsAvailable(string name, MonitorSettings settings) =>
        !Unsupported.Contains(name) &&
        (SensorFor(name) is not { } sensor ||
         settings.Sensors.TryGetValue(sensor, out var mapping) && !string.IsNullOrWhiteSpace(mapping.RegistryValue));

    /// <param name="slot">The element's shortcut slot, for APP_NAME.</param>
    public static ThemeValue Read(string? name, string? format, bool fahrenheit, ThemeDataContext c, int slot = 0)
    {
        double? S(string key) => c.Sensors.Get(key);
        ThemeValue Num(double? value, string unit, string pattern = "F0") =>
            value is { } v && double.IsFinite(v)
                ? new(v.ToString(pattern, CultureInfo.CurrentCulture), v, unit)
                : new(Missing, null, unit);
        ThemeValue Temp(double? celsius) => fahrenheit && celsius is { } t ? Num(t * 9 / 5 + 32, "°F") : Num(celsius, "°C");
        ThemeValue Text(string? text) => new(string.IsNullOrWhiteSpace(text) ? Missing : text, null, "");
        var memory = name?.StartsWith("RAM", StringComparison.OrdinalIgnoreCase) == true ? c.Memory : null;
        var media = name?.StartsWith("MEDIA_", StringComparison.OrdinalIgnoreCase) == true ? NowPlayingView.From(c.Media ?? NowPlaying.Nothing) : null;
        var now = new DateTimeOffset(c.Now);
        // A window past its reset time has nothing used yet.
        ThemeValue Used(UsageWindow? window) => Num(window?.At(now).UsedPercent, "%");
        // A 5-hour window that hasn't started (none yet, or the last one ran out) while the weekly one is known: 0%, "not started".
        bool Idle(UsageWindow? session, UsageWindow? week) => week != null && session?.At(now).ResetsAt == null;
        ThemeValue SessionUsed(UsageWindow? session, UsageWindow? week) => Idle(session, week) ? Num(0, "%") : Used(session);
        ThemeValue SessionResets(UsageWindow? session, UsageWindow? week, string? f) => Idle(session, week) ? new("not started", null, "") : Worded(Resets(session), f);
        ThemeValue Resets(UsageWindow? window) => new(AiUsageReader.Until(window?.ResetsAt, now), null, "");

        return (name ?? "").ToUpperInvariant() switch
        {
            "CPUTEMP" => Temp(S("CpuTemp")),
            "CPULOAD" => Num(S("CpuLoad"), "%"),
            "CPUPWR" => Num(S("CpuPower"), "W"),
            "CPUFAN" => Num(S("CpuFan"), "RPM"),
            // The fan only matters when it stops: "OK" while it turns, "STOPPED" (number 100, for a critical color) at 0 RPM.
            "CPUFAN_STATE" => S("CpuFan") is { } rpm ? new(rpm < 1 ? "STOPPED" : "OK", rpm < 1 ? 100 : 0, "") : new(Missing, null, ""),
            // Only a problem is worth a slot: blank (no number) while the fan turns.
            "CPUFAN_ALERT" => S("CpuFan") is < 1 ? new("STOPPED", 100, "") : new("", null, ""),
            "CPUCLOCK" => Num(S("CpuClock"), "MHz"),
            "CPUCLOCK_G" => Num(S("CpuClock") / 1000, "GHz", "F2"),
            "CPUVOLTAGE" => Num(S("CpuVoltage"), "V", "F3"),
            "CPUMODEL" => Text(c.CpuName),
            "GPUTEMP" => Temp(S("GpuTemp")),
            "GPULOAD" => Num(S("GpuLoad"), "%"),
            "GPUPWR" => Num(S("GpuPower"), "W"),
            "GPUFAN" => Num(S("GpuFan"), "RPM"),
            "GPUCLOCK" => Num(S("GpuClock"), "MHz"),
            "GPUCLOCK_G" => Num(S("GpuClock") / 1000, "GHz", "F2"),
            "GPUVOLTAGE" => Num(S("GpuVoltage"), "V", "F3"),
            "GPURAMLOAD" => Num(S("GpuVramLoad"), "%"),
            "CPU_LIMIT" => Limit(("THERMAL LIMIT", S("CpuThermalLimit")), ("POWER LIMIT", S("CpuPowerLimit"))),
            "GPU_LIMIT" => Limit(("THERMAL LIMIT", S("GpuThermalLimit")), ("POWER LIMIT", S("GpuPowerLimit")), ("VOLTAGE LIMIT", S("GpuVoltageLimit"))),
            "SENSOR_STATUS" => new(c.SensorFeed switch { SensorFeedState.Frozen => "SENSORS PAUSED", SensorFeedState.Offline => "SENSORS OFFLINE", _ => "" }, null, ""), // blank (not --) while live
            "GPURAM" => Num(VramMb(c.Sensors), "MB"),
            // "8.6 / 16 GB": the total follows from used and the memory load (HWiNFO reports both, not the size).
            "GPURAM_GB_OF" => VramMb(c.Sensors) is { } vram
                ? new(S("GpuVramLoad") is { } vload and > 0 ? $"{vram / 1024:F1} / {Math.Round(vram / 1024 / (vload / 100)):0} GB" : $"{vram / 1024:F1} GB", vram / 1024, "")
                : new(Missing, null, ""),
            "GPUMODEL" => Text(HardwareNameReader.Short(c.GpuName)),
            "FPS" => Num(S("GpuFps"), "FPS"),
            "FPS_AVG" => Num(c.AverageFps, "FPS"),
            "FRAMETIME" => Num(FrameTime(S("GpuFrameTime"), S("GpuFps")), "ms", "F1"),
            "RAMLOAD" => Num(S("MemoryLoad"), "%"),
            "RAM" => Num(memory?.UsedMb, "MB"),
            "RAMVALID" => Num(memory?.FreeMb, "MB"),
            "RAMTOTAL" => Num(memory?.TotalMb, "MB"),
            "RAM_GB" => Num(memory?.UsedMb / 1024, "GB", "F1"),
            // "23.8 / 32 GB": used of total, so the number can't be mistaken for either alone.
            "RAM_GB_OF" => memory is { } m ? new($"{m.UsedMb / 1024:F1} / {Math.Round(m.TotalMb / 1024):0} GB", m.UsedMb / 1024, "") : new(Missing, null, ""),
            "RAMVALID_GB" => Num(memory?.FreeMb / 1024, "GB", "F1"),
            "RAMTOTAL_GB" => Num(memory?.TotalMb / 1024, "GB", "F1"),
            "WATERPUMP" => Num(S("PumpRpm"), "RPM"),
            "CASEFAN1" => Num(S("CaseFan1"), "RPM"),
            "CASEFAN2" => Num(S("CaseFan2"), "RPM"),
            "DRVLOAD" => Num(S("DriveLoad"), "%"),
            "HDDTEMP" => Temp(S("DriveTemp")),
            "HDDUSED" => Num(S("DriveUsed"), "%"),
            "UPSPEED" => Rate(S("NetUpload")),
            "DOWNDSPEED" => Rate(S("NetDownload")),
            "TIME" => Text(c.Now.ToString(TimePattern(format), CultureInfo.InvariantCulture)),
            "APM" => Text(c.Now.ToString("tt", CultureInfo.InvariantCulture)),
            "DATE" => Text(c.Now.ToString(DatePattern(format), CultureInfo.InvariantCulture)),
            "DAY" => Text(c.Now.ToString("ddd", CultureInfo.InvariantCulture)),
            "MEDIA_TITLE" => new(media!.Title, null, ""),
            "MEDIA_ARTIST" => new(media!.Artist, null, ""), // blank, not "--", when nothing is playing
            "MEDIA_STATE" => new(media!.Status, null, ""),
            "MEDIA_PLAYPAUSE" => new(c.Media?.State == MediaPlayback.Playing ? PauseGlyph : PlayGlyph, null, ""),
            "MEDIA_PROGRESS" => c.MediaPosition is { Length.TotalSeconds: > 0 } p
                ? Num(Math.Clamp(p.Elapsed / p.Length * 100, 0, 100), "%") : new(Missing, null, "%"),
            "MEDIA_ELAPSED" => c.MediaPosition is { } pe ? new(TouchTimers.Format(pe.Elapsed), pe.Elapsed.TotalSeconds, "") : new("", null, ""),
            "MEDIA_TIME" => c.MediaPosition is { } pt ? new($"{TouchTimers.Format(pt.Elapsed)} / {TouchTimers.Format(pt.Length)}", pt.Elapsed.TotalSeconds, "") : new("", null, ""),
            "MEDIA_REMAINING" => c.MediaPosition is { } pr ? new("−" + TouchTimers.Format(pr.Length - pr.Elapsed), (pr.Length - pr.Elapsed).TotalSeconds, "") : new("", null, ""),
            "APP_NAME" => new(c.Shortcut(slot)?.Name ?? "", null, ""), // an empty slot shows nothing
            "CLAUDE_5H" => SessionUsed(c.Usage?.ClaudeSession, c.Usage?.ClaudeWeek),
            "CLAUDE_WEEK" => Used(c.Usage?.ClaudeWeek),
            "CLAUDE_5H_RESET" => SessionResets(c.Usage?.ClaudeSession, c.Usage?.ClaudeWeek, format),
            "CLAUDE_WEEK_RESET" => Worded(Resets(c.Usage?.ClaudeWeek), format),
            "CODEX_5H" => SessionUsed(c.Usage?.CodexSession, c.Usage?.CodexWeek),
            "CODEX_WEEK" => Used(c.Usage?.CodexWeek),
            "CODEX_5H_RESET" => SessionResets(c.Usage?.CodexSession, c.Usage?.CodexWeek, format),
            "CODEX_WEEK_RESET" => Worded(Resets(c.Usage?.CodexWeek), format),
            "VOLUME" => c.Volume is { Muted: true } ? new("Muted", 0, "") : Num(c.Volume?.Level, "%"),
            "VOLUME_ICON" => new(c.Volume is { Muted: true } ? MutedGlyph : SpeakerGlyph, null, ""),
            "TIMER" => c.Timer is { } left ? new(TouchTimers.Format(left), left.TotalSeconds, "") : new(Missing, null, ""),
            "STOPWATCH" => c.Stopwatch is { } elapsed ? new(TouchTimers.Format(elapsed), elapsed.TotalSeconds, "") : new(Missing, null, ""),
            "TIMER_ACTION" => new(c.TimerRunning ? "Pause" : "Start", null, ""),
            "STOPWATCH_ACTION" => new(c.StopwatchRunning ? "Pause" : "Start", null, ""),
            "TIMER_PHASE" => new(c.TimerPhase, null, ""),
            // One card for both: the timer or the stopwatch, whichever is shown.
            "FOCUS_MODE" => new(c.StopwatchShown ? "STOPWATCH" : c.TimerPhase.Length > 0 ? c.TimerPhase : "TIMER", null, ""),
            "FOCUS_CLOCK" => c.StopwatchShown ? Read("STOPWATCH", null, false, c) : Read("TIMER", null, false, c),
            // Stopped partway, it resumes rather than starts.
            "FOCUS_ACTION" => new((c.StopwatchShown ? c.StopwatchRunning : c.TimerRunning) ? "Pause"
                : (c.StopwatchShown ? c.Stopwatch is { TotalSeconds: >= 1 } : c.TimerPaused) ? "Resume" : "Start", null, ""),
            // 1 while the shown clock runs (for a running color), else 0.
            "FOCUS_RUNNING" => new("", (c.StopwatchShown ? c.StopwatchRunning : c.TimerRunning) ? 1 : 0, ""),
            // "Round 2 / 4" during a Pomodoro, else blank.
            "FOCUS_ROUND" => new(c.PomodoroRound > 0 ? $"Round {(c.PomodoroRound - 1) % 4 + 1} / 4" : "", c.PomodoroRound > 0 ? c.PomodoroRound : null, ""),
            "FOCUS_SWITCH" => new(c.StopwatchShown ? "Timer" : "Stopwatch", null, ""),
            "DRIVE_FREE" => Drive(c, format) is { } d ? new($"{d.FreeGb:F0}", d.FreeGb, "GB free") : new(Missing, null, "GB free"),
            // Like memory: used of total, in TB from 1 TB ("1.5 / 2 TB", "180 / 500 GB").
            "DRIVE_USED_OF" => Drive(c, format) is { TotalGb: > 0 } du ? new(UsedOf(du.TotalGb - du.FreeGb, du.TotalGb), du.TotalGb - du.FreeGb, "") : new(Missing, null, ""),
            "DRIVE_USED_PCT" => Drive(c, format) is { } u && u.TotalGb > 0 ? Num((u.TotalGb - u.FreeGb) / u.TotalGb * 100, "%") : new(Missing, null, "%"),
            "MEDIA_ALBUM" => new(c.Media?.Album ?? "", null, ""),
            var w when IsWeather(w) => Weather(w, fahrenheit, c.Weather, c.Now),
            var g when IsGaming(g) => Gaming(g, fahrenheit, c.Gaming, format, S("GpuFps")),
            _ => new(Missing, null, "")
        };
    }

    /// <summary>Whether a data name shows gaming (the game being played, its session, play time).</summary>
    /// <summary>
    /// Frame time in ms: RTSS's own reading when it is paired, else worked out from FPS. Either way it is one
    /// reading per poll, so it shows sustained stutter rather than every single slow frame.
    /// </summary>
    internal static double? FrameTime(double? rtss, double? fps) =>
        rtss is > 0 and < 10000 ? rtss : fps is > 0 and < 10000 ? 1000 / fps : null;

    public static bool IsGaming(string? name) => name?.StartsWith("GAME_", StringComparison.OrdinalIgnoreCase) == true;

    /// <summary>"2h 14m", "45m" or "0m".</summary>
    internal static string PlayTime(TimeSpan time) =>
        time.TotalHours >= 1 ? $"{(int)time.TotalHours}h {time.Minutes:00}m" : $"{(int)time.TotalMinutes}m";

    /// <summary>Format "resets": "resets in 2h 14m" rather than the bare time.</summary>
    private static ThemeValue Worded(ThemeValue v, string? format) =>
        format == "resets" && v.Text.Length > 0 && v.Text != Missing ? v with { Text = "resets in " + v.Text } : v;

    private static readonly Dictionary<string, (DateTime At, (double, double)? Space)> DriveCache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Free and total space in GB of a drive (format "D" for D:, blank for the Windows drive), read at most every 30 s.</summary>
    internal static string UsedOf(double usedGb, double totalGb) =>
        totalGb >= 1000 ? $"{usedGb / 1000:F1} / {Math.Round(totalGb / 1000, totalGb < 10000 ? 1 : 0):0.#} TB" : $"{usedGb:F0} / {totalGb:F0} GB";

    private static (double FreeGb, double TotalGb)? Drive(ThemeDataContext c, string? format)
    {
        string letter = format?.Trim().TrimEnd(':', '\\') ?? "";
        if (c.DriveSpace != null) return c.DriveSpace(letter);
        lock (DriveCache)
        {
            if (DriveCache.TryGetValue(letter, out var cached) && DateTime.UtcNow - cached.At < TimeSpan.FromSeconds(30)) return cached.Space;
            (double, double)? space = null;
            try
            {
                string root = letter.Length == 1 ? letter + @":\" : Path.GetPathRoot(Environment.SystemDirectory) ?? @"C:\";
                var drive = new DriveInfo(root);
                if (drive.IsReady) space = (drive.AvailableFreeSpace / 1e9, drive.TotalSize / 1e9);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException) { }
            DriveCache[letter] = (DateTime.UtcNow, space);
            return space;
        }
    }

    private static ThemeValue Gaming(string name, bool fahrenheit, GamingSnapshot? g, string? format = null, double? live = null)
    {
        ThemeValue Fps(double? v) => v is { } f ? new(f.ToString("F0", CultureInfo.CurrentCulture), f, "FPS") : new(Missing, null, "FPS");
        ThemeValue Temp(double? c) => c is not { } t ? new(Missing, null, fahrenheit ? "°F" : "°C")
            : fahrenheit ? new((t * 9 / 5 + 32).ToString("F0", CultureInfo.CurrentCulture), t * 9 / 5 + 32, "°F") : new(t.ToString("F0", CultureInfo.CurrentCulture), t, "°C");
        ThemeValue Played(TimeSpan? t) => t is { } v ? new(PlayTime(v), v.TotalMinutes, "") : new(Missing, null, "");
        return name switch
        {
            "GAME_NAME" => new(g?.Game ?? "", null, ""),
            "GAME_STATE" => new(g == null || g.Game == null ? "NO GAME YET" : g.Playing ? "PLAYING" : "LAST PLAYED", null, ""),
            // Format "short" matches the play-time totals ("1h 24m"); the default ticks the seconds ("1:24:10").
            // While playing, this session; afterwards the last session's length (as the Gaming page shows it).
            "GAME_SESSION" => g?.Session is { } s ? new(format == "short" ? PlayTime(s) : TouchTimers.Format(s), s.TotalMinutes, "")
                : g?.Recent.FirstOrDefault() is { } last ? new(PlayTime(last.Length), last.Length.TotalMinutes, "") : new(g?.Game == null ? Missing : "", null, ""),
            // Live FPS only while a game runs: after it the FPS sensor still reads (the desktop, a launcher), which isn't the game's.
            "GAME_FPS" => g is { Playing: true } ? Fps(live) : Fps(null),
            "GAME_FPS_AVG" => Fps(g?.AvgFps),
            "GAME_FPS_LOW" => Fps(g?.LowFps),
            // The 1% low as a share of the average: low numbers mean stutter (for warning colors).
            "GAME_FPS_LOW_PCT" => g is { LowFps: { } low, AvgFps: > 0 } ? new($"{low / g.AvgFps!.Value * 100:F0}", low / g.AvgFps.Value * 100, "%") : new(Missing, null, "%"),
            "GAME_PEAK_CPU" => Temp(g?.PeakCpuC),
            "GAME_PEAK_GPU" => Temp(g?.PeakGpuC),
            "GAME_TODAY" => Played(g?.Today),
            "GAME_WEEK" => Played(g?.Week),
            _ => new(Missing, null, "")
        };
    }

    /// <summary>Weather values: temperatures as "15" with the unit "°" (°F when chosen for the city or the element).</summary>
    /// <summary>The next sunrise or sunset after <paramref name="now"/>: today's sunrise, then today's sunset, then tomorrow's sunrise.</summary>
    internal static (DateTime? At, string Label) NextSun(WeatherReport w, DateTime now) =>
        w.Sunrise is { } rise && now < rise ? (rise, "SUNRISE")
        : w.Sunset is { } set && now < set ? (set, "SUNSET")
        : (w.SunriseTomorrow ?? w.Sunrise?.AddDays(1), "SUNRISE");

    /// <summary>
    /// Whether it is daytime now: between today's sunrise and sunset when the report has them for today, else what the
    /// report said when fetched (it can be up to half an hour old, which put a sun up after sunset).
    /// </summary>
    internal static bool IsDay(WeatherReport w, DateTime now) =>
        w.Sunrise is { } rise && w.Sunset is { } set && rise.Date == now.Date ? now >= rise && now < set : w.IsDay;

    /// <summary>"Rain from 15:00", "Rain until 17:00", or "No rain in the next 12 h", from the hourly forecast.</summary>
    internal static string RainLine(WeatherReport w, DateTime now)
    {
        var next = w.Hours.Where(h => h.Time > now.AddHours(-1) && h.Time <= now.AddHours(12)).ToList();
        if (next.Count == 0) return "";
        static bool Wet(WeatherHour h) => WeatherClient.Wet(h.Code) || h.RainChance >= 50;
        if (WeatherClient.Wet(w.Code) || Wet(next[0]))
            return next.FirstOrDefault(h => !Wet(h)) is { } dry ? $"Rain until {dry.Time:HH:mm}" : "Rain for the next 12 h";
        return next.FirstOrDefault(Wet) is { } wet ? $"Rain from {wet.Time:HH:mm}" : "No rain in the next 12 h";
    }

    private static ThemeValue Weather(string name, bool fahrenheit, WeatherReport? w, DateTime now)
    {
        if (w == null) return new(Missing, null, name.EndsWith("ICON", StringComparison.Ordinal) ? "" : "°");
        bool f = fahrenheit || w.Fahrenheit;
        ThemeValue T(double celsius)
        {
            if (!double.IsFinite(celsius)) return new(Missing, null, "°");
            double v = Math.Round(f ? celsius * 9 / 5 + 32 : celsius);
            return new(v.ToString("F0", CultureInfo.CurrentCulture), v, "°");
        }
        ThemeValue Text(string text) => new(text, null, "");
        WeatherDay? Day(int n) => w.Days.Count > n ? w.Days[n] : null;
        WeatherHour? Hour(int n) => w.Hours.Where(h => h.Time > now).Skip((n - 1) * 3).FirstOrDefault();
        string Clock(DateTime? t) => t?.ToString("HH:mm", CultureInfo.InvariantCulture) ?? Missing;
        bool day = IsDay(w, now);
        return name switch
        {
            "WEATHER" => Text($"{T(w.TempC).Text}° {WeatherClient.Describe(w.Code)}"),
            "WEATHER_TEMP" => T(w.TempC),
            "WEATHER_FEELS" => T(w.FeelsC),
            // Only when it differs: within 2 °C it would just repeat the temperature.
            "WEATHER_FEELS_NOTE" => Math.Abs(w.FeelsC - w.TempC) >= 2 ? Text($"Feels {T(w.FeelsC).Text}°") : Text(""),
            "WEATHER_KIND" => new("", WeatherClient.Kind(w.Code, day), ""),
            "WEATHER_RAIN" => Text(RainLine(w, now)),
            "WEATHER_HIGH" => T(w.HighC),
            "WEATHER_LOW" => T(w.LowC),
            "WEATHER_TEXT" => Text(WeatherClient.Describe(w.Code)),
            // Number: 1 by day, 0 at night (so a theme can color the symbol for night).
            "WEATHER_ICON" => new(WeatherClient.Symbol(w.Code, day), day ? 1 : 0, ""),
            "WEATHER_SUN_NEXT" => Text(Clock(NextSun(w, now).At)),
            "WEATHER_SUN_NEXT_LABEL" => Text(NextSun(w, now).Label),
            "WEATHER_CITY" => Text(w.City),
            "WEATHER_SUNRISE" => Text(Clock(w.Sunrise)),
            "WEATHER_SUNSET" => Text(Clock(w.Sunset)),
            "WEATHER_D1_DAY" or "WEATHER_D2_DAY" => Day(name[9] - '0') is { } d ? Text(d.Date.ToString("ddd", CultureInfo.InvariantCulture)) : Text(Missing),
            "WEATHER_D1_ICON" or "WEATHER_D2_ICON" => Day(name[9] - '0') is { } d ? Text(WeatherClient.Symbol(d.Code)) : Text(Missing),
            "WEATHER_D1_HIGH" or "WEATHER_D2_HIGH" => Day(name[9] - '0') is { } d ? T(d.HighC) : new(Missing, null, "°"),
            "WEATHER_D1_LOW" or "WEATHER_D2_LOW" => Day(name[9] - '0') is { } d ? T(d.LowC) : new(Missing, null, "°"),
            "WEATHER_D1_KIND" or "WEATHER_D2_KIND" => Day(name[9] - '0') is { } d ? new("", WeatherClient.Kind(d.Code), "") : new("", null, ""),
            // The next hours, three apart: H1 is the coming hour, H2 three hours on, and so on.
            _ when name.StartsWith("WEATHER_H", StringComparison.Ordinal) && name.Length > 11 && name[9] is >= '1' and <= '4' && name[10] == '_' =>
                Hour(name[9] - '0') is not { } h ? new(Missing, null, name.EndsWith("TEMP", StringComparison.Ordinal) ? "°" : "")
                : name[11..] switch
                {
                    "TIME" => Text(h.Time.ToString("HH:mm", CultureInfo.InvariantCulture)),
                    "ICON" => Text(WeatherClient.Symbol(h.Code, h.IsDay)),
                    "TEMP" => T(h.TempC),
                    "KIND" => new("", WeatherClient.Kind(h.Code, h.IsDay), ""),
                    "RAIN" => new($"{h.RainChance:F0}", h.RainChance, "%"),
                    _ => new(Missing, null, "")
                },
            _ => new(Missing, null, "")
        };
    }

    /// <summary>Text shown by a Data element, with its unit when the theme asks for one.</summary>
    public static string Display(ThemeValue value, bool showUnit, bool spaced = false) =>
        showUnit && value.Unit.Length > 0 && value.Text != Missing ? value.Text + (spaced && char.IsLetter(value.Unit[0]) ? " " : "") + value.Unit : value.Text;

    // Network sensors are MB/s; themes chart and label them in KB/s, switching to MB/s when large.
    private static ThemeValue Rate(double? megabytesPerSecond)
    {
        if (megabytesPerSecond is not { } mb || !double.IsFinite(mb)) return new(Missing, null, "KB/s");
        double kb = mb * 1000;
        return kb < 1000
            ? new(kb.ToString("F0", CultureInfo.CurrentCulture), kb, "KB/s")
            : new(mb.ToString("F1", CultureInfo.CurrentCulture), kb, "MB/s");
    }

    /// <summary>The first limit that is on (thermal before power before voltage), blank when none is; 1 / 0 for warning colors.</summary>
    private static ThemeValue Limit(params (string Label, double? Flag)[] limits) =>
        limits.FirstOrDefault(l => l.Flag is > 0.5) is { Label: { } on }
            ? new(on, 1, "")
            : new("", limits.Any(l => l.Flag.HasValue) ? 0 : null, "");

    private static double? VramMb(SensorSnapshot sensors)
    {
        var text = sensors.GetText("GpuVram");
        return text?.Contains("GB", StringComparison.OrdinalIgnoreCase) == true && SensorReader.ParseNumber(text) is { } gb
            ? gb * 1024 : sensors.Get("GpuVram");
    }

    internal static string TimePattern(string? format) => format switch
    {
        "h:m:s" => "HH:mm:ss",
        // Hours or minutes alone, so a theme can set the separator apart (an accent-colored colon).
        "h" => "HH",
        "m" => "mm",
        _ => "HH:mm"
    };

    // L-Connect writes date formats as Y/M/D tokens with separators, e.g. "Y-M-D" or "Y.M.D".
    internal static string DatePattern(string? format)
    {
        if (string.IsNullOrWhiteSpace(format)) return "yyyy-MM-dd";
        if (format == "long") return "dddd, MMM d"; // Sunday, Nov 8
        if (format == "short") return "ddd, MMM d";
        if (format == "month") return "MMM d"; // Dec 31, beside a separate weekday
        var pattern = new System.Text.StringBuilder();
        foreach (char ch in format)
            pattern.Append(ch switch { 'Y' => "yyyy", 'M' => "MM", 'D' => "dd", _ => "'" + ch + "'" });
        return pattern.ToString();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatus
    {
        public uint Length, Load;
        public ulong TotalPhys, AvailPhys, TotalPageFile, AvailPageFile, TotalVirtual, AvailVirtual, AvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatus status);

    internal static (double UsedMb, double FreeMb, double TotalMb)? ReadMemory()
    {
        var status = new MemoryStatus { Length = (uint)Marshal.SizeOf<MemoryStatus>() };
        if (!GlobalMemoryStatusEx(ref status)) return null;
        double total = status.TotalPhys / 1048576.0, free = status.AvailPhys / 1048576.0;
        return (total - free, free, total);
    }
}
