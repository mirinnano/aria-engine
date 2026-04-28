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
            
            // gosub の使用を検出（func呼び出し構文を推奨）
            if (features.FuncSyntax && System.Text.RegularExpressions.Regex.IsMatch(
                trimmed, @"^gosub\s+\*\w+", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            {
                _reporter.Report(new AriaError(
                    "旧構文 'gosub' が検出されました。'call name(args)' または 'name(args)' を推奨します。",
                    lineNum,
                    scriptFile,
                    AriaErrorLevel.Warning,
                    "ARIA_CHECK_GOSUB",
                    hint: "関数呼び出し構文は可読性が高く、型チェックも可能になります。"));
            }

            // 1行if構文の検出（ブロックifを推奨）
            // { を含む行はブロックifなので除外
            if (!trimmed.Contains("{") && System.Text.RegularExpressions.Regex.IsMatch(
                trimmed, @"^if\s+.+\s+(goto|jmp|gosub)\s+\*", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            {
                _reporter.Report(new AriaError(
                    "旧構文 'if cond goto *label' が検出されました。ブロックifを推奨します。",
                    lineNum,
                    scriptFile,
                    AriaErrorLevel.Warning,
                    "ARIA_CHECK_ONE_LINE_IF",
                    hint: "if %0 == 1\n    goto *label\nendif"));
            }

            // cmp + beq/bne の使用を検出（直接比較を推奨）
            if (trimmed.StartsWith("cmp ", StringComparison.OrdinalIgnoreCase))
            {
                _reporter.Report(new AriaError(
                    "旧構文 'cmp' が検出されました。if文で直接比較を推奨します。",
                    lineNum,
                    scriptFile,
                    AriaErrorLevel.Warning,
                    "ARIA_CHECK_CMP",
                    hint: "if %0 == %1 ... のように直接比較できます。"));
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
    /// readonly変数への再代入を検出
    /// </summary>
    public void CheckReadonlyReassignment(ParseResult result, string scriptFile)
    {
        if (result.ReadonlyDeclarations == null || result.ReadonlyDeclarations.Count == 0) return;

        // Build function scope map: instruction index -> function name (null = global)
        var funcRanges = result.Functions
            .Where(f => result.Labels.ContainsKey(f.QualifiedName))
            .Select(f => (Name: f.QualifiedName, Start: result.Labels[f.QualifiedName]))
            .OrderBy(f => f.Start)
            .ToList();

        string? GetFunctionForIndex(int idx)
        {
            string? currentFunc = null;
            foreach (var range in funcRanges)
            {
                if (idx >= range.Start)
                    currentFunc = range.Name;
                else
                    break;
            }
            return currentFunc;
        }

        // Opcodes that mutate their first argument
        static bool IsMutatingOp(OpCode op) => op switch
        {
            OpCode.Let or OpCode.Mov or OpCode.Add or OpCode.Sub or
            OpCode.Mul or OpCode.Div or OpCode.Mod or OpCode.SetArray or
            OpCode.Inc or OpCode.Dec => true,
            _ => false
        };

        foreach (var (declIndex, varName) in result.ReadonlyDeclarations)
        {
            if (declIndex < 0 || declIndex >= result.Instructions.Count) continue;
            string? declFunc = GetFunctionForIndex(declIndex);

            for (int i = declIndex + 1; i < result.Instructions.Count; i++)
            {
                string? instFunc = GetFunctionForIndex(i);
                if (instFunc != declFunc) continue; // Different scope, skip

                var inst = result.Instructions[i];
                if (IsMutatingOp(inst.Op) &&
                    inst.Arguments.Count > 0 &&
                    string.Equals(inst.Arguments[0], varName, StringComparison.OrdinalIgnoreCase))
                {
                    _reporter.Report(new AriaError(
                        $"readonly変数{varName}に再代入できません",
                        inst.SourceLine,
                        scriptFile,
                        AriaErrorLevel.Error,
                        "ARIA_CHECK_READONLY_REASSIGN"));
                }
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
