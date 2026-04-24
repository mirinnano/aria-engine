using Raylib_cs;
using System.Collections.Generic;
using System.Linq;
using AriaEngine.Core;
using AriaEngine.Utility;
using AriaEngine.Assets;
using System.Numerics;

namespace AriaEngine.Rendering;

public class SpriteRenderer
{
    private LRUCache<string, Texture2D> _textureCache;
    private ColorCache _colorCache;
    private ZIndexSpriteManager _spriteManager;
    private Font _font;
    private bool _fontLoaded = false;
    private readonly Dictionary<int, Font> _fontCache = new();
    private string _resolvedFontPath = "";
    private int[] _fontCodepoints = Array.Empty<int>();
    private TextureFilter _fontFilter = TextureFilter.Bilinear;
    private readonly object _renderLock = new();
    private readonly IAssetProvider _assetProvider;
    private readonly ErrorReporter? _reporter;
    private readonly HashSet<string> _failedTextures = new(StringComparer.OrdinalIgnoreCase);

    // パフォーマンス統計
    public long TotalDrawCalls { get; private set; }
    public long TotalTextureLoads { get; private set; }
    public long TotalColorParses { get; private set; }

    // GameStateの参照（最適化用）
    private GameState? _currentState;

    public SpriteRenderer(IAssetProvider assetProvider, ErrorReporter? reporter = null)
    {
        _assetProvider = assetProvider;
        _reporter = reporter;
        // LRUキャッシュの初期化（最大100テクスチャ、500MB上限）
        _textureCache = new LRUCache<string, Texture2D>(100, 500 * 1024 * 1024, OnTextureEvicted);

        // 色キャッシュの初期化
        _colorCache = new ColorCache(256);
        _colorCache.PreloadCommonColors();

        // Zインデックスマネージャーの初期化
        _spriteManager = new ZIndexSpriteManager(id =>
        {
            Sprite? sprite = null;
            if (_currentState != null && _currentState.Sprites.TryGetValue(id, out var foundSprite))
            {
                sprite = foundSprite;
            }
            return sprite;
        });
    }

    private void OnTextureEvicted(Texture2D texture)
    {
        Raylib.UnloadTexture(texture);
    }

    public void LoadFont(string fontPath, int atlasSize, string[] scriptLines, TextureFilter filter = TextureFilter.Bilinear)
    {
        var chars = new HashSet<int>();
        for (int c = 32; c < 127; c++) chars.Add(c);
        foreach (var c in "、。！？「」『』…―～（）") chars.Add(c);
        foreach (var line in scriptLines)
            foreach (var c in line)
                chars.Add(c);

        int[] codepoints = chars.ToArray();
        string resolvedFontPath;
        try
        {
            resolvedFontPath = _assetProvider.MaterializeToFile(fontPath);
            _resolvedFontPath = resolvedFontPath;
            _fontCodepoints = codepoints;
            _fontFilter = filter;
            _font = LoadSizedFont(SelectFontAtlasSize(atlasSize));
        }
        catch (Exception ex)
        {
            _reporter?.ReportException(
                "RENDER_FONT_LOAD",
                ex,
                $"フォント '{fontPath}' を読み込めませんでした。既定フォントで続行します。",
                AriaErrorLevel.Warning,
                hint: "フォントパス、Pak内の収録、またはinit.ariaのfont指定を確認してください。");
            _fontLoaded = false;
            return;
        }

        if (_font.Texture.Id == 0)
        {
            _reporter?.Report(new AriaError(
                $"フォント '{fontPath}' の読み込み結果が空でした。既定フォントで続行します。",
                level: AriaErrorLevel.Warning,
                code: "RENDER_FONT_EMPTY",
                hint: "TTF/OTFが壊れていないか、対応形式かを確認してください。"));
            _fontLoaded = false;
            return;
        }

        _fontLoaded = true;
    }

    public static int SelectFontAtlasSize(float requestedFontSize)
    {
        return Math.Clamp((int)MathF.Round(requestedFontSize), 8, 192);
    }

    private Font GetFontForSize(float requestedFontSize)
    {
        int size = SelectFontAtlasSize(requestedFontSize);
        if (_fontCache.TryGetValue(size, out var cached)) return cached;

        try
        {
            return LoadSizedFont(size);
        }
        catch (Exception ex)
        {
            _reporter?.ReportException(
                "RENDER_FONT_SIZE_LOAD",
                ex,
                $"フォントサイズ {size}px の生成に失敗しました。既定サイズで描画を続行します。",
                AriaErrorLevel.Warning,
                hint: "フォントファイルまたは文字数が大きすぎないか確認してください。");
            return _font;
        }
    }

    private Font LoadSizedFont(int size)
    {
        var font = Raylib.LoadFontEx(_resolvedFontPath, size, _fontCodepoints, _fontCodepoints.Length);
        if (font.Texture.Id == 0) return font;

        if (_fontFilter == TextureFilter.Trilinear)
        {
            var texture = font.Texture;
            Raylib.GenTextureMipmaps(ref texture);
            font.Texture = texture;
        }

        Raylib.SetTextureFilter(font.Texture, _fontFilter);
        _fontCache[size] = font;
        return font;
    }

    public void Draw(GameState state, TransitionManager transition)
    {
        lock (_renderLock)
        {
            // 現在のGameStateをキャッシュ
            _currentState = state;

            // スプライトマネージャーを更新
            foreach (var sprite in state.Sprites.Values)
            {
                if (sprite.Visible)
                {
                    _spriteManager.AddOrUpdate(sprite.Id, sprite.Z);
                }
                else
                {
                    _spriteManager.Remove(sprite.Id);
                }
            }

            int qx = 0, qy = 0;
            if (state.QuakeTimerMs > 0)
            {
                if ((int)(Raylib.GetTime() * 60) % 2 == 0)
                {
                    qx = Raylib.GetRandomValue(-state.QuakeAmplitude, state.QuakeAmplitude);
                    qy = Raylib.GetRandomValue(-state.QuakeAmplitude, state.QuakeAmplitude);
                }
            }

            // Zインデックスマネージャーからソート済みスプライトを取得
            var sortedSprites = _spriteManager.GetSortedSprites(includeInvisible: false);

            foreach (var sp in sortedSprites)
            {
                byte alpha = (byte)(sp.Opacity * 255);
                Color baseColor = ParseColor(sp.Color, alpha);

                if (sp.Type == SpriteType.Image && !string.IsNullOrEmpty(sp.ImagePath))
                {
                    DrawImageSprite(sp, baseColor, qx, qy);
                }
                else if (sp.Type == SpriteType.Rect)
                {
                    DrawRectSprite(sp, baseColor, qx, qy);
                }
                else if (sp.Type == SpriteType.Text)
                {
                    DrawTextSprite(sp, baseColor, qx, qy);
                }
            }

            transition.Draw(state);

            if (state.DebugMode)
            {
                DrawDebugInfo(state);
            }
        }
    }

    private void DrawImageSprite(Sprite sp, Color baseColor, int qx, int qy)
    {
        Texture2D tex;

        if (_failedTextures.Contains(sp.ImagePath)) return;

        string resolvedImagePath;
        try
        {
            resolvedImagePath = _assetProvider.MaterializeToFile(sp.ImagePath);
        }
        catch (Exception ex)
        {
            _failedTextures.Add(sp.ImagePath);
            _reporter?.ReportException(
                "RENDER_TEXTURE_MISSING",
                ex,
                $"画像 '{sp.ImagePath}' を読み込めませんでした。このスプライトをスキップして続行します。",
                AriaErrorLevel.Warning,
                hint: "画像ファイル名、Pak収録名、大文字小文字、拡張子を確認してください。");
            return;
        }

        if (_textureCache.TryGetValue(resolvedImagePath, out tex))
        {
            // キャッシュヒット
        }
        else
        {
            try
            {
                tex = Raylib.LoadTexture(resolvedImagePath);
                if (tex.Id != 0) Raylib.SetTextureFilter(tex, TextureFilter.Bilinear);
            }
            catch (Exception ex)
            {
                _failedTextures.Add(sp.ImagePath);
                _reporter?.ReportException(
                    "RENDER_TEXTURE_LOAD",
                    ex,
                    $"画像 '{sp.ImagePath}' のGPUロードに失敗しました。このスプライトをスキップして続行します。",
                    AriaErrorLevel.Warning,
                    hint: "画像形式がRaylibで読める形式か、ファイルが破損していないかを確認してください。");
                return;
            }

            if (tex.Id != 0)
            {
                _textureCache.AddOrUpdate(resolvedImagePath, tex, tex.Width * tex.Height * 4);
                TotalTextureLoads++;
            }
            else
            {
                _failedTextures.Add(sp.ImagePath);
                _reporter?.Report(new AriaError(
                    $"画像 '{sp.ImagePath}' のロード結果が空でした。このスプライトをスキップして続行します。",
                    level: AriaErrorLevel.Warning,
                    code: "RENDER_TEXTURE_EMPTY",
                    hint: "画像ファイルの破損、未対応形式、Pak内の内容を確認してください。"));
                return;
            }
        }

        if (tex.Id == 0) return;

        TotalDrawCalls++;

        Rectangle src = new Rectangle(0, 0, tex.Width, tex.Height);
        float dw = sp.Width > 0 ? (sp.Width * sp.ScaleX) : (tex.Width * sp.ScaleX);
        float dh = sp.Height > 0 ? (sp.Height * sp.ScaleY) : (tex.Height * sp.ScaleY);

        float hs = sp.IsHovered && sp.IsButton ? sp.HoverScale : 1.0f;
        float rWidth = dw * hs;
        float rHeight = dh * hs;

        float drawX = SnapPixel(sp.X + qx);
        float drawY = SnapPixel(sp.Y + qy);
        Rectangle dst = new Rectangle(drawX + rWidth / 2f, drawY + rHeight / 2f, rWidth, rHeight);

        if (sp.Width <= 0 || sp.Height <= 0)
        {
            sp.Width = tex.Width;
            sp.Height = tex.Height;
        }

        Raylib.DrawTexturePro(tex, src, dst, new Vector2(rWidth / 2f, rHeight / 2f), sp.Rotation, baseColor);
    }

    private void DrawRectSprite(Sprite sp, Color baseColor, int qx, int qy)
    {
        TotalDrawCalls++;

        byte fillAlpha = (byte)(sp.FillAlpha * sp.Opacity);
        string currentFillColorHex = sp.IsHovered && sp.IsButton && !string.IsNullOrEmpty(sp.HoverFillColor) ? sp.HoverFillColor : sp.FillColor;
        Color fillColor = ParseColor(currentFillColorHex, fillAlpha);

        float hs = sp.IsHovered && sp.IsButton ? sp.HoverScale : 1.0f;
        int rWidth = (int)(sp.Width * sp.ScaleX * hs);
        int rHeight = (int)(sp.Height * sp.ScaleY * hs);
        float rx = sp.X + qx - (rWidth - sp.Width * sp.ScaleX) / 2f;
        float ry = sp.Y + qy - (rHeight - sp.Height * sp.ScaleY) / 2f;
        rx = SnapPixel(rx);
        ry = SnapPixel(ry);

        Rectangle rect = new Rectangle(rx, ry, rWidth, rHeight);

        // 影（可視性チェック）
        if (sp.ShadowOffsetX != 0 || sp.ShadowOffsetY != 0)
        {
            // 影の色が空文字または透明度0の場合はスキップ
            if (!string.IsNullOrEmpty(sp.ShadowColor) && sp.ShadowAlpha > 0 && sp.Opacity > 0)
            {
                byte shadowAlpha = (byte)(sp.ShadowAlpha * sp.Opacity);
                Color shadowColor = ParseColor(sp.ShadowColor, shadowAlpha);
                Rectangle shadowRect = new Rectangle(rx + sp.ShadowOffsetX, ry + sp.ShadowOffsetY, rWidth, rHeight);
                if (sp.CornerRadius > 0)
                    Raylib.DrawRectangleRounded(shadowRect, Math.Min((float)sp.CornerRadius / (rHeight > 0 ? rHeight : 1f), 1f), 48, shadowColor);
                else
                    Raylib.DrawRectangleRec(shadowRect, shadowColor);
            }
        }

        // 塗りつぶし
        if (sp.CornerRadius > 0)
        {
            Raylib.DrawRectangleRounded(rect, Math.Min((float)sp.CornerRadius / (rHeight > 0 ? rHeight : 1f), 1f), 48, fillColor);
        }
        else if (!string.IsNullOrEmpty(sp.GradientTo))
        {
            Color gradColor = ParseColor(sp.GradientTo, fillAlpha);
            if (sp.GradientDirection == "horizontal")
                Raylib.DrawRectangleGradientH((int)rect.X, (int)rect.Y, (int)rect.Width, (int)rect.Height, fillColor, gradColor);
            else
                Raylib.DrawRectangleGradientV((int)rect.X, (int)rect.Y, (int)rect.Width, (int)rect.Height, fillColor, gradColor);
        }
        else
        {
            Raylib.DrawRectangleRec(rect, fillColor);
        }

        // 枠線
        if (sp.BorderWidth > 0)
        {
            byte borderAlpha = (byte)(sp.BorderOpacity * sp.Opacity);
            Color borderColor = ParseColor(sp.BorderColor, borderAlpha);
            if (sp.CornerRadius > 0)
            {
                Rectangle borderRect = new Rectangle(rect.X, rect.Y, rect.Width, rect.Height);
                float roundness = Math.Min((float)sp.CornerRadius / (rHeight > 0 ? rHeight : 1f), 1f);
                Raylib.DrawRectangleRoundedLinesEx(borderRect, roundness, 48, sp.BorderWidth, borderColor);
            }
            else
            {
                Rectangle borderRect = new Rectangle(rect.X, rect.Y, rect.Width, rect.Height);
                Raylib.DrawRectangleLinesEx(borderRect, sp.BorderWidth, borderColor);
            }
        }
    }

    private void DrawTextSprite(Sprite sp, Color baseColor, int qx, int qy)
    {
        if (string.IsNullOrEmpty(sp.Text) || !_fontLoaded) return;

        TotalDrawCalls++;

        float scaledFontSize = sp.FontSize > 0 ? sp.FontSize * sp.ScaleX : 24 * sp.ScaleX;
        var font = GetFontForSize(scaledFontSize);
        float spacing = scaledFontSize / 10f;

        var drawText = WrapText(font, sp.Text, sp.Width > 0 ? sp.Width : 1280, scaledFontSize, spacing);
        var textSize = Raylib.MeasureTextEx(font, drawText, scaledFontSize, spacing);

        int rWidth = sp.Width > 0 ? sp.Width : (int)textSize.X;
        int rHeight = sp.Height > 0 ? sp.Height : (int)textSize.Y;

        float rx = SnapPixel(sp.X + qx);
        float ry = SnapPixel(sp.Y + qy);

        // TextAlign implementation
        if (sp.Width > 0)
        {
            if (sp.TextAlign == "center") rx += (sp.Width - textSize.X) / 2f;
            else if (sp.TextAlign == "right") rx += (sp.Width - textSize.X);
        }
        else
        {
            if (sp.TextAlign == "center") rx -= textSize.X / 2f;
            else if (sp.TextAlign == "right") rx -= textSize.X;
        }

        // Vertical align inside explicit text area
        if (sp.Height > 0)
        {
            if (sp.TextVAlign == "center" || sp.TextVAlign == "middle")
            {
                ry += (sp.Height - textSize.Y) / 2f;
            }
            else if (sp.TextVAlign == "bottom")
            {
                ry += (sp.Height - textSize.Y);
            }
        }

        Vector2 pos = new Vector2(rx, ry);

        // テキストシャドウ（条件を強化）
        if (!string.IsNullOrEmpty(sp.TextShadowColor) && (sp.TextShadowX != 0 || sp.TextShadowY != 0) && sp.Opacity > 0)
        {
            Color tShadowColor = ParseColor(sp.TextShadowColor, baseColor.A);
            if (tShadowColor.A > 0)  // 完全に透明でない場合のみ描画
            {
                Raylib.DrawTextEx(font, drawText, new Vector2(pos.X + sp.TextShadowX, pos.Y + sp.TextShadowY), scaledFontSize, spacing, tShadowColor);
            }
        }

        // テキストアウトライン（条件を強化）
        if (sp.TextOutlineSize > 0 && !string.IsNullOrEmpty(sp.TextOutlineColor) && sp.Opacity > 0)
        {
            Color outColor = ParseColor(sp.TextOutlineColor, 255);
            if (outColor.A > 0)  // 完全に透明でない場合のみ描画
            {
                int t = sp.TextOutlineSize;
                Raylib.DrawTextEx(font, drawText, new Vector2(pos.X - t, pos.Y - t), scaledFontSize, spacing, outColor);
                Raylib.DrawTextEx(font, drawText, new Vector2(pos.X + t, pos.Y - t), scaledFontSize, spacing, outColor);
                Raylib.DrawTextEx(font, drawText, new Vector2(pos.X - t, pos.Y + t), scaledFontSize, spacing, outColor);
                Raylib.DrawTextEx(font, drawText, new Vector2(pos.X + t, pos.Y + t), scaledFontSize, spacing, outColor);
            }
        }

        // テキスト描画
        Raylib.DrawTextEx(font, drawText, pos, scaledFontSize, spacing, baseColor);
    }

    private string WrapText(Font font, string text, float maxWidth, float fontSize, float spacing)
    {
        if (string.IsNullOrEmpty(text)) return "";

        // StringBuilderプールを使用して効率的にラップ
        var builder = StringHelper.RentStringBuilder();

        try
        {
            string currentLine = "";

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];

                if (c == '\n')
                {
                    builder.Append(currentLine).Append("\n");
                    currentLine = "";
                    continue;
                }

                string testLine = currentLine + c;
                var size = Raylib.MeasureTextEx(font, testLine, fontSize, spacing);

                if (size.X > maxWidth && currentLine.Length > 0)
                {
                    builder.Append(currentLine).Append("\n");
                    currentLine = c.ToString();
                }
                else
                {
                    currentLine = testLine;
                }
            }

            builder.Append(currentLine);
            return builder.ToString();
        }
        finally
        {
            // StringBuilderプールに返却
            StringHelper.ReturnStringBuilder(builder);
        }
    }

    private Color ParseColor(string hex, int alpha)
    {
        // 色キャッシュから取得
        return _colorCache.GetColor(hex, alpha);
    }

    private static float SnapPixel(float value)
    {
        return MathF.Round(value);
    }

    public void DrawClickCursor(GameState state)
    {
        if (!state.ShowClickCursor || state.State != VmState.WaitingForClick) return;

        var mouse = Raylib.GetMousePosition();
        int x = (int)SnapPixel(mouse.X + state.ClickCursorOffsetX);
        int y = (int)SnapPixel(mouse.Y + state.ClickCursorOffsetY);

        if (!string.IsNullOrWhiteSpace(state.ClickCursorPath) && !_failedTextures.Contains(state.ClickCursorPath))
        {
            try
            {
                string resolved = _assetProvider.MaterializeToFile(state.ClickCursorPath);
                if (!_textureCache.TryGetValue(resolved, out var tex))
                {
                    tex = Raylib.LoadTexture(resolved);
                    if (tex.Id != 0)
                    {
                        Raylib.SetTextureFilter(tex, TextureFilter.Bilinear);
                        _textureCache.AddOrUpdate(resolved, tex, tex.Width * tex.Height * 4);
                    }
                }

                if (tex.Id != 0)
                {
                    Raylib.DrawTexture(tex, x, y, Color.White);
                    return;
                }
            }
            catch (Exception ex)
            {
                _failedTextures.Add(state.ClickCursorPath);
                _reporter?.ReportException(
                    "RENDER_CLICK_CURSOR",
                    ex,
                    $"クリック待ちカーソル '{state.ClickCursorPath}' を読み込めませんでした。既定カーソルで続行します。",
                    AriaErrorLevel.Warning);
            }
        }

        float t = (float)(Math.Sin(Raylib.GetTime() * 6.0) * 0.5 + 0.5);
        byte alpha = (byte)(150 + t * 90);
        var color = new Color(255, 255, 255, (int)alpha);
        Raylib.DrawTriangle(
            new Vector2(x, y),
            new Vector2(x + 12, y + 6),
            new Vector2(x, y + 12),
            color);
        Raylib.DrawTriangleLines(
            new Vector2(x, y),
            new Vector2(x + 12, y + 6),
            new Vector2(x, y + 12),
            new Color(0, 0, 0, (int)alpha));
    }

    public void DrawUiText(string text, int x, int y, int size, Color color)
    {
        if (_fontLoaded)
        {
            var font = GetFontForSize(size);
            Raylib.DrawTextEx(font, text, new Vector2(x, y), size, Math.Max(1, size / 10f), color);
        }
        else
        {
            Raylib.DrawText(text, x, y, size, color);
        }
    }

    private void DrawDebugInfo(GameState state)
    {
        Raylib.DrawFPS(10, 10);

        // パフォーマンス統計
        Raylib.DrawText($"PC: {state.ProgramCounter}", 10, 30, 20, Color.Green);
        Raylib.DrawText($"Sprites: {state.Sprites.Count}", 10, 50, 20, Color.Green);
        Raylib.DrawText($"Draw Calls: {TotalDrawCalls}", 10, 70, 20, Color.Yellow);
        Raylib.DrawText($"Tex Loads: {TotalTextureLoads}", 10, 90, 20, Color.Yellow);

        // キャッシュ統計
        var cyanColor = new Color(0, 255, 255, 255);
        Raylib.DrawText($"Color Cache: {_colorCache.Count}", 10, 110, 20, cyanColor);
        Raylib.DrawText($"Tex Cache: {_textureCache.Count}", 10, 130, 20, cyanColor);

        var texStats = _textureCache.GetStats();
        Raylib.DrawText($"Tex Stats: {texStats.UtilizationPercent:F1}%", 10, 150, 16, cyanColor);

        // スプライトマネージャー統計
        var spriteStats = _spriteManager.Stats;
        Raylib.DrawText($"Sorts: {spriteStats.SortCount}", 10, 170, 16, Color.Magenta);
        Raylib.DrawText($"Cache Hit: {spriteStats.CacheHitRate:F1}%", 10, 190, 16, Color.Magenta);
    }

    public void Unload()
    {
        lock (_renderLock)
        {
            // キャッシュをクリア
            _textureCache.Clear();
            _colorCache.Clear();
            _failedTextures.Clear();

            foreach (var font in _fontCache.Values)
            {
                Raylib.UnloadFont(font);
            }
            _fontCache.Clear();
            _fontLoaded = false;

            // 統計をリセット
            TotalDrawCalls = 0;
            TotalTextureLoads = 0;
            TotalColorParses = 0;

            _currentState = null;
        }
    }
}
