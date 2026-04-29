# チャプターシステム

このチュートリアルでは、AriaEngineのチャプターシステムを使ってゲームを章単位で管理する方法を説明します。チャプター選択画面、アンロック機能、進行度管理を段階的に実装します。

## ステップ1: チャプターの定義

### 基本的なチャプター定義

チャプターは `defchapter` 〜 `endchapter` のブロックで定義します。各チャプターにID、タイトル、説明、スクリプトパスを設定します。定義されたチャプターは `chapters.json` に自動保存されます。

```aria
; チャプター1の定義
defchapter
    chapter_id 1
    chapter_title "第一章 はじまり"
    chapter_desc "物語の始まり。新しい世界への第一歩。"
    chapter_script "assets/scripts/chapters/chapter1.aria"
endchapter

; チャプター2の定義
defchapter
    chapter_id 2
    chapter_title "第二章 冒険"
    chapter_desc "未知の世界へ踏み出す。"
    chapter_script "assets/scripts/chapters/chapter2.aria"
endchapter

; チャプター3の定義
defchapter
    chapter_id 3
    chapter_title "第三章 結末"
    chapter_desc "すべての謎が解き明かされる。"
    chapter_script "assets/scripts/chapters/chapter3.aria"
endchapter
```

### チャプター定義の配置場所

チャプター定義は通常、`init.aria` またはタイトル画面のスクリプトに記述します。ゲーム起動時に一度定義すれば、`chapters.json` から自動的に読み込まれます。

```aria
*init_chapters
    defchapter
        chapter_id 1
        chapter_title "第一章 はじまり"
        chapter_desc "物語の始まり"
        chapter_script "assets/scripts/chapters/chapter1.aria"
    endchapter

    defchapter
        chapter_id 2
        chapter_title "第二章 冒険"
        chapter_desc "新しい世界へ"
        chapter_script "assets/scripts/chapters/chapter2.aria"
    endchapter

    defchapter
        chapter_id 3
        chapter_title "第三章 決戦"
        chapter_desc "最後の戦い"
        chapter_script "assets/scripts/chapters/chapter3.aria"
    endchapter

    return
```

### チャプタースクリプトの作成

各チャプターのスクリプトファイルを作成します。

**assets/scripts/chapters/chapter1.aria**:
```aria
*chapter_start
    ; 背景表示
    bg "forest.png", 0

    ; 会話
    ミオ「こんにちは、世界へようこそ！」

    ; チャプター1を完了として記録
    set_pflag "chapter_1_completed", 1
    chapter_progress 1, 100

    ; チャプター2をアンロック
    unlock_chapter 2
    set_pflag "chapter_2_unlocked", 1

    text "第一章 完了！"
    goto *title_screen
```

## ステップ2: 自動生成チャプター選択画面

### chapter_select を使った簡易実装

`chapter_select` コマンドを使うと、登録済みのチャプターを縦に並べたカードUIが自動生成されます。スプライトID `2000` 〜 `2099` を使用するので、既存スプライトと競合しないよう注意してください。

```aria
*chapter_select_auto
    ; 画面をクリア
    csp -1
    bg "#1a1a2e", 0

    ; タイトル
    lsp_text 100, "チャプター選択", 640, 50
    sp_fontsize 100, 36
    sp_text_align 100, "center"
    sp_color 100, "#ffffff"

    ; 自動生成UI（スプライトID 2000〜2099を使用）
    chapter_select

    ; ボタン待機
    btnwait %selected

    if %selected == 1
        script "assets/scripts/chapters/chapter1.aria"
    elseif %selected == 2
        script "assets/scripts/chapters/chapter2.aria"
    elseif %selected == 3
        script "assets/scripts/chapters/chapter3.aria"
    endif
```

### 戻るボタンの追加

`chapter_select` はスプライトID 2000〜2099を占有するので、戻るボタンなどは別のID（例: 199）で事前に作成してください。

```aria
*chapter_select_with_back
    csp -1
    bg "#1a1a2e", 0

    ; タイトル
    lsp_text 100, "チャプター選択", 640, 50
    sp_fontsize 100, 36
    sp_text_align 100, "center"
    sp_color 100, "#ffffff"

    ; 戻るボタン（ID 199）
    lsp_rect 199, 440, 650, 400, 60
    sp_fill 199, "#2a2a3e", 255
    sp_round 199, 10
    sp_hover_color 199, "#3a3a5e"
    sp_isbutton 199, true
    spbtn 199, 0

    lsp_text 200, "戻る", 640, 665
    sp_text_align 200, "center"
    sp_color 200, "#ffffff"

    ; 自動生成UI（2000〜2099）
    chapter_select

    btnwait %selected

    if %selected == 0
        goto *title_screen
    elseif %selected == 1
        script "assets/scripts/chapters/chapter1.aria"
    elseif %selected == 2
        script "assets/scripts/chapters/chapter2.aria"
    elseif %selected == 3
        script "assets/scripts/chapters/chapter3.aria"
    endif
```

## ステップ3: カスタムチャプター選択画面

### chapter_card を使った手動配置

`chapter_card` を使うと、個別にカードを配置してカスタムレイアウトを構築できます。ただし、アンロック状態の判定や見た目の切り替えはスクリプト側で実装する必要があります。

```aria
*custom_chapter_select
    csp -1
    bg "#1a1a2e", 0

    ; タイトル
    lsp_text 100, "チャプター選択", 640, 50
    sp_fontsize 100, 36
    sp_text_align 100, "center"
    sp_color 100, "#ffffff"

    ; チャプター1（常にアンロック）
    chapter_card 200, "第一章 はじまり", "新しい世界への第一歩", 340, 180
    spbtn 200, 1

    ; チャプター2（アンロック状態で見た目変更）
    get_pflag "chapter_2_unlocked", %is_unlocked
    if %is_unlocked == 1
        chapter_card 203, "第二章 冒険", "未知の世界へ踏み出す", 340, 300
        spbtn 203, 2
    else
        chapter_card 203, "第二章 ？？？", "まだアンロックされていません", 340, 300
        sp_fill 203, "#1a1a1a", 255
        sp_color 204, "#555555"
        sp_color 205, "#444444"
    endif

    ; チャプター3
    get_pflag "chapter_3_unlocked", %is_unlocked
    if %is_unlocked == 1
        chapter_card 206, "第三章 結末", "すべての謎が解き明かされる", 340, 420
        spbtn 206, 3
    else
        chapter_card 206, "第三章 ？？？", "まだアンロックされていません", 340, 420
        sp_fill 206, "#1a1a1a", 255
        sp_color 207, "#555555"
        sp_color 208, "#444444"
    endif

    ; 戻るボタン
    lsp_rect 199, 440, 650, 400, 60
    sp_fill 199, "#2a2a3e", 255
    sp_round 199, 10
    sp_hover_color 199, "#3a3a5e"
    sp_isbutton 199, true
    spbtn 199, 0

    lsp_text 200, "戻る", 640, 665
    sp_text_align 200, "center"
    sp_color 200, "#ffffff"

    btnwait %selected

    if %selected == 0
        goto *title_screen
    elseif %selected == 1
        script "assets/scripts/chapters/chapter1.aria"
    elseif %selected == 2
        script "assets/scripts/chapters/chapter2.aria"
    elseif %selected == 3
        script "assets/scripts/chapters/chapter3.aria"
    endif
```

### サムネイル付きカスタムカード

`chapter_thumbnail` で設定した画像パスを参照し、スクリプト側で `lsp` を使ってサムネイルを表示できます。

```aria
*init_thumbnails
    chapter_thumbnail 1, "assets/chapters/chapter1_thumb.png"
    chapter_thumbnail 2, "assets/chapters/chapter2_thumb.png"
    chapter_thumbnail 3, "assets/chapters/chapter3_thumb.png"
    return

*chapter_card_with_thumbnail
    getparam %card_id
    getparam %chapter_num

    ; 座標計算
    let %x, 100 + (%chapter_num - 1) * 360
    let %y, 200

    ; カード背景
    lsp_rect %card_id, %x, %y, 300, 200
    sp_fill %card_id, "#2a2a3e", 255
    sp_round %card_id, 10
    sp_border %card_id, "#4a4a6e", 2

    ; サムネイル表示
    let %thumb_path, "assets/chapters/chapter${%chapter_num}_thumb.png"
    lsp %card_id + 1000, %thumb_path, %x, %y
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

## ステップ4: アンロック機能の実装

### セーブフラグを使ったアンロック管理

チャプターのアンロック状態はセーブフラグ (`pflag` / `sflag`) で管理します。セーブフラグは全セーブデータを通じて共有されるため、ゲーム再起動後も保持されます。

```aria
*init_unlock_flags
    ; チャプター1は常にアンロック
    set_pflag "chapter_1_unlocked", 1

    ; チャプター2〜3は最初はロック
    set_pflag "chapter_2_unlocked", 0
    set_pflag "chapter_3_unlocked", 0

    return
```

### チャプター完了時のアンロック

チャプターをクリアした際に、次のチャプターをアンロックします。

```aria
*chapter1_end
    ; チャプター1を完了として記録
    set_pflag "chapter_1_completed", 1
    chapter_progress 1, 100

    ; チャプター2をアンロック
    unlock_chapter 2
    set_pflag "chapter_2_unlocked", 1

    text "第一章 完了！チャプター2がアンロックされました。"
    goto *title_screen

*chapter2_end
    ; チャプター2を完了として記録
    set_pflag "chapter_2_completed", 1
    chapter_progress 2, 100

    ; チャプター3をアンロック
    unlock_chapter 3
    set_pflag "chapter_3_unlocked", 1

    text "第二章 完了！チャプター3がアンロックされました。"
    goto *title_screen
```

### unlock_chapter コマンドの使用

`unlock_chapter` は `ChapterManager` 内の該当チャプターの `IsUnlocked` を `true` に設定し、自動的に `chapters.json` へ保存します。

```aria
; チャプター2をアンロック
unlock_chapter 2

; 進行度も同時に更新
chapter_progress 2, 0
```

## ステップ5: 進行度管理

### chapter_progress で進行度を記録

`chapter_progress` は指定したチャプターの進行度（パーセンテージ）を更新します。範囲は 0 〜 100 に自動クリップされます。

```aria
*chapter1_playing
    ; イベント1
    text "イベント1が発生しました"
    chapter_progress 1, 30

    ; イベント2
    text "イベント2が発生しました"
    chapter_progress 1, 60

    ; イベント3
    text "イベント3が発生しました"
    chapter_progress 1, 90

    ; 最終イベント
    text "最終イベント"
    chapter_progress 1, 100

    goto *chapter1_end
```

### 総進行度の表示

全チャプターの完了状況を集計して、総進行度を表示します。

```aria
*show_total_progress
    let %total_chapters, 3
    let %completed_chapters, 0

    get_pflag "chapter_1_completed", %is_completed
    if %is_completed == 1
        inc %completed_chapters
    endif

    get_pflag "chapter_2_completed", %is_completed
    if %is_completed == 1
        inc %completed_chapters
    endif

    get_pflag "chapter_3_completed", %is_completed
    if %is_completed == 1
        inc %completed_chapters
    endif

    let %progress, (%completed_chapters * 100) / %total_chapters

    text "総進行度: ${%progress}%"
    text "完了: ${%completed_chapters} / ${%total_chapters} 章"

    return
```

## ステップ6: 完全なチャプター選択画面

### アンロック確認付きカスタム画面

以下は、ステップ1〜5の内容を統合した完全なチャプター選択画面の例です。

```aria
*chapter_select_screen
    csp -1
    bg "#1a1a2e", 0

    ; タイトル
    lsp_text 100, "チャプター選択", 640, 80
    sp_fontsize 100, 48
    sp_text_align 100, "center"
    sp_color 100, "#ffffff"
    sp_text_shadow 100, 3, 3, "#000000"

    ; チャプター1（常にアンロック）
    chapter_card 200, "第一章 はじまり", "物語の始まり", 340, 180
    spbtn 200, 1

    ; チャプター2
    get_pflag "chapter_2_unlocked", %is_unlocked
    if %is_unlocked == 1
        chapter_card 203, "第二章 冒険", "新しい世界へ", 340, 300
        spbtn 203, 2
    else
        chapter_card 203, "第二章 ？？？", "まだアンロックされていません", 340, 300
        sp_fill 203, "#1a1a1a", 255
        sp_color 204, "#555555"
        sp_color 205, "#444444"
    endif

    ; チャプター3
    get_pflag "chapter_3_unlocked", %is_unlocked
    if %is_unlocked == 1
        chapter_card 206, "第三章 決戦", "最後の戦い", 340, 420
        spbtn 206, 3
    else
        chapter_card 206, "第三章 ？？？", "まだアンロックされていません", 340, 420
        sp_fill 206, "#1a1a1a", 255
        sp_color 207, "#555555"
        sp_color 208, "#444444"
    endif

    ; 戻るボタン
    lsp_rect 199, 440, 650, 400, 60
    sp_fill 199, "#2a2a3e", 255
    sp_round 199, 10
    sp_border 199, "#4a4a6e", 2
    sp_hover_color 199, "#3a3a5e"
    sp_isbutton 199, true
    spbtn 199, 0

    lsp_text 201, "戻る", 640, 665
    sp_text_align 201, "center"
    sp_color 201, "#ffffff"

    ; ボタン待機
    btnwait %result

    if %result == 0
        csp -1
        goto *title_screen
    elseif %result == 1
        csp -1
        script "assets/scripts/chapters/chapter1.aria"
    elseif %result == 2
        ; アンロック再確認
        get_pflag "chapter_2_unlocked", %is_unlocked
        if %is_unlocked == 1
            csp -1
            script "assets/scripts/chapters/chapter2.aria"
        else
            text "まだアンロックされていません"
            goto *chapter_select_screen
        endif
    elseif %result == 3
        get_pflag "chapter_3_unlocked", %is_unlocked
        if %is_unlocked == 1
            csp -1
            script "assets/scripts/chapters/chapter3.aria"
        else
            text "まだアンロックされていません"
            goto *chapter_select_screen
        endif
    endif
```

### 完了状態を視覚的に表示

完了済みのチャプターにチェックマークや色分けを追加します。

```aria
*draw_chapter_status
    getparam %card_id
    getparam %chapter_num

    ; 完了状態を確認
    let %flag_name, "chapter_${%chapter_num}_completed"
    get_pflag %flag_name, %is_completed

    if %is_completed == 1
        ; 完了マーク（緑のチェック）
        lsp_text %card_id + 3000, "完了", %card_id + 280, %card_id + 230
        sp_fontsize %card_id + 3000, 14
        sp_color %card_id + 3000, "#4caf50"
    endif

    return
```

## ベストプラクティス

1. **チャプター定義は起動時に一度行う**: `init.aria` またはタイトル画面で `defchapter` を呼び出し、各チャプターの基本情報を登録します。
2. **アンロックと完了を区別する**: `chapter_X_unlocked` と `chapter_X_completed` を別々のフラグで管理し、状態を明確に分けます。
3. **`unlock_chapter` とフラグを併用する**: `unlock_chapter` は `chapters.json` に保存し、スクリプト内の分岐には `pflag` を使うと整合性が保ちやすいです。
4. **進行度は適切なタイミングで更新する**: イベントの区切りやセーブポイントで `chapter_progress` を呼び出し、プレイヤーの進行状況を記録します。
5. **スプライトIDの競合を避ける**: `chapter_select` は 2000 〜 2099 を使用します。カスタムUIではこの範囲を避け、かつ `chapter_card` の `+1` / `+2` も考慮してIDを割り当ててください。
6. **セーブフラグを優先する**: 章解放やCG解放など、ゲーム全体で共有すべき状態には `pflag` / `sflag` を使用します。`set_flag` でも動作しますが、`pflag` の方が意図が明確です。

## トラブルシューティング

### チャプターがアンロックされない

**問題**: チャプターをクリアしても次のチャプターがアンロックされない

**解決策**:
1. `unlock_chapter` のIDが正しいか確認する
2. `set_pflag` のフラグ名のスペルを確認する
3. `chapters.json` が生成されているか、該当チャプターの `IsUnlocked` が `true` になっているか確認する

### チャプター選択画面が表示されない

**問題**: `chapter_select` を呼んでもUIが表示されない、または既存スプライトが消える

**解決策**:
1. `defchapter` 〜 `endchapter` でチャプターが正しく定義されているか確認する
2. スプライトID 2000 〜 2099 を自分で使用していないか確認する
3. `csp -1` などで意図せずスプライトを消去していないか確認する

### chapter_card のテキストが表示されない

**問題**: `chapter_card` でカード背景は表示されるがテキストが表示されない

**解決策**:
1. `chapter_card` は背景（ID）、タイトル（ID+1）、説明（ID+2）の3つのスプライトを生成する。これらのIDが他のスプライトと競合していないか確認する
2. 説明が空文字列の場合、説明テキスト（ID+2）は生成されない

### 進行度が保存されない

**問題**: `chapter_progress` を呼んでも、ゲーム再起動後に進行度がリセットされる

**解決策**:
1. `chapter_progress` は `chapters.json` に自動保存される。ファイルの書き込み権限を確認する
2. エンジン終了時にも保存されるが、強制終了した場合は失われる可能性がある

## chapters.json の構造

チャプターデータはエンジンと同じディレクトリにある `chapters.json` に保存されます。

```json
{
  "Chapters": [
    {
      "Id": 1,
      "Title": "第一章 はじまり",
      "Description": "物語の始まり",
      "ScriptPath": "assets/scripts/chapters/chapter1.aria",
      "IsUnlocked": true,
      "ThumbnailPath": "assets/chapters/chapter1_thumb.png",
      "LastProgress": 100,
      "LastPlayed": "2026-04-29T10:00:00"
    }
  ]
}
```

ファイルが存在しない場合、エンジンは3つのデフォルトチャプターを自動生成します。

## 次のステップ

チャプターシステムをマスターしたら、次のチュートリアルに進みましょう。

- [セーブ/ロード実装](save-load.md) - 完全なセーブ/ロードシステム
- [UI作成](creating-ui.md) - タイトル画面とボタンの作成

## まとめ

このチュートリアルでは、以下のことを学びました。

1. `defchapter` 〜 `endchapter` でチャプターを定義する方法
2. `chapter_select` で自動生成チャプター選択画面を作る方法
3. `chapter_card` でカスタムレイアウトの選択画面を作る方法
4. `unlock_chapter` と `pflag` でアンロック機能を実装する方法
5. `chapter_progress` で進行度を管理する方法
6. カスタムカードにサムネイルや完了マークを追加する方法

これでチャプター選択画面とアンロック機能を持ったゲームが作れるようになりました。
