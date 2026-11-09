using Microsoft.Win32;
using System.Globalization;
using System.Security;

namespace Pulsewave;

/// <summary>
/// Lists only sensors that HWiNFO has exported to its Gadget/VSB registry.
/// Source identity is (hive, SensorN, LabelN), not the volatile slot index N.
/// </summary>
public sealed class SensorCatalogEntry
{
    public string Hive { get; init; } = "";
    public int Index { get; init; }
    public string Group { get; init; } = "";
    public string Label { get; init; } = "";
    public string? Raw { get; init; }
    public string? Formatted { get; init; }

    public string IndexRaw => "ValueRaw" + Index.ToString(CultureInfo.InvariantCulture);
    public string IndexFormatted => "Value" + Index.ToString(CultureInfo.InvariantCulture);
    public bool HasValue => !string.IsNullOrWhiteSpace(Raw) || !string.IsNullOrWhiteSpace(Formatted);
    public string Reading => Formatted ?? Raw ?? "--";
    public string Name => $"{(string.IsNullOrWhiteSpace(Group) ? "Unknown source" : Group)} / " +
                          (string.IsNullOrWhiteSpace(Label) ? $"Slot {Index}" : Label);
}

public static class SensorCatalog
{
    private static readonly string[] Hives = ["CurrentUser", "LocalMachine"];

    public static IReadOnlyList<SensorCatalogEntry> Scan(string path)
    {
        var entries = new List<SensorCatalogEntry>();
        foreach (var hiveName in Hives)
        {
            try
            {
                var hive = hiveName == "CurrentUser" ? RegistryHive.CurrentUser : RegistryHive.LocalMachine;
                using var root = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
                using var key = root.OpenSubKey(path, writable: false);
                if (key is null) continue;

                var indices = new SortedSet<int>();
                foreach (var name in key.GetValueNames())
                {
                    string? tail = null;
                    foreach (var prefix in new[] { "ValueRaw", "Value", "Sensor", "Label" })
                    {
                        if (!name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
                        tail = name[prefix.Length..];
                        break;
                    }
                    if (tail is not null && int.TryParse(tail, NumberStyles.None,
                        CultureInfo.InvariantCulture, out var index) && index >= 0 && index < 100000)
                        indices.Add(index);
                }

                foreach (int index in indices)
                    if (ReadEntry(key, hiveName, index) is { } entry) entries.Add(entry);
            }
            // A non-administrator account may not have access to all exported hives.
            // HWiNFO can also rewrite this key between enumeration and reading.
            catch (Exception ex) when (ex is UnauthorizedAccessException or SecurityException
                                      or System.IO.IOException or ArgumentException)
            {
                // The other hive can still be used; an offline reading is not faked.
            }
        }
        return entries;
    }

    /// <summary>
    /// Re-reads only the given slots, opening each hive once. A slot that no longer
    /// exists, or whose hive cannot be read, is returned as null.
    /// </summary>
    public static SensorCatalogEntry?[] Refresh(string path, IReadOnlyList<SensorCatalogEntry> slots)
    {
        var fresh = new SensorCatalogEntry?[slots.Count];
        foreach (var hiveName in Hives)
        {
            if (!slots.Any(s => s.Hive == hiveName)) continue;
            try
            {
                var hive = hiveName == "CurrentUser" ? RegistryHive.CurrentUser : RegistryHive.LocalMachine;
                using var root = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
                using var key = root.OpenSubKey(path, writable: false);
                if (key is null) continue;
                for (int i = 0; i < slots.Count; i++)
                    if (slots[i].Hive == hiveName) fresh[i] = ReadEntry(key, hiveName, slots[i].Index);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or SecurityException
                                      or System.IO.IOException or ArgumentException)
            {
                // Leave this hive's slots null; the caller falls back to a full scan.
            }
        }
        return fresh;
    }

    private static SensorCatalogEntry? ReadEntry(RegistryKey key, string hiveName, int index)
    {
        var sensor = Read(key, "Sensor" + index)?.Trim() ?? "";
        var label = Read(key, "Label" + index)?.Trim() ?? "";
        var raw = Read(key, "ValueRaw" + index);
        var formatted = Read(key, "Value" + index);
        if (sensor.Length == 0 && label.Length == 0 && raw is null && formatted is null)
            return null;
        return new SensorCatalogEntry
        {
            Hive = hiveName, Index = index, Group = sensor, Label = label,
            Raw = raw, Formatted = formatted
        };
    }

    private static string? Read(RegistryKey key, string name)
    {
        try { return Convert.ToString(key.GetValue(name), CultureInfo.InvariantCulture); }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException)
        { return null; }
    }

    public static SensorCatalogEntry? Resolve(SensorMapping mapping, string defaultHive,
        IReadOnlyList<SensorCatalogEntry> catalog, out string status)
    {
        if (string.IsNullOrWhiteSpace(mapping.RegistryValue))
        {
            status = "Unassigned";
            return null;
        }
        var hive = string.IsNullOrWhiteSpace(mapping.RegistryHive) ? defaultHive : mapping.RegistryHive;
        IEnumerable<SensorCatalogEntry> matches = catalog.Where(e =>
            e.Hive.Equals(hive, StringComparison.OrdinalIgnoreCase));

        // Game-specific HWiNFO FPS source titles can change while the registry
        // slot remains constant. Explicit Slot mappings never enforce a name.
        if (string.Equals(mapping.MatchMode, "Slot", StringComparison.OrdinalIgnoreCase))
        {
            var slot = matches.FirstOrDefault(e =>
                e.IndexRaw.Equals(mapping.RegistryValue, StringComparison.OrdinalIgnoreCase) ||
                e.IndexFormatted.Equals(mapping.RegistryValue, StringComparison.OrdinalIgnoreCase));
            status = slot is null ? "Slot unavailable" : slot.HasValue
                ? $"Connected by slot #{slot.Index}" : "Slot not reporting";
            return slot;
        }

        if (!string.IsNullOrWhiteSpace(mapping.SensorGroup) &&
            !string.IsNullOrWhiteSpace(mapping.SensorLabel))
        {
            var identified = matches.Where(e =>
                e.Group.Equals(mapping.SensorGroup, StringComparison.OrdinalIgnoreCase) &&
                e.Label.Equals(mapping.SensorLabel, StringComparison.OrdinalIgnoreCase)).ToList();
            if (identified.Count == 0)
            {
                status = "Named sensor not found";
                return null; // Do not fall back to an index that may now mean another sensor.
            }
            if (identified.Count > 1)
            {
                status = "Duplicate name: remap manually";
                return null; // Same group + label is not a unique identity.
            }
            status = identified[0].HasValue ? "Connected by name" : "Sensor not reporting";
            return identified[0];
        }
        // Legacy V0.1 mapping: retain its exact slot until the user chooses a name.
        var legacy = matches.FirstOrDefault(e =>
            e.IndexRaw.Equals(mapping.RegistryValue, StringComparison.OrdinalIgnoreCase) ||
            e.IndexFormatted.Equals(mapping.RegistryValue, StringComparison.OrdinalIgnoreCase));
        status = legacy is null ? "Legacy slot unavailable" : "Legacy index (select to lock name)";
        return legacy;
    }
}
