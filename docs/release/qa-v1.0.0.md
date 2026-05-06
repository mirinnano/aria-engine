# QA Record: v1.0.0

## Automated Gates

| Gate | Evidence | Status |
| --- | --- | --- |
| Unit tests | `dotnet test src\AriaEngine.Tests\AriaEngine.Tests.csproj --no-restore -c Release` | Pass, 221 tests |
| Smoke | `scripts\smoke.ps1` | Pass |
| Strict doctor | `scripts\doctor.ps1 -Project src\AriaEngine\AriaEngine.csproj -InitScript init.aria -MainScript assets\scripts\main.aria -Strict` | Pass |
| Replay | `scripts\replay.ps1 -Spec tests\replay\release-smoke.json -OutputDir artifacts\replay\results` | Pass |
| Diagnostics | `scripts\diagnostics.ps1 -OutputDir artifacts\diagnostics -Name aria-v1.0.0-diagnostics` | Pass |
| Visual capture | `scripts\visual-regression.ps1 -CaptureUiFlow -PackageDir artifacts\release\AriaEngine-v1.0.0-win-x64\app -CaptureName title-screen.png -WaitSeconds 8 -StabilizeSeconds 10` | Pass: title, config, extra, gallery, right-menu |
| Visual compare | `scripts\visual-compare.ps1` | Pass, five baselines diffRatio 0 |
| Package | `scripts\package.ps1 -Version v1.0.0 -Runtime win-x64` | Pass |
| NSIS installer | `scripts\installer.ps1 -Version v1.0.0 -Runtime win-x64 -PackageDir artifacts\release\AriaEngine-v1.0.0-win-x64\app` | Pass |
| Production pak | `data.pak` manifest has 29 entries and raw `.aria` count is 0 | Pass |
| Release zip | Required files present and no nested `dist/` directory | Pass |
| Silent install/uninstall | `umikaze-v1.0.0-win-x64-setup.exe /S /D=<temp>` then `Uninstall.exe /S` | Pass |
| Portable launch | `AriaEngine.exe --run-mode release --pak data.pak --compiled scripts/scripts.ariac` from package app dir | Pass: stayed running for 8 seconds, closed with exit code 0 |

## Manual Gates

| Gate | Status |
| --- | --- |
| Fresh install to temp install directory | Automated silent install pass |
| Launch from installer shortcut | Not recorded |
| Launch portable package | Automated launch pass |
| New game | Covered by smoke at parser/runtime level, manual visual pass not recorded |
| Existing save load | Automated empty save validation passes, migrated real-save pass not recorded |
| Persistent progress restore | Automated schema path covered, manual playthrough pass not recorded |
| Save/load/backlog/right-click menu | Automated command path covered, manual visual pass not recorded |
| Settings and gallery | Visual capture recorded for config, extra, and gallery |
| Uninstall | Automated silent uninstall pass |

## Release Decision

Automated gates plus script-owned UI visual baselines qualify the local unsigned v1.0.0 candidate. Public release promotion still needs a signing decision and any required full manual playthrough sign-off.
