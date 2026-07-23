using AriaEngine.Assets;
using AriaEngine.Audio;
using AriaEngine.Core;
using AriaEngine.Input;
using AriaEngine.Rendering;
using AriaEngine.Scripting;
using AriaEngine.Utility;
using Raylib_cs;
using System.Numerics;

namespace AriaEngine.Runtime;

/// <summary>
/// Configuration for the shared desktop/browser Raylib runtime.
/// </summary>
public sealed class RaylibRuntimeOptions
{
    public required IAssetProvider AssetProvider { get; init; }
    public ErrorReporter? Reporter { get; init; }
    public RunMode RunMode { get; init; } = RunMode.Dev;
    public RuntimeProfile Profile { get; init; } = RuntimeProfile.Debug;
    public CompiledScriptBundle? CompiledBundle { get; init; }
    public string InitPath { get; init; } = "init.aria";
    public string? RuntimeDataRoot { get; init; }
    public IAssetGroupLoader? AssetGroupLoader { get; init; }
    public bool UsePortableSaves { get; init; }
    public bool EnableLiveReload { get; init; } = true;
    public bool ShowStartupSplash { get; init; } = true;
    public bool SetWindowIcon { get; init; } = true;
    public bool InitializeAudio { get; init; } = true;
    public bool OwnAssetProvider { get; init; }
    public bool CheckWindowShouldClose { get; init; } = true;
    public int TargetFps { get; init; } = 120;
}

/// <summary>
/// Owns the complete Raylib lifecycle and advances exactly one frame at a time.
/// Desktop drives it from a normal while loop; browser-wasm drives the same
/// methods from JavaScript requestAnimationFrame.
/// </summary>
public sealed class RaylibRuntimeHost : IDisposable
{
    private readonly RaylibRuntimeOptions _options;
    private readonly Dictionary<string, int> _frameErrorCounts = new(StringComparer.Ordinal);
    private readonly ErrorReporter _reporter;

    private SpriteRenderer? _renderer;
    private AudioManager? _audio;
    private VirtualMachine? _vm;
    private TweenManager? _tweens;
    private InputHandler? _input;
    private TransitionManager? _transition;
    private LiveReloadManager? _liveReload;
    private AssetRegistry? _assetRegistry;
    private bool _windowReady;
    private bool _audioReady;
    private bool _initialized;
    private bool _shutdown;
    private string _currentWindowTitle = "";

    public RaylibRuntimeHost(RaylibRuntimeOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        ArgumentNullException.ThrowIfNull(options.AssetProvider);
        _reporter = options.Reporter ?? new ErrorReporter();
    }

    public VirtualMachine VirtualMachine => _vm ?? throw new InvalidOperationException("Raylib runtime is not initialized.");
    public ErrorReporter Reporter => _reporter;
    public bool IsInitialized => _initialized && !_shutdown;

    public void Initialize()
    {
        if (_initialized) throw new InvalidOperationException("Raylib runtime is already initialized.");
        if (_shutdown) throw new ObjectDisposedException(nameof(RaylibRuntimeHost));

        try
        {
            Program.StartupTrace("runtime-host:start");
            StringHelper.InitializeStringBuilderPool(32, 256);

            string? runtimeRoot = string.IsNullOrWhiteSpace(_options.RuntimeDataRoot)
                ? null
                : Path.GetFullPath(_options.RuntimeDataRoot);
            if (runtimeRoot is not null) Directory.CreateDirectory(runtimeRoot);

            string configPath = runtimeRoot is null ? "config.json" : Path.Combine(runtimeRoot, "config.json");
            string persistentPath = runtimeRoot is null
                ? Path.Combine("saves", "persistent.ariasav")
                : Path.Combine(runtimeRoot, "saves", "persistent.ariasav");
            string savesPath = runtimeRoot is null ? "saves" : Path.Combine(runtimeRoot, "saves");

            var config = new ConfigManager(_reporter, configPath, persistentPath, _options.UsePortableSaves);
            SafeStartup("CONFIG_LOAD", config.Load, "config.jsonの読み込みに失敗しました。既定値で続行します。");

            if (_options.AssetProvider is UnifiedAssetProvider unified)
            {
                AssetGcConfig gc = config.Config.AssetGc;
                _assetRegistry = new AssetRegistry(
                    unified,
                    gc.TotalBudgetBytes,
                    TimeSpan.FromSeconds(Math.Max(1, gc.Gen1PromotionSeconds)),
                    TimeSpan.FromSeconds(Math.Max(1, gc.Gen2PromotionSeconds)))
                {
                    Enabled = gc.Enabled
                };
            }

            var parser = new Parser(_reporter);
            var loader = new ScriptLoader(parser, _options.AssetProvider, _options.RunMode, _options.CompiledBundle);
            var saves = new SaveManager(_reporter, savesPath, _options.UsePortableSaves);
            _tweens = new TweenManager();
            _vm = new VirtualMachine(
                _reporter,
                _tweens,
                saves,
                config,
                _options.AssetProvider,
                runtimeRoot,
                _assetRegistry,
                _options.AssetGroupLoader);

            if (_options.AssetProvider.Exists("assets/i18n/locales.json"))
            {
                _vm.Localization = LocalizationManager.Load(_options.AssetProvider, "assets/i18n/locales.json");
                _vm.Localization.SetLanguage(config.Config.Language);
                _vm.SyncLocalizationRuntimeState();
            }

            Program.ApplyRuntimeProfilePolicy(_vm.State.EngineSettings, _options.Profile);

            ParseResult init = Program.TryLoadScript(
                loader,
                parser,
                _options.InitPath,
                _options.AssetProvider,
                _options.RunMode,
                _reporter,
                fallbackMessage: "");
            if (init.Instructions.Count > 0)
            {
                _vm.LoadScript(init, _options.InitPath);
                while (_vm.State.Execution.State == VmState.Running &&
                       _vm.State.Execution.ProgramCounter < init.Instructions.Count)
                {
                    SafeStartup("VM_INIT_STEP", _vm.Step, "init.aria実行中にエラーが発生しました。可能な範囲で続行します。");
                }
            }

            Program.NormalizeWindowSettings(_vm.State, _reporter);
            Raylib.InitWindow(
                _vm.State.EngineSettings.WindowWidth,
                _vm.State.EngineSettings.WindowHeight,
                "AriaEngine");
            _windowReady = true;
            Raylib.SetExitKey((KeyboardKey)0);
            if (_options.SetWindowIcon)
            {
                SafeStartup(
                    "WINDOW_ICON",
                    () => Program.TrySetWindowIcon(_options.AssetProvider, _reporter),
                    "ウィンドウアイコンの設定に失敗しました。既定アイコンで続行します。");
            }

            _currentWindowTitle = _vm.State.EngineSettings.Title;
            Raylib.SetWindowTitle(_currentWindowTitle);
            if (_options.TargetFps > 0) Raylib.SetTargetFPS(_options.TargetFps);

            if (_options.InitializeAudio)
            {
                SafeStartup(
                    "AUDIO_INIT",
                    () =>
                    {
                        Raylib.InitAudioDevice();
                        _audioReady = true;
                    },
                    "音声デバイスの初期化に失敗しました。無音で続行します。");
            }

            if (_options.ShowStartupSplash)
            {
                Program.ShowStartupSplash(_options.AssetProvider, _reporter);
            }

            _renderer = new SpriteRenderer(_options.AssetProvider, _reporter);
            _input = new InputHandler();
            _audio = new AudioManager(_options.AssetProvider, _reporter);
            _vm.Audio = _audio;
            _transition = new TransitionManager();

            ParseResult main = Program.TryLoadScript(
                loader,
                parser,
                _vm.State.EngineSettings.MainScript,
                _options.AssetProvider,
                _options.RunMode,
                _reporter,
                $"Error: 指定されたスクリプト {_vm.State.EngineSettings.MainScript} が見つかりません。aria_error_ai.txtを確認してください。");

            _vm.FontReloadRequested += (fontPath, extraGlyphText) =>
            {
                _renderer.LoadFont(
                    fontPath ?? _vm.State.EngineSettings.FontPath,
                    _vm.State.EngineSettings.FontAtlasSize,
                    main.SourceLines,
                    _vm.State.EngineSettings.FontFilter,
                    extraGlyphText);
            };

            if (!string.IsNullOrWhiteSpace(_vm.State.EngineSettings.FontPath))
            {
                string? localeFontPath = _vm.Localization.GetFontForLanguage(_vm.Localization.CurrentLanguage);
                string fontPath = string.IsNullOrWhiteSpace(localeFontPath)
                    ? _vm.State.EngineSettings.FontPath
                    : localeFontPath;
                _renderer.LoadFont(
                    fontPath,
                    _vm.State.EngineSettings.FontAtlasSize,
                    main.SourceLines,
                    _vm.State.EngineSettings.FontFilter,
                    _vm.Localization.EnumerateTextForGlyphs());
            }
            else
            {
                _reporter.Report(new AriaError(
                    "フォントパスが未設定です。既定フォントで続行します。",
                    -1,
                    _options.InitPath,
                    AriaErrorLevel.Warning,
                    "BOOT_FONT_MISSING"));
            }

            _renderer.LoadUiFont("assets/fonts/NotoSansJP-Regular.ttf");
            _vm.LoadScript(main, _vm.State.EngineSettings.MainScript);
            _vm.SetIncludeResolver(path =>
            {
                ParseResult result = Program.TryLoadScript(
                    loader,
                    parser,
                    path,
                    _options.AssetProvider,
                    _options.RunMode,
                    _reporter,
                    fallbackMessage: "");
                return result.Instructions.Count > 0 ? result : null;
            });

            if (_options.EnableLiveReload &&
                _options.RunMode == RunMode.Dev &&
                _options.Profile == RuntimeProfile.Debug &&
                _options.AssetProvider is UnifiedAssetProvider liveUnified &&
                liveUnified.DiskProvider is DiskAssetProvider disk)
            {
                _liveReload = new LiveReloadManager(_vm, loader, _reporter, _renderer, disk.Root);
            }

            SafeFrame("vm.step.initial", _vm.Step);
            _initialized = true;
            Program.StartupTrace("runtime-host:initialized");
        }
        catch
        {
            Shutdown();
            throw;
        }
    }

    public bool ShouldClose => !_initialized || _shutdown ||
        (_options.CheckWindowShouldClose && Raylib.WindowShouldClose()) ||
        VirtualMachine.State.UiRuntime.RequestClose ||
        VirtualMachine.State.Execution.State == VmState.Ended;

    /// <summary>Advances one update tick. <paramref name="deltaTimeSeconds"/> is in seconds.</summary>
    public void Update(float deltaTimeSeconds)
    {
        EnsureReady();
        VirtualMachine vm = VirtualMachine;
        _liveReload?.Update();

        if (!string.Equals(_currentWindowTitle, vm.State.EngineSettings.Title, StringComparison.Ordinal))
        {
            Raylib.SetWindowTitle(vm.State.EngineSettings.Title);
            _currentWindowTitle = vm.State.EngineSettings.Title;
        }

        float dt = Math.Clamp(deltaTimeSeconds, 0f, 0.25f);
        float dtMs = dt * 1000f;
        SafeFrame("vm.update", () => vm.Update(dtMs));

        if (vm.State.Execution.State == VmState.WaitingForAssetGroup)
        {
            if (vm.State.AssetPreload.IsFailed && IsAssetRetryPressed())
            {
                SafeFrame("asset.retry", () => vm.RetryAssetPreload());
            }
        }
        else
        {
            SafeFrame("input.update", () => _input!.Update(vm));
            SafeFrame("menu.update", vm.Menu.Update);
        }

        if (_audioReady) SafeFrame("audio.update", () => _audio!.Update(vm.State));
        SafeFrame("transition.update", () => _transition!.Update(vm, dt));
        SafeFrame("particles.update", () => vm.Particles.Update(dt));
        SafeFrame("tweens.update", () => _tweens!.Update(vm.State, dtMs));

        if (!vm.Menu.IsOpen)
        {
            if (vm.State.Playback.SkipMode || vm.State.Playback.ForceSkipMode)
            {
                SafeFrame("vm.skip", () => vm.ProcessSkipFrame(dtMs));
            }
            else if (vm.State.Execution.State == VmState.Running)
            {
                SafeFrame("vm.step", vm.Step);
            }
        }
    }

    public void Render()
    {
        EnsureReady();
        VirtualMachine vm = VirtualMachine;
        Raylib.BeginDrawing();
        try
        {
            Raylib.ClearBackground(Color.Black);
            SafeFrame("renderer.draw", () => _renderer!.Draw(vm.State, _transition!));
            SafeFrame("renderer.click_cursor", () => _renderer!.DrawClickCursor(vm.State));
            SafeFrame("menu.draw", () => vm.Menu.Draw(_renderer!));
            SafeFrame("particles.draw", vm.Particles.Draw);
            if (vm.State.Execution.State == VmState.WaitingForAssetGroup)
            {
                DrawAssetPreloadOverlay(vm.State.AssetPreload);
            }
        }
        catch (Exception ex)
        {
            _reporter.ReportException(
                "FRAME_DRAW",
                ex,
                "描画フレームでエラーが発生しました。簡易表示で続行します。",
                AriaErrorLevel.Error);
            Raylib.ClearBackground(Color.Black);
            Raylib.DrawText("AriaEngine error - see aria_error_ai.txt", 20, 20, 20, Color.Red);
        }
        finally
        {
            Raylib.EndDrawing();
        }
    }

    public void Resize(int width, int height)
    {
        EnsureReady();
        if (width <= 0 || height <= 0) return;
        if (Raylib.GetScreenWidth() == width && Raylib.GetScreenHeight() == height) return;
        Raylib.SetWindowSize(width, height);
    }

    public void Shutdown()
    {
        if (_shutdown) return;
        _shutdown = true;

        SafeShutdown(() => _liveReload?.Dispose());
        SafeShutdown(() => _vm?.SavePersistentState());
        SafeShutdown(() => _reporter.WriteLogFile());
        SafeShutdown(() => _renderer?.Unload());
        SafeShutdown(() => _audio?.Unload());
        SafeShutdown(() => _assetRegistry?.Dispose());
        if (_audioReady) SafeShutdown(Raylib.CloseAudioDevice);
        if (_windowReady) SafeShutdown(Raylib.CloseWindow);
        if (_options.OwnAssetProvider && _options.AssetProvider is IDisposable disposable)
        {
            SafeShutdown(disposable.Dispose);
        }

        _audioReady = false;
        _windowReady = false;
        _initialized = false;
    }

    public void Dispose() => Shutdown();

    private void DrawAssetPreloadOverlay(AssetPreloadRuntimeState preload)
    {
        int width = Raylib.GetScreenWidth();
        int height = Raylib.GetScreenHeight();
        Raylib.DrawRectangle(0, 0, width, height, new Color(5, 6, 7, 225));
        const int titleSize = 28;
        const int bodySize = 18;
        string title = preload.IsFailed ? "ASSET DOWNLOAD FAILED" : "LOADING ASSETS";
        int titleX = Math.Max(24, (width - Raylib.MeasureText(title, titleSize)) / 2);
        Raylib.DrawText(title, titleX, Math.Max(40, height / 2 - 86), titleSize, Color.White);

        string group = $"group: {preload.GroupName}";
        int groupX = Math.Max(24, (width - Raylib.MeasureText(group, bodySize)) / 2);
        Raylib.DrawText(group, groupX, Math.Max(78, height / 2 - 40), bodySize, new Color(200, 200, 200, 255));

        if (!preload.IsFailed) return;

        string retry = "CLICK OR PRESS R / ENTER TO RETRY";
        Rectangle button = GetRetryButton(width, height);
        Vector2 mouse = Raylib.GetMousePosition();
        bool hovered = Raylib.CheckCollisionPointRec(mouse, button);
        Raylib.DrawRectangleRec(button, hovered ? new Color(64, 74, 68, 255) : new Color(35, 42, 39, 255));
        Raylib.DrawRectangleLinesEx(button, 1f, new Color(200, 200, 200, 255));
        int retryX = (int)button.X + Math.Max(12, ((int)button.Width - Raylib.MeasureText(retry, bodySize)) / 2);
        Raylib.DrawText(retry, retryX, (int)button.Y + 14, bodySize, Color.White);
    }

    private static Rectangle GetRetryButton(int width, int height) =>
        new(Math.Max(24, width / 2 - 210), Math.Max(120, height / 2 + 8), 420, 52);

    private static bool IsAssetRetryPressed()
    {
        if (Raylib.IsKeyPressed(KeyboardKey.R) ||
            Raylib.IsKeyPressed(KeyboardKey.Enter) ||
            Raylib.IsKeyPressed(KeyboardKey.Space))
        {
            return true;
        }

        return Raylib.IsMouseButtonPressed(MouseButton.Left) &&
               Raylib.CheckCollisionPointRec(
                   Raylib.GetMousePosition(),
                   GetRetryButton(Raylib.GetScreenWidth(), Raylib.GetScreenHeight()));
    }

    private void EnsureReady()
    {
        if (!_initialized || _shutdown) throw new InvalidOperationException("Raylib runtime is not initialized.");
    }

    private void SafeStartup(string code, Action action, string message)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            _reporter.ReportException(code, ex, message, AriaErrorLevel.Error);
        }
    }

    private void SafeFrame(string key, Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            _frameErrorCounts.TryGetValue(key, out int count);
            _frameErrorCounts[key] = count + 1;
            if (count is 0 or 59 or 599)
            {
                _reporter.ReportException(
                    $"FRAME_{key.Replace('.', '_').ToUpperInvariant()}",
                    ex,
                    $"{key} でフレーム例外が発生しました。処理をスキップして続行します。発生回数: {count + 1}",
                    AriaErrorLevel.Error);
            }
        }
    }

    private static void SafeShutdown(Action action)
    {
        try
        {
            action();
        }
        catch
        {
            // Shutdown must remain best effort.
        }
    }
}
