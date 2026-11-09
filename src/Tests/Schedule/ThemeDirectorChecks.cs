using System.IO;

namespace Pulsewave.Regression;

// 0.10: which theme the HUD shows for the moment. A hand pick outranks a game, a game outranks the schedule,
// the schedule outranks the playlist; a game or schedule theme is shown over the user's own without replacing it.
internal static class ThemeDirectorChecks
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("THEMES 0.10: " + message);
        Console.WriteLine("PASS THEMES 0.10: " + message);
    }

    public static void Run(string outputRoot)
    {
        // 2026-11-09 is a Monday.
        static DateTime At(int day, int hour, int minute = 0) => new(2026, 11, day, hour, minute, 0);
        ThemeDirector.Moment M(string? game = null, string? gameTheme = null, string? period = null, string? periodTheme = null) => new(game, gameTheme, period, periodTheme);

        // Games: the theme shows while the session runs and the user's own returns when it ends.
        var d = new ThemeDirector();
        string? idle = d.Decide(M());
        string? playing = d.Decide(M("steam:1", "halo-gaming"));
        string? noTheme = d.Decide(M("steam:1", ""));
        string? after = d.Decide(M());
        Check(idle == null && playing == "halo-gaming" && noTheme == null && after == null,
            "A game shows the gaming theme while its session runs; when it ends the user's own theme shows again (nothing was replaced)");

        // Per-game picks, "don't switch" and the default.
        var gaming = new GamingSettings { Theme = "halo-gaming", Themes = new(StringComparer.OrdinalIgnoreCase) { ["steam:2"] = "noir", ["steam:3"] = GamingSettings.NoTheme } };
        Check(ThemeDirector.GameTheme(gaming, "steam:1") == "halo-gaming" && ThemeDirector.GameTheme(gaming, "STEAM:2") == "noir" &&
              ThemeDirector.GameTheme(gaming, "steam:3") == null && ThemeDirector.GameTheme(gaming, null) == null &&
              ThemeDirector.GameTheme(new GamingSettings(), "steam:1") == null,
            "One default theme for every game; a game's own pick overrides it, and \"none\" means don't switch for that game");

        // Schedule below games; after a game the schedule's theme if it still wants one.
        d = new ThemeDirector();
        string? evening = d.Decide(M(period: "0@x", periodTheme: "lifestyle"));
        string? gameOverSchedule = d.Decide(M("steam:1", "halo-gaming", "0@x", "lifestyle"));
        string? backToSchedule = d.Decide(M(period: "0@x", periodTheme: "lifestyle"));
        Check(evening == "lifestyle" && gameOverSchedule == "halo-gaming" && backToSchedule == "lifestyle",
            "A game outranks the schedule; when it ends the HUD shows what the schedule wants now");

        // A hand pick holds for the game it was made in, then the next game switches again.
        d = new ThemeDirector();
        d.Decide(M("steam:1", "halo-gaming"));
        d.HandPicked(M("steam:1", "halo-gaming"));
        string? held = d.Decide(M("steam:1", "halo-gaming"));
        string? heldOverSchedule = d.Decide(M("steam:1", "halo-gaming", "0@x", "lifestyle"));
        string? gameEnded = d.Decide(M(period: "0@x", periodTheme: "lifestyle"));
        string? nextGame = d.Decide(M("steam:2", "halo-gaming"));
        Check(held == null && heldOverSchedule == null && gameEnded == "lifestyle" && nextGame == "halo-gaming",
            "A hand pick outranks the game (and a schedule period starting meanwhile) until that game ends; the next game switches again");

        // A hand pick during a period holds until that period ends; tomorrow's run is a new period.
        d = new ThemeDirector();
        d.HandPicked(M(period: "0@2026-11-09T09:00", periodTheme: "productivity"));
        string? pickHolds = d.Decide(M(period: "0@2026-11-09T09:00", periodTheme: "productivity"));
        string? tomorrow = d.Decide(M(period: "0@2026-11-10T09:00", periodTheme: "productivity"));
        Check(pickHolds == null && tomorrow == "productivity", "A hand pick holds for the schedule period it was made in; the rule's next period applies again");

        // A pick with nothing running holds nothing.
        d = new ThemeDirector();
        d.HandPicked(M());
        Check(d.Decide(M("steam:1", "halo-gaming")) == "halo-gaming", "A theme picked with no game or period running doesn't block the next game");

        // The playlist waits while a game or a schedule period with a theme is in control, held or not.
        Check(ThemeDirector.PausesPlaylist(M("steam:1", "halo-gaming")) && ThemeDirector.PausesPlaylist(M(period: "0@x", periodTheme: "a")) &&
              !ThemeDirector.PausesPlaylist(M("steam:1", "")) && !ThemeDirector.PausesPlaylist(M()),
            "Theme rotation waits while a game or a schedule theme is in control and resumes after");

        // Schedule periods: the topmost covering rule with a theme wins; rules without one don't count.
        var workday = new ScheduleRule { Off = "09:00", On = "17:00", Lights = false, Theme = "productivity" };
        var lunch = new ScheduleRule { Off = "12:00", On = "13:00", Lights = false, Theme = "lifestyle" };
        var lightsOnly = new ScheduleRule { Off = "08:00", On = "18:00" };
        var night = new ScheduleRule { Off = "22:00", On = "06:00", Lights = false, Theme = "noir" };
        var atLunch = ThemeDirector.SchedulePeriod([lightsOnly, lunch, workday], At(9, 12, 30));
        var lunchBelow = ThemeDirector.SchedulePeriod([lightsOnly, workday, lunch], At(9, 12, 30));
        var morning = ThemeDirector.SchedulePeriod([lightsOnly, lunch, workday], At(9, 10));
        var pastMidnight = ThemeDirector.SchedulePeriod([night], At(10, 2));
        var beforeMidnight = ThemeDirector.SchedulePeriod([night], At(9, 23));
        Check(atLunch is { Theme: "lifestyle", Key: "1@2026-11-09T12:00" } && lunchBelow is { Theme: "productivity" } &&
              morning is { Theme: "productivity", Key: "2@2026-11-09T09:00" } && ThemeDirector.SchedulePeriod([lightsOnly], At(9, 12)) == null &&
              pastMidnight?.Key == beforeMidnight?.Key && pastMidnight?.Key == "0@2026-11-09T22:00" && ThemeDirector.SchedulePeriod([night], At(10, 7)) == null,
            "The topmost covering rule with a theme wins (arranged in the Schedule window); a period past midnight keeps the evening it started");

        // Settings: older files have no themes; the new fields survive a save and are tidied.
        string root = Path.Combine(outputRoot, "director-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string file = Path.Combine(root, "settings.json");
        File.WriteAllText(file, "{\"ActiveTheme\":\"x\",\"Schedule\":[{\"Off\":\"23:00\",\"On\":\"07:00\"}],\"Gaming\":{\"Suggest\":false}}");
        var older = SettingsStore.LoadFile(file);
        bool none = older.Gaming.Theme == "" && older.Gaming.Themes.Count == 0 && older.Schedule[0].Theme == "" && !older.Gaming.Suggest;
        older.Gaming.Theme = " halo-gaming ";
        older.Gaming.Themes = new() { ["steam:2"] = "noir", [""] = "x", ["steam:4"] = " " };
        older.Schedule[0].Theme = "noir";
        SettingsStore.SaveFile(file, older);
        var loaded = SettingsStore.LoadFile(file);
        // The windows: the Schedule window arranges rules (topmost wins) and names a rule's theme; the Gaming window's Theme column.
        Exception? failure = null;
        string? arranged = null, summary = null, columnDefault = null, columnOwn = null, columnNone = null;
        var thread = new Thread(() =>
        {
            try
            {
                (string, string)[] themes = [("builtin:36-paper", "Paper"), ("builtin:30-halo-gaming", "Halo Gaming")];
                List<ScheduleRule>? saved = null;
                using var form = new ScheduleForm([new ScheduleRule { Off = "09:00", On = "17:00", Lights = false, Theme = "builtin:36-paper" }, new ScheduleRule { Off = "12:00", On = "13:00" }],
                    rules => { saved = rules.Select(r => r.Copy()).ToList(); return null; }, "", themes: themes);
                form.SelectRule(1);
                form.MoveRule(-1);
                arranged = saved == null ? null : string.Join(",", saved.Select(r => r.Off));
                summary = ScheduleForm.Summary(form.Rules[1], id => themes.First(t => t.Item1 == id).Item2);
                var gamingSettings = new GamingSettings { Theme = "builtin:30-halo-gaming", Themes = new(StringComparer.OrdinalIgnoreCase) { ["steam:2"] = "builtin:36-paper", ["steam:3"] = GamingSettings.NoTheme } };
                using var gamingForm = new GamingForm(gamingSettings, () => [], () => [], () => null, (_, _) => { }, _ => { }, themes);
                (columnDefault, columnOwn, columnNone) = (gamingForm.ThemeLabel("steam:1"), gamingForm.ThemeLabel("steam:2"), gamingForm.ThemeLabel("steam:3"));
            }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
        if (failure != null) throw failure;
        Check(arranged == "12:00,09:00" && summary == "Theme Paper 09:00 → 17:00, every day" && columnDefault == "Default · Halo Gaming" &&
              columnOwn == "Paper" && columnNone == "Don't switch",
            $"Schedule ▲ ▼ rearrange and save the rules; a theme-only rule reads \"{summary?.Trim()}\"; the Gaming window shows each game's theme");

        Check(none && loaded.Gaming.Theme == "halo-gaming" && loaded.Gaming.Themes.Count == 1 && loaded.Gaming.Themes["STEAM:2"] == "noir" &&
              loaded.Schedule[0].Theme == "noir" && loaded.ActiveTheme == "x",
            "Older settings load with no gaming or schedule themes; the new choices are saved, and blank entries dropped");
    }
}
