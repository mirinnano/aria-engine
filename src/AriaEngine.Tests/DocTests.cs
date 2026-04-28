using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using Xunit;
using AriaEngine.Core;
using AriaEngine.Tools;

namespace AriaEngine.Tests;

public class DocTests
{
    private static Parser CreateParser() => new(new ErrorReporter());

    [Fact]
    public void Parse_DocCommentBeforeFunc_CapturesDocComment()
    {
        var result = CreateParser().Parse(new[]
        {
            "/// This is a test function",
            "func test()",
            "endfunc"
        }, "test.aria");

        result.Functions.Should().ContainSingle();
        result.Functions[0].DocComment.Should().Be("This is a test function");
    }

    [Fact]
    public void Parse_MultipleDocCommentLinesBeforeFunc_ConcatenatesWithNewlines()
    {
        var result = CreateParser().Parse(new[]
        {
            "/// First line",
            "/// Second line",
            "/// Third line",
            "func myFunc()",
            "endfunc"
        }, "test.aria");

        result.Functions.Should().ContainSingle();
        result.Functions[0].DocComment.Should().Be("First line\nSecond line\nThird line");
    }

    [Fact]
    public void Parse_DocCommentBeforeStruct_CapturesDocComment()
    {
        var result = CreateParser().Parse(new[]
        {
            "/// This is a test struct",
            "struct TestStruct",
            "int id",
            "endstruct"
        }, "test.aria");

        result.Structs.Should().ContainSingle();
        result.Structs[0].DocComment.Should().Be("This is a test struct");
    }

    [Fact]
    public void Parse_MultipleDocCommentLinesBeforeStruct_ConcatenatesWithNewlines()
    {
        var result = CreateParser().Parse(new[]
        {
            "/// First line of struct doc",
            "/// Second line of struct doc",
            "struct MyStruct",
            "int value",
            "endstruct"
        }, "test.aria");

        result.Structs.Should().ContainSingle();
        result.Structs[0].DocComment.Should().Be("First line of struct doc\nSecond line of struct doc");
    }

    [Fact]
    public void Parse_NoDocComment_FuncHasNullDocComment()
    {
        var result = CreateParser().Parse(new[]
        {
            "func noDoc()",
            "endfunc"
        }, "test.aria");

        result.Functions.Should().ContainSingle();
        result.Functions[0].DocComment.Should().BeNull();
    }

    [Fact]
    public void Parse_NoDocComment_StructHasNullDocComment()
    {
        var result = CreateParser().Parse(new[]
        {
            "struct NoDocStruct",
            "int x",
            "endstruct"
        }, "test.aria");

        result.Structs.Should().ContainSingle();
        result.Structs[0].DocComment.Should().BeNull();
    }

    [Fact]
    public void Parse_DocCommentAfterOtherLine_NotCaptured()
    {
        var result = CreateParser().Parse(new[]
        {
            "text \"Some text\"",
            "/// This should not be captured",
            "func test()",
            "endfunc"
        }, "test.aria");

        result.Functions.Should().ContainSingle();
        result.Functions[0].DocComment.Should().BeNull();
    }

    [Fact]
    public void Parse_DocCommentWithIndented_TrimsWhitespace()
    {
        var result = CreateParser().Parse(new[]
        {
            "   ///   Indented doc comment   ",
            "func test()",
            "endfunc"
        }, "test.aria");

        result.Functions.Should().ContainSingle();
        result.Functions[0].DocComment.Should().Be("Indented doc comment");
    }

    [Fact]
    public void Parse_DocCommentAndNormalComments_DocCommentOnlyFromTripleSlash()
    {
        var result = CreateParser().Parse(new[]
        {
            "; This is a normal comment",
            "/// Doc comment for func",
            "// Regular single-line comment",
            "func test()",
            "endfunc"
        }, "test.aria");

        result.Functions.Should().ContainSingle();
        result.Functions[0].DocComment.Should().Be("Doc comment for func");
    }

    [Fact]
    public void Parse_FuncAndStructBothWithDocComments_BothCaptured()
    {
        var result = CreateParser().Parse(new[]
        {
            "/// Function doc",
            "func myFunc()",
            "endfunc",
            "/// Struct doc",
            "struct MyStruct",
            "int field",
            "endstruct"
        }, "test.aria");

        result.Functions.Should().ContainSingle();
        result.Functions[0].DocComment.Should().Be("Function doc");
        result.Structs.Should().ContainSingle();
        result.Structs[0].DocComment.Should().Be("Struct doc");
    }

    [Fact]
    public void AriaDoc_ProducesValidJson()
    {
        // Create a temporary directory and script file
        string tempDir = Path.Combine(Path.GetTempPath(), $"aria_doc_test_{Guid.NewGuid():N}");
        string scriptPath = Path.Combine(tempDir, "test.aria");
        string outputDir = Path.Combine(tempDir, "output");

        try
        {
            Directory.CreateDirectory(outputDir);
            Directory.CreateDirectory(tempDir);

            // Write test script
            File.WriteAllLines(scriptPath, new[]
            {
                "/// Test function",
                "func button(int x)",
                "endfunc"
            });

            // Run aria-doc command
            int exitCode = AriaDocCommand.Run(new[] { scriptPath, "--out", outputDir });

            exitCode.Should().Be(0);

            // Check JSON output
            string jsonPath = Path.Combine(outputDir, "doc.json");
            File.Exists(jsonPath).Should().BeTrue();

            string json = File.ReadAllText(jsonPath);
            json.Should().NotBeNullOrEmpty();
            json.Should().Contain("\"Functions\"");
            json.Should().Contain("\"button\"");
            json.Should().Contain("Test function");
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void AriaDoc_ProducesValidMarkdown()
    {
        // Create a temporary directory and script file
        string tempDir = Path.Combine(Path.GetTempPath(), $"aria_doc_test_{Guid.NewGuid():N}");
        string scriptPath = Path.Combine(tempDir, "test.aria");
        string outputDir = Path.Combine(tempDir, "output");

        try
        {
            Directory.CreateDirectory(outputDir);
            Directory.CreateDirectory(tempDir);

            // Write test script
            File.WriteAllLines(scriptPath, new[]
            {
                "/// Test function",
                "func button(int x)",
                "endfunc"
            });

            // Run aria-doc command
            int exitCode = AriaDocCommand.Run(new[] { scriptPath, "--out", outputDir });

            exitCode.Should().Be(0);

            // Check Markdown output
            string mdPath = Path.Combine(outputDir, "doc.md");
            File.Exists(mdPath).Should().BeTrue();

            string md = File.ReadAllText(mdPath);
            md.Should().NotBeNullOrEmpty();
            md.Should().Contain("# test.aria Documentation");
            md.Should().Contain("## Functions");
            md.Should().Contain("### button");
            md.Should().Contain("Test function");
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void AriaDoc_MissingScript_ReturnsError()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"aria_doc_test_{Guid.NewGuid():N}");
        string outputDir = Path.Combine(tempDir, "output");

        try
        {
            Directory.CreateDirectory(outputDir);

            int exitCode = AriaDocCommand.Run(new[] { "nonexistent.aria", "--out", outputDir });

            exitCode.Should().NotBe(0);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }
}
