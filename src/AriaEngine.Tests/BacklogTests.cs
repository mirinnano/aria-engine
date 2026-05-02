using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Xunit;
using AriaEngine.Core;
using AriaEngine.Core.Commands;
using AriaEngine.Rendering;

namespace AriaEngine.Tests;

public class BacklogTests
{
    [Fact]
    public void BacklogEntry_StoresVoicePath()
    {
        var reporter = new ErrorReporter();
        var vm = new VirtualMachine(reporter, new TweenManager(), new SaveManager(reporter), new ConfigManager());
        var audio = new AudioCommandHandler(vm);
        var text = new TextCommandHandler(vm);

        // Simulate voice playback before text
        audio.Execute(new Instruction { Op = OpCode.Dwave, Arguments = new List<string> { "voice1.ogg" }, SourceLine = 0 });
        vm.State.Audio.LastVoicePath.Should().Be("voice1.ogg");

        // Simulate text display and backlog addition
        vm.State.TextRuntime.CurrentTextBuffer = "Hello world";
        vm.AddBacklogEntry();

        vm.State.TextRuntime.TextHistory.Count.Should().Be(1);
        vm.State.TextRuntime.TextHistory[0].Text.Should().Be("Hello world");
        vm.State.TextRuntime.TextHistory[0].VoicePath.Should().Be("voice1.ogg");
    }

    [Fact]
    public void BacklogEntry_VoicePathClearedAfterEntry()
    {
        var reporter = new ErrorReporter();
        var vm = new VirtualMachine(reporter, new TweenManager(), new SaveManager(reporter), new ConfigManager());

        vm.State.Audio.LastVoicePath = "voice.ogg";
        vm.State.TextRuntime.CurrentTextBuffer = "First line";
        vm.AddBacklogEntry();

        vm.State.Audio.LastVoicePath.Should().BeEmpty();
    }

    [Fact]
    public void JumpBack_RestoresCorrectState()
    {
        var reporter = new ErrorReporter();
        var vm = new VirtualMachine(reporter, new TweenManager(), new SaveManager(reporter), new ConfigManager());

        // Setup initial state
        vm.State.RegisterState.Registers["testreg"] = 42;
        vm.State.RegisterState.StringRegisters["teststr"] = "hello";
        vm.State.FlagRuntime.Flags["testflag"] = true;
        vm.State.Audio.CurrentBgm = "bgm.ogg";
        vm.State.Execution.ProgramCounter = 100;
        vm.State.TextRuntime.CurrentTextBuffer = "Snapshot text";

        // Add a sprite
        vm.State.Render.Sprites[1] = new Sprite { Id = 1, Type = SpriteType.Text, Text = "sprite text", X = 10, Y = 20 };

        // Capture backlog entry
        vm.AddBacklogEntry();
        vm.State.TextRuntime.TextHistory.Count.Should().Be(1);

        var entry = vm.State.TextRuntime.TextHistory[0];
        entry.StateSnapshot.Should().NotBeNull();
        entry.ProgramCounter.Should().Be(100);

        // Mutate current state
        vm.State.RegisterState.Registers["testreg"] = 99;
        vm.State.RegisterState.StringRegisters["teststr"] = "changed";
        vm.State.FlagRuntime.Flags["testflag"] = false;
        vm.State.Audio.CurrentBgm = "other.ogg";
        vm.State.Execution.ProgramCounter = 200;
        vm.State.Render.Sprites.Clear();

        // Jump back
        vm.JumpToBacklogEntry(entry);

        vm.State.RegisterState.Registers["testreg"].Should().Be(42);
        vm.State.RegisterState.StringRegisters["teststr"].Should().Be("hello");
        vm.State.FlagRuntime.Flags["testflag"].Should().BeTrue();
        vm.State.Audio.CurrentBgm.Should().Be("bgm.ogg");
        vm.State.Execution.ProgramCounter.Should().Be(100);
        vm.State.TextRuntime.CurrentTextBuffer.Should().Be("Snapshot text");
        vm.State.Render.Sprites.ContainsKey(1).Should().BeTrue();
        vm.State.Render.Sprites[1].Text.Should().Be("sprite text");
        vm.State.Render.Sprites[1].X.Should().Be(10);
        vm.State.Execution.State.Should().Be(VmState.Running);
    }

    [Fact]
    public void JumpBack_WithoutSnapshot_OnlyRestoresPcAndText()
    {
        var reporter = new ErrorReporter();
        var vm = new VirtualMachine(reporter, new TweenManager(), new SaveManager(reporter), new ConfigManager());

        var entry = new BacklogEntry
        {
            Text = "No snapshot",
            ProgramCounter = 50,
            StateSnapshot = null
        };

        vm.State.Execution.ProgramCounter = 999;
        vm.State.TextRuntime.CurrentTextBuffer = "current";

        vm.JumpToBacklogEntry(entry);

        vm.State.Execution.ProgramCounter.Should().Be(50);
        vm.State.TextRuntime.CurrentTextBuffer.Should().Be("No snapshot");
        vm.State.Execution.State.Should().Be(VmState.Running);
    }

    [Fact]
    public void Search_FiltersEntries()
    {
        var reporter = new ErrorReporter();
        var vm = new VirtualMachine(reporter, new TweenManager(), new SaveManager(reporter), new ConfigManager());

        vm.State.TextRuntime.TextHistory.Add(new BacklogEntry { Text = "Alice: Hello there" });
        vm.State.TextRuntime.TextHistory.Add(new BacklogEntry { Text = "Bob: Good morning" });
        vm.State.TextRuntime.TextHistory.Add(new BacklogEntry { Text = "Alice: How are you?" });

        var all = vm.State.TextRuntime.TextHistory;
        all.Count.Should().Be(3);

        var filtered = all.Where(e => e.Text.Contains("Alice")).ToList();
        filtered.Count.Should().Be(2);
        filtered[0].Text.Should().Contain("Alice");
        filtered[1].Text.Should().Contain("Alice");

        var filtered2 = all.Where(e => e.Text.ToLowerInvariant().Contains("morning")).ToList();
        filtered2.Count.Should().Be(1);
        filtered2[0].Text.Should().Contain("morning");
    }

    [Fact]
    public void UnreadEntries_AreMarkedCorrectly()
    {
        var reporter = new ErrorReporter();
        var vm = new VirtualMachine(reporter, new TweenManager(), new SaveManager(reporter), new ConfigManager());

        // Initially no entries
        vm.State.TextRuntime.TextHistory.Should().BeEmpty();

        // Add first entry - should be unread by default
        vm.State.TextRuntime.CurrentTextBuffer = "First line";
        vm.AddBacklogEntry();
        vm.State.TextRuntime.TextHistory[0].IsRead.Should().BeFalse();

        // Simulate opening backlog: mark all as read
        foreach (var entry in vm.State.TextRuntime.TextHistory) entry.IsRead = true;
        vm.State.TextRuntime.TextHistory[0].IsRead.Should().BeTrue();

        // Add new entry after backlog was opened
        vm.State.TextRuntime.CurrentTextBuffer = "Second line";
        vm.AddBacklogEntry();
        vm.State.TextRuntime.TextHistory[1].IsRead.Should().BeFalse();
        vm.State.TextRuntime.TextHistory[0].IsRead.Should().BeTrue();
    }

    [Fact]
    public void BacklogEntry_CapturesProgramCounter()
    {
        var reporter = new ErrorReporter();
        var vm = new VirtualMachine(reporter, new TweenManager(), new SaveManager(reporter), new ConfigManager());

        vm.State.Execution.ProgramCounter = 1234;
        vm.State.TextRuntime.CurrentTextBuffer = "Test text";
        vm.AddBacklogEntry();

        vm.State.TextRuntime.TextHistory[0].ProgramCounter.Should().Be(1234);
    }

    [Fact]
    public void BacklogEntry_CapturesTimestamp()
    {
        var reporter = new ErrorReporter();
        var vm = new VirtualMachine(reporter, new TweenManager(), new SaveManager(reporter), new ConfigManager());

        var before = System.DateTime.Now.AddSeconds(-1);
        vm.State.TextRuntime.CurrentTextBuffer = "Timed text";
        vm.AddBacklogEntry();
        var after = System.DateTime.Now.AddSeconds(1);

        vm.State.TextRuntime.TextHistory[0].Timestamp.Should().BeOnOrAfter(before);
        vm.State.TextRuntime.TextHistory[0].Timestamp.Should().BeOnOrBefore(after);
    }

    [Fact]
    public void BacklogEntry_DuplicateText_NotAddedTwice()
    {
        var reporter = new ErrorReporter();
        var vm = new VirtualMachine(reporter, new TweenManager(), new SaveManager(reporter), new ConfigManager());

        vm.State.TextRuntime.CurrentTextBuffer = "Duplicate";
        vm.AddBacklogEntry();
        vm.AddBacklogEntry();

        vm.State.TextRuntime.TextHistory.Count.Should().Be(1);
    }
}
