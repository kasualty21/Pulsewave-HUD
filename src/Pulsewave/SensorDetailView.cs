using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Brush = System.Windows.Media.Brush;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Orientation = System.Windows.Controls.Orientation;
using VerticalAlignment = System.Windows.VerticalAlignment;
using Color = System.Windows.Media.Color;
using FontFamily = System.Windows.Media.FontFamily;
using Point = System.Windows.Point;
using Size = System.Windows.Size;

namespace Pulsewave;

public sealed record DetailGroup(string Title, IReadOnlyList<string> Metrics);

/// <summary>Details page wording and numbers, separate from WPF so it is testable.</summary>
public static class SensorDetails
{
    public static readonly IReadOnlyList<DetailGroup> Groups =
    [
        new("CPU", ["CpuLoad", "CpuTemp", "CpuPower", "CpuFan"]),
        new("GPU", ["GpuLoad", "GpuTemp", "GpuPower", "GpuFps", "GpuVram"]),
        new("SYSTEM", ["MemoryLoad", "NetDownload", "NetUpload"])
    ];

    /// <summary>
    /// The page and charted reading for a sensor. Sensors without session statistics (clocks,
    /// voltages, case fans, drives) open their group's first reading.
    /// </summary>
    public static (int Group, string Metric) For(string? sensor)
    {
        for (int g = 0; g < Groups.Count; g++)
            if (Groups[g].Metrics.FirstOrDefault(m => string.Equals(m, sensor, StringComparison.OrdinalIgnoreCase)) is { } metric)
                return (g, metric);
        int group = sensor?.StartsWith("Cpu", StringComparison.OrdinalIgnoreCase) == true ? 0
            : sensor?.StartsWith("Gpu", StringComparison.OrdinalIgnoreCase) == true ? 1 : 2;
        return (group, Groups[group].Metrics[0]);
    }

    public static string Label(string metric) => metric switch
    {
        "CpuLoad" or "GpuLoad" => "Load",
        "CpuTemp" or "GpuTemp" => "Temperature",
        "CpuPower" or "GpuPower" => "Power",
        "CpuFan" => "Fan",
        "GpuFps" => "Frame rate",
        "GpuVram" => "Video memory",
        "MemoryLoad" => "Memory",
        "NetDownload" => "Download",
        "NetUpload" => "Upload",
        _ => metric
    };

    public static string Format(string metric, double? value)
    {
        if (value is not { } v || !double.IsFinite(v)) return "--";
        var c = CultureInfo.CurrentCulture;
        return metric switch
        {
            "CpuLoad" or "GpuLoad" or "MemoryLoad" => v.ToString("F0", c) + "%",
            "CpuTemp" or "GpuTemp" => v.ToString("F0", c) + "°C",
            "CpuPower" or "GpuPower" => v.ToString("F0", c) + " W",
            "CpuFan" => v.ToString("F0", c) + " RPM",
            "GpuFps" => v.ToString("F0", c) + " FPS",
            // HWiNFO reports video memory in MB; a small number is already GB.
            "GpuVram" => (v >= 64 ? v / 1024 : v).ToString("F1", c) + " GB",
            "NetDownload" or "NetUpload" => v.ToString(v < 10 ? "F2" : "F1", c) + " MB/s",
            _ => v.ToString("F1", c)
        };
    }

    /// <summary>"min 34°C · avg 51°C · max 88°C" for the session.</summary>
    public static string Range(string metric, MetricStatistics? stats) =>
        stats is null || stats.Samples == 0 ? "No readings yet this session"
            : $"min {Format(metric, stats.Minimum)}  ·  avg {Format(metric, stats.Average)}  ·  max {Format(metric, stats.Maximum)}";

    /// <summary>The telemetry alert limit drawn on the chart, for the readings that have one.</summary>
    public static double? Limit(string metric, MonitorSettings settings) => metric switch
    {
        "CpuTemp" => settings.CpuTemperatureAlertC,
        "GpuTemp" => settings.GpuTemperatureAlertC,
        "MemoryLoad" => settings.MemoryAlertPercent,
        _ => null
    };

    /// <summary>The top of the chart: 100 for percentages, otherwise a little above the highest point and the alert limit.</summary>
    public static double ChartMaximum(string metric, IEnumerable<double> values, double? limit)
    {
        if (metric is "CpuLoad" or "GpuLoad" or "MemoryLoad") return 100;
        double peak = values.DefaultIfEmpty(0).Max();
        if (limit is { } l) peak = Math.Max(peak, l);
        return Math.Max(1, peak * 1.1);
    }

    /// <summary>
    /// The page's drawing size: the display's shape with its short side 480 units, so text has
    /// the same size relative to the screen on a bar, a HYTE Y70 or a tablet.
    /// </summary>
    public static Size LogicalSize(int displayWidth, int displayHeight)
    {
        double w = Math.Max(1, displayWidth), h = Math.Max(1, displayHeight);
        return w >= h ? new Size(Math.Round(480 * w / h), 480) : new Size(480, Math.Round(480 * h / w));
    }
}

/// <summary>
/// The details page a tap on a readout opens, in the HUD's Halo / One UI look: CPU, GPU and System as
/// tabs; every reading of the page as a frosted row with its session minimum, average and maximum (tap
/// one to chart it; the charted one is filled light); and a chart of its last ten minutes with the
/// current value large and any alert limit dashed. ✕ closes it, as do 30 seconds without a tap.
/// </summary>
public sealed class SensorDetailView : Grid
{
    private static readonly Brush Sky = OneUi.Frozen(0xFF, 0x7F, 0xD8, 0xFF), Pink = OneUi.Frozen(0xFF, 0xFF, 0x8F, 0xB1),
        GridLine = OneUi.Frozen(0x1F, 0xFF, 0xFF, 0xFF), AreaFill = AreaBrush();

    private readonly TelemetryEngine _telemetry;
    private readonly MonitorSettings _settings;
    private readonly Func<int, string> _subtitle;
    private readonly DispatcherTimer _idle = new() { Interval = TimeSpan.FromSeconds(30) };
    private readonly TextBlock _title = OneUi.Label("", 32, bold: true), _hardware = OneUi.Label("", 17, OneUi.Secondary);
    private readonly StackPanel _rows = new();
    private readonly List<Border> _tabs = new();
    private readonly Dictionary<string, (Border Card, TextBlock Label, TextBlock Range, TextBlock Now)> _cards = new();
    private readonly TextBlock _chartTitle = OneUi.Label("", 20, bold: true), _chartNow = OneUi.Label("", 40, Sky), _health = OneUi.Label("", 14, OneUi.Secondary);
    private readonly Canvas _chart = new() { ClipToBounds = true };
    private readonly bool _narrow;
    private int _group;
    private string _metric = "CpuLoad";

    public event Action? CloseRequested;
    public int Group => _group;
    public string Metric => _metric;

    /// <param name="subtitle">The hardware name for a page (CPU or GPU model), or "".</param>
    public SensorDetailView(TelemetryEngine telemetry, MonitorSettings settings, Size size, Func<int, string> subtitle)
    {
        _telemetry = telemetry;
        _settings = settings;
        _subtitle = subtitle;
        Width = size.Width;
        Height = size.Height;
        Background = OneUi.Background();
        _narrow = size.Width < 700;
        _chartNow.FontFamily = new FontFamily("Segoe UI Light");
        _idle.Tick += (_, _) => { _idle.Stop(); CloseRequested?.Invoke(); };
        // Taps on the page never reach the HUD underneath.
        MouseLeftButtonUp += (_, e) => { e.Handled = true; if (_idle.IsEnabled) { _idle.Stop(); _idle.Start(); } }; // not after ✕ closed it

        // Header: the page's name and hardware, its tabs, and ✕.
        var close = OneUi.Circle("", 60, () => CloseRequested?.Invoke());
        var tabs = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        for (int g = 0; g < SensorDetails.Groups.Count; g++)
        {
            int target = g;
            string label = SensorDetails.Groups[g].Title is "SYSTEM" ? "System" : SensorDetails.Groups[g].Title;
            var tab = OneUi.Pill(label, 52, () => ShowGroup(target, null), size: 18, tag: "tab:" + label);
            tab.Margin = new Thickness(4, 0, 4, 0);
            tab.MinWidth = _narrow ? 104 : 120;
            _tabs.Add(tab);
            tabs.Children.Add(tab);
        }
        var titles = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 12, 0) };
        titles.Children.Add(_title);
        titles.Children.Add(_hardware);
        var header = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
        DockPanel.SetDock(close, Dock.Right);
        header.Children.Add(close);
        if (!_narrow) { DockPanel.SetDock(tabs, Dock.Right); tabs.Margin = new Thickness(0, 0, 12, 0); header.Children.Add(tabs); }
        header.Children.Add(titles);

        // Chart card: the reading's name and current value over its last ten minutes.
        var chartHead = new DockPanel();
        DockPanel.SetDock(_chartNow, Dock.Right);
        _chartNow.VerticalAlignment = VerticalAlignment.Center;
        _chartTitle.VerticalAlignment = VerticalAlignment.Center;
        chartHead.Children.Add(_chartNow);
        chartHead.Children.Add(_chartTitle);
        var chartLayout = new DockPanel();
        DockPanel.SetDock(chartHead, Dock.Top);
        DockPanel.SetDock(_health, Dock.Bottom);
        _health.Margin = new Thickness(2, 6, 0, 0);
        _chart.Margin = new Thickness(0, 6, 0, 0);
        chartLayout.Children.Add(chartHead);
        chartLayout.Children.Add(_health);
        chartLayout.Children.Add(_chart);
        var chartCard = OneUi.Card(chartLayout, radius: 36, padding: 18);
        _chart.SizeChanged += (_, _) => DrawChart(DateTime.Now);

        // Side by side on landscape screens; stacked (chart above the readings) on vertical ones.
        var body = new Grid();
        var rowsCard = OneUi.Card(_rows, radius: 36, padding: 12);
        if (size.Width >= size.Height * 1.2)
        {
            bool wide = size.Width >= size.Height * 2.2;
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(wide ? 0.42 : 0.5, GridUnitType.Star) });
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(wide ? 0.58 : 0.5, GridUnitType.Star) });
            SetColumn(chartCard, 1);
        }
        else
        {
            body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            SetRow(rowsCard, 1);
        }
        body.Children.Add(rowsCard);
        body.Children.Add(chartCard);

        var layout = new DockPanel { Margin = new Thickness(20, 14, 20, 12) };
        DockPanel.SetDock(header, Dock.Top);
        layout.Children.Add(header);
        if (_narrow)
        {
            tabs.HorizontalAlignment = HorizontalAlignment.Center;
            tabs.Margin = new Thickness(0, 4, 0, 8);
            DockPanel.SetDock(tabs, Dock.Top);
            layout.Children.Add(tabs);
        }
        layout.Children.Add(body);
        Children.Add(layout);
    }

    /// <summary>Opens on the page and reading for a sensor and restarts the idle timer.</summary>
    public void Open(string? sensor)
    {
        var (group, metric) = SensorDetails.For(sensor);
        ShowGroup(group, metric);
        _idle.Stop();
        _idle.Start();
    }

    public void Close() => _idle.Stop();

    private void ShowGroup(int group, string? metric)
    {
        int count = SensorDetails.Groups.Count;
        _group = (group % count + count) % count;
        var page = SensorDetails.Groups[_group];
        _metric = metric ?? page.Metrics[0];
        _title.Text = page.Title is "SYSTEM" ? "System" : page.Title;
        for (int i = 0; i < _tabs.Count; i++) OneUi.SetOn(_tabs[i], i == _group);
        _rows.Children.Clear();
        _cards.Clear();
        foreach (var id in page.Metrics)
        {
            var label = OneUi.Label(SensorDetails.Label(id), 18, bold: true);
            var range = OneUi.Label("", 13, OneUi.Secondary);
            var now = OneUi.Label("--", 26, bold: true);
            now.VerticalAlignment = VerticalAlignment.Center;
            now.Margin = new Thickness(12, 0, 0, 0);
            var words = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            words.Children.Add(label);
            words.Children.Add(range);
            var row = new DockPanel();
            DockPanel.SetDock(now, Dock.Right);
            row.Children.Add(now);
            row.Children.Add(words);
            var card = new Border { CornerRadius = new CornerRadius(30), Padding = new Thickness(22, 4, 22, 4), Margin = new Thickness(0, 0, 0, 6),
                Height = 62, Child = row, Cursor = System.Windows.Input.Cursors.Hand, Tag = "reading:" + id };
            string chosen = id;
            card.MouseLeftButtonUp += (_, _) => { _metric = chosen; Update(DateTime.Now); };
            _cards[id] = (card, label, range, now);
            _rows.Children.Add(card);
        }
        Update(DateTime.Now);
    }

    /// <summary>Refreshes readings and the chart; the HUD calls this on every poll while the page is open.</summary>
    public void Update(DateTime now)
    {
        string name = _subtitle(_group);
        _hardware.Text = name;
        _hardware.Visibility = name.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        foreach (var (id, (card, label, range, value)) in _cards)
        {
            var stats = _telemetry.Metrics.GetValueOrDefault(id);
            value.Text = SensorDetails.Format(id, stats?.Current);
            range.Text = SensorDetails.Range(id, stats);
            // The charted reading is filled light with dark text, like a selected One UI item.
            bool chosen = id == _metric;
            card.Background = chosen ? OneUi.OnFill : OneUi.Glass;
            label.Foreground = value.Foreground = chosen ? OneUi.OnText : OneUi.Text;
            range.Foreground = chosen ? OneUi.OnText : OneUi.Secondary;
            range.Opacity = chosen ? 0.75 : 1;
        }
        var focus = _telemetry.Metrics.GetValueOrDefault(_metric);
        _chartNow.Text = SensorDetails.Format(_metric, focus?.Current);
        _health.Text = focus?.Health(now) ?? "";
        DrawChart(now);
    }

    private void DrawChart(DateTime now)
    {
        _chart.Children.Clear();
        double w = _chart.ActualWidth, h = _chart.ActualHeight;
        var history = _telemetry.History(_metric, now, 600);
        double span = history.Count > 0 ? Math.Clamp((now - history[0].Time).TotalSeconds, 60, 600) : 600;
        _chartTitle.Text = $"{SensorDetails.Label(_metric)}  ·  {Math.Ceiling(span / 60):0} min";
        if (w < 4 || h < 4) return;
        double? limit = SensorDetails.Limit(_metric, _settings);
        double top = SensorDetails.ChartMaximum(_metric, history.Where(p => p.Value.HasValue).Select(p => p.Value!.Value), limit);
        foreach (double f in new[] { 0.25, 0.5, 0.75 })
            _chart.Children.Add(new Line { X1 = 0, X2 = w, Y1 = h * f, Y2 = h * f, Stroke = GridLine, StrokeThickness = 1 });
        if (limit is { } l && l <= top)
        {
            double y = h - l / top * h;
            _chart.Children.Add(new Line { X1 = 0, X2 = w, Y1 = y, Y2 = y, Stroke = Pink, StrokeThickness = 2, StrokeDashArray = [4, 3] });
            var tag = OneUi.Label("alert " + SensorDetails.Format(_metric, l), 14, Pink, bold: true);
            Canvas.SetLeft(tag, 6);
            Canvas.SetTop(tag, Math.Max(0, y - 22));
            _chart.Children.Add(tag);
        }
        // One line per unbroken stretch: a missing reading is a gap, not a drop to zero.
        var segment = new PointCollection();
        void Flush()
        {
            if (segment.Count >= 2)
            {
                var area = new PointCollection(segment) { new Point(segment[^1].X, h), new Point(segment[0].X, h) };
                _chart.Children.Add(new Polygon { Points = area, Fill = AreaFill });
                _chart.Children.Add(new Polyline { Points = segment, Stroke = Sky, StrokeThickness = 3, StrokeLineJoin = PenLineJoin.Round,
                    StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round });
            }
            segment = new PointCollection();
        }
        foreach (var point in history)
        {
            if (point.Value is not { } v || !double.IsFinite(v)) { Flush(); continue; }
            double x = w * (1 - Math.Clamp((now - point.Time).TotalSeconds, 0, span) / span);
            segment.Add(new Point(x, h - Math.Clamp(v / top, 0, 1) * h));
        }
        Flush();
    }

    /// <summary>The chart's area: sky blue fading to nothing at the bottom.</summary>
    private static Brush AreaBrush()
    {
        var brush = new LinearGradientBrush(Color.FromArgb(0x59, 0x7F, 0xD8, 0xFF), Color.FromArgb(0x00, 0x7F, 0xD8, 0xFF), 90);
        brush.Freeze();
        return brush;
    }
}
