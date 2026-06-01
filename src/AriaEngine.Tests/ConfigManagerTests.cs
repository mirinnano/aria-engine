using System;
using System.IO;
using System.Text;
using System.Text.Json;
using AriaEngine.Core;
using FluentAssertions;
using Xunit;

namespace AriaEngine.Tests;

public sealed class ConfigManagerTests : IDisposable
{
    private readonly string _testDir;

    public ConfigManagerTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "aria-config-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_testDir, recursive: true); } catch { }
    }

    [Fact]
    public void PortableJsonPersistentMode_SavesPlainJsonAndLoadsIt()
    {
        string persistentPath = Path.Combine(_testDir, "persistent.ariasav");
        var manager = new ConfigManager(
            new ErrorReporter(),
            Path.Combine(_testDir, "config.json"),
            persistentPath,
            usePortableJsonPersistent: true);

        manager.SavePersistentGameData(new PersistentGameData
        {
            SkipUnread = true,
            Registers = { ["p.route"] = 2 },
            SaveFlags = { ["ending"] = true },
            Counters = { ["seen"] = 3 },
            ReadKeys = { "main:12" },
            UnlockedCgs = { "cg01" }
        });

        byte[] bytes = File.ReadAllBytes(persistentPath);
        bytes[0].Should().Be((byte)'{');

        PersistentGameData loaded = manager.LoadPersistentGameData();
        loaded.SkipUnread.Should().BeTrue();
        loaded.Registers["p.route"].Should().Be(2);
        loaded.SaveFlags["ending"].Should().BeTrue();
        loaded.Counters["seen"].Should().Be(3);
        loaded.ReadKeys.Should().Contain("main:12");
        loaded.UnlockedCgs.Should().Contain("cg01");
    }

    [Fact]
    public void NativePersistentMode_LoadsPortableJsonForMigration()
    {
        string persistentPath = Path.Combine(_testDir, "persistent.ariasav");
        File.WriteAllText(
            persistentPath,
            JsonSerializer.Serialize(new PersistentGameData { Registers = { ["p.web"] = 7 } }),
            Encoding.UTF8);
        var manager = new ConfigManager(
            new ErrorReporter(),
            Path.Combine(_testDir, "config.json"),
            persistentPath,
            usePortableJsonPersistent: false);

        PersistentGameData loaded = manager.LoadPersistentGameData();

        loaded.Registers["p.web"].Should().Be(7);
    }
}
