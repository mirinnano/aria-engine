using Raylib_cs;

namespace AriaEngine.Core.Commands;

public sealed class SystemCommandHandler : BaseCommandHandler
{
    public override IReadOnlySet<OpCode> HandledCodes { get; } = new HashSet<OpCode>
    {
        OpCode.Backlog,
        OpCode.KidokuMode,
        OpCode.SkipMode,
        OpCode.SystemButton,
        OpCode.YesNoBox,
        OpCode.FadeIn,
        OpCode.FadeOut,
        OpCode.End,
        OpCode.FontFilter
    };

    public SystemCommandHandler(VirtualMachine vm) : base(vm)
    {
    }

    public override bool Execute(Instruction inst)
    {
        switch (inst.Op)
        {
            case OpCode.Backlog:
                if (inst.Arguments.Count > 0) State.BacklogEnabled = IsOn(inst.Arguments[0]);
                return true;

            case OpCode.KidokuMode:
                if (inst.Arguments.Count > 0) State.KidokuMode = GetVal(inst.Arguments[0]) != 0;
                return true;

            case OpCode.SkipMode:
                if (inst.Arguments.Count > 0)
                {
                    string skipMode = GetString(inst.Arguments[0]).ToLowerInvariant();
                    State.SkipUnread = skipMode is "all" or "unread" or "1" or "on";
                    MarkPersistentDirty();
                }
                return true;

            case OpCode.SystemButton:
                if (!ValidateArgs(inst, 2)) return true;
                SetSystemButton(GetString(inst.Arguments[0]), IsOn(inst.Arguments[1]));
                return true;

            case OpCode.YesNoBox:
                ExecuteYesNoBox(inst);
                return true;

            case OpCode.FadeIn:
                State.State = VmState.FadingIn;
                State.IsFading = true;
                State.FadeDurationMs = inst.Arguments.Count > 0 ? GetVal(inst.Arguments[0]) : 1000;
                return true;

            case OpCode.FadeOut:
                State.State = VmState.FadingOut;
                State.IsFading = true;
                State.FadeDurationMs = inst.Arguments.Count > 0 ? GetVal(inst.Arguments[0]) : 1000;
                return true;

            case OpCode.End:
                State.State = VmState.Ended;
                State.RequestClose = true;
                return true;

            case OpCode.FontFilter:
                if (!ValidateArgs(inst, 1)) return true;
                State.FontFilter = GetString(inst.Arguments[0]).ToLowerInvariant() switch
                {
                    "trilinear" => TextureFilter.Trilinear,
                    "point" => TextureFilter.Point,
                    "anisotropic" => TextureFilter.Trilinear,
                    _ => TextureFilter.Bilinear
                };
                return true;

            default:
                return false;
        }
    }

    private void ExecuteYesNoBox(Instruction inst)
    {
        if (!State.CompatAutoUi)
        {
            Reporter.Report(new AriaError("yesnobox の自動UI生成は compat_mode off で無効です。描画命令と btnwait で実装してください。", inst.SourceLine, CurrentScriptFile, AriaErrorLevel.Warning));
            return;
        }

        if (!ValidateArgs(inst, 3)) return;
        ClearCompatUiSprites();

        int yesNoW = Math.Max(160, State.ChoiceWidth / 3);
        int yesNoH = State.ChoiceHeight;
        int yesNoGap = 36;
        int centerX = State.WindowWidth / 2;
        int btnY = State.WindowHeight / 2 + 40;
        int leftX = centerX - yesNoW - (yesNoGap / 2);
        int rightX = centerX + (yesNoGap / 2);

        int yesRectId = AllocateCompatUiSpriteId();
        int yesTextId = AllocateCompatUiSpriteId();
        int noRectId = AllocateCompatUiSpriteId();
        int noTextId = AllocateCompatUiSpriteId();
        int msgRectId = AllocateCompatUiSpriteId();
        int msgTextId = AllocateCompatUiSpriteId();

        State.Sprites[yesRectId] = CreateChoiceRect(yesRectId, leftX, btnY, yesNoW, yesNoH, true);
        State.SpriteButtonMap[yesRectId] = 1;
        TrackCompatUiSprite(yesRectId);

        State.Sprites[yesTextId] = CreateChoiceText(yesTextId, "Yes", leftX, btnY, yesNoW, yesNoH);
        TrackCompatUiSprite(yesTextId);

        State.Sprites[noRectId] = CreateChoiceRect(noRectId, rightX, btnY, yesNoW, yesNoH, true);
        State.SpriteButtonMap[noRectId] = 0;
        TrackCompatUiSprite(noRectId);

        State.Sprites[noTextId] = CreateChoiceText(noTextId, "No", rightX, btnY, yesNoW, yesNoH);
        TrackCompatUiSprite(noTextId);

        int msgW = Math.Min(State.WindowWidth - 120, State.ChoiceWidth + 200);
        int msgX = (State.WindowWidth - msgW) / 2;
        int msgH = 110;
        int msgY = btnY - msgH - 30;
        State.Sprites[msgRectId] = new Sprite
        {
            Id = msgRectId,
            Type = SpriteType.Rect,
            Z = 9500,
            X = msgX,
            Y = msgY,
            Width = msgW,
            Height = msgH,
            FillColor = State.DefaultTextboxBgColor,
            FillAlpha = State.DefaultTextboxBgAlpha,
            CornerRadius = State.DefaultTextboxCornerRadius,
            BorderColor = State.DefaultTextboxBorderColor,
            BorderWidth = State.DefaultTextboxBorderWidth,
            BorderOpacity = State.DefaultTextboxBorderOpacity
        };
        TrackCompatUiSprite(msgRectId);

        State.Sprites[msgTextId] = new Sprite
        {
            Id = msgTextId,
            Type = SpriteType.Text,
            Z = 9501,
            Text = GetString(inst.Arguments[1]),
            X = msgX + State.DefaultTextboxPaddingX,
            Y = msgY + State.DefaultTextboxPaddingY,
            Width = msgW - (State.DefaultTextboxPaddingX * 2),
            Height = msgH - (State.DefaultTextboxPaddingY * 2),
            FontSize = State.DefaultFontSize,
            Color = State.DefaultTextColor
        };
        TrackCompatUiSprite(msgTextId);

        State.ButtonResultRegister = inst.Arguments[0];
        State.State = VmState.WaitingForButton;
    }

    private Sprite CreateChoiceRect(int id, int x, int y, int w, int h, bool isButton)
    {
        return new Sprite
        {
            Id = id,
            Type = SpriteType.Rect,
            Z = 9500,
            X = x,
            Y = y,
            Width = w,
            Height = h,
            FillColor = State.ChoiceBgColor,
            FillAlpha = State.ChoiceBgAlpha,
            IsButton = isButton,
            CornerRadius = State.ChoiceCornerRadius,
            BorderColor = State.ChoiceBorderColor,
            BorderWidth = State.ChoiceBorderWidth,
            BorderOpacity = State.ChoiceBorderOpacity,
            HoverFillColor = State.ChoiceHoverColor
        };
    }

    private Sprite CreateChoiceText(int id, string text, int x, int y, int w, int h)
    {
        return new Sprite
        {
            Id = id,
            Type = SpriteType.Text,
            Z = 9501,
            Text = text,
            X = x + State.ChoicePaddingX,
            Y = y,
            Width = w - (State.ChoicePaddingX * 2),
            Height = h,
            FontSize = State.ChoiceFontSize,
            Color = State.ChoiceTextColor,
            TextAlign = "center",
            TextVAlign = "center"
        };
    }
}
