# Pulsewave System Monitor V0.10.0

## V0.10.0 - The HUD picks the theme for the moment

**Theme while gaming.** Tray → Gaming → "Theme while gaming" shows a theme (e.g. Halo Gaming) while any recognised game runs; a game can have its own theme or "Don't switch" (pick it in the list, then "This game"). Your own theme returns when the game ends.

**Schedule themes.** A schedule rule can show a theme for its period (tray → Schedule → Show theme), e.g. Productivity on weekday daytime. When rules overlap the topmost wins; ▲ ▼ arrange them.

**Who wins.** A theme you pick by hand holds until the game or schedule period running then ends; a game comes before the schedule; theme rotation waits while either is in control. Game and schedule themes never replace your own saved theme, and never show a hidden HUD.

## V0.9.2 - Fixes: LEDs, Open OpenRGB, centred icons, crash-safe dimming

**Fixed.** At the end of a schedule's off period the LEDs now come back on even when the OpenRGB profile they were last set from is gone (the saved color is used instead). Lights → Open OpenRGB brings the OpenRGB window forward (it used to stop at OpenRGB's background service, or pick one of its hidden menus). Icons sit centred in the round buttons of the HUD's own pages (Now playing, Lights, Volume, Colors, the app drawer); they were 3–6 px low. If Pulsewave is closed unexpectedly while it has lowered the display's backlight or put it in standby, the next start puts both back.

**Improved.** Up to 48 shortcuts (was 24); the app drawer shows 24 to a page. The Steam games picker starts with nothing ticked, has a search box and counts free slots. Sensor Manager → Auto-pair pairs every missing reading with the right scale in one click. Halo Gaming's CPU card shows FAN OK, turning red STOPPED only if the fan stops. Previous / next buttons use outline icons that match the quick actions.

## V0.9.1 - Halo versions, monochrome themes, gaming sessions and night dimming

**Templates.** Thirty-nine built-in templates. Halo now comes in three versions, each for the 8.8" bar and the HYTE Y70 column: **Halo Lifestyle** (the former Halo and Halo Column; same ids, so a theme in use carries over), **Halo Gaming** (live FPS, the game session, CPU and GPU cards) and **Halo Productivity** (timer, stopwatch and Pomodoro, AI limits, drives). New monochrome pairs: **Graphite**, **Paper** and **Noir**, with album art and icons in grey. The older templates keep their layouts and gain warning colors, readable dates, tappable now playing and tinted shortcut tiles. Every template passes the quality gate with realistic and worst-case readings.

**Readings.** Temperatures, memory and AI limits turn amber and red at set levels (CPU 90 / 98 °C, GPU 83 / 90 °C, drives 70 / 80 °C); loads never warn. When HWiNFO stops updating, readings dim and Classic says SENSORS PAUSED or OFFLINE. Optional HWiNFO limit sensors show THERMAL / POWER / VOLTAGE LIMIT tags (tick them in Sensor Manager and HWiNFO's gadget). Weather adds the next hours and when rain starts.

**Gaming.** Pulsewave notices the running game (Steam, GOG, Xbox / Game Pass, games Windows recognises and games you add) and records each session: length, average FPS, 1% low and peak temperatures. Tray → Gaming lists the games; the Gaming page shows today and the past week. Nothing is captured.

**Schedule and tray.** A schedule rule can dim the HUD at night: darken the theme, lower the display's backlight over DDC/CI (restored afterwards and on exit), or both. Tray → Double-click the icon picks what double-clicking the tray icon does: show the HUD (default), a theme, a shortcut, the next theme, the playlist and more.

**Editor and windows.** The theme editor takes the Halo look with folding property sections, a searchable data picker and controls for warning colors, tile styles and date formats. Tray windows share the Halo look. Classic keeps its Midnight colors.

## V0.9.0 - Themes, Halo, touch pages, lights and media

**Themes.** Tray → Themes holds Pulsewave Classic plus twenty-nine read-only built-in templates for the 8.8" bar, the HYTE Y70 Touch Infinite (either way up), vertical bars and tablets, including **Halo** and **Halo Column**, Pulsewave's signature style, and the **One UI** pair. Duplicate a template to change it in the theme editor (drag, resize, groups, ready-made widgets, color tools), or import L-Connect 3 themes. The gallery and playlist filter by shape. **Colors** recolors any custom theme in one of 18 palettes without changing its files (tray → Colors, or the Colors page on the HUD). A playlist and global hotkeys rotate themes.

**Touch pages** (turn click-through off in the tray). Taps on theme elements open pages styled after Samsung One UI in the Halo tint: **Now playing** (art, seek bar you drag, shuffle, repeat and with Plexamp its own volume and the next 30 tracks of its queue), **Volume** (a slider per app with sound), **Lights**, **Colors** and the **app drawer**. Sliders follow the finger; lists scroll with a drag and glide after a flick.

**Lights.** Pulsewave controls RGB through OpenRGB's SDK server on this PC: profiles, a solid color, brightness and hardware effects, from tray → Lights or the HUD's Lights page (Open OpenRGB brings the program forward). It only sends commands when you tap or click; it no longer follows the lights. Tray → Schedule turns the LEDs off and on at set times and can blank the HUD (and put its display in standby over DDC/CI) overnight.

**More.** Weather for a city you set (tray → Weather; Open-Meteo, no account). Aesthetic mode fades or hides the HUD to show the wallpaper under it until tapped. Shortcuts show larger, rounded, bevelled icons and Steam cover art on tall tiles. Claude Code and Codex plan usage can be shown. Touching the HUD no longer moves the mouse cursor.

**Removed since 0.8.0** (settings kept untouched): the Appearance window and its palette modes and profiles, OpenRGB color following, demo mode, Wallpaper Engine detection, panel opacity/contrast options and the settings.json tray items. Classic keeps its default Midnight colors.
Windows 10 1809 or later, x64, .NET 10, WPF + Windows Forms. Based exclusively on the uploaded, validated Graphite-System-Monitor-v0.5.0-source.zip. The original ZIP and extracted baseline were not modified. See BASELINE-MANIFEST.txt for its SHA-256 and source comparison.

## V0.8.0 - Now playing and Plexamp

The lower part of the Time panel now shows what is playing instead of system uptime: album art, playing/paused state, title and artist. It reads the Windows media sessions that power the volume flyout, so Spotify, browsers and most players work without setup. Only metadata is read; nothing is captured and playback is never controlled. To show uptime again, turn off "Show now playing (replaces uptime)" in the tray.

Plexamp does not appear in Windows media controls. Pulsewave reads Plexamp's local player timeline and fetches title, artist and art from your Plex server, which needs your Plex token: choose **Plexamp · set Plex token…** in the tray. The token is encrypted for your Windows account in `%LOCALAPPDATA%\Pulsewave\plex-token.dat`, kept out of settings.json, and only sent to Plex's own `plex.direct` addresses or servers on your local network. Without a token the tile still shows that Plexamp is playing, with a setup hint.

## V0.7.1 - Pulsewave branding

The product name, dashboard header, app title, executable, icon filenames, source project folder and exported session CSV filenames now use Pulsewave. The default cyan preset is named Midnight; existing saved `Graphite` theme values migrate to Midnight without changing their palette. V0.7.0 profile and preview behavior is retained.

The settings directory is now `%LOCALAPPDATA%\Pulsewave`. On first launch, if that settings file is absent and `%LOCALAPPDATA%\GraphiteMonitor\settings.json` exists, Pulsewave copies the old settings and backup to the new folder. The original files remain untouched. The old startup registration is recognized; turn Launch with Windows off and back on once to register `Pulsewave.exe` under its new name.

## V0.7.0 - Appearance profiles and dashboard preview

Appearance now identifies the currently applied saved profile, custom appearance, or built-in defaults and shows whether controls contain unapplied edits. Loading a profile and restoring built-in visual defaults changes only the draft. Save, rename and delete validate names, explain replacements/deletions, and persist the appearance library without applying draft visuals. Applying an appearance updates the active profile identity when it exactly matches a saved profile.

The separate simulated 1920×480 dashboard preview uses stress-case sample values for long dates/device names, 100% CPU/RAM, large network values, temperatures and unavailable sensors. It previews draft glass, borders, contrast and reactive accents. It never reads sensors, wallpaper, images or OpenRGB; non-manual draft sources use the already applied accent when the source matches, otherwise the preview labels the manual fallback.

These controls leave sensor mappings, FPS slot identity, display placement, startup, session statistics/export and the live overlay untouched until Apply and save. The dashboard layout and text sizing remain unchanged.

## V0.6.4 - OpenRGB GPU follower

New optional palette source: OpenRGB (read-only). Select the GPU explicitly; Pulsewave follows its reported single color without changing hardware lighting. Endpoint, port and 1-10 second polling interval persist with visual profiles. Source failures use your manual preset; performance mode pauses recurring reads. Existing image palettes, appearance defaults, telemetry and dashboard typography are unchanged.

Live read-only verification located the user's Gigabyte GeForce RTX 5070 Ti Aero OC and observed its reported color change to cyan #02F7EB after the user's change in OpenRGB. For this PC use endpoint **127.0.0.2**, port **6742** to reach the background service; the desktop SDK at127.0.0.1 returned no devices. Open Appearance > OpenRGB (read-only), set that endpoint/port, Find GPU, choose the5070Ti, then Apply and save. Keep low-power mode off for continuous following.

See OPENRGB.md for full setup, limitations, brightness-floor explanation and acceptance checks. This is SDK state following, not screen capture or guaranteed live hardware-effect sensing. The agent never changed GPU lighting; the user performed the cyan test.

## V0.6.3 candidate - hardening and clarity

This candidate preserves V0.6.1 colors/defaults and the existing dashboard layout. Appearance now labels applied source, pending sampling, recurring-sampling pause and the last successful image update. Profile notes distinguish loaded/edited controls from applied settings. Draft controls never change the displayed applied diagnostics until Apply.

Settings saves use a flushed staging file and an atomic replacement on Windows, retaining the previous valid settings.json as settings.json.bak. An invalid/truncated settings.json is never automatically replaced by temporary startup defaults. Pulsewave reports the load problem once and blocks saves that would erase the damaged original. Retain a copy of the damaged file, then repair it or restore a known-good settings.json.bak to settings.json and relaunch. A backup exists only after a successful replacement save; it may not exist on first install. Older settings and null visual/profile fields are normalized without changing saved sensor identities or placement. A backup is a previous save, not a permanent version history.

Automated checks now cover coherent palette lifecycle diagnostics, profile draft behavior, malformed settings, legacy-shaped migration fixtures, interrupted staging, blocked replacement and explicit backup restoration. Real-device extended stability remains pending. This package is a candidate for that acceptance check, not a claim of completed multi-hour gaming/display recovery testing.

## V0.6.2 reliability and profiles
Image decoding, dominant-color extraction and Windows wallpaper/process lookup now run on a background worker. The overlay continues clocks, telemetry and interpolation while analysis is pending. Apply no longer decodes an image on the UI thread. Results appear on a subsequent telemetry tick, so allow a few seconds before judging the detected swatch.

Each engine permits only one worker at a time. Preference changes replace the desired request; old results are discarded before they can change the target. There is no queued decode per edit. Switching to Manual takes effect immediately. An already-running Windows image decode is allowed to finish; it is not forcibly interrupted. Exit never waits for that worker, and workers cannot modify controls/settings. Performance mode permits a read after an explicit palette change but prevents recurring reads.

Auto diagnostics now identify the selected source and explain unavailable static images, missing/denied/invalid image files, detected Wallpaper Engine, or unavailable process detection. When process detection is unavailable, Auto conservatively uses your selected-image/manual fallback. Static results remain clearly labeled static, not animated or per-monitor live content. The source status and target swatch refresh while Appearance is open without extra image reads.

**Visual profiles:** enter a name and choose Save edits as profile to preserve the controls' current appearance choices. Existing matching names are replaced. Load profile fills the controls; Apply and save activates them. Profiles include palette/image path, tint, borders, glow, performance/sampling choices, panel opacity and high-contrast selection. They never change sensors, display placement, startup or session data. Images are referenced, not bundled; keep them at a permanent path. Up to 32 names of 64 characters can be created in the window. Profiles persist in the existing settings.json. Failed saves restore the previous in-memory preferences/library entry.

Existing vibrant extraction, appearance defaults, full-opacity text and 1920×480 layout are unchanged. V0.5/V0.6/V0.6.1 preferences remain compatible. See STABILITY.md for a repeatable real-device exercise and optional read-only process observer. The automated stress run does not certify real gaming, sleep/resume or monitor reconnection.

## V0.6.1 image detection update
V0.6.0 averaged all image pixels and lightened them toward white, which could make different wallpapers look similarly muted. V0.6.1 chooses the strongest saturated hue cluster using 24 softly assigned hue bins, weighted by alpha, saturation and brightness. Chromatic pixels must cover at least 1% of visible alpha; tiny colored flecks in monochrome artwork retain the neutral fallback. This is a palette heuristic, not semantic image recognition, and may favor a strong accent over the largest dull area.

Appearance now shows **Active source (last applied)** including the actual image path or exact fallback preset, plus a **Detected target accent** swatch and hex code. These refresh once per second while the window is open, without triggering extra image reads. The swatch is the target; the overlay interpolates toward it. Unsaved image/source edits do not change the swatch until Apply and save.

To compare: choose Image, select a colorful local image, Apply and save, and inspect the swatch/source. Try a second image of a different dominant hue. Existing conservative tint/glow defaults remain unchanged. Experimental Auto still cannot sample Wallpaper Engine animations.

## Build and upgrade
1. Exit the previous version through its tray icon. Keep a backup of `%LOCALAPPDATA%\Pulsewave\settings.json` (or `%LOCALAPPDATA%\GraphiteMonitor\settings.json` when upgrading from V0.7.0 or earlier) for rollback.
2. Extract V0.8.0 into a separate permanent directory. Install the .NET 10 SDK.
3. In PowerShell in this directory, run:
   powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build-win-x64.ps1
4. Run `publish-pulsewave-win-x64-v0.8.0\Pulsewave.exe`. This publish is self-contained. Alternatively run `run-source.ps1` with the SDK installed.
5. Existing settings migrate from the old folder on first launch. Saved sensor identities, FPS slot, scaling, monitor identity, startup visibility, active theme and telemetry preferences remain intact. Original settings remain in the old folder as a rollback copy. No settings reset is required. Session statistics restart on launch, as before.
6. If Launch with Windows was enabled, turn it off and on from the new tray menu so Windows registers `Pulsewave.exe`. Source runs cannot register startup.
7. For rollback, exit Pulsewave and launch the previous executable from its original folder. Restore your settings backup only if needed.

## Themes
Right-click the tray → Themes. Pulsewave Classic is the transparent 1920×480 overlay dashboard. Built-in templates are read-only; Duplicate opens a copy in the editor. Import accepts L-Connect 3 .template/.zip themes; video backgrounds play through Media Foundation. Theme sensors beyond the 12 Classic mappings are optional and assigned in Sensor Manager. Media is read only while the active layout shows now playing. Tray → Theme playlist / hotkeys sets which themes rotate, their order and interval, and the global hotkeys (default Ctrl+Alt+PageDown next, Ctrl+Alt+PageUp previous, Ctrl+Alt+End rotation on/off), registered with the documented RegisterHotKey API. Themes carry their own screen size (editor → Screen size); one shaped unlike the display is letterboxed in its background color. With click-through off, taps and clicks (touch arrives as mouse input; the window never activates) run each element's Tap / click action: play/pause and skip through the Windows media session API or Plexamp's local player, the sensor details page, or the next/previous theme.

## Existing behavior
The Classic 1920×480 layout and all font sizes are unchanged from V0.5; it fills a bar-shaped display as before and keeps its shape (letterboxed) on others. The 12-sensor defaults, game-independent FPS slot identity, sensor discovery/editor, session statistics/CSV export, alerts, tray hide/restore, startup registration, click-through, transparent window and stable monitor recovery are retained. See README-v0.5.0.md for original operating details. System.Drawing imports in MainWindow are narrowed to SystemIcons to avoid WPF Brush/Color/Point ambiguity.

## Verify
Run: dotnet run --project .\Tests\RegressionChecks.csproj -c Release
See TESTING.md for completed checks and the hardware acceptance checklist. No live user settings or startup registrations are changed by the regression suite. Off-screen previews render app-owned controls only.
