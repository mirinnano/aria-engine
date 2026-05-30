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
    public void Build_MissingValueForInputFlag_ReturnsErrorCode3()
    {
        string outputPath = Path.Combine(_testDir, "data.pak");
        int result = AriaPackCommand.Run(new[] { "build", "--input" });
        result.Should().Be(3);
    }

    [Fact]
    public void Build_MissingValueForFormatFlag_ReturnsErrorCode3()
    {
        string inputDir = Path.Combine(_testDir, "assets");
        Directory.CreateDirectory(inputDir);
        File.WriteAllText(Path.Combine(inputDir, "test.txt"), "hello");
        string outputPath = Path.Combine(_testDir, "data.pak");
        int result = AriaPackCommand.Run(new[] { "build", "--input", inputDir, "--output", outputPath, "--format" });
        result.Should().Be(3);
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

    [Fact]
    public void WriteV3_SortsEntriesAndPathStrings_InSameOrder()
    {
        // Arrange: create manifest with entries out of hash order (z, a, m)
        var manifest = new PakManifestV3
        {
            Entries = new List<PakManifestEntryV3>
            {
                new PakManifestEntryV3 { PathHash = PakArchiveV3Reader.PathHash64("z.txt"), Offset = 0, Size = 5, OriginalSize = 5, Flags = 0 },
                new PakManifestEntryV3 { PathHash = PakArchiveV3Reader.PathHash64("a.txt"), Offset = 5, Size = 5, OriginalSize = 5, Flags = 0 },
                new PakManifestEntryV3 { PathHash = PakArchiveV3Reader.PathHash64("m.txt"), Offset = 10, Size = 5, OriginalSize = 5, Flags = 0 },
            },
            PathStrings = new List<string> { "z.txt", "a.txt", "m.txt" }
        };
        var files = new byte[][]
        {
            Encoding.UTF8.GetBytes("zdata"),
            Encoding.UTF8.GetBytes("adata"),
            Encoding.UTF8.GetBytes("mdata"),
        };
        string outputPath = Path.Combine(_testDir, Guid.NewGuid().ToString("N") + ".pak");

        // Act: write using V3 format
        using (var fs = File.Create(outputPath))
        {
            PakArchiveV3.Write(fs, manifest, files, PakArchiveV3.Category.Data);
        }

        // Read back and verify PathStrings align with Entries by PathHash
        using var reader = PakArchiveV3Reader.Open(outputPath);
        for (int i = 0; i < reader.Entries.Count; i++)
        {
            ulong expectedHash = PakArchiveV3Reader.PathHash64(reader.PathStrings[i]);
            Assert.Equal(expectedHash, reader.Entries[i].PathHash);
        }
    }

    [Fact]
    public void BuildV3Split_RespectsOutputDirectory()
    {
        // Arrange: create input dir with init.aria (boot) and a .png (data)
        string inputDir = Path.Combine(_testDir, "assets");
        Directory.CreateDirectory(inputDir);
        File.WriteAllText(Path.Combine(inputDir, "init.aria"), "boot content");
        // Create a small PNG-like file (1x1 transparent pixel)
        byte[] pngData = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52 };
        File.WriteAllBytes(Path.Combine(inputDir, "test.png"), pngData);

        string customDir = Path.Combine(_testDir, "custom");
        Directory.CreateDirectory(customDir);
        string outputPath = Path.Combine(customDir, "data.pak");

        // Act: build v3 split pack
        int result = AriaPackCommand.Run(new[] { "build", "--input", inputDir, "--output", outputPath, "--format", "v3", "--split" });

        // Assert
        result.Should().Be(0);
        // boot.arib should be in custom dir, not hardcoded "build"
        string bootPath = Path.Combine(customDir, "boot.arib");
        File.Exists(bootPath).Should().BeTrue("boot.arib should exist in output directory");
        // data.arid should also be in custom dir
        string dataPath = Path.Combine(customDir, "data.arid");
        File.Exists(dataPath).Should().BeTrue("data.arid should exist in output directory");
        // confirm hardcoded build dir was NOT created
        Directory.Exists(Path.Combine(_testDir, "build")).Should().BeFalse("hardcoded build directory should not exist");
    }

    [Fact]
    public void BuildV3Split_InitAria_AppearsOnlyInBoot()
    {
        // Arrange: create input dir with init.aria and another .aria file
        string inputDir = Path.Combine(_testDir, "assets");
        Directory.CreateDirectory(inputDir);
        File.WriteAllText(Path.Combine(inputDir, "init.aria"), "boot script content");
        File.WriteAllText(Path.Combine(inputDir, "scene.aria"), "scenario content");

        string outputPath = Path.Combine(_testDir, "data.pak");

        // Act: build v3 split pack
        int result = AriaPackCommand.Run(new[] { "build", "--input", inputDir, "--output", outputPath, "--format", "v3", "--split" });

        // Assert
        result.Should().Be(0);
        string bootPath = Path.Combine(_testDir, "boot.arib");
        string scenarioPath = Path.Combine(_testDir, "scenario.aris");

        File.Exists(bootPath).Should().BeTrue("boot.arib should exist");
        File.Exists(scenarioPath).Should().BeTrue("scenario.aris should exist");

        // Verify boot contains init.aria
        using (var bootReader = PakArchiveV3Reader.Open(bootPath))
        {
            bootReader.Entries.Should().Contain(e => e.PathHash == PakArchiveV3Reader.PathHash64("init.aria"),
                "init.aria should appear in boot category");
        }

        // Verify scenario does NOT contain init.aria
        using (var scenarioReader = PakArchiveV3Reader.Open(scenarioPath))
        {
            scenarioReader.Entries.Should().NotContain(e => e.PathHash == PakArchiveV3Reader.PathHash64("init.aria"),
                "init.aria should NOT appear in scenario category");
            scenarioReader.Entries.Should().Contain(e => e.PathHash == PakArchiveV3Reader.PathHash64("assets/scene.aria"),
                "scene.aria should appear in scenario category");
        }
}

    [Fact]
    public void Build_FormatV3WithSplit_Succeeds()
    {
        string inputDir = Path.Combine(_testDir, "assets");
        Directory.CreateDirectory(inputDir);
        File.WriteAllText(Path.Combine(inputDir, "test.txt"), "hello world");
        string outputPath = Path.Combine(_testDir, "data.pak");

        int result = AriaPackCommand.Run(new[] { "build", "--format", "v3", "--split", "--input", inputDir, "--output", outputPath });
        result.Should().Be(0);
    }

    [Fact]
    public void Build_V3ScenarioWithIncompressibleData_StoresUncompressedWithFlagsZero()
    {
        // Arrange: create a scenario file with random (incompressible) data
        string inputDir = Path.Combine(_testDir, "assets");
        Directory.CreateDirectory(inputDir);
        byte[] randomData = new byte[8192];
        new Random(42).NextBytes(randomData);
        File.WriteAllBytes(Path.Combine(inputDir, "test.aria"), randomData);

        string outputPath = Path.Combine(_testDir, "data.pak");
        int result = AriaPackCommand.Run(new[] { "build", "--format", "v3", "--split", "--input", inputDir, "--output", outputPath });
        result.Should().Be(0);

        // The scenario pak should be written to the test dir (since outputPath is data.pak in _testDir)
        string scenarioPak = Path.Combine(_testDir, "scenario.aris");
        File.Exists(scenarioPak).Should().BeTrue("scenario.aris should be created");

        using var reader = PakArchiveV3Reader.Open(scenarioPak);
        reader.Entries.Count.Should().Be(1);
        var entry = reader.Entries[0];

        // Flags should be 0x00 (uncompressed) since random data doesn't compress well
        entry.Flags.Should().Be(0x00, "incompressible data should have flags=0x00");
        // Size should equal OriginalSize since we stored uncompressed data
        entry.Size.Should().Be(entry.OriginalSize, "size should equal original size for uncompressed storage");
    }

    [Fact]
    public void Build_V3ScenarioWithCompressibleData_StoresCompressedWithFlagsTwo()
    {
        // Arrange: create a scenario file with zero-filled (highly compressible) data
        string inputDir = Path.Combine(_testDir, "assets");
        Directory.CreateDirectory(inputDir);
        byte[] zeroData = new byte[8192]; // all zeros - compresses extremely well
        File.WriteAllBytes(Path.Combine(inputDir, "compressible.aria"), zeroData);

        string outputPath = Path.Combine(_testDir, "data.pak");
        int result = AriaPackCommand.Run(new[] { "build", "--format", "v3", "--split", "--input", inputDir, "--output", outputPath });
        result.Should().Be(0);

        string scenarioPak = Path.Combine(_testDir, "scenario.aris");
        File.Exists(scenarioPak).Should().BeTrue("scenario.aris should be created");

        using var reader = PakArchiveV3Reader.Open(scenarioPak);
        reader.Entries.Count.Should().Be(1);
        var entry = reader.Entries[0];

        // Flags should be 0x02 (compressed) since zero data compresses well
        entry.Flags.Should().Be(0x02, "compressible zero data should have flags=0x02");
        // Size should be smaller than OriginalSize
        entry.Size.Should().BeLessThan(entry.OriginalSize, "compressed size should be smaller than original");
    }

    [Fact]
    public void Build_FormatV3WithoutSplit_ReturnsErrorCode3()
    {
        // v3 non-split mode is not yet supported
        string inputDir = Path.Combine(_testDir, "assets");
        Directory.CreateDirectory(inputDir);
        File.WriteAllText(Path.Combine(inputDir, "test.txt"), "hello");
        string outputPath = Path.Combine(_testDir, "data.pak");

        int result = AriaPackCommand.Run(new[] { "build", "--format", "v3", "--input", inputDir, "--output", outputPath });

        result.Should().Be(3);
    }

    [Fact]
    public void Lz4Decompress_TooShortInput_ThrowsInvalidDataException()
    {
        var compression = new AriaEngine.Packaging.Compression.Lz4Compression();
        byte[] shortInput = new byte[] { 0x01, 0x02 }; // Less than 4 bytes

        Action act = () => compression.Decompress(shortInput);
        act.Should().Throw<InvalidDataException>().WithMessage("*LZ4 input too short*");
    }

    [Fact]
    public void Lz4Decompress_IntMinValueHeader_ThrowsInvalidDataException()
    {
        var compression = new AriaEngine.Packaging.Compression.Lz4Compression();
        // 4 bytes representing int.MinValue = 0x80000000 = -2147483648
        // In little-endian: 0x00, 0x00, 0x00, 0x80
        byte[] badHeader = new byte[] { 0x00, 0x00, 0x00, 0x80, 0x01, 0x02, 0x03, 0x04 };

        Action act = () => compression.Decompress(badHeader);
        act.Should().Throw<InvalidDataException>().WithMessage("*Invalid LZ4 header*");
    }

    [Fact]
    public void Lz4Decompress_ExceedsMaxSize_ThrowsInvalidDataException()
    {
        var compression = new AriaEngine.Packaging.Compression.Lz4Compression();
        // 256MB + 1 = 16777217 = 0x01000001
        // In little-endian: 0x01, 0x00, 0x00, 0x00
        // 256MB + 1 = 268435457 = 0x10000001 (little-endian)
        byte[] largeHeader = new byte[] { 0x01, 0x00, 0x00, 0x10, 0x01, 0x02, 0x03, 0x04 };

        Action act = () => compression.Decompress(largeHeader);
        act.Should().Throw<InvalidDataException>().WithMessage("*LZ4 decompressed size exceeds maximum allowed*");
    }

    [Fact]
    public void ReadV3_ManifestEntryExceedsFileBounds_ThrowsInvalidDataException()
    {
        // Arrange: create a valid V3 pak, then corrupt an entry's offset to exceed file length
        var manifest = new PakManifestV3
        {
            Entries = new List<PakManifestEntryV3>
            {
                new PakManifestEntryV3 { PathHash = PakArchiveV3Reader.PathHash64("test.txt"), Offset = 0, Size = 5, OriginalSize = 5, Flags = 0 },
            },
            PathStrings = new List<string> { "test.txt" }
        };
        var files = new byte[][]
        {
            Encoding.UTF8.GetBytes("hello"),
        };
        string outputPath = Path.Combine(_testDir, Guid.NewGuid().ToString("N") + ".pak");

        using (var fs = File.Create(outputPath))
        {
            PakArchiveV3.Write(fs, manifest, files, PakArchiveV3.Category.Data);
        }

        long fileLength = new FileInfo(outputPath).Length;

        // Corrupt: change entry offset to point beyond file payload area
        // Header is 36 bytes, manifest follows. Entry offset is relative to PayloadOffset.
        // We need to set the entry's offset such that PayloadOffset + offset + size > fileLength
        using (var fs = File.Open(outputPath, FileMode.Open, FileAccess.ReadWrite))
        {
            // Read header to find PayloadOffset
            fs.Position = 28; // PayloadOffset field location (after Magic(4)+Version(1)+Category(1)+PakVersion(1)+Flags(1)+EntryCount(4)+ManifestOffset(8)+ManifestSize(4))
            ulong payloadOffset = ReadUInt64(fs);

            // Read entry offset from manifest area
            // Manifest starts at byte 36 (headerSize), first entry at offset 4 (after entry count int)
            fs.Position = 36 + 4 + 8; // header + entryCount(4) + PathHash(8) = offset of first entry's Offset field
            long entryOffset = (long)ReadUInt64(fs);

            // Set entry offset beyond file bounds: make it point to fileLength + 1000
            fs.Position = 36 + 4 + 8; // Back to entry offset field
            byte[] newOffsetBytes = BitConverter.GetBytes((ulong)(fileLength + 1000));
            fs.Write(newOffsetBytes, 0, 8);
        }

        // Act & Assert: opening should work, but reading should fail bounds check
        using var reader = PakArchiveV3Reader.Open(outputPath);
        Action act = () => reader.ReadAllBytes("test.txt");
        act.Should().Throw<InvalidDataException>().WithMessage("*Manifest entry exceeds file bounds*");
    }

    private static ulong ReadUInt64(Stream s)
    {
        byte[] bytes = new byte[8];
        s.ReadExactly(bytes, 0, 8);
        return BitConverter.ToUInt64(bytes, 0);
    }
}
