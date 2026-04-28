using System;
using System.Collections.Generic;
using System.Text;

namespace AriaEngine.Text;

/// <summary>
/// テキストエフェクトインラインパーサー
/// [color=red], [b], [size=1.5], [line_speed=50], [wait=2000] 等をパース
/// </summary>
public class TextEffectParser
{
    private readonly Stack<TextStyle> _styleStack = new();
    
    /// <summary>カスタムエフェクト定義テーブル（グローバル共有）</summary>
    private static readonly Dictionary<string, string> _customEffects = new(StringComparer.OrdinalIgnoreCase);
    
    /// <summary>
    /// カスタムエフェクトを登録
    /// </summary>
    public static void DefineEffect(string name, string replacement)
    {
        _customEffects[name] = replacement;
    }
    
    /// <summary>
    /// カスタムエフェクト定義をクリア
    /// </summary>
    public static void ClearCustomEffects()
    {
        _customEffects.Clear();
    }

    /// <summary>
    /// テキストをパースしてセグメントリストに分割
    /// </summary>
    public List<TextSegment> Parse(string text)
    {
        // カスタムエフェクトを事前展開
        text = ExpandCustomEffects(text);
        
        var segments = new List<TextSegment>();
        _styleStack.Clear();
        _styleStack.Push(TextStyle.Default);

        var currentText = new StringBuilder();
        int i = 0;

        while (i < text.Length)
        {
            // 改行文字の処理
            if (text[i] == '\n')
            {
                if (currentText.Length > 0)
                {
                    segments.Add(new TextSegment(currentText.ToString(), _styleStack.Peek().Clone()));
                    currentText.Clear();
                }
                segments.Add(new TextSegment("", _styleStack.Peek().Clone(), isNewLine: true));
                i++;
                continue;
            }

            // タグの開始
            if (text[i] == '[')
            {
                int closeIndex = text.IndexOf(']', i);
                if (closeIndex == -1)
                {
                    // 閉じタグがない → 通常文字として扱う
                    currentText.Append(text[i]);
                    i++;
                    continue;
                }

                string tagContent = text.Substring(i + 1, closeIndex - i - 1);
                
                // 閉じタグ
                if (tagContent.StartsWith("/"))
                {
                    if (currentText.Length > 0)
                    {
                        segments.Add(new TextSegment(currentText.ToString(), _styleStack.Peek().Clone()));
                        currentText.Clear();
                    }
                    
                    // スタイルスタックからポップ（対応する開始タグがあれば）
                    if (_styleStack.Count > 1)
                    {
                        _styleStack.Pop();
                    }
                    
                    i = closeIndex + 1;
                    continue;
                }

                // 開始タグ/単独タグ
                if (currentText.Length > 0)
                {
                    segments.Add(new TextSegment(currentText.ToString(), _styleStack.Peek().Clone()));
                    currentText.Clear();
                }

                var newStyle = ParseTag(tagContent);
                if (newStyle != null)
                {
                    _styleStack.Push(_styleStack.Peek().Merge(newStyle));
                }

                i = closeIndex + 1;
                continue;
            }

            // 通常文字
            currentText.Append(text[i]);
            i++;
        }

        // 残りのテキスト
        if (currentText.Length > 0)
        {
            segments.Add(new TextSegment(currentText.ToString(), _styleStack.Peek().Clone()));
        }

        return segments;
    }

    /// <summary>
    /// タグ文字列をパースして TextStyle を生成
    /// </summary>
    /// <summary>
    /// カスタムエフェクトタグを展開
    /// </summary>
    private static string ExpandCustomEffects(string text)
    {
        if (_customEffects.Count == 0) return text;
        
        var result = new StringBuilder();
        int i = 0;
        
        while (i < text.Length)
        {
            if (text[i] == '[')
            {
                int closeIndex = text.IndexOf(']', i);
                if (closeIndex > i)
                {
                    string tagContent = text.Substring(i + 1, closeIndex - i - 1);
                    // 閉じタグや標準タグはスキップ
                    if (!tagContent.StartsWith("/") && _customEffects.TryGetValue(tagContent, out var replacement))
                    {
                        result.Append(replacement);
                        i = closeIndex + 1;
                        continue;
                    }
                }
            }
            
            result.Append(text[i]);
            i++;
        }
        
        return result.ToString();
    }

    private TextStyle? ParseTag(string tagContent)
    {
        // 属性を分離: "color=red" → "color", "red"
        string tagName;
        string? attribute = null;
        
        int eqIndex = tagContent.IndexOf('=');
        if (eqIndex > 0)
        {
            tagName = tagContent.Substring(0, eqIndex).Trim().ToLowerInvariant();
            attribute = tagContent.Substring(eqIndex + 1).Trim();
        }
        else
        {
            tagName = tagContent.Trim().ToLowerInvariant();
        }

        return tagName switch
        {
            "color" => new TextStyle { Color = attribute },
            "b" or "bold" => new TextStyle { Bold = true },
            "i" or "italic" => new TextStyle { Italic = true },
            "u" or "underline" => new TextStyle { Underline = true },
            "s" or "strike" or "strikethrough" => new TextStyle { Strikethrough = true },
            "size" => ParseSizeTag(attribute),
            "line_speed" or "linespeed" => new TextStyle { LineSpeed = ParseInt(attribute) },
            "wait" => new TextStyle { WaitTime = ParseInt(attribute) },
            "fade" => new TextStyle { FadeDuration = ParseInt(attribute) > 0 ? ParseInt(attribute) : 500 },
            "type" or "typespeed" => new TextStyle { TypeSpeed = ParseInt(attribute) > 0 ? ParseInt(attribute) : 50 },
            "shake" => new TextStyle { ShakeIntensity = ParseInt(attribute) > 0 ? ParseInt(attribute) : 3 },
            _ => null
        };
    }

    private TextStyle ParseSizeTag(string? attribute)
    {
        if (string.IsNullOrEmpty(attribute)) return new TextStyle();

        if (attribute.StartsWith("+") || attribute.StartsWith("-"))
        {
            // 絶対サイズ加算: [size=+2], [size=-1]
            if (int.TryParse(attribute, out int offset))
            {
                return new TextStyle { SizeOffset = offset };
            }
        }
        else if (float.TryParse(attribute, out float scale))
        {
            // 相対サイズ: [size=1.5]
            return new TextStyle { SizeScale = scale };
        }

        return new TextStyle();
    }

    private static int ParseInt(string? value)
    {
        if (int.TryParse(value, out int result)) return result;
        return 0;
    }
}

/// <summary>
/// TextStyle の拡張メソッド
/// </summary>
public static class TextStyleExtensions
{
    /// <summary>
    /// ディープコピー
    /// </summary>
    public static TextStyle Clone(this TextStyle style)
    {
        return new TextStyle
        {
            Color = style.Color,
            SizeScale = style.SizeScale,
            SizeOffset = style.SizeOffset,
            Bold = style.Bold,
            Italic = style.Italic,
            Underline = style.Underline,
            Strikethrough = style.Strikethrough,
            LineSpeed = style.LineSpeed,
            WaitTime = style.WaitTime,
            FadeDuration = style.FadeDuration,
            ShakeIntensity = style.ShakeIntensity,
            TypeSpeed = style.TypeSpeed
        };
    }
}
