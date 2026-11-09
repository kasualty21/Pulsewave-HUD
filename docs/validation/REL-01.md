# REL-01 — lifecycle and coherent palette diagnostics

## Scope and source evidence

Reviewed the actual editable V0.6.2 AdaptiveVisualEngine source, existing worker tests, and its Appearance consumers. This is a source review and focused code change, not a hardware stability result.

The existing engine already runs at most one detached worker, rejects stale revisions, switches Manual without waiting on decoding, and ignores Step after disposal. No scheduling rewrite was needed. The vibrant color algorithm, UI interpolation, defaults, public Windows wallpaper lookup, image restrictions and capture-free behavior are unchanged.

Concrete diagnostic gaps found in source:

- IsSampling inspected raw task completion. It could report false before the UI accepted a result, and true for an obsolete decode while Manual was already active.
- LastSampleUtc records completion, including manual fallback. It cannot express last successful image acquisition separately.
- Separate string/status/task values did not provide Appearance one immutable source/status/sampling/success-time snapshot.

## Changes

- `src/GraphiteMonitor/AdaptiveVisualEngine.cs`: public immutable `PaletteDiagnostics(Source, Status, IsSampling, LastSuccessUtc)` and engine `Diagnostics` getter; worker result carries resolved source and image-success flag. Diagnostics are published by the UI Step, never from workers. Manual immediately clears sampling; queued newest image work reports pending until accepted. LastSuccessUtc resets on source-setting changes and records accepted image success (including selected-image fallback); failed recurring refresh retains the previous success time. Legacy Status and LastSampleUtc remain available. IsSampling now uses coherent desired-work diagnostics. Dispose marks sampling stopped.
- Internal optional clock injection supports deterministic throttle tests without wall-clock delays. Production uses UTC as before.
- `src/Tests/Reliability/ReliabilityChecks.cs`: `GraphiteMonitor.Regression.ReliabilityChecks.Run()` covers gated Manual/disposal, stale/newest requests, repeated invalidation, immutable diagnostics, success/failure distinction, performance suppression over simulated days, leaving performance mode and normal throttle timing. No live settings, startup, display topology, desktop capture or overlay launch.

## Validation and integration

Source review completed. Tests have been authored but were not executed by the reliability specialist, to avoid concurrent builds in the shared build tree. QA owns the shared Program.cs call; coordinator owns integrated build/regression execution and evidence. A passing integrated run must be recorded separately before release claims.

No version bump, release assembly, new dependency or settings schema change. MainWindow must pass a `Func<PaletteDiagnostics>` to Appearance under coordinator ownership. Appearance can show Source as the active human-readable resolved label, Status as detailed explanation, and LastSuccessUtc as the last accepted image time. A failed refresh may therefore show Manual fallback with an older last-success timestamp, intentionally.

## Remaining limits

Tests use injected decoders and a clock to exercise lifecycle transitions, not real gaming, screen reconnection, sleep/resume, memory stability or static-wallpaper/slideshow behavior. Real image decoding and existing fallback/privacy assertions remain in the pre-existing integrated suite. Direct animated Wallpaper Engine sampling remains unavailable.
