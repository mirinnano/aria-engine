# UI・テキスト表示コマンドリファレンス

テキスト表示、テキストボックス、フォント設定、選択肢、バックログ、およびUIコンポーネントに関するTextカテゴリとUiカテゴリのコマンドを解説します。

---

## 目次

- [テキスト表示](#テキスト表示)
- [テキストボックス](#テキストボックス)
- [フォント・スタイル](#フォントスタイル)
- [待機・制御](#待機制御)
- [選択肢](#選択肢)
- [バックログ・既読管理](#バックログ既読管理)
- [UIコンポーネント作成](#uiコンポーネント作成)
- [UIグループ・レイアウト](#uiグループレイアウト)
- [UIアニメーション・イベント](#uiアニメーションイベント)
- [互換モード・テーマ](#互換モードテーマ)

---

## テキスト表示

### `text`

テキストを表示します。`text_target`で指定したスプライト、または互換モード時は自動生成されたテキストボックスに出力されます。

```aria
text "表示する文字列"
```

| 引数 | 型 | 説明 |
|------|-----|------|
| 可変 | 文字列 | 表示するテキスト（複数引数はスペースで結合） |

**例:**
```aria
text "こんにちは、世界！"
text "変数の値は" "${%0}" "です"
```

**注意:** 文字列補間 `${$var}` `${%var}` `${expression}` に対応しています。`textspeed` が 0 より大きい場合、タイプライター効果で1文字ずつ表示されます。

---

### `textclear`

現在のテキストバッファをクリアします。

```aria
textclear
```

**例:**
```aria
text "前のページのテキスト"
\               ; クリック待機とページクリア
textclear       ; 明示的にクリア
text "次のページ"
```

**注意:** 互換モード (`compat_mode on`) 時は、テキストボックス背景スプライトも同時に削除されます。

---

### `br`

テキストバッファに改行を追加します。

```aria
br
```

**例:**
```aria
text "1行目"
br
text "2行目"
```

---

### `text_target`

テキスト出力先のスプライトIDを指定します。

```aria
text_target id
```

| 引数 | 型 | 説明 |
|------|-----|------|
| `id` | 整数 | テキストスプライトID（`lsp_text`で作成したもの） |

**例:**
```aria
lsp_text 100, "", 50, 500
text_target 100
text "このテキストはスプライト100に表示されます"
```

**注意:** `compat_mode off` 時は必須です。`text_target` を設定しないと `text` コマンドは警告を出して描画されません。

---

## テキストボックス

### `textbox`

テキストボックスの位置とサイズを設定します。

```aria
textbox x, y, width, height
```

| 引数 | 型 | 説明 |
|------|-----|------|
| `x` | 整数 | X座標 |
| `y` | 整数 | Y座標 |
| `width` | 整数 | 幅 |
| `height` | 整数 | 高さ |

**例:**
```aria
textbox 50, 500, 1180, 200
```

---

### `setwindow`

`textbox` と同じくテキストボックスを設定します。NScripter互換の12引数形式も一部サポートしています。

```aria
setwindow x, y, width, height
```

**注意:** 引数が12個以上の場合、引数[4]をフォントサイズ、引数[11]を背景色として読み取ります。

---

### `textbox_show`

テキストボックスを表示します。

```aria
textbox_show
```

**注意:** `textbox_hide` で非表示にしたテキストボックスとテキストスプライトを再表示します。

---

### `textbox_hide`

テキストボックスを非表示にします。`erasetextwindow` と同じです。

```aria
textbox_hide
```

**例:**
```aria
textbox_hide
bg "cg.png", 0   ; テキストボックスなしで背景表示
textbox_show     ; テキストボックスを復帰
```

---

### `textbox_color`

テキストボックスの背景色と透明度を設定します。

```aria
textbox_color color, alpha
```

| 引数 | 型 | 説明 |
|------|-----|------|
| `color` | 文字列 | 背景色（`#rrggbb` 形式） |
| `alpha` | 整数 | 不透明度（0〜255） |

**例:**
```aria
textbox_color "#1a1a2e", 180
```

---

### `textbox_style`

テキストボックスの装飾スタイルを一括設定します。

```aria
textbox_style radius, border_width, border_color, border_alpha, padding_x, padding_y, shadow_x, shadow_y, shadow_color, shadow_alpha
```

| 引数 | 型 | 説明 |
|------|-----|------|
| `radius` | 整数 | 角丸半径 |
| `border_width` | 整数 | 枠線幅 |
| `border_color` | 文字列 | 枠線色 |
| `border_alpha` | 整数 | 枠線不透明度 |
| `padding_x` | 整数 | 水平パディング |
| `padding_y` | 整数 | 垂直パディング |
| `shadow_x` | 整数 | 影のXオフセット |
| `shadow_y` | 整数 | 影のYオフセット |
| `shadow_color` | 文字列 | 影の色 |
| `shadow_alpha` | 整数 | 影の不透明度 |

**例:**
```aria
textbox_style 12, 2, "#4a4a6e", 255, 20, 16, 0, 4, "#000000", 150
```

---

## フォント・スタイル

### `fontsize`

デフォルトのフォントサイズを設定します。

```aria
fontsize size
```

| 引数 | 型 | 説明 |
|------|-----|------|
| `size` | 整数 | フォントサイズ（ピクセル） |

**例:**
```aria
fontsize 24
```

---

### `textcolor`

デフォルトのテキスト色を設定します。

```aria
textcolor color
```

| 引数 | 型 | 説明 |
|------|-----|------|
| `color` | 文字列 | テキスト色（`#rrggbb` 形式） |

**例:**
```aria
textcolor "#ffffff"
```

---

### `font`

使用するフォントファイルを指定します。`init.aria` でのみ推奨されます。

```aria
font "path"
```

| 引数 | 型 | 説明 |
|------|-----|------|
| `path` | 文字列 | フォントファイルパス |

**例:**
```aria
font "assets/fonts/NotoSansJP.ttf"
```

---

### `font_atlas_size`

フォントアトラステクスチャのサイズを設定します。

```aria
font_atlas_size size
```

| 引数 | 型 | 説明 |
|------|-----|------|
| `size` | 整数 | アトラスサイズ（8〜512、自動でクランプ） |

**注意:** 大きな文字セットを使用する場合は大きめの値を設定してください。`init.aria` でのみ推奨されます。

---

### `font_filter`

フォントテクスチャのフィルタリングモードを設定します。

```aria
font_filter mode
```

| 引数 | 型 | 説明 |
|------|-----|------|
| `mode` | 文字列 | `bilinear` / `trilinear` / `point` / `anisotropic` |

**例:**
```aria
font_filter "trilinear"
```

---

## 待機・制御

### `wait`

指定した時間待機するか、クリック待機を行います。

```aria
wait milliseconds
```

```aria
wait           ; クリック待機（引数なし）
```

| 引数 | 型 | 説明 |
|------|-----|------|
| `milliseconds` | 整数 | 待機時間（ミリ秒）。省略時はクリック待機 |

**例:**
```aria
wait 1000      ; 1秒待機
wait           ; クリック待機（バックログ登録・オートセーブあり）
```

**注意:** 引数なしの `wait` は `text` の後に自動的にバックログ登録とオートセーブを行います。

---

### `@`

クリック待機（ページクリアなし）。`wait`（引数なし）と同じです。

```aria
@
```

**例:**
```aria
text "次の行に続きます"
@
text "続きのテキスト"
```

---

### `\`

クリック待機とページクリア。`textclear` + `wait` と同等です。

```aria
\
```

**例:**
```aria
text "1ページ目"
\
text "2ページ目"
```

---

### `textspeed`

テキストのタイピング速度を設定します。

```aria
textspeed ms
```

| 引数 | 型 | 説明 |
|------|-----|------|
| `ms` | 整数 | 1文字あたりの表示間隔（ミリ秒）。0で瞬間表示 |

**例:**
```aria
textspeed 50   ; やや遅め
textspeed 0    ; 瞬間表示
```

---

### `defaultspeed`

デフォルトのテキスト速度を設定し、設定ファイルに保存します。

```aria
defaultspeed ms
```

| 引数 | 型 | 説明 |
|------|-----|------|
| `ms` | 整数 | デフォルト速度（ミリ秒） |

**注意:** `config.json` に保存され、次回起動時も維持されます。

---

## 選択肢

### `choice`

選択肢を表示してクリック待機します。`compat_mode on` 時は自動的にボタンを生成します。

```aria
choice "選択肢1", "選択肢2", ...
```

| 引数 | 型 | 説明 |
|------|-----|------|
| 可変 | 文字列 | 選択肢テキスト（複数指定可） |

**例:**
```aria
choice "はい", "いいえ", "後で決める"
; 結果は %0 に入る（0始まりのインデックス）
```

**注意:**
- `compat_mode on` 時は自動的に中央寄せのボタンを生成します。
- `compat_mode off` 時は事前に `spbtn` でボタンを作成しておく必要があります。
- 結果は `%0` レジスタに入ります（0 = 最初の選択肢）。

---

### `choice_style`

選択肢ボタンのスタイルを一括設定します。

```aria
choice_style width, height, spacing, font_size, bg_color, bg_alpha, text_color, corner_radius, border_color, border_width, border_alpha, hover_color, padding_x
```

| 引数 | 型 | 説明 |
|------|-----|------|
| `width` | 整数 | ボタン幅 |
| `height` | 整数 | ボタン高さ |
| `spacing` | 整数 | ボタン間の隙間 |
| `font_size` | 整数 | フォントサイズ |
| `bg_color` | 文字列 | 背景色 |
| `bg_alpha` | 整数 | 背景不透明度 |
| `text_color` | 文字列 | テキスト色 |
| `corner_radius` | 整数 | 角丸半径 |
| `border_color` | 文字列 | 枠線色 |
| `border_width` | 整数 | 枠線幅 |
| `border_alpha` | 整数 | 枠線不透明度 |
| `hover_color` | 文字列 | ホバー時の背景色 |
| `padding_x` | 整数 | 水平パディング |

---

## バックログ・既読管理

### `backlog`

バックログ（テキスト履歴）機能の有効/無効を切り替えます。

```aria
backlog on
backlog off
```

| 引数 | 型 | 説明 |
|------|-----|------|
| `on/off` | 文字列 | `on` で有効、`off` で無効 |

**注意:** デフォルトは有効です。無効にすると `wait` / `@` / `\` 時の履歴登録が行われません。

---

### `lookback_on`

バックログ参照機能を有効にします。

```aria
lookback_on
```

---

### `lookback_off`

バックログ参照機能を無効にします。

```aria
lookback_off
```

---

### `kidokumode`

既読モードを設定します。既読フラグを記録して、既読/未読を区別できるようにします。

```aria
kidokumode 1
kidokumode 0
```

| 引数 | 型 | 説明 |
|------|-----|------|
| `flag` | 整数 | 0 で無効、0以外で有効 |

---

### `clickcursor`

クリック待機中のカーソル表示を設定します。

```aria
clickcursor off                    ; カーソルを非表示
clickcursor "engine"               ; エンジン標準カーソル
clickcursor "path.png", ox, oy     ; カスタム画像カーソル
```

| 引数 | 型 | 説明 |
|------|-----|------|
| `mode/path` | 文字列 | `off` / `engine` / `default` / `builtin` / 画像パス |
| `ox` | 整数 | （オプション）画像カーソルのXオフセット |
| `oy` | 整数 | （オプション）画像カーソルのYオフセット |

**例:**
```aria
clickcursor "cursor.png", 8, 8
```

---

### `skipmode`

スキップモード時の未読テキストスキップ設定を行います。

```aria
skipmode mode
```

| 引数 | 型 | 説明 |
|------|-----|------|
| `mode` | 文字列 | `all` / `unread` / `on` / `off` |

**例:**
```aria
skipmode "all"     ; 未読もスキップ可能
skipmode "off"     ; 既読のみスキップ
```

**注意:** 設定は `config.json` に保存されます。

---

## UIコンポーネント作成

### `ui`

汎用UIプロパティ設定コマンド。ターゲットとプロパティ名を指定して値を設定します。

```aria
ui target, property, value, ...
```

| 引数 | 型 | 説明 |
|------|-----|------|
| `target` | 文字列 | 対象（`textbox` / `choice` / `text` / `skip` / `sprite:N` / `group:N` / `rmenu` / `save` / `load` / `backlog` / `settings` / `cursor` / `menu_action:X` / `system.X`） |
| `property` | 文字列 | プロパティ名 |
| `value` | 可変 | 設定値 |

**例:**
```aria
ui "textbox", "fill", "#1a1a2e"
ui "textbox", "padding", 20, 16
ui "choice", "hover_fill", "#3a3a5e"
ui "sprite:100", "opacity", 0.5
ui "group:1", "visible", "off"
```

**サポートされる target と property:**

| target | property | 説明 |
|--------|----------|------|
| `textbox` | `x`, `y`, `w`, `h`, `padding`, `fill`, `fill_alpha`, `text_color`, `font_size`, `radius`, `border`, `border_color`, `border_alpha`, `shadow`, `shadow_alpha`, `visible` | テキストボックス設定 |
| `choice` | `w`, `h`, `gap`, `padding`, `fill`, `fill_alpha`, `text_color`, `font_size`, `radius`, `border`, `border_color`, `border_alpha`, `hover_fill` | 選択肢スタイル |
| `text` | `speed`, `advance`, `advance_ratio`, `shadow`, `shadow_color`, `outline`, `outline_color`, `effect`, `effect_strength`, `effect_speed` | テキスト表示設定 |
| `skip` | `speed`, `force_speed` | スキップ速度 |
| `sprite:N` | `x`, `y`, `w`, `h`, `z`, `fill`, `fill_alpha`, `text`, `color`, `font_size`, `align`, `valign`, `radius`, `border`, `visible`, `opacity`, `scale`, `hover_fill`, `hover_scale`, `enabled`, `text_shadow`, `text_outline`, `text_effect`, `button` | スプライトプロパティ |
| `group:N` | 上記 `sprite:N` と同じ | グループ内全スプライトに適用 |
| `rmenu` | `w`, `align`, `fill`, `fill_alpha`, `text_color`, `border_color`, `radius` | 右クリックメニュー |
| `save` / `load` | `w`, `columns`, `fill`, `fill_alpha`, `text_color`, `border_color`, `radius` | セーブ/ロード画面 |
| `backlog` | `w`, `fill`, `fill_alpha`, `text_color`, `border_color`, `radius` | バックログ画面 |
| `settings` | `w`, `fill`, `fill_alpha`, `text_color`, `border_color`, `radius` | 設定画面 |
| `cursor` | `size`, `color`, `visible`, `mode` | クリックカーソル |
| `menu_action:X` | - | メニューアクション上書き（save/load/backlog/rmenu は予約） |
| `system.X` | `enabled` / `visible` | システムボタン（close/reset/skip/save/load） |

---

### `ui_rect`

矩形UIスプライトを作成します。

```aria
ui_rect id, x, y, width, height
```

| 引数 | 型 | 説明 |
|------|-----|------|
| `id` | 整数 | スプライトID |
| `x` | 整数 | X座標 |
| `y` | 整数 | Y座標 |
| `width` | 整数 | 幅 |
| `height` | 整数 | 高さ |

**例:**
```aria
ui_rect 100, 400, 300, 200, 60
```

**注意:** 作成時に `choice_style` のスタイル設定（背景色、枠線、ホバー色など）を継承します。

---

### `ui_text`

テキストUIスプライトを作成します。

```aria
ui_text id, "text", x, y
ui_text id, "text", x, y, width, height
```

| 引数 | 型 | 説明 |
|------|-----|------|
| `id` | 整数 | スプライトID |
| `text` | 文字列 | 表示テキスト |
| `x` | 整数 | X座標 |
| `y` | 整数 | Y座標 |
| `width` | 整数 | （オプション）幅 |
| `height` | 整数 | （オプション）高さ |

**例:**
```aria
ui_text 101, "スタート", 640, 300
```

---

### `ui_image`

画像UIスプライトを作成します。

```aria
ui_image id, "path", x, y
```

| 引数 | 型 | 説明 |
|------|-----|------|
| `id` | 整数 | スプライトID |
| `path` | 文字列 | 画像ファイルパス |
| `x` | 整数 | X座標 |
| `y` | 整数 | Y座標 |

**例:**
```aria
ui_image 10, "ui/logo.png", 400, 100
```

---

### `ui_button`

スプライトをボタンとして設定し、クリック時の戻り値を指定します。

```aria
ui_button sprite_id, result_value
```

| 引数 | 型 | 説明 |
|------|-----|------|
| `sprite_id` | 整数 | 対象スプライトID |
| `result_value` | 整数 | クリック時の戻り値 |

**例:**
```aria
ui_rect 100, 400, 300, 200, 60
ui_button 100, 1
btnwait %result
```

---

### `ui_slider`

スライダーUIを作成します。

```aria
ui_slider id, x, y, width, min, max, value
```

| 引数 | 型 | 説明 |
|------|-----|------|
| `id` | 整数 | スライダーID（track）。fill=id+1, thumb=id+2, value_text=id+3 が自動生成 |
| `x` | 整数 | X座標 |
| `y` | 整数 | Y座標 |
| `width` | 整数 | スライダー幅 |
| `min` | 整数 | 最小値 |
| `max` | 整数 | 最大値 |
| `value` | 整数 | 初期値 |

**例:**
```aria
ui_slider 100, 400, 300, 400, 0, 100, 50
```

**注意:** 4つのスプライト（トラック、フィル、つまみ、値テキスト）が自動生成されます。

---

### `ui_checkbox`

チェックボックスUIを作成します。

```aria
ui_checkbox id, x, y, "label", value
```

| 引数 | 型 | 説明 |
|------|-----|------|
| `id` | 整数 | チェックボックスID。check=id+1, label=id+2 が自動生成 |
| `x` | 整数 | X座標 |
| `y` | 整数 | Y座標 |
| `label` | 文字列 | ラベルテキスト |
| `value` | 整数 | 初期値（0=オフ、非0=オン） |

**例:**
```aria
ui_checkbox 100, 400, 300, "フルスクリーン", 1
```

---

## UIグループ・レイアウト

### `ui_group`

UIグループを作成または初期化します。

```aria
ui_group id
```

| 引数 | 型 | 説明 |
|------|-----|------|
| `id` | 整数 | グループID |

---

### `ui_group_add`

スプライトをグループに追加します。

```aria
ui_group_add group_id, sprite_id
```

| 引数 | 型 | 説明 |
|------|-----|------|
| `group_id` | 整数 | グループID |
| `sprite_id` | 整数 | 追加するスプライトID |

**例:**
```aria
ui_group 1
ui_group_add 1, 100
ui_group_add 1, 101
```

---

### `ui_group_clear`

グループ内の全スプライトを削除します。

```aria
ui_group_clear group_id
```

| 引数 | 型 | 説明 |
|------|-----|------|
| `group_id` | 整数 | グループID |

**注意:** グループ自体は残りますが、中のスプライトとボタン登録はすべて削除されます。

---

### `ui_group_show`

グループ内の全スプライトを表示します。

```aria
ui_group_show group_id
```

---

### `ui_group_hide`

グループ内の全スプライトを非表示にします。

```aria
ui_group_hide group_id
```

---

### `ui_layout`

グループのレイアウト方式を設定します。

```aria
ui_layout group_id, "mode"
```

| 引数 | 型 | 説明 |
|------|-----|------|
| `group_id` | 整数 | グループID |
| `mode` | 文字列 | `free` / `row` / `column` |

**注意:** `ui_pack` の実行時にこのレイアウトが適用されます。

---

### `ui_anchor`

スプライトのアンカー（基準点）を設定します。

```aria
ui_anchor sprite_id, "anchor"
```

| 引数 | 型 | 説明 |
|------|-----|------|
| `sprite_id` | 整数 | スプライトID |
| `anchor` | 文字列 | アンカー名 |

---

### `ui_pack`

グループ内のスプライトをレイアウトに従って整列します。

```aria
ui_pack group_id
```

| 引数 | 型 | 説明 |
|------|-----|------|
| `group_id` | 整数 | グループID |

**例:**
```aria
ui_layout 1, "row"
ui_pack 1
```

---

### `ui_style`

スプライトにプリセットスタイルを適用します。

```aria
ui_style sprite_id, "style"
```

| 引数 | 型 | 説明 |
|------|-----|------|
| `sprite_id` | 整数 | スプライトID |
| `style` | 文字列 | `mono` / `panel` |

**スタイル:**
- `mono`: 暗めの背景に白枠線、ホバーで明るく
- `panel`: メニュー設定に基づくパネルスタイル

---

### `ui_state`

スプライトの状態を設定します。

```aria
ui_state sprite_id, "state"
```

| 引数 | 型 | 説明 |
|------|-----|------|
| `sprite_id` | 整数 | スプライトID |
| `state` | 文字列 | `normal` / `hover` / `pressed` / `disabled` / `hidden` |

**注意:** `disabled` はボタン無効化と不透明度45%に、`hidden` は非表示にします。

---

### `ui_state_style`

特定の状態におけるスタイルを設定します。

```aria
ui_state_style sprite_id, "state", "property", value, ...
```

| 引数 | 型 | 説明 |
|------|-----|------|
| `sprite_id` | 整数 | スプライトID |
| `state` | 文字列 | `normal` / `hover` / `disabled` |
| `property` | 文字列 | プロパティ名 |
| `value` | 可変 | 設定値 |

**例:**
```aria
ui_state_style 100, "hover", "fill", "#3a3a5e"
ui_state_style 100, "disabled", "opacity", 0.3
```

---

## UIアニメーション・イベント

### `ui_tween`

スプライトのプロパティをTweenアニメーションします。

```aria
ui_tween sprite_id, "property", to_value, duration_ms, "ease"
```

| 引数 | 型 | 説明 |
|------|-----|------|
| `sprite_id` | 整数 | スプライトID |
| `property` | 文字列 | `x` / `y` / `scale_x` / `scale_y` / `opacity` / `alpha` |
| `to_value` | 数値 | 目標値 |
| `duration_ms` | 整数 | 時間（ミリ秒） |
| `ease` | 文字列 | `linear` / `in` / `out` / `out_cubic` / `inout` |

**例:**
```aria
ui_tween 100, "opacity", 0, 500, "out"
ui_tween 100, "x", 200, 300, "inout"
```

---

### `ui_fade`

スプライトをフェードイン/アウトします。

```aria
ui_fade sprite_id, opacity, duration_ms
```

| 引数 | 型 | 説明 |
|------|-----|------|
| `sprite_id` | 整数 | スプライトID |
| `opacity` | 数値 | 目標不透明度（0.0〜1.0 または 0〜255） |
| `duration_ms` | 整数 | 時間（ミリ秒） |

**例:**
```aria
ui_fade 100, 0, 500     ; フェードアウト
ui_fade 100, 1, 500     ; フェードイン
```

---

### `ui_move`

スプライトを移動アニメーションします。

```aria
ui_move sprite_id, x, y, duration_ms
```

| 引数 | 型 | 説明 |
|------|-----|------|
| `sprite_id` | 整数 | スプライトID |
| `x` | 整数 | 目標X座標 |
| `y` | 整数 | 目標Y座標 |
| `duration_ms` | 整数 | 時間（ミリ秒） |

**例:**
```aria
ui_move 100, 200, 300, 400
```

---

### `ui_scale`

スプライトをスケールアニメーションします。

```aria
ui_scale sprite_id, scale_x, scale_y, duration_ms
```

| 引数 | 型 | 説明 |
|------|-----|------|
| `sprite_id` | 整数 | スプライトID |
| `scale_x` | 数値 | X方向スケール |
| `scale_y` | 数値 | Y方向スケール |
| `duration_ms` | 整数 | 時間（ミリ秒） |

**例:**
```aria
ui_scale 100, 1.2, 1.2, 300
```

---

### `ui_on`

スプライトにイベントハンドラを設定します。

```aria
ui_on sprite_id, "event", "label"
```

| 引数 | 型 | 説明 |
|------|-----|------|
| `sprite_id` | 整数 | スプライトID |
| `event` | 文字列 | `click` または `hover` |
| `label` | 文字列 | ジャンプ先ラベル（`*label` 形式） |

**例:**
```aria
ui_on 100, "click", "*button_clicked"
```

**注意:** 現在 `click` と `hover` のみサポートされています。ボタンクリック時に指定ラベルへジャンプします。

---

### `ui_hotkey`

キーボードショートカットを設定します。

```aria
ui_hotkey "key", "label"
```

| 引数 | 型 | 説明 |
|------|-----|------|
| `key` | 文字列 | キー名 |
| `label` | 文字列 | ジャンプ先ラベル |

**例:**
```aria
ui_hotkey "escape", "*open_menu"
```

---

## 互換モード・テーマ

### `compat_mode`

NScripter互換の自動UI生成モードを切り替えます。

```aria
compat_mode on
compat_mode off
```

| 引数 | 型 | 説明 |
|------|-----|------|
| `mode` | 文字列 | `on` / `off` / `1` / `true` / `legacy` |

**注意:**
- `on`: `text` / `choice` / `yesnobox` / `mesbox` で自動的にUIを生成します。
- `off`: 手動で `lsp` / `spbtn` / `btnwait` を使ってUIを構築する必要があります。

---

### `textmode`

テキスト表示モードを設定します。

```aria
textmode "adv"
textmode "nvl"
textmode "manual"
```

| 引数 | 型 | 説明 |
|------|-----|------|
| `mode` | 文字列 | `adv` / `nvl` / `manual` |

**モード:**
- `adv`: 画面下部のテキストボックス（デフォルト）。位置 50,500 サイズ 1180x200。
- `nvl`: 画面全体のテキストボックス。上下に余白を持つ全画面表示。
- `manual`: 手動レイアウトモード。テキストボックス非表示、ユーザーの `lsp_text` + `text_target` を使用。

---

### `ui_theme`

UIテーマを適用します。

```aria
ui_theme "theme_name"
```

| 引数 | 型 | 説明 |
|------|-----|------|
| `theme_name` | 文字列 | テーマ名 |

---

### `ui_quality`

UI描画品質を設定します。

```aria
ui_quality "mode"
```

| 引数 | 型 | 説明 |
|------|-----|------|
| `mode` | 文字列 | `ultra` / `high` / `balanced` / `standard` / `performance` / `fast` |

**モード:**
- `ultra`: 最高品質。Trilinearフィルタ、96セグメント角丸、滑らかなモーション。
- `high`: 高品質（デフォルト）。Bilinearフィルタ、64セグメント角丸。
- `balanced` / `standard` / `normal`: 標準品質。48セグメント角丸。
- `performance` / `fast`: 高速描画。24セグメント角丸、低品質テクスチャ。

---

### `ui_motion`

UIモーション（滑らかな動き）の有効/無効を設定します。

```aria
ui_motion on
ui_motion off
ui_motion "smooth", 14
```

| 引数 | 型 | 説明 |
|------|-----|------|
| `mode` | 文字列 | `on` / `off` / `simple` / `smooth` |
| `response` | 数値 | （オプション）応答速度（1〜40）。小さいほど滑らか |

**例:**
```aria
ui_motion "smooth", 10
```

---

## 実践的な例

### 手動UI構築（compat_mode off）

```aria
compat_mode off

; テキスト出力先を作成
lsp_text 100, "", 50, 520
text_target 100

; テキストボックス背景
lsp_rect 200, 40, 500, 1200, 200
sp_fill 200, "#1a1a2e", 200
sp_round 200, 12
sp_border 200, "#3a3a5e", 2

text "手動レイアウトのテキスト表示です"
wait
```

### 選択肢の手動作成

```aria
compat_mode off

; ボタン背景
ui_rect 100, 440, 250, 400, 60
ui_rect 200, 440, 330, 400, 60

; ボタンラベル
ui_text 101, "はい", 640, 265
ui_text 201, "いいえ", 640, 345

; ボタン設定
ui_button 100, 1
ui_button 200, 2

btnwait %result
if %result == 1
    text "はいが選ばれました"
elseif %result == 2
    text "いいえが選ばれました"
endif
```

### UIグループでメニューを作成

```aria
; メニュー項目をグループ化
ui_group 1

ui_rect 100, 440, 250, 400, 60
ui_text 101, "セーブ", 640, 265
ui_button 100, 1
ui_group_add 1, 100
ui_group_add 1, 101

ui_rect 200, 440, 330, 400, 60
ui_text 201, "ロード", 640, 345
ui_button 200, 2
ui_group_add 1, 200
ui_group_add 1, 201

; 一括で非表示
ui_group_hide 1
```

### テキストエフェクトの設定

```aria
ui "text", "shadow", 2, 2, "#000000"
ui "text", "outline", 1, "#ffffff"
ui "text", "effect", "shake", 0.5, 2.0
```
