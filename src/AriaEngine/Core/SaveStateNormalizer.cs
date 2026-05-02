using System;
using System.Collections.Generic;
using System.Linq;

namespace AriaEngine.Core;

/// <summary>
/// セーブデータのロード時の状態正規化を管理するクラス
/// </summary>
public class SaveStateNormalizer
{
    private readonly GameState _state;
    private readonly ErrorReporter _reporter;
    private string _currentScriptFile = "";

    public string CurrentScriptFile => _currentScriptFile;

    public SaveStateNormalizer(GameState state, ErrorReporter reporter)
    {
        _state = state;
        _reporter = reporter;
    }

    /// <summary>
    /// ロードされたセーブデータを正規化して適用する
    /// </summary>
    public void NormalizeLoadedState(SaveData data)
    {
        var loaded = data.State;

        // 基本的な実行状態
        _state.Execution.ProgramCounter = loaded.Execution.ProgramCounter;
        _state.Execution.State = loaded.Execution.State;
        _state.SceneRuntime.CurrentScene = loaded.SceneRuntime.CurrentScene;

        // レジスタのマージ（永続レジスタを維持しつつ、セーブデータのレジスタを適用）
        var mergedRegisters = _state.RegisterState.Registers
            .Where(pair => RegisterStoragePolicy.IsPersistent(pair.Key))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);

        foreach (var pair in loaded.RegisterState.Registers)
        {
            if (RegisterStoragePolicy.IsSaveStored(pair.Key))
            {
                mergedRegisters[RegisterStoragePolicy.Normalize(pair.Key)] = pair.Value;
            }
        }
        _state.RegisterState.Registers = mergedRegisters;

        // 文字列レジスタとフラグ
        _state.RegisterState.StringRegisters = new Dictionary<string, string>(loaded.RegisterState.StringRegisters, StringComparer.OrdinalIgnoreCase);
        _state.FlagRuntime.SaveFlags = new Dictionary<string, bool>(loaded.FlagRuntime.SaveFlags, StringComparer.OrdinalIgnoreCase);
        _state.FlagRuntime.VolatileFlags = new Dictionary<string, bool>(loaded.FlagRuntime.VolatileFlags, StringComparer.OrdinalIgnoreCase);
        _state.FlagRuntime.Counters = new Dictionary<string, int>(loaded.FlagRuntime.Counters, StringComparer.OrdinalIgnoreCase);

        // スプライトとボタン
        _state.Render.Sprites = new FastSpriteDictionary(loaded.Render.Sprites);
        _state.Interaction.SpriteButtonMap = new Dictionary<int, int>(loaded.Interaction.SpriteButtonMap);

        // スタック
        _state.Execution.CallStack = new Stack<int>(loaded.Execution.CallStack.Reverse());
        _state.Execution.ParamStack = new Stack<string>(loaded.Execution.ParamStack.Reverse());
        _state.Execution.LoopStack = loaded.Execution.LoopStack != null ? new Stack<LoopState>(loaded.Execution.LoopStack.Reverse()) : new Stack<LoopState>();

        // オーディオ状態
        _state.Audio.CurrentBgm = loaded.Audio.CurrentBgm;
        _state.Audio.BgmVolume = loaded.Audio.BgmVolume;
        _state.Audio.SeVolume = loaded.Audio.SeVolume;

        // テキスト状態
        _state.TextWindow.TextboxVisible = loaded.TextWindow.TextboxVisible;
        _state.TextRuntime.CurrentTextBuffer = loaded.TextRuntime.CurrentTextBuffer;
        _state.TextRuntime.DisplayedTextLength = Math.Clamp(loaded.TextRuntime.DisplayedTextLength, 0, _state.TextRuntime.CurrentTextBuffer.Length);
        _state.TextRuntime.TextAdvanceMode = loaded.TextRuntime.TextAdvanceMode;
        _state.TextRuntime.TextAdvanceRatio = loaded.TextRuntime.TextAdvanceRatio;
        _state.TextRuntime.TextTimerMs = 0f;
        _state.TextRuntime.IsWaitingPageClear = loaded.TextRuntime.IsWaitingPageClear;
        _state.TextRuntime.TextHistory = new List<BacklogEntry>(loaded.TextRuntime.TextHistory);
        _state.TextRuntime.TextHistoryStartNumber = Math.Max(1, loaded.TextRuntime.TextHistoryStartNumber);
        _state.TextWindow.TextTargetSpriteId = loaded.TextWindow.TextTargetSpriteId;
        _state.TextWindow.TextboxBackgroundSpriteId = loaded.TextWindow.TextboxBackgroundSpriteId;
        _state.TextWindow.UseManualTextLayout = loaded.TextWindow.UseManualTextLayout;
        _state.TextWindow.CompatAutoUi = loaded.TextWindow.CompatAutoUi;

        // テキストボックス設定
        NormalizeTextboxSettings(loaded);

        // 選択肢設定
        NormalizeChoiceSettings(loaded);

        // フォントとテキスト設定
        _state.EngineSettings.FontFilter = loaded.EngineSettings.FontFilter;
        _state.TextRuntime.TextSpeedMs = loaded.TextRuntime.TextSpeedMs;
        NormalizeTextEffectSettings(loaded);
        NormalizeSkipSettings(loaded);

        // クリックカーソル設定
        NormalizeCursorSettings(loaded);

        // メニュー設定
        NormalizeMenuSettings(loaded);

        // UI構成
        _state.UiComposition.Groups = loaded.UiComposition.Groups.ToDictionary(pair => pair.Key, pair => new List<int>(pair.Value));
        _state.UiComposition.Layouts = new Dictionary<int, string>(loaded.UiComposition.Layouts);
        _state.UiComposition.Anchors = new Dictionary<int, string>(loaded.UiComposition.Anchors);
        _state.UiComposition.Events = new Dictionary<string, string>(loaded.UiComposition.Events, StringComparer.OrdinalIgnoreCase);
        _state.UiComposition.Hotkeys = new Dictionary<string, string>(loaded.UiComposition.Hotkeys, StringComparer.OrdinalIgnoreCase);
        _state.UiComposition.HoverActive = new HashSet<int>();

        // シーン情報
        _state.SaveRuntime.CurrentChapter = loaded.SaveRuntime.CurrentChapter;
        _state.FlagRuntime.UnlockedCgs = new HashSet<string>(loaded.FlagRuntime.UnlockedCgs, StringComparer.OrdinalIgnoreCase);
        _currentScriptFile = data.ScriptFile;

        // UI状態を正規化
        NormalizeUiState();
    }

    private void NormalizeTextboxSettings(GameState loaded)
    {
        _state.TextWindow.DefaultTextboxX = loaded.TextWindow.DefaultTextboxX;
        _state.TextWindow.DefaultTextboxY = loaded.TextWindow.DefaultTextboxY;
        _state.TextWindow.DefaultTextboxW = loaded.TextWindow.DefaultTextboxW;
        _state.TextWindow.DefaultTextboxH = loaded.TextWindow.DefaultTextboxH;
        _state.TextWindow.DefaultFontSize = loaded.TextWindow.DefaultFontSize;
        _state.TextWindow.DefaultTextColor = loaded.TextWindow.DefaultTextColor;
        _state.TextWindow.DefaultTextboxBgColor = loaded.TextWindow.DefaultTextboxBgColor;
        _state.TextWindow.DefaultTextboxBgAlpha = loaded.TextWindow.DefaultTextboxBgAlpha;
        _state.TextWindow.DefaultTextboxPaddingX = loaded.TextWindow.DefaultTextboxPaddingX;
        _state.TextWindow.DefaultTextboxPaddingY = loaded.TextWindow.DefaultTextboxPaddingY;
        _state.TextWindow.DefaultTextboxCornerRadius = loaded.TextWindow.DefaultTextboxCornerRadius;
        _state.TextWindow.DefaultTextboxBorderColor = loaded.TextWindow.DefaultTextboxBorderColor;
        _state.TextWindow.DefaultTextboxBorderWidth = loaded.TextWindow.DefaultTextboxBorderWidth;
        _state.TextWindow.DefaultTextboxBorderOpacity = loaded.TextWindow.DefaultTextboxBorderOpacity;
        _state.TextWindow.DefaultTextboxShadowColor = loaded.TextWindow.DefaultTextboxShadowColor;
        _state.TextWindow.DefaultTextboxShadowOffsetX = loaded.TextWindow.DefaultTextboxShadowOffsetX;
        _state.TextWindow.DefaultTextboxShadowOffsetY = loaded.TextWindow.DefaultTextboxShadowOffsetY;
        _state.TextWindow.DefaultTextboxShadowAlpha = loaded.TextWindow.DefaultTextboxShadowAlpha;
    }

    private void NormalizeChoiceSettings(GameState loaded)
    {
        _state.ChoiceStyle.ChoiceWidth = loaded.ChoiceStyle.ChoiceWidth;
        _state.ChoiceStyle.ChoiceHeight = loaded.ChoiceStyle.ChoiceHeight;
        _state.ChoiceStyle.ChoiceSpacing = loaded.ChoiceStyle.ChoiceSpacing;
        _state.ChoiceStyle.ChoiceFontSize = loaded.ChoiceStyle.ChoiceFontSize;
        _state.ChoiceStyle.ChoiceTextColor = loaded.ChoiceStyle.ChoiceTextColor;
        _state.ChoiceStyle.ChoiceBgColor = loaded.ChoiceStyle.ChoiceBgColor;
        _state.ChoiceStyle.ChoiceBgAlpha = loaded.ChoiceStyle.ChoiceBgAlpha;
        _state.ChoiceStyle.ChoiceHoverColor = loaded.ChoiceStyle.ChoiceHoverColor;
        _state.ChoiceStyle.ChoiceCornerRadius = loaded.ChoiceStyle.ChoiceCornerRadius;
        _state.ChoiceStyle.ChoiceBorderColor = loaded.ChoiceStyle.ChoiceBorderColor;
        _state.ChoiceStyle.ChoiceBorderWidth = loaded.ChoiceStyle.ChoiceBorderWidth;
        _state.ChoiceStyle.ChoiceBorderOpacity = loaded.ChoiceStyle.ChoiceBorderOpacity;
        _state.ChoiceStyle.ChoicePaddingX = loaded.ChoiceStyle.ChoicePaddingX;
    }

    private void NormalizeTextEffectSettings(GameState loaded)
    {
        _state.TextRuntime.DefaultTextShadowColor = loaded.TextRuntime.DefaultTextShadowColor;
        _state.TextRuntime.DefaultTextShadowX = loaded.TextRuntime.DefaultTextShadowX;
        _state.TextRuntime.DefaultTextShadowY = loaded.TextRuntime.DefaultTextShadowY;
        _state.TextRuntime.DefaultTextOutlineColor = loaded.TextRuntime.DefaultTextOutlineColor;
        _state.TextRuntime.DefaultTextOutlineSize = loaded.TextRuntime.DefaultTextOutlineSize;
        _state.TextRuntime.DefaultTextEffect = loaded.TextRuntime.DefaultTextEffect;
        _state.TextRuntime.DefaultTextEffectStrength = loaded.TextRuntime.DefaultTextEffectStrength;
        _state.TextRuntime.DefaultTextEffectSpeed = loaded.TextRuntime.DefaultTextEffectSpeed;
    }

    private void NormalizeSkipSettings(GameState loaded)
    {
        _state.Playback.SkipAdvancePerFrame = loaded.Playback.SkipAdvancePerFrame;
        _state.Playback.ForceSkipAdvancePerFrame = loaded.Playback.ForceSkipAdvancePerFrame;
    }

    private void NormalizeCursorSettings(GameState loaded)
    {
        _state.UiRuntime.ShowClickCursor = loaded.UiRuntime.ShowClickCursor;
        _state.UiRuntime.ClickCursorMode = loaded.UiRuntime.ClickCursorMode;
        _state.UiRuntime.ClickCursorPath = loaded.UiRuntime.ClickCursorPath;
        _state.UiRuntime.ClickCursorOffsetX = loaded.UiRuntime.ClickCursorOffsetX;
        _state.UiRuntime.ClickCursorOffsetY = loaded.UiRuntime.ClickCursorOffsetY;
        _state.UiRuntime.ClickCursorSize = loaded.UiRuntime.ClickCursorSize;
        _state.UiRuntime.ClickCursorColor = loaded.UiRuntime.ClickCursorColor;
    }

    private void NormalizeMenuSettings(GameState loaded)
    {
        _state.MenuRuntime.RightMenuWidth = loaded.MenuRuntime.RightMenuWidth;
        _state.MenuRuntime.RightMenuAlign = loaded.MenuRuntime.RightMenuAlign;
        _state.MenuRuntime.SaveLoadColumns = loaded.MenuRuntime.SaveLoadColumns;
        _state.MenuRuntime.SaveLoadWidth = loaded.MenuRuntime.SaveLoadWidth;
        _state.MenuRuntime.BacklogWidth = loaded.MenuRuntime.BacklogWidth;
        _state.MenuRuntime.SettingsWidth = loaded.MenuRuntime.SettingsWidth;
        _state.MenuRuntime.MenuFillColor = loaded.MenuRuntime.MenuFillColor;
        _state.MenuRuntime.MenuFillAlpha = loaded.MenuRuntime.MenuFillAlpha;
        _state.MenuRuntime.MenuTextColor = loaded.MenuRuntime.MenuTextColor;
        _state.MenuRuntime.MenuLineColor = loaded.MenuRuntime.MenuLineColor;
        _state.MenuRuntime.MenuCornerRadius = loaded.MenuRuntime.MenuCornerRadius;
    }

    private void NormalizeUiState()
    {
        // 無効なスプライトIDをクリア
        if (_state.TextWindow.TextboxBackgroundSpriteId >= 0 && !_state.Render.Sprites.ContainsKey(_state.TextWindow.TextboxBackgroundSpriteId))
        {
            _state.TextWindow.TextboxBackgroundSpriteId = -1;
        }

        if (_state.TextWindow.TextTargetSpriteId >= 0 && !_state.Render.Sprites.ContainsKey(_state.TextWindow.TextTargetSpriteId))
        {
            _state.TextWindow.TextTargetSpriteId = -1;
        }

        // ホバー状態をリセット
        foreach (var sprite in _state.Render.Sprites.Values)
        {
            sprite.IsHovered = false;
        }
        _state.Interaction.FocusedButtonId = -1;

        // 無効なボタンのマッピングを削除
        _state.Interaction.SpriteButtonMap = _state.Interaction.SpriteButtonMap
            .Where(pair => _state.Render.Sprites.TryGetValue(pair.Key, out var sprite) && sprite.IsButton)
            .ToDictionary(pair => pair.Key, pair => pair.Value);

        // テキストスプライトを正規化
        NormalizeRuntimeTextSprites();
    }

    private void NormalizeRuntimeTextSprites()
    {
        if (!_state.TextWindow.CompatAutoUi || _state.TextWindow.UseManualTextLayout) return;

        // テキストボックス背景スプライトを更新
        if (_state.TextWindow.TextboxBackgroundSpriteId >= 0 &&
            _state.Render.Sprites.TryGetValue(_state.TextWindow.TextboxBackgroundSpriteId, out var bg) &&
            bg.Type == SpriteType.Rect)
        {
            bg.X = _state.TextWindow.DefaultTextboxX;
            bg.Y = _state.TextWindow.DefaultTextboxY;
            bg.Width = _state.TextWindow.DefaultTextboxW;
            bg.Height = _state.TextWindow.DefaultTextboxH;
            bg.FillColor = _state.TextWindow.DefaultTextboxBgColor;
            bg.FillAlpha = _state.TextWindow.DefaultTextboxBgAlpha;
            bg.CornerRadius = _state.TextWindow.DefaultTextboxCornerRadius;
            bg.BorderColor = _state.TextWindow.DefaultTextboxBorderColor;
            bg.BorderWidth = _state.TextWindow.DefaultTextboxBorderWidth;
            bg.BorderOpacity = _state.TextWindow.DefaultTextboxBorderOpacity;
            bg.ShadowColor = _state.TextWindow.DefaultTextboxShadowColor;
            bg.ShadowOffsetX = _state.TextWindow.DefaultTextboxShadowOffsetX;
            bg.ShadowOffsetY = _state.TextWindow.DefaultTextboxShadowOffsetY;
            bg.ShadowAlpha = _state.TextWindow.DefaultTextboxShadowAlpha;
        }

        // テキストスプライトを更新
        if (_state.TextWindow.TextTargetSpriteId >= 0 &&
            _state.Render.Sprites.TryGetValue(_state.TextWindow.TextTargetSpriteId, out var textSprite) &&
            textSprite.Type == SpriteType.Text)
        {
            textSprite.X = _state.TextWindow.DefaultTextboxX + _state.TextWindow.DefaultTextboxPaddingX;
            textSprite.Y = _state.TextWindow.DefaultTextboxY + _state.TextWindow.DefaultTextboxPaddingY;
            textSprite.Width = _state.TextWindow.DefaultTextboxW - _state.TextWindow.DefaultTextboxPaddingX * 2;
            textSprite.Height = _state.TextWindow.DefaultTextboxH - _state.TextWindow.DefaultTextboxPaddingY * 2;
            textSprite.FontSize = _state.TextWindow.DefaultFontSize;
            textSprite.Color = _state.TextWindow.DefaultTextColor;
            textSprite.TextShadowColor = _state.TextRuntime.DefaultTextShadowColor;
            textSprite.TextShadowX = _state.TextRuntime.DefaultTextShadowX;
            textSprite.TextShadowY = _state.TextRuntime.DefaultTextShadowY;
            textSprite.TextOutlineColor = _state.TextRuntime.DefaultTextOutlineColor;
            textSprite.TextOutlineSize = _state.TextRuntime.DefaultTextOutlineSize;
            textSprite.TextEffect = _state.TextRuntime.DefaultTextEffect;
            textSprite.TextEffectStrength = _state.TextRuntime.DefaultTextEffectStrength;
            textSprite.TextEffectSpeed = _state.TextRuntime.DefaultTextEffectSpeed;
        }
    }
}
