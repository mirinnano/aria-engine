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
                State.DefaultTextboxX = GetVal(inst.Arguments[0]);
                State.DefaultTextboxY = GetVal(inst.Arguments[1]);
                State.DefaultTextboxW = GetVal(inst.Arguments[2]);
                State.DefaultTextboxH = GetVal(inst.Arguments[3]);
                if (inst.Op == OpCode.SetWindow && inst.Arguments.Count >= 12)
                {
                    State.DefaultFontSize = GetVal(inst.Arguments[4]);
                    State.DefaultTextboxBgColor = GetString(inst.Arguments[11]);
                }
                return true;

            case OpCode.Fontsize:
                if (!ValidateArgs(inst, 1)) return true;
                State.DefaultFontSize = GetVal(inst.Arguments[0]);
                return true;

            case OpCode.Textcolor:
                if (!ValidateArgs(inst, 1)) return true;
                State.DefaultTextColor = GetString(inst.Arguments[0]);
                return true;

            case OpCode.TextboxColor:
                if (!ValidateArgs(inst, 2)) return true;
                State.DefaultTextboxBgColor = GetString(inst.Arguments[0]);
                State.DefaultTextboxBgAlpha = GetVal(inst.Arguments[1]);
                return true;

            case OpCode.TextboxStyle:
                if (!ValidateArgs(inst, 10)) return true;
                State.DefaultTextboxCornerRadius = GetVal(inst.Arguments[0]);
                State.DefaultTextboxBorderWidth = GetVal(inst.Arguments[1]);
                State.DefaultTextboxBorderColor = GetString(inst.Arguments[2]);
                State.DefaultTextboxBorderOpacity = GetVal(inst.Arguments[3]);
                State.DefaultTextboxPaddingX = GetVal(inst.Arguments[4]);
                State.DefaultTextboxPaddingY = GetVal(inst.Arguments[5]);
                State.DefaultTextboxShadowOffsetX = GetVal(inst.Arguments[6]);
                State.DefaultTextboxShadowOffsetY = GetVal(inst.Arguments[7]);
                State.DefaultTextboxShadowColor = GetString(inst.Arguments[8]);
                State.DefaultTextboxShadowAlpha = GetVal(inst.Arguments[9]);
                return true;

            case OpCode.ChoiceStyle:
                if (!ValidateArgs(inst, 13)) return true;
                State.ChoiceWidth = GetVal(inst.Arguments[0]);
                State.ChoiceHeight = GetVal(inst.Arguments[1]);
                State.ChoiceSpacing = GetVal(inst.Arguments[2]);
                State.ChoiceFontSize = GetVal(inst.Arguments[3]);
                State.ChoiceBgColor = GetString(inst.Arguments[4]);
                State.ChoiceBgAlpha = GetVal(inst.Arguments[5]);
                State.ChoiceTextColor = GetString(inst.Arguments[6]);
                State.ChoiceCornerRadius = GetVal(inst.Arguments[7]);
                State.ChoiceBorderColor = GetString(inst.Arguments[8]);
                State.ChoiceBorderWidth = GetVal(inst.Arguments[9]);
                State.ChoiceBorderOpacity = GetVal(inst.Arguments[10]);
                State.ChoiceHoverColor = GetString(inst.Arguments[11]);
                State.ChoicePaddingX = GetVal(inst.Arguments[12]);
                return true;

            case OpCode.TextboxHide:
            case OpCode.EraseTextWindow:
                State.TextboxVisible = false;
                SetTextboxSpriteVisibility(false);
                return true;

            case OpCode.TextboxShow:
                State.TextboxVisible = true;
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
                    State.CompatAutoUi = value == "on" || value == "1" || value == "true" || value == "legacy";
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
                State.TextTargetSpriteId = GetVal(inst.Arguments[0]);
                return true;

            case OpCode.TextSpeed:
                if (!ValidateArgs(inst, 1)) return true;
                State.TextSpeedMs = GetVal(inst.Arguments[0]);
                return true;

            case OpCode.DefaultSpeed:
                if (!ValidateArgs(inst, 1)) return true;
                int speed = GetVal(inst.Arguments[0]);
                Config.Config.DefaultTextSpeedMs = speed;
                Config.Config.GlobalTextSpeedMs = speed;
                Config.Save();
                State.TextSpeedMs = speed;
                return true;

            case OpCode.TextClear:
                ClearText();
                return true;

            case OpCode.Br:
                State.CurrentTextBuffer += "\n";
                return true;

            case OpCode.WaitClick:
                CompleteTextForClick();
                AddBacklogEntry();
                State.IsWaitingPageClear = false;
                State.State = VmState.WaitingForClick;
                Vm.AutoSaveGame();
                return true;

            case OpCode.WaitClickClear:
                CompleteTextForClick();
                AddBacklogEntry();
                State.IsWaitingPageClear = true;
                State.State = VmState.WaitingForClick;
                Vm.AutoSaveGame();
                return true;

            case OpCode.Text:
                ExecuteText(inst);
                return true;

            case OpCode.Wait:
                if (inst.Arguments.Count > 0)
                {
                    State.DelayTimerMs = Math.Max(0, GetVal(inst.Arguments[0]));
                    State.State = VmState.WaitingForDelay;
                    return true;
                }
                CompleteTextForClick();
                AddBacklogEntry();
                State.State = VmState.WaitingForClick;
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
        State.CurrentTextBuffer = "";
        State.CurrentTextSegments = null;
        State.DisplayedTextLength = 0;
        State.IsWaitingPageClear = false;
        if (State.UseManualTextLayout || !State.CompatAutoUi)
        {
            if (State.Sprites.TryGetValue(State.TextTargetSpriteId, out var targetTextSprite) && targetTextSprite.Type == SpriteType.Text)
            {
                targetTextSprite.Text = "";
            }
            return;
        }

        if (State.TextboxBackgroundSpriteId >= 0) State.Sprites.Remove(State.TextboxBackgroundSpriteId);
        State.TextboxBackgroundSpriteId = -1;
        if (State.Sprites.ContainsKey(State.TextTargetSpriteId)) State.Sprites.Remove(State.TextTargetSpriteId);
    }

    private void CompleteTextForClick()
    {
        State.DisplayedTextLength = State.CurrentTextBuffer.Length;
        if (State.TextTargetSpriteId >= 0 &&
            State.Sprites.TryGetValue(State.TextTargetSpriteId, out var textSprite) &&
            textSprite.Type == SpriteType.Text)
        {
            textSprite.Text = State.CurrentTextBuffer;
        }
    }

    private readonly TextEffectParser _textEffectParser = new();

    private void ExecuteText(Instruction inst)
    {
        string fullText = string.Join(" ", inst.Arguments);
        // GetString handles ${$name}, ${%name}, ${%0}, and ${expression} interpolation
        fullText = GetString(fullText);
        State.CurrentTextBuffer += fullText;
        
        // テキストエフェクトをパース
        var segments = _textEffectParser.Parse(State.CurrentTextBuffer);
        
        // フェードエフェクトの開始時刻を設定
        float now = (float)Raylib_cs.Raylib.GetTime() * 1000f;
        foreach (var seg in segments)
        {
            if (seg.Style.FadeDuration.HasValue && seg.Style.FadeDuration.Value > 0)
            {
                seg.FadeStartTime = now;
            }
        }
        
        State.CurrentTextSegments = segments;

        if (State.CompatAutoUi && !State.UseManualTextLayout && State.TextboxVisible)
        {
            if (State.TextboxBackgroundSpriteId < 0)
            {
                State.TextboxBackgroundSpriteId = AllocateCompatUiSpriteId();
            }

            if (!State.Sprites.TryGetValue(State.TextboxBackgroundSpriteId, out var bgSprite) || bgSprite.Type != SpriteType.Rect)
            {
                bgSprite = new Sprite
                {
                    Id = State.TextboxBackgroundSpriteId,
                    Type = SpriteType.Rect,
                    Z = 9000
                };
                State.Sprites[State.TextboxBackgroundSpriteId] = bgSprite;
            }

            bgSprite.X = State.DefaultTextboxX;
            bgSprite.Y = State.DefaultTextboxY;
            bgSprite.Width = State.DefaultTextboxW;
            bgSprite.Height = State.DefaultTextboxH;
            bgSprite.FillColor = State.DefaultTextboxBgColor;
            bgSprite.FillAlpha = State.DefaultTextboxBgAlpha;
            bgSprite.CornerRadius = State.DefaultTextboxCornerRadius;
            bgSprite.BorderColor = State.DefaultTextboxBorderColor;
            bgSprite.BorderWidth = State.DefaultTextboxBorderWidth;
            bgSprite.BorderOpacity = State.DefaultTextboxBorderOpacity;
            bgSprite.ShadowColor = State.DefaultTextboxShadowColor;
            bgSprite.ShadowOffsetX = State.DefaultTextboxShadowOffsetX;
            bgSprite.ShadowOffsetY = State.DefaultTextboxShadowOffsetY;
            bgSprite.ShadowAlpha = State.DefaultTextboxShadowAlpha;
            bgSprite.IsHovered = false;
        }

        if (State.TextTargetSpriteId < 0)
        {
            if (!State.CompatAutoUi)
            {
                Reporter.Report(new AriaError("text_target が未設定のため text は描画されません。text_target で出力先を指定してください。", inst.SourceLine, CurrentScriptFile, AriaErrorLevel.Warning));
                return;
            }

            State.TextTargetSpriteId = AllocateCompatUiSpriteId();
        }

        if (!State.Sprites.TryGetValue(State.TextTargetSpriteId, out var textSprite) || textSprite.Type != SpriteType.Text)
        {
            if (!State.CompatAutoUi)
            {
                Reporter.Report(new AriaError($"text_target({State.TextTargetSpriteId}) のTextスプライトが存在しないため text は描画されません。lsp_text で先に作成してください。", inst.SourceLine, CurrentScriptFile, AriaErrorLevel.Warning));
                return;
            }

            textSprite = new Sprite
            {
                Id = State.TextTargetSpriteId,
                Type = SpriteType.Text,
                Z = 9001
            };
            State.Sprites[State.TextTargetSpriteId] = textSprite;
        }

        if (State.CompatAutoUi && !State.UseManualTextLayout)
        {
            textSprite.X = State.DefaultTextboxX + State.DefaultTextboxPaddingX;
            textSprite.Y = State.DefaultTextboxY + State.DefaultTextboxPaddingY;
            textSprite.Width = Math.Max(0, State.DefaultTextboxW - (State.DefaultTextboxPaddingX * 2));
            textSprite.Height = Math.Max(0, State.DefaultTextboxH - (State.DefaultTextboxPaddingY * 2));
        }

        textSprite.FontSize = State.DefaultFontSize;
        textSprite.Color = State.DefaultTextColor;
        textSprite.TextShadowColor = State.DefaultTextShadowColor;
        textSprite.TextShadowX = State.DefaultTextShadowX;
        textSprite.TextShadowY = State.DefaultTextShadowY;
        textSprite.TextOutlineColor = State.DefaultTextOutlineColor;
        textSprite.TextOutlineSize = State.DefaultTextOutlineSize;
        textSprite.TextEffect = State.DefaultTextEffect;
        textSprite.TextEffectStrength = State.DefaultTextEffectStrength;
        textSprite.TextEffectSpeed = State.DefaultTextEffectSpeed;

        if (State.TextSpeedMs > 0 && State.DisplayedTextLength < State.CurrentTextBuffer.Length)
        {
            int length = Math.Clamp(State.DisplayedTextLength, 0, State.CurrentTextBuffer.Length);
            textSprite.Text = State.CurrentTextBuffer.Substring(0, length);
            State.State = VmState.WaitingForAnimation;
        }
        else
        {
            State.DisplayedTextLength = State.CurrentTextBuffer.Length;
            textSprite.Text = State.CurrentTextBuffer;
        }
    }

    private void ExecuteChoice(Instruction inst)
    {
        if (!ValidateArgs(inst, 1)) return;
        if (!State.CompatAutoUi)
        {
            State.ButtonResultRegister = inst.Arguments.Count > 0 && inst.Arguments[0].StartsWith("%") ? inst.Arguments[0] : "0";

            if (inst.Arguments.Count > 0 && !inst.Arguments[0].StartsWith("%"))
            {
                Reporter.Report(new AriaError("compat_mode off では choice の文字列引数は描画に使われません。lsp/spbtn/btnwait でUIを構築してください。", inst.SourceLine, CurrentScriptFile, AriaErrorLevel.Warning));
            }

            if (!HasAnyVisibleButton())
            {
                Reporter.Report(new AriaError("choice は待機先のボタンが存在しないため実行できません。spbtn/btn で先にボタンを作成してください。", inst.SourceLine, CurrentScriptFile, AriaErrorLevel.Error));
                return;
            }

            State.State = VmState.WaitingForButton;
            return;
        }

        ClearCompatUiSprites();

        int count = inst.Arguments.Count;
        int h = State.ChoiceHeight;
        int spacing = State.ChoiceSpacing;
        int totalH = (h + spacing) * count - spacing;
        int startY = (State.WindowHeight - totalH) / 2;
        int startX = (State.WindowWidth - State.ChoiceWidth) / 2;

        for (int i = 0; i < count; i++)
        {
            int y = startY + (h + spacing) * i;
            int rectId = AllocateCompatUiSpriteId();
            int textId = AllocateCompatUiSpriteId();

            State.Sprites[rectId] = new Sprite
            {
                Id = rectId,
                Type = SpriteType.Rect,
                Z = 9500,
                X = startX,
                Y = y,
                Width = State.ChoiceWidth,
                Height = h,
                FillColor = State.ChoiceBgColor,
                FillAlpha = State.ChoiceBgAlpha,
                IsButton = true,
                CornerRadius = State.ChoiceCornerRadius,
                BorderColor = State.ChoiceBorderColor,
                BorderWidth = State.ChoiceBorderWidth,
                BorderOpacity = State.ChoiceBorderOpacity,
                HoverFillColor = State.ChoiceHoverColor
            };
            State.SpriteButtonMap[rectId] = i;
            TrackCompatUiSprite(rectId);

            State.Sprites[textId] = new Sprite
            {
                Id = textId,
                Type = SpriteType.Text,
                Z = 9501,
                Text = GetString(inst.Arguments[i]),
                X = startX + State.ChoicePaddingX,
                Y = y,
                Width = State.ChoiceWidth - (State.ChoicePaddingX * 2),
                Height = h,
                FontSize = State.ChoiceFontSize,
                Color = State.ChoiceTextColor,
                TextAlign = "center",
                TextVAlign = "center"
            };
            TrackCompatUiSprite(textId);
        }

        State.State = VmState.WaitingForButton;
    }

    private void SetTextboxSpriteVisibility(bool visible)
    {
        if (State.TextboxBackgroundSpriteId >= 0 && State.Sprites.ContainsKey(State.TextboxBackgroundSpriteId))
        {
            State.Sprites[State.TextboxBackgroundSpriteId].Visible = visible;
        }

        if (State.TextTargetSpriteId >= 0 && State.Sprites.ContainsKey(State.TextTargetSpriteId))
        {
            State.Sprites[State.TextTargetSpriteId].Visible = visible;
        }
    }

    private void ApplyTextMode(string mode)
    {
        Config.Config.TextMode = mode;
        if (mode == "manual")
        {
            State.UseManualTextLayout = true;
            State.TextboxVisible = false;
        }
        else if (mode == "nvl")
        {
            State.UseManualTextLayout = false;
            State.TextboxVisible = true;
            State.DefaultTextboxX = Math.Max(24, (int)(State.WindowWidth * 0.056f));
            State.DefaultTextboxY = Math.Max(24, (int)(State.WindowHeight * 0.089f));
            State.DefaultTextboxW = State.WindowWidth - (State.DefaultTextboxX * 2);
            State.DefaultTextboxH = State.WindowHeight - (State.DefaultTextboxY * 2);
            State.DefaultFontSize = Math.Max(State.DefaultFontSize, 30);
            State.DefaultTextboxPaddingX = 34;
            State.DefaultTextboxPaddingY = 30;
            State.DefaultTextboxBgAlpha = 220;
        }
        else
        {
            State.UseManualTextLayout = false;
            State.TextboxVisible = true;
            State.DefaultTextboxX = 50;
            State.DefaultTextboxY = 500;
            State.DefaultTextboxW = 1180;
            State.DefaultTextboxH = 200;
            State.DefaultTextboxBgAlpha = 180;
        }
    }

    private void ApplyUiQuality(string quality)
    {
        string mode = quality.Trim().ToLowerInvariant();
        State.UiQualityMode = mode;

        if (mode == "ultra")
        {
            State.SmoothUiMotion = true;
            State.SubpixelUiRendering = false;
            State.HighQualityUiTextures = true;
            State.RoundedRectSegments = 96;
            State.UiMotionResponse = 16f;
            State.FontFilter = Raylib_cs.TextureFilter.Trilinear;
            return;
        }

        if (mode == "standard" || mode == "normal" || mode == "balanced")
        {
            State.UiQualityMode = "balanced";
            State.SmoothUiMotion = true;
            State.SubpixelUiRendering = false;
            State.HighQualityUiTextures = true;
            State.RoundedRectSegments = 48;
            State.UiMotionResponse = 12f;
            State.FontFilter = Raylib_cs.TextureFilter.Bilinear;
            return;
        }

        if (mode == "performance" || mode == "fast")
        {
            State.SmoothUiMotion = false;
            State.SubpixelUiRendering = false;
            State.HighQualityUiTextures = false;
            State.RoundedRectSegments = 24;
            State.UiMotionResponse = 40f;
            State.FontFilter = Raylib_cs.TextureFilter.Bilinear;
            return;
        }

        State.UiQualityMode = "high";
        State.SmoothUiMotion = true;
        State.SubpixelUiRendering = false;
        State.HighQualityUiTextures = true;
        State.RoundedRectSegments = 64;
        State.UiMotionResponse = 14f;
        State.FontFilter = Raylib_cs.TextureFilter.Bilinear;
    }

    private void ApplyUiMotion(Instruction inst)
    {
        string value = GetString(inst.Arguments[0]).Trim().ToLowerInvariant();
        if (value is "off" or "0" or "false")
        {
            State.SmoothUiMotion = false;
            return;
        }

        State.SmoothUiMotion = true;
        if (value == "simple") State.UiMotionResponse = 9f;
        if (value == "smooth" || value == "on" || value == "true") State.UiMotionResponse = 14f;
        if (inst.Arguments.Count > 1)
        {
            State.UiMotionResponse = Math.Clamp(GetFloat(inst.Arguments[1], inst, State.UiMotionResponse), 1f, 40f);
        }
    }
}
