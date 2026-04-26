using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace AriaEngine.Core;

public class Parser
{
    private readonly ErrorReporter _reporter;
    private static readonly System.Text.RegularExpressions.Regex DialogRegex = new System.Text.RegularExpressions.Regex(@"^([^「]+?)「(.*?)」(\\?|@?)$", System.Text.RegularExpressions.RegexOptions.Compiled);

    public Parser(ErrorReporter reporter)
    {
        _reporter = reporter;
    }


    public (List<Instruction> Instructions, Dictionary<string, int> Labels) Parse(string[] lines, string scriptFile = "")
    {
        var preprocessedLines = PreprocessModernSyntax(lines);

        var instructions = new List<Instruction>();
        var labels = new Dictionary<string, int>();
        var defsubs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        
        int ifCounter = 0;
        var ifStack = new Stack<(string elseLabel, string endLabel, IReadOnlyList<string> cond)>();

        // Pre-pass for Defsubs and Labels
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
                        var cmdToken = parts[cmdIndex];
                        var opArgs = parts.Skip(cmdIndex + 1).ToList();

                        if (CommandRegistry.TryGet(cmdToken, out OpCode op))
                            instructions.Add(new Instruction(op, opArgs, sourceLine, condTokens));
                        else if (defsubs.Contains(cmdToken))
                            instructions.Add(new Instruction(OpCode.Gosub, new List<string> { cmdToken }.Concat(opArgs).ToList(), sourceLine, condTokens));
                    }
                    else
                    {
                        // ブロックif文
                        var condTokens = parts.Skip(1).ToList();
                        string elseLbl = $"__if_else_{ifCounter}";
                        string endLbl = $"__if_end_{ifCounter}";
                        ifCounter++;
                        
                        instructions.Add(new Instruction(OpCode.JumpIfFalse, new List<string> { elseLbl }, sourceLine, condTokens));
                        ifStack.Push((elseLbl, endLbl, condTokens));
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
                        ifStack.Push(("", popped.endLabel, null!));
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

        return (instructions, labels);
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

    private PreprocessedLine[] PreprocessModernSyntax(string[] lines)
    {
        var output = new List<PreprocessedLine>(lines.Length);
        var constants = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var namespaceStack = new Stack<string>();
        var blockStack = new Stack<string>();
        string? enumName = null;
        int enumNextValue = 0;

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

            if (TryStartEnum(trimmed, constants, ref enumName, ref enumNextValue)) continue;

            if (TryStartNamespace(trimmed, namespaceStack)) continue;

            if (TryCloseModernBlock(trimmed, namespaceStack, blockStack, output, sourceLine)) continue;

            string line = rawLine;
            line = RewriteFunctionStyleCall(line);
            line = ReplaceConstantsOutsideQuotes(line, constants);
            line = RewriteCppIf(line);
            line = RewriteModernBlockOpen(line, blockStack);
            line = RewriteNamespaceLabels(line, namespaceStack);
            output.Add(new PreprocessedLine(line, sourceLine));
        }

        while (blockStack.Count > 0)
        {
            blockStack.Pop();
            output.Add(new PreprocessedLine("endif", lines.Length));
        }

        return output.ToArray();
    }

    private static bool TryParseConst(string trimmed, Dictionary<string, string> constants)
    {
        var match = Regex.Match(trimmed, @"^const\s+([A-Za-z_][A-Za-z0-9_.]*)\s*(?:=)?\s*(.+?)\s*;?$", RegexOptions.IgnoreCase);
        if (!match.Success) return false;

        constants[match.Groups[1].Value] = match.Groups[2].Value.Trim();
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

        var callMatch = Regex.Match(line, @"^(\s*)([A-Za-z_][A-Za-z0-9_]*)\s*\((.*)\)\s*;?\s*$");
        if (!callMatch.Success) return line;

        string args = callMatch.Groups[3].Value.Trim();
        return args.Length == 0
            ? $"{callMatch.Groups[1].Value}{callMatch.Groups[2].Value}"
            : $"{callMatch.Groups[1].Value}{callMatch.Groups[2].Value} {args}";
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
        
        for (int i = 0; i < line.Length; i++)
        {
            if (line[i] == '"') inQuotes = !inQuotes;
            if (!inQuotes && line[i] == ':' && !IsUiTargetColon(line, i))
            {
                result.Add(line.Substring(start, i - start).Trim());
                start = i + 1;
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
                while (i < line.Length && line[i] != '"') i++;
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

