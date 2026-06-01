using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AriaEngine.Assets;
using AriaEngine.Packaging;
using Xunit;

namespace AriaEngine.Tests;

/// <summary>
/// Unit tests for <see cref="UnifiedAssetProvider"/> (Pak v3 redesign, Phase 1.2).
/// </summary>
public class UnifiedAssetProviderTests
{
    private static string CreateTempPak(
        string fileName,
        PakArchiveV3.Category category,
        params (string path, byte[] data)[] entries)
    {
        var entriesWithOffsets = new List<(string path, byte[] data, ulong offset)>();
        ulong cumulative = 0;
        foreach (var e in entries)
        {
            entriesWithOffsets.Add((e.path, e.data, cumulative));
            cumulative += (ulong)e.data.Length;
        }
        var manifest = new PakManifestV3
        {
            Entries = entriesWithOffsets.Select(e => new PakManifestEntryV3
            {
                PathHash = PakArchiveV3Reader.PathHash64(e.path),
                Offset = e.offset,
                Size = (uint)e.data.Length,
                OriginalSize = (uint)e.data.Length,
                Flags = 0
            }).ToList(),
            PathStrings = entriesWithOffsets.Select(e => e.path).ToList()
        };
        var payloads = entriesWithOffsets.Select(e => e.data).ToArray();
        var tempPath = Path.Combine(Path.GetTempPath(), fileName);
        using (var fs = File.Create(tempPath))
        {
            PakArchiveV3.Write(fs, manifest, payloads, category);
        }
        return tempPath;
    }

    private static string CreateTempDirWithFile(string dirName, string relativePath, string content)
    {
        var dir = Path.Combine(Path.GetTempPath(), dirName);
        var fullPath = Path.Combine(dir, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);
        return dir;
    }

    [Fact]
    public void DiskOnly_ResolvesFromDisk()
    {
        var root = CreateTempDirWithFile("uap_disk_only", "data/x.txt", "from-disk");
        try
        {
            using var provider = new UnifiedAssetProvider(
                diskRoot: root,
                pakPaths: System.Array.Empty<string>());

            Assert.True(provider.Exists("data/x.txt"));
            Assert.Equal("from-disk", provider.ReadAllText("data/x.txt"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void PakOnly_ResolvesFromPak()
    {
        var pak = CreateTempPak("uap_pak_only.arid",
            PakArchiveV3.Category.Data,
            ("data/y.txt", System.Text.Encoding.UTF8.GetBytes("from-pak")));
        try
        {
            using var provider = new UnifiedAssetProvider(
                diskRoot: null,
                pakPaths: new[] { pak });

            Assert.True(provider.Exists("data/y.txt"));
            Assert.Equal("from-pak", provider.ReadAllText("data/y.txt"));
        }
        finally
        {
            File.Delete(pak);
        }
    }

    [Fact]
    public void DiskFirst_DiskTakesPrecedence_WhenBothExist()
    {
        // Same path exists in both disk and pak
        var root = CreateTempDirWithFile("uap_disk_first", "data/shared.txt", "from-disk");
        var pak = CreateTempPak("uap_disk_first.arid",
            PakArchiveV3.Category.Data,
            ("data/shared.txt", System.Text.Encoding.UTF8.GetBytes("from-pak")));
        try
        {
            using var provider = new UnifiedAssetProvider(
                diskRoot: root,
                pakPaths: new[] { pak },
                diskFirst: true);

            Assert.True(provider.Exists("data/shared.txt"));
            Assert.Equal("from-disk", provider.ReadAllText("data/shared.txt"));
            Assert.True(provider.DiskHitCount >= 1);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
            File.Delete(pak);
        }
    }

    [Fact]
    public void PakFirst_PakTakesPrecedence_WhenBothExist()
    {
        var root = CreateTempDirWithFile("uap_pak_first", "data/shared.txt", "from-disk");
        var pak = CreateTempPak("uap_pak_first.arid",
            PakArchiveV3.Category.Data,
            ("data/shared.txt", System.Text.Encoding.UTF8.GetBytes("from-pak")));
        try
        {
            using var provider = new UnifiedAssetProvider(
                diskRoot: root,
                pakPaths: new[] { pak },
                diskFirst: false);

            Assert.True(provider.Exists("data/shared.txt"));
            Assert.Equal("from-pak", provider.ReadAllText("data/shared.txt"));
            Assert.True(provider.PakHitCount >= 1);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
            File.Delete(pak);
        }
    }

    [Fact]
    public void DiskFirst_FallsBackToPak_WhenNotOnDisk()
    {
        var root = CreateTempDirWithFile("uap_fallback", "data/only_on_disk.txt", "disk-content");
        var pak = CreateTempPak("uap_fallback.arid",
            PakArchiveV3.Category.Data,
            ("data/only_in_pak.txt", System.Text.Encoding.UTF8.GetBytes("pak-content")));
        try
        {
            using var provider = new UnifiedAssetProvider(
                diskRoot: root,
                pakPaths: new[] { pak },
                diskFirst: true);

            // File on disk only → disk
            Assert.Equal("disk-content", provider.ReadAllText("data/only_on_disk.txt"));
            // File in pak only → pak (fallback)
            Assert.Equal("pak-content", provider.ReadAllText("data/only_in_pak.txt"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
            File.Delete(pak);
        }
    }

    [Fact]
    public void NotFound_ThrowsFileNotFoundException()
    {
        var pak = CreateTempPak("uap_notfound.arid",
            PakArchiveV3.Category.Data,
            ("data/a.txt", System.Text.Encoding.UTF8.GetBytes("a")));
        try
        {
            using var provider = new UnifiedAssetProvider(
                diskRoot: null,
                pakPaths: new[] { pak });
            Assert.False(provider.Exists("data/missing.txt"));
            Assert.Throws<FileNotFoundException>(() => provider.ReadAllBytes("data/missing.txt"));
        }
        finally
        {
            File.Delete(pak);
        }
    }

    [Fact]
    public void ReadAllLines_NormalizesLineEndings()
    {
        var root = CreateTempDirWithFile("uap_lines", "data/multi.txt", "line1\r\nline2\r\nline3");
        try
        {
            using var provider = new UnifiedAssetProvider(
                diskRoot: root,
                pakPaths: System.Array.Empty<string>());
            var lines = provider.ReadAllLines("data/multi.txt");
            Assert.Equal(3, lines.Length);
            Assert.Equal("line1", lines[0]);
            Assert.Equal("line2", lines[1]);
            Assert.Equal("line3", lines[2]);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void OpenRead_ReturnsReadableStream()
    {
        var root = CreateTempDirWithFile("uap_stream", "data/stream.txt", "stream-content");
        try
        {
            using var provider = new UnifiedAssetProvider(
                diskRoot: root,
                pakPaths: System.Array.Empty<string>());

            using var stream = provider.OpenRead("data/stream.txt");
            using var reader = new System.IO.StreamReader(stream);
            Assert.Equal("stream-content", reader.ReadToEnd());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void LocalePath_AppliesToPakResolution()
    {
        var pak = CreateTempPak("uap_locale.aris",
            PakArchiveV3.Category.Scenario,
            ("scenario/ja-JP/main.aria", System.Text.Encoding.UTF8.GetBytes("ja-content")),
            ("scenario/en-US/main.aria", System.Text.Encoding.UTF8.GetBytes("en-content")));

        try
        {
            // Disk-first mode with locale
            using (var providerJa = new UnifiedAssetProvider(
                diskRoot: null,
                pakPaths: new[] { pak },
                locale: "ja-JP",
                diskFirst: true))
            {
                Assert.Equal("ja-content", providerJa.ReadAllText("scenario/main.aria"));
            }

            using (var providerEn = new UnifiedAssetProvider(
                diskRoot: null,
                pakPaths: new[] { pak },
                locale: "en-US",
                diskFirst: true))
            {
                Assert.Equal("en-content", providerEn.ReadAllText("scenario/main.aria"));
            }
        }
        finally
        {
            File.Delete(pak);
        }
    }

    [Fact]
    public void PakPatch_OverridesBase()
    {
        var basePak = CreateTempPak("uap_patch_base.arid",
            PakArchiveV3.Category.Data,
            ("data/x.txt", System.Text.Encoding.UTF8.GetBytes("base")));
        var patchPak = CreateTempPak("uap_patch_override.arid",
            PakArchiveV3.Category.Update,
            ("data/x.txt", System.Text.Encoding.UTF8.GetBytes("patch")));

        try
        {
            using var provider = new UnifiedAssetProvider(
                diskRoot: null,
                pakPaths: new[] { basePak },
                patchPaths: new[] { patchPak });

            // Patch should win
            Assert.Equal("patch", provider.ReadAllText("data/x.txt"));
        }
        finally
        {
            File.Delete(basePak);
            File.Delete(patchPak);
        }
    }

    [Fact]
    public void MaterializeToFile_DiskProvider_ReturnsRealPath()
    {
        var root = CreateTempDirWithFile("uap_mat", "data/file.txt", "content");
        try
        {
            using var provider = new UnifiedAssetProvider(
                diskRoot: root,
                pakPaths: System.Array.Empty<string>());

            string result = provider.MaterializeToFile("data/file.txt");
            Assert.True(File.Exists(result));
            Assert.Equal("content", File.ReadAllText(result));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void MaterializeToFile_RejectsPathTraversal()
    {
        var root = CreateTempDirWithFile("uap_trav", "data/x.txt", "x");
        try
        {
            using var provider = new UnifiedAssetProvider(
                diskRoot: root,
                pakPaths: System.Array.Empty<string>());
            Assert.Throws<ArgumentException>(() => provider.MaterializeToFile("../etc/passwd"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void MaterializeToFile_RejectsRootedPath()
    {
        var root = CreateTempDirWithFile("uap_rooted", "data/x.txt", "x");
        try
        {
            using var provider = new UnifiedAssetProvider(
                diskRoot: root,
                pakPaths: System.Array.Empty<string>());
            Assert.Throws<ArgumentException>(() => provider.MaterializeToFile("/etc/passwd"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void MaterializeToFile_PakSource_MaterializesToTemp()
    {
        var pak = CreateTempPak("uap_mat_pak.arid",
            PakArchiveV3.Category.Data,
            ("data/pakonly.txt", System.Text.Encoding.UTF8.GetBytes("pak-payload")));
        try
        {
            using var provider = new UnifiedAssetProvider(
                diskRoot: null,
                pakPaths: new[] { pak });

            string result = provider.MaterializeToFile("data/pakonly.txt");
            Assert.True(File.Exists(result));
            Assert.Equal("pak-payload", File.ReadAllText(result));
        }
        finally
        {
            File.Delete(pak);
        }
    }

    [Fact]
    public void Dispose_CleansUpTempDirs()
    {
        var pak = CreateTempPak("uap_dispose.arid",
            PakArchiveV3.Category.Data,
            ("data/x.txt", System.Text.Encoding.UTF8.GetBytes("x")));
        try
        {
            var provider = new UnifiedAssetProvider(
                diskRoot: null,
                pakPaths: new[] { pak });
            string materialized = provider.MaterializeToFile("data/x.txt");
            string tempDir = Path.GetDirectoryName(materialized)!;
            Assert.True(Directory.Exists(tempDir));

            provider.Dispose();
            Assert.False(Directory.Exists(tempDir));
        }
        finally
        {
            File.Delete(pak);
        }
    }

    [Fact]
    public void Constructor_RejectsEmptyConfig()
    {
        Assert.Throws<ArgumentException>(() =>
            new UnifiedAssetProvider(diskRoot: null, pakPaths: System.Array.Empty<string>()));
    }

    [Fact]
    public void HitCount_IncrementsCorrectly()
    {
        var root = CreateTempDirWithFile("uap_stats", "data/x.txt", "x");
        var pak = CreateTempPak("uap_stats.arid",
            PakArchiveV3.Category.Data,
            ("data/y.txt", System.Text.Encoding.UTF8.GetBytes("y")));
        try
        {
            using var provider = new UnifiedAssetProvider(
                diskRoot: root,
                pakPaths: new[] { pak },
                diskFirst: true);

            int initialDisk = provider.DiskHitCount;
            int initialPak = provider.PakHitCount;

            provider.ReadAllBytes("data/x.txt");
            Assert.True(provider.DiskHitCount > initialDisk);

            provider.ReadAllBytes("data/y.txt");
            Assert.True(provider.PakHitCount > initialPak);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
            File.Delete(pak);
        }
    }
}
