# AriaEngine

AriaEngineは.NET 8.0とRaylibを使用した、モダンなビジュアルノベルゲームエンジンです。NScripter互換の独自スクリプト言語(.aria)をサポートし、スクリプト主導の開発体験を提供します。

## 特徴

- **NScripter互換のスクリプト言語**: `.aria`ファイルでゲーム全体を記述
- **63種類のオペコード**: テキスト表示、スプライト操作、アニメーション、オーディオ、UIなど
- **スクリプト主導の設計**: チャプター、キャラクター、フラグ管理をすべてスクリプト内で定義可能
- **モダンなUIコンポーネント**: スタイリッシュなボタン、カード、アニメーション効果
- **高品質なフォントレンダリング**: 動的アンチエイリアス、高解像度フォントアトラス
- **完全なゲームフロー管理**: チャプター選択、セーブ/ロード、キャラクターシステム
- **軽量で高速**: .NET 8.0とRaylibによる効率的なレンダリング

## クイックスタート

### ビルド

```bash
dotnet build
```

### 実行

```bash
cd src/AriaEngine
dotnet run
```

### 販売向けコンパイル/パック

```bash
cd src/AriaEngine
set ARIA_PACK_KEY=YOUR_KEY
dotnet run -- aria-compile --init init.aria --main assets/scripts/main.aria --out build/scripts.ariac --key YOUR_KEY
dotnet run -- aria-pack build --input assets --compiled build/scripts.ariac --output build/data.pak --key YOUR_KEY
```

`--key` を省略した場合は環境変数 `ARIA_PACK_KEY` が自動使用されます。

### CI/CD

GitHub Actions ワークフロー: `.github/workflows/aria-cicd.yml`  
`ARIA_PACK_KEY` を Repository Secrets に設定すると、自動で読み込んで
`publish -> aria-compile -> aria-pack -> artifact upload` まで実行します。

ローカルで同等手順を実行する場合:

```bash
powershell -ExecutionPolicy Bypass -File scripts/cicd.ps1
```

### 実行モード

```bash
# 開発モード（既定）: 平文 .aria を実行
dotnet run -- --run-mode dev --init init.aria

# 販売モード: data.pak + 暗号化済み scripts.ariac を必須化
dotnet run -- --run-mode release --pak build/data.pak --compiled scripts/scripts.ariac --key YOUR_KEY --init init.aria
```

## インストール方法

### 必要条件

- **.NET 8.0 SDK**: [公式サイト](https://dotnet.microsoft.com/download/dotnet/8.0)からインストール
- **Raylib-cs**: NuGetパッケージとして自動的にインストールされます

### 初期設定

1. プロジェクトのルートディレクトリで`init.aria`ファイルを作成:

```aria
window 1280, 720, "My Visual Novel"
font "path/to/your/font.ttf"
font_atlas_size 192
font_filter "bilinear"
script "assets/scripts/main.aria"
debug on
```

2. `assets/scripts/main.aria`ファイルを作成:

```aria
*title_start
bg "#1a1a2e", 2
textclear

ミオ「こんにちは、AriaEngineへようこそ！」
wait 1000

主人公「これはとても素晴らしいエンジンですね」
wait 1000

end
```

3. 実行:

```bash
cd src/AriaEngine
dotnet run
```

## 基本的な使い方

### スクリプトの基本構造

```aria
; ラベル定義
*label_name
    text "テキストを表示"
    wait 1000
    goto *another_label

*another_label
    ; ここに処理を記述
```

### テキスト表示

```aria
; テキストコマンド
text "キャラクター「会話文」"

; インラインテキスト
キャラクター「会話文」
```

### 変数の使用

```aria
; 整数変数 (%0-%9)
let %0, 0
inc %0
if %0 == 10 goto *label_name

; 文字列変数 ($name)
let $player_name, "主人公"
text "${$player_name}「こんにちは」"
```

### スプライトの表示

```aria
; 背景スプライトの表示
lsp 10, "assets/bg/forest.png", 0, 0

; キャラクターの表示
lsp 20, "assets/ch/hero.png", 800, 100

; スプライトのプロパティ設定
sp_alpha 10, 255
sp_scale 10, 1.0
```

### ボタンの作成

```aria
; ボタンの作成
lsp_rect 100, 400, 300, 200, 50
sp_fill 100, "#2a2a3e", 255
sp_round 100, 10
sp_border 100, "#4a4a6e", 2
sp_hover_color 100, "#3a3a5e"
spbtn 100, 1

lsp_text 101, "クリック", 500, 315
sp_text_align 101, "center"

btnwait %result
if %result == 1
    text "ボタンがクリックされました"
endif
```

## スクリプト言語の基本

### ラベルとサブルーチン

```aria
*main
    gosub *subroutine
    end

*subroutine
    text "サブルーチンが呼ばれました"
    return
```

### 条件分岐

```aria
let %health, 100
if %health > 50
    text "HPは50以上です"
else
    text "HPは50以下です"
endif
```

### ループ

```aria
for %i = 0 to 10
    text "${%i}回目の処理"
next
```

### フラグ管理

```aria
set_flag "game_cleared", 0
get_flag "game_cleared", %cleared
if %cleared == 1
    text "ゲームクリア済み"
endif
```

## プロジェクト構成

```
engine/
├── src/AriaEngine/
│   ├── Core/           # 仮想マシン、パーサー、状態管理
│   ├── Rendering/      # スプライト描画、トランジション、アニメーション
│   ├── Input/          # 入力処理
│   ├── Audio/          # オーディオ管理
│   ├── assets/         # ゲームアセット
│   │   ├── fonts/
│   │   ├── bg/
│   │   ├── ch/
│   │   └── scripts/
│   ├── init.aria       # エンジン初期化スクリプト
│   └── Program.cs      # エントリーポイント
└── docs/              # ドキュメント
```

## 機能一覧

### ビジュアルシステム
- テキスト表示と整形
- 自動テキスト送り
- テキスト履歴機能

### スプライトシステム
- 画像、テキスト、矩形スプライト
- Zオーダー管理
- アニメーションとトランジション
- スタイリッシュな装飾効果（影、枠線、角丸）

### アニメーション
- Tweenアニメーション（位置、透明度、スケール）
- イージング関数
- 並列/連続アニメーション

### UIシステム
- ボタンとメニュー
- テキストボックス
- ホバーエフェクト
- レスポンシブなレイアウト

### オーディオシステム
- BGM/SEの再生と制御
- フェードイン/アウト
- 音量制御

### ゲームフロー管理
- チャプター選択システム
- セーブ/ロード機能
- キャラクター管理
- シーン遷移

### 高度な機能
- フラグとカウンター管理
- サブルーチン
- 条件分岐とループ
- 右クリックメニュー
- デバッグモード

## ドキュメント

詳細なドキュメントは[docs/](docs/)ディレクトリを参照してください：

- **[スクリプト言語リファレンス](docs/scripting/)**: 基本構文、制御構造、スプライト操作
- **[APIドキュメント](docs/api/)**: 全オペコードの詳細リファレンス
- **[チュートリアル](docs/tutorials/)**: 最初のプロジェクト作成、UI作成、チャプターシステム
- **[アーキテクチャ](docs/architecture/)**: VM、パーサー、レンダリングの技術詳細

## ライセンス

このプロジェクトはMITライセンスの下で公開されています。

## コントリビューション

コントリビューションを歓迎します！以下の手順で参加できます：

1. リポジトリをフォーク
2. ブランチを作成 (`git checkout -b feature/amazing-feature`)
3. 変更をコミット (`git commit -m 'Add amazing feature'`)
4. ブランチをプッシュ (`git push origin feature/amazing-feature`)
5. プルリクエストを作成

## 技術スタック

- **.NET 8.0**
- **Raylib-cs 7.0.2**
- **C#**

## 謝辞

AriaEngineの開発に貢献してくださったすべての方々に感謝します。
