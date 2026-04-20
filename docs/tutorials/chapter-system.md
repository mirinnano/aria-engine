# チャプターシステム

このチュートリアルでは、AriaEngineを使用してチャプターシステムを実装する方法を説明します。

## ステップ1: チャプターの基本概念

チャプターシステムとは、ゲームを複数の章（チャプター）に分けて管理するシステムです。これにより、以下のことが可能になります：

- ゲームの進行度管理
- チャプターのアンロック機能
- チャプター選択画面
- 進行度の保存とロード

## ステップ2: チャプターの定義

### 基本的なチャプター定義

まず、スクリプト内でチャプターを定義します。

```aria
; チャプター1の定義
defchapter
    chapter_id "chapter_1"
    chapter_title "第1章: はじまり"
    chapter_desc "物語の始まり"
    chapter_script "assets/scripts/chapters/chapter1.aria"
endchapter

; チャプター2の定義
defchapter
    chapter_id "chapter_2"
    chapter_title "第2章: 冒険"
    chapter_desc "新しい世界へ"
    chapter_script "assets/scripts/chapters/chapter2.aria"
endchapter

; チャプター3の定義
defchapter
    chapter_id "chapter_3"
    chapter_title "第3章: 決戦"
    chapter_desc "最後の戦い"
    chapter_script "assets/scripts/chapters/chapter3.aria"
endchapter
```

### チャプターファイルの作成

各チャプターのスクリプトファイルを作成します。

**assets/scripts/chapters/chapter1.aria**:
```aria
*chapter_start
    ; 背景表示
    lsp 10, "assets/bg/forest.png", 0, 0
    vsp 10, on

    ; キャラクター表示
    lsp 20, "assets/ch/mio.png", 800, 0
    vsp 20, on

    ; 会話
    ミオ「こんにちは、世界へようこそ！」

    ; チャプター完了フラグ
    set_flag "chapter_1_completed", 1

    ; チャプター2をアンロック
    set_flag "chapter_2_unlocked", 1

    ; 次のチャプターへ
    text "第1章完了"
    goto *title_screen
```

## ステップ3: チャプター選択画面

### 基本的なチャプター選択画面

```aria
*chapter_select
    ; 背景
    bg "#1a1a2e", 2

    ; タイトル
    lsp_text 100, "チャプター選択", 640, 100
    sp_fontsize 100, 48
    sp_text_align 100, "center"
    sp_color 100, "#ffffff"

    ; 戻るボタン
    lsp_rect 199, 440, 650, 400, 60
    sp_fill 199, "#2a2a3e", 255
    sp_round 199, 10
    sp_hover_color 199, "#3a3a5e"
    sp_isbutton 199, true
    spbtn 199, 0

    lsp_text 200, "戻る", 640, 665
    sp_text_align 200, "center"

    ; チャプター選択
    chapter_select %selected

    if %selected == 0
        goto *title_screen
    elseif %selected == 1
        goto *chapter_1
    elseif %selected == 2
        goto *chapter_2
    elseif %selected == 3
        goto *chapter_3
    endif
```

## ステップ4: アンロック機能の実装

### フラグによるアンロック管理

チャプターのアンロック状態をフラグで管理します。

```aria
*init_chapters
    ; チャプター1は常にアンロック
    set_flag "chapter_1_unlocked", 1
    set_flag "chapter_1_completed", 0

    ; チャプター2-3はロック
    set_flag "chapter_2_unlocked", 0
    set_flag "chapter_2_completed", 0
    set_flag "chapter_3_unlocked", 0
    set_flag "chapter_3_completed", 0

    return
```

### チャプターカードのアンロック状態表示

```aria
*chapter_card
    let %card_id, %1
    let %chapter_num, %2

    ; アンロック状態を確認
    if %chapter_num == 1
        let %is_unlocked, 1
    elseif %chapter_num == 2
        get_flag "chapter_2_unlocked", %is_unlocked
    elseif %chapter_num == 3
        get_flag "chapter_3_unlocked", %is_unlocked
    endif

    ; 完了状態を確認
    if %chapter_num == 1
        get_flag "chapter_1_completed", %is_completed
    elseif %chapter_num == 2
        get_flag "chapter_2_completed", %is_completed
    elseif %chapter_num == 3
        get_flag "chapter_3_completed", %is_completed
    endif

    ; カード背景
    let %x, 100 + (%chapter_num - 1) * 360
    let %y, 200

    lsp_rect %card_id, %x, %y, 300, 150

    if %is_unlocked == 1
        ; アンロック済み
        if %is_completed == 1
            ; 完了済み
            sp_fill %card_id, "#4a6fa5", 255
        else
            ; 未完了
            sp_fill %card_id, "#2a2a3e", 255
        endif

        ; ボタンとして設定
        sp_isbutton %card_id, true
        spbtn %card_id, %chapter_num
    else
        ; ロック中
        sp_fill %card_id, "#1a1a2e", 255
    endif

    sp_round %card_id, 10
    sp_border %card_id, "#4a4a6e", 2

    ; チャプタータイトル
    lsp_text %card_id + 100, "第${%chapter_num}章", %x + 150, %y + 30
    sp_fontsize %card_id + 100, 24
    sp_text_align %card_id + 100, "center"
    sp_color %card_id + 100, "#ffffff"

    ; ステータス表示
    if %is_completed == 1
        lsp_text %card_id + 200, "完了", %x + 150, %y + 80
        sp_fontsize %card_id + 200, 16
        sp_text_align %card_id + 200, "center"
        sp_color %card_id + 200, "#4a6fa5"
    elseif %is_unlocked == 1
        lsp_text %card_id + 200, "プレイ可能", %x + 150, %y + 80
        sp_fontsize %card_id + 200, 16
        sp_text_align %card_id + 200, "center"
        sp_color %card_id + 200, "#aaaaaa"
    else
        lsp_text %card_id + 200, "ロック中", %x + 150, %y + 80
        sp_fontsize %card_id + 200, 16
        sp_text_align %card_id + 200, "center"
        sp_color %card_id + 200, "#666666"
    endif

    return
```

## ステップ5: 完全なチャプター選択画面

### アンロック機能付きチャプター選択

```aria
*chapter_select_screen
    ; 背景
    bg "#1a1a2e", 2

    ; タイトル
    lsp_text 100, "チャプター選択", 640, 100
    sp_fontsize 100, 48
    sp_text_align 100, "center"
    sp_color 100, "#ffffff"
    sp_text_shadow 100, 3, 3, "#000000"

    ; 戻るボタン
    lsp_rect 199, 440, 650, 400, 60
    sp_fill 199, "#2a2a3e", 255
    sp_round 199, 10
    sp_border 199, "#4a4a6e", 2
    sp_hover_color 199, "#3a3a5e"
    sp_isbutton 199, true
    spbtn 199, 0

    lsp_text 200, "戻る", 640, 665
    sp_text_align 200, "center"
    sp_color 200, "#ffffff"

    ; チャプターカード作成
    gosub *chapter_card with 1, 1
    gosub *chapter_card with 2, 2
    gosub *chapter_card with 3, 3

    ; ボタン待機
    btnwait %result

    if %result == 0
        csp -1
        goto *title_screen
    elseif %result == 1
        csp -1
        goto *chapter_1
    elseif %result == 2
        ; アンロック確認
        get_flag "chapter_2_unlocked", %is_unlocked
        if %is_unlocked == 1
            csp -1
            goto *chapter_2
        else
            text "まだアンロックされていません"
            goto *chapter_select_screen
        endif
    elseif %result == 3
        ; アンロック確認
        get_flag "chapter_3_unlocked", %is_unlocked
        if %is_unlocked == 1
            csp -1
            goto *chapter_3
        else
            text "まだアンロックされていません"
            goto *chapter_select_screen
        endif
    endif
```

## ステップ6: 進行度管理

### チャプター完了時の処理

```aria
*chapter_1
    ; 背景表示
    lsp 10, "assets/bg/forest.png", 0, 0
    vsp 10, on

    ; キャラクター表示
    lsp 20, "assets/ch/mio.png", 800, 0
    vsp 20, on

    ; 会話
    ミオ「こんにちは、世界へようこそ！」

    ; イベント1
    text "イベント1が発生しました"
    wait 1000

    ; イベント2
    text "イベント2が発生しました"
    wait 1000

    ; チャプター完了フラグ
    set_flag "chapter_1_completed", 1

    ; 次のチャプターをアンロック
    set_flag "chapter_2_unlocked", 1

    ; 進行度を保存
    set_counter "current_chapter", 2

    ; チャプター完了メッセージ
    text "第1章完了！"
    wait 1000

    ; セーブ
    save 1

    ; タイトルへ戻る
    csp -1
    goto *title_screen
```

### 総進行度の表示

```aria
*show_progress
    ; 総チャプター数
    let %total_chapters, 3

    ; 完了チャプター数
    let %completed_chapters, 0

    ; チャプター1
    get_flag "chapter_1_completed", %is_completed
    if %is_completed == 1
        inc %completed_chapters
    endif

    ; チャプター2
    get_flag "chapter_2_completed", %is_completed
    if %is_completed == 1
        inc %completed_chapters
    endif

    ; チャプター3
    get_flag "chapter_3_completed", %is_completed
    if %is_completed == 1
        inc %completed_chapters
    endif

    ; 進行度計算
    let %progress, (%completed_chapters * 100) / %total_chapters

    ; 進行度表示
    text "進行度: ${%progress}%"
    text "完了チャプター: ${%completed_chapters}/${%total_chapters}"

    return
```

## ステップ7: チャプターサムネイル

### サムネイル画像の使用

チャプターカードにサムネイル画像を表示します。

```aria
*chapter_card_with_thumbnail
    let %card_id, %1
    let %chapter_num, %2

    ; サムネイル画像
    let %thumbnail_path, "assets/chapters/chapter${%chapter_num}_thumb.png"

    ; カード背景
    let %x, 100 + (%chapter_num - 1) * 360
    let %y, 200

    lsp_rect %card_id, %x, %y, 300, 200
    sp_fill %card_id, "#2a2a3e", 255
    sp_round %card_id, 10
    sp_border %card_id, "#4a4a6e", 2

    ; サムネイル表示
    lsp %card_id + 1000, %thumbnail_path, %x, %y
    sp_width %card_id + 1000, 300
    sp_height %card_id + 1000, 150
    sp_round %card_id + 1000, 10

    ; タイトル表示
    lsp_text %card_id + 2000, "第${%chapter_num}章", %x + 150, %y + 170
    sp_fontsize %card_id + 2000, 20
    sp_text_align %card_id + 2000, "center"
    sp_color %card_id + 2000, "#ffffff"

    return
```

## ステップ8: 自動セーブ機能

### チャプター開始時の自動セーブ

```aria
*auto_save_chapter
    let %current_chapter, %1

    ; チャプター番号を保存
    set_counter "auto_chapter", %current_chapter

    ; オートセーブ
    save 0

    text "オートセーブしました"

    return
```

### チャプター完了時の自動セーブ

```aria
*chapter_complete_auto_save
    let %completed_chapter, %1

    ; 完了チャプターを記録
    set_counter "last_completed_chapter", %completed_chapter

    ; オートセーブ
    save 0

    text "進行度を保存しました"

    return
```

## ベストプラクティス

1. **フラグの一貫性**: チャプターアンロックと完了フラグを明確に区別する
2. **進行度の保存**: 重要なポイントで自動セーブを行う
3. **ユーザーフィードバック**: アンロック時や完了時に明確なフィードバックを提供する
4. **バックアップセーブ**: チャプター開始前にセーブを作成する
5. **進行度の可視化**: ユーザーに進行度を明確に表示する

## トラブルシューティング

### チャプターがアンロックされない

**問題**: チャプター完了しても次のチャプターがアンロックされない

**解決策**:
1. フラグが正しく設定されているか確認
2. フラグ名のスペルを確認
3. セーブデータにフラグが保存されているか確認

### 進行度が保存されない

**問題**: チャプター完了後、進行度がリセットされる

**解決策**:
1. セーブが正しく行われているか確認
2. フラグとカウンターがセーブデータに含まれているか確認
3. ロード時にフラグが正しく復元されているか確認

### チャプター選択画面が正しく表示されない

**問題**: チャプターカードが正しく表示されない

**解決策**:
1. チャプター定義が正しく行われているか確認
2. カードIDの重複を確認
3. Zオーダーを確認

## 次のステップ

チャプターシステムをマスターしたら、次のチュートリアルに進みましょう：

- [セーブ/ロード実装](save-load.md) - 完全なセーブ/ロードシステム

## まとめ

このチュートリアルでは、以下のことを学びました：

1. チャプターの基本概念
2. チャプターの定義方法
3. チャプター選択画面の作成
4. アンロック機能の実装
5. フラグによるアンロック管理
6. 進行度の管理と表示
7. チャプターサムネイルの使用
8. 自動セーブ機能の実装

これで完全なチャプターシステムを実装できるようになりました！次はセーブ/ロードシステムに挑戦しましょう。
