# Aria Engine Demo/Profile/Localization Final Design

**Status:** final design for user approval before implementation.

**Goal:** prepare the engine and content pipeline for a strong demo build while preserving the native Windows release path.

**Primary decision:** Demo covers PROLOGUE through DAY 4, then stops at `demo_end`. The cut should happen after the DAY 4 hotel/rain scene in `scenario_05.aria`, where the player understands that Mio can no longer eat normally, but before the story exposes the core mystery and later consequences.

---

## 1. Scope

### In Scope

- Add profile separation: `Debug`, `Demo`, `Release`.
- Keep `RunMode` for asset/script loading and add a separate runtime profile for behavior policy.
- Add Demo story flow: `scenario_01.aria` through `scenario_05.aria`, then `demo_end`.
- Read and review the manuscript source at `C:\Users\quicp\OneDrive\Desktop\Novel`.
- Compare manuscript source with game scripts in `src/AriaEngine/assets/scripts/scenario_01.aria` through `scenario_08.aria`.
- Produce scenario fix proposals that require user approval before text changes are applied.
- Add `browser_open` for user-clicked external links.
- Add localized `demo_end`, SNS impression posting, Steam/store links, and demo-clear bonus links.
- Localize scenario, UI, promo copy, public docs, and user-facing errors for `ja-JP`, `en-US`, `zh-CN`, `zh-TW`.
- Fix the Windows taskbar/window icon path.
- Add profile-aware package, manifest, QA, and release gates.

### Out of Scope

- Rebuilding the Web/PWA target as the main deliverable.
- Rewarding Steam reviews or detecting review/share completion.
- Applying scenario rewrites without explicit user approval.
- Changing the full ending or revealing DAY 5+ core information inside the demo.

---

## 2. Story Design

### Demo Range

- Demo starts at the normal title flow and includes:
  - PROLOGUE / DAY 1: `scenario_01.aria`
  - DAY 2: `scenario_02.aria`
  - DAY 3: `scenario_03.aria`, `scenario_04.aria`
  - DAY 4: `scenario_05.aria`
- Demo ends after the DAY 4 hotel/rain scene.
- Current script end point:
  - `src/AriaEngine/assets/scripts/scenario_05.aria`
  - strong stop line: `『食うのが遅い』んじゃない。もう、喉を通らないんだ。`
  - current flow then sets `chapter_06` and returns to `*chapter_select`; Demo profile should branch to `*demo_end` instead.

### Why DAY 4 Is the Demo Cut

- It shows the escape trip becoming emotionally real.
- It delivers the first strong "this cannot last" realization.
- It includes sea, train, hotel, rain, body limit, MD/recording motifs.
- It does not yet expose the main later machinery: police escalation, report/news exposure, destination escalation, final memory structure, and endgame recovery of meaning.

### `demo_end` Tone

- Quiet, emotional, not salesy.
- Use rain ambience, dark room, small typography, and a restrained CTA screen.
- Avoid a hard cliffhanger line that overexplains illness or death.
- Recommended copy direction:
  - "ここから先で、二人の旅はもう一段深く沈んでいきます。"
  - "体験版はここまでです。感想を残してもらえると、次の旅の入口になります。"
- Final UI actions:
  - Continue to title.
  - Open Steam page.
  - Post impression on X.
  - Open official site.
  - Open SNS/community links.
  - View demo-clear bonus.

### Demo-Clear Bonus

- Use "デモクリア記念" rather than "SNS共有特典" or "レビュー特典".
- Unlock immediately after demo clear, before any share action.
- Safe bonus candidates:
  - wallpaper
  - title-screen skin
  - concept art
  - short afterword
  - music/voice note if assets exist
- Do not check whether the player posted, reviewed, followed, or wishlisted.

---

## 3. Scenario Review Workflow

### Source Priority

- Treat `C:\Users\quicp\OneDrive\Desktop\Novel\src` as the current manuscript source.
- Do not treat `memo.md` as canonical when it conflicts with current day files.
- Compare manuscript day files against `scenario_01.aria` through `scenario_08.aria`.

### Proposal Classes

- **Mechanical fixes:** typos, mojibake, punctuation, obvious duplicated particles, inconsistent full-width/half-width marks.
- **Style normalization:** `ベット/ベッド`, `俺達/俺たち/俺ら`, `有る/ある`, `出来る/できる`, device and station names.
- **VN pacing edits:** page breaks, click waits, line splits, scene transitions, repeated eating/hotel/money/check-in descriptions.
- **Emotional edits:** changes to internal monologue, dialogue, Mio/Kazuki distance, illness implication, foreshadowing.
- **Structural edits:** scene deletion, scene order changes, new lines, removed lines, DAY boundary changes.

### Approval Rule

- No scenario text patch is applied directly.
- First implementation produces a review report with grouped proposed diffs.
- User approval is required before applying any scenario change.
- Mechanical fixes may be approved as one batch.
- Emotional and structural edits require item-by-item approval.

### Must Not Expose in Demo

- Mio's exact medical details, death details, or final day specifics.
- DAY 5+ police/news escalation.
- What photos, MD recordings, diary, letters, and memory media finally preserve.
- Mio's real wish, secret drawing, or future answer.
- The final meaning of the title.

---

## 4. Runtime Profile Design

### Do Not Merge With `RunMode`

- Existing `RunMode` should stay focused on loading mode:
  - `Dev`: disk/raw script loading.
  - `Release`: pak/compiled distribution loading.
- Add a separate `RuntimeProfile`:
  - `Debug`
  - `Demo`
  - `Release`

### CLI

```text
--profile debug
--profile demo
--profile release
```

### Runtime Behavior

| Profile | Loading | Dev hotkeys | `debug on` | Raw scripts | Demo branch | Signing/QA |
| --- | --- | --- | --- | --- | --- | --- |
| Debug | disk allowed | enabled | allowed | allowed | optional | not required |
| Demo | release-like | disabled | blocked | excluded | enabled | required except signing may be optional |
| Release | release-like | disabled | blocked | excluded | disabled | required |

### Engine State

- Add `RuntimeProfile` to engine settings/runtime state.
- Keep `ProductionMode` as a compatibility bool derived from `RuntimeProfile != Debug`.
- Expose profile to scripts with a small command:

```aria
getprofile $profile
```

- Demo script branch:

```aria
getprofile $profile
if $profile == "demo" goto *demo_end
goto *chapter_select
```

---

## 5. `browser_open`

### Script API

```aria
browser_open "https://example.com"
browser_open "https://example.com", %result
```

### Rules

- Only `https://` and `http://` are allowed.
- `file:`, `javascript:`, `data:`, local absolute paths, and empty URLs are rejected.
- Demo/Release use an allowlist loaded from package/profile manifest.
- The command must be triggered by user action for SNS/store buttons.
- Return result:
  - `1`: accepted by platform bridge.
  - `0`: rejected by policy or platform failure.

### Native Implementation

- Add `IBrowserService.OpenExternal(Uri uri)` under the existing platform bridge.
- Windows native uses `ProcessStartInfo` with shell execution.
- Do not store tokens or call SNS APIs.

### Web Compatibility

- Web is not the main release target in this design.
- If the Web runtime remains built, route through JS `window.open(url, "_blank", "noopener,noreferrer")`.
- Do not add Web-specific storage or PWA work to this goal.

---

## 6. SNS and Store Links

### X Impression Posting

- Use X Web Intent URL:

```text
https://twitter.com/intent/tweet?text=<encoded>&url=<encoded>&hashtags=<encoded>
```

- Composer opens only after the player presses the button.
- No auto-posting.
- No post detection.
- No reward based on posting.

### Link Resource Keys

```text
promo.share.demo_clear.cta
promo.share.demo_clear.text
promo.share.demo_clear.hashtags
promo.share.demo_clear.url.steam
promo.share.demo_clear.url.web
promo.links.official_site
promo.links.x
promo.links.youtube
promo.links.discord
promo.links.bluesky
promo.links.misskey
```

### Tracking

- Use UTM only on outbound official links:

```text
utm_source=x&utm_medium=share&utm_campaign=demo_clear&utm_content=<locale>
```

- No hidden tracking.
- No local proof of posting.

### Steam Safety

- Do not ask for Steam reviews inside the app.
- Do not reward reviews.
- Do not connect reviews to perks in copy or rewards.
- Use "感想を投稿" for SNS and "Steamページを開く" for store navigation.

Official references checked on 2026-05-24:

- Steam User Reviews: https://partner.steamgames.com/doc/store/reviews?l=english
- Steam Demos: https://partner.steamgames.com/doc/store/application/demos
- Steam Localization: https://partner.steamgames.com/doc/store/localization?l=english
- Steam Supported Languages: https://partner.steamgames.com/doc/store/localization/languages
- Steam Store Graphical Asset Rules: https://partner.steamgames.com/doc/store/assets/rules?l=english
- X Web Intents: https://developer.x.com/en/docs/twitter-for-websites/web-intents/overview

---

## 7. Localization Design

### Target Locales

- `ja-JP`: source and fallback.
- `en-US`: English.
- `zh-CN`: Simplified Chinese.
- `zh-TW`: Traditional Chinese.

Existing locale metadata already uses these four locales in `src/AriaEngine/assets/i18n/locales.json`.

### Scenario Layout

Move from root-only scenario files to locale-specific scenario bundles:

```text
src/AriaEngine/assets/scripts/scenario/ja-JP/scenario_01.aria
src/AriaEngine/assets/scripts/scenario/en-US/scenario_01.aria
src/AriaEngine/assets/scripts/scenario/zh-CN/scenario_01.aria
src/AriaEngine/assets/scripts/scenario/zh-TW/scenario_01.aria
```

Root `scenario_*.aria` files can remain as transition shims during migration, but Release/Demo packages should eventually ship the locale bundle selected by manifest.

### Localization Scope

- Scenario text and names.
- `main.aria` title/chapter/debug-visible text.
- Save/load/backlog/settings/gallery/extra UI.
- `demo_end`.
- SNS share copy and hashtags.
- Store/public README/release notes/known issues.
- User-facing errors:
  - startup failure
  - missing asset
  - save/load corruption
  - config/persistent save failure
  - fatal runtime screen

### Steam Language Claims

- Interface support requires UI key parity.
- Subtitle/scenario support requires complete scenario files and stable read IDs.
- Do not mark a language as subtitle-supported on Steam until scenario coverage is complete.
- Store page localization and in-game localization are tracked separately.

### Gates

- JSON parse for all locale resources.
- Key parity across `ui.ja-JP.json`, `ui.en-US.json`, `ui.zh-CN.json`, `ui.zh-TW.json`.
- Placeholder parity.
- Font glyph coverage.
- Hardcoded user-facing text scan.
- Locale-specific scenario file existence.
- Include target existence.
- Stable `readid` continuity across languages.
- Visual regression for title, chapter select, ADV, NVL, save, load, backlog, settings, gallery, and share screen.

---

## 8. Windows Icon Fix

### Current State

- `src/AriaEngine/AriaEngine.csproj` already declares `ApplicationIcon=assets\branding\umikaze.ico`.
- `installer/umikaze.nsi` already uses installer/shortcut icon behavior.
- Branding assets exist:
  - `src/AriaEngine/assets/branding/umikaze.ico`
  - `src/AriaEngine/assets/branding/umikaze-icon-master.png`

### Missing Runtime Piece

- Raylib window/taskbar icon is not set after `InitWindow`.

### Design

- After `Raylib.InitWindow`, load the icon through the active asset provider.
- Prefer PNG for `Raylib.SetWindowIcon` if Raylib image loading path is simplest.
- Ensure the icon asset is included in pak/package manifests.
- Add native visual evidence capture for the icon if feasible; otherwise add package manifest verification and manual QA checklist.

---

## 9. Packaging and Manifest

### `scripts/package.ps1`

Add:

```powershell
-Profile Debug|Demo|Release
```

Manifest additions:

```json
{
  "profile": "demo",
  "content": {
    "isDemo": true,
    "demoEndLabel": "demo_end",
    "lastScenario": "scenario_05"
  },
  "runtime": {
    "runMode": "release",
    "productionMode": true,
    "devHotkeys": false
  },
  "security": {
    "browserOpenPolicy": {
      "schemes": ["https", "http"],
      "allowlist": []
    }
  },
  "qa": {
    "requiredGates": []
  }
}
```

### Installer

- Demo installer/product name should be distinct from Release.
- Demo shortcut args:

```text
--run-mode release --profile demo
```

- Release shortcut args:

```text
--run-mode release --profile release
```

### CI

- Add profile matrix:
  - `Debug`: build and smoke.
  - `Demo`: package, localization gate, scenario flow smoke, browser_open policy, demo_end visual capture.
  - `Release`: package, localization gate, full flow smoke, signing/checksum/release readiness.

---

## 10. Implementation Order

### Phase 1: Profile Core

- Add `RuntimeProfile`.
- Add `--profile`.
- Derive `ProductionMode`.
- Block `debug on` and dev hotkeys outside `Debug`.
- Add `getprofile`.
- Add tests for profile parsing and behavior.

### Phase 2: Demo Flow

- Add `*demo_end`.
- Branch from `scenario_05.aria` to `*demo_end` only in Demo profile.
- Unlock demo-clear bonus before share buttons.
- Add smoke test that Demo never reaches `scenario_06`.

### Phase 3: `browser_open`

- Add opcode/registry entry.
- Add native platform browser service.
- Add URL policy and allowlist.
- Add script tests for allowed/rejected URLs.
- Wire share/store buttons through user-click handlers.

### Phase 4: Scenario Review Report

- Read manuscript source and engine scenario scripts.
- Generate a review report with proposed fixes grouped by approval class.
- Wait for user approval before applying scenario patches.

### Phase 5: Localization

- Add scenario locale layout.
- Add missing promo/demo/public/error keys.
- Add `aria-i18n-check` gate expansion.
- Add visual captures per locale.

### Phase 6: Icon and Packaging

- Set runtime window icon.
- Ensure branding assets are packaged.
- Add `-Profile` to package/release/installer scripts.
- Add profile manifest validation.

### Phase 7: Release Evidence

- Run native build.
- Run profile tests.
- Run localization gates.
- Run smoke tests.
- Capture Demo title, DAY 4 end, `demo_end`, and share screen.
- Produce release-readiness output for Demo and Release separately.

---

## 11. Acceptance Criteria

- `Debug`, `Demo`, and `Release` profiles are explicit and test-covered.
- Demo build starts like normal, plays through DAY 4, then reaches `demo_end`.
- Demo build cannot continue into DAY 5+ from normal flow.
- `browser_open` only opens allowlisted `http/https` URLs from user actions.
- Demo-clear bonus unlocks without requiring SNS/review proof.
- No in-app Steam review solicitation exists.
- Windows taskbar/window icon is not the default Raylib icon.
- `ja-JP`, `en-US`, `zh-CN`, `zh-TW` UI resources pass parity checks.
- Steam subtitle language claims are only enabled for completed scenario locales.
- Scenario edits are proposed and approved before application.
