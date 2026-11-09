using System.IO;
using System.Text.Json;

namespace Pulsewave;

/// <summary>
/// What the schedule changed on the HUD's display and must undo: the backlight it lowered (and the level
/// to put back) and the display it put in standby. Kept in display-restore.json beside settings.json while
/// in effect, so a crash or a killed process is undone on the next start, not only a normal exit.
/// </summary>
public sealed class DisplayRestore
{
    public string? BacklightDevice { get; set; }
    public uint Brightness { get; set; }
    public string? StandbyDevice { get; set; }

    public bool IsEmpty => string.IsNullOrEmpty(BacklightDevice) && string.IsNullOrEmpty(StandbyDevice);

    public static string DefaultFile => Path.Combine(SettingsStore.DirectoryPath, "display-restore.json");

    /// <summary>The saved record, or null when there is none or it can't be read (nothing to undo then).</summary>
    public static DisplayRestore? Load(string file)
    {
        try
        {
            if (!File.Exists(file)) return null;
            var state = JsonSerializer.Deserialize<DisplayRestore>(File.ReadAllText(file));
            return state is { IsEmpty: false } ? state : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { return null; }
    }

    /// <summary>Writes the record, or deletes the file when there is nothing left to undo. Failures are ignored: this is a safety net.</summary>
    public static void Save(string file, DisplayRestore state)
    {
        try
        {
            if (state.IsEmpty) { File.Delete(file); return; }
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            string temp = file + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(state));
            File.Move(temp, file, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    /// <summary>
    /// Undoes a saved record for the displays connected now (<paramref name="connected"/>): brightness back, standby off.
    /// A display that isn't connected is left alone. Returns what was done, for the schedule's status line.
    /// </summary>
    public static string? Undo(DisplayRestore? state, ICollection<string> connected, Func<string, uint, bool> setBrightness, Func<string, bool> turnOn)
    {
        if (state == null) return null;
        var done = new List<string>();
        if (state.StandbyDevice is { Length: > 0 } standby && connected.Contains(standby) && turnOn(standby)) done.Add("display switched back on");
        if (state.BacklightDevice is { Length: > 0 } device && connected.Contains(device) && setBrightness(device, state.Brightness))
            done.Add($"brightness put back to {state.Brightness}");
        return done.Count == 0 ? null : "After an unexpected exit: " + string.Join(", ", done) + ".";
    }
}
