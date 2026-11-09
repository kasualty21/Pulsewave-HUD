# Testing and acceptance checklist — V0.6.2

## Completed on Windows, .NET SDK 10.0.402
- Release WPF compilation: passed, zero warnings/errors.
- Self-contained win-x64 single-file publish with documented flags: passed.
- Existing sensor regression suite: passed. Covers FPS source slot after game title changes, missing-slot rejection, correct hive, named-slot migration and ambiguity rejection.
- Session statistics ignore missing samples and graphs retain gaps: passed.
- Saved opacity, monitor identity and FPS mapping round-trip: passed.
- Legacy V0.5 settings acquire conservative Visuals defaults; unknown top-level preferences survive serialization: passed.
- Palette interpolation, convergence, missing/nonfinite telemetry handling and missing-image fallback: passed.
- Windows monitor interface lookup on three attached display entries: passed (read-only).
- Actual WPF XAML parsed, measured, arranged and rendered off-screen at 1920×480 with representative high telemetry values and a long date: passed; layout visually inspected. The XAML/font sizes are unchanged from baseline.
- Separate Appearance form created and rendered off-screen with isolated settings: passed. This is not a live interaction or mixed-DPI usability test.
- Real PNG decoding through the image palette pipeline: passed.
- No user settings/startup edits or desktop capture during tests.

## V0.6.2 focused verification
- Completed 90-second synthetic exercise: 5,818 telemetry updates, 180 completed palette results, 90 off-screen app-owned renders; no exception. Palette Step maximum observed 0.134 ms for this workload (not a benchmark guarantee). See V0.6.2-STABILITY-REPORT.json.
- Outstanding worker cannot overwrite an immediate Manual selection or update disposed UI state: passed.
- 20,000 simulated telemetry polls retain the configured 14,400 records while full-session statistics continue: passed.
- Blocked analysis worker returns UI control immediately: passed.
- 1,000 rapid preference invalidations retain a single worker; stale result rejected and newest snapshot wins: passed.
- Detached preference snapshot, immediate Manual selection and disposed-state behavior: passed.
- Performance mode prevents recurring analysis: passed.
- Visual profiles clone independently, JSON round-trip and restore only appearance; old settings gain an empty library: passed.
- Auto reasons distinguish detected Wallpaper Engine, unavailable process check, static-image success and both missing static/selected images: passed using injected read-only sources.
- Existing palette, image decoder, sensor, telemetry and off-screen layout checks: passed.
- See STABILITY.md and the delivered stability report for automated exercise scope. Real multi-hour gaming/sleep/display recovery remains an on-device acceptance check.

## V0.6.1 focused verification
- Dominant saturated color against neutral backgrounds: passed.
- Complementary hues choose the stronger cluster: passed.
- Transparent pixels ignored; tiny color flecks retain neutral fallback: passed.
- Dark colors receive a hue-preserving lift; grayscale stays neutral: passed.
- Active preset/target diagnostics expose the expected source/color: passed.
- Appearance form with swatch rendered off-screen and visually inspected: passed.

## User-reported V0.6.0 hardware results
The user reported all prior checklist items passed except experimental Auto, whose visible palette difference remained uncertain. Those reports apply to V0.6.0. V0.6.1 still needs an on-device check with two differently colored images and the new source/swatch diagnostics. The checklist below is retained as a regression reference, not a request to repeat every previously passed check.

## Required manual checks on the user's dedicated hardware
These are pending, not claims of completed validation.
- [ ] Compare all 12 live HWiNFO fields, units/scales and unavailable-sensor placeholders to V0.5.
- [ ] Switch games and confirm FPS keeps the saved slot, without choosing another sensor.
- [ ] Review clocks and telemetry at 3–4 feet on the 8-inch screen; test 100% CPU/RAM, long dates/names, large network values and temperatures.
- [ ] Apply each preset; change tint, borders, panel opacity; confirm text stays opaque. Re-enable high contrast after testing transparency.
- [ ] Choose a local image; delete/move it; confirm manual fallback. Test missing/unsupported/oversize images.
- [ ] Enable experimental Auto with static wallpaper, slideshow and Wallpaper Engine running/stopped. Confirm status never implies live animated sampling; set a selected-image fallback.
- [ ] Confirm low-power mode stops recurring palette reads and removes glow while statistics/export continue.
- [ ] Compare game frame times and idle CPU/GPU use with accents off/on; no performance benchmark has been claimed.
- [ ] Restart and reload settings; verify visuals, mappings, scaling, monitor identity and opacity survive. Confirm no obsolete settings window remains open.
- [ ] Compare session CSV export/reset/alerts against V0.5.
- [ ] Hide/restore overlay, tray-only launch, click-through, always-on-top and launch-with-Windows from the published EXE.
- [ ] Disconnect/reconnect the selected display, sleep/resume, change monitor numbering and test mixed DPI. Confirm no overlay jumps onto a game display.
- [ ] Keep Wallpaper Engine playing underneath and confirm transparent overlay behavior. No wallpaper integration/control is expected.

The test renderer creates work-area layout-preview.png/settings-preview.png next to the extracted project directory; these contain app-owned controls only.



