namespace AriaEngine.Core.Commands;

public sealed class ScriptCommandHandler : BaseCommandHandler
{
    public override IReadOnlySet<OpCode> HandledCodes { get; } = new HashSet<OpCode>
    {
        OpCode.Gosub,
        OpCode.Return,
        OpCode.Defsub,
        OpCode.Getparam,
        OpCode.Alias,
        OpCode.SystemCall
    };

    public ScriptCommandHandler(VirtualMachine vm) : base(vm)
    {
    }

    public override bool Execute(Instruction inst)
    {
        switch (inst.Op)
        {
            case OpCode.Gosub:
                if (!ValidateArgs(inst, 1)) return true;
                State.CallStack.Push(State.ProgramCounter);
                JumpTo(inst.Arguments[0]);
                for (int i = inst.Arguments.Count - 1; i >= 1; i--)
                {
                    State.ParamStack.Push(inst.Arguments[i]);
                }
                return true;

            case OpCode.Return:
                Vm.ReturnFromSubroutine();
                return true;

            case OpCode.Defsub:
                return true;

            case OpCode.Alias:
                if (!ValidateArgs(inst, 2)) return true;
                return true;

            case OpCode.Getparam:
                foreach (var arg in inst.Arguments)
                {
                    if (State.ParamStack.Count == 0) break;

                    var val = State.ParamStack.Pop();
                    if (arg.StartsWith("$"))
                    {
                        SetStr(arg, GetString(val));
                    }
                    else
                    {
                        SetReg(arg, GetVal(val));
                    }
                }
                return true;

            case OpCode.SystemCall:
                if (!ValidateArgs(inst, 1)) return true;
                switch (inst.Arguments[0].ToLowerInvariant())
                {
                    case "rmenu":
                        Vm.Menu.OpenMainMenu();
                        break;
                    case "autosave":
                        Vm.AutoSaveGame();
                        break;
                    case "autoload":
                    case "load_auto":
                        Vm.LoadAutoSaveGame();
                        break;
                    case "lookback":
                        Vm.Menu.OpenBacklog();
                        break;
                    case "load":
                        Vm.Menu.OpenSaveLoadMenu(false);
                        break;
                }
                return true;

            default:
                return false;
        }
    }
}
