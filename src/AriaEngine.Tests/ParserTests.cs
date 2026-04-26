using System;
using FluentAssertions;
using Xunit;
using AriaEngine.Core;

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
        result.Instructions.Should().Contain(i => i.Op == OpCode.WaitClick);
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

        result.Instructions.Should().Contain(i => i.Op == OpCode.Beq);
        result.Instructions.Should().Contain(i => i.Op == OpCode.End);
    }
}
