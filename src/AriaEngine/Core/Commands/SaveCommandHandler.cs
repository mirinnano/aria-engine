namespace AriaEngine.Core.Commands;

public sealed class SaveCommandHandler : BaseCommandHandler
{
    public override IReadOnlySet<OpCode> HandledCodes { get; } = new HashSet<OpCode>
    {
        OpCode.SaveOn,
        OpCode.SaveOff,
        OpCode.Save,
        OpCode.Load
    };

    public SaveCommandHandler(VirtualMachine vm) : base(vm)
    {
    }

    public override bool Execute(Instruction inst)
    {
        switch (inst.Op)
        {
            case OpCode.SaveOn:
                State.SaveMode = true;
                return true;

            case OpCode.SaveOff:
                State.SaveMode = false;
                return true;

            case OpCode.Save:
                if (!ValidateArgs(inst, 1)) return true;
                if (State.SaveMode) Vm.SaveGame(GetVal(inst.Arguments[0]));
                return true;

            case OpCode.Load:
                if (!ValidateArgs(inst, 1)) return true;
                Vm.LoadGame(GetVal(inst.Arguments[0]));
                return true;

            default:
                return false;
        }
    }
}
