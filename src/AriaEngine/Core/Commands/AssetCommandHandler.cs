using AriaEngine.Assets;

namespace AriaEngine.Core.Commands;

/// <summary>
/// Phase 4.2: `load_aria_asset` opcode handler.
///
/// <para>
/// Reads an asset as raw bytes via <see cref="IAssetProvider"/> and stores the
/// resulting <see cref="AssetHandle{T}"/> in <c>GameState.AssetHandleTable</c>.
/// </para>
///
/// <para>Syntax:</para>
/// <code>
/// load_aria_asset &lt;path&gt; &lt;result_var&gt; [ownership]
/// </code>
///
/// <list type="bullet">
///   <item><c>path</c>      — asset path passed to <see cref="IAssetProvider.ReadAllBytes"/>.</item>
///   <item><c>result_var</c>— key into <c>GameState.AssetHandleTable</c>.</item>
///   <item><c>ownership</c> — <c>"owned"</c> (default), <c>"borrow"</c>, or <c>"move"</c>.
///     Only <c>owned</c> is fully wired in Phase 4.2; <c>borrow</c>/<c>move</c> are accepted
///     but stored as no-op entries with a warning (Phase 4.3 will add the type-checker
///     path).</item>
/// </list>
///
/// <para>
/// Phase 4.2 is a minimum viable implementation: bytes-only payload, no scope-exit
/// auto-dispose. Phase 4.3 will add type checker integration (Scope/borrow/move) and
/// auto-dispose of <c>owned</c> handles on scope exit. Phase 5 wires the
/// <see cref="AssetRegistry"/> from <c>Program.cs</c> and turns on the GC.
/// </para>
/// </summary>
public sealed class AssetCommandHandler : BaseCommandHandler
{
    private readonly IAssetProvider _assetProvider;
    private readonly AssetRegistry? _assetRegistry;

    public override IReadOnlySet<OpCode> HandledCodes { get; } = new HashSet<OpCode>
    {
        OpCode.LoadAsset
    };

    public AssetCommandHandler(
        VirtualMachine vm,
        IAssetProvider assetProvider,
        AssetRegistry? assetRegistry = null) : base(vm)
    {
        _assetProvider = assetProvider;
        _assetRegistry = assetRegistry;
    }

    public override bool Execute(Instruction inst)
    {
        switch (inst.Op)
        {
            case OpCode.LoadAsset:
                ExecuteLoadAsset(inst);
                return true;

            default:
                return false;
        }
    }

    private void ExecuteLoadAsset(Instruction inst)
    {
        if (!ValidateArgs(inst, 2)) return;

        string path = GetString(inst.Arguments[0]);
        string resultVar = inst.Arguments[1];
        string ownership = inst.Arguments.Count > 2
            ? GetString(inst.Arguments[2]).ToLowerInvariant()
            : "owned";

        if (string.IsNullOrWhiteSpace(resultVar))
        {
            Reporter.Report(new AriaError(
                "load_aria_asset: result variable name is empty",
                inst.SourceLine,
                CurrentScriptFile,
                AriaErrorLevel.Error,
                "ASSET_LOAD_INVALID_VAR"));
            return;
        }

        // Phase 4.2: only "owned" is fully wired.
        if (ownership != "owned" && ownership != "borrow" && ownership != "move")
        {
            Reporter.Report(new AriaError(
                $"load_aria_asset: unknown ownership '{ownership}' (expected: owned/borrow/move)",
                inst.SourceLine,
                CurrentScriptFile,
                AriaErrorLevel.Error,
                "ASSET_LOAD_INVALID_OWNERSHIP"));
            return;
        }

        if (ownership != "owned")
        {
            // borrow/move: accepted but not yet implemented in Phase 4.2.
            // Phase 4.3 will add scope-aware type-checker integration.
            Reporter.Report(new AriaError(
                $"load_aria_asset: ownership '{ownership}' is reserved for Phase 4.3; storing as no-op entry",
                inst.SourceLine,
                CurrentScriptFile,
                AriaErrorLevel.Warning,
                "ASSET_LOAD_OWNERSHIP_DEFERRED"));
            State.AssetHandleTable[resultVar] = null!;
            return;
        }

        if (!_assetProvider.Exists(path))
        {
            Reporter.Report(new AriaError(
                $"load_aria_asset: asset not found: '{path}'",
                inst.SourceLine,
                CurrentScriptFile,
                AriaErrorLevel.Warning,
                "ASSET_LOAD_MISSING"));
            State.AssetHandleTable[resultVar] = null!;
            return;
        }

        byte[] bytes;
        try
        {
            bytes = _assetProvider.ReadAllBytes(path);
        }
        catch (System.Exception ex)
        {
            Reporter.Report(new AriaError(
                $"load_aria_asset: failed to read '{path}': {ex.Message}",
                inst.SourceLine,
                CurrentScriptFile,
                AriaErrorLevel.Error,
                "ASSET_LOAD_READ_ERROR",
                exceptionType: ex.GetType().Name));
            State.AssetHandleTable[resultVar] = null!;
            return;
        }

        // Construct Owned handle. The registry (if wired) is notified so refcount
        // bookkeeping and eviction work; when registry is null (test runs), the
        // handle is a self-contained disposable that just frees on Dispose.
        var handle = new AssetHandle<byte[]>(
            _assetRegistry,
            path,
            bytes,
            AssetOwnership.Owned,
            bytes.Length);
        _assetRegistry?.RegisterHandle(handle);

        State.AssetHandleTable[resultVar] = handle;

        // Phase 4.3: track owned result_var in current scope for auto-dispose.
        // Mirrors RenderCommandHandler.TrackSpriteLifetime: the parser puts
        // declared `owned <storage_class> <var>` names into State.OwnedSprites,
        // and the handler checks ownership at creation time. On scope exit,
        // VirtualMachine.ExitScopesUntil disposes all handles in the set.
        if (State.OwnedSprites.Contains(resultVar)
            && State.Execution.AssetHandleLifetimeStacks.Count > 0)
        {
            State.Execution.AssetHandleLifetimeStacks.Peek().Add(resultVar);
        }
    }
}
