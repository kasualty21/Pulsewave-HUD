using System.Drawing;
using Forms = System.Windows.Forms;

namespace Pulsewave;

/// <summary>
/// Tray → Lights: which OpenRGB to talk to and which devices to control, plus every control the
/// HUD's Lights page has with any color, and sliders for brightness and effect speed.
/// </summary>
internal sealed class LightsForm : Forms.Form
{
    /// <summary>The Halo desktop look (DesktopStyle), applied once everything in the window exists.</summary>
    protected override void OnLoad(EventArgs e) { DesktopStyle.Apply(this); base.OnLoad(e); }

    private static readonly Color Panel = Color.FromArgb(27, 38, 50), Field = Color.FromArgb(14, 21, 30), Muted = Color.FromArgb(176, 196, 209);
    private readonly LightsSettings _settings;
    private readonly Action<LightsCommand> _run;
    private readonly Action _refresh;
    private readonly Func<string?> _save;
    private readonly Forms.ComboBox _address = new() { DropDownStyle = Forms.ComboBoxStyle.DropDownList, Width = 110 };
    private readonly Forms.NumericUpDown _port = new() { Minimum = 1, Maximum = 65535, Width = 80 };
    private readonly Forms.CheckedListBox _devices = new() { Dock = Forms.DockStyle.Fill, CheckOnClick = true, BorderStyle = Forms.BorderStyle.None };
    private readonly Forms.ListBox _profiles = new() { Dock = Forms.DockStyle.Fill, BorderStyle = Forms.BorderStyle.None, IntegralHeight = false };
    private readonly Forms.ListBox _effects = new() { Dock = Forms.DockStyle.Fill, BorderStyle = Forms.BorderStyle.None, IntegralHeight = false };
    private readonly Forms.TrackBar _brightness = new() { Minimum = 0, Maximum = 100, TickFrequency = 10, SmallChange = 5, LargeChange = 10, Dock = Forms.DockStyle.Top };
    private readonly Forms.TrackBar _speed = new() { Minimum = 0, Maximum = 100, TickFrequency = 10, SmallChange = 5, LargeChange = 10, Dock = Forms.DockStyle.Top };
    private readonly Forms.Button _swatch = new() { Width = 120, Height = 34, FlatStyle = Forms.FlatStyle.Flat, Text = "" };
    private readonly Forms.Label _status = new() { Dock = Forms.DockStyle.Bottom, Height = 44, Padding = new Forms.Padding(16, 6, 16, 0) };
    private readonly Forms.Label _brightnessLabel = new() { AutoSize = true, ForeColor = Muted }, _speedLabel = new() { AutoSize = true, ForeColor = Muted };
    private readonly Forms.Timer _brightnessDelay = new() { Interval = 350 };
    private List<OpenRgbDevice> _deviceList = new();
    private List<string> _effectNames = new();
    private bool _loading;

    /// <param name="run">Runs a command (in the background) and reports back through <see cref="ShowStatus"/>.</param>
    /// <param name="refresh">Asks OpenRGB for its devices and profiles again; the answer comes to <see cref="ShowState"/>.</param>
    /// <param name="save">Saves settings; returns why it could not, or null.</param>
    internal LightsForm(LightsSettings settings, Action<LightsCommand> run, Action refresh, Func<string?> save)
    {
        _settings = settings;
        _run = run;
        _refresh = refresh;
        _save = save;
        Text = "Pulsewave · Lights";
        StartPosition = Forms.FormStartPosition.CenterScreen;
        Size = new Size(1040, 620);
        MinimumSize = new Size(880, 520);
        BackColor = Color.FromArgb(19, 27, 37);
        ForeColor = Color.FromArgb(232, 242, 249);
        Font = new Font("Segoe UI", 9.5f);
        AutoScaleMode = Forms.AutoScaleMode.Dpi;

        var title = new Forms.Label
        {
            Dock = Forms.DockStyle.Top, Height = 64, Padding = new Forms.Padding(18, 12, 18, 0), ForeColor = Muted,
            Text = "LIGHTS\nControls your RGB through OpenRGB on this PC. Start OpenRGB, then in its SDK Server tab click Start Server " +
                   "(OpenRGB's settings can start it at launch). Themes can open the Lights page with the Lights page or Lights on / off actions."
        };

        var connection = new Forms.FlowLayoutPanel { Dock = Forms.DockStyle.Top, Height = 44, Padding = new Forms.Padding(14, 6, 14, 0), WrapContents = false };
        _address.Items.AddRange(OpenRgbClient.Addresses);
        _address.SelectedItem = settings.Address;
        _port.Value = settings.Port;
        _address.SelectedIndexChanged += (_, _) => { if (!_loading && _address.SelectedItem is string a) { _settings.Address = a; Save(); _refresh(); } };
        _port.ValueChanged += (_, _) => { if (!_loading) { _settings.Port = (int)_port.Value; Save(); } };
        var connect = Button("Connect / refresh", () => { ShowStatus("Connecting…"); _refresh(); });
        var openApp = Button("Open OpenRGB", () =>
        {
            var (_, status) = OpenRgbApp.Open(_settings);
            ShowStatus(status);
            Save(); // remembers where OpenRGB is
        });
        connection.Controls.AddRange([Caption("OpenRGB address"), _address, Caption("Port"), _port, connect, openApp,
            Caption("127.0.0.1 is the OpenRGB app; a background OpenRGB service may use 127.0.0.2.")]);

        // Devices | profiles | color, brightness, effects
        var columns = new Forms.TableLayoutPanel { Dock = Forms.DockStyle.Fill, ColumnCount = 3, Padding = new Forms.Padding(10, 4, 10, 4) };
        columns.ColumnStyles.Add(new Forms.ColumnStyle(Forms.SizeType.Percent, 34));
        columns.ColumnStyles.Add(new Forms.ColumnStyle(Forms.SizeType.Percent, 26));
        columns.ColumnStyles.Add(new Forms.ColumnStyle(Forms.SizeType.Percent, 40));
        columns.RowStyles.Add(new Forms.RowStyle(Forms.SizeType.Percent, 100));

        _devices.BackColor = _profiles.BackColor = _effects.BackColor = Field;
        _devices.ForeColor = _profiles.ForeColor = _effects.ForeColor = ForeColor;
        _devices.ItemCheck += (_, e) =>
        {
            if (_loading || e.Index < 0 || e.Index >= _deviceList.Count) return;
            string id = _deviceList[e.Index].Identity;
            _settings.SkipDevices.Remove(id);
            if (e.NewValue != Forms.CheckState.Checked) _settings.SkipDevices.Add(id);
            Save();
            BeginInvoke(FillEffects);
        };
        columns.Controls.Add(Box("DEVICES · ticked ones are controlled", _devices, null), 0, 0);

        var loadProfile = Button("Load profile", () => { if (_profiles.SelectedItem is string p) Run(new LightsCommand(LightsKind.Profile, p)); });
        _profiles.DoubleClick += (_, _) => { if (_profiles.SelectedItem is string p) Run(new LightsCommand(LightsKind.Profile, p)); };
        columns.Controls.Add(Box("OPENRGB PROFILES · saved in OpenRGB", _profiles, loadProfile), 1, 0);

        var right = new Forms.TableLayoutPanel { Dock = Forms.DockStyle.Fill, RowCount = 2, Margin = new Forms.Padding(6) };
        right.RowStyles.Add(new Forms.RowStyle(Forms.SizeType.AutoSize));
        right.RowStyles.Add(new Forms.RowStyle(Forms.SizeType.Percent, 100));
        var controls = new Forms.FlowLayoutPanel { Dock = Forms.DockStyle.Fill, FlowDirection = Forms.FlowDirection.TopDown, WrapContents = false, AutoSize = true, BackColor = Panel, Padding = new Forms.Padding(10) };
        var colorRow = new Forms.FlowLayoutPanel { AutoSize = true, WrapContents = false };
        _swatch.Click += (_, _) => PickColor();
        colorRow.Controls.AddRange([_swatch, Button("Set color", () => Run(new LightsCommand(LightsKind.Color))),
            Button("Off", () => Run(new LightsCommand(LightsKind.Off))), Button("On", () => Run(new LightsCommand(LightsKind.On)))]);
        _brightness.Width = _speed.Width = 360;
        _brightness.BackColor = _speed.BackColor = Panel;
        _brightness.ValueChanged += (_, _) =>
        {
            _brightnessLabel.Text = $"Brightness {_brightness.Value}%";
            if (_loading) return;
            _brightnessDelay.Stop(); _brightnessDelay.Start(); // one command when the slider settles
        };
        _brightnessDelay.Tick += (_, _) =>
        {
            _brightnessDelay.Stop();
            _settings.Brightness = _brightness.Value;
            Run(new LightsCommand(LightsKind.Brightness));
        };
        _speed.ValueChanged += (_, _) => { _speedLabel.Text = $"Effect speed {_speed.Value}%"; if (!_loading) { _settings.Speed = _speed.Value; Save(); } };
        controls.Controls.AddRange([Heading("COLOR · click the swatch to choose any color"), colorRow, _brightnessLabel, _brightness, _speedLabel, _speed]);
        right.Controls.Add(controls, 0, 0);
        var applyEffect = Button("Apply effect", () =>
        {
            if (_effects.SelectedIndex >= 0 && _effects.SelectedIndex < _effectNames.Count) Run(new LightsCommand(LightsKind.Effect, _effectNames[_effects.SelectedIndex]));
        });
        _effects.DoubleClick += (_, _) => applyEffect.PerformClick();
        right.Controls.Add(Box("HARDWARE EFFECTS · use the color above when they take one", _effects, applyEffect), 0, 1);
        columns.Controls.Add(right, 2, 0);

        _status.ForeColor = Muted;
        Controls.Add(columns);
        Controls.Add(connection);
        Controls.Add(title);
        Controls.Add(_status);

        _loading = true;
        _brightness.Value = settings.Brightness;
        _speed.Value = settings.Speed;
        _brightnessLabel.Text = $"Brightness {settings.Brightness}%";
        _speedLabel.Text = $"Effect speed {settings.Speed}%";
        _loading = false;
        ShowSwatch();
        ShowStatus("Connecting to OpenRGB…");
        FormClosed += (_, _) => _brightnessDelay.Dispose();
    }

    internal void ShowState(LightsState? state, string status)
    {
        ShowStatus(status);
        _loading = true;
        _deviceList = state?.Devices.ToList() ?? new();
        _devices.Items.Clear();
        foreach (var d in _deviceList)
            _devices.Items.Add($"{d.Name}  ·  {d.TypeName}  ·  {d.Colors.Count} LED{(d.Colors.Count == 1 ? "" : "s")}", !_settings.SkipDevices.Contains(d.Identity));
        _profiles.Items.Clear();
        foreach (var p in state?.Profiles ?? []) _profiles.Items.Add(p);
        _loading = false;
        _state = state;
        FillEffects();
    }

    private LightsState? _state;

    private void FillEffects()
    {
        _effects.Items.Clear();
        _effectNames = _state == null ? new() : Lights.Effects(_state, _settings).Select(e => e.Name).ToList();
        int total = _state?.Devices.Count(d => !_settings.SkipDevices.Contains(d.Identity)) ?? 0;
        if (_state != null)
            foreach (var (name, count) in Lights.Effects(_state, _settings))
                _effects.Items.Add(count == total ? name : $"{name}  ·  {count} of {total} devices");
    }

    public void ShowStatus(string text) => _status.Text = text;

    private void Run(LightsCommand command)
    {
        ShowStatus("Working…");
        _run(command);
    }

    private void PickColor()
    {
        Lights.TryColor(_settings.Color, out var current);
        using var dialog = new Forms.ColorDialog { FullOpen = true, Color = Color.FromArgb(current.R, current.G, current.B) };
        if (dialog.ShowDialog(this) != Forms.DialogResult.OK) return;
        _settings.Color = $"#{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}";
        ShowSwatch();
        Run(new LightsCommand(LightsKind.Color));
    }

    private void ShowSwatch()
    {
        Lights.TryColor(_settings.Color, out var c);
        _swatch.BackColor = Color.FromArgb(c.R, c.G, c.B);
        _swatch.FlatAppearance.BorderColor = Muted;
    }

    private void Save()
    {
        if (_save() is { } error) ShowStatus(error);
    }

    private Forms.Control Box(string heading, Forms.Control body, Forms.Control? button)
    {
        var panel = new Forms.Panel { Dock = Forms.DockStyle.Fill, BackColor = Panel, Padding = new Forms.Padding(10), Margin = new Forms.Padding(6) };
        var label = Heading(heading);
        label.Dock = Forms.DockStyle.Top;
        panel.Controls.Add(body);
        if (button != null) { button.Dock = Forms.DockStyle.Bottom; panel.Controls.Add(button); }
        panel.Controls.Add(label);
        return panel;
    }

    private static Forms.Label Heading(string text) =>
        new() { Text = text, AutoSize = false, Height = 26, Width = 380, ForeColor = Color.FromArgb(118, 245, 217), Font = new Font("Segoe UI Semibold", 9.5f) };

    private static Forms.Label Caption(string text) =>
        new() { Text = text, AutoSize = true, ForeColor = Muted, Margin = new Forms.Padding(8, 7, 4, 0) };

    private Forms.Button Button(string text, Action click)
    {
        var b = new Forms.Button { Text = text, AutoSize = true, Height = 32, FlatStyle = Forms.FlatStyle.Flat, BackColor = Color.FromArgb(44, 62, 83),
            ForeColor = ForeColor, Margin = new Forms.Padding(4, 2, 4, 2) };
        b.FlatAppearance.BorderSize = 0;
        b.Click += (_, _) => click();
        return b;
    }
}
