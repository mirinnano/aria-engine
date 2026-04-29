# チャプターシステムコマンドリファレンス

チャプターの定義、アンロック、選択画面表示、進行度管理を行うCompatibilityカテゴリのコマンドです。チャプターシステムは`chapters.json`ファイルに永続化され、ゲーム再起動後も保持されます。

---

## 目次

- [チャプター定義](#チャプター定義)
- [チャプター選択](#チャプター選択)
- [チャプター管理](#チャプター管理)
- [チャプターカード](#チャプターカード)
- [実践パターン](#実践パターン)

---

## チャプター定義

チャプターは`defchapter`〜`endchapter`のブロックで定義します。各チャプターにはID、タイトル、説明、スクリプトパスを設定できます。定義されたチャプターは`chapters.json`に保存され、`ChapterManager`によって管理されます。

### defchapter

**カテゴリ**: Compatibility

**構文**:
```
defchapter
```

**引数**: なし

**説明**:
新しいチャプター定義ブロックを開始します。内部的に空の`ChapterInfo`オブジェクトを`State.CurrentChapterDefinition`に作成し、後続の`chapter_id`・`chapter_title`・`chapter_desc`・`chapter_script`コマンドでプロパティを設定します。ブロックは必ず`endchapter`で閉じる必要があります。

**例**:
```aria
defchapter
    chapter_id 1
    chapter_title "第一章 はじまり"
    chapter_desc "物語の始まり。新しい世界への第一歩。"
    chapter_script "assets/scripts/chapter1.aria"
endchapter
```

**関連コマンド**: [endchapter](#endchapter), [chapter_id](#chapter_id), [chapter_title](#chapter_title), [chapter_desc](#chapter_desc), [chapter_script](#chapter_script)

---

### chapter_id

**カテゴリ**: Compatibility

**構文**:
```
chapter_id 値
```

**引数**:
- `値`: 整数 — チャプターを識別する一意のID

**説明**:
現在定義中のチャプターにIDを設定します。`defchapter`ブロック内でのみ有効です。このIDは`unlock_chapter`や`chapter_progress`など、他のチャプター管理コマンドで参照されます。同じIDのチャプターが既に存在する場合、`endchapter`時にタイトル・説明・スクリプトパスが更新されます。

**例**:
```aria
defchapter
    chapter_id 2
endchapter
```

---

### chapter_title

**カテゴリ**: Compatibility

**構文**:
```
chapter_title "タイトル"
```

**引数**:
- `"タイトル"`: 文字列 — チャプターの表示名

**説明**:
現在定義中のチャプターにタイトルを設定します。`chapter_select`コマンドによる自動生成UIや`chapter_card`コマンドでこのタイトルが表示されます。`defchapter`ブロック内でのみ有効です。

**例**:
```aria
defchapter
    chapter_id 1
    chapter_title "第一章 はじまり"
endchapter
```

---

### chapter_desc

**カテゴリ**: Compatibility

**構文**:
```
chapter_desc "説明文"
```

**引数**:
- `"説明文"`: 文字列 — チャプターの説明テキスト

**説明**:
現在定義中のチャプターに説明文を設定します。`chapter_select`による自動生成UIで、タイトルの下に小さく表示されます。`defchapter`ブロック内でのみ有効です。

**例**:
```aria
defchapter
    chapter_id 1
    chapter_title "第一章 はじまり"
    chapter_desc "物語の始まり。新しい世界への第一歩。"
endchapter
```

---

### chapter_script

**カテゴリ**: Compatibility

**構文**:
```
chapter_script "ファイルパス"
```

**引数**:
- `"ファイルパス"`: 文字列 — このチャプターのメインスクリプトファイルへのパス

**説明**:
現在定義中のチャプターにスクリプトファイルのパスを設定します。ゲームフロー管理でこのパスが参照されることがあります。`defchapter`ブロック内でのみ有効です。

**例**:
```aria
defchapter
    chapter_id 1
    chapter_script "assets/scripts/chapter1.aria"
endchapter
```

---

### endchapter

**カテゴリ**: Compatibility

**構文**:
```
endchapter
```

**引数**: なし

**説明**:
チャプター定義ブロックを終了し、定義されたチャプターを`ChapterManager`に登録します。同じIDのチャプターが既に存在する場合は上書き更新、存在しない場合は新規追加されます。その後、`State.CurrentChapterDefinition`はクリアされ、定義中のチャプター情報は失われます。必ず`defchapter`と対で使用してください。

登録後、`chapters.json`への保存は即座には行われません。次回`unlock_chapter`などの保存を伴うコマンドが実行されるか、エンジン終了時に保存されます。

**例**:
```aria
defchapter
    chapter_id 3
    chapter_title "第三章 結末"
    chapter_desc "すべての謎が解き明かされる。"
    chapter_script "assets/scripts/chapter3.aria"
endchapter
```

**関連コマンド**: [defchapter](#defchapter)

---

## チャプター選択

### chapter_select

**カテゴリ**: Compatibility

**構文**:
```
chapter_select
```

**引数**: なし

**説明**:
チャプター選択画面を自動生成します。`ChapterManager`から取得した全チャプターを縦に並べたカードUIを生成し、各カードをボタンとして登録します。ボタン番号にはチャプターIDが割り当てられ、`btnwait`などの入力待機コマンドで選択結果を取得できます。

自動生成されるUIの詳細:
- スプライトID `2000`〜`2099`を使用（既存のスプライトは削除されます）
- チャプターごとに3つのスプライトを生成:
  - カード背景（`cardId`: Rectスプライト、幅600、高さ100）
  - タイトルテキスト（`cardId + 1`）
  - 説明テキスト（`cardId + 2`）
- アンロック状態に応じて見た目が変化:
  - アンロック済み: 背景色`#2a2a3e`、文字色白、ボタン有効
  - 未アンロック: 背景色`#1a1a2e`、文字色`#666688`、ボタン無効
- アンロック状態は`chapter.IsUnlocked`と`State.Flags[$"chapter{N}_unlocked"]`の両方を参照します

このコマンドを呼び出した後、`btnwait %result`で選択されたチャプターIDを取得できます。

**例**:
```aria
*chapter_select_screen
    bg "#1a1a2e", 0
    chapter_select
    btnwait %selected

    if %selected > 0
        text "チャプター %selected が選択されました"
    endif
```

**注意点**:
- 自動生成UIのスプライトID（2000〜2099）と競合しないよう注意してください
- 戻るボタンなどの追加UIは`chapter_select`呼び出し前に別途作成する必要があります
- `chapter_select`はスプライトID 2000〜2099の既存スプライトとボタンマップを**強制的に削除**します

**関連コマンド**: [btnwait](button.md#btnwait), [chapter_card](#chapter_card)

---

## チャプター管理

### unlock_chapter

**カテゴリ**: Compatibility

**構文**:
```
unlock_chapter チャプターID
```

**引数**:
- `チャプターID`: 整数 — アンロックするチャプターのID

**説明**:
指定したIDのチャプターをアンロック状態にします。`ChapterManager`内の該当チャプターの`IsUnlocked`を`true`に設定し、`LastPlayed`に現在時刻を記録します。操作後、自動的に`chapters.json`へ保存されます。

チャプターが存在しない場合、何も起こりません（エラーにはなりません）。

**例**:
```aria
; チャプター1クリア後にチャプター2をアンロック
set_flag "chapter_1_completed", 1
unlock_chapter 2
```

**関連コマンド**: [chapter_progress](#chapter_progress), [set_flag](flag.md#set_flag)

---

### chapter_thumbnail

**カテゴリ**: Compatibility

**構文**:
```
chapter_thumbnail チャプターID, "ファイルパス"
```

**引数**:
- `チャプターID`: 整数 — サムネイルを設定するチャプターのID
- `"ファイルパス"`: 文字列 — サムネイル画像ファイルへのパス

**説明**:
指定したチャプターのサムネイル画像パスを設定します。設定後、自動的に`chapters.json`へ保存されます。現在の実装では`chapter_select`の自動生成UIではサムネイルは表示されませんが、スクリプト側で`lsp`を使って独自のチャプター選択画面を構築する際に参照できます。

**例**:
```aria
chapter_thumbnail 1, "assets/chapters/chapter1_thumb.png"
chapter_thumbnail 2, "assets/chapters/chapter2_thumb.png"
```

---

### chapter_progress

**カテゴリ**: Compatibility

**構文**:
```
chapter_progress チャプターID, 進行度
```

**引数**:
- `チャプターID`: 整数 — 進行度を更新するチャプターのID
- `進行度`: 整数 — 0〜100の範囲で指定。範囲外の値はクリップされます

**説明**:
指定したチャプターの進行度（パーセンテージ）を更新します。`LastProgress`フィールドに設定され、同時に`LastPlayed`に現在時刻が記録されます。操作後、自動的に`chapters.json`へ保存されます。

進行度は0〜100の範囲に自動的にクリップされます（負の値は0、100を超える値は100に設定されます）。

**例**:
```aria
; チャプター1の進行度を50%に更新
chapter_progress 1, 50

; チャプター2を完了（100%）として記録
set_flag "chapter_2_completed", 1
chapter_progress 2, 100
```

**関連コマンド**: [unlock_chapter](#unlock_chapter)

---

### chapter_scroll

**カテゴリ**: Compatibility

**構文**:
```
chapter_scroll
```

**引数**: なし

**説明**:
現在の実装では**何も行いません**（no-op）。チャプター選択画面のスクロール機能の予約コマンドです。将来のバージョンで実装される可能性があります。

**例**:
```aria
; 現在は効果がありません
chapter_scroll
```

---

## チャプターカード

### chapter_card

**カテゴリ**: Compatibility

**構文**:
```
chapter_card スプライトID, "タイトル", "説明", X座標, Y座標
```

**引数**:
- `スプライトID`: 整数 — カード背景に使用するスプライトID
- `"タイトル"`: 文字列 — カードに表示するタイトル
- `"説明"`: 文字列 — カードに表示する説明文（空文字列可）
- `X座標`: 整数 — カードのX座標
- `Y座標`: 整数 — カードのY座標

**説明**:
チャプターカードを手動で作成します。指定したIDを基準として、最大3つのスプライトを生成します:

- `スプライトID`（背景）: Rectスプライト。幅600、高さ100、背景色`#333333`、ボタン有効
- `スプライトID + 1`（タイトル）: Textスプライト。24px、白色、カード左上から(20, 20)の位置
- `スプライトID + 2`（説明）: Textスプライト。16px、`#aaaaaa`色、カード左上から(20, 55)の位置。説明が空の場合は生成されません

`chapter_select`の自動生成とは異なり、個別にカードを配置してカスタムレイアウトを構築できます。ただし、アンロック状態の判定や見た目の切り替えはスクリプト側で実装する必要があります。

**例**:
```aria
; カスタムチャプター選択画面
csp -1
bg "#1a1a2e", 0

; タイトル
lsp_text 100, "チャプター選択", 640, 50
sp_fontsize 100, 36
sp_text_align 100, "center"
sp_color 100, "#ffffff"

; チャプターカードを手動配置
chapter_card 200, "第一章 はじまり", "新しい世界への第一歩", 340, 200
chapter_card 203, "第二章 展開", "物語が大きく動き出す", 340, 320
chapter_card 206, "第三章 結末", "すべての謎が解き明かされる", 340, 440

; ボタンとして登録
spbtn 200, 1
spbtn 203, 2
spbtn 206, 3

btnwait %selected
```

**注意点**:
- `スプライトID`、および`+1`・`+2`のIDが既存スプライトと競合しないよう注意してください
- このコマンドはアンロック状態を自動判定しません。必要に応じて`sp_fill`などで見た目を変更してください
- ボタン登録は自動で行われますが、入力待機は`btnwait`で別途行う必要があります

**関連コマンド**: [chapter_select](#chapter_select), [spbtn](button.md#spbtn), [btnwait](button.md#btnwait)

---

## 実践パターン

### 基本的なチャプター定義と選択

```aria
; init.aria またはタイトル画面でチャプターを定義
defchapter
    chapter_id 1
    chapter_title "第一章 はじまり"
    chapter_desc "物語の始まり"
    chapter_script "assets/scripts/chapter1.aria"
endchapter

defchapter
    chapter_id 2
    chapter_title "第二章 冒険"
    chapter_desc "新しい世界へ"
    chapter_script "assets/scripts/chapter2.aria"
endchapter

*chapter_select_screen
    ; チャプター選択画面を表示
    csp -1
    bg "#1a1a2e", 0
    chapter_select
    btnwait %selected

    if %selected == 1
        script "assets/scripts/chapter1.aria"
    elseif %selected == 2
        ; アンロック確認
        get_flag "chapter_2_unlocked", %is_unlocked
        if %is_unlocked == 1
            script "assets/scripts/chapter2.aria"
        else
            text "まだアンロックされていません"
            goto *chapter_select_screen
        endif
    endif
```

### チャプター完了時の進行度記録

```aria
*chapter1_end
    ; チャプター1を完了として記録
    set_flag "chapter_1_completed", 1
    chapter_progress 1, 100

    ; チャプター2をアンロック
    unlock_chapter 2
    set_flag "chapter_2_unlocked", 1

    text "第一章 完了！"
    goto *title_screen
```

### カスタムチャプター選択画面（chapter_card使用）

```aria
*custom_chapter_select
    csp -1
    bg "#1a1a2e", 0

    ; チャプター1（常にアンロック）
    chapter_card 200, "第一章", "はじまり", 340, 180
    spbtn 200, 1

    ; チャプター2（アンロック状態で見た目変更）
    get_flag "chapter_2_unlocked", %is_unlocked
    if %is_unlocked == 1
        chapter_card 203, "第二章", "冒険", 340, 300
        spbtn 203, 2
    else
        chapter_card 203, "第二章", "？？？", 340, 300
        sp_fill 203, "#1a1a1a", 255
        sp_color 204, "#555555"
        sp_color 205, "#444444"
    endif

    btnwait %selected
```

### chapters.jsonの構造

チャプターデータはエンジンと同じディレクトリにある`chapters.json`にJSON形式で保存されます:

```json
{
  "Chapters": [
    {
      "Id": 1,
      "Title": "第一章 はじまり",
      "Description": "物語の始まり",
      "ScriptPath": "assets/scripts/chapter1.aria",
      "IsUnlocked": true,
      "ThumbnailPath": "",
      "LastProgress": 100,
      "LastPlayed": "2026-04-29T10:00:00"
    }
  ]
}
```

ファイルが存在しない場合、エンジンは3つのデフォルトチャプターを自動生成します。

---

## コマンド一覧

| コマンド | 引数 | 説明 |
|---------|------|------|
| `defchapter` | なし | チャプター定義ブロックを開始 |
| `chapter_id` | `ID` | チャプターIDを設定 |
| `chapter_title` | `"タイトル"` | チャプタータイトルを設定 |
| `chapter_desc` | `"説明"` | チャプター説明を設定 |
| `chapter_script` | `"パス"` | スクリプトパスを設定 |
| `endchapter` | なし | チャプター定義を終了し登録 |
| `chapter_select` | なし | チャプター選択UIを自動生成 |
| `unlock_chapter` | `ID` | チャプターをアンロック |
| `chapter_thumbnail` | `ID`, `"パス"` | サムネイルパスを設定 |
| `chapter_card` | `ID`, `"タイトル"`, `"説明"`, `X`, `Y` | チャプターカードを手動生成 |
| `chapter_scroll` | なし | （予約、現在はno-op） |
| `chapter_progress` | `ID`, `進行度` | チャプター進行度を更新 |
