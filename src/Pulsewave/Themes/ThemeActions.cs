namespace Pulsewave.Themes;

/// <summary>Which action a tap on a theme element runs, and which sensor its details page opens on.</summary>
public static class ThemeActions
{
    /// <summary>
    /// The element's own action, or by default: album art and now-playing values play/pause,
    /// sensor readouts (values, bars, gauges, charts, hardware names) open their details,
    /// shortcut tiles and their names launch the shortcut.
    /// </summary>
    public static ThemeAction Effective(ThemeElement element) => element.Action ?? Automatic(element);

    /// <summary>What an element does when its action is left automatic.</summary>
    public static ThemeAction Automatic(ThemeElement element)
    {
        if (element.Kind == ThemeElementKind.Shortcut ||
            element.Kind == ThemeElementKind.Data && string.Equals(element.DataName, "APP_NAME", StringComparison.OrdinalIgnoreCase))
            return ThemeAction.Launch;
        if (element.Kind == ThemeElementKind.Data)
            switch (element.DataName?.ToUpperInvariant())
            {
                case "VOLUME" or "VOLUME_ICON": return ThemeAction.Mute;
                case "TIMER": return ThemeAction.TimerToggle;
                case "STOPWATCH": return ThemeAction.StopwatchToggle;
            }
        if (element.Kind == ThemeElementKind.AlbumArt) return ThemeAction.PlayPause;
        if (element.Kind == ThemeElementKind.Data && element.DataName?.StartsWith("MEDIA_", StringComparison.OrdinalIgnoreCase) == true)
            return ThemeAction.PlayPause;
        return element.Kind is ThemeElementKind.Data or ThemeElementKind.Bar or ThemeElementKind.Arc or ThemeElementKind.Chart &&
               SensorFor(element.DataName) != null ? ThemeAction.Details : ThemeAction.None;
    }

    /// <summary>The Pulsewave sensor a details page opens on for a data name, or null when it has none.</summary>
    public static string? SensorFor(string? dataName) => (dataName ?? "").ToUpperInvariant() switch
    {
        "CPUMODEL" => "CpuLoad",
        "GPUMODEL" => "GpuLoad",
        "FPS_AVG" => "GpuFps",
        var n when n.StartsWith("RAM", StringComparison.Ordinal) => "MemoryLoad",
        _ => ThemeData.SensorFor(dataName)
    };
}
