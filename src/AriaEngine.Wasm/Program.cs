using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using AriaEngine.Assets;
using AriaEngine.Core;
using AriaEngine.Runtime;
using AriaEngine.Scripting;

namespace AriaEngine.Wasm;

[SupportedOSPlatform("browser")]
public static partial class BrowserEntry
{
    private const string AssetRoot = "/aria-assets";
    private const string RuntimeRoot = "/aria-runtime";
    private static RaylibRuntimeHost? _host;
    private static BrowserStorageSynchronizer? _storage;
    private static double _lastTimestamp;

    public static async Task Main()
    {
        try
        {
            var http = new HttpClient { BaseAddress = new Uri(BrowserInterop.GetBaseUri()) };
            var loader = new WasmAssetGroupLoader(http, AssetRoot);
            await loader.InitializeAsync().ConfigureAwait(false);
            await loader.PreloadAsync("boot").ConfigureAwait(false);
            await loader.PreloadAsync("ui").ConfigureAwait(false);

            _storage = new BrowserStorageSynchronizer(RuntimeRoot);
            await _storage.RestoreAsync().ConfigureAwait(false);

            var provider = new UnifiedAssetProvider(
                AssetRoot,
                Array.Empty<string>(),
                patchPaths: null,
                locale: null,
                diskFirst: true);
            _host = new RaylibRuntimeHost(new RaylibRuntimeOptions
            {
                AssetProvider = provider,
                RunMode = RunMode.Dev,
                Profile = RuntimeProfile.Demo,
                InitPath = "init.aria",
                RuntimeDataRoot = RuntimeRoot,
                AssetGroupLoader = loader,
                UsePortableSaves = true,
                EnableLiveReload = false,
                ShowStartupSplash = false,
                SetWindowIcon = false,
                InitializeAudio = true,
                OwnAssetProvider = true,
                CheckWindowShouldClose = false,
                TargetFps = 0
            });
            _host.Initialize();
        }
        catch (Exception ex)
        {
            BrowserInterop.ShowFatal(ex.ToString());
        }
    }

    [JSExport]
    public static bool Frame(double timestampMilliseconds)
    {
        if (_host is null || !_host.IsInitialized || _host.ShouldClose) return false;
        float delta = _lastTimestamp <= 0
            ? 1f / 60f
            : (float)((timestampMilliseconds - _lastTimestamp) / 1000d);
        _lastTimestamp = timestampMilliseconds;
        _host.Update(delta);
        _host.Render();
        if (_storage is not null) _ = _storage.FlushAsync();
        return !_host.ShouldClose;
    }

    [JSExport]
    public static void Resize(int width, int height) => _host?.Resize(width, height);

    [JSExport]
    public static string GetRuntimeStatus()
    {
        if (_host is null || !_host.IsInitialized) return "Uninitialized;;;0;-1";
        GameState state = _host.VirtualMachine.State;
        return string.Join(
            ';',
            state.Execution.State,
            state.AssetPreload.GroupName,
            state.AssetPreload.IsFailed ? "failed" : "ok",
            state.AssetPreload.Attempt,
            state.Execution.ProgramCounter);
    }

    [JSExport]
    public static void Shutdown()
    {
        if (_storage is not null) _ = _storage.FlushAsync();
        _host?.Shutdown();
    }
}
