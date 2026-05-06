using System.IO;

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
                    State.Render.Sprites[id] = new Sprite
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
                    State.Render.Sprites[id] = new Sprite
                    {
                        Id = id,
                        Type = SpriteType.Text,
                        Text = GetString(inst.Arguments[1]),
                        X = GetVal(inst.Arguments[2]),
                        Y = GetVal(inst.Arguments[3]),
                        FontSize = State.TextWindow.DefaultFontSize,
                        Color = State.TextWindow.DefaultTextColor
                    };
                    TrackSpriteLifetime(id, inst.Arguments[0]);
                }
                return true;

            case OpCode.LspRect:
                if (!ValidateArgs(inst, 5)) return true;
                {
                    int id = GetVal(inst.Arguments[0]);
                    State.Render.Sprites[id] = new Sprite
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
                        State.Render.Sprites.TryGetValue(0, out var background);
                        State.Render.Sprites.Clear();
                        if (background != null) State.Render.Sprites[0] = background;
                        State.Interaction.SpriteButtonMap.Clear();
                        State.Interaction.FocusedButtonId = -1;
                        State.Execution.SpriteLifetimeStacks.Clear();
                    }
                    else
                    {
                        State.Render.Sprites.Remove(id);
                        State.Interaction.SpriteButtonMap.Remove(id);
                        if (State.Interaction.FocusedButtonId == id) State.Interaction.FocusedButtonId = -1;
                    }
                }
                return true;

            case OpCode.Vsp:
                if (!ValidateArgs(inst, 2)) return true;
                if (State.Render.Sprites.TryGetValue(GetVal(inst.Arguments[0]), out var vsp))
                {
                    vsp.Visible = int.TryParse(inst.Arguments[1], out int v) ? v != 0 : inst.Arguments[1] == "on";
                }
                return true;

            case OpCode.Msp:
                if (!ValidateArgs(inst, 3)) return true;
                if (State.Render.Sprites.TryGetValue(GetVal(inst.Arguments[0]), out var msp))
                {
                    msp.X = GetVal(inst.Arguments[1]);
                    msp.Y = GetVal(inst.Arguments[2]);
                }
                return true;

            case OpCode.MspRel:
                if (!ValidateArgs(inst, 3)) return true;
                if (State.Render.Sprites.TryGetValue(GetVal(inst.Arguments[0]), out var mspr))
                {
                    mspr.X += GetVal(inst.Arguments[1]);
                    mspr.Y += GetVal(inst.Arguments[2]);
                }
                return true;

            case OpCode.SpZ:
                if (!ValidateArgs(inst, 2)) return true;
                if (State.Render.Sprites.TryGetValue(GetVal(inst.Arguments[0]), out var spz)) spz.Z = GetVal(inst.Arguments[1]);
                return true;

            case OpCode.SpAlpha:
                if (!ValidateArgs(inst, 2)) return true;
                if (State.Render.Sprites.TryGetValue(GetVal(inst.Arguments[0]), out var spa)) spa.Opacity = GetVal(inst.Arguments[1]) / 255.0f;
                return true;

            case OpCode.SpScale:
                if (!ValidateArgs(inst, 3)) return true;
                if (State.Render.Sprites.TryGetValue(GetVal(inst.Arguments[0]), out var spsc))
                {
                    spsc.ScaleX = GetFloat(inst.Arguments[1], inst);
                    spsc.ScaleY = GetFloat(inst.Arguments[2], inst);
                }
                return true;

            case OpCode.SpFontsize:
                if (!ValidateArgs(inst, 2)) return true;
                if (State.Render.Sprites.TryGetValue(GetVal(inst.Arguments[0]), out var spf)) spf.FontSize = GetVal(inst.Arguments[1]);
                return true;

            case OpCode.SpColor:
                if (!ValidateArgs(inst, 2)) return true;
                if (State.Render.Sprites.TryGetValue(GetVal(inst.Arguments[0]), out var spc)) spc.Color = GetString(inst.Arguments[1]);
                return true;

            case OpCode.SpFill:
                if (!ValidateArgs(inst, 3)) return true;
                if (State.Render.Sprites.TryGetValue(GetVal(inst.Arguments[0]), out var spfl))
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
                    State.Render.Sprites[0] = CreateBackgroundSprite(bgPath, tone.TimeOfDay, tone.Preset);
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
                State.Render.BackgroundTimeOfDay = NormalizeBackgroundTime(GetVal(inst.Arguments[0]));
                State.Render.BackgroundTimePreset = inst.Arguments.Count > 1 ? GetString(inst.Arguments[1]) : "";
                return true;

            case OpCode.BgTimeMap:
                if (!ValidateArgs(inst, 2)) return true;
                {
                    string key = NormalizeBackgroundMapKey(GetString(inst.Arguments[0]));
                    State.Render.BackgroundTimeMap[key] = new BackgroundTimeMapping
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
                ExecutePrint(inst);
                return true;
            case OpCode.Effect:
                ExecuteEffect(inst);
                return true;

            case OpCode.Quake:
                {
                    int amp = inst.Arguments.Count > 0 ? GetVal(inst.Arguments[0]) : 5;
                    int time = inst.Arguments.Count > 1 ? GetVal(inst.Arguments[1]) : 500;
                    State.Render.QuakeAmplitude = amp;
                    State.Render.QuakeTimerMs = time;
                }
                return true;

            case OpCode.Clr:
                State.Render.Sprites.Clear();
                State.Interaction.SpriteButtonMap.Clear();
                State.Interaction.FocusedButtonId = -1;
                ClearCompatUiSprites();
                State.TextWindow.TextboxBackgroundSpriteId = -1;
                State.Execution.SpriteLifetimeStacks.Clear();
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
        // If this sprite is owned, ensure a lifetime tracking scope exists and record it
        if (isOwned)
        {
            if (State.Execution.SpriteLifetimeStacks.Count == 0)
            {
                State.Execution.SpriteLifetimeStacks.Push(new HashSet<int>());
            }
            if (State.Execution.SpriteLifetimeStacks.Count > 0)
            {
                State.Execution.SpriteLifetimeStacks.Peek().Add(spriteId);
            }
            // Annotate ownership on the actual sprite for later cleanup decisions
            if (State.Render.Sprites.TryGetValue(spriteId, out var sp))
            {
                sp.OwnershipMode = AriaEngine.Core.OwnershipMode.Owned;
                sp.OwnerScopeId = arg ?? string.Empty;
            }
        }
        else
        {
            // Not owned by current scope; ensure ownership state remains Unowned unless explicitly set elsewhere
            if (State.Render.Sprites.TryGetValue(spriteId, out var spNonOwned))
            {
            if (spNonOwned.OwnershipMode == AriaEngine.Core.OwnershipMode.Unowned)
            {
                spNonOwned.OwnerScopeId = string.Empty;
            }
            }
        }
    }

    private Sprite CreateBackgroundSprite(string bgPath, int timeOfDay = 0, string preset = "")
    {
        if (bgPath.StartsWith("#"))
        {
            return new Sprite { Id = 0, Type = SpriteType.Rect, FillColor = bgPath, FillAlpha = 255, Width = State.EngineSettings.WindowWidth, Height = State.EngineSettings.WindowHeight, Z = 0, BackgroundTimeOfDay = timeOfDay, BackgroundTimePreset = preset };
        }

        // Check if the background image asset exists; if not, use a solid black fallback
        if (!BackgroundAssetExists(bgPath))
        {
            Reporter.Report(new AriaError(
                $"背景画像が見つかりません: '{bgPath}' - 代替として黒背景を表示します。",
                -1,
                CurrentScriptFile,
                AriaErrorLevel.Warning,
                "BG_ASSET_MISSING"));

            return new Sprite { Id = 0, Type = SpriteType.Rect, FillColor = "#000000", FillAlpha = 255, Width = State.EngineSettings.WindowWidth, Height = State.EngineSettings.WindowHeight, Z = 0, BackgroundTimeOfDay = timeOfDay, BackgroundTimePreset = preset };
        }

        return new Sprite { Id = 0, Type = SpriteType.Image, ImagePath = bgPath, Width = State.EngineSettings.WindowWidth, Height = State.EngineSettings.WindowHeight, Z = 0, BackgroundTimeOfDay = timeOfDay, BackgroundTimePreset = preset };
    }

    private static bool BackgroundAssetExists(string path)
    {
        // Mirror DiskAssetProvider resolution logic:
        // 1. Check if path is rooted and exists directly
        // 2. Check relative to current directory
        // 3. Check under assets/ subdirectory
        if (Path.IsPathRooted(path))
        {
            return File.Exists(path);
        }

        string direct = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, path));
        if (File.Exists(direct)) return true;

        string prefixed = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "assets", path));
        return File.Exists(prefixed);
    }

    private void StartBackgroundFade(string bgPath, int durationMs, int timeOfDay = 0, string preset = "")
    {
        const int overlayId = 8999;
        int half = Math.Max(1, durationMs / 2);
        State.Render.Sprites[overlayId] = new Sprite
        {
            Id = overlayId,
            Type = SpriteType.Rect,
            X = 0,
            Y = 0,
            Width = State.EngineSettings.WindowWidth,
            Height = State.EngineSettings.WindowHeight,
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
                state.Render.Sprites[0] = CreateBackgroundSprite(bgPath, timeOfDay, preset);
                if (!state.Render.Sprites.TryGetValue(overlayId, out var overlay)) return;
                overlay.Opacity = 1f;
                Tweens.Add(new AriaEngine.Rendering.Tween
                {
                    SpriteId = overlayId,
                    Property = AriaEngine.Rendering.TweenProperty.Opacity,
                    From = 1f,
                    To = 0f,
                    DurationMs = half,
                    Ease = AriaEngine.Rendering.EaseType.EaseInOut,
                    OnComplete = (innerState, __) => innerState.Render.Sprites.Remove(overlayId)
                });
            }
        });
        State.Execution.State = VmState.WaitingForAnimation;
    }

    private static int NormalizeFadeDuration(int value)
    {
        if (value <= 0) return 0;
        return value <= 20 ? 700 : value;
    }

    private void ExecuteEffect(Instruction inst)
    {
        if (!ValidateArgs(inst, 1)) return;

        int id = GetVal(inst.Arguments[0]);
        int durationMs = inst.Arguments.Count > 1 ? Math.Max(0, GetVal(inst.Arguments[1])) : 700;
        string method = inst.Arguments.Count > 2 ? GetString(inst.Arguments[2]) : "fade";

        State.Render.NscrEffects[id] = new NscrEffectDefinition
        {
            DurationMs = durationMs,
            Transition = MapNscrEffectMethod(method),
            Method = method
        };
    }

    private void ExecutePrint(Instruction inst)
    {
        int id = inst.Arguments.Count > 0 ? GetVal(inst.Arguments[0]) : 0;
        if (id == 0)
        {
            State.Render.ActiveEffects.Add("nscr:print:0");
            return;
        }

        if (!State.Render.NscrEffects.TryGetValue(id, out var effect))
        {
            effect = new NscrEffectDefinition();
        }

        State.Render.TransitionStyle = effect.Transition;
        State.Render.FadeDurationMs = Math.Max(1, effect.DurationMs);
        State.Render.FadeProgress = 0f;
        State.Render.IsFading = true;
        State.Execution.State = VmState.FadingIn;
        State.Render.ActiveEffects.Add($"nscr:print:{id}");
    }

    private static TransitionType MapNscrEffectMethod(string method)
    {
        string normalized = method.Trim().ToLowerInvariant();
        if (int.TryParse(normalized, out int numeric))
        {
            return numeric switch
            {
                11 => TransitionType.SlideLeft,
                12 => TransitionType.SlideRight,
                13 => TransitionType.SlideUp,
                14 => TransitionType.SlideDown,
                18 or 99 => TransitionType.WipeCircle,
                _ => TransitionType.Fade
            };
        }

        return normalized switch
        {
            "slide_left" or "slideleft" or "left" => TransitionType.SlideLeft,
            "slide_right" or "slideright" or "right" => TransitionType.SlideRight,
            "slide_up" or "slideup" or "up" => TransitionType.SlideUp,
            "slide_down" or "slidedown" or "down" => TransitionType.SlideDown,
            "wipe" or "circle" or "wipe_circle" or "mask" => TransitionType.WipeCircle,
            _ => TransitionType.Fade
        };
    }

    private void ExecuteTransition(Instruction inst)
    {
        if (!ValidateArgs(inst, 4)) return;
        string target = GetString(inst.Arguments[0]).ToLowerInvariant();
        string path = GetString(inst.Arguments[1]);
        string style = GetString(inst.Arguments[2]).ToLowerInvariant();
        int duration = Math.Max(0, GetVal(inst.Arguments[3]));
        if (target != "bg") return;

        State.Render.TransitionStyle = style switch
        {
            "fade" or "crossfade" => TransitionType.Fade,
            "slide_left" or "slideleft" => TransitionType.SlideLeft,
            "slide_right" or "slideright" => TransitionType.SlideRight,
            "slide_up" or "slideup" => TransitionType.SlideUp,
            "slide_down" or "slidedown" => TransitionType.SlideDown,
            "wipe" or "circle" or "wipe_circle" => TransitionType.WipeCircle,
            "white" or "flash" => TransitionType.Fade,
            _ => TransitionType.Fade
        };

        if (style is "white" or "flash")
        {
            StartScreenPulse("#ffffff", 0.92f, Math.Min(duration, 260));
        }

        StartBackgroundFade(path, duration);
        State.Render.ActiveEffects.Add($"transition:bg:{style}");
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
        if (State.Render.BackgroundTimeMap.TryGetValue(key, out var mapped))
        {
            return (NormalizeBackgroundTime(mapped.TimeOfDay), mapped.Preset);
        }

        return (NormalizeBackgroundTime(State.Render.BackgroundTimeOfDay), State.Render.BackgroundTimePreset);
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
                State.Render.QuakeAmplitude = inst.Arguments.Count > 1 ? GetVal(inst.Arguments[1]) : 6;
                State.Render.QuakeTimerMs = inst.Arguments.Count > 2 ? GetVal(inst.Arguments[2]) : 300;
                State.Render.ActiveEffects.Add("camera:shake");
                break;
            case "pan":
                State.Render.CameraOffsetX = inst.Arguments.Count > 1 ? GetVal(inst.Arguments[1]) : 0;
                State.Render.CameraOffsetY = inst.Arguments.Count > 2 ? GetVal(inst.Arguments[2]) : 0;
                State.Render.ActiveEffects.Add("camera:pan");
                break;
            case "zoom":
                State.Render.CameraZoom = inst.Arguments.Count > 1 ? GetFloat(inst.Arguments[1], inst, 1f) : 1f;
                State.Render.ActiveEffects.Add("camera:zoom");
                break;
            case "reset":
                State.Render.CameraOffsetX = 0;
                State.Render.CameraOffsetY = 0;
                State.Render.CameraZoom = 1f;
                State.Render.QuakeAmplitude = 0;
                State.Render.QuakeTimerMs = 0;
                State.Render.ActiveEffects.RemoveAll(e => e.StartsWith("camera:", StringComparison.OrdinalIgnoreCase));
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
                State.Render.ScreenTintColor = inst.Arguments.Count > 1 ? GetString(inst.Arguments[1]) : "#1d2430";
                State.Render.ScreenTintOpacity = inst.Arguments.Count > 2 ? Math.Clamp(GetVal(inst.Arguments[2]) / 255f, 0f, 1f) : 0.35f;
                State.Render.ScreenTintTimerMs = inst.Arguments.Count > 3 ? Math.Max(0, GetVal(inst.Arguments[3])) : 0;
                State.Render.ActiveEffects.Add("screen:tint");
                break;
            case "clear":
            case "reset":
                State.Render.ScreenTintOpacity = 0f;
                State.Render.ScreenTintTimerMs = 0f;
                State.Render.ActiveEffects.RemoveAll(e => e.StartsWith("screen:", StringComparison.OrdinalIgnoreCase));
                break;
            case "vignette":
                State.Render.VignetteStrength = inst.Arguments.Count > 1
                    ? Math.Clamp(GetVal(inst.Arguments[1]) / 255f, 0f, 1f)
                    : 0.5f;
                break;
            case "particle":
                Vm.Particles.Start(AriaEngine.Rendering.ParticleSystem.ParticleType.Rain,
                    State.EngineSettings.WindowWidth, State.EngineSettings.WindowHeight);
                break;
            case "particle_snow":
                Vm.Particles.Start(AriaEngine.Rendering.ParticleSystem.ParticleType.Snow,
                    State.EngineSettings.WindowWidth, State.EngineSettings.WindowHeight);
                break;
            case "particle_sakura":
                Vm.Particles.Start(AriaEngine.Rendering.ParticleSystem.ParticleType.Sakura,
                    State.EngineSettings.WindowWidth, State.EngineSettings.WindowHeight);
                break;
            case "particle_stop":
                Vm.Particles.Stop();
                break;
        }
    }

    private void ExecuteTextFx(Instruction inst)
    {
        if (!ValidateArgs(inst, 1)) return;
        string name = GetString(inst.Arguments[0]).ToLowerInvariant();
        if (name == "reset")
        {
            State.TextRuntime.DefaultTextEffect = "none";
            State.TextRuntime.DefaultTextEffectStrength = 0f;
            State.TextRuntime.DefaultTextEffectSpeed = 0f;
            State.TextRuntime.TextSpeedMs = Config.Config.GlobalTextSpeedMs > 0 ? Config.Config.GlobalTextSpeedMs : Config.Config.DefaultTextSpeedMs;
            State.Render.ActiveEffects.RemoveAll(e => e.StartsWith("textfx:", StringComparison.OrdinalIgnoreCase));
            return;
        }

        if (name is "speed" or "type")
        {
            if (inst.Arguments.Count > 1) State.TextRuntime.TextSpeedMs = Math.Max(0, GetVal(inst.Arguments[1]));
            State.Render.ActiveEffects.Add("textfx:speed");
            return;
        }

        State.TextRuntime.DefaultTextEffect = name;
        if (inst.Arguments.Count > 1) State.TextRuntime.DefaultTextEffectStrength = GetFloat(inst.Arguments[1], inst, State.TextRuntime.DefaultTextEffectStrength);
        if (inst.Arguments.Count > 2) State.TextRuntime.DefaultTextEffectSpeed = GetFloat(inst.Arguments[2], inst, State.TextRuntime.DefaultTextEffectSpeed);
        State.Render.ActiveEffects.Add($"textfx:{name}");
    }

    private void ExecuteFx(Instruction inst)
    {
        if (!ValidateArgs(inst, 1)) return;
        string action = GetString(inst.Arguments[0]).ToLowerInvariant();
        switch (action)
        {
            case "profile":
                if (inst.Arguments.Count > 1) State.Render.FxProfile = GetString(inst.Arguments[1]).ToLowerInvariant();
                break;
            case "skip_policy":
                if (inst.Arguments.Count > 1) State.Render.FxSkipPolicy = GetString(inst.Arguments[1]).ToLowerInvariant();
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
            State.Execution.State = VmState.WaitingForAnimation;
        }
    }

    private void StartScreenPulse(string color, float opacity, int durationMs)
    {
        State.Render.ScreenTintColor = color;
        State.Render.ScreenTintOpacity = Math.Clamp(opacity, 0f, 1f);
        State.Render.ScreenTintTimerMs = Math.Max(1, durationMs);
        State.Render.ActiveEffects.Add("screen:flash");
    }

    private void CancelFx(string layer)
    {
        if (layer is "all" or "screen")
        {
            State.Render.ScreenTintOpacity = 0f;
            State.Render.ScreenTintTimerMs = 0f;
            State.Render.ActiveEffects.RemoveAll(e => e.StartsWith("screen:", StringComparison.OrdinalIgnoreCase));
        }
        if (layer is "all" or "camera")
        {
            State.Render.CameraOffsetX = 0;
            State.Render.CameraOffsetY = 0;
            State.Render.CameraZoom = 1f;
            State.Render.QuakeAmplitude = 0;
            State.Render.QuakeTimerMs = 0;
            State.Render.ActiveEffects.RemoveAll(e => e.StartsWith("camera:", StringComparison.OrdinalIgnoreCase));
        }
        if (layer is "all" or "text" or "textfx")
        {
            State.TextRuntime.DefaultTextEffect = "none";
            State.Render.ActiveEffects.RemoveAll(e => e.StartsWith("textfx:", StringComparison.OrdinalIgnoreCase));
        }
        if (layer == "all")
        {
            State.Render.ActiveEffects.Clear();
        }
    }
}
