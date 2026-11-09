using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Pulsewave.Themes;
using Point = System.Windows.Point;
using Size = System.Windows.Size;

namespace Pulsewave.Regression;

// Shortcuts, the app drawer and Claude Code / Codex usage. Everything runs on fixture files:
// nothing is launched, and neither Claude Code's nor Codex's real files are touched.
internal static class LauncherChecks
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("LAUNCHER: " + message);
        Console.WriteLine("PASS LAUNCHER: " + message);
    }

    /// <param name="shots">When set, the app drawer is also saved there as PNGs at three screen shapes.</param>
    public static void Run(string outputRoot, string? shots = null)
    {
        string root = Path.Combine(outputRoot, "launcher-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        // Settings: older files have no shortcuts; the list survives a save and is capped.
        string settingsFile = Path.Combine(root, "settings.json");
        File.WriteAllText(settingsFile, "{\"ActiveTheme\":\"\",\"FutureSetting\":7}");
        var settings = SettingsStore.LoadFile(settingsFile);
        Check(settings.Shortcuts.Count == 0, "Settings from before shortcuts load with an empty list");
        settings.Shortcuts = Enumerable.Range(1, AppShortcut.MaxCount + 6).Select(i => new AppShortcut { Name = "App " + i, Target = @"C:\Games\app" + i + ".exe" }).ToList();
        settings.Shortcuts.Insert(3, null!);
        SettingsStore.SaveFile(settingsFile, settings);
        var reloaded = SettingsStore.LoadFile(settingsFile);
        Check(reloaded.Shortcuts.Count == AppShortcut.MaxCount && reloaded.Shortcuts[3].Name == "App 4" && reloaded.Shortcuts[0].Target == @"C:\Games\app1.exe" &&
              File.ReadAllText(settingsFile).Contains("FutureSetting"),
            "Shortcuts are saved in order (empty entries dropped, at most 48) and unknown settings are kept");
        var someFree = new List<AppShortcut> { new() { Target = "a.exe" }, new(), new() { Target = "c.exe" } };
        Check(AppShortcut.MaxCount == 48 && ShortcutsForm.FreeSlots(someFree) == 46 && ShortcutsForm.FreeSlots(reloaded.Shortcuts) == 0 &&
              ShortcutsForm.SteamPickCount(3, 46) == "3 ticked · 43 left" && ShortcutsForm.SteamPickCount(50, 46) == "50 ticked · only 46 free",
            "The Steam picker counts free slots (gaps included) and says when more games are ticked than fit");
        Check(new AppShortcut { Target = @"C:\Games\Hades\Hades.exe" }.DisplayName == "Hades" && new AppShortcut { Name = " Steam ", Target = "x" }.DisplayName == "Steam",
            "A shortcut without a name is named after its file");

        // Drops and slots: a drop on a tile fills that slot (gaps stay empty), elsewhere the first free slot.
        var slots = new List<AppShortcut> { new() { Target = "a.exe" } };
        int onTile = AppShortcut.Assign(slots, new AppShortcut { Target = "e.exe" }, 5);
        int free = AppShortcut.Assign(slots, new AppShortcut { Target = "b.exe" });
        slots.Add(new AppShortcut());
        AppShortcut.TrimEnd(slots);
        Check(onTile == 5 && free == 2 && slots.Count == 5 && slots[2].IsEmpty && slots[3].IsEmpty && slots[4].Target == "e.exe" &&
              AppShortcut.Assign(Enumerable.Range(0, AppShortcut.MaxCount).Select(_ => new AppShortcut { Target = "x" }).ToList(), new AppShortcut { Target = "y" }) == 0 &&
              ShortcutView.From(new AppShortcut()) == null,
            "Dropping on a tile fills its slot (earlier slots stay empty), other drops take the first free slot, and a full list refuses");
        string dropped = Path.Combine(root, "Hades II.lnk");
        File.WriteAllText(dropped, "");
        Check(AppShortcut.FromDrop(dropped) is { Name: "Hades II" } && AppShortcut.FromDrop("steam://rungameid/1145350") is { Name: "steam", Target: "steam://rungameid/1145350" } &&
              AppShortcut.FromDrop("https://www.twitch.tv/") is { Name: "www.twitch.tv" } && AppShortcut.FromDrop(Path.Combine(root, "gone.exe")) == null &&
              AppShortcut.FromDrop(root)?.Target == root,
            "Dropped shortcuts, folders and web or steam:// addresses become shortcuts; missing files are ignored");
        var withGap = new ThemeDataContext { Shortcuts = [new ShortcutView("One", null), null, new ShortcutView("Three", null)] };
        Check(ThemeData.Read("APP_NAME", null, false, withGap, 2).Text == "" && ThemeData.Read("APP_NAME", null, false, withGap, 3).Text == "Three",
            "An empty slot between shortcuts shows nothing and later slots keep their numbers");

        // Dock mode: the reserved slice of a display; the live app bar is not registered in tests (it would move real windows).
        var display = new System.Drawing.Rectangle(0, 0, 2560, 1440);
        Check(AppBar.Slice(display, "Top", 22) == new System.Drawing.Rectangle(0, 0, 2560, 317) &&
              AppBar.Slice(display, "Right", 15) == new System.Drawing.Rectangle(2344, 0, 216, 1440) &&
              AppBar.Slice(new System.Drawing.Rectangle(0, 0, 5120, 1440), "Left", 22) == new System.Drawing.Rectangle(0, 0, 317, 1440) &&
              AppBar.Slice(new System.Drawing.Rectangle(-1920, 0, 1920, 1080), "Bottom", 99) == new System.Drawing.Rectangle(-1920, 540, 1920, 540) &&
              AppBar.Slice(display, "", 22) == display &&
              // Fit the theme: Tower (682x2560) beside a 1440-tall display, the 8.8" bar along a 2560-wide one, capped at half the short side.
              AppBar.Slice(new System.Drawing.Rectangle(0, 0, 5120, 1440), "Right", 0, 682, 2560) == new System.Drawing.Rectangle(4736, 0, 384, 1440) &&
              AppBar.Thickness(display, "Left", 0, 682, 2560, length: 1392) == 371 &&
              AppBar.Slice(display, "Top", 0) == new System.Drawing.Rectangle(0, 0, 2560, 640) &&
              AppBar.Slice(new System.Drawing.Rectangle(0, 0, 5120, 1440), "Left", 0, 1920, 480).Width == 720,
            "Dock mode fits the theme (a side dock as wide as a vertical theme needs) or takes 8–50% of the display's short side");
        File.WriteAllText(settingsFile, "{\"DockEdge\":\"Middle\",\"DockPercent\":90}");
        var dockSettings = SettingsStore.LoadFile(settingsFile);
        File.WriteAllText(settingsFile, "{}");
        Check(dockSettings.DockEdge == "" && dockSettings.DockPercent == 50 && SettingsStore.LoadFile(settingsFile) is { DockEdge: "", DockPercent: 0, ClaudeAccountUsage: false },
            "Dock mode is off by default and unknown edges or sizes are corrected");

        // Launching: nothing is started here, only refusals of targets that cannot open.
        Check(ShortcutLauncher.Launch(new AppShortcut()) != null && ShortcutLauncher.Launch(new AppShortcut { Target = Path.Combine(root, "missing.exe") })?.Contains("not found") == true,
            "An empty or missing shortcut target is reported instead of launched");

        // Tap defaults and data.
        var tile = new ThemeElement { Kind = ThemeElementKind.Shortcut, Slot = 2 };
        var name = new ThemeElement { Kind = ThemeElementKind.Data, DataName = "APP_NAME", Slot = 2 };
        var usage = new ThemeElement { Kind = ThemeElementKind.Arc, DataName = "CLAUDE_5H" };
        Check(ThemeActions.Effective(tile) == ThemeAction.Launch && ThemeActions.Effective(name) == ThemeAction.Launch && ThemeActions.Effective(usage) == ThemeAction.None,
            "Shortcut tiles and their names launch by default; usage readouts do nothing");
        var sample = ThemeSampleData.Uniform();
        var empty = new ThemeDataContext { Now = sample.Now };
        Check(ThemeData.Read("APP_NAME", null, false, sample, 2).Text == "Browser" && ThemeData.Read("APP_NAME", null, false, sample, 40).Text == "" &&
              ThemeData.Read("APP_NAME", null, false, empty, 1).Text == "",
            "A shortcut name shows the program in its slot and nothing for an empty slot");
        Check(ThemeData.Read("CLAUDE_5H", null, false, sample).Number == 42 && ThemeData.Read("CLAUDE_5H_RESET", null, false, sample).Text == "2h 14m" &&
              ThemeData.Read("CODEX_WEEK_RESET", null, false, sample).Text == "5d 2h" && ThemeData.Read("CODEX_5H", null, false, empty).Text == ThemeData.Missing,
            "Usage values show percent used and time to reset, and -- without data");
        var expired = new ThemeDataContext { Now = sample.Now, Usage = new AiUsage(new UsageWindow(97, new DateTimeOffset(sample.Now.AddMinutes(-1))), null, null, null) };
        Check(ThemeData.Read("CLAUDE_5H", null, false, expired).Number == 0 && ThemeData.Read("CLAUDE_5H_RESET", null, false, expired).Text == "--" &&
              AiUsageReader.Until(DateTimeOffset.Now.AddMinutes(5.5), DateTimeOffset.Now) == "6m",
            "A window past its reset time shows nothing used");

        // Codex: limits from its session log format, newest first, other limit ids ignored.
        const string line = "{\"timestamp\":\"2026-11-06T15:41:32.656Z\",\"type\":\"event_msg\",\"payload\":{\"type\":\"token_count\",\"info\":{},\"rate_limits\":" +
                            "{\"limit_id\":\"codex\",\"primary\":{\"used_percent\":12.0,\"window_minutes\":300,\"resets_at\":1793997442}," +
                            "\"secondary\":{\"used_percent\":3.0,\"window_minutes\":10080,\"resets_at\":1794443899},\"plan_type\":\"plus\"}}}";
        var parsed = AiUsageReader.ParseCodexLine(line);
        Check(parsed is { Session.UsedPercent: 12, Week.UsedPercent: 3 } && parsed.Value.Week!.ResetsAt == DateTimeOffset.FromUnixTimeSeconds(1794443899) &&
              AiUsageReader.ParseCodexLine(line.Replace("\"codex\"", "\"codex_other\"")) == null && AiUsageReader.ParseCodexLine("{\"payload\":{}}") == null,
            "Codex limits are read from token_count events: 5-hour and weekly windows by length");
        string codex = Path.Combine(root, "codex");
        var today = new DateTime(2026, 11, 6, 12, 0, 0);
        string day = Path.Combine(codex, "sessions", "2026", "11", "06"), older = Path.Combine(codex, "sessions", "2026", "11", "04");
        Directory.CreateDirectory(day); Directory.CreateDirectory(older);
        File.WriteAllText(Path.Combine(older, "rollout-old.jsonl"), line.Replace("12.0", "80.0") + "\n");
        File.SetLastWriteTimeUtc(Path.Combine(older, "rollout-old.jsonl"), DateTime.UtcNow.AddDays(-2));
        // The newest log is long and its limits are near the end, after a partial first line of the tail.
        File.WriteAllText(Path.Combine(day, "rollout-new.jsonl"), string.Concat(Enumerable.Repeat("{\"type\":\"response_item\",\"payload\":{\"text\":\"" + new string('x', 900) + "\"}}\n", 700)) +
                                                                      line + "\n{\"type\":\"event_msg\",\"payload\":{\"type\":\"agent_message\"}}\n");
        var (session, week) = AiUsageReader.ReadCodex(codex, today);
        Check(session?.UsedPercent == 12 && week?.UsedPercent == 3 && AiUsageReader.ReadCodex(Path.Combine(root, "no-codex"), today) == (null, null),
            "Codex usage comes from the newest session log of the last week (read from its end, without locking it)");

        // Claude Code: the status line relay saves limits and prints a short line.
        string usageFile = Path.Combine(root, "claude-usage.json");
        string input = "{\"model\":{\"display_name\":\"Opus\"},\"context_window\":{\"used_percentage\":8.4},\"rate_limits\":{\"five_hour\":{\"used_percentage\":23.5,\"resets_at\":1738425600}," +
                       "\"seven_day\":{\"used_percentage\":41.2,\"resets_at\":1738857600}}}";
        string printed = ClaudeStatusLine.Relay(input, usageFile);
        var claude = AiUsageReader.ReadClaude(usageFile);
        Check(printed == "Opus · ctx 8% · 5h 24% · wk 41%" && claude.Session?.UsedPercent == 23.5 && claude.Week?.ResetsAt == DateTimeOffset.FromUnixTimeSeconds(1738857600),
            "The Claude Code status line relay saves the 5-hour and weekly limits and prints model, context and usage");
        Check(ClaudeStatusLine.Relay("{\"model\":{\"display_name\":\"Opus\"}}", usageFile) == "Opus" && AiUsageReader.ReadClaude(usageFile).Session?.UsedPercent == 23.5 &&
              ClaudeStatusLine.Relay("not json", usageFile) == "Pulsewave",
            "Refreshes without limits (API keys, the first refresh) keep the last saved usage");
        var reader = new AiUsageReader(codex, usageFile);
        var both = reader.Read(DateTime.Now);
        Check(both.ClaudeSession?.UsedPercent == 23.5 && both.CodexWeek is not null && ReferenceEquals(both, reader.Read(DateTime.Now.AddSeconds(5))),
            "The HUD reads Claude Code and Codex usage together, at most every 15 seconds");

        // Connecting edits only the statusLine of Claude Code's settings and never replaces another one.
        string claudeSettings = Path.Combine(root, "claude", "settings.json"), launcher = Path.Combine(root, "pw", "claude-statusline.cmd");
        Directory.CreateDirectory(Path.GetDirectoryName(claudeSettings)!);
        File.WriteAllText(claudeSettings, "{\n  // comment\n  \"theme\": \"dark\",\n  \"permissions\": {\"allow\": [\"Bash(ls)\"]},\n}");
        Check(ClaudeStatusLine.Status(claudeSettings, launcher).State == ClaudeStatusLine.State.NotConnected, "Claude Code without a status line shows as not connected");
        ClaudeStatusLine.Connect(claudeSettings, launcher, @"C:\Program Files\Pulsewave\Pulsewave.exe");
        var written = JsonNode.Parse(File.ReadAllText(claudeSettings))!;
        Check(ClaudeStatusLine.Status(claudeSettings, launcher).State == ClaudeStatusLine.State.Connected && written["theme"]!.GetValue<string>() == "dark" &&
              written["permissions"]!["allow"]![0]!.GetValue<string>() == "Bash(ls)" && written["statusLine"]!["type"]!.GetValue<string>() == "command" &&
              File.Exists(claudeSettings + ".pulsewave-backup") && File.ReadAllText(launcher).Trim() == "@\"C:\\Program Files\\Pulsewave\\Pulsewave.exe\" --claude-statusline",
            "Connecting adds Pulsewave's status line, keeps every other Claude Code setting and a backup, and writes the launcher");
        Check(ClaudeStatusLine.Disconnect(claudeSettings, launcher) && JsonNode.Parse(File.ReadAllText(claudeSettings))!["statusLine"] is null &&
              JsonNode.Parse(File.ReadAllText(claudeSettings))!["theme"] is not null,
            "Disconnecting removes only Pulsewave's status line");
        File.WriteAllText(claudeSettings, "{\"statusLine\":{\"type\":\"command\",\"command\":\"~/.claude/mine.sh\"}}");
        var other = ClaudeStatusLine.Connect(claudeSettings, launcher, @"C:\Pulsewave.exe");
        Check(other == (ClaudeStatusLine.State.OtherStatusLine, "~/.claude/mine.sh") && File.ReadAllText(claudeSettings).Contains("mine.sh") &&
              !ClaudeStatusLine.Disconnect(claudeSettings, launcher),
            "Another status line is never replaced or removed");
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string command = ClaudeStatusLine.Command(Path.Combine(home, "AppData", "Local", "Pulsewave", "claude-statusline.cmd"));
        Check(command == "~/AppData/Local/Pulsewave/claude-statusline.cmd" && ClaudeStatusLine.Command(@"D:\My Tools\x.cmd") == "\"D:/My Tools/x.cmd\"",
            "The status line command uses forward slashes and ~ for the home folder, as Claude Code asks for on Windows");

        // Claude sign-in usage (opt-in): a fixture sign-in and a fake server; nothing goes on the network.
        string credentials = Path.Combine(root, "claude", ".credentials.json"), accountUsage = Path.Combine(root, "account-usage.json");
        long Ms(DateTimeOffset t) => t.ToUnixTimeMilliseconds();
        File.WriteAllText(credentials, $"{{\"claudeAiOauth\":{{\"accessToken\":\"fixture-token\",\"refreshToken\":\"never-used\",\"expiresAt\":{Ms(DateTimeOffset.UtcNow.AddHours(2))}}}}}");
        Check(ClaudeAccountUsage.ReadToken(credentials, DateTimeOffset.UtcNow) == "fixture-token" &&
              ClaudeAccountUsage.ReadToken(credentials, DateTimeOffset.UtcNow.AddHours(3)) == null && ClaudeAccountUsage.ReadToken(Path.Combine(root, "none.json"), DateTimeOffset.UtcNow) == null,
            "The Claude sign-in is used only while it is valid (an expired one waits for Claude to renew it)");
        const string response = "{\"five_hour\":{\"utilization\":11.0,\"resets_at\":\"2026-11-06T20:30:00.438829-05:00\"},\"seven_day\":{\"utilization\":10.0," +
                                "\"resets_at\":\"2026-11-12T20:00:00.438851-05:00\"},\"seven_day_opus\":null}";
        var server = new FakeServer(response);
        bool fetched = ClaudeAccountUsage.FetchAsync(new System.Net.Http.HttpClient(server), credentials, accountUsage).GetAwaiter().GetResult();
        var account = AiUsageReader.ReadClaude(accountUsage);
        Check(fetched && server.Requests == 1 && server.Url == "https://api.anthropic.com/api/oauth/usage" && server.Authorization == "Bearer fixture-token" &&
              account.Session?.UsedPercent == 11 && account.Week?.ResetsAt == DateTimeOffset.Parse("2026-11-13T01:00:00Z") &&
              !File.ReadAllText(accountUsage).Contains("fixture-token") && File.ReadAllText(credentials).Contains("never-used"),
            "Sign-in usage is asked of api.anthropic.com only, saved without the token, and the sign-in file is left as it was");
        File.WriteAllText(credentials, $"{{\"claudeAiOauth\":{{\"accessToken\":\"old\",\"expiresAt\":{Ms(DateTimeOffset.UtcNow.AddMinutes(-5))}}}}}");
        Check(!ClaudeAccountUsage.FetchAsync(new System.Net.Http.HttpClient(server), credentials, accountUsage).GetAwaiter().GetResult() && server.Requests == 1 &&
              !new FakeServer("{}", System.Net.HttpStatusCode.Unauthorized).Let(s => ClaudeAccountUsage.FetchAsync(new System.Net.Http.HttpClient(s), Path.Combine(root, "none.json"), accountUsage).GetAwaiter().GetResult()),
            "No request is made with an expired or missing sign-in");

        var (columns, rows, tileSize) = AppDrawerView.Layout(10, new Size(1888, 382));
        var (tabletColumns, _, tabletTile) = AppDrawerView.Layout(12, new Size(736, 382));
        Check(columns * rows >= 10 && tileSize >= 150 && tabletColumns * ((12 + tabletColumns - 1) / tabletColumns) >= 12 && tabletTile >= 100 &&
              AppDrawerView.Layout(0, new Size(500, 300)).Tile > 0,
            $"The app drawer picks the largest tiles that fit: 10 shortcuts on a bar as {columns}×{rows} of {tileSize:0} units");

        Exception? failure = null;
        var thread = new Thread(() => { try { Rendered(root, shots); } catch (Exception ex) { failure = ex; } });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
        if (failure != null) throw failure;
    }

    private static void Rendered(string root, string? shots)
    {
        string notepad = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "notepad.exe");
        var icon = ShortcutIcons.Load(new AppShortcut { Target = notepad }, 64);
        Check(icon is { PixelWidth: >= 32 } && Opaque(icon) > 0.1 && Opaque(icon) < 1 && ShortcutIcons.Load(new AppShortcut { Target = "https://example.com" }) == null,
            "Shortcut icons come from the shell with transparency (web addresses use a glyph)");

        string dir = BuiltInTemplates.Ensure(BuiltInTemplates.Prefix + "19-launchpad", Path.Combine(root, "templates"))!;
        var theme = ThemeStore.Load(dir);
        var view = new ThemeView(theme, dir, playVideo: false);
        view.Measure(new Size(theme.Width, theme.Height));
        view.Arrange(new Rect(0, 0, theme.Width, theme.Height));
        view.Update(ThemeSampleData.Uniform());
        view.UpdateLayout();
        var tiles = theme.Elements.Where(e => e.Kind == ThemeElementKind.Shortcut).ToList();
        var first = tiles.First(t => t.Slot == 1);
        var button = theme.Elements.First(e => e.Action == ThemeAction.AppDrawer && e.Kind == ThemeElementKind.Panel);
        var hitTile = view.ElementAt(new Point(first.X + first.Width / 2, first.Y + first.Height / 2));
        var hitName = view.ElementAt(new Point(first.X + first.Width / 2, first.Y + first.Height + 18));
        var hitButton = view.ElementAt(new Point(button.X + button.Width - 20, button.Y + button.Height / 2));
        Check(tiles.Select(t => t.Slot).Order().SequenceEqual(Enumerable.Range(1, 10)) && hitTile?.Slot == 1 && ThemeActions.Effective(hitTile) == ThemeAction.Launch &&
              hitName is { DataName: "APP_NAME", Slot: 1 } && hitButton is not null && ThemeActions.Effective(hitButton) == ThemeAction.AppDrawer,
            "Launchpad: tapping a tile or its name opens that slot's shortcut, and All apps opens the drawer");

        foreach (var (label, w, h) in new[] { ("bar", 1920, 480), ("y70-vertical", 682, 2560), ("tablet", 1920, 1200) })
        {
            var size = SensorDetails.LogicalSize(w, h);
            var drawer = new AppDrawerView(size);
            int launched = 0;
            drawer.LaunchRequested += slot => launched = slot;
            drawer.Open(ThemeSampleData.SampleShortcuts.Take(10).Prepend(null).ToList()); // an empty first slot is skipped
            drawer.Measure(size);
            drawer.Arrange(new Rect(size));
            drawer.UpdateLayout();
            var bitmap = new RenderTargetBitmap((int)size.Width, (int)size.Height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(drawer);
            // The first tile: a hit test at its centre must land on it.
            var tileBorder = Descendants(drawer).OfType<System.Windows.Controls.Border>().First(b => b.ActualWidth > 100 && b.Child is System.Windows.Controls.StackPanel);
            var centre = tileBorder.TransformToAncestor(drawer).Transform(new Point(tileBorder.ActualWidth / 2, tileBorder.ActualHeight / 2));
            DependencyObject? hit = VisualTreeHelper.HitTest(drawer, centre)?.VisualHit;
            for (; hit != null && hit != tileBorder; hit = VisualTreeHelper.GetParent(hit)) { }
            if (shots != null)
            {
                Directory.CreateDirectory(shots);
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using var stream = File.Create(Path.Combine(shots, $"drawer-{label}.png"));
                encoder.Save(stream);
            }
            // The first tile shown is slot 2: slot 1 is empty.
            tileBorder.RaiseEvent(new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice, 0, System.Windows.Input.MouseButton.Left)
                { RoutedEvent = UIElement.MouseLeftButtonUpEvent });
            Check(hit != null && Opaque(bitmap) > 0.9 && launched == 2, $"The app drawer draws at the {label} shape ({size.Width:0}x{size.Height:0}) with a tile under its centre");
        }

        // 30 shortcuts: two pages of at most 24, tiles as large as with 24, and the pages clamp at both ends.
        var barSize = SensorDetails.LogicalSize(1920, 480);
        var paged = new AppDrawerView(barSize);
        var many = Enumerable.Range(0, 30).Select(i => ThemeSampleData.SampleShortcuts[i % ThemeSampleData.SampleShortcuts.Count]).ToList();
        paged.Open(many);
        var firstPage = paged.Paging;
        paged.ShowPage(1);
        int onSecond = 0;
        paged.Measure(barSize); paged.Arrange(new Rect(barSize)); paged.UpdateLayout();
        onSecond = Descendants(paged).OfType<System.Windows.Controls.Border>().Count(b => b.Child is System.Windows.Controls.StackPanel { Children.Count: 2 } && b.ActualWidth > 60);
        paged.ShowPage(5);
        var clamped = paged.Paging;
        paged.Open(many.Take(24).ToList());
        Check(firstPage == (0, 2) && onSecond == 6 && clamped == (1, 2) && paged.Paging == (0, 1) && AppDrawerView.Pages(48) == 2,
            $"The app drawer shows 24 shortcuts to a page with page buttons beyond that ({onSecond} tiles on page 2 of 30 shortcuts)");

        // Pages keep one shape: 44 shortcuts (24 + 20) put the first tile in the same place at the same size on both pages.
        var shapes = new List<string>();
        foreach (var (w, h) in new[] { (1920, 480), (682, 2560) })
        {
            var size = SensorDetails.LogicalSize(w, h);
            var drawer = new AppDrawerView(size);
            drawer.Open(Enumerable.Range(0, 44).Select(i => ThemeSampleData.SampleShortcuts[i % ThemeSampleData.SampleShortcuts.Count]).ToList());
            Rect FirstTile(int page)
            {
                drawer.ShowPage(page);
                drawer.Measure(size); drawer.Arrange(new Rect(size)); drawer.UpdateLayout();
                var tile = Descendants(drawer).OfType<System.Windows.Controls.Border>().First(b => b.Child is System.Windows.Controls.StackPanel { Children.Count: 2 } && b.ActualWidth > 60);
                return tile.TransformToAncestor(drawer).TransformBounds(new Rect(0, 0, tile.ActualWidth, tile.ActualHeight));
            }
            Rect one = FirstTile(0), two = FirstTile(1);
            if (Math.Abs(one.X - two.X) > 0.5 || Math.Abs(one.Y - two.Y) > 0.5 || Math.Abs(one.Width - two.Width) > 0.5) shapes.Add($"{w}x{h}: {one} vs {two}");
        }
        Check(shapes.Count == 0, "Every app drawer page has the same grid: tiles keep their size and place from page to page " + string.Join("; ", shapes));
    }

    private sealed class FakeServer(string body, System.Net.HttpStatusCode status = System.Net.HttpStatusCode.OK) : System.Net.Http.HttpMessageHandler
    {
        public int Requests { get; private set; }
        public string? Url { get; private set; }
        public string? Authorization { get; private set; }

        public T Let<T>(Func<FakeServer, T> use) => use(this);

        protected override Task<System.Net.Http.HttpResponseMessage> SendAsync(System.Net.Http.HttpRequestMessage request, CancellationToken cancel)
        {
            Requests++;
            Url = request.RequestUri?.ToString();
            Authorization = request.Headers.Authorization?.ToString();
            return Task.FromResult(new System.Net.Http.HttpResponseMessage(status) { Content = new System.Net.Http.StringContent(body) });
        }
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            yield return child;
            foreach (var deeper in Descendants(child)) yield return deeper;
        }
    }

    private static double Opaque(BitmapSource source)
    {
        var converted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        var pixels = new byte[converted.PixelWidth * converted.PixelHeight * 4];
        converted.CopyPixels(pixels, converted.PixelWidth * 4, 0);
        int opaque = 0;
        for (int i = 3; i < pixels.Length; i += 4) if (pixels[i] > 200) opaque++;
        return opaque / (double)(pixels.Length / 4);
    }
}
