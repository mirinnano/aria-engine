# 高度な機能

このドキュメントでは、AriaEngineの高度な機能について説明します。

## チャプターシステム

### チャプターの定義

```aria
; チャプター定義の開始
defchapter

; チャプターID
chapter_id "chapter_1"

; チャプタータイトル
chapter_title "第1章: はじまり"

; チャプター説明
chapter_desc "物語の始まり"

; チャプタースクリプト
chapter_script "assets/scripts/chapters/chapter1.aria"

; チャプター定義の終了
endchapter
```

### 複数のチャプター定義

```aria
; チャプター1
defchapter
    chapter_id "chapter_1"
    chapter_title "第1章: はじまり"
    chapter_desc "物語の始まり"
    chapter_script "assets/scripts/chapters/chapter1.aria"
endchapter

; チャプター2
defchapter
    chapter_id "chapter_2"
    chapter_title "第2章: 冒険"
    chapter_desc "新しい世界へ"
    chapter_script "assets/scripts/chapters/chapter2.aria"
endchapter

; チャプター3
defchapter
    chapter_id "chapter_3"
    chapter_title "第3章: 決戦"
    chapter_desc "最後の戦い"
    chapter_script "assets/scripts/chapters/chapter3.aria"
endchapter
```

### チャプター選択画面

```aria
*chapter_select
    ; 背景表示
    lsp 10, "bg/menu.png", 0, 0

    ; タイトル
    lsp_text 20, "チャプター選択", 640, 100
    sp_fontsize 20, 48
    sp_text_align 20, "center"
    sp_color 20, "#ffffff"
    sp_text_shadow 20, 3, 3, "#000000"

    ; チャプター選択
    chapter_select %selected

    if %selected == 1
        goto *chapter_1
    elseif %selected == 2
        goto *chapter_2
    elseif %selected == 3
        goto *chapter_3
    endif
```

## フラグシステム

### フラグの設定

```aria
; フラグをtrueに設定
set_flag "game_started", 1

; フラグをfalseに設定
set_flag "game_started", 0
```

### フラグの取得

```aria
; フラグの値を取得
get_flag "game_started", %is_started

if %is_started == 1
    text "ゲームは開始されています"
else
    text "ゲームはまだ開始されていません"
endif
```

### フラグのクリア

```aria
; フラグをクリア（falseに設定）
clear_flag "game_started"
```

### フラグのトグル

```aria
; フラグを反転
toggle_flag "game_started"
```

### フラグの使用例

```aria
*game_start
    ; ゲーム開始フラグを設定
    set_flag "game_started", 1

    ; イベントフラグを設定
    set_flag "met_mio", 0
    set_flag "has_key", 0
    set_flag "door_unlocked", 0

*event_meet_mio
    ; ミオとの出会いイベント
    ミオ「はじめまして！」

    ; フラグを設定
    set_flag "met_mio", 1

*check_key
    ; 鍵を持っているか確認
    get_flag "has_key", %has_key

    if %has_key == 1
        text "鍵を持っています"
    else
        text "鍵を持っていません"
    endif

*door_event
    ; ドアの状態を確認
    get_flag "door_unlocked", %is_unlocked

    if %is_unlocked == 1
        text "ドアは開いています"
    else
        text "ドアは閉まっています"

        get_flag "has_key", %has_key
        if %has_key == 1
            text "鍵を使ってドアを開けます"
            set_flag "door_unlocked", 1
        else
            text "鍵が必要です"
        endif
    endif
```

## カウンターシステム

### カウンターの設定

```aria
; カウンターを設定
set_counter "score", 0
set_counter "health", 100
set_counter "money", 1000
```

### カウンターの増減

```aria
; カウンターを増加
inc_counter "score", 10

; カウンターを減少
dec_counter "health", 20
```

### カウンターの取得

```aria
; カウンターの値を取得
get_counter "score", %current_score
text "現在のスコア: ${%current_score}点"

get_counter "health", %current_health
text "現在のHP: ${%current_health}"
```

### 文字列補間と比較

`${...}` では整数レジスタ、文字列レジスタ、簡単な式を参照できます。

```aria
let $name, "ayu"
let %score, 10
text "name=${$name}, score=${%score + 5}"
```

文字列レジスタを比較する場合は、数値変換ではなく文字列として比較されます。

```aria
if $name == "ayu"
    text "matched"
endif
```

### カウンターの使用例

```aria
*battle_system
    ; HPとMPを初期化
    set_counter "player_hp", 100
    set_counter "player_mp", 50
    set_counter "enemy_hp", 200

*attack_turn
    ; 敵に攻撃
    dec_counter "enemy_hp", 25

    ; 敵のHPを確認
    get_counter "enemy_hp", %enemy_hp

    if %enemy_hp <= 0
        text "敵を倒しました！"
        inc_counter "score", 100
        goto *victory
    endif

    ; 敵の反撃
    dec_counter "player_hp", 15

    ; プレイヤーのHPを確認
    get_counter "player_hp", %player_hp

    if %player_hp <= 0
        text "あなたは倒されました..."
        goto *defeat
    endif

    goto *attack_turn

*victory
    text "勝利！"
    get_counter "score", %final_score
    text "最終スコア: ${%final_score}点"
    end

*defeat
    text "敗北..."
    end
```

## キャラクター管理

### キャラクターの表示

```aria
; キャラクターを表示
char_show "hero", "assets/ch/hero.png", 800, 0
```

### キャラクターの非表示

```aria
; キャラクターを非表示
char_hide "hero"
```

### キャラクターの表情変更

```aria
; 表情を変更
char_expression "hero", "smile"
```

### キャラクターの移動

```aria
; キャラクターを移動
char_move "hero", 100, 100
```

### キャラクター管理の使用例

```aria
*intro_scene
    ; 背景表示
    lsp 10, "bg/forest.png", 0, 0

    ; キャラクター表示
    char_show "mio", "assets/ch/mio.png", 800, 0

    ; 会話
    ミオ「こんにちは、主人公さん！」

    ; 表情を変更
    char_expression "mio", "smile"

    ミオ「今日はいい天気ですね」

    ; 別のキャラクター表示
    char_show "hero", "assets/ch/hero.png", 200, 0

    主人公「そうですね」

    ; キャラクターを非表示
    char_hide "mio"

    主人公「さようなら、ミオさん」

    char_hide "hero"
```

## 複雑なフラグ管理

### フラグによる分岐

```aria
*story_branch
    ; プレイヤーの選択によってフラグを設定
    text "どちらを選びますか？"
    text "1: 正直に言う"
    text "2: 隠す"

    btnwait %choice

    if %choice == 1
        set_flag "was_honest", 1
    else
        set_flag "was_honest", 0
    endif

*later_scene
    ; 後でフラグを確認
    get_flag "was_honest", %honest

    if %honest == 1
        ミオ「あなたは正直な人ですね」
    else
        ミオ「あなたは秘密を守る人ですね」
    endif
```

### 複数のフラグの組み合わせ

```aria
*ending_check
    ; エンディング判定
    get_flag "saved_all", %saved_all
    get_flag "found_secret", %found_secret
    get_flag "speed_clear", %speed_clear

    ; トゥルーエンディング
    if %saved_all == 1 && %found_secret == 1 && %speed_clear == 1
        goto *true_ending
    endif

    ; グッドエンディング
    if %saved_all == 1 && %found_secret == 1
        goto *good_ending
    endif

    ; ノーマルエンディング
    if %saved_all == 1
        goto *normal_ending
    endif

    ; バッドエンディング
    goto *bad_ending
```

## セーブ/ロードシステム

### セーブポイントの設定

```aria
; セーブポイント1
*save_point_1
    text "セーブポイント1"
    save 1

; セーブポイント2
*save_point_2
    text "セーブポイント2"
    save 2
```

### ロード

```aria
; セーブデータ1をロード
load 1
```

### セーブ/ロード画面

```aria
*save_menu
    ; 背景
    lsp 10, "bg/menu.png", 0, 0

    ; タイトル
    lsp_text 20, "セーブ", 640, 150
    sp_fontsize 20, 48
    sp_text_align 20, "center"

    ; セーブスロット
    for %i = 1 to 5
        let %y, 200 + (%i - 1) * 80

        lsp_rect %i * 10, 340, %y, 600, 60
        sp_fill %i * 10, "#2a2a3e", 255
        sp_round %i * 10, 10
        sp_border %i * 10, "#4a4a6e", 2
        sp_isbutton %i * 10, true
        spbtn %i * 10, %i

        lsp_text %i * 10 + 1, "セーブデータ ${%i}", 640, %y + 30
        sp_text_align %i * 10 + 1, "center"
    next

    ; 戻るボタン
    lsp_rect 60, 440, 650, 400, 60
    sp_fill 60, "#2a2a3e", 255
    sp_round 60, 10
    sp_border 60, "#4a4a6e", 2
    sp_isbutton 60, true
    spbtn 60, 0

    lsp_text 61, "戻る", 640, 665
    sp_text_align 61, "center"

    ; ボタン待機
    btnwait %result

    if %result >= 1 && %result <= 5
        save %result
        text "セーブしました"
    elseif %result == 0
        return
    endif

    goto *save_menu
```

## オートセーブ

### オートセーブの実装

```aria
*auto_save_system
    ; オートセーブを有効化
    set_flag "auto_save_enabled", 1

*gameplay_loop
    ; ゲームプレイ

    ; オートセーブチェック
    get_flag "auto_save_enabled", %auto_save

    if %auto_save == 1
        ; 定期的にオートセーブ
        get_counter "auto_save_counter", %counter
        inc_counter "auto_save_counter", 1

        get_counter "auto_save_counter", %new_counter

        if %new_counter >= 10
            save 0  ; セーブスロット0はオートセーブ用
            set_counter "auto_save_counter", 0
            text "オートセーブしました"
        endif
    endif

    goto *gameplay_loop
```

## 実践的な例

### 完全なゲームシステム

```aria
*init_game
    ; ゲーム初期化
    set_flag "game_started", 1
    set_counter "score", 0
    set_counter "health", 100
    set_counter "money", 0

    ; チャプター定義
    defchapter
        chapter_id "chapter_1"
        chapter_title "第1章: はじまり"
        chapter_desc "物語の始まり"
        chapter_script "assets/scripts/chapters/chapter1.aria"
    endchapter

    defchapter
        chapter_id "chapter_2"
        chapter_title "第2章: 冒険"
        chapter_desc "新しい世界へ"
        chapter_script "assets/scripts/chapters/chapter2.aria"
    endchapter

    goto *title_screen

*title_screen
    ; タイトル画面
    lsp 10, "bg/title.png", 0, 0

    lsp_text 20, "ゲームタイトル", 640, 200
    sp_fontsize 20, 64
    sp_text_align 20, "center"

    ; ボタン
    lsp_rect 30, 440, 300, 400, 60
    sp_fill 30, "#2a2a3e", 255
    sp_round 30, 10
    sp_isbutton 30, true
    spbtn 30, 1

    lsp_text 31, "スタート", 640, 315
    sp_text_align 31, "center"

    lsp_rect 40, 440, 380, 400, 60
    sp_fill 40, "#2a2a3e", 255
    sp_round 40, 10
    sp_isbutton 40, true
    spbtn 40, 2

    lsp_text 41, "チャプター選択", 640, 395
    sp_text_align 41, "center"

    lsp_rect 50, 440, 460, 400, 60
    sp_fill 50, "#2a2a3e", 255
    sp_round 50, 10
    sp_isbutton 50, true
    spbtn 50, 3

    lsp_text 51, "終了", 640, 475
    sp_text_align 51, "center"

    btnwait %result

    if %result == 1
        goto *new_game
    elseif %result == 2
        goto *chapter_select
    elseif %result == 3
        end
    endif

*new_game
    ; 新規ゲーム
    csp -1

    ; ゲーム開始
    lsp 10, "bg/forest.png", 0, 0
    afade 10, 255, 1000
    await

    char_show "mio", "assets/ch/mio.png", 800, 0

    ミオ「ようこそ、新しい世界へ！」

    ; 最初の選択肢
    text "どうしますか？"
    text "1: すぐに冒険に出る"
    text "2: まずは街を見回す"

    btnwait %choice

    if %choice == 1
        set_flag "chose_adventure", 1
        goto *adventure_route
    else
        set_flag "chose_adventure", 0
        goto *town_route
    endif

*adventure_route
    ; 冒険ルート
    text "冒険に出かけます..."

    inc_counter "score", 10
    set_flag "found_treasure", 0

    ; 宝探しイベント
    get_flag "found_treasure", %found

    if %found == 0
        text "宝箱を見つけました！"
        set_flag "found_treasure", 1
        inc_counter "money", 100
        inc_counter "score", 50
    endif

    goto *ending_check

*town_route
    ; 街ルート
    text "街を見回します..."

    inc_counter "score", 5

    ; 情報収集イベント
    text "情報を集めました"
    set_flag "gathered_info", 1

    goto *ending_check

*ending_check
    ; エンディング判定
    get_counter "score", %final_score
    get_flag "found_treasure", %found_treasure
    get_flag "gathered_info", %has_info

    text "最終スコア: ${%final_score}点"

    if %found_treasure == 1 && %has_info == 1
        text "ベストエンディング！"
    elseif %found_treasure == 1
        text "グッドエンディング！"
    elseif %has_info == 1
        text "ノーマルエンディング"
    else
        text "バッドエンディング"
    endif

    end
```

## ベストプラクティス

1. **フラグ名を明確に**: `met_mio`, `has_key` のように意味のある名前を使用
2. **カウンターの初期化**: ゲーム開始時にカウンターを初期化
3. **セーブポイントの配置**: 重要なイベントの前にセーブポイントを配置
4. **フラグのドキュメント**: 複雑なフラグシステムはコメントで説明
5. **エラーハンドリング**: フラグやカウンターの値を確認してから使用

## トラブルシューティング

### フラグが期待通りに動かない

1. フラグ名のスペルを確認
2. フラグが正しく設定されているか確認
3. フラグの値を取得してデバッグ

### カウンターが予期せず変化する

1. カウンター名の重複を確認
2. `inc_counter` や `dec_counter` の回数を確認
3. カウンターの初期化を確認

### セーブ/ロードが動かない

1. セーブスロットが正しいか確認
2. セーブデータの保存場所を確認
3. フラグとカウンターが正しく保存されているか確認

## 次のステップ

- [APIドキュメント](../api/opcodes.md) - 全オペコードの詳細リファレンス
- [アーキテクチャ](../architecture/overview.md) - エンジンの内部構造
