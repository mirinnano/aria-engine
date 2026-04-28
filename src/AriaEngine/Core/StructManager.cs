namespace AriaEngine.Core;

/// <summary>
/// 構造体管理
/// </summary>
public class StructManager
{
    private readonly Dictionary<string, StructDefinition> _definitions = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, StructInstance> _instances = new(StringComparer.OrdinalIgnoreCase);
    
    /// <summary>
    /// 構造体定義を登録
    /// </summary>
    public void RegisterDefinition(StructDefinition definition)
    {
        _definitions[definition.QualifiedName] = definition;
    }
    
    /// <summary>
    /// 構造体定義を取得
    /// </summary>
    public StructDefinition? GetDefinition(string name)
    {
        _definitions.TryGetValue(name, out var def);
        return def;
    }
    
    /// <summary>
    /// 名前空間付きで構造体定義を検索
    /// </summary>
    public StructDefinition? FindDefinition(string name, string? currentNamespace = null)
    {
        if (_definitions.TryGetValue(name, out var def))
            return def;
        
        if (!string.IsNullOrEmpty(currentNamespace))
        {
            string qualified = currentNamespace + "_" + name;
            if (_definitions.TryGetValue(qualified, out def))
                return def;
        }
        
        return null;
    }
    
    /// <summary>
    /// 構造体インスタンスを作成
    /// </summary>
    public StructInstance? CreateInstance(string definitionName, string instanceName)
    {
        var def = GetDefinition(definitionName);
        if (def == null) return null;
        
        var instance = new StructInstance(def);
        _instances[instanceName] = instance;
        return instance;
    }
    
    /// <summary>
    /// インスタンスを取得
    /// </summary>
    public StructInstance? GetInstance(string name)
    {
        _instances.TryGetValue(name, out var instance);
        return instance;
    }
    
    /// <summary>
    /// フィールド名からレジスタ名を生成（軽量版展開用）
    /// </summary>
    public string GetFieldRegisterName(string instanceName, string fieldName)
    {
        return $"{instanceName}_{fieldName}";
    }
}
