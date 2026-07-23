# Aria 言語仕様

Aria はビジュアルノベルのための、単一・静的解析可能な作者言語です。Rust の
可変性、所有権、借用、字句スコープを採り入れますが、物語の `scene`、`say`、
`choice`、`screen` は Aria 固有のプリミティブとして残します。

この文書が唯一の作者言語仕様です。`strict`、互換モード、言語バージョン指定、
NScripter 風の行命令はありません。

## 1. ソース単位

すべての `.aria` ファイルは、必ず次の一行から始めます。

```aria
aria;
```

`aria 3.2;`、`# aria-version: ...`、`strict on` は受理されません。`aria;` は
言語バージョンではなく、単一の作者言語を示すマーカーです。コンパイル済みの
`.ariac` には内部 ABI 識別子がありますが、それはソース構文のモードではありません。

最小の実行可能な作品は次の通りです。

```aria
aria;

entry opening;

scene opening {
    narrate "潮の匂いがした。";
    await advance;
    end;
}
```

トップレベルには以下を置けます。

```text
aria;
module <qualified.name>;
use "relative/module.aria";
entry <scene>;
state [mut] <name>: <Int|Bool|String> = <literal>;
scene <name> { <statement>* }
```

- エントリーソースだけが `entry` を一つ宣言します。
- 再利用モジュールは `entry` を持ちません。
- 読み込みには `use "...";` を使います。`import` は廃止済みです。
- ソースパスとアセットパスはプロジェクト相対、`/` 区切りです。

## 2. 値と可変性

現在の組み込み型は `Int`、`Bool`、`String`、`Node` です。通常の値は推論でき、
必要なら型注釈を書けます。

```aria
let chapter = 1;
let title: String = "海風";
let mut visits: Int = 0;
visits += 1;

state mut route: Int = 0;
route = 1;
```

`let` と `state` は既定で不変です。代入、`+=`、可変借用には明示的な `mut` が
必要です。`state` は保存対象のグローバル状態、`let` は字句スコープのローカル
束縛です。`var` はありません。

## 3. Node の所有権: GC ではなく決定的解放

画面に生成する画像・矩形・文字は `Node` です。`Node` はコピー不可の線形資源で、
GC に回収を委ねません。コンパイラが所有者を一つだけ追跡し、`drop`、所有権移動、
またはスコープ終了で一度だけ解放命令を出します。スコープ終了時の解放順は生成の
逆順です。

```aria
let mut mio = show image(asset("assets/ch/mio.webp")) at (760px, 86px) z 20;

borrow mut mio as portrait {
    move &mut portrait to (720px, 86px);
    hide &mut portrait;
}

// 同じ Node をコピーせず、所有権だけを移す。
let outro = mio;
drop outro;
```

`show` は `Node` を作ります。後で変形するなら束縛を `let mut` にします。

| 操作 | 意味 |
|---|---|
| `let n = show ...;` | 不変の Node 所有者を作る |
| `let mut n = show ...;` | 可変借用できる Node 所有者を作る |
| `let next = n;` | 所有権を `next` に移す。`n` は以後使えない |
| `drop n;` | 所有権を消費し、即時に画面資源を解放する |
| `borrow mut n as alias { ... }` | `n` をブロック中だけ排他的に貸し出す |
| `move &mut n to (...);` | 明示的な可変借用で位置を変更する |
| `hide &mut n;` / `reveal &mut n;` | 明示的な可変借用で可視性を変更する |

裸の Node 名を暗黙に変更することはできません。可変操作は常に `&mut` を必要とします。
借用中の所有者は使えず、借用エイリアスがブロックを抜けると所有者が再び利用可能に
なります。借用ブロック内で新しく作った Node も、ブロック終了時に解放されます。

### 制御フローでの規則

合流後にも実行が続く分岐では、すべての分岐が Node を同じ所有状態に残さなければ
なりません。

```aria
if route == 0 {
    drop mio;
} else {
    drop mio;
}
// 両分岐で消費済みなので、ここで mio は使えない。
```

片方だけで `drop mio;` して合流することはできません。ループはゼロ回実行される
可能性があるため、継続する反復の前後で外側 Node の所有状態を変えてはいけません。
これにより Node が二重解放・リーク・経路依存の寿命になることを防ぎます。

## 4. 物語と演出

基本の物語命令はすべてセミコロンで終えます。

```aria
scene shore {
    background asset("assets/bg/shore.webp") with fade(300ms);
    say Mio: "海へ行こう。";
    await advance;
    wait 250ms;

    choice {
        "堤防へ" => breakwater;
        "駅へ" => station;
    }
}
```

主な文は次の通りです。

- 文章: `say [Speaker:] "...";`、`narrate "...";`、`clear dialogue;`、`await advance;`
- 背景と Node: `background asset("...") [with fade(250ms)|wipe(250ms)];`、`let ... = show ...;`
- 制御: `if` / `else`、`while`、`choice`、`jump`、`call`、`return`、`end`
- 音: `play bgm|se|voice asset("...") [loop] [fade 250ms];`、`stop ...;`、`volume ... 0.8;`
- 作品状態: `flag "..." = on;`、`persistent flag "..." = on;`、`unlock chapter "...";`、`unlock cg "...";`
- 読書設定: `text_speed 24ms;`、`auto on;`、`skip read;`、`locale "ja-JP";`
- プレゼンテーションへの意味的遷移: `screen title;`、`screen settings;` など

`scene` は必ず `end`、`return`、`jump`、または `choice` で終えます。暗黙の次シーン
へのフォールスルーと再帰的な `call` は許可しません。

## 5. UI 境界

Aria は物語状態と意味的な画面遷移を所有します。React/Tauri/Web のプレゼンテーション
パッケージはレイアウト、色、フォント、アクセシビリティ、入力表示を所有します。

そのため、旧 `theme`、`textbox`、`menu`、`open`、`ui_theme`、`ui_screen`、
`ui_transition` は作者言語の機能ではありません。コンパイラは位置つき診断を出し、
VM に互換用 UI 命令を渡しません。

## 6. 診断

`aria check` は構文、型、制御フロー、資源寿命を同じフロントエンドで検査します。

| コード | 内容 |
|---|---|
| `E100` | 構文エラー |
| `E101` | `aria;` ではない旧言語ヘッダー/モード |
| `E104` | 型・演算・可変性の不一致 |
| `E106` | 不正な制御フロー |
| `E108` | 退役した UI 構文 |
| `E110` | 所有権移動または `drop` 後の Node 使用 |
| `E111` | 借用競合 |
| `E112` | `&mut` がない、または不変 Node の可変借用 |
| `E113` | Node のコピー、二重 `drop`、経路依存の所有状態 |

## 7. ツールチェーンと配布

```bash
# 解析・型検査・所有権検査
cargo run --locked -p aria-cli -- check examples/umikaze --release

# ヘッドレス実行
cargo run --locked -p aria-cli -- run examples/umikaze --headless

# ターゲット用 bundle を作成
cargo run --locked -p aria-cli -- build examples/umikaze --target web --out target/umikaze-web
```

コンパイラ、`check`、`build`、Native Player、Web runtime は同じ ARIAC7 ABI を検証します。
ARIAC7 は旧コンパイル成果物を読み込まず、互換 opcode や実行時モードを持ちません。

## 8. 明示的な非互換

以下は意図的にサポートしません。

- NScripter 風ラベル・レジスタ・裸テキスト命令
- `# aria-version`、`aria 3.x;`、`strict on/off`、`compat_mode`
- `import`、`var`、暗黙の可変 Node 操作
- 互換用ホスト opcode、旧バイトコード、旧 UI DSL

旧資料は履歴としてだけ残ります。新規作品、ツール、サンプル、テストは必ずこの文書の
`aria;` 構文を使います。
