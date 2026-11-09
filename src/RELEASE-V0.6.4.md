# V0.6.4 - OpenRGB follower

Use OPENRGB.md for setup. On the user's PC the selected GPU is Gigabyte GeForce RTX5070Ti Aero OC, available at local endpoint127.0.0.2:6742. The desktop endpoint127.0.0.1:6742 returns no devices. Select OpenRGB(read-only), set the service endpoint/port, Find GPU, choose the card and Apply. Keep low-power mode off for continuous reads.

This release changes no lighting and follows only SDK-reported state. A real cyan change was observed as#02F7EB after the user changed OpenRGB. Animated hardware effects, overlay visual matching, reconnection/sleep and long gaming stability still require on-device acceptance. Test fixtures prove client behavior; they do not replace those checks.

Exit older Graphite before launching this build. Keep the executable in a permanent folder and update startup registration if its location changed. Existing settings and mappings carry forward. For rollback, retain the earlier version and a settings backup. No OpenRGB device configuration is changed by Graphite.
