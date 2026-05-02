using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using AriaEngine.Text;
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

public enum TransitionType
{
    Fade,
    SlideLeft,
    SlideRight,
    SlideUp,
    SlideDown,
    WipeCircle
}

public enum VmState
{
    Running,
    WaitingForClick,
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
    public string Icon { get; set; } = "";
}

public sealed class RegisterState
{
    public Dictionary<string, int> Registers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> StringRegisters { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, int[]> Arrays { get; set; } = new();
}

public sealed class VmExecutionState
{
    public int ProgramCounter { get; set; }
    public VmState State { get; set; } = VmState.Running;
    public int CompareFlag { get; set; }
    public Stack<int> CallStack { get; set; } = new();
    public Stack<string> ParamStack { get; set; } = new();
    public Stack<Dictionary<string, string>> RefStack { get; set; } = new();
    public Dictionary<string, string> CurrentRefMap { get; set; } = new();
    public Stack<LoopState> LoopStack { get; set; } = new();
    public Stack<int> TryStack { get; set; } = new();
    public float DelayTimerMs { get; set; }
    public float ScriptTimerMs { get; set; }
    public int LastReturnValue { get; set; }

    // 関数ローカルスコープ
    public Stack<Dictionary<string, int>> LocalIntStacks { get; set; } = new();
    public Stack<Dictionary<string, string>> LocalStringStacks { get; set; } = new();

    // スプライト寿命管理（関数スコープ）
    public Stack<HashSet<int>> SpriteLifetimeStacks { get; set; } = new();

    // Explicit scope tracking for scope blocks (T5)
    public Stack<ScopeFrame> ScopeStack { get; set; } = new();
}

// Represents a single explicit scope frame for variables, sprite lifetimes and deferred actions
public sealed class ScopeFrame
{
    // Local variables scoped to this block
    public Dictionary<string, int> LocalInt { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> LocalString { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    // Sprites created within this scope (lifetime managed by scope)
    public HashSet<int> SpriteIds { get; set; } = new();
    // Instructions to defer until scope exit (executed in LIFO order)
    public List<Instruction> Defer { get; set; } = new();
}

public sealed class InteractionState
{
    public int ButtonTimeoutMs { get; set; }
    public float ButtonTimer { get; set; }
    public string ButtonResultRegister { get; set; } = "0";
    public Dictionary<int, int> SpriteButtonMap { get; set; } = new();
    public int FocusedButtonId { get; set; } = -1;
}

public sealed class RenderState
{
    public FastSpriteDictionary Sprites { get; set; } = new();
    public int BackgroundTimeOfDay { get; set; }
    public string BackgroundTimePreset { get; set; } = "";
    public Dictionary<string, BackgroundTimeMapping> BackgroundTimeMap { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public float FadeProgress { get; set; } = 1.0f;
    public bool IsFading { get; set; }
    public TransitionType TransitionStyle { get; set; } = TransitionType.Fade;
    public int FadeDurationMs { get; set; } = 1000;
    public int QuakeAmplitude { get; set; }
    public float QuakeTimerMs { get; set; }
    public string ScreenTintColor { get; set; } = "#000000";
    public float ScreenTintOpacity { get; set; }
    public float ScreenTintTimerMs { get; set; }
    public float CameraZoom { get; set; } = 1f;
    public float CameraOffsetX { get; set; }
    public float CameraOffsetY { get; set; }
    public string FxProfile { get; set; } = "normal";
    public string FxSkipPolicy { get; set; } = "finish";
    public List<string> ActiveEffects { get; set; } = new();
    public float VignetteStrength { get; set; } = 0f; // 0=disabled, 1=max
}

public sealed class BackgroundTimeMapping
{
    public int TimeOfDay { get; set; }
    public string Preset { get; set; } = "";
}

public sealed class AudioState
{
    public string CurrentBgm { get; set; } = "";
    public List<string> PendingSe { get; set; } = new();
    public int BgmVolume { get; set; } = 100;
    public int SeVolume { get; set; } = 100;
    public float BgmFadeOutDurationMs { get; set; }
    public float BgmFadeOutTimerMs { get; set; }
    public string LastVoicePath { get; set; } = "";
    public bool VoiceWaitRequested { get; set; }
}

public sealed class TextWindowState
{
    public int DefaultTextboxX { get; set; } = 50;
    public int DefaultTextboxY { get; set; } = 500;
    public int DefaultTextboxW { get; set; } = 1180;
    public int DefaultTextboxH { get; set; } = 200;
    public int DefaultFontSize { get; set; } = 32;
    public string DefaultTextColor { get; set; } = "#ffffff";
    public string DefaultTextboxBgColor { get; set; } = UIThemeDefaults.TextboxBgColor;
    public int DefaultTextboxBgAlpha { get; set; } = UIThemeDefaults.TextboxBgAlpha;
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

/// <summary>
/// Lightweight state snapshot captured at a backlog entry point for jump-back.
/// </summary>
public sealed class BacklogStateSnapshot
{
    public Dictionary<string, int> Registers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> StringRegisters { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, bool> Flags { get; set; } = new();
    public Dictionary<string, bool> SaveFlags { get; set; } = new();
    public Dictionary<string, int> Counters { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public FastSpriteDictionary Sprites { get; set; } = new();
    public string CurrentBgm { get; set; } = "";
    public int BgmVolume { get; set; } = 100;
    public int SeVolume { get; set; } = 100;
}

/// <summary>
/// A single entry in the text backlog.
/// </summary>
public sealed class BacklogEntry
{
    public string Text { get; set; } = "";
    public string? VoicePath { get; set; }
    public int ProgramCounter { get; set; }
    public bool IsRead { get; set; }
    public DateTime Timestamp { get; set; }
    public BacklogStateSnapshot? StateSnapshot { get; set; }
}

/// <summary>
/// JSON converter that reads legacy string arrays as BacklogEntry lists.
/// </summary>
public class BacklogEntryListConverter : JsonConverter<List<BacklogEntry>>
{
    public override List<BacklogEntry> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var result = new List<BacklogEntry>();
        if (reader.TokenType != JsonTokenType.StartArray) return result;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndArray) break;
            if (reader.TokenType == JsonTokenType.String)
            {
                result.Add(new BacklogEntry { Text = reader.GetString() ?? "" });
            }
            else if (reader.TokenType == JsonTokenType.StartObject)
            {
                var entry = JsonSerializer.Deserialize<BacklogEntry>(ref reader, options);
                if (entry != null) result.Add(entry);
            }
            else
            {
                reader.Skip();
            }
        }
        return result;
    }

    public override void Write(Utf8JsonWriter writer, List<BacklogEntry> value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        foreach (var entry in value)
        {
            writer.WriteStartObject();
            writer.WriteString(nameof(BacklogEntry.Text), entry.Text);
            if (!string.IsNullOrEmpty(entry.VoicePath))
            {
                writer.WriteString(nameof(BacklogEntry.VoicePath), entry.VoicePath);
            }
            writer.WriteNumber(nameof(BacklogEntry.ProgramCounter), entry.ProgramCounter);
            writer.WriteBoolean(nameof(BacklogEntry.IsRead), entry.IsRead);
            writer.WriteString(nameof(BacklogEntry.Timestamp), entry.Timestamp);
            if (entry.StateSnapshot != null)
            {
                writer.WritePropertyName(nameof(BacklogEntry.StateSnapshot));
                JsonSerializer.Serialize(writer, entry.StateSnapshot, options);
            }
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
    }
}

public sealed class TextRuntimeState
{
    public int TextSpeedMs { get; set; }
    public string CurrentTextBuffer { get; set; } = "";
    public List<TextSegment>? CurrentTextSegments { get; set; }
    public int DisplayedTextLength { get; set; }
    public string TextAdvanceMode { get; set; } = "complete";
    public float TextAdvanceRatio { get; set; } = 1.0f;
    public float TextTimerMs { get; set; }
    public bool IsWaitingPageClear { get; set; }
    [JsonConverter(typeof(BacklogEntryListConverter))]
    public List<BacklogEntry> TextHistory { get; set; } = new();
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
    public int SkipAdvancePerFrame { get; set; } = SkipConstants.SkipAdvancePerFrame;
    public int ForceSkipAdvancePerFrame { get; set; } = SkipConstants.ForceSkipAdvancePerFrame;
    public bool SkipUnread { get; set; }
    public float SkipTimerMs { get; set; }
    public int SkipRateMs { get; set; } = 200; // デフォルト5msg/秒
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
    public Dictionary<string, string> MenuActionOverrides { get; set; } = new(StringComparer.OrdinalIgnoreCase);
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
    public string MenuFillColor { get; set; } = UIThemeDefaults.MenuFillColor;
    public int MenuFillAlpha { get; set; } = UIThemeDefaults.MenuFillAlpha;
    public string MenuTextColor { get; set; } = UIThemeDefaults.MenuTextColor;
    public string MenuLineColor { get; set; } = UIThemeDefaults.MenuLineColor;
    public int MenuCornerRadius { get; set; } = UIThemeDefaults.MenuCornerRadius;
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
    public int FontAtlasSize { get; set; } = FontConstants.DefaultAtlasSize;
    public string MainScript { get; set; } = "assets/scripts/main.aria";
    public bool DebugMode { get; set; }
    public bool ProductionMode { get; set; }
    public TextureFilter FontFilter { get; set; } = TextureFilter.Bilinear;
}

public sealed class UiQualityState
{
    public string Quality { get; set; } = "high";
    public bool SmoothMotion { get; set; } = true;
    public bool SubpixelRendering { get; set; }
    public bool HighQualityTextures { get; set; } = true;
    public int RoundedRectSegments { get; set; } = UiQualityConstants.DefaultRoundedRectSegments;
    public float MotionResponse { get; set; } = UiQualityConstants.DefaultMotionResponse;
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
    public HashSet<string> UnlockedCgs { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, GalleryEntry> GalleryEntries { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    // T22: Total script lines for read rate calculation
    public int TotalScriptLines { get; set; }
}

public class GalleryEntry
{
    public string FlagName { get; set; } = "";
    public string ImagePath { get; set; } = "";
    public string Title { get; set; } = "";
}

public class GameState
{
    public RegisterState RegisterState { get; set; } = new();
    public VmExecutionState Execution { get; set; } = new();
    public InteractionState Interaction { get; set; } = new();
    public RenderState Render { get; set; } = new();
    public AudioState Audio { get; set; } = new();
    public TextWindowState TextWindow { get; set; } = new();
    public ChoiceStyleState ChoiceStyle { get; set; } = new();
    public TextRuntimeState TextRuntime { get; set; } = new();
    public PlaybackControlState Playback { get; set; } = new();
    public MenuRuntimeState MenuRuntime { get; set; } = new();
    public UiRuntimeState UiRuntime { get; set; } = new();
    public UiCompositionState UiComposition { get; set; } = new();
    public EngineSettingsState EngineSettings { get; set; } = new();
    public UiQualityState UiQuality { get; set; } = new();
    public SceneRuntimeState SceneRuntime { get; set; } = new();
    public SaveRuntimeState SaveRuntime { get; set; } = new();
    public FlagRuntimeState FlagRuntime { get; set; } = new();

    // T22: Read rate calculation
    /// <summary>
    /// Returns read rate as a percentage (0-100).
    /// </summary>
    public int GetReadRate()
    {
        if (FlagRuntime.TotalScriptLines <= 0) return 0;
        return (int)Math.Round((double)TextRuntime.ReadKeys.Count / FlagRuntime.TotalScriptLines * 100);
    }

    // T22: CG unlock tracking methods
    /// <summary>
    /// Unlocks a CG with the given ID.
    /// </summary>
    public void UnlockCg(string cgId)
    {
        if (!string.IsNullOrWhiteSpace(cgId))
        {
            FlagRuntime.UnlockedCgs.Add(cgId);
        }
    }

    /// <summary>
    /// Checks if a CG is unlocked.
    /// </summary>
    public bool IsCgUnlocked(string cgId)
    {
        return FlagRuntime.UnlockedCgs.Contains(cgId);
    }

    /// <summary>
    /// Returns CG unlock rate as a percentage (0-100).
    /// </summary>
    public int GetCgUnlockRate(int totalCgs)
    {
        if (totalCgs <= 0) return 0;
        return (int)Math.Round((double)FlagRuntime.UnlockedCgs.Count / totalCgs * 100);
    }

    // Variable declarations from ParseResult (T9: save format versioning)
    public Dictionary<string, string> Declarations { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    // T13: Owned sprite/resource declarations
    // Variables declared with `owned sprite %id` are auto-cleaned up when their scope exits
    public HashSet<string> OwnedSprites { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
