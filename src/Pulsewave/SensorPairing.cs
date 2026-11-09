using System.Globalization;

namespace Pulsewave;

/// <summary>
/// Sensor Manager's Auto-pair: picks the HWiNFO Gadget reading for each Pulsewave metric from its
/// sensor and label names, checks the reading's unit fits (a fan's "50 %" is not its speed), and
/// works out the scale from that unit (KB/s → MB/s, GB → MB). Ties are left for the user to pick.
/// </summary>
public static class SensorPairing
{
    /// <summary>A pick for one metric: the reading and the scale that turns its unit into Pulsewave's.</summary>
    public sealed record Pick(SensorCatalogEntry Entry, double Scale);

    /// <summary>The unit HWiNFO shows after the number ("1,183 RPM" → "RPM"), lower case; "" when unknown.</summary>
    public static string Unit(SensorCatalogEntry entry)
    {
        string text = (entry.Formatted ?? "").Trim();
        int i = text.Length;
        while (i > 0 && !char.IsDigit(text[i - 1])) i--;
        return text[i..].Trim().ToLowerInvariant();
    }

    /// <summary>The scale for a reading in this unit, or null when the unit doesn't fit the metric. Unknown units pass at 1.</summary>
    public static double? ScaleFor(string metric, SensorCatalogEntry entry)
    {
        string unit = Unit(entry);
        if (unit.Length == 0) return 1;
        return metric switch
        {
            "NetDownload" or "NetUpload" => unit switch
            {
                "kb/s" => 0.001, "mb/s" => 1, "b/s" => 0.000001, "gb/s" => 1000,
                "kbit/s" or "kbps" => 0.000125, "mbit/s" or "mbps" => 0.125, "gbit/s" or "gbps" => 125, _ => null
            },
            "GpuVram" => unit switch { "mb" => 1, "gb" => 1024, _ => null },
            "CpuTemp" or "GpuTemp" or "DriveTemp" => unit == "°c" ? 1 : null, // °F would need an offset, not a scale
            "CpuFan" or "GpuFan" or "PumpRpm" or "CaseFan1" or "CaseFan2" => unit == "rpm" ? 1 : null,
            "CpuPower" or "GpuPower" => unit == "w" ? 1 : null,
            "CpuLoad" or "GpuLoad" or "MemoryLoad" or "GpuVramLoad" or "DriveLoad" => unit == "%" ? 1 : null,
            "GpuFps" => unit == "fps" ? 1 : null,
            "CpuClock" or "GpuClock" => unit switch { "mhz" => 1, "ghz" => 1000, _ => null },
            "CpuVoltage" or "GpuVoltage" => unit switch { "v" => 1, "mv" => 0.001, _ => null },
            _ => 1
        };
    }

    /// <summary>How well a reading fits a metric (higher is better; below 80 is no match).</summary>
    public static int Rank(string metric, SensorCatalogEntry entry, string preferredHive)
    {
        if (!entry.HasValue || ScaleFor(metric, entry) is null) return -1;
        string label = entry.Label.ToLowerInvariant().Trim();
        string group = entry.Group.ToLowerInvariant();
        bool gpu = group.Contains("gpu") || group.Contains("nvidia") || group.Contains("radeon") ||
                   group.Contains("graphics") || group.Contains("geforce");
        bool cpu = !gpu && (group.Contains("cpu") || group.Contains("core ultra") || group.Contains("intel") || group.Contains("ryzen"));
        bool Has(string s) => label.Contains(s);
        int score = metric switch
        {
            // FPS: what reaches the screen first; never the 1% / 0.1% lows or frame times.
            "GpuFps" when Has("low") || Has("frame time") => -1,
            "GpuFps" when Has("framerate displayed") || Has("frame rate displayed") => 115,
            "GpuFps" when label is "framerate" or "frame rate" or "fps" => 110,
            "GpuFps" when Has("framerate") || Has("frame rate") || Has("fps") => 105,
            "GpuVram" when gpu && (Has("dedicated memory used") || Has("gpu memory allocated") ||
                                    Has("memory allocated") || Has("memory used")) => 110,
            "GpuVram" when gpu && (Has("video memory") || Has("vram")) => 95,
            "GpuLoad" when gpu && (Has("gpu core load") || Has("gpu utilization") || Has("gpu load")) => 105,
            "GpuTemp" when gpu && (Has("gpu temperature") || Has("gpu temp")) => 105,
            "GpuPower" when gpu && (Has("total board power") || Has("gpu power")) => 100,
            "CpuLoad" when cpu && (Has("total cpu usage") || Has("total cpu load") || Has("total cpu utility")) => 100,
            "CpuTemp" when cpu && (Has("cpu package temp") || Has("cpu (tctl/tdie)") || label == "cpu package") => 90,
            "CpuPower" when cpu && (Has("cpu package power") || Has("package power")) => 100,
            "CpuFan" when !gpu && Has("cpu fan") => 105,
            // Motherboard fan headers are often just "CPU" (or "CPU_FAN") under the board's monitoring chip.
            "CpuFan" when !gpu && label is "cpu" or "cpu_fan" or "cpu opt" => 100,
            "MemoryLoad" when Has("physical memory load") || Has("memory usage") && !gpu => 90,
            "NetDownload" when Has("download rate") || Has("current dl rate") || Has("total dl") => 100,
            "NetUpload" when Has("upload rate") || Has("current ul rate") || Has("current up rate") => 100,
            "CpuClock" when cpu && (Has("core clocks (avg)") || Has("average effective clock")) => 95,
            "GpuClock" when gpu && (Has("gpu clock") || Has("graphics clock")) => 100,
            "GpuFan" when gpu && Has("fan") => Has("fan1") || label.EndsWith(" fan", StringComparison.Ordinal) ? 96 : 95,
            "GpuVramLoad" when gpu && (Has("gpu memory usage") || Has("memory controller load")) => 95,
            "CpuVoltage" when cpu && (Has("vid") || Has("cpu core voltage") || Has("vcore")) => 90,
            "GpuVoltage" when gpu && Has("gpu core voltage") => 95,
            "PumpRpm" when Has("pump") => 100,
            "DriveTemp" when Has("drive temperature") => 90,
            "DriveLoad" when Has("total activity") => 90,
            "CpuThermalLimit" when cpu && Has("thermal throttling") => Has("package") ? 100 : 85,
            "CpuPowerLimit" when cpu && Has("power limit exceeded") => Has("package") ? 100 : 85,
            "GpuPowerLimit" when gpu && Has("performance limit - power") => 105,
            "GpuThermalLimit" when gpu && Has("performance limit - thermal") => 105,
            "GpuVoltageLimit" when gpu && Has("performance limit") && Has("voltage") => Has("reliability") ? 100 : 90,
            _ => -1
        };
        // HWiNFO can export the same readings to both hives; the one Pulsewave reads wins.
        if (score > 0 && entry.Hive.Equals(preferredHive, StringComparison.OrdinalIgnoreCase)) score += 5;
        return score;
    }

    /// <summary>
    /// The best reading for each metric that has a clear winner (score 80 or more, no tie), skipping metrics
    /// <paramref name="keep"/> says to leave alone (already paired to a live reading).
    /// </summary>
    public static Dictionary<string, Pick> Suggest(IEnumerable<string> metrics, IReadOnlyList<SensorCatalogEntry> catalog, string preferredHive, Func<string, bool> keep)
    {
        var picks = new Dictionary<string, Pick>(StringComparer.OrdinalIgnoreCase);
        foreach (var metric in metrics)
        {
            if (keep(metric)) continue;
            var ranked = catalog.Select(e => (Entry: e, Score: Rank(metric, e, preferredHive))).Where(x => x.Score >= 80)
                .OrderByDescending(x => x.Score).Take(2).ToList();
            if (ranked.Count == 0 || ranked.Count > 1 && ranked[0].Score == ranked[1].Score) continue;
            picks[metric] = new Pick(ranked[0].Entry, ScaleFor(metric, ranked[0].Entry) ?? 1);
        }
        return picks;
    }

    internal static string Describe(Pick pick) =>
        pick.Entry.Label + (pick.Scale == 1 ? "" : " × " + pick.Scale.ToString("G", CultureInfo.InvariantCulture));
}
