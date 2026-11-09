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
    }

    private static string WriteOld(string root)
    {
        string file = Path.Combine(root, "old-settings.json");
        File.WriteAllText(file, "{\"MonitorDeviceName\":\"old-display\",\"ActiveTheme\":\"builtin:01-glass-cards\"}");
        return file;
    }
}
