namespace AriaEngine.Web.Storage;

public static class IndexedDbSaveStore
{
    public const string DatabaseName = "aria-engine";

    public static BrowserStorageOperation WriteSave(int slot, string json)
    {
        return new BrowserStorageOperation(
            BrowserStorageArea.IndexedDb,
            BrowserStorageOperationKind.Write,
            DatabaseName,
            "saves",
            $"save:{slot:000}",
            json);
    }

    public static BrowserStorageOperation ReadSave(int slot)
    {
        return new BrowserStorageOperation(
            BrowserStorageArea.IndexedDb,
            BrowserStorageOperationKind.Read,
            DatabaseName,
            "saves",
            $"save:{slot:000}");
    }

    public static BrowserStorageOperation WriteSetting(string name, string json)
    {
        return new BrowserStorageOperation(
            BrowserStorageArea.IndexedDb,
            BrowserStorageOperationKind.Write,
            DatabaseName,
            "settings",
            $"settings:{name}",
            json);
    }
}
