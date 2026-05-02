namespace AriaEngine.Text;

/// <summary>
/// テキストセグメントのスタイル情報
/// </summary>
public class TextStyle
{
    /// <summary>文字色（色名または#hex。null=デフォルト）</summary>
    public string? Color { get; set; }
    
    /// <summary>相対サイズ（1.0=デフォルト。null=デフォルト）</summary>
    public float? SizeScale { get; set; }
    
    /// <summary>絶対サイズ加算（px。null=デフォルト）</summary>
    public int? SizeOffset { get; set; }
    
    /// <summary>太字（TextOutlineで代替表現）</summary>
    public bool Bold { get; set; }
    
    /// <summary>斜体</summary>
    public bool Italic { get; set; }
    
    /// <summary>下線</summary>
    public bool Underline { get; set; }
    
    /// <summary>取り消し線</summary>
    public bool Strikethrough { get; set; }
    
    /// <summary>タイプライター速度（ms/文字。null=デフォルト）</summary>
    public int? LineSpeed { get; set; }
    
    /// <summary>次のクリック待ち時間（ms。null=デフォルト）</summary>
    public int? WaitTime { get; set; }
    
    /// <summary>フェードイン持続時間（ms。null=無効）</summary>
    public int? FadeDuration { get; set; }
    
    /// <summary>シェイク強度（px。null=無効）</summary>
    public int? ShakeIntensity { get; set; }
    
    /// <summary>タイプライター速度（ms/文字。null=デフォルト）</summary>
    public int? TypeSpeed { get; set; }

    /// <summary>文字送りSEのパス（1文字表示ごとに再生。null=無効）</summary>
    public string? VoiceSePath { get; set; }

    /// <summary>文字送りSEの音量（0-100。デフォルト=100）</summary>
    public int VoiceSeVolume { get; set; } = 100;

    /// <summary>ルビテキスト（ふりがな。null=無効）</summary>
    public string? RubyText { get; set; }

    /// <summary>
    /// 現在のスタイルをベースに、上書きスタイルを適用した新しいスタイルを作成
    /// </summary>
    public TextStyle Merge(TextStyle? other)
    {
        if (other == null) return this;
        
        return new TextStyle
        {
            Color = other.Color ?? this.Color,
            SizeScale = other.SizeScale ?? this.SizeScale,
            SizeOffset = other.SizeOffset ?? this.SizeOffset,
            Bold = other.Bold || this.Bold,
            Italic = other.Italic || this.Italic,
            Underline = other.Underline || this.Underline,
            Strikethrough = other.Strikethrough || this.Strikethrough,
            LineSpeed = other.LineSpeed ?? this.LineSpeed,
            WaitTime = other.WaitTime ?? this.WaitTime,
            FadeDuration = other.FadeDuration ?? this.FadeDuration,
            ShakeIntensity = other.ShakeIntensity ?? this.ShakeIntensity,
            TypeSpeed = other.TypeSpeed ?? this.TypeSpeed,
            VoiceSePath = other.VoiceSePath ?? this.VoiceSePath,
            VoiceSeVolume = other.VoiceSeVolume != 100 ? other.VoiceSeVolume : this.VoiceSeVolume,
            RubyText = other.RubyText ?? this.RubyText
        };
    }

    /// <summary>
    /// デフォルトスタイル（全てnull/false）
    /// </summary>
    public static TextStyle Default => new();
}
