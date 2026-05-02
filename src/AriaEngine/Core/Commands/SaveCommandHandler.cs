namespace AriaEngine.Core.Commands;

public sealed class SaveCommandHandler : BaseCommandHandler
{
    public override IReadOnlySet<OpCode> HandledCodes { get; } = new HashSet<OpCode>
    {
        OpCode.SaveOn,
        OpCode.SaveOff,
        OpCode.Save,
        OpCode.Load,
        OpCode.SaveInfo
    };

    public SaveCommandHandler(VirtualMachine vm) : base(vm)
    {
    }

    public override bool Execute(Instruction inst)
    {
        switch (inst.Op)
        {
            case OpCode.SaveOn:
                State.MenuRuntime.SaveMode = true;
                return true;

            case OpCode.SaveOff:
                State.MenuRuntime.SaveMode = false;
                return true;

            case OpCode.Save:
                if (!ValidateArgs(inst, 1)) return true;
                if (State.MenuRuntime.SaveMode) Vm.SaveGame(GetVal(inst.Arguments[0]));
                return true;

            case OpCode.Load:
                if (!ValidateArgs(inst, 1)) return true;
                Vm.LoadGame(GetVal(inst.Arguments[0]));
                return true;

            case OpCode.SaveInfo:
                if (!ValidateArgs(inst, 4)) return true;
                {
                    int sSlot = GetVal(inst.Arguments[0]);
                    bool sExists = Vm.Saves.HasSaveData(sSlot);
                    var sData = Vm.Saves.GetSaveData(sSlot);
                    string sPreview = "";
                    string sDateTime = "";
                    if (sExists && sData != null)
                    {
                        sPreview = sData.PreviewText ?? "";
                        sDateTime = sData.SaveTime.ToString("yyyy/MM/dd HH:mm");
                    }
                    SetStr(GetString(inst.Arguments[1]), sPreview);
                    SetStr(GetString(inst.Arguments[2]), sDateTime);
                    SetReg(GetString(inst.Arguments[3]), sExists ? 1 : 0);
                }
                return true;

            default:
                return false;
        }
    }
}
