using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Xunit;
using AriaEngine.Core;
using AriaEngine.Tools;

namespace AriaEngine.Tests;

public class FormatTests
{
    [Fact]
    public void Format_EmptyScript_ReturnsEmpty()
    {
        var lines = Array.Empty<string>();
        var result = AriaFormatCommand.FormatLines(lines);
        result.Should().BeEmpty();
    }

    [Fact]
    public void Format_AddsCorrectIndentation()
    {
        var lines = new[] {
            "*label",
            "text \"hello\"",
            "if %0 == 1",
            "text \"yes\"",
            "endif"
        };

        var result = AriaFormatCommand.FormatLines(lines);

        result[0].Should().Be("*label");
        result[1].Should().Be("text \"hello\"");
        result[2].Should().Be("if %0 == 1");
        result[3].Should().Be("    text \"yes\"");
        result[4].Should().Be("endif");
    }

    [Fact]
    public void Format_NestedBlocks_IncreasesIndentation()
    {
        var lines = new[] {
            "func test()",
            "if %0 == 1",
            "while %1 < 10",
            "inc %1",
            "wend",
            "endif",
            "endfunc"
        };

        var result = AriaFormatCommand.FormatLines(lines);

        result[0].Should().Be("func test()");
        result[1].Should().Be("    if %0 == 1");
        result[2].Should().Be("        while %1 < 10");
        result[3].Should().Be("            inc %1");
        result[4].Should().Be("        wend");
        result[5].Should().Be("    endif");
        result[6].Should().Be("endfunc");
    }

    [Fact]
    public void Format_Idempotent_FormatTwiceProducesSameResult()
    {
        var lines = new[] {
            "*label",
            "text \"hello\"",
            "if %0 == 1",
            "text \"yes\"",
            "endif"
        };

        var first = AriaFormatCommand.FormatLines(lines);
        var second = AriaFormatCommand.FormatLines(first.ToArray());

        first.Should().Equal(second);
    }

    [Fact]
    public void Format_LabelsRemainLeftAligned()
    {
        var lines = new[] {
            "func test()",
            "*inner_label",
            "text \"inner\"",
            "endfunc"
        };

        var result = AriaFormatCommand.FormatLines(lines);

        result[0].Should().Be("func test()");
        result[1].Should().Be("*inner_label");
        result[2].Should().Be("    text \"inner\"");
        result[3].Should().Be("endfunc");
    }

    [Fact]
    public void Format_CommentsPreserved()
    {
        var lines = new[] {
            "func test()",
            "; this is a comment",
            "text \"hello\"",
            "endfunc"
        };

        var result = AriaFormatCommand.FormatLines(lines);

        result[0].Should().Be("func test()");
        result[1].Should().Contain("; this is a comment");
        result[2].Should().Be("    text \"hello\"");
        result[3].Should().Be("endfunc");
    }

    [Fact]
    public void Format_AllBlockTypes()
    {
        var lines = new[] {
            "if %0 == 1",
            "endif",
            "while %0 < 10",
            "wend",
            "func test()",
            "endfunc",
            "scope",
            "end_scope",
            "match %0",
            "endmatch",
            "try",
            "endtry",
            "switch %0",
            "endswitch",
            "for %i = 0 to 10",
            "next"
        };

        var result = AriaFormatCommand.FormatLines(lines);

        result.Should().HaveCountGreaterThan(0);
        // All block openers should be indented at depth 0
        result[0].Should().Be("if %0 == 1");
        result[2].Should().Be("while %0 < 10");
        result[4].Should().Be("func test()");
        result[6].Should().Be("scope");
        result[8].Should().Be("match %0");
        result[10].Should().Be("try");
        result[12].Should().Be("switch %0");
        result[14].Should().Be("for %i = 0 to 10");
    }

    [Fact]
    public void Format_StripTrailingWhitespace()
    {
        var lines = new[] {
            "text \"hello\"   ",
            "text \"world\""
        };

        var result = AriaFormatCommand.FormatLines(lines);

        result[0].Should().Be("text \"hello\"");
        result[1].Should().Be("text \"world\"");
    }

    [Fact]
    public void Format_NormalizeInternalWhitespace()
    {
        var lines = new[] {
            "text    \"hello\""
        };

        var result = AriaFormatCommand.FormatLines(lines);

        result[0].Should().Be("text \"hello\"");
    }

    [Fact]
    public void Format_OutputIsParseable()
    {
        var lines = new[] {
            "*start",
            "let %0, 1",
            "if %0 == 1",
            "text \"yes\"",
            "endif",
            "end"
        };

        var result = AriaFormatCommand.FormatLines(lines);
        var resultLines = result.ToArray();

        // Verify all lines can be processed by parser without syntax errors
        var parser = new Parser(new ErrorReporter());
        var parseResult = parser.Parse(resultLines, "formatted.aria");

        parseResult.Instructions.Should().NotBeEmpty();
        parseResult.Labels.Should().ContainKey("start");
    }

    [Fact]
    public void Format_EmptyLinesBetweenLogicalBlocks()
    {
        var lines = new[] {
            "*label1",
            "text \"one\"",
            "*label2",
            "text \"two\""
        };

        var result = AriaFormatCommand.FormatLines(lines);

        // Should have blank line before label2
        int label1Idx = result.FindIndex(x => x == "*label1");
        int label2Idx = result.FindIndex(x => x == "*label2");
        label2Idx.Should().Be(label1Idx + 2); // one blank line between
    }

    [Fact]
    public void Format_DedentOnBlockClose()
    {
        var lines = new[] {
            "func test()",
            "if %0 == 1",
            "text \"yes\"",
            "endif",
            "text \"after if\"",
            "endfunc"
        };

        var result = AriaFormatCommand.FormatLines(lines);

        result[0].Should().Be("func test()");
        result[1].Should().Be("    if %0 == 1");
        result[2].Should().Be("        text \"yes\"");
        result[3].Should().Be("    endif");
        result[4].Should().Be("    text \"after if\"");
        result[5].Should().Be("endfunc");
    }
}
