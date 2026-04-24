using System;
using System.Collections.Generic;
using System.Linq;

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
        var instructions = new List<Instruction>();
        var labels = new Dictionary<string, int>();
        var defsubs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        
        int ifCounter = 0;
        var ifStack = new Stack<(string elseLabel, string endLabel, IReadOnlyList<string> cond)>();

        // Pre-pass for Defsubs and Labels
        for (int i = 0; i < lines.Length; i++)
        {
            var rawLine = lines[i];
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

        for (int i = 0; i < lines.Length; i++)
        {
            var rawLine = lines[i];
            var line = StripComments(rawLine).TrimStart();

            if (string.IsNullOrEmpty(line)) continue;

            if (ShouldTreatAsPlainText(line, defsubs))
            {
                AddTextInstructions(instructions, line, i + 1);
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
                            instructions.Add(new Instruction(op, opArgs, i + 1, condTokens));
                        else if (defsubs.Contains(cmdToken))
                            instructions.Add(new Instruction(OpCode.Gosub, new List<string> { cmdToken }.Concat(opArgs).ToList(), i + 1, condTokens));
                    }
                    else
                    {
                        // ブロックif文
                        var condTokens = parts.Skip(1).ToList();
                        string elseLbl = $"__if_else_{ifCounter}";
                        string endLbl = $"__if_end_{ifCounter}";
                        ifCounter++;
                        
                        instructions.Add(new Instruction(OpCode.JumpIfFalse, new List<string> { elseLbl }, i + 1, condTokens));
                        ifStack.Push((elseLbl, endLbl, condTokens));
                    }
                    continue;
                }
                else if (firstToken.Equals("else", StringComparison.OrdinalIgnoreCase))
                {
                    if (ifStack.Count > 0)
                    {
                        var popped = ifStack.Pop();
                        instructions.Add(new Instruction(OpCode.Jmp, new List<string> { popped.endLabel }, i + 1));
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
                    instructions.Add(new Instruction(statementOp, args, i + 1));
                }
                else if (defsubs.Contains(firstToken))
                {
                    instructions.Add(new Instruction(OpCode.Gosub, new List<string> { firstToken }.Concat(parts.Skip(1)).ToList(), i + 1));
                }
                else
                {
                    AddTextInstructions(instructions, stmt, i + 1);
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
            if (!inQuotes && line[i] == ':')
            {
                result.Add(line.Substring(start, i - start).Trim());
                start = i + 1;
            }
        }
        result.Add(line.Substring(start).Trim());
        return result;
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
                    if (i + 1 < line.Length && line.Substring(i, 2) == "==")
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
                    else if (i + 1 < line.Length && line.Substring(i, 2) == "!=")
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
                    else if (i + 1 < line.Length && line.Substring(i, 2) == ">=")
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
                    else if (i + 1 < line.Length && line.Substring(i, 2) == "<=")
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

