namespace AriaEngine.Core;

/// <summary>
/// スプライト関連の定数
/// </summary>
public static class SpriteConstants
{
    /// <summary>
    /// 互換UIスプライトの開始ID
    /// </summary>
    public const int CompatUiStartId = 50000;

    /// <summary>
    /// 互換UIスプライトの最小Zインデックス
    /// </summary>
    public const int CompatUiMinZIndex = 9500;

    /// <summary>
    /// テキストボックスのZインデックス
    /// </summary>
    public const int TextboxZIndex = 9000;

    /// <summary>
    /// テキストコンテンツのZインデックス
    /// </summary>
    public const int TextContentZIndex = 9001;
}

/// <summary>
/// キャッシュ関連の定数
/// </summary>
public static class CacheConstants
{
    /// <summary>
    /// スプライトプールのデフォルトサイズ
    /// </summary>
    public const int SpritePoolDefaultSize = 128;

    /// <summary>
    /// テクスチャキャッシュの最大アイテム数
    /// </summary>
    public const int TextureCacheMaxItems = 100;

    /// <summary>
    /// テクスチャキャッシュの最大バイト数（500MB）
    /// </summary>
    public const long TextureCacheMaxBytes = 500 * 1024 * 1024;

    /// <summary>
    /// カラーキャッシュのサイズ
    /// </summary>
    public const int ColorCacheSize = 256;

    /// <summary>
    /// テキスト履歴の最大数
    /// </summary>
    public const int MaxTextHistory = 300;
}

/// <summary>
/// フォント関連の定数
/// </summary>
public static class FontConstants
{
    /// <summary>
    /// デフォルトのフォントアトラスサイズ
    /// </summary>
    public const int DefaultAtlasSize = 192;

    /// <summary>
    /// 最小フォントサイズ
    /// </summary>
    public const int MinFontSize = 8;

    /// <summary>
    /// 最大フォントサイズ
    /// </summary>
    public const int MaxFontSize = 192;

    /// <summary>
    /// デフォルトフォントサイズ
    /// </summary>
    public const int DefaultFontSize = 26;

    /// <summary>
    /// 最小フォントアトラスサイズ
    /// </summary>
    public const int MinAtlasSize = 8;

    /// <summary>
    /// 最大フォントアトラスサイズ
    /// </summary>
    public const int MaxAtlasSize = 192;
}

/// <summary>
/// スキップモード関連の定数
/// </summary>
public static class SkipConstants
{
    /// <summary>
    /// スキップモードの1フレームあたりの進行数
    /// </summary>
    public const int SkipAdvancePerFrame = 3;

    /// <summary>
    /// 強制スキップモードの1フレームあたりの進行数
    /// </summary>
    public const int ForceSkipAdvancePerFrame = 64;
}

/// <summary>
/// UI品質関連の定数
/// </summary>
public static class UiQualityConstants
{
    /// <summary>
    /// 角丸長方形のセグメント数（デフォルト）
    /// </summary>
    public const int DefaultRoundedRectSegments = 48;

    /// <summary>
    /// 角丸長方形の最小セグメント数
    /// </summary>
    public const int MinRoundedRectSegments = 12;

    /// <summary>
    /// 角丸長方形の最大セグメント数
    /// </summary>
    public const int MaxRoundedRectSegments = 96;

    /// <summary>
    /// UIモーションレスポンス（デフォルト）
    /// </summary>
    public const float DefaultMotionResponse = 14f;

    /// <summary>
    /// UIモーションレスポンスの最小値
    /// </summary>
    public const float MinMotionResponse = 1f;

    /// <summary>
    /// UIモーションレスポンスの最大値
    /// </summary>
    public const float MaxMotionResponse = 40f;

    /// <summary>
    /// フレーム時間の最大値（秒）
    /// </summary>
    public const float MaxFrameTime = 1f / 15f;
}

/// <summary>
/// クリックカーソル関連の定数
/// </summary>
public static class ClickCursorConstants
{
    /// <summary>
    /// クリックカーソルの最小サイズ
    /// </summary>
    public const float MinSize = 8f;

    /// <summary>
    /// クリックカーソルの最大サイズ
    /// </summary>
    public const float MaxSize = 14f;

    /// <summary>
    /// クリックカーソルのデフォルトサイズ倍率
    /// </summary>
    public const float DefaultSizeRatio = 0.34f;
}
