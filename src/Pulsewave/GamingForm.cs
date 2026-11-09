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
    private readonly Forms.ListView _games = List(("Game", 330), ("Found in", 110), ("Matched by", 300));
    private readonly Forms.ListBox _added = Box(), _ignored = Box(), _pending = Box();
    private readonly Forms.CheckBox _suggest = new() { Text = "Suggest programs that look like games (full-screen with the GPU busy for 2 minutes)", AutoSize = true, ForeColor = Color.White };
    private readonly Forms.Label _status = new() { Dock = Forms.DockStyle.Bottom, Height = 34, Padding = new Forms.Padding(16, 6, 16, 0), ForeColor = Muted };

    /// <param name="track">Adds a program as a game (path, name); <paramref name="ignore"/> marks one as never a game. Both save.</param>
    public GamingForm(GamingSettings settings, Func<IReadOnlyList<GameEntry>> catalog, Func<IReadOnlyList<string>> suggested,
        Func<string?> save, Action<string, string> track, Action<string> ignore)
    {
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
        left.Controls.Add(_games);
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
        _games.BeginUpdate();
        _games.Items.Clear();
        foreach (var g in games)
            _games.Items.Add(new Forms.ListViewItem([g.Name, g.Source, g.Exe != null ? Path.GetFileName(g.Exe) + (g.Folder.Length > 0 ? " · folder" : "") : g.Folder]) { Tag = g });
        _games.EndUpdate();
        _added.Items.Clear();
        foreach (var g in _settings.Games) _added.Items.Add($"{g.Name}  ·  {Path.GetFileName(g.Exe)}");
        _ignored.Items.Clear();
        foreach (var path in _settings.Ignored) _ignored.Items.Add(Path.GetFileName(path) + "  ·  " + Path.GetDirectoryName(path));
        _pending.Items.Clear();
        foreach (var path in _suggested()) _pending.Items.Add(GameCatalog.NameOf(path) + "  ·  " + Path.GetFileName(path));
        ShowStatus(status ?? $"{games.Count} games recognised.");
    }

    public void ShowStatus(string text) => _status.Text = text;

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
