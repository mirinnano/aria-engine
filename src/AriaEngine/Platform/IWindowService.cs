namespace AriaEngine.Platform;

public interface IWindowService
{
    int ScreenWidth { get; }
    int ScreenHeight { get; }
    void ToggleFullscreen();
    int CurrentMonitor { get; }
    int GetMonitorWidth(int monitor);
    int GetMonitorHeight(int monitor);
    void SetWindowSize(int width, int height);
}
