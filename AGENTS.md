# Shared agent instructions

## Scope and coordination
- Read README.md, docs/TEAM.md and docs/BOARD.md before edits; read docs/DESIGN.md before any visual work.
- Work only on the coordinator's explicitly assigned task. Do not expand into roadmap tasks on your own.
- The coordinator may delegate independent work in parallel. Each assignment must name writable files, acceptance criteria and dependencies.
- All agents share one filesystem. Do not edit files owned by another active task. Request a coordinator handoff if overlap becomes necessary.
- Do not create user-facing chats, recurring automations, commits, branches, worktrees, or external messages unless the user/coordinator has authorized that action within applicable tool rules.
- No unsolicited architectural rewrites or new dependencies. Deliver the smallest coherent change that satisfies the assigned task.

## Product invariants
Preserve the 1920x480 dedicated 8-inch display layout and 3–4-foot legibility, large clocks and telemetry, all 12 HWiNFO mappings and game-independent FPS slot matching, session statistics/export, opaque text, the transparent Classic overlay over Wallpaper Engine, tray/startup behavior, monitor identity and reconnect recovery. Built-in templates must keep passing the template quality gate (fonts, on-screen, no text overlap, contrast).
Use supported public Windows APIs only. Never capture desktops, apps or games.
Older settings and sensor mappings must survive upgrades; settings for removed features are kept untouched in settings.json.
Lumen is the baseline look (docs/DESIGN.md): new templates, widgets and HUD pages take their colors and fonts from src/Pulsewave/Themes/Lumen.cs, never from copied values. The other built-in templates stay as alternatives; do not restyle or remove them unasked. The Lumen template ids (40-lumen … 45-lumen-productivity-column) are saved in users' settings and must not be renamed.
Keep the app lean: the Appearance system, OpenRGB color following, the smart theme builder, demo mode and Wallpaper Engine detection were removed deliberately. OpenRGB is back only as user-requested LED control (Pulsewave drives the lights; it does not follow them), sending commands only when the user taps or clicks. Do not reintroduce removed features without the user asking.

## Ownership and validation
The coordinator owns shared settings/model interfaces, MainWindow integration, versioning and release assembly. See docs/TEAM.md for exact paths. Tests may be proposed by specialists, but shared test files are assigned to one active owner at a time.
Read-only planning is not a source audit. Distinguish directly reproduced automated evidence, user-reported hardware results, and unperformed tests. Never label a synthetic exercise as real gaming/display recovery validation.
Avoid launching the real overlay, editing live user settings, changing startup entries or display topology during automated tests. Use isolated fixtures and app-owned off-screen renders.
Keep releases/ immutable. Put scratch files in work/. Do not remove another agent's work. No destructive cleanup without resolving and verifying intended workspace paths.
Before handing back: report changed files, rationale, validation results, unresolved issues and integration needs. Do not bump versions independently.

## Agent skills

### Issue tracker

Issues and specs live in GitHub Issues on kasualty21/Pulsewave-HUD, used through the `gh` CLI. See `docs/agents/issue-tracker.md`.

### Triage labels

The five default labels: needs-triage, needs-info, ready-for-agent, ready-for-human, wontfix. See `docs/agents/triage-labels.md`.

### Domain docs

Single-context: one `GLOSSARY.md` and `docs/adr/` at the repo root, created when first needed. See `docs/agents/domain.md`.
