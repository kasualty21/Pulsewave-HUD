using System.Diagnostics;
using SystemIcons = System.Drawing.SystemIcons;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Brush = System.Windows.Media.Brush;
using System.Windows.Threading;
using Forms = System.Windows.Forms;
using Point = System.Windows.Point;
using Pulsewave.Themes;

namespace Pulsewave;

public partial class MainWindow : Window
{
    private const int GwlExStyle = -20;
    private const long WsExTransparent = 0x00000020L;
    private const long WsExToolWindow = 0x00000080L;
    private const long WsExNoActivate = 0x08000000L;
    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpFrameChanged = 0x0020;
    private const int WmMouseActivate = 0x0021;
    private const int WmDisplayChange = 0x007E;
    private const int MaNoActivate = 3;

    private Brush? _mintBrush, _blueBrush, _lavenderBrush, _amberBrush, _dangerBrush;
    private readonly NowPlayingMonitor _media = new();
    private NowPlaying? _shownMedia;
    private Brush? _artPlaceholder;
    private PlexTokenForm? _plexTokenForm;
    private ThemeView? _themeView;
    private SensorDetailView? _detail;
    private AppDrawerView? _drawer;
    private LightsView? _lightsView;
    private LightsForm? _lightsForm;
    private readonly Lights _lights = new();
    private readonly Scheduler _scheduler = new();
    private readonly AestheticMode _aesthetic = new();
    private readonly GameTracker _games = new(GameTracker.DefaultFile);
    private List<GameEntry> _gameCatalog = new();
    private DateTime _catalogDue, _gameCheckDue;
    private bool _catalogBuilding;
    private readonly GameSuggester _suggester = new();
    private readonly List<string> _suggestions = new();
    private GamingForm? _gamingForm;
    private System.Windows.Controls.Border? _suggestionCard;
    private (GameEntry Game, string Program)? _running;
    private readonly Dictionary<string, string> _gamePrograms = new();
    private bool _showsGaming;
    private MixerView? _mixer;
    private MediaView? _mediaView;
    private PaletteView? _paletteView;
    private GamingView? _gamingView;
    private DateTime _queueDue;
    private string _queueItem = "";
    private double _aestheticShown = 1; // the HUD's current aesthetic visibility (1 = fully shown)
    private ScheduleForm? _scheduleForm;
    private WeatherForm? _weatherForm;
    private WeatherReport? _weather;
    private DateTime _weatherDue;
    private bool _showsWeather, _weatherFetching;
    private bool _screenBlank;
    private DateTime _wakeUntil;
    private string _scheduleStatus = "";
    private string? _poweredDown; // the display put in standby by the schedule
    private IReadOnlyList<ShortcutView?> _shortcutViews = [];
    // The shown theme's shortcut list ("Gaming", "Productivity" or null for the main list), the slot each of its
    // tiles opens (tile 1 first) with their views, and the app drawer's order.
    private string? _shortcutList;
    private List<int> _tileSlots = [];
    private IReadOnlyList<ShortcutView?> _tileViews = [];
    private List<int> _drawerSlots = [];
    private ShortcutsForm? _shortcutsForm;
    private readonly AiUsageReader _usage = new();
    private static readonly System.Net.Http.HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };
    private bool _showsClaude, _claudeFetching;
    private DateTime _claudeNext;
    private ThemesForm? _themesForm;
    private PlaylistForm? _playlistForm;
    private GlobalHotkeys? _hotkeys;
    private const int HotkeyNext = 1, HotkeyPrevious = 2, HotkeyToggle = 3, HotkeyAesthetic = 4;
    private readonly DispatcherTimer _playlistTimer = new();
    private SensorSnapshot _lastValues = new();
    private string? _settingsLoadError;
    private MonitorSettings _settings;
    private SensorReader _reader;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly TelemetryEngine _telemetry = new();
    private Forms.NotifyIcon? _tray;
    private System.Drawing.Icon? _trayIcon;
    private Forms.ContextMenuStrip? _trayMenu;
    private SensorManagerForm? _sensorManager;
    private TelemetryForm? _telemetryForm;
    private IntPtr _hwnd;
    private bool _userHidden;
    private bool _targetMissing;
    private readonly DispatcherTimer _displayRecovery = new() { Interval = TimeSpan.FromSeconds(2) };
    private DateTime _lastDisplayCheck;
    private System.Drawing.Rectangle _screen = new(0, 0, 1920, 480);
    /// <summary>The HUD's own size: the display area, turned when the HUD is rotated 90 or 270 degrees.</summary>
    private System.Drawing.Size Shown => Rotated(_screen.Size, _settings.HudRotation);

    internal static System.Drawing.Size Rotated(System.Drawing.Size size, int degrees) =>
        degrees is 90 or 270 ? new System.Drawing.Size(size.Height, size.Width) : size;

    private readonly TouchTimers _timers = new();
    private bool _showsVolume;
    private readonly DispatcherTimer _guard = new() { Interval = TimeSpan.FromSeconds(2) };
    private Point? _pressAt;
    private DateTime _pressTime;
    private Point? _touchAt; // set while a touch is replayed as a click
    private System.Drawing.Point? _cursorBefore;
    private AppBar? _appBar;
    private string? _dockKey;
    private System.Drawing.Rectangle _docked;
    private DateTime _dockedAt;
    private bool _fullScreenApp;

    public MainWindow()
    {
        InitializeComponent();
        Opacity = 0; // suppress the first frame until display placement / tray-only state is applied
        try { _settings = SettingsStore.Load(); }
        catch (Exception ex) { _settings = new MonitorSettings(); _settingsLoadError = ex.Message; } // Save guard preserves original.
        _reader = new SensorReader(_settings);
        _shortcutViews = _settings.Shortcuts.Select(ShortcutView.From).ToList();
        ArrangeShortcuts();
        _media.PlexToken = PlexTokenStore.Load();
        // The smart builder (and its encrypted xAI key) was removed before release; delete a leftover key.
        try { System.IO.File.Delete(System.IO.Path.Combine(SettingsStore.DirectoryPath, "xai-key.dat")); }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException) { }
        _userHidden = _settings.StartHidden;
        UndoLeftoverDisplayChanges();
        _displayRecovery.Tick += (_, _) =>
        {
            _displayRecovery.Stop();
            PlaceOnTargetMonitor();
            ApplyInputStyle();
            RebuildTrayMenu();
        };
        _timer.Interval = TimeSpan.FromMilliseconds(_settings.PollIntervalMs);
        _telemetry.ThresholdCrossed += message =>
            _tray?.ShowBalloonTip(5000, "Pulsewave · telemetry alert", message, Forms.ToolTipIcon.Warning);
        SourceInitialized += OnSourceInitialized;
        // A hidden overlay should not keep decoding a theme's background video.
        IsVisibleChanged += (_, _) => { if (IsVisible) _themeView?.Resume(); else _themeView?.Pause(); };
        Loaded += OnLoaded;
        Closing += OnClosing;
        _timer.Tick += (_, _) => RefreshReadings();
        // The playlist waits while a game or a schedule period with a theme is in control (0.10).
        _playlistTimer.Tick += (_, _) => { if (!ThemeDirector.PausesPlaylist(CurrentMoment(DateTime.Now))) StepTheme(+1, byHand: false); };
        Root.PreviewMouseLeftButtonDown += (_, e) => { _pressAt = e.GetPosition(Root); _pressTime = DateTime.Now; };
        Root.MouseLeftButtonUp += OnTap;
        _guard.Tick += (_, _) => GuardDisplay();
        // With click-through off, programs, shortcuts and links dropped on the HUD become shortcuts.
        AllowDrop = true;
        DragOver += (_, e) => { e.Effects = Dropped(e.Data).Count > 0 ? System.Windows.DragDropEffects.Copy : System.Windows.DragDropEffects.None; e.Handled = true; };
        Drop += OnDrop;
        BuildTray();
    }

    private void OnSourceInitialized(object? sender, EventArgs args)
    {
        _hwnd = new WindowInteropHelper(this).Handle;
        HwndSource.FromHwnd(_hwnd)?.AddHook(WindowProc);
        _appBar = new AppBar(_hwnd);
        TouchInput.DisableFeedback(_hwnd);
        ApplyInputStyle();
        _hotkeys = new GlobalHotkeys(_hwnd);
        if (RegisterHotkeys() is { Count: > 0 } problems)
            _tray?.ShowBalloonTip(6000, "Some theme hotkeys are not active", string.Join("\n", problems), Forms.ToolTipIcon.Warning);
    }

    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        PlaceOnTargetMonitor();
        Opacity = 1;
        UpdateHistoryCaptions();
        ApplyTheme();
        RestartPlaylist();
        RefreshReadings();
        _timer.Start();
        UpdateGuard();
        if (_settingsLoadError != null) _tray?.ShowBalloonTip(8000, "Settings need attention · temporary defaults", _settingsLoadError, Forms.ToolTipIcon.Warning);
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs args)
    {
        _timer.Stop();
        _playlistTimer.Stop();
        _games.End(); // the session being played is saved
        _hotkeys?.UnregisterAll();
        _appBar?.Undock(); // give the screen edge back
        _guard.Stop();
        _playlistForm?.Close();
        _displayRecovery.Stop();
        _media.Dispose();
        _sensorManager?.Close();
        _telemetryForm?.Close();
        _plexTokenForm?.Close();
        _themesForm?.Close();
        _shortcutsForm?.Close();
        _lightsForm?.Close();
        _scheduleForm?.Close();
        if (_poweredDown is { } standby) { _poweredDown = null; DisplayPower.TurnOn(standby); } // never leave the display off
        if (_backlightBefore is { } dimmed) { _backlightBefore = null; DisplayPower.SetBrightness(dimmed.Device, dimmed.Brightness); } // nor dimmed
        SaveDisplayRestore(); // nothing left to undo: the crash record goes
        _themeView?.Dispose();
        if (_tray != null) { _tray.Visible = false; _tray.Dispose(); _tray = null; }
        _trayIcon?.Dispose();
        _trayMenu?.Dispose();
    }

    private IntPtr WindowProc(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == TouchInput.WmPointerActivate)
        {
            handled = true;
            return new IntPtr(TouchInput.PaNoActivate);
        }
        if (TouchInput.IsContact(message) && TouchInput.IsTouchOrPen(wParam))
        {
            // Handled here, so Windows never turns the touch into mouse input that moves the cursor.
            handled = true;
            var (primary, screen) = TouchInput.Decode(wParam, lParam);
            if (primary) OnTouch(message, screen);
            return IntPtr.Zero;
        }
        if (message == WmMouseActivate)
        {
            handled = true;
            return new IntPtr(MaNoActivate);
        }
        if (message == GlobalHotkeys.WmHotkey)
        {
            int id = wParam.ToInt32();
            Dispatcher.BeginInvoke(() => OnHotkey(id)); // not from inside the window procedure
            handled = true;
            return IntPtr.Zero;
        }
        if (_appBar != null && message == _appBar.CallbackMessage)
        {
            int notice = wParam.ToInt32();
            if (notice == AppBar.FullScreenApp)
            {
                // A full-screen game or video on this display: stop staying on top of it until it closes.
                _fullScreenApp = lParam != IntPtr.Zero;
                Dispatcher.BeginInvoke(ApplyInputStyle);
            }
            else if (notice == AppBar.PositionChanged && (DateTime.Now - _dockedAt).TotalSeconds > 1)
            {
                // The taskbar or another app bar moved; take the edge again (ignoring the echo of our own move).
                _dockKey = null;
                Dispatcher.BeginInvoke(PlaceOnTargetMonitor);
            }
            handled = true;
            return IntPtr.Zero;
        }
        if (message is WmDisplayChange or 0x0219 or 0x02E0 or 0x0218)
        {
            // Debounce topology, device, DPI and resume broadcasts; drivers can settle late.
            _displayRecovery.Stop();
            _displayRecovery.Start();
        }
        return IntPtr.Zero;
    }

    private void ApplyInputStyle()
    {
        if (_hwnd == IntPtr.Zero) return;
        long style = GetWindowLongPtr(_hwnd, GwlExStyle).ToInt64();
        style |= WsExToolWindow | WsExNoActivate;
        if (_settings.ClickThrough) { style |= WsExTransparent; HideDetails(); }
        else style &= ~WsExTransparent;
        SetWindowLongPtr(_hwnd, GwlExStyle, new IntPtr(style));
        SetWindowPos(_hwnd, IntPtr.Zero, 0, 0, 0, 0,
            0x0001 | 0x0002 | SwpNoZOrder | SwpNoActivate | SwpFrameChanged);
        // A docked HUD stays on top like the taskbar, except over a full-screen app on its display.
        bool docked = _appBar?.Docked == true;
        Topmost = (_settings.AlwaysOnTop || docked) && !(docked && _fullScreenApp);
    }

    private void Undock()
    {
        _appBar?.Undock();
        _dockKey = null;
        _fullScreenApp = false;
    }

    private void PlaceOnTargetMonitor()
    {
        if (_hwnd == IntPtr.Zero) return;
        var screens = Forms.Screen.AllScreens;
        if (screens.Length == 0) { _targetMissing = true; Undock(); Hide(); return; }
        var target = !string.IsNullOrWhiteSpace(_settings.MonitorInterfaceId)
            ? screens.FirstOrDefault(s => string.Equals(DisplayIdentity.For(s), _settings.MonitorInterfaceId, StringComparison.OrdinalIgnoreCase))
            : screens.FirstOrDefault(s => s.DeviceName == _settings.MonitorDeviceName)
                     ?? screens.FirstOrDefault(s => DisplaySizes.IsHudPanel(s.Bounds.Width, s.Bounds.Height))
                     ?? screens.FirstOrDefault(s => !s.Primary)
                     ?? screens[0];
        _targetMissing = target == null;
        if (target == null) { Undock(); Hide(); return; }
        // Migrate the legacy display selection once, retaining sensor settings unchanged.
        if (_settingsLoadError == null && string.IsNullOrWhiteSpace(_settings.MonitorInterfaceId))
        {
            var identity = DisplayIdentity.For(target);
            if (!string.IsNullOrWhiteSpace(identity))
            {
                _settings.MonitorInterfaceId = identity;
                _settings.MonitorDeviceName = target.DeviceName;
                try { SettingsStore.Save(_settings); }
                catch (Exception ex) { _tray?.ShowBalloonTip(4000, "Display preference not saved", ex.Message, Forms.ToolTipIcon.Warning); }
            }
        }
        if (!_userHidden && !IsVisible) Show();
        if (_userHidden && IsVisible) Hide();
        // Dock mode takes a slice along one edge of the display (reserved like the taskbar);
        // it is re-reserved only when the display, edge or size changes, or the shell asks.
        var bounds = target.Bounds;
        if (_settings.DockEdge.Length > 0 && !_userHidden && _appBar != null)
        {
            // Classic is a 1920x480 layout; fitting the dock follows the shown theme's shape.
            var (themeWidth, themeHeight) = _themeView is { } shown ? (shown.Theme.Width, shown.Theme.Height) : (1920, 480);
            if (_settings.HudRotation is 90 or 270) (themeWidth, themeHeight) = (themeHeight, themeWidth); // the theme lies on its side
            string key = $"{target.Bounds}|{_settings.DockEdge}|{_settings.DockPercent}" + (_settings.DockPercent == 0 ? $"|{themeWidth}x{themeHeight}" : "");
            if (key != _dockKey || !_appBar.Docked)
            {
                _docked = _appBar.Dock(target.Bounds, _settings.DockEdge, _settings.DockPercent, themeWidth, themeHeight);
                _dockKey = key;
                _dockedAt = DateTime.Now;
                ApplyInputStyle();
            }
            bounds = _docked;
        }
        else if (_appBar?.Docked == true) { Undock(); ApplyInputStyle(); }
        if (_screen != bounds) { _screen = bounds; FitLayout(); }

        // Use Win32 physical pixels, not WPF device-independent units.
        // Works when the target display has 100%, 125%, or mixed DPI scaling.
        SetWindowPos(_hwnd, IntPtr.Zero,
            bounds.X, bounds.Y, bounds.Width, bounds.Height,
            SwpNoZOrder | SwpNoActivate);
    }

    private void RefreshReadings()
    {
        var now = DateTime.Now;
        if ((now - _lastDisplayCheck).TotalSeconds >= 10)
        {
            _lastDisplayCheck = now;
            PlaceOnTargetMonitor(); // also recover if a driver drops the broadcast
        }
        var values = _reader.Read();
        _sensorFeed.Update(values, now);
        _lastValues = values;
        _telemetry.Ingest(now, values, _settings);
        TrackGames(now, values);
        if (DetailHost.Visibility == Visibility.Visible && DetailHost.Child == _detail) _detail?.Update(now);
        UpdateMixer();
        UpdateMediaPage();
        if (_gamingView != null && DetailHost.Child == _gamingView && DetailHost.Visibility == Visibility.Visible) _gamingView.Show(_games.Snapshot(now, GameTile));
        RunSchedule(now);
        DirectTheme(now);
        ApplyAesthetic(now);
        if (_timers.Finished(now))
        {
            System.Media.SystemSounds.Exclamation.Play();
            _tray?.ShowBalloonTip(5000, "Pulsewave", "Timer done", Forms.ToolTipIcon.Info);
        }
        if (BlankCover.Visibility == Visibility.Visible || _aestheticShown == 0) return; // nothing to draw while the screen is off or hidden
        if (_themeView != null)
        {
            if (_showsClaude) PollClaudeAccount(now);
            if (_showsWeather) PollWeather(now);
            _themeView.Update(ThemeContext(now, values));
            return; // the Classic layout is hidden; skip its updates
        }

        ClockTime.Text = now.ToString("HH:mm");
        ClockSeconds.Text = now.ToString("ss");
        ClockDate.Text = now.ToString("dddd, MMMM d").ToUpperInvariant();
        var uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);
        UptimeLabel.Text = $"{(int)uptime.TotalDays:00}D  {uptime.Hours:00}H  {uptime.Minutes:00}M";
        UpdateNowPlaying();
        CpuName.Text = HardwareNameReader.Cpu(values, _settings);
        GpuName.Text = HardwareNameReader.Gpu(values, _settings);
        CpuLoad.Text = Display(values.Get("CpuLoad"), "F0");
        CpuTemp.Text = Display(values.Get("CpuTemp"), "F0", "°C");
        CpuPower.Text = Display(values.Get("CpuPower"), "F0", " W");
        CpuFan.Text = Display(values.Get("CpuFan"), "F0", " RPM");
        GpuLoad.Text = Display(values.Get("GpuLoad"), "F0");
        GpuTemp.Text = Display(values.Get("GpuTemp"), "F0", "°C");
        GpuPower.Text = Display(values.Get("GpuPower"), "F0", " W");
        GpuFps.Text = Display(values.Get("GpuFps"), "F0");
        MemoryLoad.Text = Display(values.Get("MemoryLoad"), "F0", "%");
        DownRate.Text = Display(values.Get("NetDownload"), "F2", " MB/s");
        UpRate.Text = Display(values.Get("NetUpload"), "F2", " MB/s");
        // Convert only explicitly MB-formatted HWiNFO VRAM values. Other units stay untouched.
        var vramText = values.GetText("GpuVram");
        var vramMb = vramText?.Contains("MB", StringComparison.OrdinalIgnoreCase) == true
            ? SensorReader.ParseNumber(vramText) : null;
        GpuVram.Text = vramMb is > 0 ? $"{vramMb / 1024:0.0} GB"
            : string.IsNullOrWhiteSpace(vramText) ? "--" : vramText;
        MemoryFill.Width = values.Get("MemoryLoad") is { } mem ? 438 * Math.Clamp(mem, 0, 100) / 100 : 0;
        // Colors communicate urgency without relying on tiny captions.
        var warning = _amberBrush ??= (Brush)FindResource("Amber");
        var critical = _dangerBrush ??= (Brush)FindResource("Danger");
        CpuTemp.Foreground = WarningBrush(values.Get("CpuTemp"), _settings.CpuTemperatureAlertC, _mintBrush ??= (Brush)FindResource("Mint"), warning, critical);
        GpuTemp.Foreground = WarningBrush(values.Get("GpuTemp"), _settings.GpuTemperatureAlertC, _blueBrush ??= (Brush)FindResource("Blue"), warning, critical);
        var memoryColor = WarningBrush(values.Get("MemoryLoad"), _settings.MemoryAlertPercent, _lavenderBrush ??= (Brush)FindResource("Lavender"), warning, critical);
        MemoryLoad.Foreground = memoryColor;
        MemoryFill.Background = memoryColor;

        UpdateGraph(CpuGraph, _telemetry.History("CpuLoad", now, _settings.HistorySeconds),
            now, _settings.HistorySeconds);
        UpdateGraph(GpuGraph, _telemetry.History("GpuLoad", now, _settings.HistorySeconds),
            now, _settings.HistorySeconds);

        SensorCount.Text = $"{values.ValidCount:00} / {values.ExpectedCount:00} OK";
        HeaderStatus.Text = values.ValidCount == 0 ? "SENSORS OFFLINE"
            : _sensorFeed.State == SensorFeedState.Frozen ? "SENSORS PAUSED"
            : values.ValidCount == values.ExpectedCount ? "LIVE SENSORS" : "PARTIAL SENSOR FEED";
    }

    /// <summary>Whether HWiNFO's feed is live; updated once per sensor read.</summary>
    private readonly SensorFeed _sensorFeed = new();

    private ThemeDataContext ThemeContext(DateTime now, SensorSnapshot values)
    {
        var fps = _telemetry.History("GpuFps", now, 60).Where(p => p.Value.HasValue).Select(p => p.Value!.Value).ToList();
        return new ThemeDataContext
        {
            Sensors = values, Now = now, SensorFeed = _sensorFeed.State,
            CpuName = HardwareNameReader.Cpu(values, _settings), GpuName = HardwareNameReader.Gpu(values, _settings),
            AverageFps = fps.Count > 0 ? fps.Average() : null,
            Media = _media.Current,
            MediaPosition = _media.Plexamp is { DurationMs: > 0 } timeline
                ? (TimeSpan.FromMilliseconds(Math.Min(timeline.TimeMs, timeline.DurationMs)), TimeSpan.FromMilliseconds(timeline.DurationMs)) : null,
            Shortcuts = _tileViews,
            Usage = _usage.Read(now), // local files, read at most every 15 s
            Volume = _showsVolume ? AudioVolume.Read() : null,
            Timer = _timers.TimerLeft(now),
            Stopwatch = _timers.StopwatchElapsed(now),
            TimerRunning = _timers.TimerRunning, StopwatchRunning = _timers.StopwatchRunning,
            TimerPhase = _timers.Phase, StopwatchShown = _timers.StopwatchShown,
            Gaming = _showsGaming ? _games.Snapshot(now, GameTile) : null,
            Weather = _showsWeather && _weather != null && _weather.City == _settings.Weather.City ? _weather with { Fahrenheit = _settings.Weather.Fahrenheit } : null
        };
    }

    /// <summary>Shows the selected custom theme, or the Classic layout when none is set or it fails to load.</summary>
    // --- Which theme shows (0.10) ---------------------------------------------------------------
    // _settings.ActiveTheme is the user's own theme (their picks and the playlist). A game or a schedule period can
    // show another over it (_autoTheme) without changing it, so when they end the user's theme simply returns.
    private readonly ThemeDirector _director = new();
    private string? _autoTheme;

    /// <summary>The theme on the HUD now: a game's or the schedule's while one is in control, else the user's own.</summary>
    private string ShownTheme => _autoTheme ?? _settings.ActiveTheme;

    /// <summary>What is running now, for the director: the game session (with its theme) and the schedule period showing a theme.</summary>
    private ThemeDirector.Moment CurrentMoment(DateTime now)
    {
        string? game = _games.CurrentKey;
        string? gameTheme = ThemeDirector.GameTheme(_settings.Gaming, game);
        var period = ThemeDirector.SchedulePeriod(_settings.Schedule, now);
        return new(game, Installed(gameTheme) ? gameTheme : null, period?.Key, Installed(period?.Theme) ? period?.Theme : null);
    }

    /// <summary>A theme that can be shown (a template of this version or a theme folder); a deleted one is ignored, never a blank HUD.</summary>
    private static bool Installed(string? id) => !string.IsNullOrEmpty(id) &&
        (BuiltInTemplates.IsBuiltIn(id) ? BuiltInTemplates.Names.Contains(id[BuiltInTemplates.Prefix.Length..])
                                        : System.IO.Directory.Exists(System.IO.Path.Combine(ThemeStore.RootPath, id)));

    /// <summary>Each second: switches to the game's or the schedule's theme, or back to the user's, when that changes. Never shows a hidden HUD.</summary>
    private void DirectTheme(DateTime now)
    {
        string? want = _director.Decide(CurrentMoment(now));
        if (want == _autoTheme) return;
        _autoTheme = want;
        ApplyTheme();
    }

    /// <summary>
    /// A theme the user chose (gallery, tray, swipe, hotkey, a theme's buttons): it becomes their own theme and outranks
    /// the game and schedule period running now until those end.
    /// </summary>
    private void PickTheme(string id)
    {
        _director.HandPicked(CurrentMoment(DateTime.Now));
        bool shownChanges = id != ShownTheme;
        _autoTheme = null;
        if (id != _settings.ActiveTheme) SavePreference(() => _settings.ActiveTheme = id, ApplyTheme);
        else if (shownChanges) ApplyTheme();
    }

    private void ApplyTheme()
    {
        _themeView?.Dispose();
        _themeView = null;
        _showsClaude = _showsVolume = false;
        _shortcutList = null;
        ThemeHost.Child = null;
        string shown = ShownTheme;
        if (!string.IsNullOrEmpty(shown))
        {
            try
            {
                string dir = BuiltInTemplates.IsBuiltIn(shown)
                    ? BuiltInTemplates.Ensure(shown) ?? throw new System.IO.InvalidDataException("This template is not part of this version.")
                    : System.IO.Path.Combine(ThemeStore.RootPath, shown);
                var theme = ThemeStore.Load(dir);
                if (ThemePalettes.Find(_settings.ThemePalettes.GetValueOrDefault(shown)) is { } palette) ThemePalettes.Apply(theme, palette);
                _shortcutList = ShortcutLists.Canonical(theme.ShortcutList);
                _themeView = new ThemeView(theme, dir);
                if (_dim is { Mode: "Theme" or "Both" }) _themeView.SetGlows(false);
                ThemeHost.Child = _themeView;
                // Media is read only while the theme shows it.
                if (theme.Elements.Any(e => e.Kind == ThemeElementKind.AlbumArt ||
                                            e.DataName?.StartsWith("MEDIA_", StringComparison.OrdinalIgnoreCase) == true))
                    _media.Start();
                else _media.Stop();
                _shownMedia = null;
                _showsClaude = theme.Elements.Any(e => e.DataName?.StartsWith("CLAUDE_", StringComparison.OrdinalIgnoreCase) == true);
                _showsVolume = theme.Elements.Any(e => ThemeData.IsVolume(e.DataName));
                _showsWeather = theme.Elements.Any(e => ThemeData.IsWeather(e.DataName));
                _showsGaming = theme.Elements.Any(e => ThemeData.IsGaming(e.DataName) || e.Kind == ThemeElementKind.Shortcut && e.Slot == ThemeDataContext.GameSlot);
            }
            catch (Exception ex) when (ex is System.IO.IOException or System.IO.InvalidDataException or
                                       System.Text.Json.JsonException or UnauthorizedAccessException or UriFormatException)
            {
                _tray?.ShowBalloonTip(6000, "Theme not loaded · showing Classic", ex.Message, Forms.ToolTipIcon.Warning);
            }
        }
        ArrangeShortcuts();
        bool themed = _themeView != null;
        ThemeHost.Visibility = themed ? Visibility.Visible : Visibility.Collapsed;
        ClassicView.Visibility = themed ? Visibility.Collapsed : Visibility.Visible;
        // A dock sized to fit the theme follows a theme of another shape.
        if (_appBar?.Docked == true && _settings.DockPercent == 0) PlaceOnTargetMonitor();
        FitLayout();
        if (themed && !IsVisible) _themeView!.Pause();
        if (IsLoaded) RefreshReadings();
    }

    /// <summary>
    /// Classic fills a bar-shaped display and keeps its shape elsewhere; a theme of another shape
    /// than the display is letterboxed with its own background color, not the desktop.
    /// </summary>
    private void FitLayout()
    {
        HideDetails(); // it is laid out for the display's shape
        // Rotating the whole HUD (themes, Classic, details) lets a display Windows shows sideways run upright themes;
        // taps and drops are read through the same turn.
        Root.LayoutTransform = _settings.HudRotation == 0 ? Transform.Identity : new RotateTransform(_settings.HudRotation);
        ClassicView.Stretch = DisplaySizes.SameShape(Shown.Width, Shown.Height, 1920, 480) ? Stretch.Fill : Stretch.Uniform;
        Root.Background = _themeView is { } view ? new SolidColorBrush(ThemeView.ParseColor(view.Theme.BackgroundColor, Colors.Black)) : System.Windows.Media.Brushes.Transparent;
        ApplyAesthetic(DateTime.Now, animate: false, force: true); // a new theme or shape starts at the current level
    }

    // --- Touch and click controls ------------------------------------------------------------

    /// <summary>
    /// A finger on the HUD (click-through off). The press starts a possible swipe; the lift is replayed
    /// as a click at that point (see <see cref="TouchInput"/>), so the mouse cursor never moves.
    /// </summary>
    private void OnTouch(int message, System.Drawing.Point screen)
    {
        if (PresentationSource.FromVisual(Root) == null) return;
        var at = Root.PointFromScreen(new Point(screen.X, screen.Y));
        if (message == TouchInput.WmPointerDown)
        {
            _pressAt = at;
            _pressTime = DateTime.Now;
            _cursorBefore = TouchInput.Cursor();
            // On a page, a finger may drag a slider or seek bar, or scroll the list under it; which one is
            // decided once it has moved a little, by the direction it moves.
            _glide.Stop();
            _drag = null;
            _slide = null;
            if (DetailHost.Visibility == Visibility.Visible)
            {
                var (slider, list) = DragTargetsAt(at);
                if (list != null) _drag = new ListDrag(list, list.VerticalOffset, Root.TranslatePoint(at, list).Y);
                _slide = slider;
            }
            _sliding = false;
        }
        else if (message == TouchInput.WmPointerUpdate && _pressAt is { } pressed)
        {
            double dx = Math.Abs(at.X - pressed.X), dy = Math.Abs(at.Y - pressed.Y);
            bool decided = _sliding || _drag is { Moving: true };
            if (!decided && Math.Max(dx, dy) > 12)
            {
                if (_slide != null && (_slide.Vertical ? dy >= dx : dx > dy)) { _sliding = true; _drag = null; }
                else if (_drag != null && dy > dx) { _drag.Moving = true; _slide = null; }
                else { _slide = null; _drag = null; }
            }
            if (_sliding && _slide is UIElement element) _slide.DragTo(Root.TranslatePoint(at, element), final: false);
            else if (_drag is { Moving: true } drag)
            {
                double y = Root.TranslatePoint(at, drag.List).Y;
                drag.List.ScrollToVerticalOffset(drag.StartOffset - (y - drag.StartY));
                drag.Track(y, Environment.TickCount64);
            }
        }
        else if (message == TouchInput.WmPointerUp)
        {
            // Not from inside the window procedure: a tap can open windows or change the theme.
            var dragged = _drag is { Moving: true } d ? d : null;
            var slid = _sliding ? _slide : null;
            _drag = null;
            _slide = null;
            _sliding = false;
            Dispatcher.BeginInvoke(() =>
            {
                // A slider drag ends where the finger lifts, without a tap.
                if (slid is UIElement slidElement)
                {
                    slid.DragTo(Root.TranslatePoint(at, slidElement), final: true);
                    _pressAt = null;
                    TouchInput.RestoreCursor(_hwnd, _cursorBefore);
                    _cursorBefore = null;
                    return;
                }
                // A drag scrolled the list instead of tapping a row; a flick keeps it gliding.
                if (dragged != null)
                {
                    StartGlide(dragged.List, dragged.Velocity(Environment.TickCount64));
                    _pressAt = null;
                    TouchInput.RestoreCursor(_hwnd, _cursorBefore);
                    _cursorBefore = null;
                    return;
                }
                _touchAt = at;
                try { TouchInput.Tap(Root, at); }
                finally { _touchAt = null; }
                TouchInput.RestoreCursor(_hwnd, _cursorBefore);
                _cursorBefore = null;
            });
        }
    }

    /// <summary>A finger dragging a list: where it started and its recent movement, for a flick's speed.</summary>
    private sealed class ListDrag(System.Windows.Controls.ScrollViewer list, double startOffset, double startY)
    {
        private readonly Queue<(double Y, long Ms)> _recent = new();
        public System.Windows.Controls.ScrollViewer List { get; } = list;
        public double StartOffset { get; } = startOffset;
        public double StartY { get; } = startY;
        public bool Moving { get; set; }

        public void Track(double y, long ms)
        {
            _recent.Enqueue((y, ms));
            while (_recent.Count > 2 && ms - _recent.Peek().Ms > 100) _recent.Dequeue();
        }

        /// <summary>Pixels per second over the last tenth of a second (0 when the finger had stopped).</summary>
        public double Velocity(long now)
        {
            if (_recent.Count < 2 || now - _recent.Last().Ms > 80) return 0;
            var (y0, t0) = _recent.Peek();
            var (y1, t1) = _recent.Last();
            return t1 > t0 ? (y1 - y0) / (t1 - t0) * 1000 : 0;
        }
    }

    private ListDrag? _drag;
    private readonly DispatcherTimer _glide = new() { Interval = TimeSpan.FromMilliseconds(16) };
    private System.Windows.Controls.ScrollViewer? _glideList;
    private double _glideSpeed;

    /// <summary>After a flick the list keeps moving and slows down, like a phone's.</summary>
    private void StartGlide(System.Windows.Controls.ScrollViewer list, double velocity)
    {
        _glide.Stop();
        if (Math.Abs(velocity) < 150) return;
        _glideList = list;
        _glideSpeed = velocity;
        if (_glideHooked) { _glide.Start(); return; }
        _glideHooked = true;
        _glide.Tick += (_, _) =>
        {
            if (_glideList == null || Math.Abs(_glideSpeed) < 40) { _glide.Stop(); return; }
            double before = _glideList.VerticalOffset;
            _glideList.ScrollToVerticalOffset(before - _glideSpeed * 0.016);
            _glideSpeed *= 0.94;
            if (_glideList.VerticalOffset is 0 || _glideList.VerticalOffset >= _glideList.ScrollableHeight) _glideSpeed *= 0.5; // ease off at the ends
        };
        _glide.Start();
    }

    private bool _glideHooked;

    private ITouchDrag? _slide;
    private bool _sliding;

    /// <summary>The nearest draggable control (slider, seek bar) and scrollable list under a point of the HUD.</summary>
    private (ITouchDrag? Slider, System.Windows.Controls.ScrollViewer? List) DragTargetsAt(Point point)
    {
        ITouchDrag? slider = null;
        for (DependencyObject? node = Root.InputHitTest(point) as DependencyObject; node != null; node = VisualTreeHelper.GetParent(node))
        {
            if (slider == null && node is ITouchDrag drag) slider = drag;
            if (node is System.Windows.Controls.ScrollViewer { ScrollableHeight: > 0 } list) return (slider, list);
        }
        return (slider, null);
    }

    /// <summary>The scrollable list under a point of the HUD (in <see cref="Root"/>'s coordinates), if any.</summary>
    private System.Windows.Controls.ScrollViewer? ScrollerAt(Point point)
    {
        for (DependencyObject? node = Root.InputHitTest(point) as DependencyObject; node != null; node = VisualTreeHelper.GetParent(node))
            if (node is System.Windows.Controls.ScrollViewer { ScrollableHeight: > 0 } scroller) return scroller;
        return null;
    }

    /// <summary>Where a click or replayed touch landed, relative to <paramref name="element"/>.</summary>
    private Point TapPosition(System.Windows.Input.MouseEventArgs e, UIElement element) =>
        _touchAt is { } at ? Root.TranslatePoint(at, element) : e.GetPosition(element);

    /// <summary>
    /// Taps and clicks reach the window only when click-through is off. The window never takes
    /// focus, so a game keeps the keyboard.
    /// </summary>
    private void OnTap(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (BlankCover.Visibility == Visibility.Visible)
        {
            // Screen off by schedule: a tap shows the HUD for a minute.
            _pressAt = null;
            _wakeUntil = DateTime.Now.AddMinutes(1);
            BlankCover.Visibility = Visibility.Collapsed;
            ScreenPower(false); // out of standby too, not just the black cover
            _scheduleStatus = $"{DateTime.Now:HH:mm:ss} Screen woken by a tap for a minute.";
            if (_scheduleForm is { IsDisposed: false }) _scheduleForm.ShowStatus(_scheduleStatus);
            RefreshReadings();
            return;
        }
        // Aesthetic mode: the first tap only wakes the HUD; later taps work normally and keep it awake.
        if (_aesthetic.Tap(_settings, DateTime.Now))
        {
            _pressAt = null;
            ApplyAesthetic(DateTime.Now);
            RefreshReadings();
            return;
        }
        if (DetailHost.Visibility == Visibility.Visible) return;
        // A sideways swipe changes theme (the playlist's order, or themes shaped like the display).
        if (_pressAt is { } start && Swipe(start, TapPosition(e, Root), DateTime.Now - _pressTime, Shown.Width) is var step and not 0)
        {
            _pressAt = null;
            StepTheme(step);
            return;
        }
        _pressAt = null;
        if (_themeView != null)
        {
            if (_themeView.ElementAt(TapPosition(e, _themeView)) is { } element)
                RunAction(ThemeActions.Effective(element), ThemeActions.SensorFor(element.DataName), element.Slot);
            return;
        }
        for (var node = e.OriginalSource as DependencyObject; node != null;
             node = node is Visual ? VisualTreeHelper.GetParent(node) : LogicalTreeHelper.GetParent(node))
        {
            if (node == NowPlayingBlock) { RunAction(ThemeAction.PlayPause, null); return; }
            if (node == CpuPanel) { RunAction(ThemeAction.Details, "CpuLoad"); return; }
            if (node == GpuPanel) { RunAction(ThemeAction.Details, "GpuLoad"); return; }
            if (node == SystemPanel) { RunAction(ThemeAction.Details, "MemoryLoad"); return; }
        }
    }

    /// <summary>
    /// +1 (next theme) for a swipe to the left, -1 for one to the right, 0 for a tap: mostly sideways,
    /// at least 15% of the HUD's width (100 px minimum) and within a second.
    /// </summary>
    internal static int Swipe(Point from, Point to, TimeSpan duration, double width)
    {
        double dx = to.X - from.X, dy = to.Y - from.Y;
        if (duration > TimeSpan.FromSeconds(1) || Math.Abs(dx) < Math.Max(100, width * 0.15) || Math.Abs(dx) < Math.Abs(dy) * 2) return 0;
        return dx < 0 ? +1 : -1;
    }

    private void RunAction(ThemeAction action, string? sensor, int slot = 0)
    {
        var now = DateTime.Now;
        switch (action)
        {
            case ThemeAction.VolumeUp: AudioVolume.Change(+5); break;
            case ThemeAction.VolumeDown: AudioVolume.Change(-5); break;
            case ThemeAction.Mute: AudioVolume.ToggleMute(); break;
            case ThemeAction.TimerToggle: _timers.ToggleTimer(now); break;
            case ThemeAction.TimerReset: _timers.ResetTimer(); break;
            case ThemeAction.TimerAddMinute: _timers.AddMinute(); break;
            case ThemeAction.StopwatchToggle: _timers.ToggleStopwatch(now); break;
            case ThemeAction.StopwatchReset: _timers.ResetStopwatch(); break;
            case ThemeAction.Pomodoro25: _timers.StartPomodoro(now, 25, 5); break;
            case ThemeAction.Pomodoro50: _timers.StartPomodoro(now, 50, 10); break;
            case ThemeAction.FocusToggle: _timers.ToggleShown(now); break;
            case ThemeAction.FocusReset: _timers.ResetShown(); break;
            case ThemeAction.FocusSwitch: _timers.SwitchShown(); break;
            case ThemeAction.Launch: Launch(TileSlot(slot)); break;
            case ThemeAction.AppDrawer: ShowDrawer(); break;
            case ThemeAction.LightsPage: ShowLights(); break;
            case ThemeAction.LightsToggle: RunLights(new LightsCommand(_settings.Lights.Off ? LightsKind.On : LightsKind.Off)); break;
            case ThemeAction.AestheticToggle: SetAesthetic(!_settings.Aesthetic, announce: false); break;
            case ThemeAction.VolumeMixer: ShowMixer(); break;
            case ThemeAction.MediaPage: ShowMediaPage(); break;
            case ThemeAction.PalettePage: ShowPalettes(); break;
            case ThemeAction.GamingPage: ShowGaming(); break;
            case ThemeAction.NextPalette: SetPalette(ThemePalettes.Next(_settings.ThemePalettes.GetValueOrDefault(ShownTheme))); break;
            case ThemeAction.PlayPause: _ = _media.SendAsync(MediaCommand.PlayPause); break;
            case ThemeAction.NextTrack: _ = _media.SendAsync(MediaCommand.Next); break;
            case ThemeAction.PreviousTrack: _ = _media.SendAsync(MediaCommand.Previous); break;
            case ThemeAction.Details: ShowDetails(sensor); break;
            case ThemeAction.NextTheme: StepTheme(+1); break;
            case ThemeAction.PreviousTheme: StepTheme(-1); break;
        }
        // Show the new volume or timer state straight away rather than at the next refresh.
        if (action is >= ThemeAction.VolumeUp && IsLoaded) RefreshReadings();
    }

    private void ShowDetails(string? sensor)
    {
        var size = SensorDetails.LogicalSize(Shown.Width, Shown.Height);
        if (_detail == null || _detail.Width != size.Width || _detail.Height != size.Height)
        {
            _detail?.Close();
            _detail = new SensorDetailView(_telemetry, _settings, size, group => group switch
            {
                0 => HardwareNameReader.Cpu(_lastValues, _settings),
                1 => HardwareNameReader.Gpu(_lastValues, _settings),
                _ => ""
            });
            _detail.CloseRequested += HideDetails;
        }
        _drawer?.Close();
        _lightsView?.Close();
        _mixer?.Close();
        _mediaView?.Close();
        _paletteView?.Close();
        _gamingView?.Close();
        DetailHost.Child = _detail;
        DetailHost.Visibility = Visibility.Visible;
        _detail.Open(sensor);
    }

    /// <summary>Hides the details page or app drawer, whichever is open.</summary>
    private void HideDetails()
    {
        _detail?.Close();
        _drawer?.Close();
        _lightsView?.Close();
        _mixer?.Close();
        _mediaView?.Close();
        _paletteView?.Close();
        _gamingView?.Close();
        DetailHost.Visibility = Visibility.Collapsed;
        DetailHost.Child = null; // nothing of a closed page stays under the HUD
    }

    private void ShowDrawer()
    {
        var size = SensorDetails.LogicalSize(Shown.Width, Shown.Height);
        if (_drawer == null || _drawer.Width != size.Width || _drawer.Height != size.Height)
        {
            _drawer?.Close();
            _drawer = new AppDrawerView(size);
            _drawer.CloseRequested += HideDetails;
            _drawer.LaunchRequested += slot => { HideDetails(); Launch(slot); };
        }
        _detail?.Close();
        _lightsView?.Close();
        _mixer?.Close();
        _mediaView?.Close();
        _paletteView?.Close();
        _gamingView?.Close();
        DetailHost.Child = _drawer;
        DetailHost.Visibility = Visibility.Visible;
        _drawer.Open(_shortcutViews, _drawerSlots);
    }

    /// <summary>The Lights page: OpenRGB profiles, effects, colors and brightness as touch tiles.</summary>
    private void ShowLights()
    {
        var size = SensorDetails.LogicalSize(Shown.Width, Shown.Height);
        if (_lightsView == null || _lightsView.Width != size.Width || _lightsView.Height != size.Height)
        {
            _lightsView?.Close();
            _lightsView = new LightsView(size) { PositionOf = TapPosition };
            _lightsView.CloseRequested += HideDetails;
            _lightsView.Requested += RunLights;
            _lightsView.OpenAppRequested += OpenOpenRgb;
            _lightsView.ColorChosen += hex => { _settings.Lights.Color = hex; RunLights(new LightsCommand(LightsKind.Color)); };
            _lightsView.BrightnessChosen += value => { _settings.Lights.Brightness = value; RunLights(new LightsCommand(LightsKind.Brightness)); };
        }
        _detail?.Close();
        _drawer?.Close();
        _mixer?.Close();
        _mediaView?.Close();
        _paletteView?.Close();
        _gamingView?.Close();
        DetailHost.Child = _lightsView;
        DetailHost.Visibility = Visibility.Visible;
        _lightsView.Open(_settings.Lights);
        _ = ReadLights();
    }

    /// <summary>The volume page: the playback device and each app with sound.</summary>
    private void ShowMixer()
    {
        var size = SensorDetails.LogicalSize(Shown.Width, Shown.Height);
        if (_mixer == null || _mixer.Width != size.Width || _mixer.Height != size.Height)
        {
            _mixer?.Close();
        _mediaView?.Close();
        _paletteView?.Close();
        _gamingView?.Close();
            _mixer = new MixerView(size) { PositionOf = TapPosition };
            _mixer.CloseRequested += HideDetails;
            _mixer.LevelRequested += (key, level) =>
            {
                if (key == MixerView.MasterKey) AudioVolume.Set(level);
                else AppVolume.Set(key, level);
                UpdateMixer();
            };
            _mixer.MuteRequested += (key, mute) =>
            {
                if (key == MixerView.MasterKey) { if (AudioVolume.Read() is { } now && now.Muted != mute) AudioVolume.ToggleMute(); }
                else AppVolume.SetMute(key, mute);
                UpdateMixer();
            };
        }
        _detail?.Close();
        _drawer?.Close();
        _lightsView?.Close();
        _mediaView?.Close();
        _paletteView?.Close();
        _gamingView?.Close();
        DetailHost.Child = _mixer;
        DetailHost.Visibility = Visibility.Visible;
        _mixer.Open();
        UpdateMixer();
    }

    /// <summary>The Gaming page: the game being played or the last one, its session, and play time.</summary>
    private void ShowGaming()
    {
        var size = SensorDetails.LogicalSize(Shown.Width, Shown.Height);
        if (_gamingView == null || _gamingView.Width != size.Width || _gamingView.Height != size.Height)
        {
            _gamingView?.Close();
            _gamingView = new GamingView(size);
            _gamingView.CloseRequested += HideDetails;
        }
        _detail?.Close();
        _drawer?.Close();
        _lightsView?.Close();
        _mixer?.Close();
        _mediaView?.Close();
        _paletteView?.Close();
        DetailHost.Child = _gamingView;
        DetailHost.Visibility = Visibility.Visible;
        _gamingView.Open();
        _gamingView.Show(_games.Snapshot(DateTime.Now, GameTile));
    }

    /// <summary>The Colors page: the current theme in its own colors or one of the palettes.</summary>
    private void ShowPalettes()
    {
        var size = SensorDetails.LogicalSize(Shown.Width, Shown.Height);
        if (_paletteView == null || _paletteView.Width != size.Width || _paletteView.Height != size.Height)
        {
            _paletteView?.Close();
        _gamingView?.Close();
            _paletteView = new PaletteView(size);
            _paletteView.CloseRequested += HideDetails;
            // Applying a palette reloads the theme (which closes pages), so the page opens again on the new colors.
            _paletteView.PaletteChosen += name => { SetPalette(name); ShowPalettes(); };
        }
        _detail?.Close();
        _drawer?.Close();
        _lightsView?.Close();
        _mixer?.Close();
        _mediaView?.Close();
        DetailHost.Child = _paletteView;
        DetailHost.Visibility = Visibility.Visible;
        _paletteView.Open(_settings.ThemePalettes.GetValueOrDefault(ShownTheme) ?? "", _themeView?.Theme.Name);
    }
    /// <summary>The media page: what's playing, seek, controls, and with Plexamp shuffle, repeat, volume and the queue.</summary>
    private void ShowMediaPage()
    {
        var size = SensorDetails.LogicalSize(Shown.Width, Shown.Height);
        if (_mediaView == null || _mediaView.Width != size.Width || _mediaView.Height != size.Height)
        {
            _mediaView?.Close();
        _paletteView?.Close();
        _gamingView?.Close();
            _mediaView = new MediaView(size) { PositionOf = TapPosition };
            _mediaView.CloseRequested += HideDetails;
            _mediaView.CommandRequested += command => _ = MediaCommandAsync(() => _media.SendAsync(command));
            _mediaView.ControlRequested += (control, value) => _ = MediaCommandAsync(() => _media.PlexampControlAsync(control, value), queueChanged: control == PlexampControl.Shuffle);
        }
        _media.Start(); // also when the theme shows no media
        _detail?.Close();
        _drawer?.Close();
        _lightsView?.Close();
        _mixer?.Close();
        _paletteView?.Close();
        _gamingView?.Close();
        DetailHost.Child = _mediaView;
        DetailHost.Visibility = Visibility.Visible;
        _mediaView.Open();
        _queueDue = default;
        UpdateMediaPage();
    }

    /// <summary>Sends a tapped media command, then shows the result once the player has had a moment.</summary>
    private async Task MediaCommandAsync(Func<Task<bool>> send, bool queueChanged = false)
    {
        bool ok = await send();
        _mediaView?.ShowStatus(ok ? "" : "The player didn't answer.");
        await Task.Delay(700);
        if (queueChanged) _queueDue = default;
        UpdateMediaPage();
    }

    /// <summary>Refreshes the media page each second while it is open; the queue every 10 s or when the track changes.</summary>
    private void UpdateMediaPage()
    {
        if (_mediaView == null || DetailHost.Child != _mediaView || DetailHost.Visibility != Visibility.Visible) return;
        var timeline = _media.Plexamp;
        _mediaView.Show(_media.Current, timeline);
        if (timeline == null) return;
        var now = DateTime.Now;
        if (now < _queueDue && timeline.PlayQueueItemId == _queueItem) return;
        _queueDue = now.AddSeconds(10);
        _queueItem = timeline.PlayQueueItemId;
        var view = _mediaView;
        _ = _media.PlexampQueueAsync().ContinueWith(t =>
        {
            if (_mediaView == view && DetailHost.Child == view && _media.Plexamp != null) view.ShowQueue(t.IsCompletedSuccessfully ? t.Result : null);
        }, TaskScheduler.FromCurrentSynchronizationContext());
    }

    /// <summary>Reads the levels again while the volume page is open (they also change outside Pulsewave).</summary>
    private void UpdateMixer()
    {
        if (_mixer == null || DetailHost.Child != _mixer || DetailHost.Visibility != Visibility.Visible) return;
        _mixer.Show(AudioVolume.Read(), AppVolume.Read());
    }

    /// <summary>The Lights page's "Open OpenRGB": shows or starts the program, then asks it again once it has had time to start.</summary>
    private void OpenOpenRgb()
    {
        string before = _settings.Lights.AppPath;
        var (ok, status) = OpenRgbApp.Open(_settings.Lights);
        if (_settings.Lights.AppPath != before)
            try { SettingsStore.Save(_settings); } catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException) { }
        _lightsView?.ShowStatus(status);
        if (ok && status.StartsWith("Starting", StringComparison.Ordinal))
            _ = Task.Delay(TimeSpan.FromSeconds(8)).ContinueWith(_ =>
            {
                if (DetailHost.Child == _lightsView && DetailHost.Visibility == Visibility.Visible) _ = ReadLights();
            }, TaskScheduler.FromCurrentSynchronizationContext());
    }

    private async Task ReadLights()
    {
        var (state, status) = await _lights.ReadAsync(_settings.Lights);
        if (DetailHost.Child == _lightsView && DetailHost.Visibility == Visibility.Visible) _lightsView?.ShowState(state, status);
        if (_lightsForm is { IsDisposed: false }) _lightsForm.ShowState(state, status);
    }

    private void RunLights(LightsCommand command) => _ = RunLightsAsync(command);

    /// <summary>Runs a lighting command in the background; on success remembers it (for Lights on) and saves.</summary>
    private async Task<bool> RunLightsAsync(LightsCommand command, bool quiet = false)
    {
        var (ok, status) = await _lights.RunAsync(_settings.Lights, command);
        if (ok)
        {
            Lights.Remember(_settings.Lights, command);
            try { SettingsStore.Save(_settings); }
            catch (Exception ex) { status += " (Not saved: " + ex.Message + ")"; }
        }
        if (DetailHost.Child == _lightsView && DetailHost.Visibility == Visibility.Visible) _lightsView?.Applied(command, status);
        if (_lightsForm is { IsDisposed: false }) _lightsForm.ShowStatus(status);
        if (!ok && !quiet && DetailHost.Visibility != Visibility.Visible) _tray?.ShowBalloonTip(4000, "Lights", status, Forms.ToolTipIcon.Warning);
        return ok;
    }

    /// <summary>Acts on the schedule's edges: LEDs off/on through OpenRGB, and the HUD blank or shown.</summary>
    private void RunSchedule(DateTime now)
    {
        var state = Scheduler.Wanted(_settings.Schedule, now);
        if (_scheduler.ScreenChange(state) is { } off) { _screenBlank = off; _wakeUntil = default; }
        var cover = _screenBlank && now >= _wakeUntil ? Visibility.Visible : Visibility.Collapsed;
        if (BlankCover.Visibility != cover)
        {
            BlankCover.Visibility = cover;
            if (cover == Visibility.Visible) HideDetails();
            ScreenPower(cover == Visibility.Visible);
        }
        if (_scheduler.LightsCommand(state, now, _settings.Lights.Off, _settings.Lights.ScheduledOff) is { } kind) _ = ScheduledLights(kind);
        if (_scheduler.DimChange(state, out var dim)) ApplyDim(dim);
    }

    // Night dimming: the rule's mode and level while a dim period runs, and the backlight to put back.
    private (string Mode, int Level)? _dim;
    private (string Device, uint Brightness)? _backlightBefore;
    // Display changes left by a crash are undone first; the schedule's own display commands wait for it.
    private Task _displayUndo = Task.CompletedTask;

    /// <summary>Records what the schedule changed on the display (or deletes the record), so a crash is undone on the next start.</summary>
    private void SaveDisplayRestore() => DisplayRestore.Save(DisplayRestore.DefaultFile, new DisplayRestore
        { BacklightDevice = _backlightBefore?.Device, Brightness = _backlightBefore?.Brightness ?? 0, StandbyDevice = _poweredDown });

    /// <summary>Puts back a backlight lowered, and wakes a display put in standby, by a run of Pulsewave that didn't exit normally.</summary>
    private void UndoLeftoverDisplayChanges()
    {
        if (DisplayRestore.Load(DisplayRestore.DefaultFile) is not { } leftover) return;
        var connected = Forms.Screen.AllScreens.Select(s => s.DeviceName).ToList();
        _displayUndo = Task.Run(() =>
        {
            string? note = DisplayRestore.Undo(leftover, connected, DisplayPower.SetBrightness, DisplayPower.TurnOn);
            Dispatcher.BeginInvoke(() => { SaveDisplayRestore(); if (note != null) ScheduleNote(note); });
        });
    }

    /// <summary>
    /// Dims the HUD for a schedule period: "Theme" lays a black veil over it and drops the glows; "Backlight" lowers
    /// the display's own brightness over DDC/CI and puts it back afterwards (never the main display, never in dock mode).
    /// </summary>
    private void ApplyDim((string Mode, int Level)? dim)
    {
        _dim = dim;
        bool veil = dim is { Mode: "Theme" or "Both" }, backlight = dim is { Mode: "Backlight" or "Both" };
        DimCover.Opacity = veil ? 1 - dim!.Value.Level / 100.0 : 0;
        DimCover.Visibility = veil ? Visibility.Visible : Visibility.Collapsed;
        _themeView?.SetGlows(!veil);
        if (backlight && _backlightBefore == null && _settings.DockEdge.Length == 0 &&
            Forms.Screen.AllScreens.FirstOrDefault(s => s.DeviceName == SelectedDevice()) is { Primary: false } screen)
        {
            string device = screen.DeviceName;
            int level = dim!.Value.Level;
            var undo = _displayUndo;
            _ = Task.Run(() =>
            {
                undo.Wait(); // read the level to put back only after a crash's dimming was undone
                if (DisplayPower.Brightness(device) is not { } now) return ((string, uint)?)null;
                DisplayPower.SetBrightness(device, (uint)Math.Round(now.Maximum * level / 100.0));
                return (device, now.Current);
            }).ContinueWith(t =>
            {
                _backlightBefore = t.Result;
                SaveDisplayRestore();
                ScheduleNote(t.Result == null ? "Night dimming: this display doesn't take brightness over DDC/CI; dimming the theme only works here." : "Night dimming: display brightness lowered.");
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }
        else if (!backlight && _backlightBefore is { } before)
        {
            _backlightBefore = null;
            // The record goes once the level is back, so a crash in between still restores it.
            _ = Task.Run(() => DisplayPower.SetBrightness(before.Device, before.Brightness))
                .ContinueWith(_ => SaveDisplayRestore(), TaskScheduler.FromCurrentSynchronizationContext());
            ScheduleNote("Night dimming: display brightness restored.");
        }
    }

    private void ScheduleNote(string text)
    {
        _scheduleStatus = $"{DateTime.Now:HH:mm} {text}";
        if (_scheduleForm is { IsDisposed: false }) _scheduleForm.ShowStatus(_scheduleStatus);
    }

    /// <summary>
    /// With the option on, the HUD's display goes to standby over DDC/CI while blank and comes back
    /// for a tap or the end of the period. Never the main display, and never in dock mode.
    /// </summary>
    private void ScreenPower(bool blank)
    {
        if (blank && _poweredDown == null && _settings.ScreenPowerOff && _settings.DockEdge.Length == 0 &&
            Forms.Screen.AllScreens.FirstOrDefault(s => s.DeviceName == SelectedDevice()) is { Primary: false } screen)
        {
            string device = screen.DeviceName;
            _poweredDown = device;
            SaveDisplayRestore();
            var undo = _displayUndo;
            _ = Task.Run(() => { undo.Wait(); DisplayPower.Set(device, DisplayPower.Standby); });
        }
        else if (!blank && _poweredDown is { } device)
        {
            _poweredDown = null;
            _ = Task.Run(() => DisplayPower.TurnOn(device)).ContinueWith(t =>
            {
                SaveDisplayRestore();
                _scheduleStatus = $"{DateTime.Now:HH:mm:ss} Display on: " + (t.Result ? "done." : "it didn't confirm; tap the screen or switch it on.");
                if (_scheduleForm is { IsDisposed: false }) _scheduleForm.ShowStatus(_scheduleStatus);
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }
    }

    private async Task ScheduledLights(LightsKind kind)
    {
        bool ok = await RunLightsAsync(new LightsCommand(kind), quiet: true);
        _scheduler.LightsDone(ok);
        _scheduleStatus = $"{DateTime.Now:HH:mm} LEDs {(kind == LightsKind.Off ? "off" : "on")}: " +
                          (ok ? "done." : "it didn't work (see tray → Lights); trying again each minute.");
        if (ok)
        {
            _settings.Lights.ScheduledOff = kind == LightsKind.Off;
            try { SettingsStore.Save(_settings); } catch (Exception) { /* RunLightsAsync already reported saving trouble */ }
        }
        if (_scheduleForm is { IsDisposed: false }) _scheduleForm.ShowStatus(_scheduleStatus);
    }

    private void OpenSchedule()
    {
        if (_scheduleForm is { IsDisposed: false }) { _scheduleForm.Activate(); return; }
        _scheduleForm = new ScheduleForm(_settings.Schedule, rules =>
        {
            var prior = _settings.Schedule;
            try { _settings.Schedule = rules.Select(r => r.Copy()).ToList(); SettingsStore.Save(_settings); }
            catch (Exception ex) { _settings.Schedule = prior; return "Not saved: " + ex.Message; }
            _scheduler.Reset(); // act on the new rules at the next tick
            return null;
        }, _scheduleStatus, _settings.ScreenPowerOff, on =>
        {
            _settings.ScreenPowerOff = on;
            try { SettingsStore.Save(_settings); return null; }
            catch (Exception ex) { _settings.ScreenPowerOff = !on; return "Not saved: " + ex.Message; }
        }, ThemeCycle.Available(ThemeStore.RootPath).Select(t => (t.Id, t.Name)).ToList());
        _scheduleForm.Show(); _scheduleForm.Activate();
    }

    private void OpenLights()
    {
        if (_lightsForm is { IsDisposed: false }) { _lightsForm.Activate(); return; }
        _lightsForm = new LightsForm(_settings.Lights, RunLights, () => _ = ReadLights(), () =>
        {
            try { SettingsStore.Save(_settings); return null; }
            catch (Exception ex) { return "Not saved: " + ex.Message; }
        });
        _lightsForm.Show(); _lightsForm.Activate();
        _ = ReadLights();
    }

    /// <summary>
    /// Orders the shortcuts for the shown theme: its list's picks on the tiles (or every slot in order) and the
    /// picks first in the app drawer.
    /// </summary>
    private void ArrangeShortcuts()
    {
        var picks = ShortcutLists.PicksOf(_settings.ShortcutLists, _settings.Shortcuts, _shortcutList);
        _tileSlots = ShortcutLists.TileSlots(_settings.Shortcuts, picks);
        _tileViews = _tileSlots.Select(slot => _shortcutViews[slot - 1]).ToList();
        _drawerSlots = ShortcutLists.DrawerSlots(_settings.Shortcuts, picks);
    }

    /// <summary>The shortcut slot a theme's tile opens (1 = its first tile), or 0 when the tile has none.</summary>
    private int TileSlot(int tile) => tile >= 1 && tile <= _tileSlots.Count ? _tileSlots[tile - 1] : 0;

    /// <summary>Opens the user's shortcut in a 1-based slot; an empty slot does nothing.</summary>
    private void Launch(int slot)
    {
        if (slot < 1 || slot > _settings.Shortcuts.Count || _settings.Shortcuts[slot - 1].IsEmpty) return;
        if (ShortcutLauncher.Launch(_settings.Shortcuts[slot - 1]) is { } error)
            _tray?.ShowBalloonTip(5000, "Shortcut did not open", error, Forms.ToolTipIcon.Warning);
    }

    private static List<AppShortcut> Dropped(System.Windows.IDataObject data)
    {
        var items = data.GetData(System.Windows.DataFormats.FileDrop) as string[] ??
                    (data.GetData(System.Windows.DataFormats.UnicodeText) as string)?.Split('\n', StringSplitOptions.RemoveEmptyEntries) ?? [];
        return items.Select(AppShortcut.FromDrop).OfType<AppShortcut>().ToList();
    }

    /// <summary>
    /// A drop on a shortcut tile (or its name) puts the first item in that tile's slot; anywhere else
    /// it takes the first free slot. Further items fill the next free slots. Saved straight away.
    /// </summary>
    private void OnDrop(object sender, System.Windows.DragEventArgs e)
    {
        var dropped = Dropped(e.Data);
        if (dropped.Count == 0) return;
        int slot = 0;
        // On a theme that follows the Gaming or Productivity list, a drop on a tile past its picks adds to that list.
        bool toList = false;
        if (DetailHost.Visibility != Visibility.Visible && _themeView?.ElementAt(e.GetPosition(_themeView)) is { Slot: > 0 } target &&
            ThemeActions.Effective(target) == ThemeAction.Launch)
        {
            slot = TileSlot(target.Slot);
            toList = slot == 0 && ShortcutLists.PicksOf(_settings.ShortcutLists, _settings.Shortcuts, _shortcutList).Count > 0;
            if (slot == 0 && !toList) slot = target.Slot;
        }
        var list = _settings.Shortcuts.Select(s => s.Copy()).ToList();
        var lists = _settings.ShortcutLists.ToDictionary(p => p.Key, p => p.Value.ToList(), StringComparer.OrdinalIgnoreCase);
        var placed = new List<string>();
        foreach (var shortcut in dropped)
        {
            int used = AppShortcut.Assign(list, shortcut, slot);
            if (used == 0) break;
            ShortcutLists.Replaced(lists, used);
            if (toList) lists[_shortcutList!].Add(used);
            placed.Add($"{shortcut.DisplayName} → slot {used}");
            slot = 0;
        }
        if (placed.Count == 0) { _tray?.ShowBalloonTip(4000, "Shortcuts full", $"Up to {AppShortcut.MaxCount} shortcuts.", Forms.ToolTipIcon.Warning); return; }
        if (SaveShortcuts(list, lists) is { } error) { _tray?.ShowBalloonTip(5000, "Shortcut not added", error, Forms.ToolTipIcon.Error); return; }
        if (_shortcutsForm is { IsDisposed: false }) _shortcutsForm.ShowSaved(_settings.Shortcuts, _settings.ShortcutLists);
        if (DetailHost.Visibility == Visibility.Visible && DetailHost.Child == _drawer) _drawer?.Open(_shortcutViews, _drawerSlots);
        _tray?.ShowBalloonTip(3000, "Shortcut added", string.Join("\n", placed), Forms.ToolTipIcon.Info);
    }

    /// <summary>Saves the shortcuts and the Gaming and Productivity lists and refreshes icons; returns why they were not saved, or null.</summary>
    private string? SaveShortcuts(List<AppShortcut> list, Dictionary<string, List<int>> lists)
    {
        var prior = (_settings.Shortcuts, _settings.ShortcutLists);
        AppShortcut.TrimEnd(list);
        // Picks of shortcuts that are gone are dropped.
        var kept = ShortcutLists.Normalize(lists.ToDictionary(p => p.Key, p => ShortcutLists.Valid(list, p.Value)));
        try { _settings.Shortcuts = list; _settings.ShortcutLists = kept; SettingsStore.Save(_settings); }
        catch (Exception ex) { (_settings.Shortcuts, _settings.ShortcutLists) = prior; return "Not saved: " + ex.Message; }
        _shortcutViews = _settings.Shortcuts.Select(ShortcutView.From).ToList();
        ArrangeShortcuts();
        _catalogDue = default; // shortcuts can add games
        if (IsLoaded) RefreshReadings();
        return null;
    }

    private void OpenShortcuts()
    {
        if (_shortcutsForm is { IsDisposed: false }) { _shortcutsForm.Activate(); return; }
        _shortcutsForm = new ShortcutsForm(_settings.Shortcuts, _settings.ShortcutLists, SaveShortcuts);
        _shortcutsForm.Show(); _shortcutsForm.Activate();
    }

    /// <summary>
    /// With the opt-in on and a theme showing Claude usage, asks Anthropic for it every 3 minutes
    /// (10 after a failure) in the background; the reader picks up the saved result.
    /// </summary>
    private void PollClaudeAccount(DateTime now)
    {
        if (!_settings.ClaudeAccountUsage || _claudeFetching || now < _claudeNext) return;
        _claudeFetching = true;
        _ = Task.Run(async () =>
        {
            bool ok = await ClaudeAccountUsage.FetchAsync(Http, ClaudeAccountUsage.CredentialsFile, ClaudeStatusLine.UsageFile);
            await Dispatcher.InvokeAsync(() =>
            {
                _claudeFetching = false;
                _claudeNext = DateTime.Now.AddMinutes(ok ? 3 : 10);
                if (ok) _usage.Invalidate();
            });
        });
    }

    /// <summary>With "keep this display clear" on, moves other app windows off the HUD's display (not when docked or on a single display).</summary>
    private void GuardDisplay()
    {
        var screens = Forms.Screen.AllScreens;
        if (!_settings.KeepDisplayClear || _appBar?.Docked == true || _userHidden || screens.Length < 2) return;
        var hud = screens.FirstOrDefault(s => s.Bounds == _screen);
        var main = screens.FirstOrDefault(s => s.Primary && s != hud) ?? screens.FirstOrDefault(s => s != hud);
        if (hud == null || main == null) return;
        DisplayGuard.Sweep(hud.Bounds, main.WorkingArea, _hwnd);
    }

    private void UpdateGuard()
    {
        if (_settings.KeepDisplayClear) { _guard.Start(); GuardDisplay(); }
        else _guard.Stop();
    }

    private void SetRotation(int degrees)
    {
        int prior = _settings.HudRotation;
        try { _settings.HudRotation = degrees; SettingsStore.Save(_settings); }
        catch (Exception ex)
        {
            _settings.HudRotation = prior;
            _tray?.ShowBalloonTip(5000, "Preference not saved", ex.Message, Forms.ToolTipIcon.Error);
            return;
        }
        _dockKey = null;
        PlaceOnTargetMonitor();
        FitLayout();
        RebuildTrayMenu();
    }

    private void ToggleKeepClear()
    {
        bool prior = _settings.KeepDisplayClear;
        try { _settings.KeepDisplayClear = !prior; SettingsStore.Save(_settings); }
        catch (Exception ex)
        {
            _settings.KeepDisplayClear = prior;
            _tray?.ShowBalloonTip(5000, "Preference not saved", ex.Message, Forms.ToolTipIcon.Error);
            return;
        }
        UpdateGuard();
        RebuildTrayMenu();
    }

    /// <summary>Opens Windows' own touch setup (Tablet PC Settings), which asks you to tap the touch screen so touches land on it.</summary>
    private void OpenTouchSetup()
    {
        try { Process.Start(new ProcessStartInfo(System.IO.Path.Combine(Environment.SystemDirectory, "MultiDigiMon.exe"), "-touch") { UseShellExecute = true })?.Dispose(); }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            _tray?.ShowBalloonTip(5000, "Touch setup did not open", ex.Message, Forms.ToolTipIcon.Warning);
        }
    }

    /// <summary>Turns dock mode on, off or to another edge or size, and places the HUD again.</summary>
    private void SetDock(string edge, int percent)
    {
        var prior = (_settings.DockEdge, _settings.DockPercent);
        try { (_settings.DockEdge, _settings.DockPercent) = (edge, percent); SettingsStore.Save(_settings); }
        catch (Exception ex)
        {
            (_settings.DockEdge, _settings.DockPercent) = prior;
            _tray?.ShowBalloonTip(5000, "Preference not saved", ex.Message, Forms.ToolTipIcon.Error);
            return;
        }
        _dockKey = null;
        PlaceOnTargetMonitor();
        ApplyInputStyle();
        RebuildTrayMenu();
    }

    private void ToggleClaudeAccount()
    {
        if (!_settings.ClaudeAccountUsage && Forms.MessageBox.Show(
                "Pulsewave can ask Anthropic for your Claude plan usage (5-hour and weekly limits) with the sign-in the Claude app keeps on this PC " +
                "(" + ClaudeAccountUsage.CredentialsFile + "). It works without a terminal and updates every 3 minutes while a theme shows Claude usage.\n\n" +
                "The sign-in is sent only to api.anthropic.com and is never saved, logged or renewed by Pulsewave. This uses the same usage page Claude's " +
                "own apps read; Anthropic does not document it for other apps, so it may stop working after an update. Turn it on?",
                "Claude usage", Forms.MessageBoxButtons.YesNo, Forms.MessageBoxIcon.Question) != Forms.DialogResult.Yes) return;
        bool prior = _settings.ClaudeAccountUsage;
        try { _settings.ClaudeAccountUsage = !prior; SettingsStore.Save(_settings); }
        catch (Exception ex)
        {
            _settings.ClaudeAccountUsage = prior;
            _tray?.ShowBalloonTip(5000, "Preference not saved", ex.Message, Forms.ToolTipIcon.Error);
            return;
        }
        _claudeNext = DateTime.MinValue; // fetch on the next refresh
        RebuildTrayMenu();
    }

    /// <summary>Connects or disconnects Pulsewave as Claude Code's status line, after asking.</summary>
    private void ToggleClaudeUsage()
    {
        string claude = ClaudeStatusLine.ClaudeSettingsFile, launcher = ClaudeStatusLine.LauncherFile;
        var (state, command) = ClaudeStatusLine.Status(claude, launcher);
        try
        {
            if (state == ClaudeStatusLine.State.Connected)
            {
                if (Forms.MessageBox.Show("Stop showing Claude Code usage? This removes Pulsewave's status line from " + claude + ".",
                        "Claude Code usage", Forms.MessageBoxButtons.YesNo, Forms.MessageBoxIcon.Question) != Forms.DialogResult.Yes) return;
                ClaudeStatusLine.Disconnect(claude, launcher);
            }
            else if (state == ClaudeStatusLine.State.OtherStatusLine)
            {
                Forms.MessageBox.Show("Claude Code already runs another status line:\n\n" + command +
                    "\n\nPulsewave leaves it in place. To show Claude usage on the HUD instead, remove that status line and connect again.",
                    "Claude Code usage", Forms.MessageBoxButtons.OK, Forms.MessageBoxIcon.Information);
                return;
            }
            else
            {
                if (Environment.ProcessPath is not { } exe) return;
                if (Forms.MessageBox.Show(
                        "Claude Code shares its 5-hour and weekly plan limits only with its status line command. Pulsewave can be that command: " +
                        "it saves the limits for the HUD and shows model, context and usage in Claude Code's status line.\n\n" +
                        "This adds a \"statusLine\" setting to " + claude + " (a backup is kept next to it). Usage appears after Claude Code's next reply. Connect?",
                        "Claude Code usage", Forms.MessageBoxButtons.YesNo, Forms.MessageBoxIcon.Question) != Forms.DialogResult.Yes) return;
                ClaudeStatusLine.Connect(claude, launcher, exe);
            }
        }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            _tray?.ShowBalloonTip(6000, "Claude Code settings not changed", ex.Message, Forms.ToolTipIcon.Error);
        }
        RebuildTrayMenu();
    }

    private void OpenThemes()
    {
        if (_themesForm is { IsDisposed: false }) { _themesForm.Activate(); return; }
        _themesForm = new ThemesForm(_settings, ThemeStore.RootPath,
            id => { PickTheme(id); RestartPlaylist(); },
            live: () => ThemeContext(DateTime.Now, _lastValues),
            displaySize: () => (Shown.Width, Shown.Height),
            themeSaved: id => { if (ShownTheme == id) ApplyTheme(); }, // show edits to the running theme
            openPlaylist: OpenPlaylist,
            filterChanged: SaveThemeFilter);
        _themesForm.Show(); _themesForm.Activate();
    }

    // --- Theme playlist and hotkeys ---------------------------------------------------------

    private IReadOnlyList<string> RegisterHotkeys() => _hotkeys?.Register([
        (HotkeyNext, "Next theme", _settings.HotkeyNextTheme),
        (HotkeyPrevious, "Previous theme", _settings.HotkeyPreviousTheme),
        (HotkeyToggle, "Rotation on / off", _settings.HotkeyTogglePlaylist),
        (HotkeyAesthetic, "Aesthetic mode on / off", _settings.HotkeyAesthetic)]) ?? [];

    private void OnHotkey(int id)
    {
        switch (id)
        {
            case HotkeyNext: StepTheme(+1); break;
            case HotkeyPrevious: StepTheme(-1); break;
            case HotkeyToggle: TogglePlaylist(announce: true); break;
            case HotkeyAesthetic: SetAesthetic(!_settings.Aesthetic, announce: true); break;
        }
    }

    /// <summary>
    /// Shows the next or previous theme of the playlist (or of all themes), from the one showing; stepping restarts the
    /// rotation interval. By hand (swipe, hotkey, tray, a theme's button) it is a pick; the playlist's own steps are not.
    /// </summary>
    private void StepTheme(int step, bool byHand = true)
    {
        var themes = ThemeCycle.Available(ThemeStore.RootPath);
        var order = ThemeCycle.Order(_settings.ThemePlaylist, themes.Select(t => t.Id).ToList(), ThemeCycle.Fitting(themes, Shown.Width, Shown.Height));
        string next = ThemeCycle.Step(order, ShownTheme, step);
        if (byHand) PickTheme(next);
        else if (next != _settings.ActiveTheme) SavePreference(() => _settings.ActiveTheme = next, ApplyTheme);
        RestartPlaylist();
        if (_themesForm is { IsDisposed: false }) _themesForm.ShowActive();
    }

    private void TogglePlaylist(bool announce)
    {
        SavePreference(() => _settings.PlaylistEnabled = !_settings.PlaylistEnabled, RestartPlaylist);
        if (announce)
            _tray?.ShowBalloonTip(2500, "Pulsewave", _settings.PlaylistEnabled ? $"Theme rotation on · every {_settings.PlaylistMinutes} min" : "Theme rotation off",
                Forms.ToolTipIcon.Info);
    }

    private void RestartPlaylist()
    {
        _playlistTimer.Stop();
        if (!_settings.PlaylistEnabled) return;
        _playlistTimer.Interval = TimeSpan.FromMinutes(Math.Clamp(_settings.PlaylistMinutes, 1, 1440));
        _playlistTimer.Start();
    }

    private void OpenPlaylist()
    {
        if (_playlistForm is { IsDisposed: false }) { _playlistForm.Activate(); return; }
        _playlistForm = new PlaylistForm(_settings, ThemeCycle.Available(ThemeStore.RootPath), SavePlaylist,
            displaySize: () => (Shown.Width, Shown.Height), filterChanged: SaveThemeFilter);
        _playlistForm.Show(); _playlistForm.Activate();
    }

    /// <summary>The shape filter the Themes gallery and the playlist window share; a view choice, so a failed save is ignored.</summary>
    private void SaveThemeFilter(string key)
    {
        _settings.ThemeGalleryFilter = key;
        try { SettingsStore.Save(_settings); } catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException) { }
    }

    private IReadOnlyList<string> SavePlaylist(PlaylistChoice choice)
    {
        var prior = (_settings.ThemePlaylist, _settings.PlaylistEnabled, _settings.PlaylistMinutes,
            _settings.HotkeyNextTheme, _settings.HotkeyPreviousTheme, _settings.HotkeyTogglePlaylist, _settings.HotkeyAesthetic);
        try
        {
            (_settings.ThemePlaylist, _settings.PlaylistEnabled, _settings.PlaylistMinutes,
                _settings.HotkeyNextTheme, _settings.HotkeyPreviousTheme, _settings.HotkeyTogglePlaylist, _settings.HotkeyAesthetic) =
                (choice.Playlist, choice.Enabled, Math.Clamp(choice.Minutes, 1, 1440), choice.Next, choice.Previous, choice.Toggle, choice.Aesthetic);
            SettingsStore.Save(_settings);
        }
        catch (Exception ex)
        {
            (_settings.ThemePlaylist, _settings.PlaylistEnabled, _settings.PlaylistMinutes,
                _settings.HotkeyNextTheme, _settings.HotkeyPreviousTheme, _settings.HotkeyTogglePlaylist, _settings.HotkeyAesthetic) = prior;
            return ["Settings were not saved: " + ex.Message];
        }
        RestartPlaylist();
        RebuildTrayMenu();
        return RegisterHotkeys();
    }

    private void UpdateNowPlaying()
    {
        bool enabled = _settings.ShowNowPlaying;
        UptimeBlock.Visibility = enabled ? Visibility.Collapsed : Visibility.Visible;
        NowPlayingBlock.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
        if (!enabled) { _media.Stop(); _shownMedia = null; return; }
        _media.Start();
        // The monitor only publishes a new snapshot when something changed.
        var media = _media.Current;
        if (ReferenceEquals(media, _shownMedia)) return;
        _shownMedia = media;
        var view = NowPlayingView.From(media);
        NowPlayingStatus.Text = view.Status;
        NowPlayingStatus.Foreground = media.State == MediaPlayback.Playing
            ? _mintBrush ??= (Brush)FindResource("Mint") : (Brush)FindResource("MutedText");
        NowPlayingTitle.Text = view.Title;
        NowPlayingTitle.Foreground = (Brush)FindResource(view.Dim ? "DimText" : "BaseText");
        NowPlayingArtist.Text = view.Artist;
        NowPlayingArtist.Visibility = view.Artist.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        _artPlaceholder ??= NowPlayingArt.Background;
        NowPlayingArt.Background = media.Art is { } art
            ? new ImageBrush(art) { Stretch = Stretch.UniformToFill }
            : _artPlaceholder;
        NowPlayingArtPlaceholder.Visibility = media.Art is null ? Visibility.Visible : Visibility.Collapsed;
    }

    // Advisory colors only. Notification thresholds remain separately opt-in in V0.3.
    private static Brush WarningBrush(double? value, double threshold, Brush normal, Brush warning, Brush critical)
    {
        if (!value.HasValue || !double.IsFinite(value.Value)) return normal;
        if (value.Value >= threshold) return critical;
        if (value.Value >= threshold - 10) return warning;
        return normal;
    }

    private static string Display(double? n, string format, string suffix = "") =>
        n.HasValue && double.IsFinite(n.Value) ? n.Value.ToString(format) + suffix : "--" + suffix;

    private static void UpdateGraph(System.Windows.Shapes.Polyline polyline,
        IReadOnlyList<TelemetryPoint> history, DateTime now, int windowSeconds)
    {
        var points = new PointCollection();
        // Do not draw across missing periods. Start after the last missing sample.
        int start = 0;
        for (int i = 0; i < history.Count; i++)
            if (!history[i].Value.HasValue) start = i + 1;
        for (int i = start; i < history.Count; i++)
        {
            var point = history[i];
            if (!point.Value.HasValue) continue;
            var age = (now - point.Time).TotalSeconds;
            double x = 447 * (1 - Math.Clamp(age, 0, windowSeconds) / windowSeconds);
            double y = 81 - Math.Clamp(point.Value.Value, 0, 100) * 0.76;
            points.Add(new Point(x, y));
        }
        polyline.Points = points;
    }

    private void BuildTray()
    {
        string? executable = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(executable))
        {
            try { _trayIcon = System.Drawing.Icon.ExtractAssociatedIcon(executable); }
            catch { _trayIcon = null; }
        }
        _tray = new Forms.NotifyIcon
        {
            Text = "Pulsewave System Monitor",
            Icon = _trayIcon ?? SystemIcons.Application,
            Visible = true
        };
        _tray.DoubleClick += (_, _) => Dispatcher.Invoke(() => RunTrayAction(_settings.TrayDoubleClick));
        RebuildTrayMenu();
    }

    /// <summary>Runs what double-clicking the tray icon is set to do (see TrayActions).</summary>
    private void RunTrayAction(string id)
    {
        var now = DateTime.Now;
        if (id.StartsWith("theme:", StringComparison.Ordinal))
        {
            string theme = id[6..];
            if (ThemeCycle.Available(ThemeStore.RootPath).Any(t => t.Id == theme))
            {
                PickTheme(theme);
                RestartPlaylist();
                RestoreOverlay();
            }
            else _tray?.ShowBalloonTip(3000, "Pulsewave", "That theme is no longer installed. Pick another under Double-click the icon.", Forms.ToolTipIcon.Warning);
            return;
        }
        if (id.StartsWith("launch:", StringComparison.Ordinal) && int.TryParse(id[7..], out int slot)) { Launch(slot); return; }
        switch (id)
        {
            case "toggle": if (IsVisible) { _userHidden = true; Hide(); Undock(); } else RestoreOverlay(); break;
            case "themes": OpenThemes(); break;
            case "nextTheme": StepTheme(+1); break;
            case "previousTheme": StepTheme(-1); break;
            case "rotate": TogglePlaylist(announce: true); break;
            case "playlist": OpenPlaylist(); break;
            case "nextPalette": SetPalette(ThemePalettes.Next(_settings.ThemePalettes.GetValueOrDefault(ShownTheme))); break;
            case "shortcuts": OpenShortcuts(); break;
            case "drawer": RestoreOverlay(); ShowDrawer(); break;
            case "media": RestoreOverlay(); ShowMediaPage(); break;
            case "lightsPage": RestoreOverlay(); ShowLights(); break;
            case "lightsToggle": RunLights(new LightsCommand(_settings.Lights.Off ? LightsKind.On : LightsKind.Off)); break;
            case "aesthetic": SetAesthetic(!_settings.Aesthetic, announce: true); break;
            case "gaming": OpenGaming(); break;
            case "schedule": OpenSchedule(); break;
            case "telemetry": OpenTelemetry(); break;
            case "sensors": OpenSensorManager(); break;
            default: RestoreOverlay(); break;
        }
    }

    /// <summary>Tray → Double-click the icon: the fixed actions, a theme to show, or a shortcut to open.</summary>
    private Forms.ToolStripMenuItem DoubleClickMenu()
    {
        var themes = ThemeCycle.Available(ThemeStore.RootPath);
        string? ThemeName(string themeId) => themes.FirstOrDefault(t => t.Id == themeId).Name;
        string? ShortcutName(int slot) => slot <= _settings.Shortcuts.Count && !_settings.Shortcuts[slot - 1].IsEmpty ? _settings.Shortcuts[slot - 1].DisplayName : null;
        string current = _settings.TrayDoubleClick;
        var root = new Forms.ToolStripMenuItem("Double-click the icon · " + TrayActions.Describe(current, ThemeName, ShortcutName));
        Forms.ToolStripMenuItem Choice(string label, string id)
        {
            var item = new Forms.ToolStripMenuItem(label) { Checked = id == current };
            item.Click += (_, _) => SavePreference(() => _settings.TrayDoubleClick = id);
            return item;
        }
        foreach (var (id, label) in TrayActions.Fixed)
            root.DropDownItems.Add(id.Length == 0 ? new Forms.ToolStripSeparator() : Choice(label, id));
        root.DropDownItems.Add(new Forms.ToolStripSeparator());
        var showTheme = new Forms.ToolStripMenuItem("Show a theme") { Checked = current.StartsWith("theme:", StringComparison.Ordinal) };
        foreach (var theme in themes) showTheme.DropDownItems.Add(Choice(theme.Name, "theme:" + theme.Id));
        root.DropDownItems.Add(showTheme);
        var open = new Forms.ToolStripMenuItem("Open a shortcut") { Checked = current.StartsWith("launch:", StringComparison.Ordinal) };
        for (int slot = 1; slot <= _settings.Shortcuts.Count; slot++)
            if (!_settings.Shortcuts[slot - 1].IsEmpty) open.DropDownItems.Add(Choice(_settings.Shortcuts[slot - 1].DisplayName, "launch:" + slot));
        if (open.DropDownItems.Count == 0) open.DropDownItems.Add(new Forms.ToolStripMenuItem("No shortcuts yet (tray → Shortcuts)") { Enabled = false });
        root.DropDownItems.Add(open);
        return root;
    }

    private void RebuildTrayMenu()
    {
        var previous = _trayMenu;
        var menu = new Forms.ContextMenuStrip();
        var version = typeof(MainWindow).Assembly.GetName().Version;
        menu.Items.Add(new Forms.ToolStripMenuItem($"Pulsewave System Monitor · V{version?.Major}.{version?.Minor}.{version?.Build}") { Enabled = false });
        if (_targetMissing) menu.Items.Add(new Forms.ToolStripMenuItem("Selected display disconnected · waiting") { Enabled = false });
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Restore overlay", null, (_, _) => Dispatcher.Invoke(RestoreOverlay));
        menu.Items.Add(DoubleClickMenu());
        menu.Items.Add("Hide overlay · keep telemetry running", null, (_, _) => { _userHidden = true; Hide(); Undock(); });
        var startHidden = new Forms.ToolStripMenuItem("Start with overlay hidden (tray only)") { Checked = _settings.StartHidden };
        startHidden.Click += (_, _) => SavePreference(() => _settings.StartHidden = !_settings.StartHidden);
        menu.Items.Add(startHidden);
        try
        {
            var startup = new Forms.ToolStripMenuItem("Launch with Windows · current user") { Checked = WindowsStartup.Enabled };
            startup.Click += (_, _) =>
            {
                try { WindowsStartup.SetEnabled(!WindowsStartup.Enabled); RebuildTrayMenu(); }
                catch (Exception ex) { _tray?.ShowBalloonTip(5000, "Startup setting failed", ex.Message, Forms.ToolTipIcon.Error); }
            };
            menu.Items.Add(startup);
        }
        catch { menu.Items.Add(new Forms.ToolStripMenuItem("Windows startup status unavailable") { Enabled = false }); }

        var monitors = new Forms.ToolStripMenuItem("Display selection");
        foreach (var screen in Forms.Screen.AllScreens)
        {
            var current = screen;
            var label = $"{screen.DeviceName}  ·  {screen.Bounds.Width}×{screen.Bounds.Height}";
            var item = new Forms.ToolStripMenuItem(label) { Checked = screen.DeviceName == SelectedDevice() };
            item.Click += (_, _) => Dispatcher.Invoke(() =>
            {
                SavePreference(() => {
                    _settings.MonitorDeviceName = current.DeviceName;
                    _settings.MonitorInterfaceId = DisplayIdentity.For(current);
                }, PlaceOnTargetMonitor);
            });
            monitors.DropDownItems.Add(item);
        }
        menu.Items.Add(monitors);

        var dock = new Forms.ToolStripMenuItem(_settings.DockEdge.Length > 0 ? $"Dock to screen edge · {_settings.DockEdge.ToLowerInvariant()}" : "Dock to screen edge");
        foreach (var edge in new[] { "" }.Concat(AppBar.Edges))
        {
            var item = new Forms.ToolStripMenuItem(edge.Length == 0 ? "Off · whole display" : edge) { Checked = _settings.DockEdge == edge };
            item.Click += (_, _) => Dispatcher.Invoke(() => SetDock(edge, _settings.DockPercent));
            dock.DropDownItems.Add(item);
        }
        dock.DropDownItems.Add(new Forms.ToolStripSeparator());
        foreach (var (percent, name) in new[] { (0, "Fit the theme"), (15, "Slim"), (22, "Medium"), (30, "Large"), (40, "Extra large") })
        {
            var item = new Forms.ToolStripMenuItem(percent == 0 ? "Fit the theme · as wide as a vertical theme needs" : $"{name} · {percent}% of the display's height")
                { Checked = _settings.DockPercent == percent };
            item.Click += (_, _) => Dispatcher.Invoke(() => SetDock(_settings.DockEdge, percent));
            dock.DropDownItems.Add(item);
        }
        menu.Items.Add(dock);

        var turn = new Forms.ToolStripMenuItem(_settings.HudRotation == 0 ? "Rotate HUD" : $"Rotate HUD · {_settings.HudRotation}°");
        foreach (var (degrees, name) in new[] { (0, "Not rotated"), (90, "90° clockwise"), (180, "180° (upside down)"), (270, "90° counterclockwise") })
        {
            var item = new Forms.ToolStripMenuItem(name) { Checked = _settings.HudRotation == degrees };
            item.Click += (_, _) => Dispatcher.Invoke(() => SetRotation(degrees));
            turn.DropDownItems.Add(item);
        }
        menu.Items.Add(turn);
        var keepClear = new Forms.ToolStripMenuItem("Keep other windows off this display") { Checked = _settings.KeepDisplayClear };
        keepClear.Click += (_, _) => Dispatcher.Invoke(ToggleKeepClear);
        menu.Items.Add(keepClear);
        menu.Items.Add("Touch screen setup (Windows)…", null, (_, _) => Dispatcher.Invoke(OpenTouchSetup));

        var clickThrough = new Forms.ToolStripMenuItem("Click-through · turn off for touch and click controls") { Checked = _settings.ClickThrough };
        clickThrough.Click += (_, _) => Dispatcher.Invoke(() =>
        {
            SavePreference(() => _settings.ClickThrough = !_settings.ClickThrough, ApplyInputStyle);
        });
        menu.Items.Add(clickThrough);

        var topMost = new Forms.ToolStripMenuItem("Always on top") { Checked = _settings.AlwaysOnTop };
        topMost.Click += (_, _) => Dispatcher.Invoke(() =>
        {
            SavePreference(() => _settings.AlwaysOnTop = !_settings.AlwaysOnTop, ApplyInputStyle);
        });
        menu.Items.Add(topMost);

        var nowPlaying = new Forms.ToolStripMenuItem("Show now playing (replaces uptime)") { Checked = _settings.ShowNowPlaying };
        nowPlaying.Click += (_, _) => SavePreference(() => _settings.ShowNowPlaying = !_settings.ShowNowPlaying, UpdateNowPlaying);
        menu.Items.Add(nowPlaying);
        menu.Items.Add("Plexamp · set Plex token…", null, (_, _) =>
        {
            if (_plexTokenForm is { IsDisposed: false }) { _plexTokenForm.Activate(); return; }
            _plexTokenForm = new PlexTokenForm(token => _media.PlexToken = token);
            _plexTokenForm.Show(); _plexTokenForm.Activate();
        });

        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Themes · gallery / import", null, (_, _) => Dispatcher.Invoke(OpenThemes));
        menu.Items.Add("Next theme" + (Hotkey.TryParse(_settings.HotkeyNextTheme, out var nextKey) ? $"  ·  {nextKey}" : ""), null,
            (_, _) => Dispatcher.Invoke(() => StepTheme(+1)));
        var rotate = new Forms.ToolStripMenuItem($"Rotate themes · every {_settings.PlaylistMinutes} min") { Checked = _settings.PlaylistEnabled };
        rotate.Click += (_, _) => Dispatcher.Invoke(() => TogglePlaylist(announce: false));
        menu.Items.Add(rotate);
        menu.Items.Add("Theme playlist / hotkeys…", null, (_, _) => Dispatcher.Invoke(OpenPlaylist));
        menu.Items.Add("Shortcuts · apps and games…", null, (_, _) => Dispatcher.Invoke(OpenShortcuts));
        menu.Items.Add("Lights · OpenRGB…", null, (_, _) => Dispatcher.Invoke(OpenLights));
        menu.Items.Add("Schedule · lights and screen off/on…", null, (_, _) => Dispatcher.Invoke(OpenSchedule));
        menu.Items.Add("Gaming · games…", null, (_, _) => Dispatcher.Invoke(OpenGaming));
        menu.Items.Add(PaletteMenu());
        menu.Items.Add(AestheticMenu());
        menu.Items.Add("Weather · " + (_settings.Weather.IsSet ? _settings.Weather.City : "set your city") + "…", null, (_, _) => Dispatcher.Invoke(OpenWeather));
        var claude = new Forms.ToolStripMenuItem("Claude usage");
        var account = new Forms.ToolStripMenuItem("Read from your Claude sign-in (desktop app)…") { Checked = _settings.ClaudeAccountUsage };
        account.Click += (_, _) => Dispatcher.Invoke(ToggleClaudeAccount);
        var statusLine = ClaudeStatusLine.Status(ClaudeStatusLine.ClaudeSettingsFile, ClaudeStatusLine.LauncherFile).State switch
        {
            ClaudeStatusLine.State.Connected => new Forms.ToolStripMenuItem("Terminal status line · connected") { Checked = true },
            ClaudeStatusLine.State.OtherStatusLine => new Forms.ToolStripMenuItem("Terminal status line · another one is set…"),
            _ => new Forms.ToolStripMenuItem("Terminal status line · connect…")
        };
        statusLine.Click += (_, _) => Dispatcher.Invoke(ToggleClaudeUsage);
        claude.DropDownItems.AddRange([account, statusLine]);
        menu.Items.Add(claude);
        menu.Items.Add("Sensor Manager · discover / map", null, (_, _) => Dispatcher.Invoke(OpenSensorManager));
        menu.Items.Add("Telemetry · session stats / settings", null, (_, _) => Dispatcher.Invoke(OpenTelemetry));
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Exit Pulsewave", null, (_, _) => Dispatcher.Invoke(Close));

        _trayMenu = menu;
        if (_tray != null) _tray.ContextMenuStrip = menu;
        // A menu can be currently open; disposing it immediately would be unsafe.
        if (previous != null) Dispatcher.BeginInvoke(DispatcherPriority.Background, (Action)(() => previous.Dispose()));
    }

    /// <summary>Tray → Aesthetic mode: on/off, fade or hide, how faint, and how long a tap wakes the HUD.</summary>
    private Forms.ToolStripMenuItem AestheticMenu()
    {
        var root = new Forms.ToolStripMenuItem("Aesthetic mode · show the wallpaper") { Checked = _settings.Aesthetic };
        var on = new Forms.ToolStripMenuItem("On" + (Hotkey.TryParse(_settings.HotkeyAesthetic, out var key) ? $"  ·  {key}" : "")) { Checked = _settings.Aesthetic };
        on.Click += (_, _) => Dispatcher.Invoke(() => SetAesthetic(!_settings.Aesthetic, announce: false));
        root.DropDownItems.Add(on);
        root.DropDownItems.Add(new Forms.ToolStripSeparator());
        foreach (var (style, label) in new[] { ("fade", "Fade the HUD"), ("hide", "Hide the HUD") })
        {
            var item = new Forms.ToolStripMenuItem(label) { Checked = _settings.AestheticStyle == style };
            item.Click += (_, _) => Dispatcher.Invoke(() => SavePreference(() => _settings.AestheticStyle = style, () => ApplyAesthetic(DateTime.Now)));
            root.DropDownItems.Add(item);
        }
        var level = new Forms.ToolStripMenuItem("Fade to") { Enabled = _settings.AestheticStyle == "fade" };
        foreach (int percent in new[] { 10, 15, 25, 40 })
        {
            var item = new Forms.ToolStripMenuItem($"{percent}%") { Checked = _settings.AestheticFadePercent == percent };
            item.Click += (_, _) => Dispatcher.Invoke(() => SavePreference(() => _settings.AestheticFadePercent = percent, () => ApplyAesthetic(DateTime.Now)));
            level.DropDownItems.Add(item);
        }
        root.DropDownItems.Add(level);
        var wake = new Forms.ToolStripMenuItem("A tap shows the HUD for");
        foreach (int seconds in new[] { 30, 60, 120, 300, 600 })
        {
            var item = new Forms.ToolStripMenuItem(seconds < 60 ? $"{seconds} seconds" : $"{seconds / 60} minute{(seconds == 60 ? "" : "s")}")
                { Checked = _settings.AestheticWakeSeconds == seconds };
            item.Click += (_, _) => Dispatcher.Invoke(() => SavePreference(() => _settings.AestheticWakeSeconds = seconds));
            wake.DropDownItems.Add(item);
        }
        root.DropDownItems.Add(wake);
        if (_settings.ClickThrough)
            root.DropDownItems.Add(new Forms.ToolStripMenuItem("Waking by tap needs click-through off") { Enabled = false });
        return root;
    }

    /// <summary>Tray → Colors: show the current theme in one of the palettes, or its own colors. Custom themes only.</summary>
    private Forms.ToolStripMenuItem PaletteMenu()
    {
        string current = _settings.ThemePalettes.GetValueOrDefault(ShownTheme) ?? "";
        bool themed = ShownTheme.Length > 0;
        var root = new Forms.ToolStripMenuItem("Colors · " + (themed ? ThemePalettes.Find(current)?.Name ?? "theme's own" : "themes only")) { Enabled = themed };
        var own = new Forms.ToolStripMenuItem("Theme's own colors") { Checked = ThemePalettes.Find(current) == null };
        own.Click += (_, _) => Dispatcher.Invoke(() => SetPalette(""));
        root.DropDownItems.Add(own);
        root.DropDownItems.Add(new Forms.ToolStripSeparator());
        foreach (var palette in ThemePalettes.All)
        {
            var item = new Forms.ToolStripMenuItem(palette.Name) { Checked = ThemePalettes.Find(current) == palette, Image = Swatch(palette) };
            item.Click += (_, _) => Dispatcher.Invoke(() => SetPalette(palette.Name));
            root.DropDownItems.Add(item);
        }
        return root;
    }

    /// <summary>A small strip of the palette's background and accents for its menu item.</summary>
    private static readonly Dictionary<string, System.Drawing.Bitmap> Swatches = new();

    private static System.Drawing.Bitmap Swatch(ThemePalette palette)
    {
        if (Swatches.TryGetValue(palette.Name, out var cached)) return cached;
        var bitmap = Swatches[palette.Name] = new System.Drawing.Bitmap(32, 16);
        using var g = System.Drawing.Graphics.FromImage(bitmap);
        string[] colors = [palette.Background, .. palette.Accents];
        for (int i = 0; i < colors.Length; i++)
        {
            using var brush = new System.Drawing.SolidBrush(System.Drawing.ColorTranslator.FromHtml(colors[i]));
            g.FillRectangle(brush, i * 8, 0, 8, 16);
        }
        return bitmap;
    }

    /// <summary>Shows the current theme in a palette ("" = its own colors) and remembers it for that theme.</summary>
    private void SetPalette(string name)
    {
        string theme = ShownTheme; // the theme showing, also a game's or the schedule's
        if (theme.Length == 0) return;
        SavePreference(() =>
        {
            if (ThemePalettes.Find(name) is { } p) _settings.ThemePalettes[theme] = p.Name;
            else _settings.ThemePalettes.Remove(theme);
        }, ApplyTheme);
    }

    /// <summary>
    /// Gaming sessions: which game is running (checked every 5 s from window programs' paths), fed this
    /// second's FPS and temperatures. The game list is rebuilt every 10 minutes and when shortcuts change.
    /// </summary>
    private void TrackGames(DateTime now, SensorSnapshot values)
    {
        // The game list reads Steam, GOG, Xbox and Windows' own list (a second or two), so it is built in the background.
        if (now >= _catalogDue && !_catalogBuilding)
        {
            _catalogBuilding = true;
            _catalogDue = now.AddMinutes(10);
            var shortcuts = _settings.Shortcuts.ToList();
            var gaming = _settings.Gaming;
            _ = Task.Run(() => GameCatalog.Build(shortcuts, gaming)).ContinueWith(t =>
            {
                _catalogBuilding = false;
                if (t.IsCompletedSuccessfully) _gameCatalog = t.Result;
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }
        if (now >= _gameCheckDue)
        {
            _gameCheckDue = now.AddSeconds(5);
            _running = GameDetector.Running(_gameCatalog);
            if (_running is { } r) _gamePrograms[r.Game.Key] = r.Program;
            // Something unknown full-screen with the GPU busy for a while may be a game: ask once.
            else if (_settings.Gaming.Suggest && _gameCatalog.Count > 0 &&
                     _suggester.Check(GameDetector.FullScreenProgram(), values.Get("GpuLoad"), TimeSpan.FromSeconds(5),
                         path => GameCatalog.Match(_gameCatalog, path) != null || GameCatalog.IsIgnored(_settings.Gaming, path)) is { } candidate)
                SuggestGame(candidate);
        }
        _games.Update(now, _running?.Game.Key, _running?.Game.Name, values.Get("GpuFps"), values.Get("CpuTemp"), values.Get("GpuTemp"), values.Get("GpuFpsLow"));
    }

    /// <summary>Asks on the HUD (and in the tray) whether a program that looks like a game should be tracked.</summary>
    private void SuggestGame(string path)
    {
        if (!_suggestions.Contains(path, StringComparer.OrdinalIgnoreCase)) _suggestions.Add(path);
        string name = GameCatalog.NameOf(path);
        _gamingForm?.Refill();
        _tray?.ShowBalloonTip(6000, "Is this a game?", $"{name} has been full-screen with the GPU busy. Answer on the HUD or in tray → Gaming…", Forms.ToolTipIcon.Info);
        if (_suggestionCard != null) Root.Children.Remove(_suggestionCard);
        var text = new System.Windows.Controls.StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 18, 0) };
        text.Children.Add(OneUi.Label("Track " + name + " as a game?", 22, bold: true));
        text.Children.Add(OneUi.Label("Its sessions and play time will show on the Gaming page.", 15, OneUi.Secondary));
        var buttons = new System.Windows.Controls.StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        buttons.Children.Add(OneUi.Pill("Track", 56, () => { TrackGame(path, name); HideSuggestion(); }, selected: true, size: 18));
        buttons.Children.Add(OneUi.Pill("Never", 56, () => { IgnoreGame(path); HideSuggestion(); }, size: 18));
        buttons.Children.Add(OneUi.Circle("", 56, HideSuggestion));
        var row = new System.Windows.Controls.DockPanel();
        System.Windows.Controls.DockPanel.SetDock(buttons, System.Windows.Controls.Dock.Right);
        row.Children.Add(buttons);
        row.Children.Add(text);
        _suggestionCard = OneUi.Card(row, radius: 40, padding: 14);
        _suggestionCard.Background = OneUi.Frozen(0xF2, 0x14, 0x1A, 0x2C);
        _suggestionCard.VerticalAlignment = VerticalAlignment.Bottom;
        _suggestionCard.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
        _suggestionCard.MaxWidth = Math.Max(300, Root.ActualWidth - 40);
        _suggestionCard.Margin = new Thickness(20);
        _suggestionCard.MouseLeftButtonUp += (_, e) => e.Handled = true; // taps on the card never reach the HUD under it
        System.Windows.Controls.Panel.SetZIndex(_suggestionCard, 50);
        Root.Children.Add(_suggestionCard);
        var card = _suggestionCard;
        _ = Task.Delay(TimeSpan.FromMinutes(1)).ContinueWith(_ => { if (_suggestionCard == card) HideSuggestion(); }, TaskScheduler.FromCurrentSynchronizationContext());
    }

    private void HideSuggestion()
    {
        if (_suggestionCard != null) Root.Children.Remove(_suggestionCard);
        _suggestionCard = null;
    }

    /// <summary>Adds a program as a game (tray → Gaming… or a suggestion) and looks for it straight away.</summary>
    private void TrackGame(string path, string name)
    {
        _suggestions.RemoveAll(s => string.Equals(s, path, StringComparison.OrdinalIgnoreCase));
        _settings.Gaming.Ignored.Remove(path.ToLowerInvariant());
        if (!_settings.Gaming.Games.Any(g => string.Equals(g.Exe, path, StringComparison.OrdinalIgnoreCase)))
            _settings.Gaming.Games.Add(new UserGame { Name = name, Exe = path });
        SaveGaming();
    }

    /// <summary>Marks a program as never a game.</summary>
    private void IgnoreGame(string path)
    {
        _suggestions.RemoveAll(s => string.Equals(s, path, StringComparison.OrdinalIgnoreCase));
        _settings.Gaming.Games.RemoveAll(g => string.Equals(g.Exe, path, StringComparison.OrdinalIgnoreCase));
        if (!GameCatalog.IsIgnored(_settings.Gaming, path)) _settings.Gaming.Ignored.Add(path.ToLowerInvariant());
        SaveGaming();
    }

    private string? SaveGaming()
    {
        _catalogDue = _gameCheckDue = default; // rebuild the game list and look again now
        try { SettingsStore.Save(_settings); return null; }
        catch (Exception ex) { return "Not saved: " + ex.Message; }
    }

    private void OpenGaming()
    {
        if (_gamingForm is { IsDisposed: false }) { _gamingForm.Refill(); _gamingForm.Activate(); return; }
        _gamingForm = new GamingForm(_settings.Gaming, () => _gameCatalog, () => _suggestions, SaveGaming, TrackGame, IgnoreGame,
            ThemeCycle.Available(ThemeStore.RootPath).Select(t => (t.Id, t.Name)).ToList());
        _gamingForm.Show(); _gamingForm.Activate();
    }

    /// <summary>The cover (Steam) or program icon for a game key, as a shortcut tile shows it.</summary>
    private ShortcutView? GameTile(string key)
    {
        string name = _gameCatalog.FirstOrDefault(g => g.Key == key)?.Name ?? _games.History.LastOrDefault(s => s.Key == key)?.Name ?? "Game";
        string? program = _gamePrograms.GetValueOrDefault(key);
        var entry = _gameCatalog.FirstOrDefault(g => g.Key == key);
        var shortcut = key.StartsWith("steam:", StringComparison.Ordinal)
            ? new AppShortcut { Name = name, Target = "steam://rungameid/" + key["steam:".Length..], IconFile = program }
            : new AppShortcut { Name = name, Target = entry?.Exe ?? program ?? "", IconFile = entry?.Logo };
        if (shortcut.IsEmpty) return null;
        return ShortcutView.From(shortcut);
    }

    /// <summary>Fetches the weather every 15 minutes while the theme shows it (5 minutes after a failure).</summary>
    private void PollWeather(DateTime now)
    {
        if (_weatherFetching || !_settings.Weather.IsSet || now < _weatherDue) return;
        _weatherFetching = true;
        var place = new WeatherSettings { City = _settings.Weather.City, Latitude = _settings.Weather.Latitude, Longitude = _settings.Weather.Longitude };
        _ = Task.Run(() => WeatherClient.ForecastAsync(place, now)).ContinueWith(t =>
        {
            _weatherFetching = false;
            if (t.IsCompletedSuccessfully) { _weather = t.Result; _weatherDue = DateTime.Now.AddMinutes(15); }
            else _weatherDue = DateTime.Now.AddMinutes(5);
            if (_weatherForm is { IsDisposed: false })
                _weatherForm.ShowStatus(t.IsCompletedSuccessfully ? $"{DateTime.Now:HH:mm} Weather for {place.City} updated."
                    : $"{DateTime.Now:HH:mm} Weather didn't load ({t.Exception?.GetBaseException().Message}); trying again in 5 minutes.");
        }, TaskScheduler.FromCurrentSynchronizationContext());
    }

    private void OpenWeather()
    {
        if (_weatherForm is { IsDisposed: false }) { _weatherForm.Activate(); return; }
        _weatherForm = new WeatherForm(_settings.Weather, () =>
        {
            try { SettingsStore.Save(_settings); }
            catch (Exception ex) { return "Not saved: " + ex.Message; }
            _weatherDue = default; // fetch for the new city straight away
            RebuildTrayMenu();
            if (IsLoaded) RefreshReadings();
            return null;
        });
        _weatherForm.Show(); _weatherForm.Activate();
    }

    private void SetAesthetic(bool on, bool announce)
    {
        SavePreference(() => _settings.Aesthetic = on, () =>
        {
            _aesthetic.Reset();
            ApplyAesthetic(DateTime.Now);
            RefreshReadings();
        });
        if (announce) _tray?.ShowBalloonTip(2000, "Pulsewave", _settings.Aesthetic ? "Aesthetic mode on · tap the HUD to use it" : "Aesthetic mode off", Forms.ToolTipIcon.Info);
    }

    /// <summary>
    /// Fades the HUD to the aesthetic level (or back). Its background keeps a trace of opacity even when
    /// hidden, so taps still reach the window to wake it. A hidden theme's video pauses.
    /// </summary>
    private void ApplyAesthetic(DateTime now, bool animate = true, bool force = false)
    {
        double level = BlankCover.Visibility == Visibility.Visible ? 1 : _aesthetic.Level(_settings, now);
        if (level == _aestheticShown && !force) return;
        if (level < 1 && _aestheticShown == 1) HideDetails(); // pages are for an awake HUD
        bool dimming = level < _aestheticShown;
        _aestheticShown = level;
        var duration = animate ? TimeSpan.FromMilliseconds(dimming ? 1200 : 250) : TimeSpan.Zero;
        var fade = new System.Windows.Media.Animation.DoubleAnimation(level, duration);
        ClassicView.BeginAnimation(OpacityProperty, fade);
        ThemeHost.BeginAnimation(OpacityProperty, fade);
        // An opaque theme background fades with the HUD (never quite to nothing); Classic and see-through
        // themes have none to fade, so an invisible catcher keeps taps on the window instead.
        var themeBack = _themeView is { } view ? ThemeView.ParseColor(view.Theme.BackgroundColor, Colors.Black) : Colors.Transparent;
        if (themeBack.A >= 128)
        {
            if (Root.Background is not SolidColorBrush { IsFrozen: false } back || back.Color != themeBack)
                Root.Background = back = new SolidColorBrush(themeBack);
            back.BeginAnimation(System.Windows.Media.Brush.OpacityProperty,
                new System.Windows.Media.Animation.DoubleAnimation(Math.Max(level, AestheticMode.TapCatcher), duration));
        }
        else Root.Background = level < 1 ? AestheticCatcher : new SolidColorBrush(themeBack);
        if (level == 0) _themeView?.Pause();
        else if (IsVisible) _themeView?.Resume();
    }

    /// <summary>Classic's see-through background while aesthetic: invisible, but taps still land on the window.</summary>
    private static readonly SolidColorBrush AestheticCatcher = Frozen(System.Windows.Media.Color.FromArgb(3, 0, 0, 0));

    private static SolidColorBrush Frozen(System.Windows.Media.Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private void OpenSensorManager()
    {
        if (_sensorManager is { IsDisposed: false })
        {
            _sensorManager.BringToFront();
            _sensorManager.Activate();
            return;
        }
        var editor = new SensorManagerForm(_settings);
        _sensorManager = editor;
        editor.FormClosed += (_, _) =>
        {
            if (editor.Saved)
            {
                _reader = new SensorReader(_settings);
                _telemetry.Reset();
                RefreshReadings();
            }
            if (ReferenceEquals(_sensorManager, editor)) _sensorManager = null;
        };
        editor.Show();
        editor.Activate();
    }

    private void UpdateHistoryCaptions()
    {
        CpuGraphCaption.Text = $"CPU LOAD · {_settings.HistorySeconds}s";
        GpuGraphCaption.Text = $"GPU LOAD · {_settings.HistorySeconds}s";
    }

    private void OpenTelemetry()
    {
        if (_telemetryForm is { IsDisposed: false })
        {
            _telemetryForm.BringToFront();
            _telemetryForm.Activate();
            return;
        }
        var panel = new TelemetryForm(_telemetry, _settings, () =>
        {
            _timer.Interval = TimeSpan.FromMilliseconds(_settings.PollIntervalMs);
            UpdateHistoryCaptions();
        });
        _telemetryForm = panel;
        panel.FormClosed += (_, _) =>
        {
            if (ReferenceEquals(_telemetryForm, panel)) _telemetryForm = null;
        };
        panel.Show();
        panel.Activate();
    }

    private string SelectedDevice()
    {
        if (!string.IsNullOrWhiteSpace(_settings.MonitorInterfaceId))
            return Forms.Screen.AllScreens.FirstOrDefault(s => string.Equals(DisplayIdentity.For(s), _settings.MonitorInterfaceId, StringComparison.OrdinalIgnoreCase))?.DeviceName ?? "";
        if (!string.IsNullOrWhiteSpace(_settings.MonitorDeviceName)) return _settings.MonitorDeviceName;
        return Forms.Screen.AllScreens.FirstOrDefault(s => DisplaySizes.IsHudPanel(s.Bounds.Width, s.Bounds.Height))?.DeviceName
               ?? Forms.Screen.AllScreens.FirstOrDefault(s => !s.Primary)?.DeviceName
               ?? Forms.Screen.PrimaryScreen?.DeviceName ?? "";
    }

    private void RestoreOverlay()
    {
        _userHidden = false;
        if (WindowState != WindowState.Normal) WindowState = WindowState.Normal;
        PlaceOnTargetMonitor();
        ApplyInputStyle();
    }

    private void SavePreference(Action change, Action? afterSave = null)
    {
        var prior = (_settings.MonitorDeviceName, _settings.MonitorInterfaceId, _settings.ClickThrough,
            _settings.AlwaysOnTop, _settings.StartHidden, _settings.ShowNowPlaying, _settings.ActiveTheme, _settings.PlaylistEnabled);
        try { change(); SettingsStore.Save(_settings); }
        catch (Exception ex)
        {
            (_settings.MonitorDeviceName, _settings.MonitorInterfaceId, _settings.ClickThrough,
                _settings.AlwaysOnTop, _settings.StartHidden, _settings.ShowNowPlaying, _settings.ActiveTheme, _settings.PlaylistEnabled) = prior;
            _tray?.ShowBalloonTip(5000, "Preference not saved", ex.Message, Forms.ToolTipIcon.Error);
            return;
        }
        afterSave?.Invoke(); RebuildTrayMenu();
    }
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr(IntPtr handle, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr(IntPtr handle, int index, IntPtr value);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr handle, IntPtr insertAfter,
        int x, int y, int cx, int cy, uint flags);
}
