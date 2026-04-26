using AriaEngine.Rendering;

namespace AriaEngine.Core.Commands;

public sealed class TweenCommandHandler : BaseCommandHandler
{
    public override IReadOnlySet<OpCode> HandledCodes { get; } = new HashSet<OpCode>
    {
        OpCode.Amsp,
        OpCode.Afade,
        OpCode.Ascale,
        OpCode.Await,
        OpCode.Ease
    };

    public TweenCommandHandler(VirtualMachine vm) : base(vm)
    {
    }

    public override bool Execute(Instruction inst)
    {
        switch (inst.Op)
        {
            case OpCode.Amsp:
                if (!ValidateArgs(inst, 4)) return true;
                {
                    int tweenId = GetVal(inst.Arguments[0]);
                    float toX = GetVal(inst.Arguments[1]);
                    float toY = GetVal(inst.Arguments[2]);
                    float dur = GetVal(inst.Arguments[3]);
                    if (State.Sprites.TryGetValue(tweenId, out var sp))
                    {
                        Tweens.Add(new Tween { SpriteId = sp.Id, Property = "x", From = sp.X, To = toX, DurationMs = dur, Ease = Tweens.CurrentEaseType });
                        Tweens.Add(new Tween { SpriteId = sp.Id, Property = "y", From = sp.Y, To = toY, DurationMs = dur, Ease = Tweens.CurrentEaseType });
                    }
                }
                return true;

            case OpCode.Afade:
                if (!ValidateArgs(inst, 3)) return true;
                if (State.Sprites.TryGetValue(GetVal(inst.Arguments[0]), out var fadeSprite))
                {
                    Tweens.Add(new Tween { SpriteId = fadeSprite.Id, Property = "opacity", From = fadeSprite.Opacity, To = GetVal(inst.Arguments[1]) / 255f, DurationMs = GetVal(inst.Arguments[2]), Ease = Tweens.CurrentEaseType });
                }
                return true;

            case OpCode.Ascale:
                if (!ValidateArgs(inst, 4)) return true;
                if (State.Sprites.TryGetValue(GetVal(inst.Arguments[0]), out var scaleSprite))
                {
                    Tweens.Add(new Tween { SpriteId = scaleSprite.Id, Property = "scaleX", From = scaleSprite.ScaleX, To = GetFloat(inst.Arguments[1], inst), DurationMs = GetVal(inst.Arguments[3]), Ease = Tweens.CurrentEaseType });
                    Tweens.Add(new Tween { SpriteId = scaleSprite.Id, Property = "scaleY", From = scaleSprite.ScaleY, To = GetFloat(inst.Arguments[2], inst), DurationMs = GetVal(inst.Arguments[3]), Ease = Tweens.CurrentEaseType });
                }
                return true;

            case OpCode.Await:
                State.State = VmState.WaitingForAnimation;
                return true;

            case OpCode.Ease:
                if (!ValidateArgs(inst, 1)) return true;
                string easeName = GetString(inst.Arguments[0]).ToLowerInvariant();
                Tweens.CurrentEaseType = easeName switch
                {
                    "easein" => EaseType.EaseIn,
                    "easeout" => EaseType.EaseOut,
                    "easeinout" => EaseType.EaseInOut,
                    _ => EaseType.Linear
                };
                return true;

            default:
                return false;
        }
    }
}
