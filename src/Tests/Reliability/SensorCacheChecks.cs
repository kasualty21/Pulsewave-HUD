namespace Pulsewave.Regression;

// Verifies the reader's mapped-slot cache never reports a value from a slot whose
// identity changed, and still performs full scans on schedule.
internal static class SensorCacheChecks
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("CACHE: " + message);
        Console.WriteLine("PASS CACHE: " + message);
    }

    private static SensorCatalogEntry Entry(int index, string group, string label, string raw) =>
        new() { Hive = "CurrentUser", Index = index, Group = group, Label = label, Raw = raw };

    private static MonitorSettings Settings()
    {
        var settings = new MonitorSettings();
        settings.Sensors.Clear();
        settings.Sensors["CpuTemp"] = new SensorMapping
        {
            RegistryValue = "ValueRaw2", MatchMode = "Name", RegistryHive = "CurrentUser",
            SensorGroup = "CPU [#0]", SensorLabel = "CPU Package"
        };
        return settings;
    }

    public static void Run()
    {
        long clock = 0;
        // The "registry": slot contents the fake scan/refresh both read from.
        var slots = new Dictionary<int, SensorCatalogEntry> { [2] = Entry(2, "CPU [#0]", "CPU Package", "50") };
        int refreshes = 0;
        IReadOnlyList<SensorCatalogEntry> Scan(string _) => slots.Values.ToList();
        SensorCatalogEntry?[] Refresh(string _, IReadOnlyList<SensorCatalogEntry> wanted)
        {
            refreshes++;
            return wanted.Select(w => slots.GetValueOrDefault(w.Index)).ToArray();
        }
        var reader = new SensorReader(Settings(), Scan, Refresh, () => clock);

        Check(reader.Read().Get("CpuTemp") == 50 && reader.FullScanCount == 1, "First read performs a full scan");
        slots[2] = Entry(2, "CPU [#0]", "CPU Package", "61");
        clock += 1000;
        Check(reader.Read().Get("CpuTemp") == 61 && reader.FullScanCount == 1 && refreshes == 1,
            "Between scans the mapped slot is re-read without a full scan");

        // HWiNFO renumbers: slot 2 now means another sensor, the named one moved to 7.
        slots[2] = Entry(2, "GPU [#0]", "GPU Temperature", "40");
        slots[7] = Entry(7, "CPU [#0]", "CPU Package", "63");
        clock += 1000;
        var moved = reader.Read();
        Check(moved.Get("CpuTemp") == 63 && reader.FullScanCount == 2,
            "Identity change on a mapped slot forces an immediate rescan and follows the name");

        clock += SensorReader.FullScanIntervalMs;
        reader.Read();
        Check(reader.FullScanCount == 3, "A full scan still runs on the periodic interval");

        // Sensor disappears: missing mappings retry on the shorter interval.
        slots.Clear();
        clock += 1000;
        Check(reader.Read().Get("CpuTemp") == null && reader.FullScanCount == 4,
            "Vanished mapped slot forces a rescan and reports missing, not a cached value");
        clock += SensorReader.RetryScanIntervalMs - 1;
        reader.Read();
        Check(reader.FullScanCount == 4, "Missing mapping does not rescan before the retry interval");
        slots[7] = Entry(7, "CPU [#0]", "CPU Package", "55");
        clock += 1;
        Check(reader.Read().Get("CpuTemp") == 55 && reader.FullScanCount == 5,
            "Missing mapping recovers at the retry interval");
    }
}
