using AriaEngine.Web.Rendering;

namespace AriaEngine.Web.Input;

public sealed class BrowserInputMapper
{
    private readonly CanvasScaleMapper _mapper;

    public BrowserInputMapper(CanvasScaleMapper mapper)
    {
        _mapper = mapper;
    }

    public LogicalPoint MapPointerToLogical(double clientX, double clientY, double canvasLeft, double canvasTop)
    {
        return _mapper.MapCssToLogical(clientX - canvasLeft, clientY - canvasTop);
    }

    public bool IsPointerInside(LogicalRect rect, double clientX, double clientY, double canvasLeft, double canvasTop)
    {
        LogicalPoint point = MapPointerToLogical(clientX, clientY, canvasLeft, canvasTop);
        return point.X >= rect.X &&
               point.X <= rect.X + rect.Width &&
               point.Y >= rect.Y &&
               point.Y <= rect.Y + rect.Height;
    }
}
