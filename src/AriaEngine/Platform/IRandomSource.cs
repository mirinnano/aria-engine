namespace AriaEngine.Platform;

public interface IRandomSource
{
    int NextInclusive(int min, int max);
}
