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
    Bg, BgFade, BgTime, BgTimeMap, Transition, Camera, Screen, TextFx, Fx, Sync, Voice, VoiceWait, VoiceStop, Print, Effect, Quake,

    // UI・テキストウィンドウ・文字表示
    Textbox, Fontsize, Textcolor, TextboxColor, TextboxHide, TextboxShow,
    SetWindow, EraseTextWindow, TextClear, TextSpeed, DefaultSpeed, TextMode,
    Br, WaitClick, WaitClickClear, TextboxStyle, ChoiceStyle, TextTarget, CompatMode, UiTheme,
    UiQuality, UiMotion, Ui, UiRect, UiText, UiImage, UiButton, UiGroup, UiGroupAdd,
    UiGroupClear, UiGroupShow, UiGroupHide, UiLayout, UiAnchor, UiPack, UiStyle,
    UiState, UiStateStyle, UiOn, UiHotkey, UiTween, UiFade, UiMove, UiScale,

    // システム・ダイアログ・環境設定
    YesNoBox, MesBox, SaveOn, SaveOff, Save, Load, LookbackOn, LookbackOff,
    AutoModeTime, RightMenu, ClickCursor, Backlog, KidokuMode, SkipMode,
    WindowTitle, SystemButton,

    // 新規タイマー・演出・スクリプト機能
    Rnd, Inc, Dec, For, Next, ResetTimer, GetTimer, WaitTimer,

    // 配列操作・例外処理
    SetArray, GetArray, Throw, Assert, Panic,

    // 制御構造
    While, Wend, Break, Continue, ReturnValue,

    // CGギャラリー
    GalleryEntry, CgUnlock, GalleryCount, GalleryInfo,

    // 動的スクリプト include
    Include,
    // Defer: register an instruction to execute when exiting a scope
    Defer,

    // 設定読み書き
    GetConfig, SetConfig, SaveConfig,

    // セーブデータ情報
    SaveInfo,

    // バックログ読み出し
    BacklogCount, BacklogEntry,

    // UI インタラクティブ要素
    UiSlider,
    UiCheckbox,

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
    FontFilter,
    // Scope control (explicit scope blocks)
    ScopeEnter, ScopeExit
}
