using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using FontFamily = System.Windows.Media.FontFamily;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Orientation = System.Windows.Controls.Orientation;
using Panel = System.Windows.Controls.Panel;
using Point = System.Windows.Point;
using VerticalAlignment = System.Windows.VerticalAlignment;

namespace Pulsewave;

using Pulsewave.Themes;

/// <summary>
/// The look of the HUD's touch pages, in the Lumen style (the layout is still One UI's quick panel): near-black with a
/// cyan, violet and magenta aurora, see-through glass cards with a hairline edge that catches the light along the top,
/// round glass buttons that fill with the cyan-to-violet accent (and glow) when on, pill sliders with the same fill,
/// DIN numerals (Bahnschrift) and letter-spaced small-caps captions. Everything is sized for a finger at arm's length.
/// </summary>
internal static class OneUi
{
    public static readonly FontFamily Font = new("Segoe UI Variable Display, Segoe UI"), Icons = new("Segoe Fluent Icons, Segoe MDL2 Assets"),
        Din = new(Lumen.Din + ", Segoe UI");
    // Colors come from the shared Lumen tokens (Themes/Lumen.cs), which the Lumen templates use too.
    private static readonly Color Cyan = C(Lumen.Cyan), Violet = C(Lumen.Violet);
    public static readonly Brush Text = Solid(Lumen.Text), Secondary = Solid(Lumen.Soft), Dim = Solid(Lumen.Dim),
        Glass = Frozen(0x14, 0xFF, 0xFF, 0xFF), GlassEdge = Solid(Lumen.Edge), ButtonEdge = Solid(Lumen.ButtonEdge),
        Off = FrozenGradient(C(Lumen.ButtonTop), C(Lumen.ButtonBottom), vertical: true),
        OnFill = FrozenGradient(Cyan, Violet, vertical: false), OnText = Solid(Lumen.OnText), Track = Solid(Lumen.Track),
        Danger = Frozen(0xFF, 0xFF, 0x8A, 0x8A), Accent = Solid(Lumen.Cyan), AccentWarm = Solid(Lumen.Magenta),
        Alert = Solid(Lumen.Warm);

    private static Color C(string hex) => (Color)System.Windows.Media.ColorConverter.ConvertFromString(hex);

    private static SolidColorBrush Solid(string hex)
    {
        var brush = new SolidColorBrush(C(hex));
        brush.Freeze();
        return brush;
    }

    /// <summary>The slider fill: the accent, cyan at the round end easing to violet.</summary>
    public static readonly Brush SliderFill = FrozenGradient(Violet, Cyan, vertical: true);
    public static readonly Brush SliderFillAcross = FrozenGradient(Cyan, Violet, vertical: false);

    /// <summary>A glass card's fill (a little lighter at the top) and its edge, brightest along the top like light on a rim.</summary>
    private static readonly Brush CardFill = FrozenGradient(C(Lumen.SlabTop), C(Lumen.SlabBottom), vertical: true), CardEdge = Rim();

    /// <summary>The cyan light under a button that is on.</summary>
    private static readonly System.Windows.Media.Effects.Effect OnGlow = Glow(Cyan, 24, 0.5);

    /// <summary>A soft colored light behind an element (album art, a cover).</summary>
    public static System.Windows.Media.Effects.Effect Glow(Color color, double size, double opacity)
    {
        var glow = new System.Windows.Media.Effects.DropShadowEffect { Color = color, ShadowDepth = 0, BlurRadius = size, Opacity = opacity,
            RenderingBias = System.Windows.Media.Effects.RenderingBias.Performance };
        glow.Freeze();
        return glow;
    }

    private static Brush Rim()
    {
        var brush = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(0x80, 0xFF, 0xFF, 0xFF), 0));
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(0x26, 0xFF, 0xFF, 0xFF), 0.1));
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(0x1A, 0xFF, 0xFF, 0xFF), 1));
        brush.Freeze();
        return brush;
    }

    /// <summary>The page background: near-black with the Lumen aurora (cyan, violet, magenta).</summary>
    public static Brush Background()
    {
        var group = new DrawingGroup();
        group.Children.Add(new GeometryDrawing(FrozenGradient(C(Lumen.CanvasTop), C(Lumen.CanvasBottom), vertical: true), null,
            new RectangleGeometry(new Rect(0, 0, 1, 1))));
        foreach (var (x, y, r, color) in new[] { (0.1, 0.06, 0.55, C("#4D" + Lumen.AuroraCyan)), (1.0, 0.4, 0.5, C("#40" + Lumen.AuroraViolet)),
                     (0.0, 0.78, 0.5, C("#38" + Lumen.AuroraMagenta)), (0.92, 1.0, 0.4, C("#2E" + Lumen.AuroraCyan)) })
        {
            var glow = new RadialGradientBrush(color, Color.FromArgb(0, color.R, color.G, color.B)) { Center = new Point(x, y), GradientOrigin = new Point(x, y), RadiusX = r, RadiusY = r };
            glow.Freeze();
            group.Children.Add(new GeometryDrawing(glow, null, new RectangleGeometry(new Rect(0, 0, 1, 1))));
        }
        var brush = new DrawingBrush(group) { Stretch = Stretch.Fill };
        brush.Freeze();
        return brush;
    }

    /// <summary>A see-through glass card.</summary>
    public static Border Card(UIElement child, double radius = 36, double padding = 20) => new()
    {
        Background = CardFill, BorderBrush = CardEdge, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(radius),
        Padding = new Thickness(padding), Margin = new Thickness(8), Child = child, ClipToBounds = true
    };

    /// <summary>A page's title: light DIN, like the themes' clock.</summary>
    public static TextBlock Title(string text) => new()
    {
        Text = text, FontSize = 36, FontFamily = Din, FontWeight = FontWeights.Light, Foreground = Text, TextTrimming = TextTrimming.CharacterEllipsis
    };

    /// <summary>A section caption: small DIN capitals, letter-spaced (see <see cref="Spaced"/>).</summary>
    public static TextBlock Caption(string text, double size = 17, Brush? color = null) => new()
    {
        Text = Spaced(text), FontSize = size, FontFamily = Din, FontWeight = FontWeights.SemiBold, Foreground = color ?? Dim, TextTrimming = TextTrimming.CharacterEllipsis
    };

    /// <summary>Capitals with a hair space between the letters (WPF text has no letter-spacing).</summary>
    public static string Spaced(string text) => Lumen.Spaced(text);

    /// <summary>A reading: DIN numerals, light when large.</summary>
    public static TextBlock Number(string text, double size, Brush? color = null, bool light = false) => new()
    {
        Text = text, FontSize = size, FontFamily = Din, FontWeight = light ? FontWeights.Light : FontWeights.Normal, Foreground = color ?? Text,
        TextTrimming = TextTrimming.CharacterEllipsis
    };

    public static TextBlock Label(string text, double size, Brush? color = null, bool bold = false) => new()
    {
        Text = text, FontSize = size, FontFamily = Font, Foreground = color ?? Text, FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal,
        TextTrimming = TextTrimming.CharacterEllipsis
    };

    /// <summary>
    /// An icon. Segoe Fluent Icons' text line is exactly one em tall with each glyph centred in it, so a TextBlock
    /// centred in its button already centres the icon: no vertical correction (an earlier 0.18 em drop put every
    /// page icon low). Only the play triangle moves, sideways (see <see cref="PlayNudge"/>).
    /// Theme text is placed differently, from its line's top (see the templates' IconTop).
    /// </summary>
    public static TextBlock Glyph(string glyph, double size, Brush? color = null) => new()
    {
        Text = glyph, FontFamily = Icons, FontSize = size, Foreground = color ?? Text,
        HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
        RenderTransform = new TranslateTransform(NudgeX(glyph, size), 0)
    };

    /// <summary>
    /// The play triangle centred by its box still looks left of centre (its weight is at the flat side), so it moves
    /// right by this share of the font size; every other icon is centred by its box.
    /// </summary>
    public const double PlayNudge = 0.06;

    public static bool IsPlay(string? glyph) => glyph is Themes.ThemeData.PlayGlyph or "";

    public static double NudgeX(string? glyph, double size) => IsPlay(glyph) ? Math.Round(size * PlayNudge, 1) : 0;

    /// <summary>A soft bevel for round buttons: light along the top edge, shade along the bottom.</summary>
    public static readonly Brush Bevel = BevelBrush();

    private static Brush BevelBrush()
    {
        var brush = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(0x73, 0xFF, 0xFF, 0xFF), 0));
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(0x1A, 0xFF, 0xFF, 0xFF), 0.35));
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(0x10, 0x00, 0x00, 0x00), 0.65));
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(0x59, 0x00, 0x00, 0x00), 1));
        brush.Freeze();
        return brush;
    }

    /// <summary>A round glass button with a glyph; <see cref="SetOn"/> fills it with the accent (with a dark glyph and a glow) when on.</summary>
    public static Border Circle(string glyph, double size, Action tap, bool on = false, string? tag = null, double? glyphSize = null)
    {
        var circle = new Border
        {
            Width = size, Height = size, CornerRadius = new CornerRadius(size / 2), Cursor = System.Windows.Input.Cursors.Hand,
            BorderBrush = ButtonEdge, BorderThickness = new Thickness(1),
            Child = Glyph(glyph, glyphSize ?? size * 0.38), Tag = tag ?? glyph, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
        };
        SetOn(circle, on);
        circle.MouseLeftButtonUp += (_, _) => tap();
        return circle;
    }

    public static void SetOn(Border button, bool on)
    {
        button.Background = on ? OnFill : Off;
        button.Effect = on ? OnGlow : null;
        foreach (var text in Texts(button.Child)) text.Foreground = on ? OnText : Text;
    }

    public static void SetGlyph(Border button, string glyph)
    {
        if (button.Child is not TextBlock t) return;
        t.Text = glyph;
        if (t.RenderTransform is TranslateTransform { IsFrozen: false } move) move.X = NudgeX(glyph, t.FontSize);
    }

    /// <summary>A rounded glass pill with a label (and optional glyph), filled with the accent when selected.</summary>
    public static Border Pill(string text, double height, Action tap, bool selected = false, string? glyph = null, double size = 20, string? tag = null)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        if (glyph != null) row.Children.Add(new TextBlock { Text = glyph, FontFamily = Icons, FontSize = size, Margin = new Thickness(0, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center });
        if (text.Length > 0) row.Children.Add(new TextBlock { Text = text, FontFamily = Font, FontSize = size, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center });
        var pill = new Border
        {
            Height = height, CornerRadius = new CornerRadius(height / 2), Margin = new Thickness(6), Padding = new Thickness(18, 0, 18, 0),
            Cursor = System.Windows.Input.Cursors.Hand, Child = row, Tag = tag ?? text, BorderBrush = ButtonEdge, BorderThickness = new Thickness(1)
        };
        SetOn(pill, selected);
        pill.MouseLeftButtonUp += (_, _) => tap();
        return pill;
    }

    private static IEnumerable<TextBlock> Texts(object? node)
    {
        if (node is TextBlock t) yield return t;
        else if (node is Panel p) foreach (var child in p.Children) foreach (var inner in Texts(child)) yield return inner;
        else if (node is Decorator d) foreach (var inner in Texts(d.Child)) yield return inner;
    }

    public static SolidColorBrush Frozen(byte a, byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromArgb(a, r, g, b));
        brush.Freeze();
        return brush;
    }

    private static LinearGradientBrush FrozenGradient(Color from, Color to, bool vertical)
    {
        var brush = new LinearGradientBrush(from, to, vertical ? new Point(0, 0) : new Point(0, 0), vertical ? new Point(0, 1) : new Point(1, 0));
        brush.Freeze();
        return brush;
    }
}

/// <summary>
/// A control a finger can drag (a slider, a seek bar): the HUD's touch handling sends it the finger's
/// position, in the control's own coordinates, while it moves and once more when it lifts.
/// </summary>
internal interface ITouchDrag
{
    /// <summary>Dragged up and down (else across); a drag the other way goes to a list around it.</summary>
    bool Vertical { get; }
    void DragTo(Point at, bool final);
}

/// <summary>
/// A pill slider: a translucent track with an accent fill that grows from the bottom (or left),
/// an icon riding at the start of the fill and the level shown at the far end. The HUD turns touches
/// into taps, so a tap sets the level where it lands (never less than the round end of the fill).
/// </summary>
internal sealed class PillSlider : Grid, ITouchDrag
{
    private long _lastSent;
    private readonly bool _vertical;
    private readonly double _thickness;
    private readonly Border _track, _fill;
    private readonly ContentControl _icon = new() { Focusable = false, IsTabStop = false };
    private readonly TextBlock _value;
    private double _level;
    private bool _muted;

    /// <summary>A tap: the level 0–100 where it landed.</summary>
    public event Action<double>? LevelChosen;
    /// <summary>Where a tap landed (touch is replayed at the finger, not the mouse cursor).</summary>
    public Func<System.Windows.Input.MouseEventArgs, UIElement, Point> PositionOf { get; set; } = (e, element) => e.GetPosition(element);

    public PillSlider(bool vertical, double thickness, string tag)
    {
        _vertical = vertical;
        _thickness = thickness;
        Tag = tag;
        Cursor = System.Windows.Input.Cursors.Hand;
        Background = Brushes.Transparent;
        if (vertical) Width = thickness; else Height = thickness;
        _track = new Border { Background = OneUi.Track, CornerRadius = new CornerRadius(thickness / 2) };
        _fill = new Border
        {
            Background = vertical ? OneUi.SliderFill : OneUi.SliderFillAcross, CornerRadius = new CornerRadius(thickness / 2),
            HorizontalAlignment = vertical ? HorizontalAlignment.Stretch : HorizontalAlignment.Left,
            VerticalAlignment = vertical ? VerticalAlignment.Bottom : VerticalAlignment.Stretch,
            Child = _icon
        };
        _icon.HorizontalAlignment = vertical ? HorizontalAlignment.Center : HorizontalAlignment.Left;
        _icon.VerticalAlignment = vertical ? VerticalAlignment.Bottom : VerticalAlignment.Center;
        _icon.Width = _icon.Height = thickness;
        _value = OneUi.Number("", Math.Max(17, thickness * 0.24), OneUi.Secondary);
        _value.HorizontalAlignment = vertical ? HorizontalAlignment.Center : HorizontalAlignment.Right;
        _value.VerticalAlignment = vertical ? VerticalAlignment.Top : VerticalAlignment.Center;
        _value.Margin = vertical ? new Thickness(0, thickness * 0.3, 0, 0) : new Thickness(0, 0, thickness * 0.35, 0);
        Children.Add(_track);
        Children.Add(_value);
        Children.Add(_fill);
        SizeChanged += (_, _) => Layout();
        MouseLeftButtonUp += (_, e) =>
        {
            _touchedAt = Environment.TickCount64;
            if (IsMouseCaptured) ReleaseMouseCapture();
            if (LevelAt(PositionOf(e, this)) is { } level) LevelChosen?.Invoke(level);
        };
        // With a mouse the slider can be dragged as well as clicked.
        MouseLeftButtonDown += (_, _) => CaptureMouse();
        MouseMove += (_, e) => { if (IsMouseCaptured) DragTo(e.GetPosition(this), final: false); };
    }

    public bool Vertical => _vertical;

    /// <summary>The level 0–100 at a point on the slider (its round end counts as 0).</summary>
    private double? LevelAt(Point at)
    {
        double length = _vertical ? ActualHeight : ActualWidth;
        if (length <= _thickness) return null;
        double along = _vertical ? length - at.Y : at.X;
        return Math.Round(Math.Clamp((along - _thickness / 2) / (length - _thickness), 0, 1) * 100);
    }

    /// <summary>Follows a dragging finger: the fill moves at once; the level is sent at most ~16 times a second, and always at the end.</summary>
    public void DragTo(Point at, bool final)
    {
        if (LevelAt(at) is not { } level) return;
        Set(level, false, _value.Text.EndsWith('%') ? $"{level:0}%" : null);
        long now = Environment.TickCount64;
        _touchedAt = now;
        if (!final && now - _lastSent < 60) return;
        _lastSent = now;
        LevelChosen?.Invoke(level);
    }

    /// <summary>The icon inside the round end of the fill: a glyph, or an app's picture.</summary>
    public void SetIcon(object icon) => _icon.Content = icon;

    /// <summary>
    /// How long after the last touch readings from the device are ignored. A player such as Plexamp reports its
    /// volume a beat behind the finger; drawing those readings mid-drag made the knob jump back and forth.
    /// </summary>
    public const int HoldMs = 2000;
    private long _touchedAt = long.MinValue / 2;

    /// <summary>Whether the user is moving the slider or just let go of it.</summary>
    public bool InHand(long now) => now - _touchedAt < HoldMs;

    /// <summary>A level reported by the device (a refresh): shown unless the user is moving the slider or just let go.</summary>
    public void Show(double level, bool muted = false, string? text = null)
    {
        if (!InHand(Environment.TickCount64)) Set(level, muted, text);
    }

    /// <summary>Shows a level 0–100 at once; a muted slider shows its level faintly and says so.</summary>
    public void Set(double level, bool muted = false, string? text = null)
    {
        _level = Math.Clamp(level, 0, 100);
        _muted = muted;
        _value.Text = text ?? (muted ? "Muted" : $"{_level:0}");
        _fill.Opacity = muted ? 0.45 : 1;
        Layout();
    }

    public double Level => _level;

    private void Layout()
    {
        double length = _vertical ? ActualHeight : ActualWidth;
        if (length <= 0) return;
        double size = _thickness + (length - _thickness) * _level / 100;
        if (_vertical) _fill.Height = size; else _fill.Width = size;
        // The level reads on the track at the far end until the fill reaches it; from then on it rides just
        // inside the fill's end in dark, so the fill's edge never cuts through it.
        double inset = _thickness * (_vertical ? 0.3 : 0.35);
        double extent = _vertical ? _value.FontSize * 1.4 : Math.Max(_value.ActualWidth, _value.Text.Length * _value.FontSize * 0.62);
        double free = length - size; // track left beyond the fill
        bool covered = free < inset + extent + 4;
        double at = covered ? Math.Max(inset, free + inset * 0.6) : inset;
        _value.Margin = _vertical ? new Thickness(0, at, 0, 0) : new Thickness(0, 0, at, 0);
        _value.Foreground = covered && !_muted ? OneUi.OnText : OneUi.Secondary;
        Panel.SetZIndex(_value, covered ? 2 : 0);
    }
}
