# 最初のプロジェクト作成

このチュートリアルでは、AriaEngineを使用して最初のビジュアルノベルプロジェクトを作成する方法を説明します。

## ステップ1: 環境セットアップ

### .NET 8.0 SDKのインストール

AriaEngineは.NET 8.0を使用しています。まだインストールされていない場合は、[公式サイト](https://dotnet.microsoft.com/download/dotnet/8.0)からインストールしてください。

インストール後、以下のコマンドでバージョンを確認できます：

```bash
dotnet --version
```

`8.0.x`と表示されればOKです。

### プロジェクトのクローン

既存のプロジェクトがある場合は、GitHubからクローンします：

```bash
git clone https://github.com/your-username/aria-engine.git
cd aria-engine
```

### プロジェクトのビルド

プロジェクトをビルドします：

```bash
dotnet build
```

ビルドが成功すると、エラーメッセージが表示されず、ビルド成功のメッセージが表示されます。

## ステップ2: init.ariaの作成

`init.aria`ファイルは、エンジンの初期化設定を記述するファイルです。プロジェクトのルートディレクトリ（`src/AriaEngine/`）に作成します。

### 基本的なinit.aria

```aria
; ウィンドウ設定
window 1280, 720, "My Visual Novel"

; フォント設定
font "assets/fonts/NotoSansJP-Regular.ttf"
font_atlas_size 192
font_filter "bilinear"

; メインスクリプト設定
script "assets/scripts/main.aria"

; デバッグモード
debug on
```

### 各設定の説明

- `window 1280, 720, "My Visual Novel"`: ウィンドウサイズを1280x720に設定し、タイトルを「My Visual Novel」にします
- `font "assets/fonts/NotoSansJP-Regular.ttf"`: 日本語フォントを指定します
- `font_atlas_size 192`: フォントアトラスサイズを192pxに設定します（大きいほど高品質）
- `font_filter "bilinear"`: フォントフィルターをバイリニアに設定します（滑らかな表示）
- `script "assets/scripts/main.aria"`: メインスクリプトファイルを指定します
- `debug on`: デバッグモードを有効にします（F3キーでデバッグ情報表示）

## ステップ3: フォントファイルの準備

日本語を表示するには、日本語フォントが必要です。以下の手順でフォントを準備します：

### フォントのダウンロード

[Noto Sans JP](https://fonts.google.com/noto/specimen/Noto+Sans+JP)などの日本語フォントをダウンロードします。

### フォントの配置

ダウンロードしたフォントファイルを`src/AriaEngine/assets/fonts/`ディレクトリに配置します：

```
src/AriaEngine/
└── assets/
    └── fonts/
        └── NotoSansJP-Regular.ttf
```

## ステップ4: メインスクリプトの作成

`assets/scripts/main.aria`ファイルを作成し、最初のスクリプトを記述します。

### 最小限のスクリプト

```aria
*start
    ; 背景を設定
    bg "#1a1a2e", 2

    ; テキストをクリア
    textclear

    ; テキストを表示
    text "こんにちは、AriaEngineへようこそ！"
    wait 1000

    text "これはあなたの最初のビジュアルノベルです"
    wait 1000

    ; 終了
    end
```

### 実行とテスト

プロジェクトを実行します：

```bash
cd src/AriaEngine
dotnet run
```

ウィンドウが開き、テキストが表示されれば成功です！何かキーを押すと進みます。

## ステップ5: インタラクティブなスクリプト

次に、よりインタラクティブなスクリプトを作成します。

### 選択肢の追加

```aria
*start
    ; 背景設定
    bg "#1a1a2e", 2
    textclear

    ; イントロダクション
    ミオ「こんにちは、私はミオです」
    wait 1000

    ミオ「あなたの名前は何ですか？」
    wait 1000

    ; 名前入力（簡易版）
    let $player_name, "主人公"

    ; 選択肢
    text "どうしますか？"
    text "1: 自己紹介する"
    text "2: 黙っている"

    btnwait %choice

    if %choice == 1
        goto *introduce
    elseif %choice == 2
        goto *silent
    endif

*introduce
    主人公「はじめまして、${$player_name}です」
    ミオ「よろしくお願いします、${$player_name}さん」
    goto *continue

*silent
    ミオ「...静かなんですね」
    主人武「...」
    goto *continue

*continue
    ミオ「これから一緒に冒険しましょう」
    wait 1000

    text "（エンディング）"
    end
```

## ステップ6: 画像の追加

背景画像やキャラクター画像を追加して、ビジュアルを向上させます。

### 画像の準備

背景画像やキャラクター画像を用意し、以下のディレクトリに配置します：

```
src/AriaEngine/assets/
├── bg/
│   └── forest.png
└── ch/
    └── mio.png
```

### 画像を使用したスクリプト

```aria
*start
    ; 背景画像を表示
    lsp 10, "assets/bg/forest.png", 0, 0
    vsp 10, on

    ; フェードイン
    sp_alpha 10, 0
    afade 10, 255, 1000
    await

    ; キャラクターを表示
    lsp 20, "assets/ch/mio.png", 800, 0
    vsp 20, on

    ; 会話
    ミオ「ここは美しい森ですね」
    wait 1000

    ミオ「一緒に散歩しましょう」
    wait 1000

    ; 終了
    csp -1
    end
```

## ステップ7: ボタンの追加

ボタンを追加して、ユーザーインターフェースを作成します。

### ボタン付きスクリプト

```aria
*title_screen
    ; 背景
    bg "#1a1a2e", 2

    ; タイトルテキスト
    lsp_text 100, "私のゲーム", 640, 200
    sp_fontsize 100, 64
    sp_text_align 100, "center"
    sp_color 100, "#ffffff"

    ; スタートボタン
    lsp_rect 101, 440, 300, 400, 60
    sp_fill 101, "#2a2a3e", 255
    sp_round 101, 10
    sp_border 101, "#4a4a6e", 2
    sp_hover_color 101, "#3a3a5e"
    sp_isbutton 101, true
    spbtn 101, 1

    lsp_text 102, "スタート", 640, 315
    sp_text_align 102, "center"
    sp_color 102, "#ffffff"

    ; 終了ボタン
    lsp_rect 103, 440, 380, 400, 60
    sp_fill 103, "#2a2a3e", 255
    sp_round 103, 10
    sp_border 103, "#4a4a6e", 2
    sp_hover_color 103, "#3a3a5e"
    sp_isbutton 103, true
    spbtn 103, 2

    lsp_text 104, "終了", 640, 395
    sp_text_align 104, "center"
    sp_color 104, "#ffffff"

    ; ボタン待機
    btnwait %result

    if %result == 1
        goto *game_start
    elseif %result == 2
        end
    endif

*game_start
    csp -1
    text "ゲーム開始！"
    end
```

## ステップ8: アニメーションの追加

アニメーションを追加して、より動的な演出を行います。

### アニメーション付きスクリプト

```aria
*start
    ; 背景
    bg "#1a1a2e", 2

    ; キャラクター登場アニメーション
    lsp 10, "assets/ch/mio.png", 1400, 0  ; 画面外に配置
    vsp 10, on

    ; スライドイン
    amsp 10, 800, 0, 1000
    ease 10, "easeout"
    await

    ; 会話
    ミオ「こんにちは！」

    ; 退場アニメーション
    amsp 10, 1400, 0, 1000
    ease 10, "easein"
    await

    csp 10
    text "さようなら"
    end
```

## ステップ9: セーブ/ロード機能

セーブ/ロード機能を追加します。

### セーブ/ロード付きスクリプト

```aria
*start
    ; ゲーム開始フラグ
    set_flag "game_started", 1

    ; スコア初期化
    set_counter "score", 0

    ; イベント1
    text "イベント1"
    inc_counter "score", 10

    ; セーブポイント
    save 1

    ; イベント2
    text "イベント2"
    inc_counter "score", 20

    ; スコア表示
    get_counter "score", %current_score
    text "現在のスコア: ${%current_score}点"

    ; 終了
    end
```

## トラブルシューティング

### エラー: フォントファイルが見つからない

**問題**: `Font file not found` エラーが発生する

**解決策**:
1. フォントファイルが正しいパスにあるか確認
2. `init.aria`のフォントパスを確認
3. ファイル名の大文字小文字を確認

### エラー: スクリプトファイルが見つからない

**問題**: `Script file not found` エラーが発生する

**解決策**:
1. `assets/scripts/`ディレクトリにスクリプトファイルがあるか確認
2. `init.aria`のスクリプトパスを確認
3. ファイル名の拡張子を確認（`.aria`）

### ウィンドウが表示されない

**問題**: 実行してもウィンドウが表示されない

**解決策**:
1. `init.aria`ファイルが存在するか確認
2. フォントファイルが存在するか確認
3. コマンドラインでエラーメッセージを確認

### テキストが表示されない

**問題**: ウィンドウは表示されるがテキストが表示されない

**解決策**:
1. フォントサイズを確認（大きすぎると表示されない可能性）
2. テキスト色を確認（背景色と同じでないか）
3. `textclear`が適切に呼ばれているか確認

## 次のステップ

最初のプロジェクトが完成したら、次のチュートリアルに進みましょう：

- [UI作成](creating-ui.md) - スタイリッシュなUIの作成方法
- [チャプターシステム](chapter-system.md) - チャプター管理の実装
- [セーブ/ロード実装](save-load.md) - 完全なセーブ/ロードシステム

## まとめ

このチュートリアルでは、以下のことを学びました：

1. .NET 8.0 SDKのインストール
2. プロジェクトのビルドと実行
3. `init.aria`の作成と設定
4. フォントの準備
5. 基本的なスクリプトの作成
6. インタラクティブなスクリプトの作成
7. 画像の追加
8. ボタンの追加
9. アニメーションの追加
10. セーブ/ロード機能の追加

これでAriaEngineの基本をマスターしました！次はより高度な機能に挑戦しましょう。
