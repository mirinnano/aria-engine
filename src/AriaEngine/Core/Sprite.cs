namespace AriaEngine.Core;

public enum SpriteType { Image, Text, Rect }

public class Sprite
{
    public int Id { get; set; }
    public SpriteType Type { get; set; }

    // ダーティフラグと最終更新時刻
    public bool IsDirty { get; set; } = true;
    public DateTime LastModified { get; set; } = DateTime.Now;

    public float X { get; set; }
    public float Y { get; set; }
    public int Z { get; set; } = 0;
    public bool Visible { get; set; } = true;
    public float Opacity { get; set; } = 1.0f;
    public float ScaleX { get; set; } = 1.0f;
    public float ScaleY { get; set; } = 1.0f;
    public float Rotation { get; set; } = 0f;
    
    // Image properties
    public string ImagePath { get; set; } = "";
    
    // Text properties
    public string Text { get; set; } = "";
    public int FontSize { get; set; } = 26;
    public string Color { get; set; } = "#ffffff";
    public string TextAlign { get; set; } = "left"; // "left", "center", "right"
    public string TextVAlign { get; set; } = "top"; // "top", "center", "bottom"
    
    // Text decoration
    public string TextShadowColor { get; set; } = "";
    public int TextShadowX { get; set; } = 0;
    public int TextShadowY { get; set; } = 0;
    public string TextOutlineColor { get; set; } = "";
    public int TextOutlineSize { get; set; } = 0;
    
    // Rect properties
    public int Width { get; set; }
    public int Height { get; set; }
    public string FillColor { get; set; } = "#000000";
    public int FillAlpha { get; set; } = 255;
    
    // Rect decoration
    public int CornerRadius { get; set; } = 0;
    public string BorderColor { get; set; } = "";
    public int BorderWidth { get; set; } = 0;
    public int BorderOpacity { get; set; } = 255;
    public string GradientTo { get; set; } = "";
    public string GradientDirection { get; set; } = "vertical"; // "vertical", "horizontal"
    
    // Shadow
    public string ShadowColor { get; set; } = "";
    public int ShadowOffsetX { get; set; } = 0;
    public int ShadowOffsetY { get; set; } = 0;
    public int ShadowAlpha { get; set; } = 128;
    
    // Button properties
    public bool IsButton { get; set; } = false;
    public int ClickAreaX { get; set; } = 0;
    public int ClickAreaY { get; set; } = 0;
    public int ClickAreaW { get; set; } = 0;
    public int ClickAreaH { get; set; } = 0;
    
    // Hover effects
    public string HoverFillColor { get; set; } = "";
    public float HoverScale { get; set; } = 1.0f;
    public bool IsHovered { get; set; } = false;
}
