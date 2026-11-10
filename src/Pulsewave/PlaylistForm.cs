using Pulsewave.Themes;
using Color = System.Drawing.Color;
using Font = System.Drawing.Font;
using Size = System.Drawing.Size;
using Forms = System.Windows.Forms;

namespace Pulsewave;

/// <summary>What the playlist window saves.</summary>
public sealed record PlaylistChoice(List<string> Playlist, bool Enabled, int Minutes);

/// <summary>
/// Theme playlist: tick the themes to rotate through, order them and pick the interval
/// (hotkeys moved to tray → Hotkeys in 0.12).
/// </summary>
public sealed class PlaylistForm : Forms.Form
{
    /// <summary>The Halo desktop look (DesktopStyle), applied once everything in the window exists.</summary>
    protected override void OnLoad(EventArgs e) { DesktopStyle.Apply(this); base.OnLoad(e); }

    private readonly Forms.CheckedListBox _themes = new();
    private readonly Forms.CheckBox _enabled = new();
    private readonly Forms.NumericUpDown _minutes = new();
    private readonly List<(string Id, string Name, int Width, int Height)> _items;
    private readonly HashSet<string> _ticked;
    private readonly List<int> _shown = []; // the _items index of each list row
    private readonly Forms.ComboBox _filter = new() { DropDownStyle = Forms.ComboBoxStyle.DropDownList, Width = 200 };
    private readonly Forms.Label _hidden = new() { AutoSize = true, Padding = new Forms.Padding(10, 7, 0, 0), ForeColor = Color.FromArgb(176, 196, 209) };
    private readonly Func<(int Width, int Height)>? _displaySize;
    private readonly Action<string>? _filterChanged;
    private readonly MonitorSettings _settings;
    private readonly Func<PlaylistChoice, IReadOnlyList<string>> _save;
    private bool _filling;

    /// <param name="save">Stores the choice; returns problems (unsaved settings), if any.</param>
    /// <param name="displaySize">The HUD's size, for the "Fits this display" filter.</param>
    /// <param name="filterChanged">Saves the shape filter (shared with the Themes gallery) when it is changed.</param>
    public PlaylistForm(MonitorSettings settings, IReadOnlyList<(string Id, string Name, int Width, int Height)> available, Func<PlaylistChoice, IReadOnlyList<string>> save,
        Func<(int Width, int Height)>? displaySize = null, Action<string>? filterChanged = null)
    {
        _save = save;
        _settings = settings;
        _displaySize = displaySize;
        _filterChanged = filterChanged;
        Text = "Pulsewave · Theme playlist";
        StartPosition = Forms.FormStartPosition.CenterScreen;
        Size = new Size(620, 800);
        MinimumSize = new Size(520, 600);
        BackColor = Color.FromArgb(19, 27, 37);
        ForeColor = Color.FromArgb(232, 242, 249);
        Font = new Font("Segoe UI", 9.5f);
        AutoScaleMode = Forms.AutoScaleMode.Dpi;

        // Playlist entries first, in their saved order, then everything else in gallery order.
        // Themes made for another screen size say so.
        var themes = available.Select(t => (t.Id, Name: t.Width == 1920 && t.Height == 480 ? t.Name : $"{t.Name}  ·  {t.Width}×{t.Height}", t.Width, t.Height)).ToList();
        var byId = themes.ToDictionary(t => t.Id, StringComparer.Ordinal);
        _items = settings.ThemePlaylist.Where(byId.ContainsKey).Distinct().Select(id => byId[id])
            .Concat(themes.Where(t => !settings.ThemePlaylist.Contains(t.Id))).ToList();
        _ticked = new HashSet<string>(settings.ThemePlaylist.Where(byId.ContainsKey), StringComparer.Ordinal);

        var intro = new Forms.Label
        {
            Dock = Forms.DockStyle.Top, Height = 64, Padding = new Forms.Padding(16, 12, 16, 0), ForeColor = Color.FromArgb(176, 196, 209),
            Text = "Tick the themes to rotate through and put them in order. With none ticked, rotation and the hotkeys go through every theme shaped like the display."
        };

        _themes.Dock = Forms.DockStyle.Fill;
        _themes.CheckOnClick = true;
        _themes.BackColor = Color.FromArgb(23, 32, 43);
        _themes.ForeColor = ForeColor;
        _themes.BorderStyle = Forms.BorderStyle.None;
        _themes.IntegralHeight = false;
        _themes.ItemCheck += (_, e) =>
        {
            if (_filling) return;
            string id = _items[_shown[e.Index]].Id;
            if (e.NewValue == Forms.CheckState.Checked) _ticked.Add(id); else _ticked.Remove(id);
            ShowHidden();
        };

        // Shape filter, the same one the Themes gallery uses. Hidden themes keep their tick and place.
        var filterBar = new Forms.FlowLayoutPanel { Dock = Forms.DockStyle.Top, Height = 40, Padding = new Forms.Padding(14, 4, 14, 0), WrapContents = false };
        foreach (var (_, label) in DisplaySizes.Filters) _filter.Items.Add(label);
        _filter.SelectedIndex = Math.Max(0, DisplaySizes.Filters.ToList().FindIndex(f => f.Key == settings.ThemeGalleryFilter));
        _filter.BackColor = Color.FromArgb(27, 38, 50);
        _filter.ForeColor = ForeColor;
        _filter.FlatStyle = Forms.FlatStyle.Flat;
        _filter.SelectedIndexChanged += (_, _) =>
        {
            if (FilterKey != _settings.ThemeGalleryFilter) _filterChanged?.Invoke(FilterKey);
            Fill(_themes.SelectedIndex >= 0 ? _shown[_themes.SelectedIndex] : -1);
        };
        filterBar.Controls.AddRange([new Forms.Label { Text = "Show", AutoSize = true, Padding = new Forms.Padding(0, 7, 6, 0) }, _filter, _hidden]);
        Fill(-1);

        var order = new Forms.FlowLayoutPanel { Dock = Forms.DockStyle.Right, Width = 120, FlowDirection = Forms.FlowDirection.TopDown, Padding = new Forms.Padding(8, 0, 8, 0) };
        var up = Button("Move up", Color.FromArgb(44, 62, 83));
        var down = Button("Move down", Color.FromArgb(44, 62, 83));
        up.Click += (_, _) => MoveSelected(-1);
        down.Click += (_, _) => MoveSelected(+1);
        order.Controls.AddRange([up, down]);
        var listArea = new Forms.Panel { Dock = Forms.DockStyle.Fill, Padding = new Forms.Padding(16, 0, 8, 0) };
        listArea.Controls.Add(_themes);
        listArea.Controls.Add(order);

        var options = new Forms.TableLayoutPanel
        {
            Dock = Forms.DockStyle.Bottom, Height = 92, Padding = new Forms.Padding(16, 12, 16, 0), ColumnCount = 2, RowCount = 2
        };
        options.ColumnStyles.Add(new Forms.ColumnStyle(Forms.SizeType.Absolute, 220));
        options.ColumnStyles.Add(new Forms.ColumnStyle(Forms.SizeType.Percent, 100));
        _enabled.Text = "Rotate automatically every";
        _enabled.Checked = settings.PlaylistEnabled;
        _enabled.AutoSize = true;
        _minutes.Minimum = 1; _minutes.Maximum = 1440; _minutes.Width = 80;
        _minutes.Value = Math.Clamp(settings.PlaylistMinutes, 1, 1440);
        var minutesRow = new Forms.FlowLayoutPanel { AutoSize = true, WrapContents = false };
        minutesRow.Controls.AddRange([_minutes, new Forms.Label { Text = "minutes", AutoSize = true, Padding = new Forms.Padding(4, 6, 0, 0) }]);
        options.Controls.Add(_enabled, 0, 0);
        options.Controls.Add(minutesRow, 1, 0);
        options.Controls.Add(new Forms.Label
        {
            AutoSize = true, ForeColor = Color.FromArgb(160, 185, 201), MaximumSize = new Size(540, 0),
            Text = "Hotkeys for next / previous theme and rotation are in tray → Hotkeys, with any other action."
        }, 0, 1);
        options.SetColumnSpan(options.GetControlFromPosition(0, 1)!, 2);

        var footer = new Forms.FlowLayoutPanel
        {
            Dock = Forms.DockStyle.Bottom, Height = 60, Padding = new Forms.Padding(12, 10, 12, 8),
            FlowDirection = Forms.FlowDirection.RightToLeft, WrapContents = false
        };
        var saveButton = Button("Save", Color.FromArgb(40, 128, 117));
        var cancel = Button("Cancel", Color.FromArgb(44, 62, 83));
        saveButton.Click += (_, _) => Save();
        cancel.Click += (_, _) => Close();
        footer.Controls.AddRange([saveButton, cancel]);

        Controls.Add(listArea);
        Controls.Add(options);
        Controls.Add(footer);
        Controls.Add(filterBar);
        Controls.Add(intro);
    }

    private static Forms.Button Button(string text, Color color) => new()
    {
        Text = text, Width = 104, Height = 34, Margin = new Forms.Padding(4, 0, 4, 6),
        FlatStyle = Forms.FlatStyle.Flat, BackColor = color, ForeColor = Color.White
    };

    private string FilterKey => _filter.SelectedIndex >= 0 ? DisplaySizes.Filters[_filter.SelectedIndex].Key : "";

    /// <summary>Lists the themes the filter lets through, keeping <paramref name="select"/> (an _items index) selected.</summary>
    private void Fill(int select)
    {
        var (dw, dh) = _displaySize?.Invoke() ?? (0, 0);
        string filter = FilterKey;
        _filling = true;
        _themes.BeginUpdate();
        _themes.Items.Clear();
        _shown.Clear();
        for (int i = 0; i < _items.Count; i++)
        {
            if (!DisplaySizes.Matches(filter, _items[i].Width, _items[i].Height, dw, dh)) continue;
            _shown.Add(i);
            _themes.Items.Add(_items[i].Name, _ticked.Contains(_items[i].Id));
        }
        _themes.EndUpdate();
        _filling = false;
        int row = _shown.IndexOf(select);
        if (row >= 0) _themes.SelectedIndex = row;
        ShowHidden();
    }

    private void ShowHidden()
    {
        var shownIds = _shown.Select(i => _items[i].Id).ToHashSet(StringComparer.Ordinal);
        int hiddenTicked = _ticked.Count(id => !shownIds.Contains(id));
        _hidden.Text = hiddenTicked > 0 ? $"{_shown.Count} of {_items.Count} · {hiddenTicked} ticked hidden, still in playlist"
                                        : $"{_shown.Count} of {_items.Count} themes";
    }

    /// <summary>Swaps the selected theme with the next listed one up or down; hidden themes keep their places.</summary>
    internal void MoveSelected(int direction)
    {
        int from = _themes.SelectedIndex, to = from + direction;
        if (from < 0 || to < 0 || to >= _shown.Count) return;
        int a = _shown[from], b = _shown[to];
        (_items[a], _items[b]) = (_items[b], _items[a]);
        Fill(b);
    }

    /// <summary>The ticked themes in list order, hidden ones included.</summary>
    internal List<string> Playlist => _items.Where(t => _ticked.Contains(t.Id)).Select(t => t.Id).ToList();

    /// <summary>For checks: the themes listed now, as ids, and choosing a row or filter.</summary>
    internal IReadOnlyList<string> ShownIds => _shown.Select(i => _items[i].Id).ToList();
    internal void SelectRow(int row) => _themes.SelectedIndex = row;
    internal void SetFilter(string key) => _filter.SelectedIndex = Math.Max(0, DisplaySizes.Filters.ToList().FindIndex(f => f.Key == key));
    internal void Tick(int row, bool ticked) => _themes.SetItemChecked(row, ticked);

    private void Save()
    {
        var problems = _save(new PlaylistChoice(Playlist, _enabled.Checked, (int)_minutes.Value));
        if (problems.Count > 0)
            Forms.MessageBox.Show(this, "Some of this needs attention:\n\n" + string.Join("\n", problems),
                "Pulsewave · Theme playlist", Forms.MessageBoxButtons.OK, Forms.MessageBoxIcon.Warning);
        Close();
    }
}
