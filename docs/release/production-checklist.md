# Production Checklist

Use this before publishing a production build.

## Required Gates

- `scripts/doctor.ps1` passes.
- `scripts/smoke.ps1` passes.
- `dotnet build src/AriaEngine/AriaEngine.csproj -c Release --no-restore` passes.
- `aria-compile` succeeds for `init.aria` and `assets/scripts/main.aria`.
- `scripts/package.ps1 -Version <version>` creates a release directory and zip.
- Runtime-specific builds use `-Runtime win-x64` when the restore environment is available.
- `manifest.json` and `checksums.txt` are present in the package.
- The packaged build launches with production arguments.
- `scripts/diagnostics.ps1` creates a diagnostics zip.
- `aria-save migrate` and `aria-save validate` pass on test saves.
- `scripts/visual-compare.ps1` passes for accepted baselines.
- `scripts/replay.ps1` passes for release replay specs.
- `scripts/installer.ps1` creates an installer zip.

## Manual QA

- Start a new game.
- Load an existing save.
- Confirm `persistent.ariasav` restores read/progress state.
- Open save, load, backlog, and right-click menus.
- Confirm settings and gallery still work as script-owned screens.
- Confirm F3/F5/F9 development hotkeys do not operate in production mode.

## Release Blockers

- Missing assets.
- Script compile errors.
- Corrupt save files without a migration or reset path.
- Any crash on startup.
- Broken save/load/backlog/rmenu.
