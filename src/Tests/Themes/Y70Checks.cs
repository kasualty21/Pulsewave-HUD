using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Pulsewave.Themes;
using Point = System.Windows.Point;
using Size = System.Windows.Size;

namespace Pulsewave.Regression;

// Ideas taken from HYTE Nexus for the Y70: rotation, swipes, keeping the display clear, Steam import,
// volume and key actions, timers and see-through themes. Fixtures only: no keys are sent, nothing is
// locked or put to sleep, no window is moved and Steam's real files are not read.
internal static class Y70Checks
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Y70: " + message);
        Console.WriteLine("PASS Y70: " + message);
    }

    public static void Run(string outputRoot)
    {
        string root = Path.Combine(outputRoot, "y70-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        Check(MainWindow.Rotated(new System.Drawing.Size(2560, 682), 90) == new System.Drawing.Size(682, 2560) &&
              MainWindow.Rotated(new System.Drawing.Size(2560, 682), 180) == new System.Drawing.Size(2560, 682),
            "Rotating the HUD 90° makes a Y70 that Windows shows as 2560x682 a 682x2560 HUD for vertical themes");
        var start = new Point(400, 300);
        Check(MainWindow.Swipe(start, new Point(150, 320), TimeSpan.FromMilliseconds(300), 682) == +1 &&
              MainWindow.Swipe(start, new Point(650, 280), TimeSpan.FromMilliseconds(300), 682) == -1 &&
              MainWindow.Swipe(start, new Point(410, 305), TimeSpan.FromMilliseconds(100), 682) == 0 &&
              MainWindow.Swipe(start, new Point(150, 600), TimeSpan.FromMilliseconds(300), 682) == 0 &&
              MainWindow.Swipe(start, new Point(150, 300), TimeSpan.FromSeconds(2), 682) == 0 &&
              MainWindow.Swipe(new Point(100, 0), new Point(250, 0), TimeSpan.Zero, 1920) == 0,
            "A quick sideways swipe goes to the next (left) or previous (right) theme; taps, vertical drags and slow drags do not");

        var work = new System.Drawing.Rectangle(0, 0, 2560, 1392);
        Check(DisplayGuard.Destination(new System.Drawing.Rectangle(2600, 100, 800, 600), work) == new System.Drawing.Rectangle(880, 396, 800, 600) &&
              DisplayGuard.Destination(new System.Drawing.Rectangle(2600, 0, 682, 2560), work) == new System.Drawing.Rectangle(939, 0, 682, 1392),
            "Windows moved off the HUD's display are centred on the main display and shrunk to fit");

        // Steam: two libraries, a tool and a game whose crash handler is bigger than the game itself.
        string steam = Path.Combine(root, "Steam"), second = Path.Combine(root, "Games Drive");
        Directory.CreateDirectory(Path.Combine(steam, "steamapps"));
        Directory.CreateDirectory(Path.Combine(second, "steamapps", "common", "Fights in Tight Spaces"));
        File.WriteAllText(Path.Combine(steam, "steamapps", "libraryfolders.vdf"),
            $"\"libraryfolders\"\n{{\n\t\"0\"\n\t{{\n\t\t\"path\"\t\t\"{steam.Replace(@"\", @"\\")}\"\n\t}}\n\t\"1\"\n\t{{\n\t\t\"path\"\t\t\"{second.Replace(@"\", @"\\")}\"\n\t}}\n}}");
        File.WriteAllText(Path.Combine(steam, "steamapps", "appmanifest_228980.acf"), "\"AppState\"\n{\n\t\"appid\"\t\t\"228980\"\n\t\"name\"\t\t\"Steamworks Common Redistributables\"\n}");
        File.WriteAllText(Path.Combine(second, "steamapps", "appmanifest_1265820.acf"),
            "\"AppState\"\n{\n\t\"appid\"\t\t\"1265820\"\n\t\"name\"\t\t\"Fights in Tight Spaces\"\n\t\"installdir\"\t\t\"Fights in Tight Spaces\"\n}");
        string game = Path.Combine(second, "steamapps", "common", "Fights in Tight Spaces");
        File.WriteAllBytes(Path.Combine(game, "FightsInTightSpaces.exe"), new byte[100]);
        File.WriteAllBytes(Path.Combine(game, "UnityCrashHandler64.exe"), new byte[1000]);
        var games = SteamLibrary.Installed(steam);
        Check(games.Count == 1 && games[0] is { AppId: "1265820", Name: "Fights in Tight Spaces" } &&
              Path.GetFileName(games[0].Icon) == "FightsInTightSpaces.exe" && games[0].ToShortcut().Target == "steam://rungameid/1265820" &&
              SteamLibrary.Installed(Path.Combine(root, "no-steam")).Count == 0,
            "Steam games are found in every library (tools left out), launch through Steam and take the game's own icon, not its crash handler's");

        // Keys and power actions.
        Check(KeySender.TryParse("Ctrl+Shift+M", out var mute) && mute.Modifiers == (HotkeyModifiers.Ctrl | HotkeyModifiers.Shift) &&
              KeySender.TryParse("F9", out _) && KeySender.TryParse("MediaPlayPause", out _) && KeySender.TryParse("Shift+M", out _) &&
              !KeySender.TryParse("Ctrl+Shift", out _) && !Hotkey.TryParse("F9", out _),
            "Key shortcuts accept any key with or without modifiers (global hotkeys still need Ctrl, Alt or Win)");
        string settingsFile = Path.Combine(root, "settings.json");
        File.WriteAllText(settingsFile, "{\"Shortcuts\":[{\"Name\":\"Mute\",\"Kind\":\"Keys\",\"Target\":\"Ctrl+Shift+M\"},{\"Kind\":\"Bogus\",\"Target\":\"x.exe\"}]," +
                                        "\"HudRotation\":45,\"KeepDisplayClear\":true}");
        var settings = SettingsStore.LoadFile(settingsFile);
        Check(settings.Shortcuts[0].Kind == AppShortcut.KeysKind && settings.Shortcuts[1].Kind == "" && settings.HudRotation == 0 && settings.KeepDisplayClear,
            "Shortcut kinds, rotation and keep-clear are saved; unknown kinds and angles are corrected");
        Check(ShortcutView.From(settings.Shortcuts[0]) is { Glyph: "\uE765", Name: "Mute" } &&
              ShortcutView.From(new AppShortcut { Kind = AppShortcut.SystemKind, Target = "Sleep" }) is { Glyph: "\uE708", Name: "Sleep PC" } &&
              ShortcutLauncher.Launch(new AppShortcut { Kind = AppShortcut.KeysKind, Target = "Ctrl+" }) != null &&
              ShortcutLauncher.Launch(new AppShortcut { Kind = AppShortcut.SystemKind, Target = "Reboot" }) != null,
            "Key, lock and sleep shortcuts get their own icons, and ones that cannot run are refused");

        // Timer and stopwatch with a fixed clock.
        var timers = new TouchTimers();
        var t0 = new DateTime(2026, 11, 7, 12, 0, 0);
        timers.ToggleTimer(t0);
        timers.ToggleStopwatch(t0);
        bool earlyFinish = timers.Finished(t0.AddMinutes(2));
        timers.ToggleTimer(t0.AddMinutes(2)); // pause with 3:00 left
        timers.AddMinute();
        var paused = timers.TimerLeft(t0.AddMinutes(10));
        timers.ToggleTimer(t0.AddMinutes(10));
        bool finished = timers.Finished(t0.AddMinutes(14)), again = timers.Finished(t0.AddMinutes(15));
        Check(!earlyFinish && paused == TimeSpan.FromMinutes(4) && finished && !again && timers.TimerLeft(t0.AddMinutes(15)) == TimeSpan.Zero &&
              timers.StopwatchElapsed(t0.AddSeconds(754)) == new TimeSpan(0, 12, 34) &&
              TouchTimers.Format(TimeSpan.FromSeconds(299.4)) == "5:00" && TouchTimers.Format(new TimeSpan(1, 2, 3)) == "1:02:03",
            "The timer pauses, gains minutes and reports finishing once; the stopwatch counts up");
        timers.ToggleTimer(t0.AddMinutes(16));
        Check(timers.TimerRunning && timers.TimerLeft(t0.AddMinutes(16)) == TimeSpan.FromMinutes(6), "Starting a finished timer starts it over");

        // Data and taps.
        var sample = ThemeSampleData.Uniform();
        var muted = new ThemeDataContext { Volume = (40, true) };
        Check(ThemeData.Read("Volume", null, false, sample).Number == 64 && ThemeData.Read("VOLUME", null, false, muted).Text == "Muted" &&
              ThemeData.Read("VOLUME_ICON", null, false, muted).Text == ThemeData.MutedGlyph && ThemeData.Read("TIMER", null, false, sample).Text == "5:00" &&
              ThemeData.Read("STOPWATCH", null, false, sample).Text == "12:34" && !ThemeData.Unsupported.Contains("Volume"),
            "Volume (including L-Connect's Volume value), mute icon, timer and stopwatch values");
        ThemeAction Tap(string data) => ThemeActions.Effective(new ThemeElement { Kind = ThemeElementKind.Data, DataName = data });
        Check(Tap("VOLUME") == ThemeAction.Mute && Tap("VOLUME_ICON") == ThemeAction.Mute && Tap("TIMER") == ThemeAction.TimerToggle &&
              Tap("STOPWATCH") == ThemeAction.StopwatchToggle,
            "Tapping volume mutes, tapping the timer or stopwatch starts or pauses it");

        // Touch without taking the cursor: decoding, the cursor safety net and the replayed tap.
        var (primary, screen) = TouchInput.Decode(new IntPtr(0x2000_0001 | 0x0004_0000), new IntPtr(unchecked((int)0xFF38_FC18)));
        var (secondary, _) = TouchInput.Decode(new IntPtr(0x0004_0002), IntPtr.Zero);
        Check(primary && !secondary && screen == new System.Drawing.Point(-1000, -200) &&
              TouchInput.IsContact(TouchInput.WmPointerDown) && TouchInput.IsContact(TouchInput.WmPointerUp) && !TouchInput.IsContact(0x0201),
            "Touch messages give the first finger and its screen position, including displays left of or above the main one");
        var hud = new System.Drawing.Rectangle(2560, 0, 682, 2560);
        Check(TouchInput.ShouldRestore(new System.Drawing.Point(1280, 700), new System.Drawing.Point(2900, 900), hud) &&
              !TouchInput.ShouldRestore(new System.Drawing.Point(2700, 100), new System.Drawing.Point(2900, 900), hud) &&
              !TouchInput.ShouldRestore(new System.Drawing.Point(1280, 700), new System.Drawing.Point(1300, 700), hud),
            "The cursor is put back only when a touch pulled it onto the HUD from elsewhere");
        Exception? touchFailure = null;
        var touchThread = new Thread(() =>
        {
            try
            {
                var tile = new System.Windows.Controls.Border { Width = 100, Height = 100, Background = System.Windows.Media.Brushes.Teal,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Left, VerticalAlignment = System.Windows.VerticalAlignment.Top };
                var grid = new System.Windows.Controls.Grid { Width = 400, Height = 200, Background = System.Windows.Media.Brushes.Transparent };
                grid.Children.Add(tile);
                grid.Measure(new Size(400, 200)); grid.Arrange(new Rect(0, 0, 400, 200)); grid.UpdateLayout();
                object? tileSource = null, rootSource = null;
                int tileTaps = 0, rootTaps = 0;
                tile.MouseLeftButtonUp += (_, e) => { tileTaps++; tileSource = e.OriginalSource; e.Handled = true; };
                grid.MouseLeftButtonUp += (_, e) => { rootTaps++; rootSource = e.OriginalSource; };
                TouchInput.Tap(grid, new Point(50, 50));
                TouchInput.Tap(grid, new Point(300, 150));
                Check(tileTaps == 1 && tileSource == tile && rootTaps == 1 && rootSource == grid,
                    "A lifted touch is replayed once as a click on what is under the finger");
                // A closed menu (collapsed, its layout left behind) must not take the tap.
                tile.Visibility = Visibility.Collapsed;
                grid.UpdateLayout();
                TouchInput.Tap(grid, new Point(50, 50));
                var hidden = new System.Windows.Controls.Border { Width = 100, Height = 100, Background = System.Windows.Media.Brushes.Teal, IsHitTestVisible = false,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Right, VerticalAlignment = System.Windows.VerticalAlignment.Top };
                int hiddenTaps = 0;
                hidden.MouseLeftButtonUp += (_, _) => hiddenTaps++;
                grid.Children.Add(hidden);
                grid.UpdateLayout();
                TouchInput.Tap(grid, new Point(350, 50));
                Check(tileTaps == 1 && hiddenTaps == 0 && rootTaps == 3,
                    "A touch after a menu closes goes to the HUD, not to the closed menu's tiles");
            }
            catch (Exception ex) { touchFailure = ex; }
        });
        touchThread.SetApartmentState(ApartmentState.STA); touchThread.Start(); touchThread.Join();
        if (touchFailure != null) throw touchFailure;

        // See-through themes.
        Check(Themes.Editor.ThemeEditorWindow.SeeThrough("#FF0B1521", true) == "#000B1521" && Themes.Editor.ThemeEditorWindow.SeeThrough("#000B1521", false) == "#FF0B1521",
            "See-through keeps the background color and only turns its opacity off");
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var theme = new ThemeDefinition { Width = 400, Height = 200, BackgroundColor = "#000B1521",
                    Elements = [new ThemeElement { Kind = ThemeElementKind.Panel, X = 0, Y = 0, Width = 100, Height = 100, FillColor = "#FF22303F" }] };
                var view = new ThemeView(theme, root, playVideo: false);
                view.Measure(new Size(400, 200));
                view.Arrange(new Rect(0, 0, 400, 200));
                view.UpdateLayout();
                var bitmap = new RenderTargetBitmap(400, 200, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(view);
                var pixels = new byte[400 * 200 * 4];
                bitmap.CopyPixels(pixels, 400 * 4, 0);
                byte Alpha(int x, int y) => pixels[(y * 400 + x) * 4 + 3];
                Check(Alpha(300, 150) == 0 && Alpha(50, 50) == 255, "A see-through theme leaves the desktop visible except under its elements");
            }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
        if (failure != null) throw failure;
    }
}
