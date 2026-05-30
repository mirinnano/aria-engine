# Release Governance

## Versioning

- Use `vMAJOR.MINOR.PATCH` for public releases.
- Increment `PATCH` for fixes that keep script and save compatibility.
- Increment `MINOR` for new commands or behavior that is backward compatible.
- Increment `MAJOR` when script or save compatibility can break.

## Artifacts

Each release must include:

- release zip
- static Web/PWA package for browser releases
- optional Steam depot package
- `manifest.json`
- `checksums.txt`
- profile metadata for `Debug`, `Demo`, or `Release`
- outbound link policy for `browser_open`
- `scripts/verify-signing.ps1 -RequireSigned` audit for signed Windows release candidates
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
- Story translation stays script-owned: locale-specific scenario files may override Japanese originals, and missing translated files must fall back to the Japanese script.
- `localization.scenarioStatus` is the source of truth for public subtitle claims. `pending-translation` locales may ship as QA shims but must not be listed as Steam subtitle support.
- Demo builds must branch to `demo_end` after DAY 4 and must not unlock DAY 5+ through normal flow.
- In-app promotional copy may ask for impressions or open store/SNS links, but must not reward or solicit Steam reviews.
- Windows native and Web/PWA are official runtime targets for this release line.
- Web/PWA is the official non-Windows release target and must pass browser QA plus native/Web visual regression before public distribution.
- Steam releases must declare app ID handling, Depot contents, and Steam Cloud save path before public distribution.
