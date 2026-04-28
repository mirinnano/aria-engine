using System.Text.RegularExpressions;
using Raylib_cs;

namespace AriaEngine.Core.Commands;

public sealed class FlowCommandHandler : BaseCommandHandler
{
    private static readonly Regex ArrayAccessRegex = new(@"^%([A-Za-z_][A-Za-z0-9_]*)\[(.+)\]$", RegexOptions.Compiled);
    public override IReadOnlySet<OpCode> HandledCodes { get; } = new HashSet<OpCode>
    {
        OpCode.Let,
        OpCode.Mov,
        OpCode.SetArray,
        OpCode.GetArray,
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
        OpCode.Rnd,
        OpCode.Inc,
        OpCode.Dec,
        OpCode.For,
        OpCode.Next,
        OpCode.GetTimer,
        OpCode.ResetTimer,
        OpCode.WaitTimer,
        OpCode.Include
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
                
                // let %x = 100 の "=" を除去
                var args = inst.Arguments.Where(a => a != "=").ToList();
                if (args.Count < 2) return true;
                
                var destArrayMatch = ArrayAccessRegex.Match(args[0]);
                var srcArrayMatch = ArrayAccessRegex.Match(args[1]);
                
                if (destArrayMatch.Success)
                {
                    // let %arr[index] = value → setarray
                    string arrayName = destArrayMatch.Groups[1].Value;
                    int index = GetVal(destArrayMatch.Groups[2].Value);
                    int value = GetVal(args[1]);
                    if (!State.Arrays.TryGetValue(arrayName, out var array))
                    {
                        array = new int[index + 1];
                        State.Arrays[arrayName] = array;
                    }
                    if (index >= array.Length)
                    {
                        Array.Resize(ref array, index + 1);
                        State.Arrays[arrayName] = array;
                    }
                    if (index >= 0)
                        array[index] = value;
                }
                else if (srcArrayMatch.Success && !args[0].StartsWith("$"))
                {
                    // let %x = %arr[index] → getarray
                    string arrayName = srcArrayMatch.Groups[1].Value;
                    int index = GetVal(srcArrayMatch.Groups[2].Value);
                    if (State.Arrays.TryGetValue(arrayName, out var array) && index >= 0 && index < array.Length)
                        SetReg(args[0], array[index]);
                    else
                        SetReg(args[0], 0);
                }
                else if (args[0].StartsWith("$") || args[1].StartsWith("\""))
                {
                    SetStr(args[0], GetString(args[1]));
                }
                else
                {
                    SetReg(args[0], GetVal(args[1]));
                }
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

            case OpCode.SetArray:
                if (!ValidateArgs(inst, 3)) return true;
                {
                    string arrayName = inst.Arguments[0].TrimStart('%');
                    int index = GetVal(inst.Arguments[1]);
                    int value = GetVal(inst.Arguments[2]);
                    if (!State.Arrays.TryGetValue(arrayName, out var array))
                    {
                        // 配列が存在しない場合は自動作成（サイズは index + 1）
                        array = new int[index + 1];
                        State.Arrays[arrayName] = array;
                    }
                    if (index >= 0 && index < array.Length)
                    {
                        array[index] = value;
                    }
                }
                return true;

            case OpCode.GetArray:
                if (!ValidateArgs(inst, 3)) return true;
                {
                    string destReg = inst.Arguments[0];
                    string arrayName = inst.Arguments[1].TrimStart('%');
                    int index = GetVal(inst.Arguments[2]);
                    if (State.Arrays.TryGetValue(arrayName, out var array) && index >= 0 && index < array.Length)
                    {
                        SetReg(destReg, array[index]);
                    }
                    else
                    {
                        SetReg(destReg, 0);
                    }
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
                string forVar = inst.Arguments[0];
                int startVal = GetVal(inst.Arguments[1]);
                string targetArg = inst.Arguments[2];
                
                int targetValue;
                if (State.Arrays.TryGetValue(targetArg, out var arr))
                {
                    // for %i = 0 to arrayName → 配列長でループ
                    targetValue = arr.Length - 1;
                }
                else
                {
                    targetValue = GetVal(targetArg);
                }
                
                SetReg(forVar, startVal);
                State.LoopStack.Push(new LoopState
                {
                    PC = State.ProgramCounter,
                    VarName = forVar,
                    TargetValue = targetValue
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

            case OpCode.Include:
                if (!ValidateArgs(inst, 1)) return true;
                string includePath = GetString(inst.Arguments[0]);
                if (!Vm.IncludeScript(includePath))
                {
                    Reporter.Report(new AriaError($"include '{includePath}' の読み込みに失敗しました。", inst.SourceLine, CurrentScriptFile, AriaErrorLevel.Warning));
                }
                return true;

            default:
                return false;
        }
    }
}
