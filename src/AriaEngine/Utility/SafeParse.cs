using System;
using System.Globalization;

namespace AriaEngine.Utility;

/// <summary>
/// 安全なパースヘルパー
/// 例外をキャッチし、デフォルト値を返すパース操作を提供します。
/// </summary>
public static class SafeParse
{
    /// <summary>
    /// 整数を安全にパースします。
    /// </summary>
    /// <param name="value">パースする文字列</param>
    /// <param name="defaultValue">デフォルト値</param>
    /// <param name="errorMessage">エラーメッセージを出力するAction</param>
    /// <returns>パース結果、失敗時はデフォルト値</returns>
    public static int ParseInt(string value, int defaultValue = 0, Action<string>? errorMessage = null)
    {
        if (string.IsNullOrWhiteSpace(value))
            return defaultValue;

        try
        {
            return int.Parse(value.Trim());
        }
        catch (FormatException)
        {
            errorMessage?.Invoke($"Invalid integer format: '{value}', using default: {defaultValue}");
            return defaultValue;
        }
        catch (OverflowException)
        {
            errorMessage?.Invoke($"Integer overflow: '{value}', using default: {defaultValue}");
            return defaultValue;
        }
        catch (ArgumentNullException)
        {
            errorMessage?.Invoke($"Null value provided, using default: {defaultValue}");
            return defaultValue;
        }
    }

    /// <summary>
    /// 整数を安全にパースします（基数指定）。
    /// </summary>
    public static int ParseInt(string value, int defaultValue, int fromBase, Action<string>? errorMessage = null)
    {
        if (string.IsNullOrWhiteSpace(value))
            return defaultValue;

        try
        {
            return Convert.ToInt32(value.Trim(), fromBase);
        }
        catch (FormatException)
        {
            errorMessage?.Invoke($"Invalid base-{fromBase} integer format: '{value}', using default: {defaultValue}");
            return defaultValue;
        }
        catch (OverflowException)
        {
            errorMessage?.Invoke($"Integer overflow in base {fromBase}: '{value}', using default: {defaultValue}");
            return defaultValue;
        }
        catch (ArgumentException)
        {
            errorMessage?.Invoke($"Invalid base {fromBase} for value: '{value}', using default: {defaultValue}");
            return defaultValue;
        }
    }

    /// <summary>
    /// 浮動小数点数を安全にパースします。
    /// </summary>
    public static float ParseFloat(string value, float defaultValue = 0f, Action<string>? errorMessage = null)
    {
        if (string.IsNullOrWhiteSpace(value))
            return defaultValue;

        try
        {
            return float.Parse(value.Trim(), CultureInfo.InvariantCulture);
        }
        catch (FormatException)
        {
            errorMessage?.Invoke($"Invalid float format: '{value}', using default: {defaultValue}");
            return defaultValue;
        }
        catch (OverflowException)
        {
            errorMessage?.Invoke($"Float overflow: '{value}', using default: {defaultValue}");
            return defaultValue;
        }
        catch (ArgumentNullException)
        {
            errorMessage?.Invoke($"Null value provided, using default: {defaultValue}");
            return defaultValue;
        }
    }

    /// <summary>
    /// 倍精度浮動小数点数を安全にパースします。
    /// </summary>
    public static double ParseDouble(string value, double defaultValue = 0.0, Action<string>? errorMessage = null)
    {
        if (string.IsNullOrWhiteSpace(value))
            return defaultValue;

        try
        {
            return double.Parse(value.Trim(), CultureInfo.InvariantCulture);
        }
        catch (FormatException)
        {
            errorMessage?.Invoke($"Invalid double format: '{value}', using default: {defaultValue}");
            return defaultValue;
        }
        catch (OverflowException)
        {
            errorMessage?.Invoke($"Double overflow: '{value}', using default: {defaultValue}");
            return defaultValue;
        }
        catch (ArgumentNullException)
        {
            errorMessage?.Invoke($"Null value provided, using default: {defaultValue}");
            return defaultValue;
        }
    }

    /// <summary>
    /// 16進数の色コードを安全にパースします。
    /// </summary>
    public static int ParseHexColor(string hex, int defaultValue = 0xFFFFFF, Action<string>? errorMessage = null)
    {
        if (string.IsNullOrWhiteSpace(hex))
            return defaultValue;

        // #を削除
        hex = hex.TrimStart('#').Trim();

        try
        {
            // 6桁の16進数（RGB）
            if (hex.Length == 6)
            {
                return int.Parse(hex, NumberStyles.HexNumber);
            }
            // 8桁の16進数（RGBA）
            else if (hex.Length == 8)
            {
                return int.Parse(hex.Substring(0, 6), NumberStyles.HexNumber);
            }
            // 3桁の短縮16進数（RGB）
            else if (hex.Length == 3)
            {
                return int.Parse(
                    $"{hex[0]}{hex[0]}{hex[1]}{hex[1]}{hex[2]}{hex[2]}",
                    NumberStyles.HexNumber
                );
            }
            // 4桁の短縮16進数（RGBA）
            else if (hex.Length == 4)
            {
                return int.Parse(
                    $"{hex[0]}{hex[0]}{hex[1]}{hex[1]}{hex[2]}{hex[2]}",
                    NumberStyles.HexNumber
                );
            }
        }
        catch (FormatException)
        {
            errorMessage?.Invoke($"Invalid hex color format: '{hex}', using default: 0x{defaultValue:X}");
            return defaultValue;
        }
        catch (OverflowException)
        {
            errorMessage?.Invoke($"Hex color overflow: '{hex}', using default: 0x{defaultValue:X}");
            return defaultValue;
        }

        errorMessage?.Invoke($"Unsupported hex color length: {hex.Length} for '{hex}', using default: 0x{defaultValue:X}");
        return defaultValue;
    }

    /// <summary>
    /// ブール値を安全にパースします。
    /// </summary>
    public static bool ParseBool(string value, bool defaultValue = false, Action<string>? errorMessage = null)
    {
        if (string.IsNullOrWhiteSpace(value))
            return defaultValue;

        var trimmed = value.Trim().ToLowerInvariant();
        return trimmed switch
        {
            "true" or "1" or "yes" or "on" => true,
            "false" or "0" or "no" or "off" => false,
            _ => defaultValue
        };
    }

    /// <summary>
    /// TryParseパターンの汎用実装。
    /// </summary>
    public static bool TryParse<T>(string value, out T result, T defaultValue = default!)
    {
        result = defaultValue;

        if (string.IsNullOrWhiteSpace(value))
            return false;

        try
        {
            if (typeof(T) == typeof(int))
            {
                result = (T)(object)int.Parse(value.Trim());
                return true;
            }
            if (typeof(T) == typeof(float))
            {
                result = (T)(object)float.Parse(value.Trim(), CultureInfo.InvariantCulture);
                return true;
            }
            if (typeof(T) == typeof(double))
            {
                result = (T)(object)double.Parse(value.Trim(), CultureInfo.InvariantCulture);
                return true;
            }
            if (typeof(T) == typeof(bool))
            {
                result = (T)(object)ParseBool(value);
                return true;
            }
        }
        catch
        {
            return false;
        }

        return false;
    }
}
