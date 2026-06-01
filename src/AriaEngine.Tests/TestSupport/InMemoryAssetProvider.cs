using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using AriaEngine.Assets;

namespace AriaEngine.Tests.TestSupport;

public sealed class InMemoryAssetProvider : IAssetProvider
{
    private readonly Dictionary<string, byte[]> _files;

    public InMemoryAssetProvider(Dictionary<string, string> files)
    {
        _files = files.ToDictionary(
            pair => pair.Key.Replace('\\', '/'),
            pair => Encoding.UTF8.GetBytes(pair.Value),
            StringComparer.OrdinalIgnoreCase);
    }

    public bool Exists(string path) => _files.ContainsKey(Normalize(path));

    public string[] ReadAllLines(string path) =>
        ReadAllText(path).Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');

    public string ReadAllText(string path) => Encoding.UTF8.GetString(ReadAllBytes(path));

    public byte[] ReadAllBytes(string path)
    {
        if (_files.TryGetValue(Normalize(path), out byte[] bytes)) return bytes;
        throw new FileNotFoundException($"Asset not found: {path}");
    }

    public Stream OpenRead(string path) => new MemoryStream(ReadAllBytes(path), writable: false);

    public bool CanMaterializeToFile => false;

    public string MaterializeToFile(string path) =>
        throw new PlatformNotSupportedException($"In-memory asset cannot be materialized: {path}");

    private static string Normalize(string path) => path.Replace('\\', '/');
}
