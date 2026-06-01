# AriaEngine ドキュメント全面刷新 計画

## TL;DR

> **Quick Summary**: `docs/` 配下 73 ファイルの古くなったドキュメントを全面的に刷新する。Diátaxis フレームワークに再構成し、umikaze v1.0.0-rc.2 + 最近の T1/T2/T3 改善を反映する。
>
> **Estimated Effort**: Large (3-5 人日)
> **並列実行**: 一部可能 (Phase 4-5 は 4 ファイルずつ並列着手可)
> **Critical Path**: scripting/ 整理 → reference/opcodes 更新 → コミット
> **前提**: 直前の UX Quick Wins (T1/T2/T3) コミット済み

---

## 現状サマリ (2026-06-01 時点)

| カテゴリ | ファイル数 | 最終更新 | 状態 |
|----------|-----------|---------|------|
| `tutorials/` | 4 | 2026-04-29 | ❌ 全面刷新必要 (umikaze 現構成と乖離) |
| `how-to-guides/` | 4 | 2026-04-29 | ⚠️ コード照合で要更新 |
| `reference/` | 18 | 2026-04-29 ~ 06-01 | ⚠️ 9 ファイルが古い |
| `reference/opcodes/` | 13 | 2026-04-29 ~ 06-01 | ⚠️ 9 ファイルが古い |
| `reference/ui/` | 1 (button-feel.md) | 2026-06-01 | ✅ 新規 |
| `architecture/` | 5 | 2026-04-19 ~ 05-06 | ❌ 全面刷新必要 (新コンポーネント未記載) |
| `scripting/` | 11 | 2026-04-19 ~ 05-24 | ❌ **Diátaxis 違反**: reference/opcodes/ と重複 |
| `release/` | 16 | 2026-04-29 ~ 05-24 | ✅ ほぼ最新 |
| `spec/` | 1 | 2026-05-06 | ✅ 最新 |
| `ai-agent/` | 2 | 2026-05-06 | ✅ ほぼ最新 |
| `superpowers/` | 3 | 2026-05-23 | ✅ 最新 (企画書) |
| `scenario/` | 1 | 2026-05-24 | ✅ 最新 |
| `development/` | 1 | 2026-04-29 | ⚠️ 要確認 |
| `api/opcodes.md` | 1 | 2026-06-01 | ✅ 直近更新 |
| `docs/README.md` | 1 | 2026-06-01 | ✅ 直近更新 |
| **合計** | **73** | | |

---

## Diátaxis 再構成

### 現状の構造 (問題あり)

```
docs/
├── api/                      # 補助
├── architecture/             # 理解
├── ai-agent/                 # 別軸 (audience-specific)
├── development/              # 補助
├── how-to-guides/            # 問題解決
├── reference/                # 情報参照
│   ├── opcodes/              # 13 ファイル
│   └── ui/                   # 新規
├── release/                  # 運用 (Diátaxis 外)
├── scenario/                 # 作品固有
├── scripting/                # ❌ 11 ファイル - 重複・未分類
├── spec/                     # 仕様書 (architecture 寄り)
├── superpowers/              # 企画書 (Diátaxis 外)
└── tutorials/                # 学習
```

### 目標の構造 (Diátaxis 準拠)

```
docs/
├── api/                      # 補助 (維持)
├── architecture/             # 理解 (刷新)
│   ├── overview.md           # ❌→✅ 全面書き直し
│   ├── vm.md                 # ✅→✅ 確認
│   ├── parser.md             # ✅→✅ 確認
│   ├── rendering.md          # ✅→✅ 確認
│   ├── platform.md           # 🆕 Platform/ 抽象化レイヤ
│   ├── tools.md              # 🆕 Tools/ aria-* コマンド群
│   ├── scripting-pipeline.md # 🆕 Scripting/ コンパイル・暗号化
│   └── text-subsystem.md     # 🆕 Text/ サブシステム
├── how-to-guides/            # 問題解決 (確認・更新)
├── reference/                # 情報参照
│   ├── opcodes/              # 13 ファイル確認・更新
│   ├── ui/                   # 新規 (button-feel.md)
│   ├── config.md             # ⚠️→✅ 更新
│   ├── init-aria.md          # ⚠️→✅ 更新
│   ├── syntax.md             # ✅→✅ 確認
│   └── scripting/            # 🆕 旧 scripting/ を統合
├── release/                  # 運用 (維持)
├── scenario/                 # 作品固有 (維持)
├── spec/                     # 仕様書 (維持)
├── tutorials/                # 学習 (刷新)
├── ai-agent/                 # 別軸 (維持)
└── development/              # 開発者向け (維持)
```

### `scripting/` フォルダの処理 (Phase 1 で決定)

11 ファイルの現状:
- `basics.md` (6612B, 2026-04-23)
- `control-flow.md` (6463B, 2026-04-19)
- `sprites.md` (8106B, 2026-04-19)
- `animations.md` (8574B, 2026-04-19)
- `ui-elements.md` (11645B, 2026-04-23)
- `advanced.md` (15035B, 2026-04-29)
- `aria-features.md` (4217B, 2026-05-06)
- `core-spec.md` (5102B, 2026-05-06)
- `background-time-filter.md` (980B, 2026-04-30)
- `effects-ux.md` (2294B, 2026-04-30)
- `localization.md` (3073B, 2026-05-24)

**判断基準**:
- `reference/opcodes/*.md` と内容が重複 → 削除 or 統合
- 他のどこにもない固有情報 → `reference/scripting/` に移動
- 短すぎる (1KB 未満) → 削除 or 統合

---

## Phase 別作業

### Phase 0: 計画書作成 ✅
- 本ファイル作成

### Phase 1: Audit + scripting/ 整理 (0.5-1 人日)

**やること**:
1. 全 73 ファイルの KEEP/REWRITE/CONSOLIDATE/DELETE を決定 (audit テーブル完成)
2. `scripting/` 11 ファイルのうち、固有情報があるものを `reference/scripting/` に移動
3. `reference/opcodes/` と完全に重複しているものを削除
4. Diátaxis README.md に反映

**完了基準**:
- audit テーブル完成
- `scripting/` フォルダが空 or 削除
- `reference/scripting/` (新) に価値あるファイルのみ残存

### Phase 2: Architecture 再構成 (1-1.5 人日)

**やること**:
1. `architecture/overview.md` 全面書き直し (新ディレクトリ構造反映)
2. `architecture/platform.md` 新規 (IScreenshotService/IWindowService 等の抽象化レイヤ)
3. `architecture/tools.md` 新規 (aria-lint/aria-pack/aria-compile 等)
4. `architecture/scripting-pipeline.md` 新規 (ScriptCompiler/ScriptLoader)
5. `architecture/text-subsystem.md` 新規 (TextCommandHandler 等のコマンドハンドラ)
6. 既存 `architecture/vm.md`, `parser.md`, `rendering.md` の確認・更新

**完了基準**:
- すべての新ディレクトリ (Platform, Tools, Scripting, Text, UI, AssetIO, Packaging, Utility) がアーキ図に反映
- 新規 architecture ファイル 4 件が追加

### Phase 3: Reference/opcodes 更新 (1-1.5 人日)

**やること**:
1. 全 13 ファイルと実装を照合
2. opcode 追加・削除・変更を反映
3. T1/T2/T3 関連の参照を `button-feel.md`, `textbox_align.md` に追加
4. `reference/init-aria.md` を umikaze 現構成 (`compat_mode on`, `textbox_align` 追加) に合わせる
5. `reference/config.md` を最新に

**完了基準**:
- 全 opcode が `OpCode.cs` と一致
- v2 strict (`# aria-version: 2.0`) の説明が現状と一致
- 9 ファイル更新完了

### Phase 4: Tutorials 刷新 (1 人日)

**やること**:
1. `tutorials/getting-started.md` - umikaze 現構成 (init.aria 既存) に合わせて書き直し
2. `tutorials/creating-ui.md` - T2 ButtonFeel 反映
3. `tutorials/chapter-system.md` - umikaze の chapter 構造に合わせて
4. `tutorials/save-load.md` - T1 サムネイル修正反映

**完了基準**:
- 各チュートリアルが最新コードで動作する手順を記載
- v2 strict (`# aria-version: 2.0`) の例を併記

### Phase 5: How-to-guides 更新 (0.5-1 人日)

**やること**:
1. `how-to-guides/compile-and-package.md` - 最新リリース手順
2. `how-to-guides/custom-fonts.md` - 確認
3. `how-to-guides/debug-mode.md` - F3 デバッグ仕様確認
4. `how-to-guides/troubleshooting.md` - 既知の問題 (BacklogTests.cs 等) を追記

**完了基準**:
- すべての how-to が現環境で動作

### Phase 6: docs/README.md + Diátaxis ナビ (0.5 人日)

**やること**:
1. `docs/README.md` - Diátaxis 4 象限ナビゲーション刷新
2. `docs/reference/opcodes/index.md` - 全 opcode 一覧を最新化
3. 相互リンク (architecture ↔ reference ↔ tutorials ↔ how-to)

**完了基準**:
- Diátaxis 4 象限すべてに最低 1 ファイル
- 孤立ドキュメント (どこからもリンクなし) がゼロ

### Phase 7: コミット・プッシュ (Phase ごとに)

**やること**:
- 各 Phase 完了時に 1 コミット (8 コミット想定)
- コミットメッセージ規約: `docs(phase-N): 説明`
- プッシュは Phase ごと (ユーザー指示で)

---

## リスク・懸念

### 高い影響度
- **`architecture/overview.md` 全面書き直し**: 図が古いと新人が混乱。Phase 2 で最優先。
- **`scripting/` フォルダ削除**: リンク切れの懸念 → `git grep "scripting/"` で参照箇所を確認後に削除
- **`tutorials/getting-started.md` 刷新**: 既存ユーザーが混乱する可能性。新旧両対応の手順を残すか?

### 中程度
- 73 ファイル全文チェックは OMO Free プランで `task()` delegation 不可 → 直接ツールで 1 つずつ照合
- 各 Phase 完了時の「正しいコミット粒度」が属人的

### 低い
- `release/`, `spec/`, `ai-agent/`, `scenario/`, `superpowers/` は最新なので触らない

---

## 対象外 (Out of Scope)

- `release/` (運用、現状最新) - **本計画では触らない**
- `spec/aria-v2-strict.md` (26KB、技術仕様) - **本計画では触らない**
- `ai-agent/` (AI 開発者向け、現状ほぼ最新) - **確認のみ**
- `superpowers/` (企画書) - **本計画では触らない**
- `scenario/` (作品固有シナリオレビュー) - **本計画では触らない**
- コード自体への変更 - 本計画はドキュメントのみ
- 翻訳 (English セカンダリ) - 日本語版を先に整備

---

## Definition of Done (Phase 別)

### Phase 1
- [ ] audit テーブル完成
- [ ] `scripting/` フォルダが整理される
- [ ] `reference/scripting/` (新) に価値あるファイルのみ残存
- [ ] 1 コミット: `docs(phase-1): audit & scripting/ 整理`

### Phase 2
- [ ] `architecture/overview.md` が新ディレクトリ構造を反映
- [ ] `architecture/{platform,tools,scripting-pipeline,text-subsystem}.md` 新規追加
- [ ] 1 コミット: `docs(phase-2): architecture 再構成`

### Phase 3
- [ ] `reference/opcodes/*.md` 9 ファイル更新
- [ ] `reference/{init-aria,config}.md` 更新
- [ ] T1/T2/T3 関連参照追加
- [ ] 1 コミット: `docs(phase-3): reference 更新`

### Phase 4
- [ ] `tutorials/*.md` 4 ファイル刷新
- [ ] umikaze 現構成と一致
- [ ] 1 コミット: `docs(phase-4): tutorials 刷新`

### Phase 5
- [ ] `how-to-guides/*.md` 4 ファイル更新
- [ ] 1 コミット: `docs(phase-5): how-to-guides 更新`

### Phase 6
- [ ] `docs/README.md` 刷新
- [ ] `reference/opcodes/index.md` 全文更新
- [ ] 孤立ドキュメント ゼロ
- [ ] 1 コミット: `docs(phase-6): README + index`

### Phase 7
- [ ] ユーザー指示で 6 コミットをプッシュ

---

## 工数見積もり

| Phase | 推定工数 | 累積 |
|-------|---------|------|
| Phase 0: 計画 | 0.5h | 0.5h |
| Phase 1: Audit | 2-3h | 2.5-3.5h |
| Phase 2: Architecture | 4-6h | 6.5-9.5h |
| Phase 3: Reference | 4-6h | 10.5-15.5h |
| Phase 4: Tutorials | 3-4h | 13.5-19.5h |
| Phase 5: How-to | 2-3h | 15.5-22.5h |
| Phase 6: README | 1-2h | 16.5-24.5h |
| Phase 7: Push | 0.5h | 17-25h |

**合計: 2-3 作業日 (1 人)**

---

## 備考

- OMO Free プランで `task()` delegation 不可 → 全作業を直接ツールで実行
- 各 Phase 完了時にユーザーへ途中報告 (proactive sync)
- コミット粒度は「Phase = 1 コミット」を基本とする
- 必要に応じて `Momus` で plan レビュー予定
