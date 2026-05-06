# umikaze v1.0.0-rc.2 Release Notes

## Summary

- Release candidate for the story-included v1.0.0 line.
- Packages the umikaze story payload as `data.pak` plus `scripts/scripts.ariac`.
- Includes installer metadata, checksums, release notes, and compatibility metadata in `manifest.json`.
- Includes package `README.md` for portable/manual deployment.
- Resolves packaged `data.pak` from the installed app directory so shortcuts and portable launches do not depend on caller working directory.
- Omits raw `.aria` source scripts from production `data.pak`; packaged builds use `scripts/scripts.ariac`.

## Compatibility

- Normal save schema: AriaSave v3.
- Persistent save schema: PersistentGameData v2.
- Config schema: AppConfig v1.
- `save`, `load`, `backlog`, `lookback`, and `rmenu` remain engine-owned actions.
- Settings and gallery remain script-owned screens.
- NScripter-style `effect` / `print` now execute mapped screen transitions instead of inert compatibility shims.
- `chapter_scroll` now adjusts chapter-select card offset and redraws the selection UI.

## Install And Update

- Use `AriaEngine-v1.0.0-rc.2-win-x64-installer.zip` for normal installation.
- The installer is built with NSIS from `installer/umikaze.nsi`.
- Default install target is `%LOCALAPPDATA%\Ponkotusoft\umikaze`.
- Shortcuts launch with `--run-mode release --pak data.pak --compiled scripts/scripts.ariac`.
- Installer shortcuts set the working directory to the install directory.
- Patch files can still be published with `scripts/patch.ps1`; NSIS update packaging is not part of this RC.

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
