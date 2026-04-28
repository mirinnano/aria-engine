namespace AriaEngine.Core;

/// <summary>
/// 関数テーブル管理
/// </summary>
public class FunctionTable
{
    private readonly Dictionary<string, FunctionInfo> _functions = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, EnumDefinition> _enums = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 関数を登録
    /// </summary>
    public void Register(FunctionInfo info)
    {
        _functions[info.QualifiedName] = info;
    }

    /// <summary>
    /// 列挙型を登録
    /// </summary>
    public void RegisterEnum(EnumDefinition enumDef)
    {
        _enums[enumDef.Name] = enumDef;
    }

    /// <summary>
    /// 修飾名で関数を取得
    /// </summary>
    public FunctionInfo? GetFunction(string qualifiedName)
    {
        _functions.TryGetValue(qualifiedName, out var info);
        return info;
    }

    /// <summary>
    /// 名前空間付きで関数を検索
    /// </summary>
    public FunctionInfo? FindFunction(string name, string? currentNamespace = null)
    {
        // 修飾名で直接検索
        if (_functions.TryGetValue(name, out var info))
            return info;

        // 現在の名前空間で検索
        if (!string.IsNullOrEmpty(currentNamespace))
        {
            string qualified = currentNamespace + "_" + name;
            if (_functions.TryGetValue(qualified, out info))
                return info;
        }

        return null;
    }

    /// <summary>
    /// 列挙型を取得
    /// </summary>
    public EnumDefinition? GetEnum(string name)
    {
        _enums.TryGetValue(name, out var def);
        return def;
    }

    /// <summary>
    /// 全関数を取得
    /// </summary>
    public IReadOnlyDictionary<string, FunctionInfo> All => _functions;

    /// <summary>
    /// 全列挙型を取得
    /// </summary>
    public IReadOnlyDictionary<string, EnumDefinition> Enums => _enums;
}
