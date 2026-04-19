using Raylib_cs;
using System.Collections.Generic;
using System.Linq;
using AriaEngine.Core;
using System.Numerics;

namespace AriaEngine.Rendering;

public class SpriteRenderer
{
    private Dictionary<string, Texture2D> _textureCache = new();
    private Font _font;
    private bool _fontLoaded = false;

    public void LoadFont(string fontPath, int atlasSize, string[] scriptLines, TextureFilter filter = TextureFilter.Bilinear)
    {
        var chars = new HashSet<int>();
        for (int c = 32; c < 127; c++) chars.Add(c);
        foreach (var c in "、。！？「」『』…―～（）") chars.Add(c);
        foreach (var line in scriptLines)
            foreach (var c in line)
                chars.Add(c);

        int[] codepoints = chars.ToArray();
        _font = Raylib.LoadFontEx(fontPath, atlasSize, codepoints, codepoints.Length);
        Raylib.SetTextureFilter(_font.Texture, filter); // 動的なフィルター設定
        _fontLoaded = true;
    }

    public void Draw(GameState state, TransitionManager transition)
    {
        int qx = 0, qy = 0;
        if (state.QuakeTimerMs > 0)
        {
            // Simple random shake
            if ((int)(Raylib.GetTime() * 60) % 2 == 0) 
            {
                qx = Raylib.GetRandomValue(-state.QuakeAmplitude, state.QuakeAmplitude);
                qy = Raylib.GetRandomValue(-state.QuakeAmplitude, state.QuakeAmplitude);
            }
        }

        var sortedSprites = state.Sprites.Values
            .Where(s => s.Visible)
            .OrderBy(s => s.Z)
            .ToList();

        foreach (var sp in sortedSprites)
        {
            Color baseColor = new Color(255, 255, 255, (int)(sp.Opacity * 255));

            if (sp.Type == SpriteType.Image && !string.IsNullOrEmpty(sp.ImagePath))
            {
                if (!_textureCache.ContainsKey(sp.ImagePath))
                {
                    _textureCache[sp.ImagePath] = Raylib.LoadTexture(sp.ImagePath);
                }

                var tex = _textureCache[sp.ImagePath];
                Rectangle src = new Rectangle(0, 0, tex.Width, tex.Height);
                float dw = sp.Width > 0 ? (sp.Width * sp.ScaleX) : (tex.Width * sp.ScaleX);
                float dh = sp.Height > 0 ? (sp.Height * sp.ScaleY) : (tex.Height * sp.ScaleY);
                
                float hs = sp.IsHovered && sp.IsButton ? sp.HoverScale : 1.0f;
                float rWidth = dw * hs;
                float rHeight = dh * hs;
                
                Rectangle dst = new Rectangle(sp.X + qx + rWidth / 2f, sp.Y + qy + rHeight / 2f, rWidth, rHeight);
                
                if (sp.Width <= 0 || sp.Height <= 0) 
                {
                    sp.Width = tex.Width;
                    sp.Height = tex.Height;
                }

                Raylib.DrawTexturePro(tex, src, dst, new Vector2(rWidth / 2f, rHeight / 2f), sp.Rotation, baseColor);
            }
            else if (sp.Type == SpriteType.Rect)
            {
                Color fillColor = sp.IsHovered && sp.IsButton && !string.IsNullOrEmpty(sp.HoverFillColor) 
                    ? GetColorFromHex(sp.HoverFillColor, (int)(sp.FillAlpha * sp.Opacity)) 
                    : GetColorFromHex(sp.FillColor, (int)(sp.FillAlpha * sp.Opacity));
                
                float hs = sp.IsHovered && sp.IsButton ? sp.HoverScale : 1.0f;
                int rWidth = (int)(sp.Width * sp.ScaleX * hs);
                int rHeight = (int)(sp.Height * sp.ScaleY * hs);
                float rx = sp.X + qx - (rWidth - sp.Width * sp.ScaleX) / 2f; 
                float ry = sp.Y + qy - (rHeight - sp.Height * sp.ScaleY) / 2f;

                Rectangle rect = new Rectangle(rx, ry, rWidth, rHeight);

                // Shadow
                if (sp.ShadowOffsetX != 0 || sp.ShadowOffsetY != 0)
                {
                    Color shadColor = GetColorFromHex(sp.ShadowColor, (int)(sp.ShadowAlpha * sp.Opacity));
                    Rectangle shadRect = new Rectangle(rx + sp.ShadowOffsetX, ry + sp.ShadowOffsetY, rWidth, rHeight);
                    if (sp.CornerRadius > 0)
                        Raylib.DrawRectangleRounded(shadRect, Math.Min((float)sp.CornerRadius / (rHeight > 0 ? rHeight : 1f), 1f), 16, shadColor);
                    else
                        Raylib.DrawRectangleRec(shadRect, shadColor);
                }

                if (sp.CornerRadius > 0)
                {
                    float roundness = Math.Min((float)sp.CornerRadius / (Math.Max(rHeight, 1f) / 2f), 1f); 
                    // roundness is 0.0-1.0 based on radius vs half-height in Raylib
                    float r = Math.Min(sp.CornerRadius, rHeight / 2f);
                    float normalizedRoundness = r / (rHeight / 2f);

                    Raylib.DrawRectangleRounded(rect, normalizedRoundness, 16, fillColor);
                    
                    if (sp.BorderWidth > 0 && !string.IsNullOrEmpty(sp.BorderColor))
                    {
                        Color borderColor = GetColorFromHex(sp.BorderColor, (int)(255 * sp.Opacity));
                        Raylib.DrawRectangleRoundedLinesEx(rect, normalizedRoundness, 16, sp.BorderWidth, borderColor);
                    }
                }
                else
                {
                    if (!string.IsNullOrEmpty(sp.GradientTo))
                    {
                        Color gColor = GetColorFromHex(sp.GradientTo, (int)(sp.FillAlpha * sp.Opacity));
                        if (sp.GradientDirection == "horizontal")
                            Raylib.DrawRectangleGradientH((int)rx, (int)ry, rWidth, rHeight, fillColor, gColor);
                        else
                            Raylib.DrawRectangleGradientV((int)rx, (int)ry, rWidth, rHeight, fillColor, gColor);
                    }
                    else
                    {
                        Raylib.DrawRectangleRec(rect, fillColor);
                    }

                    if (sp.BorderWidth > 0 && !string.IsNullOrEmpty(sp.BorderColor))
                    {
                        Color borderColor = GetColorFromHex(sp.BorderColor, (int)(255 * sp.Opacity));
                        Raylib.DrawRectangleLinesEx(rect, sp.BorderWidth, borderColor);
                    }
                }
            }
            else if (sp.Type == SpriteType.Text && _fontLoaded)
            {
                // フォントフィルターの動的適用
                if (state.FontFilter != TextureFilter.Bilinear)
                {
                    Raylib.SetTextureFilter(_font.Texture, state.FontFilter);
                }

                Color txtColor = GetColorFromHex(sp.Color, (int)(255 * sp.Opacity));
                string drawText = sp.Text;
                if (sp.Width > 0)
                {
                     drawText = WrapText(_font, sp.Text, sp.Width, sp.FontSize);
                }

                // スムーススケーリングを考慮したフォントサイズ
                float scaledFontSize = sp.FontSize * Math.Max(sp.ScaleX, sp.ScaleY);

                var textSize = Raylib.MeasureTextEx(_font, drawText, scaledFontSize, 2);
                if (sp.Width <= 0 || sp.Height <= 0)
                {
                    sp.Width = (int)textSize.X;
                    sp.Height = (int)textSize.Y;
                }

                float renderX = sp.X + qx;
                if (sp.TextAlign == "center") renderX += (sp.Width - textSize.X) / 2f;
                else if (sp.TextAlign == "right") renderX += (sp.Width - textSize.X);

                Vector2 pos = new Vector2(renderX, sp.Y + qy);

                if (sp.TextOutlineSize > 0 && !string.IsNullOrEmpty(sp.TextOutlineColor))
                {
                    Color outColor = GetColorFromHex(sp.TextOutlineColor, (int)(255 * sp.Opacity));
                    int t = sp.TextOutlineSize;
                    Raylib.DrawTextEx(_font, drawText, new Vector2(pos.X - t, pos.Y - t), scaledFontSize, 2, outColor);
                    Raylib.DrawTextEx(_font, drawText, new Vector2(pos.X + t, pos.Y - t), scaledFontSize, 2, outColor);
                    Raylib.DrawTextEx(_font, drawText, new Vector2(pos.X - t, pos.Y + t), scaledFontSize, 2, outColor);
                    Raylib.DrawTextEx(_font, drawText, new Vector2(pos.X + t, pos.Y + t), scaledFontSize, 2, outColor);
                }

                if ((sp.TextShadowX != 0 || sp.TextShadowY != 0) && !string.IsNullOrEmpty(sp.TextShadowColor))
                {
                    Color shadowColor = GetColorFromHex(sp.TextShadowColor, (int)(255 * sp.Opacity));
                    Raylib.DrawTextEx(_font, drawText, new Vector2(pos.X + sp.TextShadowX, pos.Y + sp.TextShadowY), scaledFontSize, 2, shadowColor);
                }

                Raylib.DrawTextEx(_font, drawText, pos, scaledFontSize, 2, txtColor);
            }
            
            if (state.DebugMode && sp.IsButton)
            {
                int bx = (int)(sp.X + qx + sp.ClickAreaX);
                int by = (int)(sp.Y + qy + sp.ClickAreaY);
                int bw = sp.ClickAreaW > 0 ? sp.ClickAreaW : sp.Width;
                int bh = sp.ClickAreaH > 0 ? sp.ClickAreaH : sp.Height;
                Raylib.DrawRectangleLines(bx, by, (int)(bw * sp.ScaleX), (int)(bh * sp.ScaleY), Color.Red);
            }
        }
        
        transition.Draw(state);
        
        if (state.DebugMode)
        {
            Raylib.DrawFPS(10, 10);
            Raylib.DrawText($"PC: {state.ProgramCounter}", 10, 30, 20, Color.Green);
            Raylib.DrawText($"Sprites: {state.Sprites.Count}", 10, 50, 20, Color.Green);
        }
    }

    private string WrapText(Font font, string text, float maxWidth, float fontSize)
    {
        if (string.IsNullOrEmpty(text)) return "";
        string result = "";
        string currentLine = "";
        float spacing = 2;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '\n')
            {
                result += currentLine + "\n";
                currentLine = "";
                continue;
            }

            string testLine = currentLine + c;
            var size = Raylib.MeasureTextEx(font, testLine, fontSize, spacing);
            if (size.X > maxWidth && currentLine.Length > 0)
            {
                result += currentLine + "\n";
                currentLine = c.ToString();
            }
            else
            {
                currentLine = testLine;
            }
        }
        result += currentLine;
        return result;
    }

    private Color GetColorFromHex(string hex, int alpha)
    {
        hex = hex.Replace("#", "");
        if (hex.Length == 6)
        {
            byte r = byte.Parse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
            byte g = byte.Parse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
            byte b = byte.Parse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);
            return new Color(r, g, b, alpha);
        }
        return Color.White;
    }

    public void Unload()
    {
        foreach (var tex in _textureCache.Values) Raylib.UnloadTexture(tex);
        _textureCache.Clear();
        if (_fontLoaded) Raylib.UnloadFont(_font);
    }
}
