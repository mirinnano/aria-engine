# umikaze v1.0.0 Release Notes

## Summary

- First v1.0.0 Windows production line for umikaze on AriaEngine.
- Windows installer is NSIS-based and built from `installer/umikaze.nsi`.
- Production package contains `data.pak`, `scripts/scripts.ariac`, `manifest.json`, `checksums.txt`, `release-notes.md`, and `README.md`.
- Raw `.aria` source scripts are omitted from production `data.pak`.
- Packaged runtime resolves `data.pak` from the app directory, so shortcuts and portable launches do not depend on the caller working directory.

## Compatibility

- Normal save schema: AriaSave v3.
- Persistent save schema: PersistentGameData v2.
- Config schema: AppConfig v1.
- Engine-owned menu actions remain reserved: `save`, `load`, `backlog`, `lookback`, `rmenu`.
- Settings and gallery remain script-owned screens.
- NScripter-style `effect` and `print` execute mapped screen transitions.
- `chapter_scroll` adjusts chapter-select card offset and redraws the selection UI.
- `aria v2 strict` guidance now emphasizes C++-style organization plus Rust-style scope, mutability, and lifetime checks.
- `struct` is a static v2 language feature: `string` fields expand to `$instance_field`, numeric fields expand to `%instance_field`, and unknown/duplicate/type-mismatched fields are parse errors.

## Breaking Changes

- C# GUI installer removed.
- Rust installer removed.
- Patch GUI installer script removed.
- `VirtualMachine.LoadScript(List<Instruction>, Dictionary<string, int>, string)` removed.
- `defsub` no longer owns the `sub` token. `sub` resolves to arithmetic subtraction.
- Runtime save data is excluded from source control.
- Broken `JosefinSans-Thin.ttf` asset removed.
- Aria v2 `struct` string fields now use `$instance_field` instead of `%instance_field`.

## Install And Update

- Use `AriaEngine-v1.0.0-win-x64-installer.zip` for normal installation.
- Use `AriaEngine-v1.0.0-win-x64.zip` for portable/manual deployment.
- Default install target is `%LOCALAPPDATA%\Ponkotusoft\umikaze`.
- Installed shortcuts launch with `--run-mode release --pak data.pak --compiled scripts/scripts.ariac`.
- Installer shortcuts set the working directory to the install directory.

## Verification Gates

- Release unit tests.
- Strict doctor.
- Smoke tests.
- Replay spec: `tests/replay/release-smoke.json`.
- Route/runtime flowcheck: all 6 umikaze chapter routes, unlock flags, NVL/ADV entry points, chapter-select returns, and headless VM execution.
- Visual UI flow baselines: title, config, extra, gallery, chapter select, NVL, ADV, right-menu, save, load, backlog.
- Package and NSIS installer generation.
- Package manifest, checksums, compiled script bundle, and raw script exclusion checks.

## Known Release Notes

- Public code signing requires `WINDOWS_CODESIGN_PFX_BASE64` and `WINDOWS_CODESIGN_PFX_PASSWORD` in CI.
- If signing secrets are absent locally, package metadata records the artifact as unsigned.
