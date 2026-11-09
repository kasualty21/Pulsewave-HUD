# Graphite System Monitor V0.5.0

Windows x64 / .NET 10 WPF overlay for an 8-inch 1920×480 display, built from the supplied V0.4.1 ZIP. Large typography, layout, HWiNFO sensor mappings, FPS slot matching across games, Sensor Manager, graphs, alerts and session telemetry are preserved.

## Build and upgrade

1. Exit the previous version through its tray menu. Back up `%LOCALAPPDATA%\GraphiteMonitor\settings.json` if desired.
2. Extract into a new directory. Install the .NET 10 SDK if needed.
3. Open PowerShell here and run:

   ```powershell
   powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build-win-x64.ps1
   ```

4. Run `publish-win-x64\GraphiteMonitor.exe`. This self-contained build needs no separate runtime on the destination. Keep it in a permanent folder before enabling startup.
5. Right-click the tray icon for controls. Existing settings are reused; session counters restart on launch as before.

## New tray controls

- **Launch with Windows · current user:** opt-in. Adds only `HKCU\Software\Microsoft\Windows\CurrentVersion\Run\GraphiteMonitor` with a quoted executable path. Uncheck to remove. No administrator rights, service or task. Windows Startup Apps can also disable it; the tray check indicates registration, not Windows policy approval. After moving/upgrading the executable, disable and re-enable from the new executable. Source runs through dotnet cannot register startup.
- **Start with overlay hidden:** saved tray-only startup preference. **Hide overlay** temporarily hides it while telemetry continues; **Restore overlay** or tray double-click restores without activating it. Exit is explicit through the tray. No overlay taskbar/Alt+Tab entry. Settings windows opened explicitly can take focus.
- **Display selection:** saves a public Windows monitor interface path independent of DISPLAY1/2 numbering. Existing selections migrate on first placement. When the selected screen disconnects, the overlay hides and keeps sampling without moving to another monitor. Display/device/DPI/resume messages trigger a two-second settling delay, backed by a ten-second recovery check. Reconnection restores unless deliberately hidden.
- **Panel background opacity:** 0–100% tray presets; any integer percentage may be set as `PanelOpacityPercent` in settings.json and reloaded. Only the header/card backgrounds change. Text, graphs, borders and layout retain opacity. High contrast imposes a 92% minimum, retaining your saved percentage underneath. Turn high contrast off to use lower opacity. Try 55–78%, then check at 3–4 feet.
- **Detect Wallpaper Engine process:** optional, off by default. Status refreshes when the tray menu opens. It reports wallpaper32/wallpaper64 process presence only; playback, paused state, active wallpaper and monitor assignment remain unknown.
- A per-user, per-Windows-session instance guard prevents duplicate V0.5 overlays. A second launch exits quietly. Older versions must be exited before upgrading.

## Wallpaper Engine coexistence

The desktop shows between panels and through their adjustable backgrounds. Graphite does not reparent into Explorer/WorkerW, call private APIs, change wallpapers, control playback, edit Wallpaper Engine settings, or change monitor configuration. Wallpaper Engine is not required.

Wallpaper Engine may apply fullscreen/application rules to an overlay. If the wallpaper pauses, inspect its Performance / Application Rules yourself and choose behavior appropriate to your games/displays. Graphite leaves these choices to you. See the [official application rules guide](https://help.wallpaperengine.io/en/functionality/applicationrules.html). Process presence is not playback state. The [documented command interface](https://help.wallpaperengine.io/en/functionality/cli.html) offers controls, but Graphite invokes none to avoid changing your setup.

Monitor identity uses Microsoft's [EnumDisplayDevicesW monitor interface flag](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-enumdisplaydevicesw). It generally survives numbering changes; changing ports, docks, drivers or hardware can change the path. Re-select the intended screen from the tray if necessary. Mirrored displays cannot provide independent placement; use an extended desktop.

## Verification and acceptance

Run the included checks, which do not change live preferences or startup entries:

```powershell
dotnet run --project .\Tests\RegressionChecks.csproj -c Release
```

Verified during packaging on Windows, SDK 10.0.402:

- Release build: zero warnings/errors; self-contained win-x64 single-file publish succeeded.
- Regression checks passed: FPS slot/hive matching after game-title changes, missing FPS slot, named sensor renumbering/ambiguity, missing telemetry samples and graph gaps, settings serialization and legacy defaults.
- Native monitor interface lookup succeeded on three connected displays.
- SensorReader, SensorCatalog, SensorManagerForm, TelemetryEngine, TelemetryForm and Original-AIO.ini are byte-for-byte identical to V0.4.1.
- No overlay was launched, startup registered, or Wallpaper Engine controlled during checks.

Hands-on checks still required:

1. Confirm display selection, all 12 fields, FPS in two games, session stats and CSV export.
2. With a game or text field active, restore/hide the overlay and reconnect its screen. Confirm focus stays in the foreground app and the overlay never relocates to another screen.
3. Disconnect/reconnect, sleep/resume, change display ordering and mixed DPI. Allow ten seconds for recovery. Explicit Hide must remain hidden.
4. Enable startup from a permanent published location; sign out/in and check visibility/tray access. Disable and sign in again to verify removal.
5. Compare 40/55/78/100% panels over bright animated wallpaper at viewing distance. Check text/graphs retain intensity, high contrast restores readability, and settings survive restart. Check your own Wallpaper Engine application rules.

Live HWiNFO, actual hotplug/sleep recovery, game focus, sign-in startup and Wallpaper Engine visual behavior were not exercised during packaging. Compile/lookup checks do not replace these hardware tests. FPS slot mode still requires HWiNFO to retain its slot. Registry readings have no reliable sample timestamp. Decorative insets retain existing styling; opacity applies to main panel/header backplates. Historical UI notes are in V0.4.1-NOTES.md.
