using System;
using System.Collections.Concurrent;
using AriaEngine.Core;

namespace AriaEngine.Utility;

/// <summary>
/// スプライトオブジェクトプール
/// 頻繁に作成・破棄されるスプライトのメモリ割り当てを削減します。
/// </summary>
public class SpritePool
{
    private readonly ConcurrentBag<Sprite> _pool;
    private readonly int _poolSize;
    private int _availableCount;

    public int AvailableCount => _availableCount;
    public int PoolSize => _poolSize;

    public SpritePool(int poolSize = 64)
    {
        _pool = new ConcurrentBag<Sprite>();
        _poolSize = poolSize;
        _availableCount = poolSize;

        // プールを初期化
        for (int i = 0; i < poolSize; i++)
        {
            _pool.Add(new Sprite());
        }
    }

    /// <summary>
    /// スプライトをプールから取得します。
    /// プールが空の場合、新しいインスタンスを作成します。
    /// </summary>
    public Sprite Rent()
    {
        if (_pool.TryTake(out var sprite))
        {
            // スプライトをリセット
            ResetSprite(sprite);
            Interlocked.Decrement(ref _availableCount);
            return sprite;
        }

        // プールが空の場合は新しいインスタンスを作成
        return new Sprite();
    }

    /// <summary>
    /// スプライトをプールに返却します。
    /// </summary>
    /// <param name="sprite">返却するスプライト</param>
    public void Return(Sprite sprite)
    {
        if (sprite == null)
            return;

        // スプライトをリセットして返却
        ResetSprite(sprite);
        Interlocked.Increment(ref _availableCount);
        _pool.Add(sprite);
    }

    /// <summary>
    /// スプライトをリセットします。
    /// </summary>
    private void ResetSprite(Sprite sprite)
    {
        sprite.Id = 0;
        sprite.Type = SpriteType.Rect;
        sprite.X = 0;
        sprite.Y = 0;
        sprite.Z = 0;
        sprite.Visible = true;
        sprite.Opacity = 1.0f;
        sprite.ScaleX = 1.0f;
        sprite.ScaleY = 1.0f;
        sprite.Rotation = 0f;
        sprite.ImagePath = "";
        sprite.Text = "";
        sprite.FontSize = 26;
        sprite.Color = "#ffffff";
        sprite.TextAlign = "left";
        sprite.TextShadowColor = "";
        sprite.TextShadowX = 0;
        sprite.TextShadowY = 0;
        sprite.TextOutlineColor = "";
        sprite.TextOutlineSize = 0;
        sprite.Width = 0;
        sprite.Height = 0;
        sprite.FillColor = "#000000";
        sprite.FillAlpha = 255;
        sprite.CornerRadius = 0;
        sprite.BorderColor = "";
        sprite.BorderWidth = 0;
        sprite.BorderOpacity = 255;
        sprite.GradientTo = "";
        sprite.GradientDirection = "vertical";
        sprite.ShadowColor = "";
        sprite.ShadowOffsetX = 0;
        sprite.ShadowOffsetY = 0;
        sprite.ShadowAlpha = 128;
        sprite.IsButton = false;
        sprite.ClickAreaX = 0;
        sprite.ClickAreaY = 0;
        sprite.ClickAreaW = 0;
        sprite.ClickAreaH = 0;
        sprite.HoverFillColor = "";
        sprite.HoverScale = 1.0f;
        sprite.IsHovered = false;
        sprite.IsDirty = true;
        sprite.LastModified = DateTime.Now;
    }

    /// <summary>
    /// 特定ID範囲のスプライトを一括して作成・プールに追加します。
    /// </summary>
    /// <param name="startId">開始ID</param>
    /// <param name="count">作成数</param>
    public void PreloadSprites(int startId, int count)
    {
        for (int i = 0; i < count; i++)
        {
            var sprite = Rent();
            sprite.Id = startId + i;
            // ゲームステートに追加せず、プール内で管理
            Return(sprite);
        }
    }

    /// <summary>
    /// プールをクリアします。
    /// </summary>
    public void Clear()
    {
        while (_pool.TryTake(out _))
        {
            // プールを空にする
        }
        _availableCount = 0;
    }

    /// <summary>
    /// プール統計情報を取得します。
    /// </summary>
    public SpritePoolStats GetStats()
    {
        int inUse = _poolSize - _availableCount;

        return new SpritePoolStats
        {
            AvailableCount = _availableCount,
            PoolSize = _poolSize,
            TotalCapacity = _poolSize * 512, // 推定Spriteサイズ
            UtilizationPercent = _poolSize > 0 ? (inUse * 100.0 / _poolSize) : 0
        };
    }
}

/// <summary>
/// スプライトプール統計情報
/// </summary>
public class SpritePoolStats
{
    public int AvailableCount { get; set; }
    public int PoolSize { get; set; }
    public int TotalCapacity { get; set; }
    public double UtilizationPercent { get; set; }

    public override string ToString()
    {
        return $"利用中: {PoolSize - AvailableCount}/{PoolSize} ({UtilizationPercent:F1}%)";
    }
}
