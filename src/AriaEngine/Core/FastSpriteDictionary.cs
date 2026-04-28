using System.Text.Json;
using System.Text.Json.Serialization;

namespace AriaEngine.Core;

/// <summary>
/// 高速スプライト辞書（固定配列 + Dictionary フォールバック）
/// ID 0-99 を固定配列、100以上を Dictionary で管理
/// </summary>
[JsonConverter(typeof(FastSpriteDictionaryJsonConverter))]
public class FastSpriteDictionary : IDictionary<int, Sprite>
{
    private const int FastArraySize = 100;
    private readonly Sprite?[] _fastArray = new Sprite?[FastArraySize];
    private readonly Dictionary<int, Sprite> _dictionary = new();
    
    public int Count => _fastArray.Count(s => s != null) + _dictionary.Count;
    public bool IsReadOnly => false;
    
    public ICollection<int> Keys
    {
        get
        {
            var keys = new List<int>();
            for (int i = 0; i < FastArraySize; i++)
                if (_fastArray[i] != null) keys.Add(i);
            keys.AddRange(_dictionary.Keys);
            return keys;
        }
    }
    
    public ICollection<Sprite> Values
    {
        get
        {
            var values = new List<Sprite>();
            for (int i = 0; i < FastArraySize; i++)
                if (_fastArray[i] != null) values.Add(_fastArray[i]!);
            values.AddRange(_dictionary.Values);
            return values;
        }
    }
    
    public Sprite this[int key]
    {
        get
        {
            if (key >= 0 && key < FastArraySize)
            {
                var sprite = _fastArray[key];
                if (sprite != null) return sprite;
                throw new KeyNotFoundException($"Sprite ID {key} not found.");
            }
            return _dictionary[key];
        }
        set
        {
            if (key >= 0 && key < FastArraySize)
                _fastArray[key] = value;
            else
                _dictionary[key] = value;
        }
    }
    
    public bool TryGetValue(int key, out Sprite value)
    {
        if (key >= 0 && key < FastArraySize)
        {
            var sprite = _fastArray[key];
            if (sprite != null)
            {
                value = sprite;
                return true;
            }
            value = null!;
            return false;
        }
        return _dictionary.TryGetValue(key, out value!);
    }
    
    public bool ContainsKey(int key)
    {
        if (key >= 0 && key < FastArraySize)
            return _fastArray[key] != null;
        return _dictionary.ContainsKey(key);
    }
    
    public void Add(int key, Sprite value)
    {
        if (key >= 0 && key < FastArraySize)
        {
            if (_fastArray[key] != null)
                throw new ArgumentException($"Key {key} already exists.");
            _fastArray[key] = value;
        }
        else
        {
            _dictionary.Add(key, value);
        }
    }
    
    public bool Remove(int key)
    {
        if (key >= 0 && key < FastArraySize)
        {
            if (_fastArray[key] != null)
            {
                _fastArray[key] = null;
                return true;
            }
            return false;
        }
        return _dictionary.Remove(key);
    }
    
    public FastSpriteDictionary() { }
    
    public FastSpriteDictionary(IEnumerable<KeyValuePair<int, Sprite>> source)
    {
        foreach (var kvp in source)
        {
            this[kvp.Key] = kvp.Value;
        }
    }

    public void Clear()
    {
        Array.Fill(_fastArray, null);
        _dictionary.Clear();
    }
    
    // IDictionary インターフェース実装
    public IEnumerator<KeyValuePair<int, Sprite>> GetEnumerator()
    {
        for (int i = 0; i < FastArraySize; i++)
            if (_fastArray[i] != null)
                yield return new KeyValuePair<int, Sprite>(i, _fastArray[i]!);
        foreach (var kvp in _dictionary)
            yield return kvp;
    }
    
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    
    public void Add(KeyValuePair<int, Sprite> item) => Add(item.Key, item.Value);
    public bool Contains(KeyValuePair<int, Sprite> item) => ContainsKey(item.Key);
    public void CopyTo(KeyValuePair<int, Sprite>[] array, int arrayIndex) => throw new NotSupportedException();
    public bool Remove(KeyValuePair<int, Sprite> item) => Remove(item.Key);
}

/// <summary>
/// FastSpriteDictionary の JSON コンバーター
/// </summary>
public class FastSpriteDictionaryJsonConverter : JsonConverter<FastSpriteDictionary>
{
    public override FastSpriteDictionary? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var dict = JsonSerializer.Deserialize<Dictionary<int, Sprite>>(ref reader, options);
        var result = new FastSpriteDictionary();
        if (dict != null)
        {
            foreach (var kvp in dict)
            {
                result[kvp.Key] = kvp.Value;
            }
        }
        return result;
    }

    public override void Write(Utf8JsonWriter writer, FastSpriteDictionary value, JsonSerializerOptions options)
    {
        var dict = new Dictionary<int, Sprite>();
        foreach (var kvp in value)
        {
            dict[kvp.Key] = kvp.Value;
        }
        JsonSerializer.Serialize(writer, dict, options);
    }
}
