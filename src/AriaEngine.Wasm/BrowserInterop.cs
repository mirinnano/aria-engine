using System.Runtime.InteropServices.JavaScript;

namespace AriaEngine.Wasm;

internal static partial class BrowserInterop
{
    [JSImport("env.baseUri", "ariaWasm")]
    internal static partial string GetBaseUri();

    [JSImport("storage.readAll", "ariaWasm")]
    internal static partial Task<string> ReadAllStorageAsync();

    [JSImport("storage.write", "ariaWasm")]
    internal static partial Task WriteStorageAsync(string storeName, string key, string payload);

    [JSImport("ui.showFatal", "ariaWasm")]
    internal static partial void ShowFatal(string message);
}
