namespace AriaEngine.Core.Commands;

public sealed class RenderCommandHandler : BaseCommandHandler
{
    public override IReadOnlySet<OpCode> HandledCodes { get; } = new HashSet<OpCode>
    {
        OpCode.Lsp,
        OpCode.LspText,
        OpCode.LspRect,
        OpCode.Csp,
        OpCode.Vsp,
        OpCode.Msp,
        OpCode.MspRel,
        OpCode.SpZ,
        OpCode.SpAlpha,
        OpCode.SpScale,
        OpCode.SpFontsize,
        OpCode.SpColor,
        OpCode.SpFill,
        OpCode.LoadBg,
        OpCode.Bg,
        OpCode.BgFade,
        OpCode.BgTime,
        OpCode.BgTimeMap,
        OpCode.Transition,
        OpCode.Camera,
        OpCode.Screen,
        OpCode.TextFx,
        OpCode.Fx,
        OpCode.Sync,
        OpCode.Print,
        OpCode.Effect,
        OpCode.Quake,
        OpCode.Clr
    };

    public RenderCommandHandler(VirtualMachine vm) : base(vm)
    {
    }

    public override bool Execute(Instruction inst)
    {
        switch (inst.Op)
        {
            case OpCode.Lsp:
                if (!ValidateArgs(inst, 4)) return true;
                {
                    int id = GetVal(inst.Arguments[0]);
                    State.Sprites[id] = new Sprite
                    {
                        Id = id,
                        Type = SpriteType.Image,
                        ImagePath = GetString(inst.Arguments[1]),
                        X = GetVal(inst.Arguments[2]),
                        Y = GetVal(inst.Arguments[3])
                    };
                    TrackSpriteLifetime(id, inst.Arguments[0]);
                }
                return true;

            case OpCode.LspText:
                if (!ValidateArgs(inst, 4)) return true;
                {
                    int id = GetVal(inst.Arguments[0]);
                    State.Sprites[id] = new Sprite
                    {
                        Id = id,
                        Type = SpriteType.Text,
                        Text = GetString(inst.Arguments[1]),
                        X = GetVal(inst.Arguments[2]),
                        Y = GetVal(inst.Arguments[3]),
                        FontSize = State.DefaultFontSize,
                        Color = State.DefaultTextColor
                    };
                    TrackSpriteLifetime(id, inst.Arguments[0]);
                }
                return true;

            case OpCode.LspRect:
                if (!ValidateArgs(inst, 5)) return true;
                {
                    int id = GetVal(inst.Arguments[0]);
                    State.Sprites[id] = new Sprite
                    {
                        Id = id,
                        Type = SpriteType.Rect,
                        X = GetVal(inst.Arguments[1]),
                        Y = GetVal(inst.Arguments[2]),
                        Width = GetVal(inst.Arguments[3]),
                        Height = GetVal(inst.Arguments[4])
                    };
                    TrackSpriteLifetime(id, inst.Arguments[0]);
                }
                return true;

            case OpCode.Csp:
                if (!ValidateArgs(inst, 1)) return true;
                {
                    int id = GetVal(inst.Arguments[0]);
                    if (id == -1)
                    {
                        State.Sprites.TryGetValue(0, out var background);
                        State.Sprites.Clear();
                        if (background != null) State.Sprites[0] = background;
                        State.SpriteButtonMap.Clear();
                        State.FocusedButtonId = -1;
                        State.SpriteLifetimeStacks.Clear();
                    }
                    else
                    {
                        State.Sprites.Remove(id);
                        State.SpriteButtonMap.Remove(id);
                        if (State.FocusedButtonId == id) State.FocusedButtonId = -1;
                    }
                }
                return true;

            case OpCode.Vsp:
                if (!ValidateArgs(inst, 2)) return true;
                if (State.Sprites.TryGetValue(GetVal(inst.Arguments[0]), out var vsp))
                {
                    vsp.Visible = int.TryParse(inst.Arguments[1], out int v) ? v != 0 : inst.Arguments[1] == "on";
                }
                return true;

            case OpCode.Msp:
                if (!ValidateArgs(inst, 3)) return true;
                if (State.Sprites.TryGetValue(GetVal(inst.Arguments[0]), out var msp))
                {
                    msp.X = GetVal(inst.Arguments[1]);
                    msp.Y = GetVal(inst.Arguments[2]);
                }
                return true;

            case OpCode.MspRel:
                if (!ValidateArgs(inst, 3)) return true;
                if (State.Sprites.TryGetValue(GetVal(inst.Arguments[0]), out var mspr))
                {
                    mspr.X += GetVal(inst.Arguments[1]);
                    mspr.Y += GetVal(inst.Arguments[2]);
                }
                return true;

            case OpCode.SpZ:
                if (!ValidateArgs(inst, 2)) return true;
                if (State.Sprites.TryGetValue(GetVal(inst.Arguments[0]), out var spz)) spz.Z = GetVal(inst.Arguments[1]);
                return true;

            case OpCode.SpAlpha:
                if (!ValidateArgs(inst, 2)) return true;
                if (State.Sprites.TryGetValue(GetVal(inst.Arguments[0]), out var spa)) spa.Opacity = GetVal(inst.Arguments[1]) / 255.0f;
                return true;

            case OpCode.SpScale:
                if (!ValidateArgs(inst, 3)) return true;
                if (State.Sprites.TryGetValue(GetVal(inst.Arguments[0]), out var spsc))
                {
                    spsc.ScaleX = GetFloat(inst.Arguments[1], inst);
                    spsc.ScaleY = GetFloat(inst.Arguments[2], inst);
                }
                return true;

            case OpCode.SpFontsize:
                if (!ValidateArgs(inst, 2)) return true;
                if (State.Sprites.TryGetValue(GetVal(inst.Arguments[0]), out var spf)) spf.FontSize = GetVal(inst.Arguments[1]);
                return true;

            case OpCode.SpColor:
                if (!ValidateArgs(inst, 2)) return true;
                if (State.Sprites.TryGetValue(GetVal(inst.Arguments[0]), out var spc)) spc.Color = GetString(inst.Arguments[1]);
                return true;

            case OpCode.SpFill:
                if (!ValidateArgs(inst, 3)) return true;
                if (State.Sprites.TryGetValue(GetVal(inst.Arguments[0]), out var spfl))
                {
                    spfl.FillColor = GetString(inst.Arguments[1]);
                    spfl.FillAlpha = GetVal(inst.Arguments[2]);
                }
                return true;

            case OpCode.LoadBg:
            case OpCode.Bg:
                if (!ValidateArgs(inst, 1)) return true;
                {
                    string bgPath = GetString(inst.Arguments[0]);
                    var tone = ResolveBackgroundTone(inst, bgPath, 1);
                    State.Sprites[0] = CreateBackgroundSprite(bgPath, tone.TimeOfDay, tone.Preset);
                }
                return true;

            case OpCode.BgFade:
                if (!ValidateArgs(inst, 1)) return true;
                {
                    string bgPath = GetString(inst.Arguments[0]);
                    int duration = inst.Arguments.Count > 1 ? NormalizeFadeDuration(GetVal(inst.Arguments[1])) : 700;
                    var tone = ResolveBackgroundTone(inst, bgPath, 2);
                    StartBackgroundFade(bgPath, duration, tone.TimeOfDay, tone.Preset);
                }
                return true;

            case OpCode.BgTime:
                if (!ValidateArgs(inst, 1)) return true;
                State.BackgroundTimeOfDay = NormalizeBackgroundTime(GetVal(inst.Arguments[0]));
                State.BackgroundTimePreset = inst.Arguments.Count > 1 ? GetString(inst.Arguments[1]) : "";
                return true;

            case OpCode.BgTimeMap:
                if (!ValidateArgs(inst, 2)) return true;
                {
                    string key = NormalizeBackgroundMapKey(GetString(inst.Arguments[0]));
                    State.BackgroundTimeMap[key] = new BackgroundTimeMapping
                    {
                        TimeOfDay = NormalizeBackgroundTime(GetVal(inst.Arguments[1])),
                        Preset = inst.Arguments.Count > 2 ? GetString(inst.Arguments[2]) : ""
                    };
                }
                return true;

            case OpCode.Transition:
                ExecuteTransition(inst);
                return true;

            case OpCode.Camera:
                ExecuteCamera(inst);
                return true;

            case OpCode.Screen:
                ExecuteScreen(inst);
                return true;

            case OpCode.TextFx:
                ExecuteTextFx(inst);
                return true;

            case OpCode.Fx:
                ExecuteFx(inst);
                return true;

            case OpCode.Sync:
                ExecuteSync(inst);
                return true;

            case OpCode.Print:
                return true;
            case OpCode.Effect:
                return true;

            case OpCode.Quake:
                {
                    int amp = inst.Arguments.Count > 0 ? GetVal(inst.Arguments[0]) : 5;
                    int time = inst.Arguments.Count > 1 ? GetVal(inst.Arguments[1]) : 500;
                    State.QuakeAmplitude = amp;
                    State.QuakeTimerMs = time;
                }
                return true;

            case OpCode.Clr:
                State.Sprites.Clear();
                State.SpriteButtonMap.Clear();
                State.FocusedButtonId = -1;
                ClearCompatUiSprites();
                State.TextboxBackgroundSpriteId = -1;
                State.SpriteLifetimeStacks.Clear();
                return true;

            default:
                return false;
        }
    }

    /// <summary>
    /// 関数スコープ内で作成されたスプライトを寿命管理に登録（C++like RAII）
    /// T13: Owned sprites are always tracked, even outside explicit scope blocks.
    /// </summary>
    private void TrackSpriteLifetime(int spriteId, string? arg = null)
    {
        bool isOwned = arg != null && State.OwnedSprites.Contains(arg);
        if (isOwned && State.SpriteLifetimeStacks.Count == 0)
        {
            State.SpriteLifetimeStacks.Push(new HashSet<int>());
        }
        if (State.SpriteLifetimeStacks.Count > 0)
        {
            State.SpriteLifetimeStacks.Peek().Add(spriteId);
        }
    }

    private Sprite CreateBackgroundSprite(string bgPath, int timeOfDay = 0, string preset = "")
    {
        return bgPath.StartsWith("#")
            ? new Sprite { Id = 0, Type = SpriteType.Rect, FillColor = bgPath, FillAlpha = 255, Width = State.WindowWidth, Height = State.WindowHeight, Z = 0, BackgroundTimeOfDay = timeOfDay, BackgroundTimePreset = preset }
            : new Sprite { Id = 0, Type = SpriteType.Image, ImagePath = bgPath, Width = State.WindowWidth, Height = State.WindowHeight, Z = 0, BackgroundTimeOfDay = timeOfDay, BackgroundTimePreset = preset };
    }

    private void StartBackgroundFade(string bgPath, int durationMs, int timeOfDay = 0, string preset = "")
    {
        const int overlayId = 8999;
        int half = Math.Max(1, durationMs / 2);
        State.Sprites[overlayId] = new Sprite
        {
            Id = overlayId,
            Type = SpriteType.Rect,
            X = 0,
            Y = 0,
            Width = State.WindowWidth,
            Height = State.WindowHeight,
            FillColor = "#000000",
            FillAlpha = 255,
            Opacity = 0f,
            Z = 9998
        };

        Tweens.Add(new AriaEngine.Rendering.Tween
        {
            SpriteId = overlayId,
            Property = AriaEngine.Rendering.TweenProperty.Opacity,
            From = 0f,
            To = 1f,
            DurationMs = half,
            Ease = AriaEngine.Rendering.EaseType.EaseInOut,
            OnComplete = (state, _) =>
            {
                state.Sprites[0] = CreateBackgroundSprite(bgPath, timeOfDay, preset);
                if (!state.Sprites.TryGetValue(overlayId, out var overlay)) return;
                overlay.Opacity = 1f;
                Tweens.Add(new AriaEngine.Rendering.Tween
                {
                    SpriteId = overlayId,
                    Property = AriaEngine.Rendering.TweenProperty.Opacity,
                    From = 1f,
                    To = 0f,
                    DurationMs = half,
                    Ease = AriaEngine.Rendering.EaseType.EaseInOut,
                    OnComplete = (innerState, __) => innerState.Sprites.Remove(overlayId)
                });
            }
        });
        State.State = VmState.WaitingForAnimation;
    }

    private static int NormalizeFadeDuration(int value)
    {
        if (value <= 0) return 0;
        return value <= 20 ? 700 : value;
    }

    private void ExecuteTransition(Instruction inst)
    {
        if (!ValidateArgs(inst, 4)) return;
        string target = GetString(inst.Arguments[0]).ToLowerInvariant();
        string path = GetString(inst.Arguments[1]);
        string style = GetString(inst.Arguments[2]).ToLowerInvariant();
        int duration = Math.Max(0, GetVal(inst.Arguments[3]));
        if (target != "bg") return;

        if (style is "white" or "flash")
        {
            StartScreenPulse("#ffffff", 0.92f, Math.Min(duration, 260));
        }

        StartBackgroundFade(path, duration);
        State.ActiveEffects.Add($"transition:bg:{style}");
    }

    private (int TimeOfDay, string Preset) ResolveBackgroundTone(Instruction inst, string bgPath, int firstToneArg)
    {
        if (inst.Arguments.Count > firstToneArg)
        {
            int explicitTime = NormalizeBackgroundTime(GetVal(inst.Arguments[firstToneArg]));
            string explicitPreset = inst.Arguments.Count > firstToneArg + 1 ? GetString(inst.Arguments[firstToneArg + 1]) : "";
            return (explicitTime, explicitPreset);
        }

        string key = NormalizeBackgroundMapKey(bgPath);
        if (State.BackgroundTimeMap.TryGetValue(key, out var mapped))
        {
            return (NormalizeBackgroundTime(mapped.TimeOfDay), mapped.Preset);
        }

        return (NormalizeBackgroundTime(State.BackgroundTimeOfDay), State.BackgroundTimePreset);
    }

    private static int NormalizeBackgroundTime(int value) => Math.Clamp(value, 0, 4);

    private static string NormalizeBackgroundMapKey(string path)
    {
        string normalized = path.Replace('\\', '/');
        string file = normalized.Contains('/') ? normalized[(normalized.LastIndexOf('/') + 1)..] : normalized;
        int dot = file.LastIndexOf('.');
        return dot > 0 ? file[..dot] : file;
    }

    private void ExecuteCamera(Instruction inst)
    {
        if (!ValidateArgs(inst, 1)) return;
        string action = GetString(inst.Arguments[0]).ToLowerInvariant();
        switch (action)
        {
            case "shake":
                State.QuakeAmplitude = inst.Arguments.Count > 1 ? GetVal(inst.Arguments[1]) : 6;
                State.QuakeTimerMs = inst.Arguments.Count > 2 ? GetVal(inst.Arguments[2]) : 300;
                State.ActiveEffects.Add("camera:shake");
                break;
            case "pan":
                State.CameraOffsetX = inst.Arguments.Count > 1 ? GetVal(inst.Arguments[1]) : 0;
                State.CameraOffsetY = inst.Arguments.Count > 2 ? GetVal(inst.Arguments[2]) : 0;
                State.ActiveEffects.Add("camera:pan");
                break;
            case "zoom":
                State.CameraZoom = inst.Arguments.Count > 1 ? GetFloat(inst.Arguments[1], inst, 1f) : 1f;
                State.ActiveEffects.Add("camera:zoom");
                break;
            case "reset":
                State.CameraOffsetX = 0;
                State.CameraOffsetY = 0;
                State.CameraZoom = 1f;
                State.QuakeAmplitude = 0;
                State.QuakeTimerMs = 0;
                State.ActiveEffects.RemoveAll(e => e.StartsWith("camera:", StringComparison.OrdinalIgnoreCase));
                break;
        }
    }

    private void ExecuteScreen(Instruction inst)
    {
        if (!ValidateArgs(inst, 1)) return;
        string action = GetString(inst.Arguments[0]).ToLowerInvariant();
        switch (action)
        {
            case "flash":
                StartScreenPulse(
                    inst.Arguments.Count > 1 ? GetString(inst.Arguments[1]) : "#ffffff",
                    0.9f,
                    inst.Arguments.Count > 2 ? GetVal(inst.Arguments[2]) : 180);
                break;
            case "tint":
                State.ScreenTintColor = inst.Arguments.Count > 1 ? GetString(inst.Arguments[1]) : "#1d2430";
                State.ScreenTintOpacity = inst.Arguments.Count > 2 ? Math.Clamp(GetVal(inst.Arguments[2]) / 255f, 0f, 1f) : 0.35f;
                State.ScreenTintTimerMs = inst.Arguments.Count > 3 ? Math.Max(0, GetVal(inst.Arguments[3])) : 0;
                State.ActiveEffects.Add("screen:tint");
                break;
            case "clear":
            case "reset":
                State.ScreenTintOpacity = 0f;
                State.ScreenTintTimerMs = 0f;
                State.ActiveEffects.RemoveAll(e => e.StartsWith("screen:", StringComparison.OrdinalIgnoreCase));
                break;
        }
    }

    private void ExecuteTextFx(Instruction inst)
    {
        if (!ValidateArgs(inst, 1)) return;
        string name = GetString(inst.Arguments[0]).ToLowerInvariant();
        if (name == "reset")
        {
            State.DefaultTextEffect = "none";
            State.DefaultTextEffectStrength = 0f;
            State.DefaultTextEffectSpeed = 0f;
            State.TextSpeedMs = Config.Config.GlobalTextSpeedMs > 0 ? Config.Config.GlobalTextSpeedMs : Config.Config.DefaultTextSpeedMs;
            State.ActiveEffects.RemoveAll(e => e.StartsWith("textfx:", StringComparison.OrdinalIgnoreCase));
            return;
        }

        if (name is "speed" or "type")
        {
            if (inst.Arguments.Count > 1) State.TextSpeedMs = Math.Max(0, GetVal(inst.Arguments[1]));
            State.ActiveEffects.Add("textfx:speed");
            return;
        }

        State.DefaultTextEffect = name;
        if (inst.Arguments.Count > 1) State.DefaultTextEffectStrength = GetFloat(inst.Arguments[1], inst, State.DefaultTextEffectStrength);
        if (inst.Arguments.Count > 2) State.DefaultTextEffectSpeed = GetFloat(inst.Arguments[2], inst, State.DefaultTextEffectSpeed);
        State.ActiveEffects.Add($"textfx:{name}");
    }

    private void ExecuteFx(Instruction inst)
    {
        if (!ValidateArgs(inst, 1)) return;
        string action = GetString(inst.Arguments[0]).ToLowerInvariant();
        switch (action)
        {
            case "profile":
                if (inst.Arguments.Count > 1) State.FxProfile = GetString(inst.Arguments[1]).ToLowerInvariant();
                break;
            case "skip_policy":
                if (inst.Arguments.Count > 1) State.FxSkipPolicy = GetString(inst.Arguments[1]).ToLowerInvariant();
                break;
            case "cancel":
                string layer = inst.Arguments.Count > 1 ? GetString(inst.Arguments[1]).ToLowerInvariant() : "all";
                CancelFx(layer);
                break;
        }
    }

    private void ExecuteSync(Instruction inst)
    {
        if (!ValidateArgs(inst, 1)) return;
        string target = GetString(inst.Arguments[0]).ToLowerInvariant();
        if (target is "fx" or "all")
        {
            State.State = VmState.WaitingForAnimation;
        }
    }

    private void StartScreenPulse(string color, float opacity, int durationMs)
    {
        State.ScreenTintColor = color;
        State.ScreenTintOpacity = Math.Clamp(opacity, 0f, 1f);
        State.ScreenTintTimerMs = Math.Max(1, durationMs);
        State.ActiveEffects.Add("screen:flash");
    }

    private void CancelFx(string layer)
    {
        if (layer is "all" or "screen")
        {
            State.ScreenTintOpacity = 0f;
            State.ScreenTintTimerMs = 0f;
            State.ActiveEffects.RemoveAll(e => e.StartsWith("screen:", StringComparison.OrdinalIgnoreCase));
        }
        if (layer is "all" or "camera")
        {
            State.CameraOffsetX = 0;
            State.CameraOffsetY = 0;
            State.CameraZoom = 1f;
            State.QuakeAmplitude = 0;
            State.QuakeTimerMs = 0;
            State.ActiveEffects.RemoveAll(e => e.StartsWith("camera:", StringComparison.OrdinalIgnoreCase));
        }
        if (layer is "all" or "text" or "textfx")
        {
            State.DefaultTextEffect = "none";
            State.ActiveEffects.RemoveAll(e => e.StartsWith("textfx:", StringComparison.OrdinalIgnoreCase));
        }
        if (layer == "all")
        {
            State.ActiveEffects.Clear();
        }
    }
}
