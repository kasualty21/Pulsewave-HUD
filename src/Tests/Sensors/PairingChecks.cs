namespace Pulsewave.Regression;

// Sensor Manager's Auto-pair, against the HWiNFO Gadget readings exported on the user's PC
// (Core Ultra 7 265K, ASUS Z890, RTX 5070 Ti, RTSS and PresentMon), in both hives as HWiNFO writes them.
internal static class PairingChecks
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("SENSORS: " + message);
        Console.WriteLine("PASS SENSORS: " + message);
    }

    private const string Gpu = "dGPU [#2]: NVIDIA GeForce RTX 5070 Ti: GIGABYTE GeForce RTX 5070 Ti AERO OC 16G (GV-N507TAERO OC-16GD)";
    private const string Net = "Network: RealTek Semiconductor RTL8125 Gaming 2.5GbE Family Ethernet Controller - Ethernet 3";

    private static readonly (int Index, string Group, string Label, string Raw, string Formatted)[] ThisPc =
    [
        (0, "System: ASUS ", "Physical Memory Load", "67.4", "67.4 %"),
        (1, "CPU [#0]: Intel Core Ultra 7 265K", "Total CPU Utility", "30.9", "30.9 %"),
        (2, "CPU [#0]: Intel Core Ultra 7 265K: DTS", "CPU Package", "43", "43 °C"),
        (3, "CPU [#0]: Intel Core Ultra 7 265K: Enhanced", "CPU Package Power", "57.212", "57.212 W"),
        (4, "ASUS Z890 AYW GAMING WIFI W (Nuvoton NCT6701D)", "CPU", "1183", "1,183 RPM"),
        (8, Gpu, "GPU Temperature", "35.8", "35.8 °C"),
        (9, Gpu, "GPU Fan1", "1571", "1,571 RPM"), (10, Gpu, "GPU Fan2", "1570", "1,570 RPM"), (11, Gpu, "GPU Fan3", "1571", "1,571 RPM"),
        (12, Gpu, "GPU Power", "88.136", "88.136 W"),
        (13, Gpu, "GPU Core Load", "37.0", "37.0 %"),
        (14, Gpu, "GPU Memory Usage", "29.3", "29.3 %"),
        (15, Gpu, "GPU Fan1", "50", "50 %"), (16, Gpu, "GPU Fan2", "50", "50 %"), (17, Gpu, "GPU Fan3", "50", "50 %"),
        (18, Gpu, "GPU Memory Allocated", "4779", "4,779 MB"),
        (19, "RTSS", "Framerate", "0", "0 FPS"), (20, "RTSS", "Framerate 1% Low", "0", "0 FPS"), (21, "RTSS", "Framerate 0.1% Low", "0", "0 FPS"),
        (22, "RTSS", "Frame Time", "6.5", "6.5 ms"),
        (23, "PresentMon [game.exe]", "Framerate Presented (avg)", "231.9", "231.9 FPS"),
        (24, "PresentMon [game.exe]", "Framerate Displayed (avg)", "140.1", "140.1 FPS"),
        (25, Net, "Current DL rate", "2.7", "2.7 KB/s"),
        (26, Net, "Current UP rate", "12.3", "12.3 KB/s"),
    ];

    public static void Run()
    {
        var catalog = new[] { "CurrentUser", "LocalMachine" }.SelectMany(hive => ThisPc.Select(r =>
            new SensorCatalogEntry { Hive = hive, Index = r.Index, Group = r.Group, Label = r.Label, Raw = r.Raw, Formatted = r.Formatted })).ToList();
        string[] dashboard = ["CpuLoad", "CpuTemp", "CpuPower", "CpuFan", "GpuLoad", "GpuTemp", "GpuPower", "GpuFps", "GpuVram", "MemoryLoad", "NetDownload", "NetUpload"];

        var picks = SensorPairing.Suggest(dashboard.Append("GpuFan").Append("GpuVramLoad").Append("CpuThermalLimit"), catalog, "CurrentUser", _ => false);
        string Of(string id) => picks.TryGetValue(id, out var p) ? $"{p.Entry.Hive}/{p.Entry.Label}/{p.Scale}" : "-";
        Check(dashboard.All(picks.ContainsKey) && picks.Values.All(p => p.Entry.Hive == "CurrentUser") &&
              Of("CpuLoad") == "CurrentUser/Total CPU Utility/1" && Of("CpuFan") == "CurrentUser/CPU/1" &&
              Of("GpuFps") == "CurrentUser/Framerate Displayed (avg)/1" && Of("GpuVram") == "CurrentUser/GPU Memory Allocated/1" &&
              Of("NetDownload") == "CurrentUser/Current DL rate/0.001" && Of("NetUpload") == "CurrentUser/Current UP rate/0.001" &&
              Of("GpuFan") == "CurrentUser/GPU Fan1/1" && Of("GpuVramLoad") == "CurrentUser/GPU Memory Usage/1" && Of("CpuThermalLimit") == "-",
            "Auto-pair pairs all 12 dashboard readings on this PC's HWiNFO export: \"Total CPU Utility\", the board's \"CPU\" fan header, " +
            "PresentMon's displayed FPS, KB/s rates scaled to MB/s, the hive Pulsewave reads, and nothing for sensors HWiNFO doesn't export");

        var kept = SensorPairing.Suggest(dashboard, catalog, "CurrentUser", id => id != "CpuTemp");
        var unit = new SensorCatalogEntry { Hive = "CurrentUser", Group = Gpu, Label = "GPU Memory Allocated", Raw = "4.7", Formatted = "4.7 GB" };
        var fahrenheit = new SensorCatalogEntry { Hive = "CurrentUser", Group = Gpu, Label = "GPU Temperature", Raw = "96", Formatted = "96 °F" };
        var tie = new List<SensorCatalogEntry> { new() { Hive = "CurrentUser", Index = 1, Group = "CPU [#0]: A", Label = "CPU Package Power", Formatted = "50 W" },
                                                 new() { Hive = "CurrentUser", Index = 2, Group = "CPU [#1]: B", Label = "CPU Package Power", Formatted = "40 W" } };
        Check(kept.Keys.SequenceEqual(["CpuTemp"]) && SensorPairing.ScaleFor("GpuVram", unit) == 1024 && SensorPairing.Rank("GpuTemp", fahrenheit, "CurrentUser") < 0 &&
              SensorPairing.Rank("GpuFan", catalog.First(e => e.Index == 15), "CurrentUser") < 0 && !SensorPairing.Suggest(["CpuPower"], tie, "CurrentUser", _ => false).Any() &&
              SensorPairing.Unit(catalog[4]) == "rpm",
            "Auto-pair leaves live pairings alone, scales GB to MB, refuses readings in the wrong unit (°F, a fan's %), and leaves ties to the user");
    }
}
