using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Button = System.Windows.Controls.Button;
using CheckBox = System.Windows.Controls.CheckBox;
using Color = System.Windows.Media.Color;
using ContextMenu = System.Windows.Controls.ContextMenu;
using MenuItem = System.Windows.Controls.MenuItem;
using SelectionMode = System.Windows.Controls.SelectionMode;
using ComboBox = System.Windows.Controls.ComboBox;
using Cursors = System.Windows.Input.Cursors;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using Label = System.Windows.Controls.Label;
using ListBox = System.Windows.Controls.ListBox;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Orientation = System.Windows.Controls.Orientation;
using MessageBox = System.Windows.MessageBox;
using Path = System.IO.Path;
using Point = System.Windows.Point;
using Rectangle = System.Windows.Shapes.Rectangle;
using Size = System.Windows.Size;
using TextBox = System.Windows.Controls.TextBox;

namespace Pulsewave.Themes.Editor;

/// <summary>
/// Drag-and-drop theme editor: the theme at its native size (1920x480 unless changed) with sample or live values,
/// an element list and a properties panel. Edits stay in memory until Save; media added during
/// a session that is never saved is removed again.
/// </summary>
public sealed class ThemeEditorWindow : Window
{
    private const double HandleSize = 14;
    // Halo, like the tray windows: navy base, card panels, light text, soft grey, sky-blue accent.
    private static readonly Brush Panel = Frozen(Color.FromRgb(22, 29, 48));
    private static readonly Brush Fore = Frozen(Color.FromRgb(245, 247, 250));
    private static readonly Brush Muted = Frozen(Color.FromRgb(201, 210, 222));
    private static readonly Brush Accent = Frozen(Color.FromRgb(127, 216, 255));

    private readonly ThemeDocument _doc;
    private readonly MonitorSettings _settings;
    private readonly Func<ThemeDataContext>? _live;
    private readonly Action<string>? _saved;
    private readonly bool _discardFolderIfNeverSaved;
    private readonly HashSet<string> _addedFiles = new(StringComparer.OrdinalIgnoreCase);
    private readonly Grid _stage = new() { Width = 1920, Height = 480, ClipToBounds = true };
    private readonly ContentControl _backgroundHost = new();
    private readonly ContentControl _elementsHost = new();
    private readonly Canvas _overlay = new() { Background = Brushes.Transparent, Width = 1920, Height = 480 };
    private readonly ListBox _list = new();
    private readonly StackPanel _properties = new() { Margin = new Thickness(10) };
    private readonly TextBlock _status = new() { Foreground = Muted, Margin = new Thickness(10, 4, 10, 6), TextWrapping = TextWrapping.Wrap };
    private readonly CheckBox _liveToggle = new() { Content = "Live values", Foreground = Fore, Margin = new Thickness(8, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
    private readonly CheckBox _videoToggle = new() { Content = "Play video", Foreground = Fore, IsChecked = true, Margin = new Thickness(8, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
    private readonly CheckBox _backgroundToggle = new() { Content = "Move background", Foreground = Fore, Margin = new Thickness(8, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center,
        ToolTip = "Drag the canvas to move the background picture or video; scroll to zoom" };
    private readonly DispatcherTimer _wheelSettle = new() { Interval = TimeSpan.FromMilliseconds(450) };
    private readonly CheckBox _snapToggle = new() { Content = "Snap", Foreground = Fore, IsChecked = true, Margin = new Thickness(8, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
    private readonly DispatcherTimer _tick = new() { Interval = TimeSpan.FromSeconds(1) };
    private ThemeView? _elements;
    private ThemeView? _background;
    private string _backgroundKey = "";
    private int _selected = -1; // the element whose own properties show (last clicked)
    private readonly HashSet<int> _selection = new(); // everything selected: a whole group, or Ctrl+clicked elements
    private bool _everSaved;
    private bool _refreshingList;

    // Drag state
    private enum Gesture { None, Move, Resize, Background }
    private Gesture _gesture;
    private Point _dragStart;
    private Rect _startBounds;
    private int _corner; // 0 top-left, 1 top-right, 2 bottom-right, 3 bottom-left
    private Rect _preview;
    private Point _groupOrigin;
    private double _groupFactor = 1;
    // Background placement shown on the canvas; differs from the document only while dragging or scrolling.
    private double _bgZoom = 1, _bgX, _bgY, _bgStartX, _bgStartY;

    public ThemeEditorWindow(string themeDirectory, MonitorSettings settings, Func<ThemeDataContext>? live, Action<string>? saved,
        bool discardFolderIfNeverSaved = false, IEnumerable<string>? notes = null)
    {
        _doc = new ThemeDocument(themeDirectory);
        _settings = settings;
        _live = live;
        _saved = saved;
        _discardFolderIfNeverSaved = discardFolderIfNeverSaved;
        if (discardFolderIfNeverSaved) _doc.MarkUnsaved(); // closing asks before a draft is thrown away
        Title ="Lumen HUD · Theme editor · " + _doc.Theme.Name;
        Width = 1500; Height = 820; MinWidth = 1000; MinHeight = 560;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = Frozen(Color.FromRgb(14, 20, 36));
        Foreground = Fore;
        Resources.MergedDictionaries.Add(EditorStyle.Resources());
        SourceInitialized += (_, _) => DesktopStyle.DarkTitleBar(new System.Windows.Interop.WindowInteropHelper(this).Handle);
        FontFamily = new System.Windows.Media.FontFamily("Segoe UI");

        _stage.Children.Add(_backgroundHost);
        _stage.Children.Add(_elementsHost);
        _stage.Children.Add(_overlay);
        _overlay.MouseLeftButtonDown += OnMouseDown;
        _overlay.MouseMove += OnMouseMove;
        _overlay.MouseLeftButtonUp += OnMouseUp;
        _overlay.MouseWheel += OnWheel;
        _wheelSettle.Tick += (_, _) => CommitBackground(); // a burst of scrolling is one undo step
        _backgroundToggle.Click += (_, _) => { if (_backgroundToggle.IsChecked == true) Select(-1); else CommitBackground(); };

        _list.Background = Panel; _list.Foreground = Fore; _list.BorderThickness = new Thickness(0);
        ScrollViewer.SetHorizontalScrollBarVisibility(_list, ScrollBarVisibility.Disabled);
        _list.SelectionMode = SelectionMode.Extended; // Ctrl/Shift+click picks several; one click picks a single element, even inside a group
        _list.SelectionChanged += (_, e) =>
        {
            if (_refreshingList) return;
            var picked = _list.SelectedItems.Cast<object>().Select(o => _list.Items.IndexOf(o)).ToList();
            int primary = e.AddedItems.Count > 0 ? _list.Items.IndexOf(e.AddedItems[0]) : _list.SelectedIndex;
            SelectMany(picked, primary);
        };

        var root = new DockPanel();
        var toolbar = BuildToolbar();
        DockPanel.SetDock(toolbar, Dock.Top); root.Children.Add(toolbar);
        DockPanel.SetDock(_status, Dock.Bottom); root.Children.Add(_status);
        var left = new DockPanel { Width = 260, Background = Panel };
        var listTitle = new TextBlock { Text = "ELEMENTS · drawn top to bottom", Foreground = Accent, FontWeight = FontWeights.SemiBold, Margin = new Thickness(12, 10, 10, 6), FontSize = 12 };
        DockPanel.SetDock(listTitle, Dock.Top); left.Children.Add(listTitle); left.Children.Add(_list);
        DockPanel.SetDock(left, Dock.Left); root.Children.Add(left);
        var right = new ScrollViewer { Width = 330, Content = _properties, Background = Panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        DockPanel.SetDock(right, Dock.Right); root.Children.Add(right);
        root.Children.Add(new Border
        {
            Background = Frozen(Color.FromRgb(10, 14, 26)), Padding = new Thickness(16),
            Child = new Viewbox { Stretch = Stretch.Uniform, Child = new Border { BorderBrush = Muted, BorderThickness = new Thickness(1), Child = _stage } }
        });
        Content = root;

        _liveToggle.IsEnabled = _live != null;
        _liveToggle.Click += (_, _) => UpdateValues();
        _videoToggle.Click += (_, _) => { if (_videoToggle.IsChecked == true) _background?.Resume(); else _background?.Pause(); };
        _tick.Tick += (_, _) => UpdateValues();
        _doc.Changed += () => Refresh(rebuildProperties: false);
        PreviewKeyDown += OnKey;
        Closing += OnClosing;
        Closed += (_, _) => { _tick.Stop(); _background?.Dispose(); _elements?.Dispose(); };

        Refresh(rebuildProperties: true);
        _tick.Start();
        if (notes != null) _status.Text = string.Join("  ·  ", notes);
    }

    private ThemeElement? Selected => _selected >= 0 && _selected < _doc.Theme.Elements.Count ? _doc.Theme.Elements[_selected] : null;
    private bool Multiple => _selection.Count > 1;
    private List<int> SelectionOrder => _selection.Order().ToList();

    /// <summary>Every element of the group <paramref name="index"/> belongs to, or just that element.</summary>
    private List<int> GroupOf(int index) => _doc.Theme.Elements[index].Group is { } g
        ? Enumerable.Range(0, _doc.Theme.Elements.Count).Where(i => _doc.Theme.Elements[i].Group == g).ToList()
        : [index];

    /// <summary>The frame around everything selected.</summary>
    private Rect SelectionBounds()
    {
        var r = Rect.Empty;
        foreach (var i in _selection)
            if (i < _doc.Theme.Elements.Count && BoundsOf(_doc.Theme.Elements[i]) is { IsEmpty: false } b) r.Union(b);
        return r;
    }

    // --- Toolbar --------------------------------------------------------------------------

    private UIElement BuildToolbar()
    {
        var bar = new WrapPanel { Background = Panel, Margin = new Thickness(0, 0, 0, 1) };
        Button B(string text, Action click, string? tip = null)
        {
            var b = new Button { Content = text, Margin = new Thickness(4, 6, 0, 6), Padding = new Thickness(12, 6, 12, 6), ToolTip = tip };
            b.Click += (_, _) => click();
            bar.Children.Add(b);
            return b;
        }
        B("Save", () => Save(), "Ctrl+S");
        B("Undo", () => { _doc.Undo(); Refresh(true); }, "Ctrl+Z");
        B("Redo", () => { _doc.Redo(); Refresh(true); }, "Ctrl+Y");
        bar.Children.Add(new Separator { Width = 12, Background = Brushes.Transparent });
        B("+ Text", () => Add(new ThemeElement { Kind = ThemeElementKind.Text, Text = "LABEL", X = 960, Y = 60,
            Font = new ThemeFont { Size = 20, Bold = true, Color = "#FFD1E0ED", Alignment = ThemeTextAlignment.Center } }));
        B("+ Value", () => Add(new ThemeElement { Kind = ThemeElementKind.Data, DataName = "CPUTEMP", ShowUnit = true, X = 960, Y = 120,
            Font = new ThemeFont { Size = 48, Bold = true, Color = "#FFFFFFFF", Alignment = ThemeTextAlignment.Center } }));
        B("+ Bar", () => Add(new ThemeElement { Kind = ThemeElementKind.Bar, DataName = "CPULOAD", X = 810, Y = 300, Width = 300, Height = 24,
            ForeColor = "#FF7FD8FF", GradientColor = "#FFC3A6FF", BackColor = "#2EFFFFFF", CornerRadius = 6 }));
        B("+ Gauge", () => Add(new ThemeElement { Kind = ThemeElementKind.Arc, DataName = "GPULOAD", X = 890, Y = 170, Diameter = 140, Width = 140, Height = 140,
            Thickness = 14, StartAngle = 90, SweepAngle = 280, RoundCaps = true, ForeColor = "#FF9EC6FF", BackColor = "#FF22303F" }));
        B("+ Chart", () => Add(new ThemeElement { Kind = ThemeElementKind.Chart, DataName = "CPULOAD", X = 810, Y = 330, Width = 300, Height = 100,
            Samples = 60, LineColor = "#FF7FD8FF", FillColor = "#337FD8FF", LineWidth = 2 }));
        B("+ Panel", () => Add(new ThemeElement { Kind = ThemeElementKind.Panel, X = 760, Y = 140, Width = 400, Height = 200,
            CornerRadius = 32, FillColor = "#8C141A2C", GradientColor = "#A60E1322", GradientAngle = 90, BorderColor = "#40FFFFFF", BorderWidth = 1 }), "A card or backdrop behind readouts");
        B("+ Line", () => Add(new ThemeElement { Kind = ThemeElementKind.Line, X = 760, Y = 240, Width = 400, Height = 0,
            LineColor = "#407FD8FF", LineWidth = 2 }));
        B("+ Album art", () => Add(new ThemeElement { Kind = ThemeElementKind.AlbumArt, X = 860, Y = 140, Width = 120, Height = 120,
            CornerRadius = 12, FillColor = "#FF1E2633", LineColor = "#FF8FA3B8" }), "Now playing cover; add Value elements for its title and artist");
        B("+ Shortcut", () => Add(new ThemeElement { Kind = ThemeElementKind.Shortcut, Slot = NextSlot(), X = 900, Y = 180, Width = 120, Height = 120,
            CornerRadius = 28, FillColor = "#40FFFFFF", BorderColor = "#40FFFFFF", BorderWidth = 1, LineColor = "#FFD5DCE6" }),
            "A tile that opens one of your shortcuts (tray → Shortcuts); its slot number picks which");
        B("+ Apps button", () =>
        {
            string group = ThemeWidgets.UniqueGroup("Apps button", _doc.Theme.Elements);
            AddElements([
                Centered(new ThemeElement { Kind = ThemeElementKind.Panel, Action = ThemeAction.AppDrawer, X = 860, Y = 200, Width = 200, Height = 80,
                    CornerRadius = 40, FillColor = "#40FFFFFF", BorderColor = "#40FFFFFF", BorderWidth = 1, Group = group }),
                Centered(new ThemeElement { Kind = ThemeElementKind.Text, Text = "APPS", Action = ThemeAction.AppDrawer, X = 960, Y = 222, Group = group,
                    Font = new ThemeFont { Size = 20, Bold = true, Color = "#FFFFFFFF", Alignment = ThemeTextAlignment.Center } })]);
        }, "Opens the app drawer with all your shortcuts");
        B("+ Picture…", AddPicture);
        Button widgets = null!;
        widgets = B("+ Widget ▾", () =>
        {
            var menu = new ContextMenu { PlacementTarget = widgets, Placement = PlacementMode.Bottom };
            foreach (var widget in ThemeWidgets.All)
            {
                var item = new MenuItem { Header = widget.Name, ToolTip = widget.Tip };
                item.Click += (_, _) => AddWidget(widget);
                menu.Items.Add(item);
            }
            menu.IsOpen = true;
        }, "Ready-made groups: a clock, CPU/GPU/memory cards, now playing, network, volume, timer, usage and shortcuts");
        bar.Children.Add(new Separator { Width = 12, Background = Brushes.Transparent });
        B("Group", GroupSelection, "Ctrl+G · Ctrl+click elements on the canvas (or in the list) to pick several");
        B("Ungroup", Ungroup, "Ctrl+Shift+G");
        B("Duplicate", Duplicate, "Ctrl+D");
        B("Delete", DeleteSelected, "Del");
        B("Forward", () => Reorder(+1), "Draw later (on top)");
        B("Backward", () => Reorder(-1), "Draw earlier (underneath)");
        bar.Children.Add(new Separator { Width = 12, Background = Brushes.Transparent });
        bar.Children.Add(_liveToggle);
        bar.Children.Add(_videoToggle);
        bar.Children.Add(_snapToggle);
        bar.Children.Add(_backgroundToggle);
        return bar;
    }

    // --- Rendering ------------------------------------------------------------------------

    private void Refresh(bool rebuildProperties)
    {
        var theme = _doc.Theme;
        Title = "Lumen HUD · Theme editor · " + theme.Name + (_doc.IsDirty ? " *" : "");
        _stage.Width = _overlay.Width = theme.Width;
        _stage.Height = _overlay.Height = theme.Height;
        string key = $"{theme.BackgroundVideo}|{theme.BackgroundImage}|{theme.BackgroundColor}|{theme.BackgroundFit}|{theme.BackgroundVideoRotation}|{theme.Width}x{theme.Height}";
        if (key != _backgroundKey)
        {
            _backgroundKey = key;
            _background?.Dispose();
            _background = new ThemeView(theme, _doc.Directory, playVideo: true, drawElements: false);
            if (_videoToggle.IsChecked != true) _background.Pause();
            _backgroundHost.Content = _background;
        }
        if (_gesture != Gesture.Background && !_wheelSettle.IsEnabled)
            (_bgZoom, _bgX, _bgY) = (theme.BackgroundZoom, theme.BackgroundOffsetX, theme.BackgroundOffsetY);
        _background?.PlaceBackground(_bgZoom, _bgX, _bgY);
        _elements?.Dispose();
        _elements = new ThemeView(theme, _doc.Directory, playVideo: false, drawBackground: false);
        _elementsHost.Content = _elements;
        if (_selected >= theme.Elements.Count) _selected = theme.Elements.Count - 1;
        _selection.RemoveWhere(i => i >= theme.Elements.Count);
        if (_selection.Count == 0 && _selected >= 0) _selection.Add(_selected);
        UpdateValues();
        RefreshList();
        DrawOverlay();
        if (rebuildProperties) BuildProperties();
        if (_elements.MissingFonts.Count > 0)
            _status.Text = "Fonts not found (Segoe UI shown): " + string.Join(", ", _elements.MissingFonts) + ". Add them in the Themes window.";
    }

    private void UpdateValues()
    {
        var data = _liveToggle.IsChecked == true && _live != null ? _live() : ThemeSampleData.Context();
        _elements?.Update(data);
        _elements?.UpdateLayout();
        if (_gesture == Gesture.None) DrawOverlay();
    }

    private void RefreshList()
    {
        _refreshingList = true;
        _list.Items.Clear();
        foreach (var e in _doc.Theme.Elements)
        {
            string what = e.Kind switch
            {
                ThemeElementKind.Text => $"Text · {e.Text}",
                ThemeElementKind.Image => $"Picture · {Path.GetFileName(e.ImageFile)}",
                ThemeElementKind.Bar when e.DataName is null => "Plate (backdrop)",
                ThemeElementKind.Panel => $"Panel · {e.Width:0}×{e.Height:0}",
                ThemeElementKind.Line => "Line",
                ThemeElementKind.AlbumArt => "Album art",
                ThemeElementKind.Shortcut when e.Slot == ThemeDataContext.GameSlot => "Shortcut · game cover",
                ThemeElementKind.Shortcut => $"Shortcut · slot {e.Slot}",
                ThemeElementKind.Data when e.DataName == "APP_NAME" => $"Value · shortcut {e.Slot} name",
                _ => $"{KindName(e.Kind)} · {Label(e.DataName)}"
            };
            string text = (e.Hidden ? "(hidden) " : "") + (e.Group is { } g ? g + " › " : "") + what;
            _list.Items.Add(new ListBoxItem { Content = new TextBlock { Text = text, TextTrimming = TextTrimming.CharacterEllipsis }, ToolTip = text,
                Foreground = e.Hidden ? Muted : Fore });
        }
        _list.SelectedItems.Clear();
        foreach (var i in _selection.Order()) _list.SelectedItems.Add(_list.Items[i]);
        if (_selected >= 0) _list.ScrollIntoView(_list.Items[_selected]);
        _refreshingList = false;
    }

    private static string KindName(ThemeElementKind kind) => kind switch
    {
        ThemeElementKind.Data => "Value", ThemeElementKind.Arc => "Gauge", _ => kind.ToString()
    };

    private static string Label(string? data) => ThemeData.Catalog.FirstOrDefault(c => c.Name == data).Label ?? data ?? "";

    private Rect BoundsOf(ThemeElement e) => _elements?.BoundsOf(e) ?? Rect.Empty;

    private void DrawOverlay(Rect? previewRect = null, IReadOnlyList<double>? vGuides = null, IReadOnlyList<double>? hGuides = null)
    {
        _overlay.Children.Clear();
        foreach (var x in vGuides ?? []) _overlay.Children.Add(new Line { X1 = x, X2 = x, Y1 = 0, Y2 = _doc.Theme.Height, Stroke = Brushes.Magenta, StrokeThickness = 1.5 });
        foreach (var y in hGuides ?? []) _overlay.Children.Add(new Line { Y1 = y, Y2 = y, X1 = 0, X2 = _doc.Theme.Width, Stroke = Brushes.Magenta, StrokeThickness = 1.5 });
        if (_selection.Count == 0) return;
        var r = previewRect ?? SelectionBounds();
        if (r.IsEmpty) return;
        if (Multiple && previewRect == null)
            foreach (var i in _selection)
                if (BoundsOf(_doc.Theme.Elements[i]) is { IsEmpty: false } member)
                {
                    var outline = new Rectangle { Width = member.Width, Height = member.Height, Stroke = Accent, StrokeThickness = 1, Opacity = 0.45 };
                    Canvas.SetLeft(outline, member.Left); Canvas.SetTop(outline, member.Top);
                    _overlay.Children.Add(outline);
                }
        var frame = new Rectangle { Width = r.Width + 4, Height = r.Height + 4, Stroke = Accent, StrokeThickness = 2, StrokeDashArray = [4, 3] };
        Canvas.SetLeft(frame, r.Left - 2); Canvas.SetTop(frame, r.Top - 2);
        _overlay.Children.Add(frame);
        foreach (var p in Corners(r))
        {
            var handle = new Rectangle { Width = HandleSize, Height = HandleSize, Fill = Accent, Stroke = Brushes.Black, StrokeThickness = 1 };
            Canvas.SetLeft(handle, p.X - HandleSize / 2); Canvas.SetTop(handle, p.Y - HandleSize / 2);
            _overlay.Children.Add(handle);
        }
    }

    private static Point[] Corners(Rect r) => [r.TopLeft, r.TopRight, r.BottomRight, r.BottomLeft];

    // --- Mouse ----------------------------------------------------------------------------

    private bool MovingBackground => _backgroundToggle.IsChecked == true && (_doc.Theme.BackgroundImage != null || _doc.Theme.BackgroundVideo != null);

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        var p = e.GetPosition(_stage);
        if (MovingBackground)
        {
            CommitBackground();
            (_bgStartX, _bgStartY) = (_bgX, _bgY);
            Begin(Gesture.Background, p, Rect.Empty);
            return;
        }
        if (SelectionBounds() is { IsEmpty: false } current)
        {
            var corners = Corners(current);
            for (int i = 0; i < 4; i++)
                if (Math.Abs(p.X - corners[i].X) <= HandleSize && Math.Abs(p.Y - corners[i].Y) <= HandleSize)
                {
                    Begin(Gesture.Resize, p, current); _corner = i;
                    return;
                }
        }
        int hit = -1;
        for (int i = _doc.Theme.Elements.Count - 1; i >= 0; i--)
            if (!_doc.Theme.Elements[i].Hidden && BoundsOf(_doc.Theme.Elements[i]) is var b && !b.IsEmpty && b.Contains(p)) { hit = i; break; }
        bool ctrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
        if (hit < 0) { if (!ctrl) Select(-1); return; }
        if (e.ClickCount >= 2) Select(hit); // double-click: just this element, even inside a group
        else if (ctrl)
        {
            // Ctrl+click adds or removes an element (its whole group, if it has one).
            var next = new HashSet<int>(_selection);
            if (next.Contains(hit)) next.ExceptWith(GroupOf(hit)); else next.UnionWith(GroupOf(hit));
            SelectMany(next, hit);
            return;
        }
        else if (!_selection.Contains(hit)) SelectMany(GroupOf(hit), hit); // a click picks the element's whole group
        Begin(Gesture.Move, p, SelectionBounds());
    }

    private void Begin(Gesture gesture, Point at, Rect bounds)
    {
        _gesture = gesture; _dragStart = at; _startBounds = bounds; _preview = bounds;
        _overlay.CaptureMouse();
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        var p = e.GetPosition(_stage);
        if (_gesture == Gesture.Background)
        {
            (_bgX, _bgY) = (Math.Round(_bgStartX + p.X - _dragStart.X), Math.Round(_bgStartY + p.Y - _dragStart.Y));
            _background?.PlaceBackground(_bgZoom, _bgX, _bgY);
            return;
        }
        if (_gesture == Gesture.None)
        {
            if (MovingBackground) { _overlay.Cursor = Cursors.SizeAll; return; }
            bool overHandle = SelectionBounds() is { IsEmpty: false } s && Corners(s).Any(c => Math.Abs(p.X - c.X) <= HandleSize && Math.Abs(p.Y - c.Y) <= HandleSize);
            _overlay.Cursor = overHandle ? Cursors.SizeNWSE : Cursors.Arrow;
            return;
        }
        double dx = p.X - _dragStart.X, dy = p.Y - _dragStart.Y;
        var others = _doc.Theme.Elements.Where((el, i) => !_selection.Contains(i) && !el.Hidden).Select(BoundsOf);
        bool snap = _snapToggle.IsChecked == true && !Keyboard.IsKeyDown(Key.LeftAlt);
        if (_gesture == Gesture.Move)
        {
            var moved = new Rect(_startBounds.X + dx, _startBounds.Y + dy, _startBounds.Width, _startBounds.Height);
            var s = snap ? EditorGeometry.Snap(moved, others, new Size(_doc.Theme.Width, _doc.Theme.Height), 8) : new EditorGeometry.SnapResult(0, 0, [], []);
            _preview = new Rect(moved.X + s.Dx, moved.Y + s.Dy, moved.Width, moved.Height);
            foreach (var i in _selection) _elements?.Offset(_doc.Theme.Elements[i], _preview.X - _startBounds.X, _preview.Y - _startBounds.Y);
            DrawOverlay(_preview, s.VerticalGuides, s.HorizontalGuides);
        }
        else
        {
            double left = _startBounds.Left, top = _startBounds.Top, right = _startBounds.Right, bottom = _startBounds.Bottom;
            if (_corner is 0 or 3) left += dx; else right += dx;
            if (_corner is 0 or 1) top += dy; else bottom += dy;
            _preview = new Rect(new Point(Math.Min(left, right - 4), Math.Min(top, bottom - 4)), new Point(Math.Max(right, left + 4), Math.Max(bottom, top + 4)));
            if (Multiple)
            {
                // A group scales evenly from the opposite corner, so it keeps its layout.
                (_groupOrigin, _groupFactor) = EditorGeometry.GroupScale(_startBounds, _preview, _corner);
                _preview = EditorGeometry.Scaled(_startBounds, _groupOrigin, _groupFactor);
            }
            DrawOverlay(_preview);
        }
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_gesture == Gesture.None) return;
        var gesture = _gesture;
        _gesture = Gesture.None;
        _overlay.ReleaseMouseCapture();
        if (gesture == Gesture.Background) { CommitBackground(); return; }
        int index = _selected;
        var indices = SelectionOrder;
        if (index < 0 || indices.Count == 0) return;
        if (gesture == Gesture.Move)
        {
            double dx = Math.Round(_preview.X - _startBounds.X), dy = Math.Round(_preview.Y - _startBounds.Y);
            if (dx == 0 && dy == 0) { foreach (var i in indices) _elements?.Offset(_doc.Theme.Elements[i], 0, 0); DrawOverlay(); return; }
            _doc.Change(t => { foreach (var i in indices) EditorGeometry.Move(t.Elements[i], dx, dy); });
        }
        else if (_preview != _startBounds)
        {
            var before = _startBounds; var after = _preview;
            var (origin, factor) = (_groupOrigin, _groupFactor);
            if (indices.Count > 1) _doc.Change(t => { foreach (var i in indices) EditorGeometry.Scale(t.Elements[i], origin, factor); });
            else _doc.Change(t => EditorGeometry.Resize(t.Elements[index], before, after));
        }
        BuildProperties();
    }

    private void OnWheel(object sender, MouseWheelEventArgs e)
    {
        if (!MovingBackground) return;
        e.Handled = true;
        // Zoom about the pointer: the background point under it stays put.
        var p = e.GetPosition(_stage);
        double zoom = Math.Clamp(_bgZoom * Math.Pow(1.1, e.Delta / 120.0), ThemeStore.MinBackgroundZoom, ThemeStore.MaxBackgroundZoom);
        var (x, y) = EditorGeometry.ZoomAbout(p, new Point(_doc.Theme.Width / 2.0, _doc.Theme.Height / 2.0), _bgZoom, zoom, _bgX, _bgY);
        (_bgZoom, _bgX, _bgY) = (Math.Round(zoom, 3), Math.Round(x), Math.Round(y));
        _background?.PlaceBackground(_bgZoom, _bgX, _bgY);
        _wheelSettle.Stop(); _wheelSettle.Start();
    }

    private void CommitBackground()
    {
        _wheelSettle.Stop();
        var t = _doc.Theme;
        if (t.BackgroundZoom == _bgZoom && t.BackgroundOffsetX == _bgX && t.BackgroundOffsetY == _bgY) return;
        var (zoom, x, y) = (_bgZoom, _bgX, _bgY);
        _doc.Change(theme => { theme.BackgroundZoom = zoom; theme.BackgroundOffsetX = x; theme.BackgroundOffsetY = y; });
        BuildProperties();
    }

    // --- Keyboard -------------------------------------------------------------------------

    private void OnKey(object sender, KeyEventArgs e)
    {
        bool typing = Keyboard.FocusedElement is TextBox or ComboBox { IsEditable: true };
        bool ctrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
        if (ctrl && e.Key == Key.S) { Save(); e.Handled = true; return; }
        if (typing) return;
        if (ctrl && e.Key == Key.Z) { _doc.Undo(); Refresh(true); e.Handled = true; }
        else if (ctrl && e.Key == Key.Y) { _doc.Redo(); Refresh(true); e.Handled = true; }
        else if (ctrl && e.Key == Key.D) { Duplicate(); e.Handled = true; }
        else if (ctrl && e.Key == Key.G && Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) { Ungroup(); e.Handled = true; }
        else if (ctrl && e.Key == Key.G) { GroupSelection(); e.Handled = true; }
        else if (ctrl && e.Key == Key.A) { SelectMany(Enumerable.Range(0, _doc.Theme.Elements.Count), _doc.Theme.Elements.Count - 1); e.Handled = true; }
        else if (e.Key == Key.Delete) { DeleteSelected(); e.Handled = true; }
        else if (e.Key == Key.Escape) { Select(-1); e.Handled = true; }
        else if (_selection.Count > 0 && e.Key is Key.Left or Key.Right or Key.Up or Key.Down)
        {
            double step = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? 10 : 1;
            double dx = e.Key == Key.Left ? -step : e.Key == Key.Right ? step : 0;
            double dy = e.Key == Key.Up ? -step : e.Key == Key.Down ? step : 0;
            var indices = SelectionOrder;
            _doc.Change(t => { foreach (var i in indices) EditorGeometry.Move(t.Elements[i], dx, dy); });
            BuildProperties();
            e.Handled = true;
        }
    }

    // --- Commands -------------------------------------------------------------------------

    private void Select(int index) => SelectMany(index < 0 ? [] : [index], index);

    private void SelectMany(IEnumerable<int> indices, int primary)
    {
        _selection.Clear();
        foreach (var i in indices)
            if (i >= 0 && i < _doc.Theme.Elements.Count) _selection.Add(i);
        _selected = _selection.Contains(primary) ? primary : _selection.Count > 0 ? _selection.Max() : -1;
        RefreshList();
        DrawOverlay();
        BuildProperties();
    }

    private void GroupSelection()
    {
        if (!Multiple)
        {
            _status.Text = "To group, pick two or more elements: Ctrl+click them on the canvas, or Ctrl/Shift+click them in the list.";
            return;
        }
        var indices = SelectionOrder;
        string name = ThemeWidgets.UniqueGroup("Group", _doc.Theme.Elements.Where((_, i) => !_selection.Contains(i)));
        _doc.Change(t => { foreach (var i in indices) t.Elements[i].Group = name; });
        BuildProperties();
    }

    private void Ungroup()
    {
        var indices = SelectionOrder;
        if (!indices.Any(i => _doc.Theme.Elements[i].Group != null)) return;
        _doc.Change(t => { foreach (var i in indices) t.Elements[i].Group = null; });
        BuildProperties();
    }

    /// <summary>Adds a widget as a new group, giving its shortcut tiles the theme's next free slots.</summary>
    private void AddWidget(ThemeWidget widget)
    {
        var elements = ThemeWidgets.Place(widget, _doc.Theme);
        var used = _doc.Theme.Elements.Where(e => e.Kind == ThemeElementKind.Shortcut).Select(e => e.Slot).ToHashSet();
        foreach (var tile in elements.Where(e => e.Kind == ThemeElementKind.Shortcut))
        {
            tile.Slot = Enumerable.Range(1, AppShortcut.MaxCount).FirstOrDefault(s => !used.Contains(s), tile.Slot);
            used.Add(tile.Slot);
        }
        AddElements(elements);
    }

    /// <summary>The background color with its opacity off (see-through) or on, keeping the color itself.</summary>
    internal static string SeeThrough(string color, bool on)
    {
        var c = ThemeView.ParseColor(color, Colors.Black);
        return $"#{(on ? 0 : 255):X2}{c.R:X2}{c.G:X2}{c.B:X2}";
    }

    /// <summary>The first shortcut slot no tile of this theme uses yet.</summary>
    private int NextSlot()
    {
        var used = _doc.Theme.Elements.Where(e => e.Kind == ThemeElementKind.Shortcut).Select(e => e.Slot).ToHashSet();
        return Enumerable.Range(1, AppShortcut.MaxCount).FirstOrDefault(s => !used.Contains(s), 1);
    }

    private void Add(ThemeElement element) => AddElements([Centered(element)]);

    /// <summary>The toolbar's starting positions are for 1920x480; this keeps new elements near the middle of other sizes.</summary>
    private ThemeElement Centered(ThemeElement element)
    {
        element.X += _doc.Theme.Width / 2.0 - 960;
        element.Y += _doc.Theme.Height / 2.0 - 240;
        return element;
    }

    private void AddElements(List<ThemeElement> elements)
    {
        int first = _doc.Theme.Elements.Count;
        _doc.Change(t => t.Elements.AddRange(elements));
        SelectMany(Enumerable.Range(first, elements.Count), first + elements.Count - 1);
    }

    /// <summary>Copies of everything selected, a little offset; a copied group becomes a new group.</summary>
    private void Duplicate()
    {
        if (_selection.Count == 0) return;
        var copies = new List<ThemeElement>();
        var renamed = new Dictionary<string, string>();
        foreach (var i in SelectionOrder)
        {
            var copy = ThemeDocument.Clone(_doc.Theme.Elements[i]);
            if (copy.Group is { } group)
            {
                if (!renamed.TryGetValue(group, out var name))
                    renamed[group] = name = ThemeWidgets.UniqueGroup(System.Text.RegularExpressions.Regex.Replace(group, @" \d+$", ""), _doc.Theme.Elements.Concat(copies));
                copy.Group = name;
            }
            EditorGeometry.Move(copy, 20, 20);
            copies.Add(copy);
        }
        AddElements(copies);
    }

    private void DeleteSelected()
    {
        if (_selection.Count == 0) return;
        var indices = SelectionOrder;
        _doc.Change(t => { for (int k = indices.Count - 1; k >= 0; k--) t.Elements.RemoveAt(indices[k]); });
        Select(indices.Count == 1 ? Math.Min(indices[0], _doc.Theme.Elements.Count - 1) : -1);
    }

    /// <summary>Moves everything selected one step up or down the drawing order, keeping it together.</summary>
    private void Reorder(int direction)
    {
        var plan = EditorGeometry.ReorderPlan(_doc.Theme.Elements.Count, _selection, direction);
        if (plan.Count == 0) return;
        _doc.Change(t => { foreach (var (from, to) in plan) (t.Elements[from], t.Elements[to]) = (t.Elements[to], t.Elements[from]); });
        var moved = new HashSet<int>(_selection);
        int primary = _selected;
        foreach (var (from, to) in plan) { moved.Remove(from); moved.Add(to); if (primary == from) primary = to; }
        SelectMany(moved, primary);
    }

    private void AddPicture()
    {
        if (PickFile("Pictures (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp") is not { } file) return;
        try
        {
            string name = ThemeMedia.ImportImage(_doc.Directory, file, "images/picture");
            _addedFiles.Add(name);
            Add(new ThemeElement { Kind = ThemeElementKind.Image, ImageFile = name, X = 0, Y = 0, Width = 1 });
        }
        catch (Exception ex) when (ex is IOException or NotSupportedException or ArgumentException or InvalidDataException or FileFormatException)
        { Fail("That picture could not be used: " + ex.Message); }
    }

    private void SetBackgroundPicture()
    {
        if (PickFile("Pictures (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp") is not { } file) return;
        try
        {
            string name = ThemeMedia.ImportImage(_doc.Directory, file, "background");
            _addedFiles.Add(name);
            _doc.Change(t => { t.BackgroundImage = name; t.BackgroundVideo = null; t.BackgroundFit = "Cover"; });
            BuildProperties();
        }
        catch (Exception ex) when (ex is IOException or NotSupportedException or ArgumentException or InvalidDataException or FileFormatException)
        { Fail("That picture could not be used: " + ex.Message); }
    }

    private void SetBackgroundVideo()
    {
        if (PickFile("Videos (*.mp4;*.h264;*.mov;*.wmv;*.m4v)|*.mp4;*.h264;*.mov;*.wmv;*.m4v") is not { } file) return;
        try
        {
            Cursor = Cursors.Wait;
            var (name, _, _) = ThemeMedia.ImportVideo(_doc.Directory, file);
            _addedFiles.Add(name);
            string poster = ThemeMedia.SavePoster(_doc.Directory, name);
            _addedFiles.Add(poster);
            _doc.Change(t => { t.BackgroundVideo = name; t.BackgroundImage = poster; t.BackgroundVideoRotation = 0; t.BackgroundFit = "Cover"; });
            BuildProperties();
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or System.Runtime.InteropServices.COMException or InvalidOperationException or UnauthorizedAccessException)
        { Fail("That video could not be used: " + ex.Message); }
        finally { Cursor = null; }
    }

    private string? PickFile(string filter)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Filter = filter };
        return dialog.ShowDialog(this) == true ? dialog.FileName : null;
    }

    private void Fail(string message) => MessageBox.Show(this, message, "Theme editor", MessageBoxButton.OK, MessageBoxImage.Warning);

    private bool Save()
    {
        try
        {
            _doc.Theme.Preview = ThemeMedia.SavePreview(_doc.Directory, _doc.Theme); // not an undo step
            _doc.Save();
            ThemeMedia.RemoveUnreferenced(_doc.Directory, _doc.Theme);
            _addedFiles.Clear();
            _everSaved = true;
            Refresh(false);
            _status.Text = "Saved.";
            _saved?.Invoke(Path.GetFileName(_doc.Directory));
            return true;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            Fail("Could not save: " + ex.Message);
            return false;
        }
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_doc.IsDirty)
        {
            var answer = MessageBox.Show(this, "Save changes to this theme?", "Theme editor", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
            if (answer == MessageBoxResult.Cancel) { e.Cancel = true; return; }
            if (answer == MessageBoxResult.Yes && !Save()) { e.Cancel = true; return; }
        }
        // A new theme or builder draft that was never saved goes away entirely.
        if (!_everSaved && _discardFolderIfNeverSaved)
        {
            _background?.Dispose(); _background = null;
            try { Directory.Delete(_doc.Directory, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            return;
        }
        // Media added in this session but never saved is not referenced by theme.json.
        foreach (var file in _addedFiles)
            try { File.Delete(ThemeStore.ResolveFile(_doc.Directory, file)); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    // --- Properties -----------------------------------------------------------------------

    private void BuildProperties()
    {
        _properties.Children.Clear();
        _target = _properties;
        if (Selected is not { } e)
        {
            PanelTitle("Theme");
            Heading("SCREEN");
            Field("Name", _doc.Theme.Name, v => _doc.Change(t => t.Name = v.Trim().Length > 0 ? v.Trim() : t.Name));
            ColorField("Background color", _doc.Theme.BackgroundColor, v => _doc.Change(t => t.BackgroundColor = v));
            // See-through: a fully transparent background lets the desktop (and a Wallpaper Engine scene on that display) show behind the elements.
            Check("See-through background (desktop or Wallpaper Engine shows behind)", ThemeView.ParseColor(_doc.Theme.BackgroundColor, Colors.Black).A == 0,
                v => _doc.Change(t => t.BackgroundColor = SeeThrough(t.BackgroundColor, v)));
            var sizes = DisplaySizes.Presets.Select(p => p.Label).Append("Custom").ToArray();
            Choice("Screen size", sizes, DisplaySizes.Presets.FirstOrDefault(p => p.Width == _doc.Theme.Width && p.Height == _doc.Theme.Height)?.Label ?? "Custom",
                v => { if (DisplaySizes.Presets.FirstOrDefault(p => p.Label == v) is { } size) Resize(size.Width, size.Height); });
            Number("Width", _doc.Theme.Width, v => Resize((int)Math.Round(v), _doc.Theme.Height));
            Number("Height", _doc.Theme.Height, v => Resize(_doc.Theme.Width, (int)Math.Round(v)));
            Note("Elements keep their positions when the size changes; select any now outside the screen in the list and set its X and Y. A display Windows shows sideways can run a vertical theme with tray → Rotate HUD.");
            Heading("BACKGROUND");
            Note(_doc.Theme.BackgroundVideo != null ? "Background: video" : _doc.Theme.BackgroundImage != null ? "Background: picture" : "Background: color only");
            if (_doc.Theme.BackgroundImage != null || _doc.Theme.BackgroundVideo != null)
            {
                Choice("Background fit", ["Fill", "Cover", "Fit"], _doc.Theme.BackgroundFit, v => _doc.Change(t => t.BackgroundFit = v));
                Number("Background zoom %", Math.Round(_doc.Theme.BackgroundZoom * 100, 1),
                    v => _doc.Change(t => t.BackgroundZoom = Math.Clamp(v / 100, ThemeStore.MinBackgroundZoom, ThemeStore.MaxBackgroundZoom)));
                Number("Background shift X", _doc.Theme.BackgroundOffsetX, v => _doc.Change(t => t.BackgroundOffsetX = Math.Clamp(v, -8192, 8192)));
                Number("Background shift Y", _doc.Theme.BackgroundOffsetY, v => _doc.Change(t => t.BackgroundOffsetY = Math.Clamp(v, -8192, 8192)));
                Action("Reset background position", () => { _doc.Change(t => { t.BackgroundZoom = 1; t.BackgroundOffsetX = 0; t.BackgroundOffsetY = 0; }); BuildProperties(); });
                Note("Fill stretches to the screen, Cover fills it without distortion, Fit shows the whole picture. Tick Move background in the toolbar to drag it on the canvas and scroll to zoom.");
            }
            Action("Background picture…", SetBackgroundPicture);
            Action("Background video…", SetBackgroundVideo);
            Action("Clear background media", () => { _doc.Change(t => { t.BackgroundImage = null; t.BackgroundVideo = null; }); BuildProperties(); });
            Heading("COLORS");
            var kinds = _doc.Theme.Elements.GroupBy(x => (x.Kind, Plate: x.Kind == ThemeElementKind.Bar && x.DataName == null))
                .Select(g => (Label: $"{(g.Key.Plate ? "Plate" : KindName(g.Key.Kind))}s ({g.Count()})", Like: g.First())).ToList();
            if (kinds.Count > 0)
                Choice("Select every… (to color them together)", ["—", .. kinds.Select(k => k.Label)], "—",
                    v => { if (kinds.FirstOrDefault(k => k.Label == v) is { Like: { } like }) SelectAlike(like); });
            ColorTools(Enumerable.Range(0, _doc.Theme.Elements.Count).ToList(), byRole: false);
            Heading("HOW TO");
            Note("Click an element on the canvas to edit it. Drag to move (Alt disables snapping), drag a corner to resize, arrow keys nudge (Shift = 10 px).");
            Note("Groups: clicking any part of a group (a widget, say) picks all of it, so it moves and scales as one. Double-click a part, or pick it in the list, to edit it alone. Ctrl+click several elements and press Group (Ctrl+G) to make your own.");
            return;
        }
        if (Multiple) { BuildGroupProperties(); return; }
        int i = _selected;
        void Edit(Action<ThemeElement> change) => _doc.Change(t => change(t.Elements[i]));

        PanelTitle(e.Kind == ThemeElementKind.Bar && e.DataName is null ? "Plate" : KindName(e.Kind));
        if (e.Group is { } group)
        {
            Note($"Part of the group '{group}'.");
            Action("Select the whole group", () => SelectMany(GroupOf(i), i));
            Action("Take out of the group", () => { Edit(x => x.Group = null); BuildProperties(); });
        }
        int alike = _doc.Theme.Elements.Count(x => ThemeColors.Alike(x, e));
        if (alike > 1) Action($"Select all {alike} like this (to color them together)", () => SelectAlike(e));

        Heading("POSITION");
        Number("X", e.X, v => Edit(x => x.X = v));
        Number("Y", e.Y, v => Edit(x => x.Y = v));
        Check("Hidden", e.Hidden, v => Edit(x => x.Hidden = v));

        bool hasData = e.Kind is ThemeElementKind.Data or ThemeElementKind.Arc or ThemeElementKind.Chart || e.Kind == ThemeElementKind.Bar && e.DataName != null;
        if (e.Kind is ThemeElementKind.Text or ThemeElementKind.Data || hasData)
        {
            Heading(e.Kind == ThemeElementKind.Text ? "TEXT" : "DATA");
            if (e.Kind == ThemeElementKind.Text) Field("Text", e.Text ?? "", v => Edit(x => x.Text = v));
            if (hasData) DataPicker("Shows", e.DataName, name => Edit(x => x.DataName = name));
            if (e.Kind == ThemeElementKind.Data)
            {
                if (e.DataName is "TIME")
                    Choice("Format", ["h:m", "h:m:s", "h", "m"], e.DataFormat ?? "h:m", v => Edit(x => x.DataFormat = v));
                if (e.DataName is "DATE")
                {
                    string[] dates = ["Long (Thursday, Dec 31)", "Short (Thu, Dec 31)", "Custom (Y M D)"];
                    string shown = e.DataFormat switch { "long" => dates[0], "short" => dates[1], _ => dates[2] };
                    Choice("Format", dates, shown, v =>
                    {
                        Edit(x => x.DataFormat = v == dates[0] ? "long" : v == dates[1] ? "short" : v == dates[2] ? "month" : "Y-M-D");
                        BuildProperties();
                    });
                    if (shown == dates[3]) Field("Custom format (Y M D)", e.DataFormat ?? "Y-M-D", v => Edit(x => x.DataFormat = v));
                }
                Check("Show unit", e.ShowUnit, v => Edit(x => x.ShowUnit = v));
                if (e.DataName?.EndsWith("TEMP", StringComparison.OrdinalIgnoreCase) == true) Check("Fahrenheit", e.Fahrenheit, v => Edit(x => x.Fahrenheit = v));
            }
            if (e.Kind is ThemeElementKind.Text or ThemeElementKind.Data)
                Number("Max width (0 = none; longer ends in …)", e.Width, v => Edit(x => x.Width = Math.Max(0, v)));
        }

        Heading("TAP ACTION");
        string[] actionLabels = ["None", "Play / pause", "Next track", "Previous track", "Details page", "Next theme", "Previous theme",
                                 "Open shortcut (its slot)", "App drawer", "Volume up", "Volume down", "Mute / unmute",
                                 "Timer start / pause", "Timer reset", "Timer +1 minute", "Stopwatch start / pause", "Stopwatch reset",
                                 "Lights page", "Lights on / off", "Aesthetic mode on / off", "App volumes page", "Next color palette", "Media page (queue, seek)", "Color palettes page", "Gaming page",
                                 "Pomodoro 25 / 5", "Pomodoro 50 / 10", "Timer card: start / pause", "Timer card: reset", "Timer card: timer or stopwatch"];
        string automatic = $"Automatic ({actionLabels[(int)ThemeActions.Automatic(e)].ToLowerInvariant()})";
        Choice("Tap / click action", [automatic, .. actionLabels], e.Action is { } act ? actionLabels[(int)act] : automatic,
            v => Edit(x => x.Action = v == automatic ? null : (ThemeAction)Array.IndexOf(actionLabels, v)));
        if (e.Kind == ThemeElementKind.Shortcut || e.DataName == "APP_NAME" || e.Action == ThemeAction.Launch)
        {
            bool cover = e.Slot == ThemeDataContext.GameSlot;
            if (e.Kind == ThemeElementKind.Shortcut)
                Check("Show the game being played (its cover art)", cover, v => { Edit(x => x.Slot = v ? ThemeDataContext.GameSlot : NextSlot()); BuildProperties(); });
            if (!cover)
            {
                Number("Shortcut slot (tray → Shortcuts)", e.Slot, v => Edit(x => x.Slot = (int)Math.Clamp(Math.Round(v), 1, AppShortcut.MaxCount)));
                Note("Themes only name a slot; each person picks what it opens in their own Shortcuts list.");
            }
        }

        if (e.Font is { } font)
        {
            Heading("TEXT STYLE");
            var family = new ComboBox { IsEditable = true, ItemsSource = ThemeFonts.AvailableFamilies(), Text = font.Family, Margin = new Thickness(0, 2, 0, 6) };
            family.LostKeyboardFocus += (_, _) => { if (family.Text != font.Family && family.Text.Trim().Length > 0) Edit(x => x.Font!.Family = family.Text.Trim()); };
            family.SelectionChanged += (_, _) => { if (family.SelectedItem is string f && f != font.Family) Edit(x => x.Font!.Family = f); };
            Row("Font", family);
            Number("Size (pt)", font.Size, v => Edit(x => x.Font!.Size = Math.Clamp(v, 4, 400)));
            Check("Bold", font.Bold, v => Edit(x => x.Font!.Bold = v));
            Check("Italic", font.Italic, v => Edit(x => x.Font!.Italic = v));
            ColorField("Color", font.Color, v => Edit(x => x.Font!.Color = v));
            Choice("Align to X", ["Left", "Center", "Right"], font.Alignment.ToString(),
                v => Edit(x => x.Font!.Alignment = Enum.Parse<ThemeTextAlignment>(v)));
        }
        if (e.Kind is not (ThemeElementKind.Text or ThemeElementKind.Data)) Heading("LOOK");
        switch (e.Kind)
        {
            case ThemeElementKind.Bar:
                Number("Width", e.Width, v => Edit(x => x.Width = Math.Max(1, v)));
                Number("Height", e.Height, v => Edit(x => x.Height = Math.Max(1, v)));
                Shared(e, Edit);
                Number("Corner radius", e.CornerRadius, v => Edit(x => x.CornerRadius = Math.Max(0, v)));
                break;
            case ThemeElementKind.Arc:
                Number("Diameter", e.Diameter, v => Edit(x => { x.Diameter = Math.Max(4, v); x.Width = x.Height = x.Diameter; }));
                Number("Thickness", e.Thickness, v => Edit(x => x.Thickness = Math.Max(1, v)));
                Number("Start angle", e.StartAngle, v => Edit(x => x.StartAngle = v));
                Number("Sweep angle", e.SweepAngle, v => Edit(x => x.SweepAngle = Math.Clamp(v, 1, 360)));
                Check("Round ends", e.RoundCaps, v => Edit(x => x.RoundCaps = v));
                Shared(e, Edit);
                break;
            case ThemeElementKind.Chart:
                Number("Width", e.Width, v => Edit(x => x.Width = Math.Max(2, v)));
                Number("Height", e.Height, v => Edit(x => x.Height = Math.Max(2, v)));
                Number("Maximum (0 = auto)", e.Maximum, v => Edit(x => x.Maximum = Math.Max(0, v)));
                Number("Samples", e.Samples, v => Edit(x => x.Samples = (int)Math.Clamp(v, 2, 600)));
                Number("Auto scale at least (Maximum 0)", e.MinimumScale, v => Edit(x => x.MinimumScale = Math.Max(0, v)));
                OptionalNumber("Dashed guide at (blank = none)", e.GuideAt, v => Edit(x => x.GuideAt = v is > 0 ? v : null));
                OptionalNumber("Line amber above (blank = never)", e.WarnAt, v => Edit(x => { x.WarnAt = v is > 0 ? v : null; x.LowIsBad = false; }));
                ColorField("Line", e.LineColor, v => Edit(x => x.LineColor = v));
                ColorField("Fill", e.FillColor, v => Edit(x => x.FillColor = v));
                ColorField("Border", e.BorderColor, v => Edit(x => x.BorderColor = v));
                Number("Line width", e.LineWidth, v => Edit(x => x.LineWidth = Math.Max(0.5, v)));
                Number("Border width", e.BorderWidth, v => Edit(x => x.BorderWidth = Math.Max(0, v)));
                break;
            case ThemeElementKind.Image:
                Number("Scale", e.Width is > 0 ? e.Width : 1, v => Edit(x => x.Width = Math.Clamp(v, 0.05, 19)));
                Note("File: " + e.ImageFile);
                break;
            case ThemeElementKind.Panel:
                Number("Width", e.Width, v => Edit(x => x.Width = Math.Max(1, v)));
                Number("Height", e.Height, v => Edit(x => x.Height = Math.Max(1, v)));
                Choice("Corners", ["Round", "Cut"], e.CornerStyle, v => Edit(x => x.CornerStyle = v));
                Number("Corner size", e.CornerRadius, v => Edit(x => x.CornerRadius = Math.Max(0, v)));
                ColorField("Fill", e.FillColor, v => Edit(x => x.FillColor = v));
                OptionalColorField("Gradient to (blank = none)", e.GradientColor, v => Edit(x => x.GradientColor = v));
                Choice("Gradient", ["Linear", "Radial"], e.GradientStyle, v => Edit(x => x.GradientStyle = v));
                Number("Gradient angle", e.GradientAngle, v => Edit(x => x.GradientAngle = v));
                ColorField("Border", e.BorderColor, v => Edit(x => x.BorderColor = v));
                Number("Border width", e.BorderWidth, v => Edit(x => x.BorderWidth = Math.Max(0, v)));
                break;
            case ThemeElementKind.AlbumArt:
                Number("Width", e.Width, v => Edit(x => x.Width = Math.Max(8, v)));
                Number("Height", e.Height, v => Edit(x => x.Height = Math.Max(8, v)));
                Number("Corner radius", e.CornerRadius, v => Edit(x => x.CornerRadius = Math.Max(0, v)));
                ColorField("Placeholder background", e.FillColor, v => Edit(x => x.FillColor = v));
                ColorField("Placeholder note", e.LineColor, v => Edit(x => x.LineColor = v));
                break;
            case ThemeElementKind.Shortcut:
                Number("Width", e.Width, v => Edit(x => x.Width = Math.Max(16, v)));
                Number("Height", e.Height, v => Edit(x => x.Height = Math.Max(16, v)));
                Number("Corner radius", e.CornerRadius, v => Edit(x => x.CornerRadius = Math.Max(0, v)));
                Choice("Tile style", ["Plain", "Tinted by its icon"], e.TileStyle == "Tinted" ? "Tinted by its icon" : "Plain",
                    v => Edit(x => x.TileStyle = v == "Plain" ? null : "Tinted"));
                Check("Name under the icon", e.ShowLabel, v => Edit(x => x.ShowLabel = v));
                ColorField("Tile", e.FillColor, v => Edit(x => x.FillColor = v));
                OptionalColorField("Gradient to (blank = none)", e.GradientColor, v => Edit(x => x.GradientColor = v));
                ColorField("Border", e.BorderColor, v => Edit(x => x.BorderColor = v));
                Number("Border width", e.BorderWidth, v => Edit(x => x.BorderWidth = Math.Max(0, v)));
                ColorField("Icon placeholder and name", e.LineColor, v => Edit(x => x.LineColor = v));
                Note("Shows the program's own icon. Tinted tiles take a soft backdrop in the icon's color, and Lock, Sleep and key shortcuts become round buttons.");
                break;
            case ThemeElementKind.Line:
                Number("Length X", e.Width, v => Edit(x => x.Width = v));
                Number("Length Y", e.Height, v => Edit(x => x.Height = v));
                ColorField("Color", e.LineColor, v => Edit(x => x.LineColor = v));
                Number("Thickness", e.LineWidth, v => Edit(x => x.LineWidth = Math.Max(0.5, v)));
                Number("Dash (0 = solid)", e.SegmentWidth, v => Edit(x => x.SegmentWidth = Math.Max(0, v)));
                Number("Dash gap", e.SegmentGap, v => Edit(x => x.SegmentGap = Math.Max(0, v)));
                break;
        }
        if (e.Kind is ThemeElementKind.Data or ThemeElementKind.Bar or ThemeElementKind.Arc && hasData) WarningSection(e, Edit);
        if (e.Kind is not (ThemeElementKind.Image or ThemeElementKind.Chart))
        {
            Heading("GLOW");
            OptionalColorField("Glow color (blank = none)", e.GlowColor, v => Edit(x => x.GlowColor = v));
            Number("Glow size", e.GlowSize, v => Edit(x => x.GlowSize = Math.Clamp(v, 1, 80)));
        }
    }

    /// <summary>Warning colors: amber at one level and red at another, optionally counting down or decided by another reading.</summary>
    private void WarningSection(ThemeElement e, Action<Action<ThemeElement>> edit)
    {
        Heading("WARNING COLORS");
        bool on = e.WarnAt != null || e.CriticalAt != null;
        Check("Change color when a reading gets high (or low)", on, v =>
        {
            edit(x =>
            {
                if (!v) { x.WarnAt = x.CriticalAt = null; return; }
                var (warn, critical) = ThemeEditorDefaults.Thresholds(x.ColorBy ?? x.DataName);
                x.WarnAt = warn; x.CriticalAt = critical;
            });
            BuildProperties();
        });
        if (!on) { Note("Off: the colors never change. Turn on to make this go amber at a warning level and red at a critical one."); return; }
        OptionalNumber("Amber at (blank = no warning step)", e.WarnAt, v => edit(x => x.WarnAt = v));
        OptionalNumber("Red at (blank = no critical step)", e.CriticalAt, v => edit(x => x.CriticalAt = v));
        Check("Low is bad (count down, for FPS or battery)", e.LowIsBad, v => edit(x => x.LowIsBad = v));
        DataPicker("Decided by", e.ColorBy, name => edit(x => x.ColorBy = name), own: "Its own reading");
        OptionalColorField("Warning color (blank = amber)", e.WarnColor, v => edit(x => x.WarnColor = v));
        OptionalColorField("Critical color (blank = red)", e.CriticalColor, v => edit(x => x.CriticalColor = v));
        Note("Temperatures compare in °C even when shown in °F. Tick Live values in the toolbar to see it react.");
    }

    /// <summary>
    /// A button showing the chosen reading that opens a searchable list in sections (CPU, GPU, Weather, Gaming…).
    /// With <paramref name="own"/>, the first entry clears the choice (null).
    /// </summary>
    private void DataPicker(string label, string? current, Action<string?> commit, string? own = null)
    {
        string Shown(string? name) => name == null ? own ?? "Pick a reading…" : Label(name) + (ThemeData.IsAvailable(name, _settings) ? "" : "  (not mapped)");
        var button = new Button { Content = Shown(current), HorizontalContentAlignment = System.Windows.HorizontalAlignment.Left, Padding = new Thickness(10, 6, 10, 6),
            Margin = new Thickness(0, 2, 0, 6), Background = Frozen(Color.FromRgb(11, 16, 28)) };
        var search = new TextBox { Margin = new Thickness(8, 8, 8, 4) };
        var list = new ListBox { Background = Brushes.Transparent, BorderThickness = new Thickness(0), MaxHeight = 380, Margin = new Thickness(4, 0, 4, 6) };
        ScrollViewer.SetHorizontalScrollBarVisibility(list, ScrollBarVisibility.Disabled);
        var popup = new Popup { PlacementTarget = button, Placement = PlacementMode.Bottom, StaysOpen = false, AllowsTransparency = true, Width = 320,
            Child = new Border { Background = Panel, BorderBrush = Frozen(Color.FromRgb(52, 62, 86)), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10),
                Child = new DockPanel { Children = { search, list } } } };
        DockPanel.SetDock(search, Dock.Top);
        void Fill()
        {
            list.Items.Clear();
            if (own != null && search.Text.Trim().Length == 0) list.Items.Add(new ListBoxItem { Content = own, Tag = "" });
            foreach (var (group, items) in ThemeDataGroups.Grouped(search.Text))
            {
                list.Items.Add(new ListBoxItem { Content = group.ToUpperInvariant(), IsEnabled = false, Foreground = Accent, FontSize = 11, FontWeight = FontWeights.SemiBold,
                    Margin = new Thickness(0, 6, 0, 0) });
                foreach (var (name, text) in items)
                    list.Items.Add(new ListBoxItem { Content = text + (ThemeData.IsAvailable(name, _settings) ? "" : "  (not mapped)"), Tag = name,
                        FontWeight = name == current ? FontWeights.SemiBold : FontWeights.Normal });
            }
        }
        search.TextChanged += (_, _) => Fill();
        void Pick(ListBoxItem? item)
        {
            if (item?.Tag is not string name) return;
            popup.IsOpen = false;
            string? picked = name.Length == 0 ? null : name;
            if (picked == current) return;
            current = picked;
            button.Content = Shown(picked);
            commit(picked);
            BuildProperties();
        }
        list.PreviewMouseLeftButtonUp += (_, _) => Pick(list.SelectedItem as ListBoxItem);
        list.KeyDown += (_, k) => { if (k.Key == Key.Enter) Pick(list.SelectedItem as ListBoxItem); };
        search.KeyDown += (_, k) =>
        {
            if (k.Key == Key.Down) { list.SelectedItem = list.Items.OfType<ListBoxItem>().FirstOrDefault(x => x.IsEnabled); (list.SelectedItem as ListBoxItem)?.Focus(); }
            if (k.Key == Key.Enter) Pick(list.Items.OfType<ListBoxItem>().FirstOrDefault(x => x.IsEnabled && x.Tag is string { Length: > 0 }));
        };
        button.Click += (_, _) => { search.Text = ""; Fill(); popup.IsOpen = true; search.Focus(); };
        Row(label, button);
    }
    /// <summary>Properties for a group or several elements: edits apply to all of them.</summary>
    private void BuildGroupProperties()
    {
        var indices = SelectionOrder;
        var members = indices.Select(k => _doc.Theme.Elements[k]).ToList();
        string? group = members.Select(m => m.Group).Distinct().Count() == 1 ? members[0].Group : null;
        bool wholeGroup = group != null && GroupOf(indices[0]).Count == indices.Count;
        void EditAll(Action<ThemeElement> change) => _doc.Change(t => { foreach (var k in indices) change(t.Elements[k]); });

        PanelTitle(wholeGroup ? "Group" : $"{members.Count} elements");
        Heading("POSITION");
        if (wholeGroup)
            Field("Group name", group!, v =>
            {
                v = v.Trim();
                bool taken = _doc.Theme.Elements.Where((_, k) => !_selection.Contains(k)).Any(x => string.Equals(x.Group, v, StringComparison.OrdinalIgnoreCase));
                if (v.Length is > 0 and <= 64 && !taken) EditAll(x => x.Group = v);
                BuildProperties();
            });
        var bounds = SelectionBounds();
        if (!bounds.IsEmpty)
        {
            Number("X", Math.Round(bounds.X), v => { double dx = Math.Round(v - bounds.X); if (dx != 0) EditAll(x => EditorGeometry.Move(x, dx, 0)); BuildProperties(); });
            Number("Y", Math.Round(bounds.Y), v => { double dy = Math.Round(v - bounds.Y); if (dy != 0) EditAll(x => EditorGeometry.Move(x, 0, dy)); BuildProperties(); });
            Number("Scale % (from the top-left corner)", 100, v =>
            {
                var origin = bounds.TopLeft;
                if (v > 0 && Math.Abs(v - 100) > 0.01) EditAll(x => EditorGeometry.Scale(x, origin, v / 100));
                BuildProperties();
            });
            Note($"Size {bounds.Width:0} × {bounds.Height:0}");
        }
        Check("Hidden", members.All(m => m.Hidden), v => EditAll(x => x.Hidden = v));

        // Text styling for all of them at once; icon glyphs keep their icon font.
        static bool Plain(ThemeElement x) => x.Font is { } f && !f.Family.Contains("Icons", StringComparison.OrdinalIgnoreCase);
        if (members.FirstOrDefault(Plain) is { Font: { } first })
        {
            Heading("ALL TEXT");
            string current = first.Family;
            var family = new ComboBox { IsEditable = true, ItemsSource = ThemeFonts.AvailableFamilies(), Text = current, Margin = new Thickness(0, 2, 0, 6) };
            void SetFamily(string f)
            {
                f = f.Trim();
                if (f.Length == 0 || f == current) return;
                current = f;
                EditAll(x => { if (Plain(x)) x.Font!.Family = f; });
            }
            family.LostKeyboardFocus += (_, _) => SetFamily(family.Text);
            family.SelectionChanged += (_, _) => { if (family.SelectedItem is string f) SetFamily(f); };
            Row("Font", family);
        }
        ColorTools(indices, byRole: true);

        Heading("ARRANGE");
        if (!wholeGroup) Action("Group these (Ctrl+G)", GroupSelection);
        if (members.Any(m => m.Group != null)) Action("Ungroup (Ctrl+Shift+G)", Ungroup);
        Note("Drag to move them together; drag a corner to scale them evenly. Double-click one on the canvas, or pick it in the list, to edit it alone. Duplicate copies the whole group.");
    }

    /// <summary>
    /// Color tools for many elements: set one part (text, borders…) on all of them, and swap any
    /// color they use for another everywhere at once. Each change is one undo step.
    /// </summary>
    private void ColorTools(IReadOnlyList<int> indices, bool byRole)
    {
        var members = indices.Select(k => _doc.Theme.Elements[k]).ToList();
        void Apply(Func<List<ThemeElement>, int> change)
        {
            // Try it on copies first so a change that does nothing leaves no empty undo step.
            int n = change(members.Select(m => ThemeDocument.Clone(m)).ToList());
            if (n > 0) _doc.Change(t => change(indices.Select(k => t.Elements[k]).ToList()));
            _status.Text = n == 0 ? "No colors changed." : $"Changed {n} color{(n == 1 ? "" : "s")}.";
            BuildProperties();
        }
        if (byRole && ThemeColors.Roles(members) is { Count: > 0 } roles)
        {
            Heading("COLORS FOR ALL OF THEM");
            foreach (var role in roles)
            {
                string shown = ThemeColors.Common(members, role)!;
                ColorField(role, shown, v => Apply(list => ThemeColors.SetRole(list, role, v, shown)));
            }
            Note("Sets that part on every selected element that has it. Each keeps its own opacity unless you change the opacity too (the first two digits).");
        }
        var palette = ThemeColors.Palette(members);
        if (palette.Count > 0)
        {
            Heading(byRole ? "COLORS IN USE HERE" : "THEME COLORS");
            foreach (var (color, uses) in palette.Take(16))
                ColorField($"{color[3..]} · used {uses}×", color, v => Apply(list => ThemeColors.Replace(list, color, v)));
            Note((byRole ? "Change one to swap it everywhere in the selection" : "Change one to swap it everywhere in the theme") +
                 ", keeping each use's own opacity." + (palette.Count > 16 ? $" Showing the 16 most used of {palette.Count}." : ""));
        }
    }

    /// <summary>Selects every element like the given one (all values, all panels…).</summary>
    private void SelectAlike(ThemeElement like) =>
        SelectMany(Enumerable.Range(0, _doc.Theme.Elements.Count).Where(k => ThemeColors.Alike(_doc.Theme.Elements[k], like)), -1);

    private void Shared(ThemeElement e, Action<Action<ThemeElement>> edit)
    {
        Number("Maximum", e.Maximum, v => edit(x => x.Maximum = Math.Max(0, v)));
        ColorField("Fill", e.ForeColor, v => edit(x => x.ForeColor = v));
        ColorField("Track", e.BackColor, v => edit(x => x.BackColor = v));
        OptionalColorField("Gradient to (blank = none)", e.GradientColor, v => edit(x => x.GradientColor = v));
        Number("Segment width (0 = solid)", e.SegmentWidth, v => edit(x => x.SegmentWidth = Math.Max(0, v)));
        Number("Segment gap", e.SegmentGap, v => edit(x => x.SegmentGap = Math.Max(0, v)));
    }

    private void Resize(int width, int height)
    {
        width = Math.Clamp(width, 64, 8192); height = Math.Clamp(height, 64, 8192);
        if (width != _doc.Theme.Width || height != _doc.Theme.Height) _doc.Change(t => { t.Width = width; t.Height = height; });
        BuildProperties();
    }

    /// <summary>Where property rows go: the panel itself, or the open section's body.</summary>
    private System.Windows.Controls.Panel _target = null!;

    /// <summary>Sections the user has folded away; remembered while the app runs.</summary>
    private static readonly HashSet<string> Folded = new(StringComparer.Ordinal);

    /// <summary>The panel's title (the element's kind); rows that follow go straight under it.</summary>
    private void PanelTitle(string text)
    {
        _properties.Children.Add(new TextBlock { Text = text, Foreground = Fore, FontSize = 18, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 2, 0, 6) });
        _target = _properties;
    }

    /// <summary>A section with a header that folds it; rows that follow go into it.</summary>
    private void Heading(string text)
    {
        var body = new StackPanel { Margin = new Thickness(0, 2, 0, 4), Visibility = Folded.Contains(text) ? Visibility.Collapsed : Visibility.Visible };
        var arrow = new TextBlock { Text = Folded.Contains(text) ? "\uE76C" : "\uE70D", FontFamily = new System.Windows.Media.FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),
            FontSize = 10, Foreground = Muted, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
        var header = new DockPanel { Background = Brushes.Transparent, Cursor = Cursors.Hand, Margin = new Thickness(0, 12, 0, 4) };
        DockPanel.SetDock(arrow, Dock.Right);
        header.Children.Add(arrow);
        header.Children.Add(new TextBlock { Text = text, Foreground = Accent, FontSize = 12, FontWeight = FontWeights.SemiBold });
        header.MouseLeftButtonUp += (_, _) =>
        {
            bool fold = body.Visibility == Visibility.Visible;
            if (fold) Folded.Add(text); else Folded.Remove(text);
            body.Visibility = fold ? Visibility.Collapsed : Visibility.Visible;
            arrow.Text = fold ? "\uE76C" : "\uE70D";
        };
        _properties.Children.Add(new Border { Height = 1, Background = Frozen(Color.FromArgb(0x1F, 0xFF, 0xFF, 0xFF)), Margin = new Thickness(0, 8, 0, 0) });
        _properties.Children.Add(header);
        _properties.Children.Add(body);
        _target = body;
    }

    private void Note(string text) =>
        _target.Children.Add(new TextBlock { Text = text, Foreground = Muted, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 6), FontSize = 11.5 });

    private void Row(string label, UIElement control)
    {
        _target.Children.Add(new TextBlock { Text = label, Foreground = Muted, FontSize = 11 });
        _target.Children.Add(control);
    }

    private void Action(string text, Action click)
    {
        var b = new Button { Content = text, Margin = new Thickness(0, 2, 0, 4), Padding = new Thickness(10, 5, 10, 5), HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch };
        b.Click += (_, _) => click();
        _target.Children.Add(b);
    }

    // Text inputs commit on Enter or when focus leaves, so typing is a single undo step.
    private void Field(string label, string value, Action<string> commit)
    {
        var box = new TextBox { Text = value, Margin = new Thickness(0, 2, 0, 6) };
        void Commit() { if (box.Text != value) { value = box.Text; commit(box.Text); } }
        box.LostKeyboardFocus += (_, _) => Commit();
        box.KeyDown += (_, e) => { if (e.Key == Key.Enter) Commit(); };
        Row(label, box);
    }

    private void Number(string label, double value, Action<double> commit) =>
        Field(label, value.ToString("0.##", CultureInfo.InvariantCulture), text =>
        {
            if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) && double.IsFinite(v)) commit(v);
            else BuildProperties();
        });

    private void Check(string label, bool value, Action<bool> commit)
    {
        var box = new CheckBox { Content = new TextBlock { Text = label, TextWrapping = TextWrapping.Wrap }, IsChecked = value, Foreground = Fore, Margin = new Thickness(0, 2, 0, 6) };
        box.Click += (_, _) => commit(box.IsChecked == true);
        _target.Children.Add(box);
    }

    private void Choice(string label, string[] options, string value, Action<string> commit)
    {
        var combo = new ComboBox { ItemsSource = options, SelectedItem = options.FirstOrDefault(o => o.Equals(value, StringComparison.OrdinalIgnoreCase)), Margin = new Thickness(0, 2, 0, 6) };
        combo.SelectionChanged += (_, _) => { if (combo.SelectedItem is string s && s != value) { value = s; commit(s); } };
        Row(label, combo);
    }

    /// <summary>A number that may be left blank (null).</summary>
    private void OptionalNumber(string label, double? value, Action<double?> commit) =>
        Field(label, value?.ToString("0.##", CultureInfo.InvariantCulture) ?? "", text =>
        {
            if (text.Trim().Length == 0) commit(null);
            else if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) && double.IsFinite(v)) commit(v);
            else BuildProperties();
        });

    /// <summary>A color that may be left blank (null: none, or the default), with a picker.</summary>
    private void OptionalColorField(string label, string? value, Action<string?> commit) => ColorField(label, value ?? "", v => commit(v.Length == 0 ? null : v), optional: true);

    private void ColorField(string label, string value, Action<string> commit, bool optional = false)
    {
        var panel = new DockPanel { Margin = new Thickness(0, 2, 0, 6) };
        var swatch = new Button { Width = 30, Margin = new Thickness(4, 0, 0, 0), ToolTip = "Pick a color",
            Background = new SolidColorBrush(ThemeView.ParseColor(value, Colors.Transparent)), BorderBrush = Muted };
        var box = new TextBox { Text = value };
        void Commit(string text)
        {
            if (optional && text.Trim().Length == 0)
            {
                if (value.Length == 0) return;
                value = ""; box.Text = ""; swatch.Background = Brushes.Transparent;
                commit("");
                return;
            }
            var color = ThemeView.ParseColor(text, Color.FromArgb(0, 1, 2, 3));
            if (color == Color.FromArgb(0, 1, 2, 3)) { box.Text = value; return; } // not a color
            string hex = $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
            if (hex == value) return;
            value = hex; box.Text = hex; swatch.Background = new SolidColorBrush(color);
            commit(hex);
        }
        box.LostKeyboardFocus += (_, _) => Commit(box.Text);
        box.KeyDown += (_, e) => { if (e.Key == Key.Enter) Commit(box.Text); };
        swatch.Click += (_, _) =>
        {
            var current = ThemeView.ParseColor(value, Colors.White);
            using var dialog = new System.Windows.Forms.ColorDialog { FullOpen = true, Color = System.Drawing.Color.FromArgb(current.R, current.G, current.B) };
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                Commit($"#{(current.A == 0 ? 255 : current.A):X2}{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}");
        };
        DockPanel.SetDock(swatch, Dock.Right);
        panel.Children.Add(swatch);
        panel.Children.Add(box);
        Row(label, panel);
    }

    private static SolidColorBrush Frozen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
