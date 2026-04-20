using System;

namespace AriaEngine.Utility;

/// <summary>
/// 安全な数学演算ヘルパー
/// ゼロ除算やオーバーフローから保護します。
/// </summary>
public static class SafeMath
{
    /// <summary>
    /// 整数の除算を安全に行います。
    /// </summary>
    /// <param name="dividend">被除数</param>
    /// <param name="divisor">除数</param>
    /// <param name="defaultValue">ゼロ除除時のデフォルト値</param>
    /// <param name="warningAction">警告メッセージAction</param>
    /// <returns>除算結果、ゼロ除除時はデフォルト値</returns>
    public static int SafeDivide(int dividend, int divisor, int defaultValue = 0, Action<string>? warningAction = null)
    {
        if (divisor == 0)
        {
            warningAction?.Invoke($"Division by zero: {dividend} / 0, returning default: {defaultValue}");
            return defaultValue;
        }

        try
        {
            return dividend / divisor;
        }
        catch (DivideByZeroException)
        {
            warningAction?.Invoke($"Unexpected divide by zero: {dividend} / 0, returning default: {defaultValue}");
            return defaultValue;
        }
        catch (ArithmeticException ex)
        {
            warningAction?.Invoke($"Arithmetic exception in division {dividend} / {divisor}: {ex.Message}, returning default: {defaultValue}");
            return defaultValue;
        }
    }

    /// <summary>
    /// 浮動小数点数の除算を安全に行います。
    /// </summary>
    public static float SafeDivide(float dividend, float divisor, float defaultValue = 0f, Action<string>? warningAction = null)
    {
        if (Math.Abs(divisor) < float.Epsilon)
        {
            warningAction?.Invoke($"Division by zero (float): {dividend} / {divisor}, returning default: {defaultValue}");
            return defaultValue;
        }

        try
        {
            return dividend / divisor;
        }
        catch (DivideByZeroException)
        {
            warningAction?.Invoke($"Unexpected divide by zero (float): {dividend} / {divisor}, returning default: {defaultValue}");
            return defaultValue;
        }
        catch (ArithmeticException ex)
        {
            warningAction?.Invoke($"Arithmetic exception in division {dividend} / {divisor}: {ex.Message}, returning default: {defaultValue}");
            return defaultValue;
        }
    }

    /// <summary>
    /// 倍精度浮動小数点数の除算を安全に行います。
    /// </summary>
    public static double SafeDivide(double dividend, double divisor, double defaultValue = 0.0, Action<string>? warningAction = null)
    {
        if (Math.Abs(divisor) < double.Epsilon)
        {
            warningAction?.Invoke($"Division by zero (double): {dividend} / {divisor}, returning default: {defaultValue}");
            return defaultValue;
        }

        try
        {
            return dividend / divisor;
        }
        catch (DivideByZeroException)
        {
            warningAction?.Invoke($"Unexpected divide by zero (double): {dividend} / {divisor}, returning default: {defaultValue}");
            return defaultValue;
        }
        catch (ArithmeticException ex)
        {
            warningAction?.Invoke($"Arithmetic exception in division {dividend} / {divisor}: {ex.Message}, returning default: {defaultValue}");
            return defaultValue;
        }
    }

    /// <summary>
    /// 整数のモジュロ演算を安全に行います。
    /// </summary>
    public static int SafeModulo(int dividend, int divisor, int defaultValue = 0, Action<string>? warningAction = null)
    {
        if (divisor == 0)
        {
            warningAction?.Invoke($"Modulo by zero: {dividend} % 0, returning default: {defaultValue}");
            return defaultValue;
        }

        try
        {
            return dividend % divisor;
        }
        catch (DivideByZeroException)
        {
            warningAction?.Invoke($"Unexpected modulo by zero: {dividend} % 0, returning default: {defaultValue}");
            return defaultValue;
        }
    }

    /// <summary>
    /// 整数の積を安全に計算します（オーバーフロー保護）。
    /// </summary>
    public static int SafeMultiply(int a, int b, int defaultValue = 0, Action<string>? warningAction = null)
    {
        try
        {
            checked
            {
                return a * b;
            }
        }
        catch (OverflowException)
        {
            warningAction?.Invoke($"Integer overflow in multiplication {a} * {b}, returning default: {defaultValue}");
            return defaultValue;
        }
    }

    /// <summary>
    /// 整数の和を安全に計算します（オーバーフロー保護）。
    /// </summary>
    public static int SafeAdd(int a, int b, int defaultValue = 0, Action<string>? warningAction = null)
    {
        try
        {
            checked
            {
                return a + b;
            }
        }
        catch (OverflowException)
        {
            warningAction?.Invoke($"Integer overflow in addition {a} + {b}, returning default: {defaultValue}");
            return defaultValue;
        }
    }

    /// <summary>
    /// 整数の差を安全に計算します（オーバーフロー保護）。
    /// </summary>
    public static int SafeSubtract(int a, int b, int defaultValue = 0, Action<string>? warningAction = null)
    {
        try
        {
            checked
            {
                return a - b;
            }
        }
        catch (OverflowException)
        {
            warningAction?.Invoke($"Integer overflow in subtraction {a} - {b}, returning default: {defaultValue}");
            return defaultValue;
        }
    }

    /// <summary>
    /// 配列の安全なインデックスアクセス。
    /// </summary>
    public static T SafeGet<T>(T[] array, int index, T defaultValue = default!)
    {
        if (array == null || array.Length == 0)
            return defaultValue;

        if (index < 0 || index >= array.Length)
            return defaultValue;

        return array[index];
    }

    /// <summary>
    /// 範囲制限（クランプ）。
    /// </summary>
    public static T Clamp<T>(T value, T min, T max) where T : IComparable<T>
    {
        if (value.CompareTo(min) < 0) return min;
        if (value.CompareTo(max) > 0) return max;
        return value;
    }

    /// <summary>
    /// 安全な範囲制限。
    /// </summary>
    public static float SafeClamp(float value, float min, float max, float defaultValue = 0f, Action<string>? warningAction = null)
    {
        if (min > max)
        {
            warningAction?.Invoke($"Invalid clamp range: min ({min}) > max ({max}), returning default: {defaultValue}");
            return defaultValue;
        }

        return Clamp(value, min, max);
    }

    /// <summary>
    /// 整数のべき乗を安全に計算します。
    /// </summary>
    public static int SafePow(int baseValue, int exponent, int defaultValue = 0, Action<string>? warningAction = null)
    {
        if (baseValue == 0 && exponent < 0)
        {
            warningAction?.Invoke($"Zero raised to negative power: 0^{exponent}, returning default: {defaultValue}");
            return defaultValue;
        }

        if (exponent < 0)
        {
            warningAction?.Invoke($"Negative exponent in integer power: {baseValue}^{exponent}, returning default: {defaultValue}");
            return defaultValue;
        }

        try
        {
            return (int)Math.Pow(baseValue, exponent);
        }
        catch (OverflowException)
        {
            warningAction?.Invoke($"Overflow in integer power {baseValue}^{exponent}, returning default: {defaultValue}");
            return defaultValue;
        }
    }

    /// <summary>
    /// 平方根を安全に計算します。
    /// </summary>
    public static float SafeSqrt(float value, float defaultValue = 0f, Action<string>? warningAction = null)
    {
        if (value < 0)
        {
            warningAction?.Invoke($"Square root of negative number: {value}, returning default: {defaultValue}");
            return defaultValue;
        }

        return (float)Math.Sqrt(value);
    }

    /// <summary>
    /// 対数を安全に計算します。
    /// </summary>
    public static double SafeLog(double value, double defaultValue = 0.0, Action<string>? warningAction = null)
    {
        if (value <= 0)
        {
            warningAction?.Invoke($"Logarithm of non-positive number: {value}, returning default: {defaultValue}");
            return defaultValue;
        }

        return Math.Log(value);
    }

    /// <summary>
    /// アークサインを安全に計算します。
    /// </summary>
    public static float SafeAsin(float value, float defaultValue = 0f, Action<string>? warningAction = null)
    {
        if (value < -1f || value > 1f)
        {
            warningAction?.Invoke($"Asin value out of range: {value}, returning default: {defaultValue}");
            return defaultValue;
        }

        return (float)Math.Asin(value);
    }

    /// <summary>
    /// アークコサインを安全に計算します。
    /// </summary>
    public static float SafeAcos(float value, float defaultValue = 0f, Action<string>? warningAction = null)
    {
        if (value < -1f || value > 1f)
        {
            warningAction?.Invoke($"Acos value out of range: {value}, returning default: {defaultValue}");
            return defaultValue;
        }

        return (float)Math.Acos(value);
    }

    /// <summary>
    /// 安全な正規化（ベクトルの長さで除算）。
    /// </summary>
    public static float NormalizeToLength(float value, float targetLength, float defaultValue = 0f, Action<string>? warningAction = null)
    {
        if (Math.Abs(value) < float.Epsilon)
        {
            warningAction?.Invoke($"Cannot normalize zero-length vector, returning default: {defaultValue}");
            return defaultValue;
        }

        return SafeDivide(targetLength, value, defaultValue, warningAction);
    }
}
