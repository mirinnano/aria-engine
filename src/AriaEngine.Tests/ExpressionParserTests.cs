using System.Collections.Generic;
using Xunit;
using AriaEngine.Core;

namespace AriaEngine.Tests;

public class ExpressionParserTests
{
    [Fact]
    public void Parses_Array_Literal()
    {
        var tokens = new List<string> { "[", "1", ",", "2", ",", "3", "]" };
        var expr = ExpressionParser.TryParse(tokens);
        Assert.NotNull(expr);
        Assert.IsType<ArrayLiteralExpr>(expr);
        var arr = (ArrayLiteralExpr)expr!;
        Assert.Equal(3, arr.Elements.Count);
    }

    [Fact]
    public void Parses_Chained_Comparisons()
    {
        var tokens = new List<string> { "1", "<=", "%0", "<=", "10" };
        var expr = ExpressionParser.TryParse(tokens);
        Assert.NotNull(expr);
        // Top-level should be a BinaryExpr with && combining two comparisons
        Assert.IsType<BinaryExpr>(expr);
        var root = (BinaryExpr)expr!;
        Assert.Equal("&&", root.Op);
        // Left side should be a BinaryExpr for 1 <= %0
        Assert.IsType<BinaryExpr>(root.Left);
        // Right side should be a BinaryExpr for %0 <= 10
        Assert.IsType<BinaryExpr>(root.Right);
    }

    [Fact]
    public void Parses_Ternary_Operator()
    {
        var tokens = new List<string> { "%0", "?", "1", ":", "2" };
        var expr = ExpressionParser.TryParse(tokens);
        Assert.NotNull(expr);
        Assert.IsType<TernaryExpr>(expr!);
    }
}
