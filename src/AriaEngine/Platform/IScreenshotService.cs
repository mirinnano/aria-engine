namespace AriaEngine.Platform;

public interface IScreenshotService
{
    byte[]? CaptureThumbnail(int width, int height);
}
