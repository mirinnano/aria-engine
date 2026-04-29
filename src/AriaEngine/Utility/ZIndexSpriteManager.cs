using System;
using System.Collections.Generic;
using System.Linq;
using AriaEngine.Core;

namespace AriaEngine.Utility;

/// <summary>
/// Zインデックススプライトマネージャー
/// 毎フレームのスプライトソートを最適化し、パフォーマンスを向上させます。
/// </summary>
public class ZIndexSpriteManager
{
    // Z値をキー、スプライトIDを値とするバケット
    private readonly SortedDictionary<int, List<int>> _zBuckets;

    // ダーティフラグ：ソートが必要かどうか
    private bool _isDirty;

    // キャッシュされたソート済みスプライトIDリスト
    private readonly List<int> _cachedSortedSpriteIds;

    // キャッシュされたソート済みスプライトリスト
    private readonly List<Sprite> _cachedSortedSprites;

    // スプライト参照
    private readonly Func<int, Sprite?> _getSprite;

    // 統計情報
    public int TotalSprites => _cachedSortedSpriteIds.Count;
    public int BucketCount => _zBuckets.Count;
    public bool IsDirty => _isDirty;

    // 性能統計
    private long _sortCount;
    private long _cacheHitCount;
    private long _cacheMissCount;

    public PerformanceStats Stats => new PerformanceStats
    {
        SortCount = _sortCount,
        CacheHitCount = _cacheHitCount,
        CacheMissCount = _cacheMissCount,
        CacheHitRate = _sortCount > 0 ? (_cacheHitCount * 100.0 / (_cacheHitCount + _cacheMissCount)) : 0
    };

    public ZIndexSpriteManager(Func<int, Sprite?> getSprite)
    {
        _zBuckets = new SortedDictionary<int, List<int>>();
        _cachedSortedSpriteIds = new List<int>();
        _cachedSortedSprites = new List<Sprite>();
        _getSprite = getSprite;
        _isDirty = true;
    }

    /// <summary>
    /// スプライトを追加または更新します。
    /// ダーティフラグを設定し、ソートが必要であることを示します。
    /// </summary>
    /// <param name="spriteId">スプライトID</param>
    /// <param name="zOrder">Zオーダー値</param>
    public void AddOrUpdate(int spriteId, int zOrder)
    {
        // 既存のバケットから削除
        Remove(spriteId, updateOnly: true);

        // 新しいZ値のバケットに追加
        if (!_zBuckets.TryGetValue(zOrder, out var bucket))
        {
            bucket = new List<int>();
            _zBuckets[zOrder] = bucket;
        }

        // バケットの末尾に追加（挿入ソートは複雑なため）
        bucket.Add(spriteId);

        _isDirty = true;
    }

    /// <summary>
    /// スプライトを削除します（公開）。
    /// </summary>
    /// <param name="spriteId">スプライトID</param>
    /// <returns>削除に成功した場合true</returns>
    public bool Remove(int spriteId)
    {
        return Remove(spriteId, false);
    }

    /// <summary>
    /// スプライトを削除します（内部）。
    /// </summary>
    /// <param name="spriteId">スプライトID</param>
    /// <param name="updateOnly">更新専用フラグ（ダーティフラグ設定有無効化）</param>
    /// <returns>削除に成功した場合true</returns>
    private bool Remove(int spriteId, bool updateOnly = false)
    {
        foreach (var kvp in _zBuckets)
        {
            if (kvp.Value.Remove(spriteId))
            {
                if (kvp.Value.Count == 0)
                {
                    _zBuckets.Remove(kvp.Key);
                }

                if (!updateOnly)
                {
                    _isDirty = true;
                }

                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// スプライトのZ値を変更します。
    /// </summary>
    /// <param name="spriteId">スプライトID</param>
    /// <param name="oldZOrder">古いZオーダー値</param>
    /// <param name="newZOrder">新しいZオーダー値</param>
    public void UpdateZOrder(int spriteId, int oldZOrder, int newZOrder)
    {
        AddOrUpdate(spriteId, newZOrder);
    }

    /// <summary>
    /// Z順でソートされたスプライトIDリストを取得します。
    /// ダーティフラグが設定されている場合のみ再ソートされます。
    /// </summary>
    /// <param name="includeInvisible">非表示スプライトを含むか</param>
    /// <returns>ソートされたスプライトIDリスト</returns>
    public List<int> GetSortedSpriteIds(bool includeInvisible = false)
    {
        if (_isDirty)
        {
            // 再構築が必要
            _cachedSortedSpriteIds.Clear();
            _cachedSortedSprites.Clear();

            foreach (var kvp in _zBuckets)
            {
                if (includeInvisible)
                {
                    _cachedSortedSpriteIds.AddRange(kvp.Value);
                }
                else
                {
                    // 表示中のスプライトのみを追加
                    foreach (var spriteId in kvp.Value)
                    {
                        var sprite = _getSprite(spriteId);
                        if (sprite != null && sprite.Visible && sprite.Opacity > 0)
                        {
                            _cachedSortedSpriteIds.Add(spriteId);
                        }
                    }
                }
            }

            _isDirty = false;
            _sortCount++;
            _cacheMissCount++;
        }
        else
        {
            // キャッシュヒット
            _cacheHitCount++;
        }

        // キャッシュされたリストを直接返す（呼び出し元は読み取り専用で使用）
        return _cachedSortedSpriteIds;
    }

    /// <summary>
    /// Z順でソートされたスプライトリストを取得します。
    /// </summary>
    /// <param name="includeInvisible">非表示スプライトを含むか</param>
    /// <returns>ソートされたスプライトリスト</returns>
    public List<Sprite> GetSortedSprites(bool includeInvisible = false)
    {
        var sortedIds = GetSortedSpriteIds(includeInvisible);

        // キャッシュされたスプライトリストを再利用
        _cachedSortedSprites.Clear();
        _cachedSortedSprites.Capacity = sortedIds.Count; // Capacityを事前に設定

        foreach (var spriteId in sortedIds)
        {
            var sprite = _getSprite(spriteId);
            if (sprite != null)
            {
                _cachedSortedSprites.Add(sprite);
            }
        }

        return _cachedSortedSprites;
    }

    /// <summary>
    /// 指定されたZ値の範囲内のスプライトを取得します。
    /// </summary>
    /// <param name="minZ">最小Z値</param>
    /// <param name="maxZ">最大Z値</param>
    /// <param name="includeInvisible">非表示スプライトを含むか</param>
    /// <returns>Z範囲内のスプライトリスト</returns>
    public List<Sprite> GetSpritesInRange(int minZ, int maxZ, bool includeInvisible = false)
    {
        var result = new List<Sprite>();
        foreach (var kvp in _zBuckets)
        {
            if (kvp.Key < minZ)
            {
                continue;
            }

            if (kvp.Key > maxZ)
            {
                break;
            }

            foreach (var spriteId in kvp.Value)
            {
                var sprite = _getSprite(spriteId);
                if (sprite != null && (includeInvisible || (sprite.Visible && sprite.Opacity > 0)))
                {
                    result.Add(sprite);
                }
            }
        }

        return result;
    }

    /// <summary>
    /// 全てのスプライトをクリアします。
    /// </summary>
    public void Clear()
    {
        _zBuckets.Clear();
        _cachedSortedSpriteIds.Clear();
        _cachedSortedSprites.Clear();
        _isDirty = true;
    }

    /// <summary>
    /// ダーティフラグをリセットします（手動ソートトリガー用）。
    /// </summary>
    public void ResetDirtyFlag()
    {
        _isDirty = false;
    }

    /// <summary>
    /// 統計をリセットします。
    /// </summary>
    public void ResetStats()
    {
        _sortCount = 0;
        _cacheHitCount = 0;
        _cacheMissCount = 0;
    }


}

/// <summary>
/// 性能統計情報
/// </summary>
public class PerformanceStats
{
    public long SortCount { get; set; }
    public long CacheHitCount { get; set; }
    public long CacheMissCount { get; set; }
    public double CacheHitRate { get; set; }

    public override string ToString()
    {
        return $"ソート: {SortCount}, キャッシュヒット: {CacheHitCount} ({CacheHitRate:F1}%)";
    }
}
