using Raylib_cs;

namespace AriaEngine.Core.Commands;

public sealed class SystemCommandHandler : BaseCommandHandler
{
    public override IReadOnlySet<OpCode> HandledCodes { get; } = new HashSet<OpCode>
    {
        OpCode.Backlog,
        OpCode.BacklogCount,
        OpCode.BacklogEntry,
        OpCode.KidokuMode,
        OpCode.SkipMode,
        OpCode.SystemButton,
        OpCode.YesNoBox,
        OpCode.MesBox,
        OpCode.FadeIn,
        OpCode.FadeOut,
        OpCode.End,
        OpCode.FontFilter,
        OpCode.GalleryEntry,
        OpCode.CgUnlock,
        OpCode.GalleryCount,
        OpCode.GalleryInfo,
        OpCode.GetConfig,
        OpCode.SetConfig,
        OpCode.SaveConfig
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

            case OpCode.BacklogCount:
                if (!ValidateArgs(inst, 1)) return true;
                SetReg(GetString(inst.Arguments[0]), State.TextHistory.Count);
                return true;

            case OpCode.BacklogEntry:
                if (!ValidateArgs(inst, 2)) return true;
                {
                    int bIndex = GetVal(inst.Arguments[0]);
                    string bText = "";
                    if (bIndex >= 0 && bIndex < State.TextHistory.Count)
                    {
                        bText = State.TextHistory[bIndex].Text.Replace("\r", " ").Replace("\n", " / ");
                    }
                    SetStr(GetString(inst.Arguments[1]), bText);
                }
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

            case OpCode.MesBox:
                ExecuteMesBox(inst);
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

            case OpCode.GalleryEntry:
                if (!ValidateArgs(inst, 3)) return true;
                string flag = GetString(inst.Arguments[0]).Trim();
                string path = GetString(inst.Arguments[1]).Trim();
                string title = GetString(inst.Arguments[2]).Trim();
                State.GalleryEntries[flag] = new GalleryEntry
                {
                    FlagName = flag,
                    ImagePath = path,
                    Title = title
                };
                return true;

            case OpCode.CgUnlock:
                if (!ValidateArgs(inst, 1)) return true;
                string cgFlag = GetString(inst.Arguments[0]).Trim();
                if (State.UnlockedCgs.Add(cgFlag))
                {
                    Vm.MarkPersistentDirty();
                }
                return true;

            case OpCode.GetConfig:
                if (!ValidateArgs(inst, 2)) return true;
                {
                    string key = GetString(inst.Arguments[1]).Trim().ToLowerInvariant().Replace("_", "");
                    int value = key switch
                    {
                        "bgmvol" or "bgmvolume" => State.BgmVolume,
                        "sevol" or "sevolume" => State.SeVolume,
                        "textspeed" or "textspeedms" => State.TextSpeedMs,
                        "skipunread" or "skipunreadmode" => State.SkipUnread ? 1 : 0,
                        "backlog" or "backlogenabled" => State.BacklogEnabled ? 1 : 0,
                        "clickcursor" or "showclickcursor" => State.ShowClickCursor ? 1 : 0,
                        "autowait" or "automodewait" or "automodewaittimems" => State.AutoModeWaitTimeMs,
                        "fullscreen" => Config.Config.IsFullscreen ? 1 : 0,
                        _ => 0
                    };
                    SetReg(GetString(inst.Arguments[0]), value);
                }
                return true;

            case OpCode.SetConfig:
                if (!ValidateArgs(inst, 2)) return true;
                {
                    string key = GetString(inst.Arguments[0]).Trim().ToLowerInvariant().Replace("_", "");
                    int value = GetVal(inst.Arguments[1]);
                    switch (key)
                    {
                        case "bgmvol" or "bgmvolume":
                            State.BgmVolume = Math.Clamp(value, 0, 100);
                            break;
                        case "sevol" or "sevolume":
                            State.SeVolume = Math.Clamp(value, 0, 100);
                            break;
                        case "textspeed" or "textspeedms":
                            State.TextSpeedMs = Math.Max(0, value);
                            Config.Config.GlobalTextSpeedMs = State.TextSpeedMs;
                            break;
                        case "skipunread" or "skipunreadmode":
                            State.SkipUnread = value != 0;
                            break;
                        case "backlog" or "backlogenabled":
                            State.BacklogEnabled = value != 0;
                            break;
                        case "clickcursor" or "showclickcursor":
                            State.ShowClickCursor = value != 0;
                            break;
                        case "autowait" or "automodewait" or "automodewaittimems":
                            State.AutoModeWaitTimeMs = Math.Clamp(value, 100, 10000);
                            Config.Config.AutoModeWaitTimeMs = State.AutoModeWaitTimeMs;
                            break;
                        case "fullscreen":
                            bool targetFullscreen = value != 0;
                            if (Config.Config.IsFullscreen != targetFullscreen)
                                Vm.ToggleFullscreen();
                            break;
                    }
                    MarkPersistentDirty();
                }
                return true;

            case OpCode.GalleryCount:
                if (!ValidateArgs(inst, 1)) return true;
                SetReg(GetString(inst.Arguments[0]), State.GalleryEntries.Count);
                return true;

            case OpCode.GalleryInfo:
                if (!ValidateArgs(inst, 4)) return true;
                {
                    int gIndex = GetVal(inst.Arguments[0]);
                    var gEntries = State.GalleryEntries.Values.ToList();
                    string gTitle = "";
                    string gPath = "";
                    int gUnlocked = 0;
                    if (gIndex >= 0 && gIndex < gEntries.Count)
                    {
                        var gEntry = gEntries[gIndex];
                        gTitle = gEntry.Title;
                        gPath = gEntry.ImagePath;
                        gUnlocked = State.UnlockedCgs.Contains(gEntry.FlagName) ? 1 : 0;
                    }
                    SetStr(GetString(inst.Arguments[1]), gTitle);
                    SetStr(GetString(inst.Arguments[2]), gPath);
                    SetReg(GetString(inst.Arguments[3]), gUnlocked);
                }
                return true;

            case OpCode.SaveConfig:
                Vm.SavePersistentState();
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

    private void ExecuteMesBox(Instruction inst)
    {
        if (!State.CompatAutoUi)
        {
            Reporter.Report(new AriaError("mesbox の自動UI生成は compat_mode off で無効です。", inst.SourceLine, CurrentScriptFile, AriaErrorLevel.Warning));
            return;
        }

        if (!ValidateArgs(inst, 2)) return;
        ClearCompatUiSprites();

        string message = GetString(inst.Arguments[0]);
        string title = GetString(inst.Arguments[1]);

        int msgW = Math.Min(State.WindowWidth - 120, State.ChoiceWidth + 300);
        int msgX = (State.WindowWidth - msgW) / 2;
        int msgH = 140;
        int msgY = (State.WindowHeight - msgH) / 2 - 30;

        int btnW = Math.Max(100, State.ChoiceWidth / 2);
        int btnH = State.ChoiceHeight;
        int btnX = (State.WindowWidth - btnW) / 2;
        int btnY = msgY + msgH + 20;

        int msgRectId = AllocateCompatUiSpriteId();
        int msgTextId = AllocateCompatUiSpriteId();
        int titleTextId = AllocateCompatUiSpriteId();
        int btnRectId = AllocateCompatUiSpriteId();
        int btnTextId = AllocateCompatUiSpriteId();

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

        State.Sprites[titleTextId] = new Sprite
        {
            Id = titleTextId,
            Type = SpriteType.Text,
            Z = 9501,
            Text = title,
            X = msgX + State.DefaultTextboxPaddingX,
            Y = msgY + 16,
            Width = msgW - State.DefaultTextboxPaddingX * 2,
            Height = 30,
            FontSize = State.DefaultFontSize,
            Color = State.DefaultTextColor
        };
        TrackCompatUiSprite(titleTextId);

        State.Sprites[msgTextId] = new Sprite
        {
            Id = msgTextId,
            Type = SpriteType.Text,
            Z = 9501,
            Text = message,
            X = msgX + State.DefaultTextboxPaddingX,
            Y = msgY + 52,
            Width = msgW - State.DefaultTextboxPaddingX * 2,
            Height = msgH - 70,
            FontSize = State.DefaultFontSize - 2,
            Color = State.DefaultTextColor
        };
        TrackCompatUiSprite(msgTextId);

        State.Sprites[btnRectId] = CreateChoiceRect(btnRectId, btnX, btnY, btnW, btnH, true);
        State.SpriteButtonMap[btnRectId] = 1;
        TrackCompatUiSprite(btnRectId);

        State.Sprites[btnTextId] = CreateChoiceText(btnTextId, "OK", btnX, btnY, btnW, btnH);
        TrackCompatUiSprite(btnTextId);

        State.ButtonResultRegister = "0";
        State.State = VmState.WaitingForButton;
    }
}
