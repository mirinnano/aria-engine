namespace AriaEngine.Compiler;

/// <summary>
/// Ariaバイトコード命令の定義
/// バイトコードは1バイトのオペコードで表現されます
/// </summary>
public enum BytecodeOpCode : byte
{
    // ========================================
    // スタック操作 (0x01 - 0x0F)
    // ========================================

    /// <summary>整数をプッシュ</summary>
    PushInt = 0x01,

    /// <summary>浮動小数点数をプッシュ</summary>
    PushFloat = 0x02,

    /// <summary>文字列をプッシュ</summary>
    PushString = 0x03,

    /// <summary>スタックからポップ</summary>
    Pop = 0x04,

    /// <summary>スタックトップを複製</summary>
    Dup = 0x05,

    /// <summary>スタックトップを交換</summary>
    Swap = 0x06,

    /// <summary>スタックのn番目を複製</summary>
    Pick = 0x07,

    /// <summary>スタックトップをn番目にローテート</summary>
    Roll = 0x08,

    // ========================================
    // メモリ操作 (0x10 - 0x1F)
    // ========================================

    /// <summary>ローカル変数をロード</summary>
    LoadLocal = 0x10,

    /// <summary>ローカル変数にストア</summary>
    StoreLocal = 0x11,

    /// <summary>グローバル変数をロード</summary>
    LoadGlobal = 0x12,

    /// <summary>グローバル変数にストア</summary>
    StoreGlobal = 0x13,

    /// <summary>レジスタをロード</summary>
    LoadRegister = 0x14,

    /// <summary>レジスタにストア</summary>
    StoreRegister = 0x15,

    /// <summary>配列要素をロード</summary>
    LoadArrayElement = 0x16,

    /// <summary>配列要素にストア</summary>
    StoreArrayElement = 0x17,

    // ========================================
    // 算術演算 (0x20 - 0x2F)
    // ========================================

    /// <summary>加算</summary>
    Add = 0x20,

    /// <summary>減算</summary>
    Sub = 0x21,

    /// <summary>乗算</summary>
    Mul = 0x22,

    /// <summary>除算</summary>
    Div = 0x23,

    /// <summary>剰余</summary>
    Mod = 0x24,

    /// <summary>否定</summary>
    Neg = 0x25,

    /// <summary>インクリメント</summary>
    Inc = 0x26,

    /// <summary>デクリメント</summary>
    Dec = 0x27,

    /// <summary>絶対値</summary>
    Abs = 0x28,

    /// <summary>べき乗</summary>
    Pow = 0x29,

    // ========================================
    // ビット演算 (0x30 - 0x3F)
    // ========================================

    /// <summary>ビットAND</summary>
    BitAnd = 0x30,

    /// <summary>ビットOR</summary>
    BitOr = 0x31,

    /// <summary>ビットXOR</summary>
    BitXor = 0x32,

    /// <summary>ビットNOT</summary>
    BitNot = 0x33,

    /// <summary>左シフト</summary>
    ShiftLeft = 0x34,

    /// <summary>右シフト</summary>
    ShiftRight = 0x35,

    // ========================================
    // 比較演算 (0x40 - 0x4F)
    // ========================================

    /// <summary>比較</summary>
    Cmp = 0x3F,

    /// <summary>等しい</summary>
    CmpEq = 0x40,

    /// <summary>等しくない</summary>
    CmpNe = 0x41,

    /// <summary>より小さい</summary>
    CmpLt = 0x42,

    /// <summary>以下</summary>
    CmpLe = 0x43,

    /// <summary>より大きい</summary>
    CmpGt = 0x44,

    /// <summary>以上</summary>
    CmpGe = 0x45,

    /// <summary>論理AND</summary>
    LogicalAnd = 0x46,

    /// <summary>論理OR</summary>
    LogicalOr = 0x47,

    /// <summary>論理NOT</summary>
    LogicalNot = 0x48,

    // ========================================
    // 制御フロー (0x50 - 0x5F)
    // ========================================

    /// <summary>無条件ジャンプ</summary>
    Jump = 0x50,

    /// <summary>真ならジャンプ</summary>
    JumpIfTrue = 0x51,

    /// <summary>偽ならジャンプ</summary>
    JumpIfFalse = 0x52,

    /// <summary>関数呼び出し</summary>
    Call = 0x53,

    /// <summary>関数からリターン</summary>
    Return = 0x54,

    /// <summary>値を返してリターン</summary>
    ReturnValue = 0x55,

    /// <summary>呼び出し元へリターン</summary>
    ReturnToCaller = 0x56,

    /// <summary>ループ開始</summary>
    LoopStart = 0x57,

    /// <summary>ループ終了</summary>
    LoopEnd = 0x58,

    /// <summary>ブレイク</summary>
    Break = 0x59,

    /// <summary>コンティニュー</summary>
    Continue = 0x5A,

    /// <summary>未満ならジャンプ</summary>
    JumpIfLess = 0x5B,

    /// <summary>以下ならジャンプ</summary>
    JumpIfLessEqual = 0x5C,

    /// <summary>超過ならジャンプ</summary>
    JumpIfGreater = 0x5D,

    /// <summary>以上ならジャンプ</summary>
    JumpIfGreaterEqual = 0x5E,

    // ========================================
    // テキスト命令 (0x60 - 0x6F)
    // ========================================

    /// <summary>テキスト表示</summary>
    Text = 0x60,

    /// <summary>テキストクリア</summary>
    TextClear = 0x61,

    /// <summary>クリック待ち</summary>
    WaitClick = 0x62,

    /// <summary>クリック待ちとクリア</summary>
    WaitClickClear = 0x63,

    /// <summary>遅延待ち</summary>
    WaitDelay = 0x64,

    /// <summary>テキストエフェクト開始</summary>
    TextEffectStart = 0x65,

    /// <summary>テキストエフェクト終了</summary>
    TextEffectEnd = 0x66,

    /// <summary>テキストスタイル設定</summary>
    TextStyle = 0x67,

    // ========================================
    // スプライト命令 (0x70 - 0x7F)
    // ========================================

    /// <summary>スプライト表示</summary>
    SpriteLoad = 0x70,

    /// <summary>スプライト移動</summary>
    SpriteMove = 0x71,

    /// <summary>スプライト非表示</summary>
    SpriteHide = 0x72,

    /// <summary>スプライト削除</summary>
    SpriteRemove = 0x73,

    /// <summary>スプライト削除（エイリアス）</summary>
    SpriteDelete = SpriteRemove,

    /// <summary>スプライトプロパティ設定</summary>
    SpriteSetProp = 0x81,

    /// <summary>スプライトプロパティ取得</summary>
    SpriteGetProp = 0x82,

    /// <summary>スプライトアニメーション開始</summary>
    SpriteAnimStart = 0x83,

    /// <summary>スプライトアニメーション停止</summary>
    SpriteAnimStop = 0x84,

    /// <summary>スプライトテキスト表示</summary>
    SpriteTextLoad = 0x85,

    /// <summary>スプライト矩形表示</summary>
    SpriteRectLoad = 0x86,

    /// <summary>スプライト可視性設定</summary>
    SpriteVisible = 0x87,

    /// <summary>スプライト相対移動</summary>
    SpriteMoveRel = 0x88,

    /// <summary>スプライト透明度設定</summary>
    SpriteAlpha = 0x89,

    /// <summary>スプライトスケール設定</summary>
    SpriteScale = 0x8A,

    /// <summary>スプライト回転設定</summary>
    SpriteRotation = 0x8B,

    /// <summary>スプライト色設定</summary>
    SpriteColor = 0x8C,

    /// <summary>スプライトZ順序設定</summary>
    SpriteZ = 0x8D,

    // ========================================
    // 背景命令 (0x90 - 0x9F)
    // ========================================

    /// <summary>背景表示</summary>
    BackgroundLoad = 0x90,

    /// <summary>背景設定</summary>
    BackgroundSet = 0x91,

    /// <summary>背景フェード</summary>
    BackgroundFade = 0x92,

    /// <summary>画面クリア</summary>
    ScreenClear = 0x93,

    /// <summary>画面フェードイン</summary>
    ScreenFadeIn = 0x94,

    /// <summary>画面フェードアウト</summary>
    ScreenFadeOut = 0x95,

    /// <summary>画面揺れ開始</summary>
    ScreenShakeStart = 0x96,

    /// <summary>画面揺れ停止</summary>
    ScreenShakeStop = 0x97,

    // ========================================
    // オーディオ命令 (0xA0 - 0xAF)
    // ========================================

    /// <summary>BGM再生</summary>
    AudioBgmPlay = 0xA0,

    /// <summary>BGM停止</summary>
    AudioBgmStop = 0xA1,

    /// <summary>BGM一時停止</summary>
    AudioBgmPause = 0xA2,

    /// <summary>BGM再開</summary>
    AudioBgmResume = 0xA3,

    /// <summary>BGMフェード</summary>
    AudioBgmFade = 0xA4,

    /// <summary>SE再生</summary>
    AudioSePlay = 0xA5,

    /// <summary>SE停止</summary>
    AudioSeStop = 0xA6,

    /// <summary>音量設定</summary>
    AudioSetVolume = 0xA7,

    /// <summary>BGM再生（エイリアス）</summary>
    BGMPlay = AudioBgmPlay,

    /// <summary>BGM停止（エイリアス）</summary>
    BGMStop = AudioBgmStop,

    /// <summary>SE再生（エイリアス）</summary>
    SEPlay = AudioSePlay,

    /// <summary>SE停止（エイリアス）</summary>
    SEStop = AudioSeStop,

    // ========================================
    // UI命令 (0xB0 - 0xBF)
    // ========================================

    /// <summary>選択肢表示</summary>
    UiChoice = 0xB0,

    /// <summary>ボタン待ち</summary>
    UiBtnWait = 0xB1,

    /// <summary>テキストボックス表示</summary>
    UiTextboxShow = 0xB2,

    /// <summary>テキストボックス非表示</summary>
    UiTextboxHide = 0xB3,

    /// <summary>メニュー表示</summary>
    UiMenuShow = 0xB4,

    /// <summary>メニュー非表示</summary>
    UiMenuHide = 0xB5,

    /// <summary>テキストボックス設定（エイリアス）</summary>
    TextboxSet = 0xB6,

    // ========================================
    // システム命令 (0xC0 - 0xCF)
    // ========================================

    /// <summary>セーブ</summary>
    SystemSave = 0xC0,

    /// <summary>ロード</summary>
    SystemLoad = 0xC1,

    /// <summary>オートセーブ</summary>
    SystemAutoSave = 0xC2,

    /// <summary>オートロード</summary>
    SystemAutoLoad = 0xC3,

    /// <summary>設定保存</summary>
    SystemSaveConfig = 0xC4,

    /// <summary>設定ロード</summary>
    SystemLoadConfig = 0xC5,

    /// <summary>終了</summary>
    SystemExit = 0xC6,

    /// <summary>セーブ（エイリアス）</summary>
    Save = SystemSave,

    /// <summary>ロード（エイリアス）</summary>
    Load = SystemLoad,

    /// <summary>タイトルに戻る</summary>
    SystemTitle = 0xC7,

    // ========================================
    // 演出命令 (0xD0 - 0xDF)
    // ========================================

    /// <summary>ウェイト（VSync）</summary>
    EffectWaitVsync = 0xD0,

    /// <summary>ウェイト（フレーム）</summary>
    EffectWaitFrame = 0xD1,

    /// <summary>待機（エイリアス）</summary>
    Wait = EffectWaitFrame,

    /// <summary>トランジション開始</summary>
    EffectTransitionStart = 0xD2,

    /// <summary>トランジション待ち</summary>
    EffectTransitionWait = 0xD3,

    /// <summary>タイムライン開始</summary>
    EffectTimelineStart = 0xD4,

    /// <summary>タイムライン待ち</summary>
    EffectTimelineWait = 0xD5,

    // ========================================
    // デバッグ・診断命令 (0xE0 - 0xEF)
    // ========================================

    /// <summary>NOP（何もしない）</summary>
    Nop = 0xE0,

    /// <summary>デバッグブレークポイント</summary>
    DebugBreak = 0xE1,

    /// <summary>デバッグログ</summary>
    DebugLog = 0xE2,

    /// <summary>デバッグ変数表示</summary>
    DebugPrint = 0xE3,

    /// <summary>プロファイリング開始</summary>
    ProfileStart = 0xE4,

    /// <summary>プロファイリング終了</summary>
    ProfileStop = 0xE5,

    /// <summary>パフォーマンスカウンター取得</summary>
    GetCycleCount = 0xE6,

    /// <summary>メモリ使用量取得</summary>
    GetMemoryUsage = 0xE7,

    /// <summary>FPS取得</summary>
    GetFps = 0xE8,

    // ========================================
    // 予約済み (0xF0 - 0xFF)
    // ========================================

    /// <summary>拡張命令</summary>
    Extended = 0xF0,

    /// <summary>命令セットの終わり</summary>
    End = 0xFF,
}

/// <summary>
/// バイトコード命令のカテゴリ
/// </summary>
public enum BytecodeOpCodeCategory
{
    Stack,
    Memory,
    Arithmetic,
    Bitwise,
    Comparison,
    ControlFlow,
    Text,
    Sprite,
    Background,
    Audio,
    Ui,
    System,
    Effect,
    Debug,
    Extended,
}

/// <summary>
/// バイトコード命令のユーティリティクラス
/// </summary>
public static class BytecodeOpCodeExtensions
{
    /// <summary>
    /// 命令のカテゴリを取得
    /// </summary>
    public static BytecodeOpCodeCategory GetCategory(this BytecodeOpCode op)
    {
        return op switch
        {
            >= BytecodeOpCode.PushInt and <= BytecodeOpCode.Roll => BytecodeOpCodeCategory.Stack,
            >= BytecodeOpCode.LoadLocal and <= BytecodeOpCode.StoreArrayElement => BytecodeOpCodeCategory.Memory,
            >= BytecodeOpCode.Add and <= BytecodeOpCode.Pow => BytecodeOpCodeCategory.Arithmetic,
            >= BytecodeOpCode.BitAnd and <= BytecodeOpCode.ShiftRight => BytecodeOpCodeCategory.Bitwise,
            >= BytecodeOpCode.CmpEq and <= BytecodeOpCode.LogicalNot => BytecodeOpCodeCategory.Comparison,
            >= BytecodeOpCode.Jump and <= BytecodeOpCode.JumpIfGreaterEqual => BytecodeOpCodeCategory.ControlFlow,
            >= BytecodeOpCode.Text and <= BytecodeOpCode.TextStyle => BytecodeOpCodeCategory.Text,
            >= BytecodeOpCode.SpriteLoad and <= BytecodeOpCode.SpriteZ => BytecodeOpCodeCategory.Sprite,
            >= BytecodeOpCode.BackgroundLoad and <= BytecodeOpCode.ScreenShakeStop => BytecodeOpCodeCategory.Background,
            >= BytecodeOpCode.AudioBgmPlay and <= BytecodeOpCode.AudioSetVolume => BytecodeOpCodeCategory.Audio,
            >= BytecodeOpCode.UiChoice and <= BytecodeOpCode.TextboxSet => BytecodeOpCodeCategory.Ui,
            >= BytecodeOpCode.SystemSave and <= BytecodeOpCode.SystemTitle => BytecodeOpCodeCategory.System,
            >= BytecodeOpCode.EffectWaitVsync and <= BytecodeOpCode.EffectTimelineWait => BytecodeOpCodeCategory.Effect,
            >= BytecodeOpCode.Nop and <= BytecodeOpCode.GetFps => BytecodeOpCodeCategory.Debug,
            _ => BytecodeOpCodeCategory.Extended,
        };
    }

    /// <summary>
    /// 命令のスタックへの影響を取得
    /// </summary>
    /// <returns>(プッシュ数, ポップ数)</returns>
    public static (int push, int pop) GetStackEffect(this BytecodeOpCode op)
    {
        return op switch
        {
            // スタック操作
            BytecodeOpCode.PushInt or BytecodeOpCode.PushFloat or BytecodeOpCode.PushString => (1, 0),
            BytecodeOpCode.Pop => (0, 1),
            BytecodeOpCode.Dup => (1, 0),
            BytecodeOpCode.Swap => (2, 2),
            BytecodeOpCode.Pick => (1, 0),
            BytecodeOpCode.Roll => (1, 1),

            // メモリ操作
            BytecodeOpCode.LoadLocal or BytecodeOpCode.LoadGlobal or BytecodeOpCode.LoadRegister or
            BytecodeOpCode.LoadArrayElement => (1, 1),
            BytecodeOpCode.StoreLocal or BytecodeOpCode.StoreGlobal or BytecodeOpCode.StoreRegister or
            BytecodeOpCode.StoreArrayElement => (0, 2),

            // 算術演算
            BytecodeOpCode.Add or BytecodeOpCode.Sub or BytecodeOpCode.Mul or BytecodeOpCode.Div or
            BytecodeOpCode.Mod or BytecodeOpCode.Pow => (1, 2),
            BytecodeOpCode.Neg or BytecodeOpCode.Abs => (1, 1),
            BytecodeOpCode.Inc or BytecodeOpCode.Dec => (1, 1),

            // ビット演算
            BytecodeOpCode.BitAnd or BytecodeOpCode.BitOr or BytecodeOpCode.BitXor or
            BytecodeOpCode.ShiftLeft or BytecodeOpCode.ShiftRight => (1, 2),
            BytecodeOpCode.BitNot => (1, 1),

            // 比較演算
            BytecodeOpCode.CmpEq or BytecodeOpCode.CmpNe or BytecodeOpCode.CmpLt or BytecodeOpCode.CmpLe or
            BytecodeOpCode.CmpGt or BytecodeOpCode.CmpGe => (1, 2),
            BytecodeOpCode.LogicalAnd or BytecodeOpCode.LogicalOr => (1, 2),
            BytecodeOpCode.LogicalNot => (1, 1),

            // 制御フロー
            BytecodeOpCode.Call => (-1, -1), // 引数数による
            BytecodeOpCode.Return => (-1, -1), // 戻り値による
            BytecodeOpCode.ReturnValue => (0, 1),

            // その他
            _ => (0, 0),
        };
    }

    /// <summary>
    /// 命令の名前を取得
    /// </summary>
    public static string GetName(this BytecodeOpCode op)
    {
        return op.ToString();
    }
}
