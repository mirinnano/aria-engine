# トラブルシューティング

AriaEngine でよく起きる問題とその解決方法です。各項目は「症状」「原因」「解決策」「予防」の4つで構成されています。

---

## 1. ビルドが失敗する

### 症状

```
dotnet build
```

を実行したときに、SDK 関連のエラーやターゲットフレームワークの不一致エラーが出る。

### 原因

.NET 8.0 SDK がインストールされていない、または複数の SDK が入っていて古いバージョンが使われている。

### 解決策

1. インストールされている SDK バージョンを確認する:

```bash
dotnet --version
```

2. `8.0.x` 以外が表示された場合、[Microsoft のサイト](https://dotnet.microsoft.com/download/dotnet/8.0) から .NET 8.0 SDK をインストールする。

3. `global.json` で SDK バージョンを固定している場合は、以下のように 8.0 系を指定する:

```json
{
  "sdk": {
    "version": "8.0.100",
    "rollForward": "latestFeature"
  }
}
```

4. ビルドし直す:

```bash
dotnet clean
dotnet build
```

### 予防

- CI/CD やチーム開発では `global.json` で SDK バージョンを固定する。
- エンジンの `README.md` や `AGENTS.md` に .NET 8.0 の要件が記載されているので、環境構築時に確認する。

---

## 2. スクリプトが解析されない

### 症状

ゲーム起動時にスクリプトが読み込まれず、真っ暗な画面のまま進まない。またはコンソールにパーサーエラーが出力される。

### 原因

- `.aria` ファイルの構文ミス（引用符の閉じ忘れ、引数の数の不足、ラベルの重複など）
- エンコーディング問題（BOM 付き UTF-8 以外で保存された場合）
- `init.aria` の `script` コマンドで指定したメインスクリプトのパスが間違っている

### 解決策

1. エラーログを確認する。`aria_error.log` と `aria_error_ai.txt` が作業ディレクトリに出力されている場合がある。

2. スクリプトの構文を検証するため、コマンドラインツール `aria-lint` を使う:

```bash
dotnet run -- aria-lint assets/scripts/main.aria
```

3. デバッグモードを有効にして、どの行で停止しているか確認する。`init.aria` に以下を追加するか、ゲーム中に `F3` キーを押す:

```aria
debug on
```

デバッグモード有効時は画面にプログラムカウンタ（PC）が表示され、どの命令を処理しているかがわかる。

4. 構文の典型的な間違いをチェックする:
   - 文字列引数は `"` で囲む（`lsp 1, "ch.png", 100, 200`）
   - 1行ifは `if %0 == 1 command` の形式（`then` は不要）
   - ラベルは `*label_name`（先頭のアスタリスクを忘れない）

### 予防

- スクリプトを保存するたびに `aria-lint` を実行する習慣をつける。
- 大きなスクリプトを書く前に、小さなテストスクリプトで構文を確認する。
- `include "path"` で分割管理し、1ファイルあたりの行数を抑える。

---

## 3. スプライトが表示されない

### 症状

`lsp` や `bg` コマンドで画像を読み込んだのに、画面に何も表示されない。

### 原因

1. **非表示状態 (`vsp`)**: スプライトは読み込まれているが `vsp id, off` または `vsp id, 0` で非表示になっている。
2. **不透明度 (`sp_alpha`)**: `sp_alpha` を `0` に設定している、またはアニメーションで消したままになっている。
3. **Zオーダー (`sp_z`)**: 他のスプライトや背景に背面に隠れている。背景 (`bg`) はデフォルトで背面に配置されるが、明示的な `sp_z` の指定が競合している場合がある。
4. **スプライトが削除されている (`csp`)**: 以前の `csp id` または `csp -1`（全削除）で消えている。

### 解決策

1. スプライトの表示状態を確認する:

```aria
vsp 1, on
```

または数値指定:

```aria
vsp 1, 1
```

2. 不透明度を確認する（`0` から `255` の範囲、`255` で完全に不透明）:

```aria
sp_alpha 1, 255
```

3. Zオーダーを確認する。値が大きいほど手前に表示される:

```aria
sp_z 1, 100
```

4. デバッグモード（`F3`）を有効にすると、スプライト数とボタンのヒットエリアが可視化される。スプライトが存在するか確認できる。

5. 念のためスプライトを再読み込みする:

```aria
csp 1
lsp 1, "assets/ch/mio.png", 400, 200
vsp 1, on
sp_alpha 1, 255
```

### 予防

- スプライトを読み込む際は、同じブロック内で `vsp id, on` と `sp_alpha id, 255` を明示的に設定する。
- `csp -1`（全スプライト削除）は場面切り替え時に便利だが、意図しないスプライト消失に注意する。
- 背景 (`bg`) とキャラクタースプライトの Zオーダーは `init.aria` または場面開始時に統一して設定する。

---

## 4. 音声が再生されない

### 症状

`play_bgm` や `play_se` を実行しても音が出ない。または、最初は鳴っていたのにある時点から鳴らなくなる。

### 原因

1. **ファイルパスの誤り**: 相対パスの基準が作業ディレクトリ（`src/AriaEngine` または `dotnet run` を実行した場所）であることを忘れている。
2. **ファイル形式の非対応**: Raylib が対応していない音声形式（一部の可変ビットレート MP3 や特殊な WAV 形式など）を使っている。
3. **キャッシュ上限**: BGM は最大8ファイル、SE は最大16ファイルまでキャッシュされる。それ以上読み込むと古いファイルが破棄されるが、まれに参照が不整合になることがある。
4. **読み込み失敗の記録**: 一度読み込みに失敗したファイルは内部で記録され、そのセッション中は再試行されない。
5. **音量設定**: `bgmvol` や `sevol`、`config.json` の音量が `0` に設定されている。

### 解決策

1. ファイルパスを確認する。エンジンはアセットプロバイダー経由で解決するので、作業ディレクトリからの相対パスが正しいか確認する:

```aria
play_bgm "assets/audio/bgm/title.ogg"
```

2. 対応形式を確認する。推奨は OGG または標準的な PCM WAV。MP3 も基本的に対応しているが、可変ビットレートは問題になることがある。

3. 音量設定を確認する:

```aria
bgmvol 100
sevol 100
```

または `config.json`:

```json
{
  "BgmVolume": 100,
  "SeVolume": 100
}
```

4. 読み込み失敗が記録されている場合は、エンジンを再起動する（一度失敗したファイルは同じセッションでは再読み込みされない）。

5. 音声デバイスが初期化されていない場合がある。コンソール出力に「音声デバイスの初期化に失敗しました」が出ていないか確認する。

### 予防

- 音声ファイルは `assets/audio/bgm/` や `assets/audio/se/` に分類して配置する。
- BGM はループ対応の OGG を使い、SE は短い WAV または OGG を使う。
- 音量設定は `init.aria` や `config.json` で初期値を決めておく。

---

## 5. ゲーム起動時にクラッシュする

### 症状

`dotnet run` 実行後、ウィンドウが表示される前、または直後にエンジンが異常終了する。

### 原因

1. **`init.aria` の不在**: `init.aria` が作業ディレクトリに存在しない。エンジンは起動時に必ずこのファイルを探す。
2. **`script` コマンドの誤り**: `init.aria` 内の `script` コマンドで指定したメインスクリプトが存在しない、またはパスが間違っている。
3. **フォントパスの誤り**: `font` コマンドで指定した TTF ファイルが存在しない。フォントパスが未設定の場合は警告が出て既定フォントで続行するが、ファイルが存在しない場合は読み込みエラーになることがある。
4. **`font_atlas_size` の極端な値**: `font_atlas_size` に極端に大きい値または小さい値を指定している。値は自動的に `8` ～ `512` の範囲にクランプされるが、それでもフォントの文字セットと合わない場合は失敗する。
5. **ウィンドウサイズの問題**: `window` コマンドで極端な値（`0` や数万ピクセル）を指定している。

### 解決策

1. `init.aria` が作業ディレクトリに存在するか確認する:

```bash
ls init.aria
```

2. `init.aria` の内容を最小構成でテストする:

```aria
window 1280, 720, "Test"
font "assets/fonts/NotoSansJP-Regular.ttf"
script "assets/scripts/main.aria"
```

3. フォントファイルのパスが正しいか、ファイルが実際に存在するか確認する:

```bash
ls assets/fonts/NotoSansJP-Regular.ttf
```

4. `font_atlas_size` はデフォルト値 `256` から始めて、必要に応じて `512` まで増やす。日本語フォントなど文字セットが多い場合は `512` を推奨する:

```aria
font_atlas_size 512
```

5. エラーログ `aria_error.log` を確認する。`ErrorReporter` が出力した内容に、どの段階（`init.aria` 実行、ウィンドウ初期化、フォント読み込み、メインスクリプト読み込み）で失敗したかが記録されている。

### 予防

- プロジェクトテンプレートには必ず `init.aria` とダミーの `main.aria` を含める。
- `init.aria` は設定のみを記述し、`text` などの表示コマンドを含めない（初期化中に表示されず、不具合の原因になる）。
- フォントファイルはプロジェクト作成時に必ず配置し、パスを `init.aria` で確認する。

---

## 6. テキストの表示がおかしい

### 症状

テキストが文字化けする、一部の文字が豆腐（□）になる、フォントサイズが想定と違う、テキストボックスの背景が表示されない。

### 原因

1. **フォントの文字セット不足**: 読み込んだフォントに、スクリプトで使っている文字が含まれていない。日本語テキストに対して英語フォントのみを読み込んでいる場合など。
2. **アトラスサイズ不足**: `font_atlas_size` が小さく、フォントの全文字をテクスチャに収められない。デフォルトの `256` では日本語フォントが不完全になることがある。
3. **フォントフィルタ設定**: `font_filter` の設定がフォントに合っていない。ドット絵フォントに `bilinear` を使うとぼやける。
4. **テキストボックス設定の未指定**: `textbox`、`fontsize`、`textcolor`、`textbox_color` などが未設定で、デフォルト値が意図と異なる。
5. **互換モードの影響**: `compat_mode off` の場合、テキストボックス背景は自動生成されない。手動で `lsp_rect` などを使う必要がある。

### 解決策

1. 日本語テキストを表示する場合は、Noto Sans JP など日本語グリフを含むフォントを使う:

```aria
font "assets/fonts/NotoSansJP-Regular.ttf"
```

2. アトラスサイズを増やす:

```aria
font_atlas_size 512
```

3. フォントフィルタを調整する。ドット絵やピクセルフォントの場合は `point` を使う:

```aria
font_filter "point"
```

4. テキストボックスの設定を明示的に行う:

```aria
textbox 50, 500, 1180, 200
fontsize 32
textcolor "#ffffff"
textbox_color "#0b0d10", 226
```

5. 互換モードが不要な場合でも、テキストボックス背景を手動で作るか、`compat_mode on` で自動生成を有効にする:

```aria
compat_mode on
```

### 予防

- プロジェクト開始時にフォントの文字カバレッジ（特に日本語）を確認する。
- `init.aria` で `font`、`font_atlas_size`、`textbox` 系の設定を必ず行う。
- 表示確認用のテストスクリプトを作り、全ての常用漢字・ひらがな・カタカナが正しく表示されるか確認する。

---

## 7. セーブサムネイルがセーブメニュー画面になっている (T1)

### 症状

セーブデータのサムネイル画像が、ゲーム本編ではなく **セーブメニュー UI が表示された状態** で記録されている。

### 原因

T1 (UX Quick Wins) で導入された `MenuSystem._vm.PrepareThumbnail()` 経由のキャプチャが効いていない。`VM.SaveGame()` 側で `CaptureThumbnail()` フォールバックになっている可能性が高い。

### 解決策

1. `MenuSystem.OpenSaveLoadMenu(true)` が呼ばれているか確認する。呼ばれていない場合、手動で `_vm.PrepareThumbnail()` を呼んでから `save` を実行する。
2. ウィンドウが未初期化の場合、`PrepareThumbnail()` は no-op になる。エンジン起動後・ウィンドウ表示後にセーブ操作を行う。
3. それでも直らない場合は `[JsonIgnore]` 属性が付与された `Sprite.IsPressed` 状態など、無関係なランタイム状態が原因の可能性は低い。`MenuSystem.cs` 63行目と `VirtualMachine.cs` 1420-1429行目を確認。

### 予防

- セーブメニューは `OpenSaveLoadMenu(true)` 経由で開く（手動で `MenuState.Save` を設定しない）
- セーブ直前に `csp -1` で全スプライトを消すコードを書かない（キャプチャ対象が背景だけになる）

---

## 8. ボタンを押しても押下感（沈み込み・色変化）がない (T2)

### 症状

`spbtn` / `sp_isbutton` でボタンに設定したスプライトが、マウスでクリックしても視覚的に反応しない（押下時のスケール・色・Y オフセット変化がない）。

### 原因

1. **`ui_theme` が `classic` / `soft` / `glass` / `mono` のいずれにも設定されていない**: デフォルト値（`(default)`）は ButtonFeel 設定が空のため、視覚的フィードバックが発生しない。
2. **スプライトが `IsButton = true` でない**: `spbtn` を呼んでいないか、`sp_isbutton` で `false` に上書きしている。
3. **`Sprite.IsPressed` が renderer-owned runtime**: スクリプトから直接 `IsPressed` を `true` にしても毎フレーム `false` にリセットされる（仕様）。

### 解決策

1. `init.aria` でテーマを明示する:

```aria
theme "soft"
```

2. ボタンスプライトであることを確認する:

```aria
sp_isbutton 100, true
spbtn 100, 1
```

3. `SpriteRenderer` が `IsPressed && IsButton` を毎フレーム評価している。`UpdateUiPresentation()` の呼び出し経路を `InputHandler` → `SpriteRenderer` で確認。

### 予防

- `init.aria` の `theme` 宣言を必ず含める（リリースビルドでも `classic` 推奨）
- ボタンの `IsButton` フラグは `spbtn` 呼出し時点で自動設定される。手動で `sp_isbutton ... false` にしない

---

## 9. ADV モードのテキストが垂直方向で配置できない (T3)

### 症状

`textbox_align center` / `textbox_align top` などの指示がスクリプトに書かれているのに、テキストが常に画面下部に配置される。

### 原因

1. **`OpCode.TextboxAlign` が認識されていない**: スクリプトパーサーが新しい opcode を認識していない（古いスナップショットやキャッシュ）。
2. **`TextboxVerticalAlign` enum が反映されていない**: `TextWindowState.VerticalAlign` フィールドが反映される前にテキストが描画される。

### 解決策

1. `init.aria` での `textbox_align` 設定を確認する（umikaze の場合: `textbox_align bottom` がデフォルト）。
2. ランタイムでも変更可能: `textbox_align center` / `textbox_align top` / `textbox_align bottom` のいずれかを実行する。
3. 垂直方向計算は `ComputeTextboxY()` ヘルパーで行われる。フォントサイズ・テキストボックス高さに整合性があることを確認。

### 予防

- プロジェクト開始時に `init.aria` で `textbox_align` を明示的に設定する
- 中央配置後、キャラクタースプライトとの重なりがないか確認

---

## 10. dev ビルドのはずなのに release の pak が読み込まれる

### 症状

- `dotnet run` で起動したのに、リリース用 `.pak` の中のアセットが表示される
- ログやアセット参照が `data.pak` / `boot.arib` などのリリースアーティファクトに向いている
- ゲームのスクリプトや画像が、リポジトリで編集したものではなく、古いリリース版のものになる

### 原因

`Program.cs` の自動モード検出が緩すぎた。従来は exe と同じディレクトリに v3 split pak の **いずれか 1 つでも** 存在すれば Release モードに自動切替していたため、リリースビルドの stray ファイル（例: 昔のビルドの `data.arid` だけ残っている）が dev ディレクトリに転がっていると dev → release に flip していた。

### 解決策

自動検出のルールを厳格化した（`Program.cs`）:

- **v3 split**: `boot.arib` **AND** `scenario.aris` の両方が存在する場合のみ Release 自動選択
- **v2 single-pak**: `data.pak` **AND** `scripts/scripts.ariac` の両方が存在する場合のみ Release 自動選択
- **オプトアウト**: 環境変数 `ARIA_AUTO_RELEASE=0` をセットすると自動検出を完全に無効化できる

```powershell
# 自動検出を無効化して dev 強制
$env:ARIA_AUTO_RELEASE='0'
dotnet run --project src/AriaEngine/AriaEngine.csproj

# 明示的に release を指定（自動検出に依らない）
dotnet run --project src/AriaEngine/AriaEngine.csproj -- --run-mode release
```

### 予防

- dev ビルドディレクトリと release ビルドディレクトリを物理的に分ける
- リリース配布時はクリーンなディレクトリに `.exe` + 必要 pak のみをコピーする
- リポジトリの `saves/` / `bin/` / `obj/` は `.gitignore` 済みなので、ここのファイルがコミットされることはない

---

## その他のヒント

### エラーログの場所

エンジンは以下のファイルにエラーログを出力する:

- `aria_error.log` — 人間が読みやすい形式
- `aria_error_ai.txt` — AI デバッグ用の構造化テキスト
- `aria_error_ai.json` — JSON 形式

これらは作業ディレクトリ（`dotnet run` を実行した場所）に作成される。

### デバッグモード

`F3` キーでデバッグオーバーレイを切り替えられる。以下が表示される:

- FPS
- プログラムカウンタ（現在の命令インデックス）
- スプライト数
- ボタンのヒットエリア（赤枠）

### コマンドラインツール

エンジンはいくつかのコマンドラインツールを内包している:

```bash
# スクリプト構文チェック
dotnet run -- aria-lint <file.aria>

# スクリプトフォーマット
dotnet run -- aria-format <file.aria>
```

---

## 参考資料

- [init.aria リファレンス](../reference/init-aria.md) — 初期化設定の詳細
- [スプライトリファレンス](../reference/opcodes/sprite.md) — スプライトコマンド一覧
- [オーディオリファレンス](../reference/opcodes/audio.md) — 音声コマンド一覧
- [システムリファレンス](../reference/opcodes/system.md) — `save`、`load`、`debug` など
- [`textbox_align` 詳細](../reference/opcodes/textbox_align.md) — T3 ADV テキスト垂直配置
- [ButtonFeel 詳細](../reference/ui/button-feel.md) — T2 ボタン押下感
- [アーキテクチャ: 概要](../architecture/overview.md) — エラー処理とErrorReporterの構成
- [アーキテクチャ: テキストサブシステム](../architecture/text-subsystem.md) — テキストボックス描画パイプライン
