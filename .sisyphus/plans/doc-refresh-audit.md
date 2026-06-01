# AriaEngine docs/ 全面 Audit レポート (Phase 1)

> 作成日: 2026-06-01
> 対象 HEAD: `7e94f07 chore(lfs): enable Git LFS tracking for .zip files`
> Phase 1 完了基準: 本ファイルを作成し、ユーザー確認を得る

---

## 1. Executive Summary

`docs/` 配下を 73 ファイル（67 tracked + 6 untracked user WIP）精査した結果：

- **KEEP (現状維持で OK)**: 35 ファイル — 直近 1 ヶ月以内に更新済み、または T1/T2/T3 で間接更新済み
- **REWRITE (全面書き直し)**: 4 ファイル — `architecture/overview.md` ほか、Phase 2 で実施
- **CONSOLIDATE (他ファイルへ統合)**: 2 ファイル — `scripting/animations.md` → `reference/opcodes/animation.md`、`scripting/advanced.md` → `reference/opcodes/{chapter,flag}.md`
- **DELETE (重複のため削除)**: 4 ファイル — `scripting/{basics,control-flow,sprites,ui-elements}.md` は `reference/opcodes/` の各ファイルと完全重複
- **KEEP + MOVE (新フォルダへ移動)**: 5 ファイル — `scripting/{aria-features,core-spec,background-time-filter,effects-ux}.md` を `reference/scripting/` 配下へ
- **OUT OF SCOPE (触らない)**: 23 ファイル — `release/`, `spec/`, `ai-agent/`, `superpowers/`, `scenario/`、untracked user WIP 6 ファイル

**Phase 1 の主作業**:
1. 4 ファイル削除（`scripting/{basics,control-flow,sprites,ui-elements}.md`）
2. 6 ファイル移動（`scripting/*.md` → `reference/scripting/*.md`）
3. 2 ファイル統合（`scripting/{animations,advanced}.md` のユニーク内容を既存 opcodes へ）
4. `docs/README.md` ナビ更新
5. 1 コミット: `docs(phase-1): audit & scripting/ 整理`

---

## 2. Inventory

| 区分 | ファイル数 | 説明 |
|------|-----------|------|
| Tracked (git 管理) | 67 | HEAD にある既存ファイル |
| Untracked (user WIP) | 6 | ユーザー並行作業中の新規ファイル（**触らない**） |
| **合計** | **73** | 計画書の想定と一致 |

**Tracked 内訳**:
- 11 ファイル: `scripting/` 配下
- 13 ファイル: `reference/opcodes/`
- 18 ファイル: `release/`
- 5 ファイル: `architecture/`
- 4 ファイル: `tutorials/`
- 4 ファイル: `how-to-guides/`
- 4 ファイル: `reference/{config,init-aria,syntax}.md` + `reference/ui/button-feel.md`
- 2 ファイル: `ai-agent/`
- 1 ファイル: `development/git-github.md`
- 1 ファイル: `api/opcodes.md`
- 1 ファイル: `spec/aria-v2-strict.md`
- 1 ファイル: `superpowers/plans/2026-04-24-nscripter-runtime-ui.md`
- 1 ファイル: `superpowers/plans/2026-05-02-screen-transitions.md`
- 1 ファイル: `docs/README.md`

**Untracked (触らない)**:
- `docs/release/steam.md` (29 lines)
- `docs/release/windows-native.md` (23 lines)
- `docs/scenario/demo-scenario-review-proposals.md` (85 lines)
- `docs/scripting/localization.md` (86 lines) — **重要: scripting/ 整理対象だが、user WIP のため要確認**
- `docs/superpowers/plans/2026-05-23-web-pwa-official-target.md` (135 lines)
- `docs/superpowers/specs/2026-05-24-demo-profile-localization-design.md` (351 lines)

---

## 3. Per-File Audit Table

### 3.1 `docs/` ルート (1)

| ファイル | 状態 | アクション | 備考 |
|---------|------|----------|------|
| `docs/README.md` | T1/T2/T3 で更新済 | **REWRITE (Phase 6)** | Diátaxis ナビ刷新を Phase 6 で実施 |

### 3.2 `docs/ai-agent/` (2) — **OUT OF SCOPE**

| ファイル | 状態 | アクション | 備考 |
|---------|------|----------|------|
| `ai-agent/AGENTS.md` | 2026-05-07 | NONE | 最新 |
| `ai-agent/CODEBASE.md` | 2026-05-07 | NONE | 最新（451 行、2026-05-07 時点の内容と一致） |

### 3.3 `docs/api/` (1) — KEEP

| ファイル | 状態 | アクション | 備考 |
|---------|------|----------|------|
| `api/opcodes.md` | 2026-06-01 (T1/T2/T3) | NONE | カウント 254/271/253/3 反映済 |

### 3.4 `docs/architecture/` (5) — **REWRITE Phase 2**

| ファイル | 状態 | アクション | 備考 |
|---------|------|----------|------|
| `architecture/overview.md` | **2026-04-20** (古い) | **REWRITE (Phase 2)** | ❌ Platform/Tools/Scripting/Text/UI/AssetIO/Packaging/Utility の 7 新規ディレクトリが未記載。Refresh 必須。 |
| `architecture/vm.md` | 2026-05-07 | VERIFY (Phase 2) | 確認のみ |
| `architecture/parser.md` | 2026-05-07 | VERIFY (Phase 2) | 確認のみ |
| `architecture/rendering.md` | 2026-05-07 | VERIFY (Phase 2) | 確認のみ |
| `architecture/language-philosophy.md` | 2026-05-07 | VERIFY (Phase 2) | 確認のみ（v2 strict 設計書と一致） |

**Phase 2 追加ファイル (新規)**:
- `architecture/platform.md` (新) — IScreenshotService/IWindowService 抽象化レイヤ
- `architecture/tools.md` (新) — aria-lint/aria-pack/aria-compile 等のツール群
- `architecture/scripting-pipeline.md` (新) — ScriptCompiler/ScriptLoader
- `architecture/text-subsystem.md` (新) — TextCommandHandler 等のコマンドハンドラ

### 3.5 `docs/development/` (1) — KEEP

| ファイル | 状態 | アクション | 備考 |
|---------|------|----------|------|
| `development/git-github.md` | 2026-04-29 | NONE (計画書では ⚠️) | 短いファイル、内容確認した結果、現状と乖離なし。KEEP 判定 |

### 3.6 `docs/how-to-guides/` (4) — **REFRESH Phase 5**

| ファイル | 状態 | アクション | 備考 |
|---------|------|----------|------|
| `how-to-guides/compile-and-package.md` | 2026-05-07 | REFRESH (Phase 5) | `scripts/doctor.ps1` / `scripts/package.ps1` / `scripts/installer.ps1` 言及あり。最新フロー確認要 |
| `how-to-guides/custom-fonts.md` | 2026-04-29 | REFRESH (Phase 5) | T1/T2/T3 関連なし。`sp_cursor` の追加言及必要か確認 |
| `how-to-guides/debug-mode.md` | 2026-04-29 | REFRESH (Phase 5) | Draw Calls / Tex Loads / Color Cache / Tex Cache 言及が実装と一致するか確認 |
| `how-to-guides/troubleshooting.md` | 2026-04-29 | REFRESH (Phase 5) | BacklogTests.cs 等の既知問題追記候補あり（計画書記載） |

### 3.7 `docs/reference/` (17) — **REFRESH Phase 3**

| ファイル | 状態 | アクション | 備考 |
|---------|------|----------|------|
| `reference/config.md` | 2026-04-29 | REFRESH (Phase 3) | config.json スキーマと実装を照合 |
| `reference/init-aria.md` | 2026-04-29 | REFRESH (Phase 3) | `compat_mode on/off`、`textbox_color`、`textbox_style`、`choice_style` 言及あり。`textbox_align` 言及未確認 → 追加必要 |
| `reference/syntax.md` | 2026-05-07 | VERIFY (Phase 3) | 確認のみ |
| `reference/ui/button-feel.md` | 2026-06-01 (T2) | NONE | T2 で作成済 |
| `reference/opcodes/animation.md` | 2026-04-29 | **REWRITE (Phase 1+3)** | 168 行と短すぎ。`scripting/animations.md` (325 行) の内容を統合 |
| `reference/opcodes/audio.md` | 2026-04-29 | VERIFY (Phase 3) | 確認のみ |
| `reference/opcodes/basic.md` | 2026-04-29 | VERIFY (Phase 3) | 確認のみ |
| `reference/opcodes/button.md` | 2026-04-29 | REFRESH (Phase 3) | T2 (ButtonFeel) への参照追加必要 |
| `reference/opcodes/chapter.md` | 2026-05-07 | REFRESH (Phase 3) | `scripting/advanced.md` の defchapter 内容を統合候補 |
| `reference/opcodes/character.md` | 2026-04-29 | VERIFY (Phase 3) | 確認のみ |
| `reference/opcodes/flag.md` | 2026-04-29 | REFRESH (Phase 3) | `scripting/advanced.md` の flag/counter 内容を統合候補 |
| `reference/opcodes/index.md` | 2026-06-01 (T1/T2/T3) | REFRESH (Phase 3+6) | 230 行、`textbox_align` 追加済。Phase 6 で再整理 |
| `reference/opcodes/init.md` | 2026-05-07 | VERIFY (Phase 3) | 確認のみ |
| `reference/opcodes/script-control.md` | 2026-05-07 | VERIFY (Phase 3) | 確認のみ |
| `reference/opcodes/sprite.md` | 2026-04-29 | VERIFY (Phase 3) | 確認のみ |
| `reference/opcodes/system.md` | 2026-05-07 | VERIFY (Phase 3) | 確認のみ |
| `reference/opcodes/textbox_align.md` | 2026-06-01 (T3) | NONE | T3 で作成済 |
| `reference/opcodes/ui.md` | 2026-06-01 (T2) | VERIFY (Phase 3) | 907 行、T2 (ButtonFeel) + T3 (textbox_align) 反映済 |

### 3.8 `docs/release/` (18) — **OUT OF SCOPE** (計画書通り)

| ファイル | 状態 | アクション | 備考 |
|---------|------|----------|------|
| 18 ファイルすべて | 2026-04-29~05-30 | NONE | 計画書で「現状最新」と明記。**触らない**。 |
| `release/steam.md` (untracked) | - | NONE | user WIP。触らない |
| `release/windows-native.md` (untracked) | - | NONE | user WIP。触らない |

### 3.9 `docs/scenario/` (1) — **OUT OF SCOPE**

| ファイル | 状態 | アクション | 備考 |
|---------|------|----------|------|
| `scenario/demo-scenario-review-proposals.md` (untracked) | - | NONE | user WIP。触らない |

### 3.10 `docs/scripting/` (11) — **Phase 1 整理対象**

| ファイル | サイズ | 状態 | アクション | 理由 |
|---------|--------|------|----------|------|
| `scripting/basics.md` | 246 lines | 2026-04-24 | **DELETE** | 完全に `reference/opcodes/{basic.md,script-control.md}` + `reference/syntax.md` と重複。教育的役割は `tutorials/getting-started.md` に委譲 |
| `scripting/control-flow.md` | 273 lines | 2026-04-20 | **DELETE** | `if/for/goto/beq/bne` 等すべて `reference/opcodes/script-control.md` (363 行) に存在 |
| `scripting/sprites.md` | 280 lines | 2026-04-20 | **DELETE** | `lsp/lsp_text/lsp_rect/sp_*/btn_area/button` 等すべて `reference/opcodes/sprite.md` (635 行) と `ui.md` (907 行) に存在 |
| `scripting/animations.md` | 325 lines | 2026-04-20 | **CONSOLIDATE → `reference/opcodes/animation.md`** | `reference/opcodes/animation.md` (168 行) は短い。animations.md の一意コンテンツ（multi-property animation, status screen effects, damage effect 等）を統合して 168 行 → 400+ 行に拡張 |
| `scripting/ui-elements.md` | 428 lines | 2026-04-24 | **DELETE** | `btn/lsp_text/sp_*/btn_area/textbox/spbtn/btnwait` 等すべて `reference/opcodes/ui.md` (907 行) + `button.md` (301 行) に存在。slider/progressbar/scrolllist は手動実装チュートリアル的だが、`tutorials/creating-ui.md` の発展版として吸収候補 |
| `scripting/advanced.md` | 507 lines | 2026-04-29 | **CONSOLIDATE → `reference/opcodes/{chapter.md,flag.md}`** | `defchapter/chapter_select/unlock_chapter` → `chapter.md`、 `set_flag/get_flag/set_counter/inc_counter/get_counter` → `flag.md`。残りの char_show/char_hide/char_expression/char_move は `character.md` 既存。`save/load/auto_save` は `system.md` 既存。advanced.md 自体を削除 |
| `scripting/aria-features.md` | 153 lines | 2026-05-07 | **KEEP + MOVE → `reference/scripting/aria-features.md`** | 一意コンテンツ: 画面遷移 (transition), 文字送り音 ([se]/[voice]), ルビ ([ruby]/[rt]), ビネット/フラッシュ/パーティクル, F3 デバッグ詳細, テキスト配置の型安全化 enum, VM エラーレポート強化, GameState リファクタ, インストーラ, リリースビルド。`reference/opcodes/` の各ファイルに分散記載するより 1 ファイルに集約が読みやすい |
| `scripting/core-spec.md` | 166 lines | 2026-05-07 | **KEEP + MOVE → `reference/scripting/core-spec.md`** | 一意コンテンツ: 言語の最小仕様（文法、データモデル、struct、register save range、control flow、drawing/UI、command responsibility、compatibility、input wait result、error policy、strict lint、command registration）。`spec/aria-v2-strict.md` (714 行) は v2 strict 拡張仕様。`core-spec.md` は最小・基盤仕様で棲み分け可能 |
| `scripting/background-time-filter.md` | 36 lines | 2026-05-02 | **KEEP + MOVE → `reference/scripting/background-time-filter.md`** | 一意コンテンツ: `bgtime/bg/bgfade/bgtime_map` による時間ベース背景フィルタ。sprite.md にも背景操作はあるが、この機能は `bgtime` 固有 |
| `scripting/effects-ux.md` | 71 lines | 2026-05-02 | **KEEP + MOVE → `reference/scripting/effects-ux.md`** | 一意コンテンツ: `transition/camera/screen/textfx/voice/fx profile/sync fx`、シナリオ別の usage 一覧、安全ルール |
| `scripting/localization.md` | 86 lines | **untracked** | **DEFER — user WIP** | ⚠️ ユーザー並行作業中のファイル。整合性確保のためユーザー確認待ち（後述 §5 参照） |

**Phase 1 整理結果（untracked localization.md を除く）**:
- DELETE: 4 ファイル
- CONSOLIDATE: 2 ファイル
- KEEP + MOVE: 4 ファイル
- DEFER: 1 ファイル（user WIP）

### 3.11 `docs/spec/` (1) — **OUT OF SCOPE**

| ファイル | 状態 | アクション | 備考 |
|---------|------|----------|------|
| `spec/aria-v2-strict.md` | 2026-05-07 | NONE | 計画書で「本計画では触らない」と明記 |

### 3.12 `docs/superpowers/` (3) — **OUT OF SCOPE**

| ファイル | 状態 | アクション | 備考 |
|---------|------|----------|------|
| `superpowers/plans/2026-04-24-nscripter-runtime-ui.md` | 2026-04-24 | NONE | 企画書。触らない |
| `superpowers/plans/2026-05-02-screen-transitions.md` | 2026-05-07 | NONE | 企画書。触らない |
| `superpowers/plans/2026-05-23-web-pwa-official-target.md` (untracked) | - | NONE | user WIP。触らない |
| `superpowers/specs/2026-05-24-demo-profile-localization-design.md` (untracked) | - | NONE | user WIP。触らない |

### 3.13 `docs/tutorials/` (4) — **REFRESH Phase 4**

| ファイル | 状態 | アクション | 備考 |
|---------|------|----------|------|
| `tutorials/getting-started.md` | 2026-04-29 | REFRESH (Phase 4) | 全体的に umikaze 構成と一致。`init.aria` 内容は現状と一致。`bg`/`text`/`wait`/`end` の使い方は安定 |
| `tutorials/creating-ui.md` | 2026-04-29 | REFRESH (Phase 4) | T2 ButtonFeel 反映必要（現状は `sp_hover_color/scale` のみ。`ButtonFeel` 言及追加候補） |
| `tutorials/chapter-system.md` | 2026-04-29 | REFRESH (Phase 4) | umikaze 構成と一致。`chapter_select` が `defchapter` ブロックで生成する仕様が現状と一致 |
| `tutorials/save-load.md` | 2026-04-29 | REFRESH (Phase 4) | T1 thumbnail 修正反映。`PrepareThumbnail` タイミング、`*load_restore` の動作は現状と一致 |

---

## 4. Phase 1 Action Plan (本コミットで実施)

### 4.1 操作一覧（合計 7 ファイル移動/削除 + 2 ファイル統合）

#### A. DELETE (4 ファイル)

```bash
git rm docs/scripting/basics.md
git rm docs/scripting/control-flow.md
git rm docs/scripting/sprites.md
git rm docs/scripting/ui-elements.md
```

**削除理由の統一説明**:
- 既存 `reference/opcodes/{basic.md, sprite.md, ui.md, script-control.md}` の各ファイルと完全重複
- 教育的役割は `tutorials/getting-started.md` + `tutorials/creating-ui.md` に集約
- Diátaxis 違反（`scripting/` は `reference/opcodes/` と重複する un-categorized 配置）

#### B. KEEP + MOVE (4 ファイル)

```bash
mkdir -p docs/reference/scripting
git mv docs/scripting/aria-features.md docs/reference/scripting/aria-features.md
git mv docs/scripting/core-spec.md docs/reference/scripting/core-spec.md
git mv docs/scripting/background-time-filter.md docs/reference/scripting/background-time-filter.md
git mv docs/scripting/effects-ux.md docs/reference/scripting/effects-ux.md
```

**移動理由**:
- 既存 `reference/opcodes/` の各ファイルに該当機能の一部は記載されているが、独立した reference として読む方が理解しやすい
- `reference/scripting/` は計画書で定義された新フォルダ
- 既存ファイルに `Link:` ヘッダで相互リンクを追加（Phase 3 で実施予定）

#### C. CONSOLIDATE (2 ファイル)

**C-1: `scripting/animations.md` → `reference/opcodes/animation.md`**

1. `reference/opcodes/animation.md` の現行内容 (168 行) を保持
2. `scripting/animations.md` の一意コンテンツを統合:
   - 並列アニメーション (parallel)
   - 連続アニメーション (sequential)
   - 条件付きアニメーション
   - 実践例 (タイトル画面 / キャラクター登場 / ステータス画面 / ダメージ)
   - アニメーションのキャンセル
3. 結果: `reference/opcodes/animation.md` を約 400+ 行に拡張
4. `scripting/animations.md` を `git rm`

**C-2: `scripting/advanced.md` → `reference/opcodes/{chapter.md, flag.md, character.md}`**

1. `defchapter/chapter_id/chapter_title/chapter_desc/chapter_script/endchapter/unlock_chapter/chapter_select/chapter_card/chapter_thumbnail/chapter_progress` → `reference/opcodes/chapter.md` に統合
2. `set_flag/get_flag/clear_flag/toggle_flag/set_counter/get_counter/inc_counter/dec_counter` → `reference/opcodes/flag.md` に統合
3. `char_show/char_hide/char_expression/char_move` → `reference/opcodes/character.md` に統合
4. `save/load/auto_save` → `reference/opcodes/system.md` に統合
5. `scripting/advanced.md` を `git rm`

#### D. NEW (1 ファイル)

`docs/reference/scripting/README.md` を作成（任意、Phase 1 で必要なら）:
- 新フォルダの説明
- 4 ファイルへのナビゲーション
- 旧 `scripting/` から移った経緯の注記

#### E. DEFER (1 ファイル — 要確認)

`docs/scripting/localization.md` (untracked):
- ユーザー並行作業中の WIP ファイル
- 86 行、`language` / `loc_get` / `loc_format` / `readid` / scenario ファイル パターン / パッケージング注意事項
- 該当機能（LocalizationManager.cs, assets/i18n/）も untracked の user WIP
- 3 つの選択肢を §5 で提示

### 4.2 `docs/README.md` 更新

Phase 1 のコミット時点では**軽微な更新**で OK。Phase 6 でフル Diátaxis ナビを実施する。

最小更新内容:
- `reference/` セクションに `textbox_align` 詳細リンクは**既存**
- 新 `reference/scripting/` フォルダへの言及追加

### 4.3 コミット

```bash
git add -A
git commit -m "docs(phase-1): audit & scripting/ 整理

- DELETE: scripting/{basics,control-flow,sprites,ui-elements}.md
  (完全重複、reference/opcodes/ 配下に移行)
- KEEP+MOVE: scripting/{aria-features,core-spec,background-time-filter,effects-ux}.md
  → reference/scripting/ (一意コンテンツ)
- CONSOLIDATE: scripting/{animations,advanced}.md
  → reference/opcodes/{animation,chapter,flag,character,system}.md
- DEFER: scripting/localization.md (user WIP — 別途確認)

詳細: .sisyphus/plans/doc-refresh-audit.md"
```

---

## 5. ⚠️ 要ユーザー確認事項

### 5.1 Untracked `docs/scripting/localization.md` の扱い

`localization.md` (86 行) は **untracked** 状態で `docs/scripting/` にあります。対応する実装（`src/AriaEngine/Core/LocalizationManager.cs`、`src/AriaEngine/assets/i18n/`、`src/AriaEngine/Tools/AriaI18nCheckCommand.cs` 等）もすべて untracked の user WIP。

これは AriaEngine の「多言語対応」機能全体の作業中の WIP であり、ドキュメントの Phase 1 整理と密接に関係します。

**3 つの選択肢**:

| 選択肢 | 内容 | 影響 |
|--------|------|------|
| A. **完全 DEFER** | Phase 1 では触らず、`scripting/` フォルダに唯一残るファイルとする | 安全だが、Phase 1 の意図（`scripting/` 整理）が不完全になる |
| B. **移動のみ** | `git mv` で `reference/scripting/localization.md` へ移動。実装 WIP はそのまま。| 整理としては完了。ただし untracked ファイルの `git mv` はできないため、ファイルシステム移動 + ユーザー側で `git add` 必要 |
| C. **ユーザーコミット後に Phase 1 実施** | ユーザーが localization.md と関連実装を commit してから、Phase 1 を実施する | 最も安全だが、ユーザーの commit 待ちでブロック |

**推奨**: **A (DEFER)** — Phase 1 では触らず、Phase 1 コミット後は `docs/scripting/` に `localization.md` 1 ファイルのみが残る状態。これを「AriaEngine 多言語対応は WIP 中」と注記した README 更新のみ実施。

### 5.2 ユーザー WIP (6 ファイル) の確認

すべて OUT OF SCOPE として触らない方針で問題ないか確認:
- `docs/release/steam.md` (29 lines)
- `docs/release/windows-native.md` (23 lines)
- `docs/scenario/demo-scenario-review-proposals.md` (85 lines)
- `docs/scripting/localization.md` (86 lines) — §5.1 で個別確認
- `docs/superpowers/plans/2026-05-23-web-pwa-official-target.md` (135 lines)
- `docs/superpowers/specs/2026-05-24-demo-profile-localization-design.md` (351 lines)

### 5.3 既存実装と WIP の整合

untracked な実装ファイル（`Platform/`, `Scripting/`, `Tools/AriaI18nCheckCommand.cs`, `Core/LocalizationManager.cs` 等）がある。これは Phase 2 の `architecture/` 再構成（`platform.md`, `tools.md`, `scripting-pipeline.md`）と密接に関係。

**確認したい点**:
1. 上記 untracked 実装は Phase 2 までにユーザーが commit する予定か？
2. もし commit されない場合、Phase 2 で `architecture/*.md` に「未実装・計画中」セクションを追加すべきか？

### 5.4 Phase 1 即時実施の可否

以下を確認:
- A. **即時実施**: この audit に基づき、Phase 1 を進めて OK
- B. **一部保留**: localization.md 等の特定ファイルだけ保留
- C. **全面保留**: ユーザー WIP 全体が落ち着くまで Phase 1 も見送り

---

## 6. Out of Scope (計画書通り — 確認のみ)

以下は計画書 §「対象外」で明記されたとおり、**本計画では触らない**:

- `release/` (運用、現状最新) — 18 ファイル
- `spec/aria-v2-strict.md` (26KB、技術仕様) — 1 ファイル
- `ai-agent/` (AI 開発者向け、現状ほぼ最新) — 2 ファイル
- `superpowers/` (企画書) — 2 tracked + 2 untracked
- `scenario/` (作品固有シナリオレビュー) — 1 untracked
- コード自体への変更 — 本計画はドキュメントのみ
- 翻訳 (English セカンダリ) — 日本語版を先に整備

---

## 7. リスク・懸念 (Phase 1 特有)

### 7.1 削除ファイルのリンク切れ

`docs/scripting/{basics,control-flow,sprites,ui-elements}.md` への参照箇所を `git grep` で確認した。**現状、他 docs からの参照は tutorials/ の `次のステップ` セクションのみ**:

- `tutorials/getting-started.md` → 参照なし
- `tutorials/creating-ui.md` → `reference/opcodes/{sprite,button,animation}.md` を参照（scripting/ 参照なし）
- `tutorials/chapter-system.md` → 参照なし
- `tutorials/save-load.md` → `reference/opcodes/system.md` を参照（scripting/ 参照なし）
- `tutorials/*.md` → すべて `reference/opcodes/` への参照のみ

→ **削除しても他 docs からのリンク切れは発生しない**。安全。

### 7.2 統合時の重複

`scripting/animations.md` と `reference/opcodes/animation.md` の統合時、両方に同じ opcodes（`amsp`, `afade`, `ascale`, `arotation`, `ease`, `await`）の言及がある。Phase 1 統合時に重複しないよう、`reference/opcodes/animation.md` を master として、`scripting/animations.md` の**一意コンテンツのみ**を抽出する。

同様に `scripting/advanced.md` の統合も、各 opcodes ファイル（chapter.md, flag.md, character.md, system.md）を master として進める。

### 7.3 `scripting/` フォルダが 1 ファイル残る

§5.1 で DEFER を選択した場合、`docs/scripting/localization.md` 1 ファイルが `scripting/` に残る。これは Diátaxis 違反（重複配置）を完全には解消しないが、ユーザー WIP との衝突回避を優先する。

---

## 8. 次のステップ

### Phase 1 (本コミット)

1. **ユーザー確認待ち** — §5 の 4 項目
2. 確認後、§4 の操作を実施
3. 1 コミット: `docs(phase-1): audit & scripting/ 整理`
4. ユーザーへ報告、push 判断

### Phase 2 以降 (計画書通り)

- Phase 2: `architecture/overview.md` 全面書き直し + 4 新規ファイル
- Phase 3: `reference/opcodes/` 9 ファイル + `init-aria.md` + `config.md` 更新
- Phase 4: `tutorials/` 4 ファイル umikaze 現構成に合わせる
- Phase 5: `how-to-guides/` 4 ファイル更新
- Phase 6: `docs/README.md` フル Diátaxis ナビ刷新
- Phase 7: 6 コミットをユーザー指示で push

工数見積もり: 2-3 作業日（計画書通り）

---

## 9. 備考

- OMO Free プランで `task()` delegation 不可 → 全作業を直接ツールで実行
- 各 Phase 完了時にユーザーへ途中報告 (proactive sync)
- コミット粒度は「Phase = 1 コミット」を基本とする
- ユーザー WIP（untracked 6 ファイル）は本計画のすべての Phase で OUT OF SCOPE
