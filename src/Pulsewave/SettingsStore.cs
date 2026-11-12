using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Pulsewave;

public sealed class SensorMapping
{
    public string RegistryValue { get; set; } = "";
    public double Scale { get; set; } = 1.0;
    // Identity fields are added when the sensor is explicitly picked in V0.2.
    // If HWiNFO renumbers its Gadget slots, the reader can find it by name.
    public string RegistryHive { get; set; } = "";
    public string SensorGroup { get; set; } = "";
    public string SensorLabel { get; set; } = "";
    // Name = resilient to slot changes; Slot = resilient to title/name changes (FPS).
    public string MatchMode { get; set; } = "";
}

public sealed class MonitorSettings
{
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalSettings { get; set; }
    public string MonitorDeviceName { get; set; } = "";
    public string MonitorInterfaceId { get; set; } = "";
    public bool StartHidden { get; set; } = false;
    // What double-clicking the tray icon does (TrayActions): "restore" by default, as before.
    public string TrayDoubleClick { get; set; } = TrayActions.Default;
    public bool ClickThrough { get; set; } = true;
    public bool AlwaysOnTop { get; set; } = false;
    // Off restores the system uptime readout in the Time panel and stops media reads.
    public bool ShowNowPlaying { get; set; } = true;
    // The user's theme: "builtin:<template>" or a folder in the themes folder; empty (new settings, or Classic before
    // 0.14) shows Lumen Lifestyle and is saved as it.
    public string ActiveTheme { get; set; } = "";
    // Themes the playlist rotates through, in order (ids as in ActiveTheme); empty means every theme.
    public List<string> ThemePlaylist { get; set; } = new();
    // Themes gallery shape filter: "" all, "fits" shaped like the display, "horizontal", "vertical".
    public string ThemeGalleryFilter { get; set; } = "";
    // Color palette each theme is shown in (theme id → palette name); a missing entry shows the theme's own colors.
    public Dictionary<string, string> ThemePalettes { get; set; } = new(StringComparer.Ordinal);
    public bool PlaylistEnabled { get; set; } = false;
    public int PlaylistMinutes { get; set; } = 5;
    // Global hotkeys such as "Ctrl+Alt+PageDown"; empty turns one off.
    public string HotkeyNextTheme { get; set; } = "Ctrl+Alt+PageDown";
    public string HotkeyPreviousTheme { get; set; } = "Ctrl+Alt+PageUp";
    public string HotkeyTogglePlaylist { get; set; } = "Ctrl+Alt+End";
    // Since 0.12 every hotkey is a binding (tray → Hotkeys); the four fields above and HotkeyAesthetic only seed them once
    // and are otherwise left as they were. Null: not migrated yet.
    public List<HotkeyBinding>? Hotkeys { get; set; }
    public string CpuName { get; set; } = "Intel Core Ultra 7 265K";
    public string GpuName { get; set; } = "NVIDIA GeForce RTX 5070 Ti";
    public string RegistryPath { get; set; } = @"SOFTWARE\HWiNFO64\VSB";
    public string RegistryHive { get; set; } = "CurrentUser";
    public int PollIntervalMs { get; set; } = 1000;
    public int HistorySeconds { get; set; } = 90;
    public bool EnableThresholdAlerts { get; set; } = false;
    public double CpuTemperatureAlertC { get; set; } = 90;
    public double GpuTemperatureAlertC { get; set; } = 85;
    public double MemoryAlertPercent { get; set; } = 95;
    // Programs, games and links the HUD can open; theme shortcut slots are 1-based positions in this list.
    public List<AppShortcut> Shortcuts { get; set; } = new();
    // The Gaming and Productivity lists (tray → Shortcuts → List): ordered slot numbers for themes that follow them.
    public Dictionary<string, List<int>> ShortcutLists { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    // Dock mode: "" covers the whole selected display; Top, Bottom, Left or Right reserves that edge of it
    // (like the taskbar), DockPercent of the display's short side deep, or 0 to fit the theme's shape.
    public string DockEdge { get; set; } = "";
    public int DockPercent { get; set; } = 0;
    // Degrees the HUD is turned on its display (0, 90, 180, 270): a HYTE Y70 Windows shows as 2560x682
    // can run vertical themes without changing Windows display settings.
    public int HudRotation { get; set; } = 0;
    // Opt-in: move other windows that open on the HUD's display back to the main display.
    public bool KeepDisplayClear { get; set; } = false;
    // Opt-in: read Claude plan usage with the Claude app's sign-in (see ClaudeAccountUsage).
    public bool ClaudeAccountUsage { get; set; } = false;
    // LED control through OpenRGB's SDK server on this PC (tray → Lights, and the HUD's Lights page).
    public LightsSettings Lights { get; set; } = new();
    // Times the LEDs turn off and on, and optionally the HUD blanks (tray → Schedule).
    public List<ScheduleRule> Schedule { get; set; } = new();
    // Screen off by schedule also puts the HUD's display in standby over DDC/CI when it supports that
    // (never the main display, never in dock mode).
    public bool ScreenPowerOff { get; set; } = true;
    // Aesthetic mode: the HUD fades ("fade", to AestheticFadePercent) or hides ("hide") so the wallpaper or
    // screensaver under it shows; a tap wakes it for AestheticWakeSeconds (each tap starts that time again).
    public bool Aesthetic { get; set; } = false;
    // The city weather data names show (tray → Weather), from Open-Meteo.
    public WeatherSettings Weather { get; set; } = new();
    // Games added by hand, programs that are never games, and whether to suggest new ones (tray → Gaming…).
    public GamingSettings Gaming { get; set; } = new();
    public string AestheticStyle { get; set; } = "fade";
    public int AestheticFadePercent { get; set; } = 15;
    public int AestheticWakeSeconds { get; set; } = 60;
    public string HotkeyAesthetic { get; set; } = "";

    // The mapping intentionally matches the supplied AIO.ini. Raw network units
    // and the formatted VRAM field need validation against the user's HWiNFO setup.
    public Dictionary<string, SensorMapping> Sensors { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["CpuLoad"] = new() { RegistryValue = "ValueRaw1" },
        ["CpuTemp"] = new() { RegistryValue = "ValueRaw2" },
        ["CpuPower"] = new() { RegistryValue = "ValueRaw3" },
        ["CpuFan"] = new() { RegistryValue = "ValueRaw4" },
        ["GpuLoad"] = new() { RegistryValue = "ValueRaw13" },
        ["GpuTemp"] = new() { RegistryValue = "Value8" },
        ["GpuPower"] = new() { RegistryValue = "ValueRaw12" },
        ["GpuFps"] = new() { RegistryValue = "Value19", MatchMode = "Slot" },
        ["GpuVram"] = new() { RegistryValue = "Value38" },
        ["MemoryLoad"] = new() { RegistryValue = "ValueRaw0" },
        ["NetUpload"] = new() { RegistryValue = "ValueRaw26", Scale = 0.001 },
        ["NetDownload"] = new() { RegistryValue = "ValueRaw25", Scale = 0.001 }
    };
}

public static class SettingsStore
{
    public static string DirectoryPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Lumen HUD");
    public static string FilePath => Path.Combine(DirectoryPath, "settings.json");
    // Preserve V0.7.0 and earlier preferences by reading the previous folder once.
    private static string LegacyFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GraphiteMonitor", "settings.json");
    /// <summary>Where Pulsewave (0.7.1–0.13) kept its settings, themes and sessions; copied over once by Lumen HUD 0.14.</summary>
    internal static string PulsewaveDirectoryPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Pulsewave");

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    private static readonly object WriteLock = new();
    public static MonitorSettings Load()
    {
        if (!File.Exists(FilePath) && File.Exists(Path.Combine(PulsewaveDirectoryPath, "settings.json")))
            CopyPulsewaveFolder(PulsewaveDirectoryPath, DirectoryPath);
        if (!File.Exists(FilePath) && File.Exists(LegacyFilePath)) MigrateLegacySettings();
        return LoadFile(FilePath);
    }

    /// <summary>
    /// Copies Pulsewave's folder (settings and their backup, the user's themes, gaming sessions, the Plex token, weather
    /// and AI usage) into Lumen HUD's, file by file without overwriting; settings.json goes last, so a copy cut short is
    /// simply redone. The Pulsewave folder is left as it was (the old version still runs from it). Templates are skipped:
    /// they are written again from this version.
    /// </summary>
    internal static void CopyPulsewaveFolder(string from, string to)
    {
        try
        {
            foreach (var file in Directory.EnumerateFiles(from, "*", SearchOption.AllDirectories))
            {
                string relative = Path.GetRelativePath(from, file);
                if (relative.StartsWith("templates" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                    relative.Equals("settings.json", StringComparison.OrdinalIgnoreCase)) continue;
                string target = Path.Combine(to, relative);
                if (File.Exists(target)) continue;
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(file, target);
            }
            Directory.CreateDirectory(to);
            string settings = Path.Combine(to, "settings.json");
            if (!File.Exists(settings)) File.Copy(Path.Combine(from, "settings.json"), settings);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            if (!File.Exists(Path.Combine(to, "settings.json")))
                throw new InvalidDataException("Could not copy your Pulsewave settings from " + from +
                    ". They are unchanged there; close Pulsewave and check file access.", ex);
        }
    }

    private static void MigrateLegacySettings()
    {
        Directory.CreateDirectory(DirectoryPath);
        string stagingPath = FilePath + "." + Guid.NewGuid().ToString("N") + ".migration";
        try
        {
            using (var source = new FileStream(LegacyFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var destination = new FileStream(stagingPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                source.CopyTo(destination);
                destination.Flush(flushToDisk: true);
            }
            // If another Pulsewave instance won the migration race, retain its file.
            if (!File.Exists(FilePath)) File.Move(stagingPath, FilePath);
            string legacyBackup = LegacyFilePath + ".bak";
            string newBackup = FilePath + ".bak";
            if (!File.Exists(newBackup) && File.Exists(legacyBackup)) File.Copy(legacyBackup, newBackup);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            if (!File.Exists(FilePath))
                throw new InvalidDataException("Could not migrate existing settings from " + LegacyFilePath +
                    ". The original remains unchanged; close the previous version and check file access.", ex);
        }
        finally
        {
            if (File.Exists(stagingPath)) File.Delete(stagingPath);
        }
    }

    // Path-specific entry points keep regression fixtures away from real preferences.
    internal static MonitorSettings LoadFile(string filePath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(filePath))!);
        if (!File.Exists(filePath))
        {
            var defaults = new MonitorSettings(); SaveFile(filePath, defaults); return defaults;
        }
        try { return Parse(File.ReadAllText(filePath)); }
        catch (Exception ex) when (ex is JsonException or InvalidDataException)
        {
            throw new InvalidDataException("Settings could not be read. Original unchanged: " + filePath +
                ". Close Lumen HUD and repair it or restore a known-good settings.json.bak, then reload.", ex);
        }
    }
    private static MonitorSettings Parse(string json)
    {
        var config = JsonSerializer.Deserialize<MonitorSettings>(json, Options)
                     ?? throw new InvalidDataException("Settings contains no object.");
        Normalize(config); return config;
    }
    private static void Normalize(MonitorSettings config)
    {
        config.Sensors ??= new MonitorSettings().Sensors;
        foreach (var (id, sensor) in config.Sensors)
        {
            // Never guess replacements for explicitly damaged sensor identities.
            if (sensor == null || sensor.RegistryValue == null || !double.IsFinite(sensor.Scale))
                throw new InvalidDataException("Invalid sensor mapping: " + id);
            sensor.RegistryHive ??= ""; sensor.SensorGroup ??= ""; sensor.SensorLabel ??= "";
            if (string.IsNullOrWhiteSpace(sensor.MatchMode))
                sensor.MatchMode = id.Equals("GpuFps", StringComparison.OrdinalIgnoreCase) ? "Slot" : "Name";
        }
        config.CpuName ??= "CPU"; config.GpuName ??= "GPU";
        config.RegistryPath ??= @"SOFTWARE\HWiNFO64\VSB"; config.RegistryHive ??= "CurrentUser";
        config.MonitorDeviceName ??= ""; config.MonitorInterfaceId ??= "";
        config.ActiveTheme ??= "";
        config.ThemeGalleryFilter ??= "";
        config.TrayDoubleClick = TrayActions.Normalize(config.TrayDoubleClick);
        config.ThemePalettes = new Dictionary<string, string>((config.ThemePalettes ?? new()).Where(p => p.Key != null && !string.IsNullOrEmpty(p.Value)), StringComparer.Ordinal);
        config.AestheticStyle = config.AestheticStyle is "fade" or "hide" ? config.AestheticStyle : "fade";
        config.AestheticFadePercent = Math.Clamp(config.AestheticFadePercent, 5, 90);
        config.AestheticWakeSeconds = Math.Clamp(config.AestheticWakeSeconds, 10, 3600);
        config.HotkeyAesthetic ??= "";
        config.ThemePlaylist = (config.ThemePlaylist ?? new()).Where(id => id != null).Distinct(StringComparer.Ordinal).ToList();
        config.PlaylistMinutes = Math.Clamp(config.PlaylistMinutes, 1, 1440);
        config.HotkeyNextTheme ??= ""; config.HotkeyPreviousTheme ??= ""; config.HotkeyTogglePlaylist ??= "";
        config.Hotkeys = HotkeyBindings.Normalize(config.Hotkeys, config);
        config.PollIntervalMs = Math.Clamp(config.PollIntervalMs, 250, 5000);
        config.HistorySeconds = Math.Clamp(config.HistorySeconds, 30, 600);
        config.HudRotation = config.HudRotation is 90 or 180 or 270 ? config.HudRotation : 0;
        config.DockEdge = config.DockEdge is "Top" or "Bottom" or "Left" or "Right" ? config.DockEdge : "";
        config.DockPercent = config.DockPercent <= 0 ? 0 : Math.Clamp(config.DockPercent, 8, 50);
        config.Lights ??= new();
        config.Lights.Normalize();
        config.Gaming ??= new();
        config.Gaming.Normalize();
        config.Weather ??= new();
        config.Weather.Normalize();
        config.Schedule = (config.Schedule ?? new()).Where(r => r != null).Take(16).ToList();
        foreach (var rule in config.Schedule) rule.Normalize();
        config.Shortcuts = (config.Shortcuts ?? new()).Where(s => s != null).Take(AppShortcut.MaxCount).ToList();
        config.ShortcutLists = Pulsewave.ShortcutLists.Normalize(config.ShortcutLists);
        foreach (var shortcut in config.Shortcuts)
        {
            shortcut.Name ??= ""; shortcut.Target ??= ""; shortcut.Arguments ??= "";
            shortcut.Kind = shortcut.Kind is AppShortcut.KeysKind or AppShortcut.SystemKind ? shortcut.Kind : "";
        }
    }
    public static void Save(MonitorSettings config) => SaveFile(FilePath, config);
    internal static void SaveFile(string filePath, MonitorSettings config)
    {
        lock (WriteLock)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(filePath))!);
            Normalize(config);
            string serialized = JsonSerializer.Serialize(config, Options);
            // Prevent automatic placement saves and explicit edits from erasing a
            // damaged original while startup is running on temporary defaults.
            if (File.Exists(filePath))
            {
                try { Parse(File.ReadAllText(filePath)); }
                catch (Exception ex) when (ex is JsonException or InvalidDataException)
                {
                    throw new InvalidDataException("Save blocked to preserve damaged settings: " + filePath +
                        ". Close Lumen HUD and repair it or restore settings.json.bak, then reload.", ex);
                }
            }
            string stagingPath = filePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var stream = new FileStream(stagingPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                using (var writer = new StreamWriter(stream))
                {
                    writer.Write(serialized); writer.Flush(); stream.Flush(flushToDisk: true);
                }
                if (File.Exists(filePath)) File.Replace(stagingPath, filePath, filePath + ".bak", ignoreMetadataErrors: true);
                else File.Move(stagingPath, filePath);
            }
            finally
            {
                if (File.Exists(stagingPath)) File.Delete(stagingPath); // exact staging file owned by this call
            }
        }
    }
}


