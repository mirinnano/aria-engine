namespace AriaEngine.Core;

/// <summary>
/// 名前空間管理
/// </summary>
public class NamespaceManager
{
    private readonly Stack<string> _namespaceStack = new();
    private readonly HashSet<string> _imports = new(StringComparer.OrdinalIgnoreCase);
    
    /// <summary>
    /// 現在の名前空間（階層をドットで結合）
    /// </summary>
    public string? CurrentNamespace => _namespaceStack.Count > 0 ? string.Join("_", _namespaceStack.Reverse()) : null;
    
    /// <summary>
    /// 名前空間に入る
    /// </summary>
    public void Push(string name)
    {
        _namespaceStack.Push(name);
    }
    
    /// <summary>
    /// 名前空間から出る
    /// </summary>
    public void Pop()
    {
        if (_namespaceStack.Count > 0)
            _namespaceStack.Pop();
    }
    
    /// <summary>
    /// using で名前空間をインポート
    /// </summary>
    public void Import(string name)
    {
        _imports.Add(name);
    }
    
    /// <summary>
    /// 修飾名を生成
    /// </summary>
    public string Qualify(string name)
    {
        if (_namespaceStack.Count == 0) return name;
        return CurrentNamespace + "_" + name;
    }
    
    /// <summary>
    /// 名前解決（usingも考慮）
    /// </summary>
    public string Resolve(string name)
    {
        // 既に修飾名の場合はそのまま
        if (name.Contains('_')) return name;
        
        // 現在の名前空間で修飾
        if (CurrentNamespace != null)
            return Qualify(name);
        
        return name;
    }
    
    /// <summary>
    /// スタックをクリア
    /// </summary>
    public void Clear()
    {
        _namespaceStack.Clear();
        _imports.Clear();
    }
}
