using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AriaEngine.Core;

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
        Console.WriteLine("  unreachable-code   Code after end/return/goto");
        Console.WriteLine("  sprite-leak         lsp without corresponding csp");
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

        string[] lines = File.ReadAllLines(filePath);
        var reporter = new ErrorReporter();
        var parser = new Parser(reporter);
        var parseResult = parser.Parse(lines, filePath);

        // Run lint rules
        CheckUndefinedLabels(parseResult, filePath, result);
        CheckUnusedVariables(parseResult, filePath, result);
        CheckFunctionTypeMismatch(parseResult, filePath, result);
        CheckUnreachableCode(parseResult, filePath, result);
        CheckSpriteLeak(parseResult, filePath, result);

        return result;
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
        var regVar = new System.Text.RegularExpressions.Regex(@"%(\w+)");
        var stringVar = new System.Text.RegularExpressions.Regex(@"\$(\w+)");

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
                foreach (System.Text.RegularExpressions.Regex rx in new[] { regVar, stringVar })
                {
                    foreach (System.Text.RegularExpressions.Match m in rx.Matches(arg))
                    {
                        read.Add(m.Groups[0].Value);
                    }
                }
            }
            // For inc/dec, the single arg is both written AND read
            if (isSingleArgModify && inst.Arguments.Count > 0)
            {
                var arg = inst.Arguments[0];
                foreach (System.Text.RegularExpressions.Regex rx in new[] { regVar, stringVar })
                {
                    foreach (System.Text.RegularExpressions.Match m in rx.Matches(arg))
                    {
                        read.Add(m.Groups[0].Value);
                    }
                }
            }
        }

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
                                    "string" => arg.StartsWith("$") || (arg.StartsWith("\"") && arg.EndsWith("\"")),
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

private static void CheckUnreachableCode(ParseResult parseResult, string filePath, LintResult result)
    {
        var labels = parseResult.Labels;
        var instructions = parseResult.Instructions;

        // Terms that end a block: End, Return, Gosub, etc.
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

        for (int i = 0; i < instructions.Count; i++)
        {
            var inst = instructions[i];

            // Check if this instruction ends control flow
            bool isTerminal = terminalOps.Contains(inst.Op);

            // Jump instructions also end sequential flow
            if (!isTerminal && (inst.Op == OpCode.Jmp || inst.Op == OpCode.Gosub))
            {
                isTerminal = true;
            }

            if (!isTerminal)
                continue;

            // Look at subsequent instructions until next label
            if (i + 1 < instructions.Count && !labelIndices.Contains(i + 1))
            {
                var next = instructions[i + 1];

                // If it's another terminal, stop checking
                if (terminalOps.Contains(next.Op) || next.Op == OpCode.Jmp || next.Op == OpCode.Gosub)
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
}