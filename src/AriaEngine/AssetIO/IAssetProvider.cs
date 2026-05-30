using System.IO;

namespace AriaEngine.Assets;

public interface IAssetProvider
{
    bool Exists(string path);
    string[] ReadAllLines(string path);
    string ReadAllText(string path);
    byte[] ReadAllBytes(string path);
    Stream OpenRead(string path);
    bool CanMaterializeToFile { get; }
    string MaterializeToFile(string path);
}
