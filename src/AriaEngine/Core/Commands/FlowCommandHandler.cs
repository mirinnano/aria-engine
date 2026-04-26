using Raylib_cs;

namespace AriaEngine.Core.Commands;

public sealed class FlowCommandHandler : BaseCommandHandler
{
    public override IReadOnlySet<OpCode> HandledCodes { get; } = new HashSet<OpCode>
    {
        OpCode.Let,
        OpCode.Mov,
        OpCode.Add,
        OpCode.Sub,
        OpCode.Mul,
        OpCode.Div,
        OpCode.Mod,
        OpCode.Cmp,
        OpCode.Beq,
        OpCode.Bne,
        OpCode.Bgt,
        OpCode.Blt,
        OpCode.Jmp,
        OpCode.JumpIfFalse,
        OpCode.Delay,
        OpCode.Rnd,
        OpCode.Inc,
        OpCode.Dec,
        OpCode.For,
        OpCode.Next,
        OpCode.GetTimer,
        OpCode.ResetTimer,
        OpCode.WaitTimer
    };

    public FlowCommandHandler(VirtualMachine vm) : base(vm)
    {
    }

    public override bool Execute(Instruction inst)
    {
        switch (inst.Op)
        {
            case OpCode.Let:
            case OpCode.Mov:
                if (!ValidateArgs(inst, 2)) return true;
                if (inst.Arguments[0].StartsWith("$") || inst.Arguments[1].StartsWith("\""))
                    SetStr(inst.Arguments[0], GetString(inst.Arguments[1]));
                else
                    SetReg(inst.Arguments[0], GetVal(inst.Arguments[1]));
                return true;

            case OpCode.Add:
                if (!ValidateArgs(inst, 2)) return true;
                SetReg(inst.Arguments[0], GetReg(inst.Arguments[0]) + GetVal(inst.Arguments[1]));
                return true;

            case OpCode.Sub:
                if (!ValidateArgs(inst, 2)) return true;
                SetReg(inst.Arguments[0], GetReg(inst.Arguments[0]) - GetVal(inst.Arguments[1]));
                return true;

            case OpCode.Mul:
                if (!ValidateArgs(inst, 2)) return true;
                SetReg(inst.Arguments[0], GetReg(inst.Arguments[0]) * GetVal(inst.Arguments[1]));
                return true;

            case OpCode.Div:
                if (!ValidateArgs(inst, 2)) return true;
                {
                    int div = GetVal(inst.Arguments[1]);
                    SetReg(inst.Arguments[0], div != 0 ? GetReg(inst.Arguments[0]) / div : 0);
                }
                return true;

            case OpCode.Mod:
                if (!ValidateArgs(inst, 2)) return true;
                {
                    int mod = GetVal(inst.Arguments[1]);
                    SetReg(inst.Arguments[0], mod != 0 ? GetReg(inst.Arguments[0]) % mod : 0);
                }
                return true;

            case OpCode.Cmp:
                if (!ValidateArgs(inst, 2)) return true;
                {
                    int lhs = GetReg(inst.Arguments[0]);
                    int rhs = GetVal(inst.Arguments[1]);
                    State.CompareFlag = lhs == rhs ? 0 : (lhs > rhs ? 1 : -1);
                }
                return true;

            case OpCode.Beq:
                if (!ValidateArgs(inst, 1)) return true;
                if (State.CompareFlag == 0) JumpTo(inst.Arguments[0]);
                return true;

            case OpCode.Bne:
                if (!ValidateArgs(inst, 1)) return true;
                if (State.CompareFlag != 0) JumpTo(inst.Arguments[0]);
                return true;

            case OpCode.Bgt:
                if (!ValidateArgs(inst, 1)) return true;
                if (State.CompareFlag == 1) JumpTo(inst.Arguments[0]);
                return true;

            case OpCode.Blt:
                if (!ValidateArgs(inst, 1)) return true;
                if (State.CompareFlag == -1) JumpTo(inst.Arguments[0]);
                return true;

            case OpCode.Jmp:
                if (!ValidateArgs(inst, 1)) return true;
                JumpTo(inst.Arguments[0]);
                return true;

            case OpCode.JumpIfFalse:
                if (!ValidateArgs(inst, 1)) return true;
                if (!EvaluateCondition(inst.Condition)) JumpTo(inst.Arguments[0]);
                return true;

            case OpCode.Delay:
                if (!ValidateArgs(inst, 1)) return true;
                State.DelayTimerMs = GetVal(inst.Arguments[0]);
                State.State = VmState.WaitingForDelay;
                return true;

            case OpCode.Rnd:
                if (!ValidateArgs(inst, 3)) return true;
                SetReg(inst.Arguments[0], Raylib.GetRandomValue(GetVal(inst.Arguments[1]), GetVal(inst.Arguments[2])));
                return true;

            case OpCode.Inc:
                if (!ValidateArgs(inst, 1)) return true;
                SetReg(inst.Arguments[0], GetReg(inst.Arguments[0]) + 1);
                return true;

            case OpCode.Dec:
                if (!ValidateArgs(inst, 1)) return true;
                SetReg(inst.Arguments[0], GetReg(inst.Arguments[0]) - 1);
                return true;

            case OpCode.For:
                if (!ValidateArgs(inst, 3)) return true;
                SetReg(inst.Arguments[0], GetVal(inst.Arguments[1]));
                State.LoopStack.Push(new LoopState
                {
                    PC = State.ProgramCounter,
                    VarName = inst.Arguments[0],
                    TargetValue = GetVal(inst.Arguments[2])
                });
                return true;

            case OpCode.Next:
                if (State.LoopStack.Count > 0)
                {
                    var loop = State.LoopStack.Peek();
                    int currVal = GetReg(loop.VarName) + 1;
                    SetReg(loop.VarName, currVal);
                    if (currVal <= loop.TargetValue) State.ProgramCounter = loop.PC;
                    else State.LoopStack.Pop();
                }
                return true;

            case OpCode.GetTimer:
                if (!ValidateArgs(inst, 1)) return true;
                SetReg(inst.Arguments[0], (int)State.ScriptTimerMs);
                return true;

            case OpCode.ResetTimer:
                State.ScriptTimerMs = 0;
                return true;

            case OpCode.WaitTimer:
                if (!ValidateArgs(inst, 1)) return true;
                State.DelayTimerMs = Math.Max(0, GetVal(inst.Arguments[0]) - State.ScriptTimerMs);
                State.State = VmState.WaitingForDelay;
                return true;

            default:
                return false;
        }
    }
}
