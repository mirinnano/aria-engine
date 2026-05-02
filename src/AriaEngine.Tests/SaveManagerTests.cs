using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Xunit;
using AriaEngine.Core;
using AriaEngine.Rendering;
using AriaEngine.Scripting;
using AriaEngine.Assets;

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
        state.Execution.ProgramCounter = 42;
        state.SaveRuntime.CurrentChapter = "Chapter 1";
        state.TextRuntime.CurrentTextBuffer = "Test save point";
        state.RegisterState.Registers["%10"] = 100;
        state.RegisterState.Registers["p.unlock"] = 1;
        state.RegisterState.Registers["volatile.temp"] = 999;
        state.RegisterState.StringRegisters["$name"] = "Player";
        state.FlagRuntime.SaveFlags["route_a"] = true;

        _saveManager.Save(2, state, "test.aria");
        var (data, success) = _saveManager.Load(2);

        success.Should().BeTrue();
        data.Should().NotBeNull();
        data!.SlotId.Should().Be(2);
        data.ScriptFile.Should().Be("test.aria");
        data.ChapterTitle.Should().Be("Chapter 1");
        data.PreviewText.Should().Be("Test save point");
        data.State.Execution.ProgramCounter.Should().Be(42);
        data.State.RegisterState.Registers.Should().NotContainKey("10");
        data.State.RegisterState.Registers.Should().NotContainKey("p.unlock");
        data.State.RegisterState.Registers.Should().NotContainKey("volatile.temp");
        data.State.RegisterState.StringRegisters["$name"].Should().Be("Player");
        data.State.FlagRuntime.SaveFlags["route_a"].Should().BeTrue();
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

    [Fact]
    public void Save_CreatesAriaSave3File()
    {
        var state = CreateState();
        _saveManager.Save(4, state, "test.aria");

        var path = Path.Combine("saves", "slot_04.ariasav");
        File.Exists(path).Should().BeTrue();

        using var stream = File.OpenRead(path);
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: false);
        byte[] magic = reader.ReadBytes(9);
        string magicStr = Encoding.ASCII.GetString(magic);
        magicStr.Should().Be("ARIASAVE3");

        int version = reader.ReadInt32();
        version.Should().Be(3);
    }

    [Fact]
    public void Load_AriaSave3_WithDeclarations_RestoresDeclarations()
    {
        var state = CreateState();
        state.Execution.ProgramCounter = 99;
        state.Declarations["$hero"] = "local";
        state.Declarations["%gold"] = "save";
        state.Declarations["p.cleared"] = "persistent";

        _saveManager.Save(5, state, "declarations.aria");
        var (data, success) = _saveManager.Load(5);

        success.Should().BeTrue();
        data.Should().NotBeNull();
        data!.State.Execution.ProgramCounter.Should().Be(99);
        data.Declarations.Should().ContainKey("$hero").WhoseValue.Should().Be("local");
        data.Declarations.Should().ContainKey("%gold").WhoseValue.Should().Be("save");
        data.Declarations.Should().ContainKey("p.cleared").WhoseValue.Should().Be("persistent");
    }

    [Fact]
    public void Load_AriaSave2_TriggersMigrationAndPreservesData()
    {
        var state = CreateState();
        state.Execution.ProgramCounter = 77;
        state.SaveRuntime.CurrentChapter = "Legacy Chapter";
        state.RegisterState.Registers["%100"] = 42;
        state.RegisterState.StringRegisters["$legacy"] = "OldSave";
        state.FlagRuntime.SaveFlags["old_flag"] = true;

        var file = new SaveFile
        {
            Format = "AriaSave",
            Version = 2,
            Meta = new SaveMeta
            {
                SlotId = 6,
                ScriptFile = "legacy.aria",
                SaveTime = DateTime.Now,
                ChapterTitle = state.SaveRuntime.CurrentChapter,
                PreviewText = "Legacy preview",
                PlayTimeSeconds = 123
            },
            Runtime = state
        };

        WriteV2PackedSave(Path.Combine("saves", "slot_06.ariasav"), file);

        var (data, success) = _saveManager.Load(6);

        success.Should().BeTrue();
        data.Should().NotBeNull();
        data!.SlotId.Should().Be(6);
        data.ScriptFile.Should().Be("legacy.aria");
        data.ChapterTitle.Should().Be("Legacy Chapter");
        data.State.Execution.ProgramCounter.Should().Be(77);
        data.State.RegisterState.Registers["%100"].Should().Be(42);
        data.State.RegisterState.StringRegisters["$legacy"].Should().Be("OldSave");
        data.State.FlagRuntime.SaveFlags["old_flag"].Should().BeTrue();
        data.Declarations.Should().NotBeNull();
        data.Declarations.Should().BeEmpty();
    }

    [Fact]
    public void Load_MigratedSave_ResavesAsAriaSave3()
    {
        var state = CreateState();
        state.Execution.ProgramCounter = 88;

        var file = new SaveFile
        {
            Format = "AriaSave",
            Version = 2,
            Meta = new SaveMeta
            {
                SlotId = 7,
                ScriptFile = "migrate.aria",
                SaveTime = DateTime.Now,
                ChapterTitle = "",
                PreviewText = "",
                PlayTimeSeconds = 0
            },
            Runtime = state
        };

        WriteV2PackedSave(Path.Combine("saves", "slot_07.ariasav"), file);

        // Load the v2 save
        var (data, success) = _saveManager.Load(7);
        success.Should().BeTrue();

        // Re-save it
        _saveManager.Save(7, data!.State, data.ScriptFile);

        // Verify it's now v3
        using var stream = File.OpenRead(Path.Combine("saves", "slot_07.ariasav"));
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: false);
        byte[] magic = reader.ReadBytes(9);
        Encoding.ASCII.GetString(magic).Should().Be("ARIASAVE3");
        int version = reader.ReadInt32();
        version.Should().Be(3);
    }

    [Fact]
    public void Load_InvalidVersionHeader_ReturnsFalse()
    {
        var path = Path.Combine("saves", "slot_08.ariasav");
        Directory.CreateDirectory("saves");
        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: false);
        writer.Write(Encoding.ASCII.GetBytes("BADMAGIC!"));
        writer.Write(1);
        writer.Write(16);
        writer.Write(new byte[16]);
        writer.Write(4);
        writer.Write(new byte[4]);

        var (data, success) = _saveManager.Load(8);

        success.Should().BeFalse();
        data.Should().BeNull();
    }

    private static GameState CreateState()
    {
        return new GameState
        {
            SaveRuntime = { SessionStartTime = DateTime.Now, CurrentChapter = "chapter" },
            TextRuntime = { CurrentTextBuffer = "preview" }
        };
    }

    private static void WriteV2PackedSave(string path, SaveFile file)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles
        };
        byte[] plainJson = JsonSerializer.SerializeToUtf8Bytes(file, options);
        byte[] compressed = Compress(plainJson);
        using var aes = Aes.Create();
        aes.Key = SHA256.HashData(Encoding.UTF8.GetBytes("AriaEngine.LocalSave.Format.v2"));
        aes.GenerateIV();
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        using var encryptor = aes.CreateEncryptor();
        byte[] cipher = encryptor.TransformFinalBlock(compressed, 0, compressed.Length);

        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: false);
        writer.Write(Encoding.ASCII.GetBytes("ARIASAVE2"));
        writer.Write(2);
        writer.Write(aes.IV.Length);
        writer.Write(aes.IV);
        writer.Write(cipher.Length);
        writer.Write(cipher);
    }

    private static byte[] Compress(byte[] data)
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            gzip.Write(data, 0, data.Length);
        }
        return output.ToArray();
    }

    [Fact]
    public void Quicksave_Quickload_Slot0_Works()
    {
        var state = CreateState();
        state.Execution.ProgramCounter = 77;
        state.SaveRuntime.CurrentChapter = "Quick Chapter";
        _saveManager.Save(0, state, "quick.aria");

        var (data, success) = _saveManager.Load(0);
        success.Should().BeTrue();
        data.Should().NotBeNull();
        data!.SlotId.Should().Be(0);
        data.ScriptFile.Should().Be("quick.aria");
        data.ChapterTitle.Should().Be("Quick Chapter");
        data.State.Execution.ProgramCounter.Should().Be(77);
    }

    [Fact]
    public void Autosave_AfterChoice_TriggersAutoSave()
    {
        var reporter = new ErrorReporter();
        var saves = new SaveManager(reporter);
        var vm = new VirtualMachine(reporter, new TweenManager(), saves, new ConfigManager());
        vm.State.Execution.State = VmState.WaitingForButton;
        vm.State.Interaction.SpriteButtonMap[1] = 1;
        vm.ResumeFromButton(1);
        saves.HasSaveData(SaveManager.AutoSaveSlot).Should().BeTrue();
    }

    [Fact]
    public void Autosave_AtChapterLabel_TriggersAutoSave()
    {
        var reporter = new ErrorReporter();
        var saves = new SaveManager(reporter);
        var vm = new VirtualMachine(reporter, new TweenManager(), saves, new ConfigManager());

        string scriptPath = Path.Combine(_testDir, "chapter.aria");
        File.WriteAllText(scriptPath, "*start\nlet %0, 1\ngoto *chapter_1\n*chapter_1\nlet %1, 2\nend");

        var parser = new Parser(reporter);
        var provider = new DiskAssetProvider(_testDir);
        var loader = new ScriptLoader(parser, provider, RunMode.Dev);
        var result = loader.LoadScript("chapter.aria");
        vm.LoadScript(result, "chapter.aria");

        saves.HasSaveData(SaveManager.AutoSaveSlot).Should().BeFalse();
        vm.Step(); // executes let, goto, autosave at chapter_1 label, let, end in one batch
        saves.HasSaveData(SaveManager.AutoSaveSlot).Should().BeTrue();
    }

    [Fact]
    public void SaveMetadata_IncludesChapterAndPreviewText()
    {
        var state = CreateState();
        state.SaveRuntime.CurrentChapter = "Prologue";
        state.TextRuntime.CurrentTextBuffer = "This is a very long text that should be truncated to the last eighty characters of the current buffer for the preview.";

        _saveManager.Save(1, state, "meta.aria");
        var (data, success) = _saveManager.Load(1);

        success.Should().BeTrue();
        data!.ChapterTitle.Should().Be("Prologue");
        string expectedPreview = state.TextRuntime.CurrentTextBuffer[^Math.Min(80, state.TextRuntime.CurrentTextBuffer.Length)..];
        data.PreviewText.Should().Be(expectedPreview);
        data.SaveTime.Should().BeWithin(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Thumbnail_IsSavedAndLoaded()
    {
        var state = CreateState();
        byte[] screenshot = new byte[] { 0x89, 0x50, 0x4E, 0x47 };
        _saveManager.Save(2, state, "thumb.aria", screenshot);

        string thumbPath = _saveManager.GetThumbnailPath(2);
        File.Exists(thumbPath).Should().BeTrue();

        var (data, success) = _saveManager.Load(2);
        success.Should().BeTrue();
        data!.ScreenshotPath.Should().Be(thumbPath);
    }

    [Fact]
    public void DeleteSave_RemovesThumbnail()
    {
        var state = CreateState();
        byte[] screenshot = new byte[] { 0x89, 0x50, 0x4E, 0x47 };
        _saveManager.Save(3, state, "del.aria", screenshot);

        string thumbPath = _saveManager.GetThumbnailPath(3);
        File.Exists(thumbPath).Should().BeTrue();

        _saveManager.DeleteSave(3);
        File.Exists(thumbPath).Should().BeFalse();
        _saveManager.HasSaveData(3).Should().BeFalse();
    }
}
