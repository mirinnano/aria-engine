using System;
using System.Collections.Generic;

namespace AriaEngine.Utility;

/// <summary>
/// LRU（Least Recently Used）キャッシュ実装
/// メモリ使用量を制限しつつ、頻繁に使用されるアイテムを保持します。
/// </summary>
public class LRUCache<TKey, TValue> where TKey : notnull
{
    private readonly int _capacity;
    private readonly Dictionary<TKey, LinkedListNode<CacheItem>> _cache;
    private readonly LinkedList<CacheItem> _lruList;
    private readonly Action<TValue>? _onEviction;
    private long _totalSize;
    private readonly long _maxSize;

    public int Count => _cache.Count;
    public int Capacity => _capacity;
    public long TotalSize => _totalSize;

    /// <summary>
    /// キャッシュの容量制限を指定して初期化
    /// </summary>
    /// <param name="capacity">最大アイテム数</param>
    /// <param name="onEviction">アイテム追い出し時のコールバック（オプション）</param>
    public LRUCache(int capacity, long maxSize = long.MaxValue, Action<TValue>? onEviction = null)
    {
        if (capacity <= 0)
            throw new ArgumentException("キャパシティは0より大きい必要があります", nameof(capacity));

        _capacity = capacity;
        _maxSize = maxSize;
        _onEviction = onEviction;
        _cache = new Dictionary<TKey, LinkedListNode<CacheItem>>(capacity);
        _lruList = new LinkedList<CacheItem>();
    }

    /// <summary>
    /// アイテムを取得します。存在する場合、LRU順序を更新します。
    /// </summary>
    /// <param name="key">キー</param>
    /// <param name="value">取得した値</param>
    /// <returns>アイテムが存在した場合true</returns>
    public bool TryGetValue(TKey key, out TValue? value)
    {
        if (!_cache.TryGetValue(key, out var node))
        {
            value = default;
            return false;
        }

        // LRU順序を更新：ノードをリストの先頭に移動
        _lruList.Remove(node);
        _lruList.AddFirst(node);

        value = node.Value.Item;
        return true;
    }

    /// <summary>
    /// アイテムを追加または更新します。
    /// </summary>
    /// <param name="key">キー</param>
    /// <param name="value">値</param>
    /// <param name="size">アイテムのサイズ（オプション）</param>
    public void AddOrUpdate(TKey key, TValue value, long size = 1)
    {
        if (_cache.TryGetValue(key, out var existingNode))
        {
            // 既存のアイテムを更新
            _totalSize -= existingNode.Value.Size;
            existingNode.Value.Item = value;
            existingNode.Value.Size = size;
            _totalSize += size;

            // LRU順序を更新
            _lruList.Remove(existingNode);
            _lruList.AddFirst(existingNode);

            EvictIfNeeded();
        }
        else
        {
            // 新しいアイテムを追加
            var newNode = new LinkedListNode<CacheItem>(new CacheItem { Key = key, Item = value, Size = size });
            _lruList.AddFirst(newNode);
            _cache[key] = newNode;
            _totalSize += size;

            EvictIfNeeded();
        }
    }

    /// <summary>
    /// アイテムを削除します。
    /// </summary>
    /// <param name="key">キー</param>
    /// <returns>削除に成功した場合true</returns>
    public bool Remove(TKey key)
    {
        if (!_cache.TryGetValue(key, out var node))
            return false;

        _lruList.Remove(node);
        _cache.Remove(key);
        _totalSize -= node.Value.Size;

        if (_onEviction != null)
        {
            _onEviction(node.Value.Item);
        }

        return true;
    }

    /// <summary>
    /// キャッシュをクリアします。
    /// </summary>
    public void Clear()
    {
        if (_onEviction != null)
        {
            foreach (var item in _lruList)
            {
                _onEviction(item.Item);
            }
        }

        _cache.Clear();
        _lruList.Clear();
        _totalSize = 0;
    }

    /// <summary>
    /// 必要に応じてアイテムを追い出します。
    /// </summary>
    private void EvictIfNeeded()
    {
        // サイズ制限を超える場合追い出し
        while ((_lruList.Count > _capacity || _totalSize > _maxSize) && _lruList.Count > 0)
        {
            var lruNode = _lruList.Last; // 最も古いアイテム
            if (lruNode == null) break;
            _lruList.RemoveLast();
            _cache.Remove(lruNode.Value.Key);
            _totalSize -= lruNode.Value.Size;

            if (_onEviction != null)
            {
                _onEviction(lruNode.Value.Item);
            }
        }
    }

    /// <summary>
    /// キャッシュ内の全てのキーを取得します。
    /// </summary>
    public IEnumerable<TKey> GetKeys()
    {
        foreach (var item in _lruList)
        {
            yield return item.Key;
        }
    }

    /// <summary>
    /// キャッシュ統計情報を取得します。
    /// </summary>
    public CacheStats GetStats()
    {
        return new CacheStats
        {
            Count = _lruList.Count,
            Capacity = _capacity,
            TotalSize = _totalSize,
            MaxSize = _maxSize,
            UtilizationPercent = _capacity > 0 ? (_lruList.Count * 100.0 / _capacity) : 0
        };
    }

    private class CacheItem
    {
        public TKey Key { get; set; } = default!;
        public TValue Item { get; set; } = default!;
        public long Size { get; set; }
    }
}

/// <summary>
/// キャッシュ統計情報
/// </summary>
public class CacheStats
{
    public int Count { get; set; }
    public int Capacity { get; set; }
    public long TotalSize { get; set; }
    public long MaxSize { get; set; }
    public double UtilizationPercent { get; set; }

    public override string ToString()
    {
        return $"使用: {Count}/{Capacity} ({UtilizationPercent:F1}%), サイズ: {TotalSize}/{MaxSize} bytes";
    }
}
