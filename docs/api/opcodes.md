# オペコードリファレンス

このドキュメントでは、AriaEngineの全オペコード（63種類）について詳細に説明します。

## 目次

- [基本コマンド](#基本コマンド)
- [スクリプト制御](#スクリプト制御)
- [スプライト操作](#スプライト操作)
- [アニメーション](#アニメーション)
- [ボタン操作](#ボタン操作)
- [UI・テキスト](#uiテキスト)
- [オーディオ](#オーディオ)
- [システム](#システム)
- [フラグ・カウンター](#フラグカウンター)
- [チャプター管理](#チャプター管理)
- [キャラクター操作](#キャラクター操作)

---

## 基本コマンド

### text

**カテゴリ**: 基本コマンド

**構文**:
```
text "メッセージ"
```

**引数**:
- `"メッセージ"`: 表示するテキスト

**説明**:
テキストボックスにメッセージを表示します。

**例**:
```aria
text "こんにちは、世界！"
```

**関連コマンド**: textclear, textspeed, fontsize

---

### wait

**カテゴリ**: 基本コマンド

**構文**:
```
wait ミリ秒
```

**引数**:
- `ミリ秒`: 待機時間（ミリ秒）

**説明**:
指定したミリ秒間処理を待機します。

**例**:
```aria
wait 1000  ; 1秒待機
```

**関連コマンド**: delay, wait_click

---

### end

**カテゴリ**: 基本コマンド

**構文**:
```
end
```

**引数**: なし

**説明**:
スクリプトの実行を終了します。

**例**:
```aria
if %game_over == 1
    end
endif
```

---

## スクリプト制御

### let

**カテゴリ**: スクリプト制御

**構文**:
```
let %変数, 値
let $文字列変数, "文字列"
```

**引数**:
- `%変数`: 整数変数（%0-%9）
- `値`: 整数値
- `$文字列変数`: 文字列変数
- `"文字列"`: 文字列値

**説明**:
変数に値を代入します。

**例**:
```aria
let %0, 100
let $name, "主人公"
```

**関連コマンド**: mov, inc, dec

---

### inc

**カテゴリ**: スクリプト制御

**構文**:
```
inc %変数
inc %変数, 増加量
```

**引数**:
- `%変数`: 整数変数
- `増加量`: 増加する値（省略時は1）

**説明**:
変数の値を増加します。

**例**:
```aria
inc %0        ; %0を1増やす
inc %1, 10    ; %1を10増やす
```

**関連コマンド**: dec, let, add

---

### dec

**カテゴリ**: スクリプト制御

**構文**:
```
dec %変数
dec %変数, 減少量
```

**引数**:
- `%変数`: 整数変数
- `減少量`: 減少する値（省略時は1）

**説明**:
変数の値を減少します。

**例**:
```aria
dec %0        ; %0を1減らす
dec %1, 5     ; %1を5減らす
```

**関連コマンド**: inc, let, sub

---

### if

**カテゴリ**: スクリプト制御

**構文**:
```
if 条件
    コマンド
endif
```

**引数**:
- `条件`: 比較式
- `コマンド`: 条件が真の場合に実行するコマンド

**説明**:
条件が真の場合にコマンドを実行します。

**例**:
```aria
if %0 == 1
    text "条件が真です"
endif
```

**関連コマンド**: beq, bne, bgt, blt

---

### goto

**カテゴリ**: スクリプト制御

**構文**:
```
goto *ラベル名
```

**引数**:
- `*ラベル名`: ジャンプ先のラベル

**説明**:
指定したラベルに無条件ジャンプします。

**例**:
```aria
goto *game_start

*game_start
    text "ゲーム開始！"
```

**関連コマンド**: jmp, gosub

---

### gosub

**カテゴリ**: スクリプト制御

**構文**:
```
gosub *ラベル名
```

**引数**:
- `*ラベル名`: サブルーチンのラベル

**説明**:
サブルーチンを呼び出します。`return`で戻ります。

**例**:
```aria
gosub *show_message
text "戻りました"

*show_message
    text "サブルーチンです"
    return
```

**関連コマンド**: return, defsub, goto

---

### return

**カテゴリ**: スクリプト制御

**構文**:
```
return
```

**引数**: なし

**説明**:
サブルーチンから呼び出し元に戻ります。

**例**:
```aria
*subroutine
    text "処理中..."
    return
```

**関連コマンド**: gosub, defsub

---

### defsub

**カテゴリ**: スクリプト制御

**構文**:
```
defsub サブルーチン名
```

**引数**:
- `サブルーチン名`: サブルーチンの名前

**説明**:
名前付きサブルーチンを定義します。

**例**:
```aria
defsub show_title
    text "タイトル"
    return
```

**関連コマンド**: gosub, return, getparam

---

### for

**カテゴリ**: スクリプト制御

**構文**:
```
for %変数 = 開始値 to 終了値
```

**引数**:
- `%変数`: ループカウンタ用変数
- `開始値`: ループの開始値
- `終了値`: ループの終了値

**説明**:
指定回数ループします。

**例**:
```aria
for %i = 0 to 10
    text "${%i}回目"
next
```

**関連コマンド**: next, break

---

### next

**カテゴリ**: スクリプト制御

**構文**:
```
next
```

**引数**: なし

**説明**:
`for`ループの反復処理を継続します。

**例**:
```aria
for %i = 0 to 10
    text "${%i}回目"
next
```

**関連コマンド**: for, break

---

## スプライト操作

### lsp

**カテゴリ**: スプライト操作

**構文**:
```
lsp ID, "画像パス", X, Y
```

**引数**:
- `ID`: スプライトID（一意の整数）
- `"画像パス"`: 画像ファイルのパス
- `X`: X座標
- `Y`: Y座標

**説明**:
画像スプライトを作成して表示します。

**例**:
```aria
lsp 10, "assets/bg/forest.png", 0, 0
lsp 20, "assets/ch/hero.png", 800, 100
```

**関連コマンド**: lsp_text, lsp_rect, csp, vsp

---

### lsp_text

**カテゴリ**: スプライト操作

**構文**:
```
lsp_text ID, "テキスト", X, Y
```

**引数**:
- `ID`: スプライトID
- `"テキスト"`: 表示するテキスト
- `X`: X座標
- `Y`: Y座標

**説明**:
テキストスプライトを作成して表示します。

**例**:
```aria
lsp_text 100, "Hello World", 100, 100
sp_fontsize 100, 32
sp_color 100, "#ffffff"
```

**関連コマンド**: lsp, lsp_rect, sp_fontsize, sp_color

---

### lsp_rect

**カテゴリ**: スプライト操作

**構文**:
```
lsp_rect ID, X, Y, 幅, 高さ
```

**引数**:
- `ID`: スプライトID
- `X`: X座標
- `Y`: Y座標
- `幅`: 矩形の幅
- `高さ`: 矩形の高さ

**説明**:
矩形スプライトを作成して表示します。

**例**:
```aria
lsp_rect 200, 0, 0, 1280, 720
sp_fill 200, "#1a1a2e", 255
```

**関連コマンド**: lsp, lsp_text, sp_fill

---

### csp

**カテゴリ**: スプライト操作

**構文**:
```
csp ID
csp -1
```

**引数**:
- `ID`: 削除するスプライトのID
- `-1`: 全スプライトを削除

**説明**:
スプライトを削除します。

**例**:
```aria
csp 10      ; IDが10のスプライトを削除
csp -1      ; 全スプライトを削除
```

**関連コマンド**: lsp, vsp

---

### vsp

**カテゴリ**: スプライト操作

**構文**:
```
vsp ID, on/off
```

**引数**:
- `ID`: スプライトID
- `on/off`: 表示状態（onまたはoff）

**説明**:
スプライトの表示/非表示を切り替えます。

**例**:
```aria
vsp 10, on   ; 表示
vsp 10, off  ; 非表示
```

**関連コマンド**: lsp, csp, sp_alpha

---

### msp

**カテゴリ**: スプライト操作

**構文**:
```
msp ID, X, Y
msp ID, X, Y, 時間
```

**引数**:
- `ID`: スプライトID
- `X`: 移動先のX座標
- `Y`: 移動先のY座標
- `時間`: アニメーション時間（ミリ秒、省略可能）

**説明**:
スプライトを移動します。時間を指定するとアニメーション付きで移動します。

**例**:
```aria
msp 10, 100, 200           ; 即座に移動
msp 10, 100, 200, 500      ; 0.5秒かけて移動
```

**関連コマンド**: msp_rel, amsp

---

### sp_alpha

**カテゴリ**: スプライト操作

**構文**:
```
sp_alpha ID, 透明度
```

**引数**:
- `ID`: スプライトID
- `透明度`: 透明度（0-255）

**説明**:
スプライトの透明度を設定します。

**例**:
```aria
sp_alpha 10, 128  ; 50%の透明度
sp_alpha 10, 0    ; 完全に透明
sp_alpha 10, 255  ; 完全に不透明
```

**関連コマンド**: sp_scale, sp_color

---

### sp_scale

**カテゴリ**: スプライト操作

**構文**:
```
sp_scale ID, スケール
```

**引数**:
- `ID`: スプライトID
- `スケール`: スケール値（1.0 = 100%）

**説明**:
スプライトのスケールを設定します。

**例**:
```aria
sp_scale 10, 1.5  ; 150%に拡大
sp_scale 10, 0.5  ; 50%に縮小
```

**関連コマンド**: sp_alpha, sp_rotation

---

### sp_color

**カテゴリ**: スプライト操作

**構文**:
```
sp_color ID, "色"
```

**引数**:
- `ID`: スプライトID
- `"色"`: 色コード（16進数）

**説明**:
スプライトの色を設定します。

**例**:
```aria
sp_color 10, "#ff0000"  ; 赤色
sp_color 10, "#00ff00"  ; 緑色
```

**関連コマンド**: sp_fill, sp_alpha

---

### sp_fill

**カテゴリ**: スプライト操作

**構文**:
```
sp_fill ID, "色", 透明度
```

**引数**:
- `ID`: スプライトID
- `"色"`: 色コード（16進数）
- `透明度`: 透明度（0-255）

**説明**:
矩形スプライトの塗りつぶし色を設定します。

**例**:
```aria
lsp_rect 10, 0, 0, 100, 100
sp_fill 10, "#ff0000", 255  ; 赤色で塗りつぶし
```

**関連コマンド**: lsp_rect, sp_color

---

### sp_round

**カテゴリ**: スプライト操作

**構文**:
```
sp_round ID, 半径
```

**引数**:
- `ID`: スプライトID
- `半径`: 角丸の半径（ピクセル）

**説明**:
矩形スプライトの角を丸めます。

**例**:
```aria
lsp_rect 10, 0, 0, 200, 100
sp_fill 10, "#2a2a3e", 255
sp_round 10, 10  ; 10pxの角丸
```

**関連コマンド**: sp_border, sp_shadow

---

### sp_border

**カテゴリ**: スプライト操作

**構文**:
```
sp_border ID, "色", 太さ
```

**引数**:
- `ID`: スプライトID
- `"色"`: 枠線の色（16進数）
- `太さ`: 枠線の太さ（ピクセル）

**説明**:
スプライトに枠線を追加します。

**例**:
```aria
sp_border 10, "#ffffff", 2  ; 白色2pxの枠線
```

**関連コマンド**: sp_round, sp_fill

---

### sp_shadow

**カテゴリ**: スプライト操作

**構文**:
```
sp_shadow ID, X, Y, "色", 透明度
```

**引数**:
- `ID`: スプライトID
- `X`: 影のXオフセット
- `Y`: 影のYオフセット
- `"色"`: 影の色（16進数）
- `透明度`: 影の透明度（0-255）

**説明**:
スプライトに影を追加します。

**例**:
```aria
sp_shadow 10, 5, 5, "#000000", 150  ; X=5, Y=5, 黒色, 透明度150
```

**関連コマンド**: sp_round, sp_border

---

### sp_fontsize

**カテゴリ**: スプライト操作

**構文**:
```
sp_fontsize ID, サイズ
```

**引数**:
- `ID`: テキストスプライトID
- `サイズ`: フォントサイズ（ピクセル）

**説明**:
テキストスプライトのフォントサイズを設定します。

**例**:
```aria
lsp_text 10, "テキスト", 100, 100
sp_fontsize 10, 32
```

**関連コマンド**: lsp_text, sp_color

---

### sp_text_align

**カテゴリ**: スプライト操作

**構文**:
```
sp_text_align ID, "配置"
```

**引数**:
- `ID`: テキストスプライトID
- `"配置"`: テキスト配置（left, center, right）

**説明**:
テキストスプライトのテキスト配置を設定します。

**例**:
```aria
sp_text_align 10, "center"  ; 中央揃え
sp_text_align 10, "left"    ; 左揃え
sp_text_align 10, "right"   ; 右揃え
```

**関連コマンド**: lsp_text, sp_fontsize

---

### sp_z

**カテゴリ**: スプライト操作

**構文**:
```
sp_z ID, Z値
```

**引数**:
- `ID`: スプライトID
- `Z値`: Zオーダー値（数値が大きいほど手前）

**説明**:
スプライトのZオーダー（表示順序）を設定します。

**例**:
```aria
sp_z 10, 0   ; 最奥
sp_z 20, 50  ; 中間
sp_z 30, 100 ; 最前面
```

**関連コマンド**: sp_alpha, sp_scale

---

### sp_rotation

**カテゴリ**: スプライト操作

**構文**:
```
sp_rotation ID, 角度
```

**引数**:
- `ID`: スプライトID
- `角度`: 回転角度（度）

**説明**:
スプライトを回転させます。

**例**:
```aria
sp_rotation 10, 45  ; 時計回りに45度回転
```

**関連コマンド**: sp_scale, sp_alpha

---

### sp_hover_color

**カテゴリ**: スプライト操作

**構文**:
```
sp_hover_color ID, "色"
```

**引数**:
- `ID`: スプライトID
- `"色"`: ホバー時の色（16進数）

**説明**:
スプライトがホバーされた時の色を設定します。

**例**:
```aria
sp_hover_color 10, "#3a3a5e"
```

**関連コマンド**: sp_hover_scale, sp_isbutton

---

### sp_hover_scale

**カテゴリ**: スプライト操作

**構文**:
```
sp_hover_scale ID, スケール
```

**引数**:
- `ID`: スプライトID
- `スケール`: ホバー時のスケール値

**説明**:
スプライトがホバーされた時のスケールを設定します。

**例**:
```aria
sp_hover_scale 10, 1.05  ; 105%に拡大
```

**関連コマンド**: sp_hover_color, sp_isbutton

---

### sp_isbutton

**カテゴリ**: スプライト操作

**構文**:
```
sp_isbutton ID, true/false
```

**引数**:
- `ID`: スプライトID
- `true/false`: ボタンとして機能するか

**説明**:
スプライトをボタンとして機能させます。

**例**:
```aria
lsp_rect 10, 440, 300, 400, 60
sp_fill 10, "#2a2a3e", 255
sp_round 10, 10
sp_isbutton 10, true
spbtn 10, 1
```

**関連コマンド**: spbtn, btnwait

---

### spbtn

**カテゴリ**: スプライト操作

**構文**:
```
spbtn ID, ボタンID
```

**引数**:
- `ID`: スプライトID
- `ボタンID`: ボタンID

**説明**:
スプライトをボタンIDに関連付けます。

**例**:
```aria
sp_isbutton 10, true
spbtn 10, 1
```

**関連コマンド**: sp_isbutton, btnwait

---

## アニメーション

### amsp

**カテゴリ**: アニメーション

**構文**:
```
amsp ID, X, Y, 時間
```

**引数**:
- `ID`: スプライトID
- `X`: 移動先のX座標
- `Y`: 移動先のY座標
- `時間`: アニメーション時間（ミリ秒）

**説明**:
スプライトをアニメーション付きで移動します。

**例**:
```aria
amsp 10, 100, 200, 1000  ; 1秒かけて移動
await
```

**関連コマンド**: msp, await, ease

---

### afade

**カテゴリ**: アニメーション

**構文**:
```
afade ID, 透明度, 時間
```

**引数**:
- `ID`: スプライトID
- `透明度`: 目標の透明度（0-255）
- `時間`: アニメーション時間（ミリ秒）

**説明**:
スプライトの透明度をアニメーション付きで変更します。

**例**:
```aria
afade 10, 255, 1000  ; 1秒かけてフェードイン
await
```

**関連コマンド**: sp_alpha, await

---

### ascale

**カテゴリ**: アニメーション

**構文**:
```
ascale ID, スケール, 時間
```

**引数**:
- `ID`: スプライトID
- `スケール`: 目標のスケール値
- `時間`: アニメーション時間（ミリ秒）

**説明**:
スプライトのスケールをアニメーション付きで変更します。

**例**:
```aria
ascale 10, 2.0, 1000  ; 1秒かけて2倍に拡大
await
```

**関連コマンド**: sp_scale, await

---

### await

**カテゴリ**: アニメーション

**構文**:
```
await
```

**引数**: なし

**説明**:
全てのアニメーションが完了するのを待機します。

**例**:
```aria
amsp 10, 100, 200, 1000
afade 20, 255, 1000
await  ; 全てのアニメーション完了待機
text "アニメーション完了！"
```

**関連コマンド**: amsp, afade, ascale

---

### ease

**カテゴリ**: アニメーション

**構文**:
```
ease ID, "イージング関数"
```

**引数**:
- `ID`: スプライトID
- `"イージング関数"`: イージング関数名

**説明**:
アニメーションのイージング関数を設定します。

**例**:
```aria
ease 10, "linear"       ; 線形
ease 10, "easein"       ; イーズイン
ease 10, "easeout"      ; イーズアウト
ease 10, "easeinout"    ; イーズインアウト
```

**関連コマンド**: amsp, afade, ascale

---

## ボタン操作

### btnwait

**カテゴリ**: ボタン操作

**構文**:
```
btnwait %変数
```

**引数**:
- `%変数`: 結果を格納する変数

**説明**:
ボタンがクリックされるのを待機します。

**例**:
```aria
btnwait %result
if %result == 1
    text "ボタン1がクリックされました"
endif
```

**関連コマンド**: spbtn, btn

---

### btn_area

**カテゴリ**: ボタン操作

**構文**:
```
btn_area ID, X, Y, 幅, 高さ
```

**引数**:
- `ID`: ボタンエリアID
- `X`: X座標
- `Y`: Y座標
- `幅`: 幅
- `高さ`: 高さ

**説明**:
ボタンエリアを定義します。

**例**:
```aria
btn_area 1, 0, 0, 640, 720
btnwait %result
```

**関連コマンド**: btn, btnwait

---

## UI・テキスト

### textclear

**カテゴリ**: UI・テキスト

**構文**:
```
textclear
```

**引数**: なし

**説明**:
テキストボックスをクリアします。

**例**:
```aria
text "1ページ目"
textclear
text "2ページ目"
```

**関連コマンド**: text, wait_click

---

### textspeed

**カテゴリ**: UI・テキスト

**構文**:
```
textspeed 速度
```

**引数**:
- `速度`: テキスト表示速度（ミリ秒/文字）

**説明**:
テキストの表示速度を設定します。

**例**:
```aria
textspeed 50  ; 50ミリ秒/文字
```

**関連コマンド**: text, default_speed

---

### fontsize

**カテゴリ**: UI・テキスト

**構文**:
```
fontsize サイズ
```

**引数**:
- `サイズ`: フォントサイズ（ピクセル）

**説明**:
テキストボックスのフォントサイズを設定します。

**例**:
```aria
fontsize 24
```

**関連コマンド**: textcolor, textbox

---

### textcolor

**カテゴリ**: UI・テキスト

**構文**:
```
textcolor "色"
```

**引数**:
- `"色"`: テキストの色（16進数）

**説明**:
テキストボックスのテキスト色を設定します。

**例**:
```aria
textcolor "#ffffff"
```

**関連コマンド**: fontsize, textbox_color

---

### textbox

**カテゴリ**: UI・テキスト

**構文**:
```
textbox X, Y, 幅, 高さ
```

**引数**:
- `X`: X座標
- `Y`: Y座標
- `幅`: 幅
- `高さ`: 高さ

**説明**:
テキストボックスの位置とサイズを設定します。

**例**:
```aria
textbox 0, 540, 1280, 180
```

**関連コマンド**: textbox_color, textbox_show

---

### textbox_color

**カテゴリ**: UI・テキスト

**構文**:
```
textbox_color "色", 透明度
```

**引数**:
- `"色"`: テキストボックスの色（16進数）
- `透明度`: 透明度（0-255）

**説明**:
テキストボックスの背景色を設定します。

**例**:
```aria
textbox_color "#1a1a2e", 200
```

**関連コマンド**: textbox, textcolor

---

### textbox_show

**カテゴリ**: UI・テキスト

**構文**:
```
textbox_show
```

**引数**: なし

**説明**:
テキストボックスを表示します。

**例**:
```aria
textbox_show
text "テキストボックスが表示されています"
```

**関連コマンド**: textbox_hide, textbox

---

### textbox_hide

**カテゴリ**: UI・テキスト

**構文**:
```
textbox_hide
```

**引数**: なし

**説明**:
テキストボックスを非表示にします。

**例**:
```aria
textbox_hide
```

**関連コマンド**: textbox_show, textbox

---

### wait_click

**カテゴリ**: UI・テキスト

**構文**:
```
wait_click
```

**引数**: なし

**説明**:
クリックを待機します。

**例**:
```aria
text "クリックで進みます..."
wait_click
text "進みました！"
```

**関連コマンド**: wait, wait_click_clear

---

### wait_click_clear

**カテゴリ**: UI・テキスト

**構文**:
```
wait_click_clear
```

**引数**: なし

**説明**:
クリックを待機し、テキストボックスをクリアします。

**例**:
```aria
text "クリックでクリアします"
wait_click_clear
text "クリアされました"
```

**関連コマンド**: wait_click, textclear

---

## オーディオ

### play_bgm

**カテゴリ**: オーディオ

**構文**:
```
play_bgm "ファイルパス"
```

**引数**:
- `"ファイルパス"`: BGMファイルのパス

**説明**:
BGMを再生します。

**例**:
```aria
play_bgm "assets/bgm/main_theme.mp3"
```

**関連コマンド**: stop_bgm, bgmvol

---

### stop_bgm

**カテゴリ**: オーディオ

**構文**:
```
stop_bgm
```

**引数**: なし

**説明**:
BGMを停止します。

**例**:
```aria
stop_bgm
```

**関連コマンド**: play_bgm, bgmfade

---

### play_se

**カテゴリ**: オーディオ

**構文**:
```
play_se "ファイルパス"
```

**引数**:
- `"ファイルパス"`: SEファイルのパス

**説明**:
効果音（SE）を再生します。

**例**:
```aria
play_se "assets/se/click.wav"
```

**関連コマンド**: sevol

---

### bgmvol

**カテゴリ**: オーディオ

**構文**:
```
bgmvol 音量
```

**引数**:
- `音量`: BGMの音量（0-255）

**説明**:
BGMの音量を設定します。

**例**:
```aria
bgmvol 128  ; 50%の音量
```

**関連コマンド**: sevol, play_bgm

---

### sevol

**カテゴリ**: オーディオ

**構文**:
```
sevol 音量
```

**引数**:
- `音量`: SEの音量（0-255）

**説明**:
SEの音量を設定します。

**例**:
```aria
sevol 200  ; 約78%の音量
```

**関連コマンド**: bgmvol, play_se

---

### bgmfade

**カテゴリ**: オーディオ

**構文**:
```
bgmfade 時間
```

**引数**:
- `時間`: フェード時間（ミリ秒）

**説明**:
BGMをフェードアウトします。

**例**:
```aria
bgmfade 1000  ; 1秒かけてフェードアウト
```

**関連コマンド**: stop_bgm, fade_out

---

## システム

### save

**カテゴリ**: システム

**構文**:
```
save スロット番号
```

**引数**:
- `スロット番号`: セーブスロット番号

**説明**:
ゲームデータをセーブします。

**例**:
```aria
save 1  ; スロット1にセーブ
```

**関連コマンド**: load, saveon

---

### load

**カテゴリ**: システム

**構文**:
```
load スロット番号
```

**引数**:
- `スロット番号`: ロードするスロット番号

**説明**:
ゲームデータをロードします。

**例**:
```aria
load 1  ; スロット1からロード
```

**関連コマンド**: save, saveoff

---

### saveon

**カテゴリ**: システム

**構文**:
```
saveon
```

**引数**: なし

**説明**:
セーブを有効化します。

**例**:
```aria
saveon
```

**関連コマンド**: saveoff, save

---

### saveoff

**カテゴリ**: システム

**構文**:
```
saveoff
```

**引数**: なし

**説明**:
セーブを無効化します。

**例**:
```aria
saveoff
```

**関連コマンド**: saveon, load

---

### rmenu

**カテゴリ**: システム

**構文**:
```
rmenu *ラベル名
```

**引数**:
- `*ラベル名`: 右クリックメニューのラベル

**説明**:
右クリックメニューのラベルを設定します。

**例**:
```aria
rmenu *system_menu
```

**関連コマンド**: gosub, return

---

## フラグ・カウンター

### set_flag

**カテゴリ**: フラグ・カウンター

**構文**:
```
set_flag "フラグ名", 値
```

**引数**:
- `"フラグ名"`: フラグ名
- `値`: フラグの値（0または1）

**説明**:
フラグを設定します。

**例**:
```aria
set_flag "game_started", 1
set_flag "met_hero", 0
```

**関連コマンド**: get_flag, clear_flag, toggle_flag

---

### get_flag

**カテゴリ**: フラグ・カウンター

**構文**:
```
get_flag "フラグ名", %変数
```

**引数**:
- `"フラグ名"`: フラグ名
- `%変数`: 値を格納する変数

**説明**:
フラグの値を取得します。

**例**:
```aria
get_flag "game_started", %is_started
if %is_started == 1
    text "ゲームは開始されています"
endif
```

**関連コマンド**: set_flag, clear_flag

---

### clear_flag

**カテゴリ**: フラグ・カウンター

**構文**:
```
clear_flag "フラグ名"
```

**引数**:
- `"フラグ名"`: フラグ名

**説明**:
フラグをクリア（falseに設定）します。

**例**:
```aria
clear_flag "game_started"
```

**関連コマンド**: set_flag, get_flag

---

### toggle_flag

**カテゴリ**: フラグ・カウンター

**構文**:
```
toggle_flag "フラグ名"
```

**引数**:
- `"フラグ名"`: フラグ名

**説明**:
フラグを反転します。

**例**:
```aria
toggle_flag "game_started"  ; trueならfalse、falseならtrue
```

**関連コマンド**: set_flag, get_flag

---

### inc_counter

**カテゴリ**: フラグ・カウンター

**構文**:
```
inc_counter "カウンター名", 増加量
```

**引数**:
- `"カウンター名"`: カウンター名
- `増加量`: 増加する値（省略時は1）

**説明**:
カウンターを増加します。

**例**:
```aria
inc_counter "score", 10
```

**関連コマンド**: dec_counter, set_counter, get_counter

---

### dec_counter

**カテゴリ**: フラグ・カウンター

**構文**:
```
dec_counter "カウンター名", 減少量
```

**引数**:
- `"カウンター名"`: カウンター名
- `減少量`: 減少する値（省略時は1）

**説明**:
カウンターを減少します。

**例**:
```aria
dec_counter "health", 20
```

**関連コマンド**: inc_counter, set_counter, get_counter

---

### set_counter

**カテゴリ**: フラグ・カウンター

**構文**:
```
set_counter "カウンター名", 値
```

**引数**:
- `"カウンター名"`: カウンター名
- `値`: カウンターの値

**説明**:
カウンターを設定します。

**例**:
```aria
set_counter "score", 0
set_counter "health", 100
```

**関連コマンド**: inc_counter, dec_counter, get_counter

---

### get_counter

**カテゴリ**: フラグ・カウンター

**構文**:
```
get_counter "カウンター名", %変数
```

**引数**:
- `"カウンター名"`: カウンター名
- `%変数`: 値を格納する変数

**説明**:
カウンターの値を取得します。

**例**:
```aria
get_counter "score", %current_score
text "現在のスコア: ${%current_score}点"
```

**関連コマンド**: set_counter, inc_counter, dec_counter

---

## チャプター管理

### defchapter

**カテゴリ**: チャプター管理

**構文**:
```
defchapter
```

**引数**: なし

**説明**:
チャプター定義を開始します。

**例**:
```aria
defchapter
    chapter_id "chapter_1"
    chapter_title "第1章"
    chapter_desc "物語の始まり"
    chapter_script "chapter1.aria"
endchapter
```

**関連コマンド**: endchapter, chapter_id

---

### chapter_id

**カテゴリ**: チャプター管理

**構文**:
```
chapter_id "ID"
```

**引数**:
- `"ID"`: チャプターID

**説明**:
チャプターのIDを設定します。

**例**:
```aria
chapter_id "chapter_1"
```

**関連コマンド**: defchapter, chapter_title

---

### chapter_title

**カテゴリ**: チャプター管理

**構文**:
```
chapter_title "タイトル"
```

**引数**:
- `"タイトル"`: チャプタータイトル

**説明**:
チャプターのタイトルを設定します。

**例**:
```aria
chapter_title "第1章: はじまり"
```

**関連コマンド**: defchapter, chapter_desc

---

### chapter_desc

**カテゴリ**: チャプター管理

**構文**:
```
chapter_desc "説明"
```

**引数**:
- `"説明"`: チャプター説明

**説明**:
チャプターの説明を設定します。

**例**:
```aria
chapter_desc "物語の始まり"
```

**関連コマンド**: defchapter, chapter_script

---

### chapter_script

**カテゴリ**: チャプター管理

**構文**:
```
chapter_script "スクリプトパス"
```

**引数**:
- `"スクリプトパス"`: チャプタースクリプトのパス

**説明**:
チャプターのスクリプトファイルパスを設定します。

**例**:
```aria
chapter_script "assets/scripts/chapters/chapter1.aria"
```

**関連コマンド**: defchapter, endchapter

---

### endchapter

**カテゴリ**: チャプター管理

**構文**:
```
endchapter
```

**引数**: なし

**説明**:
チャプター定義を終了します。

**例**:
```aria
defchapter
    chapter_id "chapter_1"
    chapter_title "第1章"
endchapter
```

**関連コマンド**: defchapter

---

### chapter_select

**カテゴリ**: チャプター管理

**構文**:
```
chapter_select %変数
```

**引数**:
- `%変数`: 選択結果を格納する変数

**説明**:
チャプター選択画面を表示します。

**例**:
```aria
chapter_select %selected
if %selected == 1
    goto *chapter_1
endif
```

**関連コマンド**: defchapter, unlock_chapter

---

## キャラクター操作

### char_show

**カテゴリ**: キャラクター操作

**構文**:
```
char_show "キャラクター名", "画像パス", X, Y
```

**引数**:
- `"キャラクター名"`: キャラクター名
- `"画像パス"`: 画像ファイルのパス
- `X`: X座標
- `Y`: Y座標

**説明**:
キャラクターを表示します。

**例**:
```aria
char_show "mio", "assets/ch/mio.png", 800, 0
```

**関連コマンド**: char_hide, char_move

---

### char_hide

**カテゴリ**: キャラクター操作

**構文**:
```
char_hide "キャラクター名"
```

**引数**:
- `"キャラクター名"`: キャラクター名

**説明**:
キャラクターを非表示にします。

**例**:
```aria
char_hide "mio"
```

**関連コマンド**: char_show, char_move

---

### char_move

**カテゴリ**: キャラクター操作

**構文**:
```
char_move "キャラクター名", X, Y
```

**引数**:
- `"キャラクター名"`: キャラクター名
- `X`: 移動先のX座標
- `Y`: 移動先のY座標

**説明**:
キャラクターを移動します。

**例**:
```aria
char_move "mio", 600, 100
```

**関連コマンド**: char_show, char_hide

---

### char_expression

**カテゴリ**: キャラクター操作

**構文**:
```
char_expression "キャラクター名", "表情名"
```

**引数**:
- `"キャラクター名"`: キャラクター名
- `"表情名"`: 表情名

**説明**:
キャラクターの表情を変更します。

**例**:
```aria
char_expression "mio", "smile"
```

**関連コマンド**: char_show, char_pose

---

### char_pose

**カテゴリ**: キャラクター操作

**構文**:
```
char_pose "キャラクター名", "ポーズ名"
```

**引数**:
- `"キャラクター名"`: キャラクター名
- `"ポーズ名"`: ポーズ名

**説明**:
キャラクターのポーズを変更します。

**例**:
```aria
char_pose "mio", "standing"
```

**関連コマンド**: char_show, char_expression

---

### char_z

**カテゴリ**: キャラクター操作

**構文**:
```
char_z "キャラクター名", Z値
```

**引数**:
- `"キャラクター名"`: キャラクター名
- `Z値`: Zオーダー値

**説明**:
キャラクターのZオーダーを設定します。

**例**:
```aria
char_z "mio", 50
```

**関連コマンド**: char_show, sp_z

---

### char_scale

**カテゴリ**: キャラクター操作

**構文**:
```
char_scale "キャラクター名", スケール
```

**引数**:
- `"キャラクター名"`: キャラクター名
- `スケール`: スケール値

**説明**:
キャラクターのスケールを設定します。

**例**:
```aria
char_scale "mio", 1.2
```

**関連コマンド**: char_show, sp_scale

---

## Init用コマンド

### window

**カテゴリ**: Init用

**構文**:
```
window 幅, 高さ, "タイトル"
```

**引数**:
- `幅`: ウィンドウ幅
- `高さ`: ウィンドウ高さ
- `"タイトル"`: ウィンドウタイトル

**説明**:
ウィンドウサイズとタイトルを設定します。

**例**:
```aria
window 1280, 720, "My Visual Novel"
```

**関連コマンド**: font, script

---

### font

**カテゴリ**: Init用

**構文**:
```
font "フォントファイルパス"
```

**引数**:
- `"フォントファイルパス"`: TTFフォントファイルのパス

**説明**:
フォントファイルを設定します。

**例**:
```aria
font "assets/fonts/NotoSansJP-Regular.ttf"
```

**関連コマンド**: font_atlas_size, font_filter

---

### font_atlas_size

**カテゴリ**: Init用

**構文**:
```
font_atlas_size サイズ
```

**引数**:
- `サイズ`: フォントアトラスサイズ（ピクセル）

**説明**:
フォントアトラスのサイズを設定します。

**例**:
```aria
font_atlas_size 192
```

**関連コマンド**: font, font_filter

---

### font_filter

**カテゴリ**: Init用

**構文**:
```
font_filter "フィルター"
```

**引数**:
- `"フィルター"`: フィルター名（bilinear, trilinear, point）

**説明**:
フォントレンダリングのフィルターを設定します。

**例**:
```aria
font_filter "bilinear"
font_filter "trilinear"
font_filter "point"
```

**関連コマンド**: font, font_atlas_size

---

### script

**カテゴリ**: Init用

**構文**:
```
script "スクリプトファイルパス"
```

**引数**:
- `"スクリプトファイルパス"`: メインスクリプトファイルのパス

**説明**:
メインスクリプトファイルを設定します。

**例**:
```aria
script "assets/scripts/main.aria"
```

**関連コマンド**: window, font

---

### debug

**カテゴリ**: Init用

**構文**:
```
debug on/off
```

**引数**:
- `on/off`: デバッグモード（onまたはoff）

**説明**:
デバッグモードを有効化/無効化します。

**例**:
```aria
debug on
```

**関連コマンド**: window, script

---

## その他

### delay

**カテゴリ**: その他

**構文**:
```
delay ミリ秒
```

**引数**:
- `ミリ秒`: 待機時間（ミリ秒）

**説明**:
指定したミリ秒間待機します（`wait`の別名）。

**例**:
```aria
delay 500  ; 0.5秒待機
```

**関連コマンド**: wait

---

### rnd

**カテゴリ**: その他

**構文**:
```
rnd %変数, 最大値
```

**引数**:
- `%変数`: 結果を格納する変数
- `最大値`: 最大値

**説明**:
乱数を生成します。

**例**:
```aria
rnd %0, 10  ; 0-9の乱数
```

**関連コマンド**: let, inc

---

### reset_timer

**カテゴリ**: その他

**構文**:
```
reset_timer
```

**引数**: なし

**説明**:
タイマーをリセットします。

**例**:
```aria
reset_timer
```

**関連コマンド**: get_timer, wait_timer

---

### get_timer

**カテゴリ**: その他

**構文**:
```
get_timer %変数
```

**引数**:
- `%変数`: 経過時間を格納する変数

**説明**:
タイマーの経過時間を取得します。

**例**:
```aria
get_timer %elapsed
text "経過時間: ${%elapsed}ms"
```

**関連コマンド**: reset_timer, wait_timer

---

### wait_timer

**カテゴリ**: その他

**構文**:
```
wait_timer ミリ秒
```

**引数**:
- `ミリ秒`: 待機時間（ミリ秒）

**説明**:
タイマーを使用して待機します。

**例**:
```aria
reset_timer
text "3秒待ちます..."
wait_timer 3000
text "3秒経過しました"
```

**関連コマンド**: reset_timer, get_timer

---

## まとめ

AriaEngineは63種類のオペコードを提供しており、以下のカテゴリに分類されています：

1. **基本コマンド** (3): text, wait, end
2. **スクリプト制御** (10): let, inc, dec, if, goto, gosub, return, defsub, for, next
3. **スプライト操作** (20): lsp, lsp_text, lsp_rect, csp, vsp, msp, sp_alpha, sp_scale, sp_color, sp_fill, sp_round, sp_border, sp_shadow, sp_fontsize, sp_text_align, sp_z, sp_rotation, sp_hover_color, sp_hover_scale, sp_isbutton, spbtn
4. **アニメーション** (5): amsp, afade, ascale, await, ease
5. **ボタン操作** (2): btnwait, btn_area
6. **UI・テキスト** (10): textclear, textspeed, fontsize, textcolor, textbox, textbox_color, textbox_show, textbox_hide, wait_click, wait_click_clear
7. **オーディオ** (6): play_bgm, stop_bgm, play_se, bgmvol, sevol, bgmfade
8. **システム** (5): save, load, saveon, saveoff, rmenu
9. **フラグ・カウンター** (8): set_flag, get_flag, clear_flag, toggle_flag, inc_counter, dec_counter, set_counter, get_counter
10. **チャプター管理** (6): defchapter, chapter_id, chapter_title, chapter_desc, chapter_script, endchapter, chapter_select
11. **キャラクター操作** (7): char_show, char_hide, char_move, char_expression, char_pose, char_z, char_scale
12. **Init用** (6): window, font, font_atlas_size, font_filter, script, debug
13. **その他** (4): delay, rnd, reset_timer, get_timer, wait_timer

これらのオペコードを組み合わせることで、複雑なビジュアルノベルゲームを作成できます。
