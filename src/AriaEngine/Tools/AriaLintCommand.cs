using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AriaEngine.Assets;
using AriaEngine.Core;
using AriaEngine.Scripting;

namespace AriaEngine.Tools;

/// <summary>
/// Lint rule severity levels
/// </summary>
public enum LintSeverity { Info, Warning, Error }

/// <summary>
/// A single lint issue
/// </summary>
public sealed class LintIssue
{
    public string FilePath { get; }
    public int Line { get; }
    public int Column { get; }
    public LintSeverity Severity { get; }
    public string Rule { get; }
    public string Message { get; }

    public LintIssue(string filePath, int line, int column, LintSeverity severity, string rule, string message)
    {
        FilePath = filePath;
        Line = line;
        Column = column;
        Severity = severity;
        Rule = rule;
        Message = message;
    }

    public override string ToString() =>
        $"{FilePath}:{Line}:{Column}: {(Severity == LintSeverity.Error ? "error" : Severity == LintSeverity.Warning ? "warning" : "info")}: [{Rule}] {Message}";
}

/// <summary>
/// Result of linting a single file
/// </summary>
public sealed class LintResult
{
    public string FilePath { get; }
    public List<LintIssue> Issues { get; } = new();
    public int ErrorCount => Issues.Count(i => i.Severity == LintSeverity.Error);
    public int WarningCount => Issues.Count(i => i.Severity == LintSeverity.Warning);
    public int InfoCount => Issues.Count(i => i.Severity == LintSeverity.Info);

    public LintResult(string filePath) => FilePath = filePath;

    public bool HasErrors => ErrorCount > 0;
    public bool HasWarnings => WarningCount > 0;
    public bool IsClean => Issues.Count == 0;
}

/// <summary>
/// aria-lint CLI tool for static analysis of .aria scripts
/// </summary>
public static class AriaLintCommand
{
    private static readonly Regex VariableReferencePattern = new(@"(?<![\w$])[%$][A-Za-z0-9_]+", RegexOptions.Compiled);

    public static int Run(string[] args)
    {
        var files = new List<string>();
        bool verbose = false;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--help":
                case "-h":
                    PrintHelp();
                    return 0;
                case "--verbose":
                case "-v":
                    verbose = true;
                    break;
                default:
                    if (!args[i].StartsWith("--"))
                        files.Add(args[i]);
                    break;
            }
        }

        if (files.Count == 0)
        {
            Console.Error.WriteLine("aria-lint: no files specified");
            Console.Error.WriteLine("Usage: aria-lint [--verbose] <file.aria> [file2.aria ...]");
            return 2;
        }

        var allResults = new List<LintResult>();

        foreach (var filePath in files)
        {
            var result = LintFile(filePath);
            allResults.Add(result);

            foreach (var issue in result.Issues)
            {
                Console.WriteLine(issue);
            }

            if (verbose && !result.IsClean)
            {
                Console.Error.WriteLine($"  Errors: {result.ErrorCount}, Warnings: {result.WarningCount}, Info: {result.InfoCount}");
            }
        }

        int totalErrors = allResults.Sum(r => r.ErrorCount);
        int totalWarnings = allResults.Sum(r => r.WarningCount);

        Console.Error.WriteLine();
        Console.Error.WriteLine($"Linted {files.Count} file(s): {totalErrors} error(s), {totalWarnings} warning(s)");

        if (totalErrors > 0)
            return 2;
        if (totalWarnings > 0)
            return 1;
        return 0;
    }

    private static void PrintHelp()
    {
        Console.WriteLine("aria-lint - Static analyzer for .aria scripts");
        Console.WriteLine();
        Console.WriteLine("Usage: aria-lint [options] <file.aria> [file2.aria ...]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --help, -h     Show this help");
        Console.WriteLine("  --verbose, -v  Show per-file statistics");
        Console.WriteLine();
        Console.WriteLine("Rules:");
        Console.WriteLine("  undefined-label     Undefined goto/gosub targets");
        Console.WriteLine("  unused-variable     Variables written but never read");
        Console.WriteLine("  function-type-mismatch  Function argument type mismatch");
        Console.WriteLine("  numeric-register-string-assignment  String value assigned to % register");
        Console.WriteLine("  button-result-lifetime  btnwait result read after subroutine/system call");
        Console.WriteLine("  unreachable-code   Code after end/return/goto");
        Console.WriteLine("  sprite-leak         lsp without corresponding csp");
        Console.WriteLine("  E003 - Use-after-move of owned sprite");
        Console.WriteLine("  E004 - Potential double-drop of owned sprite");
        Console.WriteLine("  E010 - Sprite use after owning scope");
        Console.WriteLine("  E013 - Asset handle ownership violations (Pak v3 redesign, Phase 4.1)");
        Console.WriteLine("  asset-preload-group  Invalid asynchronous asset group name");
        Console.WriteLine();
        Console.WriteLine("Exit codes:");
        Console.WriteLine("  0 - Clean (no issues)");
        Console.WriteLine("  1 - Warnings only");
        Console.WriteLine("  2 - Errors found");
    }

    private static LintResult LintFile(string filePath)
    {
        var result = new LintResult(filePath);

        if (!File.Exists(filePath))
        {
            result.Issues.Add(new LintIssue(filePath, 0, 0, LintSeverity.Error, "file-not-found", $"File not found: {filePath}"));
            return result;
        }

        string[] lines = ReadLintLines(filePath);
        var reporter = new ErrorReporter();
        var parser = new Parser(reporter);
        var parseResult = parser.Parse(lines, filePath);

        // Run lint rules
        CheckUndefinedLabels(parseResult, filePath, result);
        CheckUnusedVariables(parseResult, filePath, result);
        CheckFunctionTypeMismatch(parseResult, filePath, result);
        CheckRegisterTypeHazards(parseResult, filePath, result);
        CheckButtonResultLifetime(parseResult, filePath, result);
        CheckUnreachableCode(parseResult, filePath, result);
        CheckSpriteLeak(parseResult, filePath, result);
        // New sprite lifetime rules
        CheckSpriteUseAfterScope(parseResult, filePath, result);
        CheckDoubleDrop(parseResult, filePath, result);
        CheckSpriteMoveInvalidation(parseResult, filePath, result);
        CheckUndefinedVariables(parseResult, filePath, result);
        CheckReadonlyAssignment(parseResult, filePath, result);
        CheckOwnedSpriteEscape(parseResult, filePath, result);
        CheckBorrowViolation(parseResult, filePath, result);
        CheckEnumUndefinedValue(parseResult, filePath, result);
        CheckFuncReturnValue(parseResult, filePath, result);
        CheckImplicitTypeConversion(parseResult, filePath, result);
        CheckUninitializedVariable(parseResult, filePath, result);
        // Pak v3 redesign, Phase 4.1: aria-lint E013 (asset handle ownership).
        CheckAssetHandleLoadWithoutDeclaration(parseResult, filePath, result);
        CheckAssetHandleDoubleLoad(parseResult, filePath, result);
        CheckAssetHandleUseAfterScope(parseResult, filePath, result);
        CheckAssetPreloadGroups(parseResult, filePath, result);

        return result;
    }

    private static void CheckAssetPreloadGroups(ParseResult parseResult, string filePath, LintResult result)
    {
        foreach (Instruction inst in parseResult.Instructions.Where(item => item.Op == OpCode.AssetPreload))
        {
            if (inst.Arguments.Count != 1)
            {
                result.Issues.Add(new LintIssue(
                    filePath,
                    inst.SourceLine,
                    0,
                    LintSeverity.Error,
                    "asset-preload-group",
                    "asset_preload requires exactly one group name."));
                continue;
            }

            string groupName = inst.Arguments[0].Trim();
            if (!Regex.IsMatch(groupName, @"^[A-Za-z0-9][A-Za-z0-9._-]*$"))
            {
                result.Issues.Add(new LintIssue(
                    filePath,
                    inst.SourceLine,
                    0,
                    LintSeverity.Error,
                    "asset-preload-group",
                    $"Invalid asset group name '{groupName}'. Use letters, digits, '.', '_' or '-'."));
            }
        }
    }

    private static string[] ReadLintLines(string filePath)
    {
        string[] rawLines = File.ReadAllLines(filePath);
        if (!rawLines.Any(IsIncludeDirective))
            return rawLines;

        string fullPath = Path.GetFullPath(filePath);
        string root = Path.GetDirectoryName(fullPath) ?? Directory.GetCurrentDirectory();
        string scriptName = Path.GetFileName(fullPath);
        var provider = new DiskAssetProvider(root);
        return ScriptPreprocessor.ExpandIncludes(scriptName, provider).Lines;
    }

    private static bool IsIncludeDirective(string line)
    {
        return line.TrimStart().StartsWith("include ", StringComparison.OrdinalIgnoreCase);
    }

    private static void CheckUndefinedLabels(ParseResult parseResult, string filePath, LintResult result)
    {
        // Collect all valid targets (labels + defsubs)
        var validTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var label in parseResult.Labels.Keys)
            validTargets.Add(label);
        foreach (var func in parseResult.Functions)
            validTargets.Add(func.QualifiedName);

        // Check jumps and gosubs
        foreach (var inst in parseResult.Instructions)
        {
            if (inst.Op == OpCode.Jmp || inst.Op == OpCode.Beq || inst.Op == OpCode.Bne ||
                inst.Op == OpCode.Bgt || inst.Op == OpCode.Blt || inst.Op == OpCode.Gosub)
            {
                if (inst.Arguments.Count == 0) continue;

                string target;
                if (inst.Op == OpCode.Gosub)
                {
                    target = inst.Arguments[0].TrimStart('*');
                }
                else
                {
                    // Jmp/Beq/Bne/Bgt/Blt - first arg is target
                    target = inst.Arguments[0].TrimStart('*');
                }

                if (!validTargets.Contains(target))
                {
                    result.Issues.Add(new LintIssue(
                        filePath, inst.SourceLine, 0, LintSeverity.Error, "undefined-label",
                        $"Undefined label or function '{target}'"));
                }
            }
        }
    }

    private static void CheckUnusedVariables(ParseResult parseResult, string filePath, LintResult result)
    {
        // Track which variables are written and read
        var written = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var read = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Regex to detect variable references
        var regVar = new Regex(@"%(\w+)");
        var stringVar = new Regex(@"\$(\w+)");

        foreach (var inst in parseResult.Instructions)
        {
            // Track writes: Let, Mov (first arg), Add/Sub/Mul/Div/Mod (first arg)
            switch (inst.Op)
            {
                case OpCode.Let:
                case OpCode.Mov:
                    if (inst.Arguments.Count > 0)
                        TrackVariable(inst.Arguments[0], written);
                    break;
                case OpCode.Add:
                case OpCode.Sub:
                case OpCode.Mul:
                case OpCode.Div:
                case OpCode.Mod:
                case OpCode.Inc:
                case OpCode.Dec:
                case OpCode.SetArray:
                    if (inst.Arguments.Count > 0)
                        TrackVariable(inst.Arguments[0], written);
                    break;
            }

            // Track reads: all instruction arguments EXCEPT the first arg of write operations
            // For single-arg modify ops (inc/dec), the arg is both written AND read
            // For Let/Mov: first arg is write target (not read), rest are read
            // For Add/Sub/etc: all args are read (even first)
            bool isSingleArgModify = inst.Op == OpCode.Inc || inst.Op == OpCode.Dec;
            int readStartIndex = (inst.Op == OpCode.Let || inst.Op == OpCode.Mov || inst.Op == OpCode.SetArray) ? 1 : 0;

            for (int ai = readStartIndex; ai < inst.Arguments.Count; ai++)
            {
                var arg = inst.Arguments[ai];
                foreach (var rx in new[] { regVar, stringVar })
                {
                    foreach (Match m in rx.Matches(arg))
                    {
                        read.Add(m.Groups[0].Value);
                    }
                }
            }
            // For inc/dec, the single arg is both written AND read
            if (isSingleArgModify && inst.Arguments.Count > 0)
            {
                var arg = inst.Arguments[0];
                foreach (var rx in new[] { regVar, stringVar })
                {
                    foreach (Match m in rx.Matches(arg))
                    {
                        read.Add(m.Groups[0].Value);
                    }
                }
            }
        }

        TrackVariablesReadFromIfConditions(parseResult.SourceLines, read, regVar, stringVar);

        // Report variables that are written but never read
        foreach (var w in written)
        {
            if (!read.Contains(w) && !IsBuiltinVariable(w))
            {
                // Find first write instruction for line number
                int firstWriteLine = 0;
                foreach (var inst in parseResult.Instructions)
                {
                    if (inst.Op == OpCode.Let || inst.Op == OpCode.Mov ||
                        inst.Op == OpCode.Add || inst.Op == OpCode.Sub ||
                        inst.Op == OpCode.Mul || inst.Op == OpCode.Div ||
                        inst.Op == OpCode.Mod || inst.Op == OpCode.Inc ||
                        inst.Op == OpCode.Dec || inst.Op == OpCode.SetArray)
                    {
                        if (inst.Arguments.Count > 0 && inst.Arguments[0].Equals(w, StringComparison.OrdinalIgnoreCase))
                        {
                            firstWriteLine = inst.SourceLine;
                            break;
                        }
                    }
                    if (firstWriteLine > 0) break;
                }

                result.Issues.Add(new LintIssue(
                    filePath, firstWriteLine, 0, LintSeverity.Warning, "unused-variable",
                    $"Variable '{w}' is written but never read"));
            }
        }
    }

    private static void TrackVariable(string expr, HashSet<string> set)
    {
        // Handle array access: %arr[0] -> %arr
        string var = expr.Trim();
        int bracket = var.IndexOf('[');
        if (bracket > 0)
            var = var.Substring(0, bracket);
        if (!string.IsNullOrEmpty(var))
            set.Add(var);
    }

    private static bool IsBuiltinVariable(string var)
    {
        // Skip %result, %0-%9 that might be used for temporary purposes
        return var.Equals("%result", StringComparison.OrdinalIgnoreCase);
    }

    private static void TrackVariablesReadFromIfConditions(
        string[] sourceLines,
        HashSet<string> read,
        Regex regVar,
        Regex stringVar)
    {
        foreach (string line in sourceLines)
        {
            string trimmed = line.TrimStart();
            if (!trimmed.StartsWith("if ", StringComparison.OrdinalIgnoreCase))
                continue;

            int bodyStart = trimmed.IndexOf('{');
            string condition = bodyStart >= 0 ? trimmed[..bodyStart] : trimmed;
            foreach (var rx in new[] { regVar, stringVar })
            {
                foreach (Match m in rx.Matches(condition))
                {
                    read.Add(m.Groups[0].Value);
                }
            }
        }
    }

    private static void CheckFunctionTypeMismatch(ParseResult parseResult, string filePath, LintResult result)
    {
        // Build function signature map
        var funcMap = new Dictionary<string, FunctionInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (var func in parseResult.Functions)
        {
            funcMap[func.QualifiedName] = func;
            if (!string.IsNullOrEmpty(func.ShortName))
                funcMap[func.ShortName] = func;
        }

        // Track current function scope for parameter validation
        var funcByPc = new Dictionary<int, FunctionInfo>();
        foreach (var func in parseResult.Functions)
        {
            if (func.EntryPC >= 0)
                funcByPc[func.EntryPC] = func;
        }

        // Find instructions that are function calls and check arguments
        for (int i = 0; i < parseResult.Instructions.Count; i++)
        {
            var inst = parseResult.Instructions[i];

            // Gosub with function-style name (qualified or short)
            if (inst.Op == OpCode.Gosub && inst.Arguments.Count > 0)
            {
                string funcName = inst.Arguments[0].TrimStart('*');
                if (funcMap.TryGetValue(funcName, out var funcInfo))
                {
                    int argCount = inst.Arguments.Count - 1; // first arg is the function name
                    int paramCount = funcInfo.Parameters.Count;

                    if (argCount != paramCount)
                    {
                        result.Issues.Add(new LintIssue(
                            filePath, inst.SourceLine, 0, LintSeverity.Error, "function-type-mismatch",
                            $"Function '{funcName}' expects {paramCount} argument(s) but got {argCount}"));
                    }
                    else
                    {
                        // Check type compatibility for each argument
                        for (int j = 0; j < argCount; j++)
                        {
                            string arg = inst.Arguments[j + 1];
                            string expectedType = funcInfo.Parameters[j].Type;

                            if (!string.IsNullOrEmpty(expectedType) && expectedType != "void")
                            {
                                bool isRegister = arg.StartsWith("%") || arg.StartsWith("$");
                                bool typeOk = expectedType switch
                                {
                                    "int" => arg.StartsWith("%") || int.TryParse(arg, out _),
                                    "string" => arg.StartsWith("$") || (arg.StartsWith("\"") && arg.EndsWith("\"")) || !arg.StartsWith("%"),
                                    "float" => arg.StartsWith("%") || float.TryParse(arg, out _),
                                    "bool" => arg.StartsWith("%") || arg == "0" || arg == "1" || arg == "true" || arg == "false",
                                    _ => true // unknown type, skip check
                                };

                                if (!isRegister && !typeOk)
                                {
                                    result.Issues.Add(new LintIssue(
                                        filePath, inst.SourceLine, 0, LintSeverity.Warning, "function-type-mismatch",
                                        $"Argument {j + 1} of '{funcName}': expected {expectedType}, got '{arg}'"));
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    private static void CheckRegisterTypeHazards(ParseResult parseResult, string filePath, LintResult result)
    {
        foreach (var inst in parseResult.Instructions)
        {
            if (inst.Op is not (OpCode.Let or OpCode.Mov) || inst.Arguments.Count < 2)
                continue;

            string target = inst.Arguments[0].Trim();
            if (!IsNumericRegisterTarget(target))
                continue;

            if (inst.Arguments.Skip(1).Any(IsStringExpression))
            {
                result.Issues.Add(new LintIssue(
                    filePath,
                    inst.SourceLine,
                    0,
                    LintSeverity.Warning,
                    "numeric-register-string-assignment",
                    $"String-like value assigned to numeric register '{target}'. Use a $ string register instead."));
            }
        }
    }

    private static void CheckButtonResultLifetime(ParseResult parseResult, string filePath, LintResult result)
    {
        bool buttonResultLive = false;
        bool invalidatedByCall = false;
        var reportedLines = new HashSet<int>();

        foreach (var inst in parseResult.Instructions)
        {
            if (invalidatedByCall && ReadsRegister(inst, "%0") && reportedLines.Add(inst.SourceLine))
            {
                result.Issues.Add(new LintIssue(
                    filePath,
                    inst.SourceLine,
                    0,
                    LintSeverity.Warning,
                    "button-result-lifetime",
                    "The btnwait result in %0 is read after a subroutine/system call that may overwrite it. Copy it to a named register before the call."));
            }

            if (inst.Op == OpCode.Jmp)
            {
                invalidatedByCall = false;
                continue;
            }

            if (inst.Op == OpCode.BtnWait && inst.Arguments.Count > 0 && IsRegister(inst.Arguments[0], "%0"))
            {
                buttonResultLive = true;
                invalidatedByCall = false;
                continue;
            }

            if (inst.Op is OpCode.Let or OpCode.Mov &&
                inst.Arguments.Count > 0 &&
                IsRegister(inst.Arguments[0], "%0"))
            {
                buttonResultLive = false;
                invalidatedByCall = false;
                continue;
            }

            if (buttonResultLive && IsPotentiallyClobberingCall(inst))
                invalidatedByCall = true;
        }
    }

    private static bool IsNumericRegisterTarget(string token)
    {
        string value = token.Trim();
        int bracket = value.IndexOf('[');
        if (bracket > 0)
            value = value[..bracket];
        return value.StartsWith("%", StringComparison.Ordinal);
    }

    private static bool IsStringExpression(string expression)
    {
        string value = expression.Trim();
        return (value.StartsWith("$", StringComparison.Ordinal) && !value.StartsWith("${", StringComparison.Ordinal)) ||
               Regex.IsMatch(value, "\"[^\"]*\"");
    }

    private static bool IsPotentiallyClobberingCall(Instruction inst)
    {
        return inst.Op is OpCode.Gosub or OpCode.SystemCall;
    }

    private static bool ReadsRegister(Instruction inst, string register)
    {
        foreach (string token in inst.Condition.ToTokenList())
        {
            if (TokenContainsRegister(token, register))
                return true;
        }

        int readStartIndex = inst.Op is OpCode.Let or OpCode.Mov or OpCode.SetArray or OpCode.BtnWait ? 1 : 0;
        for (int i = readStartIndex; i < inst.Arguments.Count; i++)
        {
            if (TokenContainsRegister(inst.Arguments[i], register))
                return true;
        }

        return inst.Op is OpCode.Inc or OpCode.Dec &&
               inst.Arguments.Count > 0 &&
               TokenContainsRegister(inst.Arguments[0], register);
    }

    private static bool TokenContainsRegister(string token, string register)
    {
        return Regex.IsMatch(token, $@"(?<![\w$%]){Regex.Escape(register)}(?![\w])");
    }

    private static bool IsRegister(string token, string register)
    {
        string value = token.Trim();
        int bracket = value.IndexOf('[');
        if (bracket > 0)
            value = value[..bracket];
        return value.Equals(register, StringComparison.OrdinalIgnoreCase);
    }

    private static void CheckUnreachableCode(ParseResult parseResult, string filePath, LintResult result)
    {
        var labels = parseResult.Labels;
        var instructions = parseResult.Instructions;

        // Terms that end a block when written as standalone source statements.
        var terminalOps = new HashSet<OpCode>
        {
            OpCode.End,
            OpCode.Return,
            OpCode.ReturnValue,
            OpCode.Throw,
            OpCode.Panic
        };

        // Build a set of instruction indices that are labels
        var labelIndices = new HashSet<int>();
        foreach (var kvp in labels)
        {
            if (kvp.Value >= 0)
                labelIndices.Add(kvp.Value);
        }
        foreach (var func in parseResult.Functions)
        {
            if (func.EntryPC >= 0)
                labelIndices.Add(func.EntryPC);
        }

        for (int i = 0; i < instructions.Count; i++)
        {
            var inst = instructions[i];

            // Check if this instruction ends control flow
            bool isTerminal = IsExplicitTerminal(inst, parseResult);

            // Explicit goto ends sequential flow; generated jumps from if/for/function lowering do not.
            if (!isTerminal && IsExplicitGoto(inst, parseResult))
            {
                isTerminal = true;
            }

            if (!isTerminal)
                continue;

            // Look at subsequent instructions until next label
            if (i + 1 < instructions.Count && !labelIndices.Contains(i + 1))
            {
                var next = instructions[i + 1];
                if (next.Op == OpCode.Defsub)
                    continue;

                // If it's another terminal, stop checking
                if (terminalOps.Contains(next.Op) || IsExplicitGoto(next, parseResult))
                    { }
                else
                {
                    // Any other instruction after terminal is unreachable
                    result.Issues.Add(new LintIssue(
                        filePath, next.SourceLine, 0, LintSeverity.Warning, "unreachable-code",
                        $"Unreachable instruction after 'end' or 'return'"));
                }
            }
        }
    }

    private static bool IsExplicitTerminal(Instruction inst, ParseResult parseResult)
    {
        string? keyword = inst.Op switch
        {
            OpCode.End => "end",
            OpCode.Return => "return",
            OpCode.ReturnValue => "return",
            OpCode.Throw => "throw",
            OpCode.Panic => "panic",
            _ => null
        };

        if (keyword is null)
            return false;

        int lineIndex = inst.SourceLine - 1;
        if (lineIndex < 0 || lineIndex >= parseResult.SourceLines.Length)
            return true;

        return parseResult.SourceLines[lineIndex]
            .TrimStart()
            .StartsWith(keyword, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsExplicitGoto(Instruction inst, ParseResult parseResult)
    {
        if (inst.Op != OpCode.Jmp)
            return false;

        int lineIndex = inst.SourceLine - 1;
        if (lineIndex < 0 || lineIndex >= parseResult.SourceLines.Length)
            return true;

        return parseResult.SourceLines[lineIndex]
            .TrimStart()
            .StartsWith("goto ", StringComparison.OrdinalIgnoreCase);
    }

    private static void CheckSpriteLeak(ParseResult parseResult, string filePath, LintResult result)
    {
        // Track lsp (load sprite) and csp (clear sprite) for each sprite ID
        var spriteIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var closedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var inst in parseResult.Instructions)
        {
            switch (inst.Op)
            {
                case OpCode.Lsp:
                case OpCode.LspText:
                case OpCode.LspRect:
                    if (inst.Arguments.Count > 0)
                    {
                        spriteIds.Add(inst.Arguments[0]);
                    }
                    break;
                case OpCode.Csp:
                    if (inst.Arguments.Count > 0)
                    {
                        closedIds.Add(inst.Arguments[0]);
                    }
                    else
                    {
                        // csp without args clears all - no leak possible
                        spriteIds.Clear();
                    }
                    break;
                case OpCode.Vsp:
                    // vsp hides but doesn't close
                    break;
            }
        }

        // Report sprites that were loaded but never closed
        foreach (var id in spriteIds)
        {
            if (!closedIds.Contains(id))
            {
                // Find the lsp instruction for line number
                int lspLine = 0;
                foreach (var inst in parseResult.Instructions)
                {
                    if ((inst.Op == OpCode.Lsp || inst.Op == OpCode.LspText || inst.Op == OpCode.LspRect)
                        && inst.Arguments.Count > 0 && inst.Arguments[0] == id)
                    {
                        lspLine = inst.SourceLine;
                        break;
                    }
                }

                result.Issues.Add(new LintIssue(
                    filePath, lspLine, 0, LintSeverity.Info, "sprite-leak",
                    $"Sprite '{id}' loaded but never explicitly cleared with 'csp' (potential leak)"));
            }
        }
    }

    // New lint rules for sprite lifetime and ownership
    private static void CheckSpriteUseAfterScope(ParseResult parseResult, string filePath, LintResult result)
    {
        if (parseResult.OwnedSprites == null || parseResult.OwnedSprites.Count == 0)
            return;

        // For each owned sprite, track first usage scope and subsequent scope violations
        var firstUsageScope = new Dictionary<string, StorageScope>(StringComparer.OrdinalIgnoreCase);
        var firstUsageLine = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var inst in parseResult.Instructions)
        {
            // Check all arguments for sprite usage
            foreach (var arg in inst.Arguments)
            {
                if (!parseResult.OwnedSprites.Contains(arg)) continue;
                if (!firstUsageScope.ContainsKey(arg))
                {
                    firstUsageScope[arg] = inst.Scope;
                    firstUsageLine[arg] = inst.SourceLine;
                }
                else
                {
                    // If used in a different scope than first usage, raise error
                    if (inst.Scope != firstUsageScope[arg])
                    {
                        result.Issues.Add(new LintIssue(
                            filePath,
                            inst.SourceLine,
                            0,
                            LintSeverity.Error,
                            "E010",
                            $"Sprite '{arg}' used outside its owning scope"));
                    }
                }
            }
        }
    }

    private static void CheckDoubleDrop(ParseResult parseResult, string filePath, LintResult result)
    {
        if (parseResult.OwnedSprites == null || parseResult.OwnedSprites.Count == 0)
            return;

        // When a CSP is performed on an owned sprite, a later end of scope might re-drop it.
        foreach (var inst in parseResult.Instructions)
        {
            if ((inst.Op == OpCode.Csp) && inst.Arguments.Count > 0)
            {
                string sprite = inst.Arguments[0];
                if (!parseResult.OwnedSprites.Contains(sprite)) continue;

                // Look ahead for end of scope indicators
                for (int i = parseResult.Instructions.IndexOf(inst) + 1; i < parseResult.Instructions.Count; i++)
                {
                    var later = parseResult.Instructions[i];
                    if (later.Op == OpCode.End || later.Op == OpCode.Return || later.Op == OpCode.ReturnValue || later.Op == OpCode.ScopeExit)
                    {
                        result.Issues.Add(new LintIssue(
                            filePath,
                            inst.SourceLine,
                            0,
                            LintSeverity.Warning,
                            "E004",
                            $"Possible double-drop: owned sprite '{sprite}' cleared while scope may still end later."));
                        break;
                    }
                }
            }
        }
    }

    private static void CheckSpriteMoveInvalidation(ParseResult parseResult, string filePath, LintResult result)
    {
        // Detect ownership move: let @y = @x or similar two-argument ownership transfer
        var movedFrom = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var inst in parseResult.Instructions)
        {
            if (inst.Op == OpCode.Let || inst.Op == OpCode.Mov)
            {
                if (inst.Arguments.Count >= 2)
                {
                    string a = inst.Arguments[0];
                    string b = inst.Arguments[1];
                    if (a.StartsWith("@") && b.StartsWith("@"))
                    {
                        movedFrom.Add(b);
                    }
                }
            }

            // After a move from X to Y, if X is used again, report error
            foreach (var arg in inst.Arguments)
            {
                if (movedFrom.Contains(arg))
                {
                    // Do not report on the first encounter (it's the move); report on subsequent use
                    result.Issues.Add(new LintIssue(
                        filePath,
                        inst.SourceLine,
                        0,
                        LintSeverity.Error,
                            "E003",
                            $"Sprite ownership moved-from '{arg}' is used after move; invalidated lifetime."));
                    // Do not flood with multiple reports for the same line
                }
            }
        }
    }

    private static void CheckUndefinedVariables(ParseResult parseResult, string filePath, LintResult result)
    {
        var declaredVars = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        // 組み込み変数は常に有効
        for (int i = 0; i < 1000; i++)
            declaredVars.Add("%" + i);

        foreach (var inst in parseResult.Instructions)
        {
            var writeIndexes = GetVariableWriteArgumentIndexes(inst).ToHashSet();
            foreach (int writeIndex in writeIndexes)
            {
                if (writeIndex >= 0 && writeIndex < inst.Arguments.Count && IsVariableToken(inst.Arguments[writeIndex]))
                {
                    declaredVars.Add(NormalizeVariableToken(inst.Arguments[writeIndex]));
                }
            }

            for (int i = 0; i < inst.Arguments.Count; i++)
            {
                if (writeIndexes.Contains(i)) continue;

                foreach (string variable in EnumerateVariableReferences(inst.Arguments[i]))
                {
                    if (!declaredVars.Contains(variable))
                    {
                        result.Issues.Add(new LintIssue(
                            filePath, inst.SourceLine, 0, LintSeverity.Error, "E002",
                            $"Undefined variable '{variable}'"));
                    }
                }
            }
        }
    }

    private static IEnumerable<int> GetVariableWriteArgumentIndexes(Instruction inst)
    {
        switch (inst.Op)
        {
            case OpCode.Let:
            case OpCode.Mov:
            case OpCode.SetArray:
            case OpCode.GetArray:
            case OpCode.Rnd:
            case OpCode.For:
            case OpCode.GetTimer:
            case OpCode.BtnWait:
            case OpCode.BacklogCount:
            case OpCode.GalleryCount:
            case OpCode.GetConfig:
            case OpCode.GetLanguage:
            case OpCode.LocGet:
            case OpCode.LocFormat:
            case OpCode.LangCount:
            case OpCode.GetProfile:
                yield return 0;
                break;

            case OpCode.LangAt:
                yield return 1;
                break;

            case OpCode.Getparam:
                for (int i = 0; i < inst.Arguments.Count; i++) yield return i;
                break;

            case OpCode.GetFlag:
            case OpCode.GetPFlag:
            case OpCode.GetSFlag:
            case OpCode.GetVFlag:
            case OpCode.GetCounter:
                yield return inst.Arguments.Count > 1 ? 1 : 0;
                break;

            case OpCode.BacklogEntry:
                yield return 1;
                break;

            case OpCode.SaveInfo:
            case OpCode.GalleryInfo:
                yield return 1;
                yield return 2;
                yield return 3;
                break;
        }
    }

    private static IEnumerable<string> EnumerateVariableReferences(string arg)
    {
        if (IsLiteralOrLabel(arg) || IsInterpolationToken(arg))
            yield break;

        foreach (Match match in VariableReferencePattern.Matches(arg))
        {
            yield return NormalizeVariableToken(match.Value);
        }
    }

    private static string NormalizeVariableToken(string token)
    {
        string value = token.Trim();
        int bracket = value.IndexOf('[');
        if (bracket > 0) value = value[..bracket];
        return value;
    }

    private static bool IsLiteralOrLabel(string token)
    {
        if (string.IsNullOrEmpty(token)) return true;
        if (token.StartsWith("*")) return true; // ラベル
        if (token.StartsWith("\"")) return true; // 文字列リテラル
        if (int.TryParse(token, out _)) return true; // 数値リテラル
        return false;
    }

    private static bool IsVariableToken(string token)
    {
        if (string.IsNullOrEmpty(token)) return false;
        if (IsInterpolationToken(token)) return false;
        if (token is "%" or "$" or "@" or "&") return false;
        return token.StartsWith("%") || token.StartsWith("$") || token.StartsWith("@") || token.StartsWith("&");
    }

    private static bool IsInterpolationToken(string token)
    {
        return token.TrimStart().StartsWith("${", StringComparison.Ordinal);
    }

    private static void CheckReadonlyAssignment(ParseResult parseResult, string filePath, LintResult result)
    {
        if (parseResult.ReadonlyDeclarations == null || parseResult.ReadonlyDeclarations.Count == 0)
            return;

        var readonlyVars = new HashSet<string>(parseResult.ReadonlyDeclarations.Select(r => r.VariableName), StringComparer.OrdinalIgnoreCase);

        foreach (var inst in parseResult.Instructions)
        {
            // 変更命令でreadonly変数が代入先になっていないか
            if (inst.Op == OpCode.Let || inst.Op == OpCode.Mov || inst.Op == OpCode.Add ||
                inst.Op == OpCode.Sub || inst.Op == OpCode.Mul || inst.Op == OpCode.Div ||
                inst.Op == OpCode.Mod || inst.Op == OpCode.Inc || inst.Op == OpCode.Dec)
            {
                if (inst.Arguments.Count > 0)
                {
                    string target = inst.Arguments[0];
                    if (readonlyVars.Contains(target))
                    {
                        result.Issues.Add(new LintIssue(
                            filePath, inst.SourceLine, 0, LintSeverity.Error, "E006",
                            $"Cannot assign to readonly variable '{target}'"));
                    }
                }
            }
        }
    }

    private static void CheckOwnedSpriteEscape(ParseResult parseResult, string filePath, LintResult result)
    {
        if (parseResult.OwnedSprites == null || parseResult.OwnedSprites.Count == 0)
            return;

        foreach (var inst in parseResult.Instructions)
        {
            if (inst.Op == OpCode.Return || inst.Op == OpCode.ReturnValue)
            {
                foreach (var arg in inst.Arguments)
                {
                    if (parseResult.OwnedSprites.Contains(arg))
                    {
                        result.Issues.Add(new LintIssue(
                            filePath, inst.SourceLine, 0, LintSeverity.Error, "E012",
                            $"Cannot return owned sprite '{arg}' from scope: it will be dropped"));
                    }
                }
            }
        }
    }

    private static void CheckBorrowViolation(ParseResult parseResult, string filePath, LintResult result)
    {
        // Detect mutable borrow conflicts: borrow @mut = @x while @x is already borrowed
        var activeBorrows = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var inst in parseResult.Instructions)
        {
            // Heuristic: if instruction text contains "borrow", track it
            if (inst.Arguments.Count >= 2 && inst.Arguments[0].StartsWith("@") && inst.Arguments[1].StartsWith("@"))
            {
                string source = inst.Arguments[1];
                if (activeBorrows.Contains(source))
                {
                    result.Issues.Add(new LintIssue(
                        filePath, inst.SourceLine, 0, LintSeverity.Error, "E005",
                        $"Cannot borrow '{source}' while it is already borrowed"));
                }
                activeBorrows.Add(source);
            }
        }
    }

    private static void CheckEnumUndefinedValue(ParseResult parseResult, string filePath, LintResult result)
    {
        if (parseResult.Enums == null || parseResult.Enums.Count == 0)
            return;

        var enumMap = new Dictionary<string, EnumDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in parseResult.Enums)
            enumMap[e.Name] = e;

        foreach (var inst in parseResult.Instructions)
        {
            foreach (var arg in inst.Arguments)
            {
                if (arg.Contains('.'))
                {
                    var parts = arg.Split('.');
                    if (parts.Length == 2)
                    {
                        string enumName = parts[0];
                        string memberName = parts[1];
                        if (enumMap.TryGetValue(enumName, out var enumDef))
                        {
                            if (!enumDef.Members.ContainsKey(memberName))
                            {
                                result.Issues.Add(new LintIssue(
                                    filePath, inst.SourceLine, 0, LintSeverity.Error, "E009",
                                    $"Enum '{enumName}' has no variant '{memberName}'"));
                            }
                        }
                    }
                }
            }
        }
    }

    private static void CheckFuncReturnValue(ParseResult parseResult, string filePath, LintResult result)
    {
        if (parseResult.Functions == null || parseResult.Functions.Count == 0)
            return;

        // Sort functions by EntryPC to determine ranges
        var sortedFuncs = parseResult.Functions.OrderBy(f => f.EntryPC).ToList();

        for (int i = 0; i < sortedFuncs.Count; i++)
        {
            var func = sortedFuncs[i];
            if (string.IsNullOrEmpty(func.ReturnType) || func.ReturnType.Equals("void", StringComparison.OrdinalIgnoreCase))
                continue;

            int startPc = func.EntryPC;
            int endPc = (i + 1 < sortedFuncs.Count) ? sortedFuncs[i + 1].EntryPC : parseResult.Instructions.Count;

            bool hasReturn = false;
            for (int pc = startPc; pc < endPc && pc < parseResult.Instructions.Count; pc++)
            {
                var inst = parseResult.Instructions[pc];
                if (inst.Op == OpCode.Return || inst.Op == OpCode.ReturnValue)
                {
                    hasReturn = true;
                    break;
                }
            }

            if (!hasReturn)
            {
                // Use EntryPC to get source line if available
                int line = (startPc < parseResult.Instructions.Count) ? parseResult.Instructions[startPc].SourceLine : 0;
                result.Issues.Add(new LintIssue(
                    filePath, line, 0, LintSeverity.Error, "E011",
                    $"Function '{func.QualifiedName}' with return type '{func.ReturnType}' may not return a value on all paths"));
            }
        }
    }

    private static void CheckImplicitTypeConversion(ParseResult parseResult, string filePath, LintResult result)
    {
        foreach (var inst in parseResult.Instructions)
        {
            // Check conditional instructions: if %x == 1 (where %x is used as boolean/flag)
            if (inst.Op == OpCode.Beq || inst.Op == OpCode.Bne)
            {
                foreach (var arg in inst.Arguments)
                {
                    if (arg.StartsWith("%") && (arg.Equals("%0", StringComparison.OrdinalIgnoreCase) || arg.Equals("%1", StringComparison.OrdinalIgnoreCase)))
                    {
                        result.Issues.Add(new LintIssue(
                            filePath, inst.SourceLine, 0, LintSeverity.Warning, "W001",
                            $"Implicit type conversion: int variable '{arg}' used where flag is expected"));
                    }
                }
            }
        }
    }

    private static void CheckUninitializedVariable(ParseResult parseResult, string filePath, LintResult result)
    {
        var uninitializedVars = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var inst in parseResult.Instructions)
        {
            // Detect let/mov with only one argument (declaration without initialization)
            if ((inst.Op == OpCode.Let || inst.Op == OpCode.Mov) && inst.Arguments.Count == 1)
            {
                string varName = inst.Arguments[0];
                if (IsVariableToken(varName))
                    uninitializedVars.Add(varName);
            }

            // If an uninitialized variable is used as a source (not target), warn
            int startIndex = (inst.Op == OpCode.Let || inst.Op == OpCode.Mov || inst.Op == OpCode.Getparam) ? 1 : 0;
            for (int i = startIndex; i < inst.Arguments.Count; i++)
            {
                string arg = inst.Arguments[i];
                if (uninitializedVars.Contains(arg))
                {
                    result.Issues.Add(new LintIssue(
                        filePath, inst.SourceLine, 0, LintSeverity.Warning, "W008",
                        $"Uninitialized variable '{arg}' is used before assignment"));
                    uninitializedVars.Remove(arg); // warn once
                }
            }
        }
    }

    // ──────────────────────────────────────────────────────────────────────
    // Pak v3 redesign, Phase 4.1: aria-lint E013 — asset handle ownership
    //   E013-1 (W013): load_aria_asset without `owned asset <var>` declaration
    //   E013-2 (W013): same result_var loaded twice (overwrites first handle)
    //   E013-3 (E013): owned asset handle used outside its owning scope
    //
    // Note: re-borrow / double-dispose / move-after-borrow are runtime
    // properties of AssetHandle<T>; static analysis only catches the
    // structural patterns above. The borrow/move opcodes themselves land
    // in a later phase.
    // ──────────────────────────────────────────────────────────────────────

    private static readonly Regex AssetHandleRefPattern =
        new(@"@[A-Za-z_][A-Za-z0-9_]*", RegexOptions.Compiled);

    private static void CheckAssetHandleLoadWithoutDeclaration(ParseResult parseResult, string filePath, LintResult result)
    {
        var declaredOwned = parseResult.OwnedSprites != null
            ? new HashSet<string>(parseResult.OwnedSprites, StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var inst in parseResult.Instructions)
        {
            if (inst.Op != OpCode.LoadAsset) continue;
            if (inst.Arguments.Count < 2) continue;  // arity is checked elsewhere

            string resultVar = inst.Arguments[1];
            if (!IsVariableToken(resultVar)) continue;

            if (!declaredOwned.Contains(resultVar))
            {
                result.Issues.Add(new LintIssue(
                    filePath, inst.SourceLine, 0, LintSeverity.Warning, "W013",
                    $"load_aria_asset: result var '{resultVar}' is not declared as `owned asset` upstream; the handle will not be auto-disposed on scope exit"));
            }
        }
    }

    private static void CheckAssetHandleDoubleLoad(ParseResult parseResult, string filePath, LintResult result)
    {
        var seenLoad = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var inst in parseResult.Instructions)
        {
            if (inst.Op != OpCode.LoadAsset) continue;
            if (inst.Arguments.Count < 2) continue;

            string resultVar = inst.Arguments[1];
            if (!IsVariableToken(resultVar)) continue;

            if (seenLoad.TryGetValue(resultVar, out int firstLine))
            {
                result.Issues.Add(new LintIssue(
                    filePath, inst.SourceLine, 0, LintSeverity.Warning, "W013",
                    $"load_aria_asset: result var '{resultVar}' is loaded a second time (first load at line {firstLine}); the first handle is overwritten without Dispose"));
            }
            else
            {
                seenLoad[resultVar] = inst.SourceLine;
            }
        }
    }

    private static void CheckAssetHandleUseAfterScope(ParseResult parseResult, string filePath, LintResult result)
    {
        if (parseResult.OwnedSprites == null || parseResult.OwnedSprites.Count == 0)
            return;

        // Asset handle keys look like @name; sprite keys look like %name.
        // Filter to asset-only declarations.
        var declaredOwnedAssets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in parseResult.OwnedSprites)
        {
            if (s != null && s.StartsWith("@")) declaredOwnedAssets.Add(s);
        }
        if (declaredOwnedAssets.Count == 0) return;

        // For each owned asset var, track first usage scope and flag cross-scope use.
        var firstUsageScope = new Dictionary<string, StorageScope>(StringComparer.OrdinalIgnoreCase);

        foreach (var inst in parseResult.Instructions)
        {
            // Only look at arguments that mention an asset handle.
            foreach (var arg in inst.Arguments)
            {
                if (arg == null) continue;
                if (!declaredOwnedAssets.Contains(arg)) continue;
                if (!firstUsageScope.TryGetValue(arg, out var scope))
                {
                    firstUsageScope[arg] = inst.Scope;
                }
                else if (inst.Scope != scope)
                {
                    result.Issues.Add(new LintIssue(
                        filePath, inst.SourceLine, 0, LintSeverity.Error, "E013",
                        $"Asset handle '{arg}' is used outside its owning scope (declared scope: {scope}, used scope: {inst.Scope})"));
                }
            }
        }
    }
}
