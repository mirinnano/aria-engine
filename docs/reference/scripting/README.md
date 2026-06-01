# Reference: Scripting

スクリプト言語 (`.aria`) に関する reference ドキュメント。
`reference/opcodes/` の各ファイル（opcodes 単位）と対比して、**機能横断的な reference** をここに集約する。

## 構成

| ファイル | 内容 |
|---------|------|
| [aria-features.md](aria-features.md) | 画面遷移、文字送り音、ルビ、ビネット、フラッシュ、パーティクル、F3 デバッグ、テキスト配置 enum、VM エラー強化、GameState リファクタ、インストーラ、リリースビルド |
| [core-spec.md](core-spec.md) | Aria 言語の最小仕様（文法、データモデル、struct、register save range、control flow、drawing/UI、command responsibility、compatibility、input wait result、error policy、strict lint、command registration） |
| [background-time-filter.md](background-time-filter.md) | `bgtime` / `bgfade` / `bgtime_map` による背景の時間ベースフィルタ（evening / night / midnight の preset） |
| [effects-ux.md](effects-ux.md) | `transition` / `camera` / `screen` / `textfx` / `voice` / `fx profile` / `sync fx` のレイヤ構成、安全ルール、シナリオ別 usage |
| [localization.md](localization.md) | `language` / `loc_get` / `loc_format` / `readid`、locale manifest、scenario ファイル パターン、パッケージング注意事項 |

## 位置づけ

- **opcodes 単位**（`amsp` / `afade` / `set_flag` / `defchapter` など）のリファレンスは [reference/opcodes/](../opcodes/) 配下
- **機能横断的・統合的** なリファレンスは本フォルダ
- 旧 `docs/scripting/` 配下にあった一意コンテンツを移管（2026-06-01 時点）
- `docs/scripting/` フォルダ自体は 2026-06-01 に整理済（重複削除、移動完了）

## 関連

- [Aria v2 Strict 技術仕様書](../../spec/aria-v2-strict.md) — v2 strict 拡張仕様（`strict on` モード時の型・所有権・スコープ規則）
- [Reference: Opcodes](../opcodes/) — opcodes 単位のリファレンス
- [Reference: Syntax](../syntax.md) — 文法規則
- [Reference: UI](../ui/) — `button-feel.md` ほか
