using Raylib_cs;

namespace AriaEngine.Platform;

public sealed class RaylibWindowService : IWindowService
{
    public int ScreenWidth => Raylib.GetScreenWidth();
    public int ScreenHeight => Raylib.GetScreenHeight();
    public int CurrentMonitor => Raylib.GetCurrentMonitor();

    public int GetMonitorWidth(int monitor) => Raylib.GetMonitorWidth(monitor);
    public int GetMonitorHeight(int monitor) => Raylib.GetMonitorHeight(monitor);
    public void SetWindowSize(int width, int height) => Raylib.SetWindowSize(width, height);
    public void ToggleFullscreen() => Raylib.ToggleFullscreen();
}
