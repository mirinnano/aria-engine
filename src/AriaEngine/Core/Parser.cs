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

    public Parser(ErrorReporter reporter)
    {
        _reporter = reporter;
    }


    public ParseResult Parse(string[] lines, string scriptFile = "")
    {
        var result = new ParseResult { SourceLines = lines };
        var (preprocessedLines, functions, structs) = PreprocessModernSyntax(lines);
        result.Functions = functions;
        result.Structs = structs;

        var instructions = new List<Instruction>();
        var labels = new Dictionary<string, int>();
        var defsubs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        
        int ifCounter = 0;
        var ifStack = new Stack<(string elseLabel, string endLabel, Condition cond)>();

        // Pre-pass for Defsubs, Labels, and Functions
        // funcで展開されたdefsubもここで登録
        foreach (var func in functions)
        {
            defsubs.Add(func.QualifiedName);
        }

        for (int i = 0; i < preprocessedLines.Length; i++)
        {
            var rawLine = preprocessedLines[i].Text;
            var line = StripComments(rawLine).TrimStart();
            if (string.IsNullOrEmpty(line)) continue;

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

            if (ShouldTreatAsPlainText(line, defsubs))
            {
                var stmts = SplitStatements(line);
                if (stmts.Count > 1)
                {
                    _reporter.Report(new AriaError(
                        $"テキスト行に':'で区切られた命令が含まれていますが、先頭がテキストとして解釈されたため、後続の命令が無視されます: '{line}'",
                        sourceLine, scriptFile, AriaErrorLevel.Warning));
                }
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
                    int cmdIndex = -1;
                    for (int j = 1; j < parts.Count; j++)
                    {
                        if (CommandRegistry.Contains(parts[j]) || defsubs.Contains(parts[j]))
                        {
                            cmdIndex = j;
                            break;
                        }
                    }

                    if (cmdIndex > 1)
                    {
                        var condTokens = parts.GetRange(1, cmdIndex - 1);
                        var condition = Condition.FromTokens(condTokens);
                        var cmdToken = parts[cmdIndex];
                        var opArgs = parts.Skip(cmdIndex + 1).ToList();

                        if (CommandRegistry.TryGet(cmdToken, out OpCode op))
                            instructions.Add(new Instruction(op, opArgs, sourceLine, condition));
                        else if (defsubs.Contains(cmdToken))
                            instructions.Add(new Instruction(OpCode.Gosub, new List<string> { cmdToken }.Concat(opArgs).ToList(), sourceLine, condition));
                    }
                    else
                    {
                        // ブロックif文
                        var condTokens = parts.Skip(1).ToList();
                        var condition = Condition.FromTokens(condTokens);
                        string elseLbl = $"__if_else_{ifCounter}";
                        string endLbl = $"__if_end_{ifCounter}";
                        ifCounter++;
                        
                        instructions.Add(new Instruction(OpCode.JumpIfFalse, new List<string> { elseLbl }, sourceLine, condition));
                        ifStack.Push((elseLbl, endLbl, condition));
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

        result.Instructions = instructions;
        result.Labels = labels;
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

    private (PreprocessedLine[] Lines, List<FunctionInfo> Functions, List<StructDefinition> Structs) PreprocessModernSyntax(string[] lines)
    {
        var output = new List<PreprocessedLine>(lines.Length);
        var functions = new List<FunctionInfo>();
        var structs = new List<StructDefinition>();
        var constants = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var namespaceStack = new Stack<string>();
        var blockStack = new Stack<string>();
        string? enumName = null;
        int enumNextValue = 0;
        
        // func/struct ブロック処理用
        bool inFuncBlock = false;
        FunctionInfo? currentFunc = null;
        bool inStructBlock = false;
        StructDefinition? currentStruct = null;
        
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

        for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            var rawLine = lines[lineIndex];
            int sourceLine = lineIndex + 1;
            var trimmed = StripComments(rawLine).Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                output.Add(new PreprocessedLine(rawLine, sourceLine));
                continue;
            }

            if (enumName is not null)
            {
                if (trimmed.StartsWith("}", StringComparison.Ordinal))
                {
                    enumName = null;
                    enumNextValue = 0;
                    continue;
                }

                ParseEnumMembers(trimmed, enumName, constants, ref enumNextValue);
                continue;
            }

            if (TryParseConst(trimmed, constants)) continue;
            
            if (TryParseDefineMacro(trimmed, constants)) continue;
            
            if (TryParseDefineEffect(trimmed)) continue;

            if (TryStartEnum(trimmed, constants, ref enumName, ref enumNextValue)) continue;

            if (TryStartNamespace(trimmed, namespaceStack)) continue;
            
            if (TryParseUsing(trimmed, namespaceStack)) continue;

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
                
                // catch キーワード検出
                if (Regex.IsMatch(trimmed, @"^catch\s*\{\s*$", RegexOptions.IgnoreCase))
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
                    continue;
                }
                
                string line = rawLine;
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
            var funcMatch = Regex.Match(trimmed, @"^func\s+([A-Za-z_][A-Za-z0-9_]*)\s*\((.*?)\)\s*(?:->\s*(\w+))?\s*$", RegexOptions.IgnoreCase);
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
                currentFunc = new FunctionInfo
                {
                    QualifiedName = qualifiedName,
                    ShortName = funcName,
                    Namespace = namespaceStack.Count > 0 ? string.Join(".", namespaceStack.Reverse()) : null,
                    ReturnType = returnType,
                    Parameters = new List<ParameterInfo>()
                };
                
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
                            output.Add(new PreprocessedLine($"getparam %{paramName}", sourceLine));
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
                    if (lLine.Contains('{')) braceDepth++;
                    if (lLine.Contains('}')) braceDepth--;
                    if (braceDepth <= 0) break;
                    
                    string procLine = lines[li];
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
                    Namespace = namespaceStack.Count > 0 ? string.Join(".", namespaceStack.Reverse()) : null
                };
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

            string line2 = rawLine;
            var expandedCalls = ExpandFuncStyleCall(line2, namespaceStack);
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

        return (output.ToArray(), functions, structs);
    }

    private static bool TryParseConst(string trimmed, Dictionary<string, string> constants)
    {
        var match = Regex.Match(trimmed, @"^const\s+([A-Za-z_][A-Za-z0-9_.]*)\s*(?:=)?\s*(.+?)\s*;?$", RegexOptions.IgnoreCase);
        if (!match.Success) return false;

        constants[match.Groups[1].Value] = match.Groups[2].Value.Trim();
        return true;
    }
    
    private static bool TryParseDefineMacro(string trimmed, Dictionary<string, string> macros)
    {
        var match = Regex.Match(trimmed, @"^#define\s+([A-Za-z_][A-Za-z0-9_]*)\s+(.+?)$", RegexOptions.IgnoreCase);
        if (!match.Success) return false;

        string name = match.Groups[1].Value;
        string value = match.Groups[2].Value.Trim();
        macros[name] = value;
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

    private static bool TryStartEnum(string trimmed, Dictionary<string, string> constants, ref string? enumName, ref int enumNextValue)
    {
        var match = Regex.Match(trimmed, @"^enum\s+([A-Za-z_][A-Za-z0-9_]*)\s*\{(.*)$", RegexOptions.IgnoreCase);
        if (!match.Success) return false;

        enumName = match.Groups[1].Value;
        enumNextValue = 0;

        string body = match.Groups[2].Value.Trim();
        bool closesInline = body.Contains('}');
        if (closesInline)
        {
            body = body.Substring(0, body.IndexOf('}'));
        }

        ParseEnumMembers(body, enumName, constants, ref enumNextValue);
        if (closesInline)
        {
            enumName = null;
            enumNextValue = 0;
        }

        return true;
    }

    private static void ParseEnumMembers(string body, string enumName, Dictionary<string, string> constants, ref int nextValue)
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

            constants[$"{enumName}.{name}"] = nextValue.ToString();
            nextValue++;
        }
    }

    private static bool TryParseUsing(string trimmed, Stack<string> namespaceStack)
    {
        var match = Regex.Match(trimmed, @"^using\s+([A-Za-z_][A-Za-z0-9_.]*)\s*;?\s*$", RegexOptions.IgnoreCase);
        if (!match.Success) return false;
        
        // using は名前空間検索パスに追加（現在は予約実装）
        // 将来: namespaceStack に加えて usingStack を管理し、名前解決時に検索対象に含める
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
            blockStack.Pop();
            output.Add(new PreprocessedLine("endif", sourceLine));
        }
        else if (namespaceStack.Count > 0)
        {
            namespaceStack.Pop();
        }

        return true;
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
    private static List<string> ExpandFuncStyleCall(string line, Stack<string> namespaceStack)
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
        
        // 名前空間修飾
        string qualifiedName = namespaceStack.Count > 0
            ? string.Join("_", namespaceStack.Reverse()) + "_" + funcName
            : funcName;
        
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
        var ifBlock = Regex.Match(line, @"^(\s*if\s+.+?)\s*\{\s*$", RegexOptions.IgnoreCase);
        if (ifBlock.Success)
        {
            blockStack.Push("if");
            return ifBlock.Groups[1].Value;
        }

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
                    // Handle comparison operators
                    if (i + 1 < line.Length && line[i] == '=' && line[i + 1] == '=')
                    {
                        if (i > start) break;
                        else
                        {
                            tokens.Add("==");
                            i += 2;
                            start = i;
                            continue;
                        }
                    }
                    else if (i + 1 < line.Length && line[i] == '!' && line[i + 1] == '=')
                    {
                        if (i > start) break;
                        else
                        {
                            tokens.Add("!=");
                            i += 2;
                            start = i;
                            continue;
                        }
                    }
                    else if (i + 1 < line.Length && line[i] == '>' && line[i + 1] == '=')
                    {
                        if (i > start) break;
                        else
                        {
                            tokens.Add(">=");
                            i += 2;
                            start = i;
                            continue;
                        }
                    }
                    else if (i + 1 < line.Length && line[i] == '<' && line[i + 1] == '=')
                    {
                        if (i > start) break;
                        else
                        {
                            tokens.Add("<=");
                            i += 2;
                            start = i;
                            continue;
                        }
                    }
                    else if (line[i] == '=')
                    {
                        if (i > start) break;
                        else { i++; break; }
                    }
                    i++;
                }

                string t = line.Substring(start, i - start);
                // Replace \n inside strings as well to support command arguments
                if (!string.IsNullOrEmpty(t))
                {
                    tokens.Add(t.Replace("\\n", "\n"));
                }
            }
        }
        return tokens;
    }
}

