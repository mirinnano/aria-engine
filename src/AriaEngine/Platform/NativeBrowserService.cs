using System.Diagnostics;

namespace AriaEngine.Platform;

public sealed class NativeBrowserService : IBrowserService
{
    public bool OpenExternal(Uri uri)
    {
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = uri.AbsoluteUri,
            UseShellExecute = true
        });

        return process is not null;
    }
}
