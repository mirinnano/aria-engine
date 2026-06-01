#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using AriaEngine.Assets;

namespace AriaEngine.Web.Assets;

public sealed class PreloadedWebAssetProvider : IAssetProvider
{
    private static readonly string[] ExternalAssetExtensions =
    {
        ".png", ".jpg", ".jpeg", ".webp", ".gif", ".bmp",
        ".ogg", ".mp3", ".wav", ".flac",
        ".ttf", ".otf", ".woff", ".woff2"
    };

    private readonly Dictionary<string, byte[]> _preloaded;

    public PreloadedWebAssetProvider(IReadOnlyDictionary<string, string> textAssets)
    {
        _preloaded = textAssets.ToDictionary(
            pair => Normalize(pair.Key),
            pair => Encoding.UTF8.GetBytes(pair.Value),
            StringComparer.OrdinalIgnoreCase);
    }

    public long PreloadedByteCount => _preloaded.Values.Sum(bytes => (long)bytes.Length);

    public bool CanMaterializeToFile => false;

    public bool Exists(string path)
    {
        string normalized = Normalize(path);
        return _preloaded.ContainsKey(normalized) ||
               IsExternalAssetPath(normalized) ||
               IsExternalAssetPath(WithAssetsPrefix(normalized));
    }

    public string[] ReadAllLines(string path) =>
        ReadAllText(path).Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');

    public string ReadAllText(string path) => Encoding.UTF8.GetString(ReadAllBytes(path));

    public byte[] ReadAllBytes(string path)
    {
        string normalized = Normalize(path);
        if (_preloaded.TryGetValue(normalized, out byte[]? bytes)) return bytes;
        throw new FileNotFoundException($"Preloaded web asset not found: {normalized}");
    }

    public Stream OpenRead(string path) => new MemoryStream(ReadAllBytes(path), writable: false);

    public string MaterializeToFile(string path) =>
        throw new PlatformNotSupportedException($"Web asset cannot be materialized: {path}");

    private static bool IsExternalAssetPath(string path)
    {
        string extension = Path.GetExtension(path);
        return path.StartsWith("assets/", StringComparison.OrdinalIgnoreCase) &&
               ExternalAssetExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
    }

    private static string WithAssetsPrefix(string path) =>
        path.StartsWith("assets/", StringComparison.OrdinalIgnoreCase) ? path : $"assets/{path}";

    private static string Normalize(string path) => path.Replace('\\', '/').TrimStart('/');
}
