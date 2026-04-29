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
                        State.Sprites.Clear();
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
                    State.Sprites[0] = bgPath.StartsWith("#")
                        ? new Sprite { Id = 0, Type = SpriteType.Rect, FillColor = bgPath, FillAlpha = 255, Width = State.WindowWidth, Height = State.WindowHeight, Z = 0 }
                        : new Sprite { Id = 0, Type = SpriteType.Image, ImagePath = bgPath, Z = 0 };
                }
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
}
