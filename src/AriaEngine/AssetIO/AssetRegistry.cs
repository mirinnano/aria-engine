using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using AriaEngine.Packaging;

namespace AriaEngine.Assets;

/// <summary>
/// AssetRegistry (Pak v3 redesign, Phase 3.1+3.2+3.3).
///
/// Tracks loaded asset handles with refcount, generational eviction,
/// mark-and-sweep (Q5 stub), and memory budget enforcement. Mirrors
/// C# GC patterns:
///   - <b>Refcount</b>: handles increment/decrement via Register/NotifyDisposed.
///   - <b>Generations</b>: Gen0 (new) → Gen1 (1s+) → Gen2 (30s+), based
///     on last-access time. Promoted by the background sweeper or via
///     manual <see cref="Promote"/>.
///   - <b>Mark</b>: Q5 stub. Marked entries survive eviction (both auto
///     via Notify and sweep). Used for "keep alive" hints in Phase 3+.
///   - <b>Budget</b>: 512 MB default, sweep evicts idle Gen0/Gen1
///     (Gen2 is protected). <see cref="EnforceBudget"/> is called after
///     every Register and is also publicly callable.
///   - <b>Background sweeper</b>: a <see cref="Timer"/> fires
///     <see cref="Sweep"/> every <c>gen1Promotion</c> (1s default).
///   - <b>Staged rollout</b>: <see cref="Enabled"/> = false (default) makes
///     the registry a passive observer; no eviction occurs. Flip to true
///     in Phase 5 once the wiring is verified.
///
/// Thread-safety: <see cref="ConcurrentDictionary{TKey, TValue}"/> for the
/// primary map, <see cref="Interlocked"/> for refcount counters,
/// <see cref="Volatile"/> for the <see cref="Enabled"/> flag and single-word
/// entry fields. <see cref="Sweep"/> iterates a snapshot (<c>Keys.ToArray()</c>)
/// to avoid concurrent modification during iteration.
/// </summary>
public sealed class AssetRegistry : IDisposable
{
    private readonly UnifiedAssetIndex? _index;
    private readonly UnifiedAssetProvider? _provider;
    private readonly long _totalBudgetBytes;
    private readonly TimeSpan _gen1Promotion;
    private readonly TimeSpan _gen2Promotion;
    private readonly ConcurrentDictionary<string, PrimaryEntry> _primaryByPath;
    private long _currentBytes;
    private int _disposed;
    private volatile bool _enabled;
    private readonly Timer? _sweeper;

    public AssetRegistry(
        UnifiedAssetProvider provider,
        long totalBudgetBytes = 512L * 1024 * 1024,
        TimeSpan? gen1Promotion = null,
        TimeSpan? gen2Promotion = null)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _index = provider.Index;
        _totalBudgetBytes = totalBudgetBytes;
        _gen1Promotion = gen1Promotion ?? TimeSpan.FromSeconds(1);
        _gen2Promotion = gen2Promotion ?? TimeSpan.FromSeconds(30);
        _primaryByPath = new ConcurrentDictionary<string, PrimaryEntry>(StringComparer.OrdinalIgnoreCase);
        _enabled = false;
        // Background sweeper fires every gen1Promotion. It is a no-op when
        // Enabled = false.
        _sweeper = new Timer(_ => Sweep(), null, _gen1Promotion, _gen1Promotion);
    }

    // ──────────────────────────────────────────────────────────────────────
    // Public properties
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>Enable or disable GC. Off by default for staged rollout (Phase 5).</summary>
    public bool Enabled
    {
        get => _enabled;
        set => _enabled = value;
    }

    /// <summary>Total memory budget in bytes (Q1: 512 MB default).</summary>
    public long TotalBudgetBytes => _totalBudgetBytes;

    /// <summary>Current tracked asset byte count.</summary>
    public long CurrentBytes => Interlocked.Read(ref _currentBytes);

    /// <summary>Number of distinct asset paths currently tracked.</summary>
    public int TrackedCount => _primaryByPath.Count;

    /// <summary>Generation 1 promotion threshold (default 1 second).</summary>
    public TimeSpan Gen1Promotion => _gen1Promotion;

    /// <summary>Generation 2 promotion threshold (default 30 seconds).</summary>
    public TimeSpan Gen2Promotion => _gen2Promotion;

    // ──────────────────────────────────────────────────────────────────────
    // Tracking API (called by AssetHandle)
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Register a handle with the registry. Owned handles become the
    /// primary entry; Borrow handles increment the borrow count for the
    /// existing primary. If the budget is exceeded (when Enabled), a sweep
    /// is triggered immediately.
    /// </summary>
    internal void RegisterHandle<T>(AssetHandle<T> handle) where T : class
    {
        if (handle == null) return;
        if (Volatile.Read(ref _disposed) == 1) return;

        if (handle.Ownership == AssetOwnership.Borrow)
        {
            if (_primaryByPath.TryGetValue(handle.Path, out var entry))
            {
                Interlocked.Increment(ref entry.BorrowCount);
                // DateTime is a value type; plain assignment is safe because
                // the surrounding ConcurrentDictionary operations (TryGetValue)
                // establish the happens-before barrier for this thread.
                entry.LastAccessUtc = DateTime.UtcNow;
            }
            return;
        }

        // Owned: register as primary
        var newEntry = new PrimaryEntry
        {
            Path = handle.Path,
            Asset = handle.Asset,
            SizeBytes = handle.SizeBytes,
            PrimaryRefCount = 1,
            BorrowCount = 0,
            LastAccessUtc = DateTime.UtcNow,
            Generation = AssetGeneration.Gen0,
            Marked = false,
        };

        if (_primaryByPath.TryAdd(handle.Path, newEntry))
        {
            Interlocked.Add(ref _currentBytes, handle.SizeBytes);
            EnforceBudget();
        }
    }

    /// <summary>
    /// Internal callback invoked by <c>AssetHandle&lt;T&gt;.Dispose()</c>.
    /// Decrements the refcount and (if Enabled) tries to evict the entry
    /// if it's idle (refcount == 0 AND borrow count == 0 AND not marked
    /// AND not Gen2). When Enabled = false, this is a passive observer
    /// — refcount bookkeeping happens but no eviction occurs (entries
    /// remain in the map until Sweep is called manually or via Timer).
    /// </summary>
    internal void NotifyDisposed<T>(AssetHandle<T> handle) where T : class
    {
        if (handle == null) return;
        if (Volatile.Read(ref _disposed) == 1) return;
        if (!_primaryByPath.TryGetValue(handle.Path, out var entry)) return;

        if (handle.Ownership == AssetOwnership.Borrow)
        {
            Interlocked.Decrement(ref entry.BorrowCount);
        }
        else
        {
            Interlocked.Exchange(ref entry.PrimaryRefCount, 0);
        }

        if (_enabled) TryEvict(entry, handle.Path);
    }

    // ──────────────────────────────────────────────────────────────────────
    // Public API: generation + mark
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Promote the entry at <paramref name="path"/> by one generation
    /// (Gen0→Gen1, Gen1→Gen2; Gen2 stays). Returns true if the entry
    /// exists and was promoted; false if not found or already Gen2.
    /// </summary>
    public bool Promote(string path)
    {
        if (!_primaryByPath.TryGetValue(path, out var entry)) return false;
        return TryPromoteOneStep(entry);
    }

    /// <summary>Mark the entry at <paramref name="path"/> for the upcoming sweep cycle (Q5).</summary>
    public bool Mark(string path)
    {
        if (!_primaryByPath.TryGetValue(path, out var entry)) return false;
        Volatile.Write(ref entry.Marked, true);
        return true;
    }

    /// <summary>Reset the mark flag for the entry at <paramref name="path"/>.</summary>
    public bool ResetMark(string path)
    {
        if (!_primaryByPath.TryGetValue(path, out var entry)) return false;
        Volatile.Write(ref entry.Marked, false);
        return true;
    }

    /// <summary>Snapshot of a primary entry. Returns null if the path is not tracked.</summary>
    public PrimaryEntry? GetEntry(string path)
    {
        return _primaryByPath.TryGetValue(path, out var entry) ? entry.Clone() : null;
    }

    // ──────────────────────────────────────────────────────────────────────
    // Sweep (Phase 3.2) + budget enforcement (Phase 3.3)
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Sweep idle Gen0/Gen1 entries (eviction) and promote entries based
    /// on age. Returns the number of entries evicted. No-op when
    /// Enabled = false.
    /// </summary>
    public int Sweep()
    {
        if (!_enabled) return 0;

        int evicted = 0;
        var now = DateTime.UtcNow;

        // Snapshot the keys to avoid concurrent modification during iteration.
        foreach (var path in _primaryByPath.Keys.ToArray())
        {
            if (!_primaryByPath.TryGetValue(path, out var entry)) continue;

            // 1. Try evict
            if (CanEvict(entry))
            {
                if (_primaryByPath.TryRemove(path, out _))
                {
                    Interlocked.Add(ref _currentBytes, -entry.SizeBytes);
                    Volatile.Write(ref entry.Asset, null);
                    evicted++;
                    continue;
                }
            }

            // 2. Try promote based on age
            TryPromoteByAge(entry, now);
        }

        return evicted;
    }

    /// <summary>
    /// Manually trigger a budget enforcement sweep. Returns the number of
    /// entries evicted. No-op when Enabled = false or when under budget.
    /// </summary>
    public int EnforceBudget()
    {
        if (!_enabled) return 0;
        if (Interlocked.Read(ref _currentBytes) <= _totalBudgetBytes) return 0;
        return Sweep();
    }

    // ──────────────────────────────────────────────────────────────────────
    // Internals
    // ──────────────────────────────────────────────────────────────────────

    private void TryEvict(PrimaryEntry entry, string path)
    {
        if (!CanEvict(entry)) return;

        if (_primaryByPath.TryRemove(path, out _))
        {
            Interlocked.Add(ref _currentBytes, -entry.SizeBytes);
            Volatile.Write(ref entry.Asset, null);
        }
    }

    private static bool CanEvict(PrimaryEntry entry)
    {
        return Volatile.Read(ref entry.PrimaryRefCount) == 0
            && Volatile.Read(ref entry.BorrowCount) == 0
            && !Volatile.Read(ref entry.Marked)
            && entry.Generation != AssetGeneration.Gen2;
    }

    private static bool TryPromoteOneStep(PrimaryEntry entry)
    {
        var current = entry.Generation;
        if (current == AssetGeneration.Gen2) return false;
        entry.Generation = current == AssetGeneration.Gen0
            ? AssetGeneration.Gen1
            : AssetGeneration.Gen2;
        return true;
    }

    private bool TryPromoteByAge(PrimaryEntry entry, DateTime now)
    {
        var current = entry.Generation;
        if (current == AssetGeneration.Gen2) return false;

        // LastAccessUtc is a value type (DateTime); plain read is safe here
        // because the ConcurrentDictionary operations (TryAdd/TryGetValue)
        // establish the happens-before barrier between RegisterHandle and
        // Sweep.
        var age = now - entry.LastAccessUtc;
        if (current == AssetGeneration.Gen0 && age >= _gen1Promotion)
        {
            entry.Generation = AssetGeneration.Gen1;
            return true;
        }
        if (current == AssetGeneration.Gen1 && age >= _gen2Promotion)
        {
            entry.Generation = AssetGeneration.Gen2;
            return true;
        }
        return false;
    }

    // ──────────────────────────────────────────────────────────────────────
    // IDisposable
    // ──────────────────────────────────────────────────────────────────────

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1) return;
        _sweeper?.Dispose();
        _primaryByPath.Clear();
        Interlocked.Exchange(ref _currentBytes, 0);
    }

    // ──────────────────────────────────────────────────────────────────────
    // PrimaryEntry (public for inspection)
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Snapshot of a primary entry. Public for test inspection via
    /// <see cref="GetEntry"/>. Mutable fields use <see cref="Volatile"/>
    /// reads internally but the snapshot itself is a plain copy.
    /// </summary>
    public sealed class PrimaryEntry
    {
        public string Path = "";
        public object? Asset;
        public int PrimaryRefCount;
        public int BorrowCount;
        public long SizeBytes;
        public DateTime LastAccessUtc;
        public AssetGeneration Generation;
        public bool Marked;

        internal PrimaryEntry Clone() => new()
        {
            Path = Path,
            Asset = Asset,
            PrimaryRefCount = PrimaryRefCount,
            BorrowCount = BorrowCount,
            SizeBytes = SizeBytes,
            LastAccessUtc = LastAccessUtc,
            Generation = Generation,
            Marked = Marked,
        };
    }
}
