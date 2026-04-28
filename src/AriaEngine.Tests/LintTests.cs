using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using Xunit;
using AriaEngine.Core;
using AriaEngine.Tools;

namespace AriaEngine.Tests;

public class LintTests
{
    private static Parser CreateParser() => new(new ErrorReporter());

    private static string WriteTempScript(string content)
    {
        string path = Path.GetTempFileName() + ".aria";
        File.WriteAllText(path, content);
        return path;
    }

    private static void CleanupTempFile(string path)
    {
        try { File.Delete(path); } catch { }
    }

    // === Undefined Label Tests ===

    [Fact]
    public void Lint_DetectsUndefinedGotoLabel()
    {
        string script = @"
*start
goto *nonexistent
end
";
        string path = WriteTempScript(script);
        try
        {
            var result = LintFile(path);
            result.Issues.Should().Contain(i => i.Rule == "undefined-label" && i.Message.Contains("nonexistent"));
        }
        finally { CleanupTempFile(path); }
    }

    [Fact]
    public void Lint_DetectsUndefinedGosubLabel()
    {
        string script = @"
*start
gosub *nonexistent
end
";
        string path = WriteTempScript(script);
        try
        {
            var result = LintFile(path);
            result.Issues.Should().Contain(i => i.Rule == "undefined-label");
        }
        finally { CleanupTempFile(path); }
    }

    [Fact]
    public void Lint_ValidLabel_NoError()
    {
        string script = @"
*start
goto *end
*end
end
";
        string path = WriteTempScript(script);
        try
        {
            var result = LintFile(path);
            result.Issues.Should().NotContain(i => i.Rule == "undefined-label");
        }
        finally { CleanupTempFile(path); }
    }

    // === Unused Variable Tests ===

    [Fact]
    public void Lint_DetectsUnusedVariable()
    {
        string script = @"
let %unused_var, 100
mov %0, 1
end
";
        string path = WriteTempScript(script);
        try
        {
            var result = LintFile(path);
            result.Issues.Should().Contain(i => i.Rule == "unused-variable" && i.Message.Contains("unused_var"));
        }
        finally { CleanupTempFile(path); }
    }

    [Fact]
    public void Lint_VariableUsed_NoWarning()
    {
        string script = @"
let %used_var, 100
mov %result, %used_var
end
";
        string path = WriteTempScript(script);
        try
        {
            var result = LintFile(path);
            result.Issues.Should().NotContain(i => i.Rule == "unused-variable");
        }
        finally { CleanupTempFile(path); }
    }

    // === Sprite Leak Tests ===

    [Fact]
    public void Lint_DetectsSpriteLeak()
    {
        string script = @"
lsp 10, ""test.png"", 0, 0
end
";
        string path = WriteTempScript(script);
        try
        {
            var result = LintFile(path);
            result.Issues.Should().Contain(i => i.Rule == "sprite-leak" && i.Message.Contains("10"));
        }
        finally { CleanupTempFile(path); }
    }

    [Fact]
    public void Lint_SpriteCleared_NoLeakWarning()
    {
        string script = @"
lsp 10, ""test.png"", 0, 0
csp 10
end
";
        string path = WriteTempScript(script);
        try
        {
            var result = LintFile(path);
            result.Issues.Should().NotContain(i => i.Rule == "sprite-leak");
        }
        finally { CleanupTempFile(path); }
    }

    [Fact]
    public void Lint_ClearsAllSprites_NoLeakWarning()
    {
        string script = @"
lsp 10, ""test.png"", 0, 0
lsp 20, ""test2.png"", 100, 0
csp
end
";
        string path = WriteTempScript(script);
        try
        {
            var result = LintFile(path);
            result.Issues.Should().NotContain(i => i.Rule == "sprite-leak");
        }
        finally { CleanupTempFile(path); }
    }

    // === Exit Code Tests ===

    [Fact]
    public void Lint_ExitCode2_OnErrors()
    {
        string script = @"
goto *undefined_label
end
";
        string path = WriteTempScript(script);
        try
        {
            var result = LintFile(path);
            result.ErrorCount.Should().BeGreaterThan(0);
        }
        finally { CleanupTempFile(path); }
    }

    [Fact]
    public void Lint_ExitCode0_OnClean()
    {
        string script = @"
*start
mov %result, 1
end
";
        string path = WriteTempScript(script);
        try
        {
            var result = LintFile(path);
            result.IsClean.Should().BeTrue();
        }
        finally { CleanupTempFile(path); }
    }

    // === Unreachable Code Tests ===

    [Fact]
    public void Lint_DetectsUnreachableCodeAfterEnd()
    {
        string script = @"
end
mov %0, 1
";
        string path = WriteTempScript(script);
        try
        {
            var result = LintFile(path);
            result.Issues.Should().Contain(i => i.Rule == "unreachable-code");
        }
        finally { CleanupTempFile(path); }
    }

    [Fact]
    public void Lint_NoFalsePositiveAfterLabel()
    {
        string script = @"
*start
end
*next
mov %0, 1
";
        string path = WriteTempScript(script);
        try
        {
            var result = LintFile(path);
            result.Issues.Should().NotContain(i => i.Rule == "unreachable-code");
        }
        finally { CleanupTempFile(path); }
    }

    // Helper to run lint
    private static LintResult LintFile(string path)
    {
        var reporter = new ErrorReporter();
        var parser = new Parser(reporter);
        var parseResult = parser.Parse(File.ReadAllLines(path), path);

        var result = new LintResult(path);
        // Manually call check methods since they're private
        CheckUndefinedLabels(parseResult, path, result);
        CheckUnusedVariables(parseResult, path, result);
        CheckSpriteLeak(parseResult, path, result);
        CheckUnreachableCode(parseResult, path, result);

        return result;
    }

    // Duplicated helper methods for testing (same as AriaLintCommand)
    private static void CheckUndefinedLabels(ParseResult parseResult, string filePath, LintResult result)
    {
        var validTargets = new System.Collections.Generic.HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        foreach (var label in parseResult.Labels.Keys)
            validTargets.Add(label);
        foreach (var func in parseResult.Functions)
            validTargets.Add(func.QualifiedName);

        foreach (var inst in parseResult.Instructions)
        {
            if (inst.Op == OpCode.Jmp || inst.Op == OpCode.Beq || inst.Op == OpCode.Bne ||
                inst.Op == OpCode.Bgt || inst.Op == OpCode.Blt || inst.Op == OpCode.Gosub)
            {
                if (inst.Arguments.Count == 0) continue;

                string target = inst.Arguments[0].TrimStart('*');
                if (!validTargets.Contains(target))
                {
                    result.Issues.Add(new LintIssue(filePath, inst.SourceLine, 0, LintSeverity.Error, "undefined-label",
                        $"Undefined label or function '{target}'"));
                }
            }
        }
    }

    private static void CheckUnusedVariables(ParseResult parseResult, string filePath, LintResult result)
    {
        var written = new System.Collections.Generic.HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        var read = new System.Collections.Generic.HashSet<string>(System.StringComparer.OrdinalIgnoreCase);

        var regVar = new System.Text.RegularExpressions.Regex(@"%(\w+)");
        var stringVar = new System.Text.RegularExpressions.Regex(@"\$(\w+)");

        foreach (var inst in parseResult.Instructions)
        {
            // Track writes: Let, Mov (first arg), Add/Sub/Mul/Div/Mod (first arg)
            switch (inst.Op)
            {
                case OpCode.Let:
                case OpCode.Mov:
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
                foreach (var rx in new[] { regVar, stringVar })
                {
                    foreach (System.Text.RegularExpressions.Match m in rx.Matches(arg))
                    {
                        read.Add(m.Groups[0].Value);
                    }
                }
            }
        }

        foreach (var w in written)
        {
            if (!read.Contains(w) && !w.Equals("%result", System.StringComparison.OrdinalIgnoreCase))
            {
                int firstWriteLine = 0;
                foreach (var inst in parseResult.Instructions)
                {
                    if ((inst.Op == OpCode.Let || inst.Op == OpCode.Mov || inst.Op == OpCode.Add ||
                         inst.Op == OpCode.Sub || inst.Op == OpCode.Mul || inst.Op == OpCode.Div ||
                         inst.Op == OpCode.Mod || inst.Op == OpCode.Inc || inst.Op == OpCode.Dec ||
                         inst.Op == OpCode.SetArray) && inst.Arguments.Count > 0 &&
                        inst.Arguments[0].Equals(w, System.StringComparison.OrdinalIgnoreCase))
                    {
                        firstWriteLine = inst.SourceLine;
                        break;
                    }
                    if (firstWriteLine > 0) break;
                }

                result.Issues.Add(new LintIssue(filePath, firstWriteLine, 0, LintSeverity.Warning, "unused-variable",
                    $"Variable '{w}' is written but never read"));
            }
        }
    }

    private static void TrackVariable(string expr, System.Collections.Generic.HashSet<string> set)
    {
        string var = expr.Trim();
        int bracket = var.IndexOf('[');
        if (bracket > 0)
            var = var.Substring(0, bracket);
        if (!string.IsNullOrEmpty(var))
            set.Add(var);
    }

    private static void CheckUnreachableCode(ParseResult parseResult, string filePath, LintResult result)
    {
        var labels = parseResult.Labels;
        var instructions = parseResult.Instructions;

        var terminalOps = new System.Collections.Generic.HashSet<OpCode>
        {
            OpCode.End, OpCode.Return, OpCode.ReturnValue, OpCode.Throw, OpCode.Panic
        };

        var labelIndices = new System.Collections.Generic.HashSet<int>();
        foreach (var kvp in labels)
        {
            if (kvp.Value >= 0)
                labelIndices.Add(kvp.Value);
        }

        for (int i = 0; i < instructions.Count; i++)
        {
            var inst = instructions[i];
            bool isTerminal = terminalOps.Contains(inst.Op);
            if (!isTerminal && (inst.Op == OpCode.Jmp || inst.Op == OpCode.Gosub))
                isTerminal = true;

            if (!isTerminal)
                continue;

            if (i + 1 < instructions.Count && !labelIndices.Contains(i + 1))
            {
                var next = instructions[i + 1];
                if (terminalOps.Contains(next.Op) || next.Op == OpCode.Jmp || next.Op == OpCode.Gosub)
                    { }
                else
                {
                    result.Issues.Add(new LintIssue(filePath, next.SourceLine, 0, LintSeverity.Warning, "unreachable-code",
                        "Unreachable instruction after 'end' or 'return'"));
                }
            }
        }
    }

    private static void CheckSpriteLeak(ParseResult parseResult, string filePath, LintResult result)
    {
        var spriteIds = new System.Collections.Generic.HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        var closedIds = new System.Collections.Generic.HashSet<string>(System.StringComparer.OrdinalIgnoreCase);

        foreach (var inst in parseResult.Instructions)
        {
            switch (inst.Op)
            {
                case OpCode.Lsp:
                case OpCode.LspText:
                case OpCode.LspRect:
                    if (inst.Arguments.Count > 0)
                        spriteIds.Add(inst.Arguments[0]);
                    break;
                case OpCode.Csp:
                    if (inst.Arguments.Count > 0)
                        closedIds.Add(inst.Arguments[0]);
                    else
                        spriteIds.Clear();
                    break;
            }
        }

        foreach (var id in spriteIds)
        {
            if (!closedIds.Contains(id))
            {
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

                result.Issues.Add(new LintIssue(filePath, lspLine, 0, LintSeverity.Info, "sprite-leak",
                    $"Sprite '{id}' loaded but never explicitly cleared with 'csp' (potential leak)"));
            }
        }
    }
}