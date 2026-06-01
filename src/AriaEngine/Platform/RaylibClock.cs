using Raylib_cs;

namespace AriaEngine.Platform;

public sealed class RaylibClock : IClock
{
    public float NowMilliseconds => (float)Raylib.GetTime() * 1000f;
}
