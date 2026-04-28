using System.Text.RegularExpressions;
using System.Linq;
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
        OpCode.ScopeEnter,
        OpCode.ScopeExit,
        OpCode.Defer,
        OpCode.JumpIfFalse,
        OpCode.Rnd,
        OpCode.Inc,
        OpCode.Dec,
        OpCode.For,
        OpCode.Next,
        OpCode.GetTimer,
        OpCode.ResetTimer,
        OpCode.WaitTimer,
        OpCode.Include,
        OpCode.ReturnValue
    };

    public FlowCommandHandler(VirtualMachine vm) : base(vm)
    {
    }

    public override bool Execute(Instruction inst)
    {
        switch (inst.Op)
        {
            case OpCode.ScopeEnter:
                Vm.EnterScope();
                return true;

            case OpCode.ScopeExit:
                Vm.ExitScopesUntil(0);
                return true;

            case OpCode.Defer:
                // defer <op> [args...]
                if (!ValidateArgs(inst, 1)) return true;
                string deferOpName = inst.Arguments[0];
                if (CommandRegistry.TryGet(deferOpName, out OpCode deferOp))
                {
                    // Capture argument values at definition time. If an argument is a register,
                    // evaluate it now and store the literal value. If it's a string reg, capture its string value.
                    var rawArgs = inst.Arguments.Skip(1).ToList();
                    var captured = new List<string>(rawArgs.Count);
                    foreach (var a in rawArgs)
                    {
                        if (a.StartsWith("%"))
                        {
                            captured.Add(GetVal(a).ToString());
                        }
                        else if (a.StartsWith("$"))
                        {
                            captured.Add(GetString(a));
                        }
                        else
                        {
                            captured.Add(a);
                        }
                    }
                    var defInst = new Instruction(deferOp, captured, inst.SourceLine);
                    if (State.Execution.ScopeStack.Count > 0)
                    {
                        State.Execution.ScopeStack.Peek().Defer.Add(defInst);
                    }
                }
                return true;
            case OpCode.Let:
            case OpCode.Mov:
                if (!ValidateArgs(inst, 2)) return true;
                
                // let %x = 100 の "=" を除去
                var args = inst.Arguments.Where(a => a != "=").ToList();
                if (args.Count < 2) return true;

                // Global scope bypass: explicit scope recorded on the instruction
                if (inst.Scope == AriaEngine.Core.StorageScope.Global)
                {
                    string dest = args[0];
                    string src = args[1];
                    // String destination
                    if (dest.StartsWith("$") || src.StartsWith("\""))
                    {
                        string strVal = GetString(src);
                        Vm.SetGlobalString(dest, strVal);
                    }
                    else
                    {
                        int intVal = GetVal(src);
                        Vm.SetGlobalRegister(dest, intVal);
                    }
                    return true;
                }
                
                var destArrayMatch = ArrayAccessRegex.Match(args[0]);
                var srcArrayMatch = ArrayAccessRegex.Match(args[1]);
                
                // 第二引数以降を式として評価（算術・比較・配列アクセス対応）
                var expr = ExpressionParser.TryParse(args.Skip(1).ToList());
                
                if (destArrayMatch.Success)
                {
                    // let %arr[index] = value → setarray
                    string arrayName = destArrayMatch.Groups[1].Value;
                    int index = GetVal(destArrayMatch.Groups[2].Value);
                    if (index < 0) return true;
                    int value = expr?.EvaluateInt(State, Vm) ?? GetVal(args[1]);
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
                    // 文字列代入: 式評価で文字列結合も対応
                    if (expr != null && expr.IsStringExpression)
                        SetStr(args[0], expr.EvaluateString(State, Vm));
                    else
                        SetStr(args[0], GetString(args[1]));
                }
                else
                {
                    // 整数代入: 式評価を試行
                    SetReg(args[0], expr?.EvaluateInt(State, Vm) ?? GetVal(args[1]));
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
                    if (div == 0)
                    {
                        Reporter.Report(new AriaError(
                            "0による除算が発生しました。結果を0として扱います。",
                            inst.SourceLine, CurrentScriptFile, AriaErrorLevel.Error, "VM_DIV_ZERO"));
                        SetReg(inst.Arguments[0], 0);
                    }
                    else
                    {
                        SetReg(inst.Arguments[0], GetReg(inst.Arguments[0]) / div);
                    }
                }
                return true;

            case OpCode.Mod:
                if (!ValidateArgs(inst, 2)) return true;
                {
                    int mod = GetVal(inst.Arguments[1]);
                    if (mod == 0)
                    {
                        Reporter.Report(new AriaError(
                            "0による剰余演算が発生しました。結果を0として扱います。",
                            inst.SourceLine, CurrentScriptFile, AriaErrorLevel.Error, "VM_MOD_ZERO"));
                        SetReg(inst.Arguments[0], 0);
                    }
                    else
                    {
                        SetReg(inst.Arguments[0], GetReg(inst.Arguments[0]) % mod);
                    }
                }
                return true;

            case OpCode.SetArray:
                if (!ValidateArgs(inst, 3)) return true;
                {
                    string arrayName = inst.Arguments[0].TrimStart('%');
                    int index = GetVal(inst.Arguments[1]);
                    if (index < 0) return true;
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
                // Ensure any deferred actions defined in the current scope are executed before jumping out
                Vm.ExitScopesUntil(0);
                JumpTo(inst.Arguments[0]);
                return true;

            case OpCode.Break:
                if (!ValidateArgs(inst, 0)) return true;
                // Break should exit the current scope and trigger any on-exit defers
                Vm.ExitScopesUntil(0);
                return true;

            case OpCode.Continue:
                if (!ValidateArgs(inst, 0)) return true;
                // Continue should behave like a loop control; ensure scope exits so defers run
                Vm.ExitScopesUntil(0);
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
                // for %i = 0 to 10 の "=" と "to" を除去
                var forArgs = inst.Arguments.Where(a => a != "=" && !a.Equals("to", StringComparison.OrdinalIgnoreCase)).ToList();
                if (forArgs.Count < 3) return true;
                string forVar = forArgs[0];
                int startVal = GetVal(forArgs[1]);
                string targetArg = forArgs[2];
                
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

            case OpCode.ReturnValue:
                if (inst.Arguments.Count > 0)
                {
                    var retExpr = ExpressionParser.TryParse(inst.Arguments.ToList());
                    State.LastReturnValue = retExpr?.EvaluateInt(State, Vm) ?? GetVal(inst.Arguments[0]);
                }
                else
                {
                    State.LastReturnValue = 0;
                }
                // ref マップを復元
                if (State.RefStack.Count > 0)
                {
                    State.CurrentRefMap = State.RefStack.Pop();
                }
                else
                {
                    State.CurrentRefMap.Clear();
                }
                // ローカルスコープとスプライト寿命をクリーンアップ
                Vm.PopFunctionScope();
                Vm.ReturnFromSubroutine();
                return true;

            default:
                return false;
        }
    }
}
