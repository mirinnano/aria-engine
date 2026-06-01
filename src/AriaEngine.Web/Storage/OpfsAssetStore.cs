namespace AriaEngine.Web.Storage;

public static class OpfsAssetStore
{
    public static BrowserStorageOperation WriteFile(string path, byte[] content)
    {
        string normalized = path.Replace('\\', '/').TrimStart('/');
        return new BrowserStorageOperation(
            BrowserStorageArea.Opfs,
            BrowserStorageOperationKind.Write,
            "origin-private-file-system",
            "assets",
            $"assets/{normalized}",
            ContentLength: content.LongLength);
    }
}
