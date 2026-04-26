using System;
using System.IO;
using FluentAssertions;
using Xunit;
using AriaEngine.Core;

namespace AriaEngine.Tests;

public sealed class SaveManagerTests : IDisposable
{
    private readonly string _originalWorkingDir;
    private readonly string _testDir;
    private readonly SaveManager _saveManager;

    public SaveManagerTests()
    {
        _originalWorkingDir = Directory.GetCurrentDirectory();
        _testDir = Path.Combine(Path.GetTempPath(), "aria-save-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);
        Directory.SetCurrentDirectory(_testDir);
        _saveManager = new SaveManager(new ErrorReporter());
    }

    public void Dispose()
    {
        Directory.SetCurrentDirectory(_originalWorkingDir);
        try { Directory.Delete(_testDir, recursive: true); } catch { }
    }

    [Fact]
    public void Constructor_CreatesSaveDirectory()
    {
        Directory.Exists("saves").Should().BeTrue();
    }

    [Fact]
    public void Save_ValidGameState_CreatesPackedSaveFile()
    {
        var state = CreateState();
        _saveManager.Save(1, state, "test.aria");

        File.Exists(Path.Combine("saves", "slot_01.ariasav")).Should().BeTrue();
    }

    [Fact]
    public void Load_ExistingSave_RestoresRuntimeAndMetadata()
    {
        var state = CreateState();
        state.ProgramCounter = 42;
        state.CurrentChapter = "Chapter 1";
        state.CurrentTextBuffer = "Test save point";
        state.Registers["%10"] = 100;
        state.Registers["p.unlock"] = 1;
        state.Registers["volatile.temp"] = 999;
        state.StringRegisters["$name"] = "Player";
        state.SaveFlags["route_a"] = true;

        _saveManager.Save(2, state, "test.aria");
        var (data, success) = _saveManager.Load(2);

        success.Should().BeTrue();
        data.Should().NotBeNull();
        data!.SlotId.Should().Be(2);
        data.ScriptFile.Should().Be("test.aria");
        data.ChapterTitle.Should().Be("Chapter 1");
        data.PreviewText.Should().Be("Test save point");
        data.State.ProgramCounter.Should().Be(42);
        data.State.Registers.Should().NotContainKey("10");
        data.State.Registers.Should().NotContainKey("p.unlock");
        data.State.Registers.Should().NotContainKey("volatile.temp");
        data.State.StringRegisters["$name"].Should().Be("Player");
        data.State.SaveFlags["route_a"].Should().BeTrue();
    }

    [Fact]
    public void Load_MissingSave_ReturnsFalse()
    {
        var (data, success) = _saveManager.Load(99);

        success.Should().BeFalse();
        data.Should().BeNull();
    }

    [Fact]
    public void DeleteSave_ExistingSave_RemovesFile()
    {
        _saveManager.Save(3, CreateState(), "test.aria");
        var path = Path.Combine("saves", "slot_03.ariasav");
        File.Exists(path).Should().BeTrue();

        _saveManager.DeleteSave(3);

        File.Exists(path).Should().BeFalse();
    }

    private static GameState CreateState()
    {
        return new GameState
        {
            SessionStartTime = DateTime.Now,
            CurrentTextBuffer = "preview",
            CurrentChapter = "chapter"
        };
    }
}
