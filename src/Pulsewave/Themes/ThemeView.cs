using Path = System.Windows.Shapes.Path;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using FontFamily = System.Windows.Media.FontFamily;
using Image = System.Windows.Controls.Image;
using Point = System.Windows.Point;
using Rectangle = System.Windows.Shapes.Rectangle;
using Size = System.Windows.Size;

namespace Pulsewave.Themes;

/// <summary>
/// Draws a <see cref="ThemeDefinition"/> at its native size: background color, still image
/// and looping muted video, then each element in order. Built once; <see cref="Update"/>
/// only changes text, bar/arc lengths and chart points.
/// </summary>
public sealed class ThemeView : Grid, IDisposable
{
    private interface IPart { void Update(ThemeDataContext data); }

    private readonly List<IPart> _parts = new();
    private readonly VideoLoop? _video;
    private readonly Dictionary<ThemeElement, List<FrameworkElement>> _visuals = new(ReferenceEqualityComparer.Instance);

    public ThemeDefinition Theme { get; }
    private readonly Image? _still, _frame;
    private double _zoom = 1, _shiftX, _shiftY;
    public VideoLoop? Video => _video;
    /// <summary>Why the background video is not playing, if it failed.</summary>
    public string? VideoError { get; private set; }
    public IReadOnlyList<string> MissingFonts { get; }

    /// <param name="drawBackground">False draws only the elements on a transparent surface (the editor layers them over its own background).</param>
    /// <param name="drawElements">False draws only the background.</param>
    public ThemeView(ThemeDefinition theme, string themeDirectory, bool playVideo = true, bool drawBackground = true, bool drawElements = true)
    {
        Theme = theme;
        Width = theme.Width;
        Height = theme.Height;
        ClipToBounds = true;
        Background = drawBackground ? Solid(theme.BackgroundColor, Colors.Black) : Brushes.Transparent;
        (_zoom, _shiftX, _shiftY) = (theme.BackgroundZoom, theme.BackgroundOffsetX, theme.BackgroundOffsetY);

        // Pictures and video are sized and placed explicitly on a canvas, so zooming out or
        // shifting reveals the rest of the picture rather than an already-cropped part.
        var backdrop = new Canvas { Width = theme.Width, Height = theme.Height };
        if (drawBackground && (theme.BackgroundImage != null || (playVideo && theme.BackgroundVideo != null))) Children.Add(backdrop);
        if (drawBackground && theme.BackgroundImage is { } still)
        {
            _still = new Image { Source = LoadImage(themeDirectory, still), Stretch = Stretch.Fill };
            backdrop.Children.Add(_still);
        }

        if (drawBackground && playVideo && theme.BackgroundVideo is { } video)
        {
            var frame = new Image
            {
                Stretch = Stretch.Fill, Visibility = Visibility.Collapsed,
                LayoutTransform = new RotateTransform(theme.BackgroundVideoRotation)
            };
            _frame = frame;
            backdrop.Children.Add(frame);
            _video = new VideoLoop(ThemeStore.ResolveFile(themeDirectory, video), Dispatcher);
            _video.Started += bitmap => { frame.Source = bitmap; frame.Visibility = Visibility.Visible; PlaceBackground(_zoom, _shiftX, _shiftY); };
            _video.Failed += message => VideoError = message; // the still image stays
        }
        PlaceBackground(_zoom, _shiftX, _shiftY);

        var canvas = new Canvas { Width = theme.Width, Height = theme.Height, IsHitTestVisible = false };
        var missing = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var element in theme.Elements.Where(e => drawElements && !e.Hidden))
        {
            int firstVisual = canvas.Children.Count;
            switch (element.Kind)
            {
                case ThemeElementKind.Image when element.ImageFile is { } file:
                    var source = LoadImage(themeDirectory, file);
                    var image = new Image { Source = source, Stretch = Stretch.Fill,
                        Width = source.PixelWidth * Scale(element.Width), Height = source.PixelHeight * Scale(element.Width) };
                    Place(canvas, image, element.X, element.Y);
                    break;
                case ThemeElementKind.Text or ThemeElementKind.Data:
                    var text = new TextPart(element, canvas, missing);
                    if (element.Kind == ThemeElementKind.Data) _parts.Add(text);
                    break;
                case ThemeElementKind.Bar: _parts.Add(new BarPart(element, canvas)); break;
                case ThemeElementKind.Arc: _parts.Add(new ArcPart(element, canvas)); break;
                case ThemeElementKind.Chart: _parts.Add(new ChartPart(element, canvas)); break;
                case ThemeElementKind.Panel: AddPanel(element, canvas); break;
                case ThemeElementKind.Line: AddLine(element, canvas); break;
                case ThemeElementKind.AlbumArt: _parts.Add(new AlbumArtPart(element, canvas)); break;
                case ThemeElementKind.Shortcut: _parts.Add(new ShortcutPart(element, canvas)); break;
            }
            if (element.GlowColor != null && element.Kind is not (ThemeElementKind.Image or ThemeElementKind.Chart))
                foreach (var visual in canvas.Children.OfType<FrameworkElement>().Skip(firstVisual)) visual.Effect = Glow(element);
            _visuals[element] = canvas.Children.OfType<FrameworkElement>().Skip(firstVisual).ToList();
        }
        Children.Add(canvas);
        MissingFonts = missing.ToList();
    }

    /// <summary>
    /// Sizes and positions the background picture and video: fitted to the screen, zoomed about
    /// its centre and shifted. The editor calls this directly to preview a drag without a rebuild.
    /// </summary>
    public void PlaceBackground(double zoom, double offsetX, double offsetY)
    {
        (_zoom, _shiftX, _shiftY) = (zoom, offsetX, offsetY);
        if (_still?.Source is BitmapSource picture) Place(_still, picture.PixelWidth, picture.PixelHeight, turned: false);
        if (_frame?.Source is BitmapSource frame) Place(_frame, frame.PixelWidth, frame.PixelHeight, Theme.BackgroundVideoRotation % 180 != 0);

        void Place(Image image, double width, double height, bool turned)
        {
            var r = BackgroundRect(Theme, turned ? height : width, turned ? width : height, _zoom, _shiftX, _shiftY);
            // LayoutTransform turns the box, so a turned video is sized before turning.
            image.Width = turned ? r.Height : r.Width;
            image.Height = turned ? r.Width : r.Height;
            Canvas.SetLeft(image, r.X);
            Canvas.SetTop(image, r.Y);
        }
    }

    /// <summary>Where a background of the given size lands on the theme's screen.</summary>
    public static Rect BackgroundRect(ThemeDefinition theme, double sourceWidth, double sourceHeight, double zoom, double offsetX, double offsetY)
    {
        sourceWidth = Math.Max(1, sourceWidth); sourceHeight = Math.Max(1, sourceHeight);
        double sx = theme.Width / sourceWidth, sy = theme.Height / sourceHeight;
        if (string.Equals(theme.BackgroundFit, "Cover", StringComparison.OrdinalIgnoreCase)) sx = sy = Math.Max(sx, sy);
        else if (string.Equals(theme.BackgroundFit, "Fit", StringComparison.OrdinalIgnoreCase)) sx = sy = Math.Min(sx, sy);
        zoom = double.IsFinite(zoom) && zoom > 0 ? Math.Clamp(zoom, ThemeStore.MinBackgroundZoom, ThemeStore.MaxBackgroundZoom) : 1;
        double w = sourceWidth * sx * zoom, h = sourceHeight * sy * zoom;
        return new Rect((theme.Width - w) / 2 + (double.IsFinite(offsetX) ? offsetX : 0), (theme.Height - h) / 2 + (double.IsFinite(offsetY) ? offsetY : 0), w, h);
    }

    private SensorFeedState _feed = SensorFeedState.Live;

    public void Update(ThemeDataContext data)
    {
        foreach (var part in _parts) part.Update(data);
        // Elements shown only while a reading has a value (an unpaired clock, a source without a timeline).
        foreach (var (element, visuals) in _visuals)
        {
            if (element.ShowIf is not { Length: > 0 } condition) continue;
            var shown = Shows(condition, data) ? Visibility.Visible : Visibility.Hidden;
            foreach (var visual in visuals) if (visual.Visibility != shown) visual.Visibility = shown;
        }
        // A frozen or offline feed dims every sensor reading, so a stuck 88 °C never looks live.
        if (data.SensorFeed != _feed)
        {
            _feed = data.SensorFeed;
            foreach (var (element, visuals) in _visuals)
                if (ThemeData.SensorFor(element.DataName) != null)
                    foreach (var visual in visuals) visual.Opacity = _feed == SensorFeedState.Live ? 1 : StaleOpacity;
        }
    }

    /// <summary>Turns the theme's glows off (night dimming) or back on.</summary>
    public void SetGlows(bool on)
    {
        foreach (var (element, visuals) in _visuals)
            if (element.GlowColor != null && element.Kind is not (ThemeElementKind.Image or ThemeElementKind.Chart))
                foreach (var visual in visuals) visual.Effect = on ? Glow(element) : null;
    }

    /// <summary>How visible a sensor reading stays while the feed is frozen or offline.</summary>
    public const double StaleOpacity = 0.35;

    /// <summary>Where an element is drawn, in theme pixels; empty when it is hidden or not drawn.</summary>
    public Rect BoundsOf(ThemeElement element)
    {
        if (!_visuals.TryGetValue(element, out var visuals) || visuals.Count == 0) return Rect.Empty;
        switch (element.Kind)
        {
            case ThemeElementKind.Text or ThemeElementKind.Data when visuals[0] is TextBlock block:
                var text = new FormattedText(block.Text.Length > 0 ? block.Text : " ", System.Globalization.CultureInfo.CurrentUICulture,
                    System.Windows.FlowDirection.LeftToRight, new Typeface(block.FontFamily, block.FontStyle, block.FontWeight, block.FontStretch),
                    block.FontSize, Brushes.White, 1.0);
                double w = text.WidthIncludingTrailingWhitespace, h = text.Height;
                if (element.Width > 0) w = Math.Min(w, element.Width);
                double left = (element.Font?.Alignment ?? ThemeTextAlignment.Left) switch
                {
                    ThemeTextAlignment.Center => element.X - w / 2,
                    ThemeTextAlignment.Right => element.X - w,
                    _ => element.X
                };
                return new Rect(left, element.Y, w, h);
            case ThemeElementKind.Arc:
                double t = Math.Max(0, element.Thickness);
                return new Rect(element.X - t / 2, element.Y - t / 2, Math.Max(1, element.Diameter) + t, Math.Max(1, element.Diameter) + t);
            case ThemeElementKind.Image when visuals[0] is Image image:
                return new Rect(element.X, element.Y, image.Width, image.Height);
            case ThemeElementKind.Line:
                var line = new Rect(new Point(element.X, element.Y), new Point(element.X + element.Width, element.Y + element.Height));
                line.Inflate(Math.Max(2, element.LineWidth / 2), Math.Max(2, element.LineWidth / 2));
                return line;
            default:
                return new Rect(element.X, element.Y, Math.Max(1, element.Width), Math.Max(1, element.Height));
        }
    }

    /// <summary>
    /// The topmost element with a tap action at a point, in theme pixels. Elements smaller than
    /// <paramref name="minTarget"/> get a finger-sized target around them.
    /// </summary>
    public ThemeElement? ElementAt(Point point, double minTarget = 64)
    {
        for (int i = Theme.Elements.Count - 1; i >= 0; i--)
        {
            var element = Theme.Elements[i];
            if (element.Hidden || ThemeActions.Effective(element) == ThemeAction.None) continue;
            var bounds = BoundsOf(element);
            if (bounds.IsEmpty) continue;
            bounds.Inflate(Math.Max(0, (minTarget - bounds.Width) / 2), Math.Max(0, (minTarget - bounds.Height) / 2));
            if (bounds.Contains(point)) return element;
        }
        return null;
    }

    /// <summary>Shifts an element's drawing without rebuilding (the editor's drag preview); 0,0 resets.</summary>
    public void Offset(ThemeElement element, double dx, double dy)
    {
        if (!_visuals.TryGetValue(element, out var visuals)) return;
        foreach (var visual in visuals) visual.RenderTransform = dx == 0 && dy == 0 ? Transform.Identity : new TranslateTransform(dx, dy);
    }

    // A hidden overlay should not keep decoding video.
    public void Pause() => _video?.Pause();
    public void Resume() => _video?.Resume();

    public void Dispose() => _video?.Dispose();

    // Panels and lines never change, so they are drawn once and not updated.
    private static void AddPanel(ThemeElement e, Canvas canvas)
    {
        double w = Math.Max(1, e.Width), h = Math.Max(1, e.Height), r = Math.Clamp(e.CornerRadius, 0, Math.Min(w, h) / 2);
        Shape shape;
        if (string.Equals(e.CornerStyle, "Cut", StringComparison.OrdinalIgnoreCase) && r > 0)
            shape = new Polygon { Points = [new(r, 0), new(w - r, 0), new(w, r), new(w, h - r), new(w - r, h), new(r, h), new(0, h - r), new(0, r)] };
        else
            shape = new Rectangle { Width = w, Height = h, RadiusX = r, RadiusY = r };
        shape.Fill = PanelFill(e);
        if (e.BorderWidth > 0) { shape.Stroke = Solid(e.BorderColor, Colors.Transparent); shape.StrokeThickness = e.BorderWidth; }
        if (e.Bevel) { shape.Stroke = OneUi.Bevel; shape.StrokeThickness = Math.Max(1, Math.Min(w, h) / 48); }
        Place(canvas, shape, e.X, e.Y);
    }

    private static Brush PanelFill(ThemeElement e)
    {
        var from = ParseColor(e.FillColor, Colors.Transparent);
        if (e.GradientColor is null) return Solid(e.FillColor, Colors.Transparent);
        var to = ParseColor(e.GradientColor, from);
        Brush brush;
        if (string.Equals(e.GradientStyle, "Radial", StringComparison.OrdinalIgnoreCase))
            brush = new RadialGradientBrush(from, to);
        else
        {
            double a = e.GradientAngle * Math.PI / 180;
            // Start and end points across the box for the angle (0 = left to right, 90 = top to bottom).
            var start = new Point(0.5 - Math.Cos(a) / 2, 0.5 - Math.Sin(a) / 2);
            var end = new Point(0.5 + Math.Cos(a) / 2, 0.5 + Math.Sin(a) / 2);
            brush = new LinearGradientBrush(from, to, start, end);
        }
        brush.Freeze();
        return brush;
    }

    private static void AddLine(ThemeElement e, Canvas canvas)
    {
        var line = new Line
        {
            X1 = e.X, Y1 = e.Y, X2 = e.X + e.Width, Y2 = e.Y + e.Height,
            Stroke = Solid(e.LineColor, Colors.White), StrokeThickness = Math.Max(0.5, e.LineWidth),
            StrokeDashArray = e.SegmentWidth > 0 && e.LineWidth > 0
                ? new DoubleCollection { e.SegmentWidth / e.LineWidth, Math.Max(0.01, e.SegmentGap / e.LineWidth) } : null
        };
        canvas.Children.Add(line);
    }

    private static System.Windows.Media.Effects.DropShadowEffect Glow(ThemeElement e)
    {
        var glow = new System.Windows.Media.Effects.DropShadowEffect
        {
            Color = ParseColor(e.GlowColor, Colors.White), ShadowDepth = 0, BlurRadius = Math.Clamp(e.GlowSize, 1, 80),
            Opacity = ParseColor(e.GlowColor, Colors.White).A / 255.0, RenderingBias = System.Windows.Media.Effects.RenderingBias.Performance
        };
        glow.Freeze();
        return glow;
    }

    private static double Scale(double factor) => factor is > 0 and < 20 ? factor : 1;

    private static BitmapImage LoadImage(string themeDirectory, string file)
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad; // no file lock while the theme runs
        bitmap.UriSource = new Uri(ThemeStore.ResolveFile(themeDirectory, file));
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    private static void Place(Canvas canvas, UIElement element, double x, double y)
    {
        Canvas.SetLeft(element, x);
        Canvas.SetTop(element, y);
        canvas.Children.Add(element);
    }

    internal static Color ParseColor(string? value, Color fallback)
    {
        try { return value is { Length: > 0 } ? (Color)ColorConverter.ConvertFromString(value) : fallback; }
        catch (FormatException) { return fallback; }
    }

    private static SolidColorBrush Solid(string? value, Color fallback)
    {
        var brush = new SolidColorBrush(ParseColor(value, fallback));
        brush.Freeze();
        return brush;
    }

    private static Brush Fill(ThemeElement e, bool vertical = false)
    {
        var front = ParseColor(e.ForeColor, Colors.White);
        if (e.GradientColor is null) return Solid(e.ForeColor, Colors.White);
        var brush = new LinearGradientBrush(front, ParseColor(e.GradientColor, front), vertical ? 90 : 0);
        brush.Freeze();
        return brush;
    }

    /// <summary>
    /// A wide picture (a video's 16:9 thumbnail) in a round frame is shown whole as a rounded rectangle across the
    /// circle, not cropped to it (which cut off its own title); covers and square frames are unchanged.
    /// </summary>
    internal static bool ShowsWhole(bool round, BitmapSource? art) => round && art is { PixelHeight: > 0 } a && a.PixelWidth / (double)a.PixelHeight > 1.25;

    /// <summary>Whether a reading has a value to show (a number, or text other than the missing mark).</summary>
    internal static bool Shows(string name, ThemeDataContext data) =>
        ThemeData.Read(name, null, false, data) is var v && (v.Number is { } n && double.IsFinite(n) || v.Text.Length > 0 && v.Text != ThemeData.Missing);

    /// <summary>The value at the top of a chart: its Maximum, or 15% above its peak but never below MinimumScale.</summary>
    internal static double ChartScale(ThemeElement element, double peak) =>
        element.Maximum > 0 ? element.Maximum : Math.Max(Math.Max(1, element.MinimumScale), peak * 1.15);

    /// <summary>0 normal, 1 warning, 2 critical, from the element's thresholds and the reading that decides (°C for temperatures).</summary>
    internal static int AlertLevel(ThemeElement e, ThemeDataContext data)
    {
        if (e.WarnAt == null && e.CriticalAt == null) return 0;
        if (ThemeData.Read(e.ColorBy ?? e.DataName, null, false, data).Number is not { } v || !double.IsFinite(v)) return 0;
        bool Past(double? t) => t is { } l && (e.LowIsBad ? v <= l : v >= l);
        return Past(e.CriticalAt) ? 2 : Past(e.WarnAt) ? 1 : 0;
    }

    /// <summary>The warning and critical brushes for an element (amber and red unless it names its own).</summary>
    private static (Brush Warn, Brush Critical) AlertBrushes(ThemeElement e) =>
        (Solid(e.WarnColor ?? "#FFFFB547", Colors.Orange), Solid(e.CriticalColor ?? "#FFFF5A6E", Colors.Red));

    private static double Fraction(double? value, double maximum) =>
        value is { } v && maximum > 0 ? Math.Clamp(v / maximum, 0, 1) : 0;

    // --- Parts ---------------------------------------------------------------------------

    private sealed class TextPart : IPart
    {
        private readonly ThemeElement _element;
        private readonly TextBlock _block;
        private readonly Brush _normal, _warn, _critical;
        private int _level;

        public TextPart(ThemeElement element, Canvas canvas, ISet<string> missing)
        {
            _element = element;
            var font = element.Font ?? new ThemeFont();
            var match = ThemeFonts.Resolve(font.Family);
            if (!match.Found) missing.Add(font.Family);
            _block = new TextBlock
            {
                Text = element.Kind == ThemeElementKind.Text ? element.Text ?? "" : ThemeData.Missing,
                FontFamily = match.Family,
                FontSize = font.Size * 96 / 72, // L-Connect sizes are points
                FontWeight = font.Bold ? FontWeights.Bold : match.Weight,
                FontStyle = font.Italic ? FontStyles.Italic : match.Style,
                FontStretch = match.Stretch,
                Foreground = _normal = Solid(font.Color, Colors.White),
                TextWrapping = TextWrapping.NoWrap,
                TextAlignment = font.Alignment switch
                {
                    ThemeTextAlignment.Center => TextAlignment.Center,
                    ThemeTextAlignment.Right => TextAlignment.Right,
                    _ => TextAlignment.Left
                }
            };
            // X is the left edge, center or right edge of the text depending on alignment, so
            // a wide fixed box anchored there keeps the text in place as its value changes.
            // A set Width caps the box instead, and longer text ends in an ellipsis.
            double box = element.Width > 0 ? element.Width : 4000;
            if (element.Width > 0) _block.TextTrimming = TextTrimming.CharacterEllipsis;
            if (element.Width > 0 && element.MaxLines > 1)
            {
                _block.TextWrapping = TextWrapping.Wrap;
                _block.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
                _block.LineHeight = Math.Round(_block.FontSize * 1.22);
                _block.MaxHeight = _block.LineHeight * element.MaxLines;
            }
            double left = font.Alignment switch
            {
                ThemeTextAlignment.Center => element.X - box / 2,
                ThemeTextAlignment.Right => element.X - box,
                _ => element.X
            };
            if (font.Alignment != ThemeTextAlignment.Left || element.Width > 0) _block.Width = box;
            (_warn, _critical) = AlertBrushes(element);
            Place(canvas, _block, left, element.Y);
        }

        public void Update(ThemeDataContext data)
        {
            var value = ThemeData.Read(_element.DataName, _element.DataFormat, _element.Fahrenheit, data, _element.Slot);
            string text = ThemeData.Display(value, _element.ShowUnit, _element.SpacedUnit);
            if (_block.Text != text)
            {
                _block.Text = text;
                // The play triangle is centred by eye, not by its box (see OneUi.PlayNudge): padding on the left
                // of a centred line moves its middle right by half the padding.
                if (_element.Font?.Alignment == ThemeTextAlignment.Center)
                    _block.Padding = new Thickness(2 * OneUi.NudgeX(text, _block.FontSize), 0, 0, 0);
            }
            int level = AlertLevel(_element, data);
            if (level != _level) { _level = level; _block.Foreground = level == 2 ? _critical : level == 1 ? _warn : _normal; }
        }
    }

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<ImageSource, ImageSource> GrayCache = new();

    /// <summary>
    /// A picture in shades of grey, its transparency kept (icons stay shaped), made once per picture. Pictures that
    /// are not bitmaps are returned as they are.
    /// </summary>
    internal static ImageSource? Gray(ImageSource? source)
    {
        if (source is not BitmapSource bitmap) return source;
        lock (GrayCache)
        {
            if (GrayCache.TryGetValue(source, out var cached)) return cached;
            var argb = bitmap.Format == PixelFormats.Bgra32 ? bitmap : new FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0);
            int w = argb.PixelWidth, h = argb.PixelHeight;
            var px = new byte[w * h * 4];
            argb.CopyPixels(px, w * 4, 0);
            for (int i = 0; i < px.Length; i += 4)
            {
                byte y = (byte)Math.Round(px[i] * 0.0722 + px[i + 1] * 0.7152 + px[i + 2] * 0.2126);
                px[i] = px[i + 1] = px[i + 2] = y;
            }
            var gray = BitmapSource.Create(w, h, bitmap.DpiX, bitmap.DpiY, PixelFormats.Bgra32, null, px, w * 4);
            gray.Freeze();
            GrayCache.AddOrUpdate(source, gray);
            return gray;
        }
    }

    private sealed class AlbumArtPart : IPart
    {
        private readonly Image _cover;
        private readonly bool _gray, _round;
        private readonly TextBlock _placeholder;
        private readonly Border _box;
        private readonly Brush _fill;
        private readonly double _w, _h, _r;
        private object? _shown = new();

        public AlbumArtPart(ThemeElement element, Canvas canvas)
        {
            _gray = element.Grayscale;
            double w = Math.Max(1, element.Width), h = Math.Max(1, element.Height), r = Math.Max(0, element.CornerRadius);
            _cover = new Image { Stretch = Stretch.UniformToFill, Width = w, Height = h, Clip = new RectangleGeometry(new Rect(0, 0, w, h), r, r) };
            _placeholder = new TextBlock
            {
                Text = "\uE8D6", FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"), FontSize = Math.Min(w, h) * 0.38,
                Foreground = Solid(element.LineColor, Colors.White), HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = System.Windows.VerticalAlignment.Center
            };
            (_w, _h, _r) = (w, h, r);
            _round = r >= Math.Min(w, h) / 2 - 1;
            _fill = Solid(element.FillColor, Colors.Transparent);
            var box = new Border { Width = w, Height = h, CornerRadius = new CornerRadius(r), Background = _fill };
            _box = box;
            var layers = new Grid();
            layers.Children.Add(_placeholder);
            layers.Children.Add(_cover);
            box.Child = layers;
            Place(canvas, box, element.X, element.Y);
        }

        private void Fit(BitmapSource? art)
        {
            if (ShowsWhole(_round, art))
            {
                double ih = Math.Min(_h, _w * art!.PixelHeight / art.PixelWidth), corner = Math.Min(_w, _h) * 0.08;
                _cover.Stretch = Stretch.Uniform;
                _cover.Clip = new RectangleGeometry(new Rect(0, (_h - ih) / 2, _w, ih), corner, corner);
                _box.Background = Brushes.Transparent;
            }
            else
            {
                _cover.Stretch = Stretch.UniformToFill;
                _cover.Clip = new RectangleGeometry(new Rect(0, 0, _w, _h), _r, _r);
                _box.Background = _fill;
            }
        }

        public void Update(ThemeDataContext data)
        {
            var art = data.Media?.Art;
            if (ReferenceEquals(art, _shown)) return;
            _shown = art;
            Fit(art);
            _cover.Source = _gray ? Gray(art) : art;
            _placeholder.Visibility = art is null ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    // A tile with the icon of the user's shortcut in its slot; a glyph stands in when there is no icon,
    // and a faint plus marks an empty slot.
    private sealed class ShortcutPart : IPart
    {
        private readonly ThemeElement _element;
        private readonly Image _icon, _cover;
        private readonly Border _iconFrame, _coverFrame;
        private readonly Grid _iconBox;
        private readonly TextBlock _glyph;
        private readonly Brush _tint, _faint, _plain;
        private readonly bool _tall, _tinted;
        private readonly Border _tile, _round;
        private readonly TextBlock? _label;
        private readonly double _glyphSize, _roundGlyph;
        private object? _shown = new();

        /// <summary>A soft bevel: light along the top edge, shade along the bottom, for a slightly raised look.</summary>
        private static readonly Brush Bevel = BevelBrush();

        private static readonly Brush RoundFill = Frozen(new LinearGradientBrush(Color.FromArgb(0x4D, 0xFF, 0xFF, 0xFF), Color.FromArgb(0x1F, 0xFF, 0xFF, 0xFF), 90));

        private static Brush Frozen(Brush brush) { brush.Freeze(); return brush; }

        /// <summary>A tile backdrop in the icon's color: brighter at the top left, deeper toward the bottom right.</summary>
        internal static Brush TintFor(Color accent)
        {
            Color Shade(double k, byte alpha) => Color.FromArgb(alpha, (byte)(accent.R * k), (byte)(accent.G * k), (byte)(accent.B * k));
            return Frozen(new LinearGradientBrush(Shade(1, 0x70), Shade(0.55, 0x48), new Point(0, 0), new Point(1, 1)));
        }

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

        public ShortcutPart(ThemeElement element, Canvas canvas)
        {
            _element = element;
            double w = Math.Max(1, element.Width), h = Math.Max(1, element.Height), r = Math.Clamp(element.CornerRadius, 0, Math.Min(w, h) / 2);
            _tall = h >= w * 1.15;
            _tinted = string.Equals(element.TileStyle, "Tinted", StringComparison.OrdinalIgnoreCase);
            // A label takes the bottom fifth of the tile; the icon centres in the rest.
            double label = element.ShowLabel ? Math.Clamp(h * 0.2, 14, 34) : 0, room = Math.Min(w, h - label);
            double size = room * (label > 0 ? 0.72 : 0.68), iconRadius = size * 0.22;
            _tint = Solid(element.LineColor, Colors.White);
            var faint = ParseColor(element.LineColor, Colors.White);
            _faint = new SolidColorBrush(Color.FromArgb((byte)(faint.A * 0.45), faint.R, faint.G, faint.B));
            _faint.Freeze();

            // The icon: larger than before, with a soft shadow; a solid (square) icon is also rounded and bevelled.
            _icon = new Image { Width = size, Height = size, Stretch = Stretch.Uniform };
            RenderOptions.SetBitmapScalingMode(_icon, BitmapScalingMode.HighQuality);
            _iconFrame = new Border { Width = size, Height = size, CornerRadius = new CornerRadius(iconRadius), BorderBrush = Bevel, BorderThickness = new Thickness(1.5) };
            _iconBox = new Grid { Width = size, Height = size, HorizontalAlignment = System.Windows.HorizontalAlignment.Center, VerticalAlignment = System.Windows.VerticalAlignment.Center,
                Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = size * 0.14, ShadowDepth = size * 0.035, Direction = 270, Opacity = 0.4, Color = Colors.Black } };
            _iconBox.Children.Add(_icon);
            _iconBox.Children.Add(_iconFrame);
            _iconBox.Tag = new RectangleGeometry(new Rect(0, 0, size, size), iconRadius, iconRadius);

            // Tall tiles show a game's cover art filling the tile, clipped to its corners, under the same bevel.
            _cover = new Image { Width = w, Height = h, Stretch = Stretch.UniformToFill, Clip = new RectangleGeometry(new Rect(0, 0, w, h), r, r) };
            RenderOptions.SetBitmapScalingMode(_cover, BitmapScalingMode.HighQuality);
            _coverFrame = new Border { Width = w, Height = h, CornerRadius = new CornerRadius(r), BorderBrush = Bevel, BorderThickness = new Thickness(1.5) };

            _glyphSize = room * 0.48;
            _glyph = new TextBlock { FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"), FontSize = _glyphSize,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center, VerticalAlignment = System.Windows.VerticalAlignment.Center };
            // System actions on tinted tiles: a round glass button in place of the tile.
            double d = room * 0.82;
            _roundGlyph = d * 0.42;
            _round = new Border { Width = d, Height = d, CornerRadius = new CornerRadius(d / 2), Visibility = Visibility.Collapsed,
                Background = RoundFill, BorderBrush = Bevel, BorderThickness = new Thickness(1.5),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center, VerticalAlignment = System.Windows.VerticalAlignment.Center };
            var layers = new Grid();
            layers.Children.Add(_round);
            layers.Children.Add(_glyph);
            layers.Children.Add(_iconBox);
            var content = new Grid();
            content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(label) });
            content.Children.Add(layers);
            if (label > 0)
            {
                var labelColor = ParseColor(element.LineColor, Colors.White);
                var labelBrush = new SolidColorBrush(Color.FromArgb((byte)(labelColor.A * 0.8), labelColor.R, labelColor.G, labelColor.B));
                labelBrush.Freeze();
                _label = new TextBlock { FontFamily = new FontFamily("Segoe UI Semibold"), FontSize = label * 0.62, Foreground = labelBrush,
                    TextTrimming = TextTrimming.CharacterEllipsis, TextAlignment = TextAlignment.Center, Padding = new Thickness(6, 0, 6, label * 0.18),
                    VerticalAlignment = System.Windows.VerticalAlignment.Center };
                Grid.SetRow(_label, 1);
                content.Children.Add(_label);
            }
            _plain = PanelFill(element);
            _tile = new Border { Width = w, Height = h, CornerRadius = new CornerRadius(r), Background = _plain, Child = content };
            if (element.BorderWidth > 0) { _tile.BorderBrush = Solid(element.BorderColor, Colors.Transparent); _tile.BorderThickness = new Thickness(element.BorderWidth); }
            Place(canvas, _tile, element.X, element.Y);
            Place(canvas, _cover, element.X, element.Y);
            Place(canvas, _coverFrame, element.X, element.Y);
        }

        public void Update(ThemeDataContext data)
        {
            var shortcut = data.Shortcut(_element.Slot);
            if (ReferenceEquals(shortcut, _shown)) return;
            _shown = shortcut;
            bool cover = _tall && shortcut?.Cover != null;
            _cover.Source = cover ? (_element.Grayscale ? Gray(shortcut!.Cover) : shortcut!.Cover) : null;
            _cover.Visibility = _coverFrame.Visibility = cover ? Visibility.Visible : Visibility.Collapsed;
            bool icon = !cover && shortcut?.Icon != null;
            _icon.Source = icon ? (_element.Grayscale ? Gray(shortcut!.Icon) : shortcut!.Icon) : null;
            _iconBox.Visibility = icon ? Visibility.Visible : Visibility.Collapsed;
            bool solid = icon && shortcut!.IconSolid;
            _icon.Clip = solid ? (Geometry)_iconBox.Tag : null;
            _iconFrame.Visibility = solid ? Visibility.Visible : Visibility.Collapsed;
            _glyph.Text = shortcut is null ? ShortcutView.EmptyGlyph : shortcut.Glyph;
            _glyph.Foreground = shortcut is null ? _faint : _tint;
            _glyph.Visibility = cover || icon ? Visibility.Collapsed : Visibility.Visible;
            if (_label != null) _label.Text = cover ? "" : shortcut?.Name ?? "";
            bool round = _tinted && shortcut is { IsAction: true };
            _round.Visibility = round ? Visibility.Visible : Visibility.Collapsed;
            _glyph.FontSize = round ? _roundGlyph : _glyphSize;
            if (_tinted)
                _tile.Background = round ? Brushes.Transparent : icon && shortcut!.Accent is { } accent ? TintFor(accent) : _plain;
        }
    }

    private sealed class BarPart : IPart
    {
        private readonly ThemeElement _element;
        private readonly Rectangle? _fill;
        private readonly Rectangle[] _segments = [];
        private readonly Brush _front, _back, _warn, _critical;
        private int _lit = -1, _level;

        public BarPart(ThemeElement element, Canvas canvas)
        {
            _element = element;
            _front = Fill(element);
            _back = Solid(element.BackColor, Colors.Transparent);
            (_warn, _critical) = AlertBrushes(element);
            double w = Math.Max(1, element.Width), h = Math.Max(1, element.Height);
            if (element.SegmentWidth > 0)
            {
                int count = Math.Max(1, (int)((w + element.SegmentGap) / (element.SegmentWidth + element.SegmentGap)));
                _segments = new Rectangle[count];
                for (int i = 0; i < count; i++)
                {
                    _segments[i] = new Rectangle { Width = element.SegmentWidth, Height = h, Fill = _back,
                        RadiusX = element.CornerRadius, RadiusY = element.CornerRadius };
                    Place(canvas, _segments[i], element.X + i * (element.SegmentWidth + element.SegmentGap), element.Y);
                }
                return;
            }
            Place(canvas, new Rectangle { Width = w, Height = h, Fill = _back, RadiusX = element.CornerRadius, RadiusY = element.CornerRadius,
                Stroke = element.BorderWidth > 0 ? Solid(element.BorderColor, Colors.Transparent) : null, StrokeThickness = element.BorderWidth },
                element.X, element.Y);
            _fill = new Rectangle { Width = 0, Height = h, Fill = _front, RadiusX = element.CornerRadius, RadiusY = element.CornerRadius };
            Place(canvas, _fill, element.X, element.Y);
        }

        public void Update(ThemeDataContext data)
        {
            double fraction = Fraction(ThemeData.Read(_element.DataName, _element.DataFormat, false, data).Number, _element.Maximum);
            int level = AlertLevel(_element, data);
            var front = level == 2 ? _critical : level == 1 ? _warn : _front;
            if (_fill != null)
            {
                double width = Math.Round(fraction * Math.Max(1, _element.Width));
                if (_fill.Width != width) _fill.Width = width;
                if (level != _level) { _level = level; _fill.Fill = front; }
                return;
            }
            int lit = (int)Math.Round(fraction * _segments.Length);
            if (lit == _lit && level == _level) return;
            _lit = lit;
            _level = level;
            for (int i = 0; i < _segments.Length; i++) _segments[i].Fill = i < lit ? front : _back;
        }
    }

    private sealed class ArcPart : IPart
    {
        private readonly ThemeElement _element;
        private readonly Path _value;
        private readonly Point _center;
        private readonly double _radius;
        private readonly Brush _front, _warn, _critical;
        private double _shown = -1;
        private int _level;

        public ArcPart(ThemeElement element, Canvas canvas)
        {
            _element = element;
            _radius = Math.Max(1, element.Diameter) / 2;
            _center = new Point(element.X + _radius, element.Y + _radius);
            var cap = element.RoundCaps ? PenLineCap.Round : PenLineCap.Flat;
            DoubleCollection? dashes = element.SegmentWidth > 0 && element.Thickness > 0
                ? new DoubleCollection { element.SegmentWidth / element.Thickness, Math.Max(0.01, element.SegmentGap / element.Thickness) }
                : null;
            canvas.Children.Add(new Path
            {
                Data = Arc(_center, _radius, element.StartAngle, element.SweepAngle),
                Stroke = Solid(element.BackColor, Colors.Transparent), StrokeThickness = element.Thickness,
                StrokeStartLineCap = cap, StrokeEndLineCap = cap, StrokeDashArray = dashes
            });
            _value = new Path
            {
                Stroke = _front = Fill(element), StrokeThickness = element.Thickness,
                StrokeStartLineCap = cap, StrokeEndLineCap = cap, StrokeDashArray = dashes?.Clone()
            };
            (_warn, _critical) = AlertBrushes(element);
            canvas.Children.Add(_value);
        }

        public void Update(ThemeDataContext data)
        {
            double fraction = Fraction(ThemeData.Read(_element.DataName, _element.DataFormat, false, data).Number, _element.Maximum);
            double sweep = Math.Round(fraction * _element.SweepAngle, 1);
            int level = AlertLevel(_element, data);
            if (level != _level) { _level = level; _value.Stroke = level == 2 ? _critical : level == 1 ? _warn : _front; }
            if (sweep == _shown) return;
            _shown = sweep;
            _value.Data = sweep <= 0 ? null : Arc(_center, _radius, _element.StartAngle, sweep);
        }

        internal static Geometry Arc(Point center, double radius, double start, double sweep)
        {
            sweep = Math.Min(sweep, 359.99); // an arc cannot end where it starts
            Point At(double degrees) => new(center.X + radius * Math.Cos(degrees * Math.PI / 180),
                                            center.Y + radius * Math.Sin(degrees * Math.PI / 180));
            var figure = new PathFigure { StartPoint = At(start), IsClosed = false };
            figure.Segments.Add(new ArcSegment(At(start + sweep), new Size(radius, radius), 0, sweep > 180, SweepDirection.Clockwise, true));
            var geometry = new PathGeometry([figure]);
            geometry.Freeze();
            return geometry;
        }
    }

    private sealed class ChartPart : IPart
    {
        private readonly ThemeElement _element;
        private readonly Queue<double> _history = new();
        private readonly Polyline _line;
        private readonly Polygon _area;
        private readonly Polyline? _warnLine;
        private readonly Line? _guide;

        public ChartPart(ThemeElement element, Canvas canvas)
        {
            _element = element;
            double w = Math.Max(1, element.Width), h = Math.Max(1, element.Height);
            var host = new Canvas { Width = w, Height = h, ClipToBounds = true };
            _area = new Polygon { Fill = Solid(element.FillColor, Colors.Transparent) };
            _line = new Polyline { Stroke = Solid(element.LineColor, Colors.White), StrokeThickness = Math.Max(0.5, element.LineWidth),
                StrokeLineJoin = PenLineJoin.Round };
            host.Children.Add(_area);
            if (element.GuideAt is > 0)
            {
                _guide = new Line { X1 = 0, X2 = w, Stroke = Solid(element.LineColor, Colors.White), Opacity = 0.45, StrokeThickness = 1.5,
                    StrokeDashArray = [4, 4] };
                host.Children.Add(_guide);
            }
            host.Children.Add(_line);
            if (element.WarnAt is > 0 && !element.LowIsBad)
            {
                // The same line in the warning color, shown only above the warning level (by its clip).
                _warnLine = new Polyline { Stroke = AlertBrushes(element).Warn, StrokeThickness = _line.StrokeThickness, StrokeLineJoin = PenLineJoin.Round };
                host.Children.Add(_warnLine);
            }
            if (element.BorderWidth > 0)
                host.Children.Add(new Rectangle { Width = w, Height = h, Stroke = Solid(element.BorderColor, Colors.Transparent),
                    StrokeThickness = element.BorderWidth });
            Place(canvas, host, element.X, element.Y);
        }

        public void Update(ThemeDataContext data)
        {
            var value = ThemeData.Read(_element.DataName, _element.DataFormat, false, data).Number;
            _history.Enqueue(value is { } v && double.IsFinite(v) ? Math.Max(0, v) : 0);
            while (_history.Count > Math.Max(2, _element.Samples)) _history.Dequeue();

            double w = Math.Max(1, _element.Width), h = Math.Max(1, _element.Height);
            double max = ChartScale(_element, _history.Max());
            var points = new PointCollection();
            // Newest sample at the right edge, older samples moving left. Until the chart is full, what there is spreads
            // across the whole width (a single reading is a flat line) instead of a sliver at the right edge.
            int slots = Math.Max(2, Math.Min(_history.Count, Math.Max(2, _element.Samples)));
            double step = w / (slots - 1);
            if (_history.Count == 1) points.Add(new Point(0, h - Math.Clamp(_history.Peek() / max, 0, 1) * h));
            int i = Math.Max(0, slots - _history.Count);
            foreach (var sample in _history)
                points.Add(new Point(i++ * step, h - Math.Clamp(sample / max, 0, 1) * h));
            points.Freeze();
            _line.Points = points;
            double Y(double v) => h - Math.Clamp(v / max, 0, 1) * h;
            if (_guide != null) _guide.Y1 = _guide.Y2 = Math.Round(Y(_element.GuideAt!.Value)) + 0.5;
            if (_warnLine != null)
            {
                _warnLine.Points = points;
                double top = Y(_element.WarnAt!.Value);
                _warnLine.Clip = top <= 0 ? Geometry.Empty : new RectangleGeometry(new Rect(0, 0, w, top));
            }
            var area = new PointCollection(points) { new Point(w, h), new Point(points[0].X, h) };
            area.Freeze();
            _area.Points = area;
        }
    }
}
