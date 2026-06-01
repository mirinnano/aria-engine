using Raylib_cs;

namespace AriaEngine.Platform;

public sealed class RaylibRandomSource : IRandomSource
{
    public int NextInclusive(int min, int max) => Raylib.GetRandomValue(min, max);
}
