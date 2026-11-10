# Team and file ownership

## Coordinator — this chat
Owns scope, task board, shared contracts, review/integration, docs/roadmap and release versioning. Resolve conflicts before edits. Changes to these application files require coordinator ownership or an explicit temporary handoff:
- src/Pulsewave/MainWindow.xaml.cs
- src/Pulsewave/SettingsStore.cs
- src/Pulsewave/Pulsewave.csproj
- src/Pulsewave/Themes/ThemeModel.cs when the theme format changes
- src/Pulsewave/AssemblyInfo.cs

## Reliability specialist
Default writable production files, when assigned: src/Pulsewave/Themes/VideoLoop.cs, VideoFrameReader.cs and NowPlaying.cs. Focus on scheduling, request/result lifecycle and error handling. Propose settings/persistence changes to the coordinator; do not edit shared schema concurrently.
New focused tests may be placed in src/Tests/Reliability/ when included in an assigned task; coordinate any entry-point changes with QA. Review does not imply confirmed defects.

## Visuals / UX specialist
Default writable files, when assigned: src/Pulsewave/ThemesForm.cs, the theme editor (src/Pulsewave/Themes/Editor), scripts/build-templates.py, the template generators in src/Tests/Themes (LumenTemplates.cs, HaloTemplates.cs and the rest) and the page kit (src/Pulsewave/OneUi.cs with the *View.cs pages). Focus on template design, gallery, pages and editor experience. Visual work follows the Lumen baseline in docs/DESIGN.md; the tokens in src/Pulsewave/Themes/Lumen.cs are a shared contract (coordinator-owned). Request model/interface changes from the coordinator. No overlay XAML or typography changes by default.

## Release / QA specialist
Default writable files, when assigned: src/Tests/Program.cs, src/Tests/StabilityExercise.cs, new release-check scripts, docs/validation/ and testing documentation. Own the shared test entry point while assigned. Propose production fixes to the responsible owner rather than editing their files concurrently. Coordinator owns final version bumps and release archive assembly.

## Protected existing subsystems
MainWindow.xaml, SensorReader, SensorCatalog, SensorManagerForm, TelemetryEngine, TelemetryForm, DesktopIntegration and App are not default targets for this milestone. Changes require a concrete reason, assignment and relevant regressions.

## Handoff protocol
1. Coordinator posts an assignment with task ID, writable paths, dependencies and acceptance criteria.
2. Specialist reads current code, reports any mismatch with planning assumptions, and edits only assigned paths.
3. Return changed files, rationale, checks and residual concerns. Shared-contract requests return to coordinator.
4. Coordinator reviews and integrates serially; QA runs the integrated gates.
5. Candidate release records exact source version/checksum, completed checks and pending hardware acceptance.

The initial planning tasks and first implementation assignments (REL-01, QA-01, UX-01, CFG-01) are complete. Specialists reviewed/edited assigned source; the coordinator integrated changes, ran clean gates and recorded V0.6.3 candidate evidence. No implementation agents are currently running. See BOARD.md for hardware acceptance status.
