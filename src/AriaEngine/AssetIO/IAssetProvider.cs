using System.IO;

namespace AriaEngine.Assets;

public interface IAssetProvider
{
    bool Exists(string path);
    string[] ReadAllLines(string path);
    string ReadAllText(string path);
    Stream OpenRead(string path);
    string MaterializeToFile(string path);
}
