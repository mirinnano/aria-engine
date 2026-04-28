namespace AriaEngine.Core;

/// <summary>
/// 関数定義情報
/// </summary>
public class FunctionInfo
{
    /// <summary>関数名（修飾済み: "Game.UI_show_message"）</summary>
    public string QualifiedName { get; set; } = "";
    
    /// <summary>短い名前（"show_message"）</summary>
    public string ShortName { get; set; } = "";
    
    /// <summary>名前空間（"Game.UI"）</summary>
    public string? Namespace { get; set; }
    
    /// <summary>エントリーポイント（ラベルPC）</summary>
    public int EntryPC { get; set; }
    
    /// <summary>引数リスト</summary>
    public List<ParameterInfo> Parameters { get; set; } = new();
    
    /// <summary>戻り値型</summary>
    public string ReturnType { get; set; } = "void";
    
    /// <summary>ローカル変数数</summary>
    public int LocalCount { get; set; }
}

/// <summary>
/// 関数パラメータ情報
/// </summary>
public class ParameterInfo
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public int Index { get; set; }
    public bool IsRef { get; set; }
}
