# AriaEngine Documentation | AriaEngine ドキュメント

<!-- Japanese primary, English secondary -->

AriaEngineビジュアルノベルエンジンのドキュメントへようこそ！
Welcome to the AriaEngine documentation hub!

---

## 何をお探しですか？ | What are you looking for?

### 🎓 初めての方へ | First time?

>AriaEngine使ったことがなく、、まずは概要を知りたい
>You want to learn what AriaEngine is and how to get started

→ [README.md](../README.md) または [tutorials/getting-started.md](tutorials/getting-started.md)

---

### 📖 4つのドキュメントタイプ | Four Documentation Types

| Quadrant | 日本語 | English | ドキュメント | Doc |
|---------|--------|---------|------------|-----|
| **Tutorials** | 📚 チュートリアル | 📚 Tutorials | 学習向き | Learning-oriented |
| **How-To Guides** | 🔧 使い方ガイド | 🔧 How-To Guides | 問題解決向き | Problem-oriented |
| **Reference** | 📋 リファレンス | 📋 Reference | 情報参照向き | Information-oriented |
| **Architecture** | 💡 設計資料 | 💡 Architecture | 理解促進向き | Understanding-oriented |

---

### 🚀 迷ったら | Not sure where to start?

```
初めてですか？     → tutorials/getting-started.md へ
スクリプトの書き方 → how-to-guides/ へ
コマンドの詳細     → reference/ へ
仕組みを理解したい → architecture/ へ
AI agentで拡張     → ai-agent/ へ
```

---

## 📚 ドキュメント構成 | Documentation Structure

### 🎓 チュートリアル | Tutorials
学習向き | Learning-oriented

- [最初プロジェクト作成](tutorials/getting-started.md) — はじめての方へ
- [UI作成](tutorials/creating-ui.md) — タイトル画面・ボタン
- [チャプターシステム](tutorials/chapter-system.md) — 場面管理
- [セーブ/ロード](tutorials/save-load.md) — データ保存

📍 パス: `docs/tutorials/`

### 🔧 使い方ガイド | How-To Guides
問題解決向き | Problem-oriented

- [リリースビルドの作成](how-to-guides/compile-and-package.md)
- [カスタムフォント](how-to-guides/custom-fonts.md)
- [デバッグモード](how-to-guides/debug-mode.md)
- [トラブルシューティング](how-to-guides/troubleshooting.md)

📍 パス: `docs/how-to-guides/`

### 📋 リファレンス | Reference
情報参照向き | Information-oriented

- [オペコード一覧](reference/opcodes/) — 全コマンド
- [`textbox_align` 詳細](reference/opcodes/textbox_align.md) — ADV テキストの垂直配置
- [`ButtonFeel` 詳細](reference/ui/button-feel.md) — ボタンの押下感
- [スクリプト機能 reference](reference/scripting/) — 機能横断的 reference (画面遷移 / FX / バックグラウンド時刻フィルタ / 多言語 など)
- [スクリプト構文](reference/syntax.md) — 文法規則
- [設定](reference/config.md) — config.json
- [init.aria](reference/init-aria.md) — 初期化スクリプト

📍 パス: `docs/reference/`

### 💡 設計資料 | Architecture
理解促進向き | Understanding-oriented

**Core (コア設計)**
- [言語理念](architecture/language-philosophy.md) — 設計思想
- [概要](architecture/overview.md) — エンジン構成と責務分担
- [VM](architecture/vm.md) — 仮想マシン
- [Parser](architecture/parser.md) — 解析処理

**Pipeline (パイプライン詳細)**
- [Scripting pipeline](architecture/scripting-pipeline.md) — スクリプト解析 → コンパイル → 実行
- [Rendering](architecture/rendering.md) — 描画パイプライン
- [Text subsystem](architecture/text-subsystem.md) — テキスト描画と表示制御

**Distribution (配布・ビルド)**
- [Platform](architecture/platform.md) — `.pak` / `.ariac` / dev-release モード
- [Tools](architecture/tools.md) — `doctor` / `package` / `installer` の内部設計

📍 パス: `docs/architecture/`

### 📋 仕様書 | Specifications
技術仕様 | Technical specifications

- [Aria v2 Strict 技術仕様書](spec/aria-v2-strict.md) — v2 strict言語拡張の完全仕様

📍 パス: `docs/spec/`

### 🤖 AI Agent向け | AI Agent Guide
AI agent 向け | AI agent-oriented

- [AGENTS.md](ai-agent/AGENTS.md)
- [CODEBASE.md](ai-agent/CODEBASE.md)

📍 パス: `docs/ai-agent/`

---

## ⚡ クイックリファレンス | Quick Reference

最も使うコマンド5つ | Top 5 most common commands:

| コマンド | Command | 説明 | Description |
|---------|---------|------|-------------|
| `text` | text | テキスト表示 | Display text |
| `wait` | wait | 待機 | Wait/delay |
| `lsp` | lsp | スプライト読込 | Load sprite |
| `msp` | msp | スプライト移動 | Move sprite |
| `if` | if | 条件分岐 | Conditional |

その他のコマンドは [reference/opcodes/](reference/opcodes/) を参照。

---

## 🆕 UX Quick Wins (T1/T2/T3) まとめ

v2.0.0-rc.1 で導入された UX 改善の主要機能。対応するドキュメントにリンクしています。

| 機能 | 概要 | ドキュメント |
|------|------|-------------|
| **T1: セーブサムネイル** | セーブメニュー open 時にゲーム画面をキャプチャ → ゲーム本編の画像が記録される | [tutorials/save-load.md](tutorials/save-load.md) (ステップ6) / [reference/opcodes/system.md](reference/opcodes/system.md) |
| **T2: ボタンの押下感** | `theme "soft"` などで全ボタンに押下アニメーション (`ButtonFeel`) を自動適用 | [reference/ui/button-feel.md](reference/ui/button-feel.md) / [tutorials/creating-ui.md](tutorials/creating-ui.md) (ステップ6) |
| **T3: ADV テキスト垂直配置** | `textbox_align center` / `top` / `bottom` でテキストボックスの垂直位置を制御 | [reference/opcodes/textbox_align.md](reference/opcodes/textbox_align.md) / [tutorials/creating-ui.md](tutorials/creating-ui.md) |

トラブルシューティングは [how-to-guides/troubleshooting.md](how-to-guides/troubleshooting.md) (セクション 7-9) を参照。

---

## 🔗 リンク | Links

- [プロジェクトREADME](../README.md)
- [GitHub](https://github.com/mirinnano/aria-engine)
- [イシュー報告](https://github.com/mirinnano/aria-engine/issues)
