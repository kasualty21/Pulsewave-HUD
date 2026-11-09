# OpenRGB follower - setup and acceptance

Pulsewave V0.6.4 adds OpenRGB (read-only) as a palette source. It follows the one color reported for the selected GPU. No lighting, mode, profile, rescan or device-write command is sent. No desktop/game/wallpaper capture is used. OpenRGB remains the lighting controller; Pulsewave is an optional follower.

## Your confirmed setup
- GPU: Gigabyte GeForce RTX 5070 Ti Aero OC, OpenRGB device type GPU, one color.
- Endpoint: 127.0.0.2, SDK port 6742. This reaches the OpenRGB background service on this PC.
- The desktop OpenRGB instance also listens on 127.0.0.1:6742 and returned an empty device list. Both are local loopback addresses; Pulsewave accepts only these two literal addresses and does no external networking.
- Live read-only test: three snapshots initially reported #000000. After the user set the GPU to cyan in OpenRGB, three snapshots reported #02F7EB. Stable GPU identity remained unchanged. Pulsewave made no physical lighting changes during this check.

## Use
1. Exit the older Pulsewave through its tray icon. Extract V0.6.4 to a new folder. Build with build-win-x64.ps1, or use the provided self-contained Windows x64 package.
2. Launch Pulsewave, open Appearance, choose OpenRGB (read-only).
3. Choose local endpoint 127.0.0.2 and port 6742. Choose a read interval (default 1 second).
4. Click Find GPU; explicitly select Gigabyte GeForce RTX 5070 Ti Aero OC from the list. Discovery alone does not change active settings.
5. Click Apply and save. Allow a few telemetry ticks for background reading and smooth interpolation. The source status includes OpenRGB's raw hex color; the target swatch shows the effective display accent.
6. Keep Low-power / gaming mode OFF while you want continuous color following. When enabled, it permits the initial explicit read, then pauses recurring reads/glow as in earlier versions.
7. Save a visual profile if desired. Endpoint, port, device identity and read interval are appearance preferences and carry with profiles. Existing sensor mappings, monitor placement and startup settings are preserved.

## Behavior
- Queries run off the UI thread through the existing one-worker/revision pipeline. Poll cadence is 1-10 seconds, additionally bounded by the existing telemetry tick. A new color may take a few seconds to appear; this is not a high-frame-rate lighting visualizer.
- The selected GPU is matched by a hashed type/name/vendor/serial/location identity, not its position in OpenRGB's list. List changes abort the current snapshot; later reads rediscover. A missing or duplicate identity never substitutes another device; choose again if you replace/rename/reconfigure hardware.
- The first release accepts one color, or multiple identical reported colors. Differing multi-LED colors produce an explained manual fallback rather than choosing an arbitrary LED.
- Connection/parse/timeout/device failures use the saved manual preset and report the reason in Appearance. Normal mode retries quietly on its polling cadence.
- Very dark/black reported colors receive a display-only brightness floor to keep accents visible. The status still shows the raw reported hex and explains the lift. It never brightens physical LEDs. Text remains opaque and retains the existing warning/readability rules.
- OpenRGB SDK colors represent OpenRGB's state, not optical measurement or guaranteed instantaneous hardware-effect readback. Static color changes were tested on this GPU. Hardware-generated animations may retain a fixed reported color; use an OpenRGB-driven effect that updates the SDK state if you want changing accents.
- The reader negotiates SDK protocol 1-3 (announces max 3 for modern compatibility), has a 2.8-second total network deadline and bounded packet/device/string/count parsing. Unsupported or malformed replies fail safely. No OpenRGB.NET/NuGet dependency or elevated Pulsewave process is required; device access remains OpenRGB's responsibility.

## Checks still needed on the display
- [ ] Launch the V0.6.4 overlay and compare its cyan target with the GPU.
- [ ] Change the GPU to a second color in OpenRGB; verify smooth following.
- [ ] Stop/restart the SDK server yourself; verify manual fallback and recovery without changing the selected GPU.
- [ ] Restart Pulsewave; verify endpoint/device persistence and existing telemetry/FPS mappings.
- [ ] Confirm performance mode pauses recurring reads.
- [ ] Test your preferred animated effect; record whether SDK values actually change.

Official references:
- https://openrgb.org/sdk.html
- https://github.com/CalcProgrammer1/OpenRGB/blob/master/Documentation/OpenRGBSDK.md
- https://raw.githubusercontent.com/CalcProgrammer1/OpenRGB/master/RGBController/RGBControllerInterface.h
