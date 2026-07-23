using System.Text.Json;

namespace AriaEngine.Wasm;

internal sealed class BrowserStorageSynchronizer
{
    private const string SavesStore = "saves";
    private const string SettingsStore = "settings";
    private readonly string _runtimeRoot;
    private readonly Dictionary<string, string> _lastPayloads = new(StringComparer.OrdinalIgnoreCase);
    private bool _flushInProgress;

    public BrowserStorageSynchronizer(string runtimeRoot)
    {
        _runtimeRoot = Path.GetFullPath(runtimeRoot);
    }

    public async Task RestoreAsync()
    {
        string json = await BrowserInterop.ReadAllStorageAsync().ConfigureAwait(false);
        StorageSnapshot snapshot = JsonSerializer.Deserialize(json, WasmJsonContext.Default.StorageSnapshot)
            ?? new StorageSnapshot();

        string saveDirectory = Path.Combine(_runtimeRoot, "saves");
        Directory.CreateDirectory(saveDirectory);
        foreach (StorageEntry entry in snapshot.Saves)
        {
            if (!TryParseSaveSlot(entry.Key, out int slot)) continue;
            string path = Path.Combine(saveDirectory, $"slot_{slot:00}.ariasav");
            await File.WriteAllTextAsync(path, entry.Payload).ConfigureAwait(false);
            _lastPayloads[$"{SavesStore}/{entry.Key}"] = entry.Payload;
        }

        foreach (StorageEntry entry in snapshot.Settings)
        {
            string? path = entry.Key.ToLowerInvariant() switch
            {
                "settings:config" => Path.Combine(_runtimeRoot, "config.json"),
                "settings:persistent" => Path.Combine(saveDirectory, "persistent.ariasav"),
                _ => null
            };
            if (path is null) continue;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(path, entry.Payload).ConfigureAwait(false);
            _lastPayloads[$"{SettingsStore}/{entry.Key}"] = entry.Payload;
        }
    }

    public async Task FlushAsync()
    {
        if (_flushInProgress) return;
        _flushInProgress = true;
        try
        {
            string saveDirectory = Path.Combine(_runtimeRoot, "saves");
            if (Directory.Exists(saveDirectory))
            {
                foreach (string path in Directory.EnumerateFiles(saveDirectory, "slot_*.ariasav"))
                {
                    string stem = Path.GetFileNameWithoutExtension(path);
                    if (!int.TryParse(stem.AsSpan("slot_".Length), out int slot)) continue;
                    await WriteIfChangedAsync(SavesStore, $"save:{slot:000}", path).ConfigureAwait(false);
                }
            }

            await WriteIfChangedAsync(SettingsStore, "settings:config", Path.Combine(_runtimeRoot, "config.json")).ConfigureAwait(false);
            await WriteIfChangedAsync(SettingsStore, "settings:persistent", Path.Combine(saveDirectory, "persistent.ariasav")).ConfigureAwait(false);
        }
        finally
        {
            _flushInProgress = false;
        }
    }

    private async Task WriteIfChangedAsync(string store, string key, string path)
    {
        if (!File.Exists(path)) return;
        string payload = await File.ReadAllTextAsync(path).ConfigureAwait(false);
        string cacheKey = $"{store}/{key}";
        if (_lastPayloads.TryGetValue(cacheKey, out string? previous) && previous == payload) return;
        await BrowserInterop.WriteStorageAsync(store, key, payload).ConfigureAwait(false);
        _lastPayloads[cacheKey] = payload;
    }

    private static bool TryParseSaveSlot(string key, out int slot)
    {
        const string prefix = "save:";
        slot = -1;
        return key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
               int.TryParse(key.AsSpan(prefix.Length), out slot) &&
               slot >= 0;
    }

}

internal sealed class StorageSnapshot
{
    public List<StorageEntry> Saves { get; set; } = new();
    public List<StorageEntry> Settings { get; set; } = new();
}

internal sealed class StorageEntry
{
    public string Key { get; set; } = "";
    public string Payload { get; set; } = "";
}
