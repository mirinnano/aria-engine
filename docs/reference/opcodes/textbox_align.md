# `textbox_align` — ADV テキストの垂直配置

`textbox_align` は ADV モードにおけるテキストボックスの **垂直方向の配置** を切り替えるオペコードです。
従来の画面下端配置だけでなく、中央配置・上部配置を選べます。

> **T3 UX Quick Wins の一部として導入**: Save サムネイルバグ修正 (T1) と ボタン押下感 (T2) と並ぶ UX 改善です。

---

## 使い方

```aria
textbox_align top        ; テキストボックスを画面上部に配置（24px マージン）
textbox_align middle     ; テキストボックスを画面中央に配置
textbox_align bottom     ; テキストボックスを画面下端に配置（既定値、後方互換）
```

モード指定は大文字小文字を区別しません（`TOP` / `Top` / `top` すべて同じ動作）。

| モード | 配置 | 計算式 |
|--------|------|--------|
| `top` | 画面上端から 24px | `TextboxTopMargin = 24`（固定） |
| `middle` | 画面中央 | `(WindowHeight - DefaultTextboxH) / 2` |
| `bottom` | 画面下端 | `DefaultTextboxY`（テーマ既定値、約 500） |

---

## いつ使うか

### 1. 縦長の ADV モードで読みやすくしたい

画面の小さな縦型 ADV では、テキストが下に貼り付くと視線が下に偏りがちです。
`textbox_align middle` で中央配置にすると、視線の往復が減って読みやすくなります。

```aria
*start
textbox_align middle
bg "forest.png", 0
ミオ「山道を登っていると、鳥のさえずりが聞こえてきた。」
\
text "山道は木漏れ日が差し込んで、清々しい。"
\
```

### 2. 画面上部にメモや選択肢ヒントを出したい

上部配置は RPG 風の HUD 的な使い方を想定しています。

```aria
*hud_mode
textbox_align top
text "現在地: 森の奥"
\
```

### 3. 既存の ADV スクリプトを壊さず互換維持したい

既定値は `bottom` なので、本機能を導入する前のスクリプトは **挙動が変わらず動作** します。
`init.aria` でテーマを設定している場合は、テーマも `Bottom` に戻すため、互換性が保たれます。

---

## 適用範囲と優先順位

| 状況 | 優先される値 |
|------|-------------|
| `textbox x, y, ...` で明示的に Y 座標を指定した場合 | その Y 座標（最優先） |
| `textbox_align` で `top` / `middle` を指定した場合 | 計算式による Y 座標 |
| `textbox_align bottom` または未指定 | テーマ既定の `DefaultTextboxY` |
| テーマ初期化 (`Apply*Theme()`) | 自動的に `bottom` にリセット |

> **注意:** `textbox_align` は「**以降のテキスト表示で** Y 座標を計算し直す」ものです。一度表示したテキストボックスの位置を後から動かすわけではありません。

---

## フォールバック動作

- 不明な値（例: `"center"`, `"diagonal"`）を指定した場合: 現在の設定を維持します（エラーなし）。
- `textbox_align` 自体が v1.x 互換モードで使われた場合: 通常の opcode として登録されているため動作します。

---

## 内部実装

`ComputeTextboxY()` ヘルパーが `TextboxVerticalAlign` enum を読み取り、Y 座標を計算します。

```csharp
private const int TextboxTopMargin = 24;
private int ComputeTextboxY() {
    return State.TextWindow.VerticalAlign switch {
        TextboxVerticalAlign.Top => TextboxTopMargin,
        TextboxVerticalAlign.Middle => (State.EngineSettings.WindowHeight - State.TextWindow.DefaultTextboxH) / 2,
        _ => State.TextWindow.DefaultTextboxY
    };
}
```

`TextboxVerticalAlign` は `GameState.TextWindow.VerticalAlign` に保持され、テーマ初期化時に `Bottom` に戻ります。

---

## 関連 API

| 種類 | 名前 | 説明 |
|------|------|------|
| OpCode | `OpCode.TextboxAlign` | 登録番号 254/271（canonical/tokens） |
| Enum | `TextboxVerticalAlign` | `Top` / `Middle` / `Bottom` |
| プロパティ | `TextWindowState.VerticalAlign` | 現在の配置設定 |
| ヘルパー | `TextCommandHandler.ComputeTextboxY()` | 内部 Y 座標計算 |
| 詳細 | [`opcodes/ui.md`](../opcodes/ui.md#textbox_align) | 他のテキストボックスオペコード |

---

## テストカバレッジ

- `TextWindowState.VerticalAlign` の既定値が `Bottom` であることの確認
- 4 テーマすべてが `VerticalAlign = Bottom` を設定することの確認
- `OpCode.TextboxAlign` が `CommandRegistry` に登録されていることの確認
- `ComputeTextboxY()` の各モード（Top=24, Middle=中央, Bottom=DefaultTextboxY）の動作確認
