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
                if (inst.Arguments.Count > 0) State.TextRuntime.BacklogEnabled = IsOn(inst.Arguments[0]);
                return true;

            case OpCode.BacklogCount:
                if (!ValidateArgs(inst, 1)) return true;
                SetReg(GetString(inst.Arguments[0]), State.TextRuntime.TextHistory.Count);
                return true;

            case OpCode.BacklogEntry:
                if (!ValidateArgs(inst, 2)) return true;
                {
                    int bIndex = GetVal(inst.Arguments[0]);
                    string bText = "";
                    if (bIndex >= 0 && bIndex < State.TextRuntime.TextHistory.Count)
                    {
                        bText = State.TextRuntime.TextHistory[bIndex].Text.Replace("\r", " ").Replace("\n", " / ");
                    }
                    SetStr(GetString(inst.Arguments[1]), bText);
                }
                return true;

            case OpCode.KidokuMode:
                if (inst.Arguments.Count > 0) State.TextRuntime.KidokuMode = GetVal(inst.Arguments[0]) != 0;
                return true;

            case OpCode.SkipMode:
                if (inst.Arguments.Count > 0)
                {
                    string skipMode = GetString(inst.Arguments[0]).ToLowerInvariant();
                    State.Playback.SkipUnread = skipMode is "all" or "unread" or "1" or "on";
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
                State.Execution.State = VmState.FadingIn;
                State.Render.IsFading = true;
                State.Render.FadeDurationMs = inst.Arguments.Count > 0 ? GetVal(inst.Arguments[0]) : 1000;
                return true;

            case OpCode.FadeOut:
                State.Execution.State = VmState.FadingOut;
                State.Render.IsFading = true;
                State.Render.FadeDurationMs = inst.Arguments.Count > 0 ? GetVal(inst.Arguments[0]) : 1000;
                return true;

            case OpCode.End:
                State.Execution.State = VmState.Ended;
                State.UiRuntime.RequestClose = true;
                return true;

            case OpCode.FontFilter:
                if (!ValidateArgs(inst, 1)) return true;
                State.EngineSettings.FontFilter = GetString(inst.Arguments[0]).ToLowerInvariant() switch
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
                State.FlagRuntime.GalleryEntries[flag] = new GalleryEntry
                {
                    FlagName = flag,
                    ImagePath = path,
                    Title = title
                };
                return true;

            case OpCode.CgUnlock:
                if (!ValidateArgs(inst, 1)) return true;
                string cgFlag = GetString(inst.Arguments[0]).Trim();
                if (State.FlagRuntime.UnlockedCgs.Add(cgFlag))
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
                        "bgmvol" or "bgmvolume" => State.Audio.BgmVolume,
                        "sevol" or "sevolume" => State.Audio.SeVolume,
                        "textspeed" or "textspeedms" => State.TextRuntime.TextSpeedMs,
                        "skipunread" or "skipunreadmode" => State.Playback.SkipUnread ? 1 : 0,
                        "backlog" or "backlogenabled" => State.TextRuntime.BacklogEnabled ? 1 : 0,
                        "clickcursor" or "showclickcursor" => State.UiRuntime.ShowClickCursor ? 1 : 0,
                        "autowait" or "automodewait" or "automodewaittimems" => State.Playback.AutoModeWaitTimeMs,
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
                            State.Audio.BgmVolume = Math.Clamp(value, 0, 100);
                            break;
                        case "sevol" or "sevolume":
                            State.Audio.SeVolume = Math.Clamp(value, 0, 100);
                            break;
                        case "textspeed" or "textspeedms":
                            State.TextRuntime.TextSpeedMs = Math.Max(0, value);
                            Config.Config.GlobalTextSpeedMs = State.TextRuntime.TextSpeedMs;
                            break;
                        case "skipunread" or "skipunreadmode":
                            State.Playback.SkipUnread = value != 0;
                            break;
                        case "backlog" or "backlogenabled":
                            State.TextRuntime.BacklogEnabled = value != 0;
                            break;
                        case "clickcursor" or "showclickcursor":
                            State.UiRuntime.ShowClickCursor = value != 0;
                            break;
                        case "autowait" or "automodewait" or "automodewaittimems":
                            State.Playback.AutoModeWaitTimeMs = Math.Clamp(value, 100, 10000);
                            Config.Config.AutoModeWaitTimeMs = State.Playback.AutoModeWaitTimeMs;
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
                SetReg(GetString(inst.Arguments[0]), State.FlagRuntime.GalleryEntries.Count);
                return true;

            case OpCode.GalleryInfo:
                if (!ValidateArgs(inst, 4)) return true;
                {
                    int gIndex = GetVal(inst.Arguments[0]);
                    var gEntries = State.FlagRuntime.GalleryEntries.Values.ToList();
                    string gTitle = "";
                    string gPath = "";
                    int gUnlocked = 0;
                    if (gIndex >= 0 && gIndex < gEntries.Count)
                    {
                        var gEntry = gEntries[gIndex];
                        gTitle = gEntry.Title;
                        gPath = gEntry.ImagePath;
                        gUnlocked = State.FlagRuntime.UnlockedCgs.Contains(gEntry.FlagName) ? 1 : 0;
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
        if (!State.TextWindow.CompatAutoUi)
        {
            Reporter.Report(new AriaError("yesnobox の自動UI生成は compat_mode off で無効です。描画命令と btnwait で実装してください。", inst.SourceLine, CurrentScriptFile, AriaErrorLevel.Warning));
            return;
        }

        if (!ValidateArgs(inst, 3)) return;
        ClearCompatUiSprites();

        int yesNoW = Math.Max(160, State.ChoiceStyle.ChoiceWidth / 3);
        int yesNoH = State.ChoiceStyle.ChoiceHeight;
        int yesNoGap = 36;
        int centerX = State.EngineSettings.WindowWidth / 2;
        int btnY = State.EngineSettings.WindowHeight / 2 + 40;
        int leftX = centerX - yesNoW - (yesNoGap / 2);
        int rightX = centerX + (yesNoGap / 2);

        int yesRectId = AllocateCompatUiSpriteId();
        int yesTextId = AllocateCompatUiSpriteId();
        int noRectId = AllocateCompatUiSpriteId();
        int noTextId = AllocateCompatUiSpriteId();
        int msgRectId = AllocateCompatUiSpriteId();
        int msgTextId = AllocateCompatUiSpriteId();

        State.Render.Sprites[yesRectId] = CreateChoiceRect(yesRectId, leftX, btnY, yesNoW, yesNoH, true);
        State.Interaction.SpriteButtonMap[yesRectId] = 1;
        TrackCompatUiSprite(yesRectId);

        State.Render.Sprites[yesTextId] = CreateChoiceText(yesTextId, "Yes", leftX, btnY, yesNoW, yesNoH);
        TrackCompatUiSprite(yesTextId);

        State.Render.Sprites[noRectId] = CreateChoiceRect(noRectId, rightX, btnY, yesNoW, yesNoH, true);
        State.Interaction.SpriteButtonMap[noRectId] = 0;
        TrackCompatUiSprite(noRectId);

        State.Render.Sprites[noTextId] = CreateChoiceText(noTextId, "No", rightX, btnY, yesNoW, yesNoH);
        TrackCompatUiSprite(noTextId);

        int msgW = Math.Min(State.EngineSettings.WindowWidth - 120, State.ChoiceStyle.ChoiceWidth + 200);
        int msgX = (State.EngineSettings.WindowWidth - msgW) / 2;
        int msgH = 110;
        int msgY = btnY - msgH - 30;
        State.Render.Sprites[msgRectId] = new Sprite
        {
            Id = msgRectId,
            Type = SpriteType.Rect,
            Z = 9500,
            X = msgX,
            Y = msgY,
            Width = msgW,
            Height = msgH,
            FillColor = State.TextWindow.DefaultTextboxBgColor,
            FillAlpha = State.TextWindow.DefaultTextboxBgAlpha,
            CornerRadius = State.TextWindow.DefaultTextboxCornerRadius,
            BorderColor = State.TextWindow.DefaultTextboxBorderColor,
            BorderWidth = State.TextWindow.DefaultTextboxBorderWidth,
            BorderOpacity = State.TextWindow.DefaultTextboxBorderOpacity
        };
        TrackCompatUiSprite(msgRectId);

        State.Render.Sprites[msgTextId] = new Sprite
        {
            Id = msgTextId,
            Type = SpriteType.Text,
            Z = 9501,
            Text = GetString(inst.Arguments[1]),
            X = msgX + State.TextWindow.DefaultTextboxPaddingX,
            Y = msgY + State.TextWindow.DefaultTextboxPaddingY,
            Width = msgW - (State.TextWindow.DefaultTextboxPaddingX * 2),
            Height = msgH - (State.TextWindow.DefaultTextboxPaddingY * 2),
            FontSize = State.TextWindow.DefaultFontSize,
            Color = State.TextWindow.DefaultTextColor
        };
        TrackCompatUiSprite(msgTextId);

        State.Interaction.ButtonResultRegister = inst.Arguments[0];
        State.Execution.State = VmState.WaitingForButton;
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
            FillColor = State.ChoiceStyle.ChoiceBgColor,
            FillAlpha = State.ChoiceStyle.ChoiceBgAlpha,
            IsButton = isButton,
            CornerRadius = State.ChoiceStyle.ChoiceCornerRadius,
            BorderColor = State.ChoiceStyle.ChoiceBorderColor,
            BorderWidth = State.ChoiceStyle.ChoiceBorderWidth,
            BorderOpacity = State.ChoiceStyle.ChoiceBorderOpacity,
            HoverFillColor = State.ChoiceStyle.ChoiceHoverColor
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
            X = x + State.ChoiceStyle.ChoicePaddingX,
            Y = y,
            Width = w - (State.ChoiceStyle.ChoicePaddingX * 2),
            Height = h,
            FontSize = State.ChoiceStyle.ChoiceFontSize,
            Color = State.ChoiceStyle.ChoiceTextColor,
            TextAlign = TextAlignment.Center,
            TextVAlign = TextVerticalAlignment.Center
        };
    }

    private void ExecuteMesBox(Instruction inst)
    {
        if (!State.TextWindow.CompatAutoUi)
        {
            Reporter.Report(new AriaError("mesbox の自動UI生成は compat_mode off で無効です。", inst.SourceLine, CurrentScriptFile, AriaErrorLevel.Warning));
            return;
        }

        if (!ValidateArgs(inst, 2)) return;
        ClearCompatUiSprites();

        string message = GetString(inst.Arguments[0]);
        string title = GetString(inst.Arguments[1]);

        int msgW = Math.Min(State.EngineSettings.WindowWidth - 120, State.ChoiceStyle.ChoiceWidth + 300);
        int msgX = (State.EngineSettings.WindowWidth - msgW) / 2;
        int msgH = 140;
        int msgY = (State.EngineSettings.WindowHeight - msgH) / 2 - 30;

        int btnW = Math.Max(100, State.ChoiceStyle.ChoiceWidth / 2);
        int btnH = State.ChoiceStyle.ChoiceHeight;
        int btnX = (State.EngineSettings.WindowWidth - btnW) / 2;
        int btnY = msgY + msgH + 20;

        int msgRectId = AllocateCompatUiSpriteId();
        int msgTextId = AllocateCompatUiSpriteId();
        int titleTextId = AllocateCompatUiSpriteId();
        int btnRectId = AllocateCompatUiSpriteId();
        int btnTextId = AllocateCompatUiSpriteId();

        State.Render.Sprites[msgRectId] = new Sprite
        {
            Id = msgRectId,
            Type = SpriteType.Rect,
            Z = 9500,
            X = msgX,
            Y = msgY,
            Width = msgW,
            Height = msgH,
            FillColor = State.TextWindow.DefaultTextboxBgColor,
            FillAlpha = State.TextWindow.DefaultTextboxBgAlpha,
            CornerRadius = State.TextWindow.DefaultTextboxCornerRadius,
            BorderColor = State.TextWindow.DefaultTextboxBorderColor,
            BorderWidth = State.TextWindow.DefaultTextboxBorderWidth,
            BorderOpacity = State.TextWindow.DefaultTextboxBorderOpacity
        };
        TrackCompatUiSprite(msgRectId);

        State.Render.Sprites[titleTextId] = new Sprite
        {
            Id = titleTextId,
            Type = SpriteType.Text,
            Z = 9501,
            Text = title,
            X = msgX + State.TextWindow.DefaultTextboxPaddingX,
            Y = msgY + 16,
            Width = msgW - State.TextWindow.DefaultTextboxPaddingX * 2,
            Height = 30,
            FontSize = State.TextWindow.DefaultFontSize,
            Color = State.TextWindow.DefaultTextColor
        };
        TrackCompatUiSprite(titleTextId);

        State.Render.Sprites[msgTextId] = new Sprite
        {
            Id = msgTextId,
            Type = SpriteType.Text,
            Z = 9501,
            Text = message,
            X = msgX + State.TextWindow.DefaultTextboxPaddingX,
            Y = msgY + 52,
            Width = msgW - State.TextWindow.DefaultTextboxPaddingX * 2,
            Height = msgH - 70,
            FontSize = State.TextWindow.DefaultFontSize - 2,
            Color = State.TextWindow.DefaultTextColor
        };
        TrackCompatUiSprite(msgTextId);

        State.Render.Sprites[btnRectId] = CreateChoiceRect(btnRectId, btnX, btnY, btnW, btnH, true);
        State.Interaction.SpriteButtonMap[btnRectId] = 1;
        TrackCompatUiSprite(btnRectId);

        State.Render.Sprites[btnTextId] = CreateChoiceText(btnTextId, "OK", btnX, btnY, btnW, btnH);
        TrackCompatUiSprite(btnTextId);

        State.Interaction.ButtonResultRegister = "0";
        State.Execution.State = VmState.WaitingForButton;
    }
}
