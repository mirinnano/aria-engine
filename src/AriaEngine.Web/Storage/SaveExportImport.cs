namespace AriaEngine.Web.Storage;

public static class SaveExportImport
{
    public static BrowserStorageOperation CreateExport(string fileName, string json)
    {
        return new BrowserStorageOperation(
            BrowserStorageArea.Download,
            BrowserStorageOperationKind.Export,
            "",
            "downloads",
            fileName,
            json,
            MimeType: "application/vnd.aria.save+json");
    }

    public static BrowserStorageOperation CreateImportRequest(string extension)
    {
        return new BrowserStorageOperation(
            BrowserStorageArea.FilePicker,
            BrowserStorageOperationKind.Import,
            "",
            "file-picker",
            extension);
    }
}
