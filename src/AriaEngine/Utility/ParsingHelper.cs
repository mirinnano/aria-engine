using System.Globalization;

namespace AriaEngine.Utility;

/// <summary>
/// 値のパースと正規化を効率的に行うヘルパークラス
/// </summary>
public static class ParsingHelper
{
    /// <summary>
    /// 不変カルチャのキャッシュ（パフォーマンス最適化）
    /// </summary>
    private static readonly CultureInfo InvariantCulture = CultureInfo.InvariantCulture;

    /// <summary>
    /// 文字列を浮動小数点数としてパースする
    /// </summary>
    /// <param name="value">パースする文字列</param>
    /// <param name="result">パース結果</param>
    /// <param name="fallback">パース失敗時のデフォルト値</param>
    /// <returns>パースに成功したかどうか</returns>
    public static bool TryParseFloat(string value, out float result, float fallback = 0f)
    {
        if (string.IsNullOrEmpty(value))
        {
            result = fallback;
            return false;
        }

        if (float.TryParse(value, NumberStyles.Float, InvariantCulture, out result))
        {
            return true;
        }

        result = fallback;
        return false;
    }

    /// <summary>
    /// 文字列を整数としてパースする
    /// </summary>
    /// <param name="value">パースする文字列</param>
    /// <param name="result">パース結果</param>
    /// <param name="fallback">パース失敗時のデフォルト値</param>
    /// <returns>パースに成功したかどうか</returns>
    public static bool TryParseInt(string value, out int result, int fallback = 0)
    {
        if (string.IsNullOrEmpty(value))
        {
            result = fallback;
            return false;
        }

        if (int.TryParse(value, NumberStyles.Integer, InvariantCulture, out result))
        {
            return true;
        }

        result = fallback;
        return false;
    }

    /// <summary>
    /// レジスタ名を正規化する
    /// </summary>
    /// <param name="name">正規化するレジスタ名</param>
    /// <returns>正規化されたレジスタ名</returns>
    public static string NormalizeRegisterName(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;

        // 先頭の記号を削除して小文字に変換
        return name.TrimStart('%', '$').ToLowerInvariant();
    }

    /// <summary>
    /// 値がレジスタ参照かどうかを判定する
    /// </summary>
    /// <param name="value">判定する値</param>
    /// <returns>レジスタ参照の場合はtrue</returns>
    public static bool IsRegisterReference(string value)
    {
        if (string.IsNullOrEmpty(value)) return false;
        return value.StartsWith("%") || value.StartsWith("$");
    }

    /// <summary>
    /// 値が整数レジスタ参照かどうかを判定する
    /// </summary>
    /// <param name="value">判定する値</param>
    /// <returns>整数レジスタ参照の場合はtrue</returns>
    public static bool IsIntRegisterReference(string value)
    {
        if (string.IsNullOrEmpty(value)) return false;
        return value.StartsWith("%");
    }

    /// <summary>
    /// 値が文字列レジスタ参照かどうかを判定する
    /// </summary>
    /// <param name="value">判定する値</param>
    /// <returns>文字列レジスタ参照の場合はtrue</returns>
    public static bool IsStringRegisterReference(string value)
    {
        if (string.IsNullOrEmpty(value)) return false;
        return value.StartsWith("$");
    }

    /// <summary>
    /// レジスタ名から記号を削除する
    /// </summary>
    /// <param name="name">レジスタ名</param>
    /// <returns>記号を削除したレジスタ名</returns>
    public static string StripRegisterPrefix(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        return name.TrimStart('%', '$');
    }
}
