using System.Drawing;
using System.Runtime.InteropServices;

namespace Pulsewave;

/// <summary>
/// Dock mode: the HUD as a Windows app bar (the documented SHAppBarMessage API the taskbar uses),
/// reserving a slice along one edge of a display so maximized windows stop short of it.
/// Coordinates are physical pixels.
/// </summary>
public sealed class AppBar
{
    public static readonly string[] Edges = ["Top", "Bottom", "Left", "Right"];

    private const uint New = 0, Remove = 1, QueryPos = 2, SetPos = 3;
    public const int PositionChanged = 1, FullScreenApp = 2;

    private readonly IntPtr _window;
    private bool _registered;

    /// <summary>The window message the shell sends with app bar notifications (wParam: <see cref="PositionChanged"/>, <see cref="FullScreenApp"/>).</summary>
    public uint CallbackMessage { get; } = RegisterWindowMessage("Pulsewave.AppBar");
    public bool Docked => _registered;

    public AppBar(IntPtr window) => _window = window;

    /// <summary>
    /// How deep the dock is. A percent is a share of the display's short side, so a side dock on a 32:9
    /// ultrawide stays slim. Percent 0 fits the theme: a side dock as wide as the theme is at the
    /// dock's length (its height), a top or bottom dock as tall. At most half the short side.
    /// </summary>
    public static int Thickness(Rectangle display, string edge, int percent, double themeWidth, double themeHeight, int? length = null)
    {
        int shortSide = Math.Min(display.Width, display.Height);
        double thickness;
        if (percent == 0 && themeWidth > 0 && themeHeight > 0)
        {
            bool side = edge is "Left" or "Right";
            double along = length ?? (side ? display.Height : display.Width);
            thickness = side ? along * themeWidth / themeHeight : along * themeHeight / themeWidth;
        }
        else thickness = shortSide * Math.Clamp(percent == 0 ? 22 : percent, 8, 50) / 100.0;
        return (int)Math.Round(Math.Clamp(thickness, 64, Math.Max(64, shortSide / 2.0)));
    }

    /// <summary>The slice of a display an edge and size ask for, before the shell moves it clear of the taskbar.</summary>
    public static Rectangle Slice(Rectangle display, string edge, int percent, double themeWidth = 1920, double themeHeight = 480)
    {
        int thickness = Thickness(display, edge, percent, themeWidth, themeHeight);
        return edge switch
        {
            "Top" => new Rectangle(display.Left, display.Top, display.Width, thickness),
            "Bottom" => new Rectangle(display.Left, display.Bottom - thickness, display.Width, thickness),
            "Left" => new Rectangle(display.Left, display.Top, thickness, display.Height),
            "Right" => new Rectangle(display.Right - thickness, display.Top, thickness, display.Height),
            _ => display
        };
    }

    /// <summary>Reserves the edge slice and returns where the HUD goes (clear of the taskbar and other app bars).</summary>
    public Rectangle Dock(Rectangle display, string edge, int percent, double themeWidth, double themeHeight)
    {
        var data = Data();
        if (!_registered)
        {
            data.CallbackMessage = CallbackMessage;
            _registered = SHAppBarMessage(New, ref data) != UIntPtr.Zero;
            if (!_registered) return Slice(display, edge, percent, themeWidth, themeHeight);
        }
        data = Data();
        data.Edge = edge switch { "Left" => 0u, "Top" => 1u, "Right" => 2u, _ => 3u };
        data.Rect = new NativeRect { Left = display.Left, Top = display.Top, Right = display.Right, Bottom = display.Bottom };
        SHAppBarMessage(QueryPos, ref data);
        // Fitting the theme uses the length left after the taskbar, so the theme fills the dock exactly.
        bool side = edge is "Left" or "Right";
        int thickness = Thickness(display, edge, percent, themeWidth, themeHeight,
            side ? data.Rect.Bottom - data.Rect.Top : data.Rect.Right - data.Rect.Left);
        // The shell moved the far side in for the taskbar; set the thickness from the side it kept.
        switch (edge)
        {
            case "Top": data.Rect.Bottom = data.Rect.Top + thickness; break;
            case "Bottom": data.Rect.Top = data.Rect.Bottom - thickness; break;
            case "Left": data.Rect.Right = data.Rect.Left + thickness; break;
            default: data.Rect.Left = data.Rect.Right - thickness; break;
        }
        SHAppBarMessage(SetPos, ref data);
        return Rectangle.FromLTRB(data.Rect.Left, data.Rect.Top, data.Rect.Right, data.Rect.Bottom);
    }

    /// <summary>Gives the reserved edge back to other windows.</summary>
    public void Undock()
    {
        if (!_registered) return;
        var data = Data();
        SHAppBarMessage(Remove, ref data);
        _registered = false;
    }

    private AppBarData Data() => new() { Size = (uint)Marshal.SizeOf<AppBarData>(), Window = _window };

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct AppBarData
    {
        public uint Size;
        public IntPtr Window;
        public uint CallbackMessage, Edge;
        public NativeRect Rect;
        public IntPtr Param;
    }

    [DllImport("shell32.dll")]
    private static extern UIntPtr SHAppBarMessage(uint message, ref AppBarData data);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern uint RegisterWindowMessage(string name);
}
