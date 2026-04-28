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
        OpCode.SystemCall,
        OpCode.Throw,
        OpCode.Assert,
        OpCode.Panic
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
                // ref マップを保存
                State.RefStack.Push(new Dictionary<string, string>(State.CurrentRefMap));
                // 関数呼び出しの場合はローカルスコープをプッシュ（C++likeなスコープ）
                string targetLabel = inst.Arguments[0].TrimStart('*');
                if (Vm.FunctionTable.GetFunction(targetLabel) != null)
                {
                    Vm.PushFunctionScope();
                }
                JumpTo(inst.Arguments[0]);
                for (int i = inst.Arguments.Count - 1; i >= 1; i--)
                {
                    State.ParamStack.Push(inst.Arguments[i]);
                }
                return true;

            case OpCode.Return:
                // Ensure scope defer cleanup runs before returning from a function
                Vm.ExitScopesUntil(0);
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
                    
                    // REF| プレフィックス処理
                    if (val.StartsWith("REF|", StringComparison.OrdinalIgnoreCase))
                    {
                        string originalReg = val.Substring(4).Trim();
                        string destReg = arg.TrimStart('%');
                        State.CurrentRefMap[destReg] = originalReg;
                    }
                    else if (arg.StartsWith("$"))
                    {
                        SetStr(arg, GetString(val));
                    }
                    else
                    {
                        SetReg(arg, GetVal(val));
                    }
                }
                return true;

            case OpCode.Throw:
                // Ensure scope defer cleanup on throw as it propagates
                Vm.ExitScopesUntil(0);
                if (State.TryStack.Count > 0)
                {
                    State.ProgramCounter = State.TryStack.Pop();
                }
                else
                {
                    Reporter.Report(new AriaError(
                        "未処理の例外 (throw)。catch ブロックがありません。",
                        State.ProgramCounter,
                        CurrentScriptFile,
                        AriaErrorLevel.Error,
                        "VM_UNHANDLED_THROW"));
                    State.State = VmState.Ended;
                }
                return true;

            case OpCode.Assert:
                // assert cond, "message" - 開発モードでのみチェック
                if (inst.Arguments.Count >= 1)
                {
                    var condition = EvaluateCondition(inst.Arguments);
                    if (!condition)
                    {
                        string message = inst.Arguments.Count > 1 ? inst.Arguments[1] : "assertion failed";
                        Reporter.Report(new AriaError(
                            $"Assertion failed: {message}",
                            State.ProgramCounter,
                            CurrentScriptFile,
                            AriaErrorLevel.Error,
                            "VM_ASSERT_FAILED"));
                    }
                }
                return true;

            case OpCode.Panic:
                // panic "message" - 即座にVMを停止
                string panicMessage = inst.Arguments.Count > 0 ? inst.Arguments[0] : "panic";
                Reporter.Report(new AriaError(
                    $"Panic: {panicMessage}",
                    State.ProgramCounter,
                    CurrentScriptFile,
                    AriaErrorLevel.Error,
                    "VM_PANIC"));
                State.State = VmState.Ended;
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
