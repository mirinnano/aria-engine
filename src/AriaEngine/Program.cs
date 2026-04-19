using System;
using System.IO;
using Raylib_cs;
using AriaEngine.Core;
using AriaEngine.Rendering;
using AriaEngine.Input;
using AriaEngine.Audio;
using System.Linq;

namespace AriaEngine;

class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        var reporter = new ErrorReporter();
        var parser = new Parser(reporter);

        var configParams = new ConfigManager();
        configParams.Load();

        var saves = new SaveManager(reporter);
        var tweens = new TweenManager();
        var vm = new VirtualMachine(reporter, tweens, saves, configParams);

        if (File.Exists("init.aria"))
        {
            var initLines = File.ReadAllLines("init.aria");
            var (initInst, initLabels) = parser.Parse(initLines, "init.aria");
            vm.LoadScript(initInst, initLabels, "init.aria");
            
            while (vm.State.State == VmState.Running && vm.State.ProgramCounter < initInst.Count)
            {
                vm.Step();
            }
        }
        else
        {
            reporter.Report(new AriaError("init.aria が見つかりません。", -1, "", AriaErrorLevel.Warning));
        }

        if (reporter.HasFatalError)
        {
            reporter.WriteLogFile();
            Console.WriteLine("致命的なエラーが発生したため終了します。詳細は aria_error.log を確認してください。");
            return;
        }

        Raylib.SetConfigFlags(ConfigFlags.Msaa4xHint);
        Raylib.InitWindow(vm.State.WindowWidth, vm.State.WindowHeight, vm.State.Title);
        Raylib.SetTargetFPS(60);
        Raylib.InitAudioDevice();

        var renderer = new SpriteRenderer();
        var input = new InputHandler();
        var audio = new AudioManager();
        var transition = new TransitionManager();

        string[] scriptLines = File.Exists(vm.State.MainScript) ? File.ReadAllLines(vm.State.MainScript) : new string[] { $"text \"Error: 指定されたスクリプト {vm.State.MainScript} が見つかりません。\"" };
        var (instructions, labels) = parser.Parse(scriptLines, vm.State.MainScript);

        if (!string.IsNullOrEmpty(vm.State.FontPath) && File.Exists(vm.State.FontPath))
        {
            renderer.LoadFont(vm.State.FontPath, vm.State.FontAtlasSize, scriptLines, vm.State.FontFilter);
        }
        else
        {
            reporter.Report(new AriaError("フォントファイルが見つかりません。", -1, "init.aria", AriaErrorLevel.Warning));
        }

        vm.LoadScript(instructions, labels, vm.State.MainScript);
        vm.Step();

        while (!Raylib.WindowShouldClose())
        {
            float dt = Raylib.GetFrameTime();
            float dtMs = dt * 1000f;

            vm.Update(dtMs);
            input.Update(vm);
            audio.Update(vm.State);
            transition.Update(vm, dt);
            tweens.Update(vm.State, dtMs);

            if (vm.State.State == VmState.Running)
            {
                vm.Step();
            }

            Raylib.BeginDrawing();
            Raylib.ClearBackground(Color.Black);
            
            renderer.Draw(vm.State, transition);
            
            Raylib.EndDrawing();
        }

        reporter.WriteLogFile();

        renderer.Unload();
        audio.Unload();
        Raylib.CloseAudioDevice();
        Raylib.CloseWindow();
    }
}
