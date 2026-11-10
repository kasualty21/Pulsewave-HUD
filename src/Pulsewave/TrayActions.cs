namespace Pulsewave;

/// <summary>
/// What double-clicking the tray icon does (tray → Double-click the icon), and what a hotkey does (tray → Hotkeys). Saved as an id: a fixed action below,
/// "theme:&lt;theme id&gt;" to show one theme, or "launch:&lt;slot&gt;" to open one of the user's shortcuts.
/// </summary>
public static class TrayActions
{
    public const string Default = "restore";

    /// <summary>The fixed actions in menu order, with a blank id marking where a separator goes.</summary>
    public static readonly IReadOnlyList<(string Id, string Label)> Fixed =
    [
        ("restore", "Show the HUD"), ("toggle", "Show or hide the HUD"),
        ("", ""),
        ("themes", "Open Themes"), ("nextTheme", "Next theme"), ("previousTheme", "Previous theme"),
        ("rotate", "Theme rotation on / off"), ("playlist", "Open the theme playlist"), ("nextPalette", "Next color palette"),
        ("", ""),
        ("shortcuts", "Open Shortcuts"), ("drawer", "Show the app drawer on the HUD"), ("hotkeys", "Open Hotkeys"),
        ("", ""),
        ("media", "Show the media page"), ("lightsPage", "Show the Lights page"), ("lightsToggle", "Lights on / off"),
        ("aesthetic", "Aesthetic mode on / off"), ("gaming", "Open Gaming"), ("schedule", "Open the schedule"),
        ("telemetry", "Open Telemetry"), ("sensors", "Open Sensor Manager"),
    ];

    public static bool IsKnown(string? id) =>
        id is not null && (Fixed.Any(a => a.Id.Length > 0 && a.Id == id) ||
                           id.StartsWith("theme:", StringComparison.Ordinal) && id.Length > 6 ||
                           id.StartsWith("launch:", StringComparison.Ordinal) && int.TryParse(id[7..], out int slot) && slot is >= 1 and <= AppShortcut.MaxCount);

    /// <summary>A saved id, or the default (show the HUD) when it is missing or unknown.</summary>
    public static string Normalize(string? id) => IsKnown(id) ? id! : Default;

    /// <summary>The action in words, for the menu's title ("Double-click the icon · Next theme").</summary>
    public static string Describe(string id, Func<string, string?> themeName, Func<int, string?> shortcutName) => id switch
    {
        _ when id.StartsWith("theme:", StringComparison.Ordinal) => "Show " + (themeName(id[6..]) ?? "a theme"),
        _ when id.StartsWith("launch:", StringComparison.Ordinal) && int.TryParse(id[7..], out int slot) => "Open " + (shortcutName(slot) ?? $"shortcut {slot}"),
        _ => Fixed.FirstOrDefault(a => a.Id == id).Label ?? Fixed[0].Label
    };
}
