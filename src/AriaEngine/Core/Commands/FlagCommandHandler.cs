namespace AriaEngine.Core.Commands;

public sealed class FlagCommandHandler : BaseCommandHandler
{
    public override IReadOnlySet<OpCode> HandledCodes { get; } = new HashSet<OpCode>
    {
        OpCode.SetFlag,
        OpCode.GetFlag,
        OpCode.ClearFlag,
        OpCode.ToggleFlag,
        OpCode.SetPFlag,
        OpCode.GetPFlag,
        OpCode.ClearPFlag,
        OpCode.TogglePFlag,
        OpCode.SetSFlag,
        OpCode.GetSFlag,
        OpCode.ClearSFlag,
        OpCode.ToggleSFlag,
        OpCode.SetVFlag,
        OpCode.GetVFlag,
        OpCode.ClearVFlag,
        OpCode.ToggleVFlag,
        OpCode.IncCounter,
        OpCode.DecCounter,
        OpCode.SetCounter,
        OpCode.GetCounter
    };

    public FlagCommandHandler(VirtualMachine vm) : base(vm)
    {
    }

    public override bool Execute(Instruction inst)
    {
        switch (inst.Op)
        {
            case OpCode.SetFlag:
                if (!ValidateArgs(inst, 2)) return true;
                State.Flags[inst.Arguments[0]] = GetVal(inst.Arguments[1]) != 0;
                MarkPersistentDirty();
                return true;

            case OpCode.SetPFlag:
                if (!ValidateArgs(inst, 2)) return true;
                State.SaveFlags[inst.Arguments[0]] = GetVal(inst.Arguments[1]) != 0;
                MarkPersistentDirty();
                return true;

            case OpCode.GetFlag:
                if (!ValidateArgs(inst, 1)) return true;
                State.Flags.TryGetValue(inst.Arguments[0], out bool flag);
                SetReg(VirtualMachine.GetResultRegister(inst), flag ? 1 : 0);
                return true;

            case OpCode.GetPFlag:
                if (!ValidateArgs(inst, 1)) return true;
                State.SaveFlags.TryGetValue(inst.Arguments[0], out bool pflag);
                SetReg(VirtualMachine.GetResultRegister(inst), pflag ? 1 : 0);
                return true;

            case OpCode.ClearFlag:
                if (!ValidateArgs(inst, 1)) return true;
                State.Flags[inst.Arguments[0]] = false;
                MarkPersistentDirty();
                return true;

            case OpCode.ClearPFlag:
                if (!ValidateArgs(inst, 1)) return true;
                State.SaveFlags[inst.Arguments[0]] = false;
                MarkPersistentDirty();
                return true;

            case OpCode.ToggleFlag:
                if (!ValidateArgs(inst, 1)) return true;
                State.Flags.TryGetValue(inst.Arguments[0], out bool currentFlag);
                State.Flags[inst.Arguments[0]] = !currentFlag;
                MarkPersistentDirty();
                return true;

            case OpCode.TogglePFlag:
                if (!ValidateArgs(inst, 1)) return true;
                State.SaveFlags.TryGetValue(inst.Arguments[0], out bool currentPFlag);
                State.SaveFlags[inst.Arguments[0]] = !currentPFlag;
                MarkPersistentDirty();
                return true;

            case OpCode.SetSFlag:
                if (!ValidateArgs(inst, 2)) return true;
                State.SaveFlags[inst.Arguments[0]] = GetVal(inst.Arguments[1]) != 0;
                MarkPersistentDirty();
                return true;

            case OpCode.GetSFlag:
                if (!ValidateArgs(inst, 1)) return true;
                bool sflag = State.SaveFlags.TryGetValue(inst.Arguments[0], out bool saveValue) && saveValue;
                SetReg(VirtualMachine.GetResultRegister(inst), sflag ? 1 : 0);
                return true;

            case OpCode.ClearSFlag:
                if (!ValidateArgs(inst, 1)) return true;
                State.SaveFlags[inst.Arguments[0]] = false;
                MarkPersistentDirty();
                return true;

            case OpCode.ToggleSFlag:
                if (!ValidateArgs(inst, 1)) return true;
                bool currentSFlag = State.SaveFlags.TryGetValue(inst.Arguments[0], out bool currentSaveValue) && currentSaveValue;
                State.SaveFlags[inst.Arguments[0]] = !currentSFlag;
                MarkPersistentDirty();
                return true;

            case OpCode.SetVFlag:
                if (!ValidateArgs(inst, 2)) return true;
                State.VolatileFlags[inst.Arguments[0]] = GetVal(inst.Arguments[1]) != 0;
                return true;

            case OpCode.GetVFlag:
                if (!ValidateArgs(inst, 1)) return true;
                bool vflag = State.VolatileFlags.TryGetValue(inst.Arguments[0], out bool volatileValue) && volatileValue;
                SetReg(VirtualMachine.GetResultRegister(inst), vflag ? 1 : 0);
                return true;

            case OpCode.ClearVFlag:
                if (!ValidateArgs(inst, 1)) return true;
                State.VolatileFlags[inst.Arguments[0]] = false;
                return true;

            case OpCode.ToggleVFlag:
                if (!ValidateArgs(inst, 1)) return true;
                bool currentVFlag = State.VolatileFlags.TryGetValue(inst.Arguments[0], out bool currentVolatileValue) && currentVolatileValue;
                State.VolatileFlags[inst.Arguments[0]] = !currentVFlag;
                return true;

            case OpCode.IncCounter:
                if (!ValidateArgs(inst, 1)) return true;
                AddCounter(inst, inst.Arguments.Count > 1 ? GetVal(inst.Arguments[1]) : 1);
                return true;

            case OpCode.DecCounter:
                if (!ValidateArgs(inst, 1)) return true;
                AddCounter(inst, -(inst.Arguments.Count > 1 ? GetVal(inst.Arguments[1]) : 1));
                return true;

            case OpCode.SetCounter:
                if (!ValidateArgs(inst, 2)) return true;
                State.Counters[inst.Arguments[0]] = GetVal(inst.Arguments[1]);
                MarkPersistentDirty();
                return true;

            case OpCode.GetCounter:
                if (!ValidateArgs(inst, 1)) return true;
                int value = State.Counters.TryGetValue(inst.Arguments[0], out int counterValue) ? counterValue : 0;
                SetReg(VirtualMachine.GetResultRegister(inst), value);
                return true;

            default:
                return false;
        }
    }

    private void AddCounter(Instruction inst, int delta)
    {
        int current = State.Counters.TryGetValue(inst.Arguments[0], out int value) ? value : 0;
        State.Counters[inst.Arguments[0]] = current + delta;
        MarkPersistentDirty();
    }
}
