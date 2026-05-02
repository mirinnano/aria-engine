using System.Text.RegularExpressions;
using AriaEngine.Text;

namespace AriaEngine.Core.Commands;

public sealed class TextCommandHandler : BaseCommandHandler
{
    public override IReadOnlySet<OpCode> HandledCodes { get; } = new HashSet<OpCode>
    {
        OpCode.Textbox,
        OpCode.SetWindow,
        OpCode.Fontsize,
        OpCode.Textcolor,
        OpCode.TextboxColor,
        OpCode.TextboxStyle,
        OpCode.ChoiceStyle,
        OpCode.TextboxHide,
        OpCode.EraseTextWindow,
        OpCode.TextboxShow,
        OpCode.TextMode,
        OpCode.CompatMode,
        OpCode.UiTheme,
        OpCode.UiQuality,
        OpCode.UiMotion,
        OpCode.TextTarget,
        OpCode.TextSpeed,
        OpCode.DefaultSpeed,
        OpCode.TextClear,
        OpCode.Br,
        OpCode.WaitClick,
        OpCode.WaitClickClear,
        OpCode.Text,
        OpCode.Wait,
        OpCode.Choice
    };

    public TextCommandHandler(VirtualMachine vm) : base(vm)
    {
    }

    public override bool Execute(Instruction inst)
    {
        switch (inst.Op)
        {
            case OpCode.Textbox:
            case OpCode.SetWindow:
                if (!ValidateArgs(inst, 4)) return true;
                State.TextWindow.DefaultTextboxX = GetVal(inst.Arguments[0]);
                State.TextWindow.DefaultTextboxY = GetVal(inst.Arguments[1]);
                State.TextWindow.DefaultTextboxW = GetVal(inst.Arguments[2]);
                State.TextWindow.DefaultTextboxH = GetVal(inst.Arguments[3]);
                if (inst.Op == OpCode.SetWindow && inst.Arguments.Count >= 12)
                {
                    State.TextWindow.DefaultFontSize = GetVal(inst.Arguments[4]);
                    State.TextWindow.DefaultTextboxBgColor = GetString(inst.Arguments[11]);
                }
                return true;

            case OpCode.Fontsize:
                if (!ValidateArgs(inst, 1)) return true;
                State.TextWindow.DefaultFontSize = GetVal(inst.Arguments[0]);
                return true;

            case OpCode.Textcolor:
                if (!ValidateArgs(inst, 1)) return true;
                State.TextWindow.DefaultTextColor = GetString(inst.Arguments[0]);
                return true;

            case OpCode.TextboxColor:
                if (!ValidateArgs(inst, 2)) return true;
                State.TextWindow.DefaultTextboxBgColor = GetString(inst.Arguments[0]);
                State.TextWindow.DefaultTextboxBgAlpha = GetVal(inst.Arguments[1]);
                return true;

            case OpCode.TextboxStyle:
                if (!ValidateArgs(inst, 10)) return true;
                State.TextWindow.DefaultTextboxCornerRadius = GetVal(inst.Arguments[0]);
                State.TextWindow.DefaultTextboxBorderWidth = GetVal(inst.Arguments[1]);
                State.TextWindow.DefaultTextboxBorderColor = GetString(inst.Arguments[2]);
                State.TextWindow.DefaultTextboxBorderOpacity = GetVal(inst.Arguments[3]);
                State.TextWindow.DefaultTextboxPaddingX = GetVal(inst.Arguments[4]);
                State.TextWindow.DefaultTextboxPaddingY = GetVal(inst.Arguments[5]);
                State.TextWindow.DefaultTextboxShadowOffsetX = GetVal(inst.Arguments[6]);
                State.TextWindow.DefaultTextboxShadowOffsetY = GetVal(inst.Arguments[7]);
                State.TextWindow.DefaultTextboxShadowColor = GetString(inst.Arguments[8]);
                State.TextWindow.DefaultTextboxShadowAlpha = GetVal(inst.Arguments[9]);
                return true;

            case OpCode.ChoiceStyle:
                if (!ValidateArgs(inst, 13)) return true;
                State.ChoiceStyle.ChoiceWidth = GetVal(inst.Arguments[0]);
                State.ChoiceStyle.ChoiceHeight = GetVal(inst.Arguments[1]);
                State.ChoiceStyle.ChoiceSpacing = GetVal(inst.Arguments[2]);
                State.ChoiceStyle.ChoiceFontSize = GetVal(inst.Arguments[3]);
                State.ChoiceStyle.ChoiceBgColor = GetString(inst.Arguments[4]);
                State.ChoiceStyle.ChoiceBgAlpha = GetVal(inst.Arguments[5]);
                State.ChoiceStyle.ChoiceTextColor = GetString(inst.Arguments[6]);
                State.ChoiceStyle.ChoiceCornerRadius = GetVal(inst.Arguments[7]);
                State.ChoiceStyle.ChoiceBorderColor = GetString(inst.Arguments[8]);
                State.ChoiceStyle.ChoiceBorderWidth = GetVal(inst.Arguments[9]);
                State.ChoiceStyle.ChoiceBorderOpacity = GetVal(inst.Arguments[10]);
                State.ChoiceStyle.ChoiceHoverColor = GetString(inst.Arguments[11]);
                State.ChoiceStyle.ChoicePaddingX = GetVal(inst.Arguments[12]);
                return true;

            case OpCode.TextboxHide:
            case OpCode.EraseTextWindow:
                State.TextWindow.TextboxVisible = false;
                SetTextboxSpriteVisibility(false);
                return true;

            case OpCode.TextboxShow:
                State.TextWindow.TextboxVisible = true;
                SetTextboxSpriteVisibility(true);
                return true;

            case OpCode.TextMode:
                if (!ValidateArgs(inst, 1)) return true;
                ApplyTextMode(GetString(inst.Arguments[0]).ToLowerInvariant());
                return true;

            case OpCode.CompatMode:
                if (!ValidateArgs(inst, 1)) return true;
                {
                    string value = GetString(inst.Arguments[0]).ToLowerInvariant();
                    State.TextWindow.CompatAutoUi = value == "on" || value == "1" || value == "true" || value == "legacy";
                }
                return true;

            case OpCode.UiTheme:
                if (!ValidateArgs(inst, 1)) return true;
                ApplyUiTheme(GetString(inst.Arguments[0]));
                return true;

            case OpCode.UiQuality:
                if (!ValidateArgs(inst, 1)) return true;
                ApplyUiQuality(GetString(inst.Arguments[0]));
                return true;

            case OpCode.UiMotion:
                if (!ValidateArgs(inst, 1)) return true;
                ApplyUiMotion(inst);
                return true;

            case OpCode.TextTarget:
                if (!ValidateArgs(inst, 1)) return true;
                State.TextWindow.TextTargetSpriteId = GetVal(inst.Arguments[0]);
                return true;

            case OpCode.TextSpeed:
                if (!ValidateArgs(inst, 1)) return true;
                State.TextRuntime.TextSpeedMs = GetVal(inst.Arguments[0]);
                return true;

            case OpCode.DefaultSpeed:
                if (!ValidateArgs(inst, 1)) return true;
                int speed = GetVal(inst.Arguments[0]);
                Config.Config.DefaultTextSpeedMs = speed;
                Config.Config.GlobalTextSpeedMs = speed;
                Config.Save();
                State.TextRuntime.TextSpeedMs = speed;
                return true;

            case OpCode.TextClear:
                ClearText();
                return true;

            case OpCode.Br:
                State.TextRuntime.CurrentTextBuffer += "\n";
                return true;

            case OpCode.WaitClick:
                CompleteTextForClick();
                AddBacklogEntry();
                State.TextRuntime.IsWaitingPageClear = false;
                State.Execution.State = VmState.WaitingForClick;
                Vm.AutoSaveGame();
                return true;

            case OpCode.WaitClickClear:
                CompleteTextForClick();
                AddBacklogEntry();
                State.TextRuntime.IsWaitingPageClear = true;
                State.Execution.State = VmState.WaitingForClick;
                Vm.AutoSaveGame();
                return true;

            case OpCode.Text:
                ExecuteText(inst);
                return true;

            case OpCode.Wait:
                if (inst.Arguments.Count > 0)
                {
                    State.Execution.DelayTimerMs = Math.Max(0, GetVal(inst.Arguments[0]));
                    State.Execution.State = VmState.WaitingForDelay;
                    return true;
                }
                CompleteTextForClick();
                AddBacklogEntry();
                State.Execution.State = VmState.WaitingForClick;
                Vm.AutoSaveGame();
                return true;

            case OpCode.Choice:
                ExecuteChoice(inst);
                return true;

            default:
                return false;
        }
    }

    private void ClearText()
    {
        State.TextRuntime.CurrentTextBuffer = "";
        State.TextRuntime.CurrentTextSegments = null;
        State.TextRuntime.DisplayedTextLength = 0;
        State.TextRuntime.IsWaitingPageClear = false;
        if (State.TextWindow.UseManualTextLayout || !State.TextWindow.CompatAutoUi)
        {
            if (State.Render.Sprites.TryGetValue(State.TextWindow.TextTargetSpriteId, out var targetTextSprite) && targetTextSprite.Type == SpriteType.Text)
            {
                targetTextSprite.Text = "";
            }
            return;
        }

        if (State.TextWindow.TextboxBackgroundSpriteId >= 0) State.Render.Sprites.Remove(State.TextWindow.TextboxBackgroundSpriteId);
        State.TextWindow.TextboxBackgroundSpriteId = -1;
        if (State.Render.Sprites.ContainsKey(State.TextWindow.TextTargetSpriteId)) State.Render.Sprites.Remove(State.TextWindow.TextTargetSpriteId);
    }

    private void CompleteTextForClick()
    {
        State.TextRuntime.DisplayedTextLength = State.TextRuntime.CurrentTextBuffer.Length;
        if (State.TextWindow.TextTargetSpriteId >= 0 &&
            State.Render.Sprites.TryGetValue(State.TextWindow.TextTargetSpriteId, out var textSprite) &&
            textSprite.Type == SpriteType.Text)
        {
            textSprite.Text = State.TextRuntime.CurrentTextBuffer;
        }
    }

    private readonly TextEffectParser _textEffectParser = new();

    private void ExecuteText(Instruction inst)
    {
        string fullText = string.Join(" ", inst.Arguments);
        // GetString handles ${$name}, ${%name}, ${%0}, and ${expression} interpolation
        fullText = GetString(fullText);
        State.TextRuntime.CurrentTextBuffer += fullText;
        
        // テキストエフェクトをパース
        var segments = _textEffectParser.Parse(State.TextRuntime.CurrentTextBuffer);
        
        // フェードエフェクトの開始時刻を設定
        float now = (float)Raylib_cs.Raylib.GetTime() * 1000f;
        foreach (var seg in segments)
        {
            if (seg.Style.FadeDuration.HasValue && seg.Style.FadeDuration.Value > 0)
            {
                seg.FadeStartTime = now;
            }
        }
        
        State.TextRuntime.CurrentTextSegments = segments;

        if (State.TextWindow.CompatAutoUi && !State.TextWindow.UseManualTextLayout && State.TextWindow.TextboxVisible)
        {
            if (State.TextWindow.TextboxBackgroundSpriteId < 0)
            {
                State.TextWindow.TextboxBackgroundSpriteId = AllocateCompatUiSpriteId();
            }

            if (!State.Render.Sprites.TryGetValue(State.TextWindow.TextboxBackgroundSpriteId, out var bgSprite) || bgSprite.Type != SpriteType.Rect)
            {
                bgSprite = new Sprite
                {
                    Id = State.TextWindow.TextboxBackgroundSpriteId,
                    Type = SpriteType.Rect,
                    Z = 9000
                };
                State.Render.Sprites[State.TextWindow.TextboxBackgroundSpriteId] = bgSprite;
            }

            bgSprite.X = State.TextWindow.DefaultTextboxX;
            bgSprite.Y = State.TextWindow.DefaultTextboxY;
            bgSprite.Width = State.TextWindow.DefaultTextboxW;
            bgSprite.Height = State.TextWindow.DefaultTextboxH;
            bgSprite.FillColor = State.TextWindow.DefaultTextboxBgColor;
            bgSprite.FillAlpha = State.TextWindow.DefaultTextboxBgAlpha;
            bgSprite.CornerRadius = State.TextWindow.DefaultTextboxCornerRadius;
            bgSprite.BorderColor = State.TextWindow.DefaultTextboxBorderColor;
            bgSprite.BorderWidth = State.TextWindow.DefaultTextboxBorderWidth;
            bgSprite.BorderOpacity = State.TextWindow.DefaultTextboxBorderOpacity;
            bgSprite.ShadowColor = State.TextWindow.DefaultTextboxShadowColor;
            bgSprite.ShadowOffsetX = State.TextWindow.DefaultTextboxShadowOffsetX;
            bgSprite.ShadowOffsetY = State.TextWindow.DefaultTextboxShadowOffsetY;
            bgSprite.ShadowAlpha = State.TextWindow.DefaultTextboxShadowAlpha;
            bgSprite.IsHovered = false;
        }

        if (State.TextWindow.TextTargetSpriteId < 0)
        {
            if (!State.TextWindow.CompatAutoUi)
            {
                Reporter.Report(new AriaError("text_target が未設定のため text は描画されません。text_target で出力先を指定してください。", inst.SourceLine, CurrentScriptFile, AriaErrorLevel.Warning));
                return;
            }

            State.TextWindow.TextTargetSpriteId = AllocateCompatUiSpriteId();
        }

        if (!State.Render.Sprites.TryGetValue(State.TextWindow.TextTargetSpriteId, out var textSprite) || textSprite.Type != SpriteType.Text)
        {
            if (!State.TextWindow.CompatAutoUi)
            {
                Reporter.Report(new AriaError($"text_target({State.TextWindow.TextTargetSpriteId}) のTextスプライトが存在しないため text は描画されません。lsp_text で先に作成してください。", inst.SourceLine, CurrentScriptFile, AriaErrorLevel.Warning));
                return;
            }

            textSprite = new Sprite
            {
                Id = State.TextWindow.TextTargetSpriteId,
                Type = SpriteType.Text,
                Z = 9001
            };
            State.Render.Sprites[State.TextWindow.TextTargetSpriteId] = textSprite;
        }

        if (State.TextWindow.CompatAutoUi && !State.TextWindow.UseManualTextLayout)
        {
            textSprite.X = State.TextWindow.DefaultTextboxX + State.TextWindow.DefaultTextboxPaddingX;
            textSprite.Y = State.TextWindow.DefaultTextboxY + State.TextWindow.DefaultTextboxPaddingY;
            textSprite.Width = Math.Max(0, State.TextWindow.DefaultTextboxW - (State.TextWindow.DefaultTextboxPaddingX * 2));
            textSprite.Height = Math.Max(0, State.TextWindow.DefaultTextboxH - (State.TextWindow.DefaultTextboxPaddingY * 2));
        }

        textSprite.FontSize = State.TextWindow.DefaultFontSize;
        textSprite.Color = State.TextWindow.DefaultTextColor;
        textSprite.TextShadowColor = State.TextRuntime.DefaultTextShadowColor;
        textSprite.TextShadowX = State.TextRuntime.DefaultTextShadowX;
        textSprite.TextShadowY = State.TextRuntime.DefaultTextShadowY;
        textSprite.TextOutlineColor = State.TextRuntime.DefaultTextOutlineColor;
        textSprite.TextOutlineSize = State.TextRuntime.DefaultTextOutlineSize;
        textSprite.TextEffect = State.TextRuntime.DefaultTextEffect;
        textSprite.TextEffectStrength = State.TextRuntime.DefaultTextEffectStrength;
        textSprite.TextEffectSpeed = State.TextRuntime.DefaultTextEffectSpeed;

        if (State.TextRuntime.TextSpeedMs > 0 && State.TextRuntime.DisplayedTextLength < State.TextRuntime.CurrentTextBuffer.Length)
        {
            int length = Math.Clamp(State.TextRuntime.DisplayedTextLength, 0, State.TextRuntime.CurrentTextBuffer.Length);
            textSprite.Text = State.TextRuntime.CurrentTextBuffer.Substring(0, length);
            State.Execution.State = VmState.WaitingForAnimation;
        }
        else
        {
            State.TextRuntime.DisplayedTextLength = State.TextRuntime.CurrentTextBuffer.Length;
            textSprite.Text = State.TextRuntime.CurrentTextBuffer;
        }
    }

    private void ExecuteChoice(Instruction inst)
    {
        if (!ValidateArgs(inst, 1)) return;
        if (!State.TextWindow.CompatAutoUi)
        {
            State.Interaction.ButtonResultRegister = inst.Arguments.Count > 0 && inst.Arguments[0].StartsWith("%") ? inst.Arguments[0] : "0";

            if (inst.Arguments.Count > 0 && !inst.Arguments[0].StartsWith("%"))
            {
                Reporter.Report(new AriaError("compat_mode off では choice の文字列引数は描画に使われません。lsp/spbtn/btnwait でUIを構築してください。", inst.SourceLine, CurrentScriptFile, AriaErrorLevel.Warning));
            }

            if (!HasAnyVisibleButton())
            {
                Reporter.Report(new AriaError("choice は待機先のボタンが存在しないため実行できません。spbtn/btn で先にボタンを作成してください。", inst.SourceLine, CurrentScriptFile, AriaErrorLevel.Error));
                return;
            }

            State.Execution.State = VmState.WaitingForButton;
            return;
        }

        ClearCompatUiSprites();

        int count = inst.Arguments.Count;
        int h = State.ChoiceStyle.ChoiceHeight;
        int spacing = State.ChoiceStyle.ChoiceSpacing;
        int totalH = (h + spacing) * count - spacing;
        int startY = (State.EngineSettings.WindowHeight - totalH) / 2;
        int startX = (State.EngineSettings.WindowWidth - State.ChoiceStyle.ChoiceWidth) / 2;

        for (int i = 0; i < count; i++)
        {
            int y = startY + (h + spacing) * i;
            int rectId = AllocateCompatUiSpriteId();
            int textId = AllocateCompatUiSpriteId();

            State.Render.Sprites[rectId] = new Sprite
            {
                Id = rectId,
                Type = SpriteType.Rect,
                Z = 9500,
                X = startX,
                Y = y,
                Width = State.ChoiceStyle.ChoiceWidth,
                Height = h,
                FillColor = State.ChoiceStyle.ChoiceBgColor,
                FillAlpha = State.ChoiceStyle.ChoiceBgAlpha,
                IsButton = true,
                CornerRadius = State.ChoiceStyle.ChoiceCornerRadius,
                BorderColor = State.ChoiceStyle.ChoiceBorderColor,
                BorderWidth = State.ChoiceStyle.ChoiceBorderWidth,
                BorderOpacity = State.ChoiceStyle.ChoiceBorderOpacity,
                HoverFillColor = State.ChoiceStyle.ChoiceHoverColor
            };
            State.Interaction.SpriteButtonMap[rectId] = i;
            TrackCompatUiSprite(rectId);

            State.Render.Sprites[textId] = new Sprite
            {
                Id = textId,
                Type = SpriteType.Text,
                Z = 9501,
                Text = GetString(inst.Arguments[i]),
                X = startX + State.ChoiceStyle.ChoicePaddingX,
                Y = y,
                Width = State.ChoiceStyle.ChoiceWidth - (State.ChoiceStyle.ChoicePaddingX * 2),
                Height = h,
                FontSize = State.ChoiceStyle.ChoiceFontSize,
                Color = State.ChoiceStyle.ChoiceTextColor,
                TextAlign = TextAlignment.Center,
                TextVAlign = TextVerticalAlignment.Center
            };
            TrackCompatUiSprite(textId);
        }

        State.Execution.State = VmState.WaitingForButton;
    }

    private void SetTextboxSpriteVisibility(bool visible)
    {
        if (State.TextWindow.TextboxBackgroundSpriteId >= 0 && State.Render.Sprites.ContainsKey(State.TextWindow.TextboxBackgroundSpriteId))
        {
            State.Render.Sprites[State.TextWindow.TextboxBackgroundSpriteId].Visible = visible;
        }

        if (State.TextWindow.TextTargetSpriteId >= 0 && State.Render.Sprites.ContainsKey(State.TextWindow.TextTargetSpriteId))
        {
            State.Render.Sprites[State.TextWindow.TextTargetSpriteId].Visible = visible;
        }
    }

    private void ApplyTextMode(string mode)
    {
        Config.Config.TextMode = mode;
        if (mode == "manual")
        {
            State.TextWindow.UseManualTextLayout = true;
            State.TextWindow.TextboxVisible = false;
        }
        else if (mode == "nvl")
        {
            State.TextWindow.UseManualTextLayout = false;
            State.TextWindow.TextboxVisible = true;
            State.TextWindow.DefaultTextboxX = Math.Max(24, (int)(State.EngineSettings.WindowWidth * 0.056f));
            State.TextWindow.DefaultTextboxY = Math.Max(24, (int)(State.EngineSettings.WindowHeight * 0.089f));
            State.TextWindow.DefaultTextboxW = State.EngineSettings.WindowWidth - (State.TextWindow.DefaultTextboxX * 2);
            State.TextWindow.DefaultTextboxH = State.EngineSettings.WindowHeight - (State.TextWindow.DefaultTextboxY * 2);
            State.TextWindow.DefaultFontSize = Math.Max(State.TextWindow.DefaultFontSize, 30);
            State.TextWindow.DefaultTextboxPaddingX = 34;
            State.TextWindow.DefaultTextboxPaddingY = 30;
            State.TextWindow.DefaultTextboxBgAlpha = 220;
        }
        else
        {
            State.TextWindow.UseManualTextLayout = false;
            State.TextWindow.TextboxVisible = true;
            State.TextWindow.DefaultTextboxX = 50;
            State.TextWindow.DefaultTextboxY = 500;
            State.TextWindow.DefaultTextboxW = 1180;
            State.TextWindow.DefaultTextboxH = 200;
            State.TextWindow.DefaultTextboxBgAlpha = 180;
        }
    }

    private void ApplyUiQuality(string quality)
    {
        string mode = quality.Trim().ToLowerInvariant();
        State.UiQuality.Quality = mode;

        if (mode == "ultra")
        {
            State.UiQuality.SmoothMotion = true;
            State.UiQuality.SubpixelRendering = false;
            State.UiQuality.HighQualityTextures = true;
            State.UiQuality.RoundedRectSegments = 96;
            State.UiQuality.MotionResponse = 16f;
            State.EngineSettings.FontFilter = Raylib_cs.TextureFilter.Trilinear;
            return;
        }

        if (mode == "standard" || mode == "normal" || mode == "balanced")
        {
            State.UiQuality.Quality = "balanced";
            State.UiQuality.SmoothMotion = true;
            State.UiQuality.SubpixelRendering = false;
            State.UiQuality.HighQualityTextures = true;
            State.UiQuality.RoundedRectSegments = 48;
            State.UiQuality.MotionResponse = 12f;
            State.EngineSettings.FontFilter = Raylib_cs.TextureFilter.Bilinear;
            return;
        }

        if (mode == "performance" || mode == "fast")
        {
            State.UiQuality.SmoothMotion = false;
            State.UiQuality.SubpixelRendering = false;
            State.UiQuality.HighQualityTextures = false;
            State.UiQuality.RoundedRectSegments = 24;
            State.UiQuality.MotionResponse = 40f;
            State.EngineSettings.FontFilter = Raylib_cs.TextureFilter.Bilinear;
            return;
        }

        State.UiQuality.Quality = "high";
        State.UiQuality.SmoothMotion = true;
        State.UiQuality.SubpixelRendering = false;
        State.UiQuality.HighQualityTextures = true;
        State.UiQuality.RoundedRectSegments = 64;
        State.UiQuality.MotionResponse = 14f;
        State.EngineSettings.FontFilter = Raylib_cs.TextureFilter.Bilinear;
    }

    private void ApplyUiMotion(Instruction inst)
    {
        string value = GetString(inst.Arguments[0]).Trim().ToLowerInvariant();
        if (value is "off" or "0" or "false")
        {
            State.UiQuality.SmoothMotion = false;
            return;
        }

        State.UiQuality.SmoothMotion = true;
        if (value == "simple") State.UiQuality.MotionResponse = 9f;
        if (value == "smooth" || value == "on" || value == "true") State.UiQuality.MotionResponse = 14f;
        if (inst.Arguments.Count > 1)
        {
            State.UiQuality.MotionResponse = Math.Clamp(GetFloat(inst.Arguments[1], inst, State.UiQuality.MotionResponse), 1f, 40f);
        }
    }
}
