using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;

namespace Pulsewave;

/// <summary>
/// Opt-in "keep the HUD display clear": app windows that open or are dropped on the HUD's display
/// are moved to the main display (documented EnumWindows/SetWindowPos). Full-screen windows,
/// tool windows, owned popups and the shell are left alone.
/// </summary>
public static class DisplayGuard
{
    /// <summary>Moves stray windows; returns how many were moved.</summary>
    public static int Sweep(Rectangle hud, Rectangle destination, IntPtr self)
    {
        var stray = new List<IntPtr>();
        EnumWindows((window, _) =>
        {
            if (window != self && IsAppWindow(window) && GetWindowRect(window, out var r))
            {
                var rect = Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom);
                var centre = new Point(rect.Left + rect.Width / 2, rect.Top + rect.Height / 2);
                // A window exactly covering a display is full screen (a game or video); leave it be.
                if (hud.Contains(centre) && rect != hud) stray.Add(window);
            }
            return true;
        }, IntPtr.Zero);
        foreach (var window in stray)
        {
            bool maximized = IsZoomed(window);
            if (maximized) ShowWindow(window, 9); // restore so it can move, then maximize on the other display
            if (!GetWindowRect(window, out var r)) continue;
            var target = Destination(Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom), destination);
            SetWindowPos(window, IntPtr.Zero, target.X, target.Y, target.Width, target.Height, 0x0004 | 0x0010);
            if (maximized) ShowWindow(window, 3);
        }
        return stray.Count;
    }

    /// <summary>Where a moved window goes: its size (shrunk to fit if needed), centred in the main display's work area.</summary>
    public static Rectangle Destination(Rectangle window, Rectangle workArea)
    {
        int w = Math.Min(window.Width, workArea.Width), h = Math.Min(window.Height, workArea.Height);
        return new Rectangle(workArea.Left + (workArea.Width - w) / 2, workArea.Top + (workArea.Height - h) / 2, w, h);
    }

    private static bool IsAppWindow(IntPtr window)
    {
        if (!IsWindowVisible(window) || IsIconic(window) || GetWindow(window, 4) != IntPtr.Zero) return false; // GW_OWNER
        long style = GetWindowLongPtr(window, -16).ToInt64(), exStyle = GetWindowLongPtr(window, -20).ToInt64();
        if ((exStyle & 0x80) != 0 && (exStyle & 0x40000) == 0) return false; // tool window that is not an app window
        if ((style & 0x00C00000) != 0x00C00000 && (exStyle & 0x40000) == 0) return false; // no caption and not an app window
        if (DwmGetWindowAttribute(window, 14, out int cloaked, sizeof(int)) == 0 && cloaked != 0) return false; // hidden store app
        var name = new StringBuilder(64);
        GetClassName(window, name, name.Capacity);
        return name.ToString() is not ("Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd");
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect { public int Left, Top, Right, Bottom; }

    private delegate bool EnumWindowsProc(IntPtr window, IntPtr param);

    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr param);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr window);
    [DllImport("user32.dll")] private static extern bool IsZoomed(IntPtr window);
    [DllImport("user32.dll")] private static extern IntPtr GetWindow(IntPtr window, uint command);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr window, out NativeRect rect);
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr window, int command);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern IntPtr GetWindowLongPtr(IntPtr window, int index);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr window, StringBuilder name, int size);
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr window, int attribute, out int value, int size);
}
