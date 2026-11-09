using System.Text.Json.Serialization;

namespace Pulsewave.Themes;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ThemeElementKind { Image, Text, Data, Bar, Arc, Chart, Panel, Line, AlbumArt, Shortcut }

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ThemeTextAlignment { Left, Center, Right }

/// <summary>What tapping or clicking an element does when click-through is off.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ThemeAction
{
    None, PlayPause, NextTrack, PreviousTrack, Details, NextTheme, PreviousTheme, Launch, AppDrawer,
    VolumeUp, VolumeDown, Mute, TimerToggle, TimerReset, TimerAddMinute, StopwatchToggle, StopwatchReset,
    LightsPage, LightsToggle, AestheticToggle, VolumeMixer, NextPalette, MediaPage, PalettePage, GamingPage,
    Pomodoro25, Pomodoro50, FocusToggle, FocusReset, FocusSwitch
}

/// <summary>
/// Pulsewave's own theme format (theme.json). Imports convert into this once; the
/// renderer never reads third-party theme files. Coordinates are display pixels of a
/// Width x Height screen (1920x480 unless the theme says otherwise).
/// </summary>
public sealed class ThemeDefinition
{
    public const int CurrentFormat = 1;
    public int Format { get; set; } = CurrentFormat;
    public string Name { get; set; } = "";
    public string Source { get; set; } = "";
    public int Width { get; set; } = 1920;
    public int Height { get; set; } = 480;
    public string BackgroundColor { get; set; } = "#FF000000";
    public string? BackgroundVideo { get; set; }
    /// <summary>Degrees to turn the video; L-Connect stores landscape videos for its portrait panel rotated.</summary>
    public int BackgroundVideoRotation { get; set; }
    public string? BackgroundImage { get; set; }
    /// <summary>"Fill" stretches the background to the screen (L-Connect); "Cover" crops it to fill without distortion; "Fit" shows all of it.</summary>
    public string BackgroundFit { get; set; } = "Fill";
    /// <summary>Background picture/video placement on top of the fit: zoom about the screen centre (1 = as fitted), then a shift in display pixels.</summary>
    public double BackgroundZoom { get; set; } = 1;
    public double BackgroundOffsetX { get; set; }
    public double BackgroundOffsetY { get; set; }
    public string? Preview { get; set; }
    /// <summary>
    /// Which of the user's shortcut lists this theme's tiles and app drawer follow: "Gaming" or "Productivity"
    /// (tray → Shortcuts → List); null or anything else is the main list, in slot order.
    /// </summary>
    public string? ShortcutList { get; set; }
    public List<ThemeElement> Elements { get; set; } = new();
}

public sealed class ThemeFont
{
    public string Family { get; set; } = "Segoe UI";
    /// <summary>Size in points, as L-Connect stores it.</summary>
    public double Size { get; set; } = 24;
    public bool Bold { get; set; }
    public bool Italic { get; set; }
    public string Color { get; set; } = "#FFFFFFFF";
    public ThemeTextAlignment Alignment { get; set; } = ThemeTextAlignment.Left;
}

public sealed class ThemeElement
{
    public ThemeElementKind Kind { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public bool Hidden { get; set; }
    /// <summary>
    /// Elements with the same group name are selected, moved and scaled together in the editor
    /// (a clock's time, date and card, say). Null when on its own; the HUD itself ignores it.
    /// </summary>
    public string? Group { get; set; }
    /// <summary>Tap/click action; null picks one from the element (see ThemeActions.Effective).</summary>
    public ThemeAction? Action { get; set; }

    // Text and Data. Width, when above 0, is the most the text may take: longer text ends in "…".
    public string? Text { get; set; }
    public string? DataName { get; set; }
    /// <summary>Data variant: time/date format ("h:m:s", "Y-M-D") or drive selector.</summary>
    public string? DataFormat { get; set; }
    public bool ShowUnit { get; set; }
    /// <summary>A space between number and unit ("188 W" rather than "188W").</summary>
    public bool SpacedUnit { get; set; }
    /// <summary>With a Width, text may wrap onto this many lines (the last ends in "…"); 0 or 1 keeps one line.</summary>
    public int MaxLines { get; set; }
    public bool Fahrenheit { get; set; }
    public ThemeFont? Font { get; set; }

    // Image
    public string? ImageFile { get; set; }

    /// <summary>
    /// Shortcut elements and APP_NAME values: which of the user's shortcuts (tray → Shortcuts, 1 = first)
    /// they show and launch. Themes only name a slot; what it opens lives in the user's settings.
    /// </summary>
    public int Slot { get; set; }

    // Shortcut: an X/Y/Width/Height tile (FillColor, BorderColor/BorderWidth, CornerRadius) with the
    // shortcut's icon centred in it; LineColor tints the placeholder of an empty slot.

    /// <summary>
    /// Shortcut tiles: "Tinted" backs each app tile with its icon's own color and draws system actions and key
    /// presses as round buttons; null keeps the plain tile.
    /// </summary>
    public string? TileStyle { get; set; }
    /// <summary>Shortcut tiles: the shortcut's name under its icon.</summary>
    public bool ShowLabel { get; set; }

    // AlbumArt: the now-playing cover in an X/Y/Width/Height box with CornerRadius; FillColor
    // shows behind a music-note placeholder when there is no art.

    // Bar, Arc and Chart
    public string ForeColor { get; set; } = "#FFFFFFFF";
    public string BackColor { get; set; } = "#00000000";
    public string? GradientColor { get; set; }
    public double CornerRadius { get; set; }
    public double SegmentWidth { get; set; }
    public double SegmentGap { get; set; }
    /// <summary>Value shown as a full bar/arc/chart; 0 scales charts to their own peak.</summary>
    public double Maximum { get; set; } = 100;

    // Arc: X/Y is the top-left of the ring's bounding square.
    public double Diameter { get; set; }
    public double Thickness { get; set; }
    /// <summary>Degrees clockwise from 3 o'clock where the arc begins.</summary>
    public double StartAngle { get; set; } = -90;
    public double SweepAngle { get; set; } = 360;
    public bool RoundCaps { get; set; }

    // Panel: X/Y/Width/Height box with FillColor (+ GradientColor), BorderColor/BorderWidth and CornerRadius.
    /// <summary>"Round" corners, or "Cut" for chamfered sci-fi corners of CornerRadius size.</summary>
    public string CornerStyle { get; set; } = "Round";
    /// <summary>"Linear" (along GradientAngle) or "Radial" (from the center).</summary>
    public string GradientStyle { get; set; } = "Linear";
    /// <summary>Degrees; 90 runs top to bottom.</summary>
    public double GradientAngle { get; set; } = 90;
    /// <summary>Album art and shortcut tiles: show pictures in shades of grey (for monochrome themes).</summary>
    public bool Grayscale { get; set; }
    /// <summary>Panel: a soft bevel on its edge (light along the top, shade along the bottom), for round buttons.</summary>
    public bool Bevel { get; set; }

    // Warning colors for text, values, bars and gauges: at WarnAt the element shows WarnColor (amber by default), at
    // CriticalAt CriticalColor (red); with LowIsBad the thresholds count downward (FPS, battery). ColorBy names the
    // reading that decides (default the element's own). Temperatures compare in °C. None set: colors never change.
    public double? WarnAt { get; set; }
    public double? CriticalAt { get; set; }
    public bool LowIsBad { get; set; }
    public string? ColorBy { get; set; }
    public string? WarnColor { get; set; }
    public string? CriticalColor { get; set; }

    /// <summary>
    /// A reading that must have a value for this element to show (an unpaired clock sensor, a media source without a
    /// timeline); null always shows. Labels name the reading of the value they belong to.
    /// </summary>
    public string? ShowIf { get; set; }

    // Glow for text, values, panels, lines, bars and gauges; none when GlowColor is null.
    public string? GlowColor { get; set; }
    public double GlowSize { get; set; } = 12;

    // Chart; Line uses LineColor/LineWidth and draws from X,Y to X+Width,Y+Height.
    public string LineColor { get; set; } = "#FFFFFFFF";
    public string FillColor { get; set; } = "#00000000";
    public string BorderColor { get; set; } = "#00000000";
    public double LineWidth { get; set; } = 1;
    public double BorderWidth { get; set; }
    public int Samples { get; set; } = 30;
    /// <summary>Chart with an automatic scale (Maximum 0): the scale never goes below this, so a calm line stays low.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double MinimumScale { get; set; }
    /// <summary>Chart: a dashed guide line at this value (16.7 ms is 60 FPS); null draws none.</summary>
    public double? GuideAt { get; set; }
    // A chart with WarnAt draws the stretches of its line above that value in WarnColor (CriticalAt is not used).
}
