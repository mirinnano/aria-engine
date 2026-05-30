# Production Checklist

Use this before publishing a production build.

## Required Gates

- `scripts/doctor.ps1` passes.
- `scripts/smoke.ps1` passes.
- `dotnet build src/AriaEngine/AriaEngine.csproj -c Release --no-restore` passes.
- `aria-compile` succeeds for `init.aria` and `assets/scripts/main.aria`.
- `aria-flowcheck --root src/AriaEngine --main assets/scripts/main.aria --chapters 6 --execute` passes.
- `scripts/package.ps1 -Version <version>` creates a release directory and zip.
- Demo packages use `scripts/package.ps1 -Version <version> -Profile Demo`.
- Full packages use `scripts/package.ps1 -Version <version> -Profile Release`.
- Runtime-specific builds use `-Runtime win-x64` when the restore environment is available.
- `manifest.json` and `checksums.txt` are present in the package.
- `manifest.json` records `profile`, `productionRunArgs`, `security.browserOpenPolicy`, `localization.scenarioStatus`, and `localization.steamSubtitleLanguages`.
- `security.browserOpenPolicy.allowlist` contains only approved outbound hosts such as `store.steampowered.com`, `twitter.com`, `x.com`, and `ponkotsu-soft.vercel.app`.
- `README.md` is present in the package.
- The packaged build launches with production arguments.
- Packaged v3 split Pak files (`boot.arib`, `scenario.aris`, `data.arid`, `voice.ariv`) resolve from the app directory, not only the caller working directory.
- Production `data.pak` does not include raw `.aria` scripts.
- `scripts/diagnostics.ps1` creates a diagnostics zip.
- `aria-save migrate` and `aria-save validate` pass on test saves.
- `scripts/visual-regression.ps1 -CaptureLaunch` captures a non-blank packaged launch screenshot.
- `scripts/visual-compare.ps1` passes against tracked baselines in `tests/visual-regression/baseline`.
- `scripts/replay.ps1 -Spec tests/replay/release-smoke.json` passes for the tracked release replay spec.
- `makensis.exe` is available before running the installer gate.
- `scripts/installer.ps1` creates the NSIS installer zip.
- Signed installer candidates pass `scripts/verify-signing.ps1 -RequireSigned`.
- Release signing is configured with `WINDOWS_CODESIGN_PFX_BASE64` and `WINDOWS_CODESIGN_PFX_PASSWORD`, or with `ARIA_SIGN_CERT_THUMBPRINT`; otherwise `scripts/sign.ps1` must fail with `Code signing is not configured`.
- `ARIA_SIGN_ALLOW_SELF_SIGNED` is permitted only for local smoke checks and must not be treated as a trusted public release gate.
- Installer shortcuts set their working directory to `$INSTDIR`.
- `release-notes.md` is present in the package.
- `manifest.json` records compatibility, packaging, and signing state.
- Release CI uses the same `scripts/release.ps1` and `scripts/installer.ps1` path as local release builds.
- Windows release matrix records FD and SC artifacts separately.
- NativeAOT artifacts remain experimental until warning, runtime launch, installer, and signing gates are clean or explicitly waived.
- `scripts/package-web.ps1` creates the static Web/PWA release package.
- `scripts/web-device-qa.ps1` passes for Chrome, Edge, Safari, and mobile browser profiles.
- `web-browser-qa-chrome.json`, `web-browser-qa-edge.json`, `web-browser-qa-safari.json`, and `web-browser-qa-mobile.json` are present and `ready: true`.
- `scripts/web-native-visual-compare.ps1` passes against the current Windows native capture.
- `web-native-visual-compare.json` records passing title, text, and menu comparisons.
- Localization resources are packaged and missing-key fallback is verified for `ja-JP`, `en-US`, `zh-CN`, and `zh-TW`.
- `aria-i18n-check --root src/AriaEngine --scripts assets/scripts --code Core --code UI` passes and validates locale scenario bundle file existence.
- Steam subtitle language claims include only scenario locales marked `source` or `complete`.
- Steam builds record manifest metadata and keep `saves/` compatible with Steam Cloud.
- Windows native and Web/PWA are official runtime targets for this release line.
- `scripts/prepare-release-evidence.ps1` creates a signing audit before the final readiness audit.
- `scripts/release-readiness-audit.ps1` passes against Windows package, NativeAOT package, and signing audit artifacts.
- `scripts/release-readiness-report.ps1` generates the prompt-to-artifact checklist report from `release-readiness-audit.json`.

## Manual QA

- Start a new game.
- Load an existing save.
- Confirm `persistent.ariasav` restores read/progress state.
- Open save, load, backlog, and right-click menus.
- Confirm settings and gallery still work as script-owned screens.
- Confirm language-specific scenario files are selected by locale when translated files are shipped.
- Confirm Demo profile reaches `demo_end` after DAY 4 and cannot unlock DAY 5+ through normal chapter flow.
- Confirm `demo_end` opens only the Steam page, X intent/profile, and official site from user-clicked buttons.
- Confirm Steam local run works with `steam_appid.txt` only in the Steam build profile.
- Confirm F3/F5/F9 development hotkeys do not operate in production mode.

## Release Blockers

- Missing assets.
- Script compile errors.
- Corrupt save files without a migration or reset path.
- Any crash on startup.
- Broken save/load/backlog/rmenu.
- Missing release notes or missing signing status metadata.
- Public release artifact marked unsigned when CI signing secrets were expected.
