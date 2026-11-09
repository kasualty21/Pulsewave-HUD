using System.Drawing;
using System.IO;
using System.Windows.Media.Imaging;
using Forms = System.Windows.Forms;

namespace Pulsewave;

/// <summary>
/// Shortcuts the HUD can open: programs, games (.exe, Start menu and Steam/Epic shortcuts),
/// folders and links. Their order is the slot number themes refer to. The Gaming and Productivity
/// lists pick and order some of them for the themes that follow those lists (Halo Gaming, Halo Productivity).
/// </summary>
public sealed class ShortcutsForm : Forms.Form
{
    /// <summary>The Halo desktop look (DesktopStyle), applied once everything in the window exists.</summary>
    protected override void OnLoad(EventArgs e) { DesktopStyle.Apply(this); base.OnLoad(e); }

    private readonly List<AppShortcut> _items;
    private readonly Dictionary<string, List<int>> _lists = new(StringComparer.OrdinalIgnoreCase);
    private readonly Func<List<AppShortcut>, Dictionary<string, List<int>>, string?> _save;
    private readonly Forms.ComboBox _listPicker = new();
    private readonly Forms.Label _listNote = new();
    private readonly List<Forms.Control> _mainOnly = [];
    // The list being edited (null: all shortcuts) and which shortcut (0-based) each row shows.
    private string? _shownList;
    private List<int> _rows = [];
    private readonly Forms.ListView _list = new();
    private readonly Forms.ImageList _icons = new() { ImageSize = new Size(32, 32), ColorDepth = Forms.ColorDepth.Depth32Bit };
    private readonly Forms.TextBox _name = new(), _target = new(), _arguments = new(), _icon = new();
    private readonly Forms.Label _status = new();
    private readonly Forms.Panel _editor = new();
    private bool _loading;

    /// <param name="lists">The Gaming and Productivity lists (1-based slots).</param>
    /// <param name="save">Saves the shortcuts and lists; returns why it could not, or null.</param>
    /// <param name="list">The list to show first ("Gaming", "Productivity"); null shows all shortcuts.</param>
    public ShortcutsForm(IEnumerable<AppShortcut> shortcuts, IReadOnlyDictionary<string, List<int>> lists,
        Func<List<AppShortcut>, Dictionary<string, List<int>>, string?> save, string? list = null)
    {
        _items = shortcuts.Select(s => s.Copy()).ToList();
        CopyLists(lists);
        _save = save;
        Text = "Pulsewave · Shortcuts";
        StartPosition = Forms.FormStartPosition.CenterScreen;
        Size = new Size(980, 640);
        MinimumSize = new Size(820, 520);
        BackColor = Color.FromArgb(19, 27, 37);
        ForeColor = Color.FromArgb(232, 242, 249);
        Font = new Font("Segoe UI", 9.5f);
        AutoScaleMode = Forms.AutoScaleMode.Dpi;

        var title = new Forms.Label
        {
            Dock = Forms.DockStyle.Top, Height = 74, Padding = new Forms.Padding(18, 14, 18, 0),
            Text = "SHORTCUTS\nPrograms, games and links to open from the HUD (click-through off). The number is the slot a theme's " +
                   "shortcut tiles show. Drag programs, shortcuts or links here, or onto a tile on the HUD.",
            ForeColor = Color.FromArgb(176, 196, 209)
        };

        _list.Dock = Forms.DockStyle.Fill;
        _list.View = Forms.View.Details;
        _list.FullRowSelect = true;
        _list.MultiSelect = false;
        _list.HideSelection = false;
        _list.SmallImageList = _icons;
        _list.HeaderStyle = Forms.ColumnHeaderStyle.Nonclickable;
        _list.Columns.Add("Slot", 56);
        _list.Columns.Add("Name", 200);
        _list.Columns.Add("Opens", 330);
        _list.BackColor = Color.FromArgb(23, 32, 43);
        _list.ForeColor = ForeColor;
        _list.BorderStyle = Forms.BorderStyle.None;
        _list.SelectedIndexChanged += (_, _) => ShowSelected();
        _list.ItemChecked += (_, e) => { if (!_loading) Pick(e.Item.Index, e.Item.Checked); };
        // Drop .exe/.lnk/.url files, folders or a web/steam:// address: onto a row to put it in that slot, elsewhere to add it.
        AllowDrop = _list.AllowDrop = true;
        _list.DragEnter += (_, e) => e.Effect = Dropped(e.Data).Count > 0 ? Forms.DragDropEffects.Copy : Forms.DragDropEffects.None;
        _list.DragDrop += (_, e) =>
        {
            var point = _list.PointToClient(new Point(e.X, e.Y));
            int row = _list.GetItemAt(point.X, point.Y)?.Index ?? -1;
            AddDropped(Dropped(e.Data), row >= 0 ? row + 1 : 0);
        };
        DragEnter += (_, e) => e.Effect = Dropped(e.Data).Count > 0 ? Forms.DragDropEffects.Copy : Forms.DragDropEffects.None;
        DragDrop += (_, e) => AddDropped(Dropped(e.Data), 0);

        // Which list: all shortcuts (slot order, used by Halo Lifestyle and most themes) or a theme list.
        var listBar = new Forms.FlowLayoutPanel
        {
            Dock = Forms.DockStyle.Top, Height = 44, Padding = new Forms.Padding(14, 6, 14, 0),
            FlowDirection = Forms.FlowDirection.LeftToRight, WrapContents = false
        };
        listBar.Controls.Add(new Forms.Label { Text = "List", AutoSize = true, ForeColor = Color.FromArgb(176, 196, 209), Margin = new Forms.Padding(0, 8, 8, 0) });
        _listPicker.DropDownStyle = Forms.ComboBoxStyle.DropDownList;
        _listPicker.Width = 330;
        _listPicker.BackColor = Color.FromArgb(14, 21, 30);
        _listPicker.ForeColor = ForeColor;
        _listPicker.FlatStyle = Forms.FlatStyle.Flat;
        _listPicker.Items.AddRange(["All shortcuts (Halo Lifestyle and most themes)", "Gaming (Halo Gaming)", "Productivity (Halo Productivity)"]);
        _listPicker.SelectedIndex = 0;
        _listPicker.SelectedIndexChanged += (_, _) => ShowList(_listPicker.SelectedIndex switch { 1 => ShortcutLists.Gaming, 2 => ShortcutLists.Productivity, _ => null });
        _listNote.AutoSize = true;
        _listNote.Text = MainNote;
        _listNote.ForeColor = Color.FromArgb(150, 170, 185);
        _listNote.Margin = new Forms.Padding(12, 8, 0, 0);
        listBar.Controls.AddRange([_listPicker, _listNote]);

        _editor.Dock = Forms.DockStyle.Right;
        _editor.Width = 330;
        _editor.Padding = new Forms.Padding(14, 8, 14, 8);
        _editor.BackColor = Color.FromArgb(27, 38, 50);
        var fields = new Forms.FlowLayoutPanel { Dock = Forms.DockStyle.Fill, FlowDirection = Forms.FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
        void Field(string label, Forms.TextBox box, Action<string> commit, string? browse = null, Action? onBrowse = null)
        {
            fields.Controls.Add(new Forms.Label { Text = label, AutoSize = true, ForeColor = Color.FromArgb(176, 196, 209), Margin = new Forms.Padding(0, 8, 0, 2) });
            box.Width = 290;
            box.BackColor = Color.FromArgb(14, 21, 30);
            box.ForeColor = ForeColor;
            box.BorderStyle = Forms.BorderStyle.FixedSingle;
            box.TextChanged += (_, _) => { if (!_loading && Selected is { } s) { commit(box.Text); RefreshRow(_list.SelectedIndices[0]); } };
            fields.Controls.Add(box);
            if (browse != null)
            {
                var button = Button(browse, Color.FromArgb(44, 62, 83));
                button.Click += (_, _) => onBrowse?.Invoke();
                fields.Controls.Add(button);
            }
        }
        Field("Name", _name, v => Selected!.Name = v);
        Field("Opens (program, shortcut, folder, web address or shell: path), keys to send (e.g. Ctrl+Shift+M), or Lock / Sleep",
            _target, v => Selected!.Target = v, "Browse…", () =>
        {
            if (Pick("Choose a program or shortcut", null) is { } file) _target.Text = file;
        });
        Field("Arguments (optional)", _arguments, v => Selected!.Arguments = v);
        Field("Icon from (optional .ico, .png or .exe)", _icon, v => Selected!.IconFile = v.Trim().Length == 0 ? null : v.Trim(), "Choose icon…", () =>
        {
            using var dialog = new Forms.OpenFileDialog { Title = "Choose an icon", Filter = "Icons and pictures (*.ico;*.png;*.jpg;*.exe;*.dll)|*.ico;*.png;*.jpg;*.jpeg;*.exe;*.dll" };
            if (dialog.ShowDialog(this) == Forms.DialogResult.OK) _icon.Text = dialog.FileName;
        });
        fields.Controls.Add(new Forms.Label
        {
            Text = "Steam and Epic games: add the game's desktop or Start menu shortcut. Web addresses such as https://… or " +
                   "steam://… open in their app.",
            AutoSize = false, Width = 290, Height = 80, ForeColor = Color.FromArgb(150, 170, 185), Margin = new Forms.Padding(0, 12, 0, 0)
        });
        _editor.Controls.Add(fields);

        var footer = new Forms.FlowLayoutPanel
        {
            Dock = Forms.DockStyle.Bottom, Height = 60, Padding = new Forms.Padding(12, 10, 12, 8),
            FlowDirection = Forms.FlowDirection.RightToLeft, WrapContents = false
        };
        var saveButton = Button("Save", Color.FromArgb(40, 128, 117));
        var close = Button("Close", Color.FromArgb(44, 62, 83));
        var remove = Button("Remove", Color.FromArgb(110, 52, 52));
        var clear = Button("Clear slot", Color.FromArgb(44, 62, 83));
        clear.Click += (_, _) => { if (Index is int i) { _items[i] = new AppShortcut(); ShortcutLists.Replaced(_lists, i + 1); Reload(i); } };
        var down = Button("Move down", Color.FromArgb(44, 62, 83));
        var up = Button("Move up", Color.FromArgb(44, 62, 83));
        saveButton.Click += (_, _) => Save();
        close.Click += (_, _) => Close();
        remove.Click += (_, _) => { if (Index is int i) { _items.RemoveAt(i); ShortcutLists.Removed(_lists, i + 1); Reload(Math.Min(i, _items.Count - 1)); } };
        up.Click += (_, _) => MoveSelected(-1);
        down.Click += (_, _) => MoveSelected(+1);
        _status.AutoSize = true;
        _status.ForeColor = Color.FromArgb(176, 196, 209);
        _status.Margin = new Forms.Padding(12, 10, 0, 0);
        footer.Controls.AddRange([saveButton, close, remove, clear, down, up, _status]);
        _mainOnly.AddRange([remove, clear]);

        var add = new Forms.FlowLayoutPanel
        {
            Dock = Forms.DockStyle.Bottom, Height = 50, Padding = new Forms.Padding(12, 8, 12, 0),
            FlowDirection = Forms.FlowDirection.LeftToRight, WrapContents = false
        };
        var addProgram = Button("Add program…", Color.FromArgb(44, 62, 83));
        var addStart = Button("From Start menu…", Color.FromArgb(44, 62, 83));
        var addLink = Button("Add web link", Color.FromArgb(44, 62, 83));
        var addSteam = Button("Steam games…", Color.FromArgb(44, 62, 83));
        var addAction = Button("Add action ▾", Color.FromArgb(44, 62, 83));
        addProgram.Width = addStart.Width = 150;
        addSteam.Width = 130;
        addProgram.Click += (_, _) => AddFile(null);
        addStart.Click += (_, _) => AddFile(Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms));
        addLink.Click += (_, _) => AddItem(new AppShortcut { Name = "Website", Target = "https://" });
        addSteam.Click += (_, _) => AddSteamGames();
        // Macro-style actions: keys sent to the app in front (Discord mute, OBS scenes), or lock / sleep the PC.
        var actions = new Forms.ContextMenuStrip();
        actions.Items.Add("Keyboard shortcut (edit the keys)", null, (_, _) => AddItem(new AppShortcut { Name = "Mute mic", Kind = AppShortcut.KeysKind, Target = "Ctrl+Shift+M" }));
        actions.Items.Add("Lock PC", null, (_, _) => AddItem(new AppShortcut { Name = "Lock", Kind = AppShortcut.SystemKind, Target = "Lock" }));
        actions.Items.Add("Sleep PC", null, (_, _) => AddItem(new AppShortcut { Name = "Sleep", Kind = AppShortcut.SystemKind, Target = "Sleep" }));
        addAction.Click += (_, _) => actions.Show(addAction, new Point(0, addAction.Height));
        add.Controls.AddRange([addProgram, addStart, addLink, addSteam, addAction]);
        _mainOnly.Add(add);

        Controls.Add(_list);
        Controls.Add(_editor);
        Controls.Add(add);
        Controls.Add(footer);
        Controls.Add(listBar);
        Controls.Add(title);
        FormClosed += (_, _) => { foreach (var image in _icons.Images.Cast<Image>().ToList()) image.Dispose(); _icons.Dispose(); };
        Reload(_items.Count > 0 ? 0 : -1);
        if (ShortcutLists.Canonical(list) is { } shown) _listPicker.SelectedIndex = Array.IndexOf(ShortcutLists.Names, shown) + 1;
    }

    /// <summary>Shows shortcuts saved elsewhere (dropped onto the HUD) while this window is open.</summary>
    public void ShowSaved(IEnumerable<AppShortcut> shortcuts, IReadOnlyDictionary<string, List<int>> lists)
    {
        _items.Clear();
        _items.AddRange(shortcuts.Select(s => s.Copy()));
        CopyLists(lists);
        Reload(Index ?? (_items.Count > 0 ? 0 : -1));
    }

    private void CopyLists(IReadOnlyDictionary<string, List<int>> lists)
    {
        _lists.Clear();
        foreach (var name in ShortcutLists.Names)
            _lists[name] = lists.TryGetValue(name, out var picks) ? picks.ToList() : [];
    }

    /// <summary>The selected shortcut's index in the full list (0-based), whichever list is shown.</summary>
    private int? Index => _list.SelectedIndices.Count == 1 && _list.SelectedIndices[0] < _rows.Count ? _rows[_list.SelectedIndices[0]] : null;
    private AppShortcut? Selected => Index is int i ? _items[i] : null;

    /// <summary>The picks of the list being edited (1-based slots), without ones that point at nothing.</summary>
    private List<int> Picks => _shownList == null ? [] : _lists[_shownList] = ShortcutLists.Valid(_items, _lists[_shownList]);

    /// <summary>Shows all shortcuts (null) or a list's picks in order, ticked, then the rest unticked.</summary>
    private void ShowList(string? list)
    {
        _shownList = list;
        SetCheckBoxes(list != null);
        foreach (var control in _mainOnly) control.Enabled = list == null;
        _listNote.Text = list == null ? MainNote
            : $"Tick shortcuts for {list}; Move up / down sets the tile order (tile 1 first).";
        _status.Text = "";
        Reload(Index ?? (_items.Count > 0 ? 0 : -1));
    }

    private const string MainNote = "Halo Gaming and Halo Productivity can show their own picks instead (pick the list).";

    private void SetCheckBoxes(bool on)
    {
        _loading = true;
        _list.CheckBoxes = on;
        _list.Columns[0].Text = on ? "Tile" : "Slot";
        _list.Columns[0].Width = on ? 104 : 56; // a tick box and the icon before the number
        _loading = false;
    }

    /// <summary>Ticking adds a shortcut to the end of the list being edited; unticking takes it out.</summary>
    private void Pick(int row, bool picked)
    {
        if (_shownList == null || row < 0 || row >= _rows.Count) return;
        var picks = Picks;
        int slot = _rows[row] + 1;
        picks.Remove(slot);
        if (picked) picks.Add(slot);
        _lists[_shownList] = picks;
        BeginInvoke(() => Reload(_rows[row]));
    }

    private static Forms.Button Button(string text, Color color) => new()
    {
        Text = text, Width = 116, Height = 36, Margin = new Forms.Padding(4, 0, 4, 0),
        FlatStyle = Forms.FlatStyle.Flat, BackColor = color, ForeColor = Color.White
    };

    private string? Pick(string title, string? folder)
    {
        // Keep .lnk and .url files as they are: they carry the game's arguments and icon.
        using var dialog = new Forms.OpenFileDialog
        {
            Title = title, DereferenceLinks = false, InitialDirectory = folder ?? "",
            Filter = "Programs and shortcuts (*.exe;*.lnk;*.url;*.bat;*.cmd)|*.exe;*.lnk;*.url;*.bat;*.cmd|All files (*.*)|*.*"
        };
        return dialog.ShowDialog(this) == Forms.DialogResult.OK ? dialog.FileName : null;
    }

    private void AddFile(string? folder)
    {
        if (Pick(folder == null ? "Add a program or game" : "Add from the Start menu", folder) is not { } file) return;
        AddItem(new AppShortcut { Name = Path.GetFileNameWithoutExtension(file), Target = file });
    }

    private void AddItem(AppShortcut shortcut)
    {
        int slot = AppShortcut.Assign(_items, shortcut);
        if (slot == 0) { _status.Text = $"Up to {AppShortcut.MaxCount} shortcuts."; return; }
        Reload(slot - 1);
    }

    /// <summary>Lists installed Steam games with ticks; ticked games not already added fill free slots.</summary>
    private void AddSteamGames()
    {
        var games = SteamLibrary.Installed();
        if (games.Count == 0) { _status.Text = "No installed Steam games found."; return; }
        var added = _items.Select(s => s.Target).ToHashSet(StringComparer.OrdinalIgnoreCase);
        using var dialog = new Forms.Form
        {
            Text = "Add Steam games", StartPosition = Forms.FormStartPosition.CenterParent, Size = new Size(460, 560),
            BackColor = BackColor, ForeColor = ForeColor, Font = Font, MinimizeBox = false, MaximizeBox = false
        };
        // Nothing is ticked at first: a big library would otherwise fill every free slot at once.
        var list = new Forms.CheckedListBox { Dock = Forms.DockStyle.Fill, CheckOnClick = true, BackColor = Color.FromArgb(23, 32, 43), ForeColor = ForeColor,
            BorderStyle = Forms.BorderStyle.None };
        var fresh = games.Where(g => !added.Contains(g.ToShortcut().Target)).ToList();
        var ticked = new HashSet<SteamGame>();
        var shown = new List<SteamGame>();
        int free = FreeSlots(_items);
        var search = new Forms.TextBox { Dock = Forms.DockStyle.Top, PlaceholderText = "Search games", BackColor = Color.FromArgb(23, 32, 43), ForeColor = ForeColor,
            BorderStyle = Forms.BorderStyle.FixedSingle };
        var count = new Forms.Label { AutoSize = false, Width = 200, Height = 36, TextAlign = ContentAlignment.MiddleLeft };
        var buttons = new Forms.FlowLayoutPanel { Dock = Forms.DockStyle.Bottom, Height = 56, Padding = new Forms.Padding(8), FlowDirection = Forms.FlowDirection.RightToLeft };
        var ok = Button("Add", Color.FromArgb(40, 128, 117));
        var cancel = Button("Cancel", Color.FromArgb(44, 62, 83));
        ok.DialogResult = Forms.DialogResult.OK;
        cancel.DialogResult = Forms.DialogResult.Cancel;
        buttons.Controls.AddRange([ok, cancel, count]);
        void Count()
        {
            count.Text = SteamPickCount(ticked.Count, free);
            count.ForeColor = ticked.Count > free ? Color.FromArgb(255, 140, 160) : ForeColor;
            ok.Enabled = ticked.Count > 0 && ticked.Count <= free;
        }
        void Fill()
        {
            list.BeginUpdate();
            list.Items.Clear();
            shown.Clear();
            foreach (var game in fresh.Where(g => g.Name.Contains(search.Text.Trim(), StringComparison.CurrentCultureIgnoreCase)))
            {
                shown.Add(game);
                list.Items.Add(game.Name, ticked.Contains(game));
            }
            list.EndUpdate();
        }
        list.ItemCheck += (_, e) =>
        {
            if (e.Index < 0 || e.Index >= shown.Count) return;
            if (e.NewValue == Forms.CheckState.Checked) ticked.Add(shown[e.Index]); else ticked.Remove(shown[e.Index]);
            Count();
        };
        search.TextChanged += (_, _) => Fill();
        Fill();
        Count();
        dialog.Controls.Add(list);
        dialog.Controls.Add(search);
        dialog.Controls.Add(buttons);
        dialog.Controls.Add(new Forms.Label { Dock = Forms.DockStyle.Top, Height = 58, Padding = new Forms.Padding(8, 8, 8, 0),
            Text = fresh.Count == 0 ? "Every installed game is already a shortcut."
                : $"Tick the games to add. Pulsewave holds up to {AppShortcut.MaxCount} shortcuts; {free} {(free == 1 ? "slot is" : "slots are")} free." });
        dialog.AcceptButton = ok;
        if (dialog.ShowDialog(this) != Forms.DialogResult.OK) return;
        AddDropped(fresh.Where(ticked.Contains).Select(g => g.ToShortcut()).ToList(), 0);
    }

    /// <summary>Free shortcut slots: empty ones between shortcuts plus those after the last.</summary>
    internal static int FreeSlots(IReadOnlyList<AppShortcut> items) =>
        AppShortcut.MaxCount - items.Take(AppShortcut.MaxCount).Count(s => !s.IsEmpty);

    /// <summary>The Steam picker's count line ("3 ticked · 21 free", or how many to untick).</summary>
    internal static string SteamPickCount(int ticked, int free) =>
        ticked > free ? $"{ticked} ticked · only {free} free" : $"{ticked} ticked · {free - ticked} left";

    /// <summary>Files, folders or addresses being dragged in, as shortcuts.</summary>
    internal static List<AppShortcut> Dropped(Forms.IDataObject? data)
    {
        var items = data?.GetData(Forms.DataFormats.FileDrop) as string[] ??
                    (data?.GetData(Forms.DataFormats.UnicodeText) as string)?.Split('\n', StringSplitOptions.RemoveEmptyEntries) ?? [];
        return items.Select(AppShortcut.FromDrop).OfType<AppShortcut>().ToList();
    }

    /// <summary>The first dropped item goes in <paramref name="slot"/> (0 = first free); the rest fill free slots after it.</summary>
    private void AddDropped(List<AppShortcut> dropped, int slot)
    {
        if (_shownList != null) { _listPicker.SelectedIndex = 0; slot = 0; } // drops go into all shortcuts
        int last = -1;
        foreach (var shortcut in dropped)
        {
            int used = AppShortcut.Assign(_items, shortcut, slot);
            if (used == 0) { _status.Text = $"Up to {AppShortcut.MaxCount} shortcuts."; break; }
            ShortcutLists.Replaced(_lists, used);
            last = used - 1;
            slot = 0;
        }
        if (last >= 0) { Reload(last); _status.Text = "Added · Save to keep"; }
    }

    private void MoveSelected(int step)
    {
        if (Index is not int i) return;
        if (_shownList != null)
        {
            // In a list, moving changes the tile order of its picks only.
            var picks = Picks;
            int at = picks.IndexOf(i + 1);
            if (at < 0 || at + step < 0 || at + step >= picks.Count) return;
            (picks[at], picks[at + step]) = (picks[at + step], picks[at]);
            _lists[_shownList] = picks;
            Reload(i);
            return;
        }
        if (i + step < 0 || i + step >= _items.Count) return;
        (_items[i], _items[i + step]) = (_items[i + step], _items[i]);
        ShortcutLists.Swapped(_lists, i + 1, i + step + 1);
        Reload(i + step);
    }

    /// <summary>Fills the rows (all shortcuts, or a list's picks then the rest) and selects the shortcut at <paramref name="select"/>.</summary>
    private void Reload(int select)
    {
        _loading = true;
        var picks = Picks;
        _rows = _shownList == null ? Enumerable.Range(0, _items.Count).ToList()
            : picks.Select(slot => slot - 1).Concat(Enumerable.Range(0, _items.Count).Where(i => !_items[i].IsEmpty && !picks.Contains(i + 1))).ToList();
        _list.BeginUpdate();
        _list.Items.Clear();
        foreach (var image in _icons.Images.Cast<Image>().ToList()) image.Dispose();
        _icons.Images.Clear();
        for (int row = 0; row < _rows.Count; row++)
        {
            int i = _rows[row];
            int tile = picks.IndexOf(i + 1) + 1;
            _icons.Images.Add(IconFor(_items[i]));
            string number = _shownList == null ? (i + 1).ToString() : tile > 0 ? tile.ToString() : "";
            _list.Items.Add(new Forms.ListViewItem([number, _items[i].IsEmpty ? "(empty slot)" : _items[i].DisplayName, _items[i].Target], row)
                { ForeColor = _items[i].IsEmpty || _shownList != null && tile == 0 ? Color.FromArgb(130, 150, 165) : _list.ForeColor, Checked = tile > 0 });
        }
        _list.EndUpdate();
        int selectRow = _rows.IndexOf(select);
        if (selectRow >= 0) { _list.Items[selectRow].Selected = true; _list.Items[selectRow].EnsureVisible(); }
        _loading = false;
        ShowSelected();
        if (_shownList != null) _status.Text = picks.Count == 0 ? $"Nothing ticked: {_shownList} themes show all shortcuts in slot order." : $"{picks.Count} on {_shownList} · Save to keep";
    }

    private void RefreshRow(int row)
    {
        int i = _rows[row];
        _list.Items[row].SubItems[1].Text = _items[i].IsEmpty ? "(empty slot)" : _items[i].DisplayName;
        _list.Items[row].SubItems[2].Text = _items[i].Target;
    }

    private void ShowSelected()
    {
        _loading = true;
        var s = Selected;
        _editor.Enabled = s != null;
        _name.Text = s?.Name ?? "";
        _target.Text = s?.Target ?? "";
        _arguments.Text = s?.Arguments ?? "";
        _icon.Text = s?.IconFile ?? "";
        _loading = false;
    }

    private void Save()
    {
        // An unfinished web link counts as empty; empty slots between shortcuts keep later slot numbers.
        for (int i = 0; i < _items.Count; i++)
            if (_items[i].Target.Trim() == "https://") _items[i] = new AppShortcut();
        AppShortcut.TrimEnd(_items);
        if (_items.FirstOrDefault(s => s.Kind == AppShortcut.KeysKind && !KeySender.TryParse(s.Target, out _)) is { } bad)
        {
            _status.Text = $"'{bad.Target}' is not a key combination (try Ctrl+Shift+M, F9 or MediaPlayPause).";
            return;
        }
        string? error = _save(_items.Select(s => new AppShortcut { Name = s.Name.Trim(), Kind = s.Kind, Target = s.Target.Trim(), Arguments = s.Arguments.Trim(), IconFile = s.IconFile }).ToList(),
            _lists.ToDictionary(p => p.Key, p => ShortcutLists.Valid(_items, p.Value), StringComparer.OrdinalIgnoreCase));
        Reload(Index ?? (_items.Count > 0 ? 0 : -1));
        int count = _items.Count(s => !s.IsEmpty);
        _status.Text = error ?? $"Saved · {count} shortcut{(count == 1 ? "" : "s")}";
    }

    private static Image IconFor(AppShortcut shortcut)
    {
        var bitmap = new Bitmap(32, 32);
        if (ShortcutIcons.Load(shortcut, 32) is not { } source) return bitmap;
        // WPF bitmap to GDI+ through PNG, which keeps transparency.
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(source));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        stream.Position = 0;
        using var icon = Image.FromStream(stream);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            g.DrawImage(icon, 0, 0, 32, 32);
        }
        return bitmap;
    }
}
