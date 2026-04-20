using System;
using System.Collections.Concurrent;
using System.Text;

namespace AriaEngine.Utility;

/// <summary>
/// StringBuilderプール
/// 頻繁な文字列操作でメモリ割り当てを削減します。
/// </summary>
public class StringBuilderPool
{
    private readonly ConcurrentBag<StringBuilder> _pool;
    private readonly int _initialCapacity;
    private int _availableCount;

    public int AvailableCount => _availableCount;

    public StringBuilderPool(int poolSize = 32, int initialCapacity = 256)
    {
        _pool = new ConcurrentBag<StringBuilder>();
        _initialCapacity = initialCapacity;
        _availableCount = poolSize;

        // プールを初期化
        for (int i = 0; i < poolSize; i++)
        {
            _pool.Add(new StringBuilder(initialCapacity));
        }
    }

    /// <summary>
    /// StringBuilderをプールから取得します。
    /// プールが空の場合、新しいインスタンスを作成します。
    /// </summary>
    public StringBuilder Rent()
    {
        if (_pool.TryTake(out var builder))
        {
            builder.Clear();
            Interlocked.Decrement(ref _availableCount);
            return builder;
        }

        return new StringBuilder(_initialCapacity);
    }

    /// <summary>
    /// StringBuilderをプールに返却します。
    /// </summary>
    /// <param name="builder">返却するStringBuilder</param>
    public void Return(StringBuilder builder)
    {
        if (builder == null || builder.Capacity > _initialCapacity * 2)
        {
            // 大きすぎるStringBuilderは返却しない（GCに任せる）
            return;
        }

        Interlocked.Increment(ref _availableCount);
        _pool.Add(builder);
    }

    /// <summary>
    /// プール統計を取得します。
    /// </summary>
    public PoolStats GetStats()
    {
        return new PoolStats
        {
            AvailableCount = AvailableCount,
            TotalCapacity = AvailableCount * _initialCapacity
        };
    }
}

/// <summary>
/// ジェネリックオブジェクトプール
/// 頻繁に作成・破棄されるオブジェクトのメモリ割り当てを削減します。
/// </summary>
/// <typeparam name="T">オブジェクトの型</typeparam>
public class ObjectPool<T> where T : class, new()
{
    private readonly ConcurrentBag<T> _pool;
    private readonly int _poolSize;
    private readonly Action<T>? _resetAction;
    private int _availableCount;

    public int AvailableCount => _availableCount;
    public int PoolSize => _poolSize;

    public ObjectPool(int poolSize = 32, Action<T>? resetAction = null)
    {
        _pool = new ConcurrentBag<T>();
        _poolSize = poolSize;
        _resetAction = resetAction;
        _availableCount = poolSize;

        // プールを初期化
        for (int i = 0; i < poolSize; i++)
        {
            _pool.Add(new T());
        }
    }

    /// <summary>
    /// オブジェクトをプールから取得します。
    /// プールが空の場合、新しいインスタンスを作成します。
    /// </summary>
    public T Rent()
    {
        if (_pool.TryTake(out var obj))
        {
            Interlocked.Decrement(ref _availableCount);
            return obj;
        }

        return new T();
    }

    /// <summary>
    /// オブジェクトをプールに返却します。
    /// 返却前にリセットアクションが実行されます。
    /// </summary>
    /// <param name="obj">返却するオブジェクト</param>
    public void Return(T obj)
    {
        if (obj == null)
            return;

        // リセットアクションを実行
        _resetAction?.Invoke(obj);

        Interlocked.Increment(ref _availableCount);
        _pool.Add(obj);
    }

    /// <summary>
    /// プール統計を取得します。
    /// </summary>
    public PoolStats GetStats()
    {
        return new PoolStats
        {
            AvailableCount = AvailableCount,
            PoolSize = _poolSize
        };
    }
}

/// <summary>
/// プール統計情報
/// </summary>
public class PoolStats
{
    public int AvailableCount { get; set; }
    public int PoolSize { get; set; }
    public int TotalCapacity { get; set; }
    public double UtilizationPercent => PoolSize > 0 ? ((PoolSize - AvailableCount) * 100.0 / PoolSize) : 0;

    public override string ToString()
    {
        return $"利用中: {PoolSize - AvailableCount}/{PoolSize} ({UtilizationPercent:F1}%)";
    }
}

/// <summary>
/// 文字列操作の簡易メソッドを提供するユーティリティクラス
/// </summary>
public static class StringHelper
{
    private static StringBuilderPool? _stringBuilderPool;

    /// <summary>
    /// StringBuilderプールを初期化します。
    /// </summary>
    public static void InitializeStringBuilderPool(int poolSize = 32, int initialCapacity = 256)
    {
        _stringBuilderPool = new StringBuilderPool(poolSize, initialCapacity);
    }

    /// <summary>
    /// StringBuilderをプールから取得します。
    /// プールが空の場合、新しいインスタンスを作成します。
    /// </summary>
    public static StringBuilder RentStringBuilder()
    {
        if (_stringBuilderPool == null)
        {
            return new System.Text.StringBuilder();
        }

        return _stringBuilderPool.Rent();
    }

    /// <summary>
    /// StringBuilderをプールに返却します。
    /// </summary>
    /// <param name="builder">返却するStringBuilder</param>
    public static void ReturnStringBuilder(StringBuilder builder)
    {
        if (builder == null || _stringBuilderPool == null)
        {
            return;
        }

        _stringBuilderPool.Return(builder);
    }

    /// <summary>
    /// 複数の文字列を効率的に結合します。
    /// </summary>
    /// <param name="parts">結合する文字列の配列</param>
    /// <param name="separator">セパレータ（オプション）</param>
    /// <returns>結合された文字列</returns>
    public static string Join(IEnumerable<string> parts, string separator = "")
    {
        if (_stringBuilderPool == null)
        {
            return string.Join(separator, parts);
        }

        var builder = _stringBuilderPool.Rent();
        try
        {
            bool first = true;
            foreach (var part in parts)
            {
                if (!first)
                {
                    builder.Append(separator);
                }
                builder.Append(part);
                first = false;
            }

            return builder.ToString();
        }
        finally
        {
            _stringBuilderPool.Return(builder);
        }
    }

    /// <summary>
    /// テキストを効率的にラップします。
    /// </summary>
    /// <param name="text">ラップするテキスト</param>
    /// <param name="maxWidth">最大幅</param>
    /// <returns>ラップされたテキスト</returns>
    public static string WrapText(string text, int maxWidth)
    {
        if (_stringBuilderPool == null)
        {
            return text; // フォールバック
        }

        var builder = _stringBuilderPool.Rent();
        try
        {
            int currentLineLength = 0;

            foreach (var c in text)
            {
                if (c == '\n')
                {
                    builder.Append(c);
                    currentLineLength = 0;
                }
                else if (c == '\t')
                {
                    int tabWidth = 4 - (currentLineLength % 4);
                    if (currentLineLength + tabWidth > maxWidth)
                    {
                        builder.AppendLine();
                        currentLineLength = 0;
                    }
                    else
                    {
                        builder.Append(' ', tabWidth);
                        currentLineLength += tabWidth;
                    }
                }
                else if (currentLineLength >= maxWidth)
                {
                    builder.AppendLine();
                    builder.Append(c);
                    currentLineLength = 1;
                }
                else
                {
                    builder.Append(c);
                    currentLineLength++;
                }
            }

            return builder.ToString();
        }
        finally
        {
            _stringBuilderPool.Return(builder);
        }
    }

    /// <summary>
    /// 指定された回数だけ文字列を繰り返します。
    /// </summary>
    /// <param name="text">繰り返す文字列</param>
    /// <param name="count">繰り返し回数</param>
    /// <returns>繰り返された文字列</returns>
    public static string Repeat(string text, int count)
    {
        if (string.IsNullOrEmpty(text) || count <= 0)
            return string.Empty;

        if (_stringBuilderPool == null)
        {
            return text.Length == 1 ? new string(text[0], count) : string.Join("", text);
        }

        var builder = _stringBuilderPool.Rent();
        try
        {
            for (int i = 0; i < count; i++)
            {
                builder.Append(text);
            }

            return builder.ToString();
        }
        finally
        {
            _stringBuilderPool.Return(builder);
        }
    }
}
