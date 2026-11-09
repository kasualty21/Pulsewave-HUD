using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Point = System.Windows.Point;

namespace Pulsewave;

/// <summary>
/// Touch on the HUD without taking the mouse pointer. Windows sends touches to a window as pointer
/// messages first (WM_POINTERDOWN/UPDATE/UP) and only turns the unhandled ones into mouse input,
/// which is what moves the cursor. The HUD handles touch and pen pointer messages itself and replays
/// the lift as a WPF mouse-up at the touched point, so taps, swipes and the drawer and details pages
/// work as before while the cursor stays where it was (in the game).
/// </summary>
internal static class TouchInput
{
    public const int WmPointerUpdate = 0x0245, WmPointerDown = 0x0246, WmPointerUp = 0x0247, WmPointerActivate = 0x024B;
    public const int PaNoActivate = 3;
    private const uint PtTouch = 2, PtPen = 3;
    private const int PointerFlagPrimary = 0x2000;

    /// <summary>Messages Windows would otherwise turn into mouse input.</summary>
    public static bool IsContact(int message) => message is WmPointerUpdate or WmPointerDown or WmPointerUp;

    /// <summary>A finger or pen, not a mouse or touchpad (those keep working as the mouse).</summary>
    public static bool IsTouchOrPen(IntPtr wParam) =>
        GetPointerType((uint)(wParam.ToInt64() & 0xFFFF), out uint type) && type is PtTouch or PtPen;

    /// <summary>Whether this is the first finger down (others are ignored) and its screen position in pixels.</summary>
    public static (bool Primary, System.Drawing.Point Screen) Decode(IntPtr wParam, IntPtr lParam)
    {
        long w = wParam.ToInt64(), l = lParam.ToInt64();
        bool primary = ((w >> 16) & PointerFlagPrimary) != 0;
        // Signed 16-bit halves: displays left of or above the main one have negative coordinates.
        return (primary, new System.Drawing.Point((short)(l & 0xFFFF), (short)((l >> 16) & 0xFFFF)));
    }

    /// <summary>
    /// Replays a lifted touch as a left mouse-up on whatever is at <paramref name="at"/> (in
    /// <paramref name="root"/>'s coordinates), or on the root itself when nothing is there.
    /// </summary>
    public static void Tap(UIElement root, Point at)
    {
        // Like a real click, skip collapsed, hidden and non-hit-testable elements and all inside them,
        // so a closed menu (still laid out underneath) never takes the tap.
        IInputElement target = root;
        DependencyObject? hit = null;
        VisualTreeHelper.HitTest(root,
            node => node is UIElement { Visibility: not Visibility.Visible } or UIElement { IsHitTestVisible: false }
                ? HitTestFilterBehavior.ContinueSkipSelfAndChildren : HitTestFilterBehavior.Continue,
            result => { hit = result.VisualHit; return HitTestResultBehavior.Stop; },
            new PointHitTestParameters(at));
        for (DependencyObject? node = hit; node != null; node = VisualTreeHelper.GetParent(node))
            if (node is UIElement element) { target = element; break; }
        target.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
            { RoutedEvent = Mouse.MouseUpEvent, Source = target });
    }

    /// <summary>
    /// Safety net for touch drivers that move the cursor anyway: put it back only when the touch
    /// pulled it onto the HUD from somewhere else.
    /// </summary>
    public static bool ShouldRestore(System.Drawing.Point before, System.Drawing.Point now, System.Drawing.Rectangle hud) =>
        hud.Contains(now) && !hud.Contains(before);

    public static System.Drawing.Point? Cursor() => GetCursorPos(out var p) ? new System.Drawing.Point(p.X, p.Y) : null;

    public static void RestoreCursor(IntPtr hwnd, System.Drawing.Point? before)
    {
        if (before is not { } was || Cursor() is not { } now || !GetWindowRect(hwnd, out var r)) return;
        if (ShouldRestore(was, now, System.Drawing.Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom)))
            SetCursorPos(was.X, was.Y);
    }

    /// <summary>Turns off Windows' touch ripples and press-and-hold (right-click) square on the HUD.</summary>
    public static void DisableFeedback(IntPtr hwnd)
    {
        // FEEDBACK_TOUCH_CONTACTVISUALIZATION (1) through FEEDBACK_GESTURE_PRESSANDTAP (11).
        int off = 0;
        for (int type = 1; type <= 11; type++)
            try { SetWindowFeedbackSetting(hwnd, type, 0, sizeof(int), ref off); }
            catch (EntryPointNotFoundException) { return; }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint { public int X, Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect { public int Left, Top, Right, Bottom; }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetPointerType(uint pointerId, out uint pointerType);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out NativePoint point);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hwnd, out NativeRect rect);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowFeedbackSetting(IntPtr hwnd, int feedback, uint flags, uint size, ref int configuration);
}
