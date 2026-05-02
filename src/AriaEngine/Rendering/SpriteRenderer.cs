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
    private readonly HashSet<string> _failedToneTextures = new(StringComparer.OrdinalIgnoreCase);

    // UI用フォント（メニュー等の英字表示用）
    private string? _uiFontPath;
    private readonly Dictionary<float, Font> _uiFontCache = new();

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
    private TextAlignment _cursorCacheAlign = TextAlignment.Left;
    private TextVerticalAlignment _cursorCacheVAlign = TextVerticalAlignment.Top;
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
                ApplyTextureFilter(ref tex, CurrentTextureFilter());
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
        foreach (var c in "■□●▶◆◇▲▼") chars.Add(c); // rmenu icons
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

    public static int SelectSmoothFontAtlasSize(float requestedFontSize, Raylib_cs.TextureFilter filter)
    {
        if (filter == Raylib_cs.TextureFilter.Point)
        {
            return SelectFontAtlasSize(requestedFontSize);
        }

        return Math.Clamp((int)MathF.Round(requestedFontSize * 2f), FontConstants.MinAtlasSize, FontConstants.MaxAtlasSize);
    }

    private Font GetFontForSize(float requestedFontSize)
    {
        if (!_fontLoaded)
        {
            return default;
        }

        int size = SelectSmoothFontAtlasSize(requestedFontSize, _fontFilter);
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
            ApplyTextureFilter(ref font, _fontFilter);
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

    public void LoadUiFont(string fontPath)
    {
        try
        {
            _uiFontPath = _assetProvider.MaterializeToFile(fontPath);
        }
        catch (Exception ex)
        {
            _reporter?.ReportException(
                "RENDER_UI_FONT_LOAD",
                ex,
                $"UIフォント '{fontPath}' を読み込めませんでした。",
                AriaErrorLevel.Warning);
            _uiFontPath = null;
        }
    }

    private Font GetUiFontForSize(float requestedFontSize)
    {
        if (string.IsNullOrEmpty(_uiFontPath)) return default;

        int size = SelectSmoothFontAtlasSize(requestedFontSize, _fontFilter);
        if (_uiFontCache.TryGetValue(size, out var cached) && cached.Texture.Id != 0)
        {
            return cached;
        }

        try
        {
            var chars = new HashSet<int>();
            for (int c = 32; c < 127; c++) chars.Add(c);
            int[] codepoints = chars.ToArray();

            var font = Raylib.LoadFontEx(_uiFontPath, size, codepoints, codepoints.Length);
            if (font.Texture.Id != 0)
            {
                ApplyTextureFilter(ref font, _fontFilter);
                _uiFontCache[size] = font;
                return font;
            }
        }
        catch (Exception ex)
        {
            _reporter?.ReportException(
                "RENDER_UI_FONT_SIZE_LOAD",
                ex,
                $"UIフォントサイズ {size}px の生成に失敗しました。",
                AriaErrorLevel.Warning);
        }
        return default;
    }

    public void DrawMenuText(string text, int x, int y, int size, Color color)
    {
        var font = GetUiFontForSize(size);
        if (font.Texture.Id != 0)
        {
            Raylib.DrawTextEx(font, text, new Vector2(x, y), size, Math.Max(1, size / 10f), color);
        }
        else
        {
            Raylib.DrawText(text, x, y, size, color);
        }
    }

    public int MeasureMenuText(string text, int size)
    {
        var font = GetUiFontForSize(size);
        if (font.Texture.Id != 0)
        {
            return (int)Raylib.MeasureTextEx(font, text, size, Math.Max(1, size / 10f)).X;
        }
        return Raylib.MeasureText(text, size);
    }

    private static void ApplyTextureFilter(ref Font font, Raylib_cs.TextureFilter filter)
    {
        var texture = font.Texture;
        ApplyTextureFilter(ref texture, filter);
        font.Texture = texture;
    }

    private static void ApplyTextureFilter(ref Texture2D texture, Raylib_cs.TextureFilter filter)
    {
        if (texture.Id == 0) return;

        if (filter == Raylib_cs.TextureFilter.Trilinear)
        {
            Raylib.GenTextureMipmaps(ref texture);
        }

        Raylib.SetTextureFilter(texture, filter);
    }

    private Raylib_cs.TextureFilter CurrentTextureFilter()
    {
        return _currentState?.UiQuality.HighQualityTextures == true ? Raylib_cs.TextureFilter.Trilinear : Raylib_cs.TextureFilter.Bilinear;
    }

    public void Draw(GameState state, TransitionManager transition)
    {
        lock (_renderLock)
        {
            // 現在のGameStateをキャッシュ
            _currentState = state;
            UpdateUiPresentation(state);

            // 可視スプライトを収集してZ順にソート
            var sortedSprites = new List<Sprite>(state.Render.Sprites.Count);
            foreach (var kvp in state.Render.Sprites)
            {
                var sprite = kvp.Value;
                if (sprite.Visible)
                    sortedSprites.Add(sprite);
            }
            sortedSprites.Sort((a, b) => a.Z.CompareTo(b.Z));

            int qx = (int)state.Render.CameraOffsetX;
            int qy = (int)state.Render.CameraOffsetY;
            if (state.Render.QuakeTimerMs > 0)
            {
                if ((int)(Raylib.GetTime() * 60) % 2 == 0)
                {
                    qx = Raylib.GetRandomValue(-state.Render.QuakeAmplitude, state.Render.QuakeAmplitude);
                    qy = Raylib.GetRandomValue(-state.Render.QuakeAmplitude, state.Render.QuakeAmplitude);
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
            DrawScreenEffects(state);

            if (state.EngineSettings.DebugMode)
            {
                DrawDebugInfo(state);
            }
        }
    }

    private void DrawScreenEffects(GameState state)
    {
        if (state.Render.ScreenTintOpacity <= 0f) return;
        byte alpha = (byte)(Math.Clamp(state.Render.ScreenTintOpacity, 0f, 1f) * 255);
        Raylib.DrawRectangle(0, 0, state.EngineSettings.WindowWidth, state.EngineSettings.WindowHeight, ParseColor(state.Render.ScreenTintColor, alpha));
        TotalDrawCalls++;
    }

    private void DrawImageSprite(Sprite sp, Color baseColor, int qx, int qy)
    {
        Texture2D tex;
        string cacheKey = TextureCacheKey(sp);

        if (_failedTextures.Contains(sp.ImagePath) || _failedToneTextures.Contains(cacheKey)) return;

        if (_textureCache.TryGetValue(cacheKey, out tex))
        {
            // キャッシュヒット
        }
        else
        {
            string resolvedImagePath;
            try
            {
                resolvedImagePath = ResolveImageForSprite(sp);
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

            try
            {
                tex = ShouldApplyBackgroundTone(sp)
                    ? LoadBackgroundToneTexture(sp, resolvedImagePath, cacheKey)
                    : Raylib.LoadTexture(resolvedImagePath);
                if (tex.Id != 0) ApplyTextureFilter(ref tex, CurrentTextureFilter());
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
                _textureCache.AddOrUpdate(cacheKey, tex, tex.Width * tex.Height * 4);
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
        float dw = SnapPixel(sp.Width > 0 ? (sp.Width * sp.RenderScaleX) : (tex.Width * sp.RenderScaleX), _currentState);
        float dh = SnapPixel(sp.Height > 0 ? (sp.Height * sp.RenderScaleY) : (tex.Height * sp.RenderScaleY), _currentState);

        float rWidth = dw;
        float rHeight = dh;

        float drawX = SnapPixel(sp.X + qx, _currentState);
        float drawY = SnapPixel(sp.Y + qy, _currentState);
        Rectangle dst = new Rectangle(drawX + rWidth / 2f, drawY + rHeight / 2f, rWidth, rHeight);

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

    private static bool ShouldApplyBackgroundTone(Sprite sp)
    {
        return sp.Id == 0 && sp.Type == SpriteType.Image && sp.BackgroundTimeOfDay > 1;
    }

    private static string TextureCacheKey(Sprite sp)
    {
        return ShouldApplyBackgroundTone(sp)
            ? $"{sp.ImagePath}::bgtime={sp.BackgroundTimeOfDay}:{sp.BackgroundTimePreset}"
            : sp.ImagePath;
    }

    private string ResolveImageForSprite(Sprite sp)
    {
        try
        {
            return _assetProvider.MaterializeToFile(sp.ImagePath);
        }
        catch when (sp.Id == 0)
        {
            foreach (string candidate in BuildBackgroundFallbackCandidates(sp.ImagePath))
            {
                try
                {
                    if (_assetProvider.Exists(candidate))
                    {
                        return _assetProvider.MaterializeToFile(candidate);
                    }
                }
                catch
                {
                    // 次候補へ進む。
                }
            }
            throw;
        }
    }

    private static IEnumerable<string> BuildBackgroundFallbackCandidates(string imagePath)
    {
        string normalized = imagePath.Replace('\\', '/');
        string dir = "";
        string file = normalized;
        int slash = normalized.LastIndexOf('/');
        if (slash >= 0)
        {
            dir = normalized[..(slash + 1)];
            file = normalized[(slash + 1)..];
        }

        string ext = Path.GetExtension(file);
        string stem = string.IsNullOrEmpty(ext) ? file : file[..^ext.Length];
        if (stem.Length <= 1) yield break;

        char suffix = char.ToLowerInvariant(stem[^1]);
        if (suffix is not ('a' or 'b' or 'c' or 'd' or 'e')) yield break;

        string baseStem = stem[..^1];
        if (!string.IsNullOrEmpty(ext))
        {
            yield return $"{dir}{baseStem}{ext}";
        }
        else
        {
            yield return $"{dir}{baseStem}.png";
            yield return $"{dir}{baseStem}.bmp";
            yield return $"{dir}{baseStem}.jpg";
            yield return $"assets/bg/{baseStem}.png";
            yield return $"assets/bg/{baseStem}.bmp";
            yield return $"assets/bg/{baseStem}.jpg";
        }
    }

    private Texture2D LoadBackgroundToneTexture(Sprite sp, string resolvedImagePath, string cacheKey)
    {
        Image image = default;
        bool imageLoaded = false;
        try
        {
            image = Raylib.LoadImage(resolvedImagePath);
            if (image.Width <= 0 || image.Height <= 0) return default;
            imageLoaded = true;
            ApplyBackgroundToneToImage(ref image, sp.BackgroundTimeOfDay, sp.BackgroundTimePreset);
            Texture2D texture = Raylib.LoadTextureFromImage(image);
            return texture;
        }
        catch (Exception ex)
        {
            _failedToneTextures.Add(cacheKey);
            _reporter?.ReportException(
                "RENDER_BGTIME_TEXTURE",
                ex,
                $"背景時間帯フィルタ '{sp.BackgroundTimeOfDay}:{sp.BackgroundTimePreset}' の生成に失敗しました。",
                AriaErrorLevel.Warning);
            return default;
        }
        finally
        {
            if (imageLoaded)
            {
                Raylib.UnloadImage(image);
            }
        }
    }

    private static void ApplyBackgroundToneToImage(ref Image image, int timeOfDay, string presetName)
    {
        BackgroundTonePreset preset = BackgroundTonePreset.Resolve(timeOfDay, presetName);
        int width = image.Width;
        int height = image.Height;
        float invW = width > 1 ? 1f / (width - 1) : 0f;
        float invH = height > 1 ? 1f / (height - 1) : 0f;

        for (int y = 0; y < height; y++)
        {
            float v = y * invH;
            for (int x = 0; x < width; x++)
            {
                float u = x * invW;
                Color c = Raylib.GetImageColor(image, x, y);
                Color adjusted = ApplyBackgroundTonePixel(c, u, v, preset);
                Raylib.ImageDrawPixel(ref image, x, y, adjusted);
            }
        }
    }

    private static Color ApplyBackgroundTonePixel(Color src, float u, float v, BackgroundTonePreset p)
    {
        float r = src.R / 255f;
        float g = src.G / 255f;
        float b = src.B / 255f;
        float lum = (r * 0.299f) + (g * 0.587f) + (b * 0.114f);
        float shadow = 1f - lum;
        float highlight = SmoothStep(0.55f, 1f, lum);

        r = Lerp(lum, r, p.Saturation);
        g = Lerp(lum, g, p.Saturation);
        b = Lerp(lum, b, p.Saturation);

        r = ApplyContrastGammaBrightness(r, p);
        g = ApplyContrastGammaBrightness(g, p);
        b = ApplyContrastGammaBrightness(b, p);

        r = Lerp(r, p.TintR, p.TintPower);
        g = Lerp(g, p.TintG, p.TintPower);
        b = Lerp(b, p.TintB, p.TintPower);

        r = Lerp(r, p.ShadowR, shadow * p.ShadowTintPower);
        g = Lerp(g, p.ShadowG, shadow * p.ShadowTintPower);
        b = Lerp(b, p.ShadowB, shadow * p.ShadowTintPower);

        r = Lerp(r, p.HighlightR, highlight * p.HighlightTintPower);
        g = Lerp(g, p.HighlightG, highlight * p.HighlightTintPower);
        b = Lerp(b, p.HighlightB, highlight * p.HighlightTintPower);

        float horizon = MathF.Pow(1f - Math.Clamp(v, 0f, 1f), 2.2f) * p.HorizonGlowPower;
        r += p.HorizonR * horizon;
        g += p.HorizonG * horizon;
        b += p.HorizonB * horizon;

        float dx = u - 0.5f;
        float dy = v - 0.5f;
        float vignette = SmoothStep(0.34f, 0.78f, MathF.Sqrt((dx * dx) + (dy * dy)));
        float edge = 1f - (vignette * p.VignettePower);
        r *= edge;
        g *= edge;
        b *= edge;

        float grain = (HashNoise(u, v) - 0.5f) * p.GrainPower;
        float dither = (HashNoise(u + 0.37f, v + 0.61f) - 0.5f) * p.DitherPower;
        r += grain + dither + p.BlackLift;
        g += grain + dither + p.BlackLift;
        b += grain + dither + p.BlackLift;

        return new Color(
            (byte)Math.Clamp((int)MathF.Round(r * 255f), 0, 255),
            (byte)Math.Clamp((int)MathF.Round(g * 255f), 0, 255),
            (byte)Math.Clamp((int)MathF.Round(b * 255f), 0, 255),
            src.A);
    }

    private static float ApplyContrastGammaBrightness(float value, BackgroundTonePreset p)
    {
        value = ((value - 0.5f) * p.Contrast) + 0.5f;
        value = MathF.Pow(Math.Clamp(value, 0f, 1f), p.Gamma);
        return value * p.Brightness;
    }

    private static float Lerp(float a, float b, float t) => a + ((b - a) * Math.Clamp(t, 0f, 1f));

    private static float SmoothStep(float edge0, float edge1, float x)
    {
        float t = Math.Clamp((x - edge0) / (edge1 - edge0), 0f, 1f);
        return t * t * (3f - (2f * t));
    }

    private static float HashNoise(float x, float y)
    {
        float n = MathF.Sin((x * 127.1f) + (y * 311.7f)) * 43758.5453f;
        return n - MathF.Floor(n);
    }

    private sealed class BackgroundTonePreset
    {
        public float Brightness { get; init; } = 1f;
        public float Contrast { get; init; } = 1f;
        public float Saturation { get; init; } = 1f;
        public float Gamma { get; init; } = 1f;
        public float TintR { get; init; } = 1f;
        public float TintG { get; init; } = 1f;
        public float TintB { get; init; } = 1f;
        public float TintPower { get; init; }
        public float ShadowR { get; init; }
        public float ShadowG { get; init; }
        public float ShadowB { get; init; }
        public float ShadowTintPower { get; init; }
        public float HighlightR { get; init; } = 1f;
        public float HighlightG { get; init; } = 1f;
        public float HighlightB { get; init; } = 1f;
        public float HighlightTintPower { get; init; }
        public float HorizonR { get; init; }
        public float HorizonG { get; init; }
        public float HorizonB { get; init; }
        public float HorizonGlowPower { get; init; }
        public float VignettePower { get; init; }
        public float GrainPower { get; init; }
        public float DitherPower { get; init; }
        public float BlackLift { get; init; }

        public static BackgroundTonePreset Resolve(int timeOfDay, string presetName)
        {
            string key = string.IsNullOrWhiteSpace(presetName)
                ? timeOfDay switch
                {
                    2 => "evening_cinematic",
                    3 => "night_moon",
                    4 => "midnight_room",
                    _ => "day"
                }
                : presetName.Trim().ToLowerInvariant();

            return key switch
            {
                "evening" or "evening_cinematic" => new BackgroundTonePreset
                {
                    Brightness = 0.92f,
                    Contrast = 1.10f,
                    Saturation = 1.12f,
                    Gamma = 0.96f,
                    TintR = 1.00f,
                    TintG = 0.60f,
                    TintB = 0.32f,
                    TintPower = 0.16f,
                    ShadowR = 0.19f,
                    ShadowG = 0.25f,
                    ShadowB = 0.37f,
                    ShadowTintPower = 0.12f,
                    HighlightR = 1.00f,
                    HighlightG = 0.82f,
                    HighlightB = 0.60f,
                    HighlightTintPower = 0.18f,
                    HorizonR = 1.00f,
                    HorizonG = 0.70f,
                    HorizonB = 0.42f,
                    HorizonGlowPower = 0.12f,
                    VignettePower = 0.10f,
                    GrainPower = 0.012f,
                    DitherPower = 0.004f
                },
                "night" or "night_moon" => new BackgroundTonePreset
                {
                    Brightness = 0.54f,
                    Contrast = 0.96f,
                    Saturation = 0.58f,
                    Gamma = 1.08f,
                    TintR = 0.18f,
                    TintG = 0.31f,
                    TintB = 0.53f,
                    TintPower = 0.28f,
                    ShadowR = 0.05f,
                    ShadowG = 0.10f,
                    ShadowB = 0.21f,
                    ShadowTintPower = 0.26f,
                    HighlightR = 0.61f,
                    HighlightG = 0.75f,
                    HighlightB = 1.00f,
                    HighlightTintPower = 0.08f,
                    HorizonR = 0.20f,
                    HorizonG = 0.32f,
                    HorizonB = 0.58f,
                    HorizonGlowPower = 0.04f,
                    VignettePower = 0.20f,
                    GrainPower = 0.018f,
                    DitherPower = 0.006f
                },
                "midnight" or "midnight_room" => new BackgroundTonePreset
                {
                    Brightness = 0.40f,
                    Contrast = 0.84f,
                    Saturation = 0.42f,
                    Gamma = 1.14f,
                    TintR = 0.09f,
                    TintG = 0.16f,
                    TintB = 0.30f,
                    TintPower = 0.34f,
                    ShadowR = 0.02f,
                    ShadowG = 0.04f,
                    ShadowB = 0.09f,
                    ShadowTintPower = 0.30f,
                    HighlightR = 0.42f,
                    HighlightG = 0.51f,
                    HighlightB = 0.72f,
                    HighlightTintPower = 0.05f,
                    HorizonR = 0.08f,
                    HorizonG = 0.14f,
                    HorizonB = 0.26f,
                    HorizonGlowPower = 0.03f,
                    VignettePower = 0.30f,
                    GrainPower = 0.022f,
                    DitherPower = 0.010f,
                    BlackLift = 0.035f
                },
                _ => new BackgroundTonePreset()
            };
        }
    }

    private void DrawRectSprite(Sprite sp, Color baseColor, int qx, int qy)
    {
        TotalDrawCalls++;

        byte fillAlpha = (byte)(sp.FillAlpha * Math.Clamp(sp.RenderOpacity, 0f, 1f));
        Color normalFill = ParseColor(sp.FillColor, fillAlpha);
        Color hoverFill = !string.IsNullOrEmpty(sp.HoverFillColor) ? ParseColor(sp.HoverFillColor, fillAlpha) : normalFill;
        Color fillColor = LerpColor(normalFill, hoverFill, sp.HoverProgress);

        float rWidth = SnapPixel(sp.Width * sp.RenderScaleX, _currentState);
        float rHeight = SnapPixel(sp.Height * sp.RenderScaleY, _currentState);
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
        if (_currentState?.TextRuntime.CurrentTextSegments != null && _currentState.TextRuntime.CurrentTextSegments.Count > 0
            && HasEffectSegments(_currentState.TextRuntime.CurrentTextSegments))
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
        if (_currentState?.TextRuntime.CurrentTextSegments == null) return;

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
        
        foreach (var seg in _currentState.TextRuntime.CurrentTextSegments)
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
            if (sp.TextAlign == TextAlignment.Center) cursorX += (sp.Width - totalWidth) / 2f;
            else if (sp.TextAlign == TextAlignment.Right) cursorX += (sp.Width - totalWidth);
        }
        else
        {
            if (sp.TextAlign == TextAlignment.Center) cursorX -= totalWidth / 2f;
            else if (sp.TextAlign == TextAlignment.Right) cursorX -= totalWidth;
        }

        if (sp.Height > 0)
        {
            if (sp.TextVAlign == TextVerticalAlignment.Center)
                cursorY += (sp.Height - totalHeight) / 2f;
            else if (sp.TextVAlign == TextVerticalAlignment.Bottom)
                cursorY += (sp.Height - totalHeight);
        }

        // タイプライター効果: DisplayedTextLength をセグメントに適用
        int remainingLength = _currentState.TextRuntime.DisplayedTextLength;

        // セグメントを順に描画（折り返し対応）
        float lineX = cursorX;
        float lineY = cursorY;
        float lineMaxHeight = baseFontSize + baseSpacing;

        foreach (var seg in _currentState.TextRuntime.CurrentTextSegments)
        {
            if (remainingLength <= 0 && _currentState.TextRuntime.TextSpeedMs > 0) break;

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
            if (_currentState.TextRuntime.TextSpeedMs > 0 && remainingLength < seg.Text.Length)
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
            
            float drawX = SnapPixel(lineX + shakeX, _currentState);
            float drawY = SnapPixel(lineY + shakeY, _currentState);
            
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

            // ルビ（ふりがな）描画
            if (!string.IsNullOrEmpty(seg.RubyText) && displayText.Length > 0)
            {
                float rubySize = segFontSize * 0.45f;
                float rubySpacing = rubySize / 10f;
                float baseTextWidth = Raylib.MeasureTextEx(segFont, displayText, segFontSize, segSpacing).X;
                float rubyTextWidth = Raylib.MeasureTextEx(segFont, seg.RubyText, rubySize, rubySpacing).X;
                float rubyX = drawX + (baseTextWidth - rubyTextWidth) / 2f;
                float rubyY = drawY - rubySize - 2f;
                Color rubyColor = segColor;
                rubyColor.A = (byte)(rubyColor.A * 0.8f);
                Raylib.DrawTextEx(segFont, seg.RubyText, new Vector2(rubyX, rubyY), rubySize, rubySpacing, rubyColor);
            }
            
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
            if (sp.TextAlign == TextAlignment.Center) rx += (sp.Width - textSize.X) / 2f;
            else if (sp.TextAlign == TextAlignment.Right) rx += (sp.Width - textSize.X);
        }
        else
        {
            if (sp.TextAlign == TextAlignment.Center) rx -= textSize.X / 2f;
            else if (sp.TextAlign == TextAlignment.Right) rx -= textSize.X;
        }

        // Vertical align inside explicit text area
        if (sp.Height > 0)
        {
            if (sp.TextVAlign == TextVerticalAlignment.Center)
            {
                ry += (sp.Height - textSize.Y) / 2f;
            }
            else if (sp.TextVAlign == TextVerticalAlignment.Bottom)
            {
                ry += (sp.Height - textSize.Y);
            }
        }

        Vector2 pos = new Vector2(rx, ry);
        pos = ApplyTextEffect(sp, pos);
        pos = SnapVector(pos, _currentState);

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
        var lineBuilder = StringHelper.RentStringBuilder();

        try
        {
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];

                if (c == '\n')
                {
                    builder.Append(lineBuilder).Append('\n');
                    lineBuilder.Clear();
                    continue;
                }

                lineBuilder.Append(c);
                var size = Raylib.MeasureTextEx(font, lineBuilder.ToString(), fontSize, spacing);

                if (size.X > maxWidth && lineBuilder.Length > 1)
                {
                    // 追加した文字を取り除いて改行
                    lineBuilder.Length--;
                    builder.Append(lineBuilder).Append('\n');
                    lineBuilder.Clear();
                    lineBuilder.Append(c);
                }
            }

            builder.Append(lineBuilder);
            return builder.ToString();
        }
        finally
        {
            // StringBuilderプールに返却
            StringHelper.ReturnStringBuilder(lineBuilder);
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
        return state?.UiQuality.SubpixelRendering == true ? value : MathF.Round(value);
    }

    private static Vector2 SnapVector(Vector2 value, GameState? state = null)
    {
        return state?.UiQuality.SubpixelRendering == true ? value : new Vector2(MathF.Round(value.X), MathF.Round(value.Y));
    }

    private static int GetRoundSegments(GameState? state)
    {
        return Math.Clamp(state?.UiQuality.RoundedRectSegments ?? UiQualityConstants.DefaultRoundedRectSegments, UiQualityConstants.MinRoundedRectSegments, UiQualityConstants.MaxRoundedRectSegments);
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
        float response = Math.Clamp(state.UiQuality.MotionResponse, UiQualityConstants.MinMotionResponse, UiQualityConstants.MaxMotionResponse);
        float blend = state.UiQuality.SmoothMotion ? 1f - MathF.Exp(-response * dt) : 1f;

        foreach (var sp in state.Render.Sprites.Values)
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
        if (!state.UiRuntime.ShowClickCursor ||
            (state.Execution.State != VmState.WaitingForClick && state.Execution.State != VmState.WaitingForAnimation))
        {
            return;
        }

        if (state.Execution.State == VmState.WaitingForAnimation &&
            state.TextRuntime.DisplayedTextLength < state.TextRuntime.CurrentTextBuffer.Length)
        {
            return;
        }

        Vector2 cursorPos = GetTextEndCursorPosition(state);
        float x = SnapPixel(cursorPos.X + 2, state);
        float y = SnapPixel(cursorPos.Y + 2, state);

        if (state.UiRuntime.ClickCursorMode.Equals("image", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(state.UiRuntime.ClickCursorPath) &&
            !_failedTextures.Contains(state.UiRuntime.ClickCursorPath))
        {
            try
            {
                string resolved = _assetProvider.MaterializeToFile(state.UiRuntime.ClickCursorPath);
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
                    float targetSize = state.UiRuntime.ClickCursorSize > 0 ? state.UiRuntime.ClickCursorSize : Math.Clamp(state.TextWindow.DefaultFontSize * ClickCursorConstants.DefaultSizeRatio, ClickCursorConstants.MinSize, ClickCursorConstants.MaxSize);
                    float scale = Math.Clamp(targetSize / Math.Max(tex.Width, tex.Height), 0.1f, 1.0f);
                    Rectangle src = new Rectangle(0, 0, tex.Width, tex.Height);
                    Rectangle dst = new Rectangle(x, y, tex.Width * scale, tex.Height * scale);
                    Raylib.DrawTexturePro(tex, src, dst, Vector2.Zero, 0f, Color.White);
                    return;
                }
            }
            catch (Exception ex)
            {
                _failedTextures.Add(state.UiRuntime.ClickCursorPath);
                _reporter?.ReportException(
                    "RENDER_CLICK_CURSOR",
                    ex,
                    $"クリック待ちカーソル '{state.UiRuntime.ClickCursorPath}' を読み込めませんでした。既定カーソルで続行します。",
                    AriaErrorLevel.Warning);
            }
        }

        // Silver filled cursor with smooth double-bounce float animation
        // Constant size, gentle 4px amplitude, 2 bounces per 2.4s loop
        float loopDuration = 2.4f;
        float time = (float)Raylib.GetTime();
        float t = (time % loopDuration) / loopDuration;

        // Two gentle bounces using smooth sine composition
        float bounce1 = MathF.Sin(t * MathF.PI * 2f);           // main bounce
        float bounce2 = MathF.Sin(t * MathF.PI * 4f + 0.5f);    // secondary ripple
        float combined = (bounce1 + bounce2 * 0.3f) / 1.3f;     // blend, keep range [-1, 1]

        // Map to small upward float only (never dips below base position)
        float floatOffset = -MathF.Abs(combined) * 4f; // max 4px up, smooth settle

        float baseSize = state.UiRuntime.ClickCursorSize > 0 ? state.UiRuntime.ClickCursorSize : Math.Clamp(state.TextWindow.DefaultFontSize * ClickCursorConstants.DefaultSizeRatio, ClickCursorConstants.MinSize, ClickCursorConstants.MaxSize);
        float s = baseSize; // constant size, no scale change

        // Silver fill color
        var silver = new Color(205, 205, 215, 255);
        float cy = y + floatOffset;

        // Simple filled downward triangle
        Raylib.DrawTriangle(
            new Vector2(x, cy),
            new Vector2(x + s, cy),
            new Vector2(x + s * 0.5f, cy + s * 1.1f),
            silver);

        // Subtle white highlight edge for depth
        var highlight = new Color(230, 230, 240, 200);
        Raylib.DrawTriangleLines(
            new Vector2(x, cy),
            new Vector2(x + s, cy),
            new Vector2(x + s * 0.5f, cy + s * 1.1f),
            highlight);
    }

    private Vector2 GetTextEndCursorPosition(GameState state)
    {
        if (state.TextWindow.TextTargetSpriteId >= 0 &&
            state.Render.Sprites.TryGetValue(state.TextWindow.TextTargetSpriteId, out var sp) &&
            sp.Type == SpriteType.Text &&
            _fontLoaded)
        {
            string text = string.IsNullOrEmpty(sp.Text) ? state.TextRuntime.CurrentTextBuffer : sp.Text;

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
            string wrapped = WrapText(font, text, sp.Width > 0 ? sp.Width : state.EngineSettings.WindowWidth, scaledFontSize, spacing);
            string[] lines = wrapped.Replace("\r\n", "\n").Split('\n');
            string lastLine = lines.Length > 0 ? lines[^1] : "";
            Vector2 lastLineSize = Raylib.MeasureTextEx(font, lastLine, scaledFontSize, spacing);
            Vector2 allTextSize = Raylib.MeasureTextEx(font, wrapped, scaledFontSize, spacing);

            float baseX = sp.X;
            float baseY = sp.Y;
            if (sp.Width > 0)
            {
                if (sp.TextAlign == TextAlignment.Center) baseX += (sp.Width - allTextSize.X) / 2f;
                else if (sp.TextAlign == TextAlignment.Right) baseX += sp.Width - allTextSize.X;
            }

            if (sp.Height > 0)
            {
                if (sp.TextVAlign == TextVerticalAlignment.Center) baseY += (sp.Height - allTextSize.Y) / 2f;
                else if (sp.TextVAlign == TextVerticalAlignment.Bottom) baseY += sp.Height - allTextSize.Y;
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

        return new Vector2(state.TextWindow.DefaultTextboxX + state.TextWindow.DefaultTextboxPaddingX, state.TextWindow.DefaultTextboxY + state.TextWindow.DefaultTextboxPaddingY);
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
        Raylib.DrawText($"PC: {state.Execution.ProgramCounter}", 10, 30, 20, Color.Green);
        Raylib.DrawText($"Sprites: {state.Render.Sprites.Count}", 10, 50, 20, Color.Green);
        Raylib.DrawText($"Draw Calls: {TotalDrawCalls}", 10, 70, 20, Color.Yellow);
        Raylib.DrawText($"Tex Loads: {TotalTextureLoads}", 10, 90, 20, Color.Yellow);

        // キャッシュ統計
        var cyanColor = new Color(0, 255, 255, 255);
        Raylib.DrawText($"Color Cache: {_colorCache.Count}", 10, 110, 20, cyanColor);
        Raylib.DrawText($"Tex Cache: {_textureCache.Count}", 10, 130, 20, cyanColor);

        var texStats = _textureCache.GetStats();
        Raylib.DrawText($"Tex Stats: {texStats.UtilizationPercent:F1}%", 10, 150, 16, cyanColor);

        // スプライト統計
        int spriteCount = _currentState?.Render.Sprites.Count ?? 0;
        Raylib.DrawText($"Sprites: {spriteCount}", 10, 170, 16, Color.Magenta);

        // レジスタ値（%0-%9）
        int y = 200;
        Raylib.DrawText("Registers:", 10, y, 14, Color.SkyBlue); y += 16;
        for (int r = 0; r < 10; r++)
        {
            string regName = r.ToString();
            int val = state.Execution.ProgramCounter; // dummy to get VM reference
            // Use fast registers or state lookup
            if (state.RegisterState.Registers.TryGetValue(regName, out int regVal) && regVal != 0)
            {
                Raylib.DrawText($"  %{r}: {regVal}", 10, y, 14, Color.White);
                y += 16;
            }
        }

        // フラグ情報
        if (state.FlagRuntime.Flags.Count > 0 || state.FlagRuntime.SaveFlags.Count > 0)
        {
            y += 4;
            Raylib.DrawText($"Flags: {state.FlagRuntime.Flags.Count}", 10, y, 14, Color.Orange); y += 16;
            Raylib.DrawText($"SaveFlags: {state.FlagRuntime.SaveFlags.Count}", 10, y, 14, Color.Orange); y += 16;
        }

        // シーン・状態情報
        y += 4;
        Raylib.DrawText($"Scene: {state.SceneRuntime.CurrentScene}", 10, y, 14, Color.Gold); y += 16;
        Raylib.DrawText($"VM: {state.Execution.State}", 10, y, 14, Color.Gold); y += 16;
        if (!string.IsNullOrEmpty(state.SaveRuntime.CurrentChapter))
        {
            Raylib.DrawText($"Chapter: {state.SaveRuntime.CurrentChapter}", 10, y, 14, Color.Gold); y += 16;
        }

        // テキストバッファプレビュー
        if (!string.IsNullOrEmpty(state.TextRuntime.CurrentTextBuffer))
        {
            y += 4;
            string preview = state.TextRuntime.CurrentTextBuffer.Length > 40
                ? state.TextRuntime.CurrentTextBuffer[..40] + "..."
                : state.TextRuntime.CurrentTextBuffer;
            Raylib.DrawText($"Text: {preview}", 10, y, 14, Color.Lime);
        }
    }

    /// <summary>
    /// 指定した画像パスのテクスチャキャッシュを無効化する（live reload用）
    /// </summary>
    public void InvalidateTexture(string imagePath)
    {
        if (string.IsNullOrWhiteSpace(imagePath)) return;
        lock (_renderLock)
        {
            string resolved = _assetProvider.MaterializeToFile(imagePath);
            if (_textureCache.TryGetValue(resolved, out var tex))
            {
                try
                {
                    if (tex.Id != 0)
                    {
                        Raylib.UnloadTexture(tex);
                    }
                }
                catch (Exception ex)
                {
                    _reporter?.ReportException("RENDER_TEXTURE_INVALIDATE", ex, $"画像 '{imagePath}' のキャッシュ無効化中にエラーが発生しました。", AriaErrorLevel.Warning);
                }
                _textureCache.Remove(resolved);
            }
            _failedTextures.Remove(imagePath);
        }
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
