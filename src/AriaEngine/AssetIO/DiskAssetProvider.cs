using System;
using System.IO;

namespace AriaEngine.Assets;

public sealed class DiskAssetProvider : IAssetProvider
{
    private readonly string _root;

    public DiskAssetProvider(string root)
    {
        _root = Path.GetFullPath(root);
    }

    public bool Exists(string path) => TryResolveExisting(path, out _);

    public string[] ReadAllLines(string path) => File.ReadAllLines(ResolveRequired(path));

    public string ReadAllText(string path) => File.ReadAllText(ResolveRequired(path));

    public Stream OpenRead(string path) => File.OpenRead(ResolveRequired(path));

    public string MaterializeToFile(string path) => ResolveRequired(path);

    private string ResolveRequired(string path)
    {
        if (TryResolveExisting(path, out string? resolved)) return resolved;
        throw new FileNotFoundException($"Asset not found: {path}");
    }

    private bool TryResolveExisting(string path, out string resolved)
    {
        if (Path.IsPathRooted(path))
        {
            resolved = path;
            return File.Exists(resolved);
        }

        string direct = Path.GetFullPath(Path.Combine(_root, path));
        if (File.Exists(direct))
        {
            resolved = direct;
            return true;
        }

        string prefixed = Path.GetFullPath(Path.Combine(_root, "assets", path));
        if (File.Exists(prefixed))
        {
            resolved = prefixed;
            return true;
        }

        resolved = direct;
        return false;
    }
}
