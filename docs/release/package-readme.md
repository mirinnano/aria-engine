# umikaze Release Package

This folder is the production payload for the Windows release build.

## Launch

Use the installer shortcut for normal play. For portable/manual launch, run:

```powershell
.\AriaEngine.exe --run-mode release --profile release
```

`AriaEngine.exe` resolves the v3 split Pak files relative to this folder when launched from a shortcut.

Demo builds use `--profile demo`. Demo and release profiles disable development hotkeys and enforce the packaged `browser_open` allowlist for Steam, X, and the official site.

## Files

- `AriaEngine.exe`: game runtime
- `boot.arib`: packed boot/init payload
- `scenario.aris`: packed script/scenario payload
- `data.arid`: packed image/font/data payload
- `voice.ariv`: packed voice/audio payload
- `scripts/scripts.ariac`: compiled script bundle
- `manifest.json`: build, compatibility, packaging, and signing metadata
- `checksums.txt`: SHA-256 checksums for shipped files
- `release-notes.md`: release notes when available
- `config.template.json`: default config reference
- `assets/i18n`: packaged localization resources when present
- `steam_appid.txt`: Steam local test app ID, only for Steam build profiles

`manifest.json` records `profile`, `productionRunArgs`, `security.browserOpenPolicy`, `localization.scenarioStatus`, and `localization.steamSubtitleLanguages`.

Raw `.aria` source scripts are not included in normal production packages.

## Save Data

Runtime saves are created outside the source tree at runtime. Do not copy developer-local `saves/` into release payloads.

Steam Cloud profiles should map the runtime `saves/` path.

## Troubleshooting

- If launch fails, check `aria_error_ai.txt` or `aria_error.log`.
- If a file looks corrupted, compare it with `checksums.txt`.
- If installed shortcuts fail, confirm the install folder contains `data.pak` and `scripts/scripts.ariac`.
