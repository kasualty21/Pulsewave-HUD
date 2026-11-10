using System.Drawing;
using Forms = System.Windows.Forms;

namespace Pulsewave;

/// <summary>
/// Tray → Hotkeys: key combinations that work from any app, each doing one of the actions the tray icon's
/// double-click offers (show or hide the HUD, step themes, open a page or window, show a theme, open a shortcut).
/// </summary>
public sealed class HotkeysForm : Forms.Form
{
    protected override void OnLoad(EventArgs e) { DesktopStyle.Apply(this); base.OnLoad(e); }

    private readonly List<HotkeyBinding> _items;
    private readonly List<(string Id, string Label)> _actions;
    private readonly Func<List<HotkeyBinding>, IReadOnlyList<string>> _save;
    private readonly Forms.ListView _list = new();
    private readonly Forms.TextBox _keys = new();
    private readonly Forms.ComboBox _action = new();
    private readonly Forms.Label _status = new();
    private readonly Forms.Panel _editor = new();
    private bool _loading;

    /// <param name="themes">Installed themes (id, name), for "Show a theme".</param>
    /// <param name="shortcuts">The user's shortcuts by slot, for "Open a shortcut".</param>
    /// <param name="save">Saves and registers the hotkeys; returns what could not be (another app owns the keys), or nothing.</param>
    public HotkeysForm(IEnumerable<HotkeyBinding> bindings, IReadOnlyList<(string Id, string Name)> themes, IReadOnlyList<AppShortcut> shortcuts,
        Func<List<HotkeyBinding>, IReadOnlyList<string>> save)
    {
        _items = bindings.Select(b => new HotkeyBinding { Keys = b.Keys, Action = b.Action }).ToList();
        _actions = Actions(themes, shortcuts);
        foreach (var b in _items.Where(b => _actions.All(a => a.Id != b.Action)))
            _actions.Add((b.Action, Describe(b.Action) + " (no longer there)"));
        _save = save;
        Text = "Pulsewave · Hotkeys";
        StartPosition = Forms.FormStartPosition.CenterScreen;
        Size = new Size(900, 560);
        MinimumSize = new Size(760, 440);
        BackColor = Color.FromArgb(19, 27, 37);
        ForeColor = Color.FromArgb(232, 242, 249);
        Font = new Font("Segoe UI", 9.5f);
        AutoScaleMode = Forms.AutoScaleMode.Dpi;

        var title = new Forms.Label
        {
            Dock = Forms.DockStyle.Top, Height = 74, Padding = new Forms.Padding(18, 14, 18, 0), ForeColor = Color.FromArgb(176, 196, 209),
            Text = "HOTKEYS\nKey combinations that work from any app. Each one does what you pick: anything the tray icon's double-click can do, " +
                   "show a theme, or open one of your shortcuts."
        };

        _list.Dock = Forms.DockStyle.Fill;
        _list.View = Forms.View.Details;
        _list.FullRowSelect = true;
        _list.MultiSelect = false;
        _list.HideSelection = false;
        _list.HeaderStyle = Forms.ColumnHeaderStyle.Nonclickable;
        _list.Columns.Add("Keys", 200);
        _list.Columns.Add("Does", 330);
        _list.BackColor = Color.FromArgb(23, 32, 43);
        _list.ForeColor = ForeColor;
        _list.BorderStyle = Forms.BorderStyle.None;
        _list.SelectedIndexChanged += (_, _) => ShowSelected();

        _editor.Dock = Forms.DockStyle.Right;
        _editor.Width = 330;
        _editor.Padding = new Forms.Padding(14, 8, 14, 8);
        _editor.BackColor = Color.FromArgb(27, 38, 50);
        var fields = new Forms.FlowLayoutPanel { Dock = Forms.DockStyle.Fill, FlowDirection = Forms.FlowDirection.TopDown, WrapContents = false };
        Forms.Label Caption(string text) => new() { Text = text, AutoSize = true, ForeColor = Color.FromArgb(176, 196, 209), Margin = new Forms.Padding(0, 10, 0, 2) };
        fields.Controls.Add(Caption("Keys: click here, then press the combination"));
        _keys.Width = 290;
        _keys.BackColor = Color.FromArgb(14, 21, 30);
        _keys.ForeColor = ForeColor;
        _keys.BorderStyle = Forms.BorderStyle.FixedSingle;
        CaptureKeys(_keys);
        _keys.TextChanged += (_, _) => { if (!_loading && Index is int i) { _items[i].Keys = _keys.Text; RefreshRow(i); } };
        fields.Controls.Add(_keys);
        fields.Controls.Add(Caption("Does"));
        _action.DropDownStyle = Forms.ComboBoxStyle.DropDownList;
        _action.Width = 290;
        _action.MaxDropDownItems = 20;
        _action.BackColor = Color.FromArgb(14, 21, 30);
        _action.ForeColor = ForeColor;
        _action.FlatStyle = Forms.FlatStyle.Flat;
        foreach (var (_, label) in _actions) _action.Items.Add(label);
        _action.SelectedIndexChanged += (_, _) =>
        {
            if (_loading || Index is not int i || _action.SelectedIndex < 0) return;
            _items[i].Action = _actions[_action.SelectedIndex].Id;
            RefreshRow(i);
        };
        fields.Controls.Add(_action);
        fields.Controls.Add(new Forms.Label
        {
            AutoSize = false, Width = 290, Height = 110, ForeColor = Color.FromArgb(150, 170, 185), Margin = new Forms.Padding(0, 14, 0, 0),
            Text = "Use Ctrl, Alt or Win with a key (Shift alone would steal typing); F13–F24 macro keys work alone. Backspace clears the keys. " +
                   "A combination another app already uses can't be taken; Save says which."
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
        var add = Button("Add hotkey", Color.FromArgb(44, 62, 83));
        saveButton.Click += (_, _) => Save();
        close.Click += (_, _) => Close();
        remove.Click += (_, _) => { if (Index is int i) { _items.RemoveAt(i); Reload(Math.Min(i, _items.Count - 1)); } };
        add.Click += (_, _) => Add();
        _status.AutoSize = true;
        _status.MaximumSize = new Size(380, 0);
        _status.ForeColor = Color.FromArgb(176, 196, 209);
        _status.Margin = new Forms.Padding(12, 10, 0, 0);
        footer.Controls.AddRange([saveButton, close, remove, add, _status]);
        AcceptButton = null; // Enter belongs to the keys box

        Controls.Add(_list);
        Controls.Add(_editor);
        Controls.Add(footer);
        Controls.Add(title);
        Reload(_items.Count > 0 ? 0 : -1);
    }

    /// <summary>Every action a hotkey can do: the tray's fixed actions, then each theme to show and each shortcut to open.</summary>
    internal static List<(string Id, string Label)> Actions(IReadOnlyList<(string Id, string Name)> themes, IReadOnlyList<AppShortcut> shortcuts)
    {
        var actions = TrayActions.Fixed.Where(a => a.Id.Length > 0).ToList();
        actions.AddRange(themes.Select(t => ("theme:" + t.Id, "Show theme · " + t.Name)));
        for (int slot = 1; slot <= shortcuts.Count; slot++)
            if (!shortcuts[slot - 1].IsEmpty) actions.Add(("launch:" + slot, "Open · " + shortcuts[slot - 1].DisplayName));
        return actions;
    }

    private string Describe(string action) =>
        _actions?.FirstOrDefault(a => a.Id == action).Label ?? TrayActions.Describe(action, _ => null, _ => null);

    /// <summary>A keys box: pressing a combination writes it ("Ctrl+Alt+M"); Backspace or Delete clears it; others beep.</summary>
    internal static void CaptureKeys(Forms.TextBox box)
    {
        box.ReadOnly = true;
        box.KeyDown += (_, e) =>
        {
            e.SuppressKeyPress = true;
            e.Handled = true;
            if (e.KeyCode is Forms.Keys.Back or Forms.Keys.Delete && e.Modifiers == Forms.Keys.None) { box.Text = ""; return; }
            if (Hotkey.IsModifierKey(e.KeyCode)) return;
            var modifiers = HotkeyModifiers.None;
            if (e.Control) modifiers |= HotkeyModifiers.Ctrl;
            if (e.Alt) modifiers |= HotkeyModifiers.Alt;
            if (e.Shift) modifiers |= HotkeyModifiers.Shift;
            if (System.Windows.Input.Keyboard.IsKeyDown(System.Windows.Input.Key.LWin) || System.Windows.Input.Keyboard.IsKeyDown(System.Windows.Input.Key.RWin))
                modifiers |= HotkeyModifiers.Win;
            var hotkey = new Hotkey(modifiers, e.KeyCode);
            if (hotkey.IsAllowed) box.Text = hotkey.ToString();
            else System.Media.SystemSounds.Beep.Play();
        };
        // Alt+key would otherwise move focus to a menu mnemonic before KeyDown sees it.
        box.PreviewKeyDown += (_, e) => e.IsInputKey = true;
    }

    private static Forms.Button Button(string text, Color color) => new()
    {
        Text = text, Width = 116, Height = 36, Margin = new Forms.Padding(4, 0, 4, 0),
        FlatStyle = Forms.FlatStyle.Flat, BackColor = color, ForeColor = Color.White
    };

    private int? Index => _list.SelectedIndices.Count == 1 ? _list.SelectedIndices[0] : null;

    internal IReadOnlyList<HotkeyBinding> Items => _items;

    /// <summary>A new row with no keys yet, doing the first action not already on a hotkey; the keys box gets focus.</summary>
    internal void Add()
    {
        if (_items.Count >= HotkeyBindings.MaxCount) { _status.Text = $"Up to {HotkeyBindings.MaxCount} hotkeys."; return; }
        string action = _actions.FirstOrDefault(a => _items.All(b => b.Action != a.Id)).Id ?? _actions[0].Id;
        _items.Add(new HotkeyBinding { Action = action });
        Reload(_items.Count - 1);
        _keys.Focus();
        _status.Text = "Press the combination, pick what it does, then Save.";
    }

    private void Reload(int select)
    {
        _list.BeginUpdate();
        _list.Items.Clear();
        foreach (var b in _items) _list.Items.Add(new Forms.ListViewItem([b.Keys.Length > 0 ? b.Keys : "(press keys)", Describe(b.Action)]));
        _list.EndUpdate();
        if (select >= 0 && select < _items.Count) { _list.Items[select].Selected = true; _list.Items[select].EnsureVisible(); }
        ShowSelected();
    }

    private void RefreshRow(int i)
    {
        _list.Items[i].SubItems[0].Text = _items[i].Keys.Length > 0 ? _items[i].Keys : "(press keys)";
        _list.Items[i].SubItems[1].Text = Describe(_items[i].Action);
    }

    private void ShowSelected()
    {
        _loading = true;
        var b = Index is int i ? _items[i] : null;
        _editor.Enabled = b != null;
        _keys.Text = b?.Keys ?? "";
        _action.SelectedIndex = b == null ? -1 : _actions.FindIndex(a => a.Id == b.Action);
        _loading = false;
    }

    private void Save()
    {
        var problems = HotkeyBindings.Problems(_items, Describe);
        if (problems.Count > 0) { _status.Text = string.Join("\n", problems.Take(3)); return; }
        var notTaken = _save(_items.Select(b => new HotkeyBinding { Keys = b.Keys, Action = b.Action }).ToList());
        _status.Text = notTaken.Count == 0 ? $"Saved · {_items.Count} hotkey{(_items.Count == 1 ? "" : "s")} active" : "Saved, but:\n" + string.Join("\n", notTaken.Take(3));
    }
}
