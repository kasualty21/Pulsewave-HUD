using System.Globalization;
using System.Text.RegularExpressions;

namespace Pulsewave;

public sealed class SensorSnapshot
{
    public Dictionary<string, double?> Values { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string?> RawTexts { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> SourceGroups { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> Statuses { get; } = new(StringComparer.OrdinalIgnoreCase);
    public int ValidCount { get; set; }
    public int ExpectedCount { get; set; }

    public double? Get(string name) => Values.TryGetValue(name, out var value) ? value : null;
    public string? GetText(string name) => RawTexts.TryGetValue(name, out var value) ? value : null;
}

/// <summary>Sensor ids: the 12 dashboard metrics, and optional extras used by themes.</summary>
public static class SensorIds
{
    public static IReadOnlyList<string> Core => TelemetryEngine.MetricOrder;

    public static readonly (string Id, string Label)[] Theme =
    [
        ("CpuClock", "CPU clock (MHz)"), ("GpuClock", "GPU clock (MHz)"), ("GpuFan", "GPU fan"),
        ("GpuVramLoad", "GPU memory load"), ("CpuVoltage", "CPU voltage"), ("GpuVoltage", "GPU voltage"),
        ("PumpRpm", "Pump"), ("CaseFan1", "Case fan 1"), ("CaseFan2", "Case fan 2"),
        ("DriveTemp", "Drive temperature"), ("DriveLoad", "Drive activity"), ("DriveUsed", "Drive used space"),
        // HWiNFO's Yes/No limit flags (tick them for the gadget in HWiNFO): why clocks and FPS are being held back.
        ("CpuThermalLimit", "CPU thermal throttling"), ("CpuPowerLimit", "CPU power limit"),
        ("GpuThermalLimit", "GPU thermal limit"), ("GpuPowerLimit", "GPU power limit"), ("GpuVoltageLimit", "GPU voltage limit"),
        // RivaTuner Statistics Server's readings through HWiNFO: frame time for the chart, its 1% low for the session.
        ("GpuFrameTime", "Frame time (RTSS, ms)"), ("GpuFpsLow", "FPS 1% low (RTSS)")
    ];

    public static bool IsCore(string id) => Core.Contains(id, StringComparer.OrdinalIgnoreCase);
}

public sealed partial class SensorReader
{
    // Full scans read 4 values for every exported slot in both hives. Between scans only
    // the mapped slots are re-read; any identity change among them forces a scan at once.
    internal const long FullScanIntervalMs = 10000;
    internal const long RetryScanIntervalMs = 2000;

    private readonly MonitorSettings _settings;
    private readonly Func<string, IReadOnlyList<SensorCatalogEntry>> _scan;
    private readonly Func<string, IReadOnlyList<SensorCatalogEntry>, SensorCatalogEntry?[]> _refresh;
    private readonly Func<long> _clockMs;
    private List<SensorCatalogEntry>? _catalog;
    private SensorCatalogEntry[] _used = [];
    private int[] _usedPositions = [];
    private bool _hadMisses;
    private long _lastFullScan;

    public SensorReader(MonitorSettings settings)
        : this(settings, SensorCatalog.Scan, SensorCatalog.Refresh, () => Environment.TickCount64) { }

    internal SensorReader(MonitorSettings settings,
        Func<string, IReadOnlyList<SensorCatalogEntry>> scan,
        Func<string, IReadOnlyList<SensorCatalogEntry>, SensorCatalogEntry?[]> refresh,
        Func<long> clockMs)
    {
        _settings = settings; _scan = scan; _refresh = refresh; _clockMs = clockMs;
    }

    internal int FullScanCount { get; private set; }

    public SensorSnapshot Read()
    {
        // Optional theme sensors count only once mapped, so leaving them unassigned never
        // turns the dashboard header into a partial feed.
        var result = new SensorSnapshot
        {
            ExpectedCount = _settings.Sensors.Count(s => SensorIds.IsCore(s.Key) || !string.IsNullOrWhiteSpace(s.Value.RegistryValue))
        };
        // HWiNFO Gadget exposes group / label / formatted / raw in the VSB key.
        // Name-locked mappings still recover if HWiNFO renumbers slots: see CurrentCatalog.
        var catalog = CurrentCatalog();
        var used = new List<SensorCatalogEntry>();
        bool misses = false;
        foreach (var (sensor, mapping) in _settings.Sensors)
        {
            if (!SensorIds.IsCore(sensor) && string.IsNullOrWhiteSpace(mapping.RegistryValue)) continue;
            var entry = SensorCatalog.Resolve(mapping, _settings.RegistryHive, catalog, out var status);
            if (entry is not null) { if (!used.Contains(entry)) used.Add(entry); }
            else if (!string.IsNullOrWhiteSpace(mapping.RegistryValue)) misses = true;
            result.Statuses[sensor] = status;
            if (entry is not null) result.SourceGroups[sensor] = entry.Group;
            bool preferRaw = mapping.RegistryValue.StartsWith("ValueRaw", StringComparison.OrdinalIgnoreCase);
            string? reading = preferRaw ? entry?.Raw ?? entry?.Formatted
                                        : entry?.Formatted ?? entry?.Raw;
            double? parsed = ParseNumber(reading);
            double? value = parsed.HasValue ? parsed.Value * mapping.Scale : null;
            result.Values[sensor] = value;
            // For VRAM retain the human-readable value and unit (e.g. "9,420 MB").
            result.RawTexts[sensor] = entry?.Formatted ?? reading;
            if (value.HasValue) result.ValidCount++;
        }
        _used = used.ToArray();
        _usedPositions = _used.Select(e => catalog.IndexOf(e)).ToArray();
        _hadMisses = misses;
        return result;
    }

    private List<SensorCatalogEntry> CurrentCatalog()
    {
        long now = _clockMs();
        // Unresolved mappings retry sooner so HWiNFO starting late is picked up quickly.
        // The periodic scan also catches newly added duplicates of a mapped name.
        long interval = _hadMisses ? RetryScanIntervalMs : FullScanIntervalMs;
        if (_catalog is not null && now - _lastFullScan < interval)
        {
            var fresh = _refresh(_settings.RegistryPath, _used);
            bool unchanged = fresh.Length == _used.Length;
            for (int i = 0; unchanged && i < _used.Length; i++)
                unchanged = fresh[i] is { } f && SameIdentity(f, _used[i]);
            if (unchanged)
            {
                for (int i = 0; i < _used.Length; i++) _catalog[_usedPositions[i]] = fresh[i]!;
                return _catalog;
            }
        }
        _catalog = _scan(_settings.RegistryPath).ToList();
        _lastFullScan = now;
        FullScanCount++;
        return _catalog;
    }

    private static bool SameIdentity(SensorCatalogEntry a, SensorCatalogEntry b) =>
        a.Hive == b.Hive && a.Index == b.Index &&
        string.Equals(a.Group, b.Group, StringComparison.Ordinal) &&
        string.Equals(a.Label, b.Label, StringComparison.Ordinal);

    public static double? ParseNumber(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        // HWiNFO shows its flags (throttling, limits) as Yes / No.
        if (text.Trim().Equals("Yes", StringComparison.OrdinalIgnoreCase)) return 1;
        if (text.Trim().Equals("No", StringComparison.OrdinalIgnoreCase)) return 0;
        var match = NumberRegex().Match(text);
        if (!match.Success) return null;
        var numeric = match.Value;
        // Common HWiNFO output conventions: "1,234.5", "1.234,5", "34,5".
        // A bare number without separators is handled directly.
        int comma = numeric.LastIndexOf(',');
        int dot = numeric.LastIndexOf('.');
        if (comma >= 0 && dot >= 0)
        {
            numeric = comma > dot ? numeric.Replace(".", "").Replace(',', '.')
                                  : numeric.Replace(",", "");
        }
        else if (comma >= 0)
        {
            // A lone comma followed by exactly 3 digits is usually a group separator.
            // Raw values are ordinarily plain invariant numbers.
            int digits = numeric.Length - comma - 1;
            numeric = digits == 3 && comma > 0 ? numeric.Replace(",", "") : numeric.Replace(',', '.');
        }
        return double.TryParse(numeric, NumberStyles.Float, CultureInfo.InvariantCulture, out var n)
               && double.IsFinite(n) ? n : null;
    }

    [GeneratedRegex(@"[-+]?\d[\d,.]*", RegexOptions.CultureInvariant)]
    private static partial Regex NumberRegex();


}
