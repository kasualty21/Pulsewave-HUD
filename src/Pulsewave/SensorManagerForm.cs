// Explicit aliases avoid WPF / WinForms type collisions (Color, Point, Size).
using Color = System.Drawing.Color;
using ContentAlignment = System.Drawing.ContentAlignment;
using Font = System.Drawing.Font;
using Point = System.Drawing.Point;
using Rectangle = System.Drawing.Rectangle;
using Size = System.Drawing.Size;
using System.Globalization;
using Forms = System.Windows.Forms;

namespace Pulsewave;

/// <summary>
/// Native configuration window deliberately separate from the click-through overlay.
/// Nothing is persisted until the user chooses Save mappings.
/// </summary>
public sealed class SensorManagerForm : Forms.Form
{
    /// <summary>The Halo desktop look (DesktopStyle), applied once everything in the window exists.</summary>
    protected override void OnLoad(EventArgs e) { DesktopStyle.Apply(this); base.OnLoad(e); }

    private static readonly (string Id, string Label)[] DashboardMetrics =
    [
        ("CpuLoad", "CPU load"), ("CpuTemp", "CPU temperature"),
        ("CpuPower", "CPU power"), ("CpuFan", "CPU fan"),
        ("GpuLoad", "GPU load"), ("GpuTemp", "GPU temperature"),
        ("GpuPower", "GPU power"), ("GpuFps", "FPS"),
        ("GpuVram", "Video memory"), ("MemoryLoad", "Memory use"),
        ("NetDownload", "Download"), ("NetUpload", "Upload")
    ];

    // Dashboard metrics, then optional extras that custom themes can show.
    private static readonly (string Id, string Label)[] Metrics = [.. DashboardMetrics, .. SensorIds.Theme];

    private sealed class Selection
    {
        public SensorCatalogEntry? Entry { get; init; }
        public bool IsUnassigned { get; init; }
        public string Title { get; init; } = "";
        public override string ToString() => Title;
    }

    private sealed class FieldControls
    {
        public string Id { get; init; } = "";
        public Forms.ComboBox Choice { get; init; } = new();
        public Forms.TextBox Scale { get; init; } = new();
        public Forms.Label Status { get; init; } = new();
        public SensorMapping Mapping { get; set; } = new();
    }

    private readonly MonitorSettings _settings;
    private readonly Dictionary<string, FieldControls> _rows = new(StringComparer.OrdinalIgnoreCase);
    private readonly Forms.ListView _list = new();
    private readonly Forms.Label _statusLine = new();
    private IReadOnlyList<SensorCatalogEntry> _catalog = [];
    private bool _reloading;

    public bool Saved { get; private set; }

    public SensorManagerForm(MonitorSettings settings)
    {
        _settings = settings;
        Text = "Pulsewave · Sensor Manager";
        StartPosition = Forms.FormStartPosition.Manual;
        var primary = Forms.Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1920, 1080);
        Size = new Size(Math.Min(1560, primary.Width), Math.Min(790, primary.Height));
        MinimumSize = new Size(960, 480);
        Location = new Point(primary.Left + (primary.Width - Width) / 2,
                             primary.Top + (primary.Height - Height) / 2);
        BackColor = Color.FromArgb(19, 27, 37);
        ForeColor = Color.FromArgb(233, 242, 247);
        Font = new Font("Segoe UI", 9.5f);
        AutoScaleMode = Forms.AutoScaleMode.Dpi;

        var header = new Forms.Panel { Dock = Forms.DockStyle.Top, Height = 94, Padding = new Forms.Padding(18, 12, 18, 8) };
        var heading = new Forms.Label
        {
            Text = "HWiNFO SENSOR DISCOVERY", Font = new Font("Segoe UI Semibold", 17f),
            ForeColor = Color.FromArgb(112, 232, 211), Dock = Forms.DockStyle.Top, Height = 37
        };
        var info = new Forms.Label
        {
            Text = "Most readings follow their names across slot changes. FPS follows its numbered slot across game-title changes.",
            ForeColor = Color.FromArgb(176, 196, 209), Dock = Forms.DockStyle.Top, Height = 32
        };
        header.Controls.Add(info);
        header.Controls.Add(heading);

        var footer = new Forms.Panel { Dock = Forms.DockStyle.Bottom, Height = 72, Padding = new Forms.Padding(16, 9, 16, 12) };
        _statusLine.AutoEllipsis = true;
        _statusLine.Dock = Forms.DockStyle.Fill;
        _statusLine.ForeColor = Color.FromArgb(173, 192, 202);
        _statusLine.TextAlign = ContentAlignment.MiddleLeft;
        footer.Controls.Add(_statusLine);

        var buttons = new Forms.FlowLayoutPanel
        {
            Dock = Forms.DockStyle.Right, Width = 545, FlowDirection = Forms.FlowDirection.RightToLeft,
            WrapContents = false, AutoSize = false
        };
        var save = Button("Save mappings", 138, Color.FromArgb(40, 128, 117));
        var cancel = Button("Cancel", 90, Color.FromArgb(48, 60, 75));
        var refresh = Button("Rescan", 94, Color.FromArgb(44, 62, 83));
        var suggest = Button("Auto-pair", 135, Color.FromArgb(44, 62, 83));
        save.Click += (_, _) => SaveMappings();
        cancel.Click += (_, _) => Close();
        refresh.Click += (_, _) => RefreshCatalog();
        suggest.Click += (_, _) => AutoPair();
        buttons.Controls.Add(save);
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(refresh);
        buttons.Controls.Add(suggest);
        footer.Controls.Add(buttons);

        var splitter = new Forms.SplitContainer
        {
            Dock = Forms.DockStyle.Fill, Orientation = Forms.Orientation.Vertical,
            SplitterWidth = 6, BackColor = Color.FromArgb(28, 40, 53)
        };
        var mappingPanel = BuildMappingPanel();
        splitter.Panel1.Controls.Add(mappingPanel);
        splitter.Panel2.Controls.Add(BuildDiscoveryPanel());

        Controls.Add(splitter);
        Controls.Add(footer);
        Controls.Add(header);
        Shown += (_, _) =>
        {
            // The control has its real width only after WinForms layout has run.
            int desired = Math.Clamp(splitter.Width * 2 / 3, 100,
                                     Math.Max(100, splitter.Width - 100 - splitter.SplitterWidth));
            splitter.SplitterDistance = desired;
            splitter.Panel1MinSize = Math.Min(560, Math.Max(1, desired - 10));
            splitter.Panel2MinSize = Math.Min(240, Math.Max(1, splitter.Width - desired - splitter.SplitterWidth - 10));
        };
        RefreshCatalog();
    }

    private Forms.Control BuildMappingPanel()
    {
        var grid = new Forms.TableLayoutPanel
        {
            Dock = Forms.DockStyle.Fill, AutoScroll = true, Padding = new Forms.Padding(12, 0, 10, 8),
            RowCount = Metrics.Length + 2, ColumnCount = 4,
            BackColor = Color.FromArgb(23, 32, 43)
        };
        grid.ColumnStyles.Add(new Forms.ColumnStyle(Forms.SizeType.Absolute, 145));
        grid.ColumnStyles.Add(new Forms.ColumnStyle(Forms.SizeType.Percent, 100));
        grid.ColumnStyles.Add(new Forms.ColumnStyle(Forms.SizeType.Absolute, 90));
        grid.ColumnStyles.Add(new Forms.ColumnStyle(Forms.SizeType.Absolute, 168));
        grid.RowStyles.Add(new Forms.RowStyle(Forms.SizeType.Absolute, 40));

        grid.Controls.Add(ColumnTitle("DASHBOARD METRIC"), 0, 0);
        grid.Controls.Add(ColumnTitle("EXPORTED SENSOR / REGISTRY SOURCE"), 1, 0);
        grid.Controls.Add(ColumnTitle("SCALE"), 2, 0);
        grid.Controls.Add(ColumnTitle("STATUS"), 3, 0);

        int gridRow = 0;
        for (int i = 0; i < Metrics.Length; i++)
        {
            var (id, display) = Metrics[i];
            if (i == DashboardMetrics.Length)
            {
                grid.RowStyles.Add(new Forms.RowStyle(Forms.SizeType.Absolute, 46));
                var section = ColumnTitle("THEME SENSORS · OPTIONAL · USED BY CUSTOM THEMES; UNASSIGNED SHOWS --");
                grid.Controls.Add(section, 0, ++gridRow);
                grid.SetColumnSpan(section, 4);
            }
            gridRow++;
            grid.RowStyles.Add(new Forms.RowStyle(Forms.SizeType.Absolute, 46));
            var name = new Forms.Label
            {
                Text = display, Dock = Forms.DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.FromArgb(222, 234, 241), AutoEllipsis = true
            };
            var combo = new Forms.ComboBox
            {
                Dock = Forms.DockStyle.Fill, DropDownStyle = Forms.ComboBoxStyle.DropDownList,
                FlatStyle = Forms.FlatStyle.Flat, Margin = new Forms.Padding(3, 9, 8, 9),
                BackColor = Color.FromArgb(40, 53, 68), ForeColor = Color.White,
                DropDownWidth = 750
            };
            var original = _settings.Sensors.TryGetValue(id, out var mapping) ? Copy(mapping) : new SensorMapping();
            var scale = new Forms.TextBox
            {
                Text = original.Scale.ToString("G", CultureInfo.InvariantCulture),
                Dock = Forms.DockStyle.Fill, Margin = new Forms.Padding(3, 11, 8, 10),
                BackColor = Color.FromArgb(40, 53, 68), ForeColor = Color.White,
                BorderStyle = Forms.BorderStyle.FixedSingle
            };
            var state = new Forms.Label
            {
                Dock = Forms.DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true,
                ForeColor = Color.FromArgb(176, 198, 209), Margin = new Forms.Padding(5, 1, 2, 1)
            };
            var row = new FieldControls { Id = id, Choice = combo, Scale = scale, Status = state, Mapping = original };
            _rows[id] = row;
            combo.SelectedIndexChanged += (_, _) =>
            {
                if (_reloading) return;
                var selected = combo.SelectedItem as Selection;
                if (selected?.IsUnassigned == true)
                    row.Mapping = new SensorMapping { Scale = row.Mapping.Scale };
                else if (selected?.Entry is { } entry)
                    row.Mapping = FromEntry(entry, row.Mapping.Scale, id == "GpuFps");
                UpdateRowStatus(row);
            };
            grid.Controls.Add(name, 0, gridRow);
            grid.Controls.Add(combo, 1, gridRow);
            grid.Controls.Add(scale, 2, gridRow);
            grid.Controls.Add(state, 3, gridRow);
        }
        return grid;
    }

    private Forms.Control BuildDiscoveryPanel()
    {
        var panel = new Forms.Panel { Dock = Forms.DockStyle.Fill, Padding = new Forms.Padding(10, 4, 10, 6) };
        var title = new Forms.Label
        {
            Dock = Forms.DockStyle.Top, Height = 42, ForeColor = Color.FromArgb(125, 184, 255),
            Text = "AVAILABLE HWiNFO GADGET READINGS", Font = new Font("Segoe UI Semibold", 11),
            TextAlign = ContentAlignment.MiddleLeft
        };
        _list.Dock = Forms.DockStyle.Fill;
        _list.View = Forms.View.Details;
        _list.FullRowSelect = true;
        _list.GridLines = false;
        _list.BackColor = Color.FromArgb(22, 32, 42);
        _list.ForeColor = Color.FromArgb(220, 230, 240);
        _list.BorderStyle = Forms.BorderStyle.None;
        _list.HeaderStyle = Forms.ColumnHeaderStyle.Nonclickable;
        _list.Columns.Add("HIVE", 62);
        _list.Columns.Add("#", 36);
        _list.Columns.Add("SENSOR / LABEL", 260);
        _list.Columns.Add("READING", 132);
        var help = new Forms.Label
        {
            Dock = Forms.DockStyle.Bottom, Height = 102, ForeColor = Color.FromArgb(158, 177, 192),
            Text = "Only readings enabled in HWiNFO → Sensor Settings → HWiNFO Gadget appear here. " +
                   "If FPS or VRAM is absent, enable its Gadget reporting in HWiNFO first, then click Rescan. " +
                   "Some FPS readings require an active 3D application.",
            Padding = new Forms.Padding(5, 12, 5, 3)
        };
        panel.Controls.Add(_list);
        panel.Controls.Add(help);
        panel.Controls.Add(title);
        return panel;
    }

    private static Forms.Label ColumnTitle(string name) => new()
    {
        Text = name, Dock = Forms.DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft,
        ForeColor = Color.FromArgb(109, 147, 166), Font = new Font("Segoe UI Semibold", 8)
    };

    private static Forms.Button Button(string name, int width, Color color) => new()
    {
        Text = name, Width = width, Height = 38, Margin = new Forms.Padding(4, 4, 4, 0),
        FlatStyle = Forms.FlatStyle.Flat, BackColor = color, ForeColor = Color.White
    };

    private static SensorMapping Copy(SensorMapping original) => new()
    {
        RegistryValue = original.RegistryValue, Scale = original.Scale,
        RegistryHive = original.RegistryHive, SensorGroup = original.SensorGroup,
        SensorLabel = original.SensorLabel, MatchMode = original.MatchMode
    };

    private static SensorMapping FromEntry(SensorCatalogEntry entry, double scale, bool fps) => new()
    {
        RegistryHive = entry.Hive,
        SensorGroup = entry.Group,
        SensorLabel = entry.Label,
        RegistryValue = entry.Raw is not null ? entry.IndexRaw : entry.IndexFormatted,
        Scale = scale, MatchMode = fps ? "Slot" : "Name"
    };

    private void RefreshCatalog()
    {
        _catalog = SensorCatalog.Scan(_settings.RegistryPath);
        _list.BeginUpdate();
        _list.Items.Clear();
        foreach (var entry in _catalog.OrderBy(e => e.Hive).ThenBy(e => e.Index))
        {
            var shortHive = entry.Hive.Equals("CurrentUser", StringComparison.OrdinalIgnoreCase) ? "HKCU" : "HKLM";
            var item = new Forms.ListViewItem(shortHive);
            item.SubItems.Add(entry.Index.ToString(CultureInfo.InvariantCulture));
            item.SubItems.Add(entry.Name);
            item.SubItems.Add(entry.Reading);
            _list.Items.Add(item);
        }
        _list.EndUpdate();

        _reloading = true;
        try
        {
            foreach (var row in _rows.Values)
            {
                row.Choice.Items.Clear();
                row.Choice.Items.Add(new Selection { IsUnassigned = true, Title = "— Unassigned —" });
                foreach (var entry in _catalog.OrderByDescending(e => e.Hive.Equals(_settings.RegistryHive, StringComparison.OrdinalIgnoreCase))
                             .ThenBy(e => e.Group).ThenBy(e => e.Label).ThenBy(e => e.Index))
                {
                    var shortHive = entry.Hive == "CurrentUser" ? "HKCU" : "HKLM";
                    row.Choice.Items.Add(new Selection
                    {
                        Entry = entry,
                        Title = $"{shortHive} #{entry.Index:000}  {entry.Name}  [{entry.Reading}]"
                    });
                }
                var entrySelected = SensorCatalog.Resolve(row.Mapping, _settings.RegistryHive, _catalog, out _);
                int selected = 0;
                if (entrySelected is not null)
                {
                    for (int j = 1; j < row.Choice.Items.Count; j++)
                    {
                        if ((row.Choice.Items[j] as Selection)?.Entry == entrySelected)
                        { selected = j; break; }
                    }
                }
                else if (!string.IsNullOrWhiteSpace(row.Mapping.RegistryValue))
                {
                    // Preserve unavailable mappings rather than silently deleting them.
                    row.Choice.Items.Add(new Selection
                    {
                        Title = $"⚠ Keep missing mapping: {row.Mapping.RegistryValue} / " +
                                $"{row.Mapping.SensorGroup} / {row.Mapping.SensorLabel}"
                    });
                    selected = row.Choice.Items.Count - 1;
                }
                row.Choice.SelectedIndex = selected;
                UpdateRowStatus(row);
            }
        }
        finally { _reloading = false; }
        _statusLine.Text = _catalog.Count == 0
            ? "No Gadget sensors found in HKCU or HKLM. Check that HWiNFO Gadget reporting is enabled."
            : $"{_catalog.Count} exported entries found · Current: {_catalog.Count(e => e.Hive == "CurrentUser")} HKCU / " +
              $"{_catalog.Count(e => e.Hive == "LocalMachine")} HKLM · Nothing changes until you save.";
    }

    private void UpdateRowStatus(FieldControls row)
    {
        var selected = row.Choice.SelectedItem as Selection;
        if (selected?.IsUnassigned == true) { row.Status.Text = "Unassigned"; return; }
        if (selected?.Entry is { } e)
        {
            bool bySlot = string.Equals(row.Mapping.MatchMode, "Slot", StringComparison.OrdinalIgnoreCase);
            bool locked = !string.IsNullOrWhiteSpace(row.Mapping.SensorGroup) &&
                          !string.IsNullOrWhiteSpace(row.Mapping.SensorLabel);
            row.Status.Text = e.HasValue ? (bySlot ? "Live · slot linked" : locked ? "Live · name linked" : "Live · index only") : "No live value";
            return;
        }
        row.Status.Text = "Missing / offline";
    }

    /// <summary>
    /// One click: every reading that is unpaired or no longer found gets its clear best match, with the scale
    /// its unit needs. Readings already paired to a live value are left alone. Then offers to save.
    /// </summary>
    private void AutoPair()
    {
        var picks = SensorPairing.Suggest(_rows.Keys, _catalog, _settings.RegistryHive,
            id => SensorCatalog.Resolve(_rows[id].Mapping, _settings.RegistryHive, _catalog, out _) is { HasValue: true });
        var paired = new List<string>();
        foreach (var (id, pick) in picks)
        {
            var row = _rows[id];
            for (int i = 1; i < row.Choice.Items.Count; i++)
            {
                if ((row.Choice.Items[i] as Selection)?.Entry != pick.Entry) continue;
                row.Scale.Text = pick.Scale.ToString("G", CultureInfo.InvariantCulture);
                row.Mapping = FromEntry(pick.Entry, pick.Scale, id == "GpuFps");
                row.Choice.SelectedIndex = i;
                paired.Add($"{Label(id)}: {SensorPairing.Describe(pick)}");
                break;
            }
        }
        var left = DashboardMetrics.Where(m => !picks.ContainsKey(m.Id) &&
            SensorCatalog.Resolve(_rows[m.Id].Mapping, _settings.RegistryHive, _catalog, out _) is not { HasValue: true }).Select(m => m.Label).ToList();
        string text = paired.Count == 0 ? "Nothing new to pair: every reading HWiNFO exports that Pulsewave recognises is already paired."
            : $"Paired {paired.Count}:\n  " + string.Join("\n  ", paired);
        if (left.Count > 0)
            text += $"\n\nStill unpaired: {string.Join(", ", left)}. Tick them in HWiNFO → Sensor Settings → HWiNFO Gadget " +
                    "(\"Report value in Gadget\"), click Rescan, then Auto-pair again, or pick them by hand.";
        if (paired.Count == 0) { Forms.MessageBox.Show(this, text, "Pulsewave · Auto-pair", Forms.MessageBoxButtons.OK, Forms.MessageBoxIcon.Information); return; }
        if (Forms.MessageBox.Show(this, text + "\n\nSave these mappings now?", "Pulsewave · Auto-pair",
                Forms.MessageBoxButtons.YesNo, Forms.MessageBoxIcon.Question) == Forms.DialogResult.Yes)
            SaveMappings();
        else
            _statusLine.Text = $"Auto-paired {paired.Count} reading(s) · review them, then Save mappings.";
    }

    private static string Label(string id) => Metrics.FirstOrDefault(m => m.Id == id).Label ?? id;

    private void SaveMappings()
    {
        var updated = new Dictionary<string, SensorMapping>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in _rows.Values)
        {
            if (!double.TryParse(row.Scale.Text.Trim(), NumberStyles.Float,
                    CultureInfo.InvariantCulture, out var scale) || !double.IsFinite(scale) || scale <= 0)
            {
                Forms.MessageBox.Show(this, $"'{row.Id}' needs a positive numeric scale (for example 1 or 0.001).",
                    "Invalid scale", Forms.MessageBoxButtons.OK, Forms.MessageBoxIcon.Warning);
                row.Scale.Focus();
                return;
            }
            var selected = row.Choice.SelectedItem as Selection;
            var mapping = selected?.Entry is { } entry ? FromEntry(entry, scale, row.Id == "GpuFps")
                        : selected?.IsUnassigned == true ? new SensorMapping { Scale = scale }
                        : Copy(row.Mapping); // Preserve a currently missing mapping.
            mapping.Scale = scale;
            updated[row.Id] = mapping;
        }

        // Keep any custom fields not shown by the built-in dashboard.
        foreach (var (id, mapping) in _settings.Sensors)
            if (!updated.ContainsKey(id)) updated[id] = Copy(mapping);

        var previous = _settings.Sensors;
        try
        {
            _settings.Sensors = updated;
            SettingsStore.Save(_settings);
            Saved = true;
            Close();
        }
        catch (Exception ex)
        {
            _settings.Sensors = previous;
            Forms.MessageBox.Show(this, "Could not save your mappings: " + ex.Message,
                "Save failed", Forms.MessageBoxButtons.OK, Forms.MessageBoxIcon.Error);
        }
    }
}
