# Recap and evidence

## Product
Pulsewave is the current name for the Windows WPF overlay (formerly Graphite System Monitor, then Pulseframe), designed for a dedicated 1920x480 8-inch display viewed at 3–4 feet. HWiNFO supplies 12 mapped sensors, including game-independent FPS by saved source slot. Clocks, readable telemetry, session statistics/CSV, tray operation, startup and stable monitor placement/recovery are established behavior.

## Development history
- V0.5: authoritative user-uploaded validated baseline; transparency/placement/startup/session behavior retained in later versions.
- V0.6.0: Adaptive Visual Engine. Manual presets, image palettes, experimental Windows static-wallpaper lookup, glass tint/opacity/borders, reactive accents and performance mode; separate Appearance window. No desktop capture or direct animated wallpaper sampling.
- V0.6.1: dominant saturated image color replaces whole-image averaging; detected target swatch/hex and active-source display. The user reported this was more vibrant and made the extracted color clear. This aesthetic is the approved baseline.
- V0.6.2: background image analysis with one worker and stale-result rejection; clearer Auto fallback reasons; named visual profiles; extra concurrency/persistence tests and stability tools.

## Evidence ledger
- User reported all V0.6.0 hardware checklist items passed except uncertainty over experimental Auto's visible effect. These include HWiNFO/FPS, legibility, transparency, startup/tray, export and display recovery. This does not automatically certify subsequent changes.
- User confirmed V0.6.1 extraction and swatch design improved vibrancy and clarity.
- V0.6.2 Windows compilation, regression checks and self-contained publish passed in the preceding workspace.
- V0.6.2 90-second synthetic exercise: 5,818 telemetry updates, 180 completed palette results and 90 off-screen app-owned frames. This was not a game benchmark or multi-hour hardware test. Original report is in src/V0.6.2-STABILITY-REPORT.json.
- Shared-workspace setup: imported archive checksum verified; dotnet run --project src/Tests/RegressionChecks.csproj -c Release succeeded here, including stale-worker/shutdown and long-session retention checks. No application source changes were made.
- Pending: extended V0.6.2 gaming/idle session, repeated Appearance/profile cycles, sleep/resume, mixed-DPI reconnect and resource observation on the actual dedicated monitor.

## Known boundaries
Auto reads Windows static wallpaper only. Detected Wallpaper Engine or unavailable source detection uses an explained selected-image/manual fallback. There is no live animated sampling, screen capture or private API integration. Image analysis cannot forcibly cancel an in-progress Windows decode; newer results supersede it and the UI does not wait.

## V0.6.3 candidate update
REL-01, QA-01, UX-01 and CFG-01 assignments are implemented and integrated. Clean automated Windows gates passed; the candidate source archive is retained in releases/. The next gate is dedicated-device acceptance, not additional automatic feature scope. See docs/validation/V0.6.3-INTEGRATION.md.

## V0.6.4 OpenRGB follower
User selected OpenRGB as the color source and RTX5070Ti as the single-color target. Implemented read-only SDK palette mode, explicit endpoint/GPU selection, stable identity, polling/fallback/profile persistence and service/desktop coexistence. Live readback confirmed Gigabyte RTX5070Ti Aero OC changed from #000000 to #02F7EB after the user's cyan change, without agent lighting writes. The user later confirmed overlay behavior, color tracking through effects, SDK stop/start recovery, and all latest-preview acceptance tests. The exact extended-run duration was not recorded. See OpenRGB-Setup.md.

## Pulseframe identity and latest hardware report
The user selected Pulseframe as the product name. A cyan pulse-frame icon is integrated into the WPF window, system tray and executable; a Windows preview package includes PNG and ICO assets. The user reports OpenRGB following works well, confirms recovery after stopping/restarting the SDK server, and confirms Pulseframe follows effects started through OpenRGB's Effects tab. The user confirms all latest-preview acceptance tests are verified; the exact extended-run duration was not recorded. The separate friend-PC GPU label issue was caused by HWiNFO setup, not the application.

## V0.7.0 profile usability and dashboard preview
Implemented applied-profile/custom/default identification, draft dirty/revert state, draft-only Load and built-in-default restore, and validated profile save/rename/delete with confirmation and rollback on save failure. Applying an appearance records the matching saved profile identity. A separate WPF preview shows simulated long text, 100% loads, large network values, temperatures and unavailable-sensor placeholders, styled from draft appearance choices. It reads no sensors, image, wallpaper, OpenRGB or screen capture. The V0.7.0 WPF project compiles with zero warnings and errors, and source and Windows x64 archives were created. The user accepted the compiled executable after reporting it looks good; this does not claim individual re-verification of each acceptance checklist item. Runtime/regression tests were not run. No new hardware behavior is claimed. Release hashes and exact checks are in docs/validation/V0.7.0-release-evidence.md.

## Performance observation
The user reports no noticeable framerate issues in normal use. No benchmark was run or claimed; defer performance profiling and optimization unless a slowdown or new workload concern appears.

## V0.7.1 Pulsewave branding
The user requested replacing current Graphite product naming, including the dashboard header and filenames, with Pulsewave. The implementation updates visible branding and output/source project names, renames the default cyan preset to Midnight without changing its color, migrates settings from `%LOCALAPPDATA%\GraphiteMonitor` to `%LOCALAPPDATA%\Pulsewave` on first launch, and recognizes the previous startup entry. The WPF project compiled with zero warnings/errors and published as `Pulsewave.exe` 0.7.1. New source/binary archives are in `releases/`; existing V0.7.0 archives remain unchanged. Runtime and upgrade acceptance remain pending; see docs/validation/V0.7.1-release-evidence.md and V0.7.1-ACCEPTANCE.md.

## V0.8.0 now playing and Plexamp
The user asked for a now-playing display, previously wanted under Rainmeter, and chose the Time panel: the clock moves up slightly and uptime is replaced. The tile shows album art, playing/paused state, title and artist from Windows media sessions (public API, metadata only). Plexamp does not register with Windows media controls; its local player timeline on port 32500 gives state and track key, and track details come from the user's Plex server with a user-entered token stored with DPAPI and sent only to plex.direct or private-network servers. The release also fixes the Sensor Manager reload race, reuses panel brushes/effects, caches HWiNFO slot reads between periodic scans, and repairs the regression suite (window icon in off-screen renders, Appearance checks out of date since V0.7.0). The WPF project compiles with zero warnings and 190 regression checks pass. Live probes read a browser media session and Plexamp. The user confirmed Plexamp title, artist and art on the dedicated display with a preview build; the full V0.8.0 checklist is pending. The release gate passed and V0.8.0 source and Windows x64 archives are in `releases/`; hashes are in docs/validation/V0.8.0-release-evidence.md.
