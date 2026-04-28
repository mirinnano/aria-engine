namespace AriaEngine.Core;

/// <summary>
/// Aria言語バージョン管理
/// </summary>
public class VersionManager
{
    /// <summary>現在のエンジンバージョン</summary>
    public static readonly Version EngineVersion = new(2, 0);
    
    /// <summary>スクリプトで指定されたバージョン（null=未指定）</summary>
    public Version? ScriptVersion { get; private set; }
    
    /// <summary>機能フラグ</summary>
    public FeatureFlags Features { get; private set; } = FeatureFlags.Default;
    
    /// <summary>
    /// スクリプトのバージョンヘッダーをパース
    /// </summary>
    public void ParseVersionHeader(string[] lines)
    {
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (!trimmed.StartsWith("#", StringComparison.Ordinal)) continue;
            
            var match = System.Text.RegularExpressions.Regex.Match(
                trimmed, 
                @"#\s*aria-version:\s*(\d+)\.(\d+)",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            
            if (match.Success)
            {
                int major = int.Parse(match.Groups[1].Value);
                int minor = int.Parse(match.Groups[2].Value);
                ScriptVersion = new Version(major, minor);
                UpdateFeatures();
                return;
            }
        }
        
        // バージョン未指定の場合は1.0として扱う（旧互換モード）
        ScriptVersion = new Version(1, 0);
        UpdateFeatures();
    }
    
    private void UpdateFeatures()
    {
        if (ScriptVersion == null)
        {
            Features = FeatureFlags.None;
            return;
        }
        
        Features = new FeatureFlags
        {
            FuncSyntax = ScriptVersion >= new Version(2, 0),
            StructSyntax = ScriptVersion >= new Version(2, 0),
            NamespaceSyntax = ScriptVersion >= new Version(2, 0),
            TextEffects = ScriptVersion >= new Version(2, 0),
            AutoVariables = ScriptVersion >= new Version(2, 1),
            LambdaSyntax = ScriptVersion >= new Version(2, 1),
            ModernBlocks = ScriptVersion >= new Version(1, 5)
        };
    }
    
    /// <summary>
    /// 指定機能が利用可能か
    /// </summary>
    public bool IsFeatureAvailable(Func<FeatureFlags, bool> featureCheck)
    {
        return featureCheck(Features);
    }
}

/// <summary>
/// 機能フラグ群
/// </summary>
public class FeatureFlags
{
    public bool FuncSyntax { get; set; }
    public bool StructSyntax { get; set; }
    public bool NamespaceSyntax { get; set; }
    public bool TextEffects { get; set; }
    public bool AutoVariables { get; set; }
    public bool LambdaSyntax { get; set; }
    public bool ModernBlocks { get; set; }
    
    public static FeatureFlags Default => new()
    {
        FuncSyntax = true,
        StructSyntax = true,
        NamespaceSyntax = true,
        TextEffects = true,
        AutoVariables = true,
        LambdaSyntax = true,
        ModernBlocks = true
    };
    
    public static FeatureFlags None => new();
}

/// <summary>
/// 簡易バージョン型
/// </summary>
public readonly struct Version : IComparable<Version>
{
    public int Major { get; }
    public int Minor { get; }
    
    public Version(int major, int minor)
    {
        Major = major;
        Minor = minor;
    }
    
    public int CompareTo(Version other)
    {
        int majorComparison = Major.CompareTo(other.Major);
        return majorComparison != 0 ? majorComparison : Minor.CompareTo(other.Minor);
    }
    
    public static bool operator >=(Version left, Version right) => left.CompareTo(right) >= 0;
    public static bool operator <=(Version left, Version right) => left.CompareTo(right) <= 0;
    public static bool operator >(Version left, Version right) => left.CompareTo(right) > 0;
    public static bool operator <(Version left, Version right) => left.CompareTo(right) < 0;
    
    public override string ToString() => $"{Major}.{Minor}";
}
