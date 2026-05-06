# umikaze Release Package

This folder is the production payload for the Windows release build.

## Launch

Use the installer shortcut for normal play. For portable/manual launch, run:

```powershell
.\AriaEngine.exe --run-mode release --pak data.pak --compiled scripts/scripts.ariac
```

`AriaEngine.exe` also resolves `data.pak` relative to this folder when launched from a shortcut.

## Files

- `AriaEngine.exe`: game runtime
- `data.pak`: packed assets and release payload
- `scripts/scripts.ariac`: compiled script bundle
- `manifest.json`: build, compatibility, packaging, and signing metadata
- `checksums.txt`: SHA-256 checksums for shipped files
- `release-notes.md`: release notes when available
- `config.template.json`: default config reference

Raw `.aria` source scripts are not included in normal production packages.

## Save Data

Runtime saves are created outside the source tree at runtime. Do not copy developer-local `saves/` into release payloads.

## Troubleshooting

- If launch fails, check `aria_error_ai.txt` or `aria_error.log`.
- If a file looks corrupted, compare it with `checksums.txt`.
- If installed shortcuts fail, confirm the install folder contains `data.pak` and `scripts/scripts.ariac`.
