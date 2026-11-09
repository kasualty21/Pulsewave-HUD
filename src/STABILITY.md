# V0.6.2 stability check

The goal is to check responsiveness and resource trends during normal use. Keep a backup of settings.json and retain your working V0.6.1 folder. Hardware checks already passed on V0.6.0; V0.6.1's vibrant extraction was subsequently confirmed by the user. V0.6.2's new concurrency/profile checks are automated; this real-device exercise is still pending.

## Short acceptance check
1. Exit the previous version; build/publish V0.6.2 and launch it from its permanent folder. Existing settings carry forward.
2. Open Appearance. In Image mode choose two very different images and Apply repeatedly. Clocks/telemetry and the configuration window should remain responsive. After the last selection settles, its source and swatch should match; an older result should never replace it.
3. Apply a missing/corrupt image: source status should explain the failure and identify the manual fallback. Apply Auto while Wallpaper Engine runs: expect an explicit animated-sampling limitation and image/manual fallback. Stop Wallpaper Engine yourself if you want to test Windows static wallpaper.
4. Save a profile, change the controls and apply, then load your profile and Apply. Verify palette, opacity, tint, borders and glow restore while monitor selection and sensor mappings remain unchanged. Restart and confirm the saved profile remains available.
5. Enable performance mode. An explicit image change may analyze once; recurring image reads/glow should stop. Clocks, sessions and export should continue. Restore your preferred settings afterward.

## Four-hour observation during normal use
Optionally run, from this source directory:

    powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Observe-Stability.ps1 -Minutes 240 -IntervalSeconds 30

This observer reads one running Pulsewave.exe process and writes a timestamped CSV beside the script. It does not launch/stop apps or alter settings, monitors, games or wallpaper. Ctrl+C stops it. Save your own notes with times for game launches, sleep/resume, display disconnect/reconnect and profile changes. After sleep, gaps in observations are expected. If Pulsewave exits, the observer stops with an error; it does not silently attach to a different process.

- Spend time idle, gaming and using Wallpaper Engine. Try repeated Appearance open/close and profile changes.
- If convenient, manually test sleep/resume and dedicated-display reconnect. Confirm stable recovery and no overlay on the game display.
- Review clocks, FPS slot, session counters/export and tray controls afterward.
- Compare memory, handles and threads after warm-up at similar workload points. A temporary increase during WPF/image initialization or telemetry retention is expected; steady sustained growth deserves investigation. CPU is cumulative seconds, not a percentage. Working set alone is not a leak test.
- Record a failure with its timestamp, active mode/status and the observer CSV. No screenshot collection is needed.

## Developer exercise (isolated synthetic workload)

    dotnet run --project .\Tests\RegressionChecks.csproj -c Release -- --soak --seconds 90

Runs regressions, then alternates valid/corrupt image sampling and performance-mode changes, ingests demo telemetry at accelerated cadence and renders app-owned WPF content off-screen. Writes soak-report.json and soak-session.csv beside the extracted project directory. It never reads user settings, edits startup, captures the desktop or changes monitor topology. Increase --seconds (maximum 14400) for a longer synthetic exercise.

This is not a gaming frame-time benchmark, a live HWiNFO test or a sleep/display-recovery test. No multi-hour stability certification is claimed.
