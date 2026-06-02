# Changelog

All notable changes to AriaEngine / umikaze are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [2.0.0-rc.2] - 2026-06-02

### Added

#### Asset GC (Pak v3 redesign, 12-day rollout across Phases 1-5)

A unified, lazy-loading asset pipeline with refcount + generational garbage
collection for `load_aria_asset` and downstream consumers. Replaces the
ad-hoc pak readers in release mode with a single normalized view.

- `UnifiedAssetIndex` — lazy manifest across the 6 v3 split pak files
  (`boot.arib` / `scenario.aris` / `data.arid` / `stream.arim` / `voice.ariv`)
  plus optional patch overrides. Patch entries are merged with last-write
  semantics.
- `UnifiedAssetProvider` — `IAssetProvider` wrapper with `diskFirst` flag
  for dev/release modes. Falls back gracefully when the disk root or any
  pak is missing.
- `AssetHandle<T>` — generic handle with `Owned` / `Borrow` / `Move` ownership
  semantics matching v2 strict. `Borrow()` returns a new handle with
  refcount=1 and leaves the parent's refcount untouched. `MoveTo` transfers
  ownership and suppresses the source's registry notification.
- `AssetRegistry` — refcount + generational GC (Gen0/1/2 with 1s / 30s
  promotion). Background `Timer` sweeps every second when `Enabled=true`.
  `Mark()` reserves entries from eviction.
- `load_aria_asset` opcode — loads an asset into a script variable.
  Honors `owned asset @x;` declarations to participate in scope-exit
  auto-dispose. Emits W013 / E013 lint diagnostics.
- aria-lint E013 / W013 — three new static checks:
  - W013: `load_aria_asset` without an upstream `owned asset` declaration
  - W013: double load of the same result var
  - E013: cross-scope use of an `@`-prefixed owned asset handle
- `AppConfig.AssetGc` section — runtime config:
  - `Enabled` (default `false` for staged rollout)
  - `TotalBudgetBytes` (default 512 MB)
  - `Gen1PromotionSeconds` (default 1)
  - `Gen2PromotionSeconds` (default 30)

#### UX Quick Wins (T1/T2/T3, v2.0.0-rc.1)

- **T1: Save thumbnail** — capturing the actual gameplay screen when the
  save menu opens, not just the menu itself. The screenshot is stored in
  the save slot metadata and shown in the load menu.
- **T2: Button press feel** — `ButtonFeel` automatically applies scale
  + opacity + duration animations to every clickable button when the
  active theme is `"soft"` (and reduced animations for `"mono"`).
- **T3: ADV vertical text alignment** — `textbox_align center | top | bottom`
  controls the vertical position of the text within the textbox.
  Defaults: `bottom` (classic ADV).

### Changed

- `AppConfig.SchemaVersion` bumped 1 → 2. The new `AssetGc` section is
  optional in old `config.json` files; missing keys fall back to the
  design defaults. (See `docs/release/breaking-changes-vNext.md`.)
- `CreateAssetProvider` in `Program.cs` now always returns a
  `UnifiedAssetProvider` so the asset GC sees a single normalized view.
  The legacy `DiskAssetProvider` / `PakAssetProviderV3` / `PakAssetProvider`
  implementations remain as the underlying sources.
- Live-reload's pattern match updated from `is DiskAssetProvider` to
  `UnifiedAssetProvider.DiskProvider is DiskAssetProvider`.
- `VirtualMachine` ctor gained a final `AssetRegistry? assetRegistry = null`
  parameter (backward compatible — existing tests pass `null`).

### Performance

- Asset startup time: 6.48 ms → 0.01 ms (648× faster, lazy manifest).
- Asset read throughput: 14.84 ms → 8.84 ms (40% faster, ratio 0.60×).
- Benchmarks live in `src/AriaEngine.Tests/UnifiedAssetBenchmarks.cs`.

### Test counts

- 474 pass / 14 fail / 1 skip / 489 total. The 14 pre-existing failures
  are documented separately and are unrelated to the Pak v3 redesign
  (DocTests / DemoFlowScriptTests coverage gaps).
- New tests added during the rollout:
  - UnifiedAssetIndex: 15
  - UnifiedAssetProvider: 17
  - AssetHandle: 28
  - AssetRegistry: 27
  - AssetCommandHandler: 21 (14 for Phase 4.2 + 7 for Phase 4.3)
  - Lint (E013/W013): 6

### Notes

- The Pak v3 redesign rollout is staged — `AssetGc.Enabled` defaults to
  `false` so the engine behaves exactly as before for users who don't
  update their `config.json`. Flip to `true` once the rest of the
  rollout is verified.
- Web (Blazor) target is unaffected. `PreloadedWebAssetProvider`
  continues to serve all assets from the bundled `web-text-assets.json`.

## [2.0.0-rc.1] - 2026-05-02

### Added

- Aria v2 strict language mode (`# aria-version: 2.0` + `strict on`):
  type-safe registers, function definitions, scope-based resource
  management, ownership tracking.
- `aria-lint` static analyzer with error codes E001-E012 and warnings
  W001-W008.
- Static-analyzer-aware `aria-compile` for encrypted script bundles.
- Web (PWA / Blazor WebAssembly) target.

### Changed

- `defsub`'s `sub` alias removed; `sub` is now arithmetic subtraction.
- `saves/persistent.ariasav` excluded from source control.
- `src/AriaEngine/assets/fonts/JosefinSans-Thin.ttf` removed.

## [1.0.0] - 2026-02-14

### Added

- First public release.
- NScripter-compatible `.aria` script language (v1.x).
- Save/load, backlog, gallery, right-menu, chapter select.
- NSIS-based Windows installer.
- Steam build pipeline.

[Unreleased]: https://github.com/mirinnano/aria-engine/compare/v2.0.0-rc.2...HEAD
[2.0.0-rc.2]: https://github.com/mirinnano/aria-engine/compare/v2.0.0-rc.1...v2.0.0-rc.2
[2.0.0-rc.1]: https://github.com/mirinnano/aria-engine/compare/v1.0.0...v2.0.0-rc.1
[1.0.0]: https://github.com/mirinnano/aria-engine/releases/tag/v1.0.0
