namespace AriaEngine.Platform;

public static class PlatformServices
{
    public static IClock Clock { get; set; } = new RaylibClock();
    public static IRandomSource Random { get; set; } = new RaylibRandomSource();
    public static IWindowService Window { get; set; } = new RaylibWindowService();
    public static IScreenshotService Screenshot { get; set; } = new RaylibScreenshotService();
    public static IBrowserService Browser { get; set; } = new NativeBrowserService();
}
