# 履歴資料: Aria v2 Strict 技術仕様書

> **廃止済み**: この文書の `# aria-version`、`strict on/off`、v1 互換構文は現行
> コンパイラに存在しません。新規作品は [Aria 言語仕様](aria.md) を使用してください。

> **Version**: 2.0.0-rc.1
> **Status**: 実装進行中（Linter・Parser・VM基盤完了）
> **Target**: `# aria-version: 2.0` 以降のスクリプト

---

## 1. 概要

### 1.1 目的

Aria v2 strict は、既存の `.aria` スクリプト言語に**安全性の検査層**を追加するアップグレードです。構文の全面刷新ではなく、以下を段階的に導入します。

| 目標 | 説明 |
|------|------|
| **安全性向上** | 型の混同、未定義動作、暗黙の副作用を検出 |
| **リソース管理** | スプライト等のUIリソースに明示的な寿命と所有権を導入 |
| **大規模スクリプト対応** | `namespace` / `func` / `struct` / `enum` で名前空間の衝突を減らす |
| **Rust-inspired 設計** | 借用チェッカー、寿命、所有権、可変性の概念をVNスクリプト向けに適用 |

### 1.2 基本方針

1. **後方互換の維持**: v1.x スクリプトはそのまま動作する
2. **Opt-in 方式**: `# aria-version: 2.0` と `strict on` で有効化
3. **Linter 先行**: 実行可能でも危険な書き方を静的に検出
4. **段階的導入**: 構造化機能は Parser 時に平坦化し、VM 命令を増やさない

### 1.3 Strict モードの有効化

```aria
# aria-version: 2.0
strict on
```

`strict off` （または未指定）では v1.x 互換の緩い検査になります。

### 1.4 実装状況

| フェーズ | 機能 | 状態 | 備考 |
|---------|------|------|------|
| Phase 1 | Linter: E001-E012, W001-W008 | ✅ 完了 | `AriaLintCommand.cs` で実装済み |
| Phase 1 | strictモード「未指定=不変」 | ✅ 完了 | `AriaCheck.cs` で検出 |
| Phase 2 | Parser: `@sprite`/`&flag`型認識 | ✅ 完了 | `[%$@&]?` 正規表現で対応 |
| Phase 3 | `func`/`namespace`/`struct`/`enum` | ✅ ほぼ完了 | return式評価を追加 |
| Phase 4 | Sprite `OwnershipMode` | ✅ 完了 | `Owned`/`Unowned`/`Borrowed`/`Moved` |
| Phase 4 | VM: `scope`/`end_scope` 自動削除 | ✅ 完了 | `ExitScopesUntil()` で実装 |
| Phase 4 | VM: `owned`/`borrow`/`move` 追跡 | ⚠️ 基盤完了 | `OwnershipMode` 導入済み、細かい追跡は未実装 |

### 1.5 使用例（海風作品での実運用）

```aria
# aria-version: 2.0
strict on

; タイトル画面のUIをscopeで管理
scope "title_ui"
    owned @bg = lsp(1, "bg/title.png", 0, 0)
    owned @btn_start = lsp_rect(10, 400, 300, 200, 50)
    owned @btn_load = lsp_rect(11, 400, 300, 260, 50)
    
    vsp @bg, on
    vsp @btn_start, on
    vsp @btn_load, on
    
    spbtn @btn_start, 1
    spbtn @btn_load, 2
    btnwait %choice
end_scope
; ここで @bg, @btn_start, @btn_load が自動的に解放される

; シーン切り替え
func scenechange(path: string, dur: int) -> void
    transition bg, $path, "fade", %dur
endfunc

; ADVモード設定
func adv() -> void
    textclear
    textmode "adv"
    textbox 40, 540, 1200, 160
    fontsize 28
    textcolor "#e7e2d6"
endfunc
```

---

## 2. 型システム

### 2.1 基本型

v2 strict では、4つの第一級型を定義します。

| 型 | リテラル例 | レジスタ接頭辞 | 説明 |
|----|-----------|--------------|------|
| `int` | `42`, `-10`, `0xFF` | `%` | 32bit符号付き整数 |
| `string` | `"hello"` | `$` | 可変長文字列（UTF-8） |
| `sprite` | `sprite(1)`, `sprite("bg")` | `@`（新設） | スプライト参照 |
| `flag` | `true`, `false` | `&`（新設） | 真偽値（セーブ範囲付き） |

#### 型接頭辞の厳格化

```aria
; strict on では、接頭辞と型が厳密に紐づく
let %n, 100        ; OK: int
let $s, "hello"    ; OK: string
let @sp, sprite(1) ; OK: sprite
let &f, true       ; OK: flag

; 以下はエラー
let %x, "hello"    ; ERROR: intレジスタにstringを代入
let $y, 100        ; ERROR: stringレジスタにintを代入
```

### 2.2 Sprite 型（所有権システムの核心）

`sprite` 型はスプライトIDへの**所有権付き参照**です。

```aria
; owned sprite: このscopeが解放責任を持つ
owned @bg = lsp_owned("bg", "title.png", 0, 0)

; borrow: 一時的な借用（所有権は移動しない）
borrow @temp = @bg
set_alpha @temp, 128    ; OK: 借用中は読み書き可能

; 所有権の移動（move）
let @next = @bg         ; @bg はその後使えなくなる
set_alpha @bg, 255      ; ERROR: use after move
```

### 2.3 Flag 型

`flag` は従来の `set_pflag` / `set_sflag` / `set_vflag` を統合します。

```aria
; 保存範囲を型として表現
local   &temp   = true     ; volatile と同等
save    &route  = false    ; セーブスロット別
persistent &cg_unlock = true ; 全セーブ共通

; strict では、flag 以外の型を条件式に使うと警告
if %0 == 1              ; WARNING: int を真偽値として使用
if &route == true       ; OK
```

### 2.4 型変換

暗黙の型変換を禁止します。明示的なキャストが必要です。

```aria
let %n, 100
let $s, to_string(%n)      ; OK: 明示的キャスト
let %m, to_int("42")       ; OK

let $bad = %n               ; ERROR: 暗黙キャスト
let %bad = "100"            ; ERROR: 暗黙キャスト
```

### 2.5 型推論

`let` に修飾子を付けない場合、右辺から型を推論します。

```aria
let %x = 100            ; int と推論
let $msg = "hello"      ; string と推論
let @sp = sprite(1)     ; sprite と推論（unowned）
```

---

## 3. 可変性とスコープ

### 3.1 可変性修飾子

Rust の `mut` / 不変の概念を導入します。

| 修飾子 | 読み取り | 書き換え | 適用対象 |
|--------|---------|---------|---------|
| `readonly` | ○ | ✕ | 変数、レジスタ、spriteフィールド |
| `mut` | ○ | ○ | 変数、レジスタ、spriteフィールド |
| （未指定） | ○ | ○ | v1互換: デフォルトで可変 |

```aria
strict on

; 不変変数
readonly %MAX_HP = 100
add %MAX_HP, 10         ; ERROR: cannot assign to readonly

; 可変変数
mut %hp = 100
add %hp, -10            ; OK

; sprite の可変性
readonly @bg = lsp("bg", "forest.png", 0, 0)
msp @bg, 100, 0         ; ERROR: readonly sprite cannot move
set_alpha @bg, 200      ; ERROR: readonly sprite cannot modify

mut @fg = lsp("fg", "chara.png", 100, 200)
msp @fg, 50, 0          ; OK
```

### 3.2 スコープ修飾子

| 修飾子 | 有効範囲 | 保存先 |
|--------|---------|--------|
| `local` | 現在の `scope` ブロックまたは `func` | メモリのみ |
| `global` | スクリプト全体 | セーブスロット別（デフォルト） |
| `persistent` | スクリプト全体 | `persistent.ariasav` |
| `save` | スクリプト全体 | セーブスロット別（global と同じ） |
| `volatile` | スクリプト全体 | なし（起動中のみ） |

```aria
func show_menu()
    local %x = 100          ; func 終了で破棄
    global $title = "Game"  ; セーブされる
    persistent &unlocked = true
    volatile %tmp = 0       ; 再起動でリセット
endfunc
```

### 3.3 デフォルト可変性ルール（strict モード）

strict モードでは、**未指定の変数は不変**とみなします。

```aria
strict on

let %x = 100
add %x, 1               ; ERROR: %x is not declared as mut

mut %y = 100
add %y, 1               ; OK
```

v1互換モード（`strict off`）では、従来通りすべて可変です。

---

## 4. 寿命管理

### 4.1 Scope ブロック

`scope` / `end_scope` でリソースの寿命を明示的に区切ります。

```aria
scope "title_ui"
    owned @bg = lsp_owned(1, "title.png", 0, 0)
    owned @btn_start = lsp_rect_owned(10, 400, 300, 200, 50)
    spbtn @btn_start, 1
    
    btnwait %choice
end_scope
; ここで @bg, @btn_start が自動的に vsp / csp され、
; 所有権が解放される
```

### 4.2 所有権モデル

#### Owned（所有）

`scope` または `func` 内で `owned` または `lsp_owned` / `lsp_rect_owned` / `lsp_text_owned` で作成されたスプライトは、そのブロックの終了時に自動的に解放されます。

```aria
scope "battle_ui"
    ; 所有スプライトの作成
    owned @hp_bar = lsp_rect_owned(100, 10, 500, 30, 0xFF0000)
    owned @hp_text = lsp_text_owned(101, "HP: 100/100", 520, 15)
    
    ; 使用
    vsp @hp_bar, on
    
    ; end_scope 到達時:
    ;   - @hp_bar, @hp_text は自動 csp
    ;   - 以降の参照はコンパイルエラー
end_scope

vsp @hp_bar, on         ; ERROR: @hp_bar has been dropped
```

#### Unowned（非所有）

従来の `lsp` で作成されたスプライトは、明示的に `csp` するまで生存します。

```aria
; 非所有スプライト: 明示的な解放が必要
@chara = lsp(1, "mio.png", 200, 400)
; ... 使用 ...
csp @chara              ; 手動解放
```

#### Borrow（借用）

一時的に所有スプライトを参照したい場合、`borrow` を使います。

```aria
scope "effect"
    owned @flash = lsp_rect_owned(50, 0, 0, 1280, 720)
    
    ; borrow: 所有権を移動せず一時的に使う
    borrow @b = @flash
    set_alpha @b, 255
    
    ; borrow 終了後も @flash は有効
    set_alpha @flash, 0   ; OK
end_scope
```

### 4.3 所有権の移動（Move）

所有スプライトを別の変数に代入すると、所有権が移動します。

```aria
scope "a"
    owned @x = lsp_owned(1, "a.png", 0, 0)
    owned @y = @x           ; @x の所有権が @y に移動
    
    vsp @x, on             ; ERROR: @x was moved
    vsp @y, on             ; OK
end_scope
; @y が解放される（@x は既に空なので二重解放は起きない）
```

### 4.4 Drop チェック

所有スプライトが `end_scope` / `endfunc` / `return` より前に手動で `csp` された場合、二重解放を検出します。

```aria
scope "err"
    owned @s = lsp_owned(1, "test.png", 0, 0)
    csp @s                  ; OK
    csp @s                  ; ERROR: double free of owned sprite
end_scope                   ; ERROR: @s already dropped
```

### 4.5 スプライトの借用ルール

1. **可変借用（mutable borrow）**: 1つのスコープで、1つの可変借用のみ可能
2. **不変借用（immutable borrow）**: 複数の不変借用は同時に可能
3. **借用と所有権移動の共存不可**: 借用中は所有権を移動できない

```aria
scope "borrow_rules"
    owned @bg = lsp_owned(1, "bg.png", 0, 0)
    
    borrow @b1 = @bg
    borrow @b2 = @bg        ; OK: 複数不変借用
    
    borrow mut @bm = @bg
    borrow @b3 = @bg        ; ERROR: cannot borrow while mutable borrow exists
    
    owned @moved = @bg      ; ERROR: cannot move while borrowed
end_scope
```

---

## 5. 構造化

### 5.1 基本方針

`func` / `namespace` / `struct` / `enum` は**パーサ時の構造化機能**です。VM 命令には展開されず、従来のレジスタ・ラベル・サブルーチンに変換されます。

### 5.2 Namespace

名前の衝突を防ぎ、大規模スクリプトを整理します。

```aria
namespace TitleScreen
    struct ButtonConfig
        int x
        int y
        int width
        int height
        string label
    endstruct
    
    func draw_button(readonly ButtonConfig cfg) -> int
        local @btn = lsp_rect(10, cfg.x, cfg.y, cfg.width, cfg.height)
        local @txt = lsp_text(11, cfg.label, cfg.x + 10, cfg.y + 10)
        spbtn @btn, 1
        return 1
    endfunc
endnamespace

; 呼び出し
local TitleScreen.ButtonConfig start_cfg = {
    x = 400, y = 300, width = 200, height = 60, label = "START"
}
TitleScreen.draw_button(start_cfg)
```

#### 展開規則

`namespace TitleScreen` は以下のように平坦化されます。

| v2 strict | 展開後（v1互換） |
|-----------|----------------|
| `TitleScreen.ButtonConfig` | `struct TitleScreen_ButtonConfig` |
| `TitleScreen.draw_button` | `defsub TitleScreen_draw_button` |
| `TitleScreen.draw_button(cfg)` | `gosub *TitleScreen_draw_button, cfg_x, cfg_y, ...` |

### 5.3 Func

型付き引数と戻り値を持つサブルーチンです。

```aria
func add(int a, int b) -> int
    return a + b
endfunc

func greet(readonly string name) -> string
    return "Hello, " + name
endfunc
```

#### 展開規則

```aria
; strict
func add(int a, int b) -> int
    return a + b
endfunc

let %result = add(10, 20)
```

```aria
; 展開後（v1互換相当）
defsub add
*add
    getparam %a
    getparam %b
    let %add_result, %a + %b
    return %add_result

; 呼び出し側
gosub *add, 10, 20
let %result, %add_result
```

#### 注意事項

- `func` は `return` で必ず値を返す（void func は作らない）
- 引数は `readonly`（デフォルト）または `mut` を指定可能
- `mut` 引数は値渡しではなく、対応するレジスタへの参照渡しとみなす

### 5.4 Struct

既存の `struct` を拡張し、型検査とメソッドなしの純粋なデータ構造とします。

```aria
struct Point
    int x
    int y
endstruct

struct Rect
    readonly Point pos   ; 入れ子は1段階まで許可
    int width
    int height
endstruct

; 初期化
let %p = Point { x = 10, y = 20 }
let %r = Rect { pos = Point { x = 0, y = 0 }, width = 100, height = 200 }

; フィールドアクセス
let %x_val = %p.x
let %area = %r.width * %r.height
```

#### 展開規則

```aria
; strict
let %p = Point { x = 10, y = 20 }
let %sum = %p.x + %p.y
```

```aria
; 展開後
let %p_x, 10
let %p_y, 20
let %sum, %p_x + %p_y
```

### 5.5 Enum

整数定数の集合を型安全にします。

```aria
enum Route
    None = 0
    HeroineA = 1
    HeroineB = 2
    BadEnd = 99
endenum

; 使用
save %current_route = Route.None

if %current_route == Route.HeroineA
    text "ヒロインAルートです"
endif
```

#### 展開規則

```aria
; strict
save %current_route = Route.None
```

```aria
; 展開後
const Route_None = 0
const Route_HeroineA = 1
; ...
let %current_route, Route_None
```

#### 型安全性

strict モードでは、enum 型と int 型の混在を警告します。

```aria
let %r = Route.HeroineA
let %x = 100

if %r == %x               ; WARNING: comparing enum with raw int
if %r == Route.HeroineB   ; OK
```

---

## 6. Linter 仕様

### 6.1 エラー（Error: 実行をブロック）

| コード | 内容 | 例 |
|--------|------|-----|
| E001 | 型の混同 | `let %x, "hello"` |
| E002 | 未定義変数の使用 | `add %undefined, 1` |
| E003 | 所有権移動後の使用 | `let @y = @x` の後に `@x` を使用 |
| E004 | 二重解放 | owned sprite の `csp` 後に再び `csp` |
| E005 | 借用違反 | mutable borrow 中の不変 borrow |
| E006 | readonly への代入 | `readonly %x` に `let` / `add` |
| E007 | 未定義ラベル | `goto *not_exist` |
| E008 | 未定義関数/サブルーチン | `UndefinedFunc()` |
| E009 | enum 未定義値 | `Route.NotExists` |
| E010 | スコープ外変数アクセス | `end_scope` 後の `local` 変数使用 |
| E011 | func の戻り値未設定 | `-> int` 指定で `return` なしの分岐 |
| E012 | owned sprite のスコープ外持ち出し | `scope` 内の `owned` を外に `return` |

### 6.2 警告（Warning: 実行は可能）

| コード | 内容 | 例 |
|--------|------|-----|
| W001 | 暗黙の型変換 | `if %0 == 1` で flag 期待箇所に int |
| W002 | btnwait 結果の未退避 | `btnwait %0` のままサブルーチン呼び出し |
| W003 | 未使用変数 | 宣言後に一度も使わない `local %x` |
| W004 | 未使用 borrow | `borrow @b = @x` 後に `@b` を未使用 |
| W005 | 長寿命の volatile 使用 | `volatile` を広いスコープで使い続ける |
| W006 | 互換モード命令 | `compat_mode on` 使用の検出 |
| W007 | 生の int と enum の比較 | `%r == 1`（%r は enum型） |
| W008 | 未初期化変数の使用 | `let %x`（初期化なし）の参照 |
| W009 | global の乱用 | 大量の `global` 宣言の検出（>50個で警告） |
| W010 | deeply nested scope | `scope` のネストが5段階を超える |

### 6.3 情報（Info: スタイル指摘）

| コード | 内容 |
|--------|------|
| I001 | 変数名の推奨: `%n` ではなく意味のある名前 |
| I002 | `let %x = %x + 1` は `inc %x` を推奨 |
| I003 | 小さな `scope` の抽出を推奨 |

### 6.4 Lint ルールの詳細

#### E003: Use After Move

```aria
scope "ex"
    owned @a = lsp_owned(1, "a.png", 0, 0)
    owned @b = @a           ; @a の所有権が @b へ移動
    
    vsp @a, on             ; ERROR [E003]: @a was moved to @b at line X
end_scope
```

#### W002: btnwait 結果の退避推奨

```aria
btnwait %0
call_some_subroutine()      ; WARNING [W002]: %0 may be clobbered
if %0 == 1                  ; before call_some_subroutine
```

**安全な書き方:**

```aria
btnwait %0
let %choice = %0            ; 退避
call_some_subroutine()
if %choice == 1
```

#### E012: Owned Sprite Escape

```aria
func make_sprite() -> sprite
    scope "inner"
        owned @s = lsp_owned(1, "test.png", 0, 0)
    end_scope               ; @s はここで drop される
    return @s               ; ERROR [E012]: returning dropped sprite
endfunc
```

**正しい書き方:**

```aria
func make_sprite() -> sprite
    @s = lsp(1, "test.png", 0, 0)   ; unowned: 呼び出し側が管理
    return @s
endfunc
```

---

## 7. 移行ガイド

### 7.1 v1.x -> v2 strict の移行パス

#### Step 1: バージョン指定と strict 有効化

```aria
# aria-version: 2.0
strict on
```

既存スクリプトに追加するだけで、Linter が既存の危険なパターンを検出します。

#### Step 2: 型の混同を修正

```aria
; BEFORE (v1.x)
let %0, "hello"
let $1, 100

; AFTER (v2 strict)
let $msg, "hello"
let %num, 100
```

#### Step 3: btnwait 結果の退避

```aria
; BEFORE
btnwait %0
gosub *check_unlock
if %0 == 1

; AFTER
btnwait %0
let %title_choice = %0
gosub *check_unlock
if %title_choice == 1
```

#### Step 4: スプライトの所有権導入

```aria
; BEFORE
lsp 1, "title.png", 0, 0
lsp_rect 10, 400, 300, 200, 50
spbtn 10, 1
btnwait %0
csp 1
csp 10

; AFTER
scope "title"
    owned @bg = lsp_owned(1, "title.png", 0, 0)
    owned @btn = lsp_rect_owned(10, 400, 300, 200, 50)
    spbtn @btn, 1
    btnwait %choice
end_scope
; 自動で解放される
```

#### Step 5: func / namespace で整理

```aria
; BEFORE
*draw_title_button
    lsp_rect 10, %0, %1, %2, %3
    lsp_text 11, %4, %0 + 10, %1 + 10
    spbtn 10, 1
    return

; AFTER
namespace TitleUI
    struct Button
        int x
        int y
        int w
        int h
        string label
    endstruct
    
    func draw(readonly Button b) -> int
        local @rect = lsp_rect(10, b.x, b.y, b.w, b.h)
        local @txt = lsp_text(11, b.label, b.x + 10, b.y + 10)
        spbtn @rect, 1
        return 1
    endfunc
endnamespace
```

### 7.2 段階的移行のための互換設定

```aria
# aria-version: 2.0
strict on

; 既存資産の移行中に一時的に緩和
pragma lint_allow W001      ; 型混在の警告を一時許容
pragma lint_allow W002      ; btnwait 未退避を一時許容
```

### 7.3 移行チェックリスト

- [ ] `# aria-version: 2.0` を先頭に追加
- [ ] `strict on` を設定
- [ ] Linter エラーをすべて修正（E001〜E012）
- [ ] Linter 警告を確認し、修正または `pragma lint_allow` で許容
- [ ] スプライト管理箇所を `scope` + `owned` に置き換え
- [ ] サブルーチンを `func` に置き換え（任意）
- [ ] 定数を `enum` / `const` に整理（任意）

---

## 8. 実装ロードマップ

### フェーズ1: Linter（最短投入ルート）

**目標**: 既存の v1.x スクリプトを解析し、strict 違反を検出する静的解析ツール `aria-lint` を提供する。

| タスク | 内容 | 想定期間 |
|--------|------|---------|
| 1.1 | 型追跡エンジンの実装 | 2日 |
| 1.2 | 所有権・借用チェッカーの実装 | 3日 |
| 1.3 | スコープ解析（scope/end_scope, local/global） | 2日 |
| 1.4 | Lint ルール E001〜E012 の実装 | 3日 |
| 1.5 | Lint ルール W001〜W010 の実装 | 2日 |
| 1.6 | CLI ツール `aria-lint` の整備 | 1日 |
| 1.7 | VS Code 拡張のプロトタイプ | 2日 |

**成果物**: `aria-lint script.aria` でエラー/警告を出力

### フェーズ2: VM 拡張（実行時安全性）

**目標**: Linter で検出した一部の問題を、実行時にも検出・防止する。

| タスク | 内容 | 想定期間 |
|--------|------|---------|
| 2.1 | `sprite` 型（`@` 接頭辞）の VM サポート | 2日 |
| 2.2 | `flag` 型（`&` 接頭辞）の VM サポート | 1日 |
| 2.3 | `scope` / `end_scope` の実行時実装（自動 csp） | 2日 |
| 2.4 | `readonly` 変数の実行時保護 | 1日 |
| 2.5 | 未初期化変数アクセスの実行時検出 | 2日 |
| 2.6 | `owned` スプライトの二重解放防止 | 1日 |

**注意**: `func` / `namespace` / `struct` / `enum` はパーサ時に平坦化するため、VM 変更は不要。

### フェーズ3: 構造化（Parser 拡張）

**目標**: `func` / `namespace` / `struct` / `enum` をパーサに導入し、v1互換命令へ展開する。

| タスク | 内容 | 想定期間 |
|--------|------|---------|
| 3.1 | `struct` 定義とフィールドアクセスの展開 | 2日 |
| 3.2 | `enum` 定義と使用箇所の定数展開 | 1日 |
| 3.3 | `func` 定義と `defsub` への展開 | 2日 |
| 3.4 | `namespace` と名前修飾の展開 | 2日 |
| 3.5 | 引数の型チェックと `getparam` 展開 | 1日 |
| 3.6 | 戻り値の `return` 展開と未返却検出 | 1日 |

### フェーズ4: 統合と検証

| タスク | 内容 | 想定期間 |
|--------|------|---------|
| 4.1 | `aria-lint` と Parser の統合 | 2日 |
| 4.2 | 既存作品 umikaze の v2 strict 移行実験 | 3日 |
| 4.3 | パフォーマンス計測（展開前後の命令数比較） | 1日 |
| 4.4 | ドキュメント更新とチュートリアル作成 | 2日 |

### マイルストーン

```
Week 1-2:  フェーズ1 完了 -> aria-lint リリース
Week 3-4:  フェーズ2 完了 -> VM 拡張リリース
Week 5-6:  フェーズ3 完了 -> Parser 拡張リリース
Week 7-8:  フェーズ4 完了 -> v2.0.0 正式リリース
```

---

## 付録A: 構文まとめ

### A.1 Strict モード専用構文

```aria
# aria-version: 2.0
strict on

; --- 型と可変性 ---
mut %x = 100
readonly $name = "aria"
local mut %tmp = 0
global readonly &flag = true

; --- スプライト所有権 ---
owned @bg = lsp_owned(1, "bg.png", 0, 0)
borrow @b = @bg

; --- スコープ ---
scope "ui"
    owned @btn = lsp_rect_owned(10, 100, 200, 80, 40)
end_scope

; --- 構造化 ---
namespace Game
    enum State
        Title = 0
        Play = 1
        End = 2
    endenum
    
    struct Player
        int hp
        int mp
        string name
    endstruct
    
    func heal(readonly Player p, int amount) -> int
        return p.hp + amount
    endfunc
endnamespace

; --- 型変換 ---
let %n = to_int("42")
let $s = to_string(100)
```

### A.2 エラーコード早見表

| コード | レベル | メッセージ例 |
|--------|--------|-------------|
| E001 | Error | `type mismatch: expected int, found string` |
| E002 | Error | `undefined variable: %undefined` |
| E003 | Error | `use of moved value: @x` |
| E004 | Error | `double free: @s was already dropped` |
| E005 | Error | `cannot borrow @x as immutable because it is borrowed as mutable` |
| E006 | Error | `cannot assign to readonly variable: %x` |
| E007 | Error | `undefined label: *not_found` |
| E008 | Error | `undefined function: unknown_func` |
| E009 | Error | `enum Route has no variant NotExists` |
| E010 | Error | `cannot access local variable %x outside its scope` |
| E011 | Error | `function add may not return a value on all paths` |
| E012 | Error | `cannot return owned sprite from scope: @s will be dropped` |
| W001 | Warning | `implicit int-to-flag conversion` |
| W002 | Warning | `btnwait result should be saved before subroutine call` |
| W003 | Warning | `unused variable: %x` |
| W004 | Warning | `unused borrow: @b` |
| W005 | Warning | `volatile variable %v used in wide scope` |
| W006 | Warning | `compat_mode is enabled` |
| W007 | Warning | `comparing enum Route with raw int` |
| W008 | Warning | `use of possibly uninitialized variable: %x` |
| W009 | Warning | `too many global variables (>50)` |
| W010 | Warning | `scope nesting exceeds 5 levels` |

---

## 付録B: Rust 概念との対応表

| Rust の概念 | Aria v2 strict の表現 | 備考 |
|------------|---------------------|------|
| `let x = 5` | `readonly %x = 5` | strict モードでは不変がデフォルト |
| `let mut x = 5` | `mut %x = 5` | 可変は明示 |
| `Box<T>` / 所有権 | `owned @s = lsp_owned(...)` | scope 終了で drop |
| `&T`（不変借用） | `borrow @b = @s` | 複数可 |
| `&mut T`（可変借用） | `borrow mut @b = @s` | 排他 |
| `drop(x)` | `end_scope` / 自動解放 | 暗黙 drop が基本 |
| `use after move` | E003 | 所有権移動後の使用 |
| `lifetime` | `scope` / `end_scope` | 明示的ブロック |
| `struct` | `struct` | 平坦化展開 |
| `enum` | `enum` | 定数集合 |
| `fn` | `func` | `defsub` へ展開 |
| `mod` / `namespace` | `namespace` | 名前修飾 |

---

## 付録C: 既存命令との対応

v2 strict で新設される構文は、原則として既存命令の組み合わせに展開されます。

| v2 strict 構文 | 展開後の v1 命令群 |
|---------------|------------------|
| `owned @s = lsp_owned(id, path, x, y)` | `lsp id, path, x, y` + メタデータ（所有権マーク） |
| `scope "name"` | （スコープ開始マーカー） |
| `end_scope` | 所有スプライトの `csp` を自動挿入 |
| `func name(args) -> type` | `defsub name` + `getparam` + `return` |
| `namespace N { ... }` | 識別子に `N_` プレフィックスを付与 |
| `struct S { fields }` | フィールドごとに独立レジスタを生成 |
| `enum E { A = 1 }` | `const E_A = 1` |
| `borrow @b = @s` | （借用チェックのみ、実行時は同じID） |
