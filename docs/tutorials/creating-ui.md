# UI作成

このチュートリアルでは、AriaEngineを使用してスタイリッシュなUIを作成する方法を説明します。

## 前提: コアモードで開始する

新規プロジェクトは次を推奨します。

```aria
compat_mode off
ui_theme "clean"
textmode manual
text_target 3001
```

`compat_mode off` では `choice` / `yesnobox` の自動UI生成は行われません。  
描画命令（`lsp_rect` / `lsp_text` / `spbtn` / `btnwait`）でUIを構築します。

## ステップ1: タイトル画面の基本

### 基本的なタイトル画面

まずはシンプルなタイトル画面を作成します。

```aria
*title_screen
    ; 背景を設定
    bg "#1a1a2e", 2

    ; タイトルテキスト
    lsp_text 100, "ゲームタイトル", 640, 200
    sp_fontsize 100, 64
    sp_text_align 100, "center"
    sp_color 100, "#ffffff"

    ; スタートボタン
    lsp_rect 101, 440, 300, 400, 60
    sp_fill 101, "#2a2a3e", 255
    sp_isbutton 101, true
    spbtn 101, 1

    lsp_text 102, "スタート", 640, 315
    sp_text_align 102, "center"

    ; ボタン待機
    btnwait %result
    if %result == 1
        goto *game_start
    endif
```

## ステップ2: スタイリッシュなボタンの作成

### 角丸と枠線の追加

ボタンをよりスタイリッシュにするために、角丸と枠線を追加します。

```aria
*stylish_button
    ; ボタン背景
    lsp_rect 101, 440, 300, 400, 60

    ; 塗りつぶし色
    sp_fill 101, "#4a6fa5", 255

    ; 角丸（12px）
    sp_round 101, 12

    ; 枠線（白色、2px）
    sp_border 101, "#5a7fb5", 2

    ; ボタンとして設定
    sp_isbutton 101, true
    spbtn 101, 1

    ; ボタンテキスト
    lsp_text 102, "スタート", 640, 315
    sp_fontsize 102, 24
    sp_text_align 102, "center"
    sp_color 102, "#ffffff"
```

### シャドウの追加

ボタンにシャドウを追加して、奥行きを出します。

```aria
*button_with_shadow
    ; ボタン背景
    lsp_rect 101, 440, 300, 400, 60
    sp_fill 101, "#4a6fa5", 255
    sp_round 101, 12
    sp_border 101, "#5a7fb5", 2

    ; シャドウ（X=5, Y=5, 黒色, 透明度150）
    sp_shadow 101, 5, 5, "#000000", 150

    sp_isbutton 101, true
    spbtn 101, 1

    lsp_text 102, "スタート", 640, 315
    sp_fontsize 102, 24
    sp_text_align 102, "center"
    sp_color 102, "#ffffff"
```

## ステップ3: ホバーエフェクトの追加

### ホバー時の色変更

ボタンがホバーされた時に色を変更します。

```aria
*hover_color
    lsp_rect 101, 440, 300, 400, 60
    sp_fill 101, "#4a6fa5", 255
    sp_round 101, 12
    sp_border 101, "#5a7fb5", 2

    ; ホバー時の色変更
    sp_hover_color 101, "#5a7fb5"

    sp_isbutton 101, true
    spbtn 101, 1

    lsp_text 102, "スタート", 640, 315
    sp_text_align 102, "center"
```

### ホバー時のスケール変更

ボタンがホバーされた時にスケールを変更します。

```aria
*hover_scale
    lsp_rect 101, 440, 300, 400, 60
    sp_fill 101, "#4a6fa5", 255
    sp_round 101, 12

    ; ホバー時に105%に拡大
    sp_hover_scale 101, 1.05

    sp_isbutton 101, true
    spbtn 101, 1

    lsp_text 102, "スタート", 640, 315
    sp_text_align 102, "center"
```

### 完全なホバーエフェクト

色とスケールの両方を変更します。

```aria
*full_hover_effect
    lsp_rect 101, 440, 300, 400, 60
    sp_fill 101, "#4a6fa5", 255
    sp_round 101, 12
    sp_border 101, "#5a7fb5", 2
    sp_shadow 101, 4, 4, "#000000", 200

    ; ホバーエフェクト
    sp_hover_color 101, "#5a7fb5"
    sp_hover_scale 101, 1.05

    sp_isbutton 101, true
    spbtn 101, 1

    lsp_text 102, "スタート", 640, 315
    sp_fontsize 102, 24
    sp_text_align 102, "center"
    sp_color 102, "#ffffff"
```

## ステップ4: テキストボックスの作成

### 基本的なテキストボックス

```aria
*basic_textbox
    ; テキストボックス背景
    lsp_rect 200, 0, 540, 1280, 180
    sp_fill 200, "#1a1a2e", 200
    sp_round 200, 10
    sp_border 200, "#3a3a5e", 2

    ; text の出力先を明示
    lsp_text 3001, "", 32, 570
    sp_fontsize 3001, 28
    sp_color 3001, "#ffffff"
    text_target 3001

    ; テキスト表示
    text "これはテキストボックスです"
```

### スタイリッシュなテキストボックス

```aria
*stylish_textbox
    ; テキストボックス背景
    lsp_rect 200, 0, 540, 1280, 180
    sp_fill 200, "#1a1a2e", 220
    sp_round 200, 15
    sp_border 200, "#4a4a6e", 3
    sp_shadow 200, 0, 5, "#000000", 150

    ; テキスト表示設定
    fontsize 24
    textcolor "#ffffff"

    ; テキスト表示
    text "これはスタイリッシュなテキストボックスです"
```

## ステップ5: 完全なタイトル画面

### 複数のボタンを持つタイトル画面

```aria
*complete_title_screen
    ; 背景
    bg "#1a1a2e", 2

    ; タイトルロゴ（背景画像がある場合）
    ; lsp 10, "assets/bg/title.png", 0, 0
    ; vsp 10, on

    ; タイトルテキスト
    lsp_text 100, "私のゲーム", 640, 150
    sp_fontsize 100, 64
    sp_text_align 100, "center"
    sp_color 100, "#ffffff"
    sp_text_shadow 100, 3, 3, "#000000"

    ; ボタン作成関数
    gosub *create_buttons

    ; ボタン待機
    btnwait %result

    if %result == 1
        csp -1
        goto *new_game
    elseif %result == 2
        goto *load_game
    elseif %result == 3
        goto *settings
    elseif %result == 4
        end
    endif

*create_buttons
    ; ボタン共通設定
    let %button_width, 400
    let %button_height, 60
    let %button_x, 440
    let %button_start_y, 250
    let %button_spacing, 80

    ; スタートボタン
    let %y, %button_start_y
    lsp_rect 101, %button_x, %y, %button_width, %button_height
    sp_fill 101, "#4a6fa5", 255
    sp_round 101, 12
    sp_border 101, "#5a7fb5", 2
    sp_shadow 101, 4, 4, "#000000", 200
    sp_hover_color 101, "#5a7fb5"
    sp_hover_scale 101, 1.05
    sp_isbutton 101, true
    spbtn 101, 1

    lsp_text 102, "スタート", 640, %y + 30
    sp_fontsize 102, 24
    sp_text_align 102, "center"
    sp_color 102, "#ffffff"

    ; ロードボタン
    let %y, %button_start_y + %button_spacing
    lsp_rect 103, %button_x, %y, %button_width, %button_height
    sp_fill 103, "#4a6fa5", 255
    sp_round 103, 12
    sp_border 103, "#5a7fb5", 2
    sp_shadow 103, 4, 4, "#000000", 200
    sp_hover_color 103, "#5a7fb5"
    sp_hover_scale 103, 1.05
    sp_isbutton 103, true
    spbtn 103, 2

    lsp_text 104, "ロード", 640, %y + 30
    sp_fontsize 104, 24
    sp_text_align 104, "center"
    sp_color 104, "#ffffff"

    ; 設定ボタン
    let %y, %button_start_y + %button_spacing * 2
    lsp_rect 105, %button_x, %y, %button_width, %button_height
    sp_fill 105, "#4a6fa5", 255
    sp_round 105, 12
    sp_border 105, "#5a7fb5", 2
    sp_shadow 105, 4, 4, "#000000", 200
    sp_hover_color 105, "#5a7fb5"
    sp_hover_scale 105, 1.05
    sp_isbutton 105, true
    spbtn 105, 3

    lsp_text 106, "設定", 640, %y + 30
    sp_fontsize 106, 24
    sp_text_align 106, "center"
    sp_color 106, "#ffffff"

    ; 終了ボタン
    let %y, %button_start_y + %button_spacing * 3
    lsp_rect 107, %button_x, %y, %button_width, %button_height
    sp_fill 107, "#4a6fa5", 255
    sp_round 107, 12
    sp_border 107, "#5a7fb5", 2
    sp_shadow 107, 4, 4, "#000000", 200
    sp_hover_color 107, "#5a7fb5"
    sp_hover_scale 107, 1.05
    sp_isbutton 107, true
    spbtn 107, 4

    lsp_text 108, "終了", 640, %y + 30
    sp_fontsize 108, 24
    sp_text_align 108, "center"
    sp_color 108, "#ffffff"

    return

*new_game
    text "新しいゲームを開始します"
    end

*load_game
    text "ロード画面へ..."
    end

*settings
    text "設定画面へ..."
    end
```

## ステップ6: レスポンシブなUI

### 画面サイズに合わせたUI配置

```aria
*responsive_ui
    ; 画面サイズを取得（仮定）
    let %screen_width, 1280
    let %screen_height, 720

    ; 中央配置計算
    let %center_x, %screen_width / 2
    let %center_y, %screen_height / 2

    ; タイトルテキスト
    lsp_text 100, "ゲームタイトル", %center_x, %center_y - 100
    sp_fontsize 100, 64
    sp_text_align 100, "center"
    sp_color 100, "#ffffff"

    ; ボタン配置
    let %button_width, 400
    let %button_height, 60
    let %button_x, (%screen_width - %button_width) / 2
    let %button_y, %center_y

    lsp_rect 101, %button_x, %button_y, %button_width, %button_height
    sp_fill 101, "#4a6fa5", 255
    sp_round 101, 12
    sp_isbutton 101, true
    spbtn 101, 1

    lsp_text 102, "スタート", %center_x, %button_y + 30
    sp_text_align 102, "center"
```

## ステップ7: アニメーション付きUI

### フェードインするタイトル画面

```aria
*animated_title
    ; 背景
    bg "#1a1a2e", 2

    ; タイトルテキスト（初期状態は透明）
    lsp_text 100, "ゲームタイトル", 640, 200
    sp_fontsize 100, 64
    sp_text_align 100, "center"
    sp_color 100, "#ffffff"
    sp_alpha 100, 0

    ; タイトルフェードイン
    afade 100, 255, 1000
    await

    ; ボタン作成
    gosub *create_buttons

    ; ボタンフェードイン
    sp_alpha 101, 0
    afade 101, 255, 500
    await

    sp_alpha 103, 0
    afade 103, 255, 500
    await

    ; ボタン待機
    btnwait %result

    if %result == 1
        goto *game_start
    endif
```

### スライドインするボタン

```aria
*slide_in_buttons
    ; 背景
    bg "#1a1a2e", 2

    ; タイトル
    lsp_text 100, "ゲームタイトル", 640, 200
    sp_fontsize 100, 64
    sp_text_align 100, "center"
    sp_color 100, "#ffffff"

    ; ボタン（画面外からスライドイン）
    lsp_rect 101, 1400, 300, 400, 60  ; 画面右外
    sp_fill 101, "#4a6fa5", 255
    sp_round 101, 12
    sp_isbutton 101, true
    spbtn 101, 1

    lsp_text 102, "スタート", 640, 315
    sp_text_align 102, "center"

    ; スライドイン
    amsp 101, 440, 300, 800
    ease 101, "easeout"
    await

    ; ボタン待機
    btnwait %result

    if %result == 1
        goto *game_start
    endif
```

## ステップ8: メニューシステム

### ポーズメニュー

```aria
*gameplay
    ; ゲームプレイ中...

    ; 右クリックメニューを設定
    rmenu *pause_menu

*pause_menu
    ; メニュー背景
    lsp_rect 200, 340, 110, 600, 500
    sp_fill 200, "#2a2a3e", 255
    sp_round 200, 15
    sp_border 200, "#4a4a6e", 3
    sp_shadow 200, 0, 10, "#000000", 200

    ; メニュータイトル
    lsp_text 201, "ポーズメニュー", 640, 150
    sp_fontsize 201, 32
    sp_text_align 201, "center"
    sp_color 201, "#ffffff"

    ; ボタン作成
    gosub *create_menu_buttons

    ; ボタン待機
    btnwait %result

    if %result == 1
        ; 再開
        csp -1
        return
    elseif %result == 2
        ; セーブ
        goto *save_menu
    elseif %result == 3
        ; 設定
        goto *settings_menu
    elseif %result == 4
        ; タイトルへ
        csp -1
        goto *title_screen
    endif

*create_menu_buttons
    ; 再開
    lsp_rect 210, 440, 200, 400, 60
    sp_fill 210, "#4a6fa5", 255
    sp_round 210, 10
    sp_hover_color 210, "#5a7fb5"
    sp_isbutton 210, true
    spbtn 210, 1

    lsp_text 211, "再開", 640, 215
    sp_text_align 211, "center"

    ; セーブ
    lsp_rect 220, 440, 280, 400, 60
    sp_fill 220, "#4a6fa5", 255
    sp_round 220, 10
    sp_hover_color 220, "#5a7fb5"
    sp_isbutton 220, true
    spbtn 220, 2

    lsp_text 221, "セーブ", 640, 295
    sp_text_align 221, "center"

    ; 設定
    lsp_rect 230, 440, 360, 400, 60
    sp_fill 230, "#4a6fa5", 255
    sp_round 230, 10
    sp_hover_color 230, "#5a7fb5"
    sp_isbutton 230, true
    spbtn 230, 3

    lsp_text 231, "設定", 640, 375
    sp_text_align 231, "center"

    ; タイトルへ
    lsp_rect 240, 440, 440, 400, 60
    sp_fill 240, "#4a6fa5", 255
    sp_round 240, 10
    sp_hover_color 240, "#5a7fb5"
    sp_isbutton 240, true
    spbtn 240, 4

    lsp_text 241, "タイトルへ", 640, 455
    sp_text_align 241, "center"

    return
```

## ベストプラクティス

1. **一貫性を保つ**: ボタンのスタイル、色、サイズを統一する
2. **ホバーエフェクト**: ユーザーにインタラクティブ性を示す
3. **適切なサイズ**: クリックしやすい十分なサイズを確保（最低44x44px）
4. **コントラスト**: テキストと背景のコントラストを高める
5. **アニメーション**: 適度なアニメーションでUXを向上させる
6. **レスポンシブ**: 画面サイズに合わせてUIを配置する

## トラブルシューティング

### ボタンが反応しない

**問題**: ボタンをクリックしても反応しない

**解決策**:
1. `sp_isbutton` が `true` に設定されているか確認
2. `spbtn` でボタンIDが設定されているか確認
3. Zオーダーが他のスプライトに隠れていないか確認

### ホバーエフェクトが動かない

**問題**: ホバー時に色やスケールが変わらない

**解決策**:
1. `sp_hover_color` や `sp_hover_scale` が設定されているか確認
2. スプライトがボタンとして設定されているか確認

### テキストが表示されない

**問題**: テキストが表示されない

**解決策**:
1. フォントサイズが適切か確認
2. テキスト色が背景色と同じでないか確認
3. `sp_text_align` で正しく配置されているか確認

## 付録: Yes/Noダイアログ

`src/AriaEngine/assets/scripts/ui_kit.aria` には、描画命令ベースの `*ui_yesno` を同梱しています。

```aria
gosub *ui_yesno, "ゲームを終了しますか？"
if %0 == 1 end
```

## 次のステップ

UI作成をマスターしたら、次のチュートリアルに進みましょう：

- [チャプターシステム](chapter-system.md) - チャプター管理の実装
- [セーブ/ロード実装](save-load.md) - 完全なセーブ/ロードシステム

## まとめ

このチュートリアルでは、以下のことを学びました：

1. 基本的なタイトル画面の作成
2. スタイリッシュなボタンの作成（角丸、枠線、シャドウ）
3. ホバーエフェクトの追加（色変更、スケール変更）
4. テキストボックスの作成
5. 完全なタイトル画面の作成
6. レスポンシブなUI配置
7. アニメーション付きUI（フェードイン、スライドイン）
8. メニューシステムの実装

これでスタイリッシュなUIを作成できるようになりました！次はチャプターシステムに挑戦しましょう。
