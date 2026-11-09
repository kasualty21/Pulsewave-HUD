# OpenRGB live connection evidence

Read-only SDK probes were explicitly authorized as part of following the user's RTX5070Ti. No OpenRGB process was started/stopped or reconfigured by the agent; user enabled the server and changed the GPU color themselves.

Initial127.0.0.1:6742 connection: success, three empty snapshots. Process/listener inspection found desktopSession1 bound specifically to127.0.0.1 and serviceSession0 bound to0.0.0.0 on the same port.

Alternate local127.0.0.2:6742 connection: success, GPU Gigabyte GeForce RTX5070Ti Aero OC, type2, one reported LED color. Three snapshots#000000. Following the user's cyan change, three snapshots#02F7EB. Controller hash stable across both sets.

This verifies protocol parsing/identity and reported static-color changes against the real service. The probe itself does not prove hardware-animation readback, display sleep/reconnect behavior, a duration-specific extended gaming test, or direct hardware color sensing. All SDK sends in the client are limited to protocol negotiation40, controller count0 and controller description1.

User report (2026-10-19): the packaged build follows color well and has been running for a while. The exact duration and workload were not specified. The user also confirmed that the saved GPU reconnects after stopping/restarting the SDK server, and that Pulseframe follows effects started from OpenRGB's Effects tab. The user subsequently confirmed all acceptance tests for the latest preview are verified.
