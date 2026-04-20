# 制御構造

このドキュメントでは、AriaEngineのスクリプト言語(.aria)の制御構造について説明します。

## 条件分岐

### if文の基本構文

```aria
if 条件
    真の場合の処理
else
    偽の場合の処理
endif
```

### 条件の種類

#### 等値比較

```aria
let %value, 10

if %value == 10
    text "値は10です"
else
    text "値は10ではありません"
endif
```

#### 大小比較

```aria
let %score, 85

if %score >= 90
    text "ランクS！"
elseif %score >= 80
    text "ランクA"
elseif %score >= 70
    text "ランクB"
else
    text "ランクC"
endif
```

#### 複数条件

```aria
let %health, 100
let %has_item, 1

if %health > 50 && %has_item == 1
    text "回復アイテムを使います"
endif
```

### 条件ジャンプ

#### beq (Branch if Equal) - 等しい場合にジャンプ

```aria
let %status, 2
beq *status_2, %status  ; %statusが2ならジャンプ

*status_2
    text "ステータス2です"
```

#### bne (Branch if Not Equal) - 等しくない場合にジャンプ

```aria
let %choice, 3
bne *not_chosen, %choice  ; %choiceが3でないならジャンプ

*chosen
    text "選択肢3が選ばれました"

*not_chosen
    text "選択肢3は選ばれませんでした"
```

#### bgt (Branch if Greater Than) - 大きい場合にジャンプ

```aria
let %score, 75
bgt *high_score, %score  ; %scoreが75より大きいならジャンプ

*high_score
    text "ハイスコアです！"
```

#### blt (Branch if Less Than) - 小さい場合にジャンプ

```aria
let %health, 30
blt *danger, %health  ; %healthが30より小さいならジャンプ

*danger
    text "危険な状態です！"
```

## ループ

### for文の基本構文

```aria
for %i = start to end
    繰り返し処理
next
```

### 基本的なforループ

```aria
for %i = 0 to 10
    text "${%i}回目の繰り返し"
next
```

### カウントダウン

```aria
for %i = 10 to 0
    text "残り${%i}回"
next
```

### ループ内でのbreak

```aria
for %i = 0 to 100
    text "${%i}回目"

    if %i == 5
        break  ; ループを終了
    endif
next
```

### ループ内でのcontinue（手動実装）

```aria
for %i = 0 to 10
    if %i % 2 == 1
        goto *continue  ; 奇数のスキップ
    endif

    text "${%i}は偶数です"

    *continue
next
```

## ジャンプ

### goto文 - 無条件ジャンプ

```aria
*main
    text "メイン処理"
    goto *end

*intermediate
    text "ここはスキップされます"

*end
    text "終了"
```

### jmpコマンド - 条件付きジャンプ

```aria
jmp *label_name
```

### 相対ジャンプ

```aria
jmp +5  ; 5行下にジャンプ
jmp -3  ; 3行上にジャンプ
```

## 待機とタイミング

### wait - ミリ秒待機

```aria
text "1秒待ちます..."
wait 1000
text "1秒経過しました"
```

### delay - ミリ秒待機（別名）

```aria
delay 500  ; 0.5秒待機
```

### reset_timer - タイマー初期化

```aria
reset_timer
```

### get_timer - タイマー取得

```aria
get_timer %elapsed
text "経過時間: ${%elapsed}ms"
```

### wait_timer - タイマー待機

```aria
reset_timer
text "3秒待ちます..."
wait_timer 3000
text "3秒経過しました"
```

## フロー制御の実践例

### ステート選択フロー

```aria
*select_state
    text "ステートを選択してください："
    text "1. 東京"
    text "2. 大阪"
    text "3. 名古屋"

    btnwait %choice

    if %choice == 1
        goto *tokyo
    elseif %choice == 2
        goto *osaka
    elseif %choice == 3
        goto *nagoya
    else
        text "無効な選択です"
        goto *select_state
    endif

*tokyo
    text "東京に向かいます"
    goto *start_game

*osaka
    text "大阪に向かいます"
    goto *start_game

*nagoya
    text "名古屋に向かいます"
    goto *start_game

*start_game
    text "ゲーム開始！"
```

### タイムアタックフロー

```aria
*game_loop
    reset_timer

    ; プレイヤーの入力待機
    btnwait %input
    get_timer %response_time

    if %response_time < 3000
        text "素早い反応です！+10ポイント"
        inc %score, 10
    else
        text "普通の反応です"
    endif

    text "現在スコア: ${%score}点"
    wait 1000

    ; ゲームオーバー判定
    if %score >= 100
        goto *game_clear
    endif

    goto *game_loop

*game_clear
    text "ゲームクリア！おめでとうございます！"
    end
```

### 状態遷移フロー

```aria
*game_start
    let %state, 0  ; 0: タイトル, 1: ゲーム中, 2: 結果

*title_loop
    text "タイトル画面"

    btnwait %input
    if %input == 1
        let %state, 1
        goto *game_loop
    endif

    goto *title_loop

*game_loop
    text "ゲームプレイ中"

    btnwait %input
    if %input == 2
        let %state, 0
        goto *title_loop
    endif

    goto *game_loop
```

## ベストプラクティス

1. **ネストの深さを制限**: 条件分岐やループのネストは深すぎないように
2. **ラベル名をわかりやすく**: `*state_1`, `*main_loop` など一貫性を保つ
3. **無限ループを回避**: ループには必ず終了条件を含める
4. **gotoの多用を避ける**: 可能な限り構造化された制御構文を使用
5. **条件を明確に**: 複雑な条件は変数に分けて可読性を向上

## トラブルシューティング

### 無限ループになった場合

```aria
; デバッグ用にループ回数を制限
let %counter, 0

for %i = 0 to 100
    inc %counter
    if %counter > 1000
        break
    endif

    text "処理中..."
next
```

### ジャンプ先が見つからない場合

```aria
; ラベル名のスペルミスに注意
goto *correct_label_name  ; 正しいラベル名を指定

*correct_label_name
    text "ここに来ます"
```

### 条件が正しく機能しない場合

```aria
; 比較演算子と変数の型を確認
let %value, 10
if %value == 10  ; == は文字列比較ではなく数値比較
    text "正しい条件です"
endif
```

## 次のステップ

- [スプライト操作](sprites.md) - ビジュアル要素の制御
- [アニメーション](animations.md) - 時間的な変化とエフェクト
- [UI要素](ui-elements.md) - ユーザーインターフェースの実装
- [高度な機能](advanced.md) - 複雑な制御フローの実装
