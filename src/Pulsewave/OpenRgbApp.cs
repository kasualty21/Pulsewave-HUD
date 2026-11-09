using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Pulsewave;

/// <summary>
/// Opens the OpenRGB program for the Lights page's "OpenRGB" button: brings a running copy's window
/// to the front (also from the tray), or starts it. Never a second copy, since that would fight the
/// running one over the devices and the SDK port.
/// </summary>
internal static class OpenRgbApp
{
    internal static readonly string DefaultPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "OpenRGB", "OpenRGB.exe");

    /// <summary>Where to start OpenRGB from: the path last seen running, else the usual install folder; null when neither exists.</summary>
    internal static string? Executable(string? remembered, Func<string, bool> exists) =>
        new[] { remembered, DefaultPath }.FirstOrDefault(p => !string.IsNullOrWhiteSpace(p) &&
            string.Equals(Path.GetFileName(p), "OpenRGB.exe", StringComparison.OrdinalIgnoreCase) && exists(p!));

    /// <summary>Shows or starts OpenRGB. Returns a message for the status line; remembers the program's path in <paramref name="settings"/>.</summary>
    public static (bool Ok, string Status) Open(LightsSettings settings)
    {
        // OpenRGB can run twice on one PC: a background service (another Windows session, no window, often
        // the SDK server) and the program on the desktop. Only a copy in this session can show a window,
        // and a service alone doesn't stop the program from being started.
        bool windowless = false;
        int session = Process.GetCurrentProcess().SessionId;
        var processes = Process.GetProcessesByName("OpenRGB");
        try
        {
            foreach (var process in processes.Where(p => SessionOf(p) == session))
            {
                try { if (process.MainModule?.FileName is { Length: > 0 } path) settings.AppPath = path; }
                catch (Exception ex) when (ex is Win32Exception or InvalidOperationException) { } // an elevated copy
                if (WindowOf(process.Id) is { } window)
                {
                    if (IsIconic(window)) ShowWindow(window, SwRestore);
                    else if (!IsWindowVisible(window) && !ReopenFromTray(process.Id, window))
                        return (false, "OpenRGB is in the tray: double-click its icon to open it.");
                    // The HUD never takes focus, so Windows may refuse to hand it over; raising the window
                    // above the others (topmost, then back) still brings it into view.
                    if (!SetForegroundWindow(window))
                    {
                        SetWindowPos(window, HwndTopmost, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpShowWindow);
                        SetWindowPos(window, HwndNoTopmost, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpShowWindow);
                    }
                    return (true, "OpenRGB is open.");
                }
                windowless = true;
            }
        }
        finally { foreach (var p in processes) p.Dispose(); }
        if (windowless) return (false, "OpenRGB is running but its window couldn't be found; open it from its tray icon.");
        if (Executable(settings.AppPath, File.Exists) is not { } exe)
            return (false, "OpenRGB isn't installed in Program Files. Start it once and Pulsewave remembers where it is.");
        try
        {
            AllowSetForegroundWindow(AsfwAny); // the HUD never takes focus; let OpenRGB come to the front
            Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(exe)! })?.Dispose();
            settings.AppPath = exe;
            return (true, "Starting OpenRGB…");
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or FileNotFoundException)
        {
            return (false, "OpenRGB didn't start: " + ex.Message);
        }
    }

    /// <summary>The process's main "OpenRGB" window, visible or hidden in the tray.</summary>
    private static IntPtr? WindowOf(int processId)
    {
        IntPtr found = IntPtr.Zero, fallback = IntPtr.Zero;
        EnumWindows((window, _) =>
        {
            GetWindowThreadProcessId(window, out uint owner);
            if (owner != processId || GetWindow(window, GwOwner) != IntPtr.Zero) return true;
            // OpenRGB (Qt) keeps a dozen hidden popup menus also titled "OpenRGB"; only a window with a
            // title bar that isn't a tool window is the main one.
            if (!IsMainWindowStyle(GetWindowLong(window, GwlStyle), GetWindowLong(window, GwlExStyle))) return true;
            var title = new StringBuilder(256);
            GetWindowText(window, title, title.Capacity);
            if (title.ToString().StartsWith("OpenRGB", StringComparison.Ordinal)) { found = window; return false; }
            if (fallback == IntPtr.Zero && IsWindowVisible(window)) fallback = window;
            return true;
        }, IntPtr.Zero);
        return found != IntPtr.Zero ? found : fallback != IntPtr.Zero ? fallback : null;
    }

    /// <summary>
    /// OpenRGB minimized to the tray has its window hidden by Qt. Showing that window from outside leaves it blank (Qt
    /// still thinks it hidden and never draws it), so instead Pulsewave sends OpenRGB's tray icon the message a
    /// double-click on it sends (Qt 5's tray callback, WM_APP + 101, to its tray message window), and OpenRGB opens its
    /// own window. This follows Qt's way of handling tray icons rather than a documented OpenRGB interface, so it
    /// is checked: true only when the window really became visible within a second.
    /// </summary>
    private static bool ReopenFromTray(int processId, IntPtr window)
    {
        if (TrayWindowOf(processId) is not { } tray) return false;
        PostMessage(tray, TrayCallback, IntPtr.Zero, (IntPtr)WmLButtonDblClk);
        for (int i = 0; i < 20 && !IsWindowVisible(window); i++) Thread.Sleep(50);
        return IsWindowVisible(window);
    }

    /// <summary>Qt's hidden window that receives the process's tray icon clicks.</summary>
    private static IntPtr? TrayWindowOf(int processId)
    {
        IntPtr found = IntPtr.Zero;
        EnumWindows((candidate, _) =>
        {
            GetWindowThreadProcessId(candidate, out uint owner);
            if (owner != processId) return true;
            var name = new StringBuilder(256);
            GetClassName(candidate, name, name.Capacity);
            if (!IsQtTrayWindowClass(name.ToString())) return true;
            found = candidate;
            return false;
        }, IntPtr.Zero);
        return found != IntPtr.Zero ? found : null;
    }

    /// <summary>Qt names its tray message window's class like "Qt5150TrayIconMessageWindowClass" (Qt version in the middle).</summary>
    internal static bool IsQtTrayWindowClass(string name) =>
        name.StartsWith("Qt", StringComparison.Ordinal) && name.EndsWith("TrayIconMessageWindowClass", StringComparison.Ordinal);

    private const uint TrayCallback = 0x8000 + 101, WmLButtonDblClk = 0x0203;
    [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr window, StringBuilder name, int size);

    /// <summary>A window with a title bar (caption) that isn't a tool window: an app's main window, not a popup menu.</summary>
    internal static bool IsMainWindowStyle(int style, int exStyle) => (style & WsCaption) == WsCaption && (exStyle & WsExToolWindow) == 0;

    private const int GwlStyle = -16, GwlExStyle = -20, WsCaption = 0x00C00000, WsExToolWindow = 0x80;
    [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr window, int index);

    private static int SessionOf(Process process)
    {
        try { return process.SessionId; }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException) { return -1; }
    }

    private static readonly IntPtr HwndTopmost = new(-1), HwndNoTopmost = new(-2);
    private const uint SwpNoSize = 0x1, SwpNoMove = 0x2, SwpShowWindow = 0x40;
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int cx, int cy, uint flags);
    private const int SwShow = 5, SwRestore = 9, AsfwAny = -1;
    private const uint GwOwner = 4;
    private delegate bool EnumWindowsProc(IntPtr window, IntPtr data);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr data);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
    [DllImport("user32.dll")] private static extern IntPtr GetWindow(IntPtr window, uint command);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr window, StringBuilder text, int size);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr window);
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr window, int command);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32.dll")] private static extern bool AllowSetForegroundWindow(int processId);
}
