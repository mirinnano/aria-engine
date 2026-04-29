using System;
using System.IO;
using System.Collections.Generic;
using Raylib_cs;
using AriaEngine.Core;
using AriaEngine.Rendering;
using AriaEngine.Input;
using AriaEngine.Audio;
using AriaEngine.Utility;
using AriaEngine.Assets;
using AriaEngine.Scripting;
using AriaEngine.Tools;

// 条件付きでテスト名前空間をインポート
#if ARIA_TEST
using AriaEngine.Tests;
#endif

namespace AriaEngine;

class Program
{
    private static readonly Dictionary<string, int> FrameErrorCounts = new(StringComparer.Ordinal);

    private sealed class RunOptions
    {
        public RunMode Mode { get; set; } = RunMode.Dev;
        public string InitPath { get; set; } = "init.aria";
        public string? PakPath { get; set; }
        public string? Key { get; set; } = Environment.GetEnvironmentVariable("ARIA_PACK_KEY");
        public string CompiledPath { get; set; } = "scripts/scripts.ariac";
        public string? BytecodePath { get; set; }  // .aribファイルパス
    }

    [STAThread]
    static void Main(string[] args)
    {
#if ARIA_TEST
        // テストモード
        return BytecodeVMTest.RunTests();
#endif

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

#if ARIA_BYTECODE
        if (args.Length > 0 && args[0].Equals("aria-bytecode-compile", StringComparison.OrdinalIgnoreCase))
        {
            Environment.ExitCode = AriaBytecodeCompileCommand.Run(args[1..]);
            return;
        }
#endif

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

        try
        {
            RunOptions runOptions = ParseRunOptions(args, reporter);

            // StringBuilderプールの初期化（パフォーマンス最適化）
            StringHelper.InitializeStringBuilderPool(32, 256);

            var parser = new Parser(reporter);

            var configParams = new ConfigManager(reporter);
            SafeStartup("CONFIG_LOAD", () => configParams.Load(), reporter, "config.jsonの読み込みに失敗しました。既定値で続行します。");

            IAssetProvider assetProvider = CreateAssetProvider(runOptions, reporter);

            CompiledScriptBundle? compiledBundle = TryLoadCompiledBundle(assetProvider, runOptions, reporter);
            RunMode effectiveMode = runOptions.Mode == RunMode.Release && compiledBundle is not null ? RunMode.Release : RunMode.Dev;
            if (runOptions.Mode == RunMode.Release && effectiveMode == RunMode.Dev)
            {
                reporter.Report(new AriaError(
                    "release実行に必要な暗号化済みスクリプトを読めなかったため、可能なら平文devロードへフォールバックします。",
                    level: AriaErrorLevel.Warning,
                    code: "BOOT_RELEASE_FALLBACK",
                    hint: "販売版ではdata.pakとscripts.ariacの収録、ARIA_PACK_KEY、--compiled指定を確認してください。"));
            }

            var scriptLoader = new ScriptLoader(parser, assetProvider, effectiveMode, compiledBundle);

            var saves = new SaveManager(reporter);
            var tweens = new TweenManager();
            var vm = new VirtualMachine(reporter, tweens, saves, configParams);
            vmForShutdown = vm;

            string initScriptPath = runOptions.InitPath;
            var initLoaded = TryLoadScript(scriptLoader, parser, initScriptPath, assetProvider, effectiveMode, reporter, fallbackMessage: "");
            if (initLoaded.Instructions.Count > 0)
            {
                vm.LoadScript(initLoaded, initScriptPath);

                while (vm.State.State == VmState.Running && vm.State.ProgramCounter < initLoaded.Instructions.Count)
                {
                    SafeStartup("VM_INIT_STEP", vm.Step, reporter, "init.aria実行中にエラーが発生しました。可能な範囲で続行します。");
                }
            }

            Raylib.SetConfigFlags(ConfigFlags.VSyncHint);
            Raylib.InitWindow(vm.State.WindowWidth, vm.State.WindowHeight, vm.State.Title);
            Raylib.SetExitKey((KeyboardKey)0);
            windowReady = true;
            string currentWindowTitle = vm.State.Title;
            Raylib.SetTargetFPS(120);
            SafeStartup("AUDIO_INIT", () => { Raylib.InitAudioDevice(); audioReady = true; }, reporter, "音声デバイスの初期化に失敗しました。無音で続行します。");

            renderer = new SpriteRenderer(assetProvider, reporter);
            var input = new InputHandler();
            audio = new AudioManager(assetProvider, reporter);
            vm.Audio = audio;
            var transition = new TransitionManager();

            var loaded = TryLoadScript(
                scriptLoader,
                parser,
                vm.State.MainScript,
                assetProvider,
                effectiveMode,
                reporter,
                fallbackMessage: $"Error: 指定されたスクリプト {vm.State.MainScript} が見つかりません。aria_error_ai.txtを確認してください。");

            if (!string.IsNullOrEmpty(vm.State.FontPath))
            {
                renderer.LoadFont(vm.State.FontPath, vm.State.FontAtlasSize, loaded.SourceLines, vm.State.FontFilter);
            }
            else
            {
                reporter.Report(new AriaError("フォントパスが未設定です。既定フォントで続行します。", -1, initScriptPath, AriaErrorLevel.Warning, "BOOT_FONT_MISSING"));
            }

            renderer.LoadUiFont("assets/fonts/JosefinSans-Thin.ttf");

            vm.LoadScript(loaded, vm.State.MainScript);

            // 動的 include 用のリゾルバを登録（スクリプト内で include "path" を使えるように）
            vm.SetIncludeResolver(path =>
            {
                var result = TryLoadScript(scriptLoader, parser, path, assetProvider, effectiveMode, reporter, fallbackMessage: "");
                return result.Instructions.Count > 0 ? result : null;
            });

            // 開発モードかつディスクロード時のみライブリロードを有効化
            if (effectiveMode == RunMode.Dev && assetProvider is DiskAssetProvider diskProvider)
            {
                liveReload = new LiveReloadManager(vm, scriptLoader, reporter, renderer, diskProvider.Root);
            }

            SafeFrame("vm.step.initial", vm.Step, reporter);

            while (!Raylib.WindowShouldClose())
            {
                liveReload?.Update();

                if (vm.State.RequestClose || vm.State.State == VmState.Ended) break;
                if (!string.Equals(currentWindowTitle, vm.State.Title, StringComparison.Ordinal))
                {
                    Raylib.SetWindowTitle(vm.State.Title);
                    currentWindowTitle = vm.State.Title;
                }

                float dt = Raylib.GetFrameTime();
                float dtMs = dt * 1000f;

                SafeFrame("vm.update", () => vm.Update(dtMs), reporter);
                SafeFrame("input.update", () => input.Update(vm), reporter);
                SafeFrame("menu.update", vm.Menu.Update, reporter);
                if (audioReady && audio is not null) SafeFrame("audio.update", () => audio.Update(vm.State), reporter);
                SafeFrame("transition.update", () => transition.Update(vm, dt), reporter);
                SafeFrame("tweens.update", () => tweens.Update(vm.State, dtMs), reporter);

                if (!vm.Menu.IsOpen)
                {
                    if (vm.State.SkipMode || vm.State.ForceSkipMode)
                    {
                        SafeFrame("vm.skip", () => vm.ProcessSkipFrame(dtMs), reporter);
                    }
                    else if (vm.State.State == VmState.Running)
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
            if (audioReady) SafeShutdown(Raylib.CloseAudioDevice);
            if (windowReady) SafeShutdown(Raylib.CloseWindow);
        }
    }

    private static RunOptions ParseRunOptions(string[] args, ErrorReporter reporter)
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
                case "--bytecode":
                    i++;
                    if (i < args.Length) options.BytecodePath = args[i];
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

        if (options.Mode == RunMode.Release && string.IsNullOrWhiteSpace(options.PakPath))
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

    private static IAssetProvider CreateAssetProvider(RunOptions options, ErrorReporter reporter)
    {
        if (options.Mode == RunMode.Release && !string.IsNullOrWhiteSpace(options.PakPath))
        {
            try
            {
                return new PakAssetProvider(options.PakPath, options.Key);
            }
            catch (Exception ex)
            {
                reporter.ReportException(
                    "BOOT_PAK_OPEN",
                    ex,
                    $"Pak '{options.PakPath}' を開けませんでした。ディスク assets からのロードへフォールバックします。",
                    AriaErrorLevel.Error,
                    hint: "Pakのパス、改ざん、ARIA_PACK_KEYを確認してください。");
            }
        }

        return new DiskAssetProvider(Directory.GetCurrentDirectory());
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
