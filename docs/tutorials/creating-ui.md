# UI作成 - タイトル画面とボタン

このチュートリアルでは、AriaEngineでタイトル画面を作成し、ボタンを配置してゲームを開始するまでの流れを学びます。

---

## 目次

1. [背景を作成する](#ステップ1-背景を作成する)
2. [ボタンを追加する](#ステップ2-ボタンを追加する)
3. [ホバーエフェクトを追加する](#ステップ3-ホバーエフェクトを追加する)
4. [クリックを処理する](#ステップ4-クリックを処理する)
5. [ゲームへ遷移する](#ステップ5-ゲームへ遷移する)

---

## ステップ1: 背景を作成する

タイトル画面の背景を設定します。画像か、色を塗りつぶした画面を使えます。

### 色で背景を作る

```aria
*title
    bg "#1a1a2e"
```

### 画像で背景を作る

```aria
*title
    bg "bg/title.png"
```

背景はスプライトID `0` に設定されます。次にタイトルロゴやテキストを重ねていきます。

### タイトルテキストを表示する

```aria
*title
    bg "#1a1a2e"

    ; タイトルテキスト
    lsp_text 100, "私のゲーム", 640, 200
    sp_fontsize 100, 64
    sp_text_align 100, "center"
    sp_color 100, "#ffffff"
    sp_text_shadow 100, 3, 3, "#000000"
```

| コマンド | 役割 |
|---------|------|
| `lsp_text` | テキストスプライトを作成 |
| `sp_fontsize` | フォントサイズを64pxに |
| `sp_text_align` | 水平方向を中央揃え |
| `sp_color` | 文字色を白に |
| `sp_text_shadow` | テキストに影を追加 |

---

## ステップ2: ボタンを追加する

ボタンは「矩形スプライト + テキストスプライト」の組み合わせで作ります。矩形がクリック領域になり、テキストが見た目のラベルになります。

### 1つのボタンを作る

```aria
*title
    bg "#1a1a2e"

    lsp_text 100, "私のゲーム", 640, 200
    sp_fontsize 100, 64
    sp_text_align 100, "center"
    sp_color 100, "#ffffff"

    ; --- スタートボタン ---
    ; ボタンの背景（矩形）
    lsp_rect 101, 440, 300, 400, 60
    sp_fill 101, "#4a6fa5", 255
    sp_round 101, 12
    sp_border 101, "#5a7fb5", 2

    ; ボタンのテキスト
    lsp_text 102, "スタート", 640, 330
    sp_fontsize 102, 24
    sp_text_align 102, "center"
    sp_color 102, "#ffffff"

    ; ボタンとして登録
    spbtn 101, 1
    spbtn 102, 1
```

| コマンド | 役割 |
|---------|------|
| `lsp_rect` | 矩形スプライトを作成（ID, X, Y, 幅, 高さ） |
| `sp_fill` | 塗りつぶし色と透明度を設定 |
| `sp_round` | 角丸の半径を設定（12px） |
| `sp_border` | 枠線の色と太さを設定 |
| `spbtn` | スプライトをボタン化し、結果値を割り当て |

**ポイント:** `spbtn` を矩形とテキストの両方に同じ結果値 `1` を割り当てると、ボタンの背景部分と文字部分のどちらをクリックしても同じ動作になります。

### シャドウを追加する

ボタンに奥行きを出すには、`sp_shadow` を使います。

```aria
    lsp_rect 101, 440, 300, 400, 60
    sp_fill 101, "#4a6fa5", 255
    sp_round 101, 12
    sp_border 101, "#5a7fb5", 2
    sp_shadow 101, 4, 4, "#000000", 150
```

| 引数 | 値 | 意味 |
|------|-----|------|
| offsetX | 4 | 影を右へ4pxずらす |
| offsetY | 4 | 影を下へ4pxずらす |
| color | `#000000` | 影の色は黒 |
| alpha | 150 | 影の透明度（0〜255） |

---

## ステップ3: ホバーエフェクトを追加する

マウスカーソルがボタンの上に来た時の見た目を変えることで、ユーザーに「これは押せる」と示せます。

### ホバー時の色変更

```aria
    lsp_rect 101, 440, 300, 400, 60
    sp_fill 101, "#4a6fa5", 255
    sp_round 101, 12
    sp_border 101, "#5a7fb5", 2
    sp_shadow 101, 4, 4, "#000000", 150

    ; ホバー時の色
    sp_hover_color 101, "#5a8fc5"
```

### ホバー時の拡大

```aria
    ; ホバー時に5%拡大
    sp_hover_scale 101, 1.05
```

### ホバー時のカーソル変更

```aria
    ; マウスカーソルを手の形に
    sp_cursor 101, "hand"
```

### 完全なホバーエフェクト例

```aria
    lsp_rect 101, 440, 300, 400, 60
    sp_fill 101, "#4a6fa5", 255
    sp_round 101, 12
    sp_border 101, "#5a7fb5", 2
    sp_shadow 101, 4, 4, "#000000", 150

    sp_hover_color 101, "#5a8fc5"
    sp_hover_scale 101, 1.05
    sp_cursor 101, "hand"

    spbtn 101, 1
```

---

## ステップ4: クリックを処理する

ボタンがクリックされたら、`btnwait` で結果を受け取り、`if` で分岐します。

### 基本的なクリック処理

```aria
*title
    bg "#1a1a2e"

    lsp_text 100, "私のゲーム", 640, 200
    sp_fontsize 100, 64
    sp_text_align 100, "center"
    sp_color 100, "#ffffff"

    lsp_rect 101, 440, 300, 400, 60
    sp_fill 101, "#4a6fa5", 255
    sp_round 101, 12
    sp_hover_color 101, "#5a8fc5"
    spbtn 101, 1

    lsp_text 102, "スタート", 640, 330
    sp_fontsize 102, 24
    sp_text_align 102, "center"
    sp_color 102, "#ffffff"
    spbtn 102, 1

    ; ボタンのクリックを待つ
    btnwait %result

    if %result == 1
        text "スタートが押されました"
    endif
```

### 複数のボタンを処理する

タイトル画面によくある「スタート」「ロード」「設定」「終了」の4ボタン例です。

```aria
*title
    bg "#1a1a2e"

    ; タイトル
    lsp_text 100, "私のゲーム", 640, 150
    sp_fontsize 100, 64
    sp_text_align 100, "center"
    sp_color 100, "#ffffff"
    sp_text_shadow 100, 3, 3, "#000000"

    ; ボタンを作成
    gosub *create_buttons

    ; クリック待ち
    btnwait %result

    if %result == 1
        goto *new_game
    elseif %result == 2
        goto *load_game
    elseif %result == 3
        goto *settings
    elseif %result == 4
        end
    endif

*create_buttons
    let %bw, 400
    let %bh, 60
    let %bx, 440
    let %by, 280
    let %gap, 80

    ; --- スタート ---
    let %y, %by
    lsp_rect 101, %bx, %y, %bw, %bh
    sp_fill 101, "#4a6fa5", 255
    sp_round 101, 12
    sp_border 101, "#5a7fb5", 2
    sp_shadow 101, 4, 4, "#000000", 150
    sp_hover_color 101, "#5a8fc5"
    sp_hover_scale 101, 1.05
    sp_cursor 101, "hand"
    spbtn 101, 1

    lsp_text 102, "スタート", 640, %y + 30
    sp_fontsize 102, 24
    sp_text_align 102, "center"
    sp_color 102, "#ffffff"
    spbtn 102, 1

    ; --- ロード ---
    let %y, %by + %gap
    lsp_rect 103, %bx, %y, %bw, %bh
    sp_fill 103, "#5a5a6e", 255
    sp_round 103, 12
    sp_border 103, "#6a6a7e", 2
    sp_shadow 103, 4, 4, "#000000", 150
    sp_hover_color 103, "#6a6a8e"
    sp_hover_scale 103, 1.05
    sp_cursor 103, "hand"
    spbtn 103, 2

    lsp_text 104, "ロード", 640, %y + 30
    sp_fontsize 104, 24
    sp_text_align 104, "center"
    sp_color 104, "#ffffff"
    spbtn 104, 2

    ; --- 設定 ---
    let %y, %by + %gap * 2
    lsp_rect 105, %bx, %y, %bw, %bh
    sp_fill 105, "#5a5a6e", 255
    sp_round 105, 12
    sp_border 105, "#6a6a7e", 2
    sp_shadow 105, 4, 4, "#000000", 150
    sp_hover_color 105, "#6a6a8e"
    sp_hover_scale 105, 1.05
    sp_cursor 105, "hand"
    spbtn 105, 3

    lsp_text 106, "設定", 640, %y + 30
    sp_fontsize 106, 24
    sp_text_align 106, "center"
    sp_color 106, "#ffffff"
    spbtn 106, 3

    ; --- 終了 ---
    let %y, %by + %gap * 3
    lsp_rect 107, %bx, %y, %bw, %bh
    sp_fill 107, "#8a4a5a", 255
    sp_round 107, 12
    sp_border 107, "#9a5a6a", 2
    sp_shadow 107, 4, 4, "#000000", 150
    sp_hover_color 107, "#9a5a6a"
    sp_hover_scale 107, 1.05
    sp_cursor 107, "hand"
    spbtn 107, 4

    lsp_text 108, "終了", 640, %y + 30
    sp_fontsize 108, 24
    sp_text_align 108, "center"
    sp_color 108, "#ffffff"
    spbtn 108, 4

    return

*new_game
    text "新しいゲームを開始します"
    end

*load_game
    text "ロード画面へ遷移します"
    end

*settings
    text "設定画面へ遷移します"
    end
```

---

## ステップ5: ゲームへ遷移する

タイトル画面からゲーム画面へ移る時は、フェードアウトで画面を暗くし、スプライトを削除してから新しい背景を表示します。

### フェードで遷移する

```aria
*title
    bg "#1a1a2e"

    ; （タイトルとボタンの作成は省略）
    gosub *create_buttons

    btnwait %result

    if %result == 1
        goto *start_game
    endif

*start_game
    ; 画面をフェードアウト
    fade_out 500
    await

    ; すべてのスプライトを削除
    csp -1

    ; ゲーム画面の背景を表示
    bg "bg/stage1.png"

    ; フェードイン
    fade_in 500
    await

    ; ゲーム開始
    text "第一章"
    end
```

| コマンド | 役割 |
|---------|------|
| `fade_out` | 画面を暗くする（引数はミリ秒） |
| `await` | アニメーション完了を待つ |
| `csp -1` | すべてのスプライトを削除 |
| `fade_in` | 画面を明るくする |

### ボタン状態をリセットする

新しい画面に移る前に、前の画面のボタン登録を解除すると安全です。

```aria
*start_game
    ; 前のボタン登録をすべて解除
    btn_clear_all

    fade_out 500
    await
    csp -1

    bg "bg/stage1.png"
    fade_in 500
    await
```

`btn_clear_all` はスプライト自体は削除しません。ボタンとしての機能だけを解除します。スプライトを消すには `csp` を使います。

---

## まとめ

このチュートリアルで学んだこと:

1. `bg` で背景を設定する
2. `lsp_rect` + `sp_fill`/`sp_round`/`sp_border` でボタン背景を作る
3. `lsp_text` でボタンラベルを作る
4. `spbtn` でスプライトをボタン化する
5. `sp_hover_color` / `sp_hover_scale` / `sp_cursor` でホバーエフェクトを追加する
6. `btnwait` でクリックを待ち、`if` で分岐する
7. `fade_out` / `csp -1` / `fade_in` で画面を遷移する

---

## トラブルシューティング

### ボタンをクリックしても反応しない

- `spbtn` でスプライトIDと結果値が正しく設定されているか確認
- テキストスプライトだけに `spbtn` を設定して、矩形に設定し忘れていないか確認
- 他のスプライトが上に重なっていないか確認（Z順を `sp_z` で調整）

### ホバーエフェクトが動かない

- `spbtn` でボタン登録されているか確認（`spbtn` なしではホバーは機能しません）
- `sp_hover_color` や `sp_hover_scale` の対象IDが正しいか確認

### テキストが表示されない

- `sp_color` で文字色が背景と被っていないか確認
- `sp_fontsize` でサイズが0になっていないか確認
- `sp_text_align` を使う時、X座標がテキストの中心になるように設定しているか確認

---

## 次のステップ

タイトル画面の作成ができたら、次のトピックに進みましょう:

- [スプライト・描画コマンドリファレンス](../reference/opcodes/sprite.md) - すべてのスプライトコマンド
- [ボタン・入力コマンドリファレンス](../reference/opcodes/button.md) - ボタンと入力の詳細
- [アニメーションリファレンス](../reference/opcodes/animation.md) - `amsp` / `afade` など
