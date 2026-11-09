# OpenRGB follow-mode UX

OpenRGB is an optional read-only color source. Start OpenRGB and enable its local SDK server (normally port 6742). In Appearance choose **OpenRGB (read-only)**, choose the local endpoint and check the port, click **Find GPU**, explicitly choose the 5070 Ti, then **Apply and save**. Discovery reads GPU data and never sends lighting changes. No device is selected automatically. Endpoint 127.0.0.1 is the default; use 127.0.0.2 for a background service when the desktop SDK is empty. Editing endpoint or port clears the discovery list while retaining the saved GPU identity. Any discovery completion from a prior endpoint or profile revision is discarded. No GPU with readable colors produces an explanatory status. A retained selection can be offline when Appearance opens or a profile loads; discovery failure does not clear its identity.

Polling has its own 1–10 second interval. Low-power / gaming mode pauses recurring reads. Applied diagnostics show the last successful color read and the active fallback reason. Draft edits and profile loads do not change the applied source until Apply. Profiles store the device identity, display name, endpoint, port and interval along with other appearance fields.

## Automated evidence

OpenRgbAppearanceChecks.Run is an isolated STA form test: no Show, timer pump, scan, network, Apply, Save or live settings. It verifies mode, retained offline name, independent polling diagnostics, profile draft loading and unchanged active settings. Coordinator wires and runs integrated checks. These tests do not validate hardware RGB animation reporting.

## Pending device acceptance

- Enable the SDK server and discover the 5070 Ti. Explicitly select it; confirm color follows its single reported LED.
- Change GPU lighting using OpenRGB and compare Graphite's applied swatch after the polling interval.
- Stop/restart OpenRGB: confirm explicit manual fallback and reconnection to the saved GPU only.
- Remove/rename/change device enumeration: confirm no other device is silently selected.
- Confirm read-only operation preserves GPU lighting modes and colors.
- Enter low-power mode and verify polling pauses. Profiles/restart retain GPU identity and interval.
- Close Appearance during discovery; confirm quiet completion without reopening a window.

No live OpenRGB hardware tests were performed by the UI specialist.
