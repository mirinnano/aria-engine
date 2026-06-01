# Demo Scenario Review Proposals

## Scope

- Worker scope: docs only. No scenario text or code was edited.
- Novel source priority: `C:\Users\quicp\OneDrive\Desktop\Novel\src` day files are treated as primary source. `memo.md` is not used as canonical text.
- Game-side files checked: `src/AriaEngine/assets/scripts/scenario_01.aria` through `scenario_08.aria`.
- Demo range for content judgement: `PROLOGUE` through the end of `DAY 4` (`scenario_05.aria`). `DAY 5` and later are checked mainly for leakage risk and continuity.

## Source Mapping

| Novel source | Game script | Demo status | Notes |
| --- | --- | --- | --- |
| `00_init.MD` | `scenario_01.aria` | Demo | PROLOGUE and opening flashbacks. |
| `01_start.md` | `scenario_02.aria` | Demo | DAY 1. Game script is substantially VN-adapted. |
| `02_day2.md` | `scenario_03.aria` | Demo | DAY 2. High-risk conversion artifacts found. |
| `03_day3.md` | `scenario_04.aria` | Demo | DAY 3. Mostly aligned; small normalization edits exist. |
| `04_day4.md` | `scenario_05.aria` | Demo end | DAY 4. Several typo/mojibake candidates found. |
| `05_day5.md` | `scenario_06.aria` | Post-demo | Should not be surfaced before demo completion. |
| `06_day6.md` | `scenario_07.aria` | Post-demo | Should not be surfaced before demo completion. |
| `07_day7.md` | `scenario_08.aria` | Post-demo | Contains police/news escalation; keep out of demo previews. |

## Full-Text Collation Summary

- `scenario_01.aria`: Mostly source-faithful at scene level, but the doctor explanation and some protagonist wording are already adapted from the Novel source. Treat any further change as author approval required.
- `scenario_02.aria`: Not a direct conversion. The opening is rewritten for VN tempo and protagonist voice (`君` -> `お前`, compressed train boarding, new wallet beat). Keep if already approved; otherwise mark as approved-needed adaptation.
- `scenario_03.aria`: Conversion is structurally risky. Many Novel narration lines appear as character dialogue, especially with `ミオ「ミオは...」`, `俺「俺は...」`, and similar prefixes. This should be prioritized before demo release.
- `scenario_04.aria`: Mostly stable. Differences are primarily orthographic normalization (`俺達` -> `俺たち`, `すいません` -> `すみません`, split lines for textbox pacing).
- `scenario_05.aria`: Mostly aligned with `04_day4.md`, but has clear mojibake/typo candidates and several typography consistency issues.
- `scenario_06` to `scenario_08`: Post-demo material includes illness escalation, `銀河鉄道の夜`, police/news exposure, and search-risk beats. Do not reveal these in demo menu copy, trailers, screenshots, chapter descriptions, or localization samples before DAY4 completion.

## Typo / Mojibake Candidates

These are likely safe to fix after owner approval because they do not change story intent.

| File | Line | Current | Proposed | Category |
| --- | ---: | --- | --- | --- |
| `scenario_03.aria` | 106 | `たどり着た` | `たどり着いた` | typo |
| `scenario_03.aria` | 125 | `たどり着た` | `たどり着いた` | typo |
| `scenario_05.aria` | 16 | `??いや、この部屋には一人しかいないんだが。` | `--いや、この部屋には一人しかいないんだが。` | mojibake |
| `scenario_05.aria` | 25 | `これっ物理的に息ができねえって。` | `これ、物理的に息ができねえって。` | typo |
| `scenario_05.aria` | 165 | `楽しみ方ももなかなか面白い` | `楽しみ方もなかなか面白い` | typo |
| `scenario_05.aria` | 497 | `お降りの方は??` | replace `??` with intended clipped announcement marker, or remove | mojibake |
| `scenario_05.aria` | 756 | `ベット` | `ベッド` | typo |
| `scenario_05.aria` | 761 | `ミオももう慣れているようで` | likely `ミオも、もう慣れているようで` | punctuation |
| `scenario_05.aria` | 863 | `ベット` | `ベッド` | typo |
| `scenario_05.aria` | 943 | `ベット` | `ベッド` | typo |

## Notation Consistency Candidates

These should be normalized only if the project wants a strict style pass.

- `俺達` vs `俺たち`: Game side often normalizes to `俺たち`. This is readable and likely preferable for VN text, but it diverges from Novel source.
- `出来る/出来た` vs `できる/できた`: Game side often normalizes to kana. Keep consistent per script.
- `すいません` vs `すみません`: `scenario_04.aria` normalizes to `すみません`. This is cleaner but changes character voice slightly.
- `う～ん` vs `うーん`: `scenario_05.aria` uses `うーん`, while Novel source has `う～ん`. Choose one for voice consistency.
- Full-width numerals: `scenario_01.aria` uses `１秒`; Novel source uses `1秒`. Prefer one convention for UI/localization.
- Ellipses: source mixes `...`, `....`, `……`. VN script should settle on `……` for polished Japanese text, unless roughness is intentional.
- Do not blindly replace every `もも` sequence. `ミオももじもじ` and `俺ももらった` are grammatically plausible; `楽しみ方ももなかなか` is the clear typo.

## VN Tempo Candidates

These are not mechanical typo fixes. They affect pacing and need author approval.

- `scenario_01.aria`: Long NVL paragraphs are split into shorter waits. This is good for readability, but some source sentences are visually broken before key nouns (`不器用で完璧な` / `「生の肯定」`). Consider whether the pause strengthens the line or over-directs it.
- `scenario_02.aria`: The opening train scene is compressed and rewritten into stronger VN beats. This improves tempo, but it changes the protagonist's voice and removes some inventory/setup details from `01_start.md`.
- `scenario_03.aria`: The speaker-prefix conversion problem harms VN readability more than line length does. Fixing narration/dialogue ownership should come before any prose polishing.
- `scenario_05.aria`: DAY4 has strong slice-of-life tempo. Keep the coin laundry and MD-recording sequence, but avoid over-explaining the "recording preserves today" motif too early.
- End of `scenario_05.aria`: The throat/food realization is a strong demo endpoint-adjacent emotional beat. It should stay understated; avoid adding extra illness exposition before DAY5.

## Localization-Difficulty Candidates

These do not require immediate rewriting, but should be flagged before translation.

- `銀河鉄道の夜` quotations in `scenario_06.aria`: Post-demo, public-domain/source-text handling and translation strategy need a separate localization note.
- Dialect/register: `ねえっ`, `よぉ`, `じゃねえ`, `だろ`, `うぐっ`, `あはは` are character-voice markers. Glossary should define how far translation can preserve roughness/cuteness.
- Place names and rail terms: Yokohama, Numazu, Sannomiya, Okayama, Matsue, Tagi, hard tickets, local train announcements. Keep a location glossary.
- Early-2000s objects: MD player, MD Walker, flip phone, CRT TV. Localizers need era notes; do not modernize them.
- Food terms: `いかめし`, `きび団子`, `メロンパン`, `明太マヨ`, `おしるこ`, `紅生姜`. Prefer transliteration plus context over broad substitution.
- Textbox pacing markers: source differences often come from line splitting rather than prose changes. Translation should preserve click rhythm, not only sentence equivalence.

## Approval-Required Body Rewrite Candidates

These must not be applied without explicit approval.

| File | Issue | Why approval is required |
| --- | --- | --- |
| `scenario_01.aria` | Doctor's line differs from Novel source: game version is more generic and less terminally concrete. | Medical/terminal framing affects tone and early spoiler intensity. |
| `scenario_01.aria` | `先生も絶対ダメ` in source appears as `お母さんも絶対ダメ`. | Changes opposition source and family pressure. |
| `scenario_01.aria` | `俺が、お前の最後の旅に付き合ってやる` is a sharpened line. | It is better as VN hook, but changes protagonist intent delivery. |
| `scenario_02.aria` | Opening DAY1 rewrite compresses boarding, money, and travel-plan setup. | Good VN tempo, but no longer full-text faithful. |
| `scenario_03.aria` | Large blocks of narration are incorrectly wrapped as dialogue. | Fixing requires changing many lines and restoring narration ownership. |
| `scenario_05.aria` | MD-recording motif may be too explicit if emphasized further. | It connects to later "trace preservation" themes and should remain subtle in demo. |
| `scenario_06.aria` | Illness escalation and `銀河鉄道の夜` symbolic material begin immediately after demo range. | Should not be teased in demo without author/product approval. |
| `scenario_08.aria` | Police/news/search exposure appears. | This is major plot pressure and should not leak into demo-facing metadata. |

## Demo Leakage Guardrails

- Demo should end after `scenario_05.aria` and avoid preview text from `scenario_06` onward.
- Do not expose these post-demo terms in demo UI summaries: `銀河鉄道の夜`, `カムパネルラ`, police/news search, face photo broadcast, hospital-bed comparison, worsening fever.
- DAY4 can hint at fragility through food, fatigue, rain, and the MD motif. It should not state the later crisis structure.
- Chapter select/debug labels for locked chapters should use neutral names only (`DAY 5`, `DAY 6`, `DAY 7`) or remain hidden in release demo builds.

## Recommended Approval Order

1. Approve mechanical typo/mojibake cleanup for `scenario_05.aria`.
2. Approve a focused `scenario_03.aria` narration/dialogue ownership repair pass.
3. Decide whether `scenario_02.aria` is an accepted VN adaptation or should be re-aligned to `01_start.md`.
4. Decide the style convention for numerals, ellipses, `俺達/俺たち`, and `出来る/できる`.
5. Decide demo public-copy guardrails for DAY5+ information.
