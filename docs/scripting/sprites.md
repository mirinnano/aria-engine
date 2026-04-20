# スプライト操作

このドキュメントでは、AriaEngineのスプライト操作について説明します。

## スプライトの基本

### スプライトの種類

AriaEngineでは3種類のスプライトをサポートしています：

1. **画像スプライト**: PNG/JPG画像を表示
2. **テキストスプライト**: テキストを表示
3. **矩形スプライト: 色りつきの四角形

## スプライトの作成

### 画像スプライト (lsp)

```aria
; 基本的な画像スプライト
lsp 10, "assets/bg/forest.png", 0, 0

; キャラクタースプライト
lsp 20, "assets/ch/hero.png", 800, 100
```

### テキストスプライト (lsp_text)

```aria
; 基本的なテキストスプライト
lsp_text 100, "Hello World", 100, 100
sp_fontsize 100, 32
sp_color 100, "#ffffff"
```

### 矩形スプライト (lsp_rect)

```aria
; 基本的な矩形スプライト
lsp_rect 200, 0, 0, 1280, 720
sp_fill 200, "#1a1a2e", 255
```

## スプライトの操作

### 表示/非表示 (vsp)

```aria
; スプライトを表示
vsp 10, on

; スプライトを非表示
vsp 10, off
```

### 削除 (csp)

```aria
; 特定のスプライトを削除
csp 10

; 全てのスプライトを削除
csp -1
```

### 移動 (msp)

```aria
; 即座に移動
msp 10, 100, 200

; アニメーション付きで移動（0.5秒かけて移動）
msp 10, 100, 200, 500
```

### 相対移動 (msp_rel)

```aria
; 相対的に移動（現在位置からx+50, y+100へ）
msp_rel 10, 50, 100
```

## スプライトのプロパティ設定

### 位置とサイズ

```aria
; 位置設定
msp 10, 100, 200  ; X=100, Y=200へ移動

; サイズ設定（テキストには効果なし）
msp 10, 100, 200
sp_width 10, 500
sp_height 10, 300
```

### 透明度

```aria
; 透明度設定 (0-255)
sp_alpha 10, 128  ; 50%の透明度

; 完全に透明
sp_alpha 10, 0

; 完全に不透明
sp_alpha 10, 255
```

### スケール

```aria
; スケール設定 (1.0 = 100%)
sp_scale 10, 1.5  ; 150%に拡大

sp_scale 10, 0.5  ; 50%に縮小
```

### 回転

```aria
; 回転角度（度）
sp_rotation 10, 45  ; 時計回りに45度回転
```

### Zオーダー

```aria
; Zオーダー設定（数値が大きいほど手前に表示）
sp_z 10, 100
```

### 色

```aria
; 色りつきスプライトの色（16進数）
sp_fill 10, "#ff0000", 255  ; 赤色

; テキストスプライトの色
sp_color 10, "#00ff00"  ; 緑色
```

### テキストスプライトのプロパティ

```aria
lsp_text 100, "テキスト", 100, 100

; フォントサイズ
sp_fontsize 100, 32

; テキスト配置
sp_text_align 100, "center"  ; left, center, right

; テキストシャドウ
sp_text_shadow 100, 2, 2, "#000000"

; テキストアウトライン
sp_text_outline 100, 2, "#ffffff"
```

## 装飾効果

### 角丸

```aria
; 角丸の半径を設定
sp_round 10, 10  ; 10pxの角丸
```

### 枠線

```aria
; 枠線の設定
sp_border 10, "#ffffff", 2  ; 白色2pxの枠線
```

### シャドウ

```aria
; シャドウの設定
sp_shadow 10, 5, 5, "#000000", 150  ; X=5, Y=5, 色=黒, 透明度=150
```

### グラデーション

```aria
; グラデーションの設定
sp_gradient 10, "#ffffff", "#000000"  ; 白から黒へのグラデーション
```

## ボタンとしての設定

### 基本的なボタン

```aria
lsp_rect 100, 400, 300, 200, 50
sp_fill 100, "#2a2a3e", 255
sp_round 100, 10
sp_border 100, "#4a4a6e", 2
sp_isbutton 100, true  ; ボタンとして設定
spbtn 100, 1  ; ボタンIDを設定

lsp_text 101, "クリック", 500, 315
sp_text_align 101, "center"

btnwait %result
if %result == 1
    text "ボタンがクリックされました"
endif
```

### ホバーエフェクト

```aria
; ホバー時の色変更
sp_hover_color 100, "#3a3a5e"

; ホバー時のスケール変更
sp_hover_scale 100, 1.05

; ホバー時のカーソル変更
sp_hover_cursor 100, "hand"
```

## Zオーダーの管理

```aria
; 背景（最奥）
lsp 10, "bg.png", 0, 0
sp_z 10, 0

; キャラクター（中間）
lsp 20, "hero.png", 800, 100
sp_z 20, 50

; UI（最前面）
lsp 30, "ui_panel.png", 0, 0
sp_z 30, 100
```

## 複数のスプライト操作

### テキスト付き矩形スプライト

```aria
lsp_rect 200, 0, 0, 1280, 720
sp_fill 200, "#1a1a2e", 255

lsp_text 201, "タイトル", 640, 100
sp_fontsize 201, 48
sp_text_align 201, "center"
sp_color 201, "#ffffff"
sp_text_shadow 201, 3, 3, "#000000"

lsp_rect 300, 440, 300, 400, 60
sp_fill 300, "#2a2a3e", 255
sp_round 300, 10
sp_border 300, "#4a4a6e", 2
sp_isbutton 300, true

lsp_text 301, "スタート", 640, 315
sp_text_align 301, "center"

spbtn 300, 1
```

### アニメーション付きスプライト

```aria
lsp 10, "character.png", 100, 100

; スケールアニメーション（1秒で2倍に拡大）
ascale 10, 2.0, 1000

; 透明度アニメーション（0.5秒でフェードアウト）
afade 10, 0, 500

await  ; アニメーション完了待機
```

## テキストラッピング

### テキスト折り返し

```aria
lsp_text 100, "これは長いテキストです。折り返しを設定すると、指定幅で自動的に折り返されます。", 100, 100
sp_width 100, 500
```

### テキスト配置の調整

```aria
lsp_text 100, "中央揃え", 100, 100
sp_width 100, 400
sp_height 100, 50
sp_text_align 100, "center"  ; left, center, right
```

## 実践的な例

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
sp_isbutton 100, true

; ボタンテキスト
lsp_text 101, "スタート", 640, 315
sp_fontsize 101, 24
sp_text_align 101, "center"
sp_color 101, "#ffffff"
sp_text_shadow 101, 2, 2, "#000000"

spbtn 100, 1
```

### キャラクター表示の基本

```aria
; キャラクターの表示
lsp 100, "assets/ch/hero.png", 800, 0
sp_z 100, 50

; セリフト表示
lsp 200, "assets/ch/hero_smile.png", 850, 50
sp_z 200, 51
sp_alpha 200, 128  ; 半透明で表示

; 会話中表示
主人公「こんにちは！」
```

### 背景遷移の実装

```aria
; 昼シーン
lsp 10, "bg/forest.png", 0, 0
afade 10, 255, 1000  ; フェードイン（1秒）
await

; シーン変更
csp 10
lsp 10, "bg/city.png", 0, 0
afade 10, 0, 1000  ; フェードアウト（1秒）
await

text "都市に到着しました"
```

## ベストプラクティス

1. **Zオーダーを意識する**: 背景 < キャラクター < UI の順に設定
2. **メモリ管理を意識する**: 使用しないスプライトは適切に削除する
3. **テキストスプライトを再利用**: 頻繁に更新する場合はIDを再利用する
4. **アニメーションにはawaitを使用**: アニメーション完了待機を明示する
5. **レスポンシブな設計**: 画面サイズに合わせて位置を調整する

## トラブルシューティング

### スプライトが表示されない

1. `vsp` コマンドで表示状態を確認
2. `sp_alpha` コマンドで透明度を確認（0なら完全に透明）
3. Zオーダーを確認（他のスプライトの後ろに隠れている可能性）
4. 画像パスが正しいか確認

### テキストが表示されない

1. フォントファイルが正しく設定されているか確認（init.aria）
2. `sp_fontsize` で適切なサイズを設定
3. `sp_color` で色を設定（デフォルトは透明な可能性）
4. `sp_width`, `sp_height` で十分なサイズを確保

### アニメーションが動かない

1. `await` コマンドを追加してアニメーション完了待機をする
2. 時間パラメータ（ミリ秒）を適切に設定
3. イージング関数を設定して動きを調整

## 次のステップ

- [アニメーション](animations.md) - 時間的な変化とエフェクト
- [UI要素](ui-elements.md) - ボタンやメニューの実装
- [高度な機能](advanced.md) - 複雑なスプライト操作
