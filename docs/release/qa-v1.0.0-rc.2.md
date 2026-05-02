# QA Record: v1.0.0-rc.2

## Automated Gates

- [ ] `scripts/doctor.ps1 -Strict`
- [ ] `scripts/smoke.ps1`
- [ ] `scripts/release.ps1 -Version v1.0.0-rc.2 -Runtime win-x64`
- [ ] `scripts/installer.ps1 -Version v1.0.0-rc.2 -Runtime win-x64`
- [ ] package zip contains `manifest.json`, `checksums.txt`, `release-notes.md`, `data.pak`, and `scripts/scripts.ariac`
- [ ] installer zip contains `AriaInstaller.exe` and bundled `app/`

## Manual Gates

- [ ] Fresh install to Program Files
- [ ] Launch from installer
- [ ] Launch from desktop shortcut
- [ ] New game
- [ ] Existing save load
- [ ] Persistent progress restore
- [ ] Save/load/backlog/right-click menu
- [ ] Settings and gallery
- [ ] Uninstall or manual cleanup path confirmed

## Release Decision

- RC can be promoted only after all automated gates pass and manual gates are checked.
