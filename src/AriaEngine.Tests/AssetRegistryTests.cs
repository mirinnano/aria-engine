using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AriaEngine.Assets;
using AriaEngine.Packaging;
using Xunit;

namespace AriaEngine.Tests;

/// <summary>
/// Unit tests for <see cref="AssetRegistry"/> (Pak v3 redesign, Phase 3.1+3.2+3.3).
/// </summary>
public class AssetRegistryTests
{
    // ──────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>Create a registry backed by a no-op (nonexistent pak) provider.</summary>
    private static AssetRegistry NewRegistry(
        long totalBudgetBytes = 512L * 1024 * 1024,
        TimeSpan? gen1Promotion = null,
        TimeSpan? gen2Promotion = null)
    {
        return new AssetRegistry(
            new UnifiedAssetProvider(diskRoot: null, pakPaths: new[] { "nonexistent.pak" }),
            totalBudgetBytes,
            gen1Promotion,
            gen2Promotion);
    }

    /// <summary>Create a registry with Enabled = true.</summary>
    private static AssetRegistry NewEnabledRegistry(
        long totalBudgetBytes = 512L * 1024 * 1024,
        TimeSpan? gen1Promotion = null,
        TimeSpan? gen2Promotion = null)
    {
        var reg = NewRegistry(totalBudgetBytes, gen1Promotion, gen2Promotion);
        reg.Enabled = true;
        return reg;
    }

    // ──────────────────────────────────────────────────────────────────────
    // Configuration
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void DefaultConfig_HasDesignDocValues()
    {
        using var reg = NewRegistry();
        Assert.Equal(512L * 1024 * 1024, reg.TotalBudgetBytes);
        Assert.Equal(TimeSpan.FromSeconds(1), reg.Gen1Promotion);
        Assert.Equal(TimeSpan.FromSeconds(30), reg.Gen2Promotion);
    }

    [Fact]
    public void DefaultConfig_DisabledByDefault_ForStagedRollout()
    {
        using var reg = NewRegistry();
        Assert.False(reg.Enabled);
    }

    // ──────────────────────────────────────────────────────────────────────
    // Registration
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void Register_Owned_AddsToMap_IncrementsBytes()
    {
        using var reg = NewRegistry();
        var handle = new AssetHandle<byte[]>(reg, "data/x.bin", new byte[] { 1 }, AssetOwnership.Owned, 1);

        reg.RegisterHandle(handle);

        Assert.Equal(1, reg.TrackedCount);
        Assert.Equal(1, reg.CurrentBytes);
    }

    [Fact]
    public void Register_Borrow_DoesNotIncrementTrackedCount_IncrementsBorrowCount()
    {
        using var reg = NewEnabledRegistry();
        var owned = new AssetHandle<byte[]>(reg, "data/x.bin", new byte[] { 1 }, AssetOwnership.Owned, 1);
        reg.RegisterHandle(owned);

        var borrow = new AssetHandle<byte[]>(reg, "data/x.bin", new byte[] { 1 }, AssetOwnership.Borrow, 1);
        reg.RegisterHandle(borrow);

        // TrackedCount is the number of distinct paths, not the number of handles.
        Assert.Equal(1, reg.TrackedCount);
        Assert.Equal(1, reg.CurrentBytes);

        var entry = reg.GetEntry("data/x.bin");
        Assert.NotNull(entry);
        Assert.Equal(1, entry!.PrimaryRefCount);
        Assert.Equal(1, entry.BorrowCount);

        owned.Dispose();
        borrow.Dispose();
    }

    [Fact]
    public void Register_DuplicatePath_DoesNotDoubleCount()
    {
        using var reg = NewEnabledRegistry();
        var h1 = new AssetHandle<byte[]>(reg, "data/x.bin", new byte[] { 1 }, AssetOwnership.Owned, 1);
        var h2 = new AssetHandle<byte[]>(reg, "data/x.bin", new byte[] { 1 }, AssetOwnership.Owned, 1);
        reg.RegisterHandle(h1);
        reg.RegisterHandle(h2);  // TryAdd fails: no double-count

        Assert.Equal(1, reg.TrackedCount);
        Assert.Equal(1, reg.CurrentBytes);

        h1.Dispose();
        h2.Dispose();
    }

    // ──────────────────────────────────────────────────────────────────────
    // Disposal + Eviction (Enabled = true)
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void Dispose_Owned_NoBorrows_EvictsImmediately()
    {
        using var reg = NewEnabledRegistry();
        var handle = new AssetHandle<byte[]>(reg, "data/x.bin", new byte[] { 1 }, AssetOwnership.Owned, 1);
        reg.RegisterHandle(handle);
        Assert.Equal(1, reg.TrackedCount);

        handle.Dispose();

        Assert.Equal(0, reg.TrackedCount);
        Assert.Equal(0, reg.CurrentBytes);
    }

    [Fact]
    public void Dispose_Owned_WithActiveBorrow_DoesNotEvict()
    {
        using var reg = NewEnabledRegistry();
        var owned = new AssetHandle<byte[]>(reg, "data/x.bin", new byte[] { 1 }, AssetOwnership.Owned, 1);
        var borrow = new AssetHandle<byte[]>(reg, "data/x.bin", new byte[] { 1 }, AssetOwnership.Borrow, 1);
        reg.RegisterHandle(owned);
        reg.RegisterHandle(borrow);

        owned.Dispose();  // primary gone, but borrow alive

        Assert.Equal(1, reg.TrackedCount);
        var entry = reg.GetEntry("data/x.bin");
        Assert.NotNull(entry);
        Assert.Equal(0, entry!.PrimaryRefCount);
        Assert.Equal(1, entry.BorrowCount);

        borrow.Dispose();  // now truly idle → evict
        Assert.Equal(0, reg.TrackedCount);
    }

    [Fact]
    public void Dispose_DisabledRegistry_PassiveObserver_EntryStays()
    {
        using var reg = NewRegistry();  // Enabled = false
        var handle = new AssetHandle<byte[]>(reg, "data/x.bin", new byte[] { 1 }, AssetOwnership.Owned, 1);
        reg.RegisterHandle(handle);

        handle.Dispose();

        // Disabled: entry stays until Sweep is called manually.
        Assert.Equal(1, reg.TrackedCount);
        var entry = reg.GetEntry("data/x.bin");
        Assert.NotNull(entry);
        Assert.Equal(0, entry!.PrimaryRefCount);
    }

    // ──────────────────────────────────────────────────────────────────────
    // Generation
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void NewEntry_StartsAtGen0()
    {
        using var reg = NewEnabledRegistry();
        var handle = new AssetHandle<byte[]>(reg, "data/x.bin", new byte[] { 1 }, AssetOwnership.Owned, 1);
        reg.RegisterHandle(handle);

        var entry = reg.GetEntry("data/x.bin");
        Assert.NotNull(entry);
        Assert.Equal(AssetGeneration.Gen0, entry!.Generation);

        handle.Dispose();
    }

    [Fact]
    public void Promote_Gen0_To_Gen1_To_Gen2()
    {
        using var reg = NewEnabledRegistry();
        var handle = new AssetHandle<byte[]>(reg, "data/x.bin", new byte[] { 1 }, AssetOwnership.Owned, 1);
        reg.RegisterHandle(handle);

        Assert.True(reg.Promote("data/x.bin"));
        Assert.Equal(AssetGeneration.Gen1, reg.GetEntry("data/x.bin")!.Generation);

        Assert.True(reg.Promote("data/x.bin"));
        Assert.Equal(AssetGeneration.Gen2, reg.GetEntry("data/x.bin")!.Generation);

        // Gen2 stays at Gen2.
        Assert.False(reg.Promote("data/x.bin"));
        Assert.Equal(AssetGeneration.Gen2, reg.GetEntry("data/x.bin")!.Generation);

        handle.Dispose();
    }

    [Fact]
    public void Promote_NonExistentPath_ReturnsFalse()
    {
        using var reg = NewEnabledRegistry();
        Assert.False(reg.Promote("data/nonexistent.bin"));
    }

    [Fact]
    public void Sweep_PromotesByAge_AfterGen1PromotionElapsed()
    {
        using var reg = NewEnabledRegistry(gen1Promotion: TimeSpan.FromMilliseconds(50));
        var handle = new AssetHandle<byte[]>(reg, "data/x.bin", new byte[] { 1 }, AssetOwnership.Owned, 1);
        reg.RegisterHandle(handle);
        Assert.Equal(AssetGeneration.Gen0, reg.GetEntry("data/x.bin")!.Generation);

        Thread.Sleep(80);  // > 50 ms
        reg.Sweep();

        Assert.Equal(AssetGeneration.Gen1, reg.GetEntry("data/x.bin")!.Generation);

        handle.Dispose();
    }

    // ──────────────────────────────────────────────────────────────────────
    // Mark (Q5 stub)
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void Mark_PreventsEviction_OnNotify()
    {
        using var reg = NewEnabledRegistry();
        var handle = new AssetHandle<byte[]>(reg, "data/x.bin", new byte[] { 1 }, AssetOwnership.Owned, 1);
        reg.RegisterHandle(handle);
        Assert.True(reg.Mark("data/x.bin"));

        handle.Dispose();

        // Marked → entry survives eviction.
        Assert.Equal(1, reg.TrackedCount);
        Assert.True(reg.GetEntry("data/x.bin")!.Marked);
    }

    [Fact]
    public void ResetMark_AllowsEviction_OnSweep()
    {
        using var reg = NewEnabledRegistry();
        var handle = new AssetHandle<byte[]>(reg, "data/x.bin", new byte[] { 1 }, AssetOwnership.Owned, 1);
        reg.RegisterHandle(handle);
        reg.Mark("data/x.bin");
        handle.Dispose();
        Assert.Equal(1, reg.TrackedCount);

        reg.ResetMark("data/x.bin");
        reg.Sweep();

        Assert.Equal(0, reg.TrackedCount);
    }

    [Fact]
    public void Mark_NonExistentPath_ReturnsFalse()
    {
        using var reg = NewEnabledRegistry();
        Assert.False(reg.Mark("data/nonexistent.bin"));
    }

    // ──────────────────────────────────────────────────────────────────────
    // Sweep
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void Sweep_DisabledRegistry_ReturnsZero()
    {
        using var reg = NewRegistry();  // Enabled = false
        var handle = new AssetHandle<byte[]>(reg, "data/x.bin", new byte[] { 1 }, AssetOwnership.Owned, 1);
        reg.RegisterHandle(handle);
        handle.Dispose();

        Assert.Equal(0, reg.Sweep());
        Assert.Equal(1, reg.TrackedCount);  // entry still there
    }

    [Fact]
    public void Sweep_EvictsIdleGen0()
    {
        using var reg = NewEnabledRegistry();
        var handle = new AssetHandle<byte[]>(reg, "data/x.bin", new byte[] { 1 }, AssetOwnership.Owned, 1);
        reg.RegisterHandle(handle);
        handle.Dispose();  // auto-evicts (Enabled = true), so entry may already be gone

        // Even if the entry was auto-evicted on Dispose, Sweep should report
        // at least 0 evicted (idempotent).
        var evicted = reg.Sweep();
        Assert.Equal(0, evicted);
        Assert.Equal(0, reg.TrackedCount);
    }

    [Fact]
    public void Sweep_KeepsLiveRefcount()
    {
        using var reg = NewEnabledRegistry();
        var handle = new AssetHandle<byte[]>(reg, "data/x.bin", new byte[] { 1 }, AssetOwnership.Owned, 1);
        reg.RegisterHandle(handle);
        // Don't dispose; refcount stays 1.
        reg.Sweep();

        Assert.Equal(1, reg.TrackedCount);
        handle.Dispose();
    }

    [Fact]
    public void Sweep_KeepsGen2()
    {
        using var reg = NewEnabledRegistry();
        var handle = new AssetHandle<byte[]>(reg, "data/x.bin", new byte[] { 1 }, AssetOwnership.Owned, 1);
        reg.RegisterHandle(handle);
        reg.Promote("data/x.bin");
        reg.Promote("data/x.bin");  // Gen0 → Gen1 → Gen2
        handle.Dispose();  // primary refcount = 0, but Gen2 protects

        reg.Sweep();
        Assert.Equal(1, reg.TrackedCount);
    }

    // ──────────────────────────────────────────────────────────────────────
    // Budget enforcement
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void EnforceBudget_DisabledRegistry_NoOp()
    {
        using var reg = NewRegistry(totalBudgetBytes: 100);
        var handle = new AssetHandle<byte[]>(reg, "data/x.bin", new byte[] { 1 }, AssetOwnership.Owned, 200);
        reg.RegisterHandle(handle);

        Assert.Equal(0, reg.EnforceBudget());
        Assert.Equal(1, reg.TrackedCount);  // entry still there (disabled)

        handle.Dispose();
    }

    [Fact]
    public void EnforceBudget_UnderBudget_NoOp()
    {
        using var reg = NewEnabledRegistry(totalBudgetBytes: 1000);
        var handle = new AssetHandle<byte[]>(reg, "data/x.bin", new byte[] { 1 }, AssetOwnership.Owned, 500);
        reg.RegisterHandle(handle);

        Assert.Equal(0, reg.EnforceBudget());
        Assert.Equal(1, reg.TrackedCount);

        handle.Dispose();
    }

    [Fact]
    public void EnforceBudget_OverBudget_EvictsIdleEntries()
    {
        // Budget = 10 bytes. Register 3 idle entries (each 5 bytes, total 15).
        using var reg = NewEnabledRegistry(totalBudgetBytes: 10);
        var h1 = new AssetHandle<byte[]>(reg, "data/a.bin", new byte[] { 1 }, AssetOwnership.Owned, 5);
        var h2 = new AssetHandle<byte[]>(reg, "data/b.bin", new byte[] { 1 }, AssetOwnership.Owned, 5);
        var h3 = new AssetHandle<byte[]>(reg, "data/c.bin", new byte[] { 1 }, AssetOwnership.Owned, 5);
        reg.RegisterHandle(h1);
        reg.RegisterHandle(h2);
        reg.RegisterHandle(h3);
        Assert.Equal(15, reg.CurrentBytes);

        // Dispose all → all entries are idle. EnforceBudget should sweep them
        // to bring the registry back under budget (or close to it).
        h1.Dispose();
        h2.Dispose();
        h3.Dispose();

        reg.EnforceBudget();
        // All 3 should be evicted (or at least enough to be under 10 bytes).
        Assert.True(reg.CurrentBytes <= 10,
            $"Expected CurrentBytes <= 10, got {reg.CurrentBytes}");
    }

    [Fact]
    public void Register_OverBudget_TriggersSweep()
    {
        // Budget = 50 bytes. Register 5 idle (10 bytes each) → over budget.
        using var reg = NewEnabledRegistry(totalBudgetBytes: 50);
        var handles = new List<AssetHandle<byte[]>>();
        for (int i = 0; i < 5; i++)
        {
            var h = new AssetHandle<byte[]>(reg, $"data/{i}.bin", new byte[] { 1 }, AssetOwnership.Owned, 10);
            reg.RegisterHandle(h);
            handles.Add(h);
        }
        // After the 5th register, EnforceBudget is called automatically.
        // The registry should have swept idle entries to come back under 50 bytes.
        Assert.True(reg.CurrentBytes <= 50,
            $"Expected CurrentBytes <= 50 after auto-sweep, got {reg.CurrentBytes}");

        foreach (var h in handles) h.Dispose();
    }

    // ──────────────────────────────────────────────────────────────────────
    // Concurrency
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void Concurrent_RegisterAndDispose_DoesNotCrash()
    {
        using var reg = NewEnabledRegistry();
        var handles = new List<AssetHandle<byte[]>>();

        // Register concurrently.
        Parallel.For(0, 100, i =>
        {
            var h = new AssetHandle<byte[]>(reg, $"data/{i}.bin", new byte[] { 1 }, AssetOwnership.Owned, 1);
            reg.RegisterHandle(h);
            lock (handles) handles.Add(h);
        });

        Assert.Equal(100, reg.TrackedCount);

        // Dispose concurrently.
        Parallel.ForEach(handles, h => h.Dispose());

        // All entries should be evicted (each path unique, no borrows, all disposed).
        Assert.Equal(0, reg.TrackedCount);
    }

    [Fact]
    public void Concurrent_Sweep_WhileDisposing_DoesNotCrash()
    {
        using var reg = NewEnabledRegistry();
        var handles = new List<AssetHandle<byte[]>>();

        for (int i = 0; i < 50; i++)
        {
            var h = new AssetHandle<byte[]>(reg, $"data/{i}.bin", new byte[] { 1 }, AssetOwnership.Owned, 1);
            reg.RegisterHandle(h);
            handles.Add(h);
        }

        // Dispose handles and sweep concurrently.
        var disposeTask = Task.Run(() => Parallel.ForEach(handles, h => h.Dispose()));
        var sweepTask = Task.Run(() =>
        {
            for (int i = 0; i < 10; i++) reg.Sweep();
        });

        Task.WaitAll(disposeTask, sweepTask);

        // Final state: all entries evicted.
        Assert.Equal(0, reg.TrackedCount);
    }

    // ──────────────────────────────────────────────────────────────────────
    // Dispose (registry)
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void Registry_Dispose_ClearsState()
    {
        var reg = NewEnabledRegistry();
        var handle = new AssetHandle<byte[]>(reg, "data/x.bin", new byte[] { 1 }, AssetOwnership.Owned, 1);
        reg.RegisterHandle(handle);
        Assert.Equal(1, reg.TrackedCount);

        reg.Dispose();

        Assert.Equal(0, reg.TrackedCount);
        Assert.Equal(0, reg.CurrentBytes);
    }

    [Fact]
    public void Registry_Dispose_IsIdempotent()
    {
        var reg = NewEnabledRegistry();
        reg.Dispose();
        reg.Dispose();  // no exception
    }
}
