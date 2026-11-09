using System.Text.Json;
using System.Windows;
using Point = System.Windows.Point;
using Size = System.Windows.Size;

namespace Pulsewave.Themes.Editor;

/// <summary>
/// The theme being edited: every change is one undo step (gestures such as drags are applied
/// once on release). Nothing reaches theme.json until <see cref="Save"/>.
/// </summary>
public sealed class ThemeDocument
{
    private const int MaxUndo = 200;
    private readonly List<string> _undo = new();
    private readonly List<string> _redo = new();

    public string Directory { get; }
    public ThemeDefinition Theme { get; private set; }
    public bool IsDirty { get; private set; }
    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;
    public event Action? Changed;

    public ThemeDocument(string directory) : this(directory, ThemeStore.Load(directory)) { }

    internal ThemeDocument(string directory, ThemeDefinition theme)
    {
        Directory = directory;
        Theme = theme;
    }

    public void Change(Action<ThemeDefinition> edit)
    {
        Push(_undo, Snapshot());
        _redo.Clear();
        edit(Theme);
        IsDirty = true;
        Changed?.Invoke();
    }

    public void Undo()
    {
        if (_undo.Count == 0) return;
        Push(_redo, Snapshot());
        Theme = Restore(Pop(_undo));
        IsDirty = true;
        Changed?.Invoke();
    }

    public void Redo()
    {
        if (_redo.Count == 0) return;
        Push(_undo, Snapshot());
        Theme = Restore(Pop(_redo));
        IsDirty = true;
        Changed?.Invoke();
    }

    /// <summary>A new theme or a builder draft has nothing on disk worth keeping until saved.</summary>
    public void MarkUnsaved() => IsDirty = true;

    public void Save()
    {
        ThemeStore.Save(Directory, Theme);
        IsDirty = false;
    }

    private string Snapshot() => JsonSerializer.Serialize(Theme, ThemeStore.Json);
    private static ThemeDefinition Restore(string json) => JsonSerializer.Deserialize<ThemeDefinition>(json, ThemeStore.Json)!;

    private static void Push(List<string> stack, string snapshot)
    {
        stack.Add(snapshot);
        if (stack.Count > MaxUndo) stack.RemoveAt(0);
    }

    private static string Pop(List<string> stack)
    {
        var top = stack[^1];
        stack.RemoveAt(stack.Count - 1);
        return top;
    }

    public static ThemeElement Clone(ThemeElement element) =>
        JsonSerializer.Deserialize<ThemeElement>(JsonSerializer.Serialize(element, ThemeStore.Json), ThemeStore.Json)!;
}

/// <summary>Move, resize and snapping rules shared by the editor and its tests.</summary>
public static class EditorGeometry
{
    /// <summary>
    /// The background shift that keeps the point under <paramref name="pointer"/> in place when the
    /// zoom (about <paramref name="center"/>) changes from <paramref name="from"/> to <paramref name="to"/>.
    /// </summary>
    public static (double X, double Y) ZoomAbout(Point pointer, Point center, double from, double to, double offsetX, double offsetY)
    {
        double k = to / from;
        return (pointer.X - center.X - k * (pointer.X - center.X - offsetX), pointer.Y - center.Y - k * (pointer.Y - center.Y - offsetY));
    }

    public sealed record SnapResult(double Dx, double Dy, IReadOnlyList<double> VerticalGuides, IReadOnlyList<double> HorizontalGuides);

    /// <summary>
    /// Nudges a moving box so its left/center/right and top/middle/bottom line up with the
    /// canvas edges and center or with other elements, when within <paramref name="tolerance"/>.
    /// </summary>
    public static SnapResult Snap(Rect moving, IEnumerable<Rect> others, Size canvas, double tolerance)
    {
        var targetsX = new List<double> { 0, canvas.Width / 2, canvas.Width };
        var targetsY = new List<double> { 0, canvas.Height / 2, canvas.Height };
        foreach (var r in others.Where(r => !r.IsEmpty))
        {
            targetsX.AddRange([r.Left, r.Left + r.Width / 2, r.Right]);
            targetsY.AddRange([r.Top, r.Top + r.Height / 2, r.Bottom]);
        }
        var (dx, gx) = Best([moving.Left, moving.Left + moving.Width / 2, moving.Right], targetsX, tolerance);
        var (dy, gy) = Best([moving.Top, moving.Top + moving.Height / 2, moving.Bottom], targetsY, tolerance);
        return new SnapResult(dx, dy, gx is { } x ? [x] : [], gy is { } y ? [y] : []);
    }

    private static (double Delta, double? Guide) Best(double[] lines, List<double> targets, double tolerance)
    {
        double best = tolerance + 1; double? guide = null; double delta = 0;
        foreach (var line in lines)
            foreach (var target in targets)
            {
                double d = Math.Abs(target - line);
                if (d <= tolerance && d < best) { best = d; delta = target - line; guide = target; }
            }
        return (delta, guide);
    }

    /// <summary>
    /// Scales an element about <paramref name="origin"/> by <paramref name="factor"/>, keeping its
    /// shape: position, size, text, ring thickness, corners, borders, lines, glow and segments.
    /// </summary>
    public static void Scale(ThemeElement e, Point origin, double factor)
    {
        double s = Math.Clamp(factor, 0.05, 20);
        double R(double v) => Math.Round(v, 1);
        e.X = R(origin.X + (e.X - origin.X) * s);
        e.Y = R(origin.Y + (e.Y - origin.Y) * s);
        if (e.Kind == ThemeElementKind.Image)
            e.Width = Math.Round(Math.Clamp((e.Width is > 0 and < 20 ? e.Width : 1) * s, 0.05, 19), 3); // Width is the picture's scale
        else { e.Width = R(e.Width * s); e.Height = R(e.Height * s); }
        e.Diameter = R(e.Diameter * s);
        e.Thickness = R(e.Thickness * s);
        if (e.Font != null) e.Font.Size = Math.Round(Math.Clamp(e.Font.Size * s, 4, 400), 1);
        e.CornerRadius = R(e.CornerRadius * s);
        e.BorderWidth = R(e.BorderWidth * s);
        e.LineWidth = Math.Max(0.5, R(e.LineWidth * s));
        e.GlowSize = Math.Clamp(R(e.GlowSize * s), 1, 80);
        e.SegmentWidth = R(e.SegmentWidth * s);
        e.SegmentGap = R(e.SegmentGap * s);
    }

    /// <summary>
    /// The even scale a corner drag gives a group (it keeps its shape: whichever of width or height
    /// changed more decides) and the opposite corner it grows from.
    /// </summary>
    public static (Point Origin, double Factor) GroupScale(Rect before, Rect after, int corner)
    {
        double sw = after.Width / Math.Max(1, before.Width), sh = after.Height / Math.Max(1, before.Height);
        double s = Math.Clamp(Math.Abs(Math.Log(sw)) >= Math.Abs(Math.Log(sh)) ? sw : sh, 0.05, 20);
        var origin = corner switch { 0 => before.BottomRight, 1 => before.BottomLeft, 2 => before.TopLeft, _ => before.TopRight };
        return (origin, s);
    }

    /// <summary>Where a group's frame ends up after <see cref="GroupScale"/>.</summary>
    public static Rect Scaled(Rect r, Point origin, double s) =>
        new(new Point(origin.X + (r.Left - origin.X) * s, origin.Y + (r.Top - origin.Y) * s),
            new Point(origin.X + (r.Right - origin.X) * s, origin.Y + (r.Bottom - origin.Y) * s));

    /// <summary>
    /// The swaps that move every selected index one step up (+1) or down (-1) the drawing order;
    /// a block already at the end stays put and the rest stop against it.
    /// </summary>
    public static List<(int From, int To)> ReorderPlan(int count, IEnumerable<int> selected, int direction)
    {
        var set = new HashSet<int>(selected);
        var plan = new List<(int, int)>();
        foreach (var i in direction > 0 ? set.OrderDescending().ToList() : set.Order().ToList())
        {
            int to = i + direction;
            if (to < 0 || to >= count || set.Contains(to)) continue;
            set.Remove(i); set.Add(to);
            plan.Add((i, to));
        }
        return plan;
    }

    public static void Move(ThemeElement element, double dx, double dy)
    {
        element.X = Math.Round(element.X + dx);
        element.Y = Math.Round(element.Y + dy);
    }

    /// <summary>
    /// Applies a new bounding box from the resize handles. Text scales its font, images their
    /// scale factor, arcs stay round; bars and charts take the box as is.
    /// </summary>
    public static void Resize(ThemeElement element, Rect before, Rect after)
    {
        if (before.IsEmpty || before.Width <= 0 || before.Height <= 0) return;
        after = new Rect(Math.Round(after.X), Math.Round(after.Y), Math.Max(4, Math.Round(after.Width)), Math.Max(4, Math.Round(after.Height)));
        switch (element.Kind)
        {
            case ThemeElementKind.Text or ThemeElementKind.Data:
                element.Font ??= new ThemeFont();
                element.Font.Size = Math.Round(Math.Clamp(element.Font.Size * after.Height / before.Height, 4, 400), 1);
                if (element.Width > 0) element.Width = Math.Round(element.Width * after.Width / before.Width); // a capped width follows the box
                element.Y = after.Top;
                element.X = element.Font.Alignment switch
                {
                    ThemeTextAlignment.Center => Math.Round(after.Left + after.Width / 2),
                    ThemeTextAlignment.Right => after.Right,
                    _ => after.Left
                };
                break;
            case ThemeElementKind.Image:
                double scale = element.Width is > 0 and < 20 ? element.Width : 1;
                element.Width = Math.Round(Math.Clamp(scale * after.Width / before.Width, 0.05, 19), 3);
                element.X = after.Left; element.Y = after.Top;
                break;
            case ThemeElementKind.Line:
                double pad = Math.Max(2, element.LineWidth / 2);
                double dx = Math.Max(0, after.Width - 2 * pad) * Math.Sign(element.Width == 0 ? 1 : element.Width);
                double dy = Math.Max(0, after.Height - 2 * pad) * Math.Sign(element.Height == 0 ? 1 : element.Height);
                element.X = element.Width >= 0 ? after.Left + pad : after.Left + pad - dx;
                element.Y = element.Height >= 0 ? after.Top + pad : after.Top + pad - dy;
                element.Width = Math.Abs(element.Width) < 0.5 ? 0 : dx;
                element.Height = Math.Abs(element.Height) < 0.5 ? 0 : dy;
                break;
            case ThemeElementKind.Arc:
                double t = Math.Max(0, element.Thickness);
                element.Diameter = Math.Max(4, Math.Min(after.Width, after.Height) - t);
                element.Width = element.Height = element.Diameter;
                element.X = after.Left + t / 2; element.Y = after.Top + t / 2;
                break;
            default:
                element.X = after.Left; element.Y = after.Top;
                element.Width = after.Width; element.Height = after.Height;
                break;
        }
    }
}
