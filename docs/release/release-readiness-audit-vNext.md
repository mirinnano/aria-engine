# Release Readiness Audit vNext

この監査メモは、NSIS installer 一本化と周辺 cleanup のリリース判定証跡です。

## Success Criteria

| 要件 | 成功条件 | 現在の証拠 | 状態 |
| --- | --- | --- | --- |
| デッドコード削除 | 旧 C# installer / Rust installer / 旧 `LoadScript` overload が実装経路から消えている | `src/AriaInstaller/`、`src/aria-installer/`、`scripts/update-installer.ps1`、旧 `LoadScript` overload を削除。grep では廃止確認用テストの否定チェックのみ検出 | 完了 |
| release build 品質 | Release build、test、smoke、doctor が通る | `dotnet test src\AriaEngine.Tests\AriaEngine.Tests.csproj --no-restore -c Release` は 232 passed。`scripts\smoke.ps1` は `ARIA smoke tests passed.`。strict doctor は 0 warnings | 完了 |
| NScripter 品質 | `main.aria` compile/lint、smoke、compat重要機能が通る | strict doctor が `Compiled scripts: 2` と `Linted 1 file(s): 0 error(s), 0 warning(s)`。`automode_time`、`effect`/`print`、`chapter_scroll`、block `if` false分岐、script-owned settings/gallery/save/load/backlog、chapter select、NVL、ADV の visual coverage と6章route/runtime flowcheckを追加済み | 完了 |
| Windows 配布 | package、release zip、NSIS setup zip を生成できる | `scripts\package.ps1 -Version v1.0.0 -Runtime win-x64`、`scripts\installer.ps1 -Version v1.0.0 -Runtime win-x64 ...`、latest setup の silent install/uninstall が成功 | 完了 |
| runtime crash 抑制 | test / smoke / doctor で重大例外なし | test / smoke / strict doctor / package / installer がすべて exit 0 | 完了 |
| 保守性 | 破壊的変更が記録され、runtime data と壊れassetが混入しない | `breaking-changes-vNext.md` 追加。`saves/` を ignore。壊れた `JosefinSans-Thin.ttf` を削除。`sub` registry衝突解消。source/reference/tests の incomplete-marker scan、package payload obsolete-file scan、pak raw `.aria` scan は clean。README / release docs は NSIS と compiled pak 前提に更新済み | 完了 |

## Verified Commands

2026-05-06 に以下を確認済み。

```powershell
dotnet test src\AriaEngine.Tests\AriaEngine.Tests.csproj --no-restore -c Release
scripts\smoke.ps1
scripts\doctor.ps1 -Project src\AriaEngine\AriaEngine.csproj -InitScript init.aria -MainScript assets\scripts\main.aria -Strict
scripts\replay.ps1 -Spec tests\replay\release-smoke.json -OutputDir artifacts\replay\results
dotnet run -c Release --project src\AriaEngine\AriaEngine.csproj -- aria-flowcheck --root src\AriaEngine --main assets/scripts/main.aria --chapters 6 --execute
scripts\visual-regression.ps1 -CaptureUiFlow -PackageDir artifacts\visual-regression\package-run-v1.0.0 -CaptureName title-screen.png -WaitSeconds 8 -StabilizeSeconds 10
scripts\visual-compare.ps1
scripts\package.ps1 -Version v1.0.0 -Runtime win-x64
scripts\installer.ps1 -Version v1.0.0 -Runtime win-x64 -PackageDir artifacts\release\AriaEngine-v1.0.0-win-x64\app
scripts\diagnostics.ps1 -OutputDir artifacts\diagnostics -Name aria-v1.0.0-diagnostics
```

## Verified Static Checks

```powershell
git diff --check
git grep -n -i -E "AriaInstaller|src/aria-installer|update-installer|JosefinSans-Thin|LoadScript\(.*Instructions" -- . ':!docs/release/release-readiness-audit-vNext.md'
reference unsupported-command wording scan
source/reference/tests incomplete-marker scan
git ls-files saves/persistent.ariasav src/AriaEngine/assets/fonts/JosefinSans-Thin.ttf src/AriaInstaller/AriaInstaller.csproj src/aria-installer/Cargo.toml scripts/update-installer.ps1
```

Notes:

- `git diff --check` は CRLF warning のみ。
- 廃止 path grep は `ReleasePipelineTests` の否定チェックのみ検出。
- unsupported-command wording scan と incomplete-marker scan は reference / src / tests を対象に検出なし。
- `git ls-files` は未コミット削除対象を表示するため、commit 前の追跡状態確認として扱う。
- latest artifact check: `artifacts\release\AriaEngine-v1.0.0-win-x64\app` に `README.md`、`release-notes.md`、`AriaEngine.exe`、`data.pak`、`scripts\scripts.ariac`、`manifest.json`、`checksums.txt` が存在し、manifest の production args は `--run-mode release --pak data.pak --compiled scripts/scripts.ariac`。
- latest pak check: `data.pak` manifest は 29 entries、raw `.aria` は 0。
- latest route/runtime flowcheck: `aria-flowcheck --root src\AriaEngine --main assets/scripts/main.aria --chapters 6 --execute` と `scripts\replay.ps1 -Spec tests\replay\release-smoke.json -OutputDir artifacts\replay\results` が成功。
- latest release zip: `artifacts\release\AriaEngine-v1.0.0-win-x64\dist\AriaEngine-v1.0.0-win-x64.zip`。
- latest NSIS artifact: `artifacts\installer\AriaEngine-v1.0.0-win-x64-installer.zip` に `umikaze-v1.0.0-win-x64-setup.exe` を収録。
- latest NSIS silent install/uninstall: `artifacts\installer-test\v1.0.0` への `/S` install と `Uninstall.exe /S` が成功。
- latest visual baseline: `title-screen.png`、`config-screen.png`、`extra-screen.png`、`gallery-screen.png`、`chapter-select.png`、`nvl-screen.png`、`adv-screen.png`、`right-menu.png`、`save-menu.png`、`load-menu.png`、`backlog-menu.png` を baseline へ昇格し、`visual-compare.json` は全11枚が `MaxDiffRatio 0.001` 未満。
- package README check: `README.md` は portable launch / checksums / troubleshooting を記載。
- release zip check: `artifacts\cicd-docs2\AriaEngine-codex-cicd-docs2-win-x64\dist\AriaEngine-codex-cicd-docs2-win-x64.zip` は `README.md`、`AriaEngine.exe`、`data.pak`、`scripts/scripts.ariac`、`manifest.json`、`checksums.txt` を含み、`dist/` 自体は含まない。

## Breaking Changes To Mention

- C# GUI installer 廃止。
- Rust installer 廃止。
- Windows installer は NSIS に一本化。
- `VirtualMachine.LoadScript(List<Instruction>, Dictionary<string, int>, string)` 削除。
- `defsub` の `sub` alias を削除し、算術 `sub` のCommandRegistry衝突を解消。
- `saves/persistent.ariasav` は source control から除外。
- 壊れた `JosefinSans-Thin.ttf` を削除し、UI font を `NotoSansJP-Regular.ttf` に統一。
- Aria v2 `struct` の `string` field は `$instance_field` に展開し、未知field / 重複field / 明らかな型不一致をParse Errorにする。
