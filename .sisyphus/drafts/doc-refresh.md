# Draft: AriaEngine ドキュメント刷新

## Requirements (confirmed)
- **対象**: すべてのドキュメント（docs/ 以下全体 + README.md + AGENTS.md + CLAUDE.md）
- **問題**: 言語の命令・機能が古い、分かりづらい
- **ターゲット読者**: エンドユーザー（スクリプト作者・ビジュアルノベル制作者）、AIエージェント
- **構成方針**: Diátaxis Framework（Tutorial, How-to Guide, Reference, Explanation）

## Audit Findings (Critical)

### P0 - Must Fix
1. **AGENTS.md / CLAUDE.md**: 完全に同一（117行完全一致）。メンテナンスの二重化、同期ずれのリスク
2. **opcodes.md**: 「63種類」と主張しているが、実際はもっと多い（カテゴリー合計で80+）。数値が矛盾
3. **core-spec.md**: 「ドラフト」と明記されたまま。設計哲学と実装が乖離している可能性
4. **bgコマンド**: tutorials/getting-started.md で使用されているが、opcodesリファレンスに存在しない
5. **パラメータ付きgosub**: `gosub *chapter_card with 1, 1` 構文がチュートリアルで使われているが、どこにも文書化されていない
6. **while/wend**: architecture/parser.md で言及されているが、control-flow.md やどのリファレンスにも存在しない
7. **BytecodeFormat.md**: プロジェクトルート外に孤立。`func`/`endfunc`/`local` 構文が他のどのドキュメントとも整合していない

### P1 - Should Fix
8. **config.json**: AGENTS.mdで言及されているが、利用可能な設定値が一切文書化されていない
9. **コンパイル/パッケージング**: README.mdで `--run-mode release`, `aria-compile`, `aria-pack` が言及されているが、詳細な使い方がどこにもない
10. **デバッグモード**: F3トグルだけが言及され、詳細な機能が文書化されていない
11. **タイpos**: "ウーザー入力"（basics.md）、"色りつき"（sprites.md）
12. **文字列比較の挙動**: advanced.mdで警告があるが、実際の動作が不明確

### P2 - Structural Issues
13. **言語の混在**: 80%が日本語、20%が英語（AGENTS.md/CLAUDE.md/BytecodeFormat.md）。AIエージェント向けは英語、エンドユーザー向けは日本語という方針が不明確
14. **重複**: Build/run手順がREADME.md/AGENTS.md/CLAUDE.md/docs/README.mdの4箇所に存在
15. **巨大ファイル**: architecture/overview.md（360行）、api/opcodes.md（2502行）
16. **advanced.mdがカオス**: チャプター、フラグ、カウンター、キャラクター管理、セーブ/ロードが1ファイルに混在

## Technical Decisions
- **Diátaxis構成**:
  - Tutorials: 学習指向（最初から最後までの手順）
  - How-to Guides: 問題解決指向（特定のゴール達成）
  - Reference: 情報参照向け（オペコード、構文、設定）
  - Explanation: 理解深化向け（設計思想、アーキテクチャ）
- **AIエージェント向け**: AGENTS.mdを統合・刷新（CLAUDE.mdは削除または役割分化）
- **言語方針**: エンドユーザー向けは日本語、AIエージェント向けは英語（ただし、日本語話者AI向けにも日本語情報は必要）

## Open Questions
- AGENTS.mdとCLAUDE.mdを統合するか、役割を分けるか？
- opcodes.mdの正確な総数は？（ソースコード確認が必要）
- `bg`, `while/wend`, パラメータ付きgosub は実際に実装されているか？

## Scope Boundaries
- INCLUDE: すべての既存.mdファイルの書き換え・再構成、新規ファイルの追加
- EXCLUDE: ソースコードの変更、新機能の実装（ドキュメントのみ）
