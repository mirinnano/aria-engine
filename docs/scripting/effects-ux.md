# Aria Effects UX

Aria effects are low-level commands plus script-side presets. The engine avoids fixed scene systems; authors compose effects in `.aria`.

## Core Commands

- `transition bg, "path", "fade", 700`
- `transition bg, "path", "white", 520`
- `camera shake, 6, 300`
- `camera pan, x, y`
- `camera zoom, 1.05`
- `camera reset`
- `screen flash, "#ffffff", 180`
- `screen tint, "#1d2430", 80, 1200`
- `screen clear`
- `textfx shake, 2, 8`
- `textfx wave, 1.2, 4`
- `textfx speed, 16`
- `textfx reset`
- `fx profile, "subtle"`
- `fx skip_policy, "finish"`
- `fx cancel, screen|camera|text|all`
- `sync fx`
- `voice "voice/mio_001.ogg"`
- `voice_wait`
- `voice_stop`

## Script Presets

`assets/scripts/main.aria` defines small helpers:

- `fx_scene_cut(path)`
- `fx_memory()`
- `fx_shock()`
- `fx_focus()`
- `fx_reset()`
- `voice_line(path)`
- `mood_calm()`
- `mood_memory()`
- `mood_danger()`
- `chapter_card_fx(title, sub)`
- `choice_enter_fx()`
- `focus_fx()`

Use presets for story tone, and use core commands directly only when a scene needs special timing.

## Recommended Layers

- Transition: `transition bg`
- Camera: `camera shake|pan|zoom|reset`
- Screen: `screen flash|tint|clear`
- Text: `textfx shake|wave|speed|reset`
- Sound: `voice`, `voice_wait`, `voice_stop`
- Safety: `fx cancel`, `sync fx`, `fx skip_policy`

## Safety Rules

- Call `fx_reset()` before large scene jumps, load-like flows, or special menus.
- Prefer `sync fx` after strong flashes or background transitions.
- Keep `screen tint` durations finite unless the tint is the intended scene mood.
- Use `textfx reset` after temporary text effects.

## Current Scenario Usage

- `scenario_01.aria`: prologue card, memory tint, viewpoint cards, confession shock.
- `scenario_02.aria`: day card, station cuts, camera shutter flashes, Ogaki dash shock, hotel tension.
- `scenario_03.aria`: day card, morning danger mood, memory scenes, camera/photo beats.
- `scenario_04.aria`: day card, calm hotel morning, light surprise shake, night memory mood.
- `scenario_05.aria`: day card, calm morning, small physical gag shake, late-scene focus.
- `scenario_06.aria`: day card, rain/danger mood, fever focus, chapter-end reset.
- Custom UI screens call `fx_reset()` on entry so story effects do not leak into menus.
