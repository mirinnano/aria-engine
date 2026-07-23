using System.Security.Cryptography;
using System.Text.Json;
using AriaEngine.Core;

namespace AriaEngine.Wasm;

public sealed class WebAssetManifest
{
    public int Version { get; set; } = 1;
    public List<WebAssetManifestEntry> Assets { get; set; } = new();
}

public sealed class WebAssetManifestEntry
{
    public string Group { get; set; } = "";
    public string LogicalPath { get; set; } = "";
    public string Url { get; set; } = "";
    public long Size { get; set; }
    public string Sha256 { get; set; } = "";
}

/// <summary>
/// Downloads manifest groups into the browser's Emscripten-backed virtual file
/// system. Raylib and managed File APIs then observe the same logical paths.
/// </summary>
public sealed class WasmAssetGroupLoader : IAssetGroupLoader
{
    private readonly HttpClient _http;
    private readonly string _dataRoot;
    private readonly Dictionary<string, Task> _inFlight = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _loaded = new(StringComparer.OrdinalIgnoreCase);
    private WebAssetManifest? _manifest;

    public WasmAssetGroupLoader(HttpClient http, string dataRoot)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _dataRoot = Path.GetFullPath(dataRoot);
        Directory.CreateDirectory(_dataRoot);
    }

    public event Action<string>? GroupLoaded;
    public event Action<string, string>? GroupLoadFailed;

    public async Task InitializeAsync(string manifestUrl = "aria-web-assets.json")
    {
        string json = await _http.GetStringAsync(manifestUrl).ConfigureAwait(false);
        _manifest = JsonSerializer.Deserialize(json, WasmJsonContext.Default.WebAssetManifest)
            ?? throw new InvalidDataException("Web asset manifest is empty.");

        if (_manifest.Version != 1)
        {
            throw new InvalidDataException($"Unsupported web asset manifest version: {_manifest.Version}");
        }
    }

    public async Task PreloadAsync(string groupName)
    {
        EnsureManifest();
        if (_loaded.Contains(groupName)) return;
        await LoadGroupAsync(groupName).ConfigureAwait(false);
        _loaded.Add(groupName);
    }

    public AssetGroupLoadResult Request(string groupName)
    {
        EnsureManifest();
        if (_loaded.Contains(groupName)) return AssetGroupLoadResult.Available();
        if (!EntriesFor(groupName).Any())
        {
            return AssetGroupLoadResult.Failed($"Unknown asset group '{groupName}'.");
        }

        if (_inFlight.ContainsKey(groupName)) return AssetGroupLoadResult.Loading();
        Task operation = CompleteRequestAsync(groupName);
        _inFlight[groupName] = operation;
        _ = ClearInFlightAsync(groupName, operation);
        return AssetGroupLoadResult.Loading();
    }

    private async Task CompleteRequestAsync(string groupName)
    {
        try
        {
            await LoadGroupAsync(groupName).ConfigureAwait(false);
            _loaded.Add(groupName);
            GroupLoaded?.Invoke(groupName);
        }
        catch (Exception ex)
        {
            GroupLoadFailed?.Invoke(groupName, ex.Message);
        }
    }

    private async Task ClearInFlightAsync(string groupName, Task operation)
    {
        try
        {
            await operation.ConfigureAwait(false);
        }
        catch
        {
            // CompleteRequestAsync reports transfer failures through its event;
            // cleanup must never become an unobserved task exception.
        }
        finally
        {
            if (_inFlight.TryGetValue(groupName, out Task? current) && ReferenceEquals(current, operation))
            {
                _inFlight.Remove(groupName);
            }
        }
    }

    private async Task LoadGroupAsync(string groupName)
    {
        WebAssetManifestEntry[] entries = EntriesFor(groupName).ToArray();
        if (entries.Length == 0) throw new InvalidDataException($"Unknown asset group '{groupName}'.");

        foreach (WebAssetManifestEntry entry in entries)
        {
            string destination = ResolveDestination(entry.LogicalPath);
            if (File.Exists(destination))
            {
                byte[] existing = await File.ReadAllBytesAsync(destination).ConfigureAwait(false);
                if (Verify(entry, existing)) continue;
            }

            byte[] bytes = await _http.GetByteArrayAsync(entry.Url).ConfigureAwait(false);
            if (!Verify(entry, bytes))
            {
                throw new InvalidDataException($"Asset integrity check failed: {entry.LogicalPath}");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            await File.WriteAllBytesAsync(destination, bytes).ConfigureAwait(false);
        }
    }

    private IEnumerable<WebAssetManifestEntry> EntriesFor(string groupName) =>
        _manifest!.Assets.Where(entry => string.Equals(entry.Group, groupName, StringComparison.OrdinalIgnoreCase));

    private string ResolveDestination(string logicalPath)
    {
        string normalized = logicalPath.Replace('\\', '/').TrimStart('/');
        if (string.IsNullOrWhiteSpace(normalized)) throw new InvalidDataException("Asset logical path is empty.");

        string destination = Path.GetFullPath(Path.Combine(_dataRoot, normalized));
        string rootPrefix = _dataRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!destination.StartsWith(rootPrefix, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Asset path escapes the data root: {logicalPath}");
        }
        return destination;
    }

    private static bool Verify(WebAssetManifestEntry entry, byte[] bytes)
    {
        if (entry.Size >= 0 && bytes.LongLength != entry.Size) return false;
        if (string.IsNullOrWhiteSpace(entry.Sha256)) return false;
        string actual = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        return string.Equals(actual, entry.Sha256.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private void EnsureManifest()
    {
        if (_manifest is null) throw new InvalidOperationException("Web asset manifest has not been initialized.");
    }
}
