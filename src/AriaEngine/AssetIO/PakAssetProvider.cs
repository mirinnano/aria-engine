using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using AriaEngine.Packaging;

namespace AriaEngine.Assets;

public sealed class PakAssetProvider : IAssetProvider
{
    private readonly PakReader _pak;
    private readonly string _tempRoot;
    private readonly Dictionary<string, string> _materialized = new(StringComparer.OrdinalIgnoreCase);

    public PakAssetProvider(string pakPath, string? keyMaterial = null)
    {
        if (!File.Exists(pakPath))
            throw new FileNotFoundException($"Pak file not found: {pakPath}. Verify the path and ensure the file exists.");

        byte[]? key = string.IsNullOrWhiteSpace(keyMaterial) ? null : CryptoHelper.DeriveKey(keyMaterial);
        try
        {
            _pak = PakArchive.Open(pakPath, key);
        }
        catch (InvalidOperationException ex)
        {
            throw new InvalidOperationException($"Failed to open pak file '{pakPath}': {ex.Message} The file may be corrupted or not a valid pak archive.", ex);
        }
        _tempRoot = Path.Combine(Path.GetTempPath(), "aria_pak_cache", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
    }

    public bool Exists(string path) => TryResolveLogical(path, out _);

    public string[] ReadAllLines(string path)
    {
        string text = ReadAllText(path);
        return text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
    }

    public string ReadAllText(string path)
    {
        byte[] bytes = _pak.ReadAllBytes(ResolveRequired(path));
        return Encoding.UTF8.GetString(bytes);
    }

    public Stream OpenRead(string path)
    {
        byte[] bytes = _pak.ReadAllBytes(ResolveRequired(path));
        return new MemoryStream(bytes, writable: false);
    }

    public string MaterializeToFile(string path)
    {
        string normalized = ResolveRequired(path);
        if (_materialized.TryGetValue(normalized, out string? cached))
            return cached;

        byte[] bytes = _pak.ReadAllBytes(normalized);
        string fullPath = Path.Combine(_tempRoot, normalized.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllBytes(fullPath, bytes);
        _materialized[normalized] = fullPath;
        return fullPath;
    }

    private string ResolveRequired(string path)
    {
        if (TryResolveLogical(path, out string? resolved)) return resolved;
        throw new FileNotFoundException($"Pak entry not found: {path}");
    }

    private bool TryResolveLogical(string path, out string resolved)
    {
        string normalized = PakArchive.NormalizePath(path);
        if (_pak.Contains(normalized))
        {
            resolved = normalized;
            return true;
        }

        string prefixed = PakArchive.NormalizePath($"assets/{normalized}");
        if (_pak.Contains(prefixed))
        {
            resolved = prefixed;
            return true;
        }

        resolved = normalized;
        return false;
    }
}
