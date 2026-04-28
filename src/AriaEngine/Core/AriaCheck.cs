namespace AriaEngine.Core;

/// <summary>
/// Ariaスクリプト診断ツール
/// 旧構文の検出・推奨構文の提案を行う
/// </summary>
public class AriaCheck
{
    private readonly ErrorReporter _reporter;
    private readonly VersionManager _versionManager;
    
    public AriaCheck(ErrorReporter reporter, VersionManager versionManager)
    {
        _reporter = reporter;
        _versionManager = versionManager;
    }
    
    /// <summary>
    /// スクリプトを診断して警告を報告
    /// </summary>
    public void CheckScript(string[] lines, string scriptFile)
    {
        _versionManager.ParseVersionHeader(lines);
        var features = _versionManager.Features;
        
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            int lineNum = i + 1;
            var trimmed = line.Trim();
            
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#")) continue;
            
            // defsub の使用を検出（func を推奨）
            if (features.FuncSyntax && trimmed.StartsWith("defsub ", StringComparison.OrdinalIgnoreCase))
            {
                _reporter.Report(new AriaError(
                    "旧構文 'defsub' が検出されました。'func name(args) -> type ... endfunc' を推奨します。",
                    lineNum,
                    scriptFile,
                    AriaErrorLevel.Warning,
                    "ARIA_CHECK_DEFSUB",
                    hint: "func 構文は型安全性と可読性が向上します。"));
            }
            
            // getparam の使用を検出（func の自動引数展開を推奨）
            if (features.FuncSyntax && trimmed.StartsWith("getparam", StringComparison.OrdinalIgnoreCase))
            {
                _reporter.Report(new AriaError(
                    "旧構文 'getparam' が検出されました。func 宣言内では自動的に引数が展開されます。",
                    lineNum,
                    scriptFile,
                    AriaErrorLevel.Warning,
                    "ARIA_CHECK_GETPARAM"));
            }
            
            // gosub 引数付きの使用を検出（func呼び出し構文を推奨）
            if (features.FuncSyntax && System.Text.RegularExpressions.Regex.IsMatch(
                trimmed, @"^gosub\s+\*\w+\s*,", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            {
                _reporter.Report(new AriaError(
                    "旧構文 'gosub *name, args' が検出されました。'name(args)' を推奨します。",
                    lineNum,
                    scriptFile,
                    AriaErrorLevel.Warning,
                    "ARIA_CHECK_GOSUB_ARGS",
                    hint: "関数呼び出し構文は可読性が高く、型チェックも可能になります。"));
            }
            
            // let %var = ... の検出（auto/var を推奨、将来のバージョンで）
            if (features.AutoVariables && System.Text.RegularExpressions.Regex.IsMatch(
                trimmed, @"^let\s+%\w+\s*=", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            {
                    _reporter.Report(new AriaError(
                    "明示的な型宣言が可能です。将来のバージョンでは 'auto %var = ...' を推奨します。",
                    lineNum,
                    scriptFile,
                    AriaErrorLevel.Warning,
                    "ARIA_CHECK_LET"));
            }
            
            // 未使用の namespace 閉じをチェック
            if (trimmed == "}" && !features.NamespaceSyntax)
            {
                _reporter.Report(new AriaError(
                    "namespace 構文が使用されていますが、スクリプトバージョンが古いため無視されます。",
                    lineNum,
                    scriptFile,
                    AriaErrorLevel.Warning,
                    "ARIA_CHECK_NAMESPACE_VERSION",
                    hint: "スクリプト先頭に '# aria-version: 2.0' を追加してください。"));
            }
        }
    }
    
    /// <summary>
    /// バージョン互換性の警告
    /// </summary>
    public void CheckCompatibility()
    {
        if (_versionManager.ScriptVersion == null) return;
        
        if (_versionManager.ScriptVersion < new Version(2, 0))
        {
                _reporter.Report(new AriaError(
                $"スクリプトバージョン {_versionManager.ScriptVersion} はレガシーモードです。新機能を使用するには '# aria-version: 2.0' を追加してください。",
                -1,
                "",
                AriaErrorLevel.Warning,
                "ARIA_CHECK_LEGACY_MODE"));
        }
    }
}
