using System.Globalization;
using System.IO;
using System.Text;

namespace Pulsewave;

public sealed class MetricStatistics
{
    public string Metric { get; }
    public long Samples { get; private set; }
    public double? Current { get; private set; }
    public double? Minimum { get; private set; }
    public double? Maximum { get; private set; }
    public double? Average => Samples > 0 ? _sum / Samples : null;
    public DateTime? LastValidAt { get; private set; }
    public DateTime? MissingSince { get; private set; }
    public DateTime? UnchangedSince { get; private set; }
    public string ReaderStatus { get; private set; } = "Awaiting first reading";
    private double _sum;

    public MetricStatistics(string metric) => Metric = metric;

    public void Add(DateTime now, double? value, string? readerStatus)
    {
        ReaderStatus = readerStatus ?? "No mapping";
        if (!value.HasValue || !double.IsFinite(value.Value))
        {
            Current = null;
            MissingSince ??= now;
            UnchangedSince = null;
            return;
        }

        MissingSince = null;
        if (Current.HasValue && Math.Abs(Current.Value - value.Value) < 0.000001)
            UnchangedSince ??= now;
        else
            UnchangedSince = now;

        Current = value;
        LastValidAt = now;
        Samples++;
        _sum += value.Value;
        Minimum = Minimum.HasValue ? Math.Min(Minimum.Value, value.Value) : value.Value;
        Maximum = Maximum.HasValue ? Math.Max(Maximum.Value, value.Value) : value.Value;
    }

    // Do NOT label a static reading as 'stale' or 'disconnected': many true sensor
    // values stay unchanged for long periods, and VSB has no sample timestamp.
    public string Health(DateTime now)
    {
        if (!Current.HasValue)
            return MissingSince.HasValue
                ? $"Missing {Math.Max(0, (int)(now - MissingSince.Value).TotalSeconds)}s · {ReaderStatus}"
                : $"Missing · {ReaderStatus}";
        return UnchangedSince.HasValue && (now - UnchangedSince.Value).TotalSeconds >= 60
            ? "Reporting · unchanged 60s+ (not necessarily stale)"
            : "Reporting · " + ReaderStatus;
    }
}

public sealed record TelemetryPoint(DateTime Time, double? Value);
public sealed record TelemetryRecord(DateTime Time, Dictionary<string, double?> Values);

public sealed class TelemetryEngine
{
    public static readonly string[] MetricOrder =
    [
        "CpuLoad", "CpuTemp", "CpuPower", "CpuFan", "GpuLoad", "GpuTemp",
        "GpuPower", "GpuFps", "GpuVram", "MemoryLoad", "NetDownload", "NetUpload"
    ];

    // Bounded time-series: at 250ms refresh this represents ~60min of samples.
    // Summary statistics remain valid from the beginning of the session.
    private const int MaxRecords = 14400;
    private readonly Queue<TelemetryRecord> _records = new();
    private readonly Dictionary<string, Queue<TelemetryPoint>> _history =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, MetricStatistics> _metrics =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _activeAlerts = new(StringComparer.OrdinalIgnoreCase);

    public DateTime StartedAt { get; private set; } = DateTime.Now;
    public int PollCount { get; private set; }
    public bool SamplesWereDiscarded { get; private set; }
    public IReadOnlyDictionary<string, MetricStatistics> Metrics => _metrics;
    public int RetainedRecordCount => _records.Count;
    public event Action<string>? ThresholdCrossed;

    public TelemetryEngine() => Reset();

    public void Reset()
    {
        _records.Clear();
        _history.Clear();
        _metrics.Clear();
        _activeAlerts.Clear();
        foreach (var id in MetricOrder)
        {
            _metrics[id] = new MetricStatistics(id);
            _history[id] = new Queue<TelemetryPoint>();
        }
        StartedAt = DateTime.Now;
        PollCount = 0;
        SamplesWereDiscarded = false;
    }

    public void Ingest(DateTime at, SensorSnapshot snapshot, MonitorSettings settings)
    {
        PollCount++;
        var record = new Dictionary<string, double?>(StringComparer.OrdinalIgnoreCase);
        foreach (string metric in MetricOrder)
        {
            double? value = snapshot.Get(metric);
            record[metric] = value;
            _metrics[metric].Add(at, value,
                snapshot.Statuses.GetValueOrDefault(metric));
            var points = _history[metric];
            points.Enqueue(new TelemetryPoint(at, value));
            // Keep up to 10 minutes for history charts; at high poll rates bound memory.
            while (points.Count > 2500 || points.Count > 0 && (at - points.Peek().Time).TotalSeconds > 600)
                points.Dequeue();
        }
        _records.Enqueue(new TelemetryRecord(at, record));
        if (_records.Count > MaxRecords)
        {
            _records.Dequeue();
            SamplesWereDiscarded = true;
        }
        if (settings.EnableThresholdAlerts)
            EvaluateAlerts(settings);
        else
            _activeAlerts.Clear();
    }

    public IReadOnlyList<TelemetryPoint> History(string id, DateTime now, int seconds)
    {
        if (!_history.TryGetValue(id, out var history)) return [];
        var cutoff = now.AddSeconds(-Math.Clamp(seconds, 30, 600));
        return history.Where(point => point.Time >= cutoff).ToList();
    }

    private void EvaluateAlerts(MonitorSettings settings)
    {
        Check("CpuTemp", "CPU temperature", settings.CpuTemperatureAlertC, "°C");
        Check("GpuTemp", "GPU temperature", settings.GpuTemperatureAlertC, "°C");
        Check("MemoryLoad", "Memory usage", settings.MemoryAlertPercent, "%");
    }

    private void Check(string key, string label, double threshold, string unit)
    {
        var reading = _metrics[key].Current;
        // Reset on sensor loss; never announce a value that is no longer live.
        if (!reading.HasValue)
        {
            _activeAlerts.Remove(key);
            return;
        }
        if (reading.Value >= threshold)
        {
            if (_activeAlerts.Add(key))
                ThresholdCrossed?.Invoke($"{label}: {reading.Value:F0}{unit} (limit {threshold:F0}{unit})");
        }
        else if (reading.Value <= threshold - (key == "MemoryLoad" ? 2 : 3))
            _activeAlerts.Remove(key); // hysteresis prevents repeated notifications near threshold.
    }

    public void ExportCsv(string path)
    {
        using var writer = new StreamWriter(path, false, new UTF8Encoding(true));
        writer.Write("TimestampLocal");
        foreach (var key in MetricOrder) { writer.Write(','); writer.Write(key); }
        writer.WriteLine();
        foreach (var row in _records)
        {
            writer.Write(row.Time.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture));
            foreach (var metric in MetricOrder)
            {
                writer.Write(',');
                if (row.Values.TryGetValue(metric, out var val) && val.HasValue)
                    writer.Write(val.Value.ToString("G17", CultureInfo.InvariantCulture));
            }
            writer.WriteLine();
        }
    }
}
