using Raylib_cs;
using System.Collections.Generic;
using System.Linq;
using AriaEngine.Core;
using AriaEngine.Text;
using AriaEngine.Utility;
using AriaEngine.Assets;
using System.Numerics;

namespace AriaEngine.Rendering;

public class SpriteRenderer
{
    private LRUCache<string, Texture2D> _textureCache;
    private ColorCache _colorCache;
    private Font _font;
    private bool _fontLoaded = false;
    private readonly Dictionary<int, Font> _fontCache = new();
    private readonly HashSet<string> _colorKeyedCursorTextures = new(StringComparer.OrdinalIgnoreCase);
    private string _resolvedFontPath = "";
    private int[] _fontCodepoints = Array.Empty<int>();
    private Raylib_cs.TextureFilter _fontFilter = Raylib_cs.TextureFilter.Bilinear;
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

    // カーソル位置キャッシュ
    private string _cursorCacheText = "";
    private int _cursorCacheSpriteId = -1;
    private float _cursorCacheX, _cursorCacheY, _cursorCacheW, _cursorCacheH;
    private float _cursorCacheFontSize, _cursorCacheScaleX;
    private string _cursorCacheAlign = "";
    private string _cursorCacheVAlign = "";
    private Vector2 _cursorCacheResult;

    public SpriteRenderer(IAssetProvider assetProvider, ErrorReporter? reporter = null)
    {
        _assetProvider = assetProvider;
        _reporter = reporter;
        // LRUキャッシュの初期化
        _textureCache = new LRUCache<string, Texture2D>(
            CacheConstants.TextureCacheMaxItems,
            CacheConstants.TextureCacheMaxBytes,
            OnTextureEvicted);

        // 色キャッシュの初期化
        _colorCache = new ColorCache(CacheConstants.ColorCacheSize);
        _colorCache.PreloadCommonColors();

        // Zインデックスマネージャーは不要（毎フレーム直接ソート）
    }

    private void OnTextureEvicted(Texture2D texture)
    {
        try
        {
            if (texture.Id != 0)
            {
                Raylib.UnloadTexture(texture);
            }
        }
        catch (Exception ex)
        {
            _reporter?.ReportException(
                "RENDER_TEXTURE_EVICT_ERROR",
                ex,
                "テクスチャのアンロード中にエラーが発生しました。",
                AriaErrorLevel.Warning);
        }
    }

    public Texture2D GetOrLoadTexture(string imagePath)
    {
        if (string.IsNullOrWhiteSpace(imagePath)) return default;
        string resolved = _assetProvider.MaterializeToFile(imagePath);
        if (_textureCache.TryGetValue(resolved, out var tex) && tex.Id != 0) return tex;
        if (_failedTextures.Contains(imagePath)) return default;
        try
        {
            tex = Raylib.LoadTexture(resolved);
            if (tex.Id != 0)
            {
                _textureCache.AddOrUpdate(resolved, tex, tex.Width * tex.Height * 4);
                TotalTextureLoads++;
                return tex;
            }
        }
        catch (Exception ex)
        {
            _failedTextures.Add(imagePath);
            _reporter?.ReportException("RENDER_TEXTURE_LOAD", ex, $"画像 '{imagePath}' のロードに失敗しました。", AriaErrorLevel.Warning);
        }
        return default;
    }

    public void LoadFont(string fontPath, int atlasSize, string[] scriptLines, Raylib_cs.TextureFilter filter = Raylib_cs.TextureFilter.Bilinear)
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
        return Math.Clamp((int)MathF.Round(requestedFontSize), FontConstants.MinAtlasSize, FontConstants.MaxAtlasSize);
    }

    private Font GetFontForSize(float requestedFontSize)
    {
        if (!_fontLoaded)
        {
            return default;
        }

        int size = SelectFontAtlasSize(requestedFontSize);
        if (_fontCache.TryGetValue(size, out var cached) && cached.Texture.Id != 0)
        {
            return cached;
        }

        try
        {
            var font = LoadSizedFont(size);
            if (font.Texture.Id != 0)
            {
                return font;
            }
            return _font.Texture.Id != 0 ? _font : default;
        }
        catch (Exception ex)
        {
            _reporter?.ReportException(
                "RENDER_FONT_SIZE_LOAD",
                ex,
                $"フォントサイズ {size}px の生成に失敗しました。既定サイズで描画を続行します。",
                AriaErrorLevel.Warning,
                hint: "フォントファイルまたは文字数が大きすぎないか確認してください。");
            return _font.Texture.Id != 0 ? _font : default;
        }
    }

    private Font LoadSizedFont(int size)
    {
        if (string.IsNullOrEmpty(_resolvedFontPath))
        {
            _reporter?.Report(new AriaError(
                "フォントパスが設定されていません。",
                level: AriaErrorLevel.Warning,
                code: "RENDER_FONT_PATH_EMPTY"));
            return default;
        }

        Font font;
        try
        {
            font = Raylib.LoadFontEx(_resolvedFontPath, size, _fontCodepoints, _fontCodepoints.Length);
        }
        catch (Exception ex)
        {
            _reporter?.ReportException(
                "RENDER_FONT_LOAD_EX",
                ex,
                $"フォント '{_resolvedFontPath}' のサイズ {size}px の読み込みに失敗しました。",
                AriaErrorLevel.Warning);
            return default;
        }

        if (font.Texture.Id == 0)
        {
            _reporter?.Report(new AriaError(
                $"フォント '{_resolvedFontPath}' の読み込み結果が空でした。",
                level: AriaErrorLevel.Warning,
                code: "RENDER_FONT_EMPTY"));
            return font;
        }

        try
        {
            if (_fontFilter == Raylib_cs.TextureFilter.Trilinear)
            {
                var texture = font.Texture;
                Raylib.GenTextureMipmaps(ref texture);
                font.Texture = texture;
            }

            Raylib.SetTextureFilter(font.Texture, _fontFilter);
            _fontCache[size] = font;
        }
        catch (Exception ex)
        {
            _reporter?.ReportException(
                "RENDER_FONT_FILTER_ERROR",
                ex,
                "フォントのフィルター設定中にエラーが発生しました。",
                AriaErrorLevel.Warning);
        }

        return font;
    }

    public void Draw(GameState state, TransitionManager transition)
    {
        lock (_renderLock)
        {
            // 現在のGameStateをキャッシュ
            _currentState = state;
            UpdateUiPresentation(state);

            // 可視スプライトを収集してZ順にソート
            var sortedSprites = new List<Sprite>(state.Sprites.Count);
            foreach (var kvp in state.Sprites)
            {
                var sprite = kvp.Value;
                if (sprite.Visible)
                    sortedSprites.Add(sprite);
            }
            sortedSprites.Sort((a, b) => a.Z.CompareTo(b.Z));

            int qx = 0, qy = 0;
            if (state.QuakeTimerMs > 0)
            {
                if ((int)(Raylib.GetTime() * 60) % 2 == 0)
                {
                    qx = Raylib.GetRandomValue(-state.QuakeAmplitude, state.QuakeAmplitude);
                    qy = Raylib.GetRandomValue(-state.QuakeAmplitude, state.QuakeAmplitude);
                }
            }

            foreach (var sp in sortedSprites)
            {
                byte alpha = (byte)(Math.Clamp(sp.RenderOpacity, 0f, 1f) * 255);
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
                if (tex.Id != 0) Raylib.SetTextureFilter(tex, _currentState?.HighQualityUiTextures == true ? Raylib_cs.TextureFilter.Trilinear : Raylib_cs.TextureFilter.Bilinear);
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

        // テクスチャの有効性を検証
        if (tex.Width <= 0 || tex.Height <= 0)
        {
            _reporter?.Report(new AriaError(
                $"画像 '{sp.ImagePath}' のサイズが無効です: {tex.Width}x{tex.Height}",
                level: AriaErrorLevel.Warning,
                code: "RENDER_TEXTURE_INVALID_SIZE"));
            return;
        }

        TotalDrawCalls++;

        Rectangle src = new Rectangle(0, 0, tex.Width, tex.Height);
        float dw = sp.Width > 0 ? (sp.Width * sp.RenderScaleX) : (tex.Width * sp.RenderScaleX);
        float dh = sp.Height > 0 ? (sp.Height * sp.RenderScaleY) : (tex.Height * sp.RenderScaleY);

        float rWidth = dw;
        float rHeight = dh;

        float drawX = SnapPixel(sp.X + qx, _currentState);
        float drawY = SnapPixel(sp.Y + qy, _currentState);
        Rectangle dst = new Rectangle(drawX + rWidth / 2f, drawY + rHeight / 2f, rWidth, rHeight);

        if (sp.Width <= 0 || sp.Height <= 0)
        {
            sp.Width = tex.Width;
            sp.Height = tex.Height;
        }

        try
        {
            Raylib.DrawTexturePro(tex, src, dst, new Vector2(rWidth / 2f, rHeight / 2f), sp.Rotation, baseColor);
        }
        catch (Exception ex)
        {
            _reporter?.ReportException(
                "RENDER_DRAW_ERROR",
                ex,
                $"スプライト '{sp.ImagePath}' の描画中にエラーが発生しました。",
                AriaErrorLevel.Warning);
        }
    }

    private void DrawRectSprite(Sprite sp, Color baseColor, int qx, int qy)
    {
        TotalDrawCalls++;

        byte fillAlpha = (byte)(sp.FillAlpha * Math.Clamp(sp.RenderOpacity, 0f, 1f));
        Color normalFill = ParseColor(sp.FillColor, fillAlpha);
        Color hoverFill = !string.IsNullOrEmpty(sp.HoverFillColor) ? ParseColor(sp.HoverFillColor, fillAlpha) : normalFill;
        Color fillColor = LerpColor(normalFill, hoverFill, sp.HoverProgress);

        float rWidth = sp.Width * sp.RenderScaleX;
        float rHeight = sp.Height * sp.RenderScaleY;
        float rx = sp.X + qx - (rWidth - sp.Width * sp.ScaleX) / 2f;
        float ry = sp.Y + qy - (rHeight - sp.Height * sp.ScaleY) / 2f;
        rx = SnapPixel(rx, _currentState);
        ry = SnapPixel(ry, _currentState);

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
                    Raylib.DrawRectangleRounded(shadowRect, Math.Min((float)sp.CornerRadius / (rHeight > 0 ? rHeight : 1f), 1f), GetRoundSegments(_currentState), shadowColor);
                else
                    Raylib.DrawRectangleRec(shadowRect, shadowColor);
            }
        }

        // 塗りつぶし
        if (sp.CornerRadius > 0)
        {
            Raylib.DrawRectangleRounded(rect, Math.Min((float)sp.CornerRadius / (rHeight > 0 ? rHeight : 1f), 1f), GetRoundSegments(_currentState), fillColor);
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
                Raylib.DrawRectangleRoundedLinesEx(borderRect, roundness, GetRoundSegments(_currentState), sp.BorderWidth, borderColor);
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

        // セグメント描画モード: エフェクトが実際にある場合のみ使用
        // 単一セグメント・デフォルトスタイルは従来描画（文字単位折り返しが正確）
        if (_currentState?.CurrentTextSegments != null && _currentState.CurrentTextSegments.Count > 0
            && HasEffectSegments(_currentState.CurrentTextSegments))
        {
            DrawTextSegments(sp, baseColor, qx, qy);
            return;
        }

        // 従来の描画（文字単位折り返し対応）
        DrawPlainTextSprite(sp, baseColor, qx, qy);
    }

    private static bool HasEffectSegments(System.Collections.Generic.List<AriaEngine.Text.TextSegment> segments)
    {
        if (segments.Count > 1) return true;
        if (segments.Count == 0) return false;
        var seg = segments[0];
        if (seg.IsNewLine) return false;
        var s = seg.Style;
        return s.Color != null || s.Bold || s.Italic || s.Underline || s.Strikethrough
            || s.FadeDuration.HasValue || s.ShakeIntensity.HasValue
            || s.SizeScale.HasValue || s.SizeOffset.HasValue
            || seg.FadeDuration > 0 || seg.ShakeIntensity > 0;
    }

    /// <summary>
    /// セグメント単位のテキスト描画（エフェクト対応）
    /// </summary>
    private void DrawTextSegments(Sprite sp, Color baseColor, int qx, int qy)
    {
        if (_currentState?.CurrentTextSegments == null) return;

        TotalDrawCalls++;

        float baseFontSize = sp.FontSize > 0 ? sp.FontSize * sp.RenderScaleX : 24 * sp.RenderScaleX;
        float baseSpacing = baseFontSize / 10f;

        float maxWidth = sp.Width > 0 ? sp.Width : 1280;
        float startX = SnapPixel(sp.X + qx, _currentState);
        float startY = SnapPixel(sp.Y + qy, _currentState);
        
        // 全体のテキストサイズを計算（整列用）
        float totalWidth = 0;
        float totalHeight = 0;
        float currentLineWidth = 0;
        float currentLineHeight = baseFontSize + baseSpacing;
        
        foreach (var seg in _currentState.CurrentTextSegments)
        {
            if (seg.IsNewLine)
            {
                totalWidth = Math.Max(totalWidth, currentLineWidth);
                totalHeight += currentLineHeight;
                currentLineWidth = 0;
                currentLineHeight = baseFontSize + baseSpacing;
                continue;
            }
            
            float segFontSize = GetSegmentFontSize(baseFontSize, seg.Style);
            var segFont = GetFontForSize(segFontSize);
            float segSpacing = segFontSize / 10f;
            var segSize = Raylib.MeasureTextEx(segFont, seg.Text, segFontSize, segSpacing);
            
            currentLineWidth += segSize.X;
            currentLineHeight = Math.Max(currentLineHeight, segSize.Y);
        }
        totalWidth = Math.Max(totalWidth, currentLineWidth);
        totalHeight += currentLineHeight;

        // アライメント適用
        float cursorX = startX;
        float cursorY = startY;
        
        if (sp.Width > 0)
        {
            if (sp.TextAlign == "center") cursorX += (sp.Width - totalWidth) / 2f;
            else if (sp.TextAlign == "right") cursorX += (sp.Width - totalWidth);
        }
        else
        {
            if (sp.TextAlign == "center") cursorX -= totalWidth / 2f;
            else if (sp.TextAlign == "right") cursorX -= totalWidth;
        }

        if (sp.Height > 0)
        {
            if (sp.TextVAlign == "center" || sp.TextVAlign == "middle")
                cursorY += (sp.Height - totalHeight) / 2f;
            else if (sp.TextVAlign == "bottom")
                cursorY += (sp.Height - totalHeight);
        }

        // タイプライター効果: DisplayedTextLength をセグメントに適用
        int remainingLength = _currentState.DisplayedTextLength;

        // セグメントを順に描画（折り返し対応）
        float lineX = cursorX;
        float lineY = cursorY;
        float lineMaxHeight = baseFontSize + baseSpacing;

        foreach (var seg in _currentState.CurrentTextSegments)
        {
            if (remainingLength <= 0 && _currentState.TextSpeedMs > 0) break;

            if (seg.IsNewLine)
            {
                lineX = cursorX;
                lineY += lineMaxHeight;
                lineMaxHeight = baseFontSize + baseSpacing;
                continue;
            }

            float segFontSize = GetSegmentFontSize(baseFontSize, seg.Style);
            var segFont = GetFontForSize(segFontSize);
            float segSpacing = segFontSize / 10f;
            
            // タイプライター: 残り文字数で切り詰め
            string displayText = seg.Text;
            if (_currentState.TextSpeedMs > 0 && remainingLength < seg.Text.Length)
            {
                displayText = seg.Text.Substring(0, Math.Max(0, remainingLength));
            }
            remainingLength -= seg.Text.Length;

            if (string.IsNullOrEmpty(displayText)) continue;

            // 折り返しチェック
            var segSize = Raylib.MeasureTextEx(segFont, displayText, segFontSize, segSpacing);
            if (sp.Width > 0 && lineX + segSize.X > cursorX + maxWidth && lineX > cursorX)
            {
                lineX = cursorX;
                lineY += lineMaxHeight;
                lineMaxHeight = segFontSize + segSpacing;
            }

            // スタイル適用
            Color segColor = seg.Style.Color != null ? ParseColor(seg.Style.Color, baseColor.A) : baseColor;
            
            // Fade: 透明度を時間で変化
            if (seg.Style.FadeDuration.HasValue && seg.Style.FadeDuration.Value > 0)
            {
                float elapsed = (float)(Raylib.GetTime() * 1000) - seg.FadeStartTime;
                float progress = Math.Clamp(elapsed / seg.Style.FadeDuration.Value, 0f, 1f);
                segColor.A = (byte)(segColor.A * progress);
            }
            
            // Shake: ランダムオフセット
            float shakeX = 0, shakeY = 0;
            if (seg.Style.ShakeIntensity.HasValue && seg.Style.ShakeIntensity.Value > 0)
            {
                float time = (float)Raylib.GetTime() * 60f;
                int seed = (int)(time + seg.Text.GetHashCode());
                shakeX = (float)((seed * 9301 + 49297) % 233280 / 233280.0 * 2 - 1) * seg.Style.ShakeIntensity.Value;
                seed = (int)(time * 1.3f + seg.Text.GetHashCode());
                shakeY = (float)((seed * 9301 + 49297) % 233280 / 233280.0 * 2 - 1) * seg.Style.ShakeIntensity.Value;
            }
            
            float drawX = lineX + shakeX;
            float drawY = lineY + shakeY;
            
            // Bold: アウトラインで代替
            if (seg.Style.Bold && sp.TextOutlineSize == 0)
            {
                int boldSize = Math.Max(1, (int)(segFontSize / 20f));
                Raylib.DrawTextEx(segFont, displayText, new Vector2(drawX - boldSize, drawY - boldSize), segFontSize, segSpacing, segColor);
                Raylib.DrawTextEx(segFont, displayText, new Vector2(drawX + boldSize, drawY - boldSize), segFontSize, segSpacing, segColor);
                Raylib.DrawTextEx(segFont, displayText, new Vector2(drawX - boldSize, drawY + boldSize), segFontSize, segSpacing, segColor);
                Raylib.DrawTextEx(segFont, displayText, new Vector2(drawX + boldSize, drawY + boldSize), segFontSize, segSpacing, segColor);
            }

            // テキスト描画
            Raylib.DrawTextEx(segFont, displayText, new Vector2(drawX, drawY), segFontSize, segSpacing, segColor);
            
            // Underline: 下線
            if (seg.Style.Underline && segSize.X > 0)
            {
                float lineThickness = Math.Max(1f, segFontSize / 15f);
                Raylib.DrawRectangle((int)drawX, (int)(drawY + segSize.Y - lineThickness), (int)segSize.X, (int)lineThickness, segColor);
            }
            
            // Strikethrough: 打ち消し線
            if (seg.Style.Strikethrough && segSize.X > 0)
            {
                float lineThickness = Math.Max(1f, segFontSize / 15f);
                float strikeY = drawY + segSize.Y / 2f;
                Raylib.DrawRectangle((int)drawX, (int)(strikeY - lineThickness / 2f), (int)segSize.X, (int)lineThickness, segColor);
            }

            lineX += segSize.X;
            lineMaxHeight = Math.Max(lineMaxHeight, segSize.Y);
        }
    }

    private static float GetSegmentFontSize(float baseFontSize, TextStyle style)
    {
        float size = baseFontSize;
        if (style.SizeScale.HasValue)
            size *= style.SizeScale.Value;
        if (style.SizeOffset.HasValue)
            size += style.SizeOffset.Value;
        return Math.Max(1f, size);
    }

    /// <summary>
    /// 従来の単一フォントテキスト描画
    /// </summary>
    private void DrawPlainTextSprite(Sprite sp, Color baseColor, int qx, int qy)
    {
        TotalDrawCalls++;

        float scaledFontSize = sp.FontSize > 0 ? sp.FontSize * sp.RenderScaleX : 24 * sp.RenderScaleX;
        var font = GetFontForSize(scaledFontSize);
        if (font.Texture.Id == 0) return;

        float spacing = scaledFontSize / 10f;

        var drawText = WrapText(font, sp.Text, sp.Width > 0 ? sp.Width : 1280, scaledFontSize, spacing);
        var textSize = Raylib.MeasureTextEx(font, drawText, scaledFontSize, spacing);

        int rWidth = sp.Width > 0 ? sp.Width : (int)textSize.X;
        int rHeight = sp.Height > 0 ? sp.Height : (int)textSize.Y;

        float rx = SnapPixel(sp.X + qx, _currentState);
        float ry = SnapPixel(sp.Y + qy, _currentState);

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
        pos = ApplyTextEffect(sp, pos);

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

    private static Vector2 ApplyTextEffect(Sprite sp, Vector2 pos)
    {
        string effect = sp.TextEffect.Trim().ToLowerInvariant();
        if (effect is "" or "none" || sp.TextEffectStrength <= 0f) return pos;

        double time = Raylib.GetTime();
        float speed = Math.Max(0.1f, sp.TextEffectSpeed);
        return effect switch
        {
            "shake" => new Vector2(
                pos.X + (float)Math.Sin(time * speed * 2.13) * sp.TextEffectStrength,
                pos.Y + (float)Math.Cos(time * speed * 1.71) * sp.TextEffectStrength),
            "wave" or "float" => new Vector2(
                pos.X,
                pos.Y + (float)Math.Sin(time * speed) * sp.TextEffectStrength),
            _ => pos
        };
    }

    private string WrapText(Font font, string text, float maxWidth, float fontSize, float spacing)
    {
        // 早期リターン: 空または短いテキスト
        if (string.IsNullOrEmpty(text)) return "";
        if (text.Length <= 1 || maxWidth <= 0) return text;

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

    private static float SnapPixel(float value, GameState? state = null)
    {
        return state?.SubpixelUiRendering == true ? value : MathF.Round(value);
    }

    private static int GetRoundSegments(GameState? state)
    {
        return Math.Clamp(state?.RoundedRectSegments ?? UiQualityConstants.DefaultRoundedRectSegments, UiQualityConstants.MinRoundedRectSegments, UiQualityConstants.MaxRoundedRectSegments);
    }

    private static Color LerpColor(Color from, Color to, float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return new Color(
            (int)MathF.Round(from.R + (to.R - from.R) * t),
            (int)MathF.Round(from.G + (to.G - from.G) * t),
            (int)MathF.Round(from.B + (to.B - from.B) * t),
            (int)MathF.Round(from.A + (to.A - from.A) * t));
    }

    private void UpdateUiPresentation(GameState state)
    {
        float dt = Math.Clamp(Raylib.GetFrameTime(), 0f, UiQualityConstants.MaxFrameTime);
        float response = Math.Clamp(state.UiMotionResponse, UiQualityConstants.MinMotionResponse, UiQualityConstants.MaxMotionResponse);
        float blend = state.SmoothUiMotion ? 1f - MathF.Exp(-response * dt) : 1f;

        foreach (var sp in state.Sprites.Values)
        {
            if (!sp.RenderStateInitialized)
            {
                sp.RenderScaleX = sp.ScaleX;
                sp.RenderScaleY = sp.ScaleY;
                sp.RenderOpacity = sp.Opacity;
                sp.HoverProgress = sp.IsHovered ? 1f : 0f;
                sp.RenderStateInitialized = true;
            }

            float hoverTarget = sp.IsHovered && sp.IsButton ? 1f : 0f;
            sp.HoverProgress += (hoverTarget - sp.HoverProgress) * blend;
            sp.HoverProgress = Math.Clamp(sp.HoverProgress, 0f, 1f);

            float hoverScale = 1f + ((sp.HoverScale - 1f) * sp.HoverProgress);
            float targetScaleX = sp.ScaleX * hoverScale;
            float targetScaleY = sp.ScaleY * hoverScale;

            sp.RenderScaleX += (targetScaleX - sp.RenderScaleX) * blend;
            sp.RenderScaleY += (targetScaleY - sp.RenderScaleY) * blend;
            sp.RenderOpacity += (sp.Opacity - sp.RenderOpacity) * blend;
        }
    }

    public void DrawClickCursor(GameState state)
    {
        if (!state.ShowClickCursor ||
            (state.State != VmState.WaitingForClick && state.State != VmState.WaitingForAnimation))
        {
            return;
        }

        if (state.State == VmState.WaitingForAnimation &&
            state.DisplayedTextLength < state.CurrentTextBuffer.Length)
        {
            return;
        }

        Vector2 cursorPos = GetTextEndCursorPosition(state);
        float x = SnapPixel(cursorPos.X + 2, state);
        float y = SnapPixel(cursorPos.Y + 2, state);

        if (state.ClickCursorMode.Equals("image", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(state.ClickCursorPath) &&
            !_failedTextures.Contains(state.ClickCursorPath))
        {
            try
            {
                string resolved = _assetProvider.MaterializeToFile(state.ClickCursorPath);
                if (!_textureCache.TryGetValue(resolved, out var tex))
                {
                    tex = LoadCursorTextureWithColorKey(resolved);
                    if (tex.Id != 0)
                    {
                        Raylib.SetTextureFilter(tex, Raylib_cs.TextureFilter.Bilinear);
                        _textureCache.AddOrUpdate(resolved, tex, tex.Width * tex.Height * 4);
                    }
                }

                if (tex.Id != 0)
                {
                    float targetSize = state.ClickCursorSize > 0 ? state.ClickCursorSize : Math.Clamp(state.DefaultFontSize * ClickCursorConstants.DefaultSizeRatio, ClickCursorConstants.MinSize, ClickCursorConstants.MaxSize);
                    float scale = Math.Clamp(targetSize / Math.Max(tex.Width, tex.Height), 0.1f, 1.0f);
                    Rectangle src = new Rectangle(0, 0, tex.Width, tex.Height);
                    Rectangle dst = new Rectangle(x, y, tex.Width * scale, tex.Height * scale);
                    Raylib.DrawTexturePro(tex, src, dst, Vector2.Zero, 0f, Color.White);
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

        float t = (float)(Math.Sin(Raylib.GetTime() * 7.5) * 0.5 + 0.5);
        float bob = (float)Math.Sin(Raylib.GetTime() * 4.0) * 1.5f;
        byte alpha = (byte)(140 + t * 100);
        var color = ParseColor(state.ClickCursorColor, alpha);
        var edge = new Color(0, 0, 0, (int)(alpha * 0.55f));
        float s = state.ClickCursorSize > 0 ? state.ClickCursorSize : Math.Clamp(state.DefaultFontSize * ClickCursorConstants.DefaultSizeRatio, ClickCursorConstants.MinSize, ClickCursorConstants.MaxSize);
        y += bob;
        Raylib.DrawTriangle(
            new Vector2(x, y),
            new Vector2(x + s, y + s * 0.5f),
            new Vector2(x, y + s),
            color);
        Raylib.DrawTriangleLines(
            new Vector2(x, y),
            new Vector2(x + s, y + s * 0.5f),
            new Vector2(x, y + s),
            edge);
    }

    private Vector2 GetTextEndCursorPosition(GameState state)
    {
        if (state.TextTargetSpriteId >= 0 &&
            state.Sprites.TryGetValue(state.TextTargetSpriteId, out var sp) &&
            sp.Type == SpriteType.Text &&
            _fontLoaded)
        {
            string text = string.IsNullOrEmpty(sp.Text) ? state.CurrentTextBuffer : sp.Text;

            // キャッシュヒットチェック
            if (_cursorCacheSpriteId == sp.Id &&
                _cursorCacheText == text &&
                _cursorCacheX == sp.X && _cursorCacheY == sp.Y &&
                _cursorCacheW == sp.Width && _cursorCacheH == sp.Height &&
                _cursorCacheFontSize == sp.FontSize && _cursorCacheScaleX == sp.RenderScaleX &&
                _cursorCacheAlign == sp.TextAlign && _cursorCacheVAlign == sp.TextVAlign)
            {
                return _cursorCacheResult;
            }

            float scaledFontSize = sp.FontSize > 0 ? sp.FontSize * Math.Max(sp.RenderScaleX, 0.001f) : 24f;
            var font = GetFontForSize(scaledFontSize);
            float spacing = scaledFontSize / 10f;
            string wrapped = WrapText(font, text, sp.Width > 0 ? sp.Width : state.WindowWidth, scaledFontSize, spacing);
            string[] lines = wrapped.Replace("\r\n", "\n").Split('\n');
            string lastLine = lines.Length > 0 ? lines[^1] : "";
            Vector2 lastLineSize = Raylib.MeasureTextEx(font, lastLine, scaledFontSize, spacing);
            Vector2 allTextSize = Raylib.MeasureTextEx(font, wrapped, scaledFontSize, spacing);

            float baseX = sp.X;
            float baseY = sp.Y;
            if (sp.Width > 0)
            {
                if (sp.TextAlign == "center") baseX += (sp.Width - allTextSize.X) / 2f;
                else if (sp.TextAlign == "right") baseX += sp.Width - allTextSize.X;
            }

            if (sp.Height > 0)
            {
                if (sp.TextVAlign == "center" || sp.TextVAlign == "middle") baseY += (sp.Height - allTextSize.Y) / 2f;
                else if (sp.TextVAlign == "bottom") baseY += sp.Height - allTextSize.Y;
            }

            float lineHeight = scaledFontSize + spacing;
            float x = baseX + lastLineSize.X;
            float y = baseY + Math.Max(0, lines.Length - 1) * lineHeight + scaledFontSize * 0.18f;
            var result = new Vector2(x, y);

            // キャッシュ更新
            _cursorCacheSpriteId = sp.Id;
            _cursorCacheText = text;
            _cursorCacheX = sp.X; _cursorCacheY = sp.Y;
            _cursorCacheW = sp.Width; _cursorCacheH = sp.Height;
            _cursorCacheFontSize = sp.FontSize; _cursorCacheScaleX = sp.RenderScaleX;
            _cursorCacheAlign = sp.TextAlign; _cursorCacheVAlign = sp.TextVAlign;
            _cursorCacheResult = result;

            return result;
        }

        return new Vector2(state.DefaultTextboxX + state.DefaultTextboxPaddingX, state.DefaultTextboxY + state.DefaultTextboxPaddingY);
    }

    private Texture2D LoadCursorTextureWithColorKey(string resolvedPath)
    {
        Image image;
        try
        {
            image = Raylib.LoadImage(resolvedPath);
        }
        catch (Exception ex)
        {
            _reporter?.ReportException(
                "RENDER_CURSOR_IMAGE_LOAD",
                ex,
                $"カーソル画像 '{resolvedPath}' の読み込みに失敗しました。",
                AriaErrorLevel.Warning);
            return default;
        }

        // Raylib-cs 4.5.0ではIntPtr型のチェック方法を変更
        if (image.Width <= 0 || image.Height <= 0)
        {
            _reporter?.Report(new AriaError(
                $"カーソル画像 '{resolvedPath}' の読み込み結果が無効です。",
                level: AriaErrorLevel.Warning,
                code: "RENDER_CURSOR_IMAGE_INVALID"));
            return default;
        }

        try
        {
            Raylib.ImageColorReplace(ref image, new Color(255, 0, 255, 255), Color.Blank);
            Raylib.ImageColorReplace(ref image, new Color(128, 0, 128, 255), Color.Blank);
            Raylib.ImageColorReplace(ref image, new Color(255, 0, 128, 255), Color.Blank);
            var texture = Raylib.LoadTextureFromImage(image);

            if (texture.Id != 0)
            {
                _colorKeyedCursorTextures.Add(resolvedPath);
            }
            else
            {
                _reporter?.Report(new AriaError(
                    $"カーソル画像 '{resolvedPath}' のテクスチャ変換に失敗しました。",
                    level: AriaErrorLevel.Warning,
                    code: "RENDER_CURSOR_TEXTURE_EMPTY"));
            }

            return texture;
        }
        catch (Exception ex)
        {
            _reporter?.ReportException(
                "RENDER_CURSOR_PROCESS",
                ex,
                $"カーソル画像 '{resolvedPath}' の処理中にエラーが発生しました。",
                AriaErrorLevel.Warning);
            return default;
        }
        finally
        {
            try
            {
                Raylib.UnloadImage(image);
            }
            catch (Exception ex)
            {
                _reporter?.ReportException(
                    "RENDER_CURSOR_IMAGE_UNLOAD",
                    ex,
                    "カーソル画像のアンロード中にエラーが発生しました。",
                    AriaErrorLevel.Warning);
            }
        }
    }

    public void DrawUiText(string text, int x, int y, int size, Color color)
    {
        if (_fontLoaded)
        {
            var font = GetFontForSize(size);
            if (font.Texture.Id != 0)
            {
                Raylib.DrawTextEx(font, text, new Vector2(x, y), size, Math.Max(1, size / 10f), color);
            }
            else
            {
                Raylib.DrawText(text, x, y, size, color);
            }
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

        // スプライト統計
        int spriteCount = _currentState?.Sprites.Count ?? 0;
        Raylib.DrawText($"Sprites: {spriteCount}", 10, 170, 16, Color.Magenta);
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
                try
                {
                    if (font.Texture.Id != 0)
                    {
                        Raylib.UnloadFont(font);
                    }
                }
                catch (Exception ex)
                {
                    _reporter?.ReportException(
                        "RENDER_FONT_UNLOAD",
                        ex,
                        "フォントのアンロード中にエラーが発生しました。",
                        AriaErrorLevel.Warning);
                }
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
