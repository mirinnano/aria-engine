# セーブ/ロード実装

このチュートリアルでは、AriaEngineを使用して完全なセーブ/ロードシステムを実装する方法を説明します。

## ステップ1: セーブデータの基本構造

AriaEngineのセーブデータには以下の情報が含まれます：

- ゲーム状態（現在のラベル、プログラムカウンタ）
- 変数の値（%0-%9, 文字列変数）
- フラグの状態
- カウンターの値
- スプライトの状態
- オーディオ状態

## ステップ2: 基本的なセーブ/ロード

### 手動セーブ

```aria
*gameplay
    ; ゲームプレイ中...

    ; 重要なイベントの前にセーブ
    text "重要なイベントが始まります"
    save 1  ; スロット1にセーブ

    ; イベント実行
    text "イベント発生！"
```

### 手動ロード

```aria
*load_game
    ; スロット1からロード
    load 1

    ; ロード後の処理
    text "ロードしました"
```

## ステップ3: セーブ画面の作成

### 基本的なセーブ画面

```aria
*save_menu
    ; 背景
    bg "#1a1a2e", 2

    ; タイトル
    lsp_text 100, "セーブ", 640, 150
    sp_fontsize 100, 48
    sp_text_align 100, "center"
    sp_color 100, "#ffffff"

    ; セーブスロット作成
    gosub *create_save_slots

    ; 戻るボタン
    lsp_rect 199, 440, 650, 400, 60
    sp_fill 199, "#2a2a3e", 255
    sp_round 199, 10
    sp_hover_color 199, "#3a3a5e"
    sp_isbutton 199, true
    spbtn 199, 0

    lsp_text 200, "戻る", 640, 665
    sp_text_align 200, "center"

    ; ボタン待機
    btnwait %result

    if %result == 0
        csp -1
        return
    elseif %result >= 1 && %result <= 5
        ; セーブ実行
        save %result
        text "セーブしました（スロット${%result}）"
        wait 1000
        goto *save_menu
    endif
```

### セーブスロットの作成

```aria
*create_save_slots
    let %slot_width, 600
    let %slot_height, 60
    let %slot_x, 340
    let %slot_start_y, 200
    let %slot_spacing, 80

    ; 5つのスロットを作成
    for %i = 1 to 5
        let %y, %slot_start_y + (%i - 1) * %slot_spacing
        let %slot_id, %i * 10

        ; スロット背景
        lsp_rect %slot_id, %slot_x, %y, %slot_width, %slot_height
        sp_fill %slot_id, "#2a2a3e", 255
        sp_round %slot_id, 10
        sp_border %slot_id, "#4a4a6e", 2
        sp_hover_color %slot_id, "#3a3a5e"
        sp_isbutton %slot_id, true
        spbtn %slot_id, %i

        ; スロットラベル
        lsp_text %slot_id + 1, "セーブデータ ${%i}", %slot_x + 300, %y + 30
        sp_fontsize %slot_id + 1, 20
        sp_text_align %slot_id + 1, "center"
        sp_color %slot_id + 1, "#ffffff"
    next

    return
```

## ステップ4: ロード画面の作成

### 基本的なロード画面

```aria
*load_menu
    ; 背景
    bg "#1a1a2e", 2

    ; タイトル
    lsp_text 100, "ロード", 640, 150
    sp_fontsize 100, 48
    sp_text_align 100, "center"
    sp_color 100, "#ffffff"

    ; ロードスロット作成
    gosub *create_load_slots

    ; 戻るボタン
    lsp_rect 199, 440, 650, 400, 60
    sp_fill 199, "#2a2a3e", 255
    sp_round 199, 10
    sp_hover_color 199, "#3a3a5e"
    sp_isbutton 199, true
    spbtn 199, 0

    lsp_text 200, "戻る", 640, 665
    sp_text_align 200, "center"

    ; ボタン待機
    btnwait %result

    if %result == 0
        csp -1
        return
    elseif %result >= 1 && %result <= 5
        ; ロード実行
        load %result
        ; ロード後は自動的にゲーム続行
    endif
```

### ロードスロットの作成（セーブデータ情報表示）

```aria
*create_load_slots
    let %slot_width, 600
    let %slot_height, 60
    let %slot_x, 340
    let %slot_start_y, 200
    let %slot_spacing, 80

    ; 5つのスロットを作成
    for %i = 1 to 5
        let %y, %slot_start_y + (%i - 1) * %slot_spacing
        let %slot_id, %i * 10

        ; スロット背景
        lsp_rect %slot_id, %slot_x, %y, %slot_width, %slot_height
        sp_fill %slot_id, "#2a2a3e", 255
        sp_round %slot_id, 10
        sp_border %slot_id, "#4a4a6e", 2
        sp_hover_color %slot_id, "#3a3a5e"
        sp_isbutton %slot_id, true
        spbtn %slot_id, %i

        ; スロット情報（簡易版）
        lsp_text %slot_id + 1, "スロット ${%i}", %slot_x + 300, %y + 30
        sp_fontsize %slot_id + 1, 20
        sp_text_align %slot_id + 1, "center"
        sp_color %slot_id + 1, "#ffffff"
    next

    return
```

## ステップ5: セーブデータ情報の表示

### セーブデータのメタ情報

セーブデータに追加情報を保存するために、カウンターを使用します。

```aria
*save_with_info
    let %slot_num, %1

    ; 現在のチャプター
    get_counter "current_chapter", %current_chapter

    ; プレイ時間
    get_counter "play_time", %play_time

    ; セーブデータ情報を設定
    set_counter "save_slot_${%slot_num}_chapter", %current_chapter
    set_counter "save_slot_${%slot_num}_time", %play_time

    ; セーブ実行
    save %slot_num

    text "セーブしました"
    return
```

### セーブデータ情報の表示

```aria
*display_save_info
    let %slot_num, %1
    let %slot_id, %slot_num * 10

    ; セーブデータ情報を取得
    get_counter "save_slot_${%slot_num}_chapter", %chapter
    get_counter "save_slot_${%slot_num}_time", %time

    ; 時間を分:秒に変換
    let %minutes, %time / 60
    let %seconds, %time % 60

    ; 情報表示
    if %chapter == 0
        ; 空のスロット
        lsp_text %slot_id + 1, "スロット ${%slot_num} (空)", %slot_x + 300, %y + 30
    else
        ; データあり
        lsp_text %slot_id + 1, "スロット ${%slot_num}", %slot_x + 150, %y + 20
        lsp_text %slot_id + 2, "第${%chapter}章", %slot_x + 450, %y + 20
        lsp_text %slot_id + 3, "${%minutes}分${%seconds}秒", %slot_x + 450, %y + 45
    endif

    return
```

## ステップ6: オートセーブ

### オートセーブの実装

```aria
*auto_save_system
    ; オートセーブを有効化
    set_flag "auto_save_enabled", 1

    ; オートセーブカウンター
    set_counter "auto_save_counter", 0

    return

*check_auto_save
    ; オートセーブが有効か確認
    get_flag "auto_save_enabled", %auto_save

    if %auto_save == 0
        return
    endif

    ; カウンターを増加
    inc_counter "auto_save_counter", 1

    ; カウンターを取得
    get_counter "auto_save_counter", %counter

    ; 10回ごとにオートセーブ
    if %counter >= 10
        ; スロット0はオートセーブ用
        save 0

        ; カウンターをリセット
        set_counter "auto_save_counter", 0

        text "オートセーブしました"
    endif

    return
```

### ゲームプレイループでのオートセーブ

```aria
*gameplay_loop
    ; イベント実行
    text "イベントが発生しました"

    ; オートセーブチェック
    gosub *check_auto_save

    ; 次のイベントへ
    text "次のイベントへ..."

    ; オートセーブチェック
    gosub *check_auto_save

    ; ループ継続
    goto *gameplay_loop
```

## ステップ7: クイックセーブ/クイックロード

### クイックセーブ

```aria
*quick_save
    ; クイックセーブはスロット99を使用
    save 99

    text "クイックセーブしました"
    wait 500

    return
```

### クイックロード

```aria
*quick_load
    ; スロット99からロード
    load 99

    ; ロード後は自動的にゲーム続行
```

### キー割り当て（スクリプト内での処理）

```aria
*check_quick_save_load
    ; クイックセーブ（F5キーなど）
    ; 実際には入力処理で検知
    text "クイックセーブ: F5"
    text "クイックロード: F9"

    return
```

## ステップ8: セーブデータの管理

### セーブデータの削除

```aria
*delete_save_data
    let %slot_num, %1

    ; セーブデータを削除するために、空のデータで上書き
    ; （AriaEngineには直接削除コマンドがないため）
    set_counter "save_slot_${%slot_num}_chapter", 0
    set_counter "save_slot_${%slot_num}_time", 0

    ; 空のスロットとして上書きセーブ
    ; 実際の実装ではエンジン側の対応が必要

    text "スロット${%slot_num}を削除しました"
    return
```

### セーブデータのコピー

```aria
*copy_save_data
    let %source_slot, %1
    let %dest_slot, %2

    ; ソーススロットをロード
    load %source_slot

    ; デスティネーションスロットにセーブ
    save %dest_slot

    text "スロット${%source_slot}をスロット${%dest_slot}にコピーしました"
    return
```

## ステップ9: 完全なセーブ/ロードシステム

### 統合されたセーブ/ロードメニュー

```aria
*save_load_menu
    ; 背景
    bg "#1a1a2e", 2

    ; タイトル
    lsp_text 100, "セーブ / ロード", 640, 100
    sp_fontsize 100, 48
    sp_text_align 100, "center"
    sp_color 100, "#ffffff"

    ; タブボタン
    lsp_rect 201, 340, 160, 300, 50
    sp_fill 201, "#4a6fa5", 255
    sp_round 201, 10
    sp_hover_color 201, "#5a7fb5"
    sp_isbutton 201, true
    spbtn 201, 1

    lsp_text 202, "セーブ", 490, 185
    sp_text_align 202, "center"
    sp_color 202, "#ffffff"

    lsp_rect 203, 640, 160, 300, 50
    sp_fill 203, "#2a2a3e", 255
    sp_round 203, 10
    sp_hover_color 203, "#3a3a5e"
    sp_isbutton 203, true
    spbtn 203, 2

    lsp_text 204, "ロード", 790, 185
    sp_text_align 204, "center"
    sp_color 204, "#ffffff"

    ; 戻るボタン
    lsp_rect 299, 440, 650, 400, 60
    sp_fill 299, "#2a2a3e", 255
    sp_round 299, 10
    sp_hover_color 299, "#3a3a5e"
    sp_isbutton 299, true
    spbtn 299, 0

    lsp_text 300, "戻る", 640, 665
    sp_text_align 300, "center"

    ; デフォルトでセーブタブ
    let %current_tab, 1

    ; タブ内容の表示
    gosub *show_tab_content

    ; ボタン待機
    *menu_loop
        btnwait %result

        if %result == 0
            csp -1
            return
        elseif %result == 1
            ; セーブタブ
            let %current_tab, 1
            gosub *update_tab_buttons
            gosub *show_tab_content
        elseif %result == 2
            ; ロードタブ
            let %current_tab, 2
            gosub *update_tab_buttons
            gosub *show_tab_content
        elseif %result >= 10 && %result <= 50
            ; スロット選択
            let %slot_num, (%result - 10) / 10

            if %current_tab == 1
                ; セーブ
                save %slot_num
                text "セーブしました（スロット${%slot_num}）"
                wait 500
            else
                ; ロード
                load %slot_num
                ; ロード後は自動的にゲーム続行
            endif
        endif

        goto *menu_loop

*update_tab_buttons
    if %current_tab == 1
        ; セーブタブがアクティブ
        sp_fill 201, "#4a6fa5", 255
        sp_fill 203, "#2a2a3e", 255
    else
        ; ロードタブがアクティブ
        sp_fill 201, "#2a2a3e", 255
        sp_fill 203, "#4a6fa5", 255
    endif
    return

*show_tab_content
    ; スロットの表示/非表示
    ; 実装ではスプライトの表示状態を切り替え
    return
```

## ベストプラクティス

1. **複数のセーブスロット**: ユーザーに選択肢を提供する
2. **オートセーブ**: 重要なポイントで自動的にセーブする
3. **セーブデータ情報**: チャプター、プレイ時間などを表示する
4. **クイックセーブ/ロード**: 頻繁に使用する機能をショートカットで
5. **セーブ確認**: 重要なデータの上書き前に確認を求める

## トラブルシューティング

### セーブデータが保存されない

**問題**: セーブしてもデータが保存されない

**解決策**:
1. セーブが有効になっているか確認（`saveon`）
2. スロット番号が正しいか確認
3. ディスク容量が十分か確認
4. 書き込み権限があるか確認

### ロード時にエラーが発生する

**問題**: ロードしようとするとエラーが発生する

**解決策**:
1. セーブデータが存在するか確認
2. セーブデータが破損していないか確認
3. スクリプトファイルが存在するか確認
4. アセットが存在するか確認

### オートセーブが動作しない

**問題**: オートセーブが実行されない

**解決策**:
1. オートセーブが有効になっているか確認
2. カウンターが正しく増加しているか確認
3. オートセーブのトリガー条件を確認

## 次のステップ

セーブ/ロードシステムをマスターしたら、次の機能に挑戦しましょう：

- [高度な機能](../scripting/advanced.md) - フラグシステム、カウンターシステム
- [アニメーション](../scripting/animations.md) - 高度なアニメーション効果

## まとめ

このチュートリアルでは、以下のことを学びました：

1. セーブデータの基本構造
2. 手動セーブ/ロードの実装
3. セーブ画面の作成
4. ロード画面の作成
5. セーブデータ情報の表示
6. オートセーブの実装
7. クイックセーブ/クイックロード
8. セーブデータの管理
9. 統合されたセーブ/ロードメニュー

これで完全なセーブ/ロードシステムを実装できるようになりました！次はより高度な機能に挑戦しましょう。
