# Release Governance

## Versioning

- Use `vMAJOR.MINOR.PATCH` for public releases.
- Increment `PATCH` for fixes that keep script and save compatibility.
- Increment `MINOR` for new commands or behavior that is backward compatible.
- Increment `MAJOR` when script or save compatibility can break.

## Artifacts

Each release must include:

- release zip
- `manifest.json`
- `checksums.txt`
- changelog or release notes
- known issues

## Rollback

- Keep the previous release artifact.
- Keep save/config migration notes with the release.
- If startup, save/load, or script compile fails after release, roll back the artifact first.

## Compatibility Policy

- Engine-owned `save`, `load`, `backlog`, `lookback`, and `rmenu` actions stay reserved.
- Script-owned custom screens such as settings and gallery remain supported.
- Deprecated opcodes should warn before removal.
- Save/config schema changes require a migration note before release.
