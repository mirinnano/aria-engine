using Raylib_cs;

namespace AriaEngine.Platform;

public sealed class RaylibScreenshotService : IScreenshotService
{
    public byte[]? CaptureThumbnail(int width, int height)
    {
        if (!Raylib.IsWindowReady()) return null;

        var image = Raylib.LoadImageFromScreen();
        try
        {
            Raylib.ImageResize(ref image, width, height);
            string tempPath = Path.Combine(Path.GetTempPath(), $"aria_thumb_{Guid.NewGuid():N}.png");
            try
            {
                Raylib.ExportImage(image, tempPath);
                return File.ReadAllBytes(tempPath);
            }
            finally
            {
                if (File.Exists(tempPath)) File.Delete(tempPath);
            }
        }
        finally
        {
            Raylib.UnloadImage(image);
        }
    }
}
