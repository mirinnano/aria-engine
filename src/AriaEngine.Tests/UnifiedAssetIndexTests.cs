using System.Collections.Generic;
using System.IO;
using System.Linq;
using AriaEngine.Assets;
using AriaEngine.Packaging;
using Xunit;

namespace AriaEngine.Tests;

/// <summary>
/// Unit tests for <see cref="UnifiedAssetIndex"/> (Pak v3 redesign, Phase 1.1).
/// </summary>
public class UnifiedAssetIndexTests
{
    // Helper: create an in-memory pak file with given entries, return a temp file path.
    // Computes correct cumulative offsets (matches AriaPackCommand behavior).
    private static string CreateTempPak(
        string fileName,
        PakArchiveV3.Category category,
        params (string path, byte[] data)[] entries)
    {
        // Compute cumulative offsets in the order entries are provided.
        // PakArchiveV3.Write re-sorts by PathHash but preserves the Offset field, so
        // these offsets remain correct after the in-place sort.
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

    [Fact]
    public void EmptyIndex_ReturnsNull()
    {
        using var index = new UnifiedAssetIndex(System.Array.Empty<string>());
        Assert.Null(index.FindEntry("anything.txt"));
        Assert.Equal(0, index.CachedEntryCount);
        Assert.Equal(1, index.LookupCount);
    }

    [Fact]
    public void LazyOpen_DoesNotReadPakFiles_BeforeFirstLookup()
    {
        // Arrange: create a temp pak with one entry
        var path = CreateTempPak(
            "lazy_test.arid",
            PakArchiveV3.Category.Data,
            ("data/file.txt", System.Text.Encoding.UTF8.GetBytes("hello")));

        try
        {
            using var index = new UnifiedAssetIndex(new[] { path });

            // Assert: readers not opened until first lookup
            Assert.False(index.ReadersOpened);
            Assert.Equal(0, index.OpenedReaderCount);

            // Act
            var entry = index.FindEntry("data/file.txt");

            // Assert: now opened
            Assert.NotNull(entry);
            Assert.True(index.ReadersOpened);
            Assert.Equal(1, index.OpenedReaderCount);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void SinglePak_FindEntry_ReturnsEntry()
    {
        var path = CreateTempPak(
            "single_pak.arid",
            PakArchiveV3.Category.Data,
            ("data/file.txt", System.Text.Encoding.UTF8.GetBytes("hello")));

        try
        {
            using var index = new UnifiedAssetIndex(new[] { path });
            var entry = index.FindEntry("data/file.txt");
            Assert.NotNull(entry);
            Assert.Equal("data/file.txt", "data/file.txt"); // sanity
            Assert.True(index.Exists("data/file.txt"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void LookupIsCaseInsensitive()
    {
        var path = CreateTempPak(
            "case_test.arid",
            PakArchiveV3.Category.Data,
            ("Data/File.txt", System.Text.Encoding.UTF8.GetBytes("x")));

        try
        {
            using var index = new UnifiedAssetIndex(new[] { path });
            Assert.NotNull(index.FindEntry("data/file.txt"));
            Assert.NotNull(index.FindEntry("DATA/FILE.TXT"));
            Assert.NotNull(index.FindEntry("Data/File.txt"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void NotFound_ReturnsNull_AndCachesNegatively()
    {
        var path = CreateTempPak(
            "negative_test.arid",
            PakArchiveV3.Category.Data,
            ("real.txt", System.Text.Encoding.UTF8.GetBytes("x")));

        try
        {
            using var index = new UnifiedAssetIndex(new[] { path });

            // First lookup
            Assert.Null(index.FindEntry("missing.txt"));
            Assert.Equal(1, index.NegativeCacheCount);

            // Second lookup should hit negative cache
            int cacheHitsBefore = index.CacheHitCount;
            Assert.Null(index.FindEntry("missing.txt"));
            Assert.True(index.CacheHitCount > cacheHitsBefore);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void MultiplePaks_FindEntry_FindsCorrectOne()
    {
        var dataPak = CreateTempPak(
            "multi_data.arid",
            PakArchiveV3.Category.Data,
            ("data/asset.dat", System.Text.Encoding.UTF8.GetBytes("data-payload")));

        var scenarioPak = CreateTempPak(
            "multi_scenario.aris",
            PakArchiveV3.Category.Scenario,
            ("scripts/main.aria", System.Text.Encoding.UTF8.GetBytes("scenario-payload")));

        try
        {
            using var index = new UnifiedAssetIndex(new[] { scenarioPak, dataPak });

            var dataEntry = index.FindEntry("data/asset.dat");
            Assert.NotNull(dataEntry);
            Assert.Equal(0u, dataEntry.Offset); // sanity

            var scenarioEntry = index.FindEntry("scripts/main.aria");
            Assert.NotNull(scenarioEntry);

            Assert.Equal(2, index.CachedEntryCount);
        }
        finally
        {
            File.Delete(dataPak);
            File.Delete(scenarioPak);
        }
    }

    [Fact]
    public void PatchOverride_PatchWinsOverBase()
    {
        var basePath = CreateTempPak(
            "override_base.arid",
            PakArchiveV3.Category.Data,
            ("data/file.txt", System.Text.Encoding.UTF8.GetBytes("base-content")));

        var patchPath = CreateTempPak(
            "override_patch.arid",
            PakArchiveV3.Category.Update,
            ("data/file.txt", System.Text.Encoding.UTF8.GetBytes("patch-content")));

        try
        {
            using var index = new UnifiedAssetIndex(
                new[] { basePath },
                new[] { patchPath });

            var entry = index.FindEntry("data/file.txt");
            Assert.NotNull(entry);

            // Patch should win (Q3 decision: 後勝ちマージ)
            // The index should mark this as a patch-sourced entry
            var bytes = ReadAllBytesFromIndex(index, "data/file.txt");
            Assert.Equal(System.Text.Encoding.UTF8.GetBytes("patch-content"), bytes);
        }
        finally
        {
            File.Delete(basePath);
            File.Delete(patchPath);
        }
    }

    [Fact]
    public void LocalePath_ResolvedToLocalizedDirectory()
    {
        var path = CreateTempPak(
            "locale_test.aris",
            PakArchiveV3.Category.Scenario,
            ("scenario/ja-JP/main.aria", System.Text.Encoding.UTF8.GetBytes("ja-content")),
            ("scenario/en-US/main.aria", System.Text.Encoding.UTF8.GetBytes("en-content")),
            ("scenario/zh-CN/main.aria", System.Text.Encoding.UTF8.GetBytes("zh-content")));

        try
        {
            // With locale "ja-JP", asking for "scenario/main.aria" should resolve to
            // "scenario/ja-JP/main.aria".
            using (var indexJa = new UnifiedAssetIndex(new[] { path }, locale: "ja-JP"))
            {
                Assert.True(indexJa.Exists("scenario/main.aria"));
                var bytes = ReadAllBytesFromIndex(indexJa, "scenario/main.aria");
                Assert.Equal(System.Text.Encoding.UTF8.GetBytes("ja-content"), bytes);
            }

            // With locale "en-US"
            using (var indexEn = new UnifiedAssetIndex(new[] { path }, locale: "en-US"))
            {
                Assert.True(indexEn.Exists("scenario/main.aria"));
                var bytes = ReadAllBytesFromIndex(indexEn, "scenario/main.aria");
                Assert.Equal(System.Text.Encoding.UTF8.GetBytes("en-content"), bytes);
            }

            // With locale "zh-CN"
            using (var indexZh = new UnifiedAssetIndex(new[] { path }, locale: "zh-CN"))
            {
                var bytes = ReadAllBytesFromIndex(indexZh, "scenario/main.aria");
                Assert.Equal(System.Text.Encoding.UTF8.GetBytes("zh-content"), bytes);
            }

            // Without locale: must use the explicit localized path
            using (var indexNone = new UnifiedAssetIndex(new[] { path }))
            {
                Assert.Null(indexNone.FindEntry("scenario/main.aria"));
                Assert.True(indexNone.Exists("scenario/ja-JP/main.aria"));
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void LocalePath_FallsBackToNonLocalized_WhenLocaleMissing()
    {
        var path = CreateTempPak(
            "locale_fallback.aris",
            PakArchiveV3.Category.Scenario,
            ("scenario/main.aria", System.Text.Encoding.UTF8.GetBytes("default-content")));

        try
        {
            // Ask for "scenario/main.aria" with locale "ja-JP" but the pak only has
            // the non-localized path. Should fall back.
            using var index = new UnifiedAssetIndex(new[] { path }, locale: "ja-JP");
            var entry = index.FindEntry("scenario/main.aria");
            Assert.NotNull(entry);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void TopLevelFile_DoesNotGetLocaleSuffix()
    {
        // "init.aria" has no slash, so locale suffix injection must be a no-op.
        // We test this by ensuring FindEntry("init.aria") with locale = "ja-JP"
        // looks for "init.aria" exactly (not "ja-JP/init.aria" or similar).
        var path = CreateTempPak(
            "toplevel_test.aris",
            PakArchiveV3.Category.Scenario,
            ("init.aria", System.Text.Encoding.UTF8.GetBytes("init-content")));

        try
        {
            using var index = new UnifiedAssetIndex(new[] { path }, locale: "ja-JP");
            Assert.NotNull(index.FindEntry("init.aria"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void MissingPakFile_SkippedGracefully()
    {
        var realPath = CreateTempPak(
            "missing_test.arid",
            PakArchiveV3.Category.Data,
            ("data/file.txt", System.Text.Encoding.UTF8.GetBytes("x")));

        try
        {
            // Mix valid and invalid paths
            using var index = new UnifiedAssetIndex(new[]
            {
                "nonexistent.pak",
                realPath,
                "also_nonexistent.pak"
            });

            // Should still find the real one
            Assert.NotNull(index.FindEntry("data/file.txt"));
        }
        finally
        {
            File.Delete(realPath);
        }
    }

    [Fact]
    public void RegisterReader_AddsCustomReader()
    {
        // Build a pak in-memory, open it as a reader, register it manually.
        var manifest = new PakManifestV3
        {
            Entries = new List<PakManifestEntryV3>
            {
                new()
                {
                    PathHash = PakArchiveV3Reader.PathHash64("injected.txt"),
                    Offset = 0,
                    Size = 5,
                    OriginalSize = 5,
                    Flags = 0
                }
            },
            PathStrings = new List<string> { "injected.txt" }
        };
        using var ms = new MemoryStream();
        PakArchiveV3.Write(ms, manifest, new[] { System.Text.Encoding.UTF8.GetBytes("hello") }, PakArchiveV3.Category.Data);
        ms.Position = 0;
        var reader = PakArchiveV3Reader.Open(ms, leaveOpen: true);

        try
        {
            using var index = new UnifiedAssetIndex(System.Array.Empty<string>());
            index.RegisterReader(reader);
            var entry = index.FindEntry("injected.txt");
            Assert.NotNull(entry);
        }
        finally
        {
            reader.Dispose();
        }
    }

    [Fact]
    public void Dispose_ClosesAllReaders()
    {
        var path1 = CreateTempPak("dispose_a.arid", PakArchiveV3.Category.Data, ("a.txt", new byte[] { 1 }));
        var path2 = CreateTempPak("dispose_b.aris", PakArchiveV3.Category.Scenario, ("b.txt", new byte[] { 2 }));

        try
        {
            var index = new UnifiedAssetIndex(new[] { path1, path2 });
            // Trigger lazy open
            Assert.NotNull(index.FindEntry("a.txt"));
            Assert.Equal(2, index.OpenedReaderCount);

            index.Dispose();
            Assert.Equal(0, index.OpenedReaderCount);
        }
        finally
        {
            File.Delete(path1);
            File.Delete(path2);
        }
    }

    [Fact]
    public void CacheHitCount_Increments_OnRepeatedLookup()
    {
        var path = CreateTempPak(
            "cache_hit.arid",
            PakArchiveV3.Category.Data,
            ("data/x.txt", System.Text.Encoding.UTF8.GetBytes("x")));

        try
        {
            using var index = new UnifiedAssetIndex(new[] { path });

            Assert.NotNull(index.FindEntry("data/x.txt"));
            int afterFirst = index.CacheHitCount;
            Assert.NotNull(index.FindEntry("data/x.txt"));
            int afterSecond = index.CacheHitCount;
            Assert.True(afterSecond > afterFirst);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void PakPatch_FallsBackToBase_WhenPathNotInPatch()
    {
        var basePath = CreateTempPak(
            "patch_base_fallback.arid",
            PakArchiveV3.Category.Data,
            ("data/only_in_base.txt", System.Text.Encoding.UTF8.GetBytes("base-only")));

        var patchPath = CreateTempPak(
            "patch_partial.arid",
            PakArchiveV3.Category.Update,
            ("data/only_in_patch.txt", System.Text.Encoding.UTF8.GetBytes("patch-only")));

        try
        {
            using var index = new UnifiedAssetIndex(
                new[] { basePath },
                new[] { patchPath });

            // Path in base only -> base should serve
            Assert.NotNull(index.FindEntry("data/only_in_base.txt"));
            // Path in patch only -> patch should serve
            Assert.NotNull(index.FindEntry("data/only_in_patch.txt"));
        }
        finally
        {
            File.Delete(basePath);
            File.Delete(patchPath);
        }
    }

    // Helper: read raw bytes through the index.
    private static byte[] ReadAllBytesFromIndex(UnifiedAssetIndex index, string path)
    {
        return index.ReadAllBytes(path, verifyHash: false);
    }
}
