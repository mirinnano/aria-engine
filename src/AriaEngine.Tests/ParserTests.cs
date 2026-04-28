using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using Xunit.Abstractions;
using Xunit;
using AriaEngine.Core;
using AriaEngine.Rendering;

namespace AriaEngine.Tests;

public class ParserTests
{
    private static Parser CreateParser() => new(new ErrorReporter());

    

    [Fact]
    public void Parse_EmptyScript_ReturnsEmptyInstructions()
    {
        var result = CreateParser().Parse(Array.Empty<string>(), "empty.aria");
        result.Instructions.Should().BeEmpty();
        result.Labels.Should().BeEmpty();
    }

    [Fact]
    public void Parse_SimpleCommand_CreatesInstructionWithArguments()
    {
        var result = CreateParser().Parse(new[] { "mov %0, 100" }, "simple.aria");

        result.Instructions.Should().ContainSingle();
        result.Instructions[0].Op.Should().Be(OpCode.Mov);
        result.Instructions[0].Arguments.Should().Equal("%0", "100");
    }

    [Fact]
    public void Parse_Label_ResolvesToInstructionIndex()
    {
        var result = CreateParser().Parse(new[]
        {
            "*start",
            "mov %0, 1",
            "*end",
            "end"
        }, "labels.aria");

        result.Labels.Should().ContainKey("start");
        result.Labels.Should().ContainKey("end");
        result.Labels["start"].Should().Be(0);
        result.Labels["end"].Should().Be(1);
    }

    [Fact]
    public void Parse_DialogSyntax_ExpandsToTextAndWait()
    {
        var result = CreateParser().Parse(new[] { "Character「Hello」" }, "dialog.aria");

        result.Instructions.Should().Contain(i => i.Op == OpCode.TextClear);
        result.Instructions.Should().Contain(i => i.Op == OpCode.Text && i.Arguments[0].Contains("Character"));
        result.Instructions.Should().Contain(i => i.Op == OpCode.WaitClickClear);
    }

    [Fact]
    public void Parse_PlainTextWithColon_DoesNotSplitAsCommand()
    {
        var result = CreateParser().Parse(new[] { "本文: コロンを含む文章\\" }, "colon.aria");

        result.Instructions.Should().Contain(i => i.Op == OpCode.Text && i.Arguments[0].Contains(":"));
        result.Instructions.Should().Contain(i => i.Op == OpCode.WaitClickClear);
    }

    [Fact]
    public void Parse_InlineIfCommand_ProducesConditionalJump()
    {
        var result = CreateParser().Parse(new[] { "if %0 == 1 goto *ok", "*ok", "end" }, "if.aria");

        // 式評価システム導入後: if %0 == 1 goto は条件付き Jmp に展開される
        result.Instructions.Should().Contain(i => i.Op == OpCode.Jmp && !i.Condition.IsEmpty);
        result.Instructions.Should().Contain(i => i.Op == OpCode.End);
    }

    [Fact]
    public void Parse_OneLineIfBlockWithoutCommand_DoesNotEmitUnresolvedJump()
    {
        var result = CreateParser().Parse(new[] { "if %0 == 1 { %0 + 1 }", "end" }, "if_block.aria");

        result.Instructions.Should().NotContain(i =>
            i.Op == OpCode.JumpIfFalse &&
            i.Arguments.Count > 0 &&
            !result.Labels.ContainsKey(i.Arguments[0].TrimStart('*')));
    }

    [Fact]
    public void Parse_UnclosedWhile_ReportsErrorInsteadOfLeavingUnresolvedJump()
    {
        var reporter = new ErrorReporter();
        var result = new Parser(reporter).Parse(new[] { "while %0 < 3", "inc %0" }, "while.aria");

        reporter.Errors.Count.Should().BeGreaterThan(0);
        result.Instructions.Should().NotContain(i =>
            i.Op == OpCode.JumpIfFalse &&
            i.Arguments.Count > 0 &&
            !result.Labels.ContainsKey(i.Arguments[0].TrimStart('*')));
    }

    [Fact]
    public void ExpressionParser_TryParse_RejectsTrailingTokens()
    {
        ExpressionParser.TryParse(new[] { "1", "2" }).Should().BeNull();
    }

    [Fact]
    public void Parse_MainAriaPatterns_BackwardCompatible()
    {
        // main.aria で使われている if/while パターンが正しくパースされることを確認
        var script = new[]
        {
            "func cls()",
            "    textclear",
            "endfunc",
            "*start",
            "if %0 == 1 { goto *target }",
            "if %0 == 2 { systemcall load }",
            "if %0 == 3 { cls() }",
            "if %10 == 1",
            "    mov %1, 100",
            "else",
            "    mov %1, 200",
            "endif",
            "if %11 == 1 { ui_button 301, 101 }",
            "while %0 < 3",
            "    inc %0",
            "wend",
            "*target",
            "end"
        };

        var reporter = new ErrorReporter();
        var result = new Parser(reporter).Parse(script, "compat.aria");

        // 関数呼び出しスタイルの cls() は defsubs に登録されているためエラーにならない
        reporter.Errors.Should().BeEmpty($"互換性テストで予期しないエラー: {string.Join(", ", reporter.Errors.Select(e => e.Message))}");
        result.Instructions.Count.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Parse_ScopeBlocks_GenerateScopeEnterAndExitOpcodes()
    {
        var script = new[] { "scope", "end_scope", "end" };
        var result = CreateParser().Parse(script, "scope.aria");
        result.Instructions.Should().Contain(i => i.Op == OpCode.ScopeEnter);
        result.Instructions.Should().Contain(i => i.Op == OpCode.ScopeExit);
    }

    [Fact]
    public void Parse_LocalGlobalDeclaration_PopulatesDeclaredVariablesAndEmitsLet()
    {
        var parser = CreateParser();
        var result = parser.Parse(new[] { "local %x = 100", "global $title = \"Game\"" }, "decl.aria");

        // Should emit at least two instructions corresponding to the declarations
        result.Instructions.Should().Contain(i => i.Op == OpCode.Let);
        // DeclaredVariables should record both declarations
        result.DeclaredVariables.Should().ContainKey("%x");
        result.DeclaredVariables["%x"].Should().Be("local");
        result.DeclaredVariables.Should().ContainKey("$title");
        result.DeclaredVariables["$title"].Should().Be("global");
    }

    [Fact]
    public void Expression_StringComparison_UsesStringValues()
    {
        var vm = new VirtualMachine(new ErrorReporter(), new TweenManager(), new SaveManager(new ErrorReporter()), new ConfigManager());
        vm.State.StringRegisters["name"] = "ayu";

        var equal = ExpressionParser.TryParse(new[] { "$name", "==", "nayuki" });

        equal.Should().NotBeNull();
        equal!.EvaluateInt(vm.State, vm).Should().Be(0);
    }

    // ===========================================================
    // if/while ブロック vs インライン境界のテスト
    // ===========================================================

    [Fact]
    public void Parse_BlockIf_WithEndif_ProducesJumpIfFalseAndLabels()
    {
        var result = CreateParser().Parse(new[]
        {
            "if %0 == 1",
            "    mov %1, 100",
            "endif",
            "end"
        }, "block_if.aria");

        // JumpIfFalse が生成され、endif ラベルが解決されている
        result.Instructions.Should().Contain(i => i.Op == OpCode.JumpIfFalse);
        result.Labels.Should().ContainKey("__if_end_0");
    }

    [Fact]
    public void Parse_BlockIf_WithElse_ProducesElseAndEndLabels()
    {
        var result = CreateParser().Parse(new[]
        {
            "if %0 == 1",
            "    mov %1, 100",
            "else",
            "    mov %1, 200",
            "endif",
            "end"
        }, "block_if_else.aria");

        result.Instructions.Should().Contain(i => i.Op == OpCode.JumpIfFalse);
        result.Instructions.Should().Contain(i => i.Op == OpCode.Jmp); // else から endif へ
        result.Labels.Should().ContainKey("__if_else_0");
        result.Labels.Should().ContainKey("__if_end_0");
    }

    [Fact]
    public void Parse_BlockIf_WithBraces_PreprocessedToEndif()
    {
        var result = CreateParser().Parse(new[]
        {
            "if %0 == 1 {",
            "    mov %1, 100",
            "}",
            "end"
        }, "block_if_braces.aria");

        // 前処理で { } が endif に変換されるため、ブロックifと同じ構造になる
        result.Instructions.Should().Contain(i => i.Op == OpCode.JumpIfFalse);
        result.Labels.Should().ContainKey("__if_end_0");
    }

    [Fact]
    public void Parse_BlockIf_WithBracesAndElse_PreprocessedCorrectly()
    {
        var result = CreateParser().Parse(new[]
        {
            "if %0 == 1 {",
            "    mov %1, 100",
            "} else {",
            "    mov %1, 200",
            "}",
            "end"
        }, "block_if_braces_else.aria");

        result.Instructions.Should().Contain(i => i.Op == OpCode.JumpIfFalse);
        result.Instructions.Should().Contain(i => i.Op == OpCode.Jmp);
        result.Labels.Should().ContainKey("__if_else_0");
        result.Labels.Should().ContainKey("__if_end_0");
    }

    [Fact]
    public void Parse_DeferCommand_ParsesAtomsCorrectly()
    {
        var reporter = new ErrorReporter();
        var parser = new Parser(reporter);
        var script = new[] { "scope", "defer lsp 1001 sprite.png 0 0", "end_scope" };

        var result = parser.Parse(script, "defer_parse.aria");

        result.Instructions.Should().Contain(i => i.Op == OpCode.Defer);
        var deferInst = result.Instructions.First(i => i.Op == OpCode.Defer);
        deferInst.Arguments.Should().Contain("lsp");
        deferInst.Arguments.Should().Contain("1001");
        deferInst.Arguments.Should().Contain("sprite.png");
    }

    [Fact]
    public void Parse_OneLineIfBlock_WithValidCommand_ProducesConditionalInstruction()
    {
        var result = CreateParser().Parse(new[]
        {
            "if %0 == 1 { mov %1, 100 }",
            "end"
        }, "one_line_if.aria");

        // 1行ブロックは条件付き mov として展開される（前処理を通らず Parse が直接処理）
        var movInst = result.Instructions.Should().ContainSingle(i => i.Op == OpCode.Mov).Subject;
        movInst.Condition.IsEmpty.Should().BeFalse();
        movInst.Arguments.Should().Equal("%1", "100");
    }

    [Fact]
    public void Parse_OneLineIfBlock_WithGoto_ProducesConditionalJmp()
    {
        var result = CreateParser().Parse(new[]
        {
            "if %0 == 1 { goto *target }",
            "*target",
            "end"
        }, "one_line_if_goto.aria");

        var jmpInst = result.Instructions.Should().ContainSingle(i => i.Op == OpCode.Jmp).Subject;
        jmpInst.Condition.IsEmpty.Should().BeFalse();
    }

    [Fact]
    public void Parse_OneLineIfBlock_WithInvalidCommand_ReportsError()
    {
        var reporter = new ErrorReporter();
        var result = new Parser(reporter).Parse(new[]
        {
            "if %0 == 1 { not_a_command }",
            "end"
        }, "one_line_if_invalid.aria");

        reporter.Errors.Count.Should().BeGreaterThan(0);
        result.Instructions.Should().NotContain(i => i.Op == OpCode.JumpIfFalse);
    }

    [Fact]
    public void Parse_InlineIf_WithoutBraces_ProducesConditionalInstruction()
    {
        var result = CreateParser().Parse(new[]
        {
            "if %0 == 1 mov %1, 100",
            "end"
        }, "inline_if.aria");

        var movInst = result.Instructions.Should().ContainSingle(i => i.Op == OpCode.Mov).Subject;
        movInst.Condition.IsEmpty.Should().BeFalse();
        movInst.Arguments.Should().Equal("%1", "100");
    }

    [Fact]
    public void Parse_ResultOption_Constructors_TranspileToValAndErr()
    {
        var result = CreateParser().Parse(new[] {
            "let %r, Ok(5)",
            "let %r2, Err(\"not found\")",
            "let %o, Some(42)",
            "let %o2, None",
            "end"
        }, "t11.aria");

        var lets = result.Instructions.Where(i => i.Op == OpCode.Let).ToList();
        lets.Should().Contain(i => i.Arguments[0] == "%r_val" && i.Arguments[1] == "5");
        lets.Should().Contain(i => i.Arguments[0] == "%r_err" && i.Arguments[1] == "0");
        lets.Should().Contain(i => i.Arguments[0] == "%r2_val" && i.Arguments[1] == "0");
        lets.Should().Contain(i => i.Arguments[0] == "%r2_err" && i.Arguments[1] == "\"not found\"");
        lets.Should().Contain(i => i.Arguments[0] == "%o_val" && i.Arguments[1] == "42");
        lets.Should().Contain(i => i.Arguments[0] == "%o_has" && i.Arguments[1] == "1");
        lets.Should().Contain(i => i.Arguments[0] == "%o2_val" && i.Arguments[1] == "0");
        lets.Should().Contain(i => i.Arguments[0] == "%o2_has" && i.Arguments[1] == "0");
    }

    [Fact]
    public void Parse_Func_ReturnType_ResultType_IsStored()
    {
        var result = CreateParser().Parse(new[] { "func test() -> Result<int, string>", "endfunc" }, "signature.aria");
        result.Functions.Should().ContainSingle(f => f.ReturnType != null && f.ReturnType.Contains("Result<"));
    }

    [Fact]
    public void Parse_BlockWhile_WithWend_ProducesLoopLabels()
    {
        var result = CreateParser().Parse(new[]
        {
            "while %0 < 3",
            "    inc %0",
            "wend",
            "end"
        }, "block_while.aria");

        result.Instructions.Should().Contain(i => i.Op == OpCode.JumpIfFalse);
        result.Instructions.Should().Contain(i => i.Op == OpCode.Jmp);
        result.Labels.Should().ContainKey("__while_start_1");
        result.Labels.Should().ContainKey("__while_end_1");
    }

    [Fact]
    public void Parse_Match_BasicTwoCasesAndDefault_TranspilesToTexts()
    {
        var script = new[] {
            "match %choice",
            "case 1",
            "    text \"one\"",
            "case 2",
            "    text \"two\"",
            "default",
            "    text \"other\"",
            "endmatch",
            "end"
        };

        var result = CreateParser().Parse(script, "match_basic.aria");
        result.Instructions.Should().Contain(i => i.Op == OpCode.Text && i.Arguments[0].Contains("one"));
        result.Instructions.Should().Contain(i => i.Op == OpCode.Text && i.Arguments[0].Contains("two"));
        result.Instructions.Should().Contain(i => i.Op == OpCode.Text && i.Arguments[0].Contains("other"));
    }

    [Fact]
    public void Parse_Match_WithWildcard_Default()
    {
        var script = new[] {
            "match %choice",
            "case 1",
            "    text \"one\"",
            "case _",
            "    text \"fallback\"",
            "endmatch",
            "end"
        };
        var result = CreateParser().Parse(script, "match_wildcard.aria");
        result.Instructions.Should().Contain(i => i.Op == OpCode.Text && i.Arguments[0].Contains("fallback"));
    }

    [Fact]
    public void Parse_Match_WithGuard_IncludesGuardedBody()
    {
        var script = new[] {
            "match %choice",
            "case 1 if %y > 0",
            "    text \"guarded\"",
            "default",
            "    text \"fallback\"",
            "endmatch",
            "end"
        };
        var result = CreateParser().Parse(script, "match_guard.aria");
        result.Instructions.Should().Contain(i => i.Op == OpCode.Text && i.Arguments[0].Contains("guarded"));
    }

    [Fact]
    public void Parse_Match_NonExhaustive_Warns()
    {
        var reporter = new ErrorReporter();
        var result = new Parser(reporter).Parse(new[] {
            "match %choice",
            "case 1",
            "    text \"one\"",
            "endmatch",
            "end"
        }, "nonex.aria");
        reporter.Errors.Should().NotBeEmpty();
    }

    [Fact]
    public void Parse_Match_InsideFunction_Works()
    {
        var script = new[] {
            "func test() -> void",
            "match %c",
            "case 1",
            "    text \"A\"",
            "default",
            "    text \"B\"",
            "endmatch",
            "endfunc",
            "end"
        };
        var result = CreateParser().Parse(script, "match_in_func.aria");
        result.Instructions.Should().Contain(i => i.Op == OpCode.Text && i.Arguments[0].Contains("A"));
    }

    [Fact]
    public void Parse_BlockWhile_WithBraces_PreprocessedToWend()
    {
        var result = CreateParser().Parse(new[]
        {
            "while %0 < 3 {",
            "    inc %0",
            "}",
            "end"
        }, "block_while_braces.aria");

        // 前処理で { } が wend に変換されるため、ブロックwhileと同じ構造になる
        result.Instructions.Should().Contain(i => i.Op == OpCode.JumpIfFalse);
        result.Instructions.Should().Contain(i => i.Op == OpCode.Jmp);
        result.Labels.Should().ContainKey("__while_start_1");
        result.Labels.Should().ContainKey("__while_end_1");
    }

    [Fact]
    public void Parse_Break_InWhile_ProducesJmpToEndLabel()
    {
        var result = CreateParser().Parse(new[]
        {
            "while %0 < 3",
            "    break",
            "wend",
            "end"
        }, "break_while.aria");

        var jmpInst = result.Instructions.Should().ContainSingle(i => i.Op == OpCode.Jmp && i.Arguments[0].StartsWith("__while_end")).Subject;
        jmpInst.Arguments[0].Should().Be("__while_end_1");
    }

    [Fact]
    public void Parse_Continue_InWhile_ProducesJmpToStartLabel()
    {
        var result = CreateParser().Parse(new[]
        {
            "while %0 < 3",
            "    continue",
            "wend",
            "end"
        }, "continue_while.aria");

        // wend の Jmp と continue の Jmp の2つが __while_start_1 へのジャンプ
        var jmpInsts = result.Instructions.Where(i => i.Op == OpCode.Jmp && i.Arguments[0].StartsWith("__while_start")).ToList();
        jmpInsts.Should().HaveCount(2);
        jmpInsts.All(i => i.Arguments[0] == "__while_start_1").Should().BeTrue();
    }

    [Fact]
    public void Parse_MixedIfStyles_DoNotInterfere()
    {
        // ブロックif、インラインif、1行ifブロックが混在しても正しく処理される
        var result = CreateParser().Parse(new[]
        {
            "if %0 == 1",
            "    mov %1, 100",
            "endif",
            "if %1 == 2 mov %2, 200",
            "if %2 == 3 { mov %3, 300 }",
            "end"
        }, "mixed_if.aria");

        var movInstructions = result.Instructions.Where(i => i.Op == OpCode.Mov).ToList();
        movInstructions.Should().HaveCount(3);

        // ブロックif内のmovは条件を持たない（条件はJumpIfFalseに付く）
        // インラインifと1行ifブロックのmovは直接条件を持つ
        movInstructions.Count(i => !i.Condition.IsEmpty).Should().Be(2);
        movInstructions.Count(i => i.Condition.IsEmpty).Should().Be(1);
    }

    // ===========================================================
    // const/enum 強化テスト
    // ===========================================================

    [Fact]
    public void Parse_EnumTypedParameter_WithValidValue_PassesValidation()
    {
        var reporter = new ErrorReporter();
        var parser = new Parser(reporter);
        var result = parser.Parse(new[]
        {
            "enum Route { Ayu, Nayuki }",
            "func move(Route: route) -> void",
            "endfunc",
            "move(Route.Ayu)"
        }, "enum_test.aria");

        reporter.Errors.Should().BeEmpty();
        result.Enums.Should().ContainSingle();
        result.Enums[0].Name.Should().Be("Route");
    }

    [Fact]
    public void Parse_EnumTypedParameter_WithInvalidValue_ReportsError()
    {
        var reporter = new ErrorReporter();
        var parser = new Parser(reporter);
        var result = parser.Parse(new[]
        {
            "enum Route { Ayu, Nayuki }",
            "func move(Route: route) -> void",
            "endfunc",
            "move(999)"
        }, "enum_test.aria");

        reporter.Errors.Should().Contain(e => e.Message.Contains("列挙型") && e.Message.Contains("Route"));
    }

    [Fact]
    public void Parse_FuncLocalConst_NotAccessibleOutside()
    {
        var reporter = new ErrorReporter();
        var parser = new Parser(reporter);
        var result = parser.Parse(new[]
        {
            "func test() -> void",
            "const LOCAL_VAL = 42",
            "let %0, LOCAL_VAL",
            "endfunc",
            "let %1, LOCAL_VAL"
        }, "const_scope.aria");

        var letInstructions = result.Instructions.Where(i => i.Op == OpCode.Mov).ToList();
        letInstructions.Should().HaveCount(2);
        // Inside func: LOCAL_VAL should be replaced with 42
        letInstructions[0].Arguments.Should().Contain("42");
        // Outside func: LOCAL_VAL should NOT be replaced
        letInstructions[1].Arguments.Should().Contain("LOCAL_VAL");
    }

    [Fact]
    public void Parse_GlobalConst_AccessibleInsideFunc()
    {
        var reporter = new ErrorReporter();
        var parser = new Parser(reporter);
        var result = parser.Parse(new[]
        {
            "const GLOBAL_VAL = 99",
            "func test() -> void",
            "let %0, GLOBAL_VAL",
            "endfunc"
        }, "const_scope.aria");

        var letInstruction = result.Instructions.FirstOrDefault(i => i.Op == OpCode.Mov);
        letInstruction.Should().NotBeNull();
        letInstruction!.Arguments.Should().Contain("99");
    }

    [Fact]
    public void Parse_NamespaceEnum_AccessibleWithQualifiedName()
    {
        var reporter = new ErrorReporter();
        var parser = new Parser(reporter);
        var result = parser.Parse(new[]
        {
            "namespace Game {",
            "enum Route { Ayu, Nayuki }",
            "}",
            "func move(Route: route) -> void",
            "endfunc",
            "move(Game.Route.Ayu)"
        }, "ns_enum.aria");

        reporter.Errors.Should().BeEmpty();
        result.Enums.Should().ContainSingle();
        result.Enums[0].Name.Should().Be("Route");
        result.Enums[0].Namespace.Should().Be("Game");
    }

    [Fact]
    public void Parse_NamespaceEnum_UnqualifiedNameStillWorks()
    {
        var reporter = new ErrorReporter();
        var parser = new Parser(reporter);
        var result = parser.Parse(new[]
        {
            "namespace Game {",
            "enum Route { Ayu, Nayuki }",
            "}",
            "func move(Route: route) -> void",
            "endfunc",
            "move(Route.Ayu)"
        }, "ns_enum.aria");

        reporter.Errors.Should().BeEmpty();
    }

    // ===========================================================
    // readonly / mut テスト
    // ===========================================================

    [Fact]
    public void Parse_ReadonlyDeclaration_FollowedByReassignment_ReportsError()
    {
        var reporter = new ErrorReporter();
        var parser = new Parser(reporter);
        parser.Parse(new[]
        {
            "readonly %x = 100",
            "let %x, 200",
            "end"
        }, "readonly.aria");

        reporter.Errors.Should().Contain(e => e.Message.Contains("readonly変数%xに再代入できません"));
    }

    [Fact]
    public void Parse_MutDeclaration_FollowedByReassignment_NoError()
    {
        var reporter = new ErrorReporter();
        var parser = new Parser(reporter);
        parser.Parse(new[]
        {
            "mut %score = 0",
            "let %score, 100",
            "end"
        }, "mut.aria");

        reporter.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Parse_LetWithoutReadonly_FollowedByReassignment_NoError()
    {
        var reporter = new ErrorReporter();
        var parser = new Parser(reporter);
        parser.Parse(new[]
        {
            "let %x, 100",
            "let %x, 200",
            "end"
        }, "let_compat.aria");

        reporter.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Parse_ReadonlyInsideFunction_ReassignedInSameFunction_ReportsError()
    {
        var reporter = new ErrorReporter();
        var parser = new Parser(reporter);
        parser.Parse(new[]
        {
            "func test() -> void",
            "readonly %x = 100",
            "let %x, 200",
            "endfunc",
            "end"
        }, "readonly_func.aria");

        reporter.Errors.Should().Contain(e => e.Message.Contains("readonly変数%xに再代入できません"));
    }

    [Fact]
    public void Parse_ReadonlyInsideFunction_NotReassigned_NoError()
    {
        var reporter = new ErrorReporter();
        var parser = new Parser(reporter);
        parser.Parse(new[]
        {
            "func test() -> void",
            "readonly %x = 100",
            "mov %y, %x",
            "endfunc",
            "end"
        }, "readonly_safe.aria");

        reporter.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Parse_ReadonlyInFunctionA_ReassignedInFunctionB_NoError()
    {
        var reporter = new ErrorReporter();
        var parser = new Parser(reporter);
        parser.Parse(new[]
        {
            "func a() -> void",
            "readonly %x = 100",
            "endfunc",
            "func b() -> void",
            "let %x, 200",
            "endfunc",
            "end"
        }, "readonly_scope.aria");

        reporter.Errors.Should().BeEmpty();
    }

    // ===========================================================
    // T13: owned sprite テスト
    // ===========================================================

    [Fact]
    public void Parse_OwnedSpriteDeclaration_PopulatesOwnedSprites()
    {
        var parser = CreateParser();
        var result = parser.Parse(new[] { "owned sprite %bg", "owned sprite $character", "end" }, "owned.aria");

        result.OwnedSprites.Should().Contain("%bg");
        result.OwnedSprites.Should().Contain("$character");
    }

    [Fact]
    public void Parse_OwnedSpriteDeclaration_CaseInsensitive()
    {
        var parser = CreateParser();
        var result = parser.Parse(new[] { "OWNED SPRITE %sprite1", "Owned Sprite %sprite2", "end" }, "owned_case.aria");

        result.OwnedSprites.Should().Contain("%sprite1");
        result.OwnedSprites.Should().Contain("%sprite2");
    }

    // ===========================================================
    // T14: struct instantiation テスト
    // ===========================================================

    [Fact]
    public void Parse_StructInstantiation_TranspilesToFieldAssignments()
    {
        var result = CreateParser().Parse(new[]
        {
            "struct Point",
            "    int x",
            "    int y",
            "endstruct",
            "let %p, new Point { %x = 10, %y = 20 }",
            "end"
        }, "struct_inst.aria");

        var lets = result.Instructions.Where(i => i.Op == OpCode.Let).ToList();
        lets.Should().Contain(i => i.Arguments[0] == "%p_x" && i.Arguments[1] == "10");
        lets.Should().Contain(i => i.Arguments[0] == "%p_y" && i.Arguments[1] == "20");
    }

    [Fact]
    public void Parse_StructInstantiation_FieldAccessRewritten()
    {
        var result = CreateParser().Parse(new[]
        {
            "struct Point",
            "    int x",
            "    int y",
            "endstruct",
            "let %p, new Point { %x = 10, %y = 20 }",
            "mov %q, %p.x",
            "end"
        }, "struct_access.aria");

        var movInst = result.Instructions.FirstOrDefault(i => i.Op == OpCode.Mov);
        movInst.Should().NotBeNull();
        movInst!.Arguments.Should().Contain("%p_x");
    }

    [Fact]
    public void Parse_StructInstantiation_StringField_Works()
    {
        var result = CreateParser().Parse(new[]
        {
            "struct Button",
            "    int x",
            "    string text",
            "endstruct",
            "let %btn, new Button { %x = 100, $text = \"OK\" }",
            "end"
        }, "struct_string.aria");

        var lets = result.Instructions.Where(i => i.Op == OpCode.Let).ToList();
        lets.Should().Contain(i => i.Arguments[0] == "%btn_x" && i.Arguments[1] == "100");
        lets.Should().Contain(i => i.Arguments[0] == "%btn_text" && i.Arguments[1] == "\"OK\"");
    }

    [Fact]
    public void Parse_StructInstantiation_FieldAccessInExpression_Rewritten()
    {
        var result = CreateParser().Parse(new[]
        {
            "struct Point",
            "    int x",
            "    int y",
            "endstruct",
            "let %p, new Point { %x = 10, %y = 20 }",
            "let %sum, %p.x + %p.y",
            "end"
        }, "struct_expr.aria");

        var lets = result.Instructions.Where(i => i.Op == OpCode.Let).ToList();
        var sumLet = lets.FirstOrDefault(i => i.Arguments[0] == "%sum");
        sumLet.Should().NotBeNull();
        sumLet!.Arguments[1].Should().Contain("%p_x");
        sumLet.Arguments[1].Should().Contain("%p_y");
    }

    [Fact]
    public void Parse_StructInstantiation_UnknownStruct_ReportsError()
    {
        var reporter = new ErrorReporter();
        var parser = new Parser(reporter);
        parser.Parse(new[]
        {
            "let %p, new Unknown { %x = 10 }",
            "end"
        }, "struct_unknown.aria");

        reporter.Errors.Should().Contain(e => e.Message.Contains("構造体") && e.Message.Contains("Unknown"));
    }

    [Fact]
    public void Parse_StructInstantiation_DefaultValuesForUnspecifiedFields()
    {
        var result = CreateParser().Parse(new[]
        {
            "struct Point",
            "    int x",
            "    int y",
            "endstruct",
            "let %p, new Point { %x = 10 }",
            "end"
        }, "struct_default.aria");

        var lets = result.Instructions.Where(i => i.Op == OpCode.Let).ToList();
        lets.Should().Contain(i => i.Arguments[0] == "%p_x" && i.Arguments[1] == "10");
        lets.Should().Contain(i => i.Arguments[0] == "%p_y" && i.Arguments[1] == "0");
    }

    // ===========================================================
    // T12: use/modules テスト
    // ===========================================================

    [Fact]
    public void Parse_UseModule_ImportsFunctionsFromModuleFile()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(Path.Combine(tempDir, "modules"));
        File.WriteAllText(Path.Combine(tempDir, "modules", "test.aria"), @"
namespace test {
func hello() -> void
endfunc
}
");
        var script = new[]
        {
            "use \"test\"",
            "hello()",
            "end"
        };
        var reporter = new ErrorReporter();
        var parser = new Parser(reporter);
        var result = parser.Parse(script, Path.Combine(tempDir, "main.aria"));
        reporter.Errors.Should().BeEmpty();
        result.Functions.Should().Contain(f => f.QualifiedName.Equals("test_hello", StringComparison.OrdinalIgnoreCase));
        result.Instructions.Should().Contain(i => i.Op == OpCode.Gosub && i.Arguments[0].Equals("*test_hello", StringComparison.OrdinalIgnoreCase));
        try { Directory.Delete(tempDir, true); } catch { }
    }

    [Fact]
    public void Parse_UseQualifiedName_RewritesQualifiedCall()
    {
        var script = new[]
        {
            "use gfx.Draw",
            "*gfx_Draw",
            "return",
            "gfx.draw()",
            "end"
        };
        var reporter = new ErrorReporter();
        var parser = new Parser(reporter);
        var result = parser.Parse(script, "test.aria");
        reporter.Errors.Should().BeEmpty();
        result.Instructions.Should().Contain(i => i.Op == OpCode.Gosub && i.Arguments[0].Equals("*gfx_Draw", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Parse_UseModule_DuplicateImports_NoError()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(Path.Combine(tempDir, "modules"));
        File.WriteAllText(Path.Combine(tempDir, "modules", "test.aria"), @"
namespace test {
func hello() -> void
endfunc
}
");
        var script = new[]
        {
            "use \"test\"",
            "use \"test\"",
            "hello()",
            "end"
        };
        var reporter = new ErrorReporter();
        var parser = new Parser(reporter);
        var result = parser.Parse(script, Path.Combine(tempDir, "main.aria"));
        reporter.Errors.Should().BeEmpty();
        try { Directory.Delete(tempDir, true); } catch { }
    }

    [Fact]
    public void Parse_UsingNamespace_ResolvesUnqualifiedCalls()
    {
        var script = new[]
        {
            "namespace ui {",
            "func button() -> void",
            "endfunc",
            "}",
            "using ui",
            "button()",
            "end"
        };
        var reporter = new ErrorReporter();
        var parser = new Parser(reporter);
        var result = parser.Parse(script, "using.aria");
        reporter.Errors.Should().BeEmpty();
        result.Instructions.Should().Contain(i => i.Op == OpCode.Gosub && i.Arguments[0].Equals("*ui_button", StringComparison.OrdinalIgnoreCase));
    }
}
