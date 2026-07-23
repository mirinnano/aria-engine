# Aria semantic-runtime architecture

Aria is a Rust visual-novel runtime, not a general-purpose engine. The same
`aria-core` VM runs in WASM and in the desktop shell; a project-owned React
package is the single production UI on both targets.

```text
.aria + aria.toml
       |
       v
aria-core: lossless syntax -> typed compiler -> ARIAC7 -> deterministic VM
       |                         |
       v                         v
aria-web: WASM scene runtime   UiViewModel / UiIntent semantic contract
       |                         |
       +---------- React presentation package ----------+
                           |                 |
                        Web/PWA           Tauri desktop
                           \_______________/
                     aria-render: scene-only GPU renderer
                              |
                     aria-cli: check/run/build/import-novel
```

`aria-core` has no OS, GPU, audio, browser, clock, filesystem, DOM, or
accessibility dependency. Its boundaries are value-only `InputSnapshot`,
`SceneFrame`, `UiViewModel`, `AudioCommand`, `RuntimeCommand`, and
`SaveEnvelopeV3`. `SceneFrame` is scene-only; it cannot contain a panel,
button, hit-test node, focus state, or accessibility role. React owns the DOM
layout and React Aria owns roles, values, focus, touch, keyboard, and gamepad
behavior. An architecture test rejects platform/device types from Core. A
replay tape is the cross-runtime oracle: Native and Web must produce the same
scene and semantic-state BLAKE3 hashes.

The presentation package translates keyboard, mouse, touch, accessibility,
and gamepad events into `UiIntent` values. It uses normal DOM focus and React
Aria rather than reconstructing an accessibility tree from a canvas. The
deterministic Core never polls a device or depends on a vendor SDK. Tauri owns
the desktop window and atomic save storage; the Web package owns browser audio
unlock, IndexedDB, and service-worker lifecycle. The scene renderer uses the
same renderer adapter on both targets and Kira/Web Audio consume only
validated asset commands from Core.

Text uses only the ordered font bytes named by `runtime.fonts`. Native parses
them into a bundled-only cosmic-text/fontdb database and glyphon draws the
result. Web loads the same bytes through `FontFace` before the first frame and
uses generated family names. Neither side performs host font discovery.
Aria text helpers operate on grapheme clusters and implement VN typewriter
and basic Japanese line-break rules; full visual goldens remain release QA.

Web is a PWA with a React presentation package: service-worker precaching,
update notification, semantic intent dispatch, standard Gamepad API polling,
audio unlock, bundled fonts, and IndexedDB generation storage. WebGPU is
preferred and WebGL2 is a complete fallback for the scene canvas. Device/context
loss clears GPU resources and reloads them from PAK bytes. Tauri embeds this
same built frontend for the desktop application.

PAK4 and ARIAC7 are target-independent. Native release wrappers are validated
against PE/ELF/Mach-O headers, while Web release packaging requires real
wasm-bindgen glue and a WebAssembly header. A package containing a debug Player,
fake Web glue, a missing bundled font, a mismatched checksum, or a stale
manifest is rejected before launch.

The 1.0 cutover gate is: Windows/Linux release Players with the same portable
bundle, macOS universal artifact evidence, Chromium Web smoke, deterministic
replay, save corruption recovery, representative game playthrough, and
supply-chain checks. Historical C#/Raylib sources are not an executable
compatibility path.
