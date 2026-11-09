# Lights (OpenRGB LED control)

Pulsewave controls RGB lighting through OpenRGB's SDK server on this PC. It drives the lights; it no longer follows their color (the V0.6.4 follower was removed in TRIM-01).

## Setup
1. Install OpenRGB and check that it sees your devices.
2. In OpenRGB's SDK Server tab click Start Server. OpenRGB's settings can start the server at launch.
3. Pulsewave tray → Lights · OpenRGB…: choose the address (127.0.0.1 for the OpenRGB app; a background OpenRGB service may be on 127.0.0.2) and port (6742), then Connect / refresh.
4. Untick devices Pulsewave should leave alone.

## Controls
- Tray window: any color (swatch), Set color, Off, On, brightness and effect speed sliders, OpenRGB profiles (Load profile) and hardware effects (Apply effect).
- HUD: the Lights page (theme action "Lights page", or + Widget → Lights in the theme editor) has Off/On, brightness − / +, ten color swatches (plus the tray's color), profiles and effects as touch tiles. "Lights on / off" toggles from any theme element.
- On brings back what was applied last: a profile, an effect or the color.

## Schedule
Tray → Schedule · lights and screen off/on… adds rules: Off and On times, the days a period starts on, and whether it turns off the LEDs, blanks the HUD, or both. The schedule acts only at the start and end of a period, never turns on lights it didn't turn off, and retries each minute if OpenRGB isn't answering. A blank HUD wakes for a minute on a tap (click-through off).

## How it works
- Colors use a device's Direct (per-LED) mode via OpenRGB's custom mode; a device without one gets its own color mode (Static, say). Devices that can't take a set color are named.
- Brightness uses the active mode's brightness where a device has it; otherwise a color Pulsewave set is re-sent dimmer.
- Effects are matched by name across devices; speed and brightness are set within each device's own range, and the chosen color is used when the effect takes one.
- Off uses a device's Off mode when it has one, else black.
- Changes are not saved to device memory. To keep a look across reboots, save it as an OpenRGB profile and load it from Pulsewave.

## Limits and safety
- Loopback only (127.0.0.1 / 127.0.0.2). Sends only: protocol version, client name, device/profile lists, load profile, set custom mode, update mode, update LEDs. Never SaveMode, profile save/delete or rescan.
- Commands run one at a time in the background, each on a short connection with a 6-second deadline; replies are size-checked. Nothing is sent unless the user taps or clicks.
- SDK protocol up to 3 is used (OpenRGB 0.7 or newer for brightness, 0.6 for profiles).

## Checks still needed on the hardware
- [ ] Start OpenRGB's SDK server; tray → Lights lists the devices and profiles (or run `src/Tests -- --openrgb-probe`, read-only).
- [ ] Set a color, Off, On, brightness and an effect; confirm each device responds.
- [ ] Load an OpenRGB profile.
- [ ] Use the HUD Lights page by touch on the Y70.
