using System.Drawing;
using Forms = System.Windows.Forms;

namespace Pulsewave;

/// <summary>
/// Tray → Weather: type a city, pick the right match, choose °C or °F. Only the typed name goes to
/// the Open-Meteo geocoder; forecasts later send only the saved coordinates.
/// </summary>
internal sealed class WeatherForm : Forms.Form
{
    /// <summary>The Halo desktop look (DesktopStyle), applied once everything in the window exists.</summary>
    protected override void OnLoad(EventArgs e) { DesktopStyle.Apply(this); base.OnLoad(e); }

    private static readonly Color Muted = Color.FromArgb(176, 196, 209);
    private readonly WeatherSettings _settings;
    private readonly Func<string?> _save;
    private readonly Forms.TextBox _query = new() { Width = 300 };
    private readonly Forms.ListBox _places = new() { Dock = Forms.DockStyle.Fill, BorderStyle = Forms.BorderStyle.None, IntegralHeight = false };
    private readonly Forms.RadioButton _celsius = new() { Text = "°C", AutoSize = true }, _fahrenheit = new() { Text = "°F", AutoSize = true };
    private readonly Forms.Label _status = new() { Dock = Forms.DockStyle.Bottom, Height = 40, Padding = new Forms.Padding(16, 6, 16, 0), ForeColor = Muted };
    private IReadOnlyList<WeatherPlace> _found = [];

    /// <param name="save">Saves settings and fetches the weather again; returns why it could not save, or null.</param>
    public WeatherForm(WeatherSettings settings, Func<string?> save)
    {
        _settings = settings;
        _save = save;
        Text = "Lumen HUD · Weather";
        StartPosition = Forms.FormStartPosition.CenterScreen;
        Size = new Size(620, 460);
        MinimumSize = new Size(520, 380);
        BackColor = Color.FromArgb(19, 27, 37);
        ForeColor = Color.FromArgb(232, 242, 249);
        Font = new Font("Segoe UI", 9.5f);
        AutoScaleMode = Forms.AutoScaleMode.Dpi;

        var title = new Forms.Label
        {
            Dock = Forms.DockStyle.Top, Height = 64, Padding = new Forms.Padding(18, 12, 18, 0), ForeColor = Muted,
            Text = "WEATHER\nType your city and pick it from the list. Weather comes from Open-Meteo (free, no account) and is " +
                   "fetched every 15 minutes while a theme shows it. Add it with the editor's + Widget → Weather."
        };
        var search = new Forms.FlowLayoutPanel { Dock = Forms.DockStyle.Top, Height = 44, Padding = new Forms.Padding(14, 6, 14, 0), WrapContents = false };
        _query.Text = settings.City;
        var find = Button("Search", async () => await FindAsync());
        _query.KeyDown += async (_, e) => { if (e.KeyCode == Forms.Keys.Enter) { e.SuppressKeyPress = true; await FindAsync(); } };
        search.Controls.AddRange([_query, find]);

        _places.BackColor = Color.FromArgb(14, 21, 30);
        _places.ForeColor = ForeColor;
        _places.DoubleClick += (_, _) => UseSelected();
        var list = new Forms.Panel { Dock = Forms.DockStyle.Fill, Padding = new Forms.Padding(16, 6, 16, 6) };
        list.Controls.Add(_places);

        var bottom = new Forms.FlowLayoutPanel { Dock = Forms.DockStyle.Bottom, Height = 46, Padding = new Forms.Padding(14, 6, 14, 0), WrapContents = false };
        (settings.Fahrenheit ? _fahrenheit : _celsius).Checked = true;
        _fahrenheit.CheckedChanged += (_, _) =>
        {
            _settings.Fahrenheit = _fahrenheit.Checked;
            ShowStatus(_save() ?? $"Temperatures in {(_settings.Fahrenheit ? "°F" : "°C")}.");
        };
        bottom.Controls.AddRange([Button("Use this city", UseSelected), new Forms.Label { Text = "Units", AutoSize = true, Padding = new Forms.Padding(16, 6, 6, 0), ForeColor = Muted },
            _celsius, _fahrenheit]);

        Controls.Add(list);
        Controls.Add(bottom);
        Controls.Add(search);
        Controls.Add(title);
        Controls.Add(_status);
        ShowStatus(settings.IsSet ? $"Showing weather for {settings.City}." : "No city set yet.");
    }

    public void ShowStatus(string text) => _status.Text = text;

    private async Task FindAsync()
    {
        string name = _query.Text.Trim();
        if (name.Length < 2) { ShowStatus("Type at least two letters of the city."); return; }
        ShowStatus("Searching…");
        try
        {
            _found = await WeatherClient.FindAsync(name);
            _places.Items.Clear();
            foreach (var place in _found) _places.Items.Add(place.Label);
            if (_found.Count > 0) _places.SelectedIndex = 0;
            ShowStatus(_found.Count == 0 ? "No place by that name." : "Pick your city, then Use this city.");
        }
        catch (Exception ex) when (ex is System.Net.Http.HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            ShowStatus("Couldn't reach Open-Meteo: " + ex.Message);
        }
    }

    private void UseSelected()
    {
        if (_places.SelectedIndex < 0 || _places.SelectedIndex >= _found.Count) { ShowStatus("Search, then pick a city in the list."); return; }
        var place = _found[_places.SelectedIndex];
        (_settings.City, _settings.Latitude, _settings.Longitude) = (place.Name, place.Latitude, place.Longitude);
        ShowStatus(_save() ?? $"Showing weather for {place.Label}.");
    }

    private Forms.Button Button(string text, Action click)
    {
        var b = new Forms.Button { Text = text, AutoSize = true, Height = 30, FlatStyle = Forms.FlatStyle.Flat, BackColor = Color.FromArgb(44, 62, 83),
            ForeColor = ForeColor, Margin = new Forms.Padding(6, 0, 4, 0) };
        b.FlatAppearance.BorderSize = 0;
        b.Click += (_, _) => click();
        return b;
    }
}
