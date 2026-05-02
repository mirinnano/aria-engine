# umikaze v1.0.0-rc.2 Release Notes

## Summary

- Release candidate for the story-included v1.0.0 line.
- Packages the umikaze story payload as `data.pak` plus `scripts/scripts.ariac`.
- Includes installer metadata, checksums, release notes, and compatibility metadata in `manifest.json`.

## Compatibility

- Normal save schema: AriaSave v3.
- Persistent save schema: PersistentGameData v2.
- Config schema: AppConfig v1.
- `save`, `load`, `backlog`, `lookback`, and `rmenu` remain engine-owned actions.
- Settings and gallery remain script-owned screens.

## Install And Update

- Use `AriaEngine-v1.0.0-rc.2-win-x64-installer.zip` for normal installation.
- Default install target is `%ProgramFiles%\ponkotusoft\umikaze`.
- Shortcuts launch with `--run-mode release --pak data.pak --compiled scripts/scripts.ariac`.
- Update packages can apply `update.patch` through `AriaInstaller.exe`.

## QA Gates

- `scripts/doctor.ps1` strict release gate.
- `scripts/smoke.ps1`.
- Release build and package generation.
- Manifest and checksum generation.
- Installer zip generation.
- Save migration/validation command path using an isolated release save directory.

## Known Issues

- Public code signing requires `WINDOWS_CODESIGN_PFX_BASE64` and `WINDOWS_CODESIGN_PFX_PASSWORD` in CI.
- If those secrets are absent locally, package metadata records the artifact as unsigned.
