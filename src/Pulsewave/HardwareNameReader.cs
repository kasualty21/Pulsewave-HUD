using System.Runtime.InteropServices;
using System.IO;
using System.Threading;
using Microsoft.Win32;

namespace Pulsewave;

/// <summary>
/// Resolves display names from the same HWiNFO source selected for each metric,
/// then falls back to public Windows hardware information APIs.
/// </summary>
internal static class HardwareNameReader
{
    private const string DefaultCpuName = "Intel Core Ultra 7 265K";
    private const string DefaultGpuName = "NVIDIA GeForce RTX 5070 Ti";
    private static int _gpuDiscoveryStarted;
    private static bool _gpuDiscoveryFinished;
    private static string? _installedGpuName;
    // The processor name cannot change while running; avoid a registry read every poll.
    private static readonly Lazy<string?> CpuNameFromRegistry = new(ReadCpuName);

    public static string Cpu(SensorSnapshot snapshot, MonitorSettings settings) =>
        FromSensorGroup(snapshot, "CpuLoad", "CPU") ?? CpuNameFromRegistry.Value ??
        CustomFallback(settings.CpuName, DefaultCpuName, "CPU");

    public static string Gpu(SensorSnapshot snapshot, MonitorSettings settings)
    {
        string? sourceName = FromSensorGroup(snapshot, "GpuLoad", "GPU");
        if (sourceName is not null) return sourceName;
        StartInstalledGpuDiscovery();
        string? installedName = Volatile.Read(ref _gpuDiscoveryFinished)
            ? Volatile.Read(ref _installedGpuName) : null;
        return installedName ?? ReadActiveDisplayAdapter() ??
            CustomFallback(settings.GpuName, DefaultGpuName, "GPU");
    }

    /// <summary>
    /// A GPU name without the vendor's brand words, for themes' small headers: "NVIDIA GeForce RTX 5070 Ti" is
    /// "RTX 5070 Ti", "AMD Radeon RX 7900 XTX" is "RX 7900 XTX", "Intel(R) Arc(TM) A770" is "Arc A770".
    /// </summary>
    public static string Short(string? name)
    {
        string n = (name ?? "").Replace("(R)", "", StringComparison.OrdinalIgnoreCase).Replace("(TM)", "", StringComparison.OrdinalIgnoreCase).Trim();
        foreach (var brand in new[] { "NVIDIA GeForce ", "NVIDIA ", "AMD Radeon ", "AMD ", "Intel " })
            if (n.StartsWith(brand, StringComparison.OrdinalIgnoreCase) && n.Length > brand.Length + 2) { n = n[brand.Length..]; break; }
        return string.Join(' ', n.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static string? FromSensorGroup(SensorSnapshot snapshot, string metric, string kind)
    {
        if (!snapshot.SourceGroups.TryGetValue(metric, out var group)) return null;
        group = group.Trim();
        // HWiNFO commonly formats these as "GPU [#0]: Vendor Model". Strip the
        // sensor-family prefix so the 8-inch dashboard shows the actual model.
        int separator = group.IndexOf(':');
        if (separator >= 0) group = group[(separator + 1)..].Trim();
        // HWiNFO can add the board partner's full name after another colon
        // ("NVIDIA GeForce RTX 5070 Ti: GIGABYTE GeForce RTX 5070 Ti AERO OC 16G"): the model is enough.
        separator = group.IndexOf(':');
        if (separator > 0) group = group[..separator].Trim();
        if (group.Length == 0 || group.Equals(kind, StringComparison.OrdinalIgnoreCase) ||
            group.StartsWith(kind + " [", StringComparison.OrdinalIgnoreCase) ||
            kind == "GPU" && IsRemoteOrVirtual(group)) return null;
        return group;
    }

    private static string? ReadCpuName()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(
                @"HARDWARE\DESCRIPTION\System\CentralProcessor\0", writable: false);
            return Clean(Convert.ToString(key?.GetValue("ProcessorNameString")));
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            return null;
        }
    }

    private static string? ReadActiveDisplayAdapter()
    {
        try
        {
            var adapters = new List<(string Name, bool Primary, bool Attached)>();
            for (uint index = 0; index < 32; index++)
            {
                var device = new DisplayDevice { Size = Marshal.SizeOf<DisplayDevice>() };
                if (!EnumDisplayDevices(null, index, ref device, 0)) break;
                const int attachedToDesktop = 0x00000001;
                const int mirroringDriver = 0x00000008;
                if ((device.Flags & mirroringDriver) != 0) continue;
                string? name = Clean(device.Description);
                if (name is null || IsRemoteOrVirtual(name) ||
                    name.Equals("Microsoft Basic Display Adapter", StringComparison.OrdinalIgnoreCase)) continue;
                adapters.Add((name, (device.Flags & 0x00000004) != 0,
                    (device.Flags & attachedToDesktop) != 0));
            }
            return adapters.OrderByDescending(adapter => adapter.Attached)
                .ThenByDescending(adapter => adapter.Primary)
                .Select(adapter => adapter.Name)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or SEHException)
        {
            return null;
        }
    }

    private static void StartInstalledGpuDiscovery()
    {
        if (Interlocked.CompareExchange(ref _gpuDiscoveryStarted, 1, 0) != 0) return;
        _ = Task.Run(() =>
        {
            try { Volatile.Write(ref _installedGpuName, ReadInstalledGpuFromWmi()); }
            catch { Volatile.Write(ref _installedGpuName, null); }
            finally { Volatile.Write(ref _gpuDiscoveryFinished, true); }
        });
    }

    // WMI's documented Win32_VideoController inventory includes installed
    // adapters when Remote Desktop replaces the active display device. Query
    // asynchronously once so a slow or unavailable WMI provider never delays
    // the dashboard's telemetry refresh.
    private static string? ReadInstalledGpuFromWmi()
    {
        object? locator = null;
        object? services = null;
        object? controllers = null;
        var candidates = new List<(string Name, bool Pci, int VendorRank)>();
        try
        {
            var locatorType = Type.GetTypeFromProgID("WbemScripting.SWbemLocator", throwOnError: true)!;
            locator = Activator.CreateInstance(locatorType);
            if (locator is null) return null;
            dynamic locatorDispatch = locator;
            services = locatorDispatch.ConnectServer(".", @"root\CIMV2");
            dynamic serviceDispatch = services!;
            controllers = serviceDispatch.ExecQuery("SELECT Name, PNPDeviceID FROM Win32_VideoController");
            foreach (dynamic controller in (System.Collections.IEnumerable)controllers!)
            {
                // SWbemObject's WMI properties are exposed through Properties_
                // rather than ordinary CLR properties on the COM dispatch.
                dynamic properties = controller.Properties_;
                string? name = Clean(Convert.ToString(properties.Item("Name").Value));
                string? deviceId = Clean(Convert.ToString(properties.Item("PNPDeviceID").Value));
                if (name is null || IsRemoteOrVirtual(name) ||
                    name.Equals("Microsoft Basic Display Adapter", StringComparison.OrdinalIgnoreCase)) continue;
                bool pci = deviceId?.StartsWith("PCI\\", StringComparison.OrdinalIgnoreCase) == true;
                int vendorRank = name.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase) ||
                                 name.Contains("GeForce", StringComparison.OrdinalIgnoreCase) ||
                                 name.Contains("AMD", StringComparison.OrdinalIgnoreCase) ||
                                 name.Contains("Radeon", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
                candidates.Add((name, pci, vendorRank));
            }
        }
        catch { return null; }
        finally
        {
            ReleaseCom(controllers);
            ReleaseCom(services);
            ReleaseCom(locator);
        }
        return candidates.OrderByDescending(candidate => candidate.Pci)
            .ThenByDescending(candidate => candidate.VendorRank)
            .Select(candidate => candidate.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    private static void ReleaseCom(object? value)
    {
        if (value is not null && Marshal.IsComObject(value))
        {
            try { Marshal.FinalReleaseComObject(value); }
            catch { }
        }
    }

    private static string CustomFallback(string? configured, string builtInDefault, string generic) =>
        !string.IsNullOrWhiteSpace(configured) &&
        !configured.Equals(builtInDefault, StringComparison.OrdinalIgnoreCase)
            ? configured.Trim() : generic;

    private static string? Clean(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        string cleaned = value.Trim();
        return cleaned.Length == 0 ? null : cleaned;
    }

    private static bool IsRemoteOrVirtual(string name) =>
        name.Contains("remote", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("RDP", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("indirect display", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("virtual display", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("virtual monitor", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("Parsec", StringComparison.OrdinalIgnoreCase);

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
    private static extern bool EnumDisplayDevices(string? device, uint index, ref DisplayDevice displayDevice, uint flags);
}
