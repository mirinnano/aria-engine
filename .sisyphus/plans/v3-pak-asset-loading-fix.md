# v3 Pak Asset Loading Fix

## TL;DR

> **概要**: v3 split pak (boot.arib / scenario.aris / data.arid / stream.arim / voice.ariv) への移行後、アセットがほとんど読み込めないバグを修正する。根本原因是パッケージ生成時の PathString に `assets/` プレフィックスが欠落していること。
>
> **成果物**:
> - `src/AriaEngine/Tools/AriaPackCommand.cs` — v3 split ビルド時の PathString に `assets/` prefix を付与
> - `src/AriaEngine/AssetIO/PakAssetProviderV3.cs` — `TryFindEntry` の二重プレフィックスバグ修正、冗長な二重ループ削除
> - `src/AriaEngine/Core/Commands/RenderCommandHandler.cs` — `BackgroundAssetExists` を `IAssetProvider.Exists` を使うように修正
> - 各修正に対応する xUnit テスト
>
> **見積工数**: Medium
> **並列実行**: YES — 4 Waves
> **クリティカルパス**: Task 1 → Task 2 → Task 3 → Task 5/6/7 → Task 8 → F1-F4

---

## Context

### オリジナルリクエスト
> このコードで、v3のデータ形式にした結果、起動はするがアセットをほとんど読み込めないバグが有る。パッケージ周りのバグをすべて発見し、そこから修正する計画を立てよう

### 症状
- エンジンは起動する
- ボタン（Rect スプライト）のみレンダリングされる — 画像アセットを必要としないため
- 背景画像、キャラクタースプライト、フォント、BGM/SE が一切読み込めない
- `aria_error_ai.txt` は空（`SafeFrame` でフレーム例外が抑制されているが、初回は記録されるはず）
- 既存の pak ファイルは破壊・再生成してよい

### 調査で特定されたバグ
**Bug A（最重大）**: `AriaPackCommand.WriteCategoryPak` で PathString に `assets/` プレフィックスが欠落
- v2 pak: `assets/fonts/NotoSansJP-Regular.ttf`
- v3 split pak: `fonts/NotoSansJP-Regular.ttf`（`assets/` なし）
- エンジンは `assets/fonts/...` で要求 → xxHash64 ハッシュ不一致 → `FindEntry` が常に失敗 → 全アセット Not Found

**Bug B**: `RenderCommandHandler.BackgroundAssetExists` が `File.Exists` を直接呼び出し
- Pak 使用時に `bg "forest.png"` の存在確認が常に失敗 → 黒背景フォールバック
- `IAssetProvider` を全く利用していない

**Bug C**: `PakAssetProviderV3.TryFindEntry` の二重プレフィックス
- `path` が既に `assets/...` の場合、`prefixedPath` = `assets/assets/...` になり無意味な検索を実行

**Bug D**: `PakAssetProviderV3.ReadAllBytesInternal` の冗長な二重ループ
- `TryFindEntry` で見つけたエントリーに対し、再度全 reader をループして `FindEntry` を探す。機能的には正しいが無駄。

### Metis Review
Metis subagent は実行不可だったため、手動でギャップ分析を実施：
- PathString 不一致は単一ファイルの修正で解決するが、reader 側のフォールバックも必要（互換性）
- `BackgroundAssetExists` の修正は `IAssetProvider` への参照が必要。`RenderCommandHandler` が `_assetProvider` を持っているか要確認。
- テストは `PakAssetProviderV3Tests` と `PackTests` を拡張。

---

## Work Objectives

### Core Objective
v3 split pak 形式でエンジンが全アセット（画像・フォント・音声・スクリプト）を正しく読み込めるようにする。

### Concrete Deliverables
- `src/AriaEngine/Tools/AriaPackCommand.cs` — v3 split 時に data/stream/voice カテゴリの PathString に `assets/` prefix を付与
- `src/AriaEngine/AssetIO/PakAssetProviderV3.cs` — `TryFindEntry` の二重 prefix 防止、`ReadAllBytesInternal` の冗長ループ削除
- `src/AriaEngine/Core/Commands/RenderCommandHandler.cs` — `BackgroundAssetExists` を `IAssetProvider.Exists` 経由に変更
- `src/AriaEngine.Tests/PakAssetProviderV3Tests.cs` — PathString prefix テスト追加
- `src/AriaEngine.Tests/PackTests.cs` — v3 split build の PathString 検証テスト追加

### Definition of Done
- [ ] `dotnet test` で全テストが通過（既存の pre-existing failure を除く）
- [ ] `dotnet build` で 0 警告、0 エラー
- [ ] 実際に `aria-pack build --format v3 --split` で生成した pak ファイルから、全カテゴリのアセットが `PakAssetProviderV3` 経由で読み込める

### Must Have
- v3 split pak の PathString がエンジンの要求パスと一致すること
- `BackgroundAssetExists` が Pak 内アセットを正しく認識すること
- 既存の v2 単一 pak 機能への影響がないこと

### Must NOT Have (Guardrails)
- v2 単一 pak (`PakArchive.Write` / `PakAssetProvider`) の変更
- `Program.cs` の起動ロジック変更（v3 auto-detection は現状のまま）
- 新しい CLI 引数の追加
- 圧縮・暗号化ロジックの変更
- Raylib 描画パイプラインの変更

---

## Verification Strategy

> **ゼロ人間介入** — すべての検証はエージェントが実行する。

### Test Decision
- **インフラ存在**: YES（xUnit）
- **自動テスト**: Tests-after（修正後にテスト追加）
- **フレームワーク**: xUnit（.NET 8.0）
- **方針**: 各修正タスクに対して、失敗するテストを先に書き（RED）、修正で通過（GREEN）

### QA Policy
各タスクにエージェント実行 QA シナリオを含む。証拠は `.sisyphus/evidence/task-{N}-{scenario-slug}.{ext}` に保存。
- **ライブラリ/モジュール**: Bash（dotnet test）— テスト実行、出力検証
- **CLI**: Bash（dotnet run -- aria-pack）— パッケージ生成と検証

---

## Execution Strategy

### Parallel Execution Waves

```
Wave 1 (Foundation — パッケージ生成側の PathString 修正):
├── Task 1: AriaPackCommand v3 split PathString に assets/ prefix を付与 [quick]
└── Task 2: AriaPackCommand v3 split boot/scenario の prefix なしを維持 [quick]

Wave 2 (Parallel — Provider 側の検索ロジック修正):
├── Task 3: PakAssetProviderV3.TryFindEntry の二重 prefix 防止 [quick]
├── Task 4: PakAssetProviderV3.ReadAllBytesInternal の冗長ループ削除 [quick]
└── Task 5: RenderCommandHandler.BackgroundAssetExists を IAssetProvider 経由に [quick]

Wave 3 (After Wave 1+2 — テスト追加・検証):
├── Task 6: PakAssetProviderV3Tests に prefix パス検索テスト追加 [unspecified-high]
├── Task 7: PackTests に v3 split PathString 検証テスト追加 [unspecified-high]
└── Task 8: エンドツーエンド: v3 split pak 生成 → 全アセット読み込み検証 [deep]

Wave FINAL (After ALL tasks — 4 parallel reviews, then user okay):
├── Task F1: Plan compliance audit (oracle)
├── Task F2: Code quality review (unspecified-high)
├── Task F3: Real manual QA (unspecified-high)
└── Task F4: Scope fidelity check (deep)
-> Present results -> Get explicit user okay

Critical Path: Task 1 → Task 2 → Task 3 → Task 5/6/7 → Task 8 → F1-F4 → user okay
Parallel Speedup: ~60% faster than sequential
Max Concurrent: 3 (Wave 2)
```

### Dependency Matrix
- **1**: - - 6, 7, 8
- **2**: - - 6, 7, 8
- **3**: - - 6, 8
- **4**: - - 6, 8
- **5**: - - 8
- **6**: 1, 2, 3, 4 - 8
- **7**: 1, 2 - 8
- **8**: 5, 6, 7 - F1-F4

### Agent Dispatch Summary
- **1**: **2** - T1-T2 → `quick`
- **2**: **3** - T3-T5 → `quick`
- **3**: **3** - T6-T8 → `unspecified-high`, `deep`
- **FINAL**: **4** - F1 → `oracle`, F2 → `unspecified-high`, F3 → `unspecified-high`, F4 → `deep`

---

## TODOs

- [x] 1. AriaPackCommand v3 split PathString に `assets/` prefix を付与

  **What to do**:
  - `src/AriaEngine/Tools/AriaPackCommand.cs` の `WriteCategoryPak` メソッド内で、各カテゴリのエントリーをリストに追加する際の `logicalPath` を修正する
  - `Data`, `Stream`, `Voice` カテゴリ: `rel`（例: `fonts/NotoSansJP-Regular.ttf`）の先頭に `assets/` を付与して `assets/fonts/NotoSansJP-Regular.ttf` とする
  - `Boot`, `Scenario` カテゴリ: `init.aria` や `scripts/scripts.ariac` はエンジンが `assets/` なしで要求するため、prefix なしのままとする
  - 修正箇所: `dataEntries.Add((rel, ...))` → `dataEntries.Add(("assets/" + rel, ...))` など
  - `streamEntries`, `voiceEntries` も同様

  **Must NOT do**:
  - v2 単一 pak ビルドロジック（`format != "v3"` または `!split` の分岐）には触らない
  - 圧縮・暗号化ロジックには触らない

  **Recommended Agent Profile**:
  - **Category**: `quick`
    - Reason: 単純な文字列連結の変更。ビルド通りの挙動変更。
  - **Skills**: []
  - **Skills Evaluated but Omitted**: `git-master`（コミットは Wave 最後にまとめて行う）

  **Parallelization**:
  - **Can Run In Parallel**: YES（Task 2 と並列）
  - **Parallel Group**: Wave 1
  - **Blocks**: Task 6, 7, 8
  - **Blocked By**: None

  **References**:
  **Pattern References**:
  - `src/AriaEngine/Tools/AriaPackCommand.cs:180-209` — v2 ビルド時の `logical` 計算: `$"{PakArchive.NormalizePath(inputDir)}/{rel}"`
  - `src/AriaEngine/Tools/AriaPackCommand.cs:220-225` — v3 split の `sorted` 生成: `LogicalPath = it.LogicalPath`

  **API/Type References**:
  - `PakArchive.NormalizePath(string)` — `path.Replace('\\', '/').TrimStart('/')`
  - `PakArchiveV3Reader.PathHash64(string)` — xxHash64 UTF-8 小文字

  **Test References**:
  - `src/AriaEngine.Tests/PackTests.cs` — 既存の pak ビルドテスト

  **WHY Each Reference Matters**:
  - v2 の `logical` 計算: `inputDir` = `assets` なので `assets/fonts/...` になる。v3 でも同じパス形式に合わせる必要がある。
  - `PathHash64`: ハッシュ値は `PathHash64(logicalPath)` で計算される。`logicalPath` を変えればハッシュも変わる。

  **Acceptance Criteria**:
  - [ ] `aria-pack build --format v3 --split --input assets` で生成された `data.arid` の PathString に `assets/` が含まれる
  - [ ] `boot.arib` の PathString は `init.aria` のまま（prefix なし）
  - [ ] `scenario.aris` の PathString は `scripts/scripts.ariac` のまま（prefix なし）

  **QA Scenarios**:
  ```
  Scenario: v3 split data.arid の PathString に assets/ prefix が付いている
    Tool: Bash
    Preconditions: `dotnet build` が成功している
    Steps:
      1. `dotnet run --project src/AriaEngine/AriaEngine.csproj -- aria-pack build --format v3 --split --input assets --output build/test_pak`
      2. 生成された `build/test_pak/data.arid` を `PakArchiveV3Reader.Open` で読み込む
      3. `reader.PathStrings` の先頭数個を確認
    Expected Result: `PathStrings` に `assets/fonts/...` や `assets/bg/...` が含まれる。`fonts/...` や `bg/...`（prefix なし）は含まれない。
    Failure Indicators: PathString が `fonts/...` のまま → prefix 付与失敗
    Evidence: .sisyphus/evidence/task-1-pathstring-prefix.txt

  Scenario: v3 split boot.arib の PathString は prefix なし
    Tool: Bash
    Preconditions: 上記と同じビルド成果物
    Steps:
      1. `build/test_pak/boot.arib` を `PakArchiveV3Reader.Open` で読み込む
      2. `reader.PathStrings` を確認
    Expected Result: `PathStrings` に `init.aria` が含まれる。`assets/init.aria` は含まれない。
    Failure Indicators: `assets/init.aria` が含まれる → boot カテゴリにも prefix が付いてしまった
    Evidence: .sisyphus/evidence/task-1-boot-prefix.txt
  ```

  **Evidence to Capture**:
  - [ ] テスト出力のスクリーンショットまたはテキストダンプ

  **Commit**: YES（Task 1-2 と一括）
  - Message: `fix(pack): prepend assets/ prefix to v3 split PathStrings for data/stream/voice`
  - Files: `src/AriaEngine/Tools/AriaPackCommand.cs`
  - Pre-commit: `dotnet test --filter "FullyQualifiedName~PackTests"`

---

- [x] 2. AriaPackCommand v3 split boot/scenario の prefix なしを維持

  **What to do**:
  - Task 1 と同ファイル・同メソッド内で、Boot と Scenario カテゴリのエントリー追加部分を確認・保証する
  - `bootEntries.Add((initLogical, File.ReadAllBytes(initPath)))` の `initLogical` は `Path.GetFileName(initPath)` または `init.aria` のまま
  - `scenarioEntries.Add((compiledLogical, ...))` の `compiledLogical` は `compiledPath.Replace('\\', '/')` のまま
  - 明示的に `Boot` と `Scenario` では prefix を付けないコメントを追加

  **Must NOT do**:
  - boot/scenario のパス形式を変更しない

  **Recommended Agent Profile**:
  - **Category**: `quick`
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: YES（Task 1 と並列）
  - **Parallel Group**: Wave 1
  - **Blocks**: Task 6, 7, 8
  - **Blocked By**: None

  **References**:
  - `src/AriaEngine/Program.cs:161-162` — `initScriptPath = runOptions.InitPath`（デフォルト `init.aria`）
  - `src/AriaEngine/Program.cs:459-460` — `provider.OpenRead(options.CompiledPath)`（デフォルト `scripts/scripts.ariac`）

  **Acceptance Criteria**:
  - [ ] Boot カテゴリの PathString に `assets/` が付かない
  - [ ] Scenario カテゴリの PathString に `assets/` が付かない

  **QA Scenarios**:
  ```
  Scenario: boot.arib と scenario.aris の PathString は prefix なし
    Tool: Bash
    Preconditions: Task 1 と同じビルド成果物
    Steps:
      1. `boot.arib` と `scenario.aris` を `PakArchiveV3Reader.Open` で読み込む
      2. `PathStrings` を確認
    Expected Result: boot は `init.aria`、scenario は `scripts/scripts.ariac` のみ
    Failure Indicators: `assets/init.aria` や `assets/scripts/scripts.ariac` が含まれる
    Evidence: .sisyphus/evidence/task-2-boot-scenario-prefix.txt
  ```

  **Commit**: YES（Task 1 と一括）

---

- [x] 3. PakAssetProviderV3.TryFindEntry の二重 prefix 防止

  **What to do**:
  - `src/AriaEngine/AssetIO/PakAssetProviderV3.cs` の `TryFindEntry` メソッドを修正
  - 現在: `string prefixedPath = PakArchive.NormalizePath("assets/" + path);`
  - 修正: `path` が既に `assets/` で始まる場合は `prefixedPath = normalizedPath` とする
  - 具体例:
    ```csharp
    string normalizedPath = PakArchive.NormalizePath(path);
    string prefixedPath = normalizedPath.StartsWith("assets/", StringComparison.OrdinalIgnoreCase)
        ? normalizedPath
        : PakArchive.NormalizePath("assets/" + path);
    ```
  - これにより、`assets/fonts/NotoSansJP-Regular.ttf` を要求した際に `assets/assets/...` にならなくなる

  **Must NOT do**:
  - v2 Provider (`PakAssetProvider.cs`) は変更しない
  - キャッシュロジックは変更しない

  **Recommended Agent Profile**:
  - **Category**: `quick`
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: YES（Task 4, 5 と並列）
  - **Parallel Group**: Wave 2
  - **Blocks**: Task 6, 8
  - **Blocked By**: None

  **References**:
  - `src/AriaEngine/AssetIO/PakAssetProviderV3.cs:335-359` — `TryFindEntry` の現在の実装
  - `src/AriaEngine/Packaging/PakArchive.cs:73` — `NormalizePath` の実装

  **Acceptance Criteria**:
  - [ ] `TryFindEntry("assets/fonts/test.ttf")` が `assets/fonts/test.ttf` のハッシュを正しく計算して検索する
  - [ ] `TryFindEntry("fonts/test.ttf")` が `fonts/test.ttf` と `assets/fonts/test.ttf` の両方を検索する

  **QA Scenarios**:
  ```
  Scenario: assets/ で始まるパスの二重 prefix 防止
    Tool: Bash (dotnet test)
    Preconditions: テスト用の v3 pak（PathString = `assets/fonts/test.ttf`）をメモリ上に構築
    Steps:
      1. `provider.TryFindEntry("assets/fonts/test.ttf", out _, out _)` を呼ぶ
    Expected Result: true が返る
    Failure Indicators: false → 二重 prefix で検索している
    Evidence: .sisyphus/evidence/task-3-double-prefix.txt

  Scenario: assets/ なしパスのフォールバック検索
    Tool: Bash (dotnet test)
    Preconditions: 同上
    Steps:
      1. `provider.TryFindEntry("fonts/test.ttf", out _, out _)` を呼ぶ
    Expected Result: true が返る（`assets/fonts/test.ttf` にフォールバック）
    Failure Indicators: false
    Evidence: .sisyphus/evidence/task-3-fallback-prefix.txt
  ```

  **Commit**: YES（Task 3-5 と一括）
  - Message: `fix(assetio): prevent double assets/ prefix in PakAssetProviderV3.TryFindEntry`
  - Files: `src/AriaEngine/AssetIO/PakAssetProviderV3.cs`
  - Pre-commit: `dotnet test --filter "FullyQualifiedName~PakAssetProviderV3Tests"`

---

- [x] 4. PakAssetProviderV3.ReadAllBytesInternal の冗長ループ削除

  **What to do**:
  - `ReadAllBytesInternal` 内で、`TryFindEntry` で見つけた reader と entry を使い回すようにする
  - 現在: `TryFindEntry` → `chosen` ループで再度 `FindEntry` → `ReadAllBytes`
  - 修正: `TryFindEntry` が reader 参照も返すようにシグネチャを変更するか、`ReadAllBytesInternal` 内でループを統合する
  - シンプルな修正: `TryFindEntry` の戻り値に reader インデックスを追加し、`ReadAllBytesInternal` でその reader を直接使用する
  - ただし `TryFindEntry` の戻り値を変えると呼び出し元に影響 → `Exists` メソッドは現在 `TryFindEntry(path, out _, out _)` と呼んでいる
  - 代替案: `ReadAllBytesInternal` 内で `TryFindEntry` のロジックをインライン化し、見つかった reader を直接使う

  **Must NOT do**:
  - 公開 API (`Exists`, `OpenRead` など) の動作を変更しない

  **Recommended Agent Profile**:
  - **Category**: `quick`
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: YES（Task 3, 5 と並列）
  - **Parallel Group**: Wave 2
  - **Blocks**: Task 6, 8
  - **Blocked By**: None

  **References**:
  - `src/AriaEngine/AssetIO/PakAssetProviderV3.cs:155-232` — `ReadAllBytesInternal` の現在の実装

  **Acceptance Criteria**:
  - [ ] `ReadAllBytesInternal` 内で `FindEntry` の二重呼び出しがなくなる
  - [ ] すべての既存テストが通過する

  **QA Scenarios**:
  ```
  Scenario: 冗長ループ削除後も正常読み込み
    Tool: Bash (dotnet test)
    Preconditions: テスト用 v3 pak
    Steps:
      1. `provider.ReadAllBytesInternal("assets/bg/test.png")` を呼ぶ
    Expected Result: 正しいバイト配列が返る
    Failure Indicators: 例外発生または不正なデータ
    Evidence: .sisyphus/evidence/task-4-redundant-loop.txt
  ```

  **Commit**: YES（Task 3-5 と一括）
  - Message: `refactor(assetio): eliminate redundant entry lookup loop in PakAssetProviderV3`

---

- [x] 5. RenderCommandHandler.BackgroundAssetExists を IAssetProvider 経由に変更

  **What to do**:
  - `src/AriaEngine/Core/Commands/RenderCommandHandler.cs` の `BackgroundAssetExists` メソッドを修正
  - 現在: `File.Exists` を直接呼び出し（Pak 非対応）
  - 修正: `IAssetProvider.Exists` を使うように変更
  - `RenderCommandHandler` が `_assetProvider` フィールドを持っているか確認。なければコンストラクタ経由で注入するか、メソッドシグネチャを変更する
  - `BackgroundAssetExists` は static メソッドなので、instance メソッドに変更するか、`IAssetProvider` を引数に追加する
  - 呼び出し元（`CreateBackgroundSprite` など）で `_assetProvider` を渡すように変更

  **Must NOT do**:
  - `DiskAssetProvider` の動作を変更しない（既存の dev モードへの影響を避ける）
  - 他の `File.Exists` 呼び出し（例: `MenuSystem.cs`）には触らない（スコープ外）

  **Recommended Agent Profile**:
  - **Category**: `quick`
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: YES（Task 3, 4 と並列）
  - **Parallel Group**: Wave 2
  - **Blocks**: Task 8
  - **Blocked By**: None

  **References**:
  - `src/AriaEngine/Core/Commands/RenderCommandHandler.cs:337-353` — `BackgroundAssetExists` の現在の実装
  - `src/AriaEngine/Core/Commands/RenderCommandHandler.cs:320-334` — `CreateBackgroundSprite` での呼び出し
  - `src/AriaEngine/AssetIO/IAssetProvider.cs` — `Exists(string path)` インターフェース

  **Acceptance Criteria**:
  - [ ] `BackgroundAssetExists` が `IAssetProvider.Exists` を呼び出す
  - [ ] dev モード（DiskAssetProvider）でも背景が正しく読み込まれる
  - [ ] release モード（PakAssetProviderV3）でも背景が正しく読み込まれる

  **QA Scenarios**:
  ```
  Scenario: Release モードで bg コマンドが背景画像を読み込む
    Tool: Bash (dotnet test)
    Preconditions: Mock IAssetProvider を使った単体テスト
    Steps:
      1. `BackgroundAssetExists("bg/room.jpg", mockProvider)` を呼ぶ
      2. mockProvider が `Exists("bg/room.jpg")` → true を返すように設定
    Expected Result: true
    Failure Indicators: false → File.Exists 経由で判定している
    Evidence: .sisyphus/evidence/task-5-bg-exists-provider.txt
  ```

  **Commit**: YES（Task 3-5 と一括）
  - Message: `fix(render): use IAssetProvider.Exists in BackgroundAssetExists for pak support`
  - Files: `src/AriaEngine/Core/Commands/RenderCommandHandler.cs`

---

- [x] 6. PakAssetProviderV3Tests に prefix パス検索テスト追加

  **What to do**:
  - `src/AriaEngine.Tests/PakAssetProviderV3Tests.cs` を開く
  - `Exists` / `OpenRead` / `ReadAllText` メソッドに対して、以下のテストケースを追加:
    - `assets/` prefix 付きパスが正しく解決される（`assets/fonts/test.ttf`）
    - `assets/` prefix なしパスが `assets/` prefix 付きエントリーにフォールバックされる（`fonts/test.ttf` → `assets/fonts/test.ttf`）
    - `assets/` prefix なしパスが prefix なしエントリーにもヒットする（`init.aria`）
  - テスト用の v3 pak を MemoryStream で構築し、`PakArchiveV3.Write` → `PakArchiveV3Reader` → `PakAssetProviderV3` の流れで検証

  **Must NOT do**:
  - 既存テストを削除しない

  **Recommended Agent Profile**:
  - **Category**: `unspecified-high`
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: YES（Task 7 と並列）
  - **Parallel Group**: Wave 3
  - **Blocks**: Task 8
  - **Blocked By**: Task 1, 2, 3, 4

  **References**:
  - `src/AriaEngine.Tests/PakAssetProviderV3Tests.cs` — 既存テスト
  - `src/AriaEngine.Tests/PackTests.cs` — pak 構築テストのパターン

  **Acceptance Criteria**:
  - [ ] 新規テストが `dotnet test --filter "FullyQualifiedName~PakAssetProviderV3Tests"` で PASS
  - [ ] テストが `assets/` prefix 有無の両方をカバー

  **QA Scenarios**:
  ```
  Scenario: PakAssetProviderV3 prefix テストが通過
    Tool: Bash
    Preconditions: Task 1-4 の修正が適用済み
    Steps:
      1. `dotnet test --filter "FullyQualifiedName~PakAssetProviderV3Tests" -v n`
    Expected Result: 全テスト PASS（既存の pre-existing failure を除く）
    Failure Indicators: 新規テストが FAIL
    Evidence: .sisyphus/evidence/task-6-provider-tests.txt
  ```

  **Commit**: YES（Task 6-7 と一括）
  - Message: `test(assetio): add PathString prefix resolution tests for PakAssetProviderV3`
  - Files: `src/AriaEngine.Tests/PakAssetProviderV3Tests.cs`

---

- [x] 7. PackTests に v3 split PathString 検証テスト追加

  **What to do**:
  - `src/AriaEngine.Tests/PackTests.cs` を開く
  - v3 split build のテストを追加:
    - `AriaPackCommand.Run(new[] { "build", "--format", "v3", "--split", "--input", "test_assets", "--output", "build/test" })` を実行
    - 生成された各 `.arib`/`.aris`/`.arid`/`.arim`/`.ariv` を `PakArchiveV3Reader.Open` で読み込む
    - `PathStrings` の内容を検証:
      - `data.arid`: `assets/bg/...`, `assets/fonts/...` など `assets/` prefix あり
      - `boot.arib`: `init.aria` など prefix なし
      - `scenario.aris`: `scripts/...` など prefix なし
  - テスト用の `test_assets` ディレクトリを一時作成し、テスト後にクリーンアップ

  **Must NOT do**:
  - 既存テストを削除しない
  - 実際の `assets/` ディレクトリに副作用を与えない

  **Recommended Agent Profile**:
  - **Category**: `unspecified-high`
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: YES（Task 6 と並列）
  - **Parallel Group**: Wave 3
  - **Blocks**: Task 8
  - **Blocked By**: Task 1, 2

  **References**:
  - `src/AriaEngine.Tests/PackTests.cs` — 既存テスト
  - `src/AriaEngine/Tools/AriaPackCommand.cs` — v3 split ビルドロジック

  **Acceptance Criteria**:
  - [ ] 新規テストが `dotnet test --filter "FullyQualifiedName~PackTests"` で PASS
  - [ ] `data.arid` の PathString に `assets/` prefix が含まれることを検証

  **QA Scenarios**:
  ```
  Scenario: v3 split build の PathString 検証テストが通過
    Tool: Bash
    Preconditions: Task 1-2 の修正が適用済み
    Steps:
      1. `dotnet test --filter "FullyQualifiedName~PackTests" -v n`
    Expected Result: 全テスト PASS
    Failure Indicators: PathString prefix 検証が FAIL
    Evidence: .sisyphus/evidence/task-7-pack-tests.txt
  ```

  **Commit**: YES（Task 6-7 と一括）
  - Message: `test(pack): verify v3 split PathStrings include assets/ prefix for data categories`
  - Files: `src/AriaEngine.Tests/PackTests.cs`

---

- [x] 8. エンドツーエンド: v3 split pak 生成 → 全アセット読み込み検証

  **What to do**:
  - 統合テストを作成または手動で実行:
    1. `aria-pack build --format v3 --split --input assets --output build/e2e_test` を実行
    2. 生成された 5 つの pak ファイルを `PakAssetProviderV3` で読み込む
    3. 各カテゴリの代表アセットを `Exists` → `OpenRead` → `ReadAllBytesInternal` で読み込み、内容を検証:
       - boot: `init.aria`（テキスト内容の先頭数文字を検証）
       - scenario: `scripts/scripts.ariac`（マジックナンバー検証）
       - data: `assets/fonts/NotoSansJP-Regular.ttf`（先頭バイト `0x00 0x01 0x00 0x00` = TrueType）
       - data: `assets/bg/forest.png`（PNG マジック `0x89 0x50 0x4E 0x47`）
       - voice: `assets/se/click.ogg`（Ogg マジック `OggS`）
    4. すべてのアセットが正しく読み込めることを確認
  - 実際の `assets/` ディレクトリ内のファイルを使用。存在しないファイルはスキップ。

  **Must NOT do**:
  - 実際の `assets/` ディレクトリを変更しない
  - Raylib を使った描画テストは行わない（スコープ外）

  **Recommended Agent Profile**:
  - **Category**: `deep`
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: NO（Task 6, 7 の後に実行）
  - **Parallel Group**: Sequential
  - **Blocks**: F1-F4
  - **Blocked By**: Task 5, 6, 7

  **References**:
  - `src/AriaEngine/AssetIO/PakAssetProviderV3.cs` — 読み込みロジック
  - `src/AriaEngine/Packaging/PakArchiveV3.cs` — フォーマット定義

  **Acceptance Criteria**:
  - [ ] すべての代表アセットが `Exists` → true
  - [ ] すべての代表アセットが `OpenRead` で正しい内容を返す
  - [ ] PNG/TTF/OGG のマジックナンバーが一致

  **QA Scenarios**:
  ```
  Scenario: v3 split pak から全アセット読み込み成功
    Tool: Bash (dotnet test)
    Preconditions: Task 1-7 の修正が適用済み、実際の assets/ ディレクトリが存在
    Steps:
      1. `dotnet test --filter "FullyQualifiedName~E2E" -v n`
    Expected Result: 全テスト PASS
    Failure Indicators: いずれかのアセットで FileNotFoundException またはマジックナンバー不一致
    Evidence: .sisyphus/evidence/task-8-e2e-asset-loading.txt
  ```

  **Commit**: YES（単独）
  - Message: `test(e2e): add v3 split pak end-to-end asset loading verification`
  - Files: `src/AriaEngine.Tests/PakV3EndToEndTests.cs`（新規ファイル）

---

## Final Verification Wave (MANDATORY — after ALL implementation tasks)

> 4 review agents run in PARALLEL. ALL must APPROVE. Present consolidated results to user and get explicit "okay" before completing.

- [x] F1. **Plan Compliance Audit** — `oracle`
  Output: `Must Have [3/3] | Must NOT Have [5/5] | Tasks [8/8] | VERDICT: APPROVE`
  Read the plan end-to-end. For each "Must Have": verify implementation exists (read file, curl endpoint, run command). For each "Must NOT Have": search codebase for forbidden patterns — reject with file:line if found. Check evidence files exist in .sisyphus/evidence/. Compare deliverables against plan.
  Output: `Must Have [N/N] | Must NOT Have [N/N] | Tasks [N/N] | VERDICT: APPROVE/REJECT`

- [x] F2. **Code Quality Review** — `unspecified-high`
  Output: `Build [PASS] | Lint [PASS] | Tests [263 pass/2 fail pre-existing] | Files [8 clean/0 issues] | VERDICT: APPROVE`
  Run `tsc --noEmit` + linter + `dotnet test`. Review all changed files for: `as any`/`@ts-ignore`, empty catches, `console.log` in prod, commented-out code, unused imports. Check AI slop: excessive comments, over-abstraction, generic names.
  Output: `Build [PASS/FAIL] | Lint [PASS/FAIL] | Tests [N pass/N fail] | Files [N clean/N issues] | VERDICT`

- [x] F3. **Real Manual QA** — `unspecified-high`
  Output: `Scenarios [3/3 pass] | Integration [1/1] | Edge Cases [2 tested] | VERDICT: APPROVE`
  - Verified data.arid PathStrings all have `assets/` prefix
  - Verified scenario.aris PathStrings have NO `assets/` prefix
  - Verified voice.ariv PathString has `assets/` prefix
  - Verified PakAssetProviderV3 can load all categories
  - E2E test passes
  Start from clean state. Execute EVERY QA scenario from EVERY task — follow exact steps, capture evidence. Test cross-task integration (features working together, not isolation). Test edge cases: empty state, invalid input, rapid actions. Save to `.sisyphus/evidence/final-qa/`.
  Output: `Scenarios [N/N pass] | Integration [N/N] | Edge Cases [N tested] | VERDICT`

- [x] F4. **Scope Fidelity Check** — `deep`
  Output: `Tasks [8/8 compliant] | Contamination [CLEAN] | Unaccounted [CLEAN] | VERDICT: APPROVE`
  - All 8 tasks implemented as specified
  - VirtualMachine.cs/Program.cs/AriaFlowCheckCommand.cs modified only for IAssetProvider injection (required by Task 5)
  - CommandTests.cs modified only for constructor signature fix (required by Task 5)
  - No v2 Provider changes, no compression changes, no new CLI args
  For each task: read "What to do", read actual diff (git log/diff). Verify 1:1 — everything in spec was built (no missing), nothing beyond spec was built (no creep). Check "Must NOT do" compliance. Detect cross-task contamination.
  Output: `Tasks [N/N compliant] | Contamination [CLEAN/N issues] | Unaccounted [CLEAN/N files] | VERDICT`

---

## Commit Strategy

- **1**: `fix(pack): prepend assets/ prefix to v3 split PathStrings for data/stream/voice` — `src/AriaEngine/Tools/AriaPackCommand.cs`
- **2**: `fix(assetio): prevent double assets/ prefix in PakAssetProviderV3.TryFindEntry` — `src/AriaEngine/AssetIO/PakAssetProviderV3.cs`
- **3**: `refactor(assetio): eliminate redundant entry lookup loop in PakAssetProviderV3` — `src/AriaEngine/AssetIO/PakAssetProviderV3.cs`
- **4**: `fix(render): use IAssetProvider.Exists in BackgroundAssetExists for pak support` — `src/AriaEngine/Core/Commands/RenderCommandHandler.cs`
- **5**: `test(assetio): add PathString prefix resolution tests for PakAssetProviderV3` — `src/AriaEngine.Tests/PakAssetProviderV3Tests.cs`
- **6**: `test(pack): verify v3 split PathStrings include assets/ prefix for data categories` — `src/AriaEngine.Tests/PackTests.cs`
- **7**: `test(e2e): add v3 split pak end-to-end asset loading verification` — `src/AriaEngine.Tests/PakV3EndToEndTests.cs`

---

## Success Criteria

### Verification Commands
```bash
# Build
dotnet build
# Expected: 0 errors, 0 warnings

# Unit tests
dotnet test --filter "FullyQualifiedName~PackTests"
dotnet test --filter "FullyQualifiedName~PakAssetProviderV3Tests"
# Expected: all PASS (except known pre-existing failures)

# E2E test
dotnet test --filter "FullyQualifiedName~PakV3EndToEndTests"
# Expected: all PASS
```

### Final Checklist
- [ ] `data.arid` の PathString に `assets/` prefix が含まれる
- [ ] `boot.arib` / `scenario.aris` の PathString に `assets/` prefix が含まれない
- [ ] `PakAssetProviderV3` が `assets/...` パスを正しく解決する
- [ ] `BackgroundAssetExists` が Pak 内アセットを認識する
- [ ] `dotnet test` で全テスト PASS（既存の pre-existing failure を除く）
- [ ] `dotnet build` で 0 警告、0 エラー
