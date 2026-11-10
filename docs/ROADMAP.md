# Roadmap

Milestones are ordered by dependencies and evidence, not fixed dates. Candidate scope may change after source review; planning recommendations are not confirmed bugs.

## V0.6.3 — Hardening and clarity (completed)

| Lane | Work | Acceptance |
|---|---|---|
| Reliability | Review palette lifecycle across rapid mode/profile changes, settings reload, Appearance close/reopen and application shutdown; harden only proven gaps. | One worker, newest selection wins, no late UI writes, Manual remains immediate, performance mode has no recurring reads. Automated cases cover transitions and failures. |
| Coordinator / persistence | Review malformed/truncated settings, missing/null profile fields, interrupted saves and migration fixtures from previous versions. Define recovery before implementing. | Recover or provide actionable guidance without silently replacing the original damaged file; retain sensor mappings and monitor identity; no recurring notification spam. |
| Visuals / UX | Group active source, update/sampling state and fallback explanation; clarify saved profile versus edited/applied state. | Source/status/swatch describe the same accepted result; Auto never implies animated capture; performance-mode state is explicit. Changes stay in Appearance, not the overlay. |
| Release / QA | Create repeatable clean build/regression/package checks and an evidence manifest; prepare the extended device test record. | Fresh extracted release builds and tests; version/checksum/package contents recorded; synthetic and hardware evidence separated. |

Integration order: agree source/status contract → reliability and independent QA tooling → Appearance presentation → coordinator settings/model changes if warranted → integrated tests → candidate package → real-device acceptance. Parallel work begins only on assigned non-overlapping files.

V0.6.3 exit: automated gates pass and a candidate is packaged; do not call it fully hardware-qualified until the user completes the applicable device checks. Suggested extended observation: 4–8 hours spanning idle/gaming and normal recovery events. Exact workload and duration must be recorded.

## V0.6.4 — OpenRGB follow and Pulseframe identity (hardware acceptance complete)
- Implemented and packaged read-only OpenRGB color following, explicit GPU selection, palette fallbacks, and the Pulseframe icon/visible branding. Pulseframe was an interim name; V0.7.1 adopts Pulsewave.
- User confirms the latest preview's acceptance tests are all verified, including OpenRGB color following, SDK stop/restart recovery, animated effect following, and the dedicated-display checks.
- An earlier report described the build as running for a while; no exact duration was recorded.
- Do not claim an extended multi-hour test without recording its duration and conditions.

The other PC's GPU label issue was resolved by correcting its HWiNFO setup; it is not an application blocker.

## V0.7 — Profile usability and preview (user accepted)
- Active-profile indication, explicit dirty state, rename/delete and a safe restore-visual-defaults action.
- Representative preview in the configuration window for long dates, 100% loads, large network numbers and missing sensors.
- Acceptance: profile actions are predictable and reversible; appearance-only changes preserve mappings/placement/startup/session state; full-opacity large text and high-contrast behavior remain clear.
- Dependency: V0.6.3 persistence/lifecycle contracts, completed V0.6.4 device acceptance, and user feedback. Avoid adding features merely because they are listed here.
- Implemented and packaged in V0.7.0. The preview uses simulated stress values and reads no telemetry, images, wallpaper, OpenRGB or screen capture. Compile and package checks passed; the user accepted the compiled executable. This does not claim each checklist item was individually retested.

## V0.7.1 — Pulsewave branding (implementation and packaging complete; user acceptance pending)
- Replace current product branding and output/source filenames with Pulsewave, including dashboard, app/window titles, executable, icon filenames, session export names and setup docs.
- Preserve existing configuration with one-time settings migration and compatibility for the former Windows startup entry.
- Rename the cyan Graphite preset to Midnight while retaining its color and migrating old saved values.
- Acceptance: retain HWiNFO mappings/FPS slot, monitor identity, opacity, profiles, startup preference, session behavior and OpenRGB following across upgrade.
- Implemented and packaged in V0.7.1. The project compiles cleanly and publishes as a self-contained Windows x64 executable. Migration and user acceptance remain for the checklist.

## V0.8 — Now playing and Plexamp (implemented and packaged; Plexamp confirmed on device)
- Replace system uptime in the Time panel with a now-playing tile (art, state, title, artist); move the clock up slightly without changing its size.
- Read Windows media sessions through the public media API; add Plexamp through its local player timeline and the user's Plex server with an encrypted, user-entered token.
- Fix the Sensor Manager settings-reload race, reduce per-refresh rendering and registry work, and restore the regression suite to a full passing run.
- Acceptance: tile legible at 3–4 ft; long titles stay inside the panel; tray toggle restores uptime; token is never stored in plain text or sent outside Plex/local-network servers; all earlier mappings, placement, startup, session, appearance and OpenRGB behavior unchanged.
- Implemented and packaged in V0.8.0. Build has no warnings, 190 regression checks pass, and live probes read Windows media sessions and Plexamp. The user confirmed Plexamp title, artist and art on the dedicated display with a preview build. The full checklist is pending.

## V0.9.0 — Themes, touch pages, lights and media (released 2026-11-07)
- Theme engine and editor, L-Connect import, Halo and One UI, touch pages, OpenRGB LED control, schedule and display power, weather, aesthetic mode, palettes. Partly confirmed on the user's hardware; see `validation/V0.9.0-ACCEPTANCE.md`.

## V0.9.1 — Halo versions, monochrome themes, gaming and night dimming (released 2026-11-08)
- Halo Lifestyle / Gaming / Productivity, Graphite / Paper / Noir, warning colors, tinted dock, editor rework, refreshed templates 01–25, gaming sessions, stale-sensor dimming, night dimming, limit tags, assignable tray double-click. 39 templates pass the quality gate with realistic and worst-case values.
- Automated evidence only so far; see `validation/V0.9.1-ACCEPTANCE.md` for what still needs a device check.

## V0.9.2 — Fixes and hardware acceptance (released 2026-11-09)
Fixes and verification, no new features. Items marked *verify* are implemented but unconfirmed on real hardware; the rest need code.

Status 2026-11-08 (user's results and the fixes since, unreleased): 1 done (48 shortcuts, drawer pages, picker unticked with search and slot count); 2 done (crash record, display-restore.json); 3 screen wake confirmed by the user, LEDs failed — fixed (missing OpenRGB profile now falls back to the saved color); 4 confirmed by the user; 5 and 6 still to check; 7 confirmed by the user; 8 failed — fixed (service process and hidden popup windows); 9 done (outline previous / next, FAN OK / STOPPED; model names were already 11–14 px, unchanged); 10 done (`scripts/Run-Tests.ps1`); 11 done (one checklist, `validation/V0.9.1-ACCEPTANCE.md`). Not yet seen on hardware: the fixes for 1, 2, 3, 8 and 9.

| # | Item | Kind | Acceptance |
|---|---|---|---|
| 1 | Steam import picker: start with nothing selected (or only installed, recently played games) instead of all; explain the 24-shortcut limit where it bites; decide whether to raise `AppShortcut.MaxCount` (slot ids `launch:N`, the drawer's pages, settings size and icon loading all scale with it). | Fix | Importing a large Steam library never silently drops games; the limit is stated in the picker; older shortcut slots and `launch:N` tray choices keep pointing at the same apps. |
| 2 | Night dimming backlight on the Y70 (DDC/CI brightness, VCP 0x10). | Verify | Backlight drops at the rule's start, returns to the previous level at its end and when Pulsewave exits. Decide what happens after a crash (the saved level is not restored today). |
| 3 | Schedule screen-off end with the read-back fix; one overnight run. | Verify | Y70 wakes by itself at the period end; duration and conditions recorded. |
| 4 | Performance-limit tags: confirm HWiNFO's sensor names on this PC (Core Ultra 7 265K, RTX 5070 Ti) match the suggestions in Sensor Manager. | Verify / fix | THERMAL / POWER LIMIT appears during a real limit and clears afterwards; adjust the name matching if HWiNFO labels differ. |
| 5 | Stale-sensor dimming against a real HWiNFO stop / restart. | Verify | Readings dim within ~30 s of HWiNFO closing, Classic shows OFFLINE, and both recover without restarting Pulsewave. |
| 6 | Tray → Double-click the icon menu, clicked through on the real tray (theme, shortcut, playlist, deleted-theme notice). | Verify | Each choice is saved and runs on double-click; settings from 0.9.0 still show the HUD. |
| 7 | Halo Lifestyle / Gaming / Productivity and Graphite / Paper / Noir on the 8.8" bar and the Y70 at 3–4 ft; the theme editor used by hand. | Verify | User picks keepers and lists changes; editor round-trips a duplicated template without losing settings. |
| 8 | Open OpenRGB from the Lights page (never tried live). | Verify | Brings a running OpenRGB forward or starts it; never a second copy. |
| 9 | Leftover design-review items: quick-action and dock icons still mix outline and solid styles; fan RPM shown only when 0 or abnormal on Halo Gaming; CPU / GPU model names smaller. | Fix | Quality gate passes; one icon style per screen. |
| 10 | Dev workflow: a running Pulsewave locks `bin\Release` and the tests' output folder. Give test runs their own output folder per run. | Fix | Suite runs while the app is open, without hand-picked OutDir folders. |
| 11 | Close out the open acceptance checklists (V0.7.1, V0.8.0, V0.9.0) by folding still-relevant items into the 0.9.1 checklist. | Docs | One current checklist; older ones marked superseded, not deleted. |

## After 0.9.2 — Agreed plan (user-confirmed 2026-11-09)
Decided in a design review with the user. Released in small steps, each checked on the displays before the next: 0.9.2 first, then 0.10, 0.11, 0.12. None reintroduces a removed feature or captures the screen.

**0.9.2 release gate:** the seven items under "Fixed after 0.9.1" in `validation/V0.9.1-ACCEPTANCE.md` confirmed on hardware (LEDs back on in the saved color, Open OpenRGB, kill-and-restart restoring backlight and display, Steam picker / 48 shortcuts, Auto-pair, FAN OK, outline and centred icons). The rest of that checklist stays open and does not block it.

### V0.10 — The HUD picks the theme for the moment (released 2026-11-09)
- **Game-aware themes.** One default "theme while gaming" plus optional per-game overrides, both in tray → Gaming (a choice at the top; a theme column per game, blank = default). When the session ends (after the existing 30 s grace) the theme from before the game returns, unless the schedule now wants another.
- **Schedule themes.** "Show theme ___" as a fourth action on the existing rules (beside LEDs, screen, dim). Rules already overlap (up to 16, combined); when two want different themes, the **topmost** rule wins, as it already does for dimming. The Schedule window gains ▲ / ▼ to arrange rules.
- **Precedence**, highest first: a theme the user picks (tray, gallery, swipe, hotkey; held until the current game or schedule period ends) → a running game → the schedule → the playlist (paused while any of those is in control, resumed after).
- **Hidden HUD:** switches happen silently; the HUD never unhides itself.
- Acceptance: precedence, restore-after-game and topmost-wins covered by fixture tests; older settings (no gaming theme, no theme actions) behave exactly as before; checked on the Y70 with a real game.

### V0.11 — Frame time and shortcut lists (released 2026-11-09)
- Halo Gaming's card chart becomes frame time in ms (replacing the FPS chart; live FPS stays the hero number), labelled "FRAME TIME · SAMPLED" because HWiNFO passes one reading per poll: it shows sustained stutter, not every single hitch. Dashed guide at 16.7 ms (60 FPS), automatic scale never below 25 ms, amber above 33 ms.
- 1% low comes from RTSS's "Framerate 1% Low" when paired, else today's slowest-1%-of-seconds figure.
- Both RTSS readings become optional sensors that Sensor Manager's Auto-pair matches (unit-checked: ms, FPS).
- Added by the user when 0.11 started (2026-11-09): separate shortcut priority per Halo setup. Tray → Shortcuts → List picks and orders shortcuts for Gaming and Productivity; Halo Gaming and Halo Productivity show their list on their tiles and first in their app drawer (then the rest); Halo Lifestyle and other themes keep the main list; an empty list behaves as before.

### V0.12 — Hotkeys for any action (released 2026-11-09)
- A tray → Hotkeys window mapping key combinations to any action from the tray double-click list. The three existing hotkeys (next / previous theme, rotation on/off) move in with their keys unchanged.

### Decided against or held
- **Dropped:** gaming session CSV export; limit alerts (the HUD's limit tags already show it, and pop-ups mid-game are alarm fatigue); named schedule sets (overlapping rules already cover concurrent schedules).
- **Held until Pulsewave goes public:** theme packages (export / import a theme with its pictures and fonts).

## Later — Evidence-led refinements (backlog)
- User reports no noticeable framerate issues in normal use. Defer formal performance profiling and optimization unless a slowdown appears or new workload evidence suggests a need; this is not a benchmark result.
- Additional palette options only if real wallpaper examples expose a need.
- Optional import/export of visual profiles, with image-path portability explained.
- Direct animated wallpaper sampling remains out of scope unless a supported, privacy-preserving and reliable approach is demonstrated. No desktop/game capture workaround.

## Decisions already established
Keep Wallpaper Engine independent, maintain the uncluttered dedicated display, preserve existing mappings/settings on upgrade, and retain the V0.6.1 visual design. Keep this chat as coordinator; specialists are delegated tasks, not autonomous permanent services.
