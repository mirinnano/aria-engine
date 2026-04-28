

# T1: if/while構文整理（ブロック vs インライン境界の明確化）

## 実装内容

### 1. RewriteModernBlockOpen の責務を明確化
- **変更前**: 1行ifブロック（`if cond { cmd }`）もブロックif開始（`if cond {`）も同じメソッドで処理していた
- **変更後**: 
  - 1行ifブロックの前処理を削除（Parse() で直接処理するフォールバック方式に変更）
  - ブロックif開始（`if cond {` → `if cond` + blockStack push）を維持
  - ブロックwhile開始（`while cond {` → `while cond` + blockStack push）を新規追加
  - `else {` → `else` を維持

### 2. TryCloseModernBlock の while 対応
- `blockStack.Pop()` の結果を確認し、`"while"` の場合は `wend` を出力、`"if"` の場合は `endif` を出力
- これにより `while cond { ... }` が前処理で `while cond ... wend` に正しく変換される

### 3. Parse() の if/while 境界を明確化
- **1行ifブロック** (`if cond { cmd }`): 波括弧を検出し、ブロック内コマンドを条件付き命令として直接生成。無効なコマンドの場合はエラーレポート。
- **インラインif** (`if cond cmd`): 波括弧なしでコマンドが見つかる場合、条件付き命令として生成。
- **ブロックif** (`if cond` ... `endif`): 波括弧なしでコマンドが見つからない場合、JumpIfFalse + ifStack を使用。
- **ブロックwhile** (`while cond` ... `wend`): 既存ロジックを維持。`while cond {` は前処理で wend 形式に変換済み。

### 4. エラーハンドリングの改善
- 1行ifブロック内に有効なコマンドが見つからない場合、以前は黙って無視していたが、現在はエラーを報告
- 空の1行ifブロック `{ }` もエラーを報告
- 関数呼び出しスタイルの空括弧 `()` を1行ifブロック内でもスキップ（`cls()` → `cls`）

## テスト結果
- 新規テスト11件を追加し全て通過:
  - `Parse_BlockIf_WithEndif_ProducesJumpIfFalseAndLabels`
  - `Parse_BlockIf_WithElse_ProducesElseAndEndLabels`
  - `Parse_BlockIf_WithBraces_PreprocessedToEndif`
  - `Parse_BlockIf_WithBracesAndElse_PreprocessedCorrectly`
  - `Parse_OneLineIfBlock_WithValidCommand_ProducesConditionalInstruction`
  - `Parse_OneLineIfBlock_WithGoto_ProducesConditionalJmp`
  - `Parse_OneLineIfBlock_WithInvalidCommand_ReportsError`
  - `Parse_InlineIf_WithoutBraces_ProducesConditionalInstruction`
  - `Parse_BlockWhile_WithWend_ProducesLoopLabels`
  - `Parse_BlockWhile_WithBraces_PreprocessedToWend`
  - `Parse_Break_InWhile_ProducesJmpToEndLabel`
  - `Parse_Continue_InWhile_ProducesJmpToStartLabel`
  - `Parse_MixedIfStyles_DoNotInterfere`
  - `Parse_MainAriaPatterns_BackwardCompatible`
- 既存テストも全て通過（合計50/50）

## 注意点・学び
- `RewriteModernBlockOpen` と `Parse()` の両方が1行ifを処理していたが、境界を明確化することでコードの重複を減らし、保守性を向上させた
- `main.aria` の実際のパースには `include` 未解決による既存のエラーが含まれるため、互換性テストでは主要なif/whileパターンを含む自己完結スクリプトを使用した
- `ErrorReporter.HasErrors` は存在しない。`Errors.Count` または `Errors.Should().BeEmpty()` を使用する必要がある
