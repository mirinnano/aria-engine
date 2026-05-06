# Production Checklist

Use this before publishing a production build.

## Required Gates

- `scripts/doctor.ps1` passes.
- `scripts/smoke.ps1` passes.
- `dotnet build src/AriaEngine/AriaEngine.csproj -c Release --no-restore` passes.
- `aria-compile` succeeds for `init.aria` and `assets/scripts/main.aria`.
- `aria-flowcheck --root src/AriaEngine --main assets/scripts/main.aria --chapters 6 --execute` passes.
- `scripts/package.ps1 -Version <version>` creates a release directory and zip.
- Runtime-specific builds use `-Runtime win-x64` when the restore environment is available.
- `manifest.json` and `checksums.txt` are present in the package.
- `README.md` is present in the package.
- The packaged build launches with production arguments.
- Packaged `data.pak` resolves from the app directory, not only the caller working directory.
- Production `data.pak` does not include raw `.aria` scripts.
- `scripts/diagnostics.ps1` creates a diagnostics zip.
- `aria-save migrate` and `aria-save validate` pass on test saves.
- `scripts/visual-regression.ps1 -CaptureLaunch` captures a non-blank packaged launch screenshot.
- `scripts/visual-compare.ps1` passes against tracked baselines in `tests/visual-regression/baseline`.
- `scripts/replay.ps1 -Spec tests/replay/release-smoke.json` passes for the tracked release replay spec.
- `makensis.exe` is available before running the installer gate.
- `scripts/installer.ps1` creates the NSIS installer zip.
- Installer shortcuts set their working directory to `$INSTDIR`.
- `release-notes.md` is present in the package.
- `manifest.json` records compatibility, packaging, and signing state.
- Release CI uses the same `scripts/release.ps1` and `scripts/installer.ps1` path as local release builds.

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
- Missing release notes or missing signing status metadata.
- Public release artifact marked unsigned when CI signing secrets were expected.
