using System.Drawing;
using System.IO;
using Forms = System.Windows.Forms;

namespace Pulsewave;

/// <summary>
/// Tray → Gaming: the games Pulsewave recognises (and where it found them), games you added, programs
/// that never count, and programs it suggested. Add a game by picking its program; changes save at once.
/// </summary>
internal sealed class GamingForm : Forms.Form
{
    /// <summary>The Halo desktop look (DesktopStyle), applied once everything in the window exists.</summary>
    protected override void OnLoad(EventArgs e) { DesktopStyle.Apply(this); base.OnLoad(e); }

    private static readonly Color Muted = Color.FromArgb(176, 196, 209), Panel = Color.FromArgb(14, 21, 30);
    private readonly GamingSettings _settings;
    private readonly Func<IReadOnlyList<GameEntry>> _catalog;
    private readonly Func<IReadOnlyList<string>> _suggested;
    private readonly Func<string?> _save;
    private readonly Action<string, string> _track;
    private readonly Action<string> _ignore;
    private readonly Forms.ListView _games = List(("Game", 300), ("Found in", 100), ("Theme", 170), ("Matched by", 230));
    private readonly IReadOnlyList<(string Id, string Name)> _themes;
    private readonly Forms.ComboBox _defaultTheme = Choice(), _gameTheme = Choice();
    private readonly Forms.Label _gameLabel = new() { AutoSize = true, ForeColor = Muted, Padding = new Forms.Padding(14, 7, 6, 0) };
    private bool _filling;

    /// <summary>A theme choice: an id, or "" for the default / don't switch, and <see cref="GamingSettings.NoTheme"/>.</summary>
    private sealed record ThemeChoice(string Id, string Label) { public override string ToString() => Label; }
    private readonly Forms.ListBox _added = Box(), _ignored = Box(), _pending = Box();
    private readonly Forms.CheckBox _suggest = new() { Text = "Suggest programs that look like games (full-screen with the GPU busy for 2 minutes)", AutoSize = true, ForeColor = Color.White };
    private readonly Forms.Label _status = new() { Dock = Forms.DockStyle.Bottom, Height = 34, Padding = new Forms.Padding(16, 6, 16, 0), ForeColor = Muted };

    /// <param name="track">Adds a program as a game (path, name); <paramref name="ignore"/> marks one as never a game. Both save.</param>
    /// <param name="themes">The installed themes (id, name) for "Theme while gaming" and each game's own pick.</param>
    public GamingForm(GamingSettings settings, Func<IReadOnlyList<GameEntry>> catalog, Func<IReadOnlyList<string>> suggested,
        Func<string?> save, Action<string, string> track, Action<string> ignore, IReadOnlyList<(string Id, string Name)>? themes = null)
    {
        _themes = (themes ?? []).Where(t => t.Id.Length > 0).ToList(); // Classic isn't offered: "don't switch" leaves the user's theme
        _settings = settings;
        _catalog = catalog;
        _suggested = suggested;
        _save = save;
        _track = track;
        _ignore = ignore;
        Text = "Pulsewave · Gaming";
        StartPosition = Forms.FormStartPosition.CenterScreen;
        Size = new Size(1000, 640);
        MinimumSize = new Size(860, 520);
        BackColor = Color.FromArgb(19, 27, 37);
        ForeColor = Color.FromArgb(232, 242, 249);
        Font = new Font("Segoe UI", 9.5f);
        AutoScaleMode = Forms.AutoScaleMode.Dpi;

        var title = new Forms.Label
        {
            Dock = Forms.DockStyle.Top, Height = 66, Padding = new Forms.Padding(18, 12, 18, 0), ForeColor = Muted,
            Text = "GAMING\nPulsewave times sessions of the games it recognises: Steam, GOG and Xbox games, the games Windows itself has recognised, " +
                   "and games you add here. Only program locations are read; nothing is captured."
        };

        var right = new Forms.TableLayoutPanel { Dock = Forms.DockStyle.Right, Width = 340, ColumnCount = 1, RowCount = 6, Padding = new Forms.Padding(8, 0, 14, 0) };
        right.RowStyles.Add(new Forms.RowStyle(Forms.SizeType.AutoSize));
        right.RowStyles.Add(new Forms.RowStyle(Forms.SizeType.Percent, 34));
        right.RowStyles.Add(new Forms.RowStyle(Forms.SizeType.AutoSize));
        right.RowStyles.Add(new Forms.RowStyle(Forms.SizeType.Percent, 33));
        right.RowStyles.Add(new Forms.RowStyle(Forms.SizeType.AutoSize));
        right.RowStyles.Add(new Forms.RowStyle(Forms.SizeType.Percent, 33));
        right.Controls.Add(Header("Added by you", Button("Add a game…", AddGame), Button("Remove", () =>
        {
            if (_added.SelectedIndex < 0 || _added.SelectedIndex >= _settings.Games.Count) return;
            _settings.Games.RemoveAt(_added.SelectedIndex);
            Saved("Removed.");
        })), 0, 0);
        right.Controls.Add(_added, 0, 1);
        right.Controls.Add(Header("Never a game", Button("Count it again", () =>
        {
            if (_ignored.SelectedIndex < 0 || _ignored.SelectedIndex >= _settings.Ignored.Count) return;
            _settings.Ignored.RemoveAt(_ignored.SelectedIndex);
            Saved("It can be recognised again.");
        })), 0, 2);
        right.Controls.Add(_ignored, 0, 3);
        right.Controls.Add(Header("Suggested", Button("Track", () => Pending(true)), Button("Never", () => Pending(false))), 0, 4);
        right.Controls.Add(_pending, 0, 5);

        var left = new Forms.Panel { Dock = Forms.DockStyle.Fill, Padding = new Forms.Padding(16, 0, 8, 6) };
        var ignoreGame = Button("Never count this one", () =>
        {
            if (_games.SelectedItems.Count == 0 || _games.SelectedItems[0].Tag is not GameEntry g || g.Exe == null) { ShowStatus("Pick a game found by its program (Windows, GOG or added)."); return; }
            _ignore(g.Exe);
            Refill("Won't count " + g.Name + ".");
        });
        var bottom = new Forms.FlowLayoutPanel { Dock = Forms.DockStyle.Bottom, Height = 40, WrapContents = false };
        _suggest.Checked = settings.Suggest;
        _suggest.CheckedChanged += (_, _) => { _settings.Suggest = _suggest.Checked; Saved(_suggest.Checked ? "Suggestions on." : "Suggestions off."); };
        bottom.Controls.AddRange([ignoreGame, _suggest]);

        // Theme while gaming (0.10): one default for every game, and the selected game's own pick.
        var themeRow = new Forms.FlowLayoutPanel { Dock = Forms.DockStyle.Top, Height = 40, WrapContents = false, Padding = new Forms.Padding(0, 4, 0, 0) };
        themeRow.Controls.Add(new Forms.Label { Text = "Theme while gaming", AutoSize = true, ForeColor = Muted, Padding = new Forms.Padding(0, 7, 6, 0) });
        themeRow.Controls.Add(_defaultTheme);
        themeRow.Controls.Add(_gameLabel);
        themeRow.Controls.Add(_gameTheme);
        _defaultTheme.Items.Add(new ThemeChoice("", "Don't switch"));
        foreach (var t in _themes) _defaultTheme.Items.Add(new ThemeChoice(t.Id, t.Name));
        _defaultTheme.SelectedItem = _defaultTheme.Items.Cast<ThemeChoice>().FirstOrDefault(c => c.Id == _settings.Theme) ?? _defaultTheme.Items[0];
        _defaultTheme.SelectedIndexChanged += (_, _) =>
        {
            if (_filling || _defaultTheme.SelectedItem is not ThemeChoice c) return;
            _settings.Theme = c.Id;
            Saved(c.Id.Length == 0 ? "Games no longer switch the theme." : $"Games show {c.Label} (the theme returns when the game ends).");
        };
        _gameTheme.SelectedIndexChanged += (_, _) =>
        {
            if (_filling || _gameTheme.SelectedItem is not ThemeChoice c || SelectedGame() is not { } g) return;
            if (c.Id.Length == 0) _settings.Themes.Remove(g.Key); else _settings.Themes[g.Key] = c.Id;
            Saved($"{g.Name}: {c.Label}.");
        };
        _games.SelectedIndexChanged += (_, _) => ShowGameTheme();

        left.Controls.Add(_games);
        left.Controls.Add(themeRow);
        left.Controls.Add(bottom);

        Controls.Add(left);
        Controls.Add(right);
        Controls.Add(title);
        Controls.Add(_status);
        Refill("Games are looked for again every 10 minutes and when this list changes.");
    }

    /// <summary>Shows the lists again (the game list is rebuilt in the background, so it can lag a moment behind).</summary>
    public void Refill(string? status = null)
    {
        var games = _catalog().OrderBy(g => g.Source == "Steam").ThenBy(g => g.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        string? selected = SelectedGame()?.Key;
        _filling = true;
        _games.BeginUpdate();
        _games.Items.Clear();
        foreach (var g in games)
        {
            var item = new Forms.ListViewItem([g.Name, g.Source, ThemeLabel(g.Key), g.Exe != null ? Path.GetFileName(g.Exe) + (g.Folder.Length > 0 ? " · folder" : "") : g.Folder]) { Tag = g };
            _games.Items.Add(item);
            if (g.Key == selected) item.Selected = true;
        }
        _games.EndUpdate();
        _filling = false;
        ShowGameTheme();
        _added.Items.Clear();
        foreach (var g in _settings.Games) _added.Items.Add($"{g.Name}  ·  {Path.GetFileName(g.Exe)}");
        _ignored.Items.Clear();
        foreach (var path in _settings.Ignored) _ignored.Items.Add(Path.GetFileName(path) + "  ·  " + Path.GetDirectoryName(path));
        _pending.Items.Clear();
        foreach (var path in _suggested()) _pending.Items.Add(GameCatalog.NameOf(path) + "  ·  " + Path.GetFileName(path));
        ShowStatus(status ?? $"{games.Count} games recognised.");
    }

    public void ShowStatus(string text) => _status.Text = text;

    private GameEntry? SelectedGame() => _games.SelectedItems.Count > 0 ? _games.SelectedItems[0].Tag as GameEntry : null;

    /// <summary>What a game shows while played, for its Theme column: its own pick, "default (…)" or "don't switch".</summary>
    internal string ThemeLabel(string key) => _settings.Themes.GetValueOrDefault(key) switch
    {
        GamingSettings.NoTheme => "Don't switch",
        { Length: > 0 } own => NameOf(own),
        _ => _settings.Theme.Length > 0 ? "Default · " + NameOf(_settings.Theme) : "—"
    };

    private string NameOf(string id) => _themes.FirstOrDefault(t => t.Id == id).Name ?? "(theme removed)";

    /// <summary>The "This game" choice follows the selected game.</summary>
    private void ShowGameTheme()
    {
        _filling = true;
        _gameTheme.Items.Clear();
        if (SelectedGame() is { } g)
        {
            _gameTheme.Items.Add(new ThemeChoice("", _settings.Theme.Length > 0 ? "Default (" + NameOf(_settings.Theme) + ")" : "Default (don't switch)"));
            _gameTheme.Items.Add(new ThemeChoice(GamingSettings.NoTheme, "Don't switch"));
            foreach (var t in _themes) _gameTheme.Items.Add(new ThemeChoice(t.Id, t.Name));
            string own = _settings.Themes.GetValueOrDefault(g.Key) ?? "";
            _gameTheme.SelectedItem = _gameTheme.Items.Cast<ThemeChoice>().FirstOrDefault(c => c.Id == own) ?? _gameTheme.Items[0];
        }
        // No empty box while nothing is selected: a hint instead.
        _gameTheme.Visible = SelectedGame() != null;
        _gameLabel.Text = SelectedGame() != null ? "This game" : "Pick a game for its own theme";
        _filling = false;
    }

    private static Forms.ComboBox Choice() => new()
    {
        DropDownStyle = Forms.ComboBoxStyle.DropDownList, Width = 190, FlatStyle = Forms.FlatStyle.Flat,
        BackColor = Color.FromArgb(40, 53, 68), ForeColor = Color.White, Margin = new Forms.Padding(0, 3, 0, 0)
    };

    private void AddGame()
    {
        using var dialog = new Forms.OpenFileDialog { Title = "Pick the game's program", Filter = "Programs (*.exe)|*.exe" };
        if (dialog.ShowDialog(this) != Forms.DialogResult.OK) return;
        _track(dialog.FileName, GameCatalog.NameOf(dialog.FileName));
        Refill("Added " + GameCatalog.NameOf(dialog.FileName) + ".");
    }

    private void Pending(bool track)
    {
        var list = _suggested();
        if (_pending.SelectedIndex < 0 || _pending.SelectedIndex >= list.Count) return;
        string path = list[_pending.SelectedIndex];
        if (track) _track(path, GameCatalog.NameOf(path)); else _ignore(path);
        Refill(track ? "Tracking " + GameCatalog.NameOf(path) + "." : "Won't suggest it again.");
    }

    private void Saved(string message) => Refill(_save() ?? message);

    private static Forms.ListView List(params (string Name, int Width)[] columns)
    {
        var view = new Forms.ListView { Dock = Forms.DockStyle.Fill, View = Forms.View.Details, FullRowSelect = true, HideSelection = false, MultiSelect = false,
            BackColor = Panel, ForeColor = Color.FromArgb(232, 242, 249), BorderStyle = Forms.BorderStyle.None, HeaderStyle = Forms.ColumnHeaderStyle.Nonclickable };
        foreach (var (name, width) in columns) view.Columns.Add(name, width);
        return view;
    }

    private static Forms.ListBox Box() =>
        new() { Dock = Forms.DockStyle.Fill, BorderStyle = Forms.BorderStyle.None, IntegralHeight = false, BackColor = Panel, ForeColor = Color.FromArgb(232, 242, 249) };

    private static Forms.FlowLayoutPanel Header(string text, params Forms.Control[] buttons)
    {
        var row = new Forms.FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Forms.Padding(0, 8, 0, 4) };
        row.Controls.Add(new Forms.Label { Text = text, AutoSize = true, ForeColor = Muted, Padding = new Forms.Padding(0, 6, 8, 0) });
        row.Controls.AddRange(buttons);
        return row;
    }

    private Forms.Button Button(string text, Action click)
    {
        var b = new Forms.Button { Text = text, AutoSize = true, Height = 28, FlatStyle = Forms.FlatStyle.Flat, BackColor = Color.FromArgb(44, 62, 83),
            ForeColor = ForeColor, Margin = new Forms.Padding(4, 2, 4, 2) };
        b.FlatAppearance.BorderSize = 0;
        b.Click += (_, _) => click();
        return b;
    }
}
