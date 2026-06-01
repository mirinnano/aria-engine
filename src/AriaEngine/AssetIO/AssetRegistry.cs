using System;
using System.Collections.Generic;
using System.Threading;
using AriaEngine.Packaging;

namespace AriaEngine.Assets;

/// <summary>
/// AssetRegistry (Pak v3 redesign, Phase 2.2 placeholder)
///
/// Manages a map of path → AssetHandle&lt;T&gt; with refcount tracking,
/// background sweep, and memory budget enforcement. Phase 2.1 ships only
/// the surface that AssetHandle&lt;T&gt; depends on (NotifyDisposed); the
/// full refcount map, generation promotion, and background sweep are added
/// in Phase 2.2 and Phase 3.
///
/// Why a placeholder now: AssetHandle&lt;T&gt; takes a constructor parameter
/// of type <see cref="AssetRegistry"/> and calls <c>NotifyDisposed</c> on
/// Dispose. We need a non-nullable type at compile time. The placeholder
/// keeps the type bound so Phase 2.2 can extend the same class without
/// changing AssetHandle's signature.
/// </summary>
public class AssetRegistry : IDisposable
{
    private readonly UnifiedAssetIndex? _index;
    private readonly UnifiedAssetProvider? _provider;
    private readonly long _totalBudgetBytes;
    private readonly TimeSpan _gen1Promotion;
    private readonly TimeSpan _gen2Promotion;
    private readonly object _lock = new();
    private readonly Dictionary<string, ITrackedHandle> _tracked = new(StringComparer.OrdinalIgnoreCase);
    private long _currentBytes;
    private int _disposed;
    private bool _enabled;

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
        _enabled = false;
    }

    /// <summary>Enable or disable GC. Off by default for staged rollout (Phase 5).</summary>
    public bool Enabled
    {
        get { lock (_lock) return _enabled; }
        set { lock (_lock) _enabled = value; }
    }

    /// <summary>Total memory budget in bytes (Q1: 512 MB default).</summary>
    public long TotalBudgetBytes => _totalBudgetBytes;

    /// <summary>Current tracked asset byte count.</summary>
    public long CurrentBytes
    {
        get { lock (_lock) return _currentBytes; }
    }

    /// <summary>Number of distinct asset paths currently tracked.</summary>
    public int TrackedCount
    {
        get { lock (_lock) return _tracked.Count; }
    }

    /// <summary>Generation 1 promotion threshold (default 1 second).</summary>
    public TimeSpan Gen1Promotion => _gen1Promotion;

    /// <summary>Generation 2 promotion threshold (default 30 seconds).</summary>
    public TimeSpan Gen2Promotion => _gen2Promotion;

    /// <summary>
    /// Internal callback invoked by <c>AssetHandle&lt;T&gt;.Dispose()</c> when
    /// a refcount decrement happens. Phase 2.2 will update the tracked map
    /// and queue the handle for sweep if refcount reaches 0.
    /// </summary>
    internal void NotifyDisposed<T>(AssetHandle<T> handle) where T : class
    {
        if (handle == null) return;
        if (Volatile.Read(ref _disposed) == 1) return;

        // Phase 2.2 will add: if handle.RefCount == 0 && Enabled, queue for sweep.
        // For now, the registry is a passive observer; it does not act.
        lock (_lock)
        {
            if (_tracked.TryGetValue(handle.Path, out var tracked))
            {
                tracked.LastEvent = "disposed";
            }
        }
    }

    /// <summary>
    /// Phase 2.2 will replace this with a real load path that:
    ///   1. Checks the in-memory cache
    ///   2. Reads bytes via <see cref="UnifiedAssetProvider"/>
    ///   3. Deserializes to <typeparamref name="T"/>
    ///   4. Wraps in <see cref="AssetHandle{T}"/>
    ///   5. Tracks in <c>_tracked</c> for sweep
    /// </summary>
    internal void RegisterHandle<T>(AssetHandle<T> handle) where T : class
    {
        if (handle == null) return;
        lock (_lock)
        {
            _tracked[handle.Path] = new TrackedHandle<T>
            {
                Path = handle.Path,
                Handle = handle,
                SizeBytes = handle.SizeBytes,
                LastEvent = "created"
            };
            _currentBytes += handle.SizeBytes;
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1) return;
        lock (_lock)
        {
            _tracked.Clear();
            _currentBytes = 0;
        }
    }

    private interface ITrackedHandle
    {
        string Path { get; }
        int SizeBytes { get; }
        string LastEvent { get; set; }
    }

    private sealed class TrackedHandle<T> : ITrackedHandle where T : class
    {
        public string Path { get; set; } = "";
        public AssetHandle<T> Handle { get; set; } = null!;
        public int SizeBytes { get; set; }
        public string LastEvent { get; set; } = "";
    }
}
