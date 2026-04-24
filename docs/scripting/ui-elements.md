# UI要素

このドキュメントでは、AriaEngineのUI要素について説明します。

## ボタン

### 基本的なボタン作成

```aria
; ボタン背景
lsp_rect 100, 440, 300, 400, 60
sp_fill 100, "#2a2a3e", 255
sp_round 100, 10
sp_border 100, "#4a4a6e", 2

; ボタンテキスト
lsp_text 101, "クリック", 640, 315
sp_text_align 101, "center"
sp_text_valign 101, "center"

; ボタンとして設定
sp_isbutton 100, true
spbtn 100, 1

; クリック待機
btnwait %result
if %result == 1
    text "ボタンがクリックされました"
endif
```

### スタイリッシュなボタン

```aria
; ボタン背景
lsp_rect 100, 440, 300, 400, 60
sp_fill 100, "#4a6fa5", 255
sp_round 100, 12
sp_border 100, "#5a7fb5", 2
sp_shadow 100, 4, 4, "#000000", 200

; ホバーエフェクト
sp_hover_color 100, "#5a7fb5"
sp_hover_scale 100, 1.05

; ボタンテキスト
lsp_text 101, "スタート", 640, 315
sp_fontsize 101, 24
sp_text_align 101, "center"
sp_text_valign 101, "center"
sp_color 101, "#ffffff"
sp_text_shadow 101, 2, 2, "#000000"

; ボタンとして設定
sp_isbutton 100, true
spbtn 100, 1
```

### ホバーエフェクト

```aria
lsp_rect 100, 440, 300, 400, 60
sp_fill 100, "#2a2a3e", 255
sp_round 100, 10

; ホバー時の色変更
sp_hover_color 100, "#3a3a5e"

; ホバー時のスケール変更
sp_hover_scale 100, 1.05

; ホバー時のカーソル変更
sp_hover_cursor 100, "hand"

sp_isbutton 100, true
spbtn 100, 1
```

## 複数のボタン

### ボタンの配置

```aria
; ボタン1
lsp_rect 100, 440, 300, 400, 60
sp_fill 100, "#2a2a3e", 255
sp_round 100, 10
sp_isbutton 100, true
spbtn 100, 1

lsp_text 101, "スタート", 640, 315
sp_text_align 101, "center"

; ボタン2
lsp_rect 200, 440, 380, 400, 60
sp_fill 200, "#2a2a3e", 255
sp_round 200, 10
sp_isbutton 200, true
spbtn 200, 2

lsp_text 201, "設定", 640, 395
sp_text_align 201, "center"

; ボタン3
lsp_rect 300, 440, 460, 400, 60
sp_fill 300, "#2a2a3e", 255
sp_round 300, 10
sp_isbutton 300, true
spbtn 300, 3

lsp_text 301, "終了", 640, 475
sp_text_align 301, "center"

; ボタン待機
btnwait %result
if %result == 1
    goto *game_start
elseif %result == 2
    goto *settings
elseif %result == 3
    end
endif
```

## ボタンエリア

### ボタンエリアの定義

```aria
; ボタンエリアを定義
btn_area 0, 0, 1280, 720

; エリア内のクリックを待機
btnwait %result
if %result == 0
    text "画面がクリックされました"
endif
```

### 複数のボタンエリア

```aria
; 左側エリア
btn_area 1, 0, 0, 640, 720

; 右側エリア
btn_area 2, 640, 0, 640, 720

; クリック待機
btnwait %result
if %result == 1
    text "左側がクリックされました"
elseif %result == 2
    text "右側がクリックされました"
endif
```

## テキストボックス

### 基本的なテキストボックス

```aria
; テキストボックス背景
lsp_rect 100, 0, 540, 1280, 180
sp_fill 100, "#1a1a2e", 200
sp_round 100, 10
sp_border 100, "#3a3a5e", 2

; テキスト表示
text "こんにちは、世界！"
```

### スタイリッシュなテキストボックス

```aria
; テキストボックス背景
lsp_rect 100, 0, 540, 1280, 180
sp_fill 100, "#1a1a2e", 220
sp_round 100, 15
sp_border 100, "#4a4a6e", 3
sp_shadow 100, 0, 5, "#000000", 150

; テキスト表示
主人公「これはスタイリッシュなテキストボックスです」
```

## メニュー

### 右クリックメニュー

```aria
; 右クリックメニューのラベルを設定
rmenu *system_menu

; メニュー処理
*system_menu
    ; メニュー背景
    lsp_rect 200, 500, 200, 300, 200
    sp_fill 200, "#2a2a3e", 255
    sp_round 200, 10
    sp_border 200, "#4a4a6e", 2

    ; メニュー項目
    lsp_text 201, "セーブ", 650, 240
    sp_text_align 201, "center"

    lsp_text 202, "ロード", 650, 280
    sp_text_align 202, "center"

    lsp_text 203, "タイトルへ", 650, 320
    sp_text_align 203, "center"

    lsp_text 204, "終了", 650, 360
    sp_text_align 204, "center"

    ; ボタン設定
    lsp_rect 210, 520, 220, 260, 30
    sp_fill 210, "#000000", 0
    sp_isbutton 210, true
    spbtn 210, 1

    lsp_rect 220, 520, 260, 260, 30
    sp_fill 220, "#000000", 0
    sp_isbutton 220, true
    spbtn 220, 2

    lsp_rect 230, 520, 300, 260, 30
    sp_fill 230, "#000000", 0
    sp_isbutton 230, true
    spbtn 230, 3

    lsp_rect 240, 520, 340, 260, 30
    sp_fill 240, "#000000", 0
    sp_isbutton 240, true
    spbtn 240, 4

    ; ボタン待機
    btnwait %result
    csp -1

    if %result == 1
        goto *save_menu
    elseif %result == 2
        goto *load_menu
    elseif %result == 3
        goto *title
    elseif %result == 4
        end
    endif

    ; メニューを閉じる
    return
```

## タイトル画面

### 基本的なタイトル画面

```aria
*title_screen
    ; 背景表示
    lsp 10, "bg/title.png", 0, 0

    ; タイトルテキスト
    lsp_text 20, "ゲームタイトル", 640, 200
    sp_fontsize 20, 64
    sp_text_align 20, "center"
    sp_color 20, "#ffffff"
    sp_text_shadow 20, 3, 3, "#000000"

    ; ボタン作成
    lsp_rect 30, 440, 300, 400, 60
    sp_fill 30, "#2a2a3e", 255
    sp_round 30, 10
    sp_border 30, "#4a4a6e", 2
    sp_isbutton 30, true
    spbtn 30, 1

    lsp_text 31, "スタート", 640, 315
    sp_text_align 31, "center"

    lsp_rect 40, 440, 380, 400, 60
    sp_fill 40, "#2a2a3e", 255
    sp_round 40, 10
    sp_border 40, "#4a4a6e", 2
    sp_isbutton 40, true
    spbtn 40, 2

    lsp_text 41, "終了", 640, 395
    sp_text_align 41, "center"

    ; ボタン待機
    btnwait %result

    if %result == 1
        goto *game_start
    elseif %result == 2
        end
    endif
```

## スライダー

### 手動スライダー実装

```aria
; スライダー背景
lsp_rect 100, 400, 300, 480, 20
sp_fill 100, "#2a2a3e", 255
sp_round 100, 10

; スライダーハンドル
lsp_rect 101, 400, 290, 20, 40
sp_fill 101, "#4a6fa5", 255
sp_round 101, 10
sp_isbutton 101, true
spbtn 101, 1

; スライダーラベル
lsp_text 102, "音量: 50%", 640, 350
sp_text_align 102, "center"

; ドラッグ処理
*slider_loop
    btnwait %result

    if %result == 1
        ; マウス位置に応じてハンドル移動
        ; （実際にはマウス位置の取得が必要）
    endif

    goto *slider_loop
```

## プログレスバー

### 手動プログレスバー実装

```aria
; プログレスバー背景
lsp_rect 100, 300, 300, 680, 30
sp_fill 100, "#2a2a3e", 255
sp_round 100, 15
sp_border 100, "#4a4a6e", 2

; プログレスバー進捗
lsp_rect 101, 302, 302, 0, 26
sp_fill 101, "#4a6fa5", 255
sp_round 101, 13

; 進捗テキスト
lsp_text 102, "ロード中... 0%", 640, 340
sp_text_align 102, "center"

; 進捗更新
*progress_loop
    let %progress, 0

    for %i = 0 to 100
        let %progress, %i
        sp_width 101, %i * 6.76
        lsp_text 102, "ロード中... ${%progress}%", 640, 340
        sp_text_align 102, "center"
        wait 50
    next

    text "ロード完了！"
```

## チャプター選択UI

### チャプターカード

```aria
; チャプターカード背景
lsp_rect 100, 100, 100, 300, 150
sp_fill 100, "#2a2a3e", 255
sp_round 100, 10
sp_border 100, "#4a4a6e", 2
sp_shadow 100, 3, 3, "#000000", 150

; チャプタータイトル
lsp_text 101, "第1章: はじまり", 250, 130
sp_fontsize 101, 24
sp_text_align 101, "center"
sp_color 101, "#ffffff"

; チャプター説明
lsp_text 102, "物語の始まり", 250, 170
sp_fontsize 102, 16
sp_text_align 102, "center"
sp_color 102, "#aaaaaa"

; ボタンとして設定
lsp_rect 103, 100, 100, 300, 150
sp_fill 103, "#000000", 0
sp_isbutton 103, true
spbtn 103, 1
```

## スクロールリスト

### 手動スクロール実装

```aria
; リスト背景
lsp_rect 100, 100, 100, 400, 500
sp_fill 100, "#1a1a2e", 255
sp_round 100, 10

; スクロールバー
lsp_rect 101, 480, 100, 20, 500
sp_fill 101, "#2a2a3e", 255
sp_round 101, 10

; スクロールハンドル
lsp_rect 102, 480, 100, 20, 100
sp_fill 102, "#4a6fa5", 255
sp_round 102, 10
sp_isbutton 102, true
spbtn 102, 1

; リスト項目
lsp_text 103, "項目1", 300, 130
lsp_text 104, "項目2", 300, 180
lsp_text 105, "項目3", 300, 230
```

## 実践的な例

### 完全なメニューシステム

```aria
*menu_system
    ; 背景
    lsp 10, "bg/menu.png", 0, 0

    ; メニュータイトル
    lsp_text 20, "メニュー", 640, 150
    sp_fontsize 20, 48
    sp_text_align 20, "center"
    sp_color 20, "#ffffff"
    sp_text_shadow 20, 3, 3, "#000000"

    ; ボタン作成関数
    gosub *create_buttons

    ; ボタン待機
    *menu_loop
        btnwait %result

        if %result == 1
            goto *save_menu
        elseif %result == 2
            goto *load_menu
        elseif %result == 3
            goto *settings_menu
        elseif %result == 4
            goto *title
        endif

        goto *menu_loop

*create_buttons
    ; ボタン1: セーブ
    lsp_rect 100, 440, 250, 400, 60
    sp_fill 100, "#2a2a3e", 255
    sp_round 100, 10
    sp_border 100, "#4a4a6e", 2
    sp_hover_color 100, "#3a3a5e"
    sp_isbutton 100, true
    spbtn 100, 1

    lsp_text 101, "セーブ", 640, 265
    sp_text_align 101, "center"
    sp_color 101, "#ffffff"

    ; ボタン2: ロード
    lsp_rect 200, 440, 330, 400, 60
    sp_fill 200, "#2a2a3e", 255
    sp_round 200, 10
    sp_border 200, "#4a4a6e", 2
    sp_hover_color 200, "#3a3a5e"
    sp_isbutton 200, true
    spbtn 200, 2

    lsp_text 201, "ロード", 640, 345
    sp_text_align 201, "center"
    sp_color 201, "#ffffff"

    ; ボタン3: 設定
    lsp_rect 300, 440, 410, 400, 60
    sp_fill 300, "#2a2a3e", 255
    sp_round 300, 10
    sp_border 300, "#4a4a6e", 2
    sp_hover_color 300, "#3a3a5e"
    sp_isbutton 300, true
    spbtn 300, 3

    lsp_text 301, "設定", 640, 425
    sp_text_align 301, "center"
    sp_color 301, "#ffffff"

    ; ボタン4: タイトルへ
    lsp_rect 400, 440, 490, 400, 60
    sp_fill 400, "#2a2a3e", 255
    sp_round 400, 10
    sp_border 400, "#4a4a6e", 2
    sp_hover_color 400, "#3a3a5e"
    sp_isbutton 400, true
    spbtn 400, 4

    lsp_text 401, "タイトルへ", 640, 505
    sp_text_align 401, "center"
    sp_color 401, "#ffffff"

    return
```

## ベストプラクティス

1. **一貫性を保つ**: ボタンのスタイルを統一する
2. **ホバーエフェクト**: ユーザーにインタラクティブ性を示す
3. **適切なサイズ**: クリックしやすい十分なサイズを確保
4. **コントラスト**: テキストと背景のコントラストを高める
5. **アクセシビリティ**: キーボード操作をサポートする

## トラブルシューティング

### ボタンが反応しない

1. `sp_isbutton` が true に設定されているか確認
2. `spbtn` でボタンIDが設定されているか確認
3. Zオーダーが他のスプライトに隠れていないか確認
4. ボタン領域が重なっていないか確認

### テキストが表示されない

1. フォントサイズが適切か確認
2. テキスト色が背景色と同じでないか確認
3. `sp_text_align` で正しく配置されているか確認

### ホバーエフェクトが動かない

1. `sp_hover_color` や `sp_hover_scale` が設定されているか確認
2. スプライトがボタンとして設定されているか確認

## 次のステップ

- [高度な機能](advanced.md) - 複雑なUIシステムの実装
- [アニメーション](animations.md) - UIアニメーションの追加
