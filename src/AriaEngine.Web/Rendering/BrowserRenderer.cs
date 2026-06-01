using System;
using System.Collections.Generic;
using System.Linq;
using AriaEngine.Core;

namespace AriaEngine.Web.Rendering;

public enum BrowserDrawKind
{
    Image,
    Text,
    Rect,
    Triangle
}

public sealed class BrowserDrawCommand
{
    public BrowserDrawKind Kind { get; set; }
    public int SpriteId { get; set; }
    public double CssX { get; set; }
    public double CssY { get; set; }
    public double CssWidth { get; set; }
    public double CssHeight { get; set; }
    public double LogicalX { get; set; }
    public double LogicalY { get; set; }
    public double LogicalWidth { get; set; }
    public double LogicalHeight { get; set; }
    public string ImagePath { get; set; } = "";
    public bool UseNaturalImageSize { get; set; }
    public string Text { get; set; } = "";
    public int FontSize { get; set; }
    public string Color { get; set; } = "#ffffff";
    public string FillColor { get; set; } = "#000000";
    public int FillAlpha { get; set; } = 255;
    public int CornerRadius { get; set; }
    public string BorderColor { get; set; } = "";
    public int BorderWidth { get; set; }
    public int BorderOpacity { get; set; } = 255;
    public string ShadowColor { get; set; } = "";
    public int ShadowOffsetX { get; set; }
    public int ShadowOffsetY { get; set; }
    public int ShadowAlpha { get; set; } = 128;
    public string TextShadowColor { get; set; } = "";
    public int TextShadowX { get; set; }
    public int TextShadowY { get; set; }
    public string TextAlign { get; set; } = "left";
    public string TextVAlign { get; set; } = "top";
    public double Opacity { get; set; } = 1d;
    public int Z { get; set; }
}

public sealed class BrowserRenderer
{
    private readonly CanvasScaleMapper _mapper;

    public BrowserRenderer(CanvasScaleMapper mapper)
    {
        _mapper = mapper;
    }

    public IReadOnlyList<BrowserDrawCommand> ToDrawCommands(IEnumerable<Sprite> sprites)
    {
        return sprites
            .Where(sprite => sprite.Visible)
            .OrderBy(sprite => sprite.Z)
            .Select(ToDrawCommand)
            .ToList();
    }

    private BrowserDrawCommand ToDrawCommand(Sprite sprite)
    {
        double logicalWidth = Math.Max(0, sprite.Width * sprite.ScaleX);
        double logicalHeight = Math.Max(0, sprite.Height * sprite.ScaleY);
        CssPoint css = _mapper.MapLogicalToCss(sprite.X, sprite.Y);

        return new BrowserDrawCommand
        {
            Kind = sprite.Type switch
            {
                SpriteType.Image => BrowserDrawKind.Image,
                SpriteType.Text => BrowserDrawKind.Text,
                _ => BrowserDrawKind.Rect
            },
            SpriteId = sprite.Id,
            CssX = css.X,
            CssY = css.Y,
            CssWidth = logicalWidth * _mapper.Scale,
            CssHeight = logicalHeight * _mapper.Scale,
            LogicalX = sprite.X,
            LogicalY = sprite.Y,
            LogicalWidth = logicalWidth,
            LogicalHeight = logicalHeight,
            ImagePath = NormalizeImagePathForStaticWeb(sprite.ImagePath),
            UseNaturalImageSize = sprite.Type == SpriteType.Image && (sprite.Width <= 0 || sprite.Height <= 0),
            Text = sprite.Text,
            FontSize = sprite.FontSize,
            Color = sprite.Color,
            FillColor = sprite.FillColor,
            FillAlpha = sprite.FillAlpha,
            CornerRadius = sprite.CornerRadius,
            BorderColor = sprite.BorderColor,
            BorderWidth = sprite.BorderWidth,
            BorderOpacity = sprite.BorderOpacity,
            ShadowColor = sprite.ShadowColor,
            ShadowOffsetX = sprite.ShadowOffsetX,
            ShadowOffsetY = sprite.ShadowOffsetY,
            ShadowAlpha = sprite.ShadowAlpha,
            TextShadowColor = sprite.TextShadowColor,
            TextShadowX = sprite.TextShadowX,
            TextShadowY = sprite.TextShadowY,
            TextAlign = sprite.TextAlign.ToString().ToLowerInvariant(),
            TextVAlign = sprite.TextVAlign.ToString().ToLowerInvariant(),
            Opacity = sprite.Opacity,
            Z = sprite.Z
        };
    }

    private static string NormalizeImagePathForStaticWeb(string path)
    {
        string normalized = path.Replace('\\', '/').TrimStart('/');
        if (string.IsNullOrWhiteSpace(normalized)) return "";
        if (normalized.StartsWith("assets/", StringComparison.OrdinalIgnoreCase)) return normalized;
        if (Uri.TryCreate(normalized, UriKind.Absolute, out _)) return normalized;
        if (normalized.StartsWith("data:", StringComparison.OrdinalIgnoreCase)) return normalized;
        return $"assets/{normalized}";
    }
}
