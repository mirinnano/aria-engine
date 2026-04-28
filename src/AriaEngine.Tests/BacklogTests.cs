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
        vm.State.LastVoicePath.Should().Be("voice1.ogg");

        // Simulate text display and backlog addition
        vm.State.CurrentTextBuffer = "Hello world";
        vm.AddBacklogEntry();

        vm.State.TextHistory.Count.Should().Be(1);
        vm.State.TextHistory[0].Text.Should().Be("Hello world");
        vm.State.TextHistory[0].VoicePath.Should().Be("voice1.ogg");
    }

    [Fact]
    public void BacklogEntry_VoicePathClearedAfterEntry()
    {
        var reporter = new ErrorReporter();
        var vm = new VirtualMachine(reporter, new TweenManager(), new SaveManager(reporter), new ConfigManager());

        vm.State.LastVoicePath = "voice.ogg";
        vm.State.CurrentTextBuffer = "First line";
        vm.AddBacklogEntry();

        vm.State.LastVoicePath.Should().BeEmpty();
    }

    [Fact]
    public void JumpBack_RestoresCorrectState()
    {
        var reporter = new ErrorReporter();
        var vm = new VirtualMachine(reporter, new TweenManager(), new SaveManager(reporter), new ConfigManager());

        // Setup initial state
        vm.State.Registers["testreg"] = 42;
        vm.State.StringRegisters["teststr"] = "hello";
        vm.State.Flags["testflag"] = true;
        vm.State.CurrentBgm = "bgm.ogg";
        vm.State.ProgramCounter = 100;
        vm.State.CurrentTextBuffer = "Snapshot text";

        // Add a sprite
        vm.State.Sprites[1] = new Sprite { Id = 1, Type = SpriteType.Text, Text = "sprite text", X = 10, Y = 20 };

        // Capture backlog entry
        vm.AddBacklogEntry();
        vm.State.TextHistory.Count.Should().Be(1);

        var entry = vm.State.TextHistory[0];
        entry.StateSnapshot.Should().NotBeNull();
        entry.ProgramCounter.Should().Be(100);

        // Mutate current state
        vm.State.Registers["testreg"] = 99;
        vm.State.StringRegisters["teststr"] = "changed";
        vm.State.Flags["testflag"] = false;
        vm.State.CurrentBgm = "other.ogg";
        vm.State.ProgramCounter = 200;
        vm.State.Sprites.Clear();

        // Jump back
        vm.JumpToBacklogEntry(entry);

        vm.State.Registers["testreg"].Should().Be(42);
        vm.State.StringRegisters["teststr"].Should().Be("hello");
        vm.State.Flags["testflag"].Should().BeTrue();
        vm.State.CurrentBgm.Should().Be("bgm.ogg");
        vm.State.ProgramCounter.Should().Be(100);
        vm.State.CurrentTextBuffer.Should().Be("Snapshot text");
        vm.State.Sprites.ContainsKey(1).Should().BeTrue();
        vm.State.Sprites[1].Text.Should().Be("sprite text");
        vm.State.Sprites[1].X.Should().Be(10);
        vm.State.State.Should().Be(VmState.Running);
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

        vm.State.ProgramCounter = 999;
        vm.State.CurrentTextBuffer = "current";

        vm.JumpToBacklogEntry(entry);

        vm.State.ProgramCounter.Should().Be(50);
        vm.State.CurrentTextBuffer.Should().Be("No snapshot");
        vm.State.State.Should().Be(VmState.Running);
    }

    [Fact]
    public void Search_FiltersEntries()
    {
        var reporter = new ErrorReporter();
        var vm = new VirtualMachine(reporter, new TweenManager(), new SaveManager(reporter), new ConfigManager());

        vm.State.TextHistory.Add(new BacklogEntry { Text = "Alice: Hello there" });
        vm.State.TextHistory.Add(new BacklogEntry { Text = "Bob: Good morning" });
        vm.State.TextHistory.Add(new BacklogEntry { Text = "Alice: How are you?" });

        var all = vm.State.TextHistory;
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
        vm.State.TextHistory.Should().BeEmpty();

        // Add first entry - should be unread by default
        vm.State.CurrentTextBuffer = "First line";
        vm.AddBacklogEntry();
        vm.State.TextHistory[0].IsRead.Should().BeFalse();

        // Simulate opening backlog: mark all as read
        foreach (var entry in vm.State.TextHistory) entry.IsRead = true;
        vm.State.TextHistory[0].IsRead.Should().BeTrue();

        // Add new entry after backlog was opened
        vm.State.CurrentTextBuffer = "Second line";
        vm.AddBacklogEntry();
        vm.State.TextHistory[1].IsRead.Should().BeFalse();
        vm.State.TextHistory[0].IsRead.Should().BeTrue();
    }

    [Fact]
    public void BacklogEntry_CapturesProgramCounter()
    {
        var reporter = new ErrorReporter();
        var vm = new VirtualMachine(reporter, new TweenManager(), new SaveManager(reporter), new ConfigManager());

        vm.State.ProgramCounter = 1234;
        vm.State.CurrentTextBuffer = "Test text";
        vm.AddBacklogEntry();

        vm.State.TextHistory[0].ProgramCounter.Should().Be(1234);
    }

    [Fact]
    public void BacklogEntry_CapturesTimestamp()
    {
        var reporter = new ErrorReporter();
        var vm = new VirtualMachine(reporter, new TweenManager(), new SaveManager(reporter), new ConfigManager());

        var before = System.DateTime.Now.AddSeconds(-1);
        vm.State.CurrentTextBuffer = "Timed text";
        vm.AddBacklogEntry();
        var after = System.DateTime.Now.AddSeconds(1);

        vm.State.TextHistory[0].Timestamp.Should().BeOnOrAfter(before);
        vm.State.TextHistory[0].Timestamp.Should().BeOnOrBefore(after);
    }

    [Fact]
    public void BacklogEntry_DuplicateText_NotAddedTwice()
    {
        var reporter = new ErrorReporter();
        var vm = new VirtualMachine(reporter, new TweenManager(), new SaveManager(reporter), new ConfigManager());

        vm.State.CurrentTextBuffer = "Duplicate";
        vm.AddBacklogEntry();
        vm.AddBacklogEntry();

        vm.State.TextHistory.Count.Should().Be(1);
    }
}
