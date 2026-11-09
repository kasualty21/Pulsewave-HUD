namespace Pulsewave.Themes;

public sealed record DisplaySize(string Label, int Width, int Height);

/// <summary>
/// Screen sizes Pulsewave lays out for: the 8.8" bar it was built for, the HYTE Y70 Touch
/// Infinite (a 682×2560 panel, usually turned to 2560×682 in Windows), vertical bars and tablets.
/// A theme can be any size; one whose shape differs from the display is shown letterboxed.
/// </summary>
public static class DisplaySizes
{
    public static readonly IReadOnlyList<DisplaySize> Presets =
    [
        new("8.8\" bar · 1920×480", 1920, 480),
        new("Vertical bar · 480×1920", 480, 1920),
        new("HYTE Y70 Touch Infinite · 2560×682", 2560, 682),
        new("HYTE Y70 Touch Infinite, vertical · 682×2560", 682, 2560),
        new("Tablet / monitor 16:9 · 1920×1080", 1920, 1080),
        new("Tablet 16:10 · 1920×1200", 1920, 1200),
        new("Tablet 16:10, portrait · 1200×1920", 1200, 1920)
    ];

    /// <summary>Displays that are almost certainly a dedicated HUD panel rather than a desktop monitor.</summary>
    public static bool IsHudPanel(int width, int height) =>
        (width, height) is (1920, 480) or (480, 1920) or (2560, 682) or (682, 2560) or (2560, 688) or (688, 2560) or (1920, 440) or (440, 1920);

    /// <summary>
    /// Whether two sizes have about the same shape (within <paramref name="tolerance"/>, as a
    /// ratio of aspect ratios), so one fills the other without noticeable bars or stretching.
    /// </summary>
    public static bool SameShape(int width, int height, int otherWidth, int otherHeight, double tolerance = 0.15)
    {
        if (width <= 0 || height <= 0 || otherWidth <= 0 || otherHeight <= 0) return false;
        double ratio = (double)width / height / ((double)otherWidth / otherHeight);
        return Math.Abs(Math.Log(ratio)) <= Math.Log(1 + tolerance);
    }

    /// <summary>The Themes gallery's shape filter: saved key and label. "" shows everything.</summary>
    public static readonly IReadOnlyList<(string Key, string Label)> Filters =
        [("", "All shapes"), ("fits", "Fits this display"), ("horizontal", "Horizontal"), ("vertical", "Vertical")];

    /// <summary>
    /// Whether a theme of <paramref name="width"/>×<paramref name="height"/> passes the gallery filter.
    /// Square themes count as both horizontal and vertical; "fits" shows all while the display size is unknown.
    /// Unknown filters (a newer version's) show everything.
    /// </summary>
    public static bool Matches(string? filter, int width, int height, int displayWidth, int displayHeight)
    {
        if (string.IsNullOrEmpty(filter) || Filters.All(f => f.Key != filter)) return true;
        if (width <= 0 || height <= 0) return false;
        return filter switch
        {
            "fits" => displayWidth <= 0 || displayHeight <= 0 || SameShape(width, height, displayWidth, displayHeight),
            "horizontal" => width >= height,
            _ => height >= width
        };
    }

    /// <summary>"HYTE Y70 Touch Infinite · 2560×682" for a preset, otherwise "1366×768".</summary>
    public static string Describe(int width, int height) =>
        Presets.FirstOrDefault(p => p.Width == width && p.Height == height)?.Label ?? $"{width}×{height}";
}
