using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using AriaEngine.Packaging;
using AriaEngine.Packaging.Compression;
using AriaEngine.Assets; // for IAssetProvider namespace compatibility
// Note: This is a v3 implementation scaffold that relies on PakArchiveV3Reader,
// Lz4Compression and ZstdCompression APIs that are introduced in Pak v3 phase.

namespace AriaEngine.Assets;

public sealed class PakAssetProviderV3 : IAssetProvider, IDisposable
{
    private readonly string[] _pakPaths;
    private readonly string? _keyMaterial;

    // Readers for each pak along with derived category. Maintain search order.
    private readonly List<(PakArchiveV3Reader Reader, string Category)> _pakReaders = new();

    // Simple LRU caches per category (data, voice) and broad caches for others.
    private readonly Dictionary<string, CacheEntry> _dataCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly LinkedList<string> _dataLru = new();
    private long _dataCachedBytes = 0;

    private readonly Dictionary<string, CacheEntry> _voiceCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly LinkedList<string> _voiceLru = new();
    private long _voiceCachedBytes = 0;

    private readonly Dictionary<string, CacheEntry> _streamCache = new(StringComparer.OrdinalIgnoreCase);

    // Track temp directories created by MaterializeToFile for cleanup in Dispose
    private readonly List<string> _tempDirs = new();

    private static string DetermineCategoryFromExtension(string pakPath)
    {
        string ext = Path.GetExtension(pakPath).ToLowerInvariant();
        return ext switch
        {
            ".arib" => "boot",
            ".aris" => "scenario",
            ".arid" => "data",
            ".arim" => "stream",
            ".ariv" => "voice",
            ".ariu" => "update",
            _ => "data",
        };
    }
   
    // Scenario/Boot keep-alives (small; simply keep in memory maps)
    private readonly Dictionary<string, CacheEntry> _scenarioCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, CacheEntry> _bootCache = new(StringComparer.OrdinalIgnoreCase);

    // Per-cache locks to ensure thread-safety for reads/writes of caches
    private readonly object _scenarioLock = new();
    private readonly object _bootLock = new();

    // Cache entry container
    private class CacheEntry
    {
        public byte[] Data = Array.Empty<byte>();
        public long OriginalSize = 0;
        public bool IsCompressed = false;
        public DateTime CachedAt = DateTime.UtcNow;
    }

    // Capacity settings (as per requirements)
    private const long DataCacheBytesLimit = 256 * 1024 * 1024; // 256 MB
    internal static int DataCacheEntriesLimit = 64;
    private const long VoiceCacheBytesLimit = 128 * 1024 * 1024; // 128 MB
    internal static int VoiceCacheEntriesLimit = 128;

        public PakAssetProviderV3(string[] pakPaths, string? keyMaterial = null)
    {
        _pakPaths = pakPaths ?? throw new ArgumentNullException(nameof(pakPaths));
        _keyMaterial = keyMaterial;

        byte[]? key = string.IsNullOrWhiteSpace(_keyMaterial) ? null : CryptoHelper.DeriveKey(_keyMaterial);

        // Open all provided pak paths with v3 reader. We keep dynamic to avoid tight coupling
        // to exact API surface of PakArchiveV3Reader.
        foreach (var path in _pakPaths)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException($"Pak file not found: {path}");

            try
            {
                PakArchiveV3Reader reader = PakArchiveV3Reader.Open(path);
                string category = DetermineCategoryFromExtension(path);
                _pakReaders.Add((reader, category));
            }
            catch (Exception ex)
            {
                // If a specific pak fails to open, skip it but keep going with others.
                // We surface the error only in verbose logs in real scenarios.
                Console.Error.WriteLine($"Failed to open v3 pak '{path}': {ex.Message}");
            }
        }
    }

    public void Dispose()
    {
        foreach (var (reader, _) in _pakReaders)
        {
            try { reader.Dispose(); } catch { /* ignore */ }
        }
        foreach (var tempDir in _tempDirs)
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { /* ignore cleanup failures */ }
        }
    }

    public bool Exists(string path) => TryFindEntry(path, out _, out _, out _, out _);

    public string[] ReadAllLines(string path)
    {
        string text = ReadAllText(path);
        // Normalize line endings for cross-platform parity
        return text.Replace("\r\n", "\n").Split('\n');
    }

    public string ReadAllText(string path)
    {
        byte[] bytes = ReadAllBytesInternal(path);
        return Encoding.UTF8.GetString(bytes);
    }

    public Stream OpenRead(string path)
    {
        byte[] bytes = ReadAllBytesInternal(path);
        return new MemoryStream(bytes, writable: false);
    }

    public string MaterializeToFile(string path)
    {
        string normalized = NormalizePath(path);
        if (normalized.Contains(".."))
            throw new ArgumentException("Path contains invalid traversal characters");
        if (Path.IsPathRooted(normalized))
            throw new ArgumentException("Path must be relative");
        // Simple file-based materialization in temp cache
        string tempRoot = Path.Combine(Path.GetTempPath(), "aria_pak3_cache", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        _tempDirs.Add(tempRoot);
        string fullPath = Path.Combine(tempRoot, normalized.Replace('/', Path.DirectorySeparatorChar));

        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllBytes(fullPath, ReadAllBytesInternal(path));
        return fullPath;
    }

    // Removed legacy TryResolveLogical in favor of direct reader-specific resolution

    // Core read path implementation that handles decompression and caching.
    private byte[] ReadAllBytesInternal(string path)
    {
        string normalizedPath = NormalizePath(path);

        // Early cache hot-path: check caches before touching disk
        // Data cache
        lock (_dataCache)
        {
            if (_dataCache.TryGetValue(normalizedPath, out var ceData))
            {
                ceData.CachedAt = DateTime.UtcNow;
                // Move to most-recent in LRU
                var node = _dataLru.Find(normalizedPath);
                if (node != null) _dataLru.Remove(node);
                _dataLru.AddLast(normalizedPath);
                return ceData.Data;
            }
        }
        // Voice cache
        lock (_voiceCache)
        {
            if (_voiceCache.TryGetValue(normalizedPath, out var ceVoice))
            {
                ceVoice.CachedAt = DateTime.UtcNow;
                var node = _voiceLru.Find(normalizedPath);
                if (node != null) _voiceLru.Remove(node);
                _voiceLru.AddLast(normalizedPath);
                return ceVoice.Data;
            }
        }
        // Scenario cache
        lock (_scenarioLock)
        {
            if (_scenarioCache.TryGetValue(normalizedPath, out var ceScenario))
            {
                ceScenario.CachedAt = DateTime.UtcNow;
                return ceScenario.Data;
            }
        }
        // Boot cache
        lock (_bootLock)
        {
            if (_bootCache.TryGetValue(normalizedPath, out var ceBoot))
            {
                ceBoot.CachedAt = DateTime.UtcNow;
                return ceBoot.Data;
            }
        }
        if (!TryFindEntry(path, out var entry, out string resolvedPath, out var foundReader, out string categoryForReader))
        {
            throw new FileNotFoundException($"Pak entry not found: {path}");
        }

        // Use the reader that TryFindEntry already found - no need to search again
        var readerChosen = foundReader!;

        byte[] data = readerChosen.ReadAllBytes(resolvedPath, verifyHash: true);
        bool compressed = false;
        try
        {
            int flags = (int)entry!.Flags;
            compressed = (flags & 0x02) != 0;
        }
        catch { /* ignore if not exposed */ }

        if (compressed)
        {
            int originalSize = data.Length;
            try { originalSize = (int)entry!.OriginalSize; } catch { }
            if (categoryForReader == "data" || categoryForReader == "voice")
            {
                data = DecompressLz4(data, originalSize);
            }
            else if (categoryForReader == "scenario" || categoryForReader == "boot")
            {
                data = DecompressZstd(data, originalSize);
            }
        }

        // Cache based on path key and category
        if (categoryForReader == "data")
            CacheAdd(_dataCache, _dataLru, ref _dataCachedBytes, normalizedPath, data, data.Length);
        else if (categoryForReader == "voice")
            CacheAdd(_voiceCache, _voiceLru, ref _voiceCachedBytes, normalizedPath, data, data.Length);
        else if (categoryForReader == "scenario" || categoryForReader == "boot")
        {
            lock (_scenarioLock)
            {
                _scenarioCache[normalizedPath] = new CacheEntry { Data = data, OriginalSize = data.Length };
            }
        }
        // No explicit caching for stream in this initial pass
        return data;
    }

    private static readonly Lz4Compression _lz4 = new();
    private static readonly ZstdCompression _zstd = new();

    private static byte[] DecompressLz4(byte[] input, int originalSize)
    {
        return _lz4.Decompress(input);
    }

    private static byte[] DecompressZstd(byte[] input, int originalSize)
    {
        return _zstd.Decompress(input);
    }

    private void CacheAdd(Dictionary<string, CacheEntry> cache, LinkedList<string> lru, ref long cachedBytes, string key, byte[] data, long originalSize)
    {
        lock (cache)
        {
            if (cache.ContainsKey(key))
            {
                // refresh timestamp
                cache[key].CachedAt = DateTime.UtcNow;
                // Move the key to the most-recent position in the corresponding LRU
                if (cache == _dataCache)
                {
                    var node = _dataLru.Find(key);
                    if (node != null) _dataLru.Remove(node);
                    _dataLru.AddLast(key);
                }
                else if (cache == _voiceCache)
                {
                    var node = _voiceLru.Find(key);
                    if (node != null) _voiceLru.Remove(node);
                    _voiceLru.AddLast(key);
                }
                return;
            }
            cache[key] = new CacheEntry { Data = data, OriginalSize = originalSize };
            lru.AddLast(key);
            cachedBytes += data.Length;
            // Evict based on both byte-size and entry-count limits
            long byteLimit = (cache == _dataCache) ? DataCacheBytesLimit : VoiceCacheBytesLimit;
            int entryLimit = (cache == _dataCache) ? DataCacheEntriesLimit : VoiceCacheEntriesLimit;
            // Evict while we exceed either the byte-size limit or the max entry count
            while (cachedBytes > byteLimit || cache.Count > entryLimit)
            {
                if (lru.First is null) break;
                string oldKey = lru.First.Value;
                lru.RemoveFirst();
                if (cache.TryGetValue(oldKey, out var ce))
                {
                    cachedBytes -= ce.Data.Length;
                    cache.Remove(oldKey);
                }
            }
        }
    }

    private string NormalizePath(string path) => PakArchive.NormalizePath(path);
    
    // (duplicate removed) DetermineCategoryFromExtension defined above

    // Try to locate a reader containing the given path and return the manifest entry if present
    private bool TryFindEntry(string path, out PakManifestEntryV3? entry, out string resolvedPath, out PakArchiveV3Reader? foundReader, out string foundCategory)
    {
        string normalizedPath = PakArchive.NormalizePath(path);
        string prefixedPath;
        if (normalizedPath.StartsWith("assets/", StringComparison.OrdinalIgnoreCase))
        {
            prefixedPath = normalizedPath;
        }
        else
        {
            prefixedPath = PakArchive.NormalizePath("assets/" + path);
        }
        resolvedPath = normalizedPath;
        entry = null;
        foundReader = null;
        foundCategory = string.Empty;
        foreach (var (reader, category) in _pakReaders)
        {
            try
            {
                var e = reader.FindEntry(normalizedPath);
                if (e != null)
                {
                    entry = e;
                    foundReader = reader;
                    foundCategory = category;
                    return true;
                }
                e = reader.FindEntry(prefixedPath);
                if (e != null)
                {
                    entry = e;
                    resolvedPath = prefixedPath;
                    foundReader = reader;
                    foundCategory = category;
                    return true;
                }
            }
            catch { /* ignore */ }
        }
        return false;
    }

    // Async prefetch: preloads given paths into the cache in background
    public Task PrefetchAsync(string[] paths)
    {
        if (paths == null || paths.Length == 0) return Task.CompletedTask;
        return Task.Run(() =>
        {
            foreach (var p in paths)
            {
                try { ReadAllBytesInternal(p); } catch { /* ignore prefetch errors */ }
            }
        });
    }
}
