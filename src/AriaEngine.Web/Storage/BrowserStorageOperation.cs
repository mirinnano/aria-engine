namespace AriaEngine.Web.Storage;

public enum BrowserStorageArea
{
    IndexedDb,
    Opfs,
    Download,
    FilePicker
}

public enum BrowserStorageOperationKind
{
    Read,
    Write,
    Export,
    Import
}

public sealed record BrowserStorageOperation(
    BrowserStorageArea Area,
    BrowserStorageOperationKind Kind,
    string DatabaseName,
    string StoreName,
    string Key,
    string Payload = "",
    long ContentLength = 0,
    string MimeType = "");
