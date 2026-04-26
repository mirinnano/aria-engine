namespace AriaEngine.Core.Commands;

public sealed class AudioCommandHandler : BaseCommandHandler
{
    public override IReadOnlySet<OpCode> HandledCodes { get; } = new HashSet<OpCode>
    {
        OpCode.PlayBgm,
        OpCode.StopBgm,
        OpCode.PlaySe,
        OpCode.Dwave,
        OpCode.BgmVol,
        OpCode.SeVol,
        OpCode.BgmFade
    };

    public AudioCommandHandler(VirtualMachine vm) : base(vm)
    {
    }

    public override bool Execute(Instruction inst)
    {
        switch (inst.Op)
        {
            case OpCode.PlayBgm:
                if (!ValidateArgs(inst, 1)) return true;
                State.CurrentBgm = GetString(inst.Arguments[0]);
                State.BgmFadeOutDurationMs = 0f;
                State.BgmFadeOutTimerMs = 0f;
                return true;

            case OpCode.StopBgm:
                State.CurrentBgm = "";
                State.BgmFadeOutDurationMs = 0f;
                State.BgmFadeOutTimerMs = 0f;
                return true;

            case OpCode.PlaySe:
            case OpCode.Dwave:
                if (!ValidateArgs(inst, 1)) return true;
                State.PendingSe.Add(inst.Arguments.Count > 1 ? GetString(inst.Arguments[1]) : GetString(inst.Arguments[0]));
                return true;

            case OpCode.BgmVol:
                if (!ValidateArgs(inst, 1)) return true;
                State.BgmVolume = GetVal(inst.Arguments[0]);
                return true;

            case OpCode.SeVol:
                if (!ValidateArgs(inst, 1)) return true;
                State.SeVolume = GetVal(inst.Arguments[0]);
                return true;

            case OpCode.BgmFade:
                {
                    int durationMs = inst.Arguments.Count > 0 ? Math.Max(0, GetVal(inst.Arguments[0])) : 500;
                    if (string.IsNullOrEmpty(State.CurrentBgm) || durationMs == 0)
                    {
                        State.CurrentBgm = "";
                        State.BgmFadeOutDurationMs = 0f;
                        State.BgmFadeOutTimerMs = 0f;
                    }
                    else
                    {
                        State.BgmFadeOutDurationMs = durationMs;
                        State.BgmFadeOutTimerMs = durationMs;
                    }
                }
                return true;

            default:
                return false;
        }
    }
}
