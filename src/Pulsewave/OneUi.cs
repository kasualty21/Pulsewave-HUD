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

/// <summary>
/// The look of the HUD's touch pages, modelled on Samsung One UI's quick panel and tinted in Pulsewave's Halo style: a dark gradient with blue, pink and green glows,
/// frosted glass cards with large rounded corners, round buttons that fill light when on, and pill
/// sliders whose light fill carries the icon. Everything is sized for a finger at arm's length.
/// </summary>
internal static class OneUi
{
    public static readonly FontFamily Font = new("Segoe UI Variable Display, Segoe UI"), Icons = new("Segoe Fluent Icons, Segoe MDL2 Assets");
    public static readonly Brush Text = Frozen(0xFF, 0xF5, 0xF7, 0xFA), Secondary = Frozen(0xB8, 0xE6, 0xEA, 0xF0),
        Glass = Frozen(0x99, 0x14, 0x1A, 0x2C), GlassEdge = Frozen(0x40, 0xFF, 0xFF, 0xFF), Off = Frozen(0x40, 0xFF, 0xFF, 0xFF),
        OnFill = Frozen(0xFF, 0xE9, 0xEC, 0xF0), OnText = Frozen(0xFF, 0x1D, 0x23, 0x30), Track = Frozen(0x2E, 0xFF, 0xFF, 0xFF),
        Danger = Frozen(0xFF, 0xFF, 0x8A, 0x8A);

    /// <summary>The light slider fill: white at the top easing to a soft grey, like One UI.</summary>
    public static readonly Brush SliderFill = FrozenGradient(Color.FromRgb(0xC3, 0xA6, 0xFF), Color.FromRgb(0x7F, 0xD8, 0xFF), vertical: true);
    public static readonly Brush SliderFillAcross = FrozenGradient(Color.FromRgb(0x7F, 0xD8, 0xFF), Color.FromRgb(0xC3, 0xA6, 0xFF), vertical: false);

    /// <summary>The page background: deep navy with a soft violet and rose glow, as behind the quick panel.</summary>
    public static Brush Background()
    {
        var group = new DrawingGroup();
        group.Children.Add(new GeometryDrawing(FrozenGradient(Color.FromRgb(0x0E, 0x14, 0x24), Color.FromRgb(0x0A, 0x0E, 0x1A), vertical: true), null,
            new RectangleGeometry(new Rect(0, 0, 1, 1))));
        foreach (var (x, y, r, color) in new[] { (0.1, 0.12, 0.55, Color.FromArgb(0x66, 0x36, 0xC2, 0xFF)), (0.95, 0.5, 0.5, Color.FromArgb(0x55, 0xFF, 0x5E, 0x8A)), (0.2, 0.92, 0.55, Color.FromArgb(0x44, 0x44, 0xE0, 0xA0)) })
        {
            var glow = new RadialGradientBrush(color, Color.FromArgb(0, color.R, color.G, color.B)) { Center = new Point(x, y), GradientOrigin = new Point(x, y), RadiusX = r, RadiusY = r };
            glow.Freeze();
            group.Children.Add(new GeometryDrawing(glow, null, new RectangleGeometry(new Rect(0, 0, 1, 1))));
        }
        var brush = new DrawingBrush(group) { Stretch = Stretch.Fill };
        brush.Freeze();
        return brush;
    }

    /// <summary>A frosted glass card.</summary>
    public static Border Card(UIElement child, double radius = 36, double padding = 20) => new()
    {
        Background = Glass, BorderBrush = GlassEdge, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(radius),
        Padding = new Thickness(padding), Margin = new Thickness(8), Child = child, ClipToBounds = true
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

    /// <summary>A round button with a glyph; <see cref="SetOn"/> fills it light (with a dark glyph) when on.</summary>
    public static Border Circle(string glyph, double size, Action tap, bool on = false, string? tag = null, double? glyphSize = null)
    {
        var circle = new Border
        {
            Width = size, Height = size, CornerRadius = new CornerRadius(size / 2), Cursor = System.Windows.Input.Cursors.Hand,
            BorderBrush = Bevel, BorderThickness = new Thickness(Math.Max(1, size / 48)),
            Child = Glyph(glyph, glyphSize ?? size * 0.38), Tag = tag ?? glyph, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
        };
        SetOn(circle, on);
        circle.MouseLeftButtonUp += (_, _) => tap();
        return circle;
    }

    public static void SetOn(Border button, bool on)
    {
        button.Background = on ? OnFill : Off;
        foreach (var text in Texts(button.Child)) text.Foreground = on ? OnText : Text;
    }

    public static void SetGlyph(Border button, string glyph)
    {
        if (button.Child is not TextBlock t) return;
        t.Text = glyph;
        if (t.RenderTransform is TranslateTransform { IsFrozen: false } move) move.X = NudgeX(glyph, t.FontSize);
    }

    /// <summary>A rounded pill with a label (and optional glyph), light when selected.</summary>
    public static Border Pill(string text, double height, Action tap, bool selected = false, string? glyph = null, double size = 20, string? tag = null)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        if (glyph != null) row.Children.Add(new TextBlock { Text = glyph, FontFamily = Icons, FontSize = size, Margin = new Thickness(0, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center });
        if (text.Length > 0) row.Children.Add(new TextBlock { Text = text, FontFamily = Font, FontSize = size, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center });
        var pill = new Border
        {
            Height = height, CornerRadius = new CornerRadius(height / 2), Margin = new Thickness(6), Padding = new Thickness(18, 0, 18, 0),
            Cursor = System.Windows.Input.Cursors.Hand, Child = row, Tag = tag ?? text
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
/// A One UI pill slider:a translucent track with a light fill that grows from the bottom (or left),
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
        _value = OneUi.Label("", Math.Max(16, thickness * 0.22), OneUi.Secondary, bold: true);
        _value.HorizontalAlignment = vertical ? HorizontalAlignment.Center : HorizontalAlignment.Right;
        _value.VerticalAlignment = vertical ? VerticalAlignment.Top : VerticalAlignment.Center;
        _value.Margin = vertical ? new Thickness(0, thickness * 0.3, 0, 0) : new Thickness(0, 0, thickness * 0.35, 0);
        Children.Add(_track);
        Children.Add(_value);
        Children.Add(_fill);
        SizeChanged += (_, _) => Layout();
        MouseLeftButtonUp += (_, e) =>
        {
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
        if (!final && now - _lastSent < 60) return;
        _lastSent = now;
        LevelChosen?.Invoke(level);
    }

    /// <summary>The icon inside the round end of the fill: a glyph, or an app's picture.</summary>
    public void SetIcon(object icon) => _icon.Content = icon;

    /// <summary>Shows a level 0–100; a muted slider shows its level faintly and says so.</summary>
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
