using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
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

public sealed class RegisterState
{
    public Dictionary<string, int> Registers { get; set; } = new();
    public Dictionary<string, string> StringRegisters { get; set; } = new();
}

public sealed class VmExecutionState
{
    public int ProgramCounter { get; set; }
    public VmState State { get; set; } = VmState.Running;
    public int CompareFlag { get; set; }
    public Stack<int> CallStack { get; set; } = new();
    public Stack<string> ParamStack { get; set; } = new();
    public Stack<LoopState> LoopStack { get; set; } = new();
    public float DelayTimerMs { get; set; }
    public float ScriptTimerMs { get; set; }
}

public sealed class InteractionState
{
    public int ButtonTimeoutMs { get; set; }
    public float ButtonTimer { get; set; }
    public string ButtonResultRegister { get; set; } = "0";
    public Dictionary<int, int> SpriteButtonMap { get; set; } = new();
}

public sealed class RenderState
{
    public Dictionary<int, Sprite> Sprites { get; set; } = new();
    public float FadeProgress { get; set; } = 1.0f;
    public bool IsFading { get; set; }
    public int FadeDurationMs { get; set; } = 1000;
    public int QuakeAmplitude { get; set; }
    public float QuakeTimerMs { get; set; }
}

public sealed class AudioState
{
    public string CurrentBgm { get; set; } = "";
    public List<string> PendingSe { get; set; } = new();
    public int BgmVolume { get; set; } = 100;
    public int SeVolume { get; set; } = 100;
    public float BgmFadeOutDurationMs { get; set; }
    public float BgmFadeOutTimerMs { get; set; }
}

public sealed class TextWindowState
{
    public int DefaultTextboxX { get; set; } = 50;
    public int DefaultTextboxY { get; set; } = 500;
    public int DefaultTextboxW { get; set; } = 1180;
    public int DefaultTextboxH { get; set; } = 200;
    public int DefaultFontSize { get; set; } = 32;
    public string DefaultTextColor { get; set; } = "#ffffff";
    public string DefaultTextboxBgColor { get; set; } = "#000000";
    public int DefaultTextboxBgAlpha { get; set; } = 180;
    public bool TextboxVisible { get; set; } = true;
    public bool UseManualTextLayout { get; set; }
    public int TextTargetSpriteId { get; set; } = -1;
    public int TextboxBackgroundSpriteId { get; set; } = -1;
    public bool CompatAutoUi { get; set; }
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
}

public sealed class ChoiceStyleState
{
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
}

public sealed class TextRuntimeState
{
    public int TextSpeedMs { get; set; }
    public string CurrentTextBuffer { get; set; } = "";
    public int DisplayedTextLength { get; set; }
    public string TextAdvanceMode { get; set; } = "complete";
    public float TextAdvanceRatio { get; set; } = 1.0f;
    public float TextTimerMs { get; set; }
    public bool IsWaitingPageClear { get; set; }
    public List<string> TextHistory { get; set; } = new();
    public int TextHistoryStartNumber { get; set; } = 1;
    public bool BacklogEnabled { get; set; } = true;
    public bool KidokuMode { get; set; } = true;
    public HashSet<string> ReadKeys { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public bool CurrentInstructionWasRead { get; set; }
    public string DefaultTextShadowColor { get; set; } = "";
    public int DefaultTextShadowX { get; set; }
    public int DefaultTextShadowY { get; set; }
    public string DefaultTextOutlineColor { get; set; } = "";
    public int DefaultTextOutlineSize { get; set; }
    public string DefaultTextEffect { get; set; } = "none";
    public float DefaultTextEffectStrength { get; set; }
    public float DefaultTextEffectSpeed { get; set; } = 8f;
}

public sealed class PlaybackControlState
{
    public bool AutoMode { get; set; }
    public int AutoModeWaitTimeMs { get; set; } = 2000;
    public float AutoModeTimerMs { get; set; }
    public bool SkipMode { get; set; }
    public bool ForceSkipMode { get; set; }
    public int SkipAdvancePerFrame { get; set; } = 3;
    public int ForceSkipAdvancePerFrame { get; set; } = 64;
    public bool SkipUnread { get; set; }
}

public sealed class MenuRuntimeState
{
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
    public bool SaveMode { get; set; } = true;
    public bool ShowSystemCloseButton { get; set; } = true;
    public bool ShowSystemResetButton { get; set; }
    public bool ShowSystemSkipButton { get; set; }
    public bool ShowSystemSaveButton { get; set; }
    public bool ShowSystemLoadButton { get; set; }
    public int RightMenuWidth { get; set; } = 360;
    public string RightMenuAlign { get; set; } = "center";
    public int SaveLoadColumns { get; set; } = 2;
    public int SaveLoadWidth { get; set; } = 760;
    public int BacklogWidth { get; set; } = 860;
    public int SettingsWidth { get; set; } = 520;
    public string MenuFillColor { get; set; } = "#000000";
    public int MenuFillAlpha { get; set; } = 238;
    public string MenuTextColor { get; set; } = "#f5f5f5";
    public string MenuLineColor { get; set; } = "#f5f5f5";
    public int MenuCornerRadius { get; set; } = 16;
}

public sealed class UiRuntimeState
{
    public bool RequestClose { get; set; }
    public bool RequestReset { get; set; }
    public bool ShowClickCursor { get; set; } = true;
    public string ClickCursorMode { get; set; } = "engine";
    public string ClickCursorPath { get; set; } = "";
    public int ClickCursorOffsetX { get; set; } = 12;
    public int ClickCursorOffsetY { get; set; } = 8;
    public float ClickCursorSize { get; set; } = 12f;
    public string ClickCursorColor { get; set; } = "#ffffff";
    public bool UseMsaa { get; set; }
    public bool UseAnisotropicFiltering { get; set; } = true;
}

public sealed class UiCompositionState
{
    public Dictionary<int, List<int>> Groups { get; set; } = new();
    public Dictionary<int, string> Layouts { get; set; } = new();
    public Dictionary<int, string> Anchors { get; set; } = new();
    public Dictionary<string, string> Events { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> Hotkeys { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<int> HoverActive { get; set; } = new();
}

public sealed class EngineSettingsState
{
    public int WindowWidth { get; set; } = 1280;
    public int WindowHeight { get; set; } = 720;
    public string Title { get; set; } = "Aria Engine";
    public string FontPath { get; set; } = "";
    public int FontAtlasSize { get; set; } = 192;
    public string MainScript { get; set; } = "assets/scripts/main.aria";
    public bool DebugMode { get; set; }
    public TextureFilter FontFilter { get; set; } = TextureFilter.Bilinear;
}

public sealed class UiQualityState
{
    public string Quality { get; set; } = "high";
    public bool SmoothMotion { get; set; } = true;
    public bool SubpixelRendering { get; set; } = true;
    public bool HighQualityTextures { get; set; } = true;
    public int RoundedRectSegments { get; set; } = 64;
    public float MotionResponse { get; set; } = 14f;
}

public sealed class SceneRuntimeState
{
    public GameScene CurrentScene { get; set; } = GameScene.TitleScreen;
    public Dictionary<string, object> SceneData { get; set; } = new();
    public bool IsTransitioning { get; set; }
}

public sealed class SaveRuntimeState
{
    public TimeSpan TotalPlayTime { get; set; } = TimeSpan.Zero;
    public DateTime SessionStartTime { get; set; } = DateTime.Now;
    public string CurrentChapter { get; set; } = "";
    public int CurrentProgress { get; set; }
}

public sealed class FlagRuntimeState
{
    public Dictionary<string, bool> Flags { get; set; } = new();
    public Dictionary<string, bool> SaveFlags { get; set; } = new();
    public Dictionary<string, bool> VolatileFlags { get; set; } = new();
    public Dictionary<string, int> Counters { get; set; } = new();
    public ChapterInfo? CurrentChapterDefinition { get; set; }
}

public class GameState
{
    [JsonIgnore] public RegisterState RegisterState { get; } = new();
    [JsonIgnore] public VmExecutionState Execution { get; } = new();
    [JsonIgnore] public InteractionState Interaction { get; } = new();
    [JsonIgnore] public RenderState Render { get; } = new();
    [JsonIgnore] public AudioState Audio { get; } = new();
    [JsonIgnore] public TextWindowState TextWindow { get; } = new();
    [JsonIgnore] public ChoiceStyleState ChoiceStyle { get; } = new();
    [JsonIgnore] public TextRuntimeState TextRuntime { get; } = new();
    [JsonIgnore] public PlaybackControlState Playback { get; } = new();
    [JsonIgnore] public MenuRuntimeState MenuRuntime { get; } = new();
    [JsonIgnore] public UiRuntimeState UiRuntime { get; } = new();
    [JsonIgnore] public UiCompositionState UiComposition { get; } = new();
    [JsonIgnore] public EngineSettingsState EngineSettings { get; } = new();
    [JsonIgnore] public UiQualityState UiQuality { get; } = new();
    [JsonIgnore] public SceneRuntimeState SceneRuntime { get; } = new();
    [JsonIgnore] public SaveRuntimeState SaveRuntime { get; } = new();
    [JsonIgnore] public FlagRuntimeState FlagRuntime { get; } = new();

    public int ProgramCounter { get => Execution.ProgramCounter; set => Execution.ProgramCounter = value; }
    public Dictionary<string, int> Registers { get => RegisterState.Registers; set => RegisterState.Registers = value; }
    public Dictionary<string, string> StringRegisters { get => RegisterState.StringRegisters; set => RegisterState.StringRegisters = value; }
    public int CompareFlag { get => Execution.CompareFlag; set => Execution.CompareFlag = value; }
    public Stack<int> CallStack { get => Execution.CallStack; set => Execution.CallStack = value; }
    public Stack<string> ParamStack { get => Execution.ParamStack; set => Execution.ParamStack = value; }
    public Stack<LoopState> LoopStack { get => Execution.LoopStack; set => Execution.LoopStack = value; }
    public int ButtonTimeoutMs { get => Interaction.ButtonTimeoutMs; set => Interaction.ButtonTimeoutMs = value; }
    public float ButtonTimer { get => Interaction.ButtonTimer; set => Interaction.ButtonTimer = value; }
    public string ButtonResultRegister { get => Interaction.ButtonResultRegister; set => Interaction.ButtonResultRegister = value; }
    public Dictionary<int, Sprite> Sprites { get => Render.Sprites; set => Render.Sprites = value; }
    public Dictionary<int, int> SpriteButtonMap { get => Interaction.SpriteButtonMap; set => Interaction.SpriteButtonMap = value; }
    public VmState State { get => Execution.State; set => Execution.State = value; }
    public string CurrentBgm { get => Audio.CurrentBgm; set => Audio.CurrentBgm = value; }
    public List<string> PendingSe { get => Audio.PendingSe; set => Audio.PendingSe = value; }
    public int BgmVolume { get => Audio.BgmVolume; set => Audio.BgmVolume = value; }
    public int SeVolume { get => Audio.SeVolume; set => Audio.SeVolume = value; }
    public float BgmFadeOutDurationMs { get => Audio.BgmFadeOutDurationMs; set => Audio.BgmFadeOutDurationMs = value; }
    public float BgmFadeOutTimerMs { get => Audio.BgmFadeOutTimerMs; set => Audio.BgmFadeOutTimerMs = value; }
    public float FadeProgress { get => Render.FadeProgress; set => Render.FadeProgress = value; }
    public bool IsFading { get => Render.IsFading; set => Render.IsFading = value; }
    public int FadeDurationMs { get => Render.FadeDurationMs; set => Render.FadeDurationMs = value; }
    public int QuakeAmplitude { get => Render.QuakeAmplitude; set => Render.QuakeAmplitude = value; }
    public float QuakeTimerMs { get => Render.QuakeTimerMs; set => Render.QuakeTimerMs = value; }
    public int DefaultTextboxX { get => TextWindow.DefaultTextboxX; set => TextWindow.DefaultTextboxX = value; }
    public int DefaultTextboxY { get => TextWindow.DefaultTextboxY; set => TextWindow.DefaultTextboxY = value; }
    public int DefaultTextboxW { get => TextWindow.DefaultTextboxW; set => TextWindow.DefaultTextboxW = value; }
    public int DefaultTextboxH { get => TextWindow.DefaultTextboxH; set => TextWindow.DefaultTextboxH = value; }
    public int DefaultFontSize { get => TextWindow.DefaultFontSize; set => TextWindow.DefaultFontSize = value; }
    public string DefaultTextColor { get => TextWindow.DefaultTextColor; set => TextWindow.DefaultTextColor = value; }
    public string DefaultTextboxBgColor { get => TextWindow.DefaultTextboxBgColor; set => TextWindow.DefaultTextboxBgColor = value; }
    public int DefaultTextboxBgAlpha { get => TextWindow.DefaultTextboxBgAlpha; set => TextWindow.DefaultTextboxBgAlpha = value; }
    public bool TextboxVisible { get => TextWindow.TextboxVisible; set => TextWindow.TextboxVisible = value; }
    public bool UseManualTextLayout { get => TextWindow.UseManualTextLayout; set => TextWindow.UseManualTextLayout = value; }
    public int TextTargetSpriteId { get => TextWindow.TextTargetSpriteId; set => TextWindow.TextTargetSpriteId = value; }
    public int TextboxBackgroundSpriteId { get => TextWindow.TextboxBackgroundSpriteId; set => TextWindow.TextboxBackgroundSpriteId = value; }
    public bool CompatAutoUi { get => TextWindow.CompatAutoUi; set => TextWindow.CompatAutoUi = value; }
    public int DefaultTextboxPaddingX { get => TextWindow.DefaultTextboxPaddingX; set => TextWindow.DefaultTextboxPaddingX = value; }
    public int DefaultTextboxPaddingY { get => TextWindow.DefaultTextboxPaddingY; set => TextWindow.DefaultTextboxPaddingY = value; }
    public int DefaultTextboxCornerRadius { get => TextWindow.DefaultTextboxCornerRadius; set => TextWindow.DefaultTextboxCornerRadius = value; }
    public string DefaultTextboxBorderColor { get => TextWindow.DefaultTextboxBorderColor; set => TextWindow.DefaultTextboxBorderColor = value; }
    public int DefaultTextboxBorderWidth { get => TextWindow.DefaultTextboxBorderWidth; set => TextWindow.DefaultTextboxBorderWidth = value; }
    public int DefaultTextboxBorderOpacity { get => TextWindow.DefaultTextboxBorderOpacity; set => TextWindow.DefaultTextboxBorderOpacity = value; }
    public string DefaultTextboxShadowColor { get => TextWindow.DefaultTextboxShadowColor; set => TextWindow.DefaultTextboxShadowColor = value; }
    public int DefaultTextboxShadowOffsetX { get => TextWindow.DefaultTextboxShadowOffsetX; set => TextWindow.DefaultTextboxShadowOffsetX = value; }
    public int DefaultTextboxShadowOffsetY { get => TextWindow.DefaultTextboxShadowOffsetY; set => TextWindow.DefaultTextboxShadowOffsetY = value; }
    public int DefaultTextboxShadowAlpha { get => TextWindow.DefaultTextboxShadowAlpha; set => TextWindow.DefaultTextboxShadowAlpha = value; }
    public int ChoiceWidth { get => ChoiceStyle.ChoiceWidth; set => ChoiceStyle.ChoiceWidth = value; }
    public int ChoiceHeight { get => ChoiceStyle.ChoiceHeight; set => ChoiceStyle.ChoiceHeight = value; }
    public int ChoiceSpacing { get => ChoiceStyle.ChoiceSpacing; set => ChoiceStyle.ChoiceSpacing = value; }
    public int ChoiceFontSize { get => ChoiceStyle.ChoiceFontSize; set => ChoiceStyle.ChoiceFontSize = value; }
    public string ChoiceTextColor { get => ChoiceStyle.ChoiceTextColor; set => ChoiceStyle.ChoiceTextColor = value; }
    public string ChoiceBgColor { get => ChoiceStyle.ChoiceBgColor; set => ChoiceStyle.ChoiceBgColor = value; }
    public int ChoiceBgAlpha { get => ChoiceStyle.ChoiceBgAlpha; set => ChoiceStyle.ChoiceBgAlpha = value; }
    public string ChoiceHoverColor { get => ChoiceStyle.ChoiceHoverColor; set => ChoiceStyle.ChoiceHoverColor = value; }
    public int ChoiceCornerRadius { get => ChoiceStyle.ChoiceCornerRadius; set => ChoiceStyle.ChoiceCornerRadius = value; }
    public string ChoiceBorderColor { get => ChoiceStyle.ChoiceBorderColor; set => ChoiceStyle.ChoiceBorderColor = value; }
    public int ChoiceBorderWidth { get => ChoiceStyle.ChoiceBorderWidth; set => ChoiceStyle.ChoiceBorderWidth = value; }
    public int ChoiceBorderOpacity { get => ChoiceStyle.ChoiceBorderOpacity; set => ChoiceStyle.ChoiceBorderOpacity = value; }
    public int ChoicePaddingX { get => ChoiceStyle.ChoicePaddingX; set => ChoiceStyle.ChoicePaddingX = value; }
    public int TextSpeedMs { get => TextRuntime.TextSpeedMs; set => TextRuntime.TextSpeedMs = value; }
    public string CurrentTextBuffer { get => TextRuntime.CurrentTextBuffer; set => TextRuntime.CurrentTextBuffer = value; }
    public int DisplayedTextLength { get => TextRuntime.DisplayedTextLength; set => TextRuntime.DisplayedTextLength = value; }
    public string TextAdvanceMode { get => TextRuntime.TextAdvanceMode; set => TextRuntime.TextAdvanceMode = value; }
    public float TextAdvanceRatio { get => TextRuntime.TextAdvanceRatio; set => TextRuntime.TextAdvanceRatio = value; }
    public float TextTimerMs { get => TextRuntime.TextTimerMs; set => TextRuntime.TextTimerMs = value; }
    public bool IsWaitingPageClear { get => TextRuntime.IsWaitingPageClear; set => TextRuntime.IsWaitingPageClear = value; }
    public List<string> TextHistory { get => TextRuntime.TextHistory; set => TextRuntime.TextHistory = value; }
    public int TextHistoryStartNumber { get => TextRuntime.TextHistoryStartNumber; set => TextRuntime.TextHistoryStartNumber = value; }
    public bool AutoMode { get => Playback.AutoMode; set => Playback.AutoMode = value; }
    public int AutoModeWaitTimeMs { get => Playback.AutoModeWaitTimeMs; set => Playback.AutoModeWaitTimeMs = value; }
    public float AutoModeTimerMs { get => Playback.AutoModeTimerMs; set => Playback.AutoModeTimerMs = value; }
    public bool SkipMode { get => Playback.SkipMode; set => Playback.SkipMode = value; }
    public bool ForceSkipMode { get => Playback.ForceSkipMode; set => Playback.ForceSkipMode = value; }
    public int SkipAdvancePerFrame { get => Playback.SkipAdvancePerFrame; set => Playback.SkipAdvancePerFrame = value; }
    public int ForceSkipAdvancePerFrame { get => Playback.ForceSkipAdvancePerFrame; set => Playback.ForceSkipAdvancePerFrame = value; }
    public bool SkipUnread { get => Playback.SkipUnread; set => Playback.SkipUnread = value; }
    public bool BacklogEnabled { get => TextRuntime.BacklogEnabled; set => TextRuntime.BacklogEnabled = value; }
    public bool KidokuMode { get => TextRuntime.KidokuMode; set => TextRuntime.KidokuMode = value; }
    public HashSet<string> ReadKeys { get => TextRuntime.ReadKeys; set => TextRuntime.ReadKeys = value; }
    public bool CurrentInstructionWasRead { get => TextRuntime.CurrentInstructionWasRead; set => TextRuntime.CurrentInstructionWasRead = value; }
    public string DefaultTextShadowColor { get => TextRuntime.DefaultTextShadowColor; set => TextRuntime.DefaultTextShadowColor = value; }
    public int DefaultTextShadowX { get => TextRuntime.DefaultTextShadowX; set => TextRuntime.DefaultTextShadowX = value; }
    public int DefaultTextShadowY { get => TextRuntime.DefaultTextShadowY; set => TextRuntime.DefaultTextShadowY = value; }
    public string DefaultTextOutlineColor { get => TextRuntime.DefaultTextOutlineColor; set => TextRuntime.DefaultTextOutlineColor = value; }
    public int DefaultTextOutlineSize { get => TextRuntime.DefaultTextOutlineSize; set => TextRuntime.DefaultTextOutlineSize = value; }
    public string DefaultTextEffect { get => TextRuntime.DefaultTextEffect; set => TextRuntime.DefaultTextEffect = value; }
    public float DefaultTextEffectStrength { get => TextRuntime.DefaultTextEffectStrength; set => TextRuntime.DefaultTextEffectStrength = value; }
    public float DefaultTextEffectSpeed { get => TextRuntime.DefaultTextEffectSpeed; set => TextRuntime.DefaultTextEffectSpeed = value; }
    public string RightMenuLabel { get => MenuRuntime.RightMenuLabel; set => MenuRuntime.RightMenuLabel = value; }
    public List<RightMenuEntry> RightMenuEntries { get => MenuRuntime.RightMenuEntries; set => MenuRuntime.RightMenuEntries = value; }
    public bool SaveMode { get => MenuRuntime.SaveMode; set => MenuRuntime.SaveMode = value; }
    public bool RequestClose { get => UiRuntime.RequestClose; set => UiRuntime.RequestClose = value; }
    public bool RequestReset { get => UiRuntime.RequestReset; set => UiRuntime.RequestReset = value; }
    public bool ShowClickCursor { get => UiRuntime.ShowClickCursor; set => UiRuntime.ShowClickCursor = value; }
    public string ClickCursorMode { get => UiRuntime.ClickCursorMode; set => UiRuntime.ClickCursorMode = value; }
    public string ClickCursorPath { get => UiRuntime.ClickCursorPath; set => UiRuntime.ClickCursorPath = value; }
    public int ClickCursorOffsetX { get => UiRuntime.ClickCursorOffsetX; set => UiRuntime.ClickCursorOffsetX = value; }
    public int ClickCursorOffsetY { get => UiRuntime.ClickCursorOffsetY; set => UiRuntime.ClickCursorOffsetY = value; }
    public float ClickCursorSize { get => UiRuntime.ClickCursorSize; set => UiRuntime.ClickCursorSize = value; }
    public string ClickCursorColor { get => UiRuntime.ClickCursorColor; set => UiRuntime.ClickCursorColor = value; }
    public bool ShowSystemCloseButton { get => MenuRuntime.ShowSystemCloseButton; set => MenuRuntime.ShowSystemCloseButton = value; }
    public bool ShowSystemResetButton { get => MenuRuntime.ShowSystemResetButton; set => MenuRuntime.ShowSystemResetButton = value; }
    public bool ShowSystemSkipButton { get => MenuRuntime.ShowSystemSkipButton; set => MenuRuntime.ShowSystemSkipButton = value; }
    public bool ShowSystemSaveButton { get => MenuRuntime.ShowSystemSaveButton; set => MenuRuntime.ShowSystemSaveButton = value; }
    public bool ShowSystemLoadButton { get => MenuRuntime.ShowSystemLoadButton; set => MenuRuntime.ShowSystemLoadButton = value; }
    public int RightMenuWidth { get => MenuRuntime.RightMenuWidth; set => MenuRuntime.RightMenuWidth = value; }
    public string RightMenuAlign { get => MenuRuntime.RightMenuAlign; set => MenuRuntime.RightMenuAlign = value; }
    public int SaveLoadColumns { get => MenuRuntime.SaveLoadColumns; set => MenuRuntime.SaveLoadColumns = value; }
    public int SaveLoadWidth { get => MenuRuntime.SaveLoadWidth; set => MenuRuntime.SaveLoadWidth = value; }
    public int BacklogWidth { get => MenuRuntime.BacklogWidth; set => MenuRuntime.BacklogWidth = value; }
    public int SettingsWidth { get => MenuRuntime.SettingsWidth; set => MenuRuntime.SettingsWidth = value; }
    public string MenuFillColor { get => MenuRuntime.MenuFillColor; set => MenuRuntime.MenuFillColor = value; }
    public int MenuFillAlpha { get => MenuRuntime.MenuFillAlpha; set => MenuRuntime.MenuFillAlpha = value; }
    public string MenuTextColor { get => MenuRuntime.MenuTextColor; set => MenuRuntime.MenuTextColor = value; }
    public string MenuLineColor { get => MenuRuntime.MenuLineColor; set => MenuRuntime.MenuLineColor = value; }
    public int MenuCornerRadius { get => MenuRuntime.MenuCornerRadius; set => MenuRuntime.MenuCornerRadius = value; }
    public Dictionary<int, List<int>> UiGroups { get => UiComposition.Groups; set => UiComposition.Groups = value; }
    public Dictionary<int, string> UiLayouts { get => UiComposition.Layouts; set => UiComposition.Layouts = value; }
    public Dictionary<int, string> UiAnchors { get => UiComposition.Anchors; set => UiComposition.Anchors = value; }
    public Dictionary<string, string> UiEvents { get => UiComposition.Events; set => UiComposition.Events = value; }
    public Dictionary<string, string> UiHotkeys { get => UiComposition.Hotkeys; set => UiComposition.Hotkeys = value; }
    public HashSet<int> UiHoverActive { get => UiComposition.HoverActive; set => UiComposition.HoverActive = value; }
    public bool UseMsaa { get => UiRuntime.UseMsaa; set => UiRuntime.UseMsaa = value; }
    public bool UseAnisotropicFiltering { get => UiRuntime.UseAnisotropicFiltering; set => UiRuntime.UseAnisotropicFiltering = value; }
    public string UiQualityMode { get => UiQuality.Quality; set => UiQuality.Quality = value; }
    public bool SmoothUiMotion { get => UiQuality.SmoothMotion; set => UiQuality.SmoothMotion = value; }
    public bool SubpixelUiRendering { get => UiQuality.SubpixelRendering; set => UiQuality.SubpixelRendering = value; }
    public bool HighQualityUiTextures { get => UiQuality.HighQualityTextures; set => UiQuality.HighQualityTextures = value; }
    public int RoundedRectSegments { get => UiQuality.RoundedRectSegments; set => UiQuality.RoundedRectSegments = value; }
    public float UiMotionResponse { get => UiQuality.MotionResponse; set => UiQuality.MotionResponse = value; }
    public float DelayTimerMs { get => Execution.DelayTimerMs; set => Execution.DelayTimerMs = value; }
    public float ScriptTimerMs { get => Execution.ScriptTimerMs; set => Execution.ScriptTimerMs = value; }
    public int WindowWidth { get => EngineSettings.WindowWidth; set => EngineSettings.WindowWidth = value; }
    public int WindowHeight { get => EngineSettings.WindowHeight; set => EngineSettings.WindowHeight = value; }
    public string Title { get => EngineSettings.Title; set => EngineSettings.Title = value; }
    public string FontPath { get => EngineSettings.FontPath; set => EngineSettings.FontPath = value; }
    public int FontAtlasSize { get => EngineSettings.FontAtlasSize; set => EngineSettings.FontAtlasSize = value; }
    public string MainScript { get => EngineSettings.MainScript; set => EngineSettings.MainScript = value; }
    public bool DebugMode { get => EngineSettings.DebugMode; set => EngineSettings.DebugMode = value; }
    public GameScene CurrentScene { get => SceneRuntime.CurrentScene; set => SceneRuntime.CurrentScene = value; }
    public Dictionary<string, object> SceneData { get => SceneRuntime.SceneData; set => SceneRuntime.SceneData = value; }
    public bool IsTransitioning { get => SceneRuntime.IsTransitioning; set => SceneRuntime.IsTransitioning = value; }
    public TimeSpan TotalPlayTime { get => SaveRuntime.TotalPlayTime; set => SaveRuntime.TotalPlayTime = value; }
    public DateTime SessionStartTime { get => SaveRuntime.SessionStartTime; set => SaveRuntime.SessionStartTime = value; }
    public string CurrentChapter { get => SaveRuntime.CurrentChapter; set => SaveRuntime.CurrentChapter = value; }
    public int CurrentProgress { get => SaveRuntime.CurrentProgress; set => SaveRuntime.CurrentProgress = value; }
    public TextureFilter FontFilter { get => EngineSettings.FontFilter; set => EngineSettings.FontFilter = value; }
    public Dictionary<string, bool> Flags { get => FlagRuntime.Flags; set => FlagRuntime.Flags = value; }
    public Dictionary<string, bool> SaveFlags { get => FlagRuntime.SaveFlags; set => FlagRuntime.SaveFlags = value; }
    public Dictionary<string, bool> VolatileFlags { get => FlagRuntime.VolatileFlags; set => FlagRuntime.VolatileFlags = value; }
    public Dictionary<string, int> Counters { get => FlagRuntime.Counters; set => FlagRuntime.Counters = value; }
    public ChapterInfo? CurrentChapterDefinition { get => FlagRuntime.CurrentChapterDefinition; set => FlagRuntime.CurrentChapterDefinition = value; }
}
