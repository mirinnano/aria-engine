namespace AriaEngine.Core;

public enum OpCode
{
    // 基本
    Text, Wait, Add, Sub, Cmp, Beq, Bne, Bgt, Blt, Jmp, Choice, End, JumpIfFalse,

    // スクリプト制御・算術
    Let, Mov, Mul, Div, Mod, Gosub, Return, Defsub, Getparam, Alias, SystemCall,

    // スプライト操作
    Lsp, LspText, LspRect, Csp, Vsp, Msp, MspRel, SpZ, SpAlpha, SpScale, SpFontsize, SpColor, SpFill,
    SpRound, SpBorder, SpGradient, SpShadow, SpTextShadow, SpTextOutline, SpTextAlign, SpRotation,
    SpTextVAlign,
    SpHoverColor, SpHoverScale, SpCursor,

    // アニメーション (Tween)
    Amsp, Afade, Ascale, Acolor, Await, Ease,

    // ボタン
    Btn, BtnArea, BtnClear, BtnClearAll, BtnWait, SpBtn, BtnTime,

    // 旧NScripter描画互換・エフェクト
    LoadBg, LoadCh, ShowCh, HideCh, Clr,
    Bg, Print, Effect, Quake,

    // UI・テキストウィンドウ・文字表示
    Textbox, Fontsize, Textcolor, TextboxColor, TextboxHide, TextboxShow,
    SetWindow, EraseTextWindow, TextClear, TextSpeed, DefaultSpeed, TextMode,
    Br, WaitClick, WaitClickClear, TextboxStyle, ChoiceStyle, TextTarget, CompatMode, UiTheme,

    // システム・ダイアログ・環境設定
    YesNoBox, MesBox, SaveOn, SaveOff, Save, Load, LookbackOn, LookbackOff,
    AutoModeTime, RightMenu, ClickCursor, Backlog, KidokuMode, SkipMode,
    WindowTitle, SystemButton,

    // 新規タイマー・演出・スクリプト機能
    Delay, Rnd, Inc, Dec, For, Next, ResetTimer, GetTimer, WaitTimer,

    // オーディオ
    PlayBgm, StopBgm, PlaySe, PlayMp3, FadeIn, FadeOut,
    BgmVol, SeVol, Mp3Vol, BgmFade, Mp3FadeOut, Dwave, DwaveLoop, DwaveStop,

    // Init用
    Window, Font, FontAtlasSize, Script, Debug, Caption,

    // チャプター選択システム
    ChapterSelect, UnlockChapter, ChapterThumbnail, ChapterCard, ChapterScroll, ChapterProgress,

    // キャラクター操作
    CharLoad, CharShow, CharHide, CharMove, CharExpression, CharPose, CharZ, CharScale,

    // ゲームフロー
    ChangeScene, ReturnScene, SetSceneData, GetSceneData,

    // フラグ管理システム
    SetFlag, GetFlag, ClearFlag, ToggleFlag,
    SetPFlag, GetPFlag, ClearPFlag, TogglePFlag,
    SetSFlag, GetSFlag, ClearSFlag, ToggleSFlag,
    SetVFlag, GetVFlag, ClearVFlag, ToggleVFlag,
    IncCounter, DecCounter, SetCounter, GetCounter,

    // チャプターデータ定義（スクリプト主導）
    DefChapter, ChapterId, ChapterTitle, ChapterDesc, ChapterScript, EndChapter,

    // フォント設定
    FontFilter
}
