#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using AriaEngine.Assets;
using AriaEngine.Core;
using AriaEngine.Core.Commands;
using Xunit;

namespace AriaEngine.Tests;

/// <summary>
/// Unit tests for <see cref="AssetCommandHandler"/> (Pak v3 redesign, Phase 4.2).
///
/// Phase 4.2 is a minimum viable implementation: bytes-only payload, no scope-exit
/// auto-dispose. <c>borrow</c> / <c>move</c> are accepted but reported as a warning
/// and stored as a no-op entry. Tests below mirror that contract.
/// </summary>
public class AssetCommandHandlerTests : IDisposable
{
    private readonly string _tempDir;

    public AssetCommandHandlerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"asset_cmd_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* ignore */ }
    }

    // ──────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────

    private (VirtualMachine vm, IAssetProvider provider, AssetRegistry? registry) NewVm(
        bool wireRegistry = true,
        long budget = 512L * 1024 * 1024)
    {
        var reporter = new ErrorReporter();
        var provider = new UnifiedAssetProvider(diskRoot: _tempDir, pakPaths: Array.Empty<string>());
        AssetRegistry? registry = wireRegistry
            ? new AssetRegistry(provider, totalBudgetBytes: budget)
            : null;
        var vm = new VirtualMachine(
            reporter,
            new Rendering.TweenManager(),
            new SaveManager(reporter),
            new ConfigManager(reporter),
            provider,
            runtimeDataRoot: null,
            assetRegistry: registry);
        return (vm, provider, registry);
    }

    private static Instruction NewLoadAssetInstruction(params string[] args)
    {
        // OpCode.LoadAsset: arity 2..3 (path, result_var, [ownership]).
        return new Instruction(OpCode.LoadAsset, args, sourceLine: 1);
    }

    private void WriteAsset(string relPath, byte[] data)
    {
        string full = Path.Combine(_tempDir, relPath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllBytes(full, data);
    }

    // ──────────────────────────────────────────────────────────────────────
    // HandledCodes
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void HandledCodes_Contains_LoadAsset()
    {
        var (vm, provider, registry) = NewVm();
        var handler = new AssetCommandHandler(vm, provider, registry);
        Assert.Contains(OpCode.LoadAsset, handler.HandledCodes);
    }

    [Fact]
    public void Execute_UnknownOp_ReturnsFalse()
    {
        var (vm, provider, registry) = NewVm();
        var handler = new AssetCommandHandler(vm, provider, registry);
        var inst = new Instruction(OpCode.Wait, new[] { "100" }, sourceLine: 1);
        Assert.False(handler.Execute(inst));
    }

    // ──────────────────────────────────────────────────────────────────────
    // Arity / argument validation
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void Execute_LoadAsset_MissingArgs_NoOp()
    {
        var (vm, provider, registry) = NewVm();
        var handler = new AssetCommandHandler(vm, provider, registry);
        var inst = new Instruction(OpCode.LoadAsset, new[] { "data/x.bin" }, sourceLine: 1);
        // ValidateArgs fails → silent no-op (ValidateArgs reports the error itself).
        Assert.True(handler.Execute(inst));
        Assert.Empty(vm.State.AssetHandleTable);
    }

    [Fact]
    public void Execute_LoadAsset_EmptyResultVar_ReportsError()
    {
        var (vm, provider, registry) = NewVm();
        var handler = new AssetCommandHandler(vm, provider, registry);
        var inst = NewLoadAssetInstruction("data/x.bin", "");
        Assert.True(handler.Execute(inst));
        // No table entry because validation failed.
        Assert.Empty(vm.State.AssetHandleTable);
    }

    [Fact]
    public void Execute_LoadAsset_InvalidOwnership_ReportsError()
    {
        WriteAsset("data/x.bin", new byte[] { 1, 2, 3 });
        var (vm, provider, registry) = NewVm();
        var handler = new AssetCommandHandler(vm, provider, registry);
        var inst = NewLoadAssetInstruction("data/x.bin", "@myasset", "garbage");
        Assert.True(handler.Execute(inst));
        // No table entry because validation failed.
        Assert.Empty(vm.State.AssetHandleTable);
    }

    // ──────────────────────────────────────────────────────────────────────
    // Borrow / Move: defer to Phase 4.3
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void Execute_LoadAsset_Borrow_StoresNoOp_Warns()
    {
        WriteAsset("data/x.bin", new byte[] { 1, 2, 3 });
        var (vm, provider, registry) = NewVm();
        var handler = new AssetCommandHandler(vm, provider, registry);
        var inst = NewLoadAssetInstruction("data/x.bin", "@myasset", "borrow");
        Assert.True(handler.Execute(inst));
        // Stored as null entry — no handle, no refcount, no eviction.
        Assert.True(vm.State.AssetHandleTable.ContainsKey("@myasset"));
        Assert.Null(vm.State.AssetHandleTable["@myasset"]);
        Assert.Equal(0, registry!.TrackedCount);
    }

    [Fact]
    public void Execute_LoadAsset_Move_StoresNoOp_Warns()
    {
        WriteAsset("data/y.bin", new byte[] { 9, 8, 7 });
        var (vm, provider, registry) = NewVm();
        var handler = new AssetCommandHandler(vm, provider, registry);
        var inst = NewLoadAssetInstruction("data/y.bin", "myasset2", "move");
        Assert.True(handler.Execute(inst));
        Assert.True(vm.State.AssetHandleTable.ContainsKey("myasset2"));
        Assert.Null(vm.State.AssetHandleTable["myasset2"]);
        Assert.Equal(0, registry!.TrackedCount);
    }

    // ──────────────────────────────────────────────────────────────────────
    // Owned: full path
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void Execute_LoadAsset_Owned_Success_CreatesHandle_StoresInTable()
    {
        byte[] payload = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF, 0x01, 0x02 };
        WriteAsset("data/blob.bin", payload);
        var (vm, provider, registry) = NewVm();
        var handler = new AssetCommandHandler(vm, provider, registry);
        var inst = NewLoadAssetInstruction("data/blob.bin", "@blob");
        Assert.True(handler.Execute(inst));

        // Table entry exists and is a real AssetHandle<byte[]>.
        Assert.True(vm.State.AssetHandleTable.ContainsKey("@blob"));
        var handle = Assert.IsType<AssetHandle<byte[]>>(vm.State.AssetHandleTable["@blob"]);
        Assert.Equal(payload, handle.Asset);  // File.ReadAllBytes returns a new array, so compare contents
        Assert.Equal("data/blob.bin", handle.Path);
        Assert.Equal(AssetOwnership.Owned, handle.Ownership);
        Assert.Equal(payload.Length, handle.SizeBytes);
        Assert.Equal(1, handle.RefCount);
        Assert.False(handle.IsDisposed);
    }

    [Fact]
    public void Execute_LoadAsset_Owned_RegistersWithRegistry_IncrementsBytes()
    {
        byte[] payload = new byte[] { 0x11, 0x22, 0x33, 0x44 };
        WriteAsset("data/reg.bin", payload);
        var (vm, provider, registry) = NewVm();
        var handler = new AssetCommandHandler(vm, provider, registry);
        var inst = NewLoadAssetInstruction("data/reg.bin", "regvar");
        Assert.True(handler.Execute(inst));

        // Registry should now track this asset.
        Assert.Equal(1, registry!.TrackedCount);
        Assert.Equal(payload.Length, registry.CurrentBytes);
        var entry = registry.GetEntry("data/reg.bin");
        Assert.NotNull(entry);
        Assert.Equal(1, entry!.PrimaryRefCount);
        Assert.Equal(0, entry.BorrowCount);
        Assert.Equal(AssetGeneration.Gen0, entry.Generation);
    }

    [Fact]
    public void Execute_LoadAsset_Owned_WithoutRegistry_StillProducesHandle()
    {
        byte[] payload = new byte[] { 0xAB, 0xCD };
        WriteAsset("data/noreg.bin", payload);
        var (vm, provider, _) = NewVm(wireRegistry: false);
        var handler = new AssetCommandHandler(vm, provider, assetRegistry: null);
        var inst = NewLoadAssetInstruction("data/noreg.bin", "@noreg");
        Assert.True(handler.Execute(inst));

        // Handle created and stored even without registry.
        var handle = Assert.IsType<AssetHandle<byte[]>>(vm.State.AssetHandleTable["@noreg"]);
        Assert.Equal(payload, handle.Asset);  // File.ReadAllBytes returns a new array, so compare contents
        Assert.Equal(1, handle.RefCount);
    }

    [Fact]
    public void Execute_LoadAsset_AssetMissing_Warns_StoresNull()
    {
        var (vm, provider, registry) = NewVm();
        var handler = new AssetCommandHandler(vm, provider, registry);
        var inst = NewLoadAssetInstruction("data/does_not_exist.bin", "@missing");
        Assert.True(handler.Execute(inst));

        // Null entry, no registry tracking.
        Assert.True(vm.State.AssetHandleTable.ContainsKey("@missing"));
        Assert.Null(vm.State.AssetHandleTable["@missing"]);
        Assert.Equal(0, registry!.TrackedCount);
    }

    [Fact]
    public void Execute_LoadAsset_Owned_DefaultOwnership_WhenOmitted()
    {
        byte[] payload = new byte[] { 0x42 };
        WriteAsset("data/default.bin", payload);
        var (vm, provider, registry) = NewVm();
        var handler = new AssetCommandHandler(vm, provider, registry);
        // No ownership arg → defaults to "owned".
        var inst = NewLoadAssetInstruction("data/default.bin", "@def");
        Assert.True(handler.Execute(inst));

        var handle = Assert.IsType<AssetHandle<byte[]>>(vm.State.AssetHandleTable["@def"]);
        Assert.Equal(AssetOwnership.Owned, handle.Ownership);
    }

    [Fact]
    public void Execute_LoadAsset_Owned_CaseInsensitive_Ownership()
    {
        byte[] payload = new byte[] { 0x99 };
        WriteAsset("data/case.bin", payload);
        var (vm, provider, registry) = NewVm();
        var handler = new AssetCommandHandler(vm, provider, registry);
        var inst = NewLoadAssetInstruction("data/case.bin", "@case", "OWNED");
        Assert.True(handler.Execute(inst));

        var handle = Assert.IsType<AssetHandle<byte[]>>(vm.State.AssetHandleTable["@case"]);
        Assert.Equal(AssetOwnership.Owned, handle.Ownership);
    }

    [Fact]
    public void Execute_LoadAsset_TwoDistinctPaths_TwoTableEntries()
    {
        WriteAsset("data/a.bin", new byte[] { 1 });
        WriteAsset("data/b.bin", new byte[] { 2, 3 });
        var (vm, provider, registry) = NewVm();
        var handler = new AssetCommandHandler(vm, provider, registry);
        handler.Execute(NewLoadAssetInstruction("data/a.bin", "@a"));
        handler.Execute(NewLoadAssetInstruction("data/b.bin", "@b"));

        Assert.Equal(2, vm.State.AssetHandleTable.Count);
        Assert.Equal(2, registry!.TrackedCount);
        Assert.Equal(3, registry.CurrentBytes);
    }

    // ──────────────────────────────────────────────────────────────────────
    // Phase 4.3: scope-exit auto-dispose for owned handles
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void Owned_Handle_IsTrackedInScope_OnLoad()
    {
        WriteAsset("data/owned.bin", new byte[] { 0xCA, 0xFE });
        var (vm, provider, _) = NewVm();
        var handler = new AssetCommandHandler(vm, provider);

        // Declare @bgm as owned (parser would do this for `owned asset @bgm`).
        vm.State.OwnedSprites.Add("@bgm");
        vm.EnterScope();

        handler.Execute(NewLoadAssetInstruction("data/owned.bin", "@bgm"));

        Assert.True(vm.State.AssetHandleTable.ContainsKey("@bgm"));
        var tracked = vm.State.Execution.AssetHandleLifetimeStacks.Peek();
        Assert.Contains("@bgm", tracked);
    }

    [Fact]
    public void Unowned_Handle_IsNotTrackedInScope_OnLoad()
    {
        WriteAsset("data/unowned.bin", new byte[] { 0xFE, 0xED });
        var (vm, provider, _) = NewVm();
        var handler = new AssetCommandHandler(vm, provider);

        // @tmp is NOT in OwnedSprites.
        vm.EnterScope();

        handler.Execute(NewLoadAssetInstruction("data/unowned.bin", "@tmp"));

        Assert.True(vm.State.AssetHandleTable.ContainsKey("@tmp"));
        var tracked = vm.State.Execution.AssetHandleLifetimeStacks.Peek();
        Assert.DoesNotContain("@tmp", tracked);
    }

    [Fact]
    public void Owned_Handle_IsDisposedAndRemoved_OnScopeExit()
    {
        WriteAsset("data/dispose.bin", new byte[] { 0x42, 0x42, 0x42 });
        var (vm, provider, registry) = NewVm();
        var handler = new AssetCommandHandler(vm, provider, registry);

        vm.State.OwnedSprites.Add("@bgm");
        vm.EnterScope();
        handler.Execute(NewLoadAssetInstruction("data/dispose.bin", "@bgm"));

        var handle = (AssetHandle<byte[]>)vm.State.AssetHandleTable["@bgm"];
        Assert.False(handle.IsDisposed);
        Assert.Equal(1, registry!.TrackedCount);

        // Exit the scope — owned handle should be disposed + removed.
        vm.ExitScopesUntil(0);

        Assert.False(vm.State.AssetHandleTable.ContainsKey("@bgm"));
        Assert.True(handle.IsDisposed);
    }

    [Fact]
    public void Unowned_Handle_PersistsPastScopeExit()
    {
        WriteAsset("data/persist.bin", new byte[] { 0x99 });
        var (vm, provider, _) = NewVm();
        var handler = new AssetCommandHandler(vm, provider);

        vm.EnterScope();
        handler.Execute(NewLoadAssetInstruction("data/persist.bin", "@kept"));

        var handle = (AssetHandle<byte[]>)vm.State.AssetHandleTable["@kept"];
        vm.ExitScopesUntil(0);

        // Unowned handle survives scope exit (no auto-dispose).
        Assert.True(vm.State.AssetHandleTable.ContainsKey("@kept"));
        Assert.Same(handle, vm.State.AssetHandleTable["@kept"]);
        Assert.False(handle.IsDisposed);
    }

    [Fact]
    public void OwnedHandle_Registry_Notified_OnScopeExit()
    {
        WriteAsset("data/reg_dispose.bin", new byte[] { 0x55 });
        var (vm, provider, registry) = NewVm();
        var handler = new AssetCommandHandler(vm, provider, registry);
        registry!.Enabled = true;  // enable so eviction can run

        vm.State.OwnedSprites.Add("@r");
        vm.EnterScope();
        handler.Execute(NewLoadAssetInstruction("data/reg_dispose.bin", "@r"));
        Assert.Equal(1, registry.TrackedCount);
        Assert.Equal(1, registry.CurrentBytes);

        vm.ExitScopesUntil(0);

        // After scope exit, the entry should be evicted (refcount 0, !marked, Gen0).
        Assert.Equal(0, registry.TrackedCount);
        Assert.Equal(0, registry.CurrentBytes);
    }

    [Fact]
    public void MultipleOwnedHandles_AllDisposed_OnScopeExit()
    {
        WriteAsset("data/m1.bin", new byte[] { 1 });
        WriteAsset("data/m2.bin", new byte[] { 2, 2 });
        WriteAsset("data/m3.bin", new byte[] { 3, 3, 3 });
        var (vm, provider, registry) = NewVm();
        var handler = new AssetCommandHandler(vm, provider, registry);

        vm.State.OwnedSprites.Add("@a");
        vm.State.OwnedSprites.Add("@b");
        vm.State.OwnedSprites.Add("@c");
        vm.EnterScope();
        handler.Execute(NewLoadAssetInstruction("data/m1.bin", "@a"));
        handler.Execute(NewLoadAssetInstruction("data/m2.bin", "@b"));
        handler.Execute(NewLoadAssetInstruction("data/m3.bin", "@c"));

        var a = (AssetHandle<byte[]>)vm.State.AssetHandleTable["@a"];
        var b = (AssetHandle<byte[]>)vm.State.AssetHandleTable["@b"];
        var c = (AssetHandle<byte[]>)vm.State.AssetHandleTable["@c"];
        Assert.Equal(3, registry!.TrackedCount);

        vm.ExitScopesUntil(0);

        Assert.True(a.IsDisposed);
        Assert.True(b.IsDisposed);
        Assert.True(c.IsDisposed);
        Assert.Empty(vm.State.AssetHandleTable);
    }

    [Fact]
    public void NestedScopes_OnlyCurrentScopeHandlesDisposed()
    {
        WriteAsset("data/outer.bin", new byte[] { 0x10 });
        WriteAsset("data/inner.bin", new byte[] { 0x20, 0x21 });
        var (vm, provider, registry) = NewVm();
        var handler = new AssetCommandHandler(vm, provider, registry);

        vm.State.OwnedSprites.Add("@outer");
        vm.State.OwnedSprites.Add("@inner");
        vm.EnterScope();
        handler.Execute(NewLoadAssetInstruction("data/outer.bin", "@outer"));
        var outer = (AssetHandle<byte[]>)vm.State.AssetHandleTable["@outer"];

        // Enter a nested scope
        vm.EnterScope();
        handler.Execute(NewLoadAssetInstruction("data/inner.bin", "@inner"));
        var inner = (AssetHandle<byte[]>)vm.State.AssetHandleTable["@inner"];

        // Exit inner scope — only @inner should be disposed.
        vm.ExitScopesUntil(1);
        Assert.True(inner.IsDisposed);
        Assert.False(outer.IsDisposed);
        Assert.False(vm.State.AssetHandleTable.ContainsKey("@inner"));
        Assert.True(vm.State.AssetHandleTable.ContainsKey("@outer"));

        // Exit outer scope — @outer disposed too.
        vm.ExitScopesUntil(0);
        Assert.True(outer.IsDisposed);
        Assert.False(vm.State.AssetHandleTable.ContainsKey("@outer"));
    }
}
