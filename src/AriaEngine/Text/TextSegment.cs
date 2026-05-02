namespace AriaEngine.Text;

/// <summary>
/// テキストをエフェクトタグで分割したセグメント
/// </summary>
public class TextSegment
{
    /// <summary>表示テキスト</summary>
    public string Text { get; set; } = "";
    
    /// <summary>このセグメントのスタイル</summary>
    public TextStyle Style { get; set; } = TextStyle.Default;
    
    /// <summary>改行を含むかどうか</summary>
    public bool IsNewLine { get; set; }
    
    /// <summary>フェードイン持続時間（ms。0=無効）</summary>
    public int FadeDuration { get; set; }
    
    /// <summary>フェードイン開始時刻（ゲーム時間ms）</summary>
    public float FadeStartTime { get; set; }
    
    /// <summary>シェイク強度（px。0=無効）</summary>
    public int ShakeIntensity { get; set; }
    
    /// <summary>タイプライター速度（ms/文字。0=デフォルト）</summary>
    public int TypeSpeed { get; set; }
    
    /// <summary>クリック待ち時間（ms。0=無効）</summary>
    public int WaitMs { get; set; }

    /// <summary>ルビテキスト（ふりがな。null=無効）</summary>
    public string? RubyText { get; set; }

    public TextSegment() { }

    public TextSegment(string text, TextStyle? style = null, bool isNewLine = false)
    {
        Text = text;
        Style = style ?? TextStyle.Default;
        IsNewLine = isNewLine;
        RubyText = style?.RubyText;
    }
}
