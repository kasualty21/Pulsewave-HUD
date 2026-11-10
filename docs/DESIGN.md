# Design baseline: Lumen

Lumen is Pulsewave's baseline look. New templates, editor widgets and HUD pages start from it; the other built-in templates (Halo, One UI, Graphite, Paper, Noir and 01–25) stay as alternatives and are not restyled.

The one source of colors and fonts is `src/Pulsewave/Themes/Lumen.cs`. Do not copy its values into other files.

## Where it lives

| Part | File | Notes |
|---|---|---|
| Tokens (colors, fonts, caption spacing) | `src/Pulsewave/Themes/Lumen.cs` | Shared by everything below. |
| Templates 40–45 | `src/Tests/Themes/LumenTemplates.cs` → `src/Pulsewave/Templates/4x-lumen*.json` | Regenerate with `RegressionChecks --widget-shots <dir> --write-oneui-templates <dir>`, then copy the six JSON files. Never edit the JSON by hand. |
| HUD pages (now playing, volume, lights, colors, apps, gaming, sensor details) | `src/Pulsewave/OneUi.cs` and the `*View.cs` files | `OneUi` keeps its name (the page layout is One UI's quick panel); its look is Lumen. Pages use it for every theme. |
| Gallery order | `BuiltInTemplates.Names` | Lumen first, then the rest by file name. |

## The look

- **Canvas.** Near-black (`Canvas`), a shade lighter at the top, with an aurora of soft radial lights: cyan near the clock, violet through the middle, magenta at the far end.
- **Glass.** Slabs and cards are see-through white (`SlabTop` → `SlabBottom`) with a hairline edge and a highlight along the top. Buttons and pills are the same glass, rounder.
- **Accent.** Cyan → violet. It fills the one thing that is on or primary (play / pause, a selected tab, Start) with dark `OnText` on it and a cyan glow. Use it sparingly: one filled control per group.
- **Channels.** Cyan is the CPU, magenta the GPU and the game, violet memory. Warm is the sun and limit warnings. Amber and red are reserved for warning levels (temperatures, nearly full meters).
- **Type.** DIN numerals (Bahnschrift, light when large) for every reading and page title; Segoe UI for names and running text; captions are small DIN capitals spaced with hair spaces (`Lumen.Spaced`).
- **Instruments.** A gauge is a ring of ticks, a glowing gradient arc on a faint track and the reading in the middle. Charts fade out downward (`Chart` + `GradientColor`). Meters are segmented.
- **Glows.** Few and meaningful: the hero number, gauge arcs, the accent button, art and covers. Night dimming turns theme glows off.

## Three versions, two shapes

Each family comes as an all-round, a Gaming and a Productivity version, for the 8.8" bar (1920×480) and the HYTE Y70 column (682×2560).

- **All-round:** clock and weather first, then CPU and GPU, memory and network, now playing, toggles.
- **Gaming:** the game's live FPS is the hero, then frame time and the session, then CPU and GPU with what is holding them back. Follows the Gaming shortcut list.
- **Productivity:** the timer, then Claude Code and Codex limits, a compact system card, a now-playing strip. Follows the Productivity shortcut list.

## Rules that do not bend

- Every built-in template passes the quality gate (fonts found, on screen, no text overlap, contrast) with sample and worst-case values and in every palette.
- Legible at 3–4 ft: readings are large, labels never below 11 pt in a theme.
- Text is opaque. A reading that can be missing is hidden (`ShowIf`) rather than shown as "--" where that reads as broken.
- Touch targets are finger-sized; a restyle does not move or shrink them.

## Not yet in the baseline

- The editor's ready-made widgets (`ThemeWidgets.cs`) and the editor's own controls (`EditorStyle.cs`) still use the Halo kit.
- The tray windows (Themes, Shortcuts, Schedule and the rest) keep their existing style.
- Pulsewave Classic keeps its Midnight look by design.
