# キャラクターコマンドリファレンス

キャラクターの定義、表示、表情・ポーズ変更、移動などを行うCompatibilityカテゴリのコマンドを解説します。

---

## 目次

- [キャラクターデータ](#キャラクターデータ)
- [表示・非表示](#表示非表示)
- [移動](#移動)
- [プロパティ変更](#プロパティ変更)

---

## キャラクターデータ

### `char_load`

キャラクターデータをJSONファイルから読み込みます。

```aria
char_load "path"
```

| 引数 | 型 | 説明 |
|------|-----|------|
| `path` | 文字列 | キャラクター定義JSONのファイルパス |

**例:**
```aria
char_load "characters.json"
```

**キャラクター定義JSONの形式:**

```json
{
  "Characters": {
    "mio": {
      "Id": "mio",
      "Name": "ミオ",
      "Expressions": {
        "normal": "ch/mio_normal.png",
        "happy": "ch/mio_happy.png",
        "sad": "ch/mio_sad.png"
      },
      "Poses": {
        "default": "ch/mio_default.png",
        "arms_crossed": "ch/mio_arms_crossed.png"
      },
      "DefaultX": 400,
      "DefaultY": 100,
      "DefaultScale": 1.0,
      "DefaultZ": 100
    }
  }
}
```

| フィールド | 型 | 説明 |
|-----------|-----|------|
| `Id` | 文字列 | キャラクター識別子（コマンドで使用） |
| `Name` | 文字列 | 表示名 |
| `Expressions` | オブジェクト | 表情名 → 画像パスのマッピング |
| `Poses` | オブジェクト | ポーズ名 → 画像パスのマッピング |
| `DefaultX` | 整数 | 表示時の初期X座標 |
| `DefaultY` | 整数 | 表示時の初期Y座標 |
| `DefaultScale` | 浮動小数 | 表示時の初期スケール（1.0 = 等倍） |
| `DefaultZ` | 整数 | 表示時の初期Z順序 |

**注意:**
- `char_load`を引数なしで呼び出すと、`characters.json`を読み込みます。
- 同名のキャラクターが既に存在する場合、上書きされます。
- ファイルが存在しない場合、エラーにはならず、空のキャラクターリストで続行します。

---

## 表示・非表示

### `char_show`

キャラクターを画面に表示します。

```aria
char_show id, [expression], [pose]
```

| 引数 | 型 | 必須 | 説明 |
|------|-----|------|------|
| `id` | 文字列 | ○ | キャラクターID（`char_load`で定義した`Id`） |
| `expression` | 文字列 | × | 表情名（デフォルト: `"normal"`） |
| `pose` | 文字列 | × | ポーズ名（デフォルト: `"default"`） |

**例:**
```aria
; デフォルト表情で表示
char_show "mio"

; 指定した表情で表示
char_show "mio", "happy"

; 指定したポーズで表示
char_show "mio", "normal", "arms_crossed"
```

**画像選択の優先順位:**
1. ポーズ名が指定され、かつ`Poses`に存在する → ポーズ画像を使用
2. 上記以外で表情名が`Expressions`に存在する → 表情画像を使用
3. どちらも存在しない → `"normal"`表情をフォールバック

**注意:**
- キャラクターは内部でスプライトID（5000番台から自動割り当て）として管理されます。
- 既に表示中のキャラクターを再度`char_show`すると、スプライトが上書きされます。
- 表示位置やスケールはJSONで定義した`DefaultX`/`DefaultY`/`DefaultScale`/`DefaultZ`が適用されます。

---

### `char_hide`

キャラクターを画面から非表示にします。

```aria
char_hide id, [fadeDuration]
```

| 引数 | 型 | 必須 | 説明 |
|------|-----|------|------|
| `id` | 文字列 | ○ | キャラクターID |
| `fadeDuration` | 整数 | × | フェードアウト時間（ミリ秒）。デフォルト: `300` |

**例:**
```aria
; 即座に非表示
char_hide "mio", 0

; 500msかけてフェードアウト
char_hide "mio", 500
```

**注意:**
- `fadeDuration`が`0`より大きい場合、不透明度を0に向かってトゥイーンし、その後スプライトを削除します。
- `fadeDuration`が`0`の場合、即座に非表示になります。
- 既に非表示のキャラクターに対して呼び出しても何も起こりません。

---

## 移動

### `char_move`

キャラクターを指定座標へ移動させます。

```aria
char_move id, x, y, [duration]
```

| 引数 | 型 | 必須 | 説明 |
|------|-----|------|------|
| `id` | 文字列 | ○ | キャラクターID |
| `x` | 整数 | ○ | 目標X座標 |
| `y` | 整数 | ○ | 目標Y座標 |
| `duration` | 整数 | × | 移動時間（ミリ秒）。デフォルト: `500` |

**例:**
```aria
; 左から右へ移動
char_show "mio"
char_move "mio", 800, 100, 1000
```

**注意:**
- 表示中のキャラクターに対してのみ有効です。非表示の場合は何も起こりません。
- X軸とY軸それぞれに独立したトゥイーンが生成され、同じ時間で同時に移動します。
- イージングは`EaseOut`（減速）が固定で適用されます。

---

## プロパティ変更

### `char_expression`

キャラクターの表情を変更します。

```aria
char_expression id, expression
```

| 引数 | 型 | 必須 | 説明 |
|------|-----|------|------|
| `id` | 文字列 | ○ | キャラクターID |
| `expression` | 文字列 | ○ | 表情名（JSONの`Expressions`に定義したキー） |

**例:**
```aria
char_show "mio"
char_expression "mio", "happy"
```

**注意:**
- 表示中のキャラクターに対してのみ有効です。
- 指定した表情名がJSONの`Expressions`に存在しない場合、何も起こりません。
- 表情変更は即座に反映され、トゥイーンは行われません。

---

### `char_pose`

キャラクターのポーズを変更します。

```aria
char_pose id, pose
```

| 引数 | 型 | 必須 | 説明 |
|------|-----|------|------|
| `id` | 文字列 | ○ | キャラクターID |
| `pose` | 文字列 | ○ | ポーズ名（JSONの`Poses`に定義したキー） |

**例:**
```aria
char_show "mio"
char_pose "mio", "arms_crossed"
```

**注意:**
- 表示中のキャラクターに対してのみ有効です。
- 指定したポーズ名がJSONの`Poses`に存在しない場合、何も起こりません。
- ポーズ変更は即座に反映され、トゥイーンは行われません。

---

### `char_z`

キャラクターのZ順序（重ね順）を変更します。

```aria
char_z id, z
```

| 引数 | 型 | 必須 | 説明 |
|------|-----|------|------|
| `id` | 文字列 | ○ | キャラクターID |
| `z` | 整数 | ○ | Z値。大きいほど手前に表示されます |

**例:**
```aria
; キャラクターAを手前に、キャラクターBを奥に
char_z "mio", 200
char_z "yuki", 50
```

**注意:**
- 表示中のキャラクターに対してのみ有効です。
- Z値はスプライトの描画順序を決定します。値が大きいほど手前に描画されます。

---

### `char_scale`

キャラクターの表示スケールを変更します。

```aria
char_scale id, scale
```

| 引数 | 型 | 必須 | 説明 |
|------|-----|------|------|
| `id` | 文字列 | ○ | キャラクターID |
| `scale` | 数値 | ○ | スケール倍率。`1.0`で等倍、`2.0`で2倍、`0.5`で半分 |

**例:**
```aria
; 等倍
char_scale "mio", 1.0

; 1.5倍に拡大
char_scale "mio", 1.5
```

**注意:**
- 表示中のキャラクターに対してのみ有効です。
- X軸・Y軸ともに同じ倍率が適用されます（等方スケーリング）。
- 負の値を指定すると、画像が反転します。

---

## 使用例

### 基本的なキャラクター表示フロー

```aria
; キャラクターデータを読み込む
char_load "characters.json"

; ミオを表示
char_show "mio"
wait 500

; 表情を変える
char_expression "mio", "happy"
wait 300

; 移動させる
char_move "mio", 600, 150, 800
wait 800

; ポーズを変える
char_pose "mio", "arms_crossed"

; 会話終了後、フェードアウトで退場
char_hide "mio", 500
```

### 複数キャラクターの重ね順管理

```aria
char_load "characters.json"

; 奥のキャラクター
char_show "yuki"
char_z "yuki", 50

; 手前のキャラクター
char_show "mio"
char_z "mio", 100

; ミオをさらに手前に
char_z "mio", 150
```
