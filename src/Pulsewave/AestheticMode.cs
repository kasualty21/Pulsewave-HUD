namespace Pulsewave;

/// <summary>
/// Aesthetic mode: while on, the HUD fades (or hides) to show the wallpaper or screensaver under it.
/// The first tap wakes it, and it stays fully shown for the wake time after the last tap.
/// </summary>
internal sealed class AestheticMode
{
    /// <summary>The lowest visibility of the HUD's background while hidden: invisible, but enough for Windows
    /// to keep sending taps and clicks to the window (fully transparent pixels let them through).</summary>
    internal const double TapCatcher = 0.01;

    private DateTime _awakeUntil;

    /// <summary>How visible the HUD is now: 1 shown, the fade level, or 0 hidden.</summary>
    public static double Level(MonitorSettings settings, bool awake) =>
        !settings.Aesthetic || awake ? 1 : settings.AestheticStyle == "hide" ? 0 : Math.Clamp(settings.AestheticFadePercent, 5, 90) / 100.0;

    public bool Awake(DateTime now) => now < _awakeUntil;

    /// <summary>The HUD's visibility at <paramref name="now"/>.</summary>
    public double Level(MonitorSettings settings, DateTime now) => Level(settings, Awake(now));

    /// <summary>
    /// A tap at <paramref name="now"/>. Returns true when it only woke the HUD (so it does nothing else);
    /// while awake, a tap is used normally and starts the wake time again.
    /// </summary>
    public bool Tap(MonitorSettings settings, DateTime now)
    {
        if (!settings.Aesthetic) return false;
        bool wakes = !Awake(now);
        _awakeUntil = now.AddSeconds(Math.Clamp(settings.AestheticWakeSeconds, 10, 3600));
        return wakes;
    }

    /// <summary>Turning the mode on or off starts it faded (or shown) straight away.</summary>
    public void Reset() => _awakeUntil = default;
}
