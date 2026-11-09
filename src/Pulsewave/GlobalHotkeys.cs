using System.Runtime.InteropServices;
using Forms = System.Windows.Forms;

namespace Pulsewave;

[Flags]
public enum HotkeyModifiers { None = 0, Alt = 0x1, Ctrl = 0x2, Shift = 0x4, Win = 0x8 } // RegisterHotKey's MOD_* values

/// <summary>A key combination such as "Ctrl+Alt+PageDown", as stored in settings.</summary>
public readonly record struct Hotkey(HotkeyModifiers Modifiers, Forms.Keys Key)
{
    private static readonly Dictionary<Forms.Keys, string> Names = new()
    {
        [Forms.Keys.PageUp] = "PageUp", [Forms.Keys.PageDown] = "PageDown", [Forms.Keys.Return] = "Enter",
        [Forms.Keys.Oemcomma] = "Comma", [Forms.Keys.OemPeriod] = "Period", [Forms.Keys.OemOpenBrackets] = "[",
        [Forms.Keys.OemCloseBrackets] = "]", [Forms.Keys.OemMinus] = "-", [Forms.Keys.Oemplus] = "="
    };

    /// <summary>
    /// Parses a stored combination. A global hotkey needs Ctrl, Alt or Win (Shift alone would steal
    /// typing), except F13–F24, which keyboards use for macro keys. Empty text means "none".
    /// </summary>
    /// <param name="requireModifier">False accepts any key alone (combinations Pulsewave sends rather than registers).</param>
    public static bool TryParse(string? text, out Hotkey hotkey, bool requireModifier = true)
    {
        hotkey = default;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var modifiers = HotkeyModifiers.None;
        Forms.Keys? key = null;
        foreach (var raw in text.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            switch (raw.ToLowerInvariant())
            {
                case "ctrl" or "control": modifiers |= HotkeyModifiers.Ctrl; continue;
                case "alt": modifiers |= HotkeyModifiers.Alt; continue;
                case "shift": modifiers |= HotkeyModifiers.Shift; continue;
                case "win" or "windows": modifiers |= HotkeyModifiers.Win; continue;
            }
            if (key != null) return false; // two keys
            var named = Names.FirstOrDefault(n => n.Value.Equals(raw, StringComparison.OrdinalIgnoreCase));
            if (named.Value != null) key = named.Key;
            else if (raw.Length == 1 && char.IsAsciiDigit(raw[0])) key = Forms.Keys.D0 + (raw[0] - '0');
            else if (Enum.TryParse<Forms.Keys>(raw, true, out var parsed) && !int.TryParse(raw, out _)) key = parsed;
            else return false;
        }
        if (key is not { } k || IsModifierKey(k)) return false;
        hotkey = new Hotkey(modifiers, k);
        return !requireModifier || hotkey.IsAllowed;
    }

    public bool IsAllowed => (Modifiers & (HotkeyModifiers.Ctrl | HotkeyModifiers.Alt | HotkeyModifiers.Win)) != 0 || Key is >= Forms.Keys.F13 and <= Forms.Keys.F24;

    public override string ToString()
    {
        var parts = new List<string>();
        if (Modifiers.HasFlag(HotkeyModifiers.Ctrl)) parts.Add("Ctrl");
        if (Modifiers.HasFlag(HotkeyModifiers.Alt)) parts.Add("Alt");
        if (Modifiers.HasFlag(HotkeyModifiers.Shift)) parts.Add("Shift");
        if (Modifiers.HasFlag(HotkeyModifiers.Win)) parts.Add("Win");
        parts.Add(Names.TryGetValue(Key, out var name) ? name : Key is >= Forms.Keys.D0 and <= Forms.Keys.D9 ? ((char)('0' + (Key - Forms.Keys.D0))).ToString() : Key.ToString());
        return string.Join("+", parts);
    }

    public static bool IsModifierKey(Forms.Keys key) => key is Forms.Keys.ControlKey or Forms.Keys.LControlKey or Forms.Keys.RControlKey or
        Forms.Keys.Menu or Forms.Keys.LMenu or Forms.Keys.RMenu or Forms.Keys.ShiftKey or Forms.Keys.LShiftKey or Forms.Keys.RShiftKey or
        Forms.Keys.LWin or Forms.Keys.RWin;
}

/// <summary>
/// Registers system-wide hotkeys for a window with the documented RegisterHotKey API; Windows
/// posts WM_HOTKEY to that window. A combination another app already owns cannot be registered.
/// </summary>
public sealed class GlobalHotkeys
{
    public const int WmHotkey = 0x0312;
    private const uint ModNoRepeat = 0x4000;
    private readonly IntPtr _window;
    private readonly List<int> _registered = new();

    public GlobalHotkeys(IntPtr window) => _window = window;

    /// <summary>Replaces all registrations; returns a message for each combination that could not be registered.</summary>
    public IReadOnlyList<string> Register(IEnumerable<(int Id, string Name, string Combination)> hotkeys)
    {
        UnregisterAll();
        var problems = new List<string>();
        foreach (var (id, name, combination) in hotkeys)
        {
            if (string.IsNullOrWhiteSpace(combination)) continue;
            if (!Hotkey.TryParse(combination, out var hotkey)) { problems.Add($"{name}: '{combination}' is not a usable hotkey."); continue; }
            if (RegisterHotKey(_window, id, (uint)hotkey.Modifiers | ModNoRepeat, (uint)hotkey.Key)) _registered.Add(id);
            else problems.Add($"{name}: {hotkey} is already used by another app.");
        }
        return problems;
    }

    public void UnregisterAll()
    {
        foreach (int id in _registered) UnregisterHotKey(_window, id);
        _registered.Clear();
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint key);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(IntPtr window, int id);
}
