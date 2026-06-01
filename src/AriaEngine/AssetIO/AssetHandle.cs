using System;
using System.Threading;

namespace AriaEngine.Assets;

/// <summary>
/// Asset generation for the GC tier (Phase 3 promotion logic).
/// Gen0: newly loaded (will be evicted first if memory pressure).
/// Gen1: survived &gt;= Gen1PromotionSeconds since last access.
/// Gen2: survived &gt;= Gen2PromotionSeconds — treated as long-lived.
/// </summary>
public enum AssetGeneration
{
    Gen0 = 0,
    Gen1 = 1,
    Gen2 = 2,
}

/// <summary>
/// Ownership semantics for an asset handle (mirrors v2 strict `owned`/`borrow`/`move`).
///   Owned  — handle is responsible for decrementing refcount on Dispose.
///   Borrow — temporary reference; Dispose decrements refcount but the asset's
///            "true" lifetime is tied to the original owned handle.
///   Move   — ownership transferred; original handle becomes invalid after Move.
/// </summary>
public enum AssetOwnership
{
    Owned = 0,
    Borrow = 1,
    Move = 2,
}

/// <summary>
/// Reference-counted handle to a loaded asset. Created by <c>AssetRegistry</c>
/// (Phase 2.2) and shared across threads. Implements <see cref="IDisposable"/>.
///
/// Lifecycle:
///   1. Registry calls <c>new AssetHandle&lt;T&gt;(path, asset, ownership, sizeBytes)</c>
///   2. Caller uses <c>handle.Asset</c> freely (read-only).
///   3. Caller calls <see cref="Dispose"/> when done (in a <c>using</c> block).
///   4. When the Owned handle's <c>RefCount</c> reaches 0, the registry can release
///      the underlying bytes. Borrow handles have their own refcount lifecycle and
///      do not affect the parent's lifecycle (mirrors v2 strict `borrow` semantics).
///
/// Ownership model (mirrors v2 strict `owned`/`borrow`/`move`):
///   - <c>Owned</c>  — sole owner. Refcount starts at 1. Dispose decrements.
///                     <c>Borrow()</c> returns a separate Borrow handle.
///   - <c>Borrow</c> — temporary view. Refcount starts at 1. Dispose decrements.
///                     Calling <c>Borrow()</c> on a Borrow handle is an error
///                     (re-borrowing is forbidden; use the original Owned handle).
///   - <c>Move</c>   — ownership transferred. Source is marked moved and disposed;
///                     target gains the transferred refcount.
///
/// Q5 stub: <see cref="Mark"/> sets a flag for future mark-and-sweep (Phase 3).
/// The actual sweep logic lives in <c>AssetRegistry</c>'s background sweep, not
/// in the handle itself. This is intentional: the handle is the data plane;
/// the registry is the control plane.
///
/// Thread-safety: all mutable state uses <see cref="Interlocked"/> operations.
/// The <c>Asset</c> reference is set once at construction and never replaced
/// (immutable), so reads of <c>Asset</c> are always safe.
/// </summary>
public sealed class AssetHandle<T> : IDisposable where T : class
{
    private readonly AssetRegistry? _registry; // null in unit tests
    private readonly string _path;
    private readonly T _asset;
    private readonly AssetOwnership _ownership;
    private readonly int _sizeBytes;

    private int _refCount;
    private int _disposed;        // 0/1 flag (Interlocked)
    private int _moved;           // 0/1: source was moved out via MoveTo (suppresses registry notify)
    private int _marked;          // Q5 mark-and-sweep flag
    private long _lastAccessTicks;
    private int _generation;       // AssetGeneration as int (Interlocked-friendly)

    /// <summary>
    /// Construct an asset handle. Internal because the registry is the sole owner
    /// of the construction path in production; tests use InternalsVisibleTo to
    /// construct directly.
    /// </summary>
    internal AssetHandle(
        AssetRegistry? registry,
        string path,
        T asset,
        AssetOwnership ownership,
        int sizeBytes)
    {
        if (string.IsNullOrEmpty(path)) throw new ArgumentException("path is empty", nameof(path));
        if (asset == null) throw new ArgumentNullException(nameof(asset));
        if (sizeBytes < 0) throw new ArgumentOutOfRangeException(nameof(sizeBytes));

        _registry = registry;
        _path = path;
        _asset = asset;
        _ownership = ownership;
        _sizeBytes = sizeBytes;
        // Owned handles start with refcount=1; borrowed handles start at 0 and
        // are incremented by Borrow() before the caller observes them.
        _refCount = ownership == AssetOwnership.Owned ? 1 : 0;
        _lastAccessTicks = DateTime.UtcNow.Ticks;
        _generation = (int)AssetGeneration.Gen0;
    }

    // ──────────────────────────────────────────────────────────────────────
    // Public read-only properties
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>Logical asset path (e.g. "data/texture/bg_title.bmp").</summary>
    public string Path => _path;

    /// <summary>The deserialized asset payload. Immutable after construction.</summary>
    public T Asset => _asset;

    /// <summary>Current reference count (live references only; not including the original Owned increment if already disposed).</summary>
    public int RefCount => Volatile.Read(ref _refCount);

    /// <summary>True if at least one live reference exists (refcount &gt; 0).</summary>
    public bool IsAlive => Volatile.Read(ref _refCount) > 0;

    /// <summary>True if <see cref="Dispose"/> has been called.</summary>
    public bool IsDisposed => Volatile.Read(ref _disposed) == 1;

    /// <summary>True if <see cref="Mark"/> was called. Q5 stub: Phase 3 will use this in mark-and-sweep.</summary>
    public bool IsMarked => Volatile.Read(ref _marked) == 1;

    /// <summary>GC generation. Promoted by the registry based on access time (Phase 3).</summary>
    public AssetGeneration Generation => (AssetGeneration)Volatile.Read(ref _generation);

    /// <summary>Ownership semantics at construction.</summary>
    public AssetOwnership Ownership => _ownership;

    /// <summary>Underlying byte size of the asset (used for memory budget enforcement in Phase 3).</summary>
    public int SizeBytes => _sizeBytes;

    /// <summary>UTC time of the last access (Touch call). Updated on Borrow, MoveTo, and read.</summary>
    public DateTime LastAccessTimeUtc
    {
        get
        {
            long ticks = Interlocked.Read(ref _lastAccessTicks);
            return new DateTime(ticks, DateTimeKind.Utc);
        }
    }

    // ──────────────────────────────────────────────────────────────────────
    // Mark-and-sweep stub (Q5: Phase 1 mark, Phase 3 sweep)
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Mark this handle for the upcoming sweep cycle. Q5 stub for Phase 3.
    /// In Phase 3 the registry will run a periodic sweep that keeps marked
    /// handles alive and evicts unmarked ones regardless of refcount. For
    /// now, <c>Mark</c> just sets a flag and does not change behavior.
    /// </summary>
    public void Mark()
    {
        if (IsDisposed) return;
        Interlocked.Exchange(ref _marked, 1);
    }

    /// <summary>
    /// Reset the mark flag. Called by the registry at the start of each sweep
    /// cycle (Phase 3). For now, no-op stub so the API surface is stable.
    /// </summary>
    internal void ResetMark()
    {
        Interlocked.Exchange(ref _marked, 0);
    }

    // ──────────────────────────────────────────────────────────────────────
    // Borrow / MoveTo / Dispose
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Borrow the asset for a temporary use. Returns a NEW <see cref="AssetHandle{T}"/>
    /// with <see cref="AssetOwnership.Borrow"/> ownership. The new handle has its own
    /// refcount lifecycle (starts at 1, decremented on Dispose); the parent's refcount
    /// is NOT affected. This mirrors v2 strict `borrow` semantics, where a borrow is
    /// a temporary view that does not extend the owner's lifetime.
    ///
    /// Re-borrowing a Borrow handle is forbidden — call <c>Borrow()</c> on the original
    /// Owned handle instead.
    ///
    /// Example:
    /// <code>
    /// using (var borrow = handle.Borrow()) {
    ///     borrow.Asset.DoSomething();
    /// }
    /// </code>
    /// </summary>
    public AssetHandle<T> Borrow()
    {
        if (IsDisposed)
            throw new ObjectDisposedException(nameof(AssetHandle<T>));
        if (_ownership == AssetOwnership.Borrow)
            throw new InvalidOperationException(
                "Cannot borrow a borrowed handle. Call Borrow() on the owned handle.");
        if (_ownership == AssetOwnership.Move)
            throw new InvalidOperationException(
                "Cannot borrow a moved handle. The asset has been moved to another owner.");

        // Update parent's LastAccessTimeUtc for LRU tracking (the access is logical
        // even though the parent's refcount is unchanged).
        Touch();

        // Create a new handle with Borrow ownership. Start with refcount 1 (the new
        // borrow is already a live reference; Dispose decrements it to 0).
        var borrow = new AssetHandle<T>(
            _registry,
            _path,
            _asset,
            AssetOwnership.Borrow,
            _sizeBytes);
        borrow.AddRef();
        return borrow;
    }

    /// <summary>
    /// Transfer ownership to another handle. The source handle becomes invalid
    /// after this call (Dispose will not undo the transfer; the refcount has
    /// already moved to the target). The target's path must match the source's.
    /// </summary>
    public void MoveTo(AssetHandle<T> target)
    {
        if (target == null) throw new ArgumentNullException(nameof(target));
        if (IsDisposed)
            throw new ObjectDisposedException(nameof(AssetHandle<T>));
        if (target.IsDisposed)
            throw new ObjectDisposedException(nameof(target));
        if (target._path != _path)
            throw new InvalidOperationException(
                $"Cannot move to a different path: source='{_path}', target='{target._path}'");
        if (target._ownership != AssetOwnership.Owned)
            throw new InvalidOperationException(
                "MoveTo target must be an Owned handle");

        // Transfer: increment target refcount, then dispose source. The source is
        // marked as moved-out BEFORE Dispose so the registry notification is
        // suppressed (the target now owns the asset, not the source).
        Interlocked.Increment(ref target._refCount);
        target.Touch();
        Interlocked.Exchange(ref _moved, 1);

        Dispose();
    }

    /// <summary>
    /// Decrement the refcount. When the count reaches 0, the registry is
    /// notified that the handle can be released. Idempotent: calling Dispose
    /// twice is a no-op.
    ///
    /// Borrow handles do NOT notify the registry (they are non-tracked temporary
    /// views). Moved-out source handles do NOT notify (target now owns the asset).
    /// </summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1) return;

        int newCount = Interlocked.Decrement(ref _refCount);
        if (newCount < 0)
        {
            // Defensive: refcount should never go negative in well-behaved code.
            // Restore to 0 to prevent further decrements underflowing.
            Interlocked.Exchange(ref _refCount, 0);
        }

        // Only Owned, non-moved handles notify the registry.
        if (_ownership == AssetOwnership.Owned && Volatile.Read(ref _moved) == 0)
        {
            _registry?.NotifyDisposed(this);
        }
    }

    // ──────────────────────────────────────────────────────────────────────
    // Internal accessors for AssetRegistry (Phase 2.2)
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>Internal: increment refcount. Used by Borrow and registry.</summary>
    internal int AddRef()
    {
        if (IsDisposed)
            throw new ObjectDisposedException(nameof(AssetHandle<T>));
        int newCount = Interlocked.Increment(ref _refCount);
        Touch();
        return newCount;
    }

    /// <summary>Internal: update last-access timestamp.</summary>
    internal void Touch()
    {
        if (IsDisposed) return;
        Interlocked.Exchange(ref _lastAccessTicks, DateTime.UtcNow.Ticks);
    }

    /// <summary>Internal: promote to a new generation (used by registry sweep).</summary>
    internal void SetGeneration(AssetGeneration gen)
    {
        Volatile.Write(ref _generation, (int)gen);
    }
}
