# ボタン・入力コマンドリファレンス

スプライトをボタン化し、クリック入力を待ち受けるInputカテゴリのコマンドを解説します。

---

## 目次

- [ボタンIDシステム](#ボタンidシステム)
- [ボタン登録](#ボタン登録)
- [ボタン待機](#ボタン待機)
- [ボタン解除](#ボタン解除)
- [右クリックメニュー](#右クリックメニュー)

---

## ボタンIDシステム

AriaEngineのボタンシステムは「スプライトID」と「ボタン結果値」を分離しています。

| 概念 | 説明 |
|------|------|
| **スプライトID** | `lsp`/`lsp_rect`/`lsp_text`で作成したスプライトのID。描画・操作の対象 |
| **ボタン結果値** | `btnwait`が返す整数値。クリックされたボタンの識別子 |

### マッピングの仕組み

`SpriteButtonMap`（内部辞書）がスプライトID → ボタン結果値の対応を管理します。

- **`btn spriteId`** → `spriteId` を結果値として登録（`spriteId == 結果値`）
- **`spbtn spriteId, buttonId`** → `buttonId` を結果値として登録
- **同じ結果値を複数のスプライトに割り当て可能** → 例：「はい」ボタンと「OK」ボタンが両方とも結果値 `1` を返す

### btnwaitの結果格納

`btnwait`実行時、クリックされたボタンの結果値が以下に書き込まれます：

1. `btnwait`に指定したレジスタ（例：`%result`）
2. `%0`（互換性用）
3. `%r0`（互換性用）

```aria
btnwait %choice     ; %choice に結果値が入る
if %choice == 1
    text "ボタン1が押されました"
endif
```

### フォーカス・キーボード操作

`btnwait`待機中：
- **マウスホバー** → そのスプライトにフォーカスが移動
- **方向キー（↑↓←→）** → フォーカスを隣のボタンに移動
- **Tab / Shift+Tab** → フォーカスの前後移動
- **Enter / Space** → フォーカス中のボタンを決定
- 複数のボタンが重なっている場合、**Z順が高い方**が優先

---

## ボタン登録

### `btn`

スプライトをボタン化します。クリック時の結果値はスプライトID自身になります。

```aria
btn spriteId
```

| 引数 | 型 | 説明 |
|------|-----|------|
| `spriteId` | 整数 | ボタン化するスプライトのID |

**例:**
```aria
lsp_rect 100, 300, 400, 200, 80
btn 100           ; スプライト100をボタン化。クリックで結果値100
btnwait %result
```

**注意:** `btn`はスプライトのクリック領域をスプライト自身のサイズに合わせます。テキストスプライトの場合、最低50×30ピクセルの領域が確保されます。

---

### `btn_area`

スプライトをボタン化し、クリック領域を任意の矩形で指定します。

```aria
btn_area spriteId, x, y, width, height
```

| 引数 | 型 | 説明 |
|------|-----|------|
| `spriteId` | 整数 | ボタン化するスプライトのID |
| `x` | 整数 | クリック領域のXオフセット（スプライト左上からの相対座標） |
| `y` | 整数 | クリック領域のYオフセット（スプライト左上からの相対座標） |
| `width` | 整数 | クリック領域の幅 |
| `height` | 整数 | クリック領域の高さ |

**例:**
```aria
; 画面全体をクリック判定にする（背景スプライト0に設定）
btn_area 0, 0, 0, 1280, 720
btnwait %result

; スプライトの左半分だけをクリック領域にする
lsp_rect 100, 100, 100, 400, 300
btn_area 100, 0, 0, 200, 300
btnwait %result
```

**注意:** `btn_area`の結果値もスプライトID自身になります。結果値を変更したい場合は、`btn_area`の後に`spbtn`を追加してください。

---

### `spbtn`

スプライトをボタン化し、クリック時の結果値を指定します。最も一般的に使われるボタン登録コマンドです。

```aria
spbtn spriteId, buttonId
```

| 引数 | 型 | 説明 |
|------|-----|------|
| `spriteId` | 整数 | ボタン化するスプライトのID |
| `buttonId` | 整数 | `btnwait`に返す結果値 |

**例:**
```aria
; 選択肢を3つ作成
lsp_rect 10, 100, 200, 300, 60
spbtn 10, 1

lsp_rect 20, 100, 300, 300, 60
spbtn 20, 2

lsp_rect 30, 100, 400, 300, 60
spbtn 30, 3

btnwait %choice
if %choice == 1
    text "選択肢1を選びました"
elseif %choice == 2
    text "選択肢2を選びました"
else
    text "選択肢3を選びました"
endif
```

**注意:**
- 同じ`buttonId`を複数のスプライトに割り当てると、どれをクリックしても同じ結果値が返ります
- `spbtn`を呼ぶと、そのスプライトの既存のボタン結果値が上書きされます
- `spbtn`は内部的に`btn`と同様に`IsButton = true`を設定します

---

## ボタン待機

### `btnwait`

ボタンがクリックされるまでスクリプト実行を停止します。クリックされたボタンの結果値がレジスタに格納されます。

```aria
btnwait [%変数]
```

| 引数 | 型 | 説明 |
|------|-----|------|
| `%変数` | レジスタ | 結果値を格納するレジスタ。省略時は`%0` |

**例:**
```aria
; タイトル画面のボタン
lsp_text 101, "はじめから", 500, 300
spbtn 101, 1

lsp_text 102, "つづきから", 500, 380
spbtn 102, 2

btnwait %result
if %result == 1
    goto *prologue
elseif %result == 2
    goto *load_menu
endif
```

**注意:**
- `btnwait`中は`WaitingForButton`状態になり、テキスト表示などは停止します
- クリック判定はマウスの左クリックと、キーボードのEnter/Spaceの両方に対応
- `btnwait`開始時にオートセーブが実行されます
- 別名：`textbtnwait`

---

### `btntime`

`btnwait`のタイムアウト時間を設定します。指定時間内にクリックがない場合、自動的に復帰します。

```aria
btntime milliseconds
```

| 引数 | 型 | 説明 |
|------|-----|------|
| `milliseconds` | 整数 | タイムアウト時間（ミリ秒）。`0`でタイムアウト無効 |

**例:**
```aria
btntime 5000      ; 5秒でタイムアウト
btnwait %result
if %result == -1
    text "時間切れです"
else
    text "ボタン" + %result + "が押されました"
endif
```

**注意:**
- タイムアウト時、結果値として`-1`がレジスタに格納されます
- タイムアウト設定は一度有効にすると、`btnwait`実行後に自動的に`0`にリセットされます
- タイムアウト中もマウスホバーでボタンがフォーカスされます

---

## ボタン解除

### `btn_clear`

指定したスプライトのボタン状態を解除します。スプライト自体は削除されません。

```aria
btn_clear spriteId
```

| 引数 | 型 | 説明 |
|------|-----|------|
| `spriteId` | 整数 | ボタン状態を解除するスプライトのID |

**例:**
```aria
spbtn 100, 1
btnwait %result

; ボタン機能を解除して通常のスプライトに戻す
btn_clear 100
```

---

### `btn_clear_all`

すべてのスプライトのボタン状態を解除し、ボタン結果値のマッピングもすべて削除します。

```aria
btn_clear_all
```

**別名:** `btndef`

**例:**
```aria
; 前の画面で登録したボタンをすべて解除
btn_clear_all

; 新しい画面のボタンを登録
spbtn 200, 1
spbtn 201, 2
btnwait %result
```

**注意:**
- `btn_clear_all`はスプライト自体は削除しません（`csp -1`とは異なります）
- フォーカス中のボタンIDもリセットされます
- `btndef`という名前はNScripter互換の別名です

---

## 右クリックメニュー

### `rmenu`

右クリックメニューの内容を設定します。

```aria
rmenu *label
rmenu "表示名1", action1, "表示名2", action2, ...
```

| 引数 | 型 | 説明 |
|------|-----|------|
| `*label` | ラベル | 右クリック時にジャンプするラベル（1引数形式） |
| `"表示名"` | 文字列 | メニューに表示するテキスト（2引数×N形式） |
| `action` | 文字列 | 選択時の動作（2引数×N形式） |

**1引数形式（ラベルジャンプ）:**
```aria
rmenu *pause_menu

*pause_menu
    text "ゲームを一時停止しました"
    btnwait %0
```

**複数エントリ形式（メニュー表示）:**
```aria
rmenu "セーブ", save, "ロード", load, "回想", backlog, "スキップ", skip, "終了", end
```

**対応しているaction値:**

| action値 | 動作 |
|---------|------|
| `save` | セーブ画面を開く |
| `load` | ロード画面を開く |
| `backlog` | バックログを表示 |
| `skip` | スキップモードを切り替え |
| `settings` / `config` | 設定画面を開く |
| `reset` | ゲームをリセット |
| `end` / `quit` | ゲームを終了 |
| `*ラベル名` | 指定したラベルにジャンプ |

**注意:**
- 複数エントリ形式では、引数は「表示名, action」のペアで指定します
- actionに`*`で始まるラベルを指定すると、メニュー選択時にそのラベルにジャンプします
- `rmenu`は`Input`カテゴリに分類されています

---

## 総合例

```aria
*title
    bg "bg/title.png"
    btn_clear_all

    ; タイトルロゴ
    lsp_text 1, "My Visual Novel", 400, 100
    sp_color 1, "#ffffff"
    sp_fontsize 1, 48
    sp_text_shadow 1, 2, 2, "#000000"

    ; スタートボタン
    lsp_rect 10, 440, 280, 200, 50
    sp_fill 10, "#4a90d9", 200
    sp_round 10, 8
    lsp_text 11, "はじめから", 460, 292
    sp_color 11, "#ffffff"
    spbtn 10, 1
    spbtn 11, 1          ; テキスト部分も同じ結果値

    ; 続きからボタン
    lsp_rect 20, 440, 360, 200, 50
    sp_fill 20, "#5a5a5a", 200
    sp_round 20, 8
    lsp_text 21, "つづきから", 460, 372
    sp_color 21, "#ffffff"
    spbtn 20, 2
    spbtn 21, 2

    ; 設定ボタン
    lsp_rect 30, 440, 440, 200, 50
    sp_fill 30, "#5a5a5a", 200
    sp_round 30, 8
    lsp_text 31, "設定", 520, 452
    sp_color 31, "#ffffff"
    spbtn 30, 3
    spbtn 31, 3

    btnwait %choice

    if %choice == 1
        goto *prologue
    elseif %choice == 2
        goto *load_menu
    elseif %choice == 3
        goto *settings
    endif

*settings
    btn_clear_all
    text "設定画面（仮）"
    btnwait %0
    goto *title
```

---

## コマンド一覧

| コマンド | 別名 | 引数 | 説明 |
|---------|------|------|------|
| `btn` | - | `spriteId` | スプライトをボタン化（結果値＝スプライトID） |
| `btn_area` | - | `spriteId, x, y, w, h` | クリック領域を指定してボタン化 |
| `spbtn` | - | `spriteId, buttonId` | スプライトをボタン化し、結果値を指定 |
| `btnwait` | `textbtnwait` | `[%変数]` | ボタンクリックを待機 |
| `btntime` | - | `milliseconds` | ボタン待機のタイムアウト設定 |
| `btn_clear` | - | `spriteId` | 指定スプライトのボタン状態を解除 |
| `btn_clear_all` | `btndef` | - | 全スプライトのボタン状態を解除 |
| `rmenu` | - | `*label` または `"名前", action, ...` | 右クリックメニュー設定 |
