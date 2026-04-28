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
        _state.ProgramCounter = loaded.ProgramCounter;
        _state.State = loaded.State;
        _state.CurrentScene = loaded.CurrentScene;

        // レジスタのマージ（永続レジスタを維持しつつ、セーブデータのレジスタを適用）
        var mergedRegisters = _state.Registers
            .Where(pair => RegisterStoragePolicy.IsPersistent(pair.Key))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);

        foreach (var pair in loaded.Registers)
        {
            if (RegisterStoragePolicy.IsSaveStored(pair.Key))
            {
                mergedRegisters[RegisterStoragePolicy.Normalize(pair.Key)] = pair.Value;
            }
        }
        _state.Registers = mergedRegisters;

        // 文字列レジスタとフラグ
        _state.StringRegisters = new Dictionary<string, string>(loaded.StringRegisters, StringComparer.OrdinalIgnoreCase);
        _state.SaveFlags = new Dictionary<string, bool>(loaded.SaveFlags, StringComparer.OrdinalIgnoreCase);
        _state.VolatileFlags = new Dictionary<string, bool>(loaded.VolatileFlags, StringComparer.OrdinalIgnoreCase);
        _state.Counters = new Dictionary<string, int>(loaded.Counters, StringComparer.OrdinalIgnoreCase);

        // スプライトとボタン
        _state.Sprites = new FastSpriteDictionary(loaded.Sprites);
        _state.SpriteButtonMap = new Dictionary<int, int>(loaded.SpriteButtonMap);

        // スタック
        _state.CallStack = new Stack<int>(loaded.CallStack.Reverse());
        _state.ParamStack = new Stack<string>(loaded.ParamStack.Reverse());
        _state.LoopStack = loaded.LoopStack != null ? new Stack<LoopState>(loaded.LoopStack.Reverse()) : new Stack<LoopState>();

        // オーディオ状態
        _state.CurrentBgm = loaded.CurrentBgm;
        _state.BgmVolume = loaded.BgmVolume;
        _state.SeVolume = loaded.SeVolume;

        // テキスト状態
        _state.TextboxVisible = loaded.TextboxVisible;
        _state.CurrentTextBuffer = loaded.CurrentTextBuffer;
        _state.DisplayedTextLength = Math.Clamp(loaded.DisplayedTextLength, 0, _state.CurrentTextBuffer.Length);
        _state.TextAdvanceMode = loaded.TextAdvanceMode;
        _state.TextAdvanceRatio = loaded.TextAdvanceRatio;
        _state.TextTimerMs = 0f;
        _state.IsWaitingPageClear = loaded.IsWaitingPageClear;
        _state.TextHistory = new List<string>(loaded.TextHistory);
        _state.TextHistoryStartNumber = Math.Max(1, loaded.TextHistoryStartNumber);
        _state.TextTargetSpriteId = loaded.TextTargetSpriteId;
        _state.TextboxBackgroundSpriteId = loaded.TextboxBackgroundSpriteId;
        _state.UseManualTextLayout = loaded.UseManualTextLayout;
        _state.CompatAutoUi = loaded.CompatAutoUi;

        // テキストボックス設定
        NormalizeTextboxSettings(loaded);

        // 選択肢設定
        NormalizeChoiceSettings(loaded);

        // フォントとテキスト設定
        _state.FontFilter = loaded.FontFilter;
        _state.TextSpeedMs = loaded.TextSpeedMs;
        NormalizeTextEffectSettings(loaded);
        NormalizeSkipSettings(loaded);

        // クリックカーソル設定
        NormalizeCursorSettings(loaded);

        // メニュー設定
        NormalizeMenuSettings(loaded);

        // UI構成
        _state.UiGroups = loaded.UiGroups.ToDictionary(pair => pair.Key, pair => new List<int>(pair.Value));
        _state.UiLayouts = new Dictionary<int, string>(loaded.UiLayouts);
        _state.UiAnchors = new Dictionary<int, string>(loaded.UiAnchors);
        _state.UiEvents = new Dictionary<string, string>(loaded.UiEvents, StringComparer.OrdinalIgnoreCase);
        _state.UiHotkeys = new Dictionary<string, string>(loaded.UiHotkeys, StringComparer.OrdinalIgnoreCase);
        _state.UiHoverActive = new HashSet<int>();

        // シーン情報
        _state.CurrentChapter = loaded.CurrentChapter;
        _state.UnlockedCgs = new HashSet<string>(loaded.UnlockedCgs, StringComparer.OrdinalIgnoreCase);
        _currentScriptFile = data.ScriptFile;

        // UI状態を正規化
        NormalizeUiState();
    }

    private void NormalizeTextboxSettings(GameState loaded)
    {
        _state.DefaultTextboxX = loaded.DefaultTextboxX;
        _state.DefaultTextboxY = loaded.DefaultTextboxY;
        _state.DefaultTextboxW = loaded.DefaultTextboxW;
        _state.DefaultTextboxH = loaded.DefaultTextboxH;
        _state.DefaultFontSize = loaded.DefaultFontSize;
        _state.DefaultTextColor = loaded.DefaultTextColor;
        _state.DefaultTextboxBgColor = loaded.DefaultTextboxBgColor;
        _state.DefaultTextboxBgAlpha = loaded.DefaultTextboxBgAlpha;
        _state.DefaultTextboxPaddingX = loaded.DefaultTextboxPaddingX;
        _state.DefaultTextboxPaddingY = loaded.DefaultTextboxPaddingY;
        _state.DefaultTextboxCornerRadius = loaded.DefaultTextboxCornerRadius;
        _state.DefaultTextboxBorderColor = loaded.DefaultTextboxBorderColor;
        _state.DefaultTextboxBorderWidth = loaded.DefaultTextboxBorderWidth;
        _state.DefaultTextboxBorderOpacity = loaded.DefaultTextboxBorderOpacity;
        _state.DefaultTextboxShadowColor = loaded.DefaultTextboxShadowColor;
        _state.DefaultTextboxShadowOffsetX = loaded.DefaultTextboxShadowOffsetX;
        _state.DefaultTextboxShadowOffsetY = loaded.DefaultTextboxShadowOffsetY;
        _state.DefaultTextboxShadowAlpha = loaded.DefaultTextboxShadowAlpha;
    }

    private void NormalizeChoiceSettings(GameState loaded)
    {
        _state.ChoiceWidth = loaded.ChoiceWidth;
        _state.ChoiceHeight = loaded.ChoiceHeight;
        _state.ChoiceSpacing = loaded.ChoiceSpacing;
        _state.ChoiceFontSize = loaded.ChoiceFontSize;
        _state.ChoiceTextColor = loaded.ChoiceTextColor;
        _state.ChoiceBgColor = loaded.ChoiceBgColor;
        _state.ChoiceBgAlpha = loaded.ChoiceBgAlpha;
        _state.ChoiceHoverColor = loaded.ChoiceHoverColor;
        _state.ChoiceCornerRadius = loaded.ChoiceCornerRadius;
        _state.ChoiceBorderColor = loaded.ChoiceBorderColor;
        _state.ChoiceBorderWidth = loaded.ChoiceBorderWidth;
        _state.ChoiceBorderOpacity = loaded.ChoiceBorderOpacity;
        _state.ChoicePaddingX = loaded.ChoicePaddingX;
    }

    private void NormalizeTextEffectSettings(GameState loaded)
    {
        _state.DefaultTextShadowColor = loaded.DefaultTextShadowColor;
        _state.DefaultTextShadowX = loaded.DefaultTextShadowX;
        _state.DefaultTextShadowY = loaded.DefaultTextShadowY;
        _state.DefaultTextOutlineColor = loaded.DefaultTextOutlineColor;
        _state.DefaultTextOutlineSize = loaded.DefaultTextOutlineSize;
        _state.DefaultTextEffect = loaded.DefaultTextEffect;
        _state.DefaultTextEffectStrength = loaded.DefaultTextEffectStrength;
        _state.DefaultTextEffectSpeed = loaded.DefaultTextEffectSpeed;
    }

    private void NormalizeSkipSettings(GameState loaded)
    {
        _state.SkipAdvancePerFrame = loaded.SkipAdvancePerFrame;
        _state.ForceSkipAdvancePerFrame = loaded.ForceSkipAdvancePerFrame;
    }

    private void NormalizeCursorSettings(GameState loaded)
    {
        _state.ShowClickCursor = loaded.ShowClickCursor;
        _state.ClickCursorMode = loaded.ClickCursorMode;
        _state.ClickCursorPath = loaded.ClickCursorPath;
        _state.ClickCursorOffsetX = loaded.ClickCursorOffsetX;
        _state.ClickCursorOffsetY = loaded.ClickCursorOffsetY;
        _state.ClickCursorSize = loaded.ClickCursorSize;
        _state.ClickCursorColor = loaded.ClickCursorColor;
    }

    private void NormalizeMenuSettings(GameState loaded)
    {
        _state.RightMenuWidth = loaded.RightMenuWidth;
        _state.RightMenuAlign = loaded.RightMenuAlign;
        _state.SaveLoadColumns = loaded.SaveLoadColumns;
        _state.SaveLoadWidth = loaded.SaveLoadWidth;
        _state.BacklogWidth = loaded.BacklogWidth;
        _state.SettingsWidth = loaded.SettingsWidth;
        _state.MenuFillColor = loaded.MenuFillColor;
        _state.MenuFillAlpha = loaded.MenuFillAlpha;
        _state.MenuTextColor = loaded.MenuTextColor;
        _state.MenuLineColor = loaded.MenuLineColor;
        _state.MenuCornerRadius = loaded.MenuCornerRadius;
    }

    private void NormalizeUiState()
    {
        // 無効なスプライトIDをクリア
        if (_state.TextboxBackgroundSpriteId >= 0 && !_state.Sprites.ContainsKey(_state.TextboxBackgroundSpriteId))
        {
            _state.TextboxBackgroundSpriteId = -1;
        }

        if (_state.TextTargetSpriteId >= 0 && !_state.Sprites.ContainsKey(_state.TextTargetSpriteId))
        {
            _state.TextTargetSpriteId = -1;
        }

        // ホバー状態をリセット
        foreach (var sprite in _state.Sprites.Values)
        {
            sprite.IsHovered = false;
        }

        // 無効なボタンのマッピングを削除
        _state.SpriteButtonMap = _state.SpriteButtonMap
            .Where(pair => _state.Sprites.TryGetValue(pair.Key, out var sprite) && sprite.IsButton)
            .ToDictionary(pair => pair.Key, pair => pair.Value);

        // テキストスプライトを正規化
        NormalizeRuntimeTextSprites();
    }

    private void NormalizeRuntimeTextSprites()
    {
        if (!_state.CompatAutoUi || _state.UseManualTextLayout) return;

        // テキストボックス背景スプライトを更新
        if (_state.TextboxBackgroundSpriteId >= 0 &&
            _state.Sprites.TryGetValue(_state.TextboxBackgroundSpriteId, out var bg) &&
            bg.Type == SpriteType.Rect)
        {
            bg.X = _state.DefaultTextboxX;
            bg.Y = _state.DefaultTextboxY;
            bg.Width = _state.DefaultTextboxW;
            bg.Height = _state.DefaultTextboxH;
            bg.FillColor = _state.DefaultTextboxBgColor;
            bg.FillAlpha = _state.DefaultTextboxBgAlpha;
            bg.CornerRadius = _state.DefaultTextboxCornerRadius;
            bg.BorderColor = _state.DefaultTextboxBorderColor;
            bg.BorderWidth = _state.DefaultTextboxBorderWidth;
            bg.BorderOpacity = _state.DefaultTextboxBorderOpacity;
            bg.ShadowColor = _state.DefaultTextboxShadowColor;
            bg.ShadowOffsetX = _state.DefaultTextboxShadowOffsetX;
            bg.ShadowOffsetY = _state.DefaultTextboxShadowOffsetY;
            bg.ShadowAlpha = _state.DefaultTextboxShadowAlpha;
        }

        // テキストスプライトを更新
        if (_state.TextTargetSpriteId >= 0 &&
            _state.Sprites.TryGetValue(_state.TextTargetSpriteId, out var textSprite) &&
            textSprite.Type == SpriteType.Text)
        {
            textSprite.X = _state.DefaultTextboxX + _state.DefaultTextboxPaddingX;
            textSprite.Y = _state.DefaultTextboxY + _state.DefaultTextboxPaddingY;
            textSprite.Width = _state.DefaultTextboxW - _state.DefaultTextboxPaddingX * 2;
            textSprite.Height = _state.DefaultTextboxH - _state.DefaultTextboxPaddingY * 2;
            textSprite.FontSize = _state.DefaultFontSize;
            textSprite.Color = _state.DefaultTextColor;
            textSprite.TextShadowColor = _state.DefaultTextShadowColor;
            textSprite.TextShadowX = _state.DefaultTextShadowX;
            textSprite.TextShadowY = _state.DefaultTextShadowY;
            textSprite.TextOutlineColor = _state.DefaultTextOutlineColor;
            textSprite.TextOutlineSize = _state.DefaultTextOutlineSize;
            textSprite.TextEffect = _state.DefaultTextEffect;
            textSprite.TextEffectStrength = _state.DefaultTextEffectStrength;
            textSprite.TextEffectSpeed = _state.DefaultTextEffectSpeed;
        }
    }
}
