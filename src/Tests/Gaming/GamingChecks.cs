using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Pulsewave.Themes;
using Size = System.Windows.Size;

namespace Pulsewave.Regression;

// Gaming sessions: telling games from other programs, sessions starting and ending, FPS statistics,
// play time today and over the week, saving, theme values and the Gaming page (off-screen).
// Which game is running on this PC is only looked at by --game-probe.
internal static class GamingChecks
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("GAMING: " + message);
        Console.WriteLine("PASS GAMING: " + message);
    }

    public static void Run(string outputRoot, string? shots = null)
    {
        var games = new List<GameEntry>
        {
            new("steam:1", "Big Game", @"E:\SteamLibrary\steamapps\common\Big Game", "1"),
            new("steam:2", "Big Game DLC", @"E:\SteamLibrary\steamapps\common\Big Game\DLC", "2"),
        };
        Check(GameCatalog.Match(games, @"E:\SteamLibrary\steamapps\common\Big Game\Bin\game.exe")?.Key == "steam:1" &&
              GameCatalog.Match(games, @"e:\steamlibrary\steamapps\common\big game\dlc\x.exe")?.Key == "steam:2" &&
              GameCatalog.Match(games, @"E:\SteamLibrary\steamapps\common\Big Gameplay\x.exe") == null &&
              GameCatalog.Match(games, @"C:\Program Files\Discord\Discord.exe") == null,
            "A program belongs to the game whose install folder holds it (the deepest one); other programs aren't games");
        Check(GameCatalog.IsStoreGame(@"C:\Program Files\Epic Games\Fortnite\FortniteClient.exe") && !GameCatalog.IsStoreGame(@"C:\Program Files (x86)\Epic Games\Launcher\Portal\EpicGamesLauncher.exe") &&
              !GameCatalog.IsStoreGame(@"D:\Games\FFIX\FF9_Launcher.exe") && !GameCatalog.IsStoreGame(@"C:\Users\x\AppData\Local\Discord\Discord.exe") &&
              GameCatalog.NotGames.Contains("431960"),
            "Shortcuts count as games only in store game folders; launchers and Wallpaper Engine never do");

        // Beyond Steam: game folders (Unreal's layout), names, exact programs, your own lists, suggestions.
        string root = Path.Combine(outputRoot, "gaming-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string unreal = Path.Combine(root, "Clair Obscur Expedition 33");
        Directory.CreateDirectory(Path.Combine(unreal, "Engine"));
        Directory.CreateDirectory(Path.Combine(unreal, "Sandfall", "Binaries", "Win64"));
        string plain = Path.Combine(root, "Diablo III", "x64");
        Directory.CreateDirectory(plain);
        Check(GameCatalog.GameFolder(Path.Combine(unreal, "Sandfall", "Binaries", "Win64", "SandFall-Win64-Shipping.exe")) == unreal &&
              GameCatalog.GameFolder(Path.Combine(plain, "Diablo III64.exe")) == Path.Combine(root, "Diablo III") &&
              GameCatalog.GameFolder(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "game.exe")) == "" &&
              GameCatalog.NameOf(Path.Combine(unreal, "Sandfall", "Binaries", "Win64", "SandFall-Win64-Shipping.exe")) == "Clair Obscur Expedition 33" &&
              GameCatalog.Clean("Diablo III Retail") == "Diablo III" && GameCatalog.Clean("RetroArch-Win64") == "RetroArch" && GameCatalog.Clean("Stellar Blade™") == "Stellar Blade" &&
              GameCatalog.IsLauncher(@"C:\Program Files (x86)\Battle.net\Battle.net.exe") && GameCatalog.IsLauncher(@"D:\Games\FFIX\FF9_Launcher.exe"),
            "Non-Steam games stand for the folder above engine subfolders (Unreal's Binaries\\Win64 too), get readable names, and launchers aren't games");
        var exact = new List<GameEntry> { new("exe:a", "Exact", "", null, @"C:\Program Files\Odd\odd.exe", null, "Windows"), new("x", "Folder", @"C:\Program Files\Odd", null) };
        Check(GameCatalog.Match(exact, @"c:\program files\odd\ODD.exe")?.Name == "Exact" && GameCatalog.Match(exact, @"C:\Program Files\Odd\other.exe")?.Name == "Folder",
            "A game found by its exact program wins over a folder match");
        string mine = Path.Combine(root, "Mine", "mine.exe"), never = Path.Combine(root, "Never", "never.exe");
        var gaming = new GamingSettings { Games = [new UserGame { Name = "Mine", Exe = mine }, new UserGame { Name = "Never", Exe = never }], Ignored = [never.ToUpperInvariant()] };
        gaming.Normalize();
        var built = GameCatalog.Build([], gaming);
        Check(built.Any(g => g.Source == "Added" && g.Name == "Mine") && !built.Any(g => g.Name == "Never") && gaming.Ignored.Single() == never.ToLowerInvariant(),
            "Games you add are recognised by their program; programs marked never are left out");
        var suggester = new GameSuggester();
        string? first = null;
        int checks = 0;
        for (; checks < 30 && first == null; checks++) first = suggester.Check(@"D:\Emu\emu.exe", 80, TimeSpan.FromSeconds(5), _ => false);
        Check(first == @"D:\Emu\emu.exe" && checks == 24 && suggester.Check(@"D:\Emu\emu.exe", 80, TimeSpan.FromSeconds(5), _ => false) == null &&
              new GameSuggester().Check(@"D:\x.exe", 30, TimeSpan.FromMinutes(5), _ => false) == null && new GameSuggester().Check(@"D:\x.exe", 90, TimeSpan.FromMinutes(5), _ => true) == null,
            "An unknown full-screen program with the GPU at half or more for 2 minutes is suggested once; light use and known games aren't");
        string settingsFile = Path.Combine(root, "settings.json");
        File.WriteAllText(settingsFile, "{\"ActiveTheme\":\"x\"}");
        var older = SettingsStore.LoadFile(settingsFile);
        bool noneYet = older.Gaming.Games.Count == 0 && older.Gaming.Suggest;
        older.Gaming.Games.Add(new UserGame { Name = "", Exe = mine });
        older.Gaming.Ignored.Add(never);
        SettingsStore.SaveFile(settingsFile, older);
        var again = SettingsStore.LoadFile(settingsFile).Gaming;
        Check(noneYet && again.Games.Single() is { Name: "mine" } && again.Ignored.Single() == never.ToLowerInvariant(),
            "Older settings start with no added games and suggestions on; added and never-count programs are saved");

        // A session: starts when seen, collects FPS and peaks, ends 30 s after the game was last seen.
        string file = Path.Combine(root, "gaming.json");
        var tracker = new GameTracker(file);
        var t0 = new DateTime(2026, 11, 9, 23, 30, 0);
        GameSession? ended = null;
        for (int s = 0; s < 3600; s++)
            ended ??= tracker.Update(t0.AddSeconds(s), "steam:1", "Big Game", s % 100 == 0 ? 40 : 120, 60 + s % 20, 70);
        var live = tracker.Snapshot(t0.AddSeconds(3599));
        var stillPlaying = tracker.Update(t0.AddSeconds(3620), null, null, null, null, null);
        var finished = tracker.Update(t0.AddSeconds(3631), null, null, null, null, null);
        Check(ended == null && live.Playing && live.Game == "Big Game" && live.Session == TimeSpan.FromSeconds(3599) && live.AvgFps == 119 && live.LowFps == 40 &&
              live.PeakCpuC == 79 && live.PeakGpuC == 70 && stillPlaying == null && finished is { Name: "Big Game", AvgFps: 119 } && finished.Length == TimeSpan.FromSeconds(3599),
            "A session runs while the game is seen, averages its FPS (1% low = the slowest 1% of seconds), keeps peak temperatures, and ends 30 s after the game is gone");

        // Play time is split at midnight: 30 min yesterday-evening, ~30 min today.
        var after = tracker.Snapshot(new DateTime(2026, 11, 10, 9, 0, 0));
        Check(!after.Playing && after.Game == "Big Game" && after.Today == TimeSpan.FromSeconds(1799) && after.Week == TimeSpan.FromSeconds(3599) &&
              after.WeekByGame.Single().Name == "Big Game" && after.Recent.Count == 1,
            "Play time today counts only today's part of a session that crossed midnight; the week counts all of it, per game");

        var shortOne = new GameTracker(null);
        shortOne.Update(t0, "steam:2", "Short", 60, null, null);
        var tooShort = shortOne.Update(t0.AddSeconds(40), "steam:1", "Next", 60, null, null);
        Check(tooShort == null && shortOne.History.Count == 0 && shortOne.CurrentGame == "Next",
            "Sessions under a minute aren't kept, and switching games ends one session and starts the next");

        var reloaded = new GameTracker(file);
        File.WriteAllText(Path.Combine(root, "broken.json"), "{ nope");
        Check(reloaded.History.Count == 1 && reloaded.History[0] is { Name: "Big Game", LowFps: 40 } && new GameTracker(Path.Combine(root, "broken.json")).History.Count == 0 &&
              GameTracker.Low(Enumerable.Repeat(100.0, 59).ToList()) == null,
            "Sessions are saved and read back; a damaged file starts empty; 1% low needs a minute of samples");

        // Theme values.
        var c = new ThemeDataContext { Gaming = live };
        string V(string n) => ThemeData.Display(ThemeData.Read(n, null, false, c), true);
        var none = new ThemeDataContext { Gaming = new GameTracker(null).Snapshot(t0) };
        Check(V("GAME_NAME") == "Big Game" && V("GAME_STATE") == "PLAYING" && V("GAME_SESSION") == "59:59" && V("GAME_FPS_AVG") == "119FPS" && V("GAME_FPS_LOW") == "40FPS" &&
              V("GAME_PEAK_GPU") == "70°C" && V("GAME_TODAY") == "29m" && V("GAME_WEEK") == "59m" &&
              ThemeData.Display(ThemeData.Read("GAME_STATE", null, false, none), true) == "NO GAME YET" && ThemeData.PlayTime(new TimeSpan(2, 5, 0)) == "2h 05m" &&
              c.Shortcut(ThemeDataContext.GameSlot) == null,
            "Themes show the game, its state, session, FPS, peak temperatures and play time; slot -1 is the game's tile");

        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                // The tray window lists games by source, your lists and suggestions.
                var form = new GamingForm(gaming, () => exact, () => [@"D:\Emu\emu.exe"], () => null, (_, _) => { }, _ => { });
                var lists = AllControls(form).ToList();
                int rows = lists.OfType<System.Windows.Forms.ListView>().Single().Items.Count;
                var boxes = lists.OfType<System.Windows.Forms.ListBox>().Select(b => b.Items.Count).ToList();
                form.Dispose();
                Check(rows == 2 && boxes.SequenceEqual([2, 1, 1]), "Tray → Gaming lists the recognised games, games you added, never-count programs and suggestions");

                var sample = ThemeSampleData.Uniform().Gaming!;
                foreach (var (w, h) in new[] { (1920, 480), (480, 1802) })
                {
                    var view = new GamingView(new Size(w, h));
                    view.Open();
                    view.Show(sample);
                    view.Measure(new Size(w, h)); view.Arrange(new Rect(0, 0, w, h)); view.UpdateLayout();
                    view.Show(sample);
                    view.UpdateLayout();
                    var texts = Visuals<System.Windows.Controls.TextBlock>(view).Where(b => b.IsVisible || b.Visibility == Visibility.Visible).ToList();
                    bool fits = texts.All(b => b.TransformToAncestor(view).TransformBounds(new Rect(0, 0, b.ActualWidth, b.ActualHeight)) is var r && r.Right <= w + 0.5 && r.Bottom <= h + 0.5);
                    bool shows = texts.Any(b => b.Text == "1:24:10") && texts.Any(b => b.Text == "2h 05m") && texts.Any(b => b.Text == "FINAL FANTASY VII REBIRTH") && texts.Any(b => b.Text == "Stellar Blade");
                    if (shots != null)
                    {
                        var bitmap = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
                        bitmap.Render(view);
                        var png = new PngBitmapEncoder();
                        png.Frames.Add(BitmapFrame.Create(bitmap));
                        Directory.CreateDirectory(shots);
                        using var output = File.Create(Path.Combine(shots, $"gaming-{w}x{h}.png"));
                        png.Save(output);
                    }
                    view.Close();
                    Check(fits && shows, $"The {w}x{h} Gaming page shows the session, play time, per-game time and recent sessions within the screen");
                }
            }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
        if (failure != null) throw failure;
    }

    private static IEnumerable<System.Windows.Forms.Control> AllControls(System.Windows.Forms.Control parent) =>
        parent.Controls.Cast<System.Windows.Forms.Control>().SelectMany(c => AllControls(c).Prepend(c));

    private static IEnumerable<T> Visuals<T>(DependencyObject node) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
        {
            var child = VisualTreeHelper.GetChild(node, i);
            if (child is T t) yield return t;
            foreach (var inner in Visuals<T>(child)) yield return inner;
        }
    }
}
