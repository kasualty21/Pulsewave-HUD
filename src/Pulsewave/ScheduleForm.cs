using System.Drawing;
using System.Globalization;
using Forms = System.Windows.Forms;

namespace Pulsewave;

/// <summary>
/// Tray → Schedule: times the LEDs turn off and back on, and optionally the HUD blanks, on chosen
/// days. Changes save straight away.
/// </summary>
internal sealed class ScheduleForm : Forms.Form
{
    /// <summary>The Halo desktop look (DesktopStyle), applied once everything in the window exists.</summary>
    protected override void OnLoad(EventArgs e) { DesktopStyle.Apply(this); base.OnLoad(e); }

    private static readonly Color Panel = Color.FromArgb(27, 38, 50), Muted = Color.FromArgb(176, 196, 209);
    // Shown Monday first; DayOfWeek numbers the bits (Sunday = 0).
    private static readonly DayOfWeek[] Week = [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday,
                                                DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday];
    private readonly List<ScheduleRule> _rules;
    private readonly Func<List<ScheduleRule>, string?> _save;
    private readonly Forms.ListBox _list = new() { Dock = Forms.DockStyle.Fill, BorderStyle = Forms.BorderStyle.None, IntegralHeight = false };
    private readonly Forms.CheckBox _enabled = Box("On"), _lights = Box("LEDs off (through OpenRGB, tray → Lights)"),
        _screen = Box("Screen off (the HUD goes black; a tap wakes it for a minute)"),
        _dim = Box("Dim the HUD (night mode)");
    private readonly Forms.ComboBox _dimMode = new() { DropDownStyle = Forms.ComboBoxStyle.DropDownList, Width = 210 };
    private readonly Forms.NumericUpDown _dimLevel = new() { Minimum = 10, Maximum = 90, Increment = 5, Width = 60, Value = 40 };
    private static readonly string[] DimLabels = ["Darken the theme, no glows", "Lower the display's backlight", "Both"];
    private readonly Forms.DateTimePicker _off = Picker(), _on = Picker();
    private readonly Forms.CheckBox[] _days = Week.Select(d => Box(CultureInfo.CurrentCulture.DateTimeFormat.GetAbbreviatedDayName(d))).ToArray();
    private readonly Forms.Panel _editor = new() { Dock = Forms.DockStyle.Right, Width = 400, BackColor = Panel, Padding = new Forms.Padding(14, 10, 14, 10) };
    private readonly Forms.Label _status = new() { Dock = Forms.DockStyle.Bottom, Height = 40, Padding = new Forms.Padding(16, 6, 16, 0), ForeColor = Muted };
    private bool _loading;

    /// <param name="save">Saves the rules; returns why it could not, or null.</param>
    /// <param name="setPowerOff">Saves the display power-down option; returns why it could not, or null.</param>
    public ScheduleForm(IEnumerable<ScheduleRule> rules, Func<List<ScheduleRule>, string?> save, string status,
        bool powerOff = false, Func<bool, string?>? setPowerOff = null)
    {
        _rules = rules.Select(r => r.Copy()).ToList();
        _save = save;
        Text = "Pulsewave · Schedule";
        StartPosition = Forms.FormStartPosition.CenterScreen;
        Size = new Size(860, 480);
        MinimumSize = new Size(760, 420);
        BackColor = Color.FromArgb(19, 27, 37);
        ForeColor = Color.FromArgb(232, 242, 249);
        Font = new Font("Segoe UI", 9.5f);
        AutoScaleMode = Forms.AutoScaleMode.Dpi;

        var title = new Forms.Label
        {
            Dock = Forms.DockStyle.Top, Height = 70, Padding = new Forms.Padding(18, 12, 18, 0), ForeColor = Muted,
            Text = "SCHEDULE\nTurn the LEDs and the HUD off, or dim the HUD, and back at set times. An Off time after the On time runs " +
                   "overnight (23:00 → 07:00). Lights you switch by hand in between are left as they are."
        };

        _list.BackColor = Color.FromArgb(14, 21, 30);
        _list.ForeColor = ForeColor;
        _list.SelectedIndexChanged += (_, _) => ShowSelected();

        var fields = new Forms.FlowLayoutPanel { Dock = Forms.DockStyle.Fill, FlowDirection = Forms.FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
        var times = new Forms.FlowLayoutPanel { AutoSize = true, WrapContents = false };
        times.Controls.AddRange([Caption("Off at"), _off, Caption("On at"), _on]);
        var days = new Forms.FlowLayoutPanel { AutoSize = true, WrapContents = true, Width = 360 };
        foreach (var day in _days) { day.Width = 64; days.Controls.Add(day); }
        var everyDay = Button("Every day", () => SetDays(127));
        var weekdays = Button("Weekdays", () => SetDays(0b0111110));
        var weekend = Button("Weekend", () => SetDays(0b1000001));
        var presets = new Forms.FlowLayoutPanel { AutoSize = true, WrapContents = false };
        presets.Controls.AddRange([everyDay, weekdays, weekend]);
        foreach (var label in DimLabels) _dimMode.Items.Add(label);
        var dimRow = new Forms.FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Forms.Padding(18, 0, 0, 0) };
        dimRow.Controls.AddRange([_dimMode, Caption("brightness %"), _dimLevel]);
        fields.Controls.AddRange([_enabled, times, Caption("Starting on"), days, presets, _lights, _screen, _dim, dimRow,
            new Forms.Label { AutoSize = false, Width = 360, Height = 96, ForeColor = Muted, Margin = new Forms.Padding(0, 10, 0, 0),
                Text = "Waking the screen by tap needs click-through off. While it is black the HUD stops drawing; telemetry and session stats keep running. " +
                       "The backlight works on displays that take brightness over DDC/CI (not the main display) and is put back afterwards." }]);
        _editor.Controls.Add(fields);

        foreach (var box in new[] { _enabled, _lights, _screen, _dim }.Concat(_days)) box.CheckedChanged += (_, _) => Changed();
        _dimMode.SelectedIndexChanged += (_, _) => Changed();
        _dimLevel.ValueChanged += (_, _) => Changed();
        _off.ValueChanged += (_, _) => Changed();
        _on.ValueChanged += (_, _) => Changed();

        var buttons = new Forms.FlowLayoutPanel { Dock = Forms.DockStyle.Bottom, Height = 44, Padding = new Forms.Padding(8, 4, 8, 4) };
        buttons.Controls.Add(Button("Add", () =>
        {
            _rules.Add(new ScheduleRule());
            Save();
            Fill(_rules.Count - 1);
        }));
        buttons.Controls.Add(Button("Remove", () =>
        {
            if (_list.SelectedIndex < 0) return;
            int index = _list.SelectedIndex;
            _rules.RemoveAt(index);
            Save();
            Fill(Math.Min(index, _rules.Count - 1));
        }));
        var power = new Forms.CheckBox { Text = "Screen off also puts the HUD's display in standby (if it supports DDC/CI)",
            Checked = powerOff, Dock = Forms.DockStyle.Bottom, Height = 30, Padding = new Forms.Padding(4, 0, 0, 0) };
        power.CheckedChanged += (_, _) => { if (setPowerOff?.Invoke(power.Checked) is { } error) ShowStatus(error); };
        var left = new Forms.Panel { Dock = Forms.DockStyle.Fill, Padding = new Forms.Padding(14, 6, 8, 6) };
        left.Controls.Add(_list);
        left.Controls.Add(buttons);
        left.Controls.Add(power);

        Controls.Add(left);
        Controls.Add(_editor);
        Controls.Add(title);
        Controls.Add(_status);
        ShowStatus(status.Length > 0 ? status : "Times use this PC's clock.");
        Fill(_rules.Count > 0 ? 0 : -1);
    }

    public void ShowStatus(string text) => _status.Text = text;

    private void Fill(int select)
    {
        _loading = true;
        _list.Items.Clear();
        foreach (var r in _rules) _list.Items.Add(Summary(r));
        _loading = false;
        if (select >= 0 && select < _list.Items.Count) _list.SelectedIndex = select;
        ShowSelected();
    }

    internal static string Summary(ScheduleRule r)
    {
        string what = r.Lights && r.Screen ? "LEDs and screen" : r.Lights ? "LEDs" : r.Screen ? "Screen" : r.Dim ? "" : "Nothing";
        string dim = r.Dim ? (what.Length > 0 ? $", dim {r.DimLevel}%" : $"Dim {r.DimLevel}%") : "";
        string days = r.Days switch
        {
            127 => "every day", 0b0111110 => "weekdays", 0b1000001 => "weekends", 0 => "no days",
            _ => string.Join(" ", Week.Where(d => (r.Days & (1 << (int)d)) != 0).Select(d => CultureInfo.CurrentCulture.DateTimeFormat.GetAbbreviatedDayName(d)))
        };
        return what.Length > 0 ? $"{(r.Enabled ? "" : "(off) ")}{what} off {r.Off} → on {r.On}{dim}, {days}"
                               : $"{(r.Enabled ? "" : "(off) ")}{dim} {r.Off} → {r.On}, {days}";
    }

    private ScheduleRule? Selected => _list.SelectedIndex >= 0 && _list.SelectedIndex < _rules.Count ? _rules[_list.SelectedIndex] : null;

    private void ShowSelected()
    {
        _editor.Enabled = Selected != null;
        if (Selected is not { } r) return;
        _loading = true;
        _enabled.Checked = r.Enabled;
        _lights.Checked = r.Lights;
        _screen.Checked = r.Screen;
        _dim.Checked = r.Dim;
        _dimMode.SelectedIndex = Math.Max(0, Array.IndexOf(ScheduleRule.DimModes, r.DimMode));
        _dimLevel.Value = Math.Clamp(r.DimLevel, 10, 90);
        _dimMode.Enabled = _dimLevel.Enabled = r.Dim;
        _off.Value = DateTime.Today + (ScheduleRule.TryTime(r.Off, out var off) ? off.ToTimeSpan() : TimeSpan.FromHours(23));
        _on.Value = DateTime.Today + (ScheduleRule.TryTime(r.On, out var on) ? on.ToTimeSpan() : TimeSpan.FromHours(7));
        for (int i = 0; i < Week.Length; i++) _days[i].Checked = (r.Days & (1 << (int)Week[i])) != 0;
        _loading = false;
    }

    private void SetDays(int days)
    {
        _loading = true;
        for (int i = 0; i < Week.Length; i++) _days[i].Checked = (days & (1 << (int)Week[i])) != 0;
        _loading = false;
        Changed();
    }

    private void Changed()
    {
        if (_loading || Selected is not { } r) return;
        r.Enabled = _enabled.Checked;
        r.Lights = _lights.Checked;
        r.Screen = _screen.Checked;
        r.Dim = _dim.Checked;
        r.DimMode = ScheduleRule.DimModes[Math.Max(0, _dimMode.SelectedIndex)];
        r.DimLevel = (int)_dimLevel.Value;
        _dimMode.Enabled = _dimLevel.Enabled = r.Dim;
        r.Off = _off.Value.ToString("HH:mm", CultureInfo.InvariantCulture);
        r.On = _on.Value.ToString("HH:mm", CultureInfo.InvariantCulture);
        r.Days = 0;
        for (int i = 0; i < Week.Length; i++) if (_days[i].Checked) r.Days |= 1 << (int)Week[i];
        Save();
        _loading = true;
        _list.Items[_list.SelectedIndex] = Summary(r);
        _loading = false;
    }

    private void Save()
    {
        if (_save(_rules) is { } error) ShowStatus(error);
    }

    private static Forms.CheckBox Box(string text) => new() { Text = text, AutoSize = true, Margin = new Forms.Padding(0, 6, 0, 2) };

    private static Forms.DateTimePicker Picker() =>
        new() { Format = Forms.DateTimePickerFormat.Custom, CustomFormat = "HH:mm", ShowUpDown = true, Width = 80 };

    private static Forms.Label Caption(string text) =>
        new() { Text = text, AutoSize = true, ForeColor = Muted, Margin = new Forms.Padding(0, 8, 6, 0) };

    private Forms.Button Button(string text, Action click)
    {
        var b = new Forms.Button { Text = text, AutoSize = true, Height = 30, FlatStyle = Forms.FlatStyle.Flat, BackColor = Color.FromArgb(44, 62, 83),
            ForeColor = ForeColor, Margin = new Forms.Padding(4, 2, 4, 2) };
        b.FlatAppearance.BorderSize = 0;
        b.Click += (_, _) => click();
        return b;
    }
}
