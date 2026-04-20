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

    public string RightMenuLabel { get; set; } = "*rmenu";
    public bool SaveMode { get; set; } = true;  // セーブ/ロードモード切替

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
    public Dictionary<string, int> Counters { get; set; } = new();

    // 現在定義中のチャプターデータ
    public ChapterInfo? CurrentChapterDefinition { get; set; }
}
