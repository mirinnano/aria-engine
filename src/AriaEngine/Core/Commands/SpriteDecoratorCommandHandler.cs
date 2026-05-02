namespace AriaEngine.Core.Commands;

public sealed class SpriteDecoratorCommandHandler : BaseCommandHandler
{
    public override IReadOnlySet<OpCode> HandledCodes { get; } = new HashSet<OpCode>
    {
        OpCode.SpRound,
        OpCode.SpBorder,
        OpCode.SpGradient,
        OpCode.SpShadow,
        OpCode.SpTextShadow,
        OpCode.SpTextOutline,
        OpCode.SpTextAlign,
        OpCode.SpTextVAlign,
        OpCode.SpRotation,
        OpCode.SpHoverColor,
        OpCode.SpHoverScale,
        OpCode.SpCursor
    };

    public SpriteDecoratorCommandHandler(VirtualMachine vm) : base(vm)
    {
    }

    public override bool Execute(Instruction inst)
    {
        switch (inst.Op)
        {
            case OpCode.SpRound:
                if (!ValidateArgs(inst, 2)) return true;
                if (State.Render.Sprites.TryGetValue(GetVal(inst.Arguments[0]), out var spr)) spr.CornerRadius = GetVal(inst.Arguments[1]);
                return true;

            case OpCode.SpBorder:
                if (!ValidateArgs(inst, 3)) return true;
                if (State.Render.Sprites.TryGetValue(GetVal(inst.Arguments[0]), out var spbr))
                {
                    spbr.BorderColor = GetString(inst.Arguments[1]);
                    spbr.BorderWidth = GetVal(inst.Arguments[2]);
                }
                return true;

            case OpCode.SpGradient:
                if (!ValidateArgs(inst, 4)) return true;
                if (State.Render.Sprites.TryGetValue(GetVal(inst.Arguments[0]), out var spg))
                {
                    spg.GradientTo = GetString(inst.Arguments[2]);
                    spg.GradientDirection = GetString(inst.Arguments[3]);
                }
                return true;

            case OpCode.SpShadow:
                if (!ValidateArgs(inst, 5)) return true;
                if (State.Render.Sprites.TryGetValue(GetVal(inst.Arguments[0]), out var spsh))
                {
                    spsh.ShadowOffsetX = GetVal(inst.Arguments[1]);
                    spsh.ShadowOffsetY = GetVal(inst.Arguments[2]);
                    spsh.ShadowColor = GetString(inst.Arguments[3]);
                    spsh.ShadowAlpha = GetVal(inst.Arguments[4]);
                }
                return true;

            case OpCode.SpTextShadow:
                if (!ValidateArgs(inst, 4)) return true;
                if (State.Render.Sprites.TryGetValue(GetVal(inst.Arguments[0]), out var spts))
                {
                    spts.TextShadowX = GetVal(inst.Arguments[1]);
                    spts.TextShadowY = GetVal(inst.Arguments[2]);
                    spts.TextShadowColor = GetString(inst.Arguments[3]);
                }
                return true;

            case OpCode.SpTextOutline:
                if (!ValidateArgs(inst, 3)) return true;
                if (State.Render.Sprites.TryGetValue(GetVal(inst.Arguments[0]), out var spto))
                {
                    spto.TextOutlineSize = GetVal(inst.Arguments[1]);
                    spto.TextOutlineColor = GetString(inst.Arguments[2]);
                }
                return true;

            case OpCode.SpTextAlign:
                if (!ValidateArgs(inst, 2)) return true;
                if (State.Render.Sprites.TryGetValue(GetVal(inst.Arguments[0]), out var spta))
                    { var align = GetString(inst.Arguments[1]); if (Enum.TryParse<TextAlignment>(align, ignoreCase: true, out var val)) spta.TextAlign = val; }
                return true;

            case OpCode.SpTextVAlign:
                if (!ValidateArgs(inst, 2)) return true;
                if (State.Render.Sprites.TryGetValue(GetVal(inst.Arguments[0]), out var sptva))
                    { var valign = GetString(inst.Arguments[1]); if (Enum.TryParse<TextVerticalAlignment>(valign, ignoreCase: true, out var val)) sptva.TextVAlign = val; }
                return true;

            case OpCode.SpRotation:
                if (!ValidateArgs(inst, 2)) return true;
                if (State.Render.Sprites.TryGetValue(GetVal(inst.Arguments[0]), out var sprot)) sprot.Rotation = GetVal(inst.Arguments[1]);
                return true;

            case OpCode.SpHoverColor:
                if (!ValidateArgs(inst, 2)) return true;
                if (State.Render.Sprites.TryGetValue(GetVal(inst.Arguments[0]), out var sphc)) sphc.HoverFillColor = GetString(inst.Arguments[1]);
                return true;

            case OpCode.SpHoverScale:
                if (!ValidateArgs(inst, 2)) return true;
                if (State.Render.Sprites.TryGetValue(GetVal(inst.Arguments[0]), out var sphs)) sphs.HoverScale = GetFloat(inst.Arguments[1], inst, 1.0f);
                return true;

            case OpCode.SpCursor:
                if (!ValidateArgs(inst, 2)) return true;
                if (State.Render.Sprites.TryGetValue(GetVal(inst.Arguments[0]), out var spc)) spc.Cursor = GetString(inst.Arguments[1]);
                return true;

            default:
                return false;
        }
    }
}
