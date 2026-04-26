namespace AriaEngine.Core.Commands;

public sealed class InputCommandHandler : BaseCommandHandler
{
    public override IReadOnlySet<OpCode> HandledCodes { get; } = new HashSet<OpCode>
    {
        OpCode.Btn,
        OpCode.BtnArea,
        OpCode.BtnClear,
        OpCode.BtnClearAll,
        OpCode.SpBtn,
        OpCode.BtnTime,
        OpCode.BtnWait,
        OpCode.RightMenu,
        OpCode.ClickCursor
    };

    public InputCommandHandler(VirtualMachine vm) : base(vm)
    {
    }

    public override bool Execute(Instruction inst)
    {
        switch (inst.Op)
        {
            case OpCode.Btn:
                if (!ValidateArgs(inst, 1)) return true;
                {
                    int id = GetVal(inst.Arguments[0]);
                    if (State.Sprites.TryGetValue(id, out var btn)) btn.IsButton = true;
                    State.SpriteButtonMap.TryAdd(id, id);
                }
                return true;

            case OpCode.BtnArea:
                if (!ValidateArgs(inst, 5)) return true;
                if (State.Sprites.TryGetValue(GetVal(inst.Arguments[0]), out var ba))
                {
                    ba.IsButton = true;
                    ba.ClickAreaX = GetVal(inst.Arguments[1]);
                    ba.ClickAreaY = GetVal(inst.Arguments[2]);
                    ba.ClickAreaW = GetVal(inst.Arguments[3]);
                    ba.ClickAreaH = GetVal(inst.Arguments[4]);
                }
                return true;

            case OpCode.BtnClear:
                if (!ValidateArgs(inst, 1)) return true;
                if (State.Sprites.TryGetValue(GetVal(inst.Arguments[0]), out var bcl)) bcl.IsButton = false;
                return true;

            case OpCode.BtnClearAll:
                foreach (var sprite in State.Sprites.Values) sprite.IsButton = false;
                State.SpriteButtonMap.Clear();
                return true;

            case OpCode.SpBtn:
                if (!ValidateArgs(inst, 2)) return true;
                {
                    int spriteId = GetVal(inst.Arguments[0]);
                    int buttonId = GetVal(inst.Arguments[1]);
                    if (State.Sprites.TryGetValue(spriteId, out var spb)) spb.IsButton = true;
                    State.SpriteButtonMap[spriteId] = buttonId;
                }
                return true;

            case OpCode.BtnTime:
                if (!ValidateArgs(inst, 1)) return true;
                State.ButtonTimeoutMs = GetVal(inst.Arguments[0]);
                return true;

            case OpCode.BtnWait:
                State.ButtonResultRegister = inst.Arguments.Count > 0 ? inst.Arguments[0] : "0";
                State.State = VmState.WaitingForButton;
                Vm.AutoSaveGame();
                return true;

            case OpCode.RightMenu:
                if (inst.Arguments.Count == 1 && inst.Arguments[0].StartsWith("*", StringComparison.Ordinal))
                {
                    State.RightMenuLabel = inst.Arguments[0];
                    return true;
                }

                State.RightMenuEntries.Clear();
                for (int i = 0; i + 1 < inst.Arguments.Count; i += 2)
                {
                    State.RightMenuEntries.Add(new RightMenuEntry
                    {
                        Label = GetString(inst.Arguments[i]),
                        Action = inst.Arguments[i + 1].TrimStart('*').ToLowerInvariant()
                    });
                }
                return true;

            case OpCode.ClickCursor:
                if (inst.Arguments.Count > 0 && inst.Arguments[0].Equals("off", StringComparison.OrdinalIgnoreCase))
                {
                    State.ShowClickCursor = false;
                    return true;
                }

                State.ShowClickCursor = true;
                if (inst.Arguments.Count > 0)
                {
                    string value = GetString(inst.Arguments[0]);
                    if (value.Equals("engine", StringComparison.OrdinalIgnoreCase) ||
                        value.Equals("default", StringComparison.OrdinalIgnoreCase) ||
                        value.Equals("builtin", StringComparison.OrdinalIgnoreCase))
                    {
                        State.ClickCursorMode = "engine";
                        State.ClickCursorPath = "";
                    }
                    else
                    {
                        State.ClickCursorMode = "image";
                        State.ClickCursorPath = value;
                    }
                }
                if (inst.Arguments.Count > 1) State.ClickCursorOffsetX = GetVal(inst.Arguments[1]);
                if (inst.Arguments.Count > 2) State.ClickCursorOffsetY = GetVal(inst.Arguments[2]);
                return true;

            default:
                return false;
        }
    }
}
