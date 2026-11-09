# Device acceptance record

Candidate version / source SHA-256:
Published executable SHA-256:
Tester / date / Windows version:
8-inch display model / resolution / scaling / connection:
GPU / driver / HWiNFO / Wallpaper Engine versions:
Evidence type: user-observed hardware acceptance (not synthetic automated evidence).

Record Pass / Fail / Not tested, observations, and reproduction steps for each item:

- All 12 HWiNFO fields, scales, units and missing values match prior behavior.
- Saved FPS slot persists across game changes without sensor reselection.
- Clock/date and worst-case telemetry remain legible from 3–4 feet at 1920x480.
- Presets, vibrant selected-image extraction, opaque text, high contrast and independent panel opacity work.
- Missing/moved/corrupt/unsupported/oversize selected images recover with explicit fallback.
- Auto reports static wallpaper only; static/slideshow/Wallpaper Engine running/stopped/unknown states use accurate source and fallback text. No animated sampling claimed.
- Low-power mode stops recurring palette reads and glow; session statistics/export continue.
- Visual profiles restore appearance without changing mappings, startup, display identity or sessions.
- Restart preserves mappings, visuals, monitor identity, scaling and opacity.
- Session CSV, reset and alerts match prior behavior.
- Published EXE hide/restore, tray-only launch, click-through, always-on-top and startup work.
- Display disconnect/reconnect, sleep/resume, numbering and mixed DPI recover without moving onto game display.
- Wallpaper Engine continues independently beneath transparent overlay.
- Extended idle/gaming observation: duration, memory, CPU/GPU and frame-time observations with accents off/on. Record method; do not claim a benchmark from subjective impressions.

Outstanding failures / deferred items:
Acceptance decision and reviewer:
