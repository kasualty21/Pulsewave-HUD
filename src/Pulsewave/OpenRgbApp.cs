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
                    else if (!IsWindowVisible(window)) ShowWindow(window, SwShow);
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
