# スクリプト制御・フロー制御リファレンス

スクリプトの実行フローを制御するコマンドのリファレンスです。条件分岐、ループ、サブルーチン呼び出し、ジャンプなどを含みます。

---

## 目次

- [`if` — 条件分岐](#if--条件分岐)
- [`goto` / `jmp` — 無条件ジャンプ](#goto--jmp--無条件ジャンプ)
- [`cmp` — 比較](#cmp--比較)
- [`beq` / `bne` / `bgt` / `blt` — 条件付きジャンプ](#beq--bne--bgt--blt--条件付きジャンプ)
- [`while` / `wend` — ループ](#while--wend--ループ)
- [`break` / `continue` — ループ制御](#break--continue--ループ制御)
- [`defsub` / `sub` — サブルーチン定義](#defsub--sub--サブルーチン定義)
- [`gosub` / `call` — サブルーチン呼び出し](#gosub--call--サブルーチン呼び出し)
- [`return` / `ret` — サブルーチンから復帰](#return--ret--サブルーチンから復帰)
- [`returnvalue` — 戻り値を設定](#returnvalue--戻り値を設定)
- [`getparam` — 引数を受け取る](#getparam--引数を受け取る)
- [`alias` / `numalias` — 定数エイリアス](#alias--numalias--定数エイリアス)
- [`include` — スクリプト読み込み](#include--スクリプト読み込み)

---

## `if` — 条件分岐

条件に応じて処理を分岐します。1行ifとブロックifの2種類があります。

### 1行if（インラインif）

条件が真の場合、1つのコマンドを実行します。

```aria
if 条件 コマンド 引数...
```

**例:**
```aria
if %0 == 1 text "変数は1です"
if %health > 0 goto *alive
```

波括弧を使った1行ifも可能です（前処理構文で変換されます）。

```aria
if %0 == 1 { text "変数は1です" }
```

### ブロックif

複数行にわたる条件分岐です。`else`は省略可能です。

```aria
if 条件
    コマンド...
[else
    コマンド...]
endif
```

**例:**
```aria
if %flag == 1
    text "フラグが立っています"
    lsp 1, "happy.png", 100, 200
else
    text "フラグは立っていません"
    lsp 1, "sad.png", 100, 200
endif
```

### 条件の書き方

| 演算子 | 意味 |
|--------|------|
| `==` | 等しい |
| `!=` | 等しくない |
| `>`  | より大きい |
| `<`  | より小さい |
| `>=` | 以上 |
| `<=` | 以下 |
| `&&` | かつ（AND）|
| `||` | または（OR）|

**例:**
```aria
if %a > 0 && %b < 10
    text "条件を満たします"
endif
```

### 注意事項

- `if`文は内部で`JumpIfFalse`オペコードに変換されます
- ブロックifは`endif`で必ず閉じる必要があります
- `else`の後も`endif`が必要です

---

## `goto` / `jmp` — 無条件ジャンプ

指定したラベルに無条件でジャンプします。

```aria
goto *ラベル名
```

**エイリアス:** `jmp`

**例:**
```aria
*start
    text "ここから開始"
    goto *chapter1

*chapter1
    text "チャプター1"
```

### 注意事項

- ラベル名の先頭に `*` を付けます
- 存在しないラベルへのジャンプはパース時にエラーになります
- `goto`実行時、現在のスコープに登録された`defer`命令が実行されます

---

## `cmp` — 比較

2つの値を比較し、結果を内部フラグに保存します。後続の`beq`/`bne`/`bgt`/`blt`と組み合わせて使います。

```aria
cmp レジスタ, 値
```

| 比較結果 | 内部フラグの値 |
|----------|---------------|
| 等しい | `0` |
| 左 > 右 | `1` |
| 左 < 右 | `-1` |

**例:**
```aria
mov %0, 5
cmp %0, 10
blt *less_than_10    ; %0 < 10 ならジャンプ
```

---

## `beq` / `bne` / `bgt` / `blt` — 条件付きジャンプ

`cmp`で設定された内部フラグに基づいてジャンプします。

| コマンド | ジャンプ条件 |
|----------|-------------|
| `beq` | 等しい（フラグ == 0）|
| `bne` | 等しくない（フラグ != 0）|
| `bgt` | より大きい（フラグ == 1）|
| `blt` | より小さい（フラグ == -1）|

```aria
beq *ラベル名
bne *ラベル名
bgt *ラベル名
blt *ラベル名
```

**例:**
```aria
mov %score, 80
cmp %score, 60
bgt *high_score       ; score > 60 ならジャンプ
beq *exactly_60       ; score == 60 ならジャンプ
blt *low_score        ; score < 60 ならジャンプ
```

### 注意事項

- 必ず`cmp`の直後に使用してください
- 複数の分岐を連続して書くことができます

---

## `while` / `wend` — ループ

条件が真である間、ブロック内の命令を繰り返し実行します。

```aria
while 条件
    コマンド...
wend
```

**例:**
```aria
mov %i, 0
while %i < 5
    lsp %i, "icon.png", %i * 50, 100
    inc %i
wend
```

### 注意事項

- `while`はブロック構造です。`wend`で必ず閉じる必要があります
- `while`/`wend`の前後にスコープが生成され、`defer`が動作します
- 波括弧構文 `while cond { ... }` も前処理で `while`/`wend` に変換されます

---

## `break` / `continue` — ループ制御

`while`ループの実行を制御します。

| コマンド | 動作 |
|----------|------|
| `break` | ループを即座に終了し、`wend`の次に進む |
| `continue` | 現在の反復をスキップし、`while`の条件判定に戻る |

**例:**
```aria
mov %i, 0
while %i < 10
    inc %i
    if %i == 5
        continue      ; 5はスキップ
    endif
    if %i == 8
        break         ; 8でループ終了
    endif
    lsp %i, "item.png", %i * 30, 200
wend
```

### 注意事項

- `break`/`continue`はループ外で使用するとエラーになります
- 実行時、現在のスコープに登録された`defer`命令が実行されます

---

## `defsub` / `sub` — サブルーチン定義

サブルーチン（関数）を定義します。`gosub`で呼び出せます。

```aria
defsub 名前
    コマンド...
    return
```

**エイリアス:** `sub`

**例:**
```aria
defsub ShowMessage
    text "サブルーチン内のメッセージ"
    wait 500
    return

*start
    gosub *ShowMessage
```

### 注意事項

- `defsub`の後に`return`が必要です
- `func`構文（モダン構文）も内部で`defsub`に変換されます
- 名前空間内で定義した場合、名前は `Namespace_Name` 形式になります

---

## `gosub` / `call` — サブルーチン呼び出し

定義済みのサブルーチンを呼び出します。呼び出し元に戻る位置がスタックに保存されます。

```aria
gosub *ラベル名
gosub *ラベル名, 引数1, 引数2, ...
```

**エイリアス:** `call`

### 引数付き呼び出し

引数を渡す場合、カンマ区切りで指定します。サブルーチン内で`getparam`で受け取ります。

```aria
defsub SetPosition
    getparam %x, %y
    lsp 1, "sprite.png", %x, %y
    return

*start
    gosub *SetPosition, 100, 200
```

**例:**
```aria
defsub ShowDialog
    getparam $name, $text
    text "$name「$text」"
    return

*start
    gosub *ShowDialog, "ミオ", "こんにちは！"
```

### 参照渡し（ref）

引数の先頭に`ref`を付けると、参照渡しになります。サブルーチン内での変更が呼び出し元に反映されます。

```aria
defsub AddTen
    getparam %value
    add %value, 10
    return

*start
    mov %num, 5
    gosub *AddTen, ref %num
    ; %num は 15 になる
```

### 注意事項

- 正しい構文は `gosub *label, arg1, arg2` です
- `gosub *label with arg1, arg2` などの構文は誤りです
- 関数スタイル呼び出し `Name(args)` も内部で`gosub`に変換されます
- 呼び出し時にローカルスコープが生成されます

---

## `return` / `ret` — サブルーチンから復帰

サブルーチンを終了し、`gosub`の次の命令に戻ります。

```aria
return
```

**エイリアス:** `ret`

**例:**
```aria
defsub CheckFlag
    if %flag == 1
        return          ; 早期復帰
    endif
    text "フラグは0です"
    return
```

### 注意事項

- `return`実行時、現在のスコープに登録された`defer`命令が実行されます
- `ref`マップとローカルスコープが復元されます

---

## `returnvalue` — 戻り値を設定

サブルーチンの戻り値を設定します。`%0`レジスタや式で結果を返す代わりに使用できます。

```aria
returnvalue 値
```

**例:**
```aria
defsub Add
    getparam %a, %b
    returnvalue %a + %b

*start
    gosub *Add, 3, 5
    ; 戻り値は State.LastReturnValue に保存される
```

### 注意事項

- 戻り値は内部状態に保存されます。呼び出し元で`%0`などに代入する場合は明示的に行ってください
- `return`と同様に、スコープと`ref`マップのクリーンアップが行われます

---

## `getparam` — 引数を受け取る

`gosub`で渡された引数をレジスタに取り出します。

```aria
getparam レジスタ1 [, レジスタ2, ...]
```

**例:**
```aria
defsub MoveSprite
    getparam %id, %x, %y
    msp %id, %x, %y
    return

*start
    gosub *MoveSprite, 1, 100, 200
```

### 注意事項

- 引数はスタック（LIFO）から取り出されるため、`gosub`の渡した順序と同じ順序で受け取ります
- `ref`渡しの引数は、サブルーチン内での変更が呼び出し元に反映されます

---

## `alias` / `numalias` — 定数エイリアス

数値定数に名前を付けます。パース時に名前が値に置き換えられます。

```aria
alias 名前, 値
```

**エイリアス:** `numalias`

**例:**
```aria
alias MAX_HP, 100
alias SPRITE_PLAYER, 1

mov %hp, MAX_HP
lsp SPRITE_PLAYER, "player.png", 0, 0
```

### 注意事項

- エイリアスはパース時に展開されるため、実行時のオーバーヘッドはありません
- `const`命令や`#define`マクロも同様の機能を提供します

---

## `include` — スクリプト読み込み

別の`.aria`スクリプトファイルを読み込み、現在のスクリプトに統合します。

```aria
include "ファイルパス"
```

**例:**
```aria
include "chapter1.aria"
include "common/sprites.aria"
```

### 注意事項

- 相対パスで指定します
- 同じファイルを複数回読み込むと内容が重複する可能性があります
- `script`コマンド（`script "path"`）も同様の機能を持ちます

---

## サンプル: 制御構造の組み合わせ

```aria
*start
    mov %count, 0
    
    while %count < 3
        gosub *ProcessCount, %count
        inc %count
    wend
    
    text "すべて完了"
    end

defsub ProcessCount
    getparam %n
    if %n == 0
        text "最初の処理"
    else
        if %n == 1
            text "2番目の処理"
        else
            text "最後の処理"
        endif
    endif
    return
```

---

## 関連項目

- [基本演算・変数](basic.md) — `mov`, `add`, `sub`, `let`, `for`, `next` など
- [オペコード一覧](index.md) — 全コマンドのカテゴリ別索引
