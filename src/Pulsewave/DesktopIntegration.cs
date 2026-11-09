using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace Pulsewave;

internal static class DisplayIdentity
{
    // Public Windows monitor interface path, independent of DISPLAY1/2 numbering.
    public static string For(Forms.Screen screen)
    {
        var device = new DisplayDevice { Size = Marshal.SizeOf<DisplayDevice>() };
        return EnumDisplayDevices(screen.DeviceName, 0, ref device, 1) ? device.Id ?? "" : "";
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DisplayDevice
    {
        public int Size;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string Name;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Description;
        public int Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Id;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Key;
    }

    [DllImport("user32.dll", EntryPoint = "EnumDisplayDevicesW", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumDisplayDevices(string name, int index, ref DisplayDevice device, uint flags);
}

internal static class WindowsStartup
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Pulsewave";
    private const string LegacyValueName = "GraphiteMonitor";

    public static bool Enabled
    {
        get { using var key = Registry.CurrentUser.OpenSubKey(RunKey); return key?.GetValue(ValueName) != null || key?.GetValue(LegacyValueName) != null; }
    }

    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (!enabled) { key.DeleteValue(ValueName, false); key.DeleteValue(LegacyValueName, false); return; }
        var executable = Environment.ProcessPath ?? throw new InvalidOperationException("Cannot locate the executable.");
        if (!System.IO.Path.GetFileName(executable).Equals("Pulsewave.exe", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Publish Pulsewave first, then enable startup from Pulsewave.exe.");
        key.SetValue(ValueName, $"\"{executable}\" --startup");
        key.DeleteValue(LegacyValueName, false);
    }
}
