using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace AriaEngine.Assets;

/// <summary>
/// UnifiedAssetProvider (Pak v3 redesign, Phase 1.2)
///
/// IAssetProvider implementation that combines <see cref="DiskAssetProvider"/>
/// (dev mode, filesystem) with <see cref="UnifiedAssetIndex"/> (release mode, pak).
/// The mode is selected by the <c>diskFirst</c> flag:
///
///   diskFirst=true   → dev mode: try disk first, fall back to pak. This is the
///                      umikaze-dev workflow where artists can drop files into
///                      <c>assets/</c> and see them without rebuilding the pak.
///
///   diskFirst=false  → release mode: try pak first, fall back to disk. Used for
///                      "patch override" workflows where a small set of files
///                      override the pak without rebuilding the full release.
///
/// No caching or decompression at this layer (Phase 3 adds AssetRegistry).
/// </summary>
public sealed class UnifiedAssetProvider : IAssetProvider, IDisposable
{
    private readonly DiskAssetProvider? _diskProvider;
    private readonly UnifiedAssetIndex? _index;
    private readonly bool _diskFirst;
    private readonly object _lock = new();
    private readonly List<string> _tempDirs = new();

    /// <summary>Stats — observable for tests and benchmarks.</summary>
    public int DiskHitCount { get; private set; }
    public int PakHitCount { get; private set; }
    public int TotalReadCount { get; private set; }

    /// <summary>Underlying index (null if no pak configured).</summary>
    public UnifiedAssetIndex? Index => _index;

    /// <summary>Underlying disk provider (null if no disk root).</summary>
    public DiskAssetProvider? DiskProvider => _diskProvider;

    public UnifiedAssetProvider(
        string? diskRoot,
        IEnumerable<string> pakPaths,
        IEnumerable<string>? patchPaths = null,
        string? locale = null,
        bool diskFirst = true)
    {
        if (!string.IsNullOrWhiteSpace(diskRoot))
        {
            _diskProvider = new DiskAssetProvider(diskRoot);
        }

        var paks = pakPaths?.Where(p => !string.IsNullOrWhiteSpace(p)).ToArray()
                   ?? Array.Empty<string>();
        var patches = patchPaths?.Where(p => !string.IsNullOrWhiteSpace(p)).ToArray()
                      ?? Array.Empty<string>();

        if (paks.Length > 0 || patches.Length > 0)
        {
            _index = new UnifiedAssetIndex(paks, patches, locale);
        }
        else if (string.IsNullOrWhiteSpace(diskRoot))
        {
            throw new ArgumentException(
                "UnifiedAssetProvider requires either a diskRoot or at least one pak/patch path.");
        }

        _diskFirst = diskFirst;
    }

    public bool Exists(string path)
    {
        if (string.IsNullOrEmpty(path)) return false;
        return TryFind(path, countStats: true).exists;
    }

    public string[] ReadAllLines(string path)
    {
        string text = ReadAllText(path);
        return text.Replace("\r\n", "\n").Split('\n');
    }

    public string ReadAllText(string path)
    {
        byte[] bytes = ReadAllBytes(path);
        return Encoding.UTF8.GetString(bytes);
    }

    public byte[] ReadAllBytes(string path)
    {
        if (string.IsNullOrEmpty(path))
            throw new ArgumentException("path is empty", nameof(path));

        var (exists, isDisk) = TryFind(path, countStats: true);
        if (!exists)
            throw new FileNotFoundException($"Asset not found: {path}");
        return isDisk
            ? _diskProvider!.ReadAllBytes(path)
            : _index!.ReadAllBytes(path);
    }

    private (bool exists, bool isDisk) TryFind(string path, bool countStats)
    {
        lock (_lock) { TotalReadCount++; }

        if (_diskFirst)
        {
            if (_diskProvider != null && _diskProvider.Exists(path))
            {
                if (countStats) DiskHitCount++;
                return (true, true);
            }
            if (_index != null && _index.Exists(path))
            {
                if (countStats) PakHitCount++;
                return (true, false);
            }
        }
        else
        {
            if (_index != null && _index.Exists(path))
            {
                if (countStats) PakHitCount++;
                return (true, false);
            }
            if (_diskProvider != null && _diskProvider.Exists(path))
            {
                if (countStats) DiskHitCount++;
                return (true, true);
            }
        }
        return (false, false);
    }

    public Stream OpenRead(string path)
    {
        byte[] bytes = ReadAllBytes(path);
        return new MemoryStream(bytes, writable: false);
    }

    public bool CanMaterializeToFile =>
        _diskProvider != null || _index != null;

    public string MaterializeToFile(string path)
    {
        if (string.IsNullOrEmpty(path))
            throw new ArgumentException("path is empty", nameof(path));

        string normalized = path.Replace('\\', '/');
        if (normalized.Contains(".."))
            throw new ArgumentException("Path contains invalid traversal characters");
        if (Path.IsPathRooted(normalized))
            throw new ArgumentException("Path must be relative");

        if (_diskFirst)
        {
            if (_diskProvider != null && _diskProvider.Exists(path))
                return _diskProvider.MaterializeToFile(path);
            if (_index != null && _index.Exists(path))
                return MaterializeFromIndex(path);
        }
        else
        {
            if (_index != null && _index.Exists(path))
                return MaterializeFromIndex(path);
            if (_diskProvider != null && _diskProvider.Exists(path))
                return _diskProvider.MaterializeToFile(path);
        }
        throw new FileNotFoundException($"Asset not found: {path}");
    }

    private string MaterializeFromIndex(string path)
    {
        if (_index == null)
            throw new InvalidOperationException("Index is null");
        byte[] bytes = _index.ReadAllBytes(path);
        string tempRoot = Path.Combine(Path.GetTempPath(), "aria_unified", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        lock (_lock) { _tempDirs.Add(tempRoot); }
        string normalized = path.Replace('/', Path.DirectorySeparatorChar);
        string fullPath = Path.Combine(tempRoot, normalized);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllBytes(fullPath, bytes);
        return fullPath;
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _index?.Dispose();
            foreach (var dir in _tempDirs)
            {
                try { Directory.Delete(dir, recursive: true); } catch { /* ignore */ }
            }
            _tempDirs.Clear();
        }
    }
}
