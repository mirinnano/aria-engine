using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AriaEngine.Assets;
using AriaEngine.Packaging;
using Xunit;

namespace AriaEngine.Tests;

/// <summary>
/// Unit tests for <see cref="AssetHandle{T}"/> (Pak v3 redesign, Phase 2.1).
/// </summary>
public class AssetHandleTests
{
    // Helper: create a registry backed by an in-memory pak (for registry-aware tests).
    private static (string pakPath, AssetRegistry registry) CreateRegistryWithPak()
    {
        var manifest = new PakManifestV3
        {
            Entries = new List<PakManifestEntryV3>
            {
                new()
                {
                    PathHash = PakArchiveV3Reader.PathHash64("data/x.txt"),
                    Offset = 0,
                    Size = 5,
                    OriginalSize = 5,
                    Flags = 0
                }
            },
            PathStrings = new List<string> { "data/x.txt" }
        };
        using var ms = new MemoryStream();
        PakArchiveV3.Write(ms, manifest, new[] { System.Text.Encoding.UTF8.GetBytes("hello") }, PakArchiveV3.Category.Data);
        ms.Position = 0;

        var tempPath = Path.Combine(Path.GetTempPath(), $"handle_test_{Guid.NewGuid():N}.arid");
        using (var fs = File.Create(tempPath))
        {
            ms.CopyTo(fs);
        }
        var provider = new UnifiedAssetProvider(diskRoot: null, pakPaths: new[] { tempPath });
        var registry = new AssetRegistry(provider);
        return (tempPath, registry);
    }

    // ──────────────────────────────────────────────────────────────────────
    // Construction
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void Construction_Owned_StartsWithRefCountOne()
    {
        using var reg = new AssetRegistry(
            new UnifiedAssetProvider(diskRoot: null, pakPaths: new[] { "nonexistent.pak" }));
        var payload = new byte[] { 1, 2, 3 };
        var handle = new AssetHandle<byte[]>(reg, "data/x.bin", payload, AssetOwnership.Owned, 3);

        Assert.Equal(1, handle.RefCount);
        Assert.True(handle.IsAlive);
        Assert.Same(payload, handle.Asset);
        Assert.Equal("data/x.bin", handle.Path);
        Assert.Equal(AssetOwnership.Owned, handle.Ownership);
        Assert.Equal(3, handle.SizeBytes);
        Assert.False(handle.IsDisposed);
        Assert.False(handle.IsMarked);
        Assert.Equal(AssetGeneration.Gen0, handle.Generation);
    }

    [Fact]
    public void Construction_Borrow_StartsWithRefCountZero()
    {
        using var reg = new AssetRegistry(
            new UnifiedAssetProvider(diskRoot: null, pakPaths: new[] { "nonexistent.pak" }));
        var handle = new AssetHandle<byte[]>(reg, "data/y.bin", new byte[] { 1 }, AssetOwnership.Borrow, 1);

        Assert.Equal(0, handle.RefCount);
        Assert.False(handle.IsAlive);
    }

    [Fact]
    public void Construction_NullRegistry_Allowed_ForStandaloneTests()
    {
        var handle = new AssetHandle<byte[]>(null, "data/x.bin", new byte[] { 1 }, AssetOwnership.Owned, 1);
        Assert.Equal(1, handle.RefCount);
        handle.Dispose();
        Assert.True(handle.IsDisposed);
    }

    [Fact]
    public void Construction_EmptyPath_Throws()
    {
        using var reg = new AssetRegistry(
            new UnifiedAssetProvider(diskRoot: null, pakPaths: new[] { "nonexistent.pak" }));
        Assert.Throws<ArgumentException>(() =>
            new AssetHandle<byte[]>(reg, "", new byte[] { 1 }, AssetOwnership.Owned, 1));
    }

    [Fact]
    public void Construction_NullAsset_Throws()
    {
        using var reg = new AssetRegistry(
            new UnifiedAssetProvider(diskRoot: null, pakPaths: new[] { "nonexistent.pak" }));
        Assert.Throws<ArgumentNullException>(() =>
            new AssetHandle<byte[]>(reg, "data/x.bin", null!, AssetOwnership.Owned, 1));
    }

    [Fact]
    public void Construction_NegativeSize_Throws()
    {
        using var reg = new AssetRegistry(
            new UnifiedAssetProvider(diskRoot: null, pakPaths: new[] { "nonexistent.pak" }));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new AssetHandle<byte[]>(reg, "data/x.bin", new byte[] { 1 }, AssetOwnership.Owned, -1));
    }

    // ──────────────────────────────────────────────────────────────────────
    // Dispose
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void Dispose_DecrementsRefCount()
    {
        using var reg = new AssetRegistry(
            new UnifiedAssetProvider(diskRoot: null, pakPaths: new[] { "nonexistent.pak" }));
        var handle = new AssetHandle<byte[]>(reg, "data/x.bin", new byte[] { 1 }, AssetOwnership.Owned, 1);

        handle.Dispose();
        Assert.Equal(0, handle.RefCount);
        Assert.False(handle.IsAlive);
        Assert.True(handle.IsDisposed);
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        using var reg = new AssetRegistry(
            new UnifiedAssetProvider(diskRoot: null, pakPaths: new[] { "nonexistent.pak" }));
        var handle = new AssetHandle<byte[]>(reg, "data/x.bin", new byte[] { 1 }, AssetOwnership.Owned, 1);

        handle.Dispose();
        handle.Dispose();
        handle.Dispose();
        // Refcount should be 0 (not negative) due to the underflow guard.
        Assert.Equal(0, handle.RefCount);
    }

    [Fact]
    public void Dispose_NotifiesRegistry()
    {
        var (pak, reg) = CreateRegistryWithPak();
        try
        {
            var handle = new AssetHandle<byte[]>(reg, "data/x.txt", new byte[] { 1, 2, 3 }, AssetOwnership.Owned, 3);
            reg.RegisterHandle(handle);
            Assert.Equal(1, reg.TrackedCount);

            handle.Dispose();
            // Registry should still have the entry (placeholder impl only updates LastEvent)
            // but the refcount is 0.
            Assert.Equal(0, handle.RefCount);
            Assert.True(handle.IsDisposed);
        }
        finally
        {
            reg.Dispose();
            if (File.Exists(pak)) File.Delete(pak);
        }
    }

    // ──────────────────────────────────────────────────────────────────────
    // Borrow
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void Borrow_ReturnsSeparateHandle_SharesAssetReference()
    {
        using var reg = new AssetRegistry(
            new UnifiedAssetProvider(diskRoot: null, pakPaths: new[] { "nonexistent.pak" }));
        var handle = new AssetHandle<byte[]>(reg, "data/x.bin", new byte[] { 1 }, AssetOwnership.Owned, 1);

        var borrow = handle.Borrow();
        Assert.NotSame(handle, borrow);
        Assert.Same(handle.Asset, borrow.Asset);
        Assert.Equal(AssetOwnership.Borrow, borrow.Ownership);
        // Parent's refcount is unchanged (mirror v2 strict `borrow`).
        Assert.Equal(1, handle.RefCount);
        // New borrow starts with refcount 1 (it IS the live reference).
        Assert.Equal(1, borrow.RefCount);

        borrow.Dispose();
        Assert.Equal(0, borrow.RefCount);
        Assert.Equal(1, handle.RefCount);
        handle.Dispose();
        Assert.Equal(0, handle.RefCount);
    }

    [Fact]
    public void Borrow_OnBorrowedHandle_Throws()
    {
        using var reg = new AssetRegistry(
            new UnifiedAssetProvider(diskRoot: null, pakPaths: new[] { "nonexistent.pak" }));
        var owned = new AssetHandle<byte[]>(reg, "data/x.bin", new byte[] { 1 }, AssetOwnership.Owned, 1);
        var borrow = owned.Borrow();

        Assert.Throws<InvalidOperationException>(() => borrow.Borrow());
    }

    [Fact]
    public void Borrow_AfterDispose_Throws()
    {
        using var reg = new AssetRegistry(
            new UnifiedAssetProvider(diskRoot: null, pakPaths: new[] { "nonexistent.pak" }));
        var handle = new AssetHandle<byte[]>(reg, "data/x.bin", new byte[] { 1 }, AssetOwnership.Owned, 1);
        handle.Dispose();

        Assert.Throws<ObjectDisposedException>(() => handle.Borrow());
    }

    [Fact]
    public void MultipleBorrows_EachHasIndependentRefCount()
    {
        using var reg = new AssetRegistry(
            new UnifiedAssetProvider(diskRoot: null, pakPaths: new[] { "nonexistent.pak" }));
        var handle = new AssetHandle<byte[]>(reg, "data/x.bin", new byte[] { 1 }, AssetOwnership.Owned, 1);

        var b1 = handle.Borrow();
        var b2 = handle.Borrow();
        var b3 = handle.Borrow();
        // Each borrow is a separate handle with its own refcount; parent is unaffected.
        Assert.Equal(1, handle.RefCount);
        Assert.Equal(1, b1.RefCount);
        Assert.Equal(1, b2.RefCount);
        Assert.Equal(1, b3.RefCount);

        b1.Dispose();
        Assert.Equal(0, b1.RefCount);
        Assert.Equal(1, handle.RefCount);
        b2.Dispose();
        b3.Dispose();
        handle.Dispose();
        Assert.Equal(0, handle.RefCount);
    }

    // ──────────────────────────────────────────────────────────────────────
    // MoveTo
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void MoveTo_TransfersOwnership()
    {
        using var reg = new AssetRegistry(
            new UnifiedAssetProvider(diskRoot: null, pakPaths: new[] { "nonexistent.pak" }));
        var src = new AssetHandle<byte[]>(reg, "data/x.bin", new byte[] { 1 }, AssetOwnership.Owned, 1);
        var dst = new AssetHandle<byte[]>(reg, "data/x.bin", new byte[] { 1 }, AssetOwnership.Owned, 1);

        src.MoveTo(dst);
        Assert.True(src.IsDisposed);
        Assert.Equal(2, dst.RefCount); // 1 original + 1 transferred
    }

    [Fact]
    public void MoveTo_DifferentPath_Throws()
    {
        using var reg = new AssetRegistry(
            new UnifiedAssetProvider(diskRoot: null, pakPaths: new[] { "nonexistent.pak" }));
        var src = new AssetHandle<byte[]>(reg, "data/x.bin", new byte[] { 1 }, AssetOwnership.Owned, 1);
        var dst = new AssetHandle<byte[]>(reg, "data/y.bin", new byte[] { 1 }, AssetOwnership.Owned, 1);

        Assert.Throws<InvalidOperationException>(() => src.MoveTo(dst));
    }

    [Fact]
    public void MoveTo_BorrowedTarget_Throws()
    {
        using var reg = new AssetRegistry(
            new UnifiedAssetProvider(diskRoot: null, pakPaths: new[] { "nonexistent.pak" }));
        var src = new AssetHandle<byte[]>(reg, "data/x.bin", new byte[] { 1 }, AssetOwnership.Owned, 1);
        var dst = new AssetHandle<byte[]>(reg, "data/x.bin", new byte[] { 1 }, AssetOwnership.Borrow, 1);

        Assert.Throws<InvalidOperationException>(() => src.MoveTo(dst));
    }

    [Fact]
    public void MoveTo_DisposedSource_Throws()
    {
        using var reg = new AssetRegistry(
            new UnifiedAssetProvider(diskRoot: null, pakPaths: new[] { "nonexistent.pak" }));
        var src = new AssetHandle<byte[]>(reg, "data/x.bin", new byte[] { 1 }, AssetOwnership.Owned, 1);
        var dst = new AssetHandle<byte[]>(reg, "data/x.bin", new byte[] { 1 }, AssetOwnership.Owned, 1);
        src.Dispose();

        Assert.Throws<ObjectDisposedException>(() => src.MoveTo(dst));
    }

    [Fact]
    public void MoveTo_NullTarget_Throws()
    {
        using var reg = new AssetRegistry(
            new UnifiedAssetProvider(diskRoot: null, pakPaths: new[] { "nonexistent.pak" }));
        var src = new AssetHandle<byte[]>(reg, "data/x.bin", new byte[] { 1 }, AssetOwnership.Owned, 1);

        Assert.Throws<ArgumentNullException>(() => src.MoveTo(null!));
    }

    // ──────────────────────────────────────────────────────────────────────
    // Mark-and-sweep stub (Q5)
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void Mark_SetsFlag()
    {
        using var reg = new AssetRegistry(
            new UnifiedAssetProvider(diskRoot: null, pakPaths: new[] { "nonexistent.pak" }));
        var handle = new AssetHandle<byte[]>(reg, "data/x.bin", new byte[] { 1 }, AssetOwnership.Owned, 1);

        Assert.False(handle.IsMarked);
        handle.Mark();
        Assert.True(handle.IsMarked);
        handle.Mark(); // idempotent
        Assert.True(handle.IsMarked);
    }

    [Fact]
    public void Mark_OnDisposedHandle_IsNoOp()
    {
        using var reg = new AssetRegistry(
            new UnifiedAssetProvider(diskRoot: null, pakPaths: new[] { "nonexistent.pak" }));
        var handle = new AssetHandle<byte[]>(reg, "data/x.bin", new byte[] { 1 }, AssetOwnership.Owned, 1);
        handle.Dispose();

        handle.Mark();
        Assert.False(handle.IsMarked); // Mark() is no-op on disposed
    }

    [Fact]
    public void ResetMark_ClearsFlag()
    {
        using var reg = new AssetRegistry(
            new UnifiedAssetProvider(diskRoot: null, pakPaths: new[] { "nonexistent.pak" }));
        var handle = new AssetHandle<byte[]>(reg, "data/x.bin", new byte[] { 1 }, AssetOwnership.Owned, 1);
        handle.Mark();
        Assert.True(handle.IsMarked);

        // Use reflection for the internal method (or add InternalsVisibleTo).
        var resetMethod = typeof(AssetHandle<byte[]>).GetMethod(
            "ResetMark",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        resetMethod!.Invoke(handle, null);
        Assert.False(handle.IsMarked);
    }

    // ──────────────────────────────────────────────────────────────────────
    // Generation
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void SetGeneration_UpdatesGeneration()
    {
        using var reg = new AssetRegistry(
            new UnifiedAssetProvider(diskRoot: null, pakPaths: new[] { "nonexistent.pak" }));
        var handle = new AssetHandle<byte[]>(reg, "data/x.bin", new byte[] { 1 }, AssetOwnership.Owned, 1);
        Assert.Equal(AssetGeneration.Gen0, handle.Generation);

        var setGen = typeof(AssetHandle<byte[]>).GetMethod(
            "SetGeneration",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        setGen!.Invoke(handle, new object[] { AssetGeneration.Gen1 });
        Assert.Equal(AssetGeneration.Gen1, handle.Generation);

        setGen.Invoke(handle, new object[] { AssetGeneration.Gen2 });
        Assert.Equal(AssetGeneration.Gen2, handle.Generation);
    }

    // ──────────────────────────────────────────────────────────────────────
    // Touch / LastAccessTimeUtc
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void Touch_UpdatesLastAccessTime()
    {
        using var reg = new AssetRegistry(
            new UnifiedAssetProvider(diskRoot: null, pakPaths: new[] { "nonexistent.pak" }));
        var handle = new AssetHandle<byte[]>(reg, "data/x.bin", new byte[] { 1 }, AssetOwnership.Owned, 1);
        var initial = handle.LastAccessTimeUtc;

        Thread.Sleep(20);

        var touch = typeof(AssetHandle<byte[]>).GetMethod(
            "Touch",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        touch!.Invoke(handle, null);
        Assert.True(handle.LastAccessTimeUtc > initial);
    }

    [Fact]
    public void Borrow_UpdatesLastAccessTime()
    {
        using var reg = new AssetRegistry(
            new UnifiedAssetProvider(diskRoot: null, pakPaths: new[] { "nonexistent.pak" }));
        var handle = new AssetHandle<byte[]>(reg, "data/x.bin", new byte[] { 1 }, AssetOwnership.Owned, 1);
        var initial = handle.LastAccessTimeUtc;

        Thread.Sleep(20);
        var borrow = handle.Borrow();
        Assert.True(handle.LastAccessTimeUtc > initial);
        borrow.Dispose();
    }

    // ──────────────────────────────────────────────────────────────────────
    // Thread-safety
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void ConcurrentDispose_RefCountStaysNonNegative()
    {
        using var reg = new AssetRegistry(
            new UnifiedAssetProvider(diskRoot: null, pakPaths: new[] { "nonexistent.pak" }));
        var handle = new AssetHandle<byte[]>(reg, "data/x.bin", new byte[] { 1 }, AssetOwnership.Owned, 1);

        // Borrow 50 times, then dispose all concurrently
        var borrows = new List<AssetHandle<byte[]>>();
        for (int i = 0; i < 50; i++) borrows.Add(handle.Borrow());

        Parallel.ForEach(borrows, b => b.Dispose());
        handle.Dispose();
        handle.Dispose(); // extra
        handle.Dispose(); // extra

        // Refcount should be 0 (underflow guard prevents negative)
        Assert.Equal(0, handle.RefCount);
    }

    [Fact]
    public void ConcurrentMark_AllSeeSameFlag()
    {
        using var reg = new AssetRegistry(
            new UnifiedAssetProvider(diskRoot: null, pakPaths: new[] { "nonexistent.pak" }));
        var handle = new AssetHandle<byte[]>(reg, "data/x.bin", new byte[] { 1 }, AssetOwnership.Owned, 1);

        Parallel.For(0, 100, _ => handle.Mark());
        Assert.True(handle.IsMarked);
    }

    // ──────────────────────────────────────────────────────────────────────
    // Registry-aware integration
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void Registry_Disabled_DoesNotEvict()
    {
        var (pak, reg) = CreateRegistryWithPak();
        try
        {
            Assert.False(reg.Enabled); // default off for staged rollout
            Assert.Equal(512L * 1024 * 1024, reg.TotalBudgetBytes);
            Assert.Equal(TimeSpan.FromSeconds(1), reg.Gen1Promotion);
            Assert.Equal(TimeSpan.FromSeconds(30), reg.Gen2Promotion);
        }
        finally
        {
            reg.Dispose();
            if (File.Exists(pak)) File.Delete(pak);
        }
    }

    [Fact]
    public void Registry_TrackedCount_Increments()
    {
        var (pak, reg) = CreateRegistryWithPak();
        try
        {
            Assert.Equal(0, reg.TrackedCount);
            var handle = new AssetHandle<byte[]>(reg, "data/x.txt", new byte[] { 1 }, AssetOwnership.Owned, 1);
            reg.RegisterHandle(handle);
            Assert.Equal(1, reg.TrackedCount);
            Assert.Equal(1, reg.CurrentBytes);
        }
        finally
        {
            reg.Dispose();
            if (File.Exists(pak)) File.Delete(pak);
        }
    }
}
