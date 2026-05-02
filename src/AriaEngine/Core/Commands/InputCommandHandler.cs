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
                    if (State.Render.Sprites.TryGetValue(id, out var btn)) btn.IsButton = true;
                    State.Interaction.SpriteButtonMap.TryAdd(id, id);
                }
                return true;

            case OpCode.BtnArea:
                if (!ValidateArgs(inst, 5)) return true;
                if (State.Render.Sprites.TryGetValue(GetVal(inst.Arguments[0]), out var ba))
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
                if (State.Render.Sprites.TryGetValue(GetVal(inst.Arguments[0]), out var bcl)) bcl.IsButton = false;
                return true;

            case OpCode.BtnClearAll:
                foreach (var sprite in State.Render.Sprites.Values) sprite.IsButton = false;
                State.Interaction.SpriteButtonMap.Clear();
                State.Interaction.FocusedButtonId = -1;
                return true;

            case OpCode.SpBtn:
                if (!ValidateArgs(inst, 2)) return true;
                {
                    int spriteId = GetVal(inst.Arguments[0]);
                    int buttonId = GetVal(inst.Arguments[1]);
                    if (State.Render.Sprites.TryGetValue(spriteId, out var spb)) spb.IsButton = true;
                    State.Interaction.SpriteButtonMap[spriteId] = buttonId;
                }
                return true;

            case OpCode.BtnTime:
                if (!ValidateArgs(inst, 1)) return true;
                State.Interaction.ButtonTimeoutMs = GetVal(inst.Arguments[0]);
                return true;

            case OpCode.BtnWait:
                State.Interaction.ButtonResultRegister = inst.Arguments.Count > 0 ? inst.Arguments[0] : "0";
                State.Execution.State = VmState.WaitingForButton;
                Vm.AutoSaveGame();
                return true;

            case OpCode.RightMenu:
                if (inst.Arguments.Count == 1 && inst.Arguments[0].StartsWith("*", StringComparison.Ordinal))
                {
                    State.MenuRuntime.RightMenuLabel = inst.Arguments[0];
                    return true;
                }

                State.MenuRuntime.RightMenuEntries.Clear();
                for (int i = 0; i + 1 < inst.Arguments.Count; i += 2)
                {
                    State.MenuRuntime.RightMenuEntries.Add(new RightMenuEntry
                    {
                        Label = GetString(inst.Arguments[i]),
                        Action = inst.Arguments[i + 1].TrimStart('*').ToLowerInvariant()
                    });
                }
                return true;

            case OpCode.ClickCursor:
                if (inst.Arguments.Count > 0 && inst.Arguments[0].Equals("off", StringComparison.OrdinalIgnoreCase))
                {
                    State.UiRuntime.ShowClickCursor = false;
                    return true;
                }

                State.UiRuntime.ShowClickCursor = true;
                if (inst.Arguments.Count > 0)
                {
                    string value = GetString(inst.Arguments[0]);
                    if (value.Equals("engine", StringComparison.OrdinalIgnoreCase) ||
                        value.Equals("default", StringComparison.OrdinalIgnoreCase) ||
                        value.Equals("builtin", StringComparison.OrdinalIgnoreCase))
                    {
                        State.UiRuntime.ClickCursorMode = "engine";
                        State.UiRuntime.ClickCursorPath = "";
                    }
                    else
                    {
                        State.UiRuntime.ClickCursorMode = "image";
                        State.UiRuntime.ClickCursorPath = value;
                    }
                }
                if (inst.Arguments.Count > 1) State.UiRuntime.ClickCursorOffsetX = GetVal(inst.Arguments[1]);
                if (inst.Arguments.Count > 2) State.UiRuntime.ClickCursorOffsetY = GetVal(inst.Arguments[2]);
                return true;

            default:
                return false;
        }
    }
}
