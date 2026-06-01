# Localization

Aria localization is low-level language state, resource lookup, font selection, and stable read IDs. It does not auto-translate story text or rewrite story layout.

## Commands

```aria
language "en-US"
getlanguage $lang
lang_count %lang_count
lang_at 0, $first_lang
loc_get $label, "settings.title"
loc_format $msg, "confirm.load_slot", 5
readid "scenario01.opening.001"
```

Supported locale codes in the default resource manifest are `ja-JP`, `en-US`, `zh-CN`, and `zh-TW`.
Use `lang_count` and `lang_at` when a script-owned settings screen needs to build a language selector from the manifest instead of hardcoding locale buttons.

The manifest also records story coverage:

- `scenarioRoot`: locale scenario bundle root.
- `scenarioFiles`: required story files for each locale.
- `scenarioStatus`: `source`, `complete`, or `pending-translation`.

Steam subtitle language claims may use only `source` or `complete` scenario locales. Pending shims exist for QA and packaging parity, not for public subtitle support.

## UI Resources

Use `loc_get` for script-owned utility labels:

```aria
loc_get $ui_config_title, "settings.title"
ui_text 200, $ui_config_title, 640, 58
```

Engine-owned menu labels use the same keys through `LocalizationManager`.

Use `loc_format` when a localized label needs runtime values:

```aria
loc_format $confirm, "confirm.load_slot", %slot
text $confirm
```

## Key Coverage

Run `aria-i18n-check` before release to catch missing locale keys:

```bash
dotnet run --project src/AriaEngine/AriaEngine.csproj -- aria-i18n-check --root src/AriaEngine --manifest assets/i18n/locales.json --scripts assets/scripts --code UI --code Core
```

Missing keys are errors. Unused keys are warnings because engine-owned and generated UI can intentionally keep reusable labels.

## Story Files

Story localization should be selected by script choice. The default Japanese files remain the source of truth unless translated scenario files are authored separately.

Pattern A: language-specific scenario files.

```aria
getlanguage $lang
if $lang == "en-US" { include "scenario/en-US/scenario_01.aria" }
if $lang == "zh-CN" { include "scenario/zh-CN/scenario_01.aria" }
if $lang == "zh-TW" { include "scenario/zh-TW/scenario_01.aria" }
if $lang == "ja-JP" { include "scenario/ja-JP/scenario_01.aria" }
goto *scenario_01
```

The current non-Japanese files are explicit shims that include the Japanese source until approved translations are authored.

Pattern B: keyed lines for small shared UI/story fragments.

```aria
readid "scenario01.opening.001"
loc_get $line, "scenario01.opening.001"
text $line
@
```

Use stable `readid` values across languages so read state, skip state, saves, and backlog references survive file/line changes.

## Packaging Notes

If translated scenario files are selected at runtime with `include`, they must be shipped in the package or compiled into a locale-specific script bundle. Do not remove translated scenario inputs from the final artifact unless the selected language path is compiled ahead of time.
