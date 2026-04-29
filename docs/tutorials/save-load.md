# セーブ/ロード実装

このチュートリアルでは、AriaEngineの組み込みセーブ/ロード機能を使って、ゲームにセーブ画面とロード画面を追加する方法を説明します。

## 前提知識

AriaEngineにはセーブ/ロード機能が組み込まれています。エンジンが自動的にゲーム状態（スクリプト位置、レジスタ、スプライト、オーディオなど）を記録するので、スクリプト側からは「いつ、どのスロットにセーブするか」を指示するだけです。セーブデータの書式やファイル操作を自分で実装する必要はありません。

使用するコマンドは5つだけです。

| コマンド | 機能 |
|---------|------|
| `saveon` | セーブを有効にする |
| `saveoff` | セーブを無効にする |
| `save` | 指定スロットにセーブする |
| `load` | 指定スロットからロードする |
| `saveinfo` | スロットのセーブ情報を取得する |

---

## ステップ1: セーブポイントを作る

ゲーム中の適切なタイミングでセーブを行うポイントを設けます。章の区切りや選択肢の直前などが一般的です。

### 基本のセーブ

```aria
*chapter1_start
    bg "room.png", 0
    textclear

    ミオ「ここから物語が始まります」

    ; セーブポイント
    save 1

    ミオ「セーブしました。続きを進めましょう」
```

`save 1` でスロット1にセーブします。セーブ時に画面のサムネイル画像が自動生成され、ゲーム状態が保存されます。

### セーブの有効/無効を切り替える

イベントシーン中など、セーブを禁止したい場面では `saveoff` を使います。

```aria
*important_event
    ; イベント中はセーブ不可
    saveoff

    bg "dramatic.png", 0
    textclear
    ミオ「これは重要なシーンです」

    ; イベント終了後にセーブを許可
    saveon
    save 1
```

初期状態ではセーブは有効なので、特に制限がなければ `saveon` を毎回呼ぶ必要はありません。

### スロット番号を変数で指定する

レジスタを使えば、プレイヤーが選んだスロット番号でセーブできます。

```aria
    let %slot, 3
    save %slot
```

---

## ステップ2: セーブ画面を実装する

ボタンを使ったセーブ画面を作ります。スロット情報を表示し、プレイヤーが選んだスロットにセーブします。

### シンプルなセーブ画面

```aria
*save_menu
    ; 背景とタイトル
    bg "#1a1a2e", 2

    lsp_text 100, "セーブ", 640, 100
    sp_fontsize 100, 48
    sp_text_align 100, "center"
    sp_color 100, "#ffffff"

    ; セーブスロットを5つ作成
    gosub *create_slots

    ; 戻るボタン
    lsp_rect 90, 440, 620, 400, 60
    sp_fill 90, "#2a2a3e", 255
    sp_round 90, 10
    sp_hover_color 90, "#3a3a5e"
    sp_isbutton 90, true
    spbtn 90, 0

    lsp_text 91, "戻る", 640, 650
    sp_text_align 91, "center"

    ; ボタン待機
    btnwait %result

    if %result == 0
        csp -1
        return
    elseif %result >= 1 && %result <= 5
        save %result
        text "セーブしました"
        wait 1000
        goto *save_menu
    endif
```

### スロットの表示

```aria
*create_slots
    let %w, 600
    let %h, 70
    let %x, 340
    let %start_y, 200
    let %gap, 90

    for %i = 1 to 5
        let %y, %start_y + (%i - 1) * %gap
        let %base_id, %i * 10

        ; スロット背景
        lsp_rect %base_id, %x, %y, %w, %h
        sp_fill %base_id, "#2a2a3e", 255
        sp_round %base_id, 10
        sp_border %base_id, "#4a4a6e", 2
        sp_hover_color %base_id, "#3a3a5e"
        sp_isbutton %base_id, true
        spbtn %base_id, %i

        ; スロット番号表示
        lsp_text %base_id + 1, "スロット ${%i}", %x + 300, %y + 35
        sp_fontsize %base_id + 1, 22
        sp_text_align %base_id + 1, "center"
        sp_color %base_id + 1, "#ffffff"
    next

    return
```

`saveinfo` を使うと、既存のセーブデータに日時とテキストプレビューを表示できます。

### セーブ情報を表示する

```aria
*create_slots_with_info
    let %w, 600
    let %h, 70
    let %x, 340
    let %start_y, 200
    let %gap, 90

    for %i = 1 to 5
        let %y, %start_y + (%i - 1) * %gap
        let %base_id, %i * 10

        ; スロット背景
        lsp_rect %base_id, %x, %y, %w, %h
        sp_fill %base_id, "#2a2a3e", 255
        sp_round %base_id, 10
        sp_border %base_id, "#4a4a6e", 2
        sp_hover_color %base_id, "#3a3a5e"
        sp_isbutton %base_id, true
        spbtn %base_id, %i

        ; セーブ情報を取得
        saveinfo %i, $preview, $datetime, %exists

        if %exists == 1
            ; データあり: 日時とプレビューを表示
            lsp_text %base_id + 1, "[${%i}] $datetime", %x + 20, %y + 20
            sp_fontsize %base_id + 1, 18
            sp_color %base_id + 1, "#aaaaaa"

            lsp_text %base_id + 2, "$preview", %x + 20, %y + 45
            sp_fontsize %base_id + 2, 16
            sp_color %base_id + 2, "#cccccc"
        else
            ; データなし
            lsp_text %base_id + 1, "[${%i}] 空のスロット", %x + 300, %y + 35
            sp_fontsize %base_id + 1, 22
            sp_text_align %base_id + 1, "center"
            sp_color %base_id + 1, "#666666"
        endif
    next

    return
```

`saveinfo` は4つの引数を取ります。スロット番号、プレビュー文字列、日時文字列、存在フラグの順です。存在フラグが1ならそのスロットにデータがあります。

---

## ステップ3: ロード画面を実装する

ロード画面もセーブ画面とほぼ同じ構造です。`load` コマンドでデータを読み込むと、ゲームはセーブ時の状態から自動的に再開します。

### シンプルなロード画面

```aria
*load_menu
    ; 背景とタイトル
    bg "#1a1a2e", 2

    lsp_text 100, "ロード", 640, 100
    sp_fontsize 100, 48
    sp_text_align 100, "center"
    sp_color 100, "#ffffff"

    ; ロードスロットを5つ作成
    gosub *create_load_slots

    ; 戻るボタン
    lsp_rect 90, 440, 620, 400, 60
    sp_fill 90, "#2a2a3e", 255
    sp_round 90, 10
    sp_hover_color 90, "#3a3a5e"
    sp_isbutton 90, true
    spbtn 90, 0

    lsp_text 91, "戻る", 640, 650
    sp_text_align 91, "center"

    ; ボタン待機
    btnwait %result

    if %result == 0
        csp -1
        return
    elseif %result >= 1 && %result <= 5
        ; データがあるか確認
        saveinfo %result, $preview, $datetime, %exists

        if %exists == 1
            load %result
            ; ロード後は自動的にゲーム再開
        else
            text "データがありません"
            wait 1000
            goto *load_menu
        endif
    endif
```

### ロードスロットの表示

```aria
*create_load_slots
    let %w, 600
    let %h, 70
    let %x, 340
    let %start_y, 200
    let %gap, 90

    for %i = 1 to 5
        let %y, %start_y + (%i - 1) * %gap
        let %base_id, %i * 10

        ; スロット背景
        lsp_rect %base_id, %x, %y, %w, %h
        sp_fill %base_id, "#2a2a3e", 255
        sp_round %base_id, 10
        sp_border %base_id, "#4a4a6e", 2
        sp_hover_color %base_id, "#3a3a5e"
        sp_isbutton %base_id, true
        spbtn %base_id, %i

        ; セーブ情報を取得
        saveinfo %i, $preview, $datetime, %exists

        if %exists == 1
            lsp_text %base_id + 1, "[${%i}] $datetime", %x + 20, %y + 20
            sp_fontsize %base_id + 1, 18
            sp_color %base_id + 1, "#aaaaaa"

            lsp_text %base_id + 2, "$preview", %x + 20, %y + 45
            sp_fontsize %base_id + 2, 16
            sp_color %base_id + 2, "#cccccc"
        else
            lsp_text %base_id + 1, "[${%i}] 空のスロット", %x + 300, %y + 35
            sp_fontsize %base_id + 1, 22
            sp_text_align %base_id + 1, "center"
            sp_color %base_id + 1, "#666666"
        endif
    next

    return
```

### ロード後の処理

`load` を実行すると、エンジンが次の順序で動作します。

1. セーブデータからゲーム状態を復元
2. スクリプト内に `*load_restore` ラベルがあれば、そこにジャンプ
3. なければ、セーブ時のスクリプト位置から自動的に再開

ロード後に特別な処理（フェードインやBGM再開など）を入れたい場合は `*load_restore` を使います。

```aria
*load_restore
    ; ロード後の共通処理
    fade_in 500
    await

    text "ロードしました"
    wait 500

    ; ゲームに戻る
    return
```

---

## ステップ4: タイトル画面に組み込む

セーブ画面とロード画面をタイトル画面やポーズメニューから呼び出せるようにします。

### タイトル画面の例

```aria
*title_screen
    bg "#1a1a2e", 2

    lsp_text 100, "私のゲーム", 640, 150
    sp_fontsize 100, 64
    sp_text_align 100, "center"
    sp_color 100, "#ffffff"

    ; スタートボタン
    lsp_rect 101, 440, 300, 400, 60
    sp_fill 101, "#4a6fa5", 255
    sp_round 101, 10
    sp_hover_color 101, "#5a7fb5"
    sp_isbutton 101, true
    spbtn 101, 1

    lsp_text 102, "スタート", 640, 330
    sp_text_align 102, "center"

    ; ロードボタン
    lsp_rect 103, 440, 380, 400, 60
    sp_fill 103, "#2a2a3e", 255
    sp_round 103, 10
    sp_hover_color 103, "#3a3a5e"
    sp_isbutton 103, true
    spbtn 103, 2

    lsp_text 104, "ロード", 640, 410
    sp_text_align 104, "center"

    btnwait %result

    if %result == 1
        csp -1
        goto *game_start
    elseif %result == 2
        goto *load_menu
    endif
```

### ポーズメニューから呼び出す

```aria
*pause_menu
    bg "#1a1a2e", 2

    lsp_text 100, "ポーズメニュー", 640, 150
    sp_fontsize 100, 36
    sp_text_align 100, "center"

    gosub *create_pause_buttons

    btnwait %result

    if %result == 1
        ; 再開
        csp -1
        return
    elseif %result == 2
        ; セーブ
        goto *save_menu
    elseif %result == 3
        ; ロード
        goto *load_menu
    elseif %result == 4
        ; タイトルへ
        csp -1
        goto *title_screen
    endif

*create_pause_buttons
    ; 再開
    lsp_rect 201, 440, 280, 400, 50
    sp_fill 201, "#4a6fa5", 255
    sp_round 201, 10
    sp_hover_color 201, "#5a7fb5"
    sp_isbutton 201, true
    spbtn 201, 1
    lsp_text 202, "再開", 640, 305
    sp_text_align 202, "center"

    ; セーブ
    lsp_rect 203, 440, 350, 400, 50
    sp_fill 203, "#2a2a3e", 255
    sp_round 203, 10
    sp_hover_color 203, "#3a3a5e"
    sp_isbutton 203, true
    spbtn 203, 2
    lsp_text 204, "セーブ", 640, 375
    sp_text_align 204, "center"

    ; ロード
    lsp_rect 205, 440, 420, 400, 50
    sp_fill 205, "#2a2a3e", 255
    sp_round 205, 10
    sp_hover_color 205, "#3a3a5e"
    sp_isbutton 205, true
    spbtn 205, 3
    lsp_text 206, "ロード", 640, 445
    sp_text_align 206, "center"

    ; タイトルへ
    lsp_rect 207, 440, 490, 400, 50
    sp_fill 207, "#2a2a3e", 255
    sp_round 207, 10
    sp_hover_color 207, "#3a3a5e"
    sp_isbutton 207, true
    spbtn 207, 4
    lsp_text 208, "タイトルへ", 640, 515
    sp_text_align 208, "center"

    return
```

---

## ステップ5: オートセーブを使う

`systemcall` でオートセーブを実行できます。スロット0はオートセーブ専用です。

```aria
*auto_save_example
    ; 重要な選択肢の直前にオートセーブ
    systemcall "autosave"

    text "重要な選択です"
    text "1: 左の道を進む"
    text "2: 右の道を進む"

    btnwait %choice
    ; ...
```

オートセーブデータのロードは `systemcall "autoload"` で行えます。

```aria
    systemcall "autoload"
```

---

## ベストプラクティス

1. **セーブポイントの設置場所**: 章の区切り、選択肢の直前、プレイヤーが休憩しやすい場面に置く
2. **スロット数**: 3〜5スロットが一般的。多すぎると選びにくくなる
3. **セーブ情報の活用**: `saveinfo` で日時とプレビューを表示し、どのセーブか分かりやすくする
4. **空スロットの表示**: 空のスロットは色や文字で区別し、誤上書きを防ぐ
5. **ロード確認**: 既存データの上書きや、空スロットからのロードは確認を入れる
6. **イベント中の制限**: `saveoff` で重要シーン中のセーブを禁止し、体験を保護する

---

## トラブルシューティング

### セーブが実行されない

**問題**: `save` を実行してもデータが保存されない

**確認点**:
1. `saveon` が有効になっているか（初期状態は有効）
2. スロット番号が0以上の整数か
3. ディスクの空き容量は十分か

### ロード後に位置がおかしい

**問題**: ロード後、想定外の場所から再開する

**確認点**:
1. `*load_restore` ラベルが存在する場合、そこにジャンプする仕様がある
2. `*load_restore` で `return` や `goto` を正しく使っているか
3. セーブ時と同じスクリプトファイルが存在するか

### saveinfoで空文字が返る

**問題**: `saveinfo` の `$preview` や `$datetime` が空になる

**確認点**:
1. そのスロットに実際にセーブデータが存在するか（`%exists` を確認）
2. 存在しないスロットに対しては空文字列が返るのが仕様

---

## まとめ

このチュートリアルでは、以下のことを学びました。

1. セーブポイントの作成（`save` / `saveon` / `saveoff`）
2. セーブ画面の実装（ボタンと `saveinfo`）
3. ロード画面の実装（`load` と `*load_restore`）
4. タイトル画面やポーズメニューへの組み込み
5. オートセーブの利用（`systemcall "autosave"`）

AriaEngineの組み込み機能を使えば、セーブ/ロードシステムを一から実装する必要はありません。スクリプト側から「いつ、どのスロットにセーブするか」を指示するだけで、エンジンが状態の保存と復元を自動的に行います。

詳細なコマンド仕様は [システム・セーブコマンドリファレンス](../reference/opcodes/system.md) を参照してください。
