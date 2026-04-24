using System;
using System.Collections.Generic;
using Raylib_cs;

namespace AriaEngine.Core;

public enum GameScene
{
    TitleScreen,
    ChapterSelect,
    GamePlay,
    SystemMenu,
    SaveLoadMenu,
    Settings,
    Gallery
}

public enum VmState
{
    Running,
    WaitingForClick,
    WaitingForChoice, // 互換性のため残すが、基本は WaitingForButton に移行
    WaitingForButton,
    WaitingForDelay,
    WaitingForAnimation,
    WaitingForTimer,
    FadingIn,
    FadingOut,
    Ended
}

public struct LoopState
{
    public int PC;
    public string VarName;
    public int TargetValue;
}

public class RightMenuEntry
{
    public string Label { get; set; } = "";
    public string Action { get; set; } = "";
}

public class GameState
{
    public int ProgramCounter { get; set; } = 0;
    public Dictionary<string, int> Registers { get; set; } = new();
    public Dictionary<string, string> StringRegisters { get; set; } = new();
    public int CompareFlag { get; set; } = 0;

    // コールスタック (gosub / return 用)
    public Stack<int> CallStack { get; set; } = new();
    public Stack<string> ParamStack { get; set; } = new();

    // for/next 用ループスタック
    public Stack<LoopState> LoopStack { get; set; } = new();

    // ボタンのタイムアウト関連
    public int ButtonTimeoutMs { get; set; } = 0;
    public float ButtonTimer { get; set; } = 0f;
    public string ButtonResultRegister { get; set; } = "0";

    // 全ての表示物を一元管理
    public Dictionary<int, Sprite> Sprites { get; set; } = new();

    // スプライト番号 -> ボタン番号マッピング (spbtn)
    public Dictionary<int, int> SpriteButtonMap { get; set; } = new();

    public VmState State { get; set; } = VmState.Running;

    // オーディオ
    public string CurrentBgm { get; set; } = "";
    public List<string> PendingSe { get; set; } = new();
    public int BgmVolume { get; set; } = 100;
    public int SeVolume { get; set; } = 100;

    // トランジション
    public float FadeProgress { get; set; } = 1.0f;
    public bool IsFading { get; set; } = false;
    public int FadeDurationMs { get; set; } = 1000;

    // 画面揺れ (Quake)
    public int QuakeAmplitude { get; set; } = 0;
    public float QuakeTimerMs { get; set; } = 0f;

    // 内部UI用（textbox系の状態を保存）
    public int DefaultTextboxX { get; set; } = 50;
    public int DefaultTextboxY { get; set; } = 500;
    public int DefaultTextboxW { get; set; } = 1180;
    public int DefaultTextboxH { get; set; } = 200;
    public int DefaultFontSize { get; set; } = 32;
    public string DefaultTextColor { get; set; } = "#ffffff";
    public string DefaultTextboxBgColor { get; set; } = "#000000";
    public int DefaultTextboxBgAlpha { get; set; } = 180;
    public bool TextboxVisible { get; set; } = true;
    public bool UseManualTextLayout { get; set; } = false;
    public int TextTargetSpriteId { get; set; } = -1;
    public int TextboxBackgroundSpriteId { get; set; } = -1;
    public bool CompatAutoUi { get; set; } = false;
    public int DefaultTextboxPaddingX { get; set; } = UIThemeDefaults.TextboxPaddingX;
    public int DefaultTextboxPaddingY { get; set; } = UIThemeDefaults.TextboxPaddingY;
    public int DefaultTextboxCornerRadius { get; set; } = UIThemeDefaults.TextboxCornerRadius;
    public string DefaultTextboxBorderColor { get; set; } = UIThemeDefaults.TextboxBorderColor;
    public int DefaultTextboxBorderWidth { get; set; } = UIThemeDefaults.TextboxBorderWidth;
    public int DefaultTextboxBorderOpacity { get; set; } = UIThemeDefaults.TextboxBorderOpacity;
    public string DefaultTextboxShadowColor { get; set; } = UIThemeDefaults.TextboxShadowColor;
    public int DefaultTextboxShadowOffsetX { get; set; } = UIThemeDefaults.TextboxShadowOffsetX;
    public int DefaultTextboxShadowOffsetY { get; set; } = UIThemeDefaults.TextboxShadowOffsetY;
    public int DefaultTextboxShadowAlpha { get; set; } = UIThemeDefaults.TextboxShadowAlpha;

    // Choice/ボタン用スタイルトークン
    public int ChoiceWidth { get; set; } = UIThemeDefaults.ChoiceWidth;
    public int ChoiceHeight { get; set; } = UIThemeDefaults.ChoiceHeight;
    public int ChoiceSpacing { get; set; } = UIThemeDefaults.ChoiceSpacing;
    public int ChoiceFontSize { get; set; } = UIThemeDefaults.ChoiceFontSize;
    public string ChoiceTextColor { get; set; } = UIThemeDefaults.ChoiceTextColor;
    public string ChoiceBgColor { get; set; } = UIThemeDefaults.ChoiceBgColor;
    public int ChoiceBgAlpha { get; set; } = UIThemeDefaults.ChoiceBgAlpha;
    public string ChoiceHoverColor { get; set; } = UIThemeDefaults.ChoiceHoverColor;
    public int ChoiceCornerRadius { get; set; } = UIThemeDefaults.ChoiceCornerRadius;
    public string ChoiceBorderColor { get; set; } = UIThemeDefaults.ChoiceBorderColor;
    public int ChoiceBorderWidth { get; set; } = UIThemeDefaults.ChoiceBorderWidth;
    public int ChoiceBorderOpacity { get; set; } = UIThemeDefaults.ChoiceBorderOpacity;
    public int ChoicePaddingX { get; set; } = UIThemeDefaults.ChoicePaddingX;

    // --- 新規追加（テキスト・システム） ---
    public int TextSpeedMs { get; set; } = 0; // 0 = 瞬間表示
    public string CurrentTextBuffer { get; set; } = "";
    public int DisplayedTextLength { get; set; } = 0;
    public float TextTimerMs { get; set; } = 0f;
    public bool IsWaitingPageClear { get; set; } = false; // \ の改ページ待ち

    public List<string> TextHistory { get; set; } = new();

    public bool AutoMode { get; set; } = false;
    public int AutoModeWaitTimeMs { get; set; } = 2000;
    public float AutoModeTimerMs { get; set; } = 0f;
    public bool SkipMode { get; set; } = false;
    public bool SkipUnread { get; set; } = false;
    public bool BacklogEnabled { get; set; } = true;
    public bool KidokuMode { get; set; } = true;
    public HashSet<string> ReadKeys { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public bool CurrentInstructionWasRead { get; set; } = false;

    public string RightMenuLabel { get; set; } = "*rmenu";
    public List<RightMenuEntry> RightMenuEntries { get; set; } = new()
    {
        new RightMenuEntry { Label = "セーブ", Action = "save" },
        new RightMenuEntry { Label = "ロード", Action = "load" },
        new RightMenuEntry { Label = "回想", Action = "lookback" },
        new RightMenuEntry { Label = "スキップ", Action = "skip" },
        new RightMenuEntry { Label = "リセット", Action = "reset" },
        new RightMenuEntry { Label = "終了", Action = "end" }
    };
    public bool SaveMode { get; set; } = true;  // セーブ/ロードモード切替
    public bool RequestClose { get; set; } = false;
    public bool RequestReset { get; set; } = false;
    public bool ShowClickCursor { get; set; } = true;
    public string ClickCursorPath { get; set; } = "";
    public int ClickCursorOffsetX { get; set; } = 12;
    public int ClickCursorOffsetY { get; set; } = 8;
    public bool ShowSystemCloseButton { get; set; } = true;
    public bool ShowSystemResetButton { get; set; } = false;
    public bool ShowSystemSkipButton { get; set; } = false;
    public bool ShowSystemSaveButton { get; set; } = false;
    public bool ShowSystemLoadButton { get; set; } = false;
    public bool UseMsaa { get; set; } = false;  // MSAA有効化（ぼやけの原因）
    public bool UseAnisotropicFiltering { get; set; } = true;  // 異方性フィルタリング

    public float DelayTimerMs { get; set; } = 0f;
    public float ScriptTimerMs { get; set; } = 0f;
    // ------------------------------------

    // エンジン・設定情報
    public int WindowWidth { get; set; } = 1280;
    public int WindowHeight { get; set; } = 720;
    public string Title { get; set; } = "Aria Engine";
    public string FontPath { get; set; } = "";
    public int FontAtlasSize { get; set; } = 192; // 高解像度フォントアトラス
    public string MainScript { get; set; } = "assets/scripts/main.aria";
    public bool DebugMode { get; set; } = false;

    // ゲームフロー管理用
    public GameScene CurrentScene { get; set; } = GameScene.TitleScreen;
    public Dictionary<string, object> SceneData { get; set; } = new();
    public bool IsTransitioning { get; set; } = false;

    // プレイ時間管理
    public TimeSpan TotalPlayTime { get; set; } = TimeSpan.Zero;
    public DateTime SessionStartTime { get; set; } = DateTime.Now;

    // セーブデータ用
    public string CurrentChapter { get; set; } = "";
    public int CurrentProgress { get; set; } = 0;

    // フォント設定
    public TextureFilter FontFilter { get; set; } = TextureFilter.Bilinear;

    // フラグ管理システム（スクリプト主導）
    public Dictionary<string, bool> Flags { get; set; } = new();
    public Dictionary<string, bool> SaveFlags { get; set; } = new();
    public Dictionary<string, bool> VolatileFlags { get; set; } = new();
    public Dictionary<string, int> Counters { get; set; } = new();

    // 現在定義中のチャプターデータ
    public ChapterInfo? CurrentChapterDefinition { get; set; }
}
