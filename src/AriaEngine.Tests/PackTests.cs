using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using FluentAssertions;
using Xunit;
using AriaEngine.Packaging;
using AriaEngine.Tools;

namespace AriaEngine.Tests;

public sealed class PackTests : IDisposable
{
    private readonly string _testDir;

    public PackTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "aria-pack-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_testDir, recursive: true); } catch { }
    }

    [Fact]
    public void Build_CreatesValidPakFile()
    {
        string inputDir = Path.Combine(_testDir, "assets");
        Directory.CreateDirectory(inputDir);
        File.WriteAllText(Path.Combine(inputDir, "test.txt"), "hello world");

        string outputPath = Path.Combine(_testDir, "data.pak");
        int result = AriaPackCommand.Run(new[] { "build", "--input", inputDir, "--output", outputPath });

        result.Should().Be(0);
        File.Exists(outputPath).Should().BeTrue();

        var reader = PakArchive.Open(outputPath, null);
        string expectedLogical = $"{PakArchive.NormalizePath(inputDir)}/test.txt";
        reader.Contains(expectedLogical).Should().BeTrue();
        byte[] data = reader.ReadAllBytes(expectedLogical);
        Encoding.UTF8.GetString(data).Should().Be("hello world");
    }

    [Fact]
    public void Build_MissingInputDirectory_ReturnsHelpfulError()
    {
        string inputDir = Path.Combine(_testDir, "nonexistent");
        string outputPath = Path.Combine(_testDir, "data.pak");
        int result = AriaPackCommand.Run(new[] { "build", "--input", inputDir, "--output", outputPath });

        result.Should().Be(2);
        File.Exists(outputPath).Should().BeFalse();
    }

    [Fact]
    public void Build_VerboseMode_WritesEntryDetails()
    {
        string inputDir = Path.Combine(_testDir, "assets");
        Directory.CreateDirectory(inputDir);
        File.WriteAllText(Path.Combine(inputDir, "a.txt"), "a");

        string outputPath = Path.Combine(_testDir, "data.pak");
        int result = AriaPackCommand.Run(new[] { "build", "--input", inputDir, "--output", outputPath, "--verbose" });

        result.Should().Be(0);
    }

    [Fact]
    public void Read_CorruptedPayload_Detected()
    {
        string pakPath = CreateSimplePak("original content");

        CorruptPakPayload(pakPath);

        var reader = PakArchive.Open(pakPath, null);
        Action act = () => reader.ReadAllBytes("assets/test.txt");
        act.Should().Throw<InvalidOperationException>().WithMessage("*Corruption detected*hash mismatch*");
    }

    [Fact]
    public void Read_InvalidHeader_ReturnsHelpfulError()
    {
        string badPath = Path.Combine(_testDir, "bad.pak");
        File.WriteAllText(badPath, "not a pak file");

        Action act = () => PakArchive.Open(badPath, null);
        act.Should().Throw<InvalidOperationException>().WithMessage("*Invalid pak header*Expected magic 'ARPK1'*");
    }

    [Fact]
    public void DiffAndApply_PatchUpdatesPakCorrectly()
    {
        string oldPak = CreatePak(new[] { ("assets/a.txt", "old a"), ("assets/b.txt", "old b") });
        string newPak = CreatePak(new[] { ("assets/a.txt", "new a"), ("assets/c.txt", "new c") });
        string patchPath = Path.Combine(_testDir, "update.patch");
        string updatedPak = Path.Combine(_testDir, "updated.pak");

        int diffResult = AriaPackCommand.Run(new[] { "diff", "--base", oldPak, "--new", newPak, "--out", patchPath });
        diffResult.Should().Be(0);
        File.Exists(patchPath).Should().BeTrue();

        int applyResult = AriaPackCommand.Run(new[] { "apply", "--base", oldPak, "--patch", patchPath, "--out", updatedPak });
        applyResult.Should().Be(0);
        File.Exists(updatedPak).Should().BeTrue();

        var reader = PakArchive.Open(updatedPak, null);
        reader.Contains("assets/a.txt").Should().BeTrue();
        reader.Contains("assets/c.txt").Should().BeTrue();
        reader.Contains("assets/b.txt").Should().BeFalse();

        Encoding.UTF8.GetString(reader.ReadAllBytes("assets/a.txt")).Should().Be("new a");
        Encoding.UTF8.GetString(reader.ReadAllBytes("assets/c.txt")).Should().Be("new c");
    }

    [Fact]
    public void Apply_InvalidPatchHeader_ReturnsHelpfulError()
    {
        string oldPak = CreateSimplePak("content");
        string badPatch = Path.Combine(_testDir, "bad.patch");
        File.WriteAllText(badPatch, "not a patch");
        string updatedPak = Path.Combine(_testDir, "updated.pak");

        int result = AriaPackCommand.Run(new[] { "apply", "--base", oldPak, "--patch", badPatch, "--out", updatedPak });
        result.Should().Be(3);
        File.Exists(updatedPak).Should().BeFalse();
    }

    [Fact]
    public void Diff_MissingBaseFile_ReturnsError()
    {
        int result = AriaPackCommand.Run(new[] { "diff", "--base", "nonexistent.pak", "--new", "nonexistent.pak", "--out", "out.patch" });
        result.Should().Be(2);
    }

    [Fact]
    public void PakAssetProvider_InvalidPakFile_ReturnsHelpfulError()
    {
        string badPath = Path.Combine(_testDir, "bad.pak");
        File.WriteAllText(badPath, "not a pak");

        Action act = () => new Assets.PakAssetProvider(badPath);
        act.Should().Throw<InvalidOperationException>().WithMessage("*Failed to open pak file*Invalid pak header*");
    }

    [Fact]
    public void PakAssetProvider_MissingPakFile_ReturnsHelpfulError()
    {
        string missingPath = Path.Combine(_testDir, "missing.pak");

        Action act = () => new Assets.PakAssetProvider(missingPath);
        act.Should().Throw<FileNotFoundException>().WithMessage("*Pak file not found*");
    }

    private string CreateSimplePak(string content)
    {
        return CreatePak(new[] { ("assets/test.txt", content) });
    }

    private string CreatePak((string path, string content)[] files)
    {
        string pakPath = Path.Combine(_testDir, Guid.NewGuid().ToString("N") + ".pak");
        var entries = new List<(string LogicalPath, string Type, byte[] Data)>();
        foreach (var (path, content) in files)
        {
            entries.Add((path, "text", Encoding.UTF8.GetBytes(content)));
        }
        PakArchive.Write(pakPath, entries, encryptionKey: null);
        return pakPath;
    }

    private void CorruptPakPayload(string pakPath)
    {
        using var fs = File.Open(pakPath, FileMode.Open, FileAccess.ReadWrite);
        byte[] magic = new byte[5];
        fs.ReadExactly(magic);
        byte[] lenBuf = new byte[4];
        fs.ReadExactly(lenBuf);
        int manifestLen = BitConverter.ToInt32(lenBuf);
        fs.Position = 5 + 4 + manifestLen;

        byte[] payload = new byte[1];
        fs.ReadExactly(payload);
        payload[0] ^= 0xFF;
        fs.Position -= 1;
        fs.Write(payload, 0, 1);
    }
}
