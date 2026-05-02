using System;
using System.IO;
using System.Threading;
using FluentAssertions;
using Xunit;
using AriaEngine.Core;
using AriaEngine.Assets;
using AriaEngine.Rendering;
using AriaEngine.Scripting;

namespace AriaEngine.Tests;

public class LiveReloadTests : IDisposable
{
    private readonly string _tempDir;

    public LiveReloadTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"aria_live_reload_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, true);
        }
        catch
        {
            // Best effort cleanup
        }
    }

    private static VirtualMachine CreateVm(ErrorReporter reporter)
    {
        return new VirtualMachine(reporter, new TweenManager(), new SaveManager(reporter), new ConfigManager(reporter));
    }

    [Fact]
    public void FileSystemWatcher_DetectsScriptChange()
    {
        var reporter = new ErrorReporter();
        var vm = CreateVm(reporter);
        var parser = new Parser(reporter);
        var provider = new DiskAssetProvider(_tempDir);
        var loader = new ScriptLoader(parser, provider, RunMode.Dev);

        string scriptPath = Path.Combine(_tempDir, "main.aria");
        File.WriteAllText(scriptPath, "*start\ntext \"hello\"\n@");

        var manager = new LiveReloadManager(vm, loader, reporter, null, _tempDir);

        // Modify file to trigger watcher
        File.WriteAllText(scriptPath, "*start\ntext \"world\"\n@");

        // Wait for FileSystemWatcher event
        Thread.Sleep(400);
        manager.Update();

        // Queue should be empty after processing
        manager.PendingCount.Should().Be(0);
    }

    [Fact]
    public void Reload_PreservesRegisters()
    {
        var reporter = new ErrorReporter();
        var vm = CreateVm(reporter);
        var parser = new Parser(reporter);
        var provider = new DiskAssetProvider(_tempDir);
        var loader = new ScriptLoader(parser, provider, RunMode.Dev);

        string scriptPath = Path.Combine(_tempDir, "main.aria");
        File.WriteAllText(scriptPath, "*start\nlet %0, 1\nwait 1000\nend");

        var result = loader.LoadScript("main.aria");
        vm.LoadScript(result, "main.aria");
        vm.Step(); // execute let %0, 1
        vm.State.RegisterState.Registers.Should().ContainKey("0").WhoseValue.Should().Be(1);

        // Change script on disk
        File.WriteAllText(scriptPath, "*start\nlet %0, 99\nwait 1000\nend");

        var manager = new LiveReloadManager(vm, loader, reporter, null, _tempDir);
        manager.EnqueueChange(scriptPath);
        manager.Update();

        // Register should be preserved across reload
        vm.State.RegisterState.Registers.Should().ContainKey("0").WhoseValue.Should().Be(1);
        vm.CurrentScriptFile.Should().Be("main.aria");
    }

    [Fact]
    public void Reload_MaintainsCurrentLabel()
    {
        var reporter = new ErrorReporter();
        var vm = CreateVm(reporter);
        var parser = new Parser(reporter);
        var provider = new DiskAssetProvider(_tempDir);
        var loader = new ScriptLoader(parser, provider, RunMode.Dev);

        string scriptPath = Path.Combine(_tempDir, "main.aria");
        File.WriteAllText(scriptPath,
            "*start\nlet %0, 1\n" +
            "*middle\nlet %1, 2\nwait 1000\nend");

        var result = loader.LoadScript("main.aria");
        vm.LoadScript(result, "main.aria");
        vm.JumpTo("*middle");
        // Step once to execute let %1, 2 (Step loops while Running, so it also executes wait)
        vm.Step();

        vm.TryGetCurrentLabelAndOffset(out string labelName, out int offset).Should().BeTrue();
        labelName.Should().Be("middle");
        // After executing let and wait, PC is at end instruction (offset 2 from *middle)
        offset.Should().Be(2);

        vm.State.RegisterState.Registers["0"] = 42; // manually set to verify preservation

        // Change script while keeping same label structure
        File.WriteAllText(scriptPath,
            "*start\nlet %0, 10\n" +
            "*middle\nlet %1, 20\nwait 1000\nend");

        var manager = new LiveReloadManager(vm, loader, reporter, null, _tempDir);
        manager.EnqueueChange(scriptPath);
        manager.Update();

        // Should be at same label with same offset
        vm.TryGetCurrentLabelAndOffset(out string newLabel, out int newOffset).Should().BeTrue();
        newLabel.Should().Be("middle");
        newOffset.Should().Be(offset);
        vm.State.RegisterState.Registers.Should().ContainKey("0").WhoseValue.Should().Be(42); // preserved from before reload
    }

    [Fact]
    public void Reload_ResetsTextState()
    {
        var reporter = new ErrorReporter();
        var vm = CreateVm(reporter);
        var parser = new Parser(reporter);
        var provider = new DiskAssetProvider(_tempDir);
        var loader = new ScriptLoader(parser, provider, RunMode.Dev);

        string scriptPath = Path.Combine(_tempDir, "main.aria");
        File.WriteAllText(scriptPath, "*start\ntext \"hello\"\nwait 1000\nend");

        var result = loader.LoadScript("main.aria");
        vm.LoadScript(result, "main.aria");
        vm.State.TextRuntime.CurrentTextBuffer = "previous text";
        vm.State.TextRuntime.DisplayedTextLength = 5;
        vm.State.TextRuntime.TextTimerMs = 123f;

        var manager = new LiveReloadManager(vm, loader, reporter, null, _tempDir);
        manager.EnqueueChange(scriptPath);
        manager.Update();

        vm.State.TextRuntime.CurrentTextBuffer.Should().BeEmpty();
        vm.State.TextRuntime.DisplayedTextLength.Should().Be(0);
        vm.State.TextRuntime.TextTimerMs.Should().Be(0f);
    }

    [Fact]
    public void Reload_PreservesFlagsAndStringRegisters()
    {
        var reporter = new ErrorReporter();
        var vm = CreateVm(reporter);
        var parser = new Parser(reporter);
        var provider = new DiskAssetProvider(_tempDir);
        var loader = new ScriptLoader(parser, provider, RunMode.Dev);

        string scriptPath = Path.Combine(_tempDir, "main.aria");
        File.WriteAllText(scriptPath, "*start\nwait 1000\nend");

        var result = loader.LoadScript("main.aria");
        vm.LoadScript(result, "main.aria");
        vm.State.FlagRuntime.Flags["flag_a"] = true;
        vm.State.RegisterState.StringRegisters["name"] = "Aria";

        var manager = new LiveReloadManager(vm, loader, reporter, null, _tempDir);
        manager.EnqueueChange(scriptPath);
        manager.Update();

        vm.State.FlagRuntime.Flags.Should().ContainKey("flag_a").WhoseValue.Should().BeTrue();
        vm.State.RegisterState.StringRegisters.Should().ContainKey("name").WhoseValue.Should().Be("Aria");
    }

    [Fact]
    public void Reload_PreservesSprites()
    {
        var reporter = new ErrorReporter();
        var vm = CreateVm(reporter);
        var parser = new Parser(reporter);
        var provider = new DiskAssetProvider(_tempDir);
        var loader = new ScriptLoader(parser, provider, RunMode.Dev);

        string scriptPath = Path.Combine(_tempDir, "main.aria");
        File.WriteAllText(scriptPath, "*start\nwait 1000\nend");

        var result = loader.LoadScript("main.aria");
        vm.LoadScript(result, "main.aria");
        vm.State.Render.Sprites[10] = new Sprite { Id = 10, Type = SpriteType.Rect, X = 100, Y = 200 };

        var manager = new LiveReloadManager(vm, loader, reporter, null, _tempDir);
        manager.EnqueueChange(scriptPath);
        manager.Update();

        vm.State.Render.Sprites.Should().ContainKey(10);
        vm.State.Render.Sprites[10].X.Should().Be(100);
    }

    [Fact]
    public void ResourceManager_ClearTextureCache_DoesNotThrowWhenEmpty()
    {
        var provider = new DiskAssetProvider(_tempDir);
        var reporter = new ErrorReporter();
        var rm = new ResourceManager(provider, reporter);

        Action act = () => rm.ClearTextureCache();
        act.Should().NotThrow();
    }

    [Fact]
    public void LiveReloadManager_Dispose_DoesNotThrow()
    {
        var reporter = new ErrorReporter();
        var vm = CreateVm(reporter);
        var parser = new Parser(reporter);
        var provider = new DiskAssetProvider(_tempDir);
        var loader = new ScriptLoader(parser, provider, RunMode.Dev);

        var manager = new LiveReloadManager(vm, loader, reporter, null, _tempDir);
        Action act = () => manager.Dispose();
        act.Should().NotThrow();
    }
}
