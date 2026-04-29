# AriaEngine ドキュメント全面刷新計画

## TL;DR

> **Quick Summary**: AriaEngineの全ドキュメントをDiátaxis Frameworkに基づき再構成。エンドユーザー向けは日本語でTutorial/How-to/Reference/Explanationに整理、AIエージェント向けは英語で統一。既存の事実誤り（オペコード数の虚偽記載、動かないチュートリアルコード、未文書化コマンドなど）をすべて修正する。
>
> **Deliverables**:
> - 新ディレクトリ構造（Diátaxis準拠）
> - 35+の刷新・新規ドキュメントファイル
> - 統合AIエージェントガイド（AGENTS.md + CLAUDE.md統合）
> - 完全なオペコードリファレンス（CommandRegistry.csと整合）
> - 検証済みチュートリアル（すべてのコードブロックがパース可能）
>
> **Estimated Effort**: Large
> **Parallel Execution**: YES - 5 Waves
> **Critical Path**: Wave 1（基盤）→ Wave 2（Reference）→ Wave 3（Tutorials/How-to）→ Wave 4（Explanation）→ Wave 5（統合検証）

---

## Context

### Original Request
「ドキュメントを刷新して。今のドキュメントは内容が古く、分かりづらい。AIにも人間にもやさしいドキュメントにして」

### Interview Summary
**Key Discussions**:
- **対象範囲**: すべて（docs/以下全体 + README.md + AGENTS.md + CLAUDE.md）
- **問題認識**: 言語の命令や機能が古い、分かりづらい
- **ターゲット読者**: エンドユーザー（スクリプト作者・ビジュアルノベル制作者）+ AIエージェント
- **構成方針**: Diátaxis Framework（Tutorial / How-to Guide / Reference / Explanation）

**Research Findings**:
- AGENTS.mdとCLAUDE.mdは117行完全一致（メンテナンス悪夢）
- opcodes.mdは「63種類」と主張するが、実際はCommandRegistry.csに80+、OpCode.csに150+存在
- `gosub *label with 1, 1`構文はチュートリアルに存在するが、Parser.csは`with`キーワードを持たない（動かないコード）
- `bg`コマンドはチュートリアルで多用されるが、opcodesリファレンスに存在しない
- `while`/`wend`はParser.csに実装されているが、どのユーザードキュメントにも存在しない
- config.json設定は11項目存在するが、どこにも文書化されていない
- aria-compile / aria-pack はREADMEに言及があるが、詳細ドキュメントが存在しない
- core-spec.mdは「ドラフト」のまま、実装と乖離している可能性

### Metis Review
**Identified Gaps** (addressed):
- **Tutorial code correctness**: すべての` ```aria `ブロックをParser.csで検証する手順を追加
- **Opcode boundary**: 公開API（CommandRegistry.cs登録済み）のみをReferenceに文書化。内部用（ScopeEnter, Defer, Panicなど）はExplanationアーキテクチャ文書に移動
- **AGENTS.md/CLAUDE.md**: 統合して`docs/ai-agent/AGENTS.md`へ。役割分化は不要（両方ともAIエージェント向け）
- **BytecodeFormat.md**: `docs/explanation/`または`docs/reference/`に再配置
- **言語方針**: エンドユーザー向け=日本語、AIエージェント向け=英語。混在はファイルレベルで分離

---

## Work Objectives

### Core Objective
AriaEngineの全ドキュメントを、事実に基づいた正確な内容に全面書き換え、Diátaxis Framework構造で再構成し、エンドユーザーとAIエージェント双方が効率的に情報にアクセスできる状態にする。

### Concrete Deliverables
- 新しい`docs/`ディレクトリ構造（Diátaxis 4象限 + AI Agent Guide）
- 刷新済みルート`README.md`
- 統合AIエージェントガイド（`docs/ai-agent/AGENTS.md`）
- 完全なオペコードリファレンス（カテゴリー別、CommandRegistry.csと完全整合）
- 検証済みチュートリアル（すべてのコードブロックが実際にパース可能）
- `config.json`設定リファレンス
- コンパイル/パッケージングHow-to Guide
- 旧ドキュメントの削除と適切なリダイレクト

### Definition of Done
- [ ] すべての旧ドキュメントファイルが新構造に移行または削除されている
- [ ] CommandRegistry.csに登録されているすべての公開コマンドがReferenceに文書化されている
- [ ] すべてのチュートリアルコードブロックがParser.csでエラーなくパースされる
- [ ] ルートREADME.mdが新構造を正しく案内している
- [ ] AGENTS.mdとCLAUDE.mdが統合され、1つの真実源となっている
- [ ] リンク切れが存在しない

### Must Have
- Diátaxis構造（tutorials/ / how-to-guides/ / reference/ / explanation/）
- AIエージェント向け統合ガイド
- 正確なオペコードリファレンス（実装と整合）
- 動作するチュートリアルコード
- config.json設定リファレンス
- コンパイル/パッケージング手順

### Must NOT Have (Guardrails)
- ソースコードの変更（ドキュメントのみ）
- 新機能の実装（ドキュメント化されていない既存機能を実装しない）
- 動画・スクリーンショット生成
- C#ソースコードへのXMLドキュメント追加
- 既存の正確な情報の無断削除（必ず新ファイルに移行してから）
- 両言語混在ファイル（1ファイル=1言語）

---

## Verification Strategy

> **ZERO HUMAN INTERVENTION** - ALL verification is agent-executed. No exceptions.

### Test Decision
- **Infrastructure exists**: NO（ドキュメント固有のテストinfraはなし）
- **Automated tests**: NO（ドキュメント刷新なのでユニットテストなし）
- **Agent-Executed QA**: MANDATORY for all tasks

### QA Policy
Every task MUST include agent-executed QA scenarios. Evidence saved to `.sisyphus/evidence/task-{N}-{scenario-slug}.{ext}`.

- **Documentation completeness**: Read file, verify sections exist, check word count > minimum
- **Code block validity**: Extract ` ```aria ` blocks, run through Parser.cs, assert zero errors
- **Link integrity**: Grep for markdown links, verify target files exist
- **Cross-reference accuracy**: Compare command names against CommandRegistry.cs

---

## Execution Strategy

### Parallel Execution Waves

```
Wave 1 (Foundation + AI Agent Guide - Start Immediately):
├── Task 1: Create new directory structure
├── Task 2: Merge AGENTS.md + CLAUDE.md → docs/ai-agent/AGENTS.md
├── Task 3: Create docs/ai-agent/CODEBASE.md
├── Task 4: Write new docs/README.md (hub)
├── Task 5: Refresh root README.md
└── Task 6: Create reference/opcodes/index.md (template)

Wave 2 (Reference - MAX PARALLEL, depends: Wave 1):
├── Task 7:  reference/syntax.md (script language syntax)
├── Task 8:  reference/init-aria.md
├── Task 9:  reference/config.md
├── Task 10: reference/opcodes/basic.md
├── Task 11: reference/opcodes/script-control.md
├── Task 12: reference/opcodes/sprite.md
├── Task 13: reference/opcodes/animation.md
├── Task 14: reference/opcodes/button.md
├── Task 15: reference/opcodes/ui.md
├── Task 16: reference/opcodes/audio.md
├── Task 17: reference/opcodes/system.md
├── Task 18: reference/opcodes/flag.md
├── Task 19: reference/opcodes/chapter.md
├── Task 20: reference/opcodes/character.md
├── Task 21: reference/opcodes/init.md

Wave 3 (Tutorials + How-to Guides - depends: Wave 1):
├── Task 22: tutorials/getting-started.md
├── Task 23: tutorials/creating-ui.md
├── Task 24: tutorials/chapter-system.md
├── Task 25: tutorials/save-load.md
├── Task 26: how-to-guides/compile-and-package.md
├── Task 27: how-to-guides/debug-mode.md
├── Task 28: how-to-guides/custom-fonts.md
├── Task 29: how-to-guides/troubleshooting.md

Wave 4 (Explanation - depends: Wave 1):
├── Task 30: explanation/language-philosophy.md
├── Task 31: explanation/architecture-overview.md
├── Task 32: explanation/virtual-machine.md
├── Task 33: explanation/parser.md
├── Task 34: explanation/rendering.md
└── Task 35: explanation/design-decisions.md

Wave 5 (Integration + Verification - depends: ALL):
├── Task 36: Remove old docs, create redirects
├── Task 37: Link integrity check
├── Task 38: Code block parse validation
├── Task 39: Final compliance audit

Wave FINAL (After ALL tasks - 4 parallel reviews):
├── Task F1: Plan compliance audit (oracle)
├── Task F2: Documentation quality review (unspecified-high)
├── Task F3: Link & code validation (unspecified-high)
├── Task F4: Scope fidelity check (deep)
-> Present results -> Get explicit user okay

Critical Path: Task 1 → Task 4/5 → Task 7-21 → Task 22-35 → Task 36-39 → F1-F4 → user okay
Parallel Speedup: ~75% faster than sequential
Max Concurrent: 15 (Wave 2)
```

### Dependency Matrix (abbreviated)

- **1**: - - 2-6, 7-35
- **2**: 1 - -
- **4**: 1 - 36
- **5**: 1 - 36
- **7-21**: 1, 6 - 36
- **22-35**: 1 - 36
- **36**: ALL - 37-39
- **37**: 36 - 39
- **38**: 36 - 39
- **39**: 36-38 - F1-F4

---

## TODOs

- [x] 1. **Create new Diátaxis directory structure**

  **What to do**:
  - Create `docs/tutorials/` directory
  - Create `docs/how-to-guides/` directory
  - Create `docs/reference/opcodes/` directory (nested)
  - Create `docs/explanation/` directory
  - Create `docs/ai-agent/` directory
  - Create `.sisyphus/evidence/` directory if not exists
  - Leave old directories untouched for now (will be removed in Task 36)

  **Must NOT do**:
  - Delete any existing files yet
  - Create any content files (just directories)

  **Recommended Agent Profile**:
  - **Category**: `quick`
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 1 (with Tasks 2-6)
  - **Blocks**: Tasks 2-35
  - **Blocked By**: None

  **References**:
  - Current `docs/` structure (from audit) - Understand existing layout to avoid conflicts

  **Acceptance Criteria**:
  - [ ] `docs/tutorials/` exists
  - [ ] `docs/how-to-guides/` exists
  - [ ] `docs/reference/opcodes/` exists
  - [ ] `docs/explanation/` exists
  - [ ] `docs/ai-agent/` exists

  **QA Scenarios**:
  ```
  Scenario: Verify directory structure created
    Tool: Bash
    Steps:
      1. `dir docs\tutorials /b` → returns empty (no error)
      2. `dir docs\how-to-guides /b` → returns empty
      3. `dir docs\reference\opcodes /b` → returns empty
      4. `dir docs\explanation /b` → returns empty
      5. `dir docs\ai-agent /b` → returns empty
    Expected Result: All 5 directories exist, no errors
    Evidence: .sisyphus/evidence/task-1-dir-check.txt
  ```

  **Commit**: YES (Wave 1)
  - Message: `docs: create Diátaxis directory structure`

- [x] 2. **Merge AGENTS.md + CLAUDE.md → docs/ai-agent/AGENTS.md**

  **What to do**:
  - Read both `AGENTS.md` and `CLAUDE.md` (currently 100% identical)
  - Create `docs/ai-agent/AGENTS.md` as the unified AI agent guide
  - Content must include:
    - Build and run instructions
    - Architecture overview (all core components)
    - Script language syntax summary
    - Engine initialization sequence
    - Main loop explanation
    - Directory structure
    - Debug mode features
  - Improve upon the originals: add cross-references to new docs, clarify ambiguous points, fix the "~80 opcodes" claim to accurate count from CommandRegistry.cs
  - Write in **English only**
  - Keep it concise but complete (AI agents need factual density, not narrative)

  **Must NOT do**:
  - Keep both old files (will delete in Task 36)
  - Add narrative/fluff content
  - Write in Japanese

  **Recommended Agent Profile**:
  - **Category**: `quick`
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 1 (with Tasks 1, 3-6)
  - **Blocks**: Task 36 (deletion of old files)
  - **Blocked By**: Task 1 (directory creation)

  **References**:
  - `AGENTS.md` (current) - Base content to improve upon
  - `CLAUDE.md` (current) - Identical to AGENTS.md, verify before discarding
  - `src/AriaEngine/Core/CommandRegistry.cs` - Accurate opcode count and names
  - `src/AriaEngine/Core/OpCode.cs` - Opcode enum definitions
  - `src/AriaEngine/Program.cs` - Main loop details
  - `docs/architecture/overview.md` (current) - Additional architecture context

  **Acceptance Criteria**:
  - [ ] File created at `docs/ai-agent/AGENTS.md`
  - [ ] Written entirely in English
  - [ ] Contains all sections from original AGENTS.md, improved
  - [ ] Opcode count matches CommandRegistry.cs (not "63" or "~80")
  - [ ] Cross-references new documentation structure
  - [ ] Word count > 2000 characters

  **QA Scenarios**:
  ```
  Scenario: Verify unified AI agent guide exists and is correct
    Tool: Bash
    Steps:
      1. `test -f docs/ai-agent/AGENTS.md` → PASS
      2. `grep -c "Build and Run" docs/ai-agent/AGENTS.md` → >= 1
      3. `grep -c "Architecture" docs/ai-agent/AGENTS.md` → >= 1
      4. `grep -c "63" docs/ai-agent/AGENTS.md` → 0 (must not contain false count)
    Expected Result: File exists, has required sections, no false opcode count
    Evidence: .sisyphus/evidence/task-2-ai-guide-check.txt
  ```

  **Commit**: YES (Wave 1)
  - Message: `docs: unify AI agent guide from AGENTS.md and CLAUDE.md`

- [x] 3. **Create docs/ai-agent/CODEBASE.md**

  **What to do**:
  - Create a deep-dive codebase guide for AI agents
  - Content: detailed file-by-file breakdown, class responsibilities, data flow diagrams (text-based), key abstractions, extension points
  - Focus on C# source code structure in `src/AriaEngine/`
  - Include: how to add a new opcode, how to add a new sprite property, how rendering pipeline works
  - Write in **English only**
  - Target length: substantial (>3000 words)

  **Must NOT do**:
  - Duplicate AGENTS.md content (this is deep-dive, AGENTS.md is overview)
  - Write tutorial-style content
  - Write in Japanese

  **Recommended Agent Profile**:
  - **Category**: `unspecified-high`
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 1 (with Tasks 1-2, 4-6)
  - **Blocks**: None
  - **Blocked By**: Task 1 (directory creation)

  **References**:
  - `src/AriaEngine/Core/VirtualMachine.cs` - VM execution flow
  - `src/AriaEngine/Core/Parser.cs` - Parsing logic
  - `src/AriaEngine/Core/GameState.cs` - State management
  - `src/AriaEngine/Core/Sprite.cs` - Sprite system
  - `src/AriaEngine/Rendering/SpriteRenderer.cs` - Rendering pipeline
  - `src/AriaEngine/Core/OpCode.cs` - Opcode definitions
  - `docs/architecture/vm.md` (current) - Existing VM documentation
  - `docs/architecture/parser.md` (current) - Existing parser documentation
  - `docs/architecture/rendering.md` (current) - Existing rendering documentation

  **Acceptance Criteria**:
  - [ ] File created at `docs/ai-agent/CODEBASE.md`
  - [ ] Written entirely in English
  - [ ] Contains file-by-file breakdown
  - [ ] Contains extension guide (how to add opcode, sprite property)
  - [ ] Word count > 3000 words

  **QA Scenarios**:
  ```
  Scenario: Verify codebase deep-dive exists
    Tool: Bash
    Steps:
      1. `test -f docs/ai-agent/CODEBASE.md` → PASS
      2. `wc -w docs/ai-agent/CODEBASE.md` → >= 3000
      3. `grep -c "VirtualMachine" docs/ai-agent/CODEBASE.md` → >= 1
      4. `grep -c "Parser" docs/ai-agent/CODEBASE.md` → >= 1
      5. `grep -c "how to add" docs/ai-agent/CODEBASE.md` → >= 2
    Expected Result: File exists, substantial content, covers key components
    Evidence: .sisyphus/evidence/task-3-codebase-check.txt
  ```

  **Commit**: YES (Wave 1)
  - Message: `docs: add AI agent codebase deep-dive`

- [x] 4. **Write new docs/README.md (documentation hub)**

  **What to do**:
  - Create the main documentation hub that guides users to the right document
  - Must be **bilingual** (Japanese primary, English secondary) since it's the entry point for all users
  - Structure:
    - Welcome message
    - "What are you looking for?" section with 4 Diátaxis quadrants + AI Agent Guide
    - Quick decision tree ("初めてですか？→ Tutorials" / "特定の機能を知りたい？→ Reference" etc.)
    - Link to each top-level section
    - Quick reference table (5 most common commands)
  - Must replace the current `docs/README.md`
  - Keep it concise but informative

  **Must NOT do**:
  - Duplicate content from other docs (just link)
  - Write everything in one language only
  - Include detailed technical content (that's for other files)

  **Recommended Agent Profile**:
  - **Category**: `quick`
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 1 (with Tasks 1-3, 5-6)
  - **Blocks**: Task 36 (old file deletion)
  - **Blocked By**: Task 1 (directory creation)

  **References**:
  - Current `docs/README.md` - Existing hub structure to improve upon
  - Diátaxis framework documentation - Understand quadrant purposes

  **Acceptance Criteria**:
  - [ ] File created at `docs/README.md` (overwrites old one)
  - [ ] Contains links to all 5 sections (tutorials, how-to-guides, reference, explanation, ai-agent)
  - [ ] Contains decision tree/guidance
  - [ ] Contains quick reference table
  - [ ] Bilingual content (Japanese + English)

  **QA Scenarios**:
  ```
  Scenario: Verify documentation hub is complete
    Tool: Bash
    Steps:
      1. `test -f docs/README.md` → PASS
      2. `grep -c "tutorials" docs/README.md` → >= 1
      3. `grep -c "how-to-guides" docs/README.md` → >= 1
      4. `grep -c "reference" docs/README.md` → >= 1
      5. `grep -c "ai-agent" docs/README.md` → >= 1
      6. `grep -c "Explanation\|説明" docs/README.md` → >= 1
    Expected Result: Hub exists, links to all sections
    Evidence: .sisyphus/evidence/task-4-hub-check.txt
  ```

  **Commit**: YES (Wave 1)
  - Message: `docs: create bilingual documentation hub`

- [x] 5. **Refresh root README.md**

  **What to do**:
  - Rewrite root `README.md` to be a project landing page, not a documentation monolith
  - Current README.md is 313 lines and duplicates much of the docs
  - New structure:
    - Project name + one-line description
    - Key features (bullet list, concise)
    - Quick start (3 commands)
    - Screenshot placeholder (text description)
    - "Documentation" section with link to `docs/README.md`
    - "For AI Agents" section with link to `docs/ai-agent/AGENTS.md`
    - License, Contributing (brief)
  - Target: <100 lines, focused on "what is this" and "where to go next"
  - Write primarily in Japanese (project's primary language)

  **Must NOT do**:
  - Duplicate detailed documentation (link instead)
  - Include full opcode lists
  - Include full tutorials
  - Keep the 313-line monolith structure

  **Recommended Agent Profile**:
  - **Category**: `quick`
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 1 (with Tasks 1-4, 6)
  - **Blocks**: Task 36 (old content removal)
  - **Blocked By**: None

  **References**:
  - Current `README.md` - Extract key info, discard duplication
  - `docs/README.md` (new) - Must reference it
  - Good open-source README examples (e.g., Raylib, SDL)

  **Acceptance Criteria**:
  - [ ] Root `README.md` rewritten
  - [ ] Line count < 100
  - [ ] Contains link to `docs/README.md`
  - [ ] Contains link to `docs/ai-agent/AGENTS.md`
  - [ ] Contains quick start section (< 10 lines)
  - [ ] No duplication of detailed docs content

  **QA Scenarios**:
  ```
  Scenario: Verify root README is concise and correct
    Tool: Bash
    Steps:
      1. `wc -l README.md` → <= 100
      2. `grep -c "docs/README.md" README.md` → >= 1
      3. `grep -c "docs/ai-agent/AGENTS.md" README.md` → >= 1
      4. `grep -c "dotnet build" README.md` → >= 1
      5. `grep -c "lsp_rect\|opcodes\|chapter" README.md` → 0 (no detailed content)
    Expected Result: Concise landing page, links to docs
    Evidence: .sisyphus/evidence/task-5-root-readme-check.txt
  ```

  **Commit**: YES (Wave 1)
  - Message: `docs: refresh root README as landing page`

- [x] 6. **Create reference/opcodes/index.md (template & category overview)**

  **What to do**:
  - Create the index/landing page for opcode reference
  - List all opcode categories with brief descriptions
  - Provide navigation to each category file
  - Include a quick-search table: command name → category file
  - Document the categorization scheme (why these categories?)
  - Write in **Japanese**
  - This is the template that Tasks 10-21 will follow

  **Must NOT do**:
  - Document individual opcodes here (that's for category files)
  - Include incorrect opcode counts

  **Recommended Agent Profile**:
  - **Category**: `quick`
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 1 (with Tasks 1-5)
  - **Blocks**: Tasks 10-21 (category files)
  - **Blocked By**: Task 1 (directory creation)

  **References**:
  - `src/AriaEngine/Core/CommandRegistry.cs` - Actual categories and commands
  - Current `docs/api/opcodes.md` - Existing structure to learn from (but fix counts)

  **Acceptance Criteria**:
  - [ ] File created at `docs/reference/opcodes/index.md`
  - [ ] Lists all categories (Basic, Script Control, Sprite, Animation, Button, UI, Audio, System, Flag, Chapter, Character, Init)
  - [ ] Contains navigation links to each category file
  - [ ] No false opcode counts

  **QA Scenarios**:
  ```
  Scenario: Verify opcode index exists and is correct
    Tool: Bash
    Steps:
      1. `test -f docs/reference/opcodes/index.md` → PASS
      2. `grep -c "Basic\|Script Control\|Sprite\|Animation\|Button\|UI\|Audio\|System\|Flag\|Chapter\|Character\|Init" docs/reference/opcodes/index.md` → >= 12
      3. `grep -c "63" docs/reference/opcodes/index.md` → 0 (no false count)
    Expected Result: Index exists, covers all categories
    Evidence: .sisyphus/evidence/task-6-opcode-index-check.txt
  ```

  **Commit**: YES (Wave 1)
  - Message: `docs: create opcode reference index`

- [x] 7. **Write reference/syntax.md (script language syntax reference)**

  **What to do**:
  - Comprehensive syntax reference for the `.aria` scripting language
  - Must document ALL syntax constructs:
    - Labels (`*label_name`)
    - Subroutines (`defsub`, `gosub`, `return`) - include CORRECT parameterized syntax (`gosub *label, arg1, arg2`) NOT the broken `with` syntax
    - Control flow (`if` one-line and block, `else`, `endif`, `goto`, `beq`)
    - Loops (`for`/`next`, `while`/`wend`)
    - Variables (`%0`-`%n`, `$name`)
    - String interpolation (`${$var}`, `${%var}`)
    - Inline text (`Character「message」`)
    - Text control (`\` for page clear, `@` for wait)
    - Comments (`; comment`)
    - Commands (general syntax pattern)
  - For each construct: syntax pattern, example, notes
  - Write in **Japanese**
  - This is THE authoritative syntax reference

  **Must NOT do**:
  - Document individual opcodes (that's for opcodes/)
  - Include the broken `gosub with` syntax
  - Leave `while`/`wend` undocumented

  **Recommended Agent Profile**:
  - **Category**: `unspecified-high`
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 2 (with Tasks 8-21)
  - **Blocks**: None
  - **Blocked By**: Task 1 (directory creation)

  **References**:
  - `src/AriaEngine/Core/Parser.cs` - Actual syntax implementation
  - Current `docs/scripting/basics.md` - Existing basics documentation
  - Current `docs/scripting/control-flow.md` - Existing control flow documentation
  - `docs/scripting/core-spec.md` - Register range definitions (verify against Parser.cs)

  **Acceptance Criteria**:
  - [ ] File created at `docs/reference/syntax.md`
  - [ ] Documents all syntax constructs listed above
  - [ ] Parameterized gosub shows CORRECT syntax (`gosub *label, arg1, arg2`)
  - [ ] Documents `while`/`wend`
  - [ ] Documents string interpolation
  - [ ] Written in Japanese

  **QA Scenarios**:
  ```
  Scenario: Verify syntax reference is complete
    Tool: Bash
    Steps:
      1. `test -f docs/reference/syntax.md` → PASS
      2. `grep -c "while" docs/reference/syntax.md` → >= 1
      3. `grep -c "wend" docs/reference/syntax.md` → >= 1
      4. `grep -c "with" docs/reference/syntax.md` → 0 (must NOT contain broken syntax)
      5. `grep -c "\\${" docs/reference/syntax.md` → >= 1 (string interpolation)
    Expected Result: Complete syntax reference, no broken syntax
    Evidence: .sisyphus/evidence/task-7-syntax-check.txt
  ```

  **Commit**: YES (Wave 2)
  - Message: `docs: add script language syntax reference`

- [x] 8. **Write reference/init-aria.md**

  **What to do**:
  - Reference for `init.aria` configuration file
  - Document ALL init-only commands:
    - `window` (width, height, title)
    - `font` (path)
    - `font_atlas_size` (size)
    - `font_filter` (filter mode)
    - `script` (main script path)
    - `debug` (on/off)
    - `compat_mode` (on/off) - explain what it does
    - `textbox` configuration commands
    - Any other init-only settings
  - For each: syntax, default value, description, example
  - Explain difference between init-only and runtime commands
  - Write in **Japanese**

  **Must NOT do**:
  - Document runtime commands here
  - Leave `compat_mode` unexplained

  **Recommended Agent Profile**:
  - **Category**: `unspecified-high`
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 2 (with Tasks 7, 9-21)
  - **Blocks**: None
  - **Blocked By**: Task 1 (directory creation)

  **References**:
  - `src/AriaEngine/Core/Parser.cs` - Init-only command detection
  - `src/AriaEngine/Program.cs` - How init.aria is processed
  - Current `docs/tutorials/getting-started.md` - Init.aria examples
  - `docs/scripting/core-spec.md` - compat_mode mention

  **Acceptance Criteria**:
  - [ ] File created at `docs/reference/init-aria.md`
  - [ ] Documents all init-only commands
  - [ ] Explains `compat_mode`
  - [ ] Explains init vs runtime distinction
  - [ ] Written in Japanese

  **QA Scenarios**:
  ```
  Scenario: Verify init-aria reference is complete
    Tool: Bash
    Steps:
      1. `test -f docs/reference/init-aria.md` → PASS
      2. `grep -c "window" docs/reference/init-aria.md` → >= 1
      3. `grep -c "font" docs/reference/init-aria.md` → >= 1
      4. `grep -c "compat_mode" docs/reference/init-aria.md` → >= 1
      5. `grep -c "init.*runtime\|runtime.*init" docs/reference/init-aria.md` → >= 1
    Expected Result: Complete init reference
    Evidence: .sisyphus/evidence/task-8-init-check.txt
  ```

  **Commit**: YES (Wave 2)
  - Message: `docs: add init.aria configuration reference`

- [x] 9. **Write reference/config.md**

  **What to do**:
  - Reference for `config.json` user settings
  - Read `ConfigManager.cs` to find all settings
  - Document each setting:
    - Name
    - Type (int, bool, string, float)
    - Default value
    - Description
    - Valid values/range
  - Known settings from audit: text speed, volumes, fullscreen, text mode, etc. (11 total)
  - Include example `config.json`
  - Write in **Japanese**

  **Must NOT do**:
  - Guess settings (must read ConfigManager.cs)
  - Leave settings undocumented

  **Recommended Agent Profile**:
  - **Category**: `unspecified-high`
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 2 (with Tasks 7-8, 10-21)
  - **Blocks**: None
  - **Blocked By**: Task 1 (directory creation)

  **References**:
  - `src/AriaEngine/Core/ConfigManager.cs` - Source of truth for all settings
  - Current `AGENTS.md` - Mentions config.json

  **Acceptance Criteria**:
  - [ ] File created at `docs/reference/config.md`
  - [ ] Documents all settings from ConfigManager.cs
  - [ ] Includes example config.json
  - [ ] Written in Japanese

  **QA Scenarios**:
  ```
  Scenario: Verify config reference matches source
    Tool: Bash
    Steps:
      1. `test -f docs/reference/config.md` → PASS
      2. Count settings in ConfigManager.cs: `grep -c "public .* { get; set; }" src/AriaEngine/Core/ConfigManager.cs`
      3. Count settings documented: `grep -c "^### \|^#### " docs/reference/config.md`
      4. Verify counts match (or documented count >= source count)
    Expected Result: All settings documented
    Evidence: .sisyphus/evidence/task-9-config-check.txt
  ```

  **Commit**: YES (Wave 2)
  - Message: `docs: add config.json settings reference`

- [x] 10. **Write reference/opcodes/basic.md**

  **What to do**:
  - Document fundamental opcodes (primarily from CommandRegistry.cs "Core" category + basic Text commands)
  - Typical commands: `wait`, `delay` (alias of wait), `end`, `textclear`, etc.
  - Note: `text` command is handled as inline text by parser, not registered in CommandRegistry.cs - document it here as a special case
  - For each opcode: syntax, parameters, description, example, notes
  - Mark aliases clearly (`delay` = `wait` alias)
  - Write in **Japanese**

  **Must NOT do**:
  - Include opcodes from other categories
  - Leave aliases undocumented

  **Recommended Agent Profile**:
  - **Category**: `unspecified-high`
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 2 (with Tasks 7-9, 11-21)
  - **Blocks**: None
  - **Blocked By**: Task 6 (opcode index template)

  **References**:
  - `src/AriaEngine/Core/CommandRegistry.cs` - Look for "Core" category + any basic commands
  - `src/AriaEngine/Core/OpCode.cs` - Opcode definitions
  - Current `docs/api/opcodes.md` - Existing basic opcodes section
  - `src/AriaEngine/Core/VirtualMachine.cs` - Implementation details

  **Acceptance Criteria**:
  - [ ] File created at `docs/reference/opcodes/basic.md`
  - [ ] Documents Core category opcodes from CommandRegistry.cs + basic text commands
  - [ ] Each opcode has syntax, description, example
  - [ ] Aliases marked clearly
  - [ ] Written in Japanese

  **QA Scenarios**:
  ```
  Scenario: Verify basic opcodes match source
    Tool: Grep + Read
    Steps:
      1. Read CommandRegistry.cs to list Core category commands
      2. Read docs/reference/opcodes/basic.md to verify each Core command is documented
      3. Verify `text` command is documented (special case)
    Expected Result: All Core opcodes + text command documented
    Evidence: .sisyphus/evidence/task-10-basic-opcodes-check.txt
  ```
  Scenario: Verify basic opcodes match source
    Tool: Bash
    Steps:
      1. `test -f docs/reference/opcodes/basic.md` → PASS
      2. List Basic opcodes from CommandRegistry.cs
      3. Verify each appears in the markdown file
    Expected Result: All Basic opcodes documented
    Evidence: .sisyphus/evidence/task-10-basic-opcodes-check.txt
  ```

  **Commit**: YES (Wave 2)
  - Message: `docs: add basic opcode reference`

- [x] 11. **Write reference/opcodes/script-control.md**

  **What to do**:
  - Document control flow opcodes (primarily from CommandRegistry.cs "Script" category + Core category control flow commands)
  - Commands: `goto`, `gosub`, `return`, `defsub`, `if`, `else`, `endif`, `for`, `next`, `while`, `wend`, `end`, `beq`, etc.
  - Include the CORRECT parameterized gosub syntax
  - Document both one-line `if` and block `if`/`else`/`endif`
  - Write in **Japanese**

  **Must NOT do**:
  - Use broken `gosub with` syntax

  **Recommended Agent Profile**:
  - **Category**: `unspecified-high`
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 2 (with Tasks 7-10, 12-21)
  - **Blocks**: None
  - **Blocked By**: Task 6 (opcode index template)

  **References**:
  - `src/AriaEngine/Core/CommandRegistry.cs` - Look for "Script" and "Core" categories
  - `src/AriaEngine/Core/Parser.cs` - Control flow parsing
  - `src/AriaEngine/Core/VirtualMachine.cs` - Control flow execution
  - Current `docs/api/opcodes.md` - Script control section
  - `docs/scripting/control-flow.md` - Existing control flow docs

  **Acceptance Criteria**:
  - [ ] File created at `docs/reference/opcodes/script-control.md`
  - [ ] Documents Script category + relevant Core category opcodes
  - [ ] Correct parameterized gosub syntax
  - [ ] Documents `while`/`wend`
  - [ ] Written in Japanese

  **QA Scenarios**:
  ```
  Scenario: Verify script control opcodes
    Tool: Grep + Read
    Steps:
      1. Read CommandRegistry.cs to list Script + relevant Core category commands
      2. Read docs/reference/opcodes/script-control.md to verify coverage
      3. Verify `while` and `wend` are documented
      4. Verify `with` does NOT appear (no broken syntax)
    Expected Result: Complete script control reference
    Evidence: .sisyphus/evidence/task-11-script-control-check.txt
  ```

  **Commit**: YES (Wave 2)
  - Message: `docs: add script control opcode reference`

- [x] 12. **Write reference/opcodes/sprite.md**

  **What to do**:
  - Document sprite and visual opcodes (from CommandRegistry.cs "Render" category)
  - Commands: `lsp`, `lsp_text`, `lsp_rect`, `vsp`, `msp`, `csp`, `sp_alpha`, `sp_scale`, `sp_rotate`, `sp_pos`, `sp_z`, `sp_fill`, `sp_border`, `sp_round`, `sp_shadow`, `sp_hover_color`, `sp_isbutton`, etc.
  - Include `bg` command (currently undocumented but widely used; found in Compatibility category)
  - For each: syntax, parameters, description, example
  - Write in **Japanese**

  **Must NOT do**:
  - Leave `bg` undocumented

  **Recommended Agent Profile**:
  - **Category**: `unspecified-high`
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 2 (with Tasks 7-11, 13-21)
  - **Blocks**: None
  - **Blocked By**: Task 6 (opcode index template)

  **References**:
  - `src/AriaEngine/Core/CommandRegistry.cs` - Look for "Render" category
  - `src/AriaEngine/Core/Sprite.cs` - Sprite properties
  - Current `docs/api/opcodes.md` - Sprite section
  - `docs/scripting/sprites.md` - Existing sprite docs

  **Acceptance Criteria**:
  - [ ] File created at `docs/reference/opcodes/sprite.md`
  - [ ] Documents Render category opcodes + `bg` command
  - [ ] Includes `bg` command documentation
  - [ ] Written in Japanese

  **QA Scenarios**:
  ```
  Scenario: Verify sprite opcodes
    Tool: Grep + Read
    Steps:
      1. Read CommandRegistry.cs to list Render category commands
      2. Read docs/reference/opcodes/sprite.md to verify coverage
      3. Verify `bg` command is documented
    Expected Result: Complete sprite reference including bg
    Evidence: .sisyphus/evidence/task-12-sprite-check.txt
  ```

  **Commit**: YES (Wave 2)
  - Message: `docs: add sprite opcode reference`

- [x] 13. **Write reference/opcodes/animation.md**

  **What to do**:
  - Document animation and effect opcodes (from CommandRegistry.cs "Render" category - animation subset)
  - Commands: tween-related (`msp` with duration?), effect commands, transition commands
  - Read CommandRegistry.cs to find animation-related opcodes in Render category
  - Document easing functions if they exist as commands
  - Write in **Japanese**

  **Recommended Agent Profile**:
  - **Category**: `unspecified-high`
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 2 (with Tasks 7-12, 14-21)
  - **Blocks**: None
  - **Blocked By**: Task 6 (opcode index template)

  **References**:
  - `src/AriaEngine/Core/CommandRegistry.cs` - Look for Render category animation commands
  - `src/AriaEngine/Rendering/TweenManager.cs` - Tween implementation
  - Current `docs/api/opcodes.md` - Animation section
  - `docs/scripting/animations.md` - Existing animation docs

  **Acceptance Criteria**:
  - [ ] File created at `docs/reference/opcodes/animation.md`
  - [ ] Documents animation-related opcodes from Render category
  - [ ] Written in Japanese

  **QA Scenarios**:
  ```
  Scenario: Verify animation opcodes
    Tool: Grep + Read
    Steps:
      1. Read CommandRegistry.cs to identify animation-related commands in Render category
      2. Read docs/reference/opcodes/animation.md to verify coverage
    Expected Result: Complete animation reference
    Evidence: .sisyphus/evidence/task-13-animation-check.txt
  ```

  **Commit**: YES (Wave 2)
  - Message: `docs: add animation opcode reference`

- [x] 14. **Write reference/opcodes/button.md**

  **What to do**:
  - Document button and input opcodes (from CommandRegistry.cs "Input" category)
  - Commands: `spbtn`, `btnwait`, `btn_area` (if exists), etc.
  - Document button ID system
  - Write in **Japanese**

  **Recommended Agent Profile**:
  - **Category**: `unspecified-high`
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 2 (with Tasks 7-13, 15-21)
  - **Blocks**: None
  - **Blocked By**: Task 6 (opcode index template)

  **References**:
  - `src/AriaEngine/Core/CommandRegistry.cs` - Look for "Input" category
  - `src/AriaEngine/Input/InputHandler.cs` - Button handling
  - Current `docs/api/opcodes.md` - Button section

  **Acceptance Criteria**:
  - [ ] File created at `docs/reference/opcodes/button.md`
  - [ ] Documents Input category opcodes
  - [ ] Written in Japanese

  **QA Scenarios**:
  ```
  Scenario: Verify button opcodes
    Tool: Grep + Read
    Steps:
      1. Read CommandRegistry.cs to list Input category commands
      2. Read docs/reference/opcodes/button.md to verify coverage
    Expected Result: Complete button reference
    Evidence: .sisyphus/evidence/task-14-button-check.txt
  ```

  **Commit**: YES (Wave 2)
  - Message: `docs: add button opcode reference`

- [x] 15. **Write reference/opcodes/ui.md**

  **What to do**:
  - Document UI and text display opcodes (from CommandRegistry.cs "Ui" and "Text" categories)
  - Commands: `textbox`, `textclear`, `backlog`, `ui_theme`, `ui_quality`, etc.
  - Read CommandRegistry.cs to find all Ui and Text category opcodes
  - Write in **Japanese**

  **Recommended Agent Profile**:
  - **Category**: `unspecified-high`
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 2 (with Tasks 7-14, 16-21)
  - **Blocks**: None
  - **Blocked By**: Task 6 (opcode index template)

  **References**:
  - `src/AriaEngine/Core/CommandRegistry.cs` - Look for "Ui" and "Text" categories
  - `src/AriaEngine/Core/VirtualMachine.cs` - UI command execution
  - Current `docs/api/opcodes.md` - UI section
  - `docs/scripting/ui-elements.md` - Existing UI docs

  **Acceptance Criteria**:
  - [ ] File created at `docs/reference/opcodes/ui.md`
  - [ ] Documents Ui + Text category opcodes
  - [ ] Written in Japanese

  **QA Scenarios**:
  ```
  Scenario: Verify UI opcodes
    Tool: Grep + Read
    Steps:
      1. Read CommandRegistry.cs to list Ui and Text category commands
      2. Read docs/reference/opcodes/ui.md to verify coverage
    Expected Result: Complete UI reference
    Evidence: .sisyphus/evidence/task-15-ui-check.txt
  ```

  **Commit**: YES (Wave 2)
  - Message: `docs: add UI opcode reference`

- [x] 16. **Write reference/opcodes/audio.md**

  **What to do**:
  - Document audio opcodes (from CommandRegistry.cs "Audio" category)
  - Commands: `bgm`, `bgmstop`, `se`, `sestop`, `dwave`, `mp3*`, `fadein`, `fadeout`, `vol_bgm`, `vol_se`, etc.
  - Write in **Japanese**

  **Recommended Agent Profile**:
  - **Category**: `unspecified-high`
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 2 (with Tasks 7-15, 17-21)
  - **Blocks**: None
  - **Blocked By**: Task 6 (opcode index template)

  **References**:
  - `src/AriaEngine/Core/CommandRegistry.cs` - Look for "Audio" category
  - `src/AriaEngine/Audio/AudioManager.cs` - Audio implementation
  - Current `docs/api/opcodes.md` - Audio section

  **Acceptance Criteria**:
  - [ ] File created at `docs/reference/opcodes/audio.md`
  - [ ] Documents Audio category opcodes
  - [ ] Written in Japanese

  **QA Scenarios**:
  ```
  Scenario: Verify audio opcodes
    Tool: Grep + Read
    Steps:
      1. Read CommandRegistry.cs to list Audio category commands
      2. Read docs/reference/opcodes/audio.md to verify coverage
    Expected Result: Complete audio reference
    Evidence: .sisyphus/evidence/task-16-audio-check.txt
  ```

  **Commit**: YES (Wave 2)
  - Message: `docs: add audio opcode reference`

- [x] 17. **Write reference/opcodes/system.md**

  **What to do**:
  - Document system and save/load opcodes (from CommandRegistry.cs "System" and "Save" categories)
  - Commands: `save`, `load`, `reset`, `end`, `assert`, `panic`, `throw`, etc.
  - Write in **Japanese**

  **Recommended Agent Profile**:
  - **Category**: `unspecified-high`
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 2 (with Tasks 7-16, 18-21)
  - **Blocks**: None
  - **Blocked By**: Task 6 (opcode index template)

  **References**:
  - `src/AriaEngine/Core/CommandRegistry.cs` - Look for "System" and "Save" categories
  - `src/AriaEngine/Core/VirtualMachine.cs`
  - Current `docs/api/opcodes.md` - System section

  **Acceptance Criteria**:
  - [ ] File created at `docs/reference/opcodes/system.md`
  - [ ] Documents System + Save category opcodes
  - [ ] Written in Japanese

  **QA Scenarios**:
  ```
  Scenario: Verify system opcodes
    Tool: Grep + Read
    Steps:
      1. Read CommandRegistry.cs to list System and Save category commands
      2. Read docs/reference/opcodes/system.md to verify coverage
    Expected Result: Complete system reference
    Evidence: .sisyphus/evidence/task-17-system-check.txt
  ```

  **Commit**: YES (Wave 2)
  - Message: `docs: add system opcode reference`

- [x] 18. **Write reference/opcodes/flag.md**

  **What to do**:
  - Document flag and counter opcodes (from CommandRegistry.cs "Flags" category)
  - Commands: `set_flag`, `get_flag`, `set_pflag`, `get_pflag`, `inc`, `dec`, etc.
  - Document flag persistence semantics
  - Write in **Japanese**

  **Recommended Agent Profile**:
  - **Category**: `unspecified-high`
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 2 (with Tasks 7-17, 19-21)
  - **Blocks**: None
  - **Blocked By**: Task 6 (opcode index template)

  **References**:
  - `src/AriaEngine/Core/CommandRegistry.cs` - Look for "Flags" category
  - `src/AriaEngine/Core/GameState.cs` - Flag implementation
  - Current `docs/api/opcodes.md` - Flag section
  - `docs/scripting/advanced.md` - Existing flag documentation

  **Acceptance Criteria**:
  - [ ] File created at `docs/reference/opcodes/flag.md`
  - [ ] Documents Flags category opcodes
  - [ ] Written in Japanese

  **QA Scenarios**:
  ```
  Scenario: Verify flag opcodes
    Tool: Grep + Read
    Steps:
      1. Read CommandRegistry.cs to list Flags category commands
      2. Read docs/reference/opcodes/flag.md to verify coverage
    Expected Result: Complete flag reference
    Evidence: .sisyphus/evidence/task-18-flag-check.txt
  ```

  **Commit**: YES (Wave 2)
  - Message: `docs: add flag opcode reference`

- [x] 19. **Write reference/opcodes/chapter.md**

  **What to do**:
  - Document chapter system opcodes (from CommandRegistry.cs "Compatibility" category - chapter subset)
  - Commands: `chapter`, `chapter_select`, `unlock_chapter`, etc.
  - Write in **Japanese**

  **Recommended Agent Profile**:
  - **Category**: `unspecified-high`
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 2 (with Tasks 7-18, 20-21)
  - **Blocks**: None
  - **Blocked By**: Task 6 (opcode index template)

  **References**:
  - `src/AriaEngine/Core/CommandRegistry.cs` - Look for chapter-related commands
  - `src/AriaEngine/Core/VirtualMachine.cs`
  - Current `docs/api/opcodes.md` - Chapter section
  - `docs/tutorials/chapter-system.md` - Existing chapter docs

  **Acceptance Criteria**:
  - [ ] File created at `docs/reference/opcodes/chapter.md`
  - [ ] Documents chapter-related opcodes from Compatibility category
  - [ ] Written in Japanese

  **QA Scenarios**:
  ```
  Scenario: Verify chapter opcodes
    Tool: Grep + Read
    Steps:
      1. Read CommandRegistry.cs to identify chapter-related commands
      2. Read docs/reference/opcodes/chapter.md to verify coverage
    Expected Result: Complete chapter reference
    Evidence: .sisyphus/evidence/task-19-chapter-check.txt
  ```

  **Commit**: YES (Wave 2)
  - Message: `docs: add chapter opcode reference`

- [x] 20. **Write reference/opcodes/character.md**

  **What to do**:
  - Document character system opcodes (from CommandRegistry.cs "Compatibility" category - character subset)
  - Commands: `char`, `char_name`, `char_face`, etc.
  - Write in **Japanese**

  **Recommended Agent Profile**:
  - **Category**: `unspecified-high`
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 2 (with Tasks 7-19, 21)
  - **Blocks**: None
  - **Blocked By**: Task 6 (opcode index template)

  **References**:
  - `src/AriaEngine/Core/CommandRegistry.cs` - Look for character-related commands
  - `src/AriaEngine/Core/VirtualMachine.cs`
  - Current `docs/api/opcodes.md` - Character section

  **Acceptance Criteria**:
  - [ ] File created at `docs/reference/opcodes/character.md`
  - [ ] Documents character-related opcodes from Compatibility category
  - [ ] Written in Japanese

  **QA Scenarios**:
  ```
  Scenario: Verify character opcodes
    Tool: Grep + Read
    Steps:
      1. Read CommandRegistry.cs to identify character-related commands
      2. Read docs/reference/opcodes/character.md to verify coverage
    Expected Result: Complete character reference
    Evidence: .sisyphus/evidence/task-20-character-check.txt
  ```

  **Commit**: YES (Wave 2)
  - Message: `docs: add character opcode reference`

- [x] 21. **Write reference/opcodes/init.md**

  **What to do**:
  - Document initialization opcodes (from CommandRegistry.cs "System" and "Text" categories - init-only subset)
  - Commands: `window`, `font`, `font_atlas_size`, `font_filter`, `script`, `debug`, `compat_mode`, `textbox`, etc.
  - These are init-only commands (also documented in reference/init-aria.md, but this is the opcode-level reference)
  - Cross-reference `reference/init-aria.md`
  - Write in **Japanese**

  **Recommended Agent Profile**:
  - **Category**: `unspecified-high`
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 2 (with Tasks 7-20)
  - **Blocks**: None
  - **Blocked By**: Task 6 (opcode index template)

  **References**:
  - `src/AriaEngine/Core/CommandRegistry.cs` - Look for System/Text category init-only commands
  - `src/AriaEngine/Core/Parser.cs` - Init-only command handling
  - `docs/reference/init-aria.md` - Cross-reference

  **Acceptance Criteria**:
  - [ ] File created at `docs/reference/opcodes/init.md`
  - [ ] Documents init-only opcodes from System + Text categories
  - [ ] Cross-references `reference/init-aria.md`
  - [ ] Written in Japanese

  **QA Scenarios**:
  ```
  Scenario: Verify init opcodes
    Tool: Grep + Read
    Steps:
      1. Read CommandRegistry.cs to identify init-only commands in System/Text categories
      2. Read docs/reference/opcodes/init.md to verify coverage
      3. Verify `window`, `font`, `compat_mode` are documented
    Expected Result: Complete init reference
    Evidence: .sisyphus/evidence/task-21-init-check.txt
  ```

  **Commit**: YES (Wave 2)
  - Message: `docs: add init opcode reference`

- [x] 22. **Write tutorials/getting-started.md**

  **What to do**:
  - Complete rewrite of the getting-started tutorial
  - Learning-oriented: takes user from zero to running their first script
  - Step-by-step, minimal explanation, lots of examples
  - Must include:
    1. Install .NET 8.0 SDK
    2. Clone/build the project
    3. Create init.aria (with explanations)
    4. Create main.aria with simple text
    5. Run and see results
    6. Next steps (link to creating-ui tutorial)
  - Use CORRECT syntax throughout (fix `bg` command usage, ensure all commands are documented)
  - Write in **Japanese**

  **Must NOT do**:
  - Use undocumented commands without explanation
  - Include broken syntax
  - Go into deep explanation (that's for Explanation docs)

  **Recommended Agent Profile**:
  - **Category**: `writing`
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 3 (with Tasks 23-29)
  - **Blocks**: None
  - **Blocked By**: Task 1 (directory creation)

  **References**:
  - Current `docs/tutorials/getting-started.md` - Existing tutorial to improve
  - `docs/reference/init-aria.md` - init.aria reference (cross-link)
  - `docs/reference/syntax.md` - Syntax reference (cross-link)
  - Root `README.md` - Quick start section

  **Acceptance Criteria**:
  - [ ] File created at `docs/tutorials/getting-started.md`
  - [ ] Step-by-step tutorial format
  - [ ] All code blocks use documented commands
  - [ ] Links to next tutorial (creating-ui)
  - [ ] Written in Japanese

  **QA Scenarios**:
  ```
  Scenario: Verify getting-started tutorial code is valid
    Tool: Bash
    Steps:
      1. `test -f docs/tutorials/getting-started.md` → PASS
      2. Extract all ```aria blocks to temp files
      3. Run Parser.cs against each temp file
      4. Assert zero parse errors
    Expected Result: All tutorial code parses successfully
    Evidence: .sisyphus/evidence/task-22-tutorial-parse-check.txt
  ```

  **Commit**: YES (Wave 3)
  - Message: `docs: rewrite getting-started tutorial`

- [x] 23. **Write tutorials/creating-ui.md**

  **What to do**:
  - Tutorial for creating a title screen with buttons
  - Step-by-step: create background, add buttons, add hover effects, handle clicks
  - Use documented opcodes only
  - Fix any broken syntax from current version
  - Write in **Japanese**

  **Recommended Agent Profile**:
  - **Category**: `writing`
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 3 (with Tasks 22, 24-29)
  - **Blocks**: None
  - **Blocked By**: Task 1 (directory creation)

  **References**:
  - Current `docs/tutorials/creating-ui.md` - Existing tutorial
  - `docs/reference/opcodes/sprite.md` - Sprite commands (cross-link)
  - `docs/reference/opcodes/button.md` - Button commands (cross-link)

  **Acceptance Criteria**:
  - [ ] File created at `docs/tutorials/creating-ui.md`
  - [ ] Step-by-step tutorial format
  - [ ] All code blocks use documented commands
  - [ ] Written in Japanese

  **QA Scenarios**:
  ```
  Scenario: Verify creating-ui tutorial code is valid
    Tool: Bash
    Steps:
      1. `test -f docs/tutorials/creating-ui.md` → PASS
      2. Extract all ```aria blocks, run through Parser.cs
      3. Assert zero parse errors
    Expected Result: All tutorial code parses successfully
    Evidence: .sisyphus/evidence/task-23-ui-tutorial-check.txt
  ```

  **Commit**: YES (Wave 3)
  - Message: `docs: rewrite creating-ui tutorial`

- [x] 24. **Write tutorials/chapter-system.md**

  **What to do**:
  - Tutorial for implementing a chapter selection system
  - Step-by-step: define chapters, create selection screen, implement unlock logic
  - **CRITICAL**: Fix the broken `gosub *chapter_card with 1, 1` syntax to `gosub *chapter_card, 1, 1`
  - Use documented opcodes only
  - Write in **Japanese**

  **Must NOT do**:
  - Use `gosub with` syntax

  **Recommended Agent Profile**:
  - **Category**: `writing`
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 3 (with Tasks 22-23, 25-29)
  - **Blocks**: None
  - **Blocked By**: Task 1 (directory creation)

  **References**:
  - Current `docs/tutorials/chapter-system.md` - Existing tutorial (fix syntax!)
  - `docs/reference/opcodes/chapter.md` - Chapter commands (cross-link)
  - `docs/reference/opcodes/flag.md` - Flag commands (cross-link)

  **Acceptance Criteria**:
  - [ ] File created at `docs/tutorials/chapter-system.md`
  - [ ] Step-by-step tutorial format
  - [ ] All code blocks use documented commands
  - [ ] No `gosub with` syntax (uses `gosub *label, arg1, arg2`)
  - [ ] Written in Japanese

  **QA Scenarios**:
  ```
  Scenario: Verify chapter tutorial code is valid
    Tool: Bash
    Steps:
      1. `test -f docs/tutorials/chapter-system.md` → PASS
      2. `grep -c "with" docs/tutorials/chapter-system.md` → 0 (no broken syntax)
      3. Extract all ```aria blocks, run through Parser.cs
      4. Assert zero parse errors
    Expected Result: All tutorial code parses successfully, no broken gosub syntax
    Evidence: .sisyphus/evidence/task-24-chapter-tutorial-check.txt
  ```

  **Commit**: YES (Wave 3)
  - Message: `docs: rewrite chapter-system tutorial`

- [x] 25. **Write tutorials/save-load.md**

  **What to do**:
  - Tutorial for implementing save/load functionality
  - Step-by-step: create save points, implement save screen, implement load screen
  - Clarify that engine has built-in save/load and tutorial shows how to use it (not reimplement it)
  - Use documented opcodes only
  - Write in **Japanese**

  **Recommended Agent Profile**:
  - **Category**: `writing`
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 3 (with Tasks 22-24, 26-29)
  - **Blocks**: None
  - **Blocked By**: Task 1 (directory creation)

  **References**:
  - Current `docs/tutorials/save-load.md` - Existing tutorial
  - `docs/reference/opcodes/system.md` - Save/load commands (cross-link)

  **Acceptance Criteria**:
  - [ ] File created at `docs/tutorials/save-load.md`
  - [ ] Step-by-step tutorial format
  - [ ] All code blocks use documented commands
  - [ ] Clarifies built-in vs custom save/load
  - [ ] Written in Japanese

  **QA Scenarios**:
  ```
  Scenario: Verify save-load tutorial code is valid
    Tool: Bash
    Steps:
      1. `test -f docs/tutorials/save-load.md` → PASS
      2. Extract all ```aria blocks, run through Parser.cs
      3. Assert zero parse errors
    Expected Result: All tutorial code parses successfully
    Evidence: .sisyphus/evidence/task-25-save-load-check.txt
  ```

  **Commit**: YES (Wave 3)
  - Message: `docs: rewrite save-load tutorial`

- [ ] 26. **Write how-to-guides/compile-and-package.md**

  **What to do**:
  - How-to guide for creating release builds
  - Goal-oriented: "How do I distribute my game?"
  - Cover:
    1. Development vs Release mode explanation
    2. `aria-compile` command usage
    3. `aria-pack` command usage
    4. Key management (environment variables)
    5. CI/CD integration (GitHub Actions)
    6. Testing the release build
  - Include concrete command examples with placeholders
  - Write in **Japanese**

  **Must NOT do**:
  - Explain why compilation works (that's Explanation)
  - Write narrative/tutorial (that's Tutorials)

  **Recommended Agent Profile**:
  - **Category**: `writing`
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 3 (with Tasks 22-25, 27-29)
  - **Blocks**: None
  - **Blocked By**: Task 1 (directory creation)

  **References**:
  - Root `README.md` - Compilation/packaging section
  - `.github/workflows/aria-cicd.yml` - CI/CD workflow
  - `scripts/cicd.ps1` - Local CI script

  **Acceptance Criteria**:
  - [ ] File created at `docs/how-to-guides/compile-and-package.md`
  - [ ] Goal-oriented format (not tutorial)
  - [ ] Covers aria-compile and aria-pack
  - [ ] Covers key management
  - [ ] Covers CI/CD
  - [ ] Written in Japanese

  **QA Scenarios**:
  ```
  Scenario: Verify compile guide is complete
    Tool: Bash
    Steps:
      1. `test -f docs/how-to-guides/compile-and-package.md` → PASS
      2. `grep -c "aria-compile" docs/how-to-guides/compile-and-package.md` → >= 1
      3. `grep -c "aria-pack" docs/how-to-guides/compile-and-package.md` → >= 1
      4. `grep -c "ARIA_PACK_KEY" docs/how-to-guides/compile-and-package.md` → >= 1
    Expected Result: Complete compile/pack guide
    Evidence: .sisyphus/evidence/task-26-compile-check.txt
  ```

  **Commit**: YES (Wave 3)
  - Message: `docs: add compile and package how-to guide`

- [ ] 27. **Write how-to-guides/debug-mode.md**

  **What to do**:
  - How-to guide for using debug mode
  - Cover:
    1. Enabling debug mode (init.aria + F3 toggle)
    2. What each debug display shows (FPS, PC, sprite count, button outlines)
    3. How to interpret the information
    4. Common debugging workflows
  - Write in **Japanese**

  **Recommended Agent Profile**:
  - **Category**: `writing`
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 3 (with Tasks 22-26, 28-29)
  - **Blocks**: None
  - **Blocked By**: Task 1 (directory creation)

  **References**:
  - `AGENTS.md` - Debug mode mention
  - `src/AriaEngine/Program.cs` - Debug toggle implementation
  - `src/AriaEngine/Input/InputHandler.cs` - F3 handling

  **Acceptance Criteria**:
  - [ ] File created at `docs/how-to-guides/debug-mode.md`
  - [ ] Goal-oriented format
  - [ ] Covers all debug features
  - [ ] Written in Japanese

  **QA Scenarios**:
  ```
  Scenario: Verify debug guide is complete
    Tool: Bash
    Steps:
      1. `test -f docs/how-to-guides/debug-mode.md` → PASS
      2. `grep -c "F3" docs/how-to-guides/debug-mode.md` → >= 1
      3. `grep -c "FPS\|PC\|sprite" docs/how-to-guides/debug-mode.md` → >= 3
    Expected Result: Complete debug guide
    Evidence: .sisyphus/evidence/task-27-debug-check.txt
  ```

  **Commit**: YES (Wave 3)
  - Message: `docs: add debug mode how-to guide`

- [ ] 28. **Write how-to-guides/custom-fonts.md**

  **What to do**:
  - How-to guide for using custom fonts
  - Cover:
    1. Supported font formats (TTF)
    2. Font file placement
    3. init.aria font configuration
    4. Font atlas size considerations
    5. Filter mode (bilinear, etc.)
    6. Troubleshooting font issues
  - Write in **Japanese**

  **Recommended Agent Profile**:
  - **Category**: `writing`
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 3 (with Tasks 22-27, 29)
  - **Blocks**: None
  - **Blocked By**: Task 1 (directory creation)

  **References**:
  - `docs/reference/init-aria.md` - Font configuration (cross-link)
  - `src/AriaEngine/Rendering/SpriteRenderer.cs` - Font loading

  **Acceptance Criteria**:
  - [ ] File created at `docs/how-to-guides/custom-fonts.md`
  - [ ] Goal-oriented format
  - [ ] Covers font configuration
  - [ ] Written in Japanese

  **QA Scenarios**:
  ```
  Scenario: Verify custom fonts guide exists
    Tool: Bash
    Steps:
      1. `test -f docs/how-to-guides/custom-fonts.md` → PASS
      2. `grep -c "font" docs/how-to-guides/custom-fonts.md` → >= 3
    Expected Result: Complete font guide
    Evidence: .sisyphus/evidence/task-28-fonts-check.txt
  ```

  **Commit**: YES (Wave 3)
  - Message: `docs: add custom fonts how-to guide`

- [ ] 29. **Write how-to-guides/troubleshooting.md**

  **What to do**:
  - How-to guide for common problems
  - Problem/solution format:
    - "Build fails" → check .NET SDK version
    - "Script won't parse" → check syntax, use debug mode
    - "Sprites not showing" → check vsp, alpha, z-order
    - "Audio not playing" → check file path, format
    - "Game crashes on start" → check init.aria, font path
    - "Text looks wrong" → check font, atlas size
  - Each problem: symptoms, cause, solution, prevention
  - Write in **Japanese**

  **Recommended Agent Profile**:
  - **Category**: `writing`
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 3 (with Tasks 22-28)
  - **Blocks**: None
  - **Blocked By**: Task 1 (directory creation)

  **References**:
  - Current `docs/README.md` - Troubleshooting section (minimal)
  - Current `docs/tutorials/getting-started.md` - Common issues
  - `src/AriaEngine/Core/Errors.cs` or error handling - Error messages

  **Acceptance Criteria**:
  - [ ] File created at `docs/how-to-guides/troubleshooting.md`
  - [ ] Problem/solution format
  - [ ] Covers at least 6 common problems
  - [ ] Written in Japanese

  **QA Scenarios**:
  ```
  Scenario: Verify troubleshooting guide is comprehensive
    Tool: Bash
    Steps:
      1. `test -f docs/how-to-guides/troubleshooting.md` → PASS
      2. Count problem sections: `grep -c "^## " docs/how-to-guides/troubleshooting.md`
      3. Verify count >= 6
    Expected Result: Comprehensive troubleshooting guide
    Evidence: .sisyphus/evidence/task-29-troubleshoot-check.txt
  ```

  **Commit**: YES (Wave 3)
  - Message: `docs: add troubleshooting how-to guide`

- [ ] 30. **Write explanation/language-philosophy.md**

  **What to do**:
  - Explanation of Aria scripting language design philosophy
  - Understanding-oriented: why was it designed this way?
  - Cover:
    - NScripter compatibility goals and trade-offs
    - Why script-driven design (vs code-driven)
    - Register model (`%n`, `$name`) design decisions
    - Init vs runtime separation rationale
    - Compatibility mode purpose
  - Update from current core-spec.md: remove draft status, validate claims against implementation, mark any unimplemented features as "planned"
  - Write in **Japanese**

  **Must NOT do**:
  - Leave unimplemented features undocumented as "existing"
  - Keep "draft" status

  **Recommended Agent Profile**:
  - **Category**: `writing`
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 4 (with Tasks 31-35)
  - **Blocks**: None
  - **Blocked By**: Task 1 (directory creation)

  **References**:
  - Current `docs/architecture/language-philosophy.md` - Existing philosophy doc
  - `docs/scripting/core-spec.md` - Core spec (extract valid parts, discard draft)
  - `src/AriaEngine/Core/Parser.cs` - Validate claims

  **Acceptance Criteria**:
  - [ ] File created at `docs/explanation/language-philosophy.md`
  - [ ] Understanding-oriented (not tutorial/reference)
  - [ ] No "draft" marking
  - [ ] Unimplemented features marked as "planned"
  - [ ] Written in Japanese

  **QA Scenarios**:
  ```
  Scenario: Verify language philosophy doc
    Tool: Bash
    Steps:
      1. `test -f docs/explanation/language-philosophy.md` → PASS
      2. `grep -c "ドラフト\|draft" docs/explanation/language-philosophy.md` → 0
      3. `grep -c "NScripter" docs/explanation/language-philosophy.md` → >= 1
    Expected Result: Polished philosophy doc, no draft status
    Evidence: .sisyphus/evidence/task-30-philosophy-check.txt
  ```

  **Commit**: YES (Wave 4)
  - Message: `docs: add language philosophy explanation`

- [ ] 31. **Write explanation/architecture-overview.md**

  **What to do**:
  - High-level explanation of AriaEngine architecture
  - Understanding-oriented: how do the pieces fit together?
  - Cover:
    - Component diagram (text-based)
    - Data flow: script → parser → VM → renderer
    - State management (GameState)
    - Rendering pipeline
    - Audio pipeline
    - Input flow
  - Condense current `docs/architecture/overview.md` (360 lines) to focused explanation
  - Write in **Japanese** (or bilingual if architecture explanation benefits both audiences)

  **Recommended Agent Profile**:
  - **Category**: `writing`
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 4 (with Tasks 30, 32-35)
  - **Blocks**: None
  - **Blocked By**: Task 1 (directory creation)

  **References**:
  - Current `docs/architecture/overview.md` - Existing overview
  - `AGENTS.md` - Architecture summary
  - `src/AriaEngine/Program.cs` - Main loop

  **Acceptance Criteria**:
  - [ ] File created at `docs/explanation/architecture-overview.md`
  - [ ] Contains component diagram (text)
  - [ ] Explains data flow
  - [ ] Written in Japanese

  **QA Scenarios**:
  ```
  Scenario: Verify architecture overview
    Tool: Bash
    Steps:
      1. `test -f docs/explanation/architecture-overview.md` → PASS
      2. `grep -c "Parser\|VM\|Renderer" docs/explanation/architecture-overview.md` → >= 3
    Expected Result: Complete architecture overview
    Evidence: .sisyphus/evidence/task-31-arch-overview-check.txt
  ```

  **Commit**: YES (Wave 4)
  - Message: `docs: add architecture overview explanation`

- [ ] 32. **Write explanation/virtual-machine.md**

  **What to do**:
  - Deep explanation of the Virtual Machine
  - Cover:
    - Instruction format
    - Program counter and execution flow
    - Call stack and subroutine handling
    - State machine (Running, WaitingForClick, etc.)
    - Register model
    - How opcodes are dispatched
  - Based on current `docs/architecture/vm.md` but restructured for understanding
  - Write in **Japanese**

  **Recommended Agent Profile**:
  - **Category**: `writing`
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 4 (with Tasks 30-31, 33-35)
  - **Blocks**: None
  - **Blocked By**: Task 1 (directory creation)

  **References**:
  - Current `docs/architecture/vm.md` - Existing VM documentation
  - `src/AriaEngine/Core/VirtualMachine.cs` - VM implementation
  - `src/AriaEngine/Core/GameState.cs` - State definitions

  **Acceptance Criteria**:
  - [ ] File created at `docs/explanation/virtual-machine.md`
  - [ ] Explains VM execution model
  - [ ] Written in Japanese

  **QA Scenarios**:
  ```
  Scenario: Verify VM explanation
    Tool: Bash
    Steps:
      1. `test -f docs/explanation/virtual-machine.md` → PASS
      2. `grep -c "program counter\|call stack\|state" docs/explanation/virtual-machine.md` → >= 3
    Expected Result: Complete VM explanation
    Evidence: .sisyphus/evidence/task-32-vm-check.txt
  ```

  **Commit**: YES (Wave 4)
  - Message: `docs: add virtual machine explanation`

- [ ] 33. **Write explanation/parser.md**

  **What to do**:
  - Explanation of the script parser
  - Cover:
    - Tokenization process
    - Command parsing
    - Label resolution
    - Subroutine handling
    - Expression parsing (if conditions)
    - Error handling and reporting
  - Based on current `docs/architecture/parser.md` but focused on understanding
  - Write in **Japanese**

  **Recommended Agent Profile**:
  - **Category**: `writing`
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 4 (with Tasks 30-32, 34-35)
  - **Blocks**: None
  - **Blocked By**: Task 1 (directory creation)

  **References**:
  - Current `docs/architecture/parser.md` - Existing parser documentation
  - `src/AriaEngine/Core/Parser.cs` - Parser implementation

  **Acceptance Criteria**:
  - [ ] File created at `docs/explanation/parser.md`
  - [ ] Explains parsing pipeline
  - [ ] Written in Japanese

  **QA Scenarios**:
  ```
  Scenario: Verify parser explanation
    Tool: Bash
    Steps:
      1. `test -f docs/explanation/parser.md` → PASS
      2. `grep -c "token\|parse\|label" docs/explanation/parser.md` → >= 3
    Expected Result: Complete parser explanation
    Evidence: .sisyphus/evidence/task-33-parser-check.txt
  ```

  **Commit**: YES (Wave 4)
  - Message: `docs: add parser explanation`

- [ ] 34. **Write explanation/rendering.md**

  **What to do**:
  - Explanation of the rendering system
  - Cover:
    - Sprite types (Image, Text, Rect)
    - Z-order and rendering pipeline
    - Text rendering and wrapping
    - Transitions and effects
    - Tween system
    - Performance considerations
  - Based on current `docs/architecture/rendering.md`
  - Write in **Japanese**

  **Recommended Agent Profile**:
  - **Category**: `writing`
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 4 (with Tasks 30-33, 35)
  - **Blocks**: None
  - **Blocked By**: Task 1 (directory creation)

  **References**:
  - Current `docs/architecture/rendering.md` - Existing rendering documentation
  - `src/AriaEngine/Rendering/SpriteRenderer.cs` - Renderer implementation
  - `src/AriaEngine/Rendering/TweenManager.cs` - Tween system
  - `src/AriaEngine/Core/Sprite.cs` - Sprite definitions

  **Acceptance Criteria**:
  - [ ] File created at `docs/explanation/rendering.md`
  - [ ] Explains rendering pipeline
  - [ ] Written in Japanese

  **QA Scenarios**:
  ```
  Scenario: Verify rendering explanation
    Tool: Bash
    Steps:
      1. `test -f docs/explanation/rendering.md` → PASS
      2. `grep -c "sprite\|Z-order\|transition\|tween" docs/explanation/rendering.md` → >= 4
    Expected Result: Complete rendering explanation
    Evidence: .sisyphus/evidence/task-34-rendering-check.txt
  ```

  **Commit**: YES (Wave 4)
  - Message: `docs: add rendering explanation`

- [ ] 35. **Write explanation/design-decisions.md**

  **What to do**:
  - Document key design decisions and their rationale
  - Cover:
    - Why .aria instead of Lua/Python?
    - Why register-based variables instead of full variables?
    - Why init/runtime separation?
    - Why Raylib?
    - Why NScripter compatibility?
    - Trade-offs made (simplicity vs power)
  - New file (no existing equivalent)
  - Write in **Japanese**

  **Recommended Agent Profile**:
  - **Category**: `writing`
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 4 (with Tasks 30-34)
  - **Blocks**: None
  - **Blocked By**: Task 1 (directory creation)

  **References**:
  - `docs/architecture/language-philosophy.md` - Design principles
  - Root `README.md` - Feature list (understand priorities)
  - Git history (if relevant) - Major design changes

  **Acceptance Criteria**:
  - [ ] File created at `docs/explanation/design-decisions.md`
  - [ ] Covers at least 5 design decisions
  - [ ] Each decision has rationale and trade-offs
  - [ ] Written in Japanese

  **QA Scenarios**:
  ```
  Scenario: Verify design decisions doc
    Tool: Bash
    Steps:
      1. `test -f docs/explanation/design-decisions.md` → PASS
      2. Count decisions: `grep -c "^## " docs/explanation/design-decisions.md`
      3. Verify count >= 5
    Expected Result: Comprehensive design decisions doc
    Evidence: .sisyphus/evidence/task-35-design-check.txt
  ```

  **Commit**: YES (Wave 4)
  - Message: `docs: add design decisions explanation`

- [ ] 36. **Remove old docs and create redirects**

  **What to do**:
  - After ALL new docs are written and verified:
    1. Delete old directories: `docs/architecture/`, `docs/scripting/`, `docs/api/`, `docs/tutorials/` (old), `docs/development/`
    2. Delete old files: `docs/README.md` (old version, already overwritten by Task 4), `AGENTS.md` (already superseded by Task 2), `CLAUDE.md` (already superseded by Task 2)
    3. Handle `src/AriaEngine/Compiler/BytecodeFormat.md`: move to `docs/explanation/bytecode-format.md` OR delete if obsolete (decide based on whether it's still relevant)
    4. Handle `docs/development/git-github.md`: move to `docs/development/` (keep as-is, it's accurate)
    5. Create minimal redirect files if external links might exist (optional)
  - BE CAREFUL: only delete after verifying new files exist and contain correct content

  **Must NOT do**:
  - Delete anything before new files are verified
  - Delete `docs/superpowers/` (appears to be planning docs, not end-user docs)

  **Recommended Agent Profile**:
  - **Category**: `quick`
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: NO (must run after all content tasks)
  - **Parallel Group**: Wave 5 (sequential)
  - **Blocks**: Tasks 37-39
  - **Blocked By**: ALL Tasks 1-35

  **References**:
  - All new doc files created in Tasks 1-35 - Verify existence before deletion

  **Acceptance Criteria**:
  - [ ] Old `docs/architecture/` deleted
  - [ ] Old `docs/scripting/` deleted
  - [ ] Old `docs/api/` deleted
  - [ ] Old `docs/tutorials/` (if different from new one) handled
  - [ ] `AGENTS.md` deleted
  - [ ] `CLAUDE.md` deleted
  - [ ] `BytecodeFormat.md` relocated or deleted
  - [ ] New files still exist after cleanup

  **QA Scenarios**:
  ```
  Scenario: Verify old docs are removed
    Tool: Bash
    Steps:
      1. `test ! -f AGENTS.md` → PASS
      2. `test ! -f CLAUDE.md` → PASS
      3. `test ! -d docs/architecture` → PASS
      4. `test ! -d docs/scripting` → PASS
      5. `test ! -d docs/api` → PASS
      6. `test -d docs/tutorials` → PASS (new one exists)
      7. `test -d docs/reference` → PASS (new one exists)
    Expected Result: Old dirs/files gone, new ones remain
    Evidence: .sisyphus/evidence/task-36-cleanup-check.txt
  ```

  **Commit**: YES (Wave 5)
  - Message: `docs: remove old documentation structure`

- [ ] 37. **Link integrity check**

  **What to do**:
  - Scan ALL new markdown files for internal links
  - Verify each link target exists
  - Fix any broken links
  - Check for links to deleted old docs and update them
  - Tools: grep for markdown link patterns, verify file existence

  **Must NOT do**:
  - Leave broken links

  **Recommended Agent Profile**:
  - **Category**: `quick`
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: NO (depends on Task 36)
  - **Parallel Group**: Wave 5 (sequential after 36)
  - **Blocks**: Task 38-39
  - **Blocked By**: Task 36

  **References**:
  - All markdown files in `docs/` - Scan for links

  **Acceptance Criteria**:
  - [ ] All internal markdown links verified
  - [ ] Zero broken internal links
  - [ ] No links to deleted old docs

  **QA Scenarios**:
  ```
  Scenario: Verify all links are valid
    Tool: Bash
    Steps:
      1. Extract all `[text](path)` links from docs/**/*.md
      2. For each relative link, verify target file exists
      3. Count broken links: should be 0
    Expected Result: Zero broken links
    Evidence: .sisyphus/evidence/task-37-links-check.txt
  ```

  **Commit**: YES (Wave 5)
  - Message: `docs: fix link integrity`

- [ ] 38. **Code block parse validation**

  **What to do**:
  - Extract ALL ` ```aria ` code blocks from `docs/tutorials/` and `docs/how-to-guides/`
  - Run each through Parser.cs to verify they parse without errors
  - If Parser.cs can't be run standalone, create a minimal validation script
  - Report any parse errors with file:line information
  - Fix any issues found

  **Must NOT do**:
  - Skip this validation
  - Leave broken tutorial code

  **Recommended Agent Profile**:
  - **Category**: `unspecified-high`
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: NO (depends on Task 36)
  - **Parallel Group**: Wave 5 (sequential after 36)
  - **Blocks**: Task 39
  - **Blocked By**: Task 36

  **References**:
  - `src/AriaEngine/Core/Parser.cs` - Parser to use for validation
  - All `docs/tutorials/*.md` and `docs/how-to-guides/*.md` - Files to validate

  **Acceptance Criteria**:
  - [ ] All aria code blocks extracted
  - [ ] All code blocks parse without error
  - [ ] Parse validation report generated

  **QA Scenarios**:
  ```
  Scenario: Verify all tutorial code parses
    Tool: Bash
    Steps:
      1. Extract all ```aria blocks from docs/tutorials/ and docs/how-to-guides/
      2. Create temp .aria files from each block
      3. Run parser validation on each
      4. Assert zero errors across all blocks
    Expected Result: 100% parse success rate
    Evidence: .sisyphus/evidence/task-38-parse-validation.txt
  ```

  **Commit**: YES (Wave 5)
  - Message: `docs: validate tutorial code blocks`

- [ ] 39. **Final compliance audit**

  **What to do**:
  - Comprehensive final check:
    1. Verify all "Must Have" items from plan are present
    2. Verify all "Must NOT Have" items are absent
    3. Verify Diátaxis structure is complete (all 4 quadrants + AI agent)
    4. Verify language separation (Japanese for end-user, English for AI)
    5. Verify opcode reference matches CommandRegistry.cs
    6. Verify no old files remain
    7. Generate final report
  - Create checklist and tick off each item

  **Recommended Agent Profile**:
  - **Category**: `oracle`
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: NO (depends on Tasks 36-38)
  - **Parallel Group**: Wave 5 (final task)
  - **Blocks**: F1-F4
  - **Blocked By**: Tasks 36-38

  **References**:
  - This plan document - Checklist against plan
  - `src/AriaEngine/Core/CommandRegistry.cs` - Opcode verification
  - `docs/` directory - Structure verification

  **Acceptance Criteria**:
  - [ ] All Must Have verified present
  - [ ] All Must NOT Have verified absent
  - [ ] Diátaxis structure verified complete
  - [ ] Language separation verified
  - [ ] Opcode reference verified against source
  - [ ] Final report generated

  **QA Scenarios**:
  ```
  Scenario: Final compliance verification
    Tool: Bash
    Steps:
      1. Check Must Have: verify dirs exist (tutorials, how-to-guides, reference, explanation, ai-agent)
      2. Check Must NOT Have: verify no AGENTS.md, no CLAUDE.md, no old architecture/
      3. Check language: grep Japanese files for English-only sections (should be minimal)
      4. Check AI files: verify docs/ai-agent/AGENTS.md is English
      5. Generate report with PASS/FAIL per item
    Expected Result: All items PASS
    Evidence: .sisyphus/evidence/task-39-final-compliance.txt
  ```

  **Commit**: YES (Wave 5)
  - Message: `docs: final compliance audit`

---

## Final Verification Wave

- [ ] F1. **Plan Compliance Audit** — `oracle`
  Read the plan end-to-end. For each "Must Have": verify implementation exists (read file, check sections). For each "Must NOT Have": search codebase for forbidden patterns — reject with file:line if found. Check evidence files exist in .sisyphus/evidence/. Compare deliverables against plan.
  Output: `Must Have [N/N] | Must NOT Have [N/N] | Tasks [N/N] | VERDICT: APPROVE/REJECT`

- [ ] F2. **Documentation Quality Review** — `unspecified-high`
  Review all new docs for: consistent terminology, correct markdown formatting, clear section headers, no placeholder text, no TODOs left in content. Check AI slop: excessive comments, generic content, copy-paste errors.
  Output: `Files [N reviewed] | Issues [N found] | VERDICT`

- [ ] F3. **Link & Code Validation** — `unspecified-high`
  Extract all markdown links, verify target files exist. Extract all ` ```aria ` blocks from tutorials, run through Parser.cs, assert zero errors. Cross-reference opcode names in reference/opcodes/ against CommandRegistry.cs.
  Output: `Links [N/N valid] | Code blocks [N/N parse] | Opcodes [N/N match] | VERDICT`

- [ ] F4. **Scope Fidelity Check** — `deep`
  For each task: read "What to do", read actual file. Verify 1:1 — everything in spec was built (no missing), nothing beyond spec was built (no creep). Check "Must NOT do" compliance. Detect cross-task contamination.
  Output: `Tasks [N/N compliant] | Contamination [CLEAN/N issues] | Unaccounted [CLEAN/N files] | VERDICT`

---

## Commit Strategy

- **Wave 1**: `docs: restructure directories and add AI agent guide`
- **Wave 2**: `docs: add complete opcode and syntax reference`
- **Wave 3**: `docs: add verified tutorials and how-to guides`
- **Wave 4**: `docs: add architecture explanations`
- **Wave 5**: `docs: remove old docs and verify integrity`
- **Final**: `docs: finalize documentation refresh`

---

## Success Criteria

### Verification Commands
```bash
# Directory structure check
dir docs\tutorials, docs\how-to-guides, docs\reference, docs\explanation, docs\ai-agent

# Opcode count validation
# (Compare docs/reference/opcodes/*.md against CommandRegistry.cs)

# Tutorial code validation
# (Extract all ```aria blocks, run through parser)

# Link integrity
# (Grep for markdown links, verify targets exist)
```

### Final Checklist
- [ ] All "Must Have" present
- [ ] All "Must NOT Have" absent
- [ ] All old docs removed or redirected
- [ ] All new docs linked from hub README
- [ ] AI agent guide merged into single source
- [ ] Tutorial code blocks parse without error
- [ ] Opcode reference matches CommandRegistry.cs
- [ ] Zero broken internal links
