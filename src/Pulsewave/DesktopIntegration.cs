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
    public const string ExecutableName = "LumenHUD.exe";
    private const string ValueName = "Lumen HUD";
    // Earlier names of the app's own entry: Pulsewave (0.7.1–0.13) and GraphiteMonitor (before).
    private static readonly string[] LegacyValueNames = ["Pulsewave", "GraphiteMonitor"];

    public static bool Enabled
    {
        get { using var key = Registry.CurrentUser.OpenSubKey(RunKey); return key?.GetValue(ValueName) != null || LegacyValueNames.Any(n => key?.GetValue(n) != null); }
    }

    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (!enabled) { key.DeleteValue(ValueName, false); foreach (var n in LegacyValueNames) key.DeleteValue(n, false); return; }
        var executable = Environment.ProcessPath ?? throw new InvalidOperationException("Cannot locate the executable.");
        if (!System.IO.Path.GetFileName(executable).Equals(ExecutableName, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Publish Lumen HUD first, then enable startup from " + ExecutableName + ".");
        key.SetValue(ValueName, $"\"{executable}\" --startup");
        foreach (var n in LegacyValueNames) key.DeleteValue(n, false);
    }

    /// <summary>
    /// After the rename: when Pulsewave was set to launch with Windows, this copy takes its place (the old entry would
    /// start the old app). Only from a published LumenHUD.exe; returns whether it changed anything.
    /// </summary>
    public static bool TakeOverPulsewaveEntry()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
        if (key?.GetValue("Pulsewave") == null || key.GetValue(ValueName) != null) return false;
        if (!System.IO.Path.GetFileName(Environment.ProcessPath ?? "").Equals(ExecutableName, StringComparison.OrdinalIgnoreCase)) return false;
        SetEnabled(true);
        return true;
    }
}
