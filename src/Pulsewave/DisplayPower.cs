using System.Runtime.InteropServices;

namespace Pulsewave;

/// <summary>A display's DDC/CI power support, as read from it.</summary>
internal sealed record DisplayPowerInfo(string DeviceName, string Description, bool Ddc, uint? PowerMode, string Note);

/// <summary>
/// Powers a display down and up over DDC/CI with Windows' Monitor Configuration API (dxva2):
/// VCP code 0xD6 (power mode), 1 = on, 4 = off (DPM off), 5 = off (power button). Many displays,
/// especially USB-C panels, don't support DDC/CI; those report Ddc = false and are left alone.
/// </summary>
internal static class DisplayPower
{
    private const byte PowerModeCode = 0xD6;
    public const uint On = 1, Standby = 4, Off = 5;
    private static readonly object Gate = new(); // one DDC/CI conversation at a time

    /// <summary>Reads (never changes) every display's power-mode support.</summary>
    public static List<DisplayPowerInfo> Probe()
    {
        var found = new List<DisplayPowerInfo>();
        foreach (var (monitor, device) in Monitors())
            WithPhysical(monitor, (handle, description) =>
            {
                bool ok = GetVCPFeatureAndVCPFeatureReply(handle, PowerModeCode, out _, out uint current, out uint maximum);
                found.Add(new DisplayPowerInfo(device, description, ok, ok ? current : null,
                    ok ? $"power mode {current} (max {maximum})" : $"no DDC/CI power control (error {Marshal.GetLastWin32Error()})"));
            });
        return found;
    }

    /// <summary>Sets the power mode of the display with this device name (\\.\DISPLAY2); false when it can't.</summary>
    public static bool Set(string deviceName, uint mode)
    {
        lock (Gate)
        {
            bool done = false;
            foreach (var (monitor, device) in Monitors())
                if (string.Equals(device, deviceName, StringComparison.OrdinalIgnoreCase))
                    WithPhysical(monitor, (handle, _) => done |= SetVCPFeature(handle, PowerModeCode, mode));
            return done;
        }
    }

    /// <summary>
    /// Turns the display back on and reads it back, sending again until it says it is on: a display
    /// waking from standby may miss the first command or not answer for a moment.
    /// </summary>
    public static bool TurnOn(string deviceName)
    {
        for (int attempt = 0; attempt < 6; attempt++)
        {
            Set(deviceName, On);
            Thread.Sleep(1500);
            if (Mode(deviceName) == On) return true;
        }
        return false;
    }

    /// <summary>The display's power mode (1 = on), or null when it doesn't answer.</summary>
    public static uint? Mode(string deviceName)
    {
        lock (Gate)
        {
            uint? mode = null;
            foreach (var (monitor, device) in Monitors())
                if (string.Equals(device, deviceName, StringComparison.OrdinalIgnoreCase))
                    WithPhysical(monitor, (handle, description) =>
                    {
                        if (GetVCPFeatureAndVCPFeatureReply(handle, PowerModeCode, out _, out uint current, out _)) mode = current;
                    });
            return mode;
        }
    }

    private const byte BrightnessCode = 0x10;

    /// <summary>The display's brightness (VCP 0x10) and its maximum, or null when it doesn't answer.</summary>
    public static (uint Current, uint Maximum)? Brightness(string deviceName)
    {
        lock (Gate)
        {
            (uint, uint)? found = null;
            foreach (var (monitor, device) in Monitors())
                if (string.Equals(device, deviceName, StringComparison.OrdinalIgnoreCase))
                    WithPhysical(monitor, (handle, description) =>
                    {
                        if (GetVCPFeatureAndVCPFeatureReply(handle, BrightnessCode, out _, out uint current, out uint maximum) && maximum > 0) found = (current, maximum);
                    });
            return found;
        }
    }

    /// <summary>Sets the display's brightness (VCP 0x10); false when it can't.</summary>
    public static bool SetBrightness(string deviceName, uint value)
    {
        lock (Gate)
        {
            bool done = false;
            foreach (var (monitor, device) in Monitors())
                if (string.Equals(device, deviceName, StringComparison.OrdinalIgnoreCase))
                    WithPhysical(monitor, (handle, _) => done |= SetVCPFeature(handle, BrightnessCode, value));
            return done;
        }
    }

    private static List<(IntPtr Monitor, string Device)> Monitors()
    {
        var list = new List<(IntPtr, string)>();
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (monitor, _, _, _) =>
        {
            var info = new MonitorInfoEx { Size = Marshal.SizeOf<MonitorInfoEx>() };
            if (GetMonitorInfo(monitor, ref info)) list.Add((monitor, info.Device));
            return true;
        }, IntPtr.Zero);
        return list;
    }

    private static void WithPhysical(IntPtr monitor, Action<IntPtr, string> use)
    {
        if (!GetNumberOfPhysicalMonitorsFromHMONITOR(monitor, out uint count) || count == 0 || count > 16) return;
        var physical = new PhysicalMonitor[count];
        if (!GetPhysicalMonitorsFromHMONITOR(monitor, count, physical)) return;
        try { foreach (var p in physical) use(p.Handle, p.Description); }
        finally { DestroyPhysicalMonitors(count, physical); }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct PhysicalMonitor
    {
        public IntPtr Handle;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Description;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MonitorInfoEx
    {
        public int Size;
        public NativeRect Monitor, Work;
        public uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string Device;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect { public int Left, Top, Right, Bottom; }

    private delegate bool MonitorEnum(IntPtr monitor, IntPtr dc, IntPtr rect, IntPtr data);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumDisplayMonitors(IntPtr dc, IntPtr clip, MonitorEnum callback, IntPtr data);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetMonitorInfoW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfoEx info);

    [DllImport("dxva2.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetNumberOfPhysicalMonitorsFromHMONITOR(IntPtr monitor, out uint count);

    [DllImport("dxva2.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetPhysicalMonitorsFromHMONITOR(IntPtr monitor, uint count, [Out] PhysicalMonitor[] monitors);

    [DllImport("dxva2.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyPhysicalMonitors(uint count, PhysicalMonitor[] monitors);

    [DllImport("dxva2.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetVCPFeatureAndVCPFeatureReply(IntPtr monitor, byte code, out uint type, out uint current, out uint maximum);

    [DllImport("dxva2.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetVCPFeature(IntPtr monitor, byte code, uint value);
}
