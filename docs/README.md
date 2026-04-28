# AriaEngine ドキュメント

AriaEngineのドキュメントへようこそ！このドキュメントでは、エンジンの使用方法からアーキテクチャまで、開発に必要な情報を網羅しています。

## ドキュメント構成

### 📚 入門ガイド
- [README.md](../README.md) - プロジェクトの概要とクイックスタート

### 💻 スクリプト言語リファレンス ([scripting/](scripting/))
- [コア仕様（ドラフト）](scripting/core-spec.md) - 命令責務、文法方針、副作用ルール
- [基本構文](scripting/basics.md) - テキスト表示、変数、ラベル、サブルーチン
- [制御構造](scripting/control-flow.md) - 条件分岐、ループ、ジャンプ
- [スプライト操作](scripting/sprites.md) - スプライト作成、操作、プロパティ設定
- [アニメーション](scripting/animations.md) - Tweenアニメーション、イージング、待機
- [UI要素](scripting/ui-elements.md) - ボタン、メニュー、テキストボックス
- [高度な機能](scripting/advanced.md) - チャプター管理、フラグシステム、キャラクター管理

### 🔧 APIドキュメント ([api/](api/))
- [オペコードリファレンス](api/opcodes.md) - 全63種類のオペコードの詳細説明

### 🎓 チュートリアル ([tutorials/](tutorials/))
- [最初のプロジェクト作成](tutorials/getting-started.md) - 環境セットアップから最初のスクリプトまで
- [UI作成](tutorials/creating-ui.md) - タイトル画面、ボタン、ホバーエフェクト
- [チャプターシステム](tutorials/chapter-system.md) - チャプター定義、選択画面、アンロック機能
- [セーブ/ロード実装](tutorials/save-load.md) - セーブデータ構造、セーブポイント、ロード画面

### 🏗️ アーキテクチャ ([architecture/](architecture/))
- [言語理念](architecture/language-philosophy.md) - Ariaの設計原則と非目標
- [概要](architecture/overview.md) - エンジンの全体構成と主要コンポーネント
- [仮想マシン](architecture/vm.md) - VMの仕組み、プログラムカウンタ、コールスタック
- [パーサー](architecture/parser.md) - トークン化、命令生成、エラー処理
- [レンダリング](architecture/rendering.md) - スプライト描画、Zオーダー、トランジション

### 🛠️ 開発運用 ([development/](development/))
- [Git/GitHub Workflow](development/git-github.md) - 差分確認、検証、PR作成の定型手順

## クイックリファレンス

### よく使うコマンド

| コマンド | 説明 | 使用例 |
|---------|------|--------|
| `text` | テキスト表示 | `text "こんにちは"` |
| `wait` | 待機 | `wait 1000` |
| `lsp` | スプライトロード | `lsp 10, "bg.png", 0, 0` |
| `msp` | スプライト移動 | `msp 10, 100, 200, 500` |
| `if` | 条件分岐 | `if %0 == 1 goto *label` |
| `for` | ループ | `for %i = 0 to 10` |

### プロジェクトの開始方法

```bash
# ビルド
dotnet build

# 実行
cd src/AriaEngine
dotnet run
```

### スクリプトの基本構造

```aria
*label_name
    text "テキスト"
    wait 1000
    goto *another_label
```

## トラブルシューティング

### ビルドエラー
- `.NET 8.0 SDK`がインストールされているか確認
- `dotnet --version` でバージョンを確認

### 実行時エラー
- `init.aria` ファイルが存在するか確認
- フォントファイルパスが正しいか確認
- スクリプトファイルの構文エラーがないか確認

### スプライトが表示されない
- `vsp` コマンドで表示状態を確認
- `sp_alpha` コマンドで透明度を確認
- Zオーダーを確認（重なっている可能性）

## 貢献

ドキュメントの改善や修正を歓迎します！以下の手順で貢献できます：

1. ドキュメントを修正・追加
2. 差分を確認 (`.\scripts\git-report.ps1 -IncludeDiffStat`)
3. 変更をコミット (`git commit -m 'docs: Improve documentation'`)
4. ブランチをプッシュ (`git push origin feature/docs-improvement`)
5. プルリクエストを作成

## リンク

- [GitHubリポジトリ](https://github.com/mirinnano/aria-engine)
- [問題報告](https://github.com/mirinnano/aria-engine/issues)
- [ディスカッション](https://github.com/mirinnano/aria-engine/discussions)
