# Historical: Aria 3.2 presentation contract

> **Retired as an author-language version.** The UI boundary described here is
> still useful context, but new projects use the single [`aria;` language](aria.md)
> and ARIAC7. Do not copy the versioned source examples in this document.

Aria 3.2 keeps story semantics in the Aria language and moves visual UI
composition to a project-owned React presentation package. This is a
deliberate break from the experimental V3.1/V3.2 visual DSL: the VM no longer
positions controls, paints menus, calculates focus geometry, or owns an
accessibility tree.

The result is one source of truth per responsibility:

| Responsibility | Owner |
|---|---|
| Story, choices, saves, route state, deterministic replay | `aria-core` |
| Scene art, sprites, transitions, screen effects | `SceneFrame` renderer |
| Layout, typography, responsive behavior, focus order, accessibility | React/Tauri presentation package |
| Native window, atomic desktop saves, updater integration | Tauri shell |

## Project boundary

Every V3.2 project declares its frontend directory in `aria.toml`.

```toml
schema = 4

[game]
id = "jp.example.umikaze"
version = "3.2.0"
title = "海風"

[runtime]
entry = "scripts/main.aria"
logical_width = 1280
logical_height = 720
asset_roots = ["assets"]
fonts = ["assets/fonts/NotoSansJP-Regular.ttf"]
save_namespace = "umikaze-v32"

[presentation]
frontend = "ui"
```

The frontend is built by the project (`npm run build`). `aria build --target
web` packages that result together with the checked WASM runtime, PAK, ARIAC6
program, renderer adapter, audio adapter, and generation-safe save adapter.
The Tauri shell uses the same built web payload; it does not have a separate
native layout implementation.

## Semantic contract

Each VM step produces two deliberately separate values:

- `SceneFrame`: scene-only draw commands, scene transition/effect data, and
  the replayed viewport. It contains no button, panel, text-box, hit-test, or
  accessibility node.
- `UiViewModel`: route, localized game metadata, dialogue, choices, menu
  actions, settings values, backlog, chapters, gallery, and semantic scroll
  positions. It contains no pixels, coordinates, CSS classes, or DOM IDs.

The frontend returns stable `UiIntent` values such as
`Activate { id: "choice:0" }`, `OpenRoute`, `Dismiss`, `SetSetting`,
`ToggleSetting`, and `Scroll`. Coordinates never cross this boundary for UI
activation. `UiViewport` does travel with replay input so scene projection is
deterministic across Web and Native, but it is host-ephemeral and never saved.

Routes are standard semantic names: `dialogue`, `title`, `pause`, `save`,
`load`, `settings`, `backlog`, `chapter_select`, and `gallery`. A `screen`
statement or a semantic route intent changes a route; the React package decides
whether it appears as a rail, side sheet, dialog, or mobile bottom sheet.

## Author language

Story source continues to use `say`, `choice`, `screen`, settings, save/load,
chapter, gallery, audio, and scene commands. For example:

```aria
aria 3.2;
entry title;

scene title {
  screen title;
  choice {
    "はじめる" => opening;
    "章を選ぶ" => chapters;
  }
}

scene opening {
  screen dialogue;
  say "凪" "海の匂いが、窓から静かに入ってくる。";
  await advance;
  end;
}
```

`ui_theme`, `ui_screen`, and `ui_transition` are retired syntax. The parser
retains only their source span so V3.2 reports E108 at the declaration; none
of them are compiled, serialized, or interpreted. `theme` and `textbox`
visual declarations are also rejected in V3.2. Move colors, tokens, layout,
and interaction styling into the React presentation package.

`aria migrate` converts known legacy system UI to semantic route usage and a
frontend manifest. Custom legacy UI stops migration with a file and line
number instead of guessing at a visual translation.

## Accessibility and responsive behavior

The presentation package uses semantic HTML and React Aria components. Buttons,
dialogs, switches, sliders, labels, values, Escape dismissal, keyboard focus,
gamepad mapping, and touch interaction are owned by the DOM—not synthesized
from a canvas scene frame. All actionable controls are at least 44 CSS pixels.

Responsive layout is likewise frontend-owned. The Umikaze package implements:

- wide: left title rail and right-side sheets;
- medium: compressed two-column surfaces;
- narrow: one-column reading flow and bottom sheets.

Focus and hover have a visible non-colour-only treatment, while
`prefers-reduced-motion`, high contrast, and explicit value labels remain
first-class behavior rather than decorative afterthoughts.

## Saves and replay

VM snapshot schema 7 stores only semantic presentation state: route history
and list offsets. It excludes viewport, focus, hover, pressed state, slider
geometry, tokens, and CSS. The browser stores three IndexedDB generations;
the Tauri shell uses the same three-generation atomic write model on desktop.

Replay hashes cover the deterministic scene and semantic model contract. Web
and Native therefore agree on story state and scene rendering while each host
uses the same React implementation for actual UI layout and accessibility.

## Umikaze reference frontend

[`examples/umikaze/ui`](../../examples/umikaze/ui) is the reference package.
It treats the existing seaside background as the protagonist: deep indigo,
sea fog, pale paper, and restrained gold form a quiet reading surface without
turning every screen into a dashboard. The title rail, reading band, choice
cards, menu sheet, settings controls, backlog, chapter grid, and gallery are
all ordinary accessible React components. Text-free sea-fog, paper-grain,
wave-divider, and chapter-ornament assets remain decorative only; player-facing
words stay localizable engine/DOM text.
