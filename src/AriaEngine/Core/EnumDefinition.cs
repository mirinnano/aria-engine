namespace AriaEngine.Core;

/// <summary>
/// 列挙型定義
/// </summary>
public class EnumDefinition
{
    /// <summary>列挙型名（"Route"）</summary>
    public string Name { get; set; } = "";

    /// <summary>名前空間（"Game"）</summary>
    public string? Namespace { get; set; }

    /// <summary>メンバー定義（名前 -> 値）</summary>
    public Dictionary<string, int> Members { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 指定した値が有効な列挙型値かどうか
    /// </summary>
    public bool IsValidValue(int value)
    {
        foreach (var memberValue in Members.Values)
        {
            if (memberValue == value) return true;
        }
        return false;
    }
}
