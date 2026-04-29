using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using AriaEngine.Text;

namespace AriaEngine.Core;

    public class Parser
    {
        private readonly ErrorReporter _reporter;
        private static readonly System.Text.RegularExpressions.Regex DialogRegex = new System.Text.RegularExpressions.Regex(@"^([^「]*)「(.*)」(\\?|@?)$", System.Text.RegularExpressions.RegexOptions.Compiled);

        // Helper for match/case blocks
        private class MatchCase
        {
            public string? Key;
            public bool IsWildcard;
            public bool IsDefault;
            public string? Guard;
            public List<string> Body = new List<string>();
        }

    public Parser(ErrorReporter reporter)
    {
        _reporter = reporter;
    }


    public ParseResult Parse(string[] lines, string scriptFile = "")
    {
        var result = new ParseResult { SourceLines = lines };
        var (preprocessedLines, functions, structs, enums) = PreprocessModernSyntax(lines, scriptFile);
        // Post-process T11 transpilation in a dedicated pass to ensure all patterns are emitted
        preprocessedLines = ApplyPostT11Transpilations(preprocessedLines);
        result.Functions = functions;
        result.Structs = structs;
        result.Enums = enums;

        var instructions = new List<Instruction>();
        var labels = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var defsubs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        
        int ifCounter = 0;
        var ifStack = new Stack<(string elseLabel, string endLabel, Condition cond)>();
        int loopCounter = 0;
        var loopStack = new Stack<(string startLabel, string endLabel)>();

        // Pre-pass for Defsubs, Labels, and Functions
        // funcで展開されたdefsubもここで登録
        foreach (var func in functions)
        {
            defsubs.Add(func.QualifiedName);
        }

        for (int i = 0; i < preprocessedLines.Length; i++)
        {
            var rawLine = preprocessedLines[i].Text;
            int sourceLine = preprocessedLines[i].SourceLine;
            var line = StripComments(rawLine).TrimStart();
            if (string.IsNullOrEmpty(line)) continue;

            // Step: support generic Let parsing for lines like `let x, y` producing OpCode.Let
            if (Regex.IsMatch(line, @"^\s*let\s+", RegexOptions.IgnoreCase))
            {
                var mLet = Regex.Match(line, @"^\s*let\s+([%$]?[A-Za-z_][A-Za-z0-9_]*)\s*,\s*(.+)$", RegexOptions.IgnoreCase);
                if (mLet.Success)
                {
                    string dest = mLet.Groups[1].Value;
                    string val = mLet.Groups[2].Value;
                    instructions.Add(new Instruction(OpCode.Let, new List<string> { dest, val }, sourceLine));
                    continue;
                }
            }

            if (line.StartsWith("*"))
            {
                var labelName = line.Substring(1).Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)[0];
                labels[labelName] = -1; // Temp placeholder
            }
            else if (line.StartsWith("defsub ", StringComparison.OrdinalIgnoreCase))
            {
                var parts = Tokenize(line);
                if (parts.Count > 1) defsubs.Add(parts[1]);
            }
        }

        for (int i = 0; i < preprocessedLines.Length; i++)
        {
            var rawLine = preprocessedLines[i].Text;
            int sourceLine = preprocessedLines[i].SourceLine;
            var line = StripComments(rawLine).TrimStart();

            if (string.IsNullOrEmpty(line)) continue;

            

            // T13: owned sprite declarations
            // owned sprite <var>
            if (Regex.IsMatch(line, @"^owned\s+sprite\s+", RegexOptions.IgnoreCase))
            {
                var mOwned = Regex.Match(line, @"^owned\s+sprite\s+([%$]?[A-Za-z_][A-Za-z0-9_]*)\s*$", RegexOptions.IgnoreCase);
                if (mOwned.Success)
                {
                    string ownedVar = mOwned.Groups[1].Value;
                    result.OwnedSprites.Add(ownedVar);
                    continue;
                }
            }

            // Special handling: explicit storage declarations at line start
            // local/global/persistent/save/volatile/readonly/mut <var> = <value>
            if (Regex.IsMatch(line, @"^(local|global|persistent|save|volatile|readonly|mut)\s+", RegexOptions.IgnoreCase))
            {
                var m = Regex.Match(line, @"^(local|global|persistent|save|volatile|readonly|mut)\s+([%$]?[A-Za-z_][A-Za-z0-9_]*)\s*=\s*(.*)$", RegexOptions.IgnoreCase);
                if (m.Success)
                {
                    string scopeWord = m.Groups[1].Value.ToLowerInvariant();
                    string dest = m.Groups[2].Value.Trim();
                    string valuePart = m.Groups[3].Value.Trim();
                    // Build a simple let instruction: let <dest>, <value>
                    var opArgs = new List<string> { dest, valuePart };
                    // scope mapping (readonly/mut default to Local scope)
                    var scope = scopeWord switch
                    {
                        "global" => AriaEngine.Core.StorageScope.Global,
                        "persistent" => AriaEngine.Core.StorageScope.Persistent,
                        "save" => AriaEngine.Core.StorageScope.Save,
                        "volatile" => AriaEngine.Core.StorageScope.Volatile,
                        _ => AriaEngine.Core.StorageScope.Local
                    };
                    // Record declaration
                    if (result.DeclaredVariables == null) result.DeclaredVariables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    result.DeclaredVariables[dest] = scopeWord;
                    // Emit a Let instruction with explicit scope
                    int declIndex = instructions.Count;
                    instructions.Add(new Instruction(OpCode.Let, opArgs, sourceLine, default, scope));
                    if (scopeWord == "readonly")
                        result.ReadonlyDeclarations.Add((declIndex, dest));
                    continue;
                }
            }
            if (ShouldTreatAsPlainText(line, defsubs))
            {
                AddTextInstructions(instructions, line, sourceLine);
                continue;
            }

            // Handle labels (multi-commands on same line via ":" is supported by NScripter, but we parse strictly. For ARIA we split by ":" if not in quotes)
            var statements = SplitStatements(line);
            foreach (var stmt in statements)
            {
                if (string.IsNullOrEmpty(stmt)) continue;

                if (stmt.StartsWith("*"))
                {
                    var labelName = stmt.Substring(1).Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)[0];
                    labels[labelName] = instructions.Count;
                    continue;
                }

                var parts = Tokenize(stmt);
                if (parts.Count == 0) continue;

                string firstToken = parts[0];

                if (firstToken.Equals("if", StringComparison.OrdinalIgnoreCase))
                {
                    int openBrace = parts.IndexOf("{");
                    int closeBrace = parts.LastIndexOf("}");
                    bool isOneLineBlock = openBrace > 0 && closeBrace > openBrace;

                    if (isOneLineBlock)
                    {
                        // 1行ifブロック: if cond { command args }
                        // 前処理を通らずに波括弧が残っているケース（フォールバック）
                        var condTokens = parts.GetRange(1, openBrace - 1);
                        var condition = Condition.FromTokens(condTokens);

                        var blockTokens = parts.GetRange(openBrace + 1, closeBrace - openBrace - 1);
                        if (blockTokens.Count > 0)
                        {
                            var cmdToken = blockTokens[0];
                            var opArgs = blockTokens.Skip(1).ToList();

                            // 関数呼び出しの空括弧 () をスキップ
                            if (opArgs.Count >= 2 && opArgs[0] == "(" && opArgs[1] == ")")
                                opArgs = opArgs.Skip(2).ToList();

                            if (CommandRegistry.TryGet(cmdToken, out OpCode op))
                                instructions.Add(new Instruction(op, opArgs, sourceLine, condition));
                            else if (defsubs.Contains(cmdToken))
                                instructions.Add(new Instruction(OpCode.Gosub, new List<string> { cmdToken }.Concat(opArgs).ToList(), sourceLine, condition));
                            else if (labels.ContainsKey(cmdToken))
                                instructions.Add(new Instruction(OpCode.Gosub, new List<string> { cmdToken }.Concat(opArgs).ToList(), sourceLine, condition));
                            else
                            {
                                _reporter.Report(new AriaError(
                                    $"1行ifブロック内に有効なコマンドが見つかりません: '{cmdToken}'",
                                    sourceLine, scriptFile, AriaErrorLevel.Error));
                            }
                        }
                        else
                        {
                            _reporter.Report(new AriaError(
                                "1行ifブロックが空です。",
                                sourceLine, scriptFile, AriaErrorLevel.Error));
                        }
                    }
                    else
                    {
                        // 波括弧がない → インラインif（if cond command）またはブロックif（if cond ... endif）
                        int cmdIndex = -1;
                        for (int j = 1; j < parts.Count; j++)
                        {
                            if (CommandRegistry.Contains(parts[j]) || defsubs.Contains(parts[j]) || labels.ContainsKey(parts[j]))
                            {
                                cmdIndex = j;
                                break;
                            }
                        }

                        if (cmdIndex > 1)
                        {
                            // インラインif: if cond command args
                            var condTokens = parts.GetRange(1, cmdIndex - 1);
                            var condition = Condition.FromTokens(condTokens);
                            var cmdToken = parts[cmdIndex];
                            var opArgs = parts.Skip(cmdIndex + 1).ToList();

                            // 関数呼び出しの空括弧 () をスキップ
                            if (opArgs.Count >= 2 && opArgs[0] == "(" && opArgs[1] == ")")
                                opArgs = opArgs.Skip(2).ToList();

                            if (CommandRegistry.TryGet(cmdToken, out OpCode op))
                                instructions.Add(new Instruction(op, opArgs, sourceLine, condition));
                            else if (defsubs.Contains(cmdToken) || labels.ContainsKey(cmdToken))
                                instructions.Add(new Instruction(OpCode.Gosub, new List<string> { cmdToken }.Concat(opArgs).ToList(), sourceLine, condition));
                        }
                        else
                        {
                            // ブロックif文: if cond ... [else ...] endif
                            var condTokens = parts.Skip(1).ToList();
                            var condition = Condition.FromTokens(condTokens);
                            string elseLbl = $"__if_else_{ifCounter}";
                            string endLbl = $"__if_end_{ifCounter}";
                            ifCounter++;

                            instructions.Add(new Instruction(OpCode.JumpIfFalse, new List<string> { elseLbl }, sourceLine, condition));
                            ifStack.Push((elseLbl, endLbl, condition));
                        }
                    }
                    continue;
                }
                else if (firstToken.Equals("else", StringComparison.OrdinalIgnoreCase))
                {
                    if (ifStack.Count > 0)
                    {
                        var popped = ifStack.Pop();
                        instructions.Add(new Instruction(OpCode.Jmp, new List<string> { popped.endLabel }, sourceLine));
                        labels[popped.elseLabel] = instructions.Count;
                        ifStack.Push(("", popped.endLabel, default));
                    }
                    continue;
                }
                else if (firstToken.Equals("endif", StringComparison.OrdinalIgnoreCase))
                {
                    if (ifStack.Count > 0)
                    {
                        var popped = ifStack.Pop();
                        if (!string.IsNullOrEmpty(popped.elseLabel)) labels[popped.elseLabel] = instructions.Count;
                        labels[popped.endLabel] = instructions.Count;
                    }
                    continue;
                }
                else if (firstToken.Equals("while", StringComparison.OrdinalIgnoreCase))
                {
                    // ブロックwhile: while cond ... wend
                    // while cond { ... } は前処理で while cond ... wend に変換済み
                    loopCounter++;
                    string startLbl = $"__while_start_{loopCounter}";
                    string endLbl = $"__while_end_{loopCounter}";
                    labels[startLbl] = instructions.Count;
                    var condTokens = parts.Skip(1).ToList();
                    var condition = Condition.FromTokens(condTokens);
                    instructions.Add(new Instruction(OpCode.JumpIfFalse, new List<string> { endLbl }, sourceLine, condition));
                    loopStack.Push((startLbl, endLbl));
                    continue;
                }
                else if (firstToken.Equals("wend", StringComparison.OrdinalIgnoreCase))
                {
                    if (loopStack.Count > 0)
                    {
                        var loop = loopStack.Pop();
                        instructions.Add(new Instruction(OpCode.Jmp, new List<string> { loop.startLabel }, sourceLine));
                        labels[loop.endLabel] = instructions.Count;
                    }
                    continue;
                }
                else if (firstToken.Equals("break", StringComparison.OrdinalIgnoreCase))
                {
                    if (loopStack.Count > 0)
                    {
                        instructions.Add(new Instruction(OpCode.Jmp, new List<string> { loopStack.Peek().endLabel }, sourceLine));
                    }
                    else
                    {
                        _reporter.Report(new AriaError(
                            "'break' はループ外で使用できません。",
                            sourceLine, scriptFile, AriaErrorLevel.Error));
                    }
                    continue;
                }
                else if (firstToken.Equals("continue", StringComparison.OrdinalIgnoreCase))
                {
                    if (loopStack.Count > 0)
                    {
                        instructions.Add(new Instruction(OpCode.Jmp, new List<string> { loopStack.Peek().startLabel }, sourceLine));
                    }
                    else
                    {
                        _reporter.Report(new AriaError(
                            "'continue' はループ外で使用できません。",
                            sourceLine, scriptFile, AriaErrorLevel.Error));
                    }
                    continue;
                }
                else if (firstToken.Equals("return", StringComparison.OrdinalIgnoreCase) && parts.Count > 1)
                {
                    // return value → returnvalue opcode
                    var args = parts.Skip(1).ToList();
                    instructions.Add(new Instruction(OpCode.ReturnValue, args, sourceLine));
                    continue;
                }

                if (CommandRegistry.TryGet(firstToken, out OpCode statementOp))
                {
                    var args = parts.Skip(1).ToList();
                    instructions.Add(new Instruction(statementOp, args, sourceLine));
                }
                else if (defsubs.Contains(firstToken))
                {
                    instructions.Add(new Instruction(OpCode.Gosub, new List<string> { firstToken }.Concat(parts.Skip(1)).ToList(), sourceLine));
                }
                else
                {
                    AddTextInstructions(instructions, stmt, sourceLine);
                }
            }
        }

        // Validate if/else/endif block consistency
        if (ifStack.Count > 0)
        {
            while (ifStack.Count > 0)
            {
                var unmatched = ifStack.Pop();
                _reporter.Report(new AriaError(
                    "閉じられていないifブロックがあります。endifが不足しています。",
                    0, scriptFile, AriaErrorLevel.Error));
            }
        }

        while (loopStack.Count > 0)
        {
            var unmatched = loopStack.Pop();
            labels[unmatched.endLabel] = instructions.Count;
            _reporter.Report(new AriaError(
                "閉じられていないwhileブロックがあります。wendが不足しています。",
                0, scriptFile, AriaErrorLevel.Error));
        }

        foreach (var inst in instructions)
        {
            if (inst.Op == OpCode.Jmp || inst.Op == OpCode.Beq || inst.Op == OpCode.Bne || 
                inst.Op == OpCode.Bgt || inst.Op == OpCode.Blt || inst.Op == OpCode.Gosub)
            {
                if (inst.Arguments.Count > 0 && inst.Op != OpCode.Gosub)
                {
                    string target = inst.Arguments[0].TrimStart('*');
                    if (!labels.ContainsKey(target))
                        _reporter.Report(new AriaError($"未定義のラベル '*{target}' へのジャンプです。", inst.SourceLine, scriptFile, AriaErrorLevel.Error));
                }
                else if (inst.Op == OpCode.Gosub)
                {
                    string target = inst.Arguments[0].TrimStart('*');
                    if (!labels.ContainsKey(target))
                        _reporter.Report(new AriaError($"未定義のサブルーチン/ラベル '{target}' の呼び出しです。", inst.SourceLine, scriptFile, AriaErrorLevel.Error));
                }
            }
        }

        // 関数呼び出しの型チェック（C++like）
        ValidateFunctionCalls(instructions, functions, enums, scriptFile);

        result.Instructions = instructions;
        result.Labels = labels;

        // Run static analysis: readonly reassignment check
        var ariaCheck = new AriaCheck(_reporter, new VersionManager());
        ariaCheck.CheckReadonlyReassignment(result, scriptFile);

        return result;
    }

    private bool ShouldTreatAsPlainText(string line, HashSet<string> defsubs)
    {
        var firstStatement = SplitStatements(line).FirstOrDefault();
        if (string.IsNullOrWhiteSpace(firstStatement)) return false;

        var parts = Tokenize(firstStatement);
        if (parts.Count == 0) return false;

        string firstToken = parts[0];
        return !firstToken.StartsWith("*")
            && !firstToken.Equals("if", StringComparison.OrdinalIgnoreCase)
            && !firstToken.Equals("else", StringComparison.OrdinalIgnoreCase)
            && !firstToken.Equals("endif", StringComparison.OrdinalIgnoreCase)
            && !CommandRegistry.Contains(firstToken)
            && !defsubs.Contains(firstToken);
    }

    private readonly record struct PreprocessedLine(string Text, int SourceLine);

    // Post-process helper: ensure Ok/Err/Some/None and if_err/if_none transpilation is realized on the preprocessed lines
    private PreprocessedLine[] ApplyPostT11Transpilations(PreprocessedLine[] lines)
    {
        var outList = new List<PreprocessedLine>(lines.Length * 2);
        foreach (var pl in lines)
        {
            string text = pl.Text;
            // Try to catch raw forms that may have slipped through and ensure we transpile again if needed
            // Use the same patterns as in PreprocessModernSyntax with a lightweight check
            var mOk = Regex.Match(text, @"^\s*let\s+([%$]?[A-Za-z_][A-Za-z0-9_]*)\s*,\s*Ok\(([^\)]*)\)\s*$", RegexOptions.IgnoreCase);
            if (mOk.Success)
            {
                string varName = mOk.Groups[1].Value;
                string val = mOk.Groups[2].Value;
                outList.Add(new PreprocessedLine($"let {varName}_val, {val}", pl.SourceLine));
                outList.Add(new PreprocessedLine($"let {varName}_err, 0", pl.SourceLine));
                continue;
            }
            var mErr = Regex.Match(text, @"^\s*let\s+([%$]?[A-Za-z_][A-Za-z0-9_]*)\s*,\s*Err\(([^\)]*)\)\s*$", RegexOptions.IgnoreCase);
            if (mErr.Success)
            {
                string varName = mErr.Groups[1].Value;
                string val = mErr.Groups[2].Value;
                outList.Add(new PreprocessedLine($"let {varName}_val, 0", pl.SourceLine));
                outList.Add(new PreprocessedLine($"let {varName}_err, {val}", pl.SourceLine));
                continue;
            }
            var mSome = Regex.Match(text, @"^\s*let\s+([%$]?[A-Za-z_][A-Za-z0-9_]*)\s*,\s*Some\(([^\)]*)\)\s*$", RegexOptions.IgnoreCase);
            if (mSome.Success)
            {
                string varName = mSome.Groups[1].Value;
                string val = mSome.Groups[2].Value;
                outList.Add(new PreprocessedLine($"let {varName}_val, {val}", pl.SourceLine));
                outList.Add(new PreprocessedLine($"let {varName}_has, 1", pl.SourceLine));
                continue;
            }
            var mNone = Regex.Match(text, @"^\s*let\s+([%$]?[A-Za-z_][A-Za-z0-9_]*)\s*,\s*None\s*$", RegexOptions.IgnoreCase);
            if (mNone.Success)
            {
                string varName = mNone.Groups[1].Value;
                outList.Add(new PreprocessedLine($"let {varName}_val, 0", pl.SourceLine));
                outList.Add(new PreprocessedLine($"let {varName}_has, 0", pl.SourceLine));
                continue;
            }
            // If nothing matched, keep original line
            outList.Add(pl);
        }
        return outList.ToArray();
    }

    private (PreprocessedLine[] Lines, List<FunctionInfo> Functions, List<StructDefinition> Structs, List<EnumDefinition> Enums) PreprocessModernSyntax(string[] lines, string scriptFile = "")
    {
        var importedModules = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        return PreprocessModernSyntaxCore(lines, scriptFile, importedModules);
    }

    private (PreprocessedLine[] Lines, List<FunctionInfo> Functions, List<StructDefinition> Structs, List<EnumDefinition> Enums) PreprocessModernSyntaxCore(string[] lines, string scriptFile, HashSet<string> importedModules)
    {
        var output = new List<PreprocessedLine>(lines.Length);
        var functions = new List<FunctionInfo>();
        var structs = new List<StructDefinition>();
        var enums = new List<EnumDefinition>();
        var enumRegistry = new Dictionary<string, EnumDefinition>(StringComparer.OrdinalIgnoreCase);
        var globalConstants = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var funcLocalConstants = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var constants = globalConstants; // active dictionary for replacement
        var namespaceStack = new Stack<string>();
        var blockStack = new Stack<string>();
        var structRegistry = new Dictionary<string, StructDefinition>(StringComparer.OrdinalIgnoreCase);
        var instanceMap = new Dictionary<string, StructDefinition>(StringComparer.OrdinalIgnoreCase);
        string? enumName = null;
        int enumNextValue = 0;
        EnumDefinition? currentEnum = null;

        // func/struct ブロック処理用
        bool inFuncBlock = false;
        FunctionInfo? currentFunc = null;
        bool inStructBlock = false;
        StructDefinition? currentStruct = null;

        // /// doc comment 収集用
        string? pendingDocComment = null;
        bool hasNonDocLineSinceComment = false;
        
        // switch ブロック処理用
        bool inSwitchBlock = false;
        string? switchExpression = null;
        int switchCounter = 0;
        var switchCaseLines = new List<(string Key, List<string> Lines)>();
        var switchDefaultLines = new List<string>();
        string? currentSwitchCase = null;
        
        // try-catch ブロック処理用
        bool inTryBlock = false;
        bool inCatchBlock = false;
        int tryCounter = 0;
        var tryBodyLines = new List<string>();
        var catchBodyLines = new List<string>();
        string? currentTryLabel = null;
        string? currentCatchLabel = null;
        string? currentTryEndLabel = null;

        // match block state (new):
        bool inMatchBlock = false;
        string? matchExpression = null;
        int matchCounter = 0;
        List<MatchCase> currentMatchCases = new();
        int matchStartSourceLine = 0;
        MatchCase? currentMatchCase = null;

        string baseDir = "";
        if (!string.IsNullOrEmpty(scriptFile))
        {
            string localPath = scriptFile.Replace('/', Path.DirectorySeparatorChar);
            baseDir = Path.GetDirectoryName(localPath) ?? "";
            baseDir = baseDir.Replace('\\', '/');
        }

        var importMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var usingNamespaces = new List<string>();

        for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            var rawLine = lines[lineIndex];
            int sourceLine = lineIndex + 1;

            var trimmed = StripComments(rawLine).Trim();

            // /// doc comment 収集: /// で始まる行はキャプチャして出力しない
            var rawTrimmed = rawLine.TrimStart();
            if (rawTrimmed.StartsWith("///"))
            {
                string docLine = rawTrimmed.Substring(3).Trim();
                if (string.IsNullOrEmpty(pendingDocComment))
                    pendingDocComment = docLine;
                else
                    pendingDocComment += "\n" + docLine;
                // doc comment lines don't break the chain if we haven't seen other code yet
                // (hasNonDocLineSinceComment stays true if it was already true)
                // /// 行は命令として出力しない
                continue;
            }
            else if (!string.IsNullOrEmpty(trimmed))
            {
                // /// 以外の意味がある行 - func/struct/endfunc/endstruct 以外ならフラグを立てる
                bool isBlockEnd = trimmed.Equals("endfunc", StringComparison.OrdinalIgnoreCase) ||
                                  trimmed.Equals("endstruct", StringComparison.OrdinalIgnoreCase);
                bool isFuncOrStruct = trimmed.StartsWith("func ", StringComparison.OrdinalIgnoreCase) ||
                                      trimmed.StartsWith("struct ", StringComparison.OrdinalIgnoreCase);
                if (!isBlockEnd && !isFuncOrStruct)
                    hasNonDocLineSinceComment = true;
            }
            if (string.IsNullOrEmpty(trimmed))
            {
                output.Add(new PreprocessedLine(rawLine, sourceLine));
                continue;
            }

            // T11: Result/Option constructors transpilation (Ok/Err/Some/None) and if_err/if_none syntax
            // 1) let %var, Ok(n)  -> let %var_val, n ; let %var_err, 0
            // 2) let %var, Err(v) -> let %var_val, 0 ; let %var_err, v
            // 3) let %var, Some(v) -> let %var_val, v ; let %var_has, 1
            // 4) let %var, None    -> let %var_val, 0 ; let %var_has, 0
            var mOk = Regex.Match(trimmed, @"^\s*let\s+([%$]?[A-Za-z_][A-Za-z0-9_]*)\s*,\s*Ok\(([^\)]*)\)\s*$", RegexOptions.IgnoreCase);
            if (mOk.Success)
            {
                string varName = mOk.Groups[1].Value;
                string val = mOk.Groups[2].Value;
                output.Add(new PreprocessedLine($"let {varName}_val, {val}", sourceLine));
                output.Add(new PreprocessedLine($"let {varName}_err, 0", sourceLine));
                continue;
            }
            var mErr = Regex.Match(trimmed, @"^\s*let\s+([%$]?[A-Za-z_][A-Za-z0-9_]*)\s*,\s*Err\(([^\)]*)\)\s*$", RegexOptions.IgnoreCase);
            if (mErr.Success)
            {
                string varName = mErr.Groups[1].Value;
                string val = mErr.Groups[2].Value;
                output.Add(new PreprocessedLine($"let {varName}_val, 0", sourceLine));
                output.Add(new PreprocessedLine($"let {varName}_err, {val}", sourceLine));
                continue;
            }
            var mSome = Regex.Match(trimmed, @"^\s*let\s+([%$]?[A-Za-z_][A-Za-z0-9_]*)\s*,\s*Some\(([^\)]*)\)\s*$", RegexOptions.IgnoreCase);
            if (mSome.Success)
            {
                string varName = mSome.Groups[1].Value;
                string val = mSome.Groups[2].Value;
                output.Add(new PreprocessedLine($"let {varName}_val, {val}", sourceLine));
                output.Add(new PreprocessedLine($"let {varName}_has, 1", sourceLine));
                continue;
            }
            var mNone = Regex.Match(trimmed, @"^\s*let\s+([%$]?[A-Za-z_][A-Za-z0-9_]*)\s*,\s*None\s*$", RegexOptions.IgnoreCase);
            if (mNone.Success)
            {
                string varName = mNone.Groups[1].Value;
                output.Add(new PreprocessedLine($"let {varName}_val, 0", sourceLine));
                output.Add(new PreprocessedLine($"let {varName}_has, 0", sourceLine));
                continue;
            }
            // 5) if_err <var> goto *label  -> if <var>_err != 0 goto *label
            var mIfErr = Regex.Match(trimmed, @"^\s*if_err\s+([%$]?[A-Za-z_][A-Za-z0-9_]*)\s+goto\s+\*(\w+)\s*$", RegexOptions.IgnoreCase);
            if (mIfErr.Success)
            {
                string varName = mIfErr.Groups[1].Value;
                string label = mIfErr.Groups[2].Value;
                output.Add(new PreprocessedLine($"if {varName}_err != 0 goto *{label}", sourceLine));
                continue;
            }
            // 6) if_none <var> goto *label  -> if <var>_has == 0 goto *label
            var mIfNone = Regex.Match(trimmed, @"^\s*if_none\s+([%$]?[A-Za-z_][A-Za-z0-9_]*)\s+goto\s+\*(\w+)\s*$", RegexOptions.IgnoreCase);

            // Fallback: handle common Ok/Err/Some/None forms even if regex misses (robust parsing)
            if (!mOk.Success && !mErr.Success && !mSome.Success && !mNone.Success)
            {
                if (trimmed.StartsWith("let ", StringComparison.OrdinalIgnoreCase))
                {
                    var rest = trimmed.Substring(4).Trim();
                    var comma = rest.IndexOf(',');
                    if (comma > -1)
                    {
                        var lhs = rest.Substring(0, comma).Trim();
                        var rhs = rest.Substring(comma + 1).Trim();
                        if (rhs.StartsWith("Ok(") && rhs.EndsWith(")"))
                        {
                            var val = rhs.Substring(3, rhs.Length - 4).Trim();
                            output.Add(new PreprocessedLine($"let {lhs}_val, {val}", sourceLine));
                            output.Add(new PreprocessedLine($"let {lhs}_err, 0", sourceLine));
                            continue;
                        }
                        if (rhs.StartsWith("Err(") && rhs.EndsWith(")"))
                        {
                            var val = rhs.Substring(4, rhs.Length - 5).Trim();
                            output.Add(new PreprocessedLine($"let {lhs}_val, 0", sourceLine));
                            output.Add(new PreprocessedLine($"let {lhs}_err, {val}", sourceLine));
                            continue;
                        }
                        if (rhs.StartsWith("Some(") && rhs.EndsWith(")"))
                        {
                            var val = rhs.Substring(5, rhs.Length - 6).Trim();
                            output.Add(new PreprocessedLine($"let {lhs}_val, {val}", sourceLine));
                            output.Add(new PreprocessedLine($"let {lhs}_has, 1", sourceLine));
                            continue;
                        }
                        if (rhs.Equals("None", StringComparison.OrdinalIgnoreCase))
                        {
                            output.Add(new PreprocessedLine($"let {lhs}_val, 0", sourceLine));
                            output.Add(new PreprocessedLine($"let {lhs}_has, 0", sourceLine));
                            continue;
                        }
                    }
                }
            }
            if (mIfNone.Success)
            {
                string varName = mIfNone.Groups[1].Value;
                string label = mIfNone.Groups[2].Value;
                output.Add(new PreprocessedLine($"if {varName}_has == 0 goto *{label}", sourceLine));
                continue;
            }

            // T14: struct instantiation syntax
            // let %var, new StructName { %field = value, ... }
            var mStructNew = Regex.Match(trimmed, @"^\s*let\s+([%$][A-Za-z_][A-Za-z0-9_]*)\s*,\s*new\s+([A-Za-z_][A-Za-z0-9_]*)\s*\{(.*)\}\s*$", RegexOptions.IgnoreCase);
            if (mStructNew.Success)
            {
                string varName = mStructNew.Groups[1].Value;
                string structName = mStructNew.Groups[2].Value;
                string fieldsRaw = mStructNew.Groups[3].Value;

                // Look up struct: try exact name, then namespace-qualified
                StructDefinition? def = null;
                if (!structRegistry.TryGetValue(structName, out def) && namespaceStack.Count > 0)
                {
                    string qualified = string.Join("_", namespaceStack.Reverse()) + "_" + structName;
                    structRegistry.TryGetValue(qualified, out def);
                }

                if (def != null)
                {
                    instanceMap[varName] = def;
                    var fieldAssignments = ParseStructFieldAssignments(fieldsRaw);
                    foreach (var field in def.Fields)
                    {
                        string value = fieldAssignments.ContainsKey(field.Name)
                            ? fieldAssignments[field.Name]
                            : GetDefaultValue(field.Type);
                        string destReg = varName.StartsWith("%")
                            ? $"%{varName.Substring(1)}_{field.Name}"
                            : $"{varName}_{field.Name}";
                        output.Add(new PreprocessedLine($"let {destReg}, {value}", sourceLine));
                    }
                    continue;
                }
                else
                {
                    _reporter.Report(new AriaError(
                        $"構造体 '{structName}' が見つかりません。",
                        sourceLine, "", AriaErrorLevel.Error));
                }
            }

            if (enumName is not null)
            {
                if (trimmed.StartsWith("}", StringComparison.Ordinal))
                {
                    enumName = null;
                    enumNextValue = 0;
                    currentEnum = null;
                    continue;
                }

                var enumStoreTarget = inFuncBlock ? funcLocalConstants : globalConstants;
                var enumMergedTarget = inFuncBlock ? constants : null;
                ParseEnumMembers(trimmed, enumName, enumStoreTarget, currentEnum!, ref enumNextValue, enumMergedTarget);
                continue;
            }

            var constStoreTarget = inFuncBlock ? funcLocalConstants : globalConstants;
            var constMergedTarget = inFuncBlock ? constants : null;
            if (TryParseConst(trimmed, constStoreTarget, constMergedTarget)) continue;

            if (TryParseDefineMacro(trimmed, constStoreTarget, constMergedTarget)) continue;

            if (TryParseDefineEffect(trimmed)) continue;

            if (TryStartEnum(trimmed, constStoreTarget, enums, enumRegistry, namespaceStack, ref enumName, ref enumNextValue, ref currentEnum)) continue;

            if (TryStartNamespace(trimmed, namespaceStack)) continue;
            
            // T12: use/modules support
            if (trimmed.StartsWith("use ", StringComparison.OrdinalIgnoreCase))
            {
                string rest = trimmed.Substring(4).Trim();
                if (rest.StartsWith("\"") && rest.EndsWith("\""))
                {
                    string moduleName = rest.Trim('"');
                    if (TryImportModule(moduleName, baseDir, output, functions, structs, enums, importedModules, importMap))
                        continue;
                }
                else if (rest.Contains('.'))
                {
                    var parts = rest.Split('.', 2);
                    string ns = parts[0];
                    string name = parts[1];
                    importMap[name] = $"{ns}_{name}";
                    continue;
                }
            }

            if (TryParseUsing(trimmed, usingNamespaces)) continue;

        // switch ブロック処理（TryCloseModernBlock の前に配置して } を捕捉）
            if (inSwitchBlock)
            {
                if (trimmed.StartsWith("}", StringComparison.Ordinal))
                {
                    // switch 終了 → 展開
                    inSwitchBlock = false;
                    string switchEnd = $"__switch_end_{switchCounter}";
                    
                    // case 判定を出力
                    foreach (var caseKvp in switchCaseLines)
                    {
                        string caseLabel = $"__switch_{switchCounter}_case_{caseKvp.Key}";
                        output.Add(new PreprocessedLine($"if {switchExpression} == {caseKvp.Key} goto *{caseLabel}", sourceLine));
                    }
                    if (switchDefaultLines.Count > 0)
                    {
                        output.Add(new PreprocessedLine($"goto *{switchEnd}_default", sourceLine));
                    }
                    else
                    {
                        output.Add(new PreprocessedLine($"goto *{switchEnd}", sourceLine));
                    }
                    
                    // case 本体を出力
                    foreach (var caseKvp in switchCaseLines)
                    {
                        string caseLabel = $"__switch_{switchCounter}_case_{caseKvp.Key}";
                        output.Add(new PreprocessedLine($"*{caseLabel}", sourceLine));
                        foreach (var caseLine in caseKvp.Lines)
                        {
                            output.Add(new PreprocessedLine(caseLine, sourceLine));
                        }
                        output.Add(new PreprocessedLine($"goto *{switchEnd}", sourceLine));
                    }
                    
                    // default 本体を出力
                    if (switchDefaultLines.Count > 0)
                    {
                        output.Add(new PreprocessedLine($"*{switchEnd}_default", sourceLine));
                        foreach (var defaultLine in switchDefaultLines)
                        {
                            output.Add(new PreprocessedLine(defaultLine, sourceLine));
                        }
                    }
                    
                    output.Add(new PreprocessedLine($"*{switchEnd}", sourceLine));
                    
                    switchExpression = null;
                    switchCaseLines.Clear();
                    switchDefaultLines.Clear();
                    currentSwitchCase = null;
                    continue;
                }
                
                // case 行を検出
                var caseMatch = Regex.Match(trimmed, @"^case\s+(.+?):$", RegexOptions.IgnoreCase);
                if (caseMatch.Success)
                {
                    string caseKey = caseMatch.Groups[1].Value.Trim();
                    if (switchCaseLines.Any(c => c.Key == caseKey))
                    {
                        _reporter.Report(new AriaError(
                            $"switch文に重複するcase値 '{caseKey}' があります。",
                            sourceLine, "", AriaErrorLevel.Error));
                    }
                    currentSwitchCase = caseKey;
                    switchCaseLines.Add((caseKey, new List<string>()));
                    continue;
                }
                
                // default 行を検出
                if (Regex.IsMatch(trimmed, @"^default\s*:\s*$", RegexOptions.IgnoreCase))
                {
                    currentSwitchCase = "__default__";
                    continue;
                }
                
                // switch 内の通常行を収集
                string processedLine = rawLine;
                processedLine = RewriteQualifiedCalls(processedLine, importMap);
                processedLine = RewriteFunctionStyleCall(processedLine);
                processedLine = ReplaceConstantsOutsideQuotes(processedLine, constants);
                processedLine = RewriteCppIf(processedLine);
                processedLine = RewriteModernBlockOpen(processedLine, blockStack);
                processedLine = RewriteNamespaceLabels(processedLine, namespaceStack);
                
                if (currentSwitchCase == "__default__")
                {
                    switchDefaultLines.Add(processedLine);
                }
                else if (currentSwitchCase != null)
                {
                    switchCaseLines.Last(c => c.Key == currentSwitchCase).Lines.Add(processedLine);
                }
                continue;
            }

            // match/case ブロック処理
            // 1) match <expr> 行を検出 → ブロック開始
            if (trimmed.StartsWith("match ", StringComparison.OrdinalIgnoreCase))
            {
                inMatchBlock = true;
                matchExpression = trimmed.Substring("match ".Length).Trim();
                matchCounter++;
                currentMatchCases = new List<MatchCase>();
                matchStartSourceLine = sourceLine;
                // prepare to collect cases on subsequent lines
                continue;
            }

            // inside a match block: collect cases and body until endmatch
            if (inMatchBlock)
            {
                // end of match block
                if (trimmed.StartsWith("endmatch", StringComparison.OrdinalIgnoreCase))
                {
                    // Determine default/wildcard index if any
                    int defaultIndex = -1;
                    for (int ci = 0; ci < currentMatchCases.Count; ci++)
                    {
                        if (currentMatchCases[ci].IsWildcard || currentMatchCases[ci].IsDefault)
                        {
                            defaultIndex = ci;
                            break;
                        }
                    }

                    // Emit transpiled if/goto chain
                    // 1) non-default cases with literals (and optional guards)
                    for (int ci = 0; ci < currentMatchCases.Count; ci++)
                    {
                        var c = currentMatchCases[ci];
                        string caseLabel = $"__match_{matchCounter}_case_{ci}";

                        if (!c.IsWildcard && !c.IsDefault)
                        {
                            string cond = $"{matchExpression} == {c.Key}";
                            if (!string.IsNullOrWhiteSpace(c.Guard))
                                cond += $" && {c.Guard}";
                            output.Add(new PreprocessedLine($"if {cond} goto *{caseLabel}", matchStartSourceLine));
                            if (ci + 1 < currentMatchCases.Count)
                                output.Add(new PreprocessedLine($"goto *__match_{matchCounter}_case_{ci + 1}", matchStartSourceLine));
                        }
                    }

                    // 2) Emit labels and bodies
                    for (int ci = 0; ci < currentMatchCases.Count; ci++)
                    {
                        var c = currentMatchCases[ci];
                        string caseLabel = $"__match_{matchCounter}_case_{ci}";
                        output.Add(new PreprocessedLine($"*{caseLabel}", matchStartSourceLine));
                        foreach (var b in c.Body)
                        {
                            output.Add(new PreprocessedLine(b, matchStartSourceLine));
                        }
                        output.Add(new PreprocessedLine($"goto *__match_end_{matchCounter}", matchStartSourceLine));
                    }

                    // 3) Default/wildcard body (fallback)
                    if (defaultIndex >= 0)
                    {
                        var d = currentMatchCases[defaultIndex];
                        string dLabel = $"__match_{matchCounter}_case_{defaultIndex}";
                        output.Add(new PreprocessedLine($"*{dLabel}", matchStartSourceLine));
                        foreach (var b in d.Body)
                        {
                            output.Add(new PreprocessedLine(b, matchStartSourceLine));
                        }
                    }
                    // End label for the match block
                    output.Add(new PreprocessedLine($"*__match_end_{matchCounter}", matchStartSourceLine));

                    // Exhaustiveness check: if no default or wildcard, warn
                    bool hasFallback = currentMatchCases.Any(mc => mc.IsDefault || mc.IsWildcard);
                    if (!hasFallback)
                    {
                        _reporter.Report(new AriaError(
                            "match ブロックが非網羅です。デフォルト(case _ / default) を追加してください。",
                            matchStartSourceLine, "", AriaErrorLevel.Warning));
                    }

                    // reset match state
                    inMatchBlock = false;
                    matchExpression = null;
                    currentMatchCases = null!;
                    matchStartSourceLine = 0;
                    continue;
                }

                // case 分岐 lines
                if (trimmed.StartsWith("case ", StringComparison.OrdinalIgnoreCase))
                {
                    // finalize place for new case
                    string rest = trimmed.Substring("case ".Length).Trim();
                    var c = new MatchCase();
                    // default case
                    if (rest.StartsWith("default", StringComparison.OrdinalIgnoreCase))
                    {
                        c.IsDefault = true;
                        currentMatchCases.Add(c);
                        // subsequent lines become body for default until next case/default/endmatch
                        currentMatchCase = c;
                        continue;
                    }
                    // wildcard underscore
                    if (rest.StartsWith("_"))
                    {
                        c.IsWildcard = true;
                        currentMatchCases.Add(c);
                        currentMatchCase = c;
                        continue;
                    }
                    // literal and optional guard
                    string literal = rest;
                    string guard = null!;
                    int ifIdx = literal.IndexOf(" if ", StringComparison.OrdinalIgnoreCase);
                    if (ifIdx >= 0)
                    {
                        guard = literal.Substring(ifIdx + 4).Trim();
                        literal = literal.Substring(0, ifIdx).Trim();
                    }
                    c.Key = literal;
                    c.Guard = string.IsNullOrWhiteSpace(guard) ? null : guard;
                    currentMatchCases.Add(c);
                    currentMatchCase = c;
                    continue;
                }

                // body line inside a match case
                if (currentMatchCase != null)
                {
                    string processedLine = rawLine;
                    processedLine = RewriteQualifiedCalls(processedLine, importMap);
                    processedLine = RewriteFunctionStyleCall(processedLine);
                    processedLine = ReplaceConstantsOutsideQuotes(processedLine, constants);
                    processedLine = RewriteCppIf(processedLine);
                    processedLine = RewriteModernBlockOpen(processedLine, blockStack);
                    processedLine = RewriteNamespaceLabels(processedLine, namespaceStack);
                    currentMatchCase!.Body.Add(processedLine);
                    // do not emit until endmatch
                    continue;
                }
            }

            // try-catch ブロック処理
            if (inTryBlock || inCatchBlock)
            {
                if (trimmed.StartsWith("}", StringComparison.Ordinal))
                {
                    if (inCatchBlock)
                    {
                        // catch 終了
                        inCatchBlock = false;
                        output.Add(new PreprocessedLine($"*{currentTryEndLabel}", sourceLine));
                        
                        currentTryLabel = null;
                        currentCatchLabel = null;
                        currentTryEndLabel = null;
                        tryBodyLines.Clear();
                        catchBodyLines.Clear();
                    }
                    else if (inTryBlock)
                    {
                        // try 終了 → try 本体を出力して catch 開始
                        inTryBlock = false;
                        output.Add(new PreprocessedLine($"*{currentTryLabel}", sourceLine));
                        foreach (var line in tryBodyLines)
                        {
                            output.Add(new PreprocessedLine(line, sourceLine));
                        }
                        output.Add(new PreprocessedLine($"goto *{currentTryEndLabel}", sourceLine));
                        output.Add(new PreprocessedLine($"*{currentCatchLabel}", sourceLine));
                        foreach (var line in catchBodyLines)
                        {
                            output.Add(new PreprocessedLine(line, sourceLine));
                        }
                        inCatchBlock = true;
                    }
                    continue;
                }
                
                // catch キーワード検出（catchブロック内で再発火しないようガード）
                if (!inCatchBlock && Regex.IsMatch(trimmed, @"^catch\s*\{\s*$", RegexOptions.IgnoreCase))
                {
                    inTryBlock = false;
                    output.Add(new PreprocessedLine($"*{currentTryLabel}", sourceLine));
                    foreach (var line in tryBodyLines)
                    {
                        output.Add(new PreprocessedLine(line, sourceLine));
                    }
                    output.Add(new PreprocessedLine($"goto *{currentTryEndLabel}", sourceLine));
                    output.Add(new PreprocessedLine($"*{currentCatchLabel}", sourceLine));
                    foreach (var line in catchBodyLines)
                    {
                        output.Add(new PreprocessedLine(line, sourceLine));
                    }
                    inCatchBlock = true;
                    continue;
                }
                
                string tcLine = rawLine;
                tcLine = RewriteQualifiedCalls(tcLine, importMap);
                tcLine = RewriteFunctionStyleCall(tcLine);
                tcLine = ReplaceConstantsOutsideQuotes(tcLine, constants);
                tcLine = RewriteCppIf(tcLine);
                tcLine = RewriteModernBlockOpen(tcLine, blockStack);
                tcLine = RewriteNamespaceLabels(tcLine, namespaceStack);
                
                // throw → goto catch
                if (Regex.IsMatch(tcLine.Trim(), @"^throw\b", RegexOptions.IgnoreCase))
                {
                    tcLine = Regex.Replace(tcLine.Trim(), @"^throw\b.*$", $"goto *{currentCatchLabel}", RegexOptions.IgnoreCase);
                }
                
                if (inCatchBlock)
                {
                    catchBodyLines.Add(tcLine);
                }
                else
                {
                    tryBodyLines.Add(tcLine);
                }
                continue;
            }

            if (TryCloseModernBlock(trimmed, namespaceStack, blockStack, output, sourceLine)) continue;

            // func ブロック処理
            if (inFuncBlock)
            {
                if (trimmed.Equals("endfunc", StringComparison.OrdinalIgnoreCase))
                {
                    inFuncBlock = false;
                    if (currentFunc != null) functions.Add(currentFunc);
                    output.Add(new PreprocessedLine("return", sourceLine));
                    currentFunc = null;
                    constants = globalConstants;
                    funcLocalConstants.Clear();
                    continue;
                }
                
                string line = rawLine;
                line = RewriteQualifiedCalls(line, importMap);
                line = RewriteFunctionStyleCall(line);
                line = ReplaceConstantsOutsideQuotes(line, constants);
                line = RewriteCppIf(line);
                line = RewriteModernBlockOpen(line, blockStack);
                line = RewriteNamespaceLabels(line, namespaceStack);
                output.Add(new PreprocessedLine(line, sourceLine));
                continue;
            }
            
            // struct ブロック処理
            if (inStructBlock)
            {
                if (trimmed.Equals("endstruct", StringComparison.OrdinalIgnoreCase))
                {
                    inStructBlock = false;
                    if (currentStruct != null) structs.Add(currentStruct);
                    currentStruct = null;
                    continue;
                }
                
                // フィールド行をパース: "int id" または "string text"
                var fieldParts = trimmed.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (fieldParts.Length >= 2 && currentStruct != null)
                {
                    int fieldSize = fieldParts[0].ToLowerInvariant() switch
                    {
                        "int" or "bool" => 4,
                        "float" => 4,
                        "string" => 256,
                        _ => 4
                    };
                    currentStruct.Fields.Add(new StructField
                    {
                        Name = fieldParts[1].TrimEnd(';'),
                        Type = fieldParts[0],
                        Offset = currentStruct.TotalSize,
                        Size = fieldSize
                    });
                }
                continue;
            }
            
            // switch 開始行を検出
            var switchMatch = Regex.Match(trimmed, @"^switch\s+(.+?)\s*\{\s*$", RegexOptions.IgnoreCase);
            if (switchMatch.Success)
            {
                inSwitchBlock = true;
                switchExpression = switchMatch.Groups[1].Value.Trim();
                switchCounter++;
                switchCaseLines.Clear();
                switchDefaultLines.Clear();
                currentSwitchCase = null;
                continue;
            }
            
            // func 開始行を検出
            var funcMatch = Regex.Match(trimmed, @"^func\s+([A-Za-z_][A-Za-z0-9_]*)\s*\((.*?)\)\s*(?:->\s*([A-Za-z_][A-Za-z0-9_]*(?:<[^>]+>)?))?\s*$", RegexOptions.IgnoreCase);
            if (funcMatch.Success)
            {
                string funcName = funcMatch.Groups[1].Value;
                string argsStr = funcMatch.Groups[2].Value.Trim();
                string returnType = funcMatch.Groups[3].Success ? funcMatch.Groups[3].Value : "void";

                // 名前空間プレフィックスを適用
                string qualifiedName = namespaceStack.Count > 0
                    ? string.Join("_", namespaceStack.Reverse()) + "_" + funcName
                    : funcName;

                inFuncBlock = true;
                funcLocalConstants.Clear();
                constants = new Dictionary<string, string>(globalConstants, StringComparer.OrdinalIgnoreCase);
                currentFunc = new FunctionInfo
                {
                    QualifiedName = qualifiedName,
                    ShortName = funcName,
                    Namespace = namespaceStack.Count > 0 ? string.Join(".", namespaceStack.Reverse()) : null,
                    ReturnType = returnType,
                    Parameters = new List<ParameterInfo>(),
                    DocComment = hasNonDocLineSinceComment ? null : pendingDocComment
                };
                pendingDocComment = null;
                hasNonDocLineSinceComment = false;
                
                // defsub に書き換えて出力（ラベルも生成）
                output.Add(new PreprocessedLine($"defsub {qualifiedName}", sourceLine));
                output.Add(new PreprocessedLine($"*{qualifiedName}", sourceLine));
                
                // 引数をパースして getparam を生成
                if (!string.IsNullOrEmpty(argsStr))
                {
                    var argList = argsStr.Split(',');
                    int paramIndex = 0;
                    foreach (var arg in argList)
                    {
                        var argTrim = arg.Trim();
                        var argParts = argTrim.Split(':', StringSplitOptions.RemoveEmptyEntries);
                        if (argParts.Length >= 1)
                        {
                            bool isRef = argTrim.StartsWith("ref ", StringComparison.OrdinalIgnoreCase);
                            string paramName = isRef ? argParts[0].Trim().Substring(4).Trim() : argParts[0].Trim();
                            string paramType = argParts.Length > 1 ? argParts[1].Trim() : "int";
                            currentFunc.Parameters.Add(new ParameterInfo
                            {
                                Name = paramName,
                                Type = paramType,
                                Index = paramIndex,
                                IsRef = isRef
                            });
                            string paramRegister = paramType.Equals("string", StringComparison.OrdinalIgnoreCase) ||
                                                   paramType.Equals("str", StringComparison.OrdinalIgnoreCase)
                                ? $"${paramName}"
                                : $"%{paramName}";
                            output.Add(new PreprocessedLine($"getparam {paramRegister}", sourceLine));
                            paramIndex++;
                        }
                    }
                    currentFunc.LocalCount = paramIndex;
                }
                continue;
            }
            
            // try-catch 開始行を検出
            var tryMatch = Regex.Match(trimmed, @"^try\s*\{\s*$", RegexOptions.IgnoreCase);
            if (tryMatch.Success)
            {
                inTryBlock = true;
                tryCounter++;
                currentTryLabel = $"__try_{tryCounter}";
                currentCatchLabel = $"__catch_{tryCounter}";
                currentTryEndLabel = $"__try_end_{tryCounter}";
                tryBodyLines.Clear();
                catchBodyLines.Clear();
                continue;
            }
            
            // lambda 開始行を検出: lambda(args) -> type { ... }(call_args)
            var lambdaMatch = Regex.Match(trimmed, @"^lambda\s*\((.*?)\)\s*(?:->\s*(\w+))?\s*\{\s*$", RegexOptions.IgnoreCase);
            if (lambdaMatch.Success)
            {
                string lambdaArgs = lambdaMatch.Groups[1].Value.Trim();
                string lambdaReturn = lambdaMatch.Groups[2].Success ? lambdaMatch.Groups[2].Value : "void";
                string lambdaName = $"__lambda_{Guid.NewGuid().ToString("N")[..8]}";
                
                // lambda ブロックを func と同じように処理（インライン展開）
                output.Add(new PreprocessedLine($"defsub {lambdaName}", sourceLine));
                output.Add(new PreprocessedLine($"*{lambdaName}", sourceLine));
                
                if (!string.IsNullOrEmpty(lambdaArgs))
                {
                    foreach (var arg in lambdaArgs.Split(','))
                    {
                        var argTrim = arg.Trim();
                        if (!string.IsNullOrEmpty(argTrim))
                        {
                            var argParts = argTrim.Split(':', StringSplitOptions.RemoveEmptyEntries);
                            string paramName = argParts[0].Trim().Replace("ref ", "").Replace("ref ", "").Trim();
                            output.Add(new PreprocessedLine($"getparam %{paramName}", sourceLine));
                        }
                    }
                }
                
                // 次の行から lambda 本体を読み込む
                int lambdaStart = lineIndex + 1;
                int braceDepth = 1;
                int li = lambdaStart;
                for (; li < lines.Length && braceDepth > 0; li++)
                {
                    var lLine = StripComments(lines[li]).Trim();
                    if (string.IsNullOrEmpty(lLine)) continue;
                    bool inQuotes = false;
                    for (int ci = 0; ci < lLine.Length; ci++)
                    {
                        if (lLine[ci] == '"') inQuotes = !inQuotes;
                        else if (!inQuotes && lLine[ci] == '{') braceDepth++;
                        else if (!inQuotes && lLine[ci] == '}') braceDepth--;
                    }
                    if (braceDepth <= 0) break;
                    
                    string procLine = lines[li];
                    procLine = RewriteQualifiedCalls(procLine, importMap);
                    procLine = RewriteFunctionStyleCall(procLine);
                    procLine = ReplaceConstantsOutsideQuotes(procLine, constants);
                    procLine = RewriteCppIf(procLine);
                    procLine = RewriteModernBlockOpen(procLine, blockStack);
                    procLine = RewriteNamespaceLabels(procLine, namespaceStack);
                    output.Add(new PreprocessedLine(procLine, li + 1));
                }
                
                output.Add(new PreprocessedLine("return", sourceLine));
                lineIndex = li;
                continue;
            }
            
            // struct 開始行を検出
            var structMatch = Regex.Match(trimmed, @"^struct\s+([A-Za-z_][A-Za-z0-9_]*)\s*$", RegexOptions.IgnoreCase);
            if (structMatch.Success)
            {
                string structName = structMatch.Groups[1].Value;
                string qualifiedName = namespaceStack.Count > 0 
                    ? string.Join("_", namespaceStack.Reverse()) + "_" + structName 
                    : structName;
                
                inStructBlock = true;
                currentStruct = new StructDefinition
                {
                    QualifiedName = qualifiedName,
                    ShortName = structName,
                    Namespace = namespaceStack.Count > 0 ? string.Join(".", namespaceStack.Reverse()) : null,
                    DocComment = hasNonDocLineSinceComment ? null : pendingDocComment
                };
                pendingDocComment = null;
                hasNonDocLineSinceComment = false;
                structRegistry[qualifiedName] = currentStruct;
                structRegistry[structName] = currentStruct;
                continue;
            }

            // auto/var → let に展開
            var autoMatch = Regex.Match(trimmed, @"^(auto|var)\s+(.+)$", RegexOptions.IgnoreCase);
            if (autoMatch.Success)
            {
                string autoValue = autoMatch.Groups[2].Value;
                autoValue = ReplaceConstantsOutsideQuotes(autoValue, constants);
                output.Add(new PreprocessedLine($"let {autoValue}", sourceLine));
                continue;
            }

            string line2 = RewriteQualifiedCalls(rawLine, importMap);
            var expandedCalls = ExpandFuncStyleCall(line2, namespaceStack, importMap, usingNamespaces, functions);
            if (expandedCalls.Count > 0 && expandedCalls[0] != line2)
            {
                // func呼び出しが展開された
                foreach (var expandedLine in expandedCalls)
                {
                    var processed = ReplaceConstantsOutsideQuotes(expandedLine, constants);
                    processed = RewriteCppIf(processed);
                    processed = RewriteModernBlockOpen(processed, blockStack);
                    processed = RewriteNamespaceLabels(processed, namespaceStack);
                    output.Add(new PreprocessedLine(processed, sourceLine));
                }
            }
            else
            {
                line2 = RewriteFunctionStyleCall(line2);
                line2 = ReplaceConstantsOutsideQuotes(line2, constants);
                line2 = RewriteCppIf(line2);
                line2 = RewriteModernBlockOpen(line2, blockStack);
                line2 = RewriteNamespaceLabels(line2, namespaceStack);
                output.Add(new PreprocessedLine(line2, sourceLine));
            }
        }

        while (blockStack.Count > 0)
        {
            blockStack.Pop();
            output.Add(new PreprocessedLine("endif", lines.Length));
        }

        // T14 post-processing: expand any remaining struct instantiations (e.g., from auto/var)
        // and rewrite field accesses
        var finalOutput = new List<PreprocessedLine>(output.Count * 2);
        foreach (var pl in output)
        {
            var expanded = TryExpandStructInstantiation(pl.Text, pl.SourceLine, structRegistry, instanceMap);
            if (expanded != null)
            {
                finalOutput.AddRange(expanded);
            }
            else
            {
                finalOutput.Add(pl);
            }
        }

        // Rewrite field accesses: %var.field -> %var_field
        for (int i = 0; i < finalOutput.Count; i++)
        {
            var pl = finalOutput[i];
            string rewritten = RewriteStructFieldAccess(pl.Text, instanceMap);
            finalOutput[i] = new PreprocessedLine(rewritten, pl.SourceLine);
        }

        return (finalOutput.ToArray(), functions, structs, enums);
    }

    private static bool TryParseConst(string trimmed, Dictionary<string, string> target, Dictionary<string, string>? mergedTarget = null)
    {
        var match = Regex.Match(trimmed, @"^const\s+([A-Za-z_][A-Za-z0-9_.]*)\s*(?:=)?\s*(.+?)\s*;?$", RegexOptions.IgnoreCase);
        if (!match.Success) return false;

        string key = match.Groups[1].Value;
        string value = match.Groups[2].Value.Trim();
        target[key] = value;
        if (mergedTarget != null) mergedTarget[key] = value;
        return true;
    }
    
    private static bool TryParseDefineMacro(string trimmed, Dictionary<string, string> target, Dictionary<string, string>? mergedTarget = null)
    {
        var match = Regex.Match(trimmed, @"^#define\s+([A-Za-z_][A-Za-z0-9_]*)\s+(.+?)$", RegexOptions.IgnoreCase);
        if (!match.Success) return false;

        string name = match.Groups[1].Value;
        string value = match.Groups[2].Value.Trim();
        target[name] = value;
        if (mergedTarget != null) mergedTarget[name] = value;
        return true;
    }
    
    private static bool TryParseDefineEffect(string trimmed)
    {
        var match = Regex.Match(trimmed, @"^define-effect\s+([A-Za-z_][A-Za-z0-9_]*)\s+(.+)$", RegexOptions.IgnoreCase);
        if (!match.Success) return false;

        string name = match.Groups[1].Value;
        string replacement = match.Groups[2].Value.Trim();
        if (replacement.StartsWith("\"") && replacement.EndsWith("\""))
            replacement = replacement.Substring(1, replacement.Length - 2);
        
        TextEffectParser.DefineEffect(name, replacement);
        return true;
    }

    private static bool TryStartEnum(string trimmed, Dictionary<string, string> target, List<EnumDefinition> enums, Dictionary<string, EnumDefinition> registry, Stack<string> namespaceStack, ref string? enumName, ref int enumNextValue, ref EnumDefinition? currentEnum)
    {
        var match = Regex.Match(trimmed, @"^enum\s+([A-Za-z_][A-Za-z0-9_]*)\s*\{(.*)$", RegexOptions.IgnoreCase);
        if (!match.Success) return false;

        enumName = match.Groups[1].Value;
        enumNextValue = 0;
        string? ns = namespaceStack.Count > 0 ? string.Join(".", namespaceStack.Reverse()) : null;

        currentEnum = new EnumDefinition { Name = enumName, Namespace = ns };
        enums.Add(currentEnum);
        registry[enumName] = currentEnum;

        string body = match.Groups[2].Value.Trim();
        bool closesInline = body.Contains('}');
        if (closesInline)
        {
            body = body.Substring(0, body.IndexOf('}'));
        }

        ParseEnumMembers(body, enumName, target, currentEnum, ref enumNextValue);
        if (closesInline)
        {
            enumName = null;
            enumNextValue = 0;
            currentEnum = null;
        }

        return true;
    }

    private static void ParseEnumMembers(string body, string enumName, Dictionary<string, string> target, EnumDefinition enumDef, ref int nextValue, Dictionary<string, string>? mergedTarget = null)
    {
        foreach (var rawMember in body.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var member = rawMember.Trim().TrimEnd(';');
            if (member.Length == 0) continue;

            var parts = member.Split('=', 2);
            string name = parts[0].Trim();
            if (!Regex.IsMatch(name, @"^[A-Za-z_][A-Za-z0-9_]*$")) continue;

            if (parts.Length == 2 && int.TryParse(parts[1].Trim(), out int explicitValue))
            {
                nextValue = explicitValue;
            }

            string key = $"{enumName}.{name}";
            target[key] = nextValue.ToString();
            if (mergedTarget != null) mergedTarget[key] = nextValue.ToString();
            enumDef.Members[name] = nextValue;

            if (!string.IsNullOrEmpty(enumDef.Namespace))
            {
                string qualifiedKey = $"{enumDef.Namespace}.{enumName}.{name}";
                target[qualifiedKey] = nextValue.ToString();
                if (mergedTarget != null) mergedTarget[qualifiedKey] = nextValue.ToString();
            }

            nextValue++;
        }
    }

    private static bool TryParseUsing(string trimmed, List<string> usingNamespaces)
    {
        var match = Regex.Match(trimmed, @"^using\s+([A-Za-z_][A-Za-z0-9_.]*)\s*;?\s*$", RegexOptions.IgnoreCase);
        if (!match.Success) return false;

        usingNamespaces.Add(match.Groups[1].Value);
        return true;
    }

    private bool TryImportModule(string moduleName, string baseDir, List<PreprocessedLine> output, List<FunctionInfo> functions, List<StructDefinition> structs, List<EnumDefinition> enums, HashSet<string> importedModules, Dictionary<string, string> importMap)
    {
        string modulePath = string.IsNullOrEmpty(baseDir)
            ? Path.Combine("modules", moduleName + ".aria")
            : Path.Combine(baseDir, "modules", moduleName + ".aria");
        modulePath = modulePath.Replace('\\', '/');

        string localPath = modulePath.Replace('/', Path.DirectorySeparatorChar);
        if (importedModules.Contains(localPath)) return true;
        if (!File.Exists(localPath))
        {
            _reporter.Report(new AriaError($"モジュールファイルが見つかりません: {modulePath}", 0, "", AriaErrorLevel.Error));
            return false;
        }
        importedModules.Add(localPath);

        string[] moduleLines;
        try
        {
            moduleLines = File.ReadAllLines(localPath);
        }
        catch (Exception ex)
        {
            _reporter.Report(new AriaError($"モジュールファイルの読み込みに失敗しました: {modulePath} ({ex.Message})", 0, "", AriaErrorLevel.Error));
            return false;
        }

        var (modLines, modFuncs, modStructs, modEnums) = PreprocessModernSyntaxCore(moduleLines, modulePath, importedModules);

        output.AddRange(modLines);
        functions.AddRange(modFuncs);
        structs.AddRange(modStructs);
        enums.AddRange(modEnums);

        foreach (var f in modFuncs)
        {
            if (!f.QualifiedName.Equals(f.ShortName, StringComparison.OrdinalIgnoreCase))
            {
                if (!importMap.ContainsKey(f.ShortName))
                    importMap[f.ShortName] = f.QualifiedName;
            }
        }

        return true;
    }

    private static bool TryStartNamespace(string trimmed, Stack<string> namespaceStack)
    {
        var match = Regex.Match(trimmed, @"^namespace\s+([A-Za-z_][A-Za-z0-9_.]*)\s*\{\s*$", RegexOptions.IgnoreCase);
        if (!match.Success) return false;

        namespaceStack.Push(match.Groups[1].Value);
        return true;
    }

    private static bool TryCloseModernBlock(string trimmed, Stack<string> namespaceStack, Stack<string> blockStack, List<PreprocessedLine> output, int sourceLine)
    {
        if (Regex.IsMatch(trimmed, @"^\}\s*else\s*\{\s*$", RegexOptions.IgnoreCase))
        {
            output.Add(new PreprocessedLine("else", sourceLine));
            return true;
        }

        if (!Regex.IsMatch(trimmed, @"^\}\s*;?\s*$")) return false;

        if (blockStack.Count > 0)
        {
            var blockType = blockStack.Pop();
            output.Add(new PreprocessedLine(blockType == "while" ? "wend" : "endif", sourceLine));
        }
        else if (namespaceStack.Count > 0)
        {
            namespaceStack.Pop();
        }

        return true;
    }

    private static string RewriteQualifiedCalls(string line, Dictionary<string, string> importMap)
    {
        if (importMap.Count == 0) return line;

        var sb = new System.Text.StringBuilder(line.Length);
        bool inQuotes = false;
        int i = 0;
        while (i < line.Length)
        {
            if (line[i] == '"')
            {
                inQuotes = !inQuotes;
                sb.Append(line[i++]);
                continue;
            }
            if (inQuotes)
            {
                sb.Append(line[i++]);
                continue;
            }

            var m = Regex.Match(line.Substring(i), @"^([A-Za-z_][A-Za-z0-9_]*)\.([A-Za-z_][A-Za-z0-9_]*)\s*\(");
            if (m.Success)
            {
                string ns = m.Groups[1].Value;
                string name = m.Groups[2].Value;
                if (importMap.TryGetValue(name, out var qualified) && qualified.Equals($"{ns}_{name}", StringComparison.OrdinalIgnoreCase))
                {
                    sb.Append(qualified);
                    sb.Append('(');
                    i += m.Length;
                    continue;
                }
            }

            sb.Append(line[i++]);
        }
        return sb.ToString();
    }

    private static string RewriteFunctionStyleCall(string line)
    {
        var ifMatch = Regex.Match(line, @"^(\s*)if\s*\((.*)\)\s*\{\s*$", RegexOptions.IgnoreCase);
        if (ifMatch.Success) return $"{ifMatch.Groups[1].Value}if {ifMatch.Groups[2].Value} {{";

        if (!TryExtractFuncCall(line, out string? indent, out string? funcName, out string? args, out string? trailing))
            return line;

        // Remove trailing semicolon
        if (trailing!.EndsWith(";")) trailing = trailing.Substring(0, trailing.Length - 1).Trim();
        if (!string.IsNullOrEmpty(trailing)) return line; // extra chars after ) - not a simple call

        return args!.Length == 0
            ? $"{indent}{funcName}"
            : $"{indent}{funcName} {args}";
    }

    /// <summary>
    /// 関数スタイル呼び出し `name(args)` から名前と引数を抽出（引用符内の ) を無視）
    /// </summary>
    private static bool TryExtractFuncCall(string line, out string? indent, out string? funcName, out string? args, out string? trailing)
    {
        indent = null;
        funcName = null;
        args = null;
        trailing = null;

        var nameMatch = Regex.Match(line, @"^(\s*)([A-Za-z_][A-Za-z0-9_]*)\s*\(");
        if (!nameMatch.Success) return false;

        int parenStart = nameMatch.Index + nameMatch.Length - 1; // position of '('
        int parenDepth = 1;
        bool inQuotes = false;
        int i = parenStart + 1;

        while (i < line.Length && parenDepth > 0)
        {
            if (line[i] == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (!inQuotes)
            {
                if (line[i] == '(') parenDepth++;
                else if (line[i] == ')') parenDepth--;
            }
            i++;
        }

        if (parenDepth != 0) return false; // unmatched parentheses

        int argsStart = parenStart + 1;
        int argsEnd = i - 1;
        indent = nameMatch.Groups[1].Value;
        funcName = nameMatch.Groups[2].Value;
        args = line.Substring(argsStart, argsEnd - argsStart).Trim();
        trailing = line.Substring(i).Trim();
        return true;
    }
    
    /// <summary>
    /// funcスタイル呼び出しを gosub に展開（引数は gosub に付加）
    /// </summary>
    private List<string> ExpandFuncStyleCall(string line, Stack<string> namespaceStack, Dictionary<string, string> importMap, List<string> usingNamespaces, List<FunctionInfo> knownFunctions)
    {
        var result = new List<string>();
        
        if (!TryExtractFuncCall(line, out string? indent, out string? funcName, out string? argsStr, out string? trailing))
        {
            result.Add(line);
            return result;
        }

        // if, while, for 等の制御構文は除外
        if (funcName!.Equals("if", StringComparison.OrdinalIgnoreCase) ||
            funcName.Equals("while", StringComparison.OrdinalIgnoreCase) ||
            funcName.Equals("for", StringComparison.OrdinalIgnoreCase) ||
            funcName.Equals("switch", StringComparison.OrdinalIgnoreCase))
        {
            result.Add(line);
            return result;
        }
        
        // 既存コマンドかチェック（コマンド呼び出しはそのまま、func展開対象外）
        if (CommandRegistry.Contains(funcName))
        {
            result.Add(line);
            return result;
        }
        
        // 名前空間修飾（importMap / using / 現在のnamespaceStack）
        string qualifiedName;
        if (importMap.TryGetValue(funcName, out var mapped))
        {
            qualifiedName = mapped;
        }
        else
        {
            string? usingQualified = null;
            foreach (var ns in usingNamespaces)
            {
                string candidate = $"{ns}_{funcName}";
                if (knownFunctions.Any(f => f.QualifiedName.Equals(candidate, StringComparison.OrdinalIgnoreCase)))
                {
                    usingQualified = candidate;
                    break;
                }
            }
            if (usingQualified != null)
                qualifiedName = usingQualified;
            else
                qualifiedName = namespaceStack.Count > 0
                    ? string.Join("_", namespaceStack.Reverse()) + "_" + funcName
                    : funcName;
        }
        
        if (string.IsNullOrEmpty(argsStr))
        {
            // 引数なし: gosub のみ
            result.Add($"{indent}gosub *{qualifiedName}");
        }
        else
        {
            // 引数あり: ref キーワードを REF| プレフィックスに変換（Tokenize で分割されない）
            var processedArgs = argsStr.Split(',').Select(a =>
            {
                var trimmed = a.Trim();
                if (trimmed.StartsWith("ref ", StringComparison.OrdinalIgnoreCase))
                    return "REF|" + trimmed.Substring(4).Trim();
                return trimmed;
            });
            result.Add($"{indent}gosub *{qualifiedName}, {string.Join(", ", processedArgs)}");
        }
        
        return result;
    }

    private static string RewriteCppIf(string line)
    {
        var match = Regex.Match(line, @"^(\s*)if\s*\((.*)\)(.*)$", RegexOptions.IgnoreCase);
        if (!match.Success) return line;

        return $"{match.Groups[1].Value}if {match.Groups[2].Value}{match.Groups[3].Value}";
    }

    private static string RewriteModernBlockOpen(string line, Stack<string> blockStack)
    {
        // ブロックif開始: if cond {  ->  if cond （blockStackにプッシュ）
        var ifBlock = Regex.Match(line, @"^(\s*if\s+.+?)\s*\{\s*$", RegexOptions.IgnoreCase);
        if (ifBlock.Success)
        {
            blockStack.Push("if");
            return ifBlock.Groups[1].Value;
        }

        // ブロックwhile開始: while cond {  ->  while cond （blockStackにプッシュ）
        var whileBlock = Regex.Match(line, @"^(\s*while\s+.+?)\s*\{\s*$", RegexOptions.IgnoreCase);
        if (whileBlock.Success)
        {
            blockStack.Push("while");
            return whileBlock.Groups[1].Value;
        }

        // else {  ->  else
        var elseBlock = Regex.Match(line, @"^\s*else\s*\{\s*$", RegexOptions.IgnoreCase);
        return elseBlock.Success ? "else" : line;
    }

    private static string ReplaceConstantsOutsideQuotes(string line, Dictionary<string, string> constants)
    {
        if (constants.Count == 0) return line;

        var ordered = constants.Keys.OrderByDescending(k => k.Length).ToArray();
        var result = new System.Text.StringBuilder(line.Length);
        bool inQuotes = false;

        for (int i = 0; i < line.Length;)
        {
            if (line[i] == '"')
            {
                inQuotes = !inQuotes;
                result.Append(line[i++]);
                continue;
            }

            if (!inQuotes)
            {
                string? key = ordered.FirstOrDefault(k => IsConstantMatch(line, i, k));
                if (key is not null)
                {
                    result.Append(constants[key]);
                    i += key.Length;
                    continue;
                }
            }

            result.Append(line[i++]);
        }

        return result.ToString();
    }

    private static bool IsConstantMatch(string line, int index, string key)
    {
        if (index + key.Length > line.Length) return false;
        if (!line.AsSpan(index, key.Length).Equals(key.AsSpan(), StringComparison.OrdinalIgnoreCase)) return false;

        bool beforeOk = index == 0 || !IsModernNameChar(line[index - 1]);
        int after = index + key.Length;
        bool afterOk = after >= line.Length || !IsModernNameChar(line[after]);
        return beforeOk && afterOk;
    }

    private static bool IsModernNameChar(char c)
    {
        return char.IsLetterOrDigit(c) || c == '_' || c == '.';
    }

    private static string RewriteNamespaceLabels(string line, Stack<string> namespaceStack)
    {
        if (namespaceStack.Count == 0) return line;

        string ns = string.Join(".", namespaceStack.Reverse());
        string rewritten = Regex.Replace(line, @"^(\s*)\*([A-Za-z_][A-Za-z0-9_]*)\b", m => $"{m.Groups[1].Value}*{ns}.{m.Groups[2].Value}");

        return Regex.Replace(rewritten, @"\b(goto|jmp|gosub|call|beq|bne|bgt|blt)\s+\*([A-Za-z_][A-Za-z0-9_]*)\b",
            m => $"{m.Groups[1].Value} *{ns}.{m.Groups[2].Value}",
            RegexOptions.IgnoreCase);
    }

    private void AddTextInstructions(List<Instruction> instructions, string sourceText, int sourceLine)
    {
        string textData = sourceText.TrimEnd().Replace("\\n", "\n");
        var match = DialogRegex.Match(textData);

        if (match.Success)
        {
            instructions.Add(new Instruction(OpCode.TextClear, new List<string>(), sourceLine));
            if (match.Groups[3].Value == "") textData += "\\";
        }

        string buf = "";
        for (int c = 0; c < textData.Length; c++)
        {
            if (textData[c] == '\\')
            {
                if (buf.Length > 0) { instructions.Add(new Instruction(OpCode.Text, new List<string> { buf }, sourceLine)); buf = ""; }
                instructions.Add(new Instruction(OpCode.WaitClickClear, new List<string>(), sourceLine));
            }
            else if (textData[c] == '@')
            {
                if (buf.Length > 0) { instructions.Add(new Instruction(OpCode.Text, new List<string> { buf }, sourceLine)); buf = ""; }
                instructions.Add(new Instruction(OpCode.WaitClick, new List<string>(), sourceLine));
            }
            else
            {
                buf += textData[c];
            }
        }

        if (buf.Length > 0)
        {
            instructions.Add(new Instruction(OpCode.Text, new List<string> { buf }, sourceLine));
        }
    }

    private string StripComments(string line)
    {
        // # で始まる行はプリプロセッサディレクティブ（#define, # aria-version 等）として無視
        if (line.TrimStart().StartsWith("#")) return "";

        bool inQuotes = false;
        for (int i = 0; i < line.Length; i++)
        {
            if (line[i] == '"') inQuotes = !inQuotes;
            if (!inQuotes)
            {
                if (line[i] == ';') return line.Substring(0, i);
                if (i < line.Length - 1 && line[i] == '/' && line[i + 1] == '/') return line.Substring(0, i);
            }
        }
        return line;
    }

    private List<string> SplitStatements(string line)
    {
        var result = new List<string>();
        int start = 0;
        bool inQuotes = false;
        int braceDepth = 0;
        
        for (int i = 0; i < line.Length; i++)
        {
            if (line[i] == '"') inQuotes = !inQuotes;
            if (!inQuotes)
            {
                if (line[i] == '{') braceDepth++;
                else if (line[i] == '}') braceDepth--;
                else if (braceDepth == 0 && line[i] == ':' && !IsUiTargetColon(line, i))
                {
                    result.Add(line.Substring(start, i - start).Trim());
                    start = i + 1;
                }
            }
        }
        result.Add(line.Substring(start).Trim());
        return result;
    }

    private static bool IsUiTargetColon(string line, int colonIndex)
    {
        int tokenStart = colonIndex - 1;
        while (tokenStart >= 0 && !char.IsWhiteSpace(line[tokenStart]) && line[tokenStart] != ',') tokenStart--;
        tokenStart++;

        string prefix = line.Substring(tokenStart, colonIndex - tokenStart);
        return prefix.Equals("sprite", StringComparison.OrdinalIgnoreCase) ||
               prefix.Equals("group", StringComparison.OrdinalIgnoreCase);
    }

    private List<string> Tokenize(string line)
    {
        var tokens = new List<string>();
        int i = 0;
        
        while (i < line.Length)
        {
            if (char.IsWhiteSpace(line[i])) { i++; continue; }
            if (line[i] == ',') { i++; continue; }

            if (line[i] == '"')
            {
                i++;
                int start = i;
                while (i < line.Length)
                {
                    if (line[i] == '\\' && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        i += 2; // skip escaped quote
                    }
                    else if (line[i] == '"')
                    {
                        break;
                    }
                    else
                    {
                        i++;
                    }
                }
                tokens.Add(line.Substring(start, i - start));
                if (i < line.Length) i++;
            }
            else
            {
                int start = i;
                while (i < line.Length && !char.IsWhiteSpace(line[i]) && line[i] != ',' && line[i] != '"')
                {
                    char c = line[i];

                    // Single-char operators that always split: + / ( ) [ ]
                    // - は単項マイナスか二項演算子として扱う
                    if (c is '+' or '/' or '(' or ')' or '[' or ']')
                    {
                        if (i > start) break;
                        tokens.Add(c.ToString());
                        i++;
                        start = i;
                        continue;
                    }

                    // - は単項マイナス（後続数字あり）か二項演算子
                    if (c == '-')
                    {
                        // 単項マイナス: 後続に数字があれば一続きのトークン
                        if (i + 1 < line.Length && char.IsDigit(line[i + 1]))
                        {
                            i++;
                            while (i < line.Length && char.IsDigit(line[i])) i++;
                            tokens.Add(line.Substring(start, i - start));
                            start = i;
                            continue;
                        }
                        // 二項演算子の -
                        if (i > start) break;
                        tokens.Add(c.ToString());
                        i++;
                        start = i;
                        continue;
                    }

                    // * はラベル参照 (*label) の一部か、乗算演算子
                    if (c == '*')
                    {
                        if (i + 1 < line.Length && (char.IsLetterOrDigit(line[i + 1]) || line[i + 1] == '_'))
                        {
                            // ラベル参照 (*label) の一部
                            i++;
                            while (i < line.Length && (char.IsLetterOrDigit(line[i]) || line[i] == '_'))
                            {
                                i++;
                            }
                            tokens.Add(line.Substring(start, i - start));
                            start = i;
                            continue;
                        }
                        if (i > start) break;
                        tokens.Add(c.ToString());
                        i++;
                        start = i;
                        continue;
                    }

                    // % はレジスタ名の一部か、modulo演算子
                    if (c == '%')
                    {
                        if (i + 1 < line.Length && (char.IsLetterOrDigit(line[i + 1]) || line[i + 1] == '_'))
                        {
                            // レジスタ名 (%0, %abc) の一部
                            i++;
                            while (i < line.Length && (char.IsLetterOrDigit(line[i]) || line[i] == '_'))
                            {
                                i++;
                            }
                            tokens.Add(line.Substring(start, i - start));
                            start = i;
                            continue;
                        }
                        if (i > start) break;
                        tokens.Add(c.ToString());
                        i++;
                        start = i;
                        continue;
                    }

                    // NOT operator
                    if (c == '!')
                    {
                        if (i + 1 < line.Length && line[i + 1] == '=')
                        {
                            if (i > start) break;
                            tokens.Add("!=");
                            i += 2;
                            start = i;
                            continue;
                        }
                        if (i > start) break;
                        tokens.Add("!");
                        i++;
                        start = i;
                        continue;
                    }

                    // Logical AND
                    if (c == '&' && i + 1 < line.Length && line[i + 1] == '&')
                    {
                        if (i > start) break;
                        tokens.Add("&&");
                        i += 2;
                        start = i;
                        continue;
                    }

                    // Logical OR
                    if (c == '|' && i + 1 < line.Length && line[i + 1] == '|')
                    {
                        if (i > start) break;
                        tokens.Add("||");
                        i += 2;
                        start = i;
                        continue;
                    }

                    // Comparison operators
                    if (i + 1 < line.Length && line[i] == '=' && line[i + 1] == '=')
                    {
                        if (i > start) break;
                        tokens.Add("==");
                        i += 2;
                        start = i;
                        continue;
                    }
                    else if (i + 1 < line.Length && line[i] == '>' && line[i + 1] == '=')
                    {
                        if (i > start) break;
                        tokens.Add(">=");
                        i += 2;
                        start = i;
                        continue;
                    }
                    else if (i + 1 < line.Length && line[i] == '<' && line[i + 1] == '=')
                    {
                        if (i > start) break;
                        tokens.Add("<=");
                        i += 2;
                        start = i;
                        continue;
                    }
                    else if (c == '>')
                    {
                        if (i > start) break;
                        tokens.Add(">");
                        i++;
                        start = i;
                        continue;
                    }
                    else if (c == '<')
                    {
                        if (i > start) break;
                        tokens.Add("<");
                        i++;
                        start = i;
                        continue;
                    }
                    else if (c == '=')
                    {
                        if (i > start) break;
                        tokens.Add("=");
                        i++;
                        start = i;
                        continue;
                    }
                    i++;
                }

                string t = line.Substring(start, i - start);
                if (!string.IsNullOrEmpty(t))
                {
                    tokens.Add(t.Replace("\\n", "\n"));
                }
            }
        }
        return tokens;
    }

    /// <summary>
    /// 関数呼び出しの引数型チェック（C++like）
    /// </summary>
    private void ValidateFunctionCalls(List<Instruction> instructions, List<FunctionInfo> functions, List<EnumDefinition> enums, string scriptFile)
    {
        var funcMap = functions.ToDictionary(f => f.QualifiedName, StringComparer.OrdinalIgnoreCase);
        var enumMap = enums.ToDictionary(e => e.Name.ToLowerInvariant(), StringComparer.OrdinalIgnoreCase);
        foreach (var inst in instructions)
        {
            if (inst.Op != OpCode.Gosub || inst.Arguments.Count == 0) continue;
            string target = inst.Arguments[0].TrimStart('*');
            if (!funcMap.TryGetValue(target, out var func)) continue;

            int argCount = inst.Arguments.Count - 1; // 第1引数は関数名
            int paramCount = func.Parameters.Count;
            if (argCount != paramCount)
            {
                _reporter.Report(new AriaError(
                    $"関数 '{func.QualifiedName}' の引数の数が一致しません。期待: {paramCount}, 実際: {argCount}",
                    inst.SourceLine, scriptFile, AriaErrorLevel.Error));
                continue;
            }

            for (int i = 0; i < paramCount; i++)
            {
                string arg = inst.Arguments[i + 1];
                string paramType = func.Parameters[i].Type.ToLowerInvariant();
                bool argIsStringReg = arg.StartsWith("$");
                bool argIsNumberReg = arg.StartsWith("%");
                bool argIsNumberLiteral = int.TryParse(arg, out int argValue);
                bool argIsStringLiteral = !argIsStringReg && !argIsNumberReg && !argIsNumberLiteral && !string.IsNullOrEmpty(arg);
                bool paramIsString = paramType is "string" or "str";
                bool paramIsInt = paramType is "int" or "integer" or "bool" or "float";

                // 列挙型パラメータのチェック
                if (enumMap.TryGetValue(paramType, out var enumDef))
                {
                    if (argIsNumberLiteral && !enumDef.IsValidValue(argValue))
                    {
                        _reporter.Report(new AriaError(
                            $"関数 '{func.QualifiedName}' の引数 {i + 1} ('{func.Parameters[i].Name}') は列挙型 '{enumDef.Name}' の有効な値が期待されます。",
                            inst.SourceLine, scriptFile, AriaErrorLevel.Error));
                    }
                    else if (argIsStringReg || argIsStringLiteral)
                    {
                        _reporter.Report(new AriaError(
                            $"関数 '{func.QualifiedName}' の引数 {i + 1} ('{func.Parameters[i].Name}') は列挙型 '{enumDef.Name}' が期待されます。",
                            inst.SourceLine, scriptFile, AriaErrorLevel.Error));
                    }
                    continue;
                }

                // 文字列型パラメータに数値リテラル/数値レジスタを渡した場合のみ警告
                if (paramIsString && argIsNumberLiteral)
                {
                    _reporter.Report(new AriaError(
                        $"関数 '{func.QualifiedName}' の引数 {i + 1} ('{func.Parameters[i].Name}') は文字列型が期待されます。",
                        inst.SourceLine, scriptFile, AriaErrorLevel.Warning));
                }
                // 数値型パラメータに文字列レジスタ/文字列リテラルを渡した場合のみ警告
                else if (paramIsInt && (argIsStringReg || argIsStringLiteral))
                {
                    _reporter.Report(new AriaError(
                        $"関数 '{func.QualifiedName}' の引数 {i + 1} ('{func.Parameters[i].Name}') は数値型が期待されます。",
                        inst.SourceLine, scriptFile, AriaErrorLevel.Warning));
                }
            }
        }
    }

    // T14: struct instantiation helpers

    private static List<PreprocessedLine>? TryExpandStructInstantiation(
        string line, int sourceLine,
        Dictionary<string, StructDefinition> structRegistry,
        Dictionary<string, StructDefinition> instanceMap)
    {
        var m = Regex.Match(line, @"^\s*let\s+([%$][A-Za-z_][A-Za-z0-9_]*)\s*,\s*new\s+([A-Za-z_][A-Za-z0-9_]*)\s*\{(.*)\}\s*$", RegexOptions.IgnoreCase);
        if (!m.Success) return null;

        string varName = m.Groups[1].Value;
        string structName = m.Groups[2].Value;
        string fieldsRaw = m.Groups[3].Value;

        if (!structRegistry.TryGetValue(structName, out var def))
            return null;

        instanceMap[varName] = def;
        var fieldAssignments = ParseStructFieldAssignments(fieldsRaw);
        var result = new List<PreprocessedLine>();

        foreach (var field in def.Fields)
        {
            string value = fieldAssignments.ContainsKey(field.Name)
                ? fieldAssignments[field.Name]
                : GetDefaultValue(field.Type);
            string destReg = varName.StartsWith("%")
                ? $"%{varName.Substring(1)}_{field.Name}"
                : $"{varName}_{field.Name}";
            result.Add(new PreprocessedLine($"let {destReg}, {value}", sourceLine));
        }

        return result;
    }

    private static Dictionary<string, string> ParseStructFieldAssignments(string fieldsRaw)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var entries = SplitByCommaQuoteAware(fieldsRaw);
        foreach (var entry in entries)
        {
            var eqIdx = entry.IndexOf('=');
            if (eqIdx < 0) continue;
            var fieldName = entry.Substring(0, eqIdx).Trim();
            var fieldValue = entry.Substring(eqIdx + 1).Trim();
            if (fieldName.StartsWith("%") || fieldName.StartsWith("$"))
                fieldName = fieldName.Substring(1);
            if (!string.IsNullOrEmpty(fieldName))
                result[fieldName] = fieldValue;
        }
        return result;
    }

    private static List<string> SplitByCommaQuoteAware(string text)
    {
        var result = new List<string>();
        int start = 0;
        bool inQuotes = false;
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '"') inQuotes = !inQuotes;
            if (!inQuotes && text[i] == ',')
            {
                result.Add(text.Substring(start, i - start).Trim());
                start = i + 1;
            }
        }
        if (start < text.Length)
            result.Add(text.Substring(start).Trim());
        return result;
    }

    private static string GetDefaultValue(string fieldType)
    {
        return fieldType.ToLowerInvariant() switch
        {
            "string" => "\"\"",
            "bool" => "0",
            "float" => "0.0",
            _ => "0"
        };
    }

    private static string RewriteStructFieldAccess(string line, Dictionary<string, StructDefinition> instanceMap)
    {
        if (instanceMap.Count == 0) return line;

        var sb = new System.Text.StringBuilder(line.Length);
        bool inQuotes = false;

        for (int i = 0; i < line.Length;)
        {
            if (line[i] == '"')
            {
                inQuotes = !inQuotes;
                sb.Append(line[i++]);
                continue;
            }

            if (!inQuotes && (line[i] == '%' || line[i] == '$'))
            {
                int start = i;
                i++;
                while (i < line.Length && (char.IsLetterOrDigit(line[i]) || line[i] == '_')) i++;
                string varName = line.Substring(start, i - start);

                if (i < line.Length && line[i] == '.' && instanceMap.ContainsKey(varName))
                {
                    i++; // skip '.'
                    int fieldStart = i;
                    while (i < line.Length && (char.IsLetterOrDigit(line[i]) || line[i] == '_')) i++;
                    string fieldName = line.Substring(fieldStart, i - fieldStart);
                    string prefix = varName[0].ToString();
                    string baseName = varName.Substring(1);
                    sb.Append($"{prefix}{baseName}_{fieldName}");
                }
                else
                {
                    sb.Append(varName);
                }
            }
            else
            {
                sb.Append(line[i++]);
            }
        }

        return sb.ToString();
    }
}

