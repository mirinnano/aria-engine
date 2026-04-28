namespace AriaEngine.Core;

/// <summary>
/// 構造体フィールド定義
/// </summary>
public class StructField
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public int Offset { get; set; }
    public int Size { get; set; }
}

/// <summary>
/// 構造体定義
/// </summary>
public class StructDefinition
{
    /// <summary>修飾済み名（"Game.UI_Button"）</summary>
    public string QualifiedName { get; set; } = "";
    
    /// <summary>短い名前（"Button"）</summary>
    public string ShortName { get; set; } = "";
    
    /// <summary>名前空間</summary>
    public string? Namespace { get; set; }
    
    /// <summary>フィールド定義</summary>
    public List<StructField> Fields { get; set; } = new();
    
    /// <summary>構造体全体のバイトサイズ</summary>
    public int TotalSize => Fields.Count > 0 ? Fields[^1].Offset + Fields[^1].Size : 0;
    
    /// <summary>
    /// フィールド名からオフセットを取得
    /// </summary>
    public StructField? GetField(string name)
    {
        foreach (var field in Fields)
        {
            if (field.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                return field;
        }
        return null;
    }
    
    /// <summary>
    /// フィールド名からオフセットを取得（バイト単位）
    /// </summary>
    public int GetFieldOffset(string name)
    {
        var field = GetField(name);
        return field?.Offset ?? -1;
    }
}
