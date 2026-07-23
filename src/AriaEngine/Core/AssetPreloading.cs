namespace AriaEngine.Core;

/// <summary>
/// Result returned when the VM asks the platform host to make an asset group
/// available. Native hosts normally return <see cref="Available"/> immediately;
/// browser hosts start an HTTP transfer and return <see cref="Loading"/>.
/// </summary>
public enum AssetGroupLoadStatus
{
    Available,
    Loading,
    Failed
}

public readonly record struct AssetGroupLoadResult(AssetGroupLoadStatus Status, string Error = "")
{
    public static AssetGroupLoadResult Available() => new(AssetGroupLoadStatus.Available);
    public static AssetGroupLoadResult Loading() => new(AssetGroupLoadStatus.Loading);
    public static AssetGroupLoadResult Failed(string error) => new(AssetGroupLoadStatus.Failed, error);
}

/// <summary>
/// Platform boundary for the <c>asset_preload</c> instruction.
/// Implementations may complete asynchronously through the events below.
/// </summary>
public interface IAssetGroupLoader
{
    event Action<string>? GroupLoaded;
    event Action<string, string>? GroupLoadFailed;

    AssetGroupLoadResult Request(string groupName);
}

/// <summary>
/// Desktop implementation: assets already exist on disk or in a pak, so a
/// preload is only a synchronization marker and completes immediately.
/// </summary>
public sealed class ImmediateAssetGroupLoader : IAssetGroupLoader
{
    public static ImmediateAssetGroupLoader Instance { get; } = new();

    private ImmediateAssetGroupLoader()
    {
    }

    public event Action<string>? GroupLoaded
    {
        add { }
        remove { }
    }

    public event Action<string, string>? GroupLoadFailed
    {
        add { }
        remove { }
    }

    public AssetGroupLoadResult Request(string groupName) => AssetGroupLoadResult.Available();
}
