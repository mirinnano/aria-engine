# init.aria リファレンス

`init.aria` はエンジン起動時に最初に読み込まれ、実行される初期化スクリプトです。ウィンドウ設定、フォント、メインスクリプトパス、互換モードなど、エンジンの根本的な挙動を決定するコマンドのみを記述します。

## init-only コマンドとランタイムコマンドの違い

- **init-only コマンド**: `init.aria` 内でしか効果を発揮しない（またはそう設計された）コマンド。エンジンは `init.aria` の実行後にウィンドウやフォント、メインスクリプトを読み込むため、これらのコマンドをメインスクリプトやその他のスクリプトで使用しても期待通りの動作になりません。
- **ランタイムコマンド**: ゲーム中（メインスクリプトやサブルーチン）でも使用できるコマンド。`init.aria` で初期値を設定し、ゲーム中に変更することも可能です。

---

## コマンド一覧

### window

ウィンドウのサイズとタイトルを設定します。**init-only** です。幅と高さは `init.aria` 実行後に一度だけ読み込まれ、ゲーム中の変更は反映されません（タイトルのみ `window_title` / `caption` で変更可能）。

```aria
window <width>, <height>, "<title>"
```

| 引数 | 型 | 説明 |
|------|-----|------|
| `width` | int | ウィンドウ幅（ピクセル） |
| `height` | int | ウィンドウ高さ（ピクセル） |
| `title` | string | ウィンドウタイトル |

**デフォルト値**: 幅 `1280`、高さ `720`、タイトル `"AriaEngine"`

**例**:
```aria
window 1280, 720, "海風 -The Records of Autumn-"
```

---

### font

使用するフォントファイル（TTF）のパスを設定します。**init-only** です。`init.aria` 実行後にフォントが読み込まれます。

```aria
font "<path>"
```

| 引数 | 型 | 説明 |
|------|-----|------|
| `path` | string | TTFフォントファイルへのパス |

**デフォルト値**: 未設定（フォントが読み込まれず、警告が出ます）

**例**:
```aria
font "assets/fonts/NotoSansJP-Regular.ttf"
```

---

### font_atlas_size

フォントテクスチャアトラスのサイズを設定します。**init-only** です。大きな文字セットを使う場合は大きめの値にしてください。値は `8` ～ `512` の範囲にクランプされます。

```aria
font_atlas_size <size>
```

| 引数 | 型 | 説明 |
|------|-----|------|
| `size` | int | アトラスサイズ（ピクセル） |

**デフォルト値**: `256`

**例**:
```aria
font_atlas_size 256
```

---

### font_filter

フォントテクスチャのフィルタリングモードを設定します。**init-only** です。

```aria
font_filter "<mode>"
```

| 引数 | 型 | 説明 |
|------|-----|------|
| `mode` | string | `bilinear`（デフォルト）、`trilinear`、`point`、`anisotropic` |

**デフォルト値**: `"bilinear"`

- `bilinear`: バイリニア補間（標準）
- `trilinear` / `anisotropic`: トリリニア（アニソトロピックも同じ設定にフォールバック）
- `point`: ニアレストネイバー（ドット絵風）

**例**:
```aria
font_filter "bilinear"
```

---

### script

メインスクリプトファイルのパスを設定します。**init-only** です。`init.aria` 実行後にこのファイルが読み込まれ、ゲームのメインループが開始されます。

```aria
script "<path>"
```

| 引数 | 型 | 説明 |
|------|-----|------|
| `path` | string | メインスクリプト（`.aria`）へのパス |

**デフォルト値**: 未設定（エラーになります）

**例**:
```aria
script "assets/scripts/main.aria"
```

---

### debug

デバッグモードの有効/無効を設定します。`init.aria` で設定するのが一般的ですが、**ランタイムでも変更可能**です。

```aria
debug <on|off>
```

| 引数 | 型 | 説明 |
|------|-----|------|
| `on` / `off` | string | `on` でデバッグモード有効 |

**デフォルト値**: `off`

デバッグモード有効時（`F3` キーでも切り替え可能）:
- FPSカウンター表示
- プログラムカウンタ（PC）表示
- スプライト数表示
- ボタンヒットエリアの赤枠表示

**例**:
```aria
debug on
```

---

### compat_mode

NScripter互換モードの有効/無効を設定します。`init.aria` で設定するのが一般的ですが、**ランタイムでも変更可能**です。

```aria
compat_mode <on|off>
```

| 引数 | 型 | 説明 |
|------|-----|------|
| `on` / `off` | string | `on` で互換モード有効 |

**デフォルト値**: `off`

**互換モード (`on`) の挙動**:
- `text` コマンド実行時に、テキストボックスの背景スプライトを自動生成します
- `choice` コマンドで文字列引数を受け取り、選択肢のスプライトを自動生成します
- `yesnobox` / `mesbox` の自動UI生成が有効になります
- 互換モード `off` では、これらのUIは `lsp` / `spbtn` / `btnwait` などで手動構築する必要があります

互換モードは、NScripterからの移植や従来のスクリプト資産をそのまま使いたい場合に有効です。新規プロジェクトでは `off` にして、AriaEngine のモダンなUIシステムを活用することを推奨します。

**例**:
```aria
compat_mode on
```

---

### textbox

テキストボックス（テキスト表示領域）の位置とサイズを設定します。**ランタイムでも変更可能**ですが、通常は `init.aria` で初期化します。

```aria
textbox <x>, <y>, <width>, <height>
```

| 引数 | 型 | デフォルト値 | 説明 |
|------|-----|-------------|------|
| `x` | int | `50` | X座標 |
| `y` | int | `500` | Y座標 |
| `width` | int | `1180` | 幅 |
| `height` | int | `200` | 高さ |

**例**:
```aria
textbox 50, 500, 1180, 200
```

---

### fontsize

デフォルトのテキストフォントサイズを設定します。**ランタイムでも変更可能**です。

```aria
fontsize <size>
```

| 引数 | 型 | デフォルト値 |
|------|-----|-------------|
| `size` | int | `32` |

**例**:
```aria
fontsize 32
```

---

### textcolor

デフォルトのテキスト色を設定します。**ランタイムでも変更可能**です。

```aria
textcolor "<color>"
```

| 引数 | 型 | デフォルト値 |
|------|-----|-------------|
| `color` | string（16進カラー） | `"#ffffff"` |

**例**:
```aria
textcolor "#ffffff"
```

---

### textbox_color

テキストボックス背景の色と不透明度を設定します。**ランタイムでも変更可能**です。

```aria
textbox_color "<color>", <alpha>
```

| 引数 | 型 | デフォルト値 | 説明 |
|------|-----|-------------|------|
| `color` | string（16進カラー） | `"#0b0d10"` | 背景色 |
| `alpha` | int（0～255） | `226` | 不透明度 |

**例**:
```aria
textbox_color "#0b0d10", 226
```

---

### textbox_style

テキストボックスの装飾スタイルを設定します。**ランタイムでも変更可能**です。

```aria
textbox_style <corner_radius>, <border_width>, "<border_color>", <border_opacity>, <padding_x>, <padding_y>, <shadow_offset_x>, <shadow_offset_y>, "<shadow_color>", <shadow_alpha>
```

| 引数 | 型 | デフォルト値 | 説明 |
|------|-----|-------------|------|
| `corner_radius` | int | `8` | 角丸半径 |
| `border_width` | int | `1` | 枠線幅 |
| `border_color` | string | `"#9aa18f"` | 枠線色 |
| `border_opacity` | int | `116` | 枠線不透明度 |
| `padding_x` | int | `30` | 水平内側余白 |
| `padding_y` | int | `22` | 垂直内側余白 |
| `shadow_offset_x` | int | `0` | 影のXオフセット |
| `shadow_offset_y` | int | `6` | 影のYオフセット |
| `shadow_color` | string | `"#000000"` | 影の色 |
| `shadow_alpha` | int | `150` | 影の不透明度 |

**例**:
```aria
textbox_style 8, 1, "#9aa18f", 116, 30, 22, 0, 6, "#000000", 150
```

---

### choice_style

`choice` コマンドで使用される選択肢ボタンのスタイルを設定します。**ランタイムでも変更可能**です。

```aria
choice_style <width>, <height>, <spacing>, <font_size>, "<bg_color>", <bg_alpha>, "<text_color>", <corner_radius>, "<border_color>", <border_width>, <border_opacity>, "<hover_color>", <padding_x>
```

| 引数 | 型 | デフォルト値 | 説明 |
|------|-----|-------------|------|
| `width` | int | `640` | 選択肢の幅 |
| `height` | int | `54` | 選択肢の高さ |
| `spacing` | int | `12` | 選択肢間の間隔 |
| `font_size` | int | `26` | フォントサイズ |
| `bg_color` | string | `"#11161a"` | 背景色 |
| `bg_alpha` | int | `232` | 背景不透明度 |
| `text_color` | string | `"#e7e2d6"` | テキスト色 |
| `corner_radius` | int | `6` | 角丸半径 |
| `border_color` | string | `"#9aa18f"` | 枠線色 |
| `border_width` | int | `1` | 枠線幅 |
| `border_opacity` | int | `118` | 枠線不透明度 |
| `hover_color` | string | `"#252a2f"` | ホバー時の色 |
| `padding_x` | int | `22` | 水平内側余白 |

**例**:
```aria
choice_style 640, 54, 12, 26, "#11161a", 232, "#e7e2d6", 6, "#9aa18f", 1, 118, "#252a2f", 22
```

---

## 完全な設定例

```aria
; ウィンドウ設定（init-only）
window 1280, 720, "海風 -The Records of Autumn-"

; NScripter互換モード
compat_mode on

; フォント設定（init-only）
font "assets/fonts/NotoSansJP-Regular.ttf"
font_atlas_size 256
font_filter "bilinear"

; メインスクリプト（init-only）
script "assets/scripts/main.aria"

; デバッグモード
debug on

; テキストボックス設定（ランタイムでも変更可能）
textbox 50, 500, 1180, 200
fontsize 32
textcolor "#ffffff"
textbox_color "#0b0d10", 226
textbox_style 8, 1, "#9aa18f", 116, 30, 22, 0, 6, "#000000", 150
choice_style 640, 54, 12, 26, "#11161a", 232, "#e7e2d6", 6, "#9aa18f", 1, 118, "#252a2f", 22
```

---

## 注意事項

- `init.aria` はメインスクリプトより**前**に1回だけ実行されます。
- init-only コマンド（`window` のサイズ、`script`、`font` 系）をメインスクリプトで使用しても、エンジン初期化後に読み込まれた値は無視されるため、意図した動作になりません。
- `init.aria` 中で `text` コマンドを使用した場合、エンジン初期化中に表示されず、メインループ開始後の最初のフレームで描画される可能性があります。初期化スクリプトでは表示コマンドを避け、純粋に設定のみを記述してください。
