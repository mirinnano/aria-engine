namespace AriaEngine.Core.Commands;

public sealed class AudioCommandHandler : BaseCommandHandler
{
    public override IReadOnlySet<OpCode> HandledCodes { get; } = new HashSet<OpCode>
    {
        OpCode.PlayBgm,
        OpCode.StopBgm,
        OpCode.PlaySe,
        OpCode.PlayMp3,
        OpCode.Dwave,
        OpCode.DwaveLoop,
        OpCode.DwaveStop,
        OpCode.Voice,
        OpCode.VoiceWait,
        OpCode.VoiceStop,
        OpCode.BgmVol,
        OpCode.SeVol,
        OpCode.Mp3Vol,
        OpCode.BgmFade,
        OpCode.Mp3FadeOut
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
                State.Audio.CurrentBgm = GetString(inst.Arguments[0]);
                State.Audio.BgmFadeOutDurationMs = 0f;
                State.Audio.BgmFadeOutTimerMs = 0f;
                return true;

            case OpCode.StopBgm:
                State.Audio.CurrentBgm = "";
                State.Audio.BgmFadeOutDurationMs = 0f;
                State.Audio.BgmFadeOutTimerMs = 0f;
                return true;

            case OpCode.PlaySe:
                if (!ValidateArgs(inst, 1)) return true;
                QueueSound(inst.Arguments.Count > 1 ? GetString(inst.Arguments[1]) : GetString(inst.Arguments[0]));
                return true;

            case OpCode.Dwave:
                if (!ValidateArgs(inst, 1)) return true;
                string voicePath = inst.Arguments.Count > 1 ? GetString(inst.Arguments[1]) : GetString(inst.Arguments[0]);
                QueueSound(voicePath);
                State.Audio.LastVoicePath = voicePath;
                return true;

            case OpCode.PlayMp3:
                if (!ValidateArgs(inst, 1)) return true;
                State.Audio.CurrentBgm = GetString(inst.Arguments[0]);
                State.Audio.BgmFadeOutDurationMs = 0f;
                State.Audio.BgmFadeOutTimerMs = 0f;
                return true;

            case OpCode.DwaveLoop:
                if (!ValidateArgs(inst, 1)) return true;
                QueueSound(inst.Arguments.Count > 1 ? GetString(inst.Arguments[1]) : GetString(inst.Arguments[0]));
                return true;

            case OpCode.DwaveStop:
                State.Audio.PendingSe.Clear();
                return true;

            case OpCode.Voice:
                if (!ValidateArgs(inst, 1)) return true;
                string path = GetString(inst.Arguments[0]);
                QueueSound(path);
                State.Audio.LastVoicePath = path;
                State.Audio.VoiceWaitRequested = false;
                return true;

            case OpCode.VoiceWait:
                State.Audio.VoiceWaitRequested = true;
                State.Execution.State = VmState.WaitingForAnimation;
                return true;

            case OpCode.VoiceStop:
                State.Audio.LastVoicePath = "";
                State.Audio.VoiceWaitRequested = false;
                return true;

            case OpCode.BgmVol:
                if (!ValidateArgs(inst, 1)) return true;
                State.Audio.BgmVolume = GetVal(inst.Arguments[0]);
                return true;

            case OpCode.SeVol:
                if (!ValidateArgs(inst, 1)) return true;
                State.Audio.SeVolume = GetVal(inst.Arguments[0]);
                return true;

            case OpCode.Mp3Vol:
                if (!ValidateArgs(inst, 1)) return true;
                State.Audio.BgmVolume = GetVal(inst.Arguments[0]);
                return true;

            case OpCode.BgmFade:
                {
                    int durationMs = inst.Arguments.Count > 0 ? Math.Max(0, GetVal(inst.Arguments[0])) : 500;
                    if (string.IsNullOrEmpty(State.Audio.CurrentBgm) || durationMs == 0)
                    {
                        State.Audio.CurrentBgm = "";
                        State.Audio.BgmFadeOutDurationMs = 0f;
                        State.Audio.BgmFadeOutTimerMs = 0f;
                    }
                    else
                    {
                        State.Audio.BgmFadeOutDurationMs = durationMs;
                        State.Audio.BgmFadeOutTimerMs = durationMs;
                    }
                }
                return true;

            case OpCode.Mp3FadeOut:
                {
                    int mp3FadeMs = inst.Arguments.Count > 0 ? Math.Max(0, GetVal(inst.Arguments[0])) : 500;
                    if (string.IsNullOrEmpty(State.Audio.CurrentBgm) || mp3FadeMs == 0)
                    {
                        State.Audio.CurrentBgm = "";
                        State.Audio.BgmFadeOutDurationMs = 0f;
                        State.Audio.BgmFadeOutTimerMs = 0f;
                    }
                    else
                    {
                        State.Audio.BgmFadeOutDurationMs = mp3FadeMs;
                        State.Audio.BgmFadeOutTimerMs = mp3FadeMs;
                    }
                }
                return true;

            default:
                return false;
        }
    }

    private void QueueSound(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        State.Audio.PendingSe.Add(path);
    }
}
