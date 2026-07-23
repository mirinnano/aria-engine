# AriaEngine Documentation | AriaEngine ドキュメント

<!-- Japanese primary, English secondary -->

AriaEngineビジュアルノベルエンジンのドキュメントへようこそ！
Welcome to the AriaEngine documentation hub!

新規プロジェクトの作者言語は、モードなしの単一構文 `aria;` です。
[Aria 言語仕様](spec/aria.md)を唯一の正とします。Rust の可変性・所有権・借用を採り入れた
物語言語であり、旧 C# コマンド一覧、`strict`、言語バージョン指定、互換モードは
新規作品に使用しません。React/Tauri の presentation 境界、ARIAC7/PAK4、同梱フォント、
Player 包装は [ランタイム・ファイル形式](spec/aria-v3-runtime.md) に集約しています。

> **履歴資料について**: `aria-v2-strict.md`、`aria-3.1.md`、`aria-3.2.md`、旧 opcode
> reference と C# 向けチュートリアルは、移行前の記録です。現行コンパイラはそこにある
> ヘッダーや命令を受け付けません。

---

## 何をお探しですか？ | What are you looking for?

### 🎓 初めての方へ | First time?

>AriaEngine使ったことがなく、、まずは概要を知りたい
>You want to learn what AriaEngine is and how to get started

→ [README.md](../README.md) → [Aria 言語仕様](spec/aria.md)

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
初めてですか？     → ../README.md → spec/aria.md へ
スクリプトの書き方 → spec/aria.md へ
コマンドの詳細     → spec/aria-v3-runtime.md へ
仕組みを理解したい → architecture/language-philosophy.md へ
AI agentで拡張     → ai-agent/ へ
```

---

## 📚 ドキュメント構成 | Documentation Structure

### 🎓 歴史的 C# チュートリアル | Historical C# Tutorials

> 下記は現行 `aria;` の入門ではありません。C# runtime の資料を参照する場合だけ使用してください。

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

- [Aria 言語仕様](spec/aria.md) — 現行構文・型・所有権・借用
- [旧オペコード一覧](reference/opcodes/) — 履歴資料。新規作品には使わない
- [旧スクリプト構文](reference/syntax.md) — 履歴資料
- [旧 opcode / UI reference](reference/opcodes/) — 履歴資料

📍 パス: `docs/reference/`

### 💡 設計資料 | Architecture
理解促進向き | Understanding-oriented

**Core (コア設計)**
- [言語理念](architecture/language-philosophy.md) — 単一言語と所有権の設計判断
- [Semantic-runtime architecture](architecture/v3-native-first.md) — Rust Core、Native/Web adapter、配布境界
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

- [Aria 言語仕様](spec/aria.md) — **現行**。`aria;`、型、所有権、借用、診断
- [Aria runtime/file-format contract](spec/aria-v3-runtime.md) — ARIAC7、manifest、bytecode、save、pak
- [履歴: Aria v2 Strict](spec/aria-v2-strict.md)
- [履歴: Aria 3.1 author language](spec/aria-3.1.md)
- [履歴: Aria 3.2 presentation contract](spec/aria-3.2.md)
- [PAK4 distribution and license contract](spec/pak4.md) — profiles、role、chunk保護、License Provider

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
| `say` / `narrate` | dialogue | テキスト表示 | Display text |
| `await advance` | advance | 次送り待機 | Wait for advance |
| `let mut n = show ...` | Node | 所有 Node を生成 | Create owned Node |
| `move &mut n to ...` | borrow | 明示借用で移動 | Move through explicit borrow |
| `if` | conditional | 条件分岐 | Conditional |

その他の現行構文は [Aria 言語仕様](spec/aria.md) を参照。旧 opcode 一覧は新規作品の
リファレンスではありません。

---

## 履歴: UX Quick Wins (T1/T2/T3)

> ここは旧 C# runtime の記録です。現行 UI は project-owned presentation package が実装します。

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
