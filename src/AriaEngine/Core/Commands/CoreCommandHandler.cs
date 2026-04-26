namespace AriaEngine.Core.Commands;

public sealed class CoreCommandHandler : BaseCommandHandler
{
    public override IReadOnlySet<OpCode> HandledCodes { get; } = new HashSet<OpCode>
    {
        OpCode.Window,
        OpCode.Caption,
        OpCode.WindowTitle,
        OpCode.Font,
        OpCode.FontAtlasSize,
        OpCode.Script,
        OpCode.Debug
    };

    public CoreCommandHandler(VirtualMachine vm) : base(vm)
    {
    }

    public override bool Execute(Instruction inst)
    {
        switch (inst.Op)
        {
            case OpCode.Window:
                if (!ValidateArgs(inst, 3)) return true;
                State.WindowWidth = GetVal(inst.Arguments[0]);
                State.WindowHeight = GetVal(inst.Arguments[1]);
                State.Title = inst.Arguments[2];
                return true;

            case OpCode.Caption:
                if (!ValidateArgs(inst, 1)) return true;
                State.Title = inst.Arguments[0];
                return true;

            case OpCode.WindowTitle:
                if (!ValidateArgs(inst, 1)) return true;
                State.Title = GetString(inst.Arguments[0]);
                return true;

            case OpCode.Font:
                if (!ValidateArgs(inst, 1)) return true;
                State.FontPath = inst.Arguments[0];
                return true;

            case OpCode.FontAtlasSize:
                if (!ValidateArgs(inst, 1)) return true;
                State.FontAtlasSize = Math.Clamp(GetVal(inst.Arguments[0]), 8, 512);
                return true;

            case OpCode.Script:
                if (!ValidateArgs(inst, 1)) return true;
                State.MainScript = inst.Arguments[0];
                return true;

            case OpCode.Debug:
                State.DebugMode = inst.Arguments.Count > 0 && inst.Arguments[0] == "on";
                return true;

            default:
                return false;
        }
    }
}
