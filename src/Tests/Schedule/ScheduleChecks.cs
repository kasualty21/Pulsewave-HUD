using System.IO;

namespace Pulsewave.Regression;

// The lights and screen schedule: which times a rule covers (same day, overnight, chosen days),
// acting only at the edges, leaving hand-switched lights alone, retries, and saved settings.
internal static class ScheduleChecks
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("SCHEDULE: " + message);
        Console.WriteLine("PASS SCHEDULE: " + message);
    }

    public static void Run(string outputRoot)
    {
        // 2026-11-09 is a Monday.
        static DateTime At(int day, int hour, int minute = 0) => new(2026, 11, day, hour, minute, 0);
        var night = new ScheduleRule { Off = "23:00", On = "07:00" };
        var weekdayNight = new ScheduleRule { Off = "23:00", On = "07:00", Days = 0b0111110 }; // starts Mon-Fri
        var lunch = new ScheduleRule { Off = "12:00", On = "13:00", Lights = false, Screen = true };
        Check(night.Covers(At(9, 23)) && night.Covers(At(10, 6, 59)) && !night.Covers(At(10, 7)) && !night.Covers(At(9, 22, 59)) &&
              lunch.Covers(At(9, 12, 30)) && !lunch.Covers(At(9, 13)),
            "A rule covers Off up to On, overnight when Off is later than On");
        // Friday 23:00 starts a weekday night that ends Saturday 07:00; Saturday 23:00 starts none; Sunday night doesn't either.
        Check(weekdayNight.Covers(At(13, 23, 30)) && weekdayNight.Covers(At(14, 6)) && !weekdayNight.Covers(At(14, 23, 30)) &&
              !weekdayNight.Covers(At(15, 6)) && !weekdayNight.Covers(At(15, 23, 30)) && weekdayNight.Covers(At(16, 6)) == false &&
              weekdayNight.Covers(At(17, 2)),
            "Days are the days an off period starts on; its morning end follows the night before");
        Check(!new ScheduleRule { Enabled = false }.Covers(At(9, 23, 30)) && !new ScheduleRule { Off = "08:00", On = "08:00" }.Covers(At(9, 8)) &&
              !new ScheduleRule { Days = 0 }.Covers(At(9, 23, 30)),
            "Switched-off rules, equal times and rules with no days cover nothing");
        var both = Scheduler.Wanted([night, lunch], At(9, 12, 15));
        Check(both == new ScheduleState(false, true) && Scheduler.Wanted([night, lunch], At(9, 23, 15)) == new ScheduleState(true, false),
            "Each rule turns off only what it is for (LEDs, screen)");

        // Edges: off when a period starts, on afterwards only if the schedule turned them off.
        var s = new Scheduler();
        var off = new ScheduleState(true, false);
        var on = new ScheduleState(false, false);
        var first = s.LightsCommand(off, At(9, 23), lightsOff: false, scheduledOff: false);
        var whileWaiting = s.LightsCommand(off, At(9, 23, 0), false, false);
        s.LightsDone(false); // OpenRGB not running
        var tooSoon = s.LightsCommand(off, At(9, 23, 0).AddSeconds(30), false, false);
        var retry = s.LightsCommand(off, At(9, 23, 1), false, false);
        s.LightsDone(true);
        var settled = s.LightsCommand(off, At(9, 23, 5), true, true);
        var morning = s.LightsCommand(on, At(10, 7), lightsOff: true, scheduledOff: true);
        s.LightsDone(true);
        Check(first == LightsKind.Off && whileWaiting == null && tooSoon == null && retry == LightsKind.Off && settled == null && morning == LightsKind.On,
            "LEDs go off when a period starts (retried a minute later if OpenRGB didn't answer) and back on when it ends");

        var hand = new Scheduler();
        hand.LightsCommand(on, At(9, 20), lightsOff: false, scheduledOff: false);
        var manualDuringDay = hand.LightsCommand(on, At(9, 21), lightsOff: true, scheduledOff: false);
        var alreadyOff = hand.LightsCommand(off, At(9, 23), lightsOff: true, scheduledOff: false);
        var endAfterHand = hand.LightsCommand(on, At(10, 7), lightsOff: true, scheduledOff: false);
        var nightHand = new Scheduler();
        nightHand.LightsCommand(off, At(9, 23), false, false); nightHand.LightsDone(true);
        var turnedOnByHand = nightHand.LightsCommand(off, At(10, 1), lightsOff: false, scheduledOff: true);
        Check(manualDuringDay == null && alreadyOff == null && endAfterHand == null && turnedOnByHand == null,
            "Lights switched by hand are left alone: no command until the next edge, and no turning on lights the schedule didn't turn off");

        var startup = new Scheduler();
        var startScreenOn = startup.ScreenChange(new ScheduleState(false, false));
        var dusk = startup.ScreenChange(new ScheduleState(false, true));
        var still = startup.ScreenChange(new ScheduleState(false, true));
        var dawn = startup.ScreenChange(new ScheduleState(false, false));
        var startDark = new Scheduler().ScreenChange(new ScheduleState(false, true));
        Check(startScreenOn == false && dusk == true && still == null && dawn == false && startDark == true,
            "The screen blanks when a period starts (or at start-up inside one) and shows again when it ends");
        var edited = new Scheduler();
        edited.ScreenChange(new ScheduleState(false, true));
        edited.Reset(); // the rule was changed so that now is outside it
        Check(edited.ScreenChange(new ScheduleState(false, false)) == false,
            "After a schedule edit, a screen the old rules blanked comes back when the new ones say it should be on");

        // Saved settings: older files get none; bad values are repaired.
        string root = Path.Combine(outputRoot, "schedule-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string file = Path.Combine(root, "settings.json");
        File.WriteAllText(file, "{\"ActiveTheme\":\"x\"}");
        var older = SettingsStore.LoadFile(file);
        bool none = older.Schedule.Count == 0 && !older.Lights.ScheduledOff && older.ScreenPowerOff;
        older.Schedule = [new ScheduleRule { Off = "25:99", On = "7:05", Days = 1000, Screen = true }, .. Enumerable.Range(0, 20).Select(_ => new ScheduleRule())];
        SettingsStore.SaveFile(file, older);
        var loaded = SettingsStore.LoadFile(file);
        Check(none && loaded.Schedule.Count == 16 && loaded.Schedule[0] is { Off: "23:00", On: "07:05", Days: 1000 & 127, Screen: true, Lights: true },
            "Schedules are saved; older settings have none, and bad times and days are repaired");
        Check(ScheduleForm.Summary(new ScheduleRule { Screen = true }) == "LEDs and screen off 23:00 → on 07:00, every day" &&
              ScheduleForm.Summary(new ScheduleRule { Lights = false, Screen = true, Days = 0b0111110, Enabled = false }).StartsWith("(off) Screen off 23:00 → on 07:00, weekdays"),
            "The schedule list says what each rule does in words");

        // Crash safety: what the schedule changed on the display is recorded and undone on the next start.
        string restoreFile = Path.Combine(root, "display-restore.json");
        DisplayRestore.Save(restoreFile, new DisplayRestore { BacklightDevice = @"\\.\DISPLAY2", Brightness = 70, StandbyDevice = @"\\.\DISPLAY2" });
        var saved = DisplayRestore.Load(restoreFile);
        var calls = new List<string>();
        string? note = DisplayRestore.Undo(saved, [@"\\.\DISPLAY1", @"\\.\DISPLAY2"],
            (d, b) => { calls.Add($"{d} {b}"); return true; }, d => { calls.Add("on " + d); return true; });
        string? elsewhere = DisplayRestore.Undo(saved, [@"\\.\DISPLAY1"], (_, _) => throw new Exception("wrong display"), _ => throw new Exception("wrong display"));
        DisplayRestore.Save(restoreFile, new DisplayRestore());
        File.WriteAllText(Path.Combine(root, "broken.json"), "{not json");
        Check(saved is { Brightness: 70 } && calls.SequenceEqual([@"on \\.\DISPLAY2", @"\\.\DISPLAY2 70"]) && note!.Contains("brightness put back to 70") &&
              elsewhere == null && !File.Exists(restoreFile) && DisplayRestore.Load(restoreFile) == null &&
              DisplayRestore.Load(Path.Combine(root, "broken.json")) == null && DisplayRestore.Undo(null, [], (_, _) => true, _ => true) == null,
            "A lowered backlight and a display in standby are recorded while in effect and undone on the next start after a crash (connected displays only); the record goes when nothing is left");

        // Night dimming: the first covering rule that dims decides; the change is reported once at each edge.
        var dimRule = new ScheduleRule { Lights = false, Dim = true, DimMode = "backlight", DimLevel = 5, Off = "22:00", On = "06:30" };
        dimRule.Normalize();
        var dimRules = new List<ScheduleRule> { dimRule };
        var dimSched = new Scheduler();
        var late = new DateTime(2026, 3, 4, 23, 0, 0);
        var dimState = Scheduler.Wanted(dimRules, late);
        bool dimFirst = dimSched.DimChange(dimState, out var dimNow) && dimNow == ("Backlight", 10);
        bool quiet = !dimSched.DimChange(Scheduler.Wanted(dimRules, late.AddMinutes(1)), out _);
        bool ends = dimSched.DimChange(Scheduler.Wanted(dimRules, late.AddHours(8)), out var after) && after == null;
        var olderRule = System.Text.Json.JsonSerializer.Deserialize<ScheduleRule>("{\"Off\":\"23:00\",\"On\":\"07:00\",\"Screen\":true}")!;
        Check(dimRule.DimMode == "Backlight" && dimRule.DimLevel == 10 && dimFirst && quiet && ends && !olderRule.Dim && olderRule.DimMode == "Theme" &&
              ScheduleForm.Summary(dimRule) == "Dim 10% 22:00 → 06:30, every day" && ScheduleForm.Summary(new ScheduleRule { Dim = true }).Contains("LEDs off 23:00 → on 07:00, dim 40%"),
            "Night dimming: a rule dims by theme, backlight or both at a 10–90% level, reports once at each edge, and older rules don't dim");

        // The tray icon's double-click: older settings keep "show the HUD"; unknown or out-of-range choices fall back to it.
        var trayFile = Path.Combine(root, "tray-settings.json");
        File.WriteAllText(trayFile, "{\"ActiveTheme\":\"\"}");
        var traySettings = SettingsStore.LoadFile(trayFile);
        bool olderDefault = traySettings.TrayDoubleClick == "restore";
        traySettings.TrayDoubleClick = "theme:builtin:36-paper";
        SettingsStore.SaveFile(trayFile, traySettings);
        bool kept = SettingsStore.LoadFile(trayFile).TrayDoubleClick == "theme:builtin:36-paper";
        Check(olderDefault && kept && TrayActions.Normalize("nextTheme") == "nextTheme" && TrayActions.Normalize("launch:3") == "launch:3" &&
              TrayActions.Normalize("launch:999") == "restore" && TrayActions.Normalize("explode") == "restore" && TrayActions.Normalize(null) == "restore" &&
              TrayActions.Describe("launch:2", _ => null, s => s == 2 ? "Steam" : null) == "Open Steam" &&
              TrayActions.Describe("theme:x", id => id == "x" ? "Paper" : null, _ => null) == "Show Paper" && TrayActions.Describe("rotate", _ => null, _ => null) == "Theme rotation on / off",
            "The tray icon's double-click can show a theme, open a shortcut, step or rotate themes and more; older settings keep showing the HUD");
    }
}