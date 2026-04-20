# スクリプト言語の基本構文

このドキュメントでは、AriaEngineのスクリプト言語(.aria)の基本構文について説明します。

## テキスト表示

### textコマンド

`text`コマンドはテキストボックスにテキストを表示します。

```aria
text "こんにちは、世界！"
```

### インラインテキスト

会話文を簡潔に記述するために、キャラクター名とテキストを組み合わせた構文も使用できます。

```aria
主人公「こんにちは、世界！」
ミオ「よろしくお願いします！」
```

この形式を使用すると、自動的に以下の処理が行われます：
1. 前のテキストボックスをクリア
2. 新しいテキストを表示
3. ページ送り待機（クリック待ち）

### ページ制御

- `\`: ページをクリアして次のページへ
- `@`: ページ送り待機（クリック待ち）

```aria
text "これは1ページ目のテキストです。"
wait 1000
text "2ページ目に移動します。\"
text "クリックで次のページへ進みます。\"  ; ここでページクリア
text "3ページ目です。"
```

## 変数

### 整数変数 (%0-%9)

`%0`から`%9`までの10個の整数レジスタが使用できます。

```aria
let %0, 0          ; 変数の初期化
inc %0           ; %0を1増やす
dec %0           ; %0を1減らす
add %0, 10       ; %0に10を加算
sub %0, 5        ; %0から5を減算
mul %0, 2        ; %0を2倍にする
div %0, 3        ; %0を3で割る
```

### 文字列変数 ($name)

文字列変数を定義して使用できます。

```aria
let $player_name, "主人公"
text "${$player_name}「こんにちは」"
```

### 変数の使用例

```aria
let %score, 100
text "現在のスコア: ${%score}点"
```

## ラベルとサブルーチン

### ラベル定義

`*`で始まる行はラベルとして認識されます。

```aria
*main
    text "メインルーチン"
    gosub *subroutine
    end

*subroutine
    text "サブルーチンが呼ばれました"
    return
```

### サブルーチン定義

`defsub`コマンドでサブルーチンを定義できます。

```aria
defsub show_message
    text "これはサブルーチンです"
    return

*main
    show_message
    end
```

### サブルーチンの呼び出し

`gosub`コマンドでサブルーチンを呼び出し、`return`で戻ります。

```aria
*main
    gosub *setup
    gosub *game_loop
    end

*setup
    text "初期設定を行います"
    return

*game_loop
    text "ゲームループ"
    return
```

### パラメータ付きサブルーチン

`getparam`コマンドでパラメータを受け取れます。

```aria
defsub show_message
    getparam $name, $text
    text "${$name}「${$text}」"
    return

*main
    show_message "主人公", "こんにちは"
    end
```

## コメント

`;`で始まる行はコメントとして認識され、実行されません。

```aria
; これはコメントです
let %score, 100  ; スコアを初期化
text "ゲーム開始！"    ; メッセージを表示
```

## ウーザー入力

### 右クリックメニュー

`rmenu`コマンドで右クリックメニューのラベルを設定します。

```aria
rmenu *system_menu
```

### キー入力待機

`wait`コマンドで指定したミリ秒間待機します。

```aria
text "3秒後に進みます..."
wait 3000
text "進みました！"
```

### クリック待機

`wait_click`コマンドでクリックを待機します。

```aria
text "クリックで進みます..."
wait_click
text "クリックされました！"
```

## 条件分岐

### if文

```aria
let %score, 100

if %score > 90
    text "素晴らしいスコアです！"
else
    text "頑張りましょう！"
endif
```

### 条件ジャンプ

```aria
let %health, 100

if %health <= 0
    goto *game_over
endif

text "ゲーム続行中..."
```

### 比較演算子

- `==`: 等しい
- `!=`: 等しくない
- `>`: より大きい
- `<`: より小さい
- `>=`: 以上
- `<=`: 以下

```aria
let %value, 5

if %value == 5
    text "値は5です"
endif

if %value > 0
    text "値は正です"
endif
```

## ループ

### for文

```aria
for %i = 0 to 10
    text "${%i}回目の処理"
next
```

### ループの終了

`break`コマンドでループを終了できます。

```aria
for %i = 0 to 100
    text "${%i}回目"
    if %i == 5
        break
    endif
next
```

## ジャンプ

### goto文

指定したラベルにジャンプします。

```aria
*main
    text "メイン処理"
    goto *end

*another_label
    text "ここには来ません"

*end
    text "終了"
```

### jmpコマンド

`goto`と同じですが、より柔軟なジャンプが可能です。

```aria
jmp *label_name
```

## 基本的なフロー制御

### 基本的なゲームループ構造

```aria
*main
    ; 初期化
    gosub *init

    ; メインループ
    *game_loop
        text "選択してください"
        wait 1000
        goto *game_loop
    end

    ; 初期化処理
    *init
        text "初期化中..."
        return
```

## エラーハンドリング

### よくあるエラー

**変数未定義エラー**:
```aria
let %0, 100  ; 変数を初期化してから使用する
text "${%0}ポイント"
```

**ラベル未定義エラー**:
```aria
goto *existing_label  ; ラベルを定義してから使用する
*existing_label
    text "ここに来ます"
```

**構文エラー**:
- 引数の数が正しいか確認
- カンマや引用符が正しいか確認
- コマンドのスペルが正しいか確認

## ベストプラクティス

1. **変数は初期化してから使用**: `let` コマンドで初期化
2. **ラベルは意味のある名前を付ける**: `*start`, `*game_over` など
3. **コメントを活用**: 複雑なロジックに説明を追加
4. **サブルーチンで再利用**: 共通処理はサブルーチンに分割
5. **条件分岐を明確に**: ネストした条件は読みにくいためます

## 次のステップ

- [制御構造](control-flow.md) - より高度な条件分岐とループ
- [スプライト操作](sprites.md) - ビジュアル要素の操作方法
- [アニメーション](animations.md) - 動的なエフェクトの実装
- [UI要素](ui-elements.md) - ボタンやメニューの作成
