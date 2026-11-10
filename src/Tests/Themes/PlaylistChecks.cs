using System.IO;
using Pulsewave.Themes;
using Forms = System.Windows.Forms;

namespace Pulsewave.Regression;

// Theme playlist order and hotkey text. Hotkeys are only parsed here: registering one would be
// a system-wide change, so RegisterHotKey is left to the running app.
internal static class PlaylistChecks
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("PLAYLIST: " + message);
        Console.WriteLine("PASS PLAYLIST: " + message);
    }

    public static void Run(string outputRoot)
    {
        string[] all = ["", "builtin:01-glass-cards", "builtin:02-aurora", "mine"];
        Check(ThemeCycle.Order([], all).SequenceEqual(all), "An empty playlist cycles through every theme");
        var order = ThemeCycle.Order(["mine", "gone", "", "mine"], all);
        Check(order.SequenceEqual(["mine", ""]), "The playlist keeps its own order and drops deleted or repeated themes");
        Check(ThemeCycle.Order(["gone"], all).SequenceEqual(all), "A playlist of only deleted themes falls back to every theme");
        Check(ThemeCycle.Step(order, "mine", +1) == "" && ThemeCycle.Step(order, "", +1) == "mine" && ThemeCycle.Step(order, "mine", -1) == "",
            "Next and previous wrap around the playlist");
        Check(ThemeCycle.Step(order, "builtin:02-aurora", +1) == "mine" && ThemeCycle.Step(order, "builtin:02-aurora", -1) == "",
            "From a theme outside the playlist, next starts at the first entry and previous at the last");

        string root = Path.Combine(outputRoot, "playlist-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var available = ThemeCycle.Available(root);
        Check(available[0].Id == ThemeStore.ClassicId && available.Count == 1 + BuiltInTemplates.Names.Count &&
              available.Any(t => t.Id == "builtin:09-vinyl" && t.Name == "Vinyl"),
            "Every theme is available to the playlist by its gallery name");

        Check(Hotkey.TryParse("Ctrl+Alt+PageDown", out var next) && next.Key == Forms.Keys.PageDown &&
              next.Modifiers == (HotkeyModifiers.Ctrl | HotkeyModifiers.Alt) && next.ToString() == "Ctrl+Alt+PageDown",
            "Hotkey text round-trips (Ctrl+Alt+PageDown)");
        Check(Hotkey.TryParse("win + shift + 1", out var digit) && digit.ToString() == "Shift+Win+1" && Hotkey.TryParse("F13", out _),
            "Hotkeys accept Win, digits and lone F13–F24 macro keys");
        Check(!Hotkey.TryParse("PageDown", out _) && !Hotkey.TryParse("Shift+A", out _) && !Hotkey.TryParse("Ctrl+Alt", out _) &&
              !Hotkey.TryParse("Ctrl+A+B", out _) && !Hotkey.TryParse("", out _) && !Hotkey.TryParse("Ctrl+Nonsense", out _),
            "Hotkeys that would steal typing, or are incomplete or unknown, are refused");

        string file = Path.Combine(root, "settings.json");
        File.WriteAllText(file, "{\"ActiveTheme\":\"mine\",\"ThemePlaylist\":null,\"PlaylistMinutes\":0,\"HotkeyNextTheme\":null}");
        var loaded = SettingsStore.LoadFile(file);
        Check(loaded.ThemePlaylist.Count == 0 && loaded.PlaylistMinutes == 1 && loaded.HotkeyNextTheme == "" && !loaded.PlaylistEnabled,
            "Damaged playlist settings load safely (empty playlist, 1-minute minimum, hotkey off)");
        var older = SettingsStore.LoadFile(WriteOld(root));
        Check(older.ThemePlaylist.Count == 0 && !older.PlaylistEnabled && older.PlaylistMinutes == 5 &&
              older.HotkeyNextTheme == "Ctrl+Alt+PageDown" && older.HotkeyPreviousTheme == "Ctrl+Alt+PageUp" && older.HotkeyTogglePlaylist == "Ctrl+Alt+End",
            "Settings from before the playlist get rotation off and the default hotkeys");

        // 0.12: every hotkey is a binding to an action; the fixed ones of earlier versions move in with their keys.
        string Of(IEnumerable<HotkeyBinding> list) => string.Join(", ", list.Select(b => $"{b.Keys}={b.Action}"));
        File.WriteAllText(file, "{\"ActiveTheme\":\"mine\",\"HotkeyNextTheme\":\"Ctrl+Alt+N\",\"HotkeyPreviousTheme\":\"\",\"HotkeyAesthetic\":\"Win+F9\",\"FutureSetting\":7}");
        var migrated = SettingsStore.LoadFile(file);
        Check(Of(older.Hotkeys!) == "Ctrl+Alt+PageDown=nextTheme, Ctrl+Alt+PageUp=previousTheme, Ctrl+Alt+End=rotate" &&
              Of(migrated.Hotkeys!) == "Ctrl+Alt+N=nextTheme, Ctrl+Alt+End=rotate, Win+F9=aesthetic",
            "Settings from before 0.12 keep their hotkeys: next / previous theme, rotation and aesthetic mode become bindings with the same keys (blank ones left out)");
        migrated.Hotkeys = [];
        SettingsStore.SaveFile(file, migrated);
        var none = SettingsStore.LoadFile(file);
        string json = File.ReadAllText(file);
        none.Hotkeys = [new HotkeyBinding { Keys = "ctrl+alt+m", Action = "lightsToggle" }, new HotkeyBinding { Keys = "Ctrl+Alt+M", Action = "media" },
                        new HotkeyBinding { Keys = "Ctrl+Alt+1", Action = "theme:builtin:30-halo-gaming" }, new HotkeyBinding { Keys = "Ctrl+Alt+2", Action = "launch:3" },
                        new HotkeyBinding { Keys = "Ctrl+Alt+3", Action = "explode" }, new HotkeyBinding { Keys = "", Action = "drawer" }, null!];
        SettingsStore.SaveFile(file, none);
        var cleaned = SettingsStore.LoadFile(file).Hotkeys!;
        Check(Of(cleaned) == "Ctrl+Alt+M=lightsToggle, Ctrl+Alt+1=theme:builtin:30-halo-gaming, Ctrl+Alt+2=launch:3" &&
              json.Contains("\"Hotkeys\": []") && json.Contains("HotkeyNextTheme") && json.Contains("FutureSetting") &&
              HotkeyBindings.KeysFor(cleaned, "lightsToggle") == "Ctrl+Alt+M" && HotkeyBindings.KeysFor(cleaned, "nextTheme") == null,
            "Removing every hotkey stays removed (no re-migration; the old fields are kept untouched); saved hotkeys keep one action per key combination, " +
            "known actions (a theme, a shortcut) and no blank keys");
        var problems = HotkeyBindings.Problems([new HotkeyBinding { Keys = "Ctrl+Alt+M", Action = "media" }, new HotkeyBinding { Keys = "ctrl+alt+m", Action = "drawer" },
            new HotkeyBinding { Keys = "Shift+A", Action = "media" }, new HotkeyBinding { Keys = "", Action = "themes" }, new HotkeyBinding { Keys = "Ctrl+F1", Action = "?" }], a => a);
        Check(problems.Count == 4 && problems[0].Contains("used twice") && problems[1].StartsWith("Shift+A") && problems[2].Contains("no keys") && problems[3].Contains("pick"),
            "The Hotkeys window refuses keys used twice, keys that would steal typing, rows without keys and rows without an action");
        var actions = HotkeysForm.Actions([("builtin:30-halo-gaming", "Halo Gaming")], [new AppShortcut { Name = "Steam", Target = "steam://open/main" }, new AppShortcut()]);
        Check(actions.Count == TrayActions.Fixed.Count(a => a.Id.Length > 0) + 2 && actions.Any(a => a is ("hotkeys", "Open Hotkeys")) &&
              actions[^2] == ("theme:builtin:30-halo-gaming", "Show theme · Halo Gaming") && actions[^1] == ("launch:1", "Open · Steam") && TrayActions.IsKnown("hotkeys"),
            "A hotkey can do any tray double-click action, show any installed theme or open any shortcut (empty slots left out)");
    }

    private static string WriteOld(string root)
    {
        string file = Path.Combine(root, "old-settings.json");
        File.WriteAllText(file, "{\"MonitorDeviceName\":\"old-display\",\"ActiveTheme\":\"builtin:01-glass-cards\"}");
        return file;
    }
}
