namespace AriaEngine.Core;

public enum SpriteType { Image, Text, Rect }

public class Sprite
{
    public int Id { get; set; }
    public SpriteType Type { get; set; }

    // ダーティフラグと最終更新時刻（未使用、互換性維持）
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsDirty { get; set; } = true;
    [System.Text.Json.Serialization.JsonIgnore]
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
    public string TextEffect { get; set; } = "none";
    public float TextEffectStrength { get; set; } = 0f;
    public float TextEffectSpeed { get; set; } = 8f;
    
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
    public string Cursor { get; set; } = "";

    // Slider properties
    public int SliderMin { get; set; } = 0;
    public int SliderMax { get; set; } = 0;
    public int SliderValue { get; set; } = 0;
    public string SliderTrackColor { get; set; } = "#555555";
    public string SliderFillColor { get; set; } = "#f5f5f5";
    public string SliderThumbColor { get; set; } = "#ffffff";

    // Checkbox properties
    public bool CheckboxValue { get; set; } = false;
    public string CheckboxLabel { get; set; } = "";

    // Smooth UI presentation state. These are renderer-owned runtime values.
    [System.Text.Json.Serialization.JsonIgnore]
    public float HoverProgress { get; set; } = 0f;
    [System.Text.Json.Serialization.JsonIgnore]
    public float RenderScaleX { get; set; } = 1.0f;
    [System.Text.Json.Serialization.JsonIgnore]
    public float RenderScaleY { get; set; } = 1.0f;
    [System.Text.Json.Serialization.JsonIgnore]
    public float RenderOpacity { get; set; } = 1.0f;
    [System.Text.Json.Serialization.JsonIgnore]
    public bool RenderStateInitialized { get; set; } = false;
}
