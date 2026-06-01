using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AriaEngine.Packaging;

namespace AriaEngine.Assets;

/// <summary>
/// UnifiedAssetIndex (Pak v3 redesign, Phase 1.1)
///
/// Single dictionary lookup across all 6 pak categories (boot/scenario/data/stream/voice/update).
/// Manifest parsing is lazy: readers are opened and entries are populated only on first
/// <see cref="FindEntry"/> call. This avoids the upfront parse cost at engine startup that
/// the original <c>PakAssetProviderV3</c> incurred by opening every pak in its constructor.
///
/// Resolved Decisions (2026-06-01):
///   Q3: Pak patch override — patch readers are searched first (後勝ち).
///   Q4: Locale path — locale suffix injected as <c>dir/locale/file</c>.
///   Q5: Mark-and-sweep upgrade is reserved for Phase 3; this class is read-only lookup
///       (write path lives in <c>AssetHandle&lt;T&gt;</c>, Phase 2.1).
/// </summary>
public sealed class UnifiedAssetIndex : IDisposable
{
    private readonly string[] _basePaths;
    private readonly string[] _patchPaths;
    private readonly string? _locale;
    private readonly object _lock = new();

    // Populated lazily on first FindEntry.
    private List<PakArchiveV3Reader>? _baseReaders;
    private List<PakArchiveV3Reader>? _patchReaders;

    // Normalized path -> resolved entry. OrdinalIgnoreCase.
    private readonly Dictionary<string, IndexedEntry> _index =
        new(StringComparer.OrdinalIgnoreCase);

    // Negative cache: paths we already know don't exist. Avoids repeated disk probing.
    private readonly HashSet<string> _negativeCache = new(StringComparer.OrdinalIgnoreCase);

    // Stats (read-only, observable for benchmarks)
    private int _lookupCount;
    private int _cacheHitCount;

    public UnifiedAssetIndex(
        IEnumerable<string> basePakPaths,
        IEnumerable<string>? patchPakPaths = null,
        string? locale = null)
    {
        if (basePakPaths == null) throw new ArgumentNullException(nameof(basePakPaths));
        _basePaths = basePakPaths.ToArray();
        _patchPaths = patchPakPaths?.ToArray() ?? Array.Empty<string>();
        _locale = string.IsNullOrWhiteSpace(locale) ? null : locale;
    }

    /// <summary>Locale used for path resolution (or null for default).</summary>
    public string? Locale => _locale;

    /// <summary>True if all base and patch readers have been opened at least once.</summary>
    public bool ReadersOpened => _baseReaders != null;

    /// <summary>Number of cached entries (positive lookups).</summary>
    public int CachedEntryCount
    {
        get { lock (_lock) return _index.Count; }
    }

    /// <summary>Number of negative cache entries (failed lookups).</summary>
    public int NegativeCacheCount
    {
        get { lock (_lock) return _negativeCache.Count; }
    }

    /// <summary>Total lookups performed.</summary>
    public int LookupCount => _lookupCount;

    /// <summary>Lookups that hit the cache (positive or negative).</summary>
    public int CacheHitCount => _cacheHitCount;

    /// <summary>True if any pak readers have been opened (lazy stats).</summary>
    public int OpenedReaderCount
    {
        get
        {
            int n = 0;
            if (_baseReaders != null) n += _baseReaders.Count;
            if (_patchReaders != null) n += _patchReaders.Count;
            return n;
        }
    }

    /// <summary>True if the given path is resolvable through the index.</summary>
    public bool Exists(string path) => FindEntry(path) != null;

    /// <summary>
    /// Read raw bytes for a path through the unified index. No decompression, no caching.
    /// Phase 1.2 wraps this with decompression + LRU; for now callers get the raw pak payload.
    /// </summary>
    public byte[] ReadAllBytes(string path, bool verifyHash = false)
    {
        if (string.IsNullOrEmpty(path)) throw new ArgumentException("path is empty", nameof(path));
        var (entry, reader) = ResolveEntryAndReader(path);
        if (entry == null || reader == null)
            throw new FileNotFoundException($"Pak entry not found: {path}");
        return reader.ReadAllBytes(GetResolvedPath(path), verifyHash: verifyHash);
    }

    /// <summary>
    /// Find a manifest entry by logical path. Returns null if not found.
    /// Thread-safe. The first call lazily opens all pak readers.
    /// </summary>
    public PakManifestEntryV3? FindEntry(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        string normalized = Normalize(path);
        string localized = ResolveLocalePath(normalized);

        lock (_lock)
        {
            _lookupCount++;

            // Fast path: positive cache hit
            if (_index.TryGetValue(localized, out var hit))
            {
                _cacheHitCount++;
                return hit.Entry;
            }

            // Fast path: negative cache hit
            if (_negativeCache.Contains(localized))
            {
                _cacheHitCount++;
                // Locale fallback: try base path
                if (localized != normalized && TryFindInReaders(normalized, preferPatch: true) is { } fb)
                    return fb;
                return null;
            }

            // Lazy: ensure readers are open
            EnsureReaders();

            // Patch first (override), then base
            var found = TryFindInReaders(localized, preferPatch: true);
            if (found != null) return found;

            // Locale fallback: try base path (no locale)
            if (localized != normalized)
            {
                if (_index.TryGetValue(normalized, out hit))
                {
                    _cacheHitCount++;
                    return hit.Entry;
                }
                found = TryFindInReaders(normalized, preferPatch: true);
                if (found != null) return found;
            }

            _negativeCache.Add(localized);
            return null;
        }
    }

    private PakManifestEntryV3? TryFindInReaders(string path, bool preferPatch)
    {
        if (preferPatch && _patchReaders != null)
        {
            foreach (var reader in _patchReaders)
            {
                var e = reader.FindEntry(path);
                if (e != null)
                {
                    _index[path] = new IndexedEntry(e, reader, isPatch: true);
                    return e;
                }
            }
        }
        if (_baseReaders != null)
        {
            foreach (var reader in _baseReaders)
            {
                var e = reader.FindEntry(path);
                if (e != null)
                {
                    _index[path] = new IndexedEntry(e, reader, isPatch: false);
                    return e;
                }
            }
        }
        if (!preferPatch && _patchReaders != null)
        {
            foreach (var reader in _patchReaders)
            {
                var e = reader.FindEntry(path);
                if (e != null)
                {
                    _index[path] = new IndexedEntry(e, reader, isPatch: true);
                    return e;
                }
            }
        }
        return null;
    }

    /// <summary>
    /// Resolve both the manifest entry and the reader that serves it.
    /// Returns (null, null) if not found. The reader is the one whose pak actually
    /// contains the path (after patch override + locale fallback).
    /// </summary>
    private (PakManifestEntryV3? entry, PakArchiveV3Reader? reader) ResolveEntryAndReader(string path)
    {
        string normalized = Normalize(path);
        string localized = ResolveLocalePath(normalized);

        lock (_lock)
        {
            EnsureReaders();

            // Patch first, then base; on the localized path first.
            var (entry, reader) = TryFindInReadersWithReader(localized, preferPatch: true);
            if (entry != null) return (entry, reader);

            // Locale fallback: try without locale suffix
            if (localized != normalized)
            {
                (entry, reader) = TryFindInReadersWithReader(normalized, preferPatch: true);
                if (entry != null) return (entry, reader);
            }
            return (null, null);
        }
    }

    private (PakManifestEntryV3? entry, PakArchiveV3Reader? reader) TryFindInReadersWithReader(string path, bool preferPatch)
    {
        if (preferPatch && _patchReaders != null)
        {
            foreach (var reader in _patchReaders)
            {
                var e = reader.FindEntry(path);
                if (e != null)
                {
                    _index[path] = new IndexedEntry(e, reader, isPatch: true);
                    return (e, reader);
                }
            }
        }
        if (_baseReaders != null)
        {
            foreach (var reader in _baseReaders)
            {
                var e = reader.FindEntry(path);
                if (e != null)
                {
                    _index[path] = new IndexedEntry(e, reader, isPatch: false);
                    return (e, reader);
                }
            }
        }
        if (!preferPatch && _patchReaders != null)
        {
            foreach (var reader in _patchReaders)
            {
                var e = reader.FindEntry(path);
                if (e != null)
                {
                    _index[path] = new IndexedEntry(e, reader, isPatch: true);
                    return (e, reader);
                }
            }
        }
        return (null, null);
    }

    /// <summary>
    /// Internal: get the cached resolved path (locale-rewritten) for a given logical path.
    /// Used by <see cref="ReadAllBytes"/> to pass the actual key to the reader's
    /// <c>ReadAllBytes</c> (which is keyed by the entry's stored path string).
    /// </summary>
    private string GetResolvedPath(string path)
    {
        string normalized = Normalize(path);
        string localized = ResolveLocalePath(normalized);

        lock (_lock)
        {
            // We rely on the index cache populated by ResolveEntryAndReader.
            // Pick whichever path the entry was actually cached under.
            if (_index.ContainsKey(localized)) return localized;
            if (_index.ContainsKey(normalized)) return normalized;
        }
        return localized;
    }

    private void EnsureReaders()
    {
        if (_baseReaders == null)
        {
            _baseReaders = new List<PakArchiveV3Reader>(_basePaths.Length);
            foreach (var path in _basePaths)
            {
                if (string.IsNullOrEmpty(path)) continue;
                if (!File.Exists(path)) continue;
                try
                {
                    _baseReaders.Add(PakArchiveV3Reader.Open(path));
                }
                catch
                {
                    // Skip invalid pak files; they should not bring down the whole index.
                }
            }
        }
        if (_patchReaders == null && _patchPaths.Length > 0)
        {
            _patchReaders = new List<PakArchiveV3Reader>(_patchPaths.Length);
            foreach (var path in _patchPaths)
            {
                if (string.IsNullOrEmpty(path)) continue;
                if (!File.Exists(path)) continue;
                try
                {
                    _patchReaders.Add(PakArchiveV3Reader.Open(path));
                }
                catch
                {
                    // Skip invalid patch files
                }
            }
        }
    }

    /// <summary>
    /// Inject a pre-opened reader directly. Test-only escape hatch and a future Phase 1.2
    /// entry point for streamed/synthetic providers. Skips the file existence check.
    /// </summary>
    public void RegisterReader(PakArchiveV3Reader reader, bool isPatch = false)
    {
        if (reader == null) throw new ArgumentNullException(nameof(reader));
        lock (_lock)
        {
            if (isPatch)
            {
                _patchReaders ??= new List<PakArchiveV3Reader>();
                _patchReaders.Add(reader);
            }
            else
            {
                _baseReaders ??= new List<PakArchiveV3Reader>();
                _baseReaders.Add(reader);
            }
        }
    }

    /// <summary>
    /// Resolve <c>scenario/main.aria</c> with locale <c>ja-JP</c> to <c>scenario/ja-JP/main.aria</c>.
    /// Top-level files (no slash) and empty locale are returned unchanged.
    /// </summary>
    private string ResolveLocalePath(string path)
    {
        if (string.IsNullOrEmpty(_locale)) return path;
        int slash = path.IndexOf('/');
        if (slash <= 0) return path; // top-level files don't get locale suffix
        string dir = path.Substring(0, slash);
        string rest = path.Substring(slash + 1);
        return $"{dir}/{_locale}/{rest}";
    }

    private static string Normalize(string path) =>
        PakArchive.NormalizePath(path);

    public void Dispose()
    {
        lock (_lock)
        {
            _baseReaders?.ForEach(r => { try { r.Dispose(); } catch { /* ignore */ } });
            _patchReaders?.ForEach(r => { try { r.Dispose(); } catch { /* ignore */ } });
            _baseReaders = null;
            _patchReaders = null;
        }
    }

    private sealed class IndexedEntry
    {
        public PakManifestEntryV3 Entry { get; }
        public PakArchiveV3Reader Reader { get; }
        public bool IsPatch { get; }
        public IndexedEntry(PakManifestEntryV3 entry, PakArchiveV3Reader reader, bool isPatch)
        {
            Entry = entry;
            Reader = reader;
            IsPatch = isPatch;
        }
    }
}
