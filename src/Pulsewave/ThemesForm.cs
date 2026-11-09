using System.Diagnostics;
using System.IO;
using Pulsewave.Themes;
using Pulsewave.Themes.Editor;
using Color = System.Drawing.Color;
using Font = System.Drawing.Font;
using Size = System.Drawing.Size;
using Forms = System.Windows.Forms;

namespace Pulsewave;

/// <summary>
/// Theme gallery: Pulsewave Classic plus imported and custom themes, with import, apply, delete,
/// fonts, the editor and the playlist.
/// </summary>
public sealed class ThemesForm : Forms.Form
{
    /// <summary>The Halo desktop look (DesktopStyle), applied once everything in the window exists.</summary>
    protected override void OnLoad(EventArgs e) { DesktopStyle.Apply(this); base.OnLoad(e); }

    private const string ClassicKey = "\0classic";
    private readonly MonitorSettings _settings;
    private readonly string _root;
    private readonly Action<string> _apply;
    private readonly Func<ThemeDataContext>? _live;
    private readonly Action<string>? _themeSaved;
    private readonly Func<(int Width, int Height)>? _displaySize;
    private readonly Forms.Button _new, _edit, _duplicate, _playlist;
    private readonly Forms.ListView _list = new();
    private readonly Forms.ImageList _thumbs = new() { ImageSize = ThumbSize, ColorDepth = Forms.ColorDepth.Depth32Bit };
    private readonly Forms.Label _details = new();
    private readonly Forms.Button _import, _applyButton, _delete, _addFont, _openFolder;
    private readonly Forms.ComboBox _filter = new() { DropDownStyle = Forms.ComboBoxStyle.DropDownList, Width = 200 };
    private readonly Forms.Label _count = new() { AutoSize = true, Padding = new Forms.Padding(10, 7, 0, 0), ForeColor = Color.FromArgb(176, 196, 209) };
    private readonly Action<string>? _filterChanged;
    private IReadOnlyList<ThemeInfo> _themes = [];

    /// <param name="filterChanged">Saves the shape filter when it is changed.</param>
    public ThemesForm(MonitorSettings settings, string themesRoot, Action<string> apply,
        Func<ThemeDataContext>? live = null, Action<string>? themeSaved = null, Action? openPlaylist = null,
        Func<(int Width, int Height)>? displaySize = null, Action<string>? filterChanged = null)
    {
        _displaySize = displaySize;
        _filterChanged = filterChanged;
        _settings = settings;
        _root = themesRoot;
        _apply = apply;
        _live = live;
        _themeSaved = themeSaved;
        Text = "Pulsewave · Themes";
        StartPosition = Forms.FormStartPosition.CenterScreen;
        Size = new Size(1100, 640);
        MinimumSize = new Size(820, 460);
        BackColor = Color.FromArgb(19, 27, 37);
        ForeColor = Color.FromArgb(232, 242, 249);
        Font = new Font("Segoe UI", 9.5f);
        AutoScaleMode = Forms.AutoScaleMode.Dpi;

        var title = new Forms.Label
        {
            Dock = Forms.DockStyle.Top, Height = 74, Padding = new Forms.Padding(18, 14, 18, 0),
            Text = "THEMES\nPick a built-in template, import L-Connect 3 themes, or design your own, then choose what the display shows. " +
                   "Pulsewave Classic keeps the transparent Wallpaper Engine overlay.",
            Font = new Font("Segoe UI", 9.5f), ForeColor = Color.FromArgb(176, 196, 209)
        };

        _list.Dock = Forms.DockStyle.Fill;
        _list.View = Forms.View.LargeIcon;
        _list.LargeImageList = _thumbs;
        _list.MultiSelect = false;
        _list.HideSelection = false;
        _list.BackColor = Color.FromArgb(23, 32, 43);
        _list.ForeColor = ForeColor;
        _list.BorderStyle = Forms.BorderStyle.None;
        _list.SelectedIndexChanged += (_, _) => ShowDetails();
        _list.ItemActivate += (_, _) => ApplySelected();

        _details.Dock = Forms.DockStyle.Right;
        _details.Width = 330;
        _details.Padding = new Forms.Padding(14, 8, 14, 8);
        _details.ForeColor = Color.FromArgb(214, 228, 238);
        _details.BackColor = Color.FromArgb(27, 38, 50);

        var footer = new Forms.FlowLayoutPanel
        {
            Dock = Forms.DockStyle.Bottom, Height = 60, Padding = new Forms.Padding(12, 10, 12, 8),
            FlowDirection = Forms.FlowDirection.RightToLeft, WrapContents = false
        };
        _applyButton = Button("Apply", Color.FromArgb(40, 128, 117));
        _import = Button("Import…", Color.FromArgb(44, 62, 83));
        _delete = Button("Delete", Color.FromArgb(110, 52, 52));
        _addFont = Button("Add font…", Color.FromArgb(44, 62, 83));
        _openFolder = Button("Open folder", Color.FromArgb(44, 62, 83));
        _applyButton.Click += (_, _) => ApplySelected();
        _import.Click += async (_, _) => await ImportAsync();
        _delete.Click += (_, _) => DeleteSelected();
        _addFont.Click += (_, _) => AddFont();
        _openFolder.Click += (_, _) =>
        {
            Directory.CreateDirectory(_root);
            Process.Start(new ProcessStartInfo(_root) { UseShellExecute = true });
        };
        footer.Controls.AddRange([_applyButton, _import, _delete, _addFont, _openFolder]);

        var create = new Forms.FlowLayoutPanel
        {
            Dock = Forms.DockStyle.Bottom, Height = 50, Padding = new Forms.Padding(12, 8, 12, 0),
            FlowDirection = Forms.FlowDirection.RightToLeft, WrapContents = false
        };
        _new = Button("New", Color.FromArgb(44, 62, 83));
        _edit = Button("Edit", Color.FromArgb(44, 62, 83));
        _duplicate = Button("Duplicate", Color.FromArgb(44, 62, 83));
        _new.Click += (_, _) => NewTheme();
        _edit.Click += (_, _) => { if (SelectedId is { Length: > 0 } id) OpenEditor(id, draft: false); };
        _duplicate.Click += (_, _) => DuplicateSelected();
        _playlist = Button("Playlist…", Color.FromArgb(44, 62, 83));
        _playlist.Visible = openPlaylist != null;
        _playlist.Click += (_, _) => openPlaylist?.Invoke();
        create.Controls.AddRange([_new, _edit, _duplicate, _playlist]);

        // Shape filter: all, shaped like the display, horizontal or vertical.
        var filterBar = new Forms.FlowLayoutPanel { Dock = Forms.DockStyle.Top, Height = 40, Padding = new Forms.Padding(14, 4, 14, 0), WrapContents = false };
        foreach (var (_, label) in DisplaySizes.Filters) _filter.Items.Add(label);
        int saved = DisplaySizes.Filters.ToList().FindIndex(f => f.Key == _settings.ThemeGalleryFilter);
        _filter.SelectedIndex = Math.Max(0, saved);
        _filter.BackColor = Color.FromArgb(27, 38, 50);
        _filter.ForeColor = ForeColor;
        _filter.FlatStyle = Forms.FlatStyle.Flat;
        _filter.SelectedIndexChanged += (_, _) =>
        {
            string key = FilterKey;
            if (key == _settings.ThemeGalleryFilter) return;
            _filterChanged?.Invoke(key);
            Reload(SelectedId);
        };
        filterBar.Controls.AddRange([new Forms.Label { Text = "Show", AutoSize = true, Padding = new Forms.Padding(0, 7, 6, 0) }, _filter, _count]);

        Controls.Add(_list);
        Controls.Add(_details);
        Controls.Add(create);
        Controls.Add(footer);
        Controls.Add(filterBar);
        Controls.Add(title);
        FormClosed += (_, _) => _thumbs.Dispose();
        Reload(_settings.ActiveTheme);
    }

    /// <summary>Refreshes the ✓ after the theme changed outside this window (playlist or hotkey).</summary>
    public void ShowActive() => Reload(SelectedId);

    private static Forms.Button Button(string text, Color color) => new()
    {
        Text = text, Width = 116, Height = 36, Margin = new Forms.Padding(4, 0, 4, 0),
        FlatStyle = Forms.FlatStyle.Flat, BackColor = color, ForeColor = Color.White
    };

    private void Reload(string? select)
    {
        _themes = BuiltInTemplates.List().Concat(ThemeStore.List(_root)).ToList();
        _list.BeginUpdate();
        _list.Items.Clear();
        foreach (var image in _thumbs.Images.Cast<System.Drawing.Image>().ToList()) image.Dispose();
        _thumbs.Images.Clear();

        var (dw, dh) = _displaySize?.Invoke() ?? (0, 0);
        string filter = FilterKey;
        if (DisplaySizes.Matches(filter, 1920, 480, dw, dh))
        {
            _thumbs.Images.Add(ClassicKey, ClassicThumbnail());
            _list.Items.Add(new Forms.ListViewItem(Label("Pulsewave Classic", ThemeStore.ClassicId), ClassicKey) { Tag = ThemeStore.ClassicId });
        }
        foreach (var theme in _themes)
        {
            // Unreadable themes have no shape: they show under All shapes only.
            if (filter.Length > 0 && !DisplaySizes.Matches(filter, theme.Definition?.Width ?? 0, theme.Definition?.Height ?? 0, dw, dh)) continue;
            _thumbs.Images.Add(theme.Id, Thumbnail(theme));
            _list.Items.Add(new Forms.ListViewItem(Label(theme.Name, theme.Id), theme.Id) { Tag = theme.Id });
        }
        _list.EndUpdate();
        int total = _themes.Count + 1;
        _count.Text = filter.Length == 0 ? $"{total} themes" : $"{_list.Items.Count} of {total} themes";
        var match = _list.Items.Cast<Forms.ListViewItem>().FirstOrDefault(i => (string)i.Tag! == (select ?? ""));
        if (match != null) { match.Selected = true; match.EnsureVisible(); }
        ShowDetails();
    }

    private string Label(string name, string id) =>
        (_settings.ActiveTheme == id ? "✓ " : "") + name + (BuiltInTemplates.IsBuiltIn(id) ? "  ·  template" : "");

    private string FilterKey => _filter.SelectedIndex >= 0 ? DisplaySizes.Filters[_filter.SelectedIndex].Key : "";

    private string? SelectedId => _list.SelectedItems.Count == 1 ? (string?)_list.SelectedItems[0].Tag : null;

    private void ShowDetails()
    {
        string? id = SelectedId;
        _delete.Enabled = _applyButton.Enabled = id != null;
        bool readable = id is { Length: > 0 } && _themes.Any(t => t.Id == id && t.Definition != null);
        _duplicate.Enabled = readable;
        _edit.Enabled = readable && !BuiltInTemplates.IsBuiltIn(id);
        if (BuiltInTemplates.IsBuiltIn(id)) _delete.Enabled = false; // templates stay as shipped
        _addFont.Enabled = true;
        if (id == null) { _details.Text = "Select a theme."; return; }
        if (id == ThemeStore.ClassicId)
        {
            _delete.Enabled = false;
            _details.Text = "PULSEWAVE CLASSIC\n\nThe built-in dashboard: clock with now playing, CPU, GPU and system panels, " +
                            "Midnight colors and the transparent Wallpaper Engine overlay.\n\n" +
                            (_settings.ActiveTheme == id ? "Currently showing." : "");
            return;
        }
        var info = _themes.FirstOrDefault(t => t.Id == id);
        if (info?.Definition is not { } theme)
        {
            _applyButton.Enabled = false;
            _details.Text = "This theme could not be read:\n" + info?.Error;
            return;
        }
        var lines = new List<string> { theme.Name.ToUpperInvariant(), "", $"{theme.Source} · {DisplaySizes.Describe(theme.Width, theme.Height)}" };
        if (BuiltInTemplates.IsBuiltIn(id)) lines.Add("Built-in template. Apply it as is, or Duplicate it to make your own editable version.");
        var kinds = theme.Elements.GroupBy(e => e.Kind).Select(g => $"{g.Count()} {g.Key.ToString().ToLowerInvariant()}");
        lines.Add(string.Join(", ", kinds) + (theme.BackgroundVideo != null ? " · video background" : ""));
        if (_settings.ActiveTheme == id) lines.Add("Currently showing.");

        if (_displaySize?.Invoke() is var (dw, dh) && dw > 0 && !DisplaySizes.SameShape(theme.Width, theme.Height, dw, dh))
        {
            lines.Add("");
            lines.Add($"Made for a {(theme.Height > theme.Width ? "vertical" : "different")} screen: the display is {dw}×{dh}, so the theme is shown " +
                      "at its own shape with bars of its background color. Duplicate it and change the screen size in the editor to rearrange it.");
        }
        var fonts = theme.Elements.Where(e => e.Font != null).Select(e => e.Font!.Family).Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(f => !ThemeFonts.Resolve(f).Found).ToList();
        if (fonts.Count > 0)
        {
            lines.Add("");
            lines.Add("Fonts to add (Segoe UI is used until then): " + string.Join(", ", fonts) + ". Use Add font… with the .ttf/.otf file.");
        }
        var dataNames = theme.Elements.Select(e => e.DataName).OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var unmapped = dataNames.Select(ThemeData.SensorFor).OfType<string>().Distinct()
            .Where(s => !_settings.Sensors.TryGetValue(s, out var m) || string.IsNullOrWhiteSpace(m.RegistryValue))
            .Select(s => SensorIds.Theme.FirstOrDefault(t => t.Id == s).Label ?? s).ToList();
        if (unmapped.Count > 0)
        {
            lines.Add("");
            lines.Add("Map in Sensor Manager to show values: " + string.Join(", ", unmapped) + ".");
        }
        int slots = theme.Elements.Where(e => e.Kind == ThemeElementKind.Shortcut || e.DataName == "APP_NAME").Select(e => e.Slot).DefaultIfEmpty(0).Max();
        if (slots > _settings.Shortcuts.Count)
        {
            lines.Add("");
            lines.Add($"Shortcut tiles: {Math.Min(slots, _settings.Shortcuts.Count)} of {slots} slots have a program. Add apps and games from the tray: Shortcuts.");
        }
        if (dataNames.Any(n => n.StartsWith("CLAUDE_", StringComparison.OrdinalIgnoreCase)) && !_settings.ClaudeAccountUsage &&
            ClaudeStatusLine.Status(ClaudeStatusLine.ClaudeSettingsFile, ClaudeStatusLine.LauncherFile).State != ClaudeStatusLine.State.Connected)
        {
            lines.Add("");
            lines.Add("Claude usage shows once it is turned on: tray → Claude usage → Read from your Claude sign-in (or the terminal status line).");
        }
        if (dataNames.Any(n => n.StartsWith("CODEX_", StringComparison.OrdinalIgnoreCase)) && !Directory.Exists(Path.Combine(AiUsageReader.CodexHome, "sessions")))
        {
            lines.Add("");
            lines.Add("Codex usage comes from the Codex app or CLI's own logs; none were found on this PC yet.");
        }
        if (dataNames.Any(ThemeData.IsWeather) && !_settings.Weather.IsSet)
        {
            lines.Add("");
            lines.Add("Weather shows once you set your city: tray → Weather.");
        }
        var unsupported = dataNames.Where(ThemeData.Unsupported.Contains).ToList();
        if (unsupported.Count > 0)
        {
            lines.Add("");
            lines.Add("Not available yet (shows --): " + string.Join(", ", unsupported) + ".");
        }
        _details.Text = string.Join("\n", lines);
    }

    private void OpenEditor(string id, bool draft, IEnumerable<string>? notes = null)
    {
        try
        {
            var editor = new ThemeEditorWindow(Path.Combine(_root, id), _settings, _live, saved =>
            {
                Reload(saved);
                _themeSaved?.Invoke(saved);
            }, discardFolderIfNeverSaved: draft, notes);
            editor.Closed += (_, _) => { if (!IsDisposed) Reload(id); };
            editor.Show();
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or System.Text.Json.JsonException or UnauthorizedAccessException)
        {
            Forms.MessageBox.Show(this, "The theme could not be opened: " + ex.Message, "Theme editor");
        }
    }

    private void NewTheme()
    {
        Directory.CreateDirectory(_root);
        string id = ThemeStore.NewId(_root, "New theme");
        Directory.CreateDirectory(Path.Combine(_root, id));
        // A new theme starts at the size of the display it will run on.
        var (width, height) = _displaySize?.Invoke() ?? (1920, 480);
        if (width is < 1 or > 8192 || height is < 1 or > 8192) (width, height) = (1920, 480);
        ThemeStore.Save(Path.Combine(_root, id), new ThemeDefinition { Name = "New theme", Source = "Pulsewave editor", BackgroundColor = "#FF0A0E1A",
            Width = width, Height = height });
        Reload(id);
        OpenEditor(id, draft: true);
    }

    private void DuplicateSelected()
    {
        if (SelectedId is not { Length: > 0 } id || _themes.FirstOrDefault(t => t.Id == id)?.Definition is not { } theme) return;
        string copy = ThemeStore.NewId(_root, theme.Name + " copy");
        string from = _themes.First(t => t.Id == id).Directory, to = Path.Combine(_root, copy);
        foreach (var file in Directory.EnumerateFiles(from, "*", SearchOption.AllDirectories))
        {
            string target = Path.Combine(to, Path.GetRelativePath(from, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }
        var definition = ThemeStore.Load(to);
        definition.Name = theme.Name + " copy";
        ThemeStore.Save(to, definition);
        Reload(copy);
        OpenEditor(copy, draft: false); // a copy exists to be customized
    }

    private void ApplySelected()
    {
        if (SelectedId is not { } id || !_applyButton.Enabled) return;
        _apply(id);
        Reload(id);
    }

    private async Task ImportAsync()
    {
        using var dialog = new Forms.OpenFileDialog
        {
            Title = "Import an L-Connect theme",
            Filter = "L-Connect themes (*.zip;*.template)|*.zip;*.template",
            Multiselect = true
        };
        if (dialog.ShowDialog(this) != Forms.DialogResult.OK) return;
        SetBusy(true, "Importing…");
        var messages = new List<string>();
        string? lastId = null;
        foreach (var file in dialog.FileNames)
        {
            try
            {
                var result = await Task.Run(() => LConnectImporter.Import(file, _root));
                lastId = result.ThemeId;
                messages.Add($"Imported '{result.Theme.Name}'." + string.Concat(result.Notes.Select(n => "\n  · " + n)));
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or
                                       NotSupportedException or ArgumentException or System.Text.Json.JsonException)
            {
                messages.Add($"Could not import {Path.GetFileName(file)}: {ex.Message}");
            }
        }
        SetBusy(false, null);
        Reload(lastId ?? SelectedId);
        Forms.MessageBox.Show(this, string.Join("\n\n", messages), "Pulsewave · Import", Forms.MessageBoxButtons.OK,
            Forms.MessageBoxIcon.Information);
    }

    private void DeleteSelected()
    {
        if (SelectedId is not { Length: > 0 } id || BuiltInTemplates.IsBuiltIn(id)) return;
        var name = _themes.FirstOrDefault(t => t.Id == id)?.Name ?? id;
        if (Forms.MessageBox.Show(this, $"Delete '{name}'? Its imported files are removed; the original download is not touched.",
                "Delete theme", Forms.MessageBoxButtons.YesNo, Forms.MessageBoxIcon.Question) != Forms.DialogResult.Yes) return;
        try
        {
            if (_settings.ActiveTheme == id) _apply(ThemeStore.ClassicId); // releases the video file first
            ThemeStore.Delete(_root, id);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Forms.MessageBox.Show(this, "Could not delete the theme: " + ex.Message, "Delete theme");
        }
        Reload(ThemeStore.ClassicId);
    }

    private void AddFont()
    {
        using var dialog = new Forms.OpenFileDialog { Title = "Add a font for themes", Filter = "Fonts (*.ttf;*.otf)|*.ttf;*.otf", Multiselect = true };
        if (dialog.ShowDialog(this) != Forms.DialogResult.OK) return;
        var failed = new List<string>();
        foreach (var file in dialog.FileNames)
        {
            try { ThemeFonts.Add(file); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            { failed.Add(Path.GetFileName(file)); }
        }
        if (failed.Count > 0)
            Forms.MessageBox.Show(this, "Not a usable font: " + string.Join(", ", failed), "Add font");
        // A running theme picks up the font when it is applied again.
        if (_settings.ActiveTheme.Length > 0 && failed.Count < dialog.FileNames.Length) _apply(_settings.ActiveTheme);
        ShowDetails();
    }

    private void SetBusy(bool busy, string? message)
    {
        UseWaitCursor = busy;
        foreach (var button in new[] { _import, _applyButton, _delete, _addFont, _new, _edit, _duplicate }) button.Enabled = !busy;
        if (message != null) _details.Text = message;
    }

    /// <summary>
    /// Gallery tiles are 2:1, so bars, the HYTE Y70 and tablets all show at a useful size. Portrait themes are
    /// turned on their side (top at the left) to fill the tile instead of a thin sliver, and marked VERTICAL.
    /// </summary>
    internal static readonly Size ThumbSize = new(240, 120);

    private static readonly Color TileBack = Color.FromArgb(23, 32, 43);

    internal static System.Drawing.Image Thumbnail(ThemeInfo theme)
    {
        var bitmap = new System.Drawing.Bitmap(ThumbSize.Width, ThumbSize.Height);
        using var g = System.Drawing.Graphics.FromImage(bitmap);
        g.Clear(TileBack);
        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        bool portrait = theme.Definition is { } d && d.Height > d.Width;
        string? preview = theme.Definition?.Preview ?? theme.Definition?.BackgroundImage;
        try
        {
            if (preview != null)
            {
                // Read into memory so the gallery never locks theme files.
                using var stream = new MemoryStream(File.ReadAllBytes(ThemeStore.ResolveFile(theme.Directory, preview)));
                using var source = System.Drawing.Image.FromStream(stream);
                using var image = new System.Drawing.Bitmap(source);
                if (portrait) image.RotateFlip(System.Drawing.RotateFlipType.Rotate270FlipNone);
                g.DrawImage(image, Fit(image.Width, image.Height));
                if (portrait) Badge(g, "VERTICAL");
                return bitmap;
            }
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or InvalidDataException or OutOfMemoryException) { }
        // No preview: the theme's shape in its background color, with its name.
        var (tw, th) = theme.Definition is { Width: > 0, Height: > 0 } t ? (t.Width, t.Height) : (1920, 480);
        if (portrait) (tw, th) = (th, tw);
        var shape = Fit(tw, th);
        using (var fill = new System.Drawing.SolidBrush(Color.FromArgb(11, 21, 33))) g.FillRectangle(fill, shape);
        using var font = new Font("Segoe UI", 10);
        g.DrawString(theme.Name, font, System.Drawing.Brushes.LightGray, new RectangleF(shape.X + 8, shape.Y, shape.Width - 16, shape.Height),
            new StringFormat { LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter });
        if (portrait) Badge(g, "VERTICAL");
        return bitmap;
    }

    private static Rectangle Fit(int width, int height)
    {
        double scale = Math.Min((double)ThumbSize.Width / width, (double)ThumbSize.Height / height);
        int w = Math.Max(1, (int)Math.Round(width * scale)), h = Math.Max(1, (int)Math.Round(height * scale));
        return new Rectangle((ThumbSize.Width - w) / 2, (ThumbSize.Height - h) / 2, w, h);
    }

    private static void Badge(System.Drawing.Graphics g, string text)
    {
        using var font = new Font("Segoe UI Semibold", 7);
        var size = g.MeasureString(text, font);
        var box = new RectangleF(ThumbSize.Width - size.Width - 10, ThumbSize.Height - size.Height - 6, size.Width + 6, size.Height + 2);
        using var back = new System.Drawing.SolidBrush(Color.FromArgb(220, 11, 21, 33));
        g.FillRectangle(back, box);
        using var accent = new System.Drawing.SolidBrush(Color.FromArgb(118, 245, 217));
        g.DrawString(text, font, accent, box.X + 3, box.Y + 1);
    }

    internal static System.Drawing.Image ClassicThumbnail()
    {
        var bitmap = new System.Drawing.Bitmap(ThumbSize.Width, ThumbSize.Height);
        using var g = System.Drawing.Graphics.FromImage(bitmap);
        g.Clear(TileBack);
        // A 1920x480 sketch, laid out on 256x64 and scaled into the tile.
        var area = Fit(1920, 480);
        g.TranslateTransform(area.X, area.Y);
        g.ScaleTransform(area.Width / 256f, area.Height / 64f);
        using (var back = new System.Drawing.SolidBrush(Color.FromArgb(11, 21, 33))) g.FillRectangle(back, 0, 0, 256, 64);
        using var panel = new System.Drawing.SolidBrush(Color.FromArgb(30, 48, 64));
        foreach (var (x, w) in new[] { (4, 44), (52, 64), (120, 64), (188, 64) }) g.FillRectangle(panel, x, 10, w, 50);
        using var accent = new System.Drawing.SolidBrush(Color.FromArgb(118, 245, 217));
        using var font = new Font("Segoe UI Semibold", 8);
        g.DrawString("PULSEWAVE", font, accent, 4, 0);
        return bitmap;
    }
}
