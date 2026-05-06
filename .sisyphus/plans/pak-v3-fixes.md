# Pak v3 クリティカル修正計画

## TL;DR

> **概要**: 5つのレビュー（Goal Verification, QA, Code Quality, Security, Context Mining）で特定されたPak v3実装のクリティカルバグを修正する。
>
> **成果物**:
> - `PakArchiveV3.cs` — PathString/Entry同期修正、境界チェック追加
> - `PakAssetProviderV3.cs` — キャッシュ読み取り、LRU更新、スレッド安全性、パストラバーサル対策、一時ファイルクリーンアップ
> - `Lz4Compression.cs` — 境界チェック、int.MinValueガード
> - `ZstdCompression.cs` — 最大出力サイズ制限
> - `AriaPackCommand.cs` — 引数パース安全化、Zstd圧縮フラグ修正、`--output`尊重、init.aria重複排除、v3非split対応
>
> **見積工数**: Medium
> **並列実行**: YES — 4 Waves
> **クリティカルパス**: Task 1 → Task 2 → Task 5/6/7 → Task 13 → F1-F4

---

## Context

### オリジナルリクエスト
レビューで特定された19の問題を、修正優先の方針で対処する。

### レビュー概要
**Key Discussions**:
- Goal Verification: キャッシュが読まれない、ChunkEncryption未統合、`.ariu`未生成、`--format v3`でv2に暗黙フォールバック
- Code Quality: PathString/Entryデシンク、LRU更新漏れ、スレッド安全性欠如、Zstd圧縮フラグバグ
- Security: パストラバーサル、zip bomb、LZ4負数オーバーフロー、境界チェック欠如
- Context Mining: `Program.cs`がv3未対応、エントリー数キャッシュ制限が死コード

### Metis Review
**特定されたギャップ**（対処済み）:
- 報告された問題と実コードの相違を検証 → 4件すべて実際のバグと確認
- Phase 1（外科的修正）とPhase 2（機能完成）の分離を推奨
- 受け入れ基準に実行可能なテストを要求

---

## Work Objectives

### Core Objective
Pak v3実装のデータ破損、セキュリティ脆弱性、および動作不良を修正し、リリース可能な品質に引き上げる。

### Concrete Deliverables
- `src/AriaEngine/Packaging/PakArchiveV3.cs` — マニフェスト並べ替え修正、Read境界チェック
- `src/AriaEngine/AssetIO/PakAssetProviderV3.cs` — キャッシュ読み取り、LRU更新、ロック、パストラバーサル対策、Disposeクリーンアップ
- `src/AriaEngine/Packaging/Compression/Lz4Compression.cs` — 入力長チェック、int.MinValueガード
- `src/AriaEngine/Packaging/Compression/ZstdCompression.cs` — 最大出力サイズ制限
- `src/AriaEngine/Tools/AriaPackCommand.cs` — 引数パース安全化、Zstdフラグ修正、output尊重、重複排除、v3非split対応
- 各修正に対応するxUnitテスト

### Definition of Done
- [ ] すべての修正が単体テストで検証されている
- [ ] `dotnet test` で全テストが通過（既存の2件のpre-existing failureを除く）
- [ ] `dotnet build` で0警告、0エラー

### Must Have
- データ破損を防ぐマニフェスト同期修正
- セキュリティ脆弱性（パストラバーサル、zip bomb、境界チェック）の対策
- キャッシュが実際に機能する（読み取り→LRU更新→エントリー数制限）
- CLIの安全な引数パース

### Must NOT Have (Guardrails)
- ChunkEncryptionの配線（Phase 2に分離）
- `.ariu`生成（Phase 2に分離）
- `Program.cs`でのv3プロバイダー有効化（Phase 2に分離）
- v2プロバイダー（`PakAssetProvider`）の変更
- 新しいCLI引数の追加（`--output`修正以外）
- パフォーマンス最適化（正確性修正のみ）

---

## Verification Strategy

> **ゼロ人間介入** — すべての検証はエージェントが実行する。

### Test Decision
- **インフラ存在**: YES（xUnit）
- **自動テスト**: Tests-after（修正後にテスト追加）
- **フレームワーク**: xUnit（.NET 8.0）
- **方針**: 各修正タスクに対して、失敗するテストを先に書き（RED）、修正で通過（GREEN）、リファクタリングなし（修正範囲を限定）

### QA Policy
各タスクにエージェント実行QAシナリオを含む。証拠は `.sisyphus/evidence/task-{N}-{scenario-slug}.{ext}` に保存。
- **ライブラリ/モジュール**: Bash（dotnet test）— テスト実行、出力検証

---

## Execution Strategy

### Parallel Execution Waves

```
Wave 1 (Foundation — データ破損修正 + 安全基盤):
├── Task 1: PakArchiveV3.Write() PathStrings同期修正 [quick]
├── Task 2: PakAssetProviderV3 キャッシュ読み取り + LRU修正 [deep]
└── Task 3: AriaPackCommand 引数パース安全化 [quick]

Wave 2 (Parallel — 圧縮・CLI修正):
├── Task 4: AriaPackCommand Zstd圧縮フラグ修正 [quick]
├── Task 5: AriaPackCommand --output尊重 + init.aria重複排除 [quick]
├── Task 6: PakAssetProviderV3 スレッド安全性 + エントリー数制限 [deep]
└── Task 7: AriaPackCommand --format v3 非split対応 [quick]

Wave 3 (Security — ハードニング):
├── Task 8: PakAssetProviderV3 MaterializeToFile パストラバーサル対策 [unspecified-high]
├── Task 9: PakArchiveV3Reader ReadAllBytes 境界チェック [quick]
├── Task 10: Lz4Compression 入力検証 + int.MinValueガード [quick]
└── Task 11: ZstdCompression 最大出力サイズ制限 [quick]

Wave 4 (Cleanup + Integration):
└── Task 12: PakAssetProviderV3 一時ファイルクリーンアップ [quick]

Wave FINAL (4並列レビュー → ユーザ承認):
├── Task F1: 計画適合監査 (oracle)
├── Task F2: コード品質レビュー (unspecified-high)
├── Task F3: 実手動QA (unspecified-high)
└── Task F4: 範囲忠実性チェック (deep)
```

### Dependency Matrix

- **Task 1**: なし → Task 2, F1-F4
- **Task 2**: なし → Task 6, F1-F4
- **Task 3**: なし → Task 4, 5, 7, F1-F4
- **Task 4**: Task 3 → F1-F4
- **Task 5**: Task 3 → F1-F4
- **Task 6**: Task 2 → F1-F4
- **Task 7**: Task 3 → F1-F4
- **Task 8**: Task 2, 6 → F1-F4
- **Task 9**: Task 1 → F1-F4
- **Task 10**: なし → F1-F4
- **Task 11**: なし → F1-F4
- **Task 12**: Task 8 → F1-F4

### Agent Dispatch Summary

- **Wave 1**: 3 tasks — T1 `quick`, T2 `deep`, T3 `quick`
- **Wave 2**: 4 tasks — T4 `quick`, T5 `quick`, T6 `deep`, T7 `quick`
- **Wave 3**: 4 tasks — T8 `unspecified-high`, T9 `quick`, T10 `quick`, T11 `quick`
- **Wave 4**: 1 task — T12 `quick`
- **FINAL**: 4 tasks — F1 `oracle`, F2 `unspecified-high`, F3 `unspecified-high`, F4 `deep`

---

## TODOs

- [x] 1. PakArchiveV3.Write() PathStrings同期修正

  **What to do**:
  - `PakArchiveV3.Write()` で `manifest.Entries` を `PathHash` でソートする際、`manifest.PathStrings` も同じ順序で並べ替える
  - 現在のコード（line 52-53）は `Entries` のみソートし、`PathStrings` は元の順序のまま → index-based mapping が破損
  - 修正後: `sorted` の順序に基づいて `PathStrings` も再構築

  **Must NOT do**:
  - v2の `PakArchive.Write()` は変更しない
  - バイナリフォーマットのマジックバイトやヘッダー構造は変更しない

  **Recommended Agent Profile**:
  - **Category**: `quick`
    - Reason: 単一ファイル、局所的な並べ替えロジックの修正
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 1（Task 2, 3と並列）
  - **Blocks**: Task 9（境界チェックテストはソート済みマニフェストを前提）
  - **Blocked By**: なし

  **References**:
  - `src/AriaEngine/Packaging/PakArchiveV3.cs:52-53` — `OrderBy(e => e.PathHash)` で Entries のみソート
  - `src/AriaEngine/Packaging/PakArchiveV3.cs:116-122` — PathStringPool の書き込み（index-based）
  - `src/AriaEngine/Tools/AriaPackCommand.cs:276-287` — `manifest.PathStrings.Add(logicalPath)` をエントリーと同じ順序で追加

  **Acceptance Criteria**:
  - [ ] テスト: 3エントリー（`z.txt`, `a.txt`, `m.txt`）をWrite → Read → 各 `Entries[i].PathHash` が `PathHash64(PathStrings[i])` と一致
  - [ ] `dotnet test` で新規テストがPASS

  **QA Scenarios**:
  ```
  Scenario: Write 3 entries out-of-hash-order, read back, verify sync
    Tool: Bash (dotnet test)
    Preconditions: テストプロジェクトがビルド済み
    Steps:
      1. xUnitテストを実行: 非ソート順のエントリーをWrite → Read
      2. アサーション: `manifest.Entries[i].PathHash == PakArchiveV3Reader.PathHash64(manifest.PathStrings[i])` for all i
    Expected Result: すべてのインデックスで一致
    Failure Indicators: 任意のインデックスで不一致
    Evidence: .sisyphus/evidence/task-1-manifest-sync.xml
  ```

  **Evidence to Capture**:
  - [ ] `task-1-manifest-sync.xml` — テスト実行結果

  **Commit**: YES（Wave 1完了時にグループコミット）
  - Message: `fix(packaging): Sync PathStrings order with sorted Entries in PakArchiveV3`
  - Files: `src/AriaEngine/Packaging/PakArchiveV3.cs`

- [x] 2. PakAssetProviderV3 キャッシュ読み取り + LRU修正

  **What to do**:
  - `ReadAllBytesInternal` の先頭で、該当カテゴリーのキャッシュ（`_dataCache`, `_voiceCache`, `_scenarioCache`, `_bootCache`）を先に参照し、ヒットなら即return
  - `CacheAdd` でキャッシュヒット時に `LinkedList` ノードを末尾に移動（LRU順序更新）
  - `_dataLru.Remove(key)` → `_dataLru.AddLast(key)` のパターンを追加

  **Must NOT do**:
  - キャッシュの全体構造をリファクタリングしない（外科的修正のみ）
  - `_streamCache` はこのタスクでは触らない（未使用のため）

  **Recommended Agent Profile**:
  - **Category**: `deep`
    - Reason: スレッド安全性、LRUアルゴリズム、複数キャッシュの整合性が絡む
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 1（Task 1, 3と並列）
  - **Blocks**: Task 6（エントリー数制限はLRUが正しく機能してから）、Task 8
  - **Blocked By**: なし

  **References**:
  - `src/AriaEngine/AssetIO/PakAssetProviderV3.cs:141-199` — `ReadAllBytesInternal`（現在キャッシュ参照なし）
  - `src/AriaEngine/AssetIO/PakAssetProviderV3.cs:214-240` — `CacheAdd`（LRU更新漏れ）

  **Acceptance Criteria**:
  - [ ] テスト: 同じアセットを2回読み取り、2回目はディスクアクセスなし（キャッシュヒット）
  - [ ] テスト: 3アセット（A, B, C）を読み取り、Aを繰り返しアクセス後にキャッシュ満杯にし、BまたはCが追い出されることを確認（Aは残る）
  - [ ] `dotnet test` で新規テストがPASS

  **QA Scenarios**:
  ```
  Scenario: Cache hit avoids disk read
    Tool: Bash (dotnet test)
    Preconditions: テスト用v3 pak（1エントリー）を作成
    Steps:
      1. `ReadAllBytes("test.txt")` → 初回読み取り（キャッシュミス）
      2. `ReadAllBytes("test.txt")` → 2回目（キャッシュヒット）
      3. アサーション: 2回目の呼び出しで `PakArchiveV3Reader.ReadAllBytes` が呼ばれていない（Moqまたは内部カウンター）
    Expected Result: 2回目はキャッシュから返却
    Evidence: .sisyphus/evidence/task-2-cache-hit.xml

  Scenario: LRU update prevents hot item eviction
    Tool: Bash (dotnet test)
    Preconditions: データキャッシュを容量1エントリーに制限（テスト用）
    Steps:
      1. アセットAを読み取り
      2. アセットBを読み取り（Aが追い出されるはずだが、Aを再度アクセス）
      3. アセットCを読み取り
      4. アサーション: Bが追い出され、Aは残存
    Expected Result: 頻繁にアクセスされたAが残り、Bが追い出される
    Evidence: .sisyphus/evidence/task-2-lru-eviction.xml
  ```

  **Evidence to Capture**:
  - [ ] `task-2-cache-hit.xml`
  - [ ] `task-2-lru-eviction.xml`

  **Commit**: YES（Wave 1完了時にグループコミット）
  - Message: `fix(assetio): Fix PakAssetProviderV3 cache read and LRU update`
  - Files: `src/AriaEngine/AssetIO/PakAssetProviderV3.cs`

- [x] 3. AriaPackCommand 引数パース安全化

  **What to do**:
  - すべての `args[++i]` パターンに対し、`i + 1 < args.Length` の境界チェックを追加
  - 境界超過時は `InvalidOperationException("Missing value for argument {flag}")` を投げる
  - `ast_grep_search` で `args\[\+\+i\]` パターンを検索し、すべて対応

  **Must NOT do**:
  - 引数パース全体をリファクタリングしない（最小限の修正）
  - 新しいCLI引数を追加しない

  **Recommended Agent Profile**:
  - **Category**: `quick`
    - Reason: 単純な境界チェック追加
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 1（Task 1, 2と並列）
  - **Blocks**: Task 4, 5, 7（これらはCLIの安全なパースを前提）
  - **Blocked By**: なし

  **References**:
  - `src/AriaEngine/Tools/AriaPackCommand.cs:91-109` — `RunBuild` の引数パース
  - `src/AriaEngine/Tools/AriaPackCommand.cs:343-360` — `RunDiff` の引数パース
  - `src/AriaEngine/Tools/AriaPackCommand.cs:391-408` — `RunApply` の引数パース

  **Acceptance Criteria**:
  - [ ] テスト: `aria-pack build --input`（値なし）→ `InvalidOperationException` で "Missing value for argument --input"
  - [ ] テスト: `aria-pack build --format`（値なし）→ 同様
  - [ ] `dotnet test` で新規テストがPASS

  **QA Scenarios**:
  ```
  Scenario: Missing argument value throws clear error
    Tool: Bash (dotnet test)
    Preconditions: テストプロジェクトがビルド済み
    Steps:
      1. `AriaPackCommand.Run(new[] { "build", "--input" })` を実行
      2. アサーション: `InvalidOperationException` が投げられ、メッセージに "Missing value for argument --input" を含む
    Expected Result: 境界超過で例外、Not IndexOutOfRangeException
    Evidence: .sisyphus/evidence/task-3-arg-parse.xml
  ```

  **Evidence to Capture**:
  - [ ] `task-3-arg-parse.xml`

  **Commit**: YES（Wave 1完了時にグループコミット）
  - Message: `fix(tools): Add bounds checks to AriaPackCommand argument parsing`
  - Files: `src/AriaEngine/Tools/AriaPackCommand.cs`

- [x] 4. AriaPackCommand Zstd圧縮フラグ修正

  **What to do**:
  - Boot/ScenarioカテゴリーのZstd圧縮で、圧縮効率が悪い場合（`payload.Length >= data.Length`）に `payload = data`（元データ）を設定
  - 現在のコードは `compressed = false` にするが `payload` は圧縮データのまま → consumer は非圧縮フラグなのに圧縮データを読む
  - 修正: `if (payload.Length >= data.Length) { payload = data; compressed = false; flags = 0x00; }`

  **Must NOT do**:
  - LZ4のロジックは変更しない（LZ4ラッパーが既に非圧縮マーカーを付ける）
  - 圧縮レベルは変更しない

  **Recommended Agent Profile**:
  - **Category**: `quick`
    - Reason: 単純な条件分岐追加
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 2（Task 5, 6, 7と並列）
  - **Blocks**: F1-F4
  - **Blocked By**: Task 3

  **References**:
  - `src/AriaEngine/Tools/AriaPackCommand.cs:220-226` — Boot/Scenario Zstd圧縮ロジック
  - `src/AriaEngine/Packaging/Compression/ZstdCompression.cs` — ZstdCompress実装

  **Acceptance Criteria**:
  - [ ] テスト: 非圧縮可能なデータ（ランダムバイト）をZstd圧縮 → `flags == 0x00` かつ `Size == OriginalSize`
  - [ ] テスト: 圧縮可能なデータ（0埋めバイト）をZstd圧縮 → `flags == 0x02` かつ `Size < OriginalSize`

  **QA Scenarios**:
  ```
  Scenario: Incompressible data stores original bytes with flags=0x00
    Tool: Bash (dotnet test)
    Preconditions: テスト用データ準備
    Steps:
      1. ランダムバイト配列（圧縮不可）でZstdCompress
      2. 結果の `flags` と `payload.Length` を検証
      3. アサーション: `flags == 0x00 && payload.Length == data.Length`
    Expected Result: 非圧縮フラグ、元データサイズ
    Evidence: .sisyphus/evidence/task-4-zstd-flags.xml
  ```

  **Evidence to Capture**:
  - [ ] `task-4-zstd-flags.xml`

  **Commit**: YES（Wave 2完了時にグループコミット）
  - Message: `fix(tools): Store original payload when Zstd compression is ineffective`
  - Files: `src/AriaEngine/Tools/AriaPackCommand.cs`

- [x] 5. AriaPackCommand --output尊重 + init.aria重複排除

  **What to do**:
  - `WriteCategoryPak` の出力先を `outputPath` のディレクトリに変更（現在は `build/` 固定）
  - `outputPath` がファイルパスならそのディレクトリを、ディレクトリならそのまま使用
  - init.aria のスキャンループでの重複排除: `if (rel == bootLogical) continue;`

  **Must NOT do**:
  - v2パス（非split）の `--output` ロジックは変更しない（既に機能している）
  - `WriteCategoryPak` のシグネチャを変更しない（内部ローカル関数のまま）

  **Recommended Agent Profile**:
  - **Category**: `quick`
    - Reason: パス計算とスキップロジックの追加
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 2（Task 4, 6, 7と並列）
  - **Blocks**: F1-F4
  - **Blocked By**: Task 3

  **References**:
  - `src/AriaEngine/Tools/AriaPackCommand.cs:128-145` — boot追加とスキャンループ
  - `src/AriaEngine/Tools/AriaPackCommand.cs:189-298` — `WriteCategoryPak`（`build/` 固定）
  - `src/AriaEngine/Tools/AriaPackCommand.cs:293-294` — `Directory.CreateDirectory("build")`

  **Acceptance Criteria**:
  - [ ] テスト: `--output ./custom/out.pak --format v3 --split` → `./custom/boot.arib` などが生成される
  - [ ] テスト: `init.aria` が boot のみに含まれ、scenario に含まれない

  **QA Scenarios**:
  ```
  Scenario: v3 split respects --output directory
    Tool: Bash (dotnet test)
    Preconditions: テスト用入力ディレクトリ（init.aria + 1画像 + 1スクリプト）
    Steps:
      1. `RunBuild(new[] { "build", "--input", "./test_assets", "--output", "./custom/data.pak", "--format", "v3", "--split" })`
      2. アサーション: `./custom/boot.arib` と `./custom/scenario.aris` が存在
      3. アサーション: `./build/` は存在しない（または古いものと区別）
    Expected Result: カスタムディレクトリに出力
    Evidence: .sisyphus/evidence/task-5-output-respect.xml

  Scenario: init.aria appears only in boot
    Tool: Bash (dotnet test)
    Preconditions: テスト用入力ディレクトリに init.aria と他の .aria
    Steps:
      1. v3 splitビルド実行
      2. boot.arib を読み取り → init.aria が存在
      3. scenario.aris を読み取り → init.aria が存在しない
    Expected Result: init.aria は boot のみ
    Evidence: .sisyphus/evidence/task-5-init-aria-dedup.xml
  ```

  **Evidence to Capture**:
  - [ ] `task-5-output-respect.xml`
  - [ ] `task-5-init-aria-dedup.xml`

  **Commit**: YES（Wave 2完了時にグループコミット）
  - Message: `fix(tools): Respect --output in v3 split mode and dedupe init.aria`
  - Files: `src/AriaEngine/Tools/AriaPackCommand.cs`

- [x] 6. PakAssetProviderV3 スレッド安全性 + エントリー数制限

  **What to do**:
  - `_scenarioCache` と `_bootCache` へのアクセスを `lock` で保護
  - `CacheAdd` のエビクションループにエントリー数制限（`DataCacheEntriesLimit`, `VoiceCacheEntriesLimit`）を追加
  - エビクション条件: `cachedBytes > limit || cache.Count > entryLimit`

  **Must NOT do**:
  - `ConcurrentDictionary` に置き換えない（既存の `Dictionary + lock` パターンに合わせる）
  - LRUアルゴリズム全体をリファクタリングしない

  **Recommended Agent Profile**:
  - **Category**: `deep`
    - Reason: スレッド安全性、複数キャッシュの整合性、エビクションロジック
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 2（Task 4, 5, 7と並列）
  - **Blocks**: F1-F4
  - **Blocked By**: Task 2

  **References**:
  - `src/AriaEngine/AssetIO/PakAssetProviderV3.cs:49-50` — `_scenarioCache`, `_bootCache`（ロックなし）
  - `src/AriaEngine/AssetIO/PakAssetProviderV3.cs:62-65` — エントリー数制限定数（未使用）
  - `src/AriaEngine/AssetIO/PakAssetProviderV3.cs:191-196` — シナリオ/bootキャッシュへの書き込み
  - `src/AriaEngine/AssetIO/PakAssetProviderV3.cs:214-240` — `CacheAdd`（バイト制限のみ）

  **Acceptance Criteria**:
  - [ ] テスト: `Parallel.For(0, 100, _ => provider.ReadAllBytes("boot/init.aria"))` が `InvalidOperationException` を投げない
  - [ ] テスト: 65個のデータエントリーをキャッシュ → 64個を超えると最古のものが追い出される

  **QA Scenarios**:
  ```
  Scenario: Concurrent boot reads are thread-safe
    Tool: Bash (dotnet test)
    Preconditions: v3 pak（bootカテゴリー）を作成
    Steps:
      1. `Parallel.For(0, 100, _ => provider.ReadAllBytes("boot_file.txt"))`
      2. アサーション: 例外なし、100回すべて正常終了
    Expected Result: スレッド安全
    Evidence: .sisyphus/evidence/task-6-thread-safety.xml

  Scenario: Entry count limit enforces eviction
    Tool: Bash (dotnet test)
    Preconditions: テスト用に `DataCacheEntriesLimit = 2` に一時変更
    Steps:
      1. 3つの異なるアセットを順次読み取り
      2. アサーション: `_dataCache.Count == 2`
      3. アサーション: 最初に読み取ったアセットが追い出されている
    Expected Result: エントリー数制限が機能
    Evidence: .sisyphus/evidence/task-6-entry-limit.xml
  ```

  **Evidence to Capture**:
  - [ ] `task-6-thread-safety.xml`
  - [ ] `task-6-entry-limit.xml`

  **Commit**: YES（Wave 2完了時にグループコミット）
  - Message: `fix(assetio): Add thread-safety and entry count limits to PakAssetProviderV3`
  - Files: `src/AriaEngine/AssetIO/PakAssetProviderV3.cs`

- [x] 7. AriaPackCommand --format v3 非split対応

  **What to do**:
  - `--format v3` かつ `--split` なしの場合、単一のv3 pakを生成する分岐を追加
  - または、v3非split未対応として明示的なエラーを投げる: `throw new InvalidOperationException("v3 non-split mode is not yet supported. Use --split with --format v3.")`
  - 方針: 現時点ではエラーを投げる（v3非splitの仕様が未確定のため）

  **Must NOT do**:
  - v3非splitの完全実装（設計判断が必要）
  - v2パスへの影響

  **Recommended Agent Profile**:
  - **Category**: `quick`
    - Reason: 単純な条件分岐追加
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 2（Task 4, 5, 6と並列）
  - **Blocks**: F1-F4
  - **Blocked By**: Task 3

  **References**:
  - `src/AriaEngine/Tools/AriaPackCommand.cs:122` — `if (format == "v3" && split)`
  - `src/AriaEngine/Tools/AriaPackCommand.cs:313-333` — v2フォールバックパス

  **Acceptance Criteria**:
  - [ ] テスト: `RunBuild(new[] { "build", "--format", "v3", "--input", "./assets" })` → `InvalidOperationException` で明確なメッセージ
  - [ ] テスト: `RunBuild(new[] { "build", "--format", "v3", "--split", "--input", "./assets" })` → 正常実行

  **QA Scenarios**:
  ```
  Scenario: v3 without split throws clear error
    Tool: Bash (dotnet test)
    Preconditions: テスト用入力ディレクトリ
    Steps:
      1. `AriaPackCommand.Run(new[] { "build", "--format", "v3", "--input", "./test_assets" })`
      2. アサーション: `InvalidOperationException` が投げられ、メッセージに "v3 non-split" を含む
    Expected Result: 明示的エラー、暗黙フォールバックなし
    Evidence: .sisyphus/evidence/task-7-v3-nosplit.xml
  ```

  **Evidence to Capture**:
  - [ ] `task-7-v3-nosplit.xml`

  **Commit**: YES（Wave 2完了時にグループコミット）
  - Message: `fix(tools): Reject --format v3 without --split with clear error`
  - Files: `src/AriaEngine/Tools/AriaPackCommand.cs`

- [x] 8. PakAssetProviderV3 MaterializeToFile パストラバーサル対策

  **What to do**:
  - `MaterializeToFile` で `normalized` パスに `..` や絶対パス（`C:\`, `/`）が含まれていないことを検証
  - 無効なパスの場合は `ArgumentException("Path contains invalid traversal characters")` を投げる
  - `Path.DirectorySeparatorChar` と `/` の両方をチェック

  **Must NOT do**:
  - `MaterializeToFile` の基本動作（一時ディレクトリ作成、ファイル書き込み）は変更しない
  - 正規表現ベースの複雑なパス検証を導入しない（単純な `Contains("..")` で十分）

  **Recommended Agent Profile**:
  - **Category**: `unspecified-high`
    - Reason: セキュリティ修正、入力検証の正確性が重要
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 3（Task 9, 10, 11と並列）
  - **Blocks**: Task 12（クリーンアップはこの修正後）
  - **Blocked By**: Task 2, 6

  **References**:
  - `src/AriaEngine/AssetIO/PakAssetProviderV3.cs:125-136` — `MaterializeToFile`
  - `src/AriaEngine/AssetIO/PakAssetProviderV3.cs:242` — `NormalizePath`

  **Acceptance Criteria**:
  - [ ] テスト: `MaterializeToFile("../../../etc/passwd")` → `ArgumentException`
  - [ ] テスト: `MaterializeToFile("normal/path/file.txt")` → 正常に一時ファイルが作成される
  - [ ] テスト: `MaterializeToFile("/absolute/path")` → `ArgumentException`

  **QA Scenarios**:
  ```
  Scenario: Path traversal attempt is rejected
    Tool: Bash (dotnet test)
    Preconditions: PakAssetProviderV3インスタンス
    Steps:
      1. `provider.MaterializeToFile("../../../windows/system32/evil.dll")`
      2. アサーション: `ArgumentException` が投げられる
      3. アサーション: 一時ディレクトリ外にファイルが作成されていない
    Expected Result: 例外、ファイル作成なし
    Evidence: .sisyphus/evidence/task-8-path-traversal.xml
  ```

  **Evidence to Capture**:
  - [ ] `task-8-path-traversal.xml`

  **Commit**: YES（Wave 3完了時にグループコミット）
  - Message: `fix(security): Prevent path traversal in PakAssetProviderV3.MaterializeToFile`
  - Files: `src/AriaEngine/AssetIO/PakAssetProviderV3.cs`

- [x] 9. PakArchiveV3Reader ReadAllBytes 境界チェック

  **What to do**:
  - `ReadAllBytes` で `entry.Offset + entry.Size` がファイル/ストリームの実際の長さを超えていないことを検証
  - 超過している場合は `InvalidDataException("Manifest entry exceeds file bounds")` を投げる
  - MMFパスとストリームパスの両方でチェック

  **Must NOT do**:
  - マニフェストのパース時に厳密すぎる検証を追加しない（互換性維持）
  - ファイル全体の読み込みを追加しない（遅延読み込みを維持）

  **Recommended Agent Profile**:
  - **Category**: `quick`
    - Reason: 単純な境界チェック追加
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 3（Task 8, 10, 11と並列）
  - **Blocks**: F1-F4
  - **Blocked By**: Task 1

  **References**:
  - `src/AriaEngine/Packaging/PakArchiveV3.cs` — `PakArchiveV3Reader` クラス（ReadAllBytes実装）
  - `src/AriaEngine/Packaging/PakManifestV3.cs` — `PakManifestEntryV3`（Offset, Sizeフィールド）

  **Acceptance Criteria**:
  - [ ] テスト: 改竄されたpak（`manifestOffset` がファイル長を超える）を `Read()` → `InvalidDataException`
  - [ ] テスト: 改竄されたpak（`entry.Offset + entry.Size > fileLength`）を `ReadAllBytes` → `InvalidDataException`

  **QA Scenarios**:
  ```
  Scenario: Corrupted manifest offset throws on read
    Tool: Bash (dotnet test)
    Preconditions: テスト用v3 pakバイト列（ヘッダーの manifestOffset をファイル長+1に改竄）
    Steps:
      1. 改竄バイト列から `MemoryStream` を作成
      2. `PakArchiveV3Reader.Read(stream)` を実行
      3. アサーション: `InvalidDataException` が投げられる
    Expected Result: 境界超過で例外
    Evidence: .sisyphus/evidence/task-9-bounds-check.xml
  ```

  **Evidence to Capture**:
  - [ ] `task-9-bounds-check.xml`

  **Commit**: YES（Wave 3完了時にグループコミット）
  - Message: `fix(packaging): Add bounds checking to PakArchiveV3Reader`
  - Files: `src/AriaEngine/Packaging/PakArchiveV3.cs`

- [x] 10. Lz4Compression 入力検証 + int.MinValueガード

  **What to do**:
  - `Decompress` の先頭で `compressed.Length >= 4` を検証（不足なら `InvalidDataException`）
  - `storedSize < 0` の場合、`storedSize == int.MinValue` を特別にチェック（`InvalidDataException`）
  - 正の `storedSize` の場合、上限チェック（例: `storedSize <= compressed.Length * 100` または `storedSize <= 256 * 1024 * 1024`）

  **Must NOT do**:
  - `Compress` ロジックは変更しない
  - 既存の圧縮済みpakの互換性を破壊しない

  **Recommended Agent Profile**:
  - **Category**: `quick`
    - Reason: 単純な入力検証追加
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 3（Task 8, 9, 11と並列）
  - **Blocks**: F1-F4
  - **Blocked By**: なし

  **References**:
  - `src/AriaEngine/Packaging/Compression/Lz4Compression.cs:49-80` — `Decompress`
  - `src/AriaEngine/Packaging/Compression/Lz4Compression.cs:54-55` — `BitConverter.ToInt32`

  **Acceptance Criteria**:
  - [ ] テスト: `Decompress(new byte[] { 0x01, 0x02 })`（2バイト）→ `InvalidDataException`
  - [ ] テスト: `Decompress` with header `int.MinValue` → `InvalidDataException`
  - [ ] テスト: `Decompress` with header `256MB + 1` → `InvalidDataException`

  **QA Scenarios**:
  ```
  Scenario: Short input rejected
    Tool: Bash (dotnet test)
    Preconditions: なし
    Steps:
      1. `_lz4.Decompress(new byte[] { 0x01, 0x02 })`
      2. アサーション: `InvalidDataException`
    Expected Result: 4バイト未満で例外
    Evidence: .sisyphus/evidence/task-10-lz4-short.xml

  Scenario: int.MinValue header rejected
    Tool: Bash (dotnet test)
    Preconditions: なし
    Steps:
      1. `BitConverter.GetBytes(int.MinValue)` でヘッダーを作成し、適当なペイロードを付加
      2. `_lz4.Decompress` を実行
      3. アサーション: `InvalidDataException`
    Expected Result: int.MinValueヘッダーで例外
    Evidence: .sisyphus/evidence/task-10-lz4-minvalue.xml
  ```

  **Evidence to Capture**:
  - [ ] `task-10-lz4-short.xml`
  - [ ] `task-10-lz4-minvalue.xml`

  **Commit**: YES（Wave 3完了時にグループコミット）
  - Message: `fix(security): Harden Lz4Compression against malformed input`
  - Files: `src/AriaEngine/Packaging/Compression/Lz4Compression.cs`

- [x] 11. ZstdCompression 最大出力サイズ制限

  **What to do**:
  - `Decompress` で `MemoryStream` の出力を制限（例: 最大256MB）
  - 制限超過時は `InvalidDataException("Zstd decompressed size exceeds maximum allowed")` を投げる
  - 実装: `while` ループで `output.Length > maxSize` をチェック、または `ZstdStream` 読み取り中に累積サイズを監視

  **Must NOT do**:
  - `Compress` ロジックは変更しない
  - 既存の圧縮済みpakの互換性を破壊しない

  **Recommended Agent Profile**:
  - **Category**: `quick`
    - Reason: ストリームラッパー追加
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 3（Task 8, 9, 10と並列）
  - **Blocks**: F1-F4
  - **Blocked By**: なし

  **References**:
  - `src/AriaEngine/Packaging/Compression/ZstdCompression.cs` — `Decompress` 実装

  **Acceptance Criteria**:
  - [ ] テスト: 小さな圧縮データ（1KB → 10KB）→ 正常に解凍
  - [ ] テスト: zip bomb（1KB → 256MB+ を主張）→ `InvalidDataException`

  **QA Scenarios**:
  ```
  Scenario: Normal Zstd decompression works
    Tool: Bash (dotnet test)
    Preconditions: 圧縮済みテストデータ
    Steps:
      1. `_zstd.Decompress(compressedData)`
      2. アサーション: 出力が元データと一致
    Expected Result: 正常解凍
    Evidence: .sisyphus/evidence/task-11-zstd-normal.xml

  Scenario: Zip bomb rejected
    Tool: Bash (dotnet test)
    Preconditions: 圧縮率が極端に高いテストデータ（小さな入力が大きな出力になる）
    Steps:
      1. `_zstd.Decompress(zipBombData)`
      2. アサーション: `InvalidDataException`
    Expected Result: サイズ制限で例外
    Evidence: .sisyphus/evidence/task-11-zstd-zipbomb.xml
  ```

  **Evidence to Capture**:
  - [ ] `task-11-zstd-normal.xml`
  - [ ] `task-11-zstd-zipbomb.xml`

  **Commit**: YES（Wave 3完了時にグループコミット）
  - Message: `fix(security): Add maximum output size limit to ZstdCompression`
  - Files: `src/AriaEngine/Packaging/Compression/ZstdCompression.cs`

- [x] 12. PakAssetProviderV3 一時ファイルクリーンアップ

  **What to do**:
  - `Dispose()` で作成した一時ディレクトリ（`%TEMP%\aria_pak3_cache\{guid}\`）を削除
  - 作成したディレクトリパスのリスト `_tempDirs` を保持
  - `Dispose()` で `_tempDirs` を巡回し `Directory.Delete(tempDir, recursive: true)` を実行
  - 削除失敗は無視（try-catch）

  **Must NOT do**:
  - `MaterializeToFile` の戻り値のライフタイム契約を変更しない（呼び出し元が使用完了後にDisposeを呼ぶ前提）
  - プロセス終了時の自動クリーンアップはこのタスクでは実装しない

  **Recommended Agent Profile**:
  - **Category**: `quick`
    - Reason: 単純なリスト管理と削除
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: NO（Wave 4単独）
  - **Parallel Group**: Wave 4
  - **Blocks**: F1-F4
  - **Blocked By**: Task 8

  **References**:
  - `src/AriaEngine/AssetIO/PakAssetProviderV3.cs:96-102` — 現在の `Dispose`（pakリーダーのみ）
  - `src/AriaEngine/AssetIO/PakAssetProviderV3.cs:125-136` — `MaterializeToFile`（一時ディレクトリ作成）

  **Acceptance Criteria**:
  - [ ] テスト: `MaterializeToFile("test.txt")` → `Dispose()` → 一時ディレクトリが存在しない
  - [ ] テスト: 複数回 `MaterializeToFile` → `Dispose()` → すべてのGUIDディレクトリが削除されている

  **QA Scenarios**:
  ```
  Scenario: Temp files cleaned up on dispose
    Tool: Bash (dotnet test)
    Preconditions: PakAssetProviderV3インスタンス
    Steps:
      1. `var path = provider.MaterializeToFile("test.txt")`
      2. `var tempDir = Path.GetDirectoryName(path)`
      3. `provider.Dispose()`
      4. アサーション: `Directory.Exists(tempDir) == false`
    Expected Result: 一時ディレクトリが削除されている
    Evidence: .sisyphus/evidence/task-12-temp-cleanup.xml
  ```

  **Evidence to Capture**:
  - [ ] `task-12-temp-cleanup.xml`

  **Commit**: YES（Wave 4完了時にグループコミット）
  - Message: `fix(assetio): Clean up temp directories in PakAssetProviderV3.Dispose`
  - Files: `src/AriaEngine/AssetIO/PakAssetProviderV3.cs`

---

## Final Verification Wave

> 4つのレビューエージェントを並列実行。すべてが承認する必要がある。結果を統合してユーザーに提示し、明示的な「OK」を得てから完了とする。

- [ ] F1. **計画適合監査** — `oracle`
  計画をエンドツーエンドで読む。各「Must Have」に対して実装が存在することを検証（ファイル読み取り、コマンド実行）。各「Must NOT Have」に対して禁止パターンが存在しないことを検索。証拠ファイルが `.sisyphus/evidence/` に存在することを確認。
  出力: `Must Have [N/N] | Must NOT Have [N/N] | Tasks [N/N] | VERDICT: APPROVE/REJECT`

- [ ] F2. **コード品質レビュー** — `unspecified-high`
  `dotnet build` + `dotnet test` を実行。変更されたファイルをレビュー: `as any`/`@ts-ignore`、空のcatch、`console.log`、コメントアウトコード、未使用using。AI slopチェック: 過剰コメント、過剰抽象化、一般的な名前。
  出力: `Build [PASS/FAIL] | Tests [N pass/N fail] | Files [N clean/N issues] | VERDICT`

- [ ] F3. **実手動QA** — `unspecified-high`
  クリーン状態から開始。各タスクのQAシナリオをすべて実行 — 正確な手順に従い、証拠を取得。クロスタスク統合テスト（機能の連携）。エッジケース: 空状態、無効入力、高速アクション。
  出力: `Scenarios [N/N pass] | Integration [N/N] | Edge Cases [N tested] | VERDICT`

- [x] F4. **範囲忠実性チェック** — `deep`
  各タスクについて「What to do」と実際のdiff（git log/diff）を読み比べ。仕様通りに構築されていること（不足なし）、仕様外が構築されていないこと（クリープなし）を確認。「Must NOT do」遵守。クロスタスク汚染を検出。
  出力: `Tasks [12/12 compliant] | Contamination [CLEAN] | Unaccounted [CLEAN] | VERDICT: APPROVE`

---

## Remediation Wave (Post-FINAL)

> F2・F3 で CONDITIONAL PASS が出た残件を修正し、再レビューする。

### 残件一覧
- [x] **R1. Critical: LRU race condition** — `PakAssetProviderV3.cs:163-186`
  `ReadAllBytesInternal` の data/voice キャッシュヒット時のLRU操作（`Find`/`Remove`/`AddLast`）を `lock (_dataCache)` / `lock (_voiceCache)` で保護。
- [x] **R2. Test gap: Task 2 cache hit** — `PakAssetProviderV3Tests.cs`
  `DataCacheHit_ReturnsCachedData_WithoutTouchingDisk` を追加。`_pakReaders` を空にし `_dataCache` へリフレクション注入後、`ReadAllText` がディスクアクセスなしでキャッシュヒットすることを検証。
- [x] **R3. Test gap: Task 7 v3 nosplit error** — `PackTests.cs`
  `Build_FormatV3WithoutSplit_ReturnsErrorCode3` を追加。`--format v3`（`--split` なし）で `InvalidOperationException` がスローされ `Run` が `3` を返すことを検証。

### 検証結果（Remediation後）
- `dotnet build`: **0 warnings, 0 errors**
- `dotnet test`: **261/263 passed**（2件は `CommandTests` の pre-existing failure）
- 新規テスト3件: **すべて PASS**

---

## Final Verification Wave (Re-run F2, F3 after Remediation)

> F2・F3 を再実行して FULL PASS を得る。

- [x] F2-R. **コード品質再レビュー** — `unspecified-high`
  変更ファイル: `PakAssetProviderV3.cs`, `PakAssetProviderV3Tests.cs`, `PackTests.cs`
  `dotnet build` + `dotnet test` 実行。LRUロックの正確性、テストの網羅性、コード品質を再評価。
  出力: `Build [PASS/FAIL] | Tests [N pass/N fail] | Files [N clean/N issues] | VERDICT: APPROVE/REJECT`

- [x] F3-R. **実手動QA再レビュー** — `unspecified-high`
  Remediation R1-R3 のQAシナリオを実行。R1: スレッド安全性の並列読み取り。R2: キャッシュヒットでディスクアクセス回避。R3: v3 nosplit でエラーコード3。
  出力: `Scenarios [N/N pass] | Integration [N/N] | Edge Cases [N tested] | VERDICT: APPROVE/REJECT`

---

## Commit Strategy

- **Wave 1完了時**: `fix(packaging): PakArchiveV3 manifest sync and cache correctness`
- **Wave 2完了時**: `fix(packaging): AriaPackCommand compression flags and CLI fixes`
- **Wave 3完了時**: `fix(security): Pak v3 path traversal and bounds checks`
- **Wave 4完了時**: `fix(assetio): PakAssetProviderV3 temp cleanup and disposal`
- **FINAL後**: `test(packaging): Add xUnit tests for Pak v3 critical fixes`

---

## Success Criteria

### Verification Commands
```bash
dotnet build src/AriaEngine/AriaEngine.csproj  # Expected: 0 warnings, 0 errors
dotnet test src/AriaEngine.Tests/AriaEngine.Tests.csproj --verbosity minimal  # Expected: 236+ passed, 0 new failures
```

### Final Checklist
- [x] すべての「Must Have」が実装されている
- [x] すべての「Must NOT Have」が存在しない
- [x] すべてのテストが通過（2件のpre-existing failureを除く）
- [x] ビルドがクリーン（0警告、0エラー）
