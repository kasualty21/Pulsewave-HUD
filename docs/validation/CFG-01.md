# CFG-01 - settings preservation

Source review found that V0.6.2's startup fallback used default settings after a read failure, but later placement migration called Save unconditionally. That could erase the malformed original. Null sensor entries could also cause normalization to fail, whereas null visual/profile values needed safe defaults.

Implemented in SettingsStore/MainWindow:
- Path-specific internal load/save entry points for isolated tests; production continues using the original per-user settings.json.
- Reject malformed JSON, empty objects represented as null/arrays and explicitly damaged sensor identities without replacing the file.
- Block replacement of an already damaged existing destination, even if startup is temporarily running defaults.
- Flush a uniquely named staging file, atomically File.Replace the destination and retain the previous committed version as .bak. An incomplete abandoned staging file is ignored.
- Preserve unknown top-level properties, saved FPS slot/hive/scale and monitor identity.
- Normalize missing/null visual/profile fields. Null library entries remain retained and are safely ignored by Load controls.
- Show one startup attention message and skip automatic placement-save migration while a load error is active. Explicit reload clears the error only after a successful load.

Isolated integrated fixtures passed for legacy-shaped migration, null profiles, damaged-original preservation, interrupted staging, failed replacement while the destination is locked, backup preservation after corruption, and explicit backup restoration. No live settings/startup entries were changed.

No automatic damaged-file recovery was added: recovery is explicit so the user can retain evidence and choose a known-good backup. The first install has no historical backup. File replacement cannot provide a guarantee against hardware failure; this is a flushed Windows atomic-replacement design, not a physical power-loss experiment.

Integrated coordinator review: five previously direct tray save handlers now use guarded preference saves. On failed persistence, their affected fields are restored and display/style/demo changes are not applied. This prevents the deliberate damaged-file save guard from becoming an unhandled tray exception.
