# Aria Engine - 追加機能ドキュメント

> 最終更新: 2026-05-02

---

## 画面遷移（Transition）

`transition bg, "bg_path", <style>, <duration_ms>` 命令で背景切り替え時の演出を指定。

| style | 効果 |
|---|---|
| `fade` / `crossfade` | クロスフェード（デフォルト） |
| `slide_left` / `slideleft` | 右から左へ黒幕スライド |
| `slide_right` / `slideright` | 左から右へ黒幕スライド |
| `slide_up` / `slideup` | 下から上へ黒幕スライド |
| `slide_down` / `slidedown` | 上から下へ黒幕スライド |
| `wipe` / `circle` / `wipe_circle` | 中央から円形拡大 |

```aria
; 例
transition bg, "bg_night.png", slide_left, 800
transition bg, "bg_morning.png", wipe, 600
```

---

## テキスト演出

### 文字送り音（1文字ごとのSE）

タイプライターで1文字表示されるたびにSEを再生する。インラインタグで指定。

```aria
; クリック音を鳴らしながら表示
text "[se=click.wav]カチカチと文字が表示される[/se]"

; 音量指定も可（0-100、デフォルト100）
text "[se=type.wav][voicevol=50]静かな文字送り音[/voicevol]"
```

タグ: `[se=path]`, `[voice=path]`, `[sevol=N]`, `[voicevol=N]`

### ルビ（ふりがな）

本文の上に小さくふりがなを表示。

```aria
text "[ruby=かんじ]漢字[/ruby]の[ruby=よ]読[/ruby]み[ruby=かた]方[/ruby]"
```

タグ: `[ruby=よみがな]本文[/ruby]`, `[rt=よみがな]本文[/rt]`

---

## 画面エフェクト

### ビネット（Vignette）

画面の四隅を暗くする。回想シーンや集中演出に。

```aria
screen vignette           ; デフォルト強度（0.5）
screen vignette, 200      ; 強度指定（0-255）
screen clear              ; 解除
```

### 画面フラッシュ

```aria
screen flash              ; 白フラッシュ
screen flash, "#ff0000"   ; 赤フラッシュ
screen flash, "#ffffff", 200  ; 白・200ms
```

### パーティクル

雨・雪・桜のパーティクル演出。

```aria
screen particle           ; 雨
screen particle_snow      ; 雪
screen particle_sakura    ; 桜
screen particle_stop      ; 停止
```

---

## デバッグ機能

### F3 デバッグオーバーレイ

開発ビルド（Debug構成）でのみ有効。

- PC（プログラムカウンタ）
- スプライト数
- レジスタ値（%0〜%9、非ゼロのみ）
- フラグ数（Flags, SaveFlags）
- 現在のシーン / VM状態 / チャプター
- テキストバッファプレビュー（先頭40文字）

---

## 既存機能の改善

### テキスト配置の型安全化

`TextAlignment` enum:
- `Left` / `Center` / `Right`

`TextVerticalAlignment` enum:
- `Top` / `Center` / `Middle` / `Bottom`（`Middle` は `Center` のエイリアス）

### VMエラーレポート強化

実行時エラーに OpCode、プログラムカウンタ、引数リストの詳細情報が追加された。

### GameState リファクタリング

パススループロパティが削除され、サブオブジェクト経由の直接アクセスに統一された。
例: `state.DefaultTextboxX` → `state.TextWindow.DefaultTextboxX`

---

## インストーラ

Rust製（264KB、自己完結）。NSIS風UI。

- 製品情報表示（名前・バージョン）
- インストール先選択（参照ボタン付き）
- デフォルト: `C:\Program Files\Ponkotusoft\umikaze`
- デスクトップ / スタートメニューショートカット（チェックボックス）
- ファイル飛行アニメーション
- インストール進捗ログ
- .NET 8 ランタイムチェック（未インストール時はDL誘導）

---

## リリースビルド

```powershell
$env:ARIA_PACK_KEY = "your-32-char-key"
./scripts/release.ps1 -Version "v1.0.0"
```

出力: `artifacts/release/AriaEngine-v1.0.0-portable/dist/*.zip`

エンジンは `data.pak` と `aria.key` を自動検出してReleaseモードで起動する。
