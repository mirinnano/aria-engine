#nullable enable

using FluentAssertions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using Xunit;

namespace AriaEngine.Tests;

public sealed class WebAssetManifestTests
{
    private static string RepoRoot => Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    [Fact]
    public void CheckedManifest_CoversEveryAssetWithCurrentIntegrityMetadata()
    {
        string manifestPath = Path.Combine(
            RepoRoot,
            "src",
            "AriaEngine.Wasm",
            "wwwroot",
            "aria-web-assets.json");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(manifestPath));
        JsonElement[] entries = document.RootElement.GetProperty("assets").EnumerateArray().ToArray();

        var expectedPaths = Directory
            .EnumerateFiles(Path.Combine(RepoRoot, "src", "AriaEngine", "assets"), "*", SearchOption.AllDirectories)
            .Select(path => "assets/" + Path.GetRelativePath(
                Path.Combine(RepoRoot, "src", "AriaEngine", "assets"),
                path).Replace('\\', '/'))
            .Append("init.aria")
            .ToHashSet(StringComparer.Ordinal);
        var actualPaths = entries
            .Select(entry => entry.GetProperty("logicalPath").GetString()!)
            .ToHashSet(StringComparer.Ordinal);

        actualPaths.Should().BeEquivalentTo(expectedPaths);
        foreach (JsonElement entry in entries)
        {
            string logicalPath = entry.GetProperty("logicalPath").GetString()!;
            string sourcePath = logicalPath == "init.aria"
                ? Path.Combine(RepoRoot, "src", "AriaEngine", "init.aria")
                : Path.Combine(RepoRoot, "src", "AriaEngine", logicalPath.Replace('/', Path.DirectorySeparatorChar));
            byte[] bytes = File.ReadAllBytes(sourcePath);
            entry.GetProperty("size").GetInt64().Should().Be(bytes.LongLength, logicalPath);
            entry.GetProperty("sha256").GetString().Should().Be(
                Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(),
                logicalPath);
        }
    }

    [Fact]
    public void CheckedManifest_HasBootUiAndEveryScenarioGroup()
    {
        string manifestPath = Path.Combine(
            RepoRoot,
            "src",
            "AriaEngine.Wasm",
            "wwwroot",
            "aria-web-assets.json");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(manifestPath));
        JsonElement[] entries = document.RootElement.GetProperty("assets").EnumerateArray().ToArray();
        HashSet<string> groups = entries
            .Select(entry => entry.GetProperty("group").GetString()!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        groups.Should().Contain("boot").And.Contain("ui");
        groups.Should().Contain(Enumerable.Range(1, 8).Select(index => $"scenario_{index:00}"));
        entries
            .Where(entry => entry.GetProperty("group").GetString() == "boot")
            .Select(entry => entry.GetProperty("logicalPath").GetString()!)
            .Should().NotContain(path =>
                path.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(".ico", StringComparison.OrdinalIgnoreCase));
    }
}
