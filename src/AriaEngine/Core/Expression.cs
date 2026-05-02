using System;
using System.Collections.Generic;
using System.Globalization;

namespace AriaEngine.Core;

/// <summary>
/// 式のASTノード基底クラス
/// </summary>
public abstract class Expression
{
    public abstract int EvaluateInt(GameState state, VirtualMachine vm);
    public abstract string EvaluateString(GameState state, VirtualMachine vm);
    public abstract bool IsStringExpression { get; }
}

/// <summary>
/// 整数リテラル
/// </summary>
public sealed class IntLiteralExpr : Expression
{
    public int Value { get; }
    public IntLiteralExpr(int value) => Value = value;
    public override int EvaluateInt(GameState state, VirtualMachine vm) => Value;
    public override string EvaluateString(GameState state, VirtualMachine vm) => Value.ToString();
    public override bool IsStringExpression => false;
}

/// <summary>
/// 文字列リテラル
/// </summary>
public sealed class StringLiteralExpr : Expression
{
    public string Value { get; }
    public StringLiteralExpr(string value) => Value = value;
    public override int EvaluateInt(GameState state, VirtualMachine vm)
    {
        if (int.TryParse(Value, out int result)) return result;
        return 0;
    }
    public override string EvaluateString(GameState state, VirtualMachine vm) => Value;
    public override bool IsStringExpression => true;
}

/// <summary>
/// レジスタ参照（%name または %0-%9）
/// </summary>
public sealed class RegisterExpr : Expression
{
    public string Name { get; }
    public RegisterExpr(string name) => Name = name;
    public override int EvaluateInt(GameState state, VirtualMachine vm)
    {
        string normalized = Name.TrimStart('%');
        // 高速パス: %0-%9
        if (normalized.Length == 1 && normalized[0] >= '0' && normalized[0] <= '9')
        {
            return vm.GetReg(normalized);
        }
        return vm.GetReg(normalized);
    }
    public override string EvaluateString(GameState state, VirtualMachine vm)
    {
        return EvaluateInt(state, vm).ToString();
    }
    public override bool IsStringExpression => false;
}

/// <summary>
/// 文字列レジスタ参照（$name）
/// </summary>
public sealed class StringRegisterExpr : Expression
{
    public string Name { get; }
    public StringRegisterExpr(string name) => Name = name;
    public override int EvaluateInt(GameState state, VirtualMachine vm)
    {
        string key = Name.TrimStart('$');
        if (state.RegisterState.StringRegisters.TryGetValue(key, out string? value) && int.TryParse(value, out int result))
            return result;
        return 0;
    }
    public override string EvaluateString(GameState state, VirtualMachine vm)
    {
        string key = Name.TrimStart('$');
        return state.RegisterState.StringRegisters.TryGetValue(key, out string? value) ? value ?? "" : "";
    }
    public override bool IsStringExpression => true;
}

/// <summary>
/// 配列アクセス（%arr[index]）
/// </summary>
public sealed class ArrayAccessExpr : Expression
{
    public string ArrayName { get; }
    public Expression Index { get; }
    public ArrayAccessExpr(string arrayName, Expression index)
    {
        ArrayName = arrayName.TrimStart('%');
        Index = index;
    }
    public override int EvaluateInt(GameState state, VirtualMachine vm)
    {
        int idx = Index.EvaluateInt(state, vm);
        if (state.RegisterState.Arrays.TryGetValue(ArrayName, out var array) && idx >= 0 && idx < array.Length)
            return array[idx];
        return 0;
    }
    public override string EvaluateString(GameState state, VirtualMachine vm) => EvaluateInt(state, vm).ToString();
    public override bool IsStringExpression => false;
}

/// <summary>
/// 二項演算
/// </summary>
public sealed class BinaryExpr : Expression
{
    public Expression Left { get; }
    public string Op { get; }
    public Expression Right { get; }

    public BinaryExpr(Expression left, string op, Expression right)
    {
        Left = left;
        Op = op;
        Right = right;
    }

    public override int EvaluateInt(GameState state, VirtualMachine vm)
    {
        if ((Op == "==" || Op == "!=") && (Left.IsStringExpression || Right.IsStringExpression))
        {
            bool equals = string.Equals(Left.EvaluateString(state, vm), Right.EvaluateString(state, vm), StringComparison.Ordinal);
            return Op == "==" ? (equals ? 1 : 0) : (!equals ? 1 : 0);
        }

        int left = Left.EvaluateInt(state, vm);
        int right = Right.EvaluateInt(state, vm);
        return Op switch
        {
            "+" => left + right,
            "-" => left - right,
            "*" => left * right,
            "/" => right != 0 ? left / right : 0,
            "%" => right != 0 ? left % right : 0,
            "==" => left == right ? 1 : 0,
            "!=" => left != right ? 1 : 0,
            ">" => left > right ? 1 : 0,
            "<" => left < right ? 1 : 0,
            ">=" => left >= right ? 1 : 0,
            "<=" => left <= right ? 1 : 0,
            "&&" => (left != 0 && right != 0) ? 1 : 0,
            "||" => (left != 0 || right != 0) ? 1 : 0,
            _ => 0
        };
    }

    public override string EvaluateString(GameState state, VirtualMachine vm)
    {
        if (Op == "+")
        {
            if (Left.IsStringExpression || Right.IsStringExpression)
            {
                return Left.EvaluateString(state, vm) + Right.EvaluateString(state, vm);
            }
        }
        return EvaluateInt(state, vm).ToString();
    }

    public override bool IsStringExpression => Op == "+" && (Left.IsStringExpression || Right.IsStringExpression);
}

/// <summary>
/// 単項演算
/// </summary>
public sealed class UnaryExpr : Expression
{
    public string Op { get; }
    public Expression Operand { get; }
    public UnaryExpr(string op, Expression operand)
    {
        Op = op;
        Operand = operand;
    }
    public override int EvaluateInt(GameState state, VirtualMachine vm)
    {
        int val = Operand.EvaluateInt(state, vm);
        return Op switch
        {
            "!" => val == 0 ? 1 : 0,
            "-" => -val,
            _ => val
        };
    }
    public override string EvaluateString(GameState state, VirtualMachine vm) => EvaluateInt(state, vm).ToString();
    public override bool IsStringExpression => false;
}

/// <summary>
/// 配列リテラル（例: [1, 2, 3]）
/// 制限事項: 現状のVM実装では配列の数学的演算がサポートされていないため、
/// 最初の要素を整数として評価する簡易的な実装としています。
public sealed class ArrayLiteralExpr : Expression
{
    public List<Expression> Elements { get; }
    public ArrayLiteralExpr(List<Expression> elements)
    {
        Elements = elements;
    }
    public override int EvaluateInt(GameState state, VirtualMachine vm)
    {
        if (Elements.Count == 0) return 0;
        // 最初の要素の値を返す簡易実装
        return Elements[0].EvaluateInt(state, vm);
    }
    public override string EvaluateString(GameState state, VirtualMachine vm)
    {
        var parts = new List<string>();
        foreach (var e in Elements) parts.Add(e.EvaluateString(state, vm));
        return string.Join(",", parts);
    }
    public override bool IsStringExpression => false;
}

/// <summary>
/// 三項演算子: condition ? trueExpr : falseExpr
/// precedence is handled by ExpressionParser to sit after OR expressions.
/// </summary>
public sealed class TernaryExpr : Expression
{
    public Expression Condition { get; }
    public Expression TrueExpr { get; }
    public Expression FalseExpr { get; }
    public TernaryExpr(Expression condition, Expression trueExpr, Expression falseExpr)
    {
        Condition = condition;
        TrueExpr = trueExpr;
        FalseExpr = falseExpr;
    }
    public override int EvaluateInt(GameState state, VirtualMachine vm)
    {
        int cond = Condition.EvaluateInt(state, vm);
        return (cond != 0) ? TrueExpr.EvaluateInt(state, vm) : FalseExpr.EvaluateInt(state, vm);
    }
    public override string EvaluateString(GameState state, VirtualMachine vm)
    {
        int cond = Condition.EvaluateInt(state, vm);
        return (cond != 0) ? TrueExpr.EvaluateString(state, vm) : FalseExpr.EvaluateString(state, vm);
    }
    public override bool IsStringExpression => TrueExpr.IsStringExpression || FalseExpr.IsStringExpression;
}
