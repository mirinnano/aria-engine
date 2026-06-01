# はじめてのプロジェクト

このチュートリアルでは、AriaEngineで最初のビジュアルノベルを動かすまでの手順を順番に説明します。前提知識は不要です。各ステップをそのまま実行していけば、最後にはゲームが動きます。

---

## ステップ1: .NET 8.0 SDKをインストールする

AriaEngineは.NET 8.0で動作します。まだインストールされていない場合は、[.NETの公式サイト](https://dotnet.microsoft.com/download/dotnet/8.0)からSDKをダウンロードしてください。

インストール後、ターミナルでバージョンを確認します。

```bash
dotnet --version
```

`8.0.x` と表示されればOKです。

---

## ステップ2: プロジェクトをクローンしてビルドする

GitHubからプロジェクトを取得し、ビルドします。

```bash
git clone https://github.com/mirinnano/aria-engine.git
cd aria-engine
dotnet build
```

ビルドが成功すると、`ビルド succeeded` のようなメッセージが表示されます。

---

## ステップ3: init.ariaを作成する

`init.aria` はエンジン起動時に最初に読み込まれる設定ファイルです。ウィンドウサイズやフォント、メインスクリプトの場所を指定します。

`src/AriaEngine/` ディレクトリに `init.aria` を新規作成してください。

```aria
; ウィンドウ設定（幅, 高さ, タイトル）
window 1280, 720, "はじめてのビジュアルノベル"

; フォント設定
font "assets/fonts/NotoSansJP-Regular.ttf"
font_atlas_size 256

; メインスクリプトのパス
script "assets/scripts/main.aria"

; NScripter互換モード（テキストボックスを自動生成する）
compat_mode on

; デバッグモード（F3キーで表示切替）
debug on

; テキストボックスの位置とサイズ（x, y, 幅, 高さ）
textbox 50, 500, 1180, 200

; 文字の大きさと色
fontsize 32
textcolor "#ffffff"
```

各設定の意味は以下の通りです。

| 設定 | 意味 |
|------|------|
| `window` | ゲームウィンドウのサイズとタイトル |
| `font` | 使用するフォントファイルのパス |
| `font_atlas_size` | フォントの品質（大きいほどきれい） |
| `script` | ゲームの本体スクリプトファイルのパス |
| `compat_mode` | `on` にするとテキストボックスが自動で作られる |
| `debug` | `on` にするとデバッグ情報が表示される |
| `textbox` | テキスト表示領域の位置とサイズ |
| `fontsize` | デフォルトの文字サイズ |
| `textcolor` | デフォルトの文字色（16進カラー） |

詳細は [init.ariaリファレンス](../reference/init-aria.md) を参照してください。

### フォントファイルの準備

日本語を表示するには、日本語対応のTTFフォントが必要です。ここでは [Noto Sans JP](https://fonts.google.com/noto/specimen/Noto+Sans+JP) を例にします。

ダウンロードしたフォントファイルを `src/AriaEngine/assets/fonts/` に配置してください。

```
src/AriaEngine/
└── assets/
    └── fonts/
        └── NotoSansJP-Regular.ttf
```

---

## ステップ4: main.ariaを作成する

メインスクリプトにゲームの内容を書きます。

`src/AriaEngine/assets/scripts/` ディレクトリを作成し、その中に `main.aria` を新規作成してください。

```aria
*start
    ; 背景を黒に設定
    bg "#0f0f1a"

    ; テキストをクリア
    textclear

    ; テキストを表示
    text "ようこそ、AriaEngineへ！"
    wait

    text "これはあなたの最初のスクリプトです。"
    wait

    ; キャラクター名付きのセリフ（自動的にクリック待機＋クリアが入る）
    ミオ「こんにちは！私はミオです。」

    ミオ「一緒にゲームを作っていきましょう。」

    ; ゲーム終了
    end
```

ここで使っているコマンドの概要です。

| コマンド | 動作 |
|---------|------|
| `*start` | ラベル（ここから実行が始まる） |
| `bg` | 背景画像または背景色を設定 |
| `textclear` | テキスト表示領域をクリア |
| `text` | テキストを表示 |
| `wait` | クリック待機（引数なし） |
| `名前「セリフ」` | キャラクター名付きテキスト（自動でクリック待機） |
| `end` | ゲームを終了する |

`wait` は引数なしで使うと「クリックされるまで待つ」になります。テキストを読んだらマウスクリックまたはEnterキーで次に進みます。

構文の詳細は [スクリプト構文リファレンス](../reference/syntax.md) を参照してください。

---

## ステップ5: 実行してみる

エンジンを起動します。

```bash
cd src/AriaEngine
dotnet run
```

ウィンドウが開き、テキストが表示されたら成功です。クリックまたはEnterキーで次のテキストへ進みます。最後まで読むとウィンドウが閉じます。

うまく動かない場合は、以下を確認してください。

- `init.aria` が `src/AriaEngine/` にあるか
- フォントファイルのパスが正しいか
- `main.aria` が `src/AriaEngine/assets/scripts/` にあるか

---

## ステップ6: 次のステップ

基本が動いたら、次のチュートリアルでUIの作り方を学びましょう。

- [UI作成](creating-ui.md) — タイトル画面とボタンの作り方
- [チャプターシステム](chapter-system.md) — 章管理と選択画面
- [セーブ/ロード](save-load.md) — データ保存と復元

より詳しいコマンドの一覧は [オペコードリファレンス](../reference/opcodes/) を参照してください。

---

## 補足: v2 strict モード（モダンな書き方）

上記は v1.x 互換モード（`compat_mode on`）を使った従来スタイルの例です。AriaEngine v2.0 では、**型安全性・寿命管理・構造化** を有効にする v2 strict モードも利用できます。`init.aria` の先頭で `strict on` を宣言し、スクリプトも `# aria-version: 2.0` プラグマで宣言します。

```aria
# aria-version: 2.0
strict on

*start
    textclear
    func greet(name: string) -> void
        text "こんにちは、${name}さん！"
    endfunc

    greet("ミオ")
```

| 機能 | v1.x 互換（`compat_mode on`） | v2 strict（`strict on`） |
|------|----------------------------|------------------------|
| 型チェック | なし | あり（int / string / sprite / flag） |
| 変数寿命 | 手動 | `scope` / `owned` / `borrow` で自動管理 |
| 関数 | `defsub` のみ | `func` / `endfunc` で構造化 |
| 解析 | 実行時 | 静的（`aria-lint` で事前検出） |

**初心者への推奨**: まずは v1.x 互換で始めて、慣れてきたら v2 strict に移行するのがスムーズです。両モードは共存可能で、同じプロジェクト内に混在させられます。

詳細は [Aria v2 Strict 仕様書](../spec/aria-v2-strict.md) を参照してください。

---

## 補足: UX Quick Wins 機能

AriaEngine v1.0 では以下の UX 改善が利用可能です。すべて `init.aria` またはスクリプトから有効化できます。

| 機能 | コマンド | 説明 |
|------|---------|------|
| セーブサムネイル改善（T1） | `save` コマンドが自動でゲーム本編をキャプチャ | セーブメニュー UI ではなくゲーム画面がサムネイルに保存 |
| ボタン押下感（T2） | `ui_theme "soft"` などでテーマ切替 | ボタンにホバー / 押下時のアニメーション |
| ADV テキスト中央配置（T3） | `textbox_align middle` | 画面下端だけでなく中央にも配置可能 |

詳細は [init.aria リファレンス](../reference/init-aria.md) と [オペコードリファレンス](../reference/opcodes/) を参照してください。
