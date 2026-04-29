# フラグ・カウンターコマンドリファレンス

ゲーム進行状態を記録・参照するFlagsカテゴリのコマンドを解説します。フラグ（真偽値）とカウンター（整数値）の4種類のストレージと、それぞれの永続性の違いを理解することが重要です。

---

## 目次

- [永続性の概要](#永続性の概要)
- [通常フラグ](#通常フラグ)
- [セーブフラグ](#セーブフラグ)
- [揮発フラグ](#揮発フラグ)
- [カウンター](#カウンター)
- [実践パターン](#実践パターン)

---

## 永続性の概要

AriaEngineには4種類のフラグ・カウンターストレージがあります。

| 種類 | コマンド接頭辞 | ストレージ | セーブデータ | 用途 |
|------|---------------|-----------|-------------|------|
| 通常フラグ | `flag` | `Flags` | `saves/persistent.ariasav` | シナリオ進行フラグ |
| セーブフラグ | `pflag` / `sflag` | `SaveFlags` | `saves/persistent.ariasav` | 章解放・CG解放など |
| 揮発フラグ | `vflag` | `VolatileFlags` | **保存されない** | 一時的な状態 |
| カウンター | `counter` | `Counters` | `saves/persistent.ariasav` | 数値の累積・管理 |

**重要な注意点:**

- `pflag`と`sflag`は**同じストレージ**を参照します。`set_pflag`で設定した値は`get_sflag`で読めます
- 揮発フラグ（`vflag`）はゲーム終了時に失われます。セーブ・ロードでも復元されません
- `persistent.ariasav`は暗号化・圧縮されて保存されます。手動での編集はできません
- フラグ名とカウンター名は**文字列**で指定します。引用符で囲むことを推奨します

---

## 通常フラグ

通常フラグはシナリオ進行やイベント発生有無を記録する最基本的なフラグです。セーブデータに含まれ、ゲーム再起動後も保持されます。

### `set_flag`

フラグを設定します。値が0以外なら`true`（1）、0なら`false`（0）として保存されます。

```aria
set_flag "フラグ名", 値
```

| 引数 | 型 | 説明 |
|------|-----|------|
| `フラグ名` | 文字列 | フラグを識別する名前 |
| `値` | 整数 | 0以外でON、0でOFF |

**例:**
```aria
set_flag "game_started", 1
set_flag "met_hero", 0
set_flag "door_unlocked", %unlock_state
```

---

### `get_flag`

フラグの状態を読み出し、結果を指定したレジスタに格納します。

```aria
get_flag "フラグ名", %レジスタ
```

| 引数 | 型 | 説明 |
|------|-----|------|
| `フラグ名` | 文字列 | 読み出すフラグの名前 |
| `%レジスタ` | レジスタ | 結果を格納する先（1または0） |

**例:**
```aria
get_flag "game_started", %is_started
if %is_started == 1
    text "続きから開始します"
endif
```

**注意:** 存在しないフラグを読むと0（OFF）が返されます。

---

### `clear_flag`

フラグをOFF（false）に設定します。`set_flag "名前", 0`と同じ意味です。

```aria
clear_flag "フラグ名"
```

| 引数 | 型 | 説明 |
|------|-----|------|
| `フラグ名` | 文字列 | OFFにするフラグの名前 |

**例:**
```aria
clear_flag "in_battle"
clear_flag "dialogue_active"
```

---

### `toggle_flag`

フラグの状態を反転します。ONならOFFに、OFFならONにします。

```aria
toggle_flag "フラグ名"
```

| 引数 | 型 | 説明 |
|------|-----|------|
| `フラグ名` | 文字列 | 反転するフラグの名前 |

**例:**
```aria
toggle_flag "debug_mode"
toggle_flag "auto_skip"
```

---

## セーブフラグ

セーブフラグは章解放、CG解放、ルート分岐の達成状態など、プレイヤーの全セーブデータを通じて共有すべき長期的なフラグに使用します。`pflag`（persistent flag）と`sflag`（save flag）は**同じストレージ**を参照します。新規スクリプトでは意図が明確な方を選んで使用してください。

### `set_pflag`

セーブフラグを設定します。

```aria
set_pflag "フラグ名", 値
```

| 引数 | 型 | 説明 |
|------|-----|------|
| `フラグ名` | 文字列 | フラグを識別する名前 |
| `値` | 整数 | 0以外でON、0でOFF |

**例:**
```aria
set_pflag "chapter_01", 1
set_pflag "chapter_02_unlocked", 1
```

---

### `set_sflag`

`set_pflag`と同じストレージに書き込みます。セーブフラグを設定します。

```aria
set_sflag "フラグ名", 値
```

**例:**
```aria
set_sflag "scenario_01_started", 1
set_sflag "route_seen", 1
```

---

### `get_pflag`

セーブフラグの状態を読み出します。

```aria
get_pflag "フラグ名", %レジスタ
```

**例:**
```aria
get_pflag "chapter_01", %is_unlocked
if %is_unlocked == 1
    text "章1は解放済みです"
endif
```

---

### `get_sflag`

`get_pflag`と同じストレージから読み出します。

```aria
get_sflag "フラグ名", %レジスタ
```

**例:**
```aria
get_sflag "route_seen", %route
```

---

### `clear_pflag`

セーブフラグをOFFにします。

```aria
clear_pflag "フラグ名"
```

**例:**
```aria
clear_pflag "chapter_01"
```

---

### `clear_sflag`

`clear_pflag`と同じストレージのフラグをOFFにします。

```aria
clear_sflag "フラグ名"
```

---

### `toggle_pflag`

セーブフラグの状態を反転します。

```aria
toggle_pflag "フラグ名"
```

---

### `toggle_sflag`

`toggle_pflag`と同じストレージのフラグを反転します。

```aria
toggle_sflag "フラグ名"
```

---

## 揮発フラグ

揮発フラグはゲームセッション中のみ有効な一時的な状態を記録します。セーブ・ロード、ゲーム再起動で失われます。

### `set_vflag`

揮発フラグを設定します。

```aria
set_vflag "フラグ名", 値
```

| 引数 | 型 | 説明 |
|------|-----|------|
| `フラグ名` | 文字列 | フラグを識別する名前 |
| `値` | 整数 | 0以外でON、0でOFF |

**例:**
```aria
set_vflag "in_scene", 1
set_vflag "skip_done", 1
```

---

### `get_vflag`

揮発フラグの状態を読み出します。

```aria
get_vflag "フラグ名", %レジスタ
```

**例:**
```aria
get_vflag "in_scene", %is_in_scene
if %is_in_scene == 1
    text "シーン内です"
endif
```

---

### `clear_vflag`

揮発フラグをOFFにします。

```aria
clear_vflag "フラグ名"
```

**例:**
```aria
clear_vflag "in_scene"
```

---

### `toggle_vflag`

揮発フラグの状態を反転します。

```aria
toggle_vflag "フラグ名"
```

---

## カウンター

カウンターは整数値を保存・累積するためのストレージです。スコア、HP、クリック回数などの数値管理に使用します。

### `set_counter`

カウンターの値を設定します。

```aria
set_counter "カウンター名", 値
```

| 引数 | 型 | 説明 |
|------|-----|------|
| `カウンター名` | 文字列 | カウンターを識別する名前 |
| `値` | 整数 | 設定する整数値 |

**例:**
```aria
set_counter "score", 0
set_counter "health", 100
set_counter "money", 1000
set_counter "current_chapter", %chapter
```

---

### `get_counter`

カウンターの値を読み出します。存在しないカウンターを読むと0が返されます。

```aria
get_counter "カウンター名", %レジスタ
```

| 引数 | 型 | 説明 |
|------|-----|------|
| `カウンター名` | 文字列 | 読み出すカウンターの名前 |
| `%レジスタ` | レジスタ | 結果を格納する先 |

**例:**
```aria
get_counter "score", %current_score
text "現在のスコアは ${%current_score} 点です"
```

---

### `inc_counter`

カウンターの値を増加させます。増加量を省略した場合は1増加します。

```aria
inc_counter "カウンター名" [, 増加量]
```

| 引数 | 型 | 説明 |
|------|-----|------|
| `カウンター名` | 文字列 | 増加させるカウンターの名前 |
| `増加量` | 整数 | 増やす値（省略時は1） |

**例:**
```aria
inc_counter "score", 10
inc_counter "click_count"
inc_counter "player_hp", %heal_amount
```

---

### `dec_counter`

カウンターの値を減少させます。減少量を省略した場合は1減少します。

```aria
dec_counter "カウンター名" [, 減少量]
```

| 引数 | 型 | 説明 |
|------|-----|------|
| `カウンター名` | 文字列 | 減少させるカウンターの名前 |
| `減少量` | 整数 | 減らす値（省略時は1） |

**例:**
```aria
dec_counter "health", 20
dec_counter "enemy_hp", %damage
dec_counter "turns_remaining"
```

---

## 実践パターン

### 章解放システム

```aria
; 章クリア時
set_pflag "chapter_01", 1
set_pflag "chapter_02_unlocked", 1

; 章選択画面で
get_pflag "chapter_02_unlocked", %is_unlocked
if %is_unlocked == 1
    text "章2：選択可能"
else
    text "章2：ロック中"
endif
```

### スコア管理

```aria
set_counter "score", 0

; イベント発生時
inc_counter "score", 10

; 最終結果表示
get_counter "score", %final_score
text "最終スコア：${%final_score}点"
```

### 一時的な状態管理（揮発フラグ）

```aria
; シーン開始時
set_vflag "in_scene", 1

; シーン内の処理...

; シーン終了時
clear_vflag "in_scene"
```

### 条件分岐との組み合わせ

```aria
set_flag "has_key", 1

get_flag "has_key", %has_key
if %has_key == 1
    text "鍵を使ってドアを開けた"
    set_flag "door_unlocked", 1
else
    text "鍵がかかっている"
endif
```

---

## 関連項目

- [セーブ・ロード](../tutorials/save-load.md) — フラグ・カウンターの永続データ詳細
- [スクリプト構文](../syntax.md) — 条件分岐（`if`）の構文
- [システムコマンド](system.md) — `save` / `load` コマンド
