using System;

namespace AriaEngine.Core.Commands;

public sealed class FlagCommandExecutor
{
    private readonly VirtualMachine _vm;

    public FlagCommandExecutor(VirtualMachine vm)
    {
        _vm = vm;
    }

    public bool Execute(Instruction inst)
    {
        switch (inst.Op)
        {
            case OpCode.SetFlag:
            case OpCode.SetPFlag:
                if (!_vm.ValidateArgs(inst, 2)) return true;
                _vm.State.Flags[inst.Arguments[0]] = _vm.GetVal(inst.Arguments[1]) != 0;
                _vm.MarkPersistentDirty();
                return true;

            case OpCode.GetFlag:
            case OpCode.GetPFlag:
                if (!_vm.ValidateArgs(inst, 1)) return true;
                _vm.State.Flags.TryGetValue(inst.Arguments[0], out bool pflag);
                _vm.SetReg(VirtualMachine.GetResultRegister(inst), pflag ? 1 : 0);
                return true;

            case OpCode.ClearFlag:
            case OpCode.ClearPFlag:
                if (!_vm.ValidateArgs(inst, 1)) return true;
                _vm.State.Flags[inst.Arguments[0]] = false;
                _vm.MarkPersistentDirty();
                return true;

            case OpCode.ToggleFlag:
            case OpCode.TogglePFlag:
                if (!_vm.ValidateArgs(inst, 1)) return true;
                _vm.State.Flags.TryGetValue(inst.Arguments[0], out bool currentPFlag);
                _vm.State.Flags[inst.Arguments[0]] = !currentPFlag;
                _vm.MarkPersistentDirty();
                return true;

            case OpCode.SetSFlag:
                if (!_vm.ValidateArgs(inst, 2)) return true;
                _vm.State.SaveFlags[inst.Arguments[0]] = _vm.GetVal(inst.Arguments[1]) != 0;
                return true;

            case OpCode.GetSFlag:
                if (!_vm.ValidateArgs(inst, 1)) return true;
                bool sflag = _vm.State.SaveFlags.TryGetValue(inst.Arguments[0], out bool saveValue) && saveValue;
                _vm.SetReg(VirtualMachine.GetResultRegister(inst), sflag ? 1 : 0);
                return true;

            case OpCode.ClearSFlag:
                if (!_vm.ValidateArgs(inst, 1)) return true;
                _vm.State.SaveFlags[inst.Arguments[0]] = false;
                return true;

            case OpCode.ToggleSFlag:
                if (!_vm.ValidateArgs(inst, 1)) return true;
                bool currentSFlag = _vm.State.SaveFlags.TryGetValue(inst.Arguments[0], out bool currentSaveValue) && currentSaveValue;
                _vm.State.SaveFlags[inst.Arguments[0]] = !currentSFlag;
                return true;

            case OpCode.SetVFlag:
                if (!_vm.ValidateArgs(inst, 2)) return true;
                _vm.State.VolatileFlags[inst.Arguments[0]] = _vm.GetVal(inst.Arguments[1]) != 0;
                return true;

            case OpCode.GetVFlag:
                if (!_vm.ValidateArgs(inst, 1)) return true;
                bool vflag = _vm.State.VolatileFlags.TryGetValue(inst.Arguments[0], out bool volatileValue) && volatileValue;
                _vm.SetReg(VirtualMachine.GetResultRegister(inst), vflag ? 1 : 0);
                return true;

            case OpCode.ClearVFlag:
                if (!_vm.ValidateArgs(inst, 1)) return true;
                _vm.State.VolatileFlags[inst.Arguments[0]] = false;
                return true;

            case OpCode.ToggleVFlag:
                if (!_vm.ValidateArgs(inst, 1)) return true;
                bool currentVFlag = _vm.State.VolatileFlags.TryGetValue(inst.Arguments[0], out bool currentVolatileValue) && currentVolatileValue;
                _vm.State.VolatileFlags[inst.Arguments[0]] = !currentVFlag;
                return true;

            case OpCode.IncCounter:
                if (!_vm.ValidateArgs(inst, 1)) return true;
                AddCounter(inst, inst.Arguments.Count > 1 ? _vm.GetVal(inst.Arguments[1]) : 1);
                return true;

            case OpCode.DecCounter:
                if (!_vm.ValidateArgs(inst, 1)) return true;
                AddCounter(inst, -(inst.Arguments.Count > 1 ? _vm.GetVal(inst.Arguments[1]) : 1));
                return true;

            case OpCode.SetCounter:
                if (!_vm.ValidateArgs(inst, 2)) return true;
                _vm.State.Counters[inst.Arguments[0]] = _vm.GetVal(inst.Arguments[1]);
                _vm.MarkPersistentDirty();
                return true;

            case OpCode.GetCounter:
                if (!_vm.ValidateArgs(inst, 1)) return true;
                int value = _vm.State.Counters.TryGetValue(inst.Arguments[0], out int counterValue) ? counterValue : 0;
                _vm.SetReg(VirtualMachine.GetResultRegister(inst), value);
                return true;

            default:
                return false;
        }
    }

    private void AddCounter(Instruction inst, int delta)
    {
        int current = _vm.State.Counters.TryGetValue(inst.Arguments[0], out int value) ? value : 0;
        _vm.State.Counters[inst.Arguments[0]] = current + delta;
        _vm.MarkPersistentDirty();
    }
}
