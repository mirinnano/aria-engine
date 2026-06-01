# Steam Release

Steam support uses the normal portable Windows package as a Depot payload. Do not wrap the Steam build in the NSIS installer.

## Build

```powershell
scripts/package.ps1 -Version <version> -Runtime win-x64 -Profile release -SteamBuild -SteamAppId <app-id>
```

The package manifest records `steam.steamCompatible`, `steam.appId`, and `steam.cloudSavePath`. When `-SteamAppId` is provided, `steam_appid.txt` is written beside the executable for local Steam client testing.
It also records `localization.scenarioStatus` and `localization.steamSubtitleLanguages`; do not claim Steam subtitle support for `pending-translation` locales.

## Depot Layout

Upload the contents of `artifacts/release/AriaEngine-<version>-win-x64/app` as the Depot root:

```text
AriaEngine.exe
data.pak
boot.ari
scenario.aris
manifest.json
checksums.txt
README.md
```

## Steam Cloud

Use `saves/` as the Steam Cloud sync root. It contains normal save slots and `persistent.ariasav`, so read state, unlocked CGs, settings-backed progress, and save/load language metadata stay together.

## Verification

- Launch from Steam client and from the Depot folder directly.
- Confirm `manifest.json` has `steam.steamCompatible = true`.
- Confirm `manifest.json` has `profile = release` for the full build or `profile = demo` for a demo Depot.
- Confirm `localization.steamSubtitleLanguages` contains only `source` or `complete` scenario locales.
- Confirm save/load works, then restart through Steam and verify Steam Cloud restores `saves/`.
- Confirm language switching for `ja-JP`, `en-US`, `zh-CN`, and `zh-TW`.
- Confirm overlay does not block fullscreen/windowed switching.
