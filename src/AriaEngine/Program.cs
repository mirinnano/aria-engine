using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using Raylib_cs;
using AriaEngine.Core;
using AriaEngine.Rendering;
using AriaEngine.Input;
using AriaEngine.Audio;
using AriaEngine.Utility;
using AriaEngine.Assets;
using AriaEngine.Scripting;
using AriaEngine.Tools;



namespace AriaEngine;

class Program
{
    private static readonly Dictionary<string, int> FrameErrorCounts = new(StringComparer.Ordinal);
    private const string StartupSplashLogoPath = "assets/branding/ponkotu-splash.png";
    private const double StartupSplashSeconds = 2.8;
    private const double StartupSplashFadeInSeconds = 0.55;
    private const double StartupSplashHoldSeconds = 1.35;
    private const double StartupSplashFadeOutSeconds = 0.65;
    private const double StartupSplashSkipFadeSeconds = 0.25;
    private const string WindowIconPath = "assets/branding/umikaze-icon-master.png";
    private static readonly string[] DefaultBrowserOpenAllowlist =
    {
        "store.steampowered.com",
        "twitter.com",
        "x.com",
        "ponkotsu-soft.vercel.app"
    };

    internal sealed class RunOptions
    {
        public RunMode Mode { get; set; } = RunMode.Dev;
        public RuntimeProfile Profile { get; set; } = RuntimeProfile.Debug;
        public bool ProfileExplicit { get; set; }
        public string InitPath { get; set; } = "init.aria";
        public string? PakPath { get; set; }
        public string? Key { get; set; } = Environment.GetEnvironmentVariable("ARIA_PACK_KEY");
        public string CompiledPath { get; set; } = "scripts/scripts.ariac";
    }

    [STAThread]
    static void Main(string[] args)
    {


        if (args.Length > 0 && args[0].Equals("aria-doc", StringComparison.OrdinalIgnoreCase))
        {
            Environment.ExitCode = AriaDocCommand.Run(args[1..]);
            return;
        }

        if (args.Length > 0 && args[0].Equals("aria-compile", StringComparison.OrdinalIgnoreCase))
        {
            Environment.ExitCode = AriaCompileCommand.Run(args[1..]);
            return;
        }



if (args.Length > 0 && args[0].Equals("aria-pack", StringComparison.OrdinalIgnoreCase))
        {
            Environment.ExitCode = AriaPackCommand.Run(args[1..]);
            return;
        }

        if (args.Length > 0 && args[0].Equals("aria-lint", StringComparison.OrdinalIgnoreCase))
        {
            Environment.ExitCode = AriaLintCommand.Run(args[1..]);
            return;
        }

        if (args.Length > 0 && args[0].Equals("aria-i18n-check", StringComparison.OrdinalIgnoreCase))
        {
            Environment.ExitCode = AriaI18nCheckCommand.Run(args[1..]);
            return;
        }

        if (args.Length > 0 && args[0].Equals("aria-flowcheck", StringComparison.OrdinalIgnoreCase))
        {
            Environment.ExitCode = AriaFlowCheckCommand.Run(args[1..]);
            return;
        }

        if (args.Length > 0 && args[0].Equals("aria-format", StringComparison.OrdinalIgnoreCase))
        {
            Environment.ExitCode = AriaFormatCommand.Run(args[1..]);
            return;
        }

        if (args.Length > 0 && args[0].Equals("aria-save", StringComparison.OrdinalIgnoreCase))
        {
            Environment.ExitCode = AriaSaveCommand.Run(args[1..]);
            return;
        }

        var reporter = new ErrorReporter();
        SpriteRenderer? renderer = null;
        AudioManager? audio = null;
        VirtualMachine? vmForShutdown = null;
        LiveReloadManager? liveReload = null;
        bool windowReady = false;
        bool audioReady = false;
        // Pak v3 redesign, Phase 5.2: declared at outer scope so the
        // finally block can dispose it even if construction throws.
        AriaEngine.Assets.AssetRegistry? assetRegistry = null;

        try
        {
            StartupTrace("start");
            RunOptions runOptions = ParseRunOptions(args, reporter);
            StartupTrace("options");

            // Auto-detect: if v3 split paks or data.pak exists next to exe, use Release mode automatically
            if (runOptions.Mode == RunMode.Dev && string.IsNullOrWhiteSpace(runOptions.PakPath))
            {
                string exeDir = AppDomain.CurrentDomain.BaseDirectory;
                string autoPak = Path.Combine(exeDir, "data.pak");
                string autoCompiled = Path.Combine(exeDir, "scripts", "scripts.ariac");
                string keyFile = Path.Combine(exeDir, "aria.key");

                // v3 split auto-detection
                string[] v3Files = new[] { "boot.arib", "scenario.aris", "data.arid", "stream.arim", "voice.ariv" };
                bool v3Detected = v3Files.Any(f => File.Exists(Path.Combine(exeDir, f)));

                if (v3Detected || File.Exists(autoPak))
                {
                    runOptions.Mode = RunMode.Release;
                    if (File.Exists(autoPak)) runOptions.PakPath = "data.pak";
                    if (File.Exists(autoCompiled)) runOptions.CompiledPath = "scripts/scripts.ariac";
                    if (File.Exists(keyFile) && string.IsNullOrEmpty(runOptions.Key))
                    {
                        runOptions.Key = File.ReadAllText(keyFile).Trim();
                    }
                    StartupTrace(v3Detected ? "auto-release: v3 split paks detected" : "auto-release: data.pak detected");
                }
            }
            if (!runOptions.ProfileExplicit && runOptions.Mode == RunMode.Release)
            {
                runOptions.Profile = RuntimeProfile.Release;
            }

            // StringBuilderプールの初期化（パフォーマンス最適化）
            StringHelper.InitializeStringBuilderPool(32, 256);

            var parser = new Parser(reporter);

            var configParams = new ConfigManager(reporter);
            SafeStartup("CONFIG_LOAD", () => configParams.Load(), reporter, "config.jsonの読み込みに失敗しました。既定値で続行します。");

            IAssetProvider assetProvider = CreateAssetProvider(runOptions, reporter);
            StartupTrace("asset-provider");

            // Pak v3 redesign, Phase 5.2: create the AssetRegistry that
            // backs `load_aria_asset` and the refcount/generational GC.
            // Wraps the same UnifiedAssetProvider the rest of the engine
            // uses, so the registry sees the same on-disk / in-pak bytes
            // the renderer / parser see. Settings come from config.json's
            // AssetGc section; Enabled defaults to false for staged rollout.
            if (assetProvider is UnifiedAssetProvider unified)
            {
                var gcCfg = configParams.Config.AssetGc;
                assetRegistry = new AssetRegistry(
                    provider: unified,
                    totalBudgetBytes: gcCfg.TotalBudgetBytes,
                    gen1Promotion: TimeSpan.FromSeconds(Math.Max(1, gcCfg.Gen1PromotionSeconds)),
                    gen2Promotion: TimeSpan.FromSeconds(Math.Max(1, gcCfg.Gen2PromotionSeconds)));
                assetRegistry.Enabled = gcCfg.Enabled;
            }

            CompiledScriptBundle? compiledBundle = TryLoadCompiledBundle(assetProvider, runOptions, reporter);
            // v3 split pak stores .aria scripts directly in scenario.aris; compiled bundle is optional
            RunMode effectiveMode = runOptions.Mode == RunMode.Release ? RunMode.Release : RunMode.Dev;
            StartupTrace("compiled-bundle");
            if (runOptions.Mode == RunMode.Release && compiledBundle is null)
            {
                reporter.Report(new AriaError(
                    "release実行にコンパイル済みスクリプトバンドルがありません。v3 split pakではscenario.aris内の平文.ariaを直接使用します。",
                    level: AriaErrorLevel.Warning,
                    code: "BOOT_RELEASE_NO_COMPILEDBUNDLE",
                    hint: "v3 split pakではscripts.ariacは不要です。scenario.arisに.ariaスクリプトが含まれていることを確認してください。"));
            }

            StartupTrace("before-script-loader");
            var scriptLoader = new ScriptLoader(parser, assetProvider, effectiveMode, compiledBundle);

            StartupTrace("before-runtime-state");
            var saves = new SaveManager(reporter);
            var tweens = new TweenManager();
            var vm = new VirtualMachine(reporter, tweens, saves, configParams, assetProvider, runtimeDataRoot: null, assetRegistry: assetRegistry);
            StartupTrace("after-runtime-state");
            if (assetProvider.Exists("assets/i18n/locales.json"))
            {
                StartupTrace("before-localization");
                vm.Localization = LocalizationManager.Load(assetProvider, "assets/i18n/locales.json");
                StartupTrace("localization-loaded");
                vm.Localization.SetLanguage(configParams.Config.Language);
                vm.SyncLocalizationRuntimeState();
                StartupTrace("localization-synced");
            }
            vmForShutdown = vm;

            ApplyRuntimeProfilePolicy(vm.State.EngineSettings, runOptions);

            string initScriptPath = runOptions.InitPath;
            StartupTrace("before-init-load");
            var initLoaded = TryLoadScript(scriptLoader, parser, initScriptPath, assetProvider, effectiveMode, reporter, fallbackMessage: "");
            StartupTrace($"after-init-load {initLoaded.Instructions.Count}");
            if (initLoaded.Instructions.Count > 0)
            {
                vm.LoadScript(initLoaded, initScriptPath);
                StartupTrace("init-loaded");

                while (vm.State.Execution.State == VmState.Running && vm.State.Execution.ProgramCounter < initLoaded.Instructions.Count)
                {
                    SafeStartup("VM_INIT_STEP", vm.Step, reporter, "init.aria実行中にエラーが発生しました。可能な範囲で続行します。");
                }
                StartupTrace("init-executed");
            }

            NormalizeWindowSettings(vm.State, reporter);
            StartupTrace($"before-window {vm.State.EngineSettings.WindowWidth}x{vm.State.EngineSettings.WindowHeight} title={vm.State.EngineSettings.Title}");
            Raylib.InitWindow(vm.State.EngineSettings.WindowWidth, vm.State.EngineSettings.WindowHeight, "AriaEngine");
            SafeStartup("WINDOW_ICON", () => TrySetWindowIcon(assetProvider, reporter), reporter, "ウィンドウアイコンの設定に失敗しました。既定アイコンで続行します。");
            Raylib.SetExitKey((KeyboardKey)0);
            windowReady = true;
            StartupTrace("after-window");
            string currentWindowTitle = vm.State.EngineSettings.Title;
            Raylib.SetWindowTitle(currentWindowTitle);
            Raylib.SetTargetFPS(120);
            StartupTrace("before-audio");
            SafeStartup("AUDIO_INIT", () => { Raylib.InitAudioDevice(); audioReady = true; }, reporter, "音声デバイスの初期化に失敗しました。無音で続行します。");
            StartupTrace("after-audio");
            ShowStartupSplash(assetProvider, reporter);
            StartupTrace("startup-splash");

            renderer = new SpriteRenderer(assetProvider, reporter);
            StartupTrace("renderer");
            var input = new InputHandler();
            audio = new AudioManager(assetProvider, reporter);
            vm.Audio = audio;
            var transition = new TransitionManager();
            StartupTrace("managers");

            var loaded = TryLoadScript(
                scriptLoader,
                parser,
                vm.State.EngineSettings.MainScript,
                assetProvider,
                effectiveMode,
                reporter,
                fallbackMessage: $"Error: 指定されたスクリプト {vm.State.EngineSettings.MainScript} が見つかりません。aria_error_ai.txtを確認してください。");

            vm.FontReloadRequested += (fontPath, extraGlyphText) =>
            {
                renderer!.LoadFont(
                    fontPath,
                    vm.State.EngineSettings.FontAtlasSize,
                    loaded.SourceLines,
                    vm.State.EngineSettings.FontFilter,
                    extraGlyphText);
            };

            if (!string.IsNullOrEmpty(vm.State.EngineSettings.FontPath))
            {
                StartupTrace("before-font");
                string? localeFontPath = vm.Localization.GetFontForLanguage(vm.Localization.CurrentLanguage);
                string fontPath = string.IsNullOrWhiteSpace(localeFontPath)
                    ? vm.State.EngineSettings.FontPath
                    : localeFontPath;
                renderer.LoadFont(
                    fontPath,
                    vm.State.EngineSettings.FontAtlasSize,
                    loaded.SourceLines,
                    vm.State.EngineSettings.FontFilter,
                    vm.Localization.EnumerateTextForGlyphs());
                StartupTrace("after-font");
            }
            else
            {
                reporter.Report(new AriaError("フォントパスが未設定です。既定フォントで続行します。", -1, initScriptPath, AriaErrorLevel.Warning, "BOOT_FONT_MISSING"));
            }

            StartupTrace("before-ui-font");
            renderer.LoadUiFont("assets/fonts/NotoSansJP-Regular.ttf");
            StartupTrace("after-ui-font");

            vm.LoadScript(loaded, vm.State.EngineSettings.MainScript);
            StartupTrace("main-loaded");

            // 動的 include 用のリゾルバを登録（スクリプト内で include "path" を使えるように）
            vm.SetIncludeResolver(path =>
            {
                var result = TryLoadScript(scriptLoader, parser, path, assetProvider, effectiveMode, reporter, fallbackMessage: "");
                return result.Instructions.Count > 0 ? result : null;
            });

            // 開発モードかつディスクロード時のみライブリロードを有効化
            // (Phase 5.2: assetProvider is now always UnifiedAssetProvider.
            // The disk root is exposed via .DiskProvider for live-reload.)
            if (effectiveMode == RunMode.Dev &&
                runOptions.Profile == RuntimeProfile.Debug &&
                assetProvider is UnifiedAssetProvider unifiedProvider &&
                unifiedProvider.DiskProvider is DiskAssetProvider diskProvider)
            {
                liveReload = new LiveReloadManager(vm, scriptLoader, reporter, renderer, diskProvider.Root);
            }

            StartupTrace("before-initial-step");
            SafeFrame("vm.step.initial", vm.Step, reporter);
            StartupTrace("initial-step");

            while (!Raylib.WindowShouldClose())
            {
                liveReload?.Update();

                if (vm.State.UiRuntime.RequestClose || vm.State.Execution.State == VmState.Ended) break;
                if (!string.Equals(currentWindowTitle, vm.State.EngineSettings.Title, StringComparison.Ordinal))
                {
                    Raylib.SetWindowTitle(vm.State.EngineSettings.Title);
                    currentWindowTitle = vm.State.EngineSettings.Title;
                }

                float dt = Raylib.GetFrameTime();
                float dtMs = dt * 1000f;

                SafeFrame("vm.update", () => vm.Update(dtMs), reporter);
                SafeFrame("input.update", () => input.Update(vm), reporter);
                SafeFrame("menu.update", vm.Menu.Update, reporter);
                if (audioReady && audio is not null) SafeFrame("audio.update", () => audio.Update(vm.State), reporter);
                SafeFrame("transition.update", () => transition.Update(vm, dt), reporter);
                SafeFrame("particles.update", () => vm.Particles.Update(dt), reporter);
                SafeFrame("tweens.update", () => tweens.Update(vm.State, dtMs), reporter);

                if (!vm.Menu.IsOpen)
                {
                    if (vm.State.Playback.SkipMode || vm.State.Playback.ForceSkipMode)
                    {
                        SafeFrame("vm.skip", () => vm.ProcessSkipFrame(dtMs), reporter);
                    }
                    else if (vm.State.Execution.State == VmState.Running)
                    {
                        SafeFrame("vm.step", vm.Step, reporter);
                    }
                }

                Raylib.BeginDrawing();
                try
                {
                    Raylib.ClearBackground(Color.Black);

                    SafeFrame("renderer.draw", () => renderer.Draw(vm.State, transition), reporter);
                    SafeFrame("renderer.click_cursor", () => renderer.DrawClickCursor(vm.State), reporter);
                    SafeFrame("menu.draw", () => vm.Menu.Draw(renderer), reporter);
                    SafeFrame("particles.draw", vm.Particles.Draw, reporter);
                }
                catch (Exception ex)
                {
                    reporter.ReportException("FRAME_DRAW", ex, "描画フレームでエラーが発生しました。簡易表示で続行します。", AriaErrorLevel.Error);
                    Raylib.ClearBackground(Color.Black);
                    Raylib.DrawText("AriaEngine error - see aria_error_ai.txt", 20, 20, 20, Color.Red);
                }
                finally
                {
                    Raylib.EndDrawing();
                }
            }
        }
        catch (Exception ex)
        {
            reporter.ReportException("BOOT_UNHANDLED", ex, "未処理例外を捕捉しました。可能な限りログを書き出して終了します。", AriaErrorLevel.Fatal);
            if (windowReady)
            {
                try
                {
                    Raylib.BeginDrawing();
                    Raylib.ClearBackground(Color.Black);
                    Raylib.DrawText("AriaEngine fatal error - see aria_error_ai.txt", 20, 20, 20, Color.Red);
                    Raylib.EndDrawing();
                }
                catch
                {
                    // 最終防衛線。ここではログ書き出しを優先する。
                }
            }
        }
        finally
        {
            SafeShutdown(() => liveReload?.Dispose());
            SafeShutdown(() => vmForShutdown?.SavePersistentState());
            reporter.WriteLogFile();

            SafeShutdown(() => renderer?.Unload());
            SafeShutdown(() => audio?.Unload());
            // Pak v3 redesign, Phase 5.2: stop the registry's background
            // sweeper timer before tearing down the window. Sweep logs
            // stay in the reporter's error log.
            SafeShutdown(() => assetRegistry?.Dispose());
            if (audioReady) SafeShutdown(Raylib.CloseAudioDevice);
            if (windowReady) SafeShutdown(Raylib.CloseWindow);
        }
    }

    internal static RunOptions ParseRunOptions(string[] args, ErrorReporter reporter)
    {
        var options = new RunOptions();
        int i = 0;
        while (i < args.Length)
        {
            string arg = args[i];
            switch (arg)
            {
                case "--run-mode":
                    i++;
                    if (i < args.Length) options.Mode = string.Equals(args[i], "release", StringComparison.OrdinalIgnoreCase) ? RunMode.Release : RunMode.Dev;
                    break;
                case "--profile":
                    i++;
                    if (i < args.Length)
                    {
                        options.ProfileExplicit = true;
                        options.Profile = ParseRuntimeProfile(args[i], reporter);
                    }
                    break;
                case "--pak":
                    i++;
                    if (i < args.Length) options.PakPath = args[i];
                    break;
                case "--key":
                    i++;
                    if (i < args.Length) options.Key = args[i];
                    break;
                case "--compiled":
                    i++;
                    if (i < args.Length) options.CompiledPath = args[i];
                    break;

                case "--init":
                    i++;
                    if (i < args.Length) options.InitPath = args[i];
                    break;
                default:
                    if (!arg.StartsWith("--", StringComparison.Ordinal) && string.Equals(options.InitPath, "init.aria", StringComparison.OrdinalIgnoreCase))
                    {
                        options.InitPath = arg;
                    }
                    break;
            }
            i++;
        }

        if (options.Mode == RunMode.Release &&
            string.IsNullOrWhiteSpace(options.PakPath) &&
            !HasDistributionPakInBaseDirectory())
        {
            reporter.Report(new AriaError(
                "--run-mode release に --pak が指定されていません。dev相当のディスクロードへフォールバックします。",
                level: AriaErrorLevel.Warning,
                code: "BOOT_RELEASE_NO_PAK",
                hint: "販売版起動では --pak build/data.pak を指定してください。"));
            options.Mode = RunMode.Dev;
        }

        return options;
    }

    private static RuntimeProfile ParseRuntimeProfile(string value, ErrorReporter reporter)
    {
        return value.Trim().ToLowerInvariant() switch
        {
            "debug" or "dev" => RuntimeProfile.Debug,
            "demo" => RuntimeProfile.Demo,
            "release" or "prod" or "production" => RuntimeProfile.Release,
            _ => WarnAndDefaultProfile(value, reporter)
        };
    }

    private static RuntimeProfile WarnAndDefaultProfile(string value, ErrorReporter reporter)
    {
        reporter.Report(new AriaError(
            $"unknown runtime profile '{value}'. Falling back to Debug.",
            level: AriaErrorLevel.Warning,
            code: "BOOT_PROFILE_UNKNOWN",
            hint: "Use --profile debug, --profile demo, or --profile release."));
        return RuntimeProfile.Debug;
    }

    internal static void ApplyRuntimeProfilePolicy(EngineSettingsState settings, RunOptions options)
    {
        settings.RuntimeProfile = options.Profile;
        settings.ProductionMode = options.Profile != RuntimeProfile.Debug;
        settings.BrowserOpenAllowlist.Clear();
        if (settings.ProductionMode)
        {
            settings.BrowserOpenAllowlist.AddRange(DefaultBrowserOpenAllowlist);
        }
    }

    private static bool HasDistributionPakInBaseDirectory()
    {
        string exeDir = AppDomain.CurrentDomain.BaseDirectory;
        string[] v3Files = new[] { "boot.arib", "scenario.aris", "data.arid", "stream.arim", "voice.ariv" };
        return v3Files.Any(file => File.Exists(Path.Combine(exeDir, file))) ||
               File.Exists(Path.Combine(exeDir, "data.pak"));
    }

    private static IAssetProvider CreateAssetProvider(RunOptions options, ErrorReporter reporter)
    {
        // Pak v3 redesign, Phase 5.2: every boot path now flows through
        // UnifiedAssetProvider so the AssetRegistry (Phase 3) can attach
        // to a single normalized provider. Dev mode = diskFirst=true,
        // release mode = diskFirst=false (pak wins, disk acts as patch).
        // Returns IAssetProvider for backward compatibility with all
        // downstream callers; the underlying type is UnifiedAssetProvider.
        if (options.Mode == RunMode.Release)
        {
            string exeDir = AppDomain.CurrentDomain.BaseDirectory;
            string[] v3Candidates = new[]
            {
                Path.Combine(exeDir, "boot.arib"),
                Path.Combine(exeDir, "scenario.aris"),
                Path.Combine(exeDir, "data.arid"),
                Path.Combine(exeDir, "stream.arim"),
                Path.Combine(exeDir, "voice.ariv")
            };
            string[] v3Paks = v3Candidates.Where(File.Exists).ToArray();

            if (v3Paks.Length > 0)
            {
                try
                {
                    // Verify the paks are readable before wrapping them.
                    // PakAssetProviderV3 is IDisposable, but legacy
                    // PakAssetProvider is not — call Exists() as a cheap
                    // open-check that throws on a bad key/path.
                    using (new PakAssetProviderV3(v3Paks, options.Key)) { }
                    return new UnifiedAssetProvider(
                        diskRoot: null,
                        pakPaths: v3Paks,
                        patchPaths: null,
                        locale: null,
                        diskFirst: false);
                }
                catch (Exception ex)
                {
                    reporter.ReportException(
                        "BOOT_V3_PAK_OPEN",
                        ex,
                        "v3 split Pak の読み込みに失敗しました。",
                        AriaErrorLevel.Error,
                        hint: "Pakのパス、改ざん、ARIA_PACK_KEYを確認してください。");
                }
            }

            // Legacy v2 single-pak fallback
            if (!string.IsNullOrWhiteSpace(options.PakPath))
            {
                string pakPath = ResolveDistributionPath(options.PakPath);
                try
                {
                    // PakAssetProvider is not IDisposable; touching a
                    // missing path throws FileNotFoundException. Probe
                    // existence first to keep the try/catch cheap.
                    if (!File.Exists(pakPath))
                        throw new FileNotFoundException($"Pak not found: {pakPath}", pakPath);
                    return new UnifiedAssetProvider(
                        diskRoot: null,
                        pakPaths: new[] { pakPath },
                        patchPaths: null,
                        locale: null,
                        diskFirst: false);
                }
                catch (Exception ex)
                {
                    reporter.ReportException(
                        "BOOT_PAK_OPEN",
                        ex,
                        $"Pak '{pakPath}' を開けませんでした。ディスク assets からのロードへフォールバックします。",
                        AriaErrorLevel.Error,
                        hint: "Pakのパス、改ざん、ARIA_PACK_KEYを確認してください。");
                }
            }
        }

        // Dev mode (or release with no pak found): filesystem only.
        return new UnifiedAssetProvider(
            diskRoot: Directory.GetCurrentDirectory(),
            pakPaths: Array.Empty<string>(),
            patchPaths: null,
            locale: null,
            diskFirst: true);
    }

    private static string ResolveDistributionPath(string path)
    {
        if (Path.IsPathFullyQualified(path))
        {
            return path;
        }

        string appBaseCandidate = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, path));
        if (File.Exists(appBaseCandidate))
        {
            return appBaseCandidate;
        }

        return Path.GetFullPath(path);
    }

    private static CompiledScriptBundle? TryLoadCompiledBundle(IAssetProvider provider, RunOptions options, ErrorReporter reporter)
    {
        if (options.Mode != RunMode.Release) return null;

        try
        {
            using var compiledStream = provider.OpenRead(options.CompiledPath);
            return CompiledBundleCodec.Load(compiledStream, options.Key);
        }
        catch (Exception ex)
        {
            // v3 split fallback: compiled script may be stored as assets/scripts/scripts.ariac in scenario.aris
            if (!options.CompiledPath.StartsWith("assets/", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    string fallbackPath = "assets/" + options.CompiledPath;
                    using var compiledStream = provider.OpenRead(fallbackPath);
                    return CompiledBundleCodec.Load(compiledStream, options.Key);
                }
                catch { /* ignore fallback failure, report original error */ }
            }

            try
            {
                string compiledDiskPath = ResolveDistributionPath(options.CompiledPath);
                if (File.Exists(compiledDiskPath))
                {
                    using var compiledStream = File.OpenRead(compiledDiskPath);
                    return CompiledBundleCodec.Load(compiledStream, options.Key);
                }
            }
            catch { /* ignore disk fallback failure, report original error */ }

            reporter.ReportException(
                "BOOT_COMPILED_LOAD",
                ex,
                $"コンパイル済みスクリプト '{options.CompiledPath}' を読み込めませんでした。",
                AriaErrorLevel.Error,
                hint: "aria-compile/aria-packの出力、Pakマニフェスト、暗号キーを確認してください。");
            return null;
        }
    }

    private static ParseResult TryLoadScript(
        ScriptLoader loader,
        Parser parser,
        string path,
        IAssetProvider provider,
        RunMode mode,
        ErrorReporter reporter,
        string fallbackMessage)
    {
        try
        {
            if (mode == RunMode.Release || provider.Exists(path))
            {
                return loader.LoadScript(path);
            }

            throw new FileNotFoundException($"Script not found: {path}", path);
        }
        catch (Exception ex)
        {
            reporter.ReportException(
                "SCRIPT_LOAD",
                ex,
                $"スクリプト '{path}' を読み込めませんでした。",
                AriaErrorLevel.Error,
                path,
                hint: "include/script指定、Pak収録、ファイル名の大文字小文字を確認してください。");

            if (string.IsNullOrWhiteSpace(fallbackMessage))
            {
                return new ParseResult
                {
                    Instructions = new List<Instruction>(),
                    Labels = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
                    Functions = new List<FunctionInfo>(),
                    Structs = new List<StructDefinition>(),
                    SourceLines = Array.Empty<string>()
                };
            }

            string[] lines = { $"text \"{fallbackMessage.Replace("\"", "'")}\"", "@" };
            var parsed = parser.Parse(lines, path);
            return parsed;
        }
    }

    private static void SafeStartup(string code, Action action, ErrorReporter reporter, string message)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            reporter.ReportException(code, ex, message, AriaErrorLevel.Error);
        }
    }

    private static void SafeFrame(string key, Action action, ErrorReporter reporter)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            FrameErrorCounts.TryGetValue(key, out int count);
            FrameErrorCounts[key] = count + 1;
            if (count is 0 or 59 or 599)
            {
                reporter.ReportException(
                    $"FRAME_{key.Replace('.', '_').ToUpperInvariant()}",
                    ex,
                    $"{key} でフレーム例外が発生しました。処理をスキップして続行します。発生回数: {count + 1}",
                    AriaErrorLevel.Error);
            }
        }
    }

    private static void NormalizeWindowSettings(GameState state, ErrorReporter reporter)
    {
        const int fallbackWidth = 1280;
        const int fallbackHeight = 720;
        if (state.EngineSettings.WindowWidth < 320 || state.EngineSettings.WindowHeight < 240 || state.EngineSettings.WindowWidth > 7680 || state.EngineSettings.WindowHeight > 4320)
        {
            reporter.Report(new AriaError(
                $"window指定が不正です: {state.EngineSettings.WindowWidth}x{state.EngineSettings.WindowHeight}。{fallbackWidth}x{fallbackHeight}で起動します。",
                level: AriaErrorLevel.Warning,
                code: "BOOT_WINDOW_SIZE_INVALID"));
            state.EngineSettings.WindowWidth = fallbackWidth;
            state.EngineSettings.WindowHeight = fallbackHeight;
        }
    }

    private static void TrySetWindowIcon(IAssetProvider assetProvider, ErrorReporter reporter)
    {
        if (!assetProvider.Exists(WindowIconPath))
        {
            reporter.Report(new AriaError(
                $"ウィンドウアイコン '{WindowIconPath}' が見つかりません。",
                level: AriaErrorLevel.Warning,
                code: "BOOT_WINDOW_ICON_MISSING"));
            return;
        }

        if (!assetProvider.CanMaterializeToFile)
        {
            reporter.Report(new AriaError(
                "現在のasset providerではウィンドウアイコンを一時ファイル化できません。",
                level: AriaErrorLevel.Warning,
                code: "BOOT_WINDOW_ICON_UNSUPPORTED"));
            return;
        }

        Image icon = default;
        try
        {
            string iconPath = assetProvider.MaterializeToFile(WindowIconPath);
            icon = Raylib.LoadImage(iconPath);
            if (icon.Width <= 0 || icon.Height <= 0)
            {
                reporter.Report(new AriaError(
                    $"ウィンドウアイコン '{WindowIconPath}' の読み込み結果が無効です。",
                    level: AriaErrorLevel.Warning,
                    code: "BOOT_WINDOW_ICON_INVALID"));
                return;
            }

            Raylib.SetWindowIcon(icon);
        }
        finally
        {
            if (icon.Width > 0 && icon.Height > 0)
            {
                Raylib.UnloadImage(icon);
            }
        }
    }

    private static void ShowStartupSplash(IAssetProvider assetProvider, ErrorReporter reporter)
    {
        Texture2D logo = default;
        string? versionText = System.Reflection.Assembly.GetExecutingAssembly()
            .GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        try
        {
            if (!assetProvider.Exists(StartupSplashLogoPath))
            {
                reporter.Report(new AriaError(
                    $"起動ロゴ '{StartupSplashLogoPath}' が見つかりません。",
                    level: AriaErrorLevel.Warning,
                    code: "BOOT_SPLASH_LOGO_MISSING"));
                return;
            }

            string logoPath = assetProvider.MaterializeToFile(StartupSplashLogoPath);
            logo = Raylib.LoadTexture(logoPath);
            if (logo.Id == 0 || logo.Width <= 0 || logo.Height <= 0)
            {
                reporter.Report(new AriaError(
                    $"起動ロゴ '{StartupSplashLogoPath}' の読み込み結果が無効です。",
                    level: AriaErrorLevel.Warning,
                    code: "BOOT_SPLASH_LOGO_INVALID"));
                return;
            }
            Raylib.GenTextureMipmaps(ref logo);
            Raylib.SetTextureFilter(logo, TextureFilter.Trilinear);

            double startedAt = Raylib.GetTime();
            double? skipFadeStart = null;
            float skipStartAlpha = 0f;

            while (!Raylib.WindowShouldClose())
            {
                double elapsed = Raylib.GetTime() - startedAt;

                // 通常演出の自然な終了またはスキップフェード完了で抜ける
                if (skipFadeStart is null && elapsed >= StartupSplashSeconds)
                {
                    break;
                }
                if (skipFadeStart is not null && Raylib.GetTime() - skipFadeStart.Value >= StartupSplashSkipFadeSeconds)
                {
                    break;
                }

                // 入力判定：フェードアウト中も受付するが、即抜けはさせない
                if (skipFadeStart is null)
                {
                    if (Raylib.IsMouseButtonPressed(MouseButton.Left) ||
                        Raylib.IsKeyPressed(KeyboardKey.Enter) ||
                        Raylib.IsKeyPressed(KeyboardKey.Space))
                    {
                        skipFadeStart = Raylib.GetTime();
                        skipStartAlpha = GetStartupSplashAlpha(elapsed);
                    }
                }

                float alpha;
                if (skipFadeStart is not null)
                {
                    double fadeElapsed = Raylib.GetTime() - skipFadeStart.Value;
                    float t = Math.Clamp((float)(fadeElapsed / StartupSplashSkipFadeSeconds), 0f, 1f);
                    alpha = skipStartAlpha * (1f - EaseInOutCubic(t));
                }
                else
                {
                    alpha = GetStartupSplashAlpha(elapsed);
                }

                float reveal = EaseOutCubic(Math.Clamp((float)(elapsed / StartupSplashFadeInSeconds), 0f, 1f));
                int screenWidth = Raylib.GetScreenWidth();
                int screenHeight = Raylib.GetScreenHeight();
                float maxWidth = screenWidth * 0.34f;
                float maxHeight = screenHeight * 0.18f;
                float baseScale = MathF.Min(maxWidth / logo.Width, maxHeight / logo.Height);
                baseScale = MathF.Min(baseScale, 0.68f);
                float scale = baseScale * (0.96f + (0.04f * reveal));
                float width = logo.Width * scale;
                float height = logo.Height * scale;
                float x = (screenWidth - width) / 2f;
                float y = (screenHeight - height) / 2f;
                var source = new Rectangle(0, 0, logo.Width, logo.Height);
                var dest = new Rectangle(x, y, width, height);
                byte logoAlpha = (byte)Math.Clamp((int)(alpha * 255f), 0, 255);
                Color logoTint = Rgba(255, 255, 255, logoAlpha);

                Raylib.BeginDrawing();
                try
                {
                    Raylib.ClearBackground(Rgba(250, 250, 248, 255));
                    DrawStartupSplashVignette(screenWidth, screenHeight, alpha);
                    DrawStartupSplashBackdrop(screenWidth, screenHeight, x, y, width, height, alpha, (float)elapsed);
                    DrawStartupSplashParticles(screenWidth, screenHeight, (float)elapsed, alpha);
                    DrawStartupSplashLogoShadow(dest, alpha);
                    Raylib.DrawTexturePro(logo, source, dest, Vector2.Zero, 0f, logoTint);

                    // バージョン番号を右下に小さく表示
                    if (!string.IsNullOrEmpty(versionText))
                    {
                        const int fontSize = 12;
                        int textWidth = Raylib.MeasureText(versionText, fontSize);
                        int textX = screenWidth - textWidth - 16;
                        int textY = screenHeight - fontSize - 12;
                        byte versionAlpha = (byte)Math.Clamp((int)(alpha * 0.3f * 255f), 0, 255);
                        Raylib.DrawText(versionText, textX, textY, fontSize, Rgba(100, 100, 100, versionAlpha));
                    }
                }
                finally
                {
                    Raylib.EndDrawing();
                }
            }
        }
        catch (Exception ex)
        {
            reporter.ReportException(
                "BOOT_SPLASH",
                ex,
                "起動スプラッシュの表示に失敗しました。通常起動へ進みます。",
                AriaErrorLevel.Warning);
        }
        finally
        {
            if (logo.Id != 0)
            {
                Raylib.UnloadTexture(logo);
            }
        }
    }

    private static float GetStartupSplashAlpha(double elapsed)
    {
        if (elapsed < StartupSplashFadeInSeconds)
        {
            return EaseOutCubic((float)(elapsed / StartupSplashFadeInSeconds));
        }

        double fadeOutStart = StartupSplashFadeInSeconds + StartupSplashHoldSeconds;
        if (elapsed < fadeOutStart)
        {
            return 1f;
        }

        float t = Math.Clamp((float)((elapsed - fadeOutStart) / StartupSplashFadeOutSeconds), 0f, 1f);
        return 1f - EaseInOutCubic(t);
    }

    private static void DrawStartupSplashBackdrop(int screenWidth, int screenHeight, float logoX, float logoY, float logoWidth, float logoHeight, float alpha, float elapsed)
    {
        float centerX = screenWidth / 2f;
        float centerY = screenHeight / 2f;
        float glowWidth = MathF.Max(logoWidth * 1.35f, screenWidth * 0.28f);
        float glowHeight = MathF.Max(logoHeight * 2.0f, screenHeight * 0.18f);
        byte glowAlpha = (byte)Math.Clamp((int)(22f * alpha), 0, 22);

        float pulse = 0.82f + 0.18f * MathF.Sin(elapsed * 2.4f);
        byte lineAlpha = (byte)Math.Clamp((int)(26f * alpha * pulse), 0, 35);

        Raylib.DrawRectangleGradientV(
            (int)(centerX - glowWidth / 2f),
            (int)(centerY - glowHeight / 2f),
            (int)glowWidth,
            (int)glowHeight,
            Rgba(255, 255, 255, glowAlpha),
            Rgba(232, 232, 228, glowAlpha));

        float lineY = logoY + logoHeight + MathF.Max(14f, screenHeight * 0.018f);
        float lineWidth = MathF.Min(screenWidth * 0.28f, logoWidth * 0.52f);
        Raylib.DrawRectangleRec(
            new Rectangle(centerX - lineWidth / 2f, lineY, lineWidth, 1f),
            Rgba(38, 38, 38, lineAlpha));
    }

    private static void DrawStartupSplashVignette(int screenWidth, int screenHeight, float alpha)
    {
        int v = (int)(screenHeight * 0.22f);
        int h = (int)(screenWidth * 0.18f);
        byte va = (byte)Math.Clamp((int)(20f * alpha), 0, 20);

        Raylib.DrawRectangleGradientV(0, 0, screenWidth, v, Rgba(0, 0, 0, va), Rgba(0, 0, 0, 0));
        Raylib.DrawRectangleGradientV(0, screenHeight - v, screenWidth, v, Rgba(0, 0, 0, 0), Rgba(0, 0, 0, va));
        Raylib.DrawRectangleGradientH(0, 0, h, screenHeight, Rgba(0, 0, 0, va), Rgba(0, 0, 0, 0));
        Raylib.DrawRectangleGradientH(screenWidth - h, 0, h, screenHeight, Rgba(0, 0, 0, 0), Rgba(0, 0, 0, va));
    }

    private static void DrawStartupSplashLogoShadow(Rectangle logoDest, float alpha)
    {
        float shadowAlpha = 0.06f * alpha;
        for (int i = 3; i >= 1; i--)
        {
            float expand = i * 2.5f;
            float offset = i * 1.5f;
            byte a = (byte)(shadowAlpha * (4 - i) / 3f * 255);
            var r = new Rectangle(
                logoDest.X - expand,
                logoDest.Y - expand + offset,
                logoDest.Width + expand * 2f,
                logoDest.Height + expand * 2f);
            Raylib.DrawRectangleRec(r, Rgba(160, 160, 155, a));
        }
    }

    private static void DrawStartupSplashParticles(int w, int h, float elapsed, float alpha)
    {
        const int count = 7;
        for (int i = 0; i < count; i++)
        {
            int seed = i * 7919;
            float fx = HashFloat(seed, 0f, w);
            float fy = HashFloat(seed + 1, 0f, h);
            float speed = HashFloat(seed + 2, 12f, 32f);
            float size = HashFloat(seed + 3, 1.8f, 3.2f);
            float swayFreq = HashFloat(seed + 4, 0.6f, 1.4f);
            float swayAmp = HashFloat(seed + 5, 6f, 18f);

            float yPos = fy - (elapsed * speed);
            yPos = ((yPos % h) + h) % h;
            float xPos = fx + MathF.Sin(elapsed * swayFreq + seed) * swayAmp;

            float distFromCenter = MathF.Abs(yPos - h * 0.5f) / (h * 0.5f);
            float localAlpha = (1f - distFromCenter * 0.4f) * alpha * 0.35f;
            byte a = (byte)Math.Clamp((int)(localAlpha * 255), 0, 90);

            Raylib.DrawCircle((int)xPos, (int)yPos, size, Rgba(210, 205, 195, a));
        }
    }

    private static float HashFloat(int seed, float min, float max)
    {
        float t = MathF.Abs(MathF.Sin(seed * 12.9898f) * 43758.5453f) % 1f;
        return min + t * (max - min);
    }

    private static float EaseOutCubic(float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        float inv = 1f - t;
        return 1f - (inv * inv * inv);
    }

    private static Color Rgba(byte r, byte g, byte b, byte a)
    {
        return new Color(r, g, b, a);
    }

    private static float EaseInOutCubic(float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return t < 0.5f
            ? 4f * t * t * t
            : 1f - MathF.Pow(-2f * t + 2f, 3f) / 2f;
    }

    private static void StartupTrace(string marker)
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("ARIA_STARTUP_TRACE"), "1", StringComparison.Ordinal))
        {
            return;
        }

        try
        {
            File.AppendAllText(
                Path.Combine(AppContext.BaseDirectory, "startup_trace.log"),
                $"{DateTime.UtcNow:O} {marker}{Environment.NewLine}");
        }
        catch
        {
            // 起動診断は失敗しても本処理を止めない。
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
            // 終了処理ではログ破損や二次クラッシュを避ける。
        }
    }
}
