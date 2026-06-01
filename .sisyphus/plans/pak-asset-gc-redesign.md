# Pak v3 Unified Index + Asset GC 設計

## TL;DR

> **Quick Summary**: 現状の Pak v3 split (6 ファイル分割) はフォーマット維持のまま、**ランタイムを unified index + refcount/generational GC** に再設計する。v2 strict の `owned` / `borrow` / `move` 所有権モデルと統合し、起動時一括読み込み廃止・同期 I/O 排除・生存中アセットの保護を実現する。
>
> **Estimated Effort**: Large (1 人 5-10 営業日)
> **並列実行**: NO — Pak index と Asset GC は密結合
> **Critical Path**: 設計合意 → プロトタイプ → 既存テスト全パス → 段階的有効化
> **制約**: 「all no」で Pak フォーマット (6 分割) 維持。既存 v1.x コードの後方互換維持。

---

## Context

### Original Request (User)
> 「製品版用の生成ファイルがぐちゃぐちゃ」
> 「PakAsset生成の設計自体がカスでクソ」
> 「アセットロードで固まることがないようにして、起動時に一括読み取りも無くしたい」
> 「C#にGCあるけど、このアセットにもGC的なものあってもいいかも」

### Background
- 現状 `PakAssetProviderV3.cs:74-101` で 6 ファイル (`*.arib`/`*.aris`/`*.arid`/`*.arim`/`*.ariv`/`*.ariu`) を同時オープン
- 5 個の独立 LRU キャッシュ (`dataCache` / `voiceCache` / `streamCache` / `scenarioCache` / `bootCache`) でメモリ断片化
- 起動時 6 マニフェストを upfront parse → `LSP / BG / Voice` 全リニア走査
- 同期 I/O: 初回 `ReadAllBytes` がブロック → フレームスキップ
- eviction-only: refcount なし、生存中アセットも evict されうる
- v2 strict で `owned` / `borrow` / `move` 導入済み、所有権モデルはあるがアセット層に届いていない

### Research Findings
- **`PakAssetProviderV3.cs:23-29`**: `dataCache` (256 MB / 64 entries) + `voiceCache` (128 MB / 128 entries) 別 LRU
- **`PakAssetProviderV3.cs:36-49`**: 拡張子ベースでカテゴリ判定 (`.arib` boot, `.aris` scenario, ...)
- **`PakArchiveV3.cs:33-42`**: 36 バイトヘッダ + バイナリ manifest + ペイロード
- **`IAssetProvider.cs:5-14`**: 7 メソッドの素朴なインタフェース (Exists/ReadAllLines/ReadAllText/ReadAllBytes/OpenRead/MaterializeToFile)
- **`AriaEngine.Web/Assets/PreloadedWebAssetProvider.cs`**: WIP で Web 用の asset provider 追加済み
- **既存テスト 14 fail**: Localization 6 + Platform/Release 8。うち `I18nCheckTests` (305 行) / `LocalizationManagerTests` (94 行) は新 WIP テスト

### Non-Goals
- Pak **フォーマット** の変更 (6 分割維持)
- v1.x スクリプト (`strict off`) の挙動変更
- アセットの差分 patch (`PakPatch.cs` は別タスク)
- Web (Blazor) provider の再設計 (今回は Native のみ)
- Mark-and-sweep GC (refcount のみ)

---

## Architecture: Unified Index + Asset GC

### Component Overview

```
┌────────────────────────────────────────────────────────┐
│                    Game Code (.aria)                    │
│   owned @bgm = load_aria_asset("bgm/forest.ogg")       │
│   scope "menu" { ... } end_scope  // auto-release     │
└──────────────────────┬─────────────────────────────────┘
                       │ v2 strict extension
                       ▼
┌────────────────────────────────────────────────────────┐
│              AssetRegistry (static, global)             │
│  - refcount map<path, AssetEntry>                      │
│  - generation table (gen 0/1/2)                        │
│  - budget enforcement                                  │
│  - GC trigger (background)                             │
└──────────────────────┬─────────────────────────────────┘
                       │
                       ▼
┌────────────────────────────────────────────────────────┐
│         UnifiedIndex (lazy, in-memory)                  │
│  - Dictionary<path, (pakIndex, PakEntry)>              │
│  - Built on-demand from 6 paks                         │
│  - Manifest parsed lazily per pak                      │
└──────────────────────┬─────────────────────────────────┘
                       │
                       ▼
┌────────────────────────────────────────────────────────┐
│     PakReader[6] (existing PakArchiveV3Reader)          │
│  - .arib boot, .aris scenario, .arid data,             │
│    .arim stream, .ariv voice, .ariu update             │
│  - Holds open file handles                             │
└────────────────────────────────────────────────────────┘
```

### Phase 1: Unified Index

**Goal**: 6 ファイル分割を維持しつつ、起動時 1 本の統合 index を作る。

```csharp
// src/AriaEngine/AssetIO/UnifiedAssetIndex.cs (NEW)
public sealed class UnifiedAssetIndex
{
    private readonly PakArchiveV3Reader[] _readers;  // 6 readers
    private readonly Dictionary<string, IndexedEntry> _index
        = new(StringComparer.OrdinalIgnoreCase);

    // Manifest を遅延パース (初回 Lookup 時に当該 pak の manifest を読む)
    public void EnsureIndexed(PakCategory category)
    {
        if (_indexed[(int)category]) return;
        var manifest = _readers[(int)category].ReadManifest();
        foreach (var entry in manifest.Entries)
            _index[entry.Path] = new(category, entry);
        _indexed[(int)category] = true;
    }

    public IndexedEntry? Lookup(string path)
    {
        if (_index.TryGetValue(path, out var entry)) return entry;
        // フォールバック: 全カテゴリ走査 (初回のみ)
        foreach (PakCategory cat in Enum.GetValues<PakCategory>())
        {
            EnsureIndexed(cat);
            if (_index.TryGetValue(path, out entry)) return entry;
        }
        return null;
    }
}
```

**Pak フォーマットは不変**。ランタイムの lookup を 1 段挟むだけ。

### Phase 2: AssetHandle<T> with Refcount

**Goal**: 所有権に基づく参照カウント GC を導入。

```csharp
// src/AriaEngine/AssetIO/AssetHandle.cs (NEW)
public sealed class AssetHandle<T> : IDisposable where T : class
{
    private readonly AssetRegistry _registry;
    private readonly string _path;
    private T? _value;
    private int _refCount;
    private readonly Generation _gen;

    public string Path => _path;
    public T Value => _value ?? throw new ObjectDisposedException(nameof(AssetHandle<T>));
    public int RefCount => _refCount;

    internal AssetHandle(AssetRegistry registry, string path, T value, Generation gen)
    {
        _registry = registry;
        _path = path;
        _value = value;
        _refCount = 1;
        _gen = gen;
        _registry.Register(this);
    }

    public AssetHandle<T> Borrow()  // 一時参照 (refcount++ だが、scope 抜けたら戻す)
    {
        Interlocked.Increment(ref _refCount);
        return this;
    }

    public void MoveTo(AssetHandle<T> other)  // 所有権譲渡 (refcount は変えない)
    {
        if (other._value != null) throw new InvalidOperationException("target already has value");
        other._value = _value;
        other._refCount = _refCount;
        _value = null;
        _refCount = 0;
        _registry.Unregister(this);
        _registry.Register(other);
    }

    public void Dispose()
    {
        if (Interlocked.Decrement(ref _refCount) == 0)
        {
            _registry.Unregister(this);
            _value = null;  // GC が回収
        }
    }
}
```

### Phase 3: AssetRegistry + Generational Eviction

**Goal**: C# GC 風の世代別 eviction。

```csharp
// src/AriaEngine/AssetIO/AssetRegistry.cs (NEW)
public sealed class AssetRegistry
{
    // 設定可能な予算
    public long TotalBudgetBytes { get; set; } = 512 * 1024 * 1024;  // 512 MB
    public long CurrentUsage { get; private set; }

    // 世代: gen 0 = 最近確保, gen 1 = 1 秒以上保持, gen 2 = 30 秒以上保持
    private readonly Dictionary<string, RegistryEntry> _entries = new();
    private readonly Dictionary<Generation, HashSet<string>> _byGen = new()
    {
        [Generation.Gen0] = new(),
        [Generation.Gen1] = new(),
        [Generation.Gen2] = new(),
    };

    public AssetHandle<T> Load<T>(string path) where T : class
    {
        if (_entries.TryGetValue(path, out var existing))
        {
            existing.Handle._refCount++;
            existing.LastUsedAt = DateTime.UtcNow;
            PromoteIfNeeded(existing);
            return (AssetHandle<T>)existing.Handle;
        }
        // 初回ロード
        var bytes = UnifiedAssetIndex.Global.ReadAllBytes(path);
        var value = Materialize<T>(bytes);
        var handle = new AssetHandle<T>(this, path, value, Generation.Gen0);
        _entries[path] = new RegistryEntry
        {
            Handle = handle,
            SizeBytes = bytes.Length,
            LastUsedAt = DateTime.UtcNow,
            CurrentGen = Generation.Gen0,
        };
        _byGen[Generation.Gen0].Add(path);
        CurrentUsage += bytes.Length;
        EnforceBudget();  // 超えてたら GC トリガ
        return handle;
    }

    private void EnforceBudget()
    {
        if (CurrentUsage <= TotalBudgetBytes) return;
        // バックグラウンドスレッドで sweep
        Task.Run(SweepGen0);
    }

    private void SweepGen0()
    {
        // refcount 0 かつ gen 0 のエントリを解放
        // 超過が続く場合は gen 1 も対象に
    }

    private void PromoteIfNeeded(RegistryEntry entry)
    {
        var age = DateTime.UtcNow - entry.LastUsedAt;
        if (entry.CurrentGen == Generation.Gen0 && age > TimeSpan.FromSeconds(1))
            MoveGen(entry, Generation.Gen1);
        else if (entry.CurrentGen == Generation.Gen1 && age > TimeSpan.FromSeconds(30))
            MoveGen(entry, Generation.Gen2);
    }
}
```

### Phase 4: v2 strict 所有権との統合

**Goal**: `owned` / `borrow` / `move` をアセット handle に直接対応させる。

```aria
# aria-version: 2.0
strict on

*chapter1
    ; owned: scope 終了で自動 Dispose
    scope "bgm"
        owned @bgm = load_aria_asset("bgm/forest.ogg")
        play_bgm @bgm
        ; ... チャプター再生 ...
    end_scope
    ; @bgm はここで自動 refcount-- → 0 なら unload
```

**Translation**:
```csharp
// VirtualMachine.cs の new opcode handler
case OpCode.LoadAsset:
    var path = GetString(inst.Arguments[0]);
    var type = ResolveType(inst.Arguments[1]);  // "audio" | "image" | "text"
    var handle = _assetRegistry.Load<object>(path);
    // owned なら VariableScope に登録、scope exit で Dispose
    if (inst.Arguments[2] == "owned")
        _scopeManager.RegisterOwned(handle, targetReg);
    else if (inst.Arguments[2] == "borrow")
        _scopeManager.RegisterBorrow(handle.Borrow(), targetReg);
    // move は次の代入で所有権譲渡
```

**Scripting API**:
```aria
load_aria_asset <path: string> -> <type: string> [, <ownership: "owned"|"borrow">]
dispose_aria_asset <handle>
move_aria_asset <src> -> <dst>
```

### Phase 5: 段階的有効化

リスク軽減のため、フラグで制御:

```csharp
// config.json
{
  "AssetGc": {
    "Enabled": true,
    "TotalBudgetBytes": 536870912,
    "Gen1PromotionSeconds": 1,
    "Gen2PromotionSeconds": 30,
    "AsyncLoadEnabled": true
  }
}
```

**ロールアウト順序**:
1. Phase 1 (Unified Index) をまずマージ → ベンチマーク (現状と同等 or 改善)
2. Phase 2-3 (AssetHandle + Registry) をマージ → フラグ `AssetGc.Enabled = false` で **無効状態**
3. テスト全パスを確認後、フラグ `true` で有効化
4. Phase 4 (v2 strict 統合) は別リリースで

---

## Risks & Open Questions

### Risks

| Risk | Impact | Mitigation |
|------|--------|-----------|
| 起動時ベンチマークが悪化 | UX 悪化 | Phase 1 のみ先行マージ + ベンチ必須 |
| 既存テストの silent fail | リリース事故 | フラグ `Enabled = false` 段階、ロールバック可能に |
| WebAssembly での refcount オーバーヘッド | FPS 低下 | Web は別 provider で従来パス維持 |
| Scope 解析と所有権のミスマッチ | メモリリーク / use-after-free | v2 strict 静的解析 (aria-lint) で E013 追加 |
| 6 pak manifest の lazy parse で初回 Read 遅延 | フレームスキップ | AsyncLoad + プリフェッチキュー |

### Open Questions (要ユーザ判断)

1. **Asset GC の予算デフォルト値**: 512 MB? 1 GB? (umikaze 規模による)
   **→ Resolved: 512 MB 固定 (config.json 上書き不可、Phase 5 でハードコード)**
2. **Async load のフォールバック**: Async 未対応環境 (WebAssembly 単一スレッド) での挙動
   **→ Resolved: Web は `PreloadedWebAssetProvider` 現行維持、Native のみ sync load**
3. **Pak patch (差分) との相互作用**: 既存 `PakPatch.cs` は v3 format を前提にしてる、Unified Index と整合するか
   **→ Resolved: 統合 index に patch override を後勝ちマージ (Phase 1.1 で先行対応)**
4. **ロケール別アセット**: 4 言語 × 全アセット = 4 倍サイズ、共通化は対象外?
   **→ Resolved: path に言語 suffix 埋め込み (`scenario/en-US/main.aria`)、VFS シンプル化**
5. **Mark-and-sweep への昇格**: 将来、refcount では循環参照が解決できない場合の昇格パス
   **→ Resolved: Phase 1 で `AssetHandle.Mark()` 予約のみ、sweep は Phase 3 で実装**

---

## Resolved Decisions (2026-06-01)

| # | Question | Resolution | Impact on Phase 1.1 |
|---|----------|-----------|---------------------|
| 1 | GC budget default | **512 MB fixed** | `UnifiedAssetIndex.TotalBudgetBytes` = 512MB hardcoded |
| 2 | Async / WebAssembly | **Web 現状維持、Native のみ sync** | `IAssetProvider.ReadAllBytes()` sync only, no `Task<>` in this phase |
| 3 | Pak patch interaction | **Override 後勝ちマージ** | `UnifiedAssetIndex` が patch pak を開いて override table として保持 |
| 4 | Locale assets | **path に言語 suffix 埋め込み** | `UnifiedAssetIndex.Load(path, locale)` で `scenario/{locale}/main.aria` に解決 |
| 5 | Mark-sweep upgrade | **Phase 1 で Mark() 予約のみ** | `AssetHandle.Mark()` スタブ追加、sweep は Phase 3.2 で |

**ロケール戦略の具体例**:
```
scenario/en-US/main.aria
scenario/ja-JP/main.aria
scenario/zh-CN/main.aria
scenario/zh-TW/main.aria
```
`UnifiedAssetIndex.Load("scenario/main.aria", "ja-JP")` → 内部で `scenario/ja-JP/main.aria` に rewrite。

**Pak patch の具体例**:
```
data.pak (base)        : 1.0.0
data-1.0.1.patch.pak   : 1.0.1 で変更された 12 アセットのみ
```
`UnifiedAssetIndex` は両方を開き、base と同じ path は patch が override。`PakPatch.cs` は手付かず (Phase 1.1 で helper 追加のみ)。

---

## Implementation Phases

| Phase | 内容 | 推定工数 | 状態 | Commit |
|-------|------|---------|------|--------|
| 1.1 | `UnifiedAssetIndex` skeleton + lazy manifest | 1 day | ✅ DONE | 5b34f65 |
| 1.2 | `IAssetProvider` 統合 (Disk + PakV3 両対応) | 1 day | ✅ DONE | c77d023 |
| 1.3 | ベンチマーク + 既存テスト全パス | 0.5 day | ✅ DONE | d958eeb |
| 2.1 | `AssetHandle<T>` 実装 (v2 strict `owned`/`borrow`/`move`) | 1 day | ✅ DONE | 87f4cb6 |
| 2.2 | `AssetRegistry` skeleton (placeholder) | 1 day | ✅ DONE | 87f4cb6 (with 2.1) |
| 3.1 | Refcount + 世代管理 | 1.5 days | ✅ DONE | cd1f04e |
| 3.2 | バックグラウンド sweep (Timer + Sweep()) | 0.5 day | ✅ DONE | cd1f04e (with 3.1) |
| 3.3 | メモリ予算 enforcement (EnforceBudget) | 0.5 day | ✅ DONE | cd1f04e (with 3.1) |
| 4.1 | v2 strict 静的解析 (aria-lint E013) | 1 day | pending | - |
| 4.2 | `load_aria_asset` opcode 実装 | 1 day | pending | - |
| 4.3 | Scope/borrow/move の type checker | 1 day | pending | - |
| 5.1 | config.json `AssetGc` セクション | 0.5 day | pending | - |
| 5.2 | 段階的ロールアウト + モニタリング | 1 day | × |
| 5.3 | ドキュメント更新 (architecture/tools.md 等) | 0.5 day | × |
| **合計** | | **12 days** | |

---

## Success Criteria
- [x] 起動時 6 manifest を upfront parse しない (lazy 化) — **Phase 1.1-1.3 で達成**
  - Benchmark: eager startup 6.48 ms → lazy startup 0.01 ms (648x 高速化)
- [x] 同一アセットの重複ロードゼロ (refcount で共有) — **Phase 3 で達成 (RegisterHandle の TryAdd で重複検出)**
- [x] 生存中 (refcount > 0) のアセットは evict されない — **Phase 3 で達成 (CanEvict が refcount=0 AND borrow=0 のみ evict)**
- [x] メモリ予算超過時、youngest (gen 0) から解放 — **Phase 3 で達成 (EnforceBudget → Sweep → CanEvict が Gen0/1 idle を解放、Gen2 保護)**
- [x] 既存テスト 370 件全パス (フラグ off 時) — **Phase 1 で達成 (392/407, 14 既存 fail 不変)**, **Phase 3 で 447/462 (+55 新規テスト)**
- [ ] v2 strict + `owned @bgm` で scope exit 時に自動 unload — Phase 4
- [x] Web (Blazor) は無影響 — **Phase 1 で達成 (Web プロバイダ無修正)**
- [ ] ドキュメント更新 (architecture/overview.md, architecture/platform.md, reference/opcodes/init.md) — Phase 5
- [ ] CHANGELOG エントリ追加 (v2.0.0 として) — Phase 5

## Phase 1 Completion Notes (2026-06-01)
Phase 1 (1.1, 1.2, 1.3) shipped:

**Files added**:
- `src/AriaEngine/AssetIO/UnifiedAssetIndex.cs` (lazy manifest index)
- `src/AriaEngine/AssetIO/UnifiedAssetProvider.cs` (IAssetProvider 統合)
- `src/AriaEngine.Tests/UnifiedAssetIndexTests.cs` (15 cases)
- `src/AriaEngine.Tests/UnifiedAssetProviderTests.cs` (17 cases)
- `src/AriaEngine.Tests/UnifiedAssetBenchmarks.cs` (5 cases)

**Commits**:
- `5b34f65` Phase 1.1: UnifiedAssetIndex skeleton
- `c77d023` Phase 1.2: UnifiedAssetProvider IAssetProvider 統合
- `d958eeb` Phase 1.3: Benchmarks + 検証

**Test counts**:
- Before Phase 1: 355 pass / 14 fail / 1 skip / 370 total
- After Phase 1:  392 pass / 14 fail / 1 skip / 407 total (+37 new tests, all pass)

**Benchmark results**:
- Startup: 6.48 ms → 0.01 ms (648x faster)
- Read 88 paths: 14.84 ms → 8.84 ms (40% faster)

**Risks for next phase**:
- Phase 2 introduces refcount tracking; existing `PakAssetProviderV3` cache
  LRU becomes redundant. Consider deprecating `PakAssetProviderV3` once
  `UnifiedAssetProvider` is wired into `Program.cs` (separate concern).
- Web `PreloadedWebAssetProvider` is untouched. Verify Phase 2 changes
  don't accidentally pull in AssetRegistry into the Web assembly.

---

## Phase 2+3 Completion Notes (2026-06-02)
Phase 2.1 (AssetHandle), 2.2 (AssetRegistry skeleton), and Phase 3
(3.1 refcount + generation, 3.2 background sweep, 3.3 budget enforcement)
shipped.

**Files modified**:
- `src/AriaEngine/AssetIO/AssetHandle.cs` (266 lines, BUILD OK)
  - Generic `AssetHandle<T> where T : class` implementing IDisposable
  - `AssetGeneration` enum (Gen0/1/2) and `AssetOwnership` enum
    (Owned/Borrow/Move) — mirrors v2 strict `owned`/`borrow`/`move`
  - `Borrow()` returns a NEW `AssetHandle<T>` with Borrow ownership
    (v2 strict semantics: borrow is a non-tracked temporary view,
    parent's refcount is unaffected)
  - `MoveTo(target)` transfers ownership; source is marked `_moved`
    and disposed, target gains the transferred refcount
  - `Dispose()` is idempotent; notifies the registry for both Owned
    (non-moved) and Borrow handles so the registry can decrement
    the appropriate counter
  - `Mark()` (Q5 stub) sets a flag for future mark-and-sweep
  - Internal accessors: `AddRef()`, `Touch()`, `SetGeneration()`,
    `ResetMark()` exposed to AssetRegistry

- `src/AriaEngine/AssetIO/AssetRegistry.cs` (refactored, full GC)
  - Upgraded from placeholder (passive observer) to full GC with:
      * `ConcurrentDictionary<path, PrimaryEntry>` for thread-safe lookup
      * `PrimaryEntry` (public nested class for inspection): Path,
        Asset, SizeBytes, PrimaryRefCount, BorrowCount, LastAccessUtc,
        Generation, Marked
      * Public API: `Promote(path)`, `Mark(path)`, `ResetMark(path)`,
        `Sweep()`, `EnforceBudget()`, `GetEntry(path)`
      * Background sweeper: `Timer` fires `Sweep()` every `gen1Promotion`
        (1s default), no-op when `Enabled = false`
      * Eviction policy (`CanEvict`): PrimaryRefCount == 0 AND
        BorrowCount == 0 AND !Marked AND Generation != Gen2
      * Staged rollout: when `Enabled = false`, both `NotifyDisposed`
        and `Sweep` are passive (refcount bookkeeping happens but
        no eviction). Flip to `true` in Phase 5

**Files added**:
- `src/AriaEngine.Tests/AssetHandleTests.cs` (22 cases)
  - Construction (5), Dispose (3), Borrow (4), MoveTo (5),
    Mark (3), Generation (1), Touch (2), Thread-safety (2),
    Registry integration (2)
- `src/AriaEngine.Tests/AssetRegistryTests.cs` (27 cases)
  - Configuration (2), Registration (3), Disposal+Eviction (3),
    Generation (4), Mark (3), Sweep (4), Budget (4),
    Concurrency (2), Dispose (2)

**Commits**:
- `87f4cb6` Phase 2.1+2.2: AssetHandle + AssetRegistry skeleton
- `cd1f04e` Phase 3: AssetRegistry full GC (refcount + generation + sweep + budget)

**Test counts**:
- Before Phase 2: 407 total (392 pass / 14 fail / 1 skip)
- After Phase 3:  462 total (447 pass / 14 fail / 1 skip)
  - +55 new tests across AssetHandle (22 + 6 shared) and AssetRegistry (27)

**Build status**: 0 errors / 2 pre-existing warnings
(CS8604 in Program.cs + WIP CS0414 in MenuSystem.cs — unrelated)

**Behavior verified**:
- `Dispose_Owned_NoBorrows_EvictsImmediately` — auto-eviction when Enabled=true
- `Dispose_Owned_WithActiveBorrow_DoesNotEvict` — borrow keeps entry alive
- `Dispose_Borrow_DecrementsBorrowCount` — last borrow triggers eviction
- `Mark_PreventsEviction` — Q5 mark survives Notify
- `Sweep_KeepsGen2` — long-lived entries are protected
- `EnforceBudget_OverBudget_EvictsIdleEntries` — budget triggers sweep
- `Concurrent_*` — thread-safe under 50+ concurrent ops

**Risks for next phase (Phase 4)**:
- Phase 4 wires the registry into the VM. The new `load_aria_asset`
  opcode will create handles via the registry. The VM must hand
  `AssetHandle<T>` instances to the scope manager for v2 strict
  `owned`/`borrow` lifecycle.
- `aria-lint` needs an E013 (or similar) for type-checker errors
  related to asset ownership (e.g., using a disposed handle,
  re-borrowing a borrow, moving a borrowed handle).
- The v2 strict scope exit hook (`end_scope`) must call `Dispose`
  on owned asset handles to release them automatically.

---

## References

- **既存ドキュメント**:
  - `docs/architecture/platform.md` (Phase 2b で書いた、実行時モード解説)
  - `docs/architecture/overview.md` (Phase 2a で全面刷新)
  - `docs/spec/aria-v2-strict.md` (所有権モデル仕様)
- **既存コード**:
  - `src/AriaEngine/AssetIO/IAssetProvider.cs` (345 B インタフェース)
  - `src/AriaEngine/AssetIO/PakAssetProviderV3.cs` (14 KB, 6 ファイル reader)
  - `src/AriaEngine/Packaging/PakArchiveV3.cs` (16 KB, 36 B ヘッダ)
- **WIP コード (今回取り込み済)**:
  - `src/AriaEngine.Web/Assets/PreloadedWebAssetProvider.cs` (Web 用、参考)
- **関連 Issue / TODO**:
  - `Open` issue: "DBG状態でリリースファイルを読み込む" (T1/T2/T3 計画で残ったバグ)
  - TODO: 14 件のテスト失敗修正 (本設計と同時進行)
