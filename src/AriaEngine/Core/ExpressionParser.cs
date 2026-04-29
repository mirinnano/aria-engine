using System;
using System.Collections.Generic;

namespace AriaEngine.Core;

/// <summary>
/// 式の再帰下降パーサー
/// 条件式トークンリストからExpression ASTを構築
/// </summary>
public static class ExpressionParser
{
    public static Expression? TryParse(IReadOnlyList<string> tokens)
    {
        if (tokens == null || tokens.Count == 0) return null;
        var parser = new ParserState(tokens);
        try
        {
            var expr = ParseOr(parser);
            // Support ternary at the end of a whole expression: cond ? a : b
            if (parser.Peek() == "?")
            {
                parser.Consume();
                var trueExpr = ParseOr(parser);
                if (!parser.Match(":")) throw new InvalidOperationException("Missing ':' in ternary expression");
                var falseExpr = ParseOr(parser);
                expr = new TernaryExpr(expr, trueExpr, falseExpr);
            }
            return parser.Pos == tokens.Count ? expr : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private class ParserState
    {
        public IReadOnlyList<string> Tokens { get; }
        public int Pos { get; set; }
        public ParserState(IReadOnlyList<string> tokens) => Tokens = tokens;
        public string? Peek() => Pos < Tokens.Count ? Tokens[Pos] : null;
        public string? Consume()
        {
            if (Pos < Tokens.Count) return Tokens[Pos++];
            return null;
        }
        public bool Match(string op)
        {
            if (Peek() == op) { Pos++; return true; }
            return false;
        }
    }

    // or_expr := and_expr ( "||" and_expr )*
    private static Expression ParseOr(ParserState p)
    {
        var left = ParseAnd(p);
        while (p.Match("||"))
        {
            var right = ParseAnd(p);
            left = new BinaryExpr(left, "||", right);
        }
        return left;
    }

    // and_expr := cmp_expr ( "&&" cmp_expr )*
    private static Expression ParseAnd(ParserState p)
    {
        var left = ParseCmp(p);
        while (p.Match("&&"))
        {
            var right = ParseCmp(p);
            left = new BinaryExpr(left, "&&", right);
        }
        return left;
    }

    // cmp_expr := add_expr ( ( "==" | "!=" | ">" | "<" | ">=" | "<=" ) add_expr )?
    private static Expression ParseCmp(ParserState p)
    {
        // Support chained comparisons: a <= b <= c -> (a <= b) && (b <= c)
        var left = ParseAdd(p);
        var op = p.Peek();
        var cmpOps = new HashSet<string> { "==", "!=", ">", "<", ">=", "<=" };
        if (op is string o && cmpOps.Contains(o))
        {
            p.Consume();
            var middle = ParseAdd(p);
            Expression acc = new BinaryExpr(left, op, middle);

            // further chained comparisons: a <= b <= c <= d ...
            while (true)
            {
                var nextOp = p.Peek();
                if (nextOp is string nOp && cmpOps.Contains(nOp))
                {
                    p.Consume();
                    var nextRight = ParseAdd(p);
                    acc = new BinaryExpr(acc, "&&", new BinaryExpr(middle, nOp, nextRight));
                    middle = nextRight;
                }
                else
                {
                    break;
                }
            }
            return acc;
        }
        return left;
    }

    // add_expr := mul_expr ( ( "+" | "-" ) mul_expr )*
    private static Expression ParseAdd(ParserState p)
    {
        var left = ParseMul(p);
        while (true)
        {
            var op = p.Peek();
            if (op is "+" or "-")
            {
                p.Consume();
                var right = ParseMul(p);
                left = new BinaryExpr(left, op, right);
            }
            else break;
        }
        return left;
    }

    // mul_expr := unary ( ( "*" | "/" | "%" ) unary )*
    private static Expression ParseMul(ParserState p)
    {
        var left = ParseUnary(p);
        while (true)
        {
            var op = p.Peek();
            if (op is "*" or "/" or "%")
            {
                p.Consume();
                var right = ParseUnary(p);
                left = new BinaryExpr(left, op, right);
            }
            else break;
        }
        return left;
    }

    // unary := ( "!" | "-" )? primary
    private static Expression ParseUnary(ParserState p)
    {
        var op = p.Peek();
        if (op is "!")
        {
            p.Consume();
            return new UnaryExpr("!", ParseUnary(p));
        }
        if (op is "-" && (p.Pos == 0 || IsOperator(p.Tokens[p.Pos - 1])))
        {
            // 負数: トークンが単独の "-" で、前が演算子か先頭
            p.Consume();
            return new UnaryExpr("-", ParseUnary(p));
        }
        return ParsePrimary(p);
    }

    // primary := NUMBER | REGISTER | STRING | ARRAY_ACCESS | "(" expr ")" | ARRAY_LITERAL
    private static Expression ParsePrimary(ParserState p)
    {
        var token = p.Consume();
        if (token == null) throw new InvalidOperationException("Unexpected end of expression");

        // 括弧
        if (token == "(")
        {
            var expr = ParseOr(p);
            if (!p.Match(")")) throw new InvalidOperationException("Missing closing parenthesis");
            return expr;
        }

        // 配列リテラル: [elem1, elem2, ...]
        if (token == "[")
        {
            var elements = new List<Expression>();
            if (p.Peek() == "]") { p.Consume(); return new ArrayLiteralExpr(elements); }
            while (true)
            {
                var el = ParseOr(p);
                elements.Add(el);
                if (p.Match("]")) break;
                if (!p.Match(",")) throw new InvalidOperationException("Missing comma in array literal");
            }
            return new ArrayLiteralExpr(elements);
        }

        // 文字列リテラル
        if (token.StartsWith("$") && token.Length > 1)
        {
            return new StringRegisterExpr(token);
        }

        // 配列アクセス: %name[index]
        if (token.StartsWith("%") && p.Match("["))
        {
            string arrayName = token;
            var indexExpr = ParseOr(p);
            if (!p.Match("]")) throw new InvalidOperationException("Missing closing bracket in array access");
            return new ArrayAccessExpr(arrayName, indexExpr);
        }

        // 整数レジスタ
        if (token.StartsWith("%"))
        {
            return new RegisterExpr(token);
        }

        // 数値リテラル
        if (int.TryParse(token, out int numValue))
        {
            return new IntLiteralExpr(numValue);
        }

        // 文字列リテラル（引用符なし、または残りのトークン）
        return new StringLiteralExpr(token);
    }

    private static bool IsOperator(string token)
    {
        return token is "+" or "-" or "*" or "/" or "%" or "==" or "!=" or ">" or "<" or ">=" or "<=" or "&&" or "||" or "(" or "[";
    }
}
