# v1.0.0 Compatibility Contract

## Runtime Files

- Production launch uses `data.pak` and `scripts/scripts.ariac`.
- Raw `assets/` and `init.aria` are not required in normal release packages.
- `manifest.json`, `checksums.txt`, and `release-notes.md` are part of the release payload.

## Saves

- Normal saves use AriaSave v3.
- Persistent progress uses PersistentGameData v2.
- Save migration must back up user data before rewriting.
- Release validation must not depend on developer-local save slots.

## Script Compatibility

- Engine-owned menu actions: `save`, `load`, `backlog`, `lookback`, `rmenu`.
- Script-owned screens: settings, gallery, and omake-style extras.
- Deprecated command behavior must warn before removal.

## Installer Compatibility

- Default install path remains `%ProgramFiles%\ponkotusoft\umikaze`.
- Installer receipt must record version, source, installed file count, encryption state, and signature state.
- Shortcuts must launch the release-mode packed payload.
