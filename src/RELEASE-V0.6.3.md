# V0.6.3 candidate - assignment results

## Included
- REL-01: coherent palette source/status/sampling/last-success diagnostics, with gated worker and deterministic throttle regressions.
- UX-01: applied-source/swatch and sampling status distinguished from edited controls; profile Load remains clearly unapplied.
- CFG-01: damaged settings cannot be erased by temporary startup defaults; flushed atomic saves retain the previous valid file as .bak. Tray saves roll back unsaved values and never apply placement/style/demo side effects after a failed save.
- QA-01: isolated tests and a repeatable staged build/publish/source-checksum gate. Workspace automation lives in scripts/Check-Release.ps1; application source package includes the regression suites.

## Upgrade
Exit the running version, retain its folder and back up settings.json, extract this candidate to a separate folder, then run build-win-x64.ps1 and launch publish-win-x64/GraphiteMonitor.exe. Keep the executable in its permanent location before re-registering startup. Do not run two versions concurrently.

## Damaged configuration
If settings cannot be read, the original remains unchanged and the app temporarily uses defaults. A startup message reports this. Saves are blocked while the existing file is damaged. Close Graphite and retain a copy of the damaged file; repair it or explicitly copy a known-good settings.json.bak to settings.json, then relaunch. The .bak is created after a successful replacement save, not on first installation. Nothing automatically chooses a backup or discards the damaged file.

## Validation scope
Candidate Windows build, regressions and self-contained publish are exercised by the workspace release gate. Protected dashboard XAML, sensor reader/catalog/editor, telemetry engine/form, DesktopIntegration and App sources are unchanged from V0.6.2. No live overlay, user settings, startup or monitor topology are changed by the regression suite.

The 90-second report bundled from V0.6.2 remains historical evidence. No multi-hour V0.6.3 gaming/idle/sleep/reconnect qualification is claimed. Use STABILITY.md for the device exercise. V0.6.3 remains a hardware-acceptance candidate until those results are recorded.
