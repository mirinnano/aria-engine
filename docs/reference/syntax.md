# AriaEngine スクリプト言語構文リファレンス

`.aria`ファイルで使用されるスクリプト言語の構文を網羅的に解説します。個別のオペコードの詳細については [`opcodes/`](opcodes/) を参照してください。

---

## 目次

- [基本構造](#基本構造)
- [コメント](#コメント)
- [ラベル](#ラベル)
- [変数](#変数)
- [文字列補間](#文字列補間)
- [コマンド](#コマンド)
- [サブルーチン](#サブルーチン)
- [制御フロー](#制御フロー)
- [ループ](#ループ)
- [テキスト表示](#テキスト表示)
- [複数ステートメント](#複数ステートメント)
- [定数とマクロ](#定数とマクロ)

---

## 基本構造

`.aria`スクリプトは1行ごとに解釈されます。各行は**コマンド**、**ラベル定義**、**テキスト**、または**制御構文**のいずれかです。空白行は無視されます。

```aria
*start
    bg "forest.png", 0
    textclear
    ミオ「ようこそ！」
    wait
```

---

## コメント

行内コメントは `;`（セミコロン）または `//`（ダブルスラッシュ）で開始します。引用符内の `;` や `//` はコメントとして扱われません。

```aria
; これはコメントです
bg "forest.png", 0  ; 背景を表示
// これもコメントです
text "Hello"  // インラインコメントも可
```

`#` で始まる行はプリプロセッサディレクティブとして解釈されます（通常のコメントではありません）。

---

## ラベル

ラベルは `*` に続けて名前を記述します。ジャンプ先やサブルーチンの入口として使用されます。

**構文:**
```
*label_name
```

**例:**
```aria
*start
text "ここから開始"

*ending
text "終了"
```

**注意:**
- ラベル名は英数字とアンダースコア `_` を使用できます
- 大文字・小文字は区別されません

---

## 変数

### 数値レジスタ

`%` に続けて名前または数字を記述します。

| 構文 | 説明 |
|------|------|
| `%0` ~ `%9` | 高速アクセスレジスタ（最も効率的） |
| `%name` | 一般数値レジスタ |

```aria
let %0, 100
let %count, %0
add %count, 1
```

### 文字列レジスタ

`$` に続けて名前を記述します。

```aria
$player_name, "ミオ"
text "こんにちは、${$player_name}さん"
```

### 変数宣言とスコープ

スコープ付きで変数を宣言できます。

```aria
local %x = 0
global $title = "My Game"
persistent %flags = 0
save %chapter = 1
volatile %temp = 0
readonly $version = "1.0"
mut %counter = 0
```

| 修飾子 | 説明 |
|--------|------|
| `local` | ローカルスコープ（デフォルト） |
| `global` | グローバルスコープ |
| `persistent` | セッションを超えて保持 |
| `save` | セーブデータに含める |
| `volatile` | 毎回リセット |
| `readonly` | 再代入不可 |
| `mut` | 可変（localと同じスコープ） |

### 値の代入

`let` コマンドで値を代入します。

```aria
let %var, 100
let $text, "hello"
let %result, %0
```

`auto` / `var` も `let` と同じ動作になります。

---

## 文字列補間

文字列リテラル内で `${...}` を使用すると、実行時に変数や式の値を展開できます。

**構文:**
```
"...${$var}..."
"...${%var}..."
"...${expression}..."
```

**例:**
```aria
$name, "ミオ"
let %hp, 80
text "${$name}のHPは${%hp}です"
; → "ミオのHPは80です"

text "合計: ${%a + %b}"
; → 式も評価可能
```

**注意:**
- `${$name}` → 文字列レジスタ `$name` の値
- `${%name}` または `${%0}` → 数値レジスタを文字列化
- `${expression}` → 式を評価して結果を文字列化

---

## コマンド

**構文:**
```
command arg1, arg2, arg3, ...
```

または関数的な書き方も可能です：

```
command(arg1, arg2, arg3)
```

**例:**
```aria
bg "forest.png", 0
lsp 1, "character.png", 200, 300
msp 1, 100, 0
```

引数はカンマ `,` または空白で区切ることができます。文字列引数はダブルクォート `"` で囲みます（一部のコマンドでは省略可能）。

---

## サブルーチン

### 定義

`defsub` でサブルーチンを定義します。ラベルとセットで使用します。

```aria
defsub greet
*greet
    text "こんにちは！"
    return
```

### 呼び出し

**方法1:** `gosub` コマンドを使用

```aria
gosub *greet
gosub *greet, 100, "hello"
```

**構文:**
```
gosub *label
gosub *label, arg1, arg2, ...
```

**方法2:** 関数的呼び出し（関数名を直接記述）

```aria
greet
greet 100, "hello"
```

### 引数の受け取り

サブルーチン内で `getparam` を使用して引数を受け取ります。

```aria
defsub show_status
*show_status
    getparam %hp
    getparam $name
    text "${$name}のHPは${%hp}です"
    return
```

呼び出し:
```aria
gosub *show_status, 80, "ミオ"
; または
show_status 80, "ミオ"
```

### 戻り値

`return value` で数値を返します。

```aria
defsub add
*add
    getparam %a
    getparam %b
    return %a + %b
```

### 終了

`return` でサブルーチンから呼び出し元に戻ります。

---

## 制御フロー

### goto

無条件ジャンプ。

```aria
goto *label
```

### 条件分岐（if）

#### 1行if

条件が真の場合に1つのコマンドを実行します。

```aria
if %0 == 1 text "一致"
if %hp > 0 goto *battle
if $name == "ミオ" bg "mio_bg.png", 0
```

**構文:**
```
if condition command args
```

#### ブロックif

複数行にわたる条件分岐。

```aria
if %hp > 50
    text "元気です"
else
    text "危険です"
endif
```

**構文:**
```
if condition
    ...
[else
    ...]
endif
```

波括弧 `{}` を使用したブロック記法もサポートされています：

```aria
if %hp > 50 {
    text "元気です"
} else {
    text "危険です"
}
```

#### 比較演算子

| 演算子 | 意味 |
|--------|------|
| `==` | 等しい |
| `!=` | 等しくない |
| `>` | より大きい |
| `<` | より小さい |
| `>=` | 以上 |
| `<=` | 以下 |

#### 論理演算

```aria
if %a == 1 && %b == 2
if %x > 0 || %y > 0
if !%flag
```

### beq / bne / bgt / blt

NScripter互換の条件付きジャンプ。

```aria
beq *label    ; 直前の比較が等しければジャンプ
bne *label    ; 等しくなければジャンプ
bgt *label    ; 大きければジャンプ
blt *label    ; 小さければジャンプ
```

---

## ループ

### while / wend

条件が真の間、繰り返し実行します。

```aria
let %i, 0
while %i < 5
    text "カウント: ${%i}"
    add %i, 1
wend
```

**構文:**
```
while condition
    ...
wend
```

波括弧 `{}` を使用した記法もサポートされています：

```aria
while %i < 5 {
    text "カウント: ${%i}"
    add %i, 1
}
```

### for / next

指定回数繰り返します。

```aria
for %i = 0 to 4
    text "${%i}回目"
next
```

**構文:**
```
for %var = start to end
    ...
next
```

配列の長さでループすることもできます：

```aria
for %i = 0 to arrayName
    text "${%i}番目"
next
```

### break

ループを中断して脱出します。

```aria
while %x < 100
    add %x, 1
    if %x == 50 break
wend
```

### continue

現在の繰り返しをスキップして次の繰り返しに進みます。

```aria
for %i = 0 to 9
    if %i == 5 continue
    text "${%i}"
next
```

---

## テキスト表示

### テキストコマンド

```aria
text "表示する文字列"
text "1行目\n2行目"
```

### インラインテキスト（会話形式）

`Character「message」` の形式で記述できます。`textclear` が自動的に挿入されます。

```aria
ミオ「こんにちは！"
; → textclear + text "ミオ「こんにちは！"
```

**構文:**
```
名前「メッセージ」[\または@]
```

末尾に `\` または `@` を付けることでページ制御ができます（付けない場合は自動的に `\` が追加されます）。

### ページ制御

| 記号 | 動作 |
|------|------|
| `\` | クリック待機 + テキストクリア |
| `@` | クリック待機（テキストはクリアしない） |

```aria
ミオ「こんにちは！\   ; クリック待機＋クリア
ユイ「おはよう！@    ; クリック待機（クリアしない）
```

---

## 複数ステートメント

1行に `:` （コロン）で区切って複数のステートメントを記述できます。引用符内のコロンは区切りとして扱われません。

```aria
bg "room.png", 0 : textclear : text "開始"
```

---

## 定数とマクロ

### const

```aria
const MAX_HP = 100
const TITLE = "My Game"
```

### #define

```aria
#define DEBUG 1
#define GREETING "Hello"
```

---

## 構文まとめ表

| 構文 | 用途 | 例 |
|------|------|-----|
| `*label` | ラベル定義 | `*start` |
| `; comment` | コメント | `; これは注釈` |
| `%0` ~ `%9` | 高速数値レジスタ | `let %0, 100` |
| `%name` | 一般数値レジスタ | `let %count, 0` |
| `$name` | 文字列レジスタ | `$name, "ミオ"` |
| `let dest, value` | 代入 | `let %x, 10` |
| `command args` | コマンド実行 | `bg "file.png", 0` |
| `gosub *label, args` | サブルーチン呼び出し | `gosub *sub, 1, 2` |
| `defsub name` | サブルーチン定義 | `defsub mysub` |
| `return` | サブルーチン終了 | `return` |
| `return value` | 戻り値付き終了 | `return %x + 1` |
| `getparam %var` | 引数受け取り | `getparam %arg1` |
| `if cond command` | 1行if | `if %x == 1 text "OK"` |
| `if cond ... endif` | ブロックif | `if %x > 0 ... endif` |
| `else` | else節 | `else` |
| `goto *label` | 無条件ジャンプ | `goto *start` |
| `beq *label` | 等しければジャンプ | `beq *next` |
| `while cond ... wend` | whileループ | `while %i < 5 ... wend` |
| `for %i = 0 to 9 ... next` | forループ | `for %i = 0 to 9 ... next` |
| `break` | ループ中断 | `break` |
| `continue` | ループスキップ | `continue` |
| `name「msg」\` | インラインテキスト | `ミオ「こんにちは」\` |
| `\` | クリック待機＋クリア | `text "次へ" \` |
| `@` | クリック待機 | `text "待機" @` |
| `:` | 複数ステートメント | `cmd1 : cmd2` |
| `${$var}` | 文字列補間 | `"${$name}さん"` |
| `${%var}` | 数値補間 | `"${%hp}点"` |
| `${expr}` | 式補間 | `"${%a + %b}"` |
| `const NAME = val` | 定数定義 | `const MAX = 100` |
| `#define NAME val` | マクロ定義 | `#define DEBUG 1` |
