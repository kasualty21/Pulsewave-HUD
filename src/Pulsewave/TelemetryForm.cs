using System.Drawing;
using System.Globalization;
using System.IO;
using Forms = System.Windows.Forms;

namespace Pulsewave;

/// <summary>Session stats and health stay off the 1920x480 wallpaper overlay.</summary>
public sealed class TelemetryForm : Forms.Form
{
    /// <summary>The Halo desktop look (DesktopStyle), applied once everything in the window exists.</summary>
    protected override void OnLoad(EventArgs e) { DesktopStyle.Apply(this); base.OnLoad(e); }

    private readonly TelemetryEngine _engine;
    private readonly MonitorSettings _settings;
    private readonly Action _onSettingsSaved;
    private readonly Forms.ListView _metrics = new();
    private readonly Forms.Label _summary = new();
    private readonly Forms.Timer _uiTimer = new() { Interval = 1000 };

    public TelemetryForm(TelemetryEngine engine, MonitorSettings settings, Action onSettingsSaved)
    {
        _engine = engine;
        _settings = settings;
        _onSettingsSaved = onSettingsSaved;
        Text = "Pulsewave · Telemetry & session stats";
        StartPosition = Forms.FormStartPosition.CenterScreen;
        var work = Forms.Screen.PrimaryScreen?.WorkingArea;
        Size = new Size(Math.Min(1080, work?.Width ?? 1080), Math.Min(620, work?.Height ?? 620));
        MinimumSize = new Size(850, 480);
        BackColor = Color.FromArgb(19, 27, 37);
        ForeColor = Color.FromArgb(232, 242, 249);
        Font = new Font("Segoe UI", 9);
        AutoScaleMode = Forms.AutoScaleMode.Dpi;

        var title = new Forms.Label
        {
            Dock = Forms.DockStyle.Top, Height = 58, Padding = new Forms.Padding(18, 15, 0, 0),
            Text = "SESSION TELEMETRY  /  SENSOR HEALTH",
            Font = new Font("Segoe UI Semibold", 17), ForeColor = Color.FromArgb(112, 232, 211)
        };
        _summary.Dock = Forms.DockStyle.Top;
        _summary.Height = 52;
        _summary.Padding = new Forms.Padding(17, 4, 12, 0);
        _summary.ForeColor = Color.FromArgb(160, 185, 201);

        _metrics.Dock = Forms.DockStyle.Fill;
        _metrics.View = Forms.View.Details;
        _metrics.FullRowSelect = true;
        _metrics.GridLines = false;
        _metrics.HeaderStyle = Forms.ColumnHeaderStyle.Nonclickable;
        _metrics.BorderStyle = Forms.BorderStyle.None;
        _metrics.BackColor = Color.FromArgb(27, 38, 50);
        _metrics.ForeColor = ForeColor;
        foreach (var (heading, width) in new []
        {
            ("METRIC", 148), ("NOW", 96), ("MIN", 96), ("AVG", 96),
            ("MAX", 96), ("SAMPLES", 84), ("HEALTH / SOURCE", 360)
        }) _metrics.Columns.Add(heading, width);

        var footer = new Forms.FlowLayoutPanel
        {
            Dock = Forms.DockStyle.Bottom, Height = 68,
            FlowDirection = Forms.FlowDirection.RightToLeft,
            BackColor = Color.FromArgb(22, 31, 42), Padding = new Forms.Padding(12, 11, 12, 8)
        };
        var close = MakeButton("Close", 100);
        var options = MakeButton("Polling & alerts", 147);
        var export = MakeButton("Export CSV", 126);
        var reset = MakeButton("Reset session", 135);
        close.Click += (_, _) => Close();
        options.Click += (_, _) =>
        {
            using var dialog = new TelemetrySettingsForm(_settings);
            if (dialog.ShowDialog(this) == Forms.DialogResult.OK)
            {
                _onSettingsSaved();
                RefreshGrid();
            }
        };
        export.Click += (_, _) => Export();
        reset.Click += (_, _) =>
        {
            if (Forms.MessageBox.Show(this, "Reset accumulated min / average / max and clear session samples?",
                "Reset telemetry session", Forms.MessageBoxButtons.YesNo,
                Forms.MessageBoxIcon.Question) != Forms.DialogResult.Yes) return;
            _engine.Reset();
            RefreshGrid();
        };
        footer.Controls.Add(close);
        footer.Controls.Add(options);
        footer.Controls.Add(export);
        footer.Controls.Add(reset);

        Controls.Add(_metrics);
        Controls.Add(footer);
        Controls.Add(_summary);
        Controls.Add(title);
        _uiTimer.Tick += (_, _) => RefreshGrid();
        Shown += (_, _) => { RefreshGrid(); _uiTimer.Start(); };
        FormClosed += (_, _) => { _uiTimer.Stop(); _uiTimer.Dispose(); };
    }

    private static Forms.Button MakeButton(string text, int width) => new()
    {
        Text = text, Width = width, Height = 39, Margin = new Forms.Padding(4, 2, 4, 0),
        BackColor = Color.FromArgb(44, 64, 80), ForeColor = Color.White,
        FlatStyle = Forms.FlatStyle.Flat
    };

    private static string N(double? value) => value.HasValue ?
        value.Value.ToString("0.##", CultureInfo.InvariantCulture) : "--";

    private void RefreshGrid()
    {
        var now = DateTime.Now;
        var elapsed = now - _engine.StartedAt;
        _summary.Text = $"Started: {_engine.StartedAt:yyyy-MM-dd HH:mm:ss}  ·  Elapsed: {(int)elapsed.TotalHours:00}:{elapsed.Minutes:00}:{elapsed.Seconds:00}  ·  " +
            $"Polls: {_engine.PollCount:N0}  ·  Retained samples: {_engine.RetainedRecordCount:N0}  ·  " +
            "LIVE SENSOR SESSION" +
            (_engine.SamplesWereDiscarded ? "  ·  CSV window truncated (stats are full-session)" : "");
        _metrics.BeginUpdate();
        try
        {
            _metrics.Items.Clear();
            foreach (var id in TelemetryEngine.MetricOrder)
            {
                var metric = _engine.Metrics[id];
                var row = new Forms.ListViewItem(id);
                row.SubItems.Add(N(metric.Current));
                row.SubItems.Add(N(metric.Minimum));
                row.SubItems.Add(N(metric.Average));
                row.SubItems.Add(N(metric.Maximum));
                row.SubItems.Add(metric.Samples.ToString("N0", CultureInfo.InvariantCulture));
                row.SubItems.Add(metric.Health(now));
                if (metric.Current is null) row.ForeColor = Color.FromArgb(243, 169, 112);
                _metrics.Items.Add(row);
            }
        }
        finally { _metrics.EndUpdate(); }
    }

    private void Export()
    {
        if (_engine.RetainedRecordCount == 0)
        {
            Forms.MessageBox.Show(this, "No readings have been recorded yet.");
            return;
        }
        using var dialog = new Forms.SaveFileDialog
        {
            Title = "Export telemetry samples", Filter = "CSV files (*.csv)|*.csv",
            FileName = $"pulsewave-session-{DateTime.Now:yyyyMMdd-HHmmss}.csv",
            AddExtension = true
        };
        if (dialog.ShowDialog(this) != Forms.DialogResult.OK) return;
        try
        {
            _engine.ExportCsv(dialog.FileName);
            Forms.MessageBox.Show(this,
                "Exported retained timestamped samples. The min/avg/max above cover the full current session." +
                (_engine.SamplesWereDiscarded ? " Early samples exceeded the recording cap and are absent from this CSV." : ""),
                "Pulsewave · CSV exported", Forms.MessageBoxButtons.OK, Forms.MessageBoxIcon.Information);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Forms.MessageBox.Show(this, "Export failed: " + ex.Message, "Export error",
                Forms.MessageBoxButtons.OK, Forms.MessageBoxIcon.Error);
        }
    }
}

public sealed class TelemetrySettingsForm : Forms.Form
{
    /// <summary>The Halo desktop look (DesktopStyle), applied once everything in the window exists.</summary>
    protected override void OnLoad(EventArgs e) { DesktopStyle.Apply(this); base.OnLoad(e); }

    private readonly MonitorSettings _settings;
    private readonly Forms.NumericUpDown _poll = new();
    private readonly Forms.NumericUpDown _history = new();
    private readonly Forms.NumericUpDown _cpu = new();
    private readonly Forms.NumericUpDown _gpu = new();
    private readonly Forms.NumericUpDown _ram = new();
    private readonly Forms.CheckBox _alerts = new();

    public TelemetrySettingsForm(MonitorSettings settings)
    {
        _settings = settings;
        Text = "Pulsewave · Polling & alerts";
        Size = new Size(575, 425);
        MinimumSize = new Size(530, 405);
        StartPosition = Forms.FormStartPosition.CenterParent;
        BackColor = Color.FromArgb(22, 32, 43);
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 10);

        var grid = new Forms.TableLayoutPanel
        {
            Dock = Forms.DockStyle.Fill, ColumnCount = 2, RowCount = 7,
            Padding = new Forms.Padding(24, 20, 24, 12)
        };
        grid.ColumnStyles.Add(new Forms.ColumnStyle(Forms.SizeType.Percent, 63));
        grid.ColumnStyles.Add(new Forms.ColumnStyle(Forms.SizeType.Percent, 37));
        for (var i = 0; i < 7; i++) grid.RowStyles.Add(new Forms.RowStyle(Forms.SizeType.Absolute, i == 6 ? 68 : 43));
        AddRow(grid, 0, "Polling interval (milliseconds)", _poll, 250, 5000, settings.PollIntervalMs, 250);
        AddRow(grid, 1, "Graph window (seconds)", _history, 30, 600, settings.HistorySeconds, 30);
        AddRow(grid, 2, "CPU temperature warning (°C)", _cpu, 40, 115, settings.CpuTemperatureAlertC, 1);
        AddRow(grid, 3, "GPU temperature warning (°C)", _gpu, 40, 115, settings.GpuTemperatureAlertC, 1);
        AddRow(grid, 4, "Memory warning (%)", _ram, 50, 100, settings.MemoryAlertPercent, 1);
        _alerts.Text = "Enable tray threshold notifications";
        _alerts.Checked = settings.EnableThresholdAlerts;
        _alerts.ForeColor = Color.FromArgb(188, 216, 226);
        _alerts.AutoSize = true;
        grid.Controls.Add(_alerts, 0, 5);
        grid.SetColumnSpan(_alerts, 2);
        var hint = new Forms.Label
        {
            Text = "Notifications are disabled by default; alerts fire on crossing, with hysteresis. " +
                   "Unchanged readings are not assumed to be faulty.",
            ForeColor = Color.FromArgb(163, 186, 204), Dock = Forms.DockStyle.Fill
        };
        grid.Controls.Add(hint, 0, 6);
        grid.SetColumnSpan(hint, 2);

        var footer = new Forms.FlowLayoutPanel
        {
            Dock = Forms.DockStyle.Bottom, Height = 62, Padding = new Forms.Padding(12, 10, 12, 8),
            FlowDirection = Forms.FlowDirection.RightToLeft
        };
        var cancel = new Forms.Button { Text = "Cancel", Width = 100, Height = 34, DialogResult = Forms.DialogResult.Cancel };
        var apply = new Forms.Button { Text = "Save", Width = 100, Height = 34 };
        apply.Click += (_, _) =>
        {
            // Apply atomically: on disk failure, restore previous settings.
            var old = (_settings.PollIntervalMs, _settings.HistorySeconds, _settings.CpuTemperatureAlertC,
                       _settings.GpuTemperatureAlertC, _settings.MemoryAlertPercent, _settings.EnableThresholdAlerts);
            _settings.PollIntervalMs = (int)_poll.Value;
            _settings.HistorySeconds = (int)_history.Value;
            _settings.CpuTemperatureAlertC = (double)_cpu.Value;
            _settings.GpuTemperatureAlertC = (double)_gpu.Value;
            _settings.MemoryAlertPercent = (double)_ram.Value;
            _settings.EnableThresholdAlerts = _alerts.Checked;
            try { SettingsStore.Save(_settings); DialogResult = Forms.DialogResult.OK; Close(); }
            catch (Exception ex)
            {
                (_settings.PollIntervalMs, _settings.HistorySeconds, _settings.CpuTemperatureAlertC,
                 _settings.GpuTemperatureAlertC, _settings.MemoryAlertPercent, _settings.EnableThresholdAlerts) = old;
                Forms.MessageBox.Show(this, ex.Message, "Could not save settings");
            }
        };
        footer.Controls.Add(cancel);
        footer.Controls.Add(apply);
        Controls.Add(grid);
        Controls.Add(footer);
        AcceptButton = apply;
        CancelButton = cancel;
    }

    private static void AddRow(Forms.TableLayoutPanel grid, int row, string label,
        Forms.NumericUpDown input, decimal min, decimal max, double value, decimal step)
    {
        grid.Controls.Add(new Forms.Label
        {
            Text = label, ForeColor = Color.FromArgb(221, 234, 242),
            Dock = Forms.DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft
        }, 0, row);
        input.Minimum = min;
        input.Maximum = max;
        input.Increment = step;
        input.Value = Math.Clamp((decimal)value, min, max);
        input.Dock = Forms.DockStyle.Fill;
        input.BackColor = Color.FromArgb(42, 56, 72);
        input.ForeColor = Color.White;
        grid.Controls.Add(input, 1, row);
    }
}
