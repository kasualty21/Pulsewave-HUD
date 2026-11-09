using System.IO;

namespace Pulsewave.Regression;

// Aesthetic mode: how visible the HUD is, the first tap only waking it, the wake time restarting
// on each tap, and its saved settings. The fading itself is drawn by the running app.
internal static class AestheticChecks
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("AESTHETIC: " + message);
        Console.WriteLine("PASS AESTHETIC: " + message);
    }

    public static void Run(string outputRoot)
    {
        var at = new DateTime(2026, 11, 9, 20, 0, 0);
        var settings = new MonitorSettings { Aesthetic = true, AestheticStyle = "fade", AestheticFadePercent = 15, AestheticWakeSeconds = 60 };
        var mode = new AestheticMode();
        double faded = mode.Level(settings, at);
        bool firstWakesOnly = mode.Tap(settings, at);
        double awake = mode.Level(settings, at.AddSeconds(30));
        bool secondIsNormal = !mode.Tap(settings, at.AddSeconds(50)); // restarts the minute
        double stillAwake = mode.Level(settings, at.AddSeconds(100));
        double fadedAgain = mode.Level(settings, at.AddSeconds(111));
        Check(faded == 0.15 && firstWakesOnly && awake == 1 && secondIsNormal && stillAwake == 1 && fadedAgain == 0.15,
            "The HUD fades; the first tap only wakes it, later taps work and keep it awake for the wake time");

        settings.AestheticStyle = "hide";
        bool hidden = mode.Level(settings, at.AddMinutes(10)) == 0;
        settings.Aesthetic = false;
        bool offShown = mode.Level(settings, at.AddMinutes(10)) == 1 && !mode.Tap(settings, at.AddMinutes(10));
        Check(hidden && offShown && Math.Round(255 * AestheticMode.TapCatcher) >= 2,
            "Hide shows nothing but still takes taps; with the mode off every tap works and the HUD stays shown");

        string root = Path.Combine(outputRoot, "aesthetic-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string file = Path.Combine(root, "settings.json");
        File.WriteAllText(file, "{\"ActiveTheme\":\"x\"}");
        var older = SettingsStore.LoadFile(file);
        bool defaults = older is { Aesthetic: false, AestheticStyle: "fade", AestheticFadePercent: 15, AestheticWakeSeconds: 60, HotkeyAesthetic: "" };
        older.AestheticStyle = "blur"; older.AestheticFadePercent = 0; older.AestheticWakeSeconds = 1; older.Aesthetic = true;
        SettingsStore.SaveFile(file, older);
        var loaded = SettingsStore.LoadFile(file);
        Check(defaults && loaded is { Aesthetic: true, AestheticStyle: "fade", AestheticFadePercent: 5, AestheticWakeSeconds: 10 },
            "Older settings start with aesthetic mode off (fade to 15%, wake for a minute); bad values are repaired");
    }
}
