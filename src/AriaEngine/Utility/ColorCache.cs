using System;
using Raylib_cs;

namespace AriaEngine.Utility;

/// <summary>
/// 色キャッシュ
/// 16進数の色コードをColorオブジェクトにキャッシュし、パース処理を最適化します。
/// </summary>
public class ColorCache
{
    private readonly LRUCache<string, Color> _colorCache;
    private readonly object _lock = new();
    public int TotalColorParses { get; private set; }

    public int Count => _colorCache.Count;
    public ColorCacheStats Stats => new ColorCacheStats { TotalParses = TotalColorParses, Count = _colorCache.Count };

    public ColorCache(int capacity = 256)
    {
        _colorCache = new LRUCache<string, Color>(capacity);
    }

    /// <summary>
    /// 16進数の色コードからColorオブジェクトを取得します。
    /// キャッシュに存在する場合はキャッシュされた値を使用します。
    /// </summary>
    /// <param name="hex">16進数の色コード（例: "#ffffff"）</param>
    /// <param name="alpha">透明度（0-255）</param>
    /// <returns>Colorオブジェクト</returns>
    public Color GetColor(string hex, int alpha = 255)
    {
        lock (_lock)
        {
            var cacheKey = $"{hex}_{alpha}";

            if (_colorCache.TryGetValue(cacheKey, out var cachedColor))
            {
                return cachedColor;
            }

            // 新しい色をパースしてキャッシュ
            TotalColorParses++;
            var color = ParseColorFromHex(hex, alpha);
            _colorCache.AddOrUpdate(cacheKey, color, 4); // Colorは4バイト（RGBA）
            return color;
        }
    }

    /// <summary>
    /// 定義済みの色をプリロードします。
    /// </summary>
    public void PreloadCommonColors()
    {
        lock (_lock)
        {
            var commonColors = new[]
            {
                ("#000000", "black"),
                ("#ffffff", "white"),
                ("#ff0000", "red"),
                ("#00ff00", "green"),
                ("#0000ff", "blue"),
                ("#ffff00", "yellow"),
                ("#00ffff", "cyan"),
                ("#ff00ff", "magenta"),
                ("#ff8800", "orange"),
                ("#888888", "gray"),
                ("#cccccc", "lightgray"),
                ("#444444", "darkgray"),
                ("#2a2a3e", "darkblue"),
                ("#4a6fa5", "skyblue"),
                ("#3a3a5e", "mediumblue"),
                ("#1a1a2e", "verydarkblue")
            };

            foreach (var (hex, _) in commonColors)
            {
                GetColor(hex, 255); // キャッシュに追加
            }
        }
    }

    /// <summary>
    /// キャッシュをクリアします。
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _colorCache.Clear();
        }
    }

    /// <summary>
    /// 16進数の色コードをColorオブジェクトにパースします。
    /// </summary>
    /// <param name="hex">16進数の色コード</param>
    /// <param name="alpha">透明度（0-255）</param>
    /// <returns>Colorオブジェクト</returns>
    private Color ParseColorFromHex(string hex, int alpha)
    {
        if (string.IsNullOrEmpty(hex))
            return new Color(255, 255, 255, alpha);

        // #を削除
        hex = hex.TrimStart('#');

        try
        {
            // 6桁の16進数（RGB）
            if (hex.Length == 6)
            {
                byte r = byte.Parse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
                byte g = byte.Parse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
                byte b = byte.Parse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);
                return new Color((int)r, (int)g, (int)b, alpha);
            }
            // 8桁の16進数（RGBA）
            else if (hex.Length == 8)
            {
                byte r = byte.Parse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
                byte g = byte.Parse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
                byte b = byte.Parse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);
                byte a = byte.Parse(hex.Substring(6, 2), System.Globalization.NumberStyles.HexNumber);
                return new Color((int)r, (int)g, (int)b, (int)a);
            }
        }
        catch (FormatException)
        {
            // パースエラー時はデフォルト色を返す
        }

        return new Color(255, 255, 255, alpha);
    }

    /// <summary>
    /// 定義済みの色名（簡易アクセス用）
    /// </summary>
    public static class CommonColors
    {
        public static readonly Color Black = new Color(0, 0, 0, 255);
        public static readonly Color White = new Color(255, 255, 255, 255);
        public static readonly Color Red = new Color(255, 0, 0, 255);
        public static readonly Color Green = new Color(0, 255, 0, 255);
        public static readonly Color Blue = new Color(0, 0, 255, 255);
        public static readonly Color Yellow = new Color(255, 255, 0, 255);
        public static readonly Color Cyan = new Color(0, 255, 255, 255);
        public static readonly Color Magenta = new Color(255, 0, 255, 255);
        public static readonly Color Transparent = new Color(0, 0, 0, 0);
    }

    /// <summary>
    /// 色パース統計（デバッグ用）
    /// </summary>
    public class ColorCacheStats
    {
        public int TotalParses { get; set; }
        public int CacheHits { get; set; }
        public int Count { get; set; }

        public double CacheHitRate => TotalParses > 0 ? (CacheHits * 100.0 / TotalParses) : 0;

        public override string ToString()
        {
            return $"パース: {TotalParses}, ヒット: {CacheHits} ({CacheHitRate:F1}%)";
        }
    }
}
