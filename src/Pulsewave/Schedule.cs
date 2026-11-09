using System.Globalization;

namespace Pulsewave;

/// <summary>
/// One schedule entry: from Off to On on the ticked days, the LEDs are off and/or the HUD is blank.
/// An Off time later than On runs past midnight (23:00 to 07:00); the day is the one it starts on.
/// </summary>
public sealed class ScheduleRule
{
    public bool Enabled { get; set; } = true;
    public string Off { get; set; } = "23:00";
    public string On { get; set; } = "07:00";
    /// <summary>Days it starts on: bit 0 is Sunday … bit 6 Saturday (127 = every day).</summary>
    public int Days { get; set; } = 127;
    public bool Lights { get; set; } = true;
    /// <summary>Blank the HUD (all black; a tap wakes it for a minute).</summary>
    public bool Screen { get; set; }
    /// <summary>Dim the HUD for the period (night mode).</summary>
    public bool Dim { get; set; }
    /// <summary>
    /// How to dim: "Theme" darkens what the HUD draws and drops its glows; "Backlight" lowers the panel's own
    /// brightness over DDC/CI (darker in a dark room, where an LCD still glows behind a dark picture); "Both".
    /// </summary>
    public string DimMode { get; set; } = "Theme";
    /// <summary>Brightness kept while dimmed, 10–90 %.</summary>
    public int DimLevel { get; set; } = 40;

    public static readonly string[] DimModes = ["Theme", "Backlight", "Both"];

    public ScheduleRule Copy() => (ScheduleRule)MemberwiseClone();

    internal static bool TryTime(string? text, out TimeOnly time) =>
        TimeOnly.TryParseExact(text?.Trim(), ["H:mm", "HH:mm"], CultureInfo.InvariantCulture, DateTimeStyles.None, out time);

    public void Normalize()
    {
        Off = TryTime(Off, out var off) ? off.ToString("HH:mm", CultureInfo.InvariantCulture) : "23:00";
        On = TryTime(On, out var on) ? on.ToString("HH:mm", CultureInfo.InvariantCulture) : "07:00";
        Days &= 127;
        DimMode = DimModes.FirstOrDefault(m => m.Equals(DimMode?.Trim(), StringComparison.OrdinalIgnoreCase)) ?? "Theme";
        DimLevel = Math.Clamp(DimLevel, 10, 90);
    }

    /// <summary>Whether this rule's off period covers <paramref name="now"/>.</summary>
    public bool Covers(DateTime now)
    {
        if (!Enabled || Days == 0 || !TryTime(Off, out var off) || !TryTime(On, out var on) || off == on) return false;
        var t = TimeOnly.FromDateTime(now);
        bool today = (Days & (1 << (int)now.DayOfWeek)) != 0, yesterday = (Days & (1 << (int)now.AddDays(-1).DayOfWeek)) != 0;
        return off < on ? today && t >= off && t < on
                        : today && t >= off || yesterday && t < on;
    }
}

/// <summary>What the schedule wants right now.</summary>
/// <summary>What the schedule wants right now. Dim is the mode and brightness of the first covering rule that dims, or null.</summary>
internal readonly record struct ScheduleState(bool LightsOff, bool ScreenOff, (string Mode, int Level)? Dim = null);

/// <summary>
/// Turns the schedule into actions at its edges only: when an off period starts the LEDs go off,
/// and when it ends they come back on only if the schedule turned them off, so lights switched by
/// hand in between are left as they are. A failed LED command is retried each minute.
/// </summary>
internal sealed class Scheduler
{
    private bool? _lightsWant, _screenWant;
    private (string Mode, int Level)? _dimWant;
    private bool _dimKnown;
    private bool _lightsPending;
    private DateTime _retryAt;

    public static ScheduleState Wanted(IEnumerable<ScheduleRule> rules, DateTime now)
    {
        var active = rules.Where(r => r.Covers(now)).ToList();
        var dim = active.FirstOrDefault(r => r.Dim);
        return new ScheduleState(active.Any(r => r.Lights), active.Any(r => r.Screen), dim is null ? null : (dim.DimMode, dim.DimLevel));
    }

    /// <summary>Starts over (the rules changed): the next tick acts on the current state again.</summary>
    public void Reset() { _lightsWant = null; _screenWant = null; _dimKnown = false; _lightsPending = false; _retryAt = default; }

    /// <summary>The dimming wanted when it just changed (null inside: not dimmed), else false; like ScreenChange.</summary>
    public bool DimChange(ScheduleState state, out (string Mode, int Level)? dim)
    {
        dim = state.Dim;
        if (_dimKnown && _dimWant == state.Dim) return false;
        (_dimKnown, _dimWant) = (true, state.Dim);
        return true;
    }

    /// <summary>The screen's wanted state when it just changed, else null.</summary>
    public bool? ScreenChange(ScheduleState state)
    {
        // The first check after start-up or a schedule edit reports either way, so a screen the old
        // rules blanked comes back when the new ones say it should be on.
        if (_screenWant == state.ScreenOff) return null;
        _screenWant = state.ScreenOff;
        return state.ScreenOff;
    }

    /// <summary>
    /// The LED command to send now, or null. Call <see cref="LightsDone"/> with the result.
    /// <paramref name="lightsOff"/> and <paramref name="scheduledOff"/> come from the saved light settings.
    /// </summary>
    public LightsKind? LightsCommand(ScheduleState state, DateTime now, bool lightsOff, bool scheduledOff)
    {
        if (_lightsWant != state.LightsOff) { _lightsWant = state.LightsOff; _lightsPending = true; _retryAt = default; }
        if (!_lightsPending || now < _retryAt) return null;
        if (state.LightsOff ? lightsOff : !scheduledOff) { _lightsPending = false; return null; } // nothing to do
        _retryAt = now.AddMinutes(1); // until LightsDone says otherwise
        return state.LightsOff ? LightsKind.Off : LightsKind.On;
    }

    public void LightsDone(bool ok) { if (ok) _lightsPending = false; }
}
