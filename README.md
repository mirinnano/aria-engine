# AriaEngine

.NET 8.0 + Raylibで動くビジュアルノベルゲームエンジン。NScripter互換の`.aria`スクリプト言語をサポート。

## 特徴

- **スクリプト駆動**: `.aria`ファイルでゲーム全体を記述
- **NScripter互換**: テキスト、スプライト、アニメーション、オーディオ、UIを61種類のオペコードで制御
- **モダーンなUI**: ボタン、ホバーエフェクト、トランジション
- **高效的レンダリング**: .NET 8.0 + Raylibによる軽量高速な描画

## クイックスタート

```bash
dotnet build
cd src/AriaEngine && dotnet run
```

## プロジェクト構成

```
engine/
├── src/AriaEngine/     # エンジン本体
│   ├── Core/           # VM、パーサー、状態管理
│   ├── Rendering/      # スプライト描画、アニメーション
│   ├── Input/          # 入力処理
│   ├── Audio/          # オーディオ管理
│   └── assets/         # フォント、背景、キャラクタースクリプト
├── docs/               # ドキュメント
└── init.aria           # エンジン初期化設定
```

## スクリプト例

```aria
*start
    bg "forest.png", 0
    textclear
    ミオ「ようこそ！」

    ; ボタン作成
    lsp_rect 100, 400, 300, 200, 50
    spbtn 100, 1
    btnwait %result

    if %result == 1
        text "クリックされました"
    endif
```

## ドキュメント

- [ドキュメント一覧](docs/README.md) - チュートリアル、リファレンス、ガイド
- [スクリプト言語リファレンス](docs/reference/opcodes/) - 全オペコード詳細

## AIエージェント向け

AI agent開発者はこちらから:
- [AGENTS.md](docs/ai-agent/AGENTS.md) - プロジェクト構造、コードパターン、貢献方法

## ライセンス

MIT License

## コントリビューション

フォーク→ブランチ作成→コミット→PR。欢迎贡献！