# AriaEngine v3.0 言語・エンジン拡張計画

## TL;DR

> AriaEngineのスクリプト言語をv3.0に拡張。明示スコープ（scope/end_scope）、defer、local/global/persistent宣言、match/case、result/option、開発者ツール（lint/format）、エンジン機能（live reload/save UX）を実装。
>
> **推定工数**: XL（大規模）
> **並列実行**: YES - 5 Phase構成
> **Critical Path**: Phase 1（基盤）→ Phase 2（スコープ）→ Phase 3（高度言語）→ Phase 5（エンジン統合）

---

## Context

### Original Request
AriaEngineの言語設計を「小さいコア + 明示スコープ + 作者が組める高レベル化」に寄せる。以下の機能を追加：
- scope/end_scope、defer、local/global/persistent/save/volatile宣言
- const/enum強化、match/case、assert/panic
- lint/format/docコメント
- result/option、readonly/mut、owned sprite
- aria-pack安定化、live reload、save UX、backlog強化、既読率/解放率

### Interview Summary
**Key Decisions**:
- 全機能を1つの巨大計画に統合
- Metis推奨の技術依存順序で実行（元優先順位をFeature IDとして保持）
- C++like構文を維持
- 既存機能との後方互換性を保持（# aria-version: 3.0で新機能有効化）

### Research Findings
**Metis Review**:
- 既存実装済み: const/enum、func、struct、while、try-catch、lambda、switch、namespace、auto/var、ローカルスコープ、スプライト寿命管理、レジスタ分類
- PreprocessModernSyntaxは既に507行 - 新機能追加は複雑度崩壊のリスク
- 技術的依存関係: if/while→式強化→assert→scope→defer→local→match→result→lint/format

### Metis Review Applied
**Guardrails Applied**:
- match/case: リテラルマッチ + `_` ワイルドカード + `if`ガードのみ。ADT・destructuring禁止
- scope/end_scope: 明示ブロック + deferのみ。クロージャー・キャプチャ禁止
- defer: 文レベルのみ。LIFO。パラメータ化defer禁止
- result/option: 固定パラメータ特殊型のみ。ユーザ定義ジェネリクス禁止
- lint: 5コアルールのみ（未定義ラベル、未使用変数、型不一致、到達不能コード、スプライトリーク）
- format: インデント・スペーシング統一のみ。設定可能スタイル禁止
- doc comment: `///` func/structのみ。出力: JSON/Markdown

---

## Work Objectives

### Core Objective
AriaEngineスクリプト言語をv3.0に拡張し、「明示スコープ + 型安全性 + 開発者ツール + ゲーム機能」を実現する。

### Concrete Deliverables
- `.sisyphus/plans/aria-v3-roadmap.md` (本計画)
- `src/AriaEngine/Core/Parser.cs` 更新（新構文サポート）
- `src/AriaEngine/Core/VirtualMachine.cs` 更新（新opcode実行）
- `src/AriaEngine/Core/OpCode.cs` 更新（新opcode定義）
- `src/AriaEngine/Core/CommandRegistry.cs` 更新（新コマンド登録）
- CLIツール: `aria-lint`、 `aria-format`
- Save UX、Live Reload、Backlog強化エンジン機能

### Definition of Done
```bash
dotnet build src/AriaEngine  # Expected: 0 warnings, 0 errors
dotnet test src/AriaEngine.Tests  # Expected: 18+ pass, 0 fail
dotnet run --project src/AriaEngine  # Expected: 正常起動
```

### Must Have
- scope/end_scope + defer
- local/global/persistent/save/volatile宣言
- match/case（リテラルマッチ）
- assert/panic
- lint/format CLIツール
- aria-pack安定化

### Must NOT Have (Guardrails)
- ADT（代数データ型）・destructuring
- クロージャー・キャプチャ変数
- ユーザ定義ジェネリクス
- パラメータ化defer
- 設定可能フォーマットスタイル
- パターンマッチング以外の型システム拡張
- VMの完全書き換え

---

## Verification Strategy

### Test Decision
- **Infrastructure exists**: YES
- **Automated tests**: Tests-after（実装後にテスト追加）
- **Framework**: xUnit (existing)
- **Agent-Executed QA**: 全タスクに含む

### QA Policy
Every task MUST include agent-executed QA scenarios. Evidence saved to `.sisyphus/evidence/`.

---

## Execution Strategy

### Parallel Execution Waves

```
Phase 1 (Foundation - Parser Stabilization):
├── T1: if/while syntax cleanup (block vs inline clarification)
├── T2: Expression strengthening (array literals, chained comparisons)
├── T3: assert/panic implementation
└── T4: const/enum enhancement (type checking, scoping)

Phase 2 (Scope & Type System - VM Changes):
├── T5: scope/end_scope opcodes + parser support
├── T6: defer implementation (LIFO on scope exit)
├── T7: local/global/persistent/save/volatile declarations
├── T8: readonly/mut compile-time enforcement
└── T9: Save format versioning + migration

Phase 3 (Advanced Language):
├── T10: match/case (literal matching + wildcard + guards)
├── T11: result/option types (special types, fixed params)
├── T12: use/modules (functional imports, namespace resolution)
├── T13: owned sprite/resource types
└── T14: struct instantiation enhancement

Phase 4 (Developer Tools - Parallel):
├── T15: lint CLI tool (5 core rules)
├── T16: format CLI tool
└── T17: doc comment extraction (/// on func/struct)

Phase 5 (Engine Features - Separate Stream):
├── T18: aria-pack stabilization (encryption, diff patch)
├── T19: Live Reload (.aria save re-parse)
├── T20: Save UX (quicksave, autosave, thumbnails)
├── T21: Backlog enhancement (voice, jump back, search)
└── T22: Clear rate / read rate tracking

Wave FINAL (Verification):
├── F1: Plan compliance audit (oracle)
├── F2: Code quality review
├── F3: Real manual QA
└── F4: Scope fidelity check
-> Present results -> Get explicit user okay

Critical Path: T1→T2→T3→T5→T6→T7→T8→T10→T11→T12→T18→T20→F1-F4→user okay
Parallel Speedup: ~60% faster than sequential
Max Concurrent: 5 (Phase 1) + 5 (Phase 2) + 5 (Phase 3) + 3 (Phase 4) + 5 (Phase 5)
```

### Dependency Matrix

- **T1-T4**: - → T5-T14, Phase 4
- **T5**: T1-T4 → T6, T7, T8
- **T6**: T5 → T7, T10
- **T7**: T5-T6 → T8, T10, T13
- **T8**: T7 → T10, T13
- **T10**: T1-T4, T7-T8 → T11
- **T11**: T10 → T12
- **T12**: T11 → T13
- **T13**: T7-T8 → Phase 5
- **T15-T17**: T1-T4 → Phase 5 (independent, can run with Phase 2-3)
- **T18-T22**: T13, T15 → F1-F4

### Agent Dispatch Summary

- **Phase 1**: 4 tasks → all `quick`/`unspecified-high`
- **Phase 2**: 5 tasks → `deep`/`unspecified-high`
- **Phase 3**: 5 tasks → `deep`/`unspecified-high`
- **Phase 4**: 3 tasks → `quick`/`unspecified-high`
- **Phase 5**: 5 tasks → `deep`/`unspecified-high`/`visual-engineering`
- **FINAL**: 4 tasks → `oracle`/`deep`/`unspecified-high`

---

## TODOs

- [x] **T1. if/while構文整理（ブロック vs インライン境界の明確化）**

  **What to do**:
  - Parserのif/while処理を整理: 1行式とブロック式の境界を明確化
  - `{}` は任意、基本は `endif`/`wend` で安定
  - `if %flag == 1` → ブロックif（次のendifまで）
  - `if %flag == 1 { goto *next }` → 1行if（波括弧で囲まれた1文）
  - 同様にwhileも整理
  - 既存のRewriteModernBlockOpenを整理・統合

  **Must NOT do**:
  - 構文を破壊的に変更しない（既存スクリプトはそのまま動く）
  - 新しい制御構文は追加しない（elifなど）

  **Recommended Agent Profile**:
  - **Category**: `unspecified-high`
  - **Reason**: Parser変更は影響範囲が大きい。既存テストとの整合性が必要

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Phase 1 (with T2, T3, T4)
  - **Blocks**: T5-T14（Phase 2-3はParser基盤に依存）
  - **Blocked By**: None

  **References**:
  - `src/AriaEngine/Core/Parser.cs` - RewriteModernBlockOpen, if/while parsing
  - `src/AriaEngine/Core/Parser.cs` - Tokenizer（`{`, `}`の扱い）
  - `src/AriaEngine/assets/scripts/main.aria` - 既存if/while使用例

  **Acceptance Criteria**:
  - [ ] `if %x == 1\n  text "hello"\nendif` がブロックifとして正しくパースされる
  - [ ] `if %x == 1 { goto *label }` が1行ifとして正しくパースされる
  - [ ] `while %i < 10\n  inc %i\nwend` が正しくパースされる
  - [ ] 既存テスト18個がすべてPASS
  - [ ] main.ariaが正常にパースされる（ERROR出力なし）

  **QA Scenarios**:
  ```
  Scenario: ブロックifの正しいパース
    Tool: Bash
    Preconditions: Parserが初期化されている
    Steps:
      1. `dotnet test src/AriaEngine.Tests --filter "FullyQualifiedName~ParserTests"`
    Expected Result: 全テストPASS
    Evidence: .sisyphus/evidence/t1-block-if-parse.txt

  Scenario: 1行ifの正しいパース
    Tool: Bash
    Preconditions: 同上
    Steps:
      1. `dotnet test src/AriaEngine.Tests --filter "FullyQualifiedName~InlineIf"`
    Expected Result: PASS
    Evidence: .sisyphus/evidence/t1-inline-if-parse.txt
  ```

- [x] **T2. 式強化（配列リテラル、連鎖比較、三項演算子）**

  **What to do**:
  - ExpressionParserに配列リテラル構文を追加: `[1, 2, 3]`
  - 連鎖比較を追加: `1 <= %x <= 10`（`1 <= %x && %x <= 10`に展開）
  - 三項演算子を追加: `%x > 0 ? %a : %b`
  - 配列インデックスアクセスの強化: `%arr[%i]`
  - 文字列比較: `$name == "ayu"`

  **Must NOT do**:
  - メソッド呼び出し式は追加しない
  - 演算子オーバーロードは追加しない
  - 型推論の大幅拡張はしない

  **Recommended Agent Profile**:
  - **Category**: `deep`
  - **Reason**: Expression ASTの変更は再帰下降パーサー全体に影響

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Phase 1 (with T1, T3, T4)
  - **Blocks**: T10-T13（match/resultは式に依存）
  - **Blocked By**: None

  **References**:
  - `src/AriaEngine/Core/ExpressionParser.cs` - 再帰下降パーサー
  - `src/AriaEngine/Core/Expression.cs` - ASTノード
  - `src/AriaEngine/Core/VirtualMachine.cs` - EvaluateCondition

  **Acceptance Criteria**:
  - [ ] `[1, 2, 3]` が配列リテラルとしてパース・評価される
  - [ ] `1 <= %x <= 10` が連鎖比較として評価される
  - [ ] `%x > 0 ? %a : %b` が三項演算子として評価される
  - [ ] `%arr[%i]` が配列アクセスとして評価される
  - [ ] 既存テストがすべてPASS

  **QA Scenarios**:
  ```
  Scenario: 連鎖比較の評価
    Tool: Bash
    Steps:
      1. テストスクリプトで `let %x, 5` → `if 1 <= %x <= 10` → `text "in range"`
      2. `dotnet test` で確認
    Expected Result: 条件が真と評価される
    Evidence: .sisyphus/evidence/t2-chained-compare.txt
  ```

- [x] **T3. assert/panic実装**

  **What to do**:
  - assert: `assert %x >= 0, "x must be non-negative"`
    - デバッグモード（# aria-version: 3.0 + debug on）: 条件が偽ならpanic
    - リリースモード: no-op（または軽いログのみ）
  - panic: `panic "unexpected state"`
    - VMを停止し、エラーメッセージを表示
    - 既存のthrowメカニズムを再利用
  - Parser: PreprocessModernSyntaxでassert/panicを検出
  - VM: 新opcode `Assert`、`Panic` を追加（または既存throwを利用）

  **Must NOT do**:
  - スタックトレースの詳細出力は必須ではない
  - カスタム例外型は追加しない

  **Recommended Agent Profile**:
  - **Category**: `quick`
  - **Reason**: 既存throw機構のラッパー。比較的単純

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Phase 1 (with T1, T2, T4)
  - **Blocks**: None（独立機能）
  - **Blocked By**: None

  **References**:
  - `src/AriaEngine/Core/Parser.cs` - PreprocessModernSyntax
  - `src/AriaEngine/Core/OpCode.cs` - opcode定義
  - `src/AriaEngine/Core/Commands/ScriptCommandHandler.cs` - Throwハンドラ

  **Acceptance Criteria**:
  - [ ] `assert %x >= 0, "msg"` がデバッグ時に条件をチェック
  - [ ] 条件が偽ならpanic相当の動作
  - [ ] `panic "msg"` でVMが停止しメッセージ表示
  - [ ] リリースモードでassertがno-opまたは軽いログ

  **QA Scenarios**:
  ```
  Scenario: assert失敗でpanic
    Tool: Bash
    Steps:
      1. `assert 1 == 2, "should fail"` を含むスクリプト実行
    Expected Result: エラーメッセージ表示 + VM停止
    Evidence: .sisyphus/evidence/t3-assert-fail.txt
  ```

- [x] **T4. const/enum強化（型チェック・スコープ）**

  **What to do**:
  - 既存のconst/enumはプリプロセッサマクロとして動作
  - 強化: enum値の型チェック（関数引数にenum値を渡す際に検証）
  - 強化: constのスコープ制限（func内constはfuncスコープのみ）
  - 強化: enumの名前空間対応（`Route.Ayu` のような参照）
  - Parser: ValidateFunctionCallsでenum型パラメータをチェック

  **Must NOT do**:
  - enumメソッドは追加しない
  - 列挙型の反復（for value in enum）は追加しない

  **Recommended Agent Profile**:
  - **Category**: `unspecified-high`
  - **Reason**: 既存実装の上に型チェックを追加

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Phase 1 (with T1, T2, T3)
  - **Blocks**: T10-T12（match/useは名前空間に依存）
  - **Blocked By**: None

  **References**:
  - `src/AriaEngine/Core/Parser.cs` - TryParseConst, TryStartEnum
  - `src/AriaEngine/Core/Parser.cs` - ValidateFunctionCalls
  - `src/AriaEngine/Core/FunctionTable.cs` - FunctionInfo, ParameterInfo

  **Acceptance Criteria**:
  - [ ] enum値が関数のenum型パラメータに正しく渡せる
  - [ ] 不正なenum値の渡し方でパース時警告/エラー
  - [ ] func内constがfunc外から参照できない
  - [ ] `Route.Ayu` のような名前空間付きenum参照が可能

  **QA Scenarios**:
  ```
  Scenario: enum型チェック
    Tool: Bash
    Steps:
      1. `enum Route { common = 0, ayu = 1 }` → `func select(route: Route)`
      2. `select(5)` を呼び出し
    Expected Result: パース時警告「enum Route型が期待されます」
    Evidence: .sisyphus/evidence/t4-enum-type-check.txt
  ```

- [x] **T5. scope/end_scope実装（VM基盤変更）**

  **What to do**:
  - 新opcode: `ScopeEnter`, `ScopeExit`
  - VM: `VmExecutionState`に`ScopeStack`を追加（`Stack<ScopeFrame>`）
  - `ScopeFrame`: ローカル変数（int/string）、スプライト寿命、deferリスト
  - Parser: `scope`/`end_scope`をPreprocessModernSyntaxで検出
    - `scope` → `ScopeEnter` opcode
    - `end_scope` → `ScopeExit` opcode（defer実行 → スプライト削除 → ローカル破棄）
  - 制御フローとの相互作用を定義:
    - `return` inside scope: スコープのdefer実行 → 関数のdefer実行
    - `break`/`continue` inside scope: スコープのdefer実行 → ループ制御
    - `goto` out of scope: スコープのdefer実行 → ジャンプ
    - `throw` inside scope: スコープのdefer実行 → 例外伝播

  **Must NOT do**:
  - クロージャー・キャプチャ変数は追加しない
  - ブロックスコープ内の関数定義は禁止

  **Recommended Agent Profile**:
  - **Category**: `deep`
  - **Reason**: VM実行ループの深い変更。全制御フローに影響

  **Parallelization**:
  - **Can Run In Parallel**: NO（Phase 2の基盤）
  - **Parallel Group**: Phase 2（Sequential with T6-T9）
  - **Blocks**: T6-T9, T10-T13
  - **Blocked By**: T1-T4

  **References**:
  - `src/AriaEngine/Core/VirtualMachine.cs` - Step(), Update()
  - `src/AriaEngine/Core/GameState.cs` - VmExecutionState
  - `src/AriaEngine/Core/OpCode.cs` - opcode定義
  - `src/AriaEngine/Core/Commands/ScriptCommandHandler.cs` - Gosub/Return

  **Acceptance Criteria**:
  - [ ] `scope\n  let %x, 100\nend_scope` で%xがスコープ外から参照不可
  - [ ] `scope\n  lsp 10, "bg.png", 0, 0\nend_scope` でスプライト10が自動削除
  - [ ] `scope\n  defer csp 10\nend_scope` でdeferがLIFO順で実行
  - [ ] `return` inside scopeでスコープdefer + 関数deferが実行
  - [ ] 既存テストがすべてPASS

  **QA Scenarios**:
  ```
  Scenario: スコープ内スプライト自動削除
    Tool: Bash
    Steps:
      1. テストスクリプト: `scope\n  lsp 10, "bg.png", 0, 0\nend_scope\ncsp 10`（不存在のはず）
    Expected Result: csp 10がエラーにならない（既に削除済み）
    Evidence: .sisyphus/evidence/t5-scope-sprite-cleanup.txt
  ```

- [x] **T6. defer実装（LIFOスコープ出口実行）**

  **What to do**:
  - `defer <command>` をParserで検出
  - defer文を現在のスコープフレームのdeferリストに追加
  - スコープ出口時（end_scope、return、break、continue、goto、throw）にLIFO順で実行
  - deferの引数はdefer定義時に評価（キャプチャ）、実行時にコマンド実行
  - VM: `Defer` opcode追加、またはParserでdeferリストを管理

  **Must NOT do**:
  - パラメータ化deferは追加しない
  - 式レベルのdeferは追加しない

  **Recommended Agent Profile**:
  - **Category**: `deep`
  - **Reason**: 全制御フロー出口にdeferアンウィンドが必要

  **Parallelization**:
  - **Can Run In Parallel**: NO（T5と密接）
  - **Parallel Group**: Phase 2（with T5, T7-T9）
  - **Blocks**: T7（local宣言はscope上に構築）
  - **Blocked By**: T5

  **References**:
  - `src/AriaEngine/Core/VirtualMachine.cs` - PopFunctionScope, ReturnFromSubroutine
  - `src/AriaEngine/Core/Commands/FlowCommandHandler.cs` - Break, Continue, Jmp
  - `src/AriaEngine/Core/Parser.cs` - PreprocessModernSyntax

  **Acceptance Criteria**:
  - [ ] `defer csp %sp` がスコープ出口で実行
  - [ ] 複数deferがLIFO順で実行（最後に定義されたものが最初に実行）
  - [ ] defer内で例外が起きても残りのdeferが実行される
  - [ ] `return` inside scopeでdeferが実行された後にreturn

  **QA Scenarios**:
  ```
  Scenario: defer LIFO順実行
    Tool: Bash
    Steps:
      1. `scope\n  defer text "first"\n  defer text "second"\nend_scope`
    Expected Result: "second"が先に表示され、"first"が後に表示
    Evidence: .sisyphus/evidence/t6-defer-lifo.txt
  ```

- [x] **T7. local/global/persistent/save/volatile宣言実装**

  **What to do**:
  - 新構文: `local %x = 100`、`global $name = "ayu"`、`persistent %flag = 1`
  - Parser: 宣言を検出し、RegisterStoragePolicyに登録
  - VM: 宣言に応じた保存動作
    - `local`: 関数/スコープ終了で破棄（LocalIntStacks/LocalStringStacks）
    - `global`: プロセス終了まで保持（既存の通常レジスタと同じ）
    - `persistent`: セーブデータに保存（既存のPFlagと同じ）
    - `save`: セーブスロットに保存（既存のSaveFlagsと同じ）
    - `volatile`: リセットで初期化（既存のv_*命名規則と同じ）
  - 既存の命名規則（v_*, s_*, p_*）と共存、宣言が優先

  **Must NOT do**:
  - 既存のlet/mov構文を廃止しない
  - 宣言なし変数を禁止しない（後方互換性）

  **Recommended Agent Profile**:
  - **Category**: `deep`
  - **Reason**: RegisterStoragePolicyとVMの統合変更

  **Parallelization**:
  - **Can Run In Parallel**: NO（T5-T6と密接）
  - **Parallel Group**: Phase 2
  - **Blocks**: T8, T10, T13
  - **Blocked By**: T5-T6

  **References**:
  - `src/AriaEngine/Core/RegisterStoragePolicy.cs` - 既存分類
  - `src/AriaEngine/Core/GameState.cs` - LocalIntStacks, LocalStringStacks
  - `src/AriaEngine/Core/VirtualMachine.cs` - SetReg, GetReg, SetStr, GetString

  **Acceptance Criteria**:
  - [ ] `local %x = 100` で関数/スコープ内のみ有効
  - [ ] `persistent %flag = 1` でセーブ・ロードで復元
  - [ ] 宣言なし変数（`let %y, 5`）も依然として動作
  - [ ] SaveManagerでpersistent/save変数が正しくシリアライズ

  **QA Scenarios**:
  ```
  Scenario: local変数のスコープ
    Tool: Bash
    Steps:
      1. `func test()\n  local %x = 100\nendfunc\ntest()\ntext "%x"`
    Expected Result: %xは空または0（スコープ外で未定義）
    Evidence: .sisyphus/evidence/t7-local-scope.txt
  ```

- [x] **T8. readonly/mutコンパイル時強制**

  **What to do**:
  - `readonly %screen.w = 1280` - 再代入禁止
  - `mut %score = 0` - 明示的に可変（デフォルトはreadonlyに移行）
  - Parser/静的解析: 再代入を検出しエラー/警告
  - VM変更は不要（コンパイル時のみ）
  - structフィールドにも適用: `struct Button { readonly %x, mut %y }`

  **Must NOT do**:
  - ランタイム型タグは追加しない（VMは変更しない）
  - 既存スクリプトを壊さない（letは依然として可変として扱う）

  **Recommended Agent Profile**:
  - **Category**: `unspecified-high`
  - **Reason**: 静的解析の追加。VM変更なし

  **Parallelization**:
  - **Can Run In Parallel**: YES（T7と並列可能）
  - **Parallel Group**: Phase 2
  - **Blocks**: T13（ownedはmutabilityに依存）
  - **Blocked By**: T5-T6

  **References**:
  - `src/AriaEngine/Core/Parser.cs` - Parse結果のInstructionリスト
  - `src/AriaEngine/Core/AriaCheck.cs` - 既存静的チェック
  - `src/AriaEngine/Core/GameState.cs` - struct定義

  **Acceptance Criteria**:
  - [ ] `readonly %x = 100` の後に `%x = 200` でパースエラー
  - [ ] `mut %score = 0` の後に `%score = 100` で正常
  - [ ] 既存の`let %x, 100` → `%x = 200` は依然として正常（後方互換）

  **QA Scenarios**:
  ```
  Scenario: readonly再代入エラー
    Tool: Bash
    Steps:
      1. `readonly %x = 100\nlet %x, 200` をパース
    Expected Result: エラー「readonly変数%xに再代入できません」
    Evidence: .sisyphus/evidence/t8-readonly-error.txt
  ```

- [x] **T9. Saveフォーマットバージョニング + マイグレーション**

  **What to do**:
  - Saveフォーマットを"ARIASAVE2"から"ARIASAVE3"に更新
  - local/global/persistent/save/volatileの宣言情報をセーブに含める
  - ロード時: 旧バージョン（ARIASAVE2）を検出し、マイグレーション
  - SaveManagerにバージョンチェックとマイグレーションロジックを追加

  **Must NOT do**:
  - 既存セーブファイルを破壊しない（マイグレーション必須）

  **Recommended Agent Profile**:
  - **Category**: `unspecified-high`
  - **Reason**: SaveManagerの拡張。後方互換性が重要

  **Parallelization**:
  - **Can Run In Parallel**: YES（T7-T8と並列可能）
  - **Parallel Group**: Phase 2
  - **Blocks**: T20（Save UXはセーブフォーマットに依存）
  - **Blocked By**: T7

  **References**:
  - `src/AriaEngine/Core/SaveManager.cs` - 既存セーブ/ロード
  - `src/AriaEngine/Core/GameState.cs` - シリアライズ対象
  - `src/AriaEngine.Tests/SaveManagerTests.cs` - 既存テスト

  **Acceptance Criteria**:
  - [ ] ARIASAVE3で正しくセーブ・ロード
  - [ ] ARIASAVE2からARIASAVE3へのマイグレーション成功
  - [ ] マイグレーション後も既存データ（レジスタ、フラグ）が保持

  **QA Scenarios**:
  ```
  Scenario: 旧セーブマイグレーション
    Tool: Bash
    Steps:
      1. ARIASAVE2ファイルを作成（既存テストで）
      2. ARIASAVE3でロード
    Expected Result: データが正しく復元され、バージョンが3に更新
    Evidence: .sisyphus/evidence/t9-save-migration.txt
  ```

- [x] **T10. match/case実装（リテラルマッチ + ワイルドカード + ガード）**

  **What to do**:
  - 新構文:
    ```
    match %choice
    case 1
        goto *start
    case 2
        goto *load
    default
        goto *title
    endmatch
    ```
  - Parser: matchをif-gotoチェーンにトランスパイル
  - サポート: リテラルマッチ（数値、文字列）、`_`ワイルドカード、`if`ガード
  - 非網羅チェック: 警告（エラーではない）
  - 既存のswitch-caseとは別物（switchは既存、matchは新機能）

  **Must NOT do**:
  - ADT（代数データ型）は追加しない
  - destructuringは追加しない
  - パターンバインディングは追加しない

  **Recommended Agent Profile**:
  - **Category**: `deep`
  - **Reason**: 新しい制御構文。ParserとVMの両方に影響

  **Parallelization**:
  - **Can Run In Parallel**: NO（Phase 3の基盤）
  - **Parallel Group**: Phase 3
  - **Blocks**: T11
  - **Blocked By**: T1-T4（基盤）, T7-T8（宣言）

  **References**:
  - `src/AriaEngine/Core/Parser.cs` - PreprocessModernSyntax（switch-case参照）
  - `src/AriaEngine/Core/OpCode.cs` - 既存opcode
  - `src/AriaEngine/Core/Commands/FlowCommandHandler.cs` - 条件分岐

  **Acceptance Criteria**:
  - [ ] `match %x case 1 text "one" case 2 text "two" default text "other" endmatch` が動作
  - [ ] `_` ワイルドカードがデフォルトケースとして動作
  - [ ] `case 1 if %y > 0` ガードが動作
  - [ ] 非網羅matchで警告が出力
  - [ ] 既存テストがすべてPASS

  **QA Scenarios**:
  ```
  Scenario: matchリテラルマッチ
    Tool: Bash
    Steps:
      1. `match %x case 1 text "one" endmatch` で %x=1
    Expected Result: "one"が表示
    Evidence: .sisyphus/evidence/t10-match-literal.txt
  ```

- [x] **T11. result/option型実装（特殊型・固定パラメータ）**

  **What to do**:
  - 特殊型（ジェネリクスなし）:
    - `Option<int>` - `Some(5)` / `None`
    - `Result<int, string>` - `Ok(5)` / `Err("msg")`
  - Parser: 型注釈をパース、関数シグネチャで使用
  - VM: ランタイム型タグは不要（構造体として実装）
    - `Some`/`Ok`/`Err` は既存のレジスタ/フラグで表現
  - アクセス: `match`または`if_err`/`if_none`
  - 例: `if_err %result goto *fallback`

  **Must NOT do**:
  - ユーザ定義ジェネリクスは追加しない
  - メソッド呼び出し（`.unwrap()`など）は追加しない

  **Recommended Agent Profile**:
  - **Category**: `deep`
  - **Reason**: 型システムの拡張。VMへの影響

  **Parallelization**:
  - **Can Run In Parallel**: NO（T10と密接）
  - **Parallel Group**: Phase 3
  - **Blocks**: T12
  - **Blocked By**: T10

  **References**:
  - `src/AriaEngine/Core/Parser.cs` - FunctionInfo, ParameterInfo
  - `src/AriaEngine/Core/VirtualMachine.cs` - GetReg, SetReg
  - `src/AriaEngine/Core/Commands/FlowCommandHandler.cs` - 条件分岐

  **Acceptance Criteria**:
  - [ ] `Result<int, string>` 型の変数宣言が可能
  - [ ] `Ok(5)` で成功値を設定、`Err("msg")` でエラー値を設定
  - [ ] `if_err %result goto *fallback` でエラーチェック
  - [ ] 既存テストがすべてPASS

  **QA Scenarios**:
  ```
  Scenario: Resultエラーハンドリング
    Tool: Bash
    Steps:
      1. `let %result, Err("not found")` → `if_err %result goto *error`
    Expected Result: *errorラベルにジャンプ
    Evidence: .sisyphus/evidence/t11-result-error.txt
  ```

- [x] **T12. use/modules実装（関数インポート・名前空間解決）**

  **What to do**:
  - `use "ui"` - ui名前空間の関数をインポート
  - `use ui.Button` - 特定の型/関数のみインポート
  - 既存の`using`（placeholder）を機能させる
  - Parser: 名前空間解決（`ui.button()` → `ui_button()`）
  - モジュールファイル探索: `modules/ui.aria` など
  - 既存のnamespaceスタックを活用

  **Must NOT do**:
  - パッケージマネージャーは追加しない
  - 循環参照検出は必須ではない（将来対応）

  **Recommended Agent Profile**:
  - **Category**: `unspecified-high`
  - **Reason**: 名前空間解決。Parser変更

  **Parallelization**:
  - **Can Run In Parallel**: NO（T11と密接）
  - **Parallel Group**: Phase 3
  - **Blocks**: T13
  - **Blocked By**: T4（enum名前空間）, T10-T11

  **References**:
  - `src/AriaEngine/Core/NamespaceManager.cs` - 既存名前空間
  - `src/AriaEngine/Core/Parser.cs` - TryParseUsing, RewriteNamespaceLabels
  - `src/AriaEngine/Scripting/ScriptPreprocessor.cs` - include処理

  **Acceptance Criteria**:
  - [ ] `use "ui"` でui名前空間の関数が参照可能
  - [ ] `ui.button()` が `ui_button()` に解決
  - [ ] モジュールファイルが自動的にincludeされる
  - [ ] 既存テストがすべてPASS

  **QA Scenarios**:
  ```
  Scenario: useで関数インポート
    Tool: Bash
    Steps:
      1. `use "ui"` → `button("test", 100, 100, 1)` を呼び出し
    Expected Result: `ui_button` が正しく呼ばれる
    Evidence: .sisyphus/evidence/t12-use-import.txt
  ```

- [x] **T13. ownedスプライト/リソース型実装**

  **What to do**:
  - `owned sprite %bg` - スコープ終了時に自動削除
  - `lsp %bg, "bg.png", 0, 0` - ownedスプライトは寿命管理対象
  - コンパイル時注釈（VM変更最小）
  - 既存のSpriteLifetimeStacksを活用
  - 所有権移譲はv3.0では不要（所有権は宣言スコープに固定）

  **Must NOT do**:
  - 所有権移譲（move/ borrow）は追加しない
  - 参照カウントは追加しない

  **Recommended Agent Profile**:
  - **Category**: `unspecified-high`
  - **Reason**: SpriteLifetimeStacksの活用。比較的軽め

  **Parallelization**:
  - **Can Run In Parallel**: YES（T12と並列可能）
  - **Parallel Group**: Phase 3
  - **Blocks**: None
  - **Blocked By**: T7-T8（local/readonly）

  **References**:
  - `src/AriaEngine/Core/VirtualMachine.cs` - SpriteLifetimeStacks
  - `src/AriaEngine/Core/Commands/RenderCommandHandler.cs` - TrackSpriteLifetime
  - `src/AriaEngine/Core/GameState.cs` - VmExecutionState

  **Acceptance Criteria**:
  - [ ] `owned sprite %bg` で宣言したスプライトがスコープ終了時に削除
  - [ ] 非ownedスプライトは従来通り手動削除が必要
  - [ ] 既存テストがすべてPASS

  **QA Scenarios**:
  ```
  Scenario: ownedスプライト自動削除
    Tool: Bash
    Steps:
      1. `scope\n  owned sprite %bg\n  lsp %bg, "bg.png", 0, 0\nend_scope\ncsp %bg`
    Expected Result: csp %bgがエラーにならない（自動削除済み）
    Evidence: .sisyphus/evidence/t13-owned-sprite.txt
  ```

- [x] **T14. structインスタンス化強化**

  **What to do**:
  - 既存のstruct定義は動作しているが、インスタンス化構文がない
  - `new Button { %x = 100, %y = 200, $text = "OK" }` のような構文を追加
  - または: `let %btn, new Button` → `%btn.x = 100`
  - structはレジスタの集合として実装（既存の配列や連想配列と同じ）

  **Must NOT do**:
  - メソッドは追加しない
  - コンストラクタは追加しない
  - 継承は追加しない

  **Recommended Agent Profile**:
  - **Category**: `unspecified-high`
  - **Reason**: 既存struct定義の上にインスタンス化を追加

  **Parallelization**:
  - **Can Run In Parallel**: YES（Phase 3内で並列）
  - **Parallel Group**: Phase 3
  - **Blocks**: None
  - **Blocked By**: T4（struct定義）

  **References**:
  - `src/AriaEngine/Core/StructManager.cs` - 既存struct管理
  - `src/AriaEngine/Core/Parser.cs` - struct定義パース
  - `src/AriaEngine/Core/GameState.cs` - 配列・連想配列

  **Acceptance Criteria**:
  - [ ] `new Button { %x = 100 }` でstructインスタンスが作成
  - [ ] `%btn.x` でフィールドアクセス
  - [ ] 既存テストがすべてPASS

  **QA Scenarios**:
  ```
  Scenario: structインスタンス化
    Tool: Bash
    Steps:
      1. `struct Point { %x, %y }` → `let %p, new Point { %x = 10, %y = 20 }` → `text "%p.x"`
    Expected Result: "10"が表示
    Evidence: .sisyphus/evidence/t14-struct-instance.txt
  ```

- [x] **T15. lint CLIツール実装（5コアルール）**

  **What to do**:
  - CLIコマンド: `dotnet run --project src/AriaEngine -- aria-lint assets/scripts/main.aria`
  - 5コアルール:
    1. 未定義ラベル/関数呼び出し
    2. 未使用変数/レジスタ
    3. 関数呼び出しの型不一致
    4. 到達不能コード
    5. スプライトリーク（csp忘れ検出）
  - 出力: コンソール（ファイル名:行:カラム: 重大度: メッセージ）
  - 重大度: Error / Warning / Info
  - 終了コード: 0（問題なし）、1（警告あり）、2（エラーあり）

  **Must NOT do**:
    - 設定ファイルは追加しない
    - 50+ルールは追加しない
    - スタイルチェック（インデントなど）はformatに委譲

  **Recommended Agent Profile**:
  - **Category**: `quick`
  - **Reason**: 既存ParserとErrorReporterを再利用

  **Parallelization**:
  - **Can Run In Parallel**: YES（独立）
  - **Parallel Group**: Phase 4（with T16, T17）
  - **Blocks**: None
  - **Blocked By**: T1-T4（Parser基盤）

  **References**:
  - `src/AriaEngine/Tools/AriaCompileCommand.cs` - 既存CLIツールパターン
  - `src/AriaEngine/Core/ErrorReporter.cs` - エラー報告
  - `src/AriaEngine/Core/Parser.cs` - ParseResult（ラベル、関数一覧）

  **Acceptance Criteria**:
  - [ ] `aria-lint main.aria` で未定義ラベルを検出
  - [ ] `aria-lint main.aria` で未使用変数を検出
  - [ ] 終了コードが重大度に応じて変化
  - [ ] 既存テストがすべてPASS

  **QA Scenarios**:
  ```
  Scenario: lintで未定義ラベル検出
    Tool: Bash
    Steps:
      1. `goto *undefined_label` を含むスクリプトをlint
    Expected Result: エラー「未定義ラベル '*undefined_label'」
    Evidence: .sisyphus/evidence/t15-lint-undefined.txt
  ```

- [x] **T16. format CLIツール実装**

  **What to do**:
  - CLIコマンド: `dotnet run --project src/AriaEngine -- aria-format assets/scripts/main.aria`
  - 整形ルール:
    - インデント: スペース4つ（一貫性）
    - コマンドと引数の間: スペース1つ
    - 空行: 論理ブロック間に1行
    - ラベル: 左寄せ
    - コメント: インデントに合わせる
  - 出力: 標準出力または上書き（--writeフラグ）
  - 冪等性: 2回formatしても同じ結果

  **Must NOT do**:
    - 設定可能スタイルは追加しない
    - コメントの内容変更はしない

  **Recommended Agent Profile**:
  - **Category**: `quick`
  - **Reason**: テキスト整形のみ。VM変更なし

  **Parallelization**:
  - **Can Run In Parallel**: YES（独立）
  - **Parallel Group**: Phase 4
  - **Blocks**: None
  - **Blocked By**: T1-T4

  **References**:
  - `src/AriaEngine/Tools/AriaCompileCommand.cs` - CLIパターン
  - `src/AriaEngine/assets/scripts/main.aria` - 整形対象サンプル

  **Acceptance Criteria**:
  - [ ] `aria-format main.aria` で整形されたコードが出力
  - [ ] 冪等性: 2回formatしても同じ
  - [ ] 構文は変更されない（Parserで再パース可能）

  **QA Scenarios**:
  ```
  Scenario: format冪等性
    Tool: Bash
    Steps:
      1. `aria-format main.aria > formatted.aria`
      2. `aria-format formatted.aria > formatted2.aria`
      3. `diff formatted.aria formatted2.aria`
    Expected Result: 差分なし
    Evidence: .sisyphus/evidence/t16-format-idempotent.txt
  ```

- [x] **T17. doc comment実装（/// func/struct）**

  **What to do**:
  - 構文: `/// description` （func/structの直前）
  - 例:
    ```
    /// button(text, x, y, result)
    /// Creates a clickable text button.
    func button($text: string, %x: int, %y: int, %result: int)
    ```
  - Parser: doc commentをFunctionInfo/StructDefinitionに保存
  - 出力: JSONまたはMarkdown
  - CLI: `dotnet run --project src/AriaEngine -- aria-doc assets/scripts/main.aria --out docs/`

  **Must NOT do**:
    - `@param`, `@return` タグは追加しない（将来対応）
    - HTML出力は追加しない

  **Recommended Agent Profile**:
  - **Category**: `quick`
  - **Reason**: コメント収集と出力のみ

  **Parallelization**:
  - **Can Run In Parallel**: YES（独立）
  - **Parallel Group**: Phase 4
  - **Blocks**: None
  - **Blocked By**: T4（func/struct定義）

  **References**:
  - `src/AriaEngine/Core/FunctionInfo.cs` - 関数メタデータ
  - `src/AriaEngine/Core/StructDefinition.cs` - structメタデータ
  - `src/AriaEngine/Core/Parser.cs` - func/structパース

  **Acceptance Criteria**:
  - [ ] `/// comment` がfunc/structに紐付けて保存
  - [ ] `aria-doc` でJSON/Markdown出力
  - [ ] 既存テストがすべてPASS

  **QA Scenarios**:
  ```
  Scenario: doc comment抽出
    Tool: Bash
    Steps:
      1. `/// Creates a button.\nfunc button()\nendfunc` をパース
      2. FunctionInfo.DocComment を確認
    Expected Result: "Creates a button." が保存されている
    Evidence: .sisyphus/evidence/t17-doc-comment.txt
  ```

- [x] **T18. aria-pack安定化（暗号化pak・差分パッチ）**

  **What to do**:
  - `aria-pack` コマンドの安定化
  - 暗号化pakの検証（既存のAES+GZip）
  - 差分パッチ生成（`.patch` ファイル）
  - エラーハンドリング強化（破損pak検出）
  - 既存の `AriaPackCommand.cs` を強化

  **Must NOT do**:
    - 新しい暗号化方式は追加しない
    - 圧縮方式の変更はしない

  **Recommended Agent Profile**:
  - **Category**: `unspecified-high`
  - **Reason**: パッケージングツールの堅牢化

  **Parallelization**:
  - **Can Run In Parallel**: YES（独立）
  - **Parallel Group**: Phase 5
  - **Blocks**: None
  - **Blocked By**: None

  **References**:
    - `src/AriaEngine/Tools/AriaPackCommand.cs` - 既存パックコマンド
    - `src/AriaEngine/AssetIO/PakAssetProvider.cs` - pak読み込み

  **Acceptance Criteria**:
    - [ ] `aria-pack` で正常にpakファイル生成
    - [ ] 破損pakを検出してエラーメッセージ
    - [ ] 差分パッチが正しく適用

  **QA Scenarios**:
    ```
    Scenario: pak暗号化・復号
      Tool: Bash
      Steps:
        1. `aria-pack build --input assets --out test.pak --key testkey`
        2. pakファイルを復号して検証
      Expected Result: 元ファイルと一致
      Evidence: .sisyphus/evidence/t18-pack-roundtrip.txt
    ```

- [x] **T19. Live Reload実装**

  **What to do**:
  - `.aria` ファイル保存時に自動再パース
  - 画像差し替え即反映（テクスチャキャッシュ無効化）
  - フォント/設定は必要時のみ再読込
  - 現在ラベルを維持（リロード後に同じ位置から再開）
  - FileSystemWatcherまたはポーリング

  **Must NOT do**:
    - スクリプト実行中のリロードは安全に行わない（将来対応）
    - セーブデータの自動リロードはしない

  **Recommended Agent Profile**:
    - **Category**: `unspecified-high`
    - **Reason**: ファイル監視 + VM状態管理

  **Parallelization**:
    - **Can Run In Parallel**: YES
    - **Parallel Group**: Phase 5
    - **Blocks**: None
    - **Blocked By**: None

  **References**:
    - `src/AriaEngine/Program.cs` - メインループ
    - `src/AriaEngine/Core/VirtualMachine.cs` - LoadScript
    - `src/AriaEngine/Scripting/ScriptLoader.cs` - スクリプト読み込み

  **Acceptance Criteria**:
    - [ ] `.aria` 保存時に自動再パース
    - [ ] 画像差し替えが即反映
    - [ ] 現在ラベルが維持される

  **QA Scenarios**:
    ```
    Scenario: スクリプト変更リロード
      Tool: Bash
      Steps:
        1. エンジン起動 → スクリプト変更 → 保存
      Expected Result: 変更が反映され、同じラベルから再開
      Evidence: .sisyphus/evidence/t19-live-reload.txt
    ```

- [x] **T20. Save UX強化（クイックセーブ・オートセーブ・サムネイル）**

  **What to do**:
  - クイックセーブ/クイックロード（F5/F9）
  - オートセーブ（選択肢後・章開始時）
  - セーブサムネイル（スクリーンショット保存）
  - セーブ時刻/章名/本文抜粋をメタデータに保存
  - セーブ互換エラー時の説明ダイアログ

  **Must NOT do**:
    - UIデザインの全面リニューアルはしない
    - クラウドセーブは追加しない

  **Recommended Agent Profile**:
    - **Category**: `visual-engineering`
    - **Reason**: UI/UX + レンダリング

  **Parallelization**:
    - **Can Run In Parallel**: YES
    - **Parallel Group**: Phase 5
    - **Blocks**: None
    - **Blocked By**: T9（セーブフォーマットバージョニング）

  **References**:
    - `src/AriaEngine/Core/SaveManager.cs` - 既存セーブ管理
    - `src/AriaEngine/Core/GameState.cs` - 状態データ
    - `src/AriaEngine/Rendering/SpriteRenderer.cs` - スクリーンショット

  **Acceptance Criteria**:
    - [ ] F5でクイックセーブ、F9でクイックロード
    - [ ] オートセーブが選択肢後に作成
    - [ ] セーブサムネイルが正しく保存/表示

  **QA Scenarios**:
    ```
    Scenario: クイックセーブ・ロード
      Tool: Bash
      Steps:
        1. ゲーム進行 → F5 → さらに進行 → F9
      Expected Result: クイックセーブ時点に戻る
      Evidence: .sisyphus/evidence/t20-quicksave.txt
    ```

- [x] **T21. Backlog強化（ボイス・ジャンプバック・検索・未読マーク）**

  **What to do**:
  - ボイス再生（バックログ内でクリックして再生）
  - ジャンプバック（バックログの任意の行に戻る）
  - キャラ名/本文検索
  - 未読行マーキング
  - 既存のMenuSystem.cs backlog機能を強化

  **Must NOT do**:
    - 全文検索インデックスは追加しない（将来対応）

  **Recommended Agent Profile**:
    - **Category**: `visual-engineering`
    - **Reason**: UI/UX強化

  **Parallelization**:
    - **Can Run In Parallel**: YES
    - **Parallel Group**: Phase 5
    - **Blocks**: None
    - **Blocked By**: None

  **References**:
    - `src/AriaEngine/UI/MenuSystem.cs` - 既存backlog
    - `src/AriaEngine/Core/GameState.cs` - Backlogエントリ

  **Acceptance Criteria**:
    - [ ] バックログ内でクリックしてボイス再生
    - [ ] バックログの行にジャンプして戻る
    - [ ] 未読行が視覚的にマークされる

  **QA Scenarios**:
    ```
    Scenario: バックログジャンプ
      Tool: Bash
      Steps:
        1. テキスト進行 → バックログ表示 → 過去の行を選択
      Expected Result: 選択した行のシナリオ位置に戻る
      Evidence: .sisyphus/evidence/t21-backlog-jump.txt
    ```

- [x] **T22. 解放率・既読率トラッキング**

  **What to do**:
  - 既読テキスト行を追跡（GameState.ReadKeysに保存）
  - 解放CG・シナリオを追跡（GameState.UnlockedCgs）
  - 統計表示: 既読率（%）、CG解放率（%）
  - omake_ui.ariaに統計表示を追加

  **Must NOT do**:
    - 実績システムは追加しない

  **Recommended Agent Profile**:
    - **Category**: `quick`
    - **Reason**: 既存データ構造の活用

  **Parallelization**:
    - **Can Run In Parallel**: YES
    - **Parallel Group**: Phase 5
    - **Blocks**: None
    - **Blocked By**: None

  **References**:
    - `src/AriaEngine/Core/GameState.cs` - ReadKeys, UnlockedCgs
    - `src/AriaEngine/assets/scripts/omake_ui.aria` - 統計表示先

  **Acceptance Criteria**:
    - [ ] 既読テキストが正しく追跡される
    - [ ] 解放CGが正しく追跡される
    - [ ] omake_uiに統計表示が追加される

  **QA Scenarios**:
    ```
    Scenario: 既読率計算
      Tool: Bash
      Steps:
        1. シナリオ進行 → omake画面で統計確認
      Expected Result: 既読率が正しく表示
      Evidence: .sisyphus/evidence/t22-read-rate.txt
    ```

---

## Final Verification Wave

### F1. Plan Compliance Audit — `oracle`
Read plan end-to-end. Verify all "Must Have" exist, all "Must NOT Have" absent. Check evidence files exist. Compare deliverables against plan.
Output: `Must Have [N/N] | Must NOT Have [N/N] | Tasks [N/N] | VERDICT: APPROVE/REJECT`

### F2. Code Quality Review — `unspecified-high`
Run `dotnet build src/AriaEngine` + `dotnet test src/AriaEngine.Tests --no-build`. Review for AI slop patterns.
Output: `Build [PASS/FAIL] | Tests [N pass/N fail] | VERDICT`

### F3. Real Manual QA — `unspecified-high`
Execute every QA scenario. Test cross-task integration. Test edge cases. Save to `.sisyphus/evidence/final-qa/`.
Output: `Scenarios [N/N pass] | Integration [N/N] | Edge Cases [N tested] | VERDICT`

### F4. Scope Fidelity Check — `deep`
For each task: read "What to do", read actual diff. Verify 1:1 compliance. Check "Must NOT do" compliance. Detect cross-task contamination.
Output: `Tasks [N/N compliant] | Contamination [CLEAN/N issues] | Unaccounted [CLEAN/N files] | VERDICT`

---

## Commit Strategy

- **Phase 1**: `feat(parser): if/while syntax + expressions + assert` (T1-T4)
- **Phase 2**: `feat(vm): scope/defer/declarations/readonly` (T5-T9)
- **Phase 3**: `feat(lang): match/result/use/owned` (T10-T14)
- **Phase 4**: `feat(tools): lint/format/doc` (T15-T17)
- **Phase 5**: `feat(engine): pack/reload/save/backlog` (T18-T22)
- **FINAL**: `chore(verify): v3.0 compliance audit`

---

## Success Criteria

### Verification Commands
```bash
dotnet build src/AriaEngine  # Expected: 0 warnings, 0 errors
dotnet test src/AriaEngine.Tests  # Expected: 18+ pass, 0 fail
cd src/AriaEngine && dotnet run  # Expected: 正常起動
```

### Final Checklist
- [x] All "Must Have" present
- [x] All "Must NOT Have" absent
- [x] All tests pass
- [x] No regressions in main.aria, scenario_01.aria
- [x] Save format version bumped if needed
- [x] CLI tools (lint/format) produce expected output
- [x] Evidence files exist in .sisyphus/evidence/
