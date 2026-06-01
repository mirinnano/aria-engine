using System;

namespace AriaEngine.Web.Rendering;

public readonly record struct CssPoint(double X, double Y);
public readonly record struct LogicalPoint(double X, double Y);
public readonly record struct LogicalRect(double X, double Y, double Width, double Height);

public sealed class CanvasScaleMapper
{
    public const double NativeWidth = 1280d;
    public const double NativeHeight = 720d;

    private CanvasScaleMapper(double cssWidth, double cssHeight)
    {
        CssWidth = cssWidth;
        CssHeight = cssHeight;
        Scale = Math.Min(cssWidth / NativeWidth, cssHeight / NativeHeight);
        OffsetX = (cssWidth - (NativeWidth * Scale)) / 2d;
        OffsetY = (cssHeight - (NativeHeight * Scale)) / 2d;
    }

    public double CssWidth { get; }
    public double CssHeight { get; }
    public double Scale { get; }
    public double OffsetX { get; }
    public double OffsetY { get; }

    public static CanvasScaleMapper Create(double cssWidth, double cssHeight)
    {
        return new CanvasScaleMapper(Math.Max(1d, cssWidth), Math.Max(1d, cssHeight));
    }

    public CssPoint MapLogicalToCss(double x, double y)
    {
        return new CssPoint(OffsetX + x * Scale, OffsetY + y * Scale);
    }

    public LogicalPoint MapCssToLogical(double x, double y)
    {
        return new LogicalPoint((x - OffsetX) / Scale, (y - OffsetY) / Scale);
    }
}
