using AriaEngine.Rendering;

namespace AriaEngine.Core.Commands;

public abstract class BaseCommandHandler : ICommandHandler
{
    protected readonly VirtualMachine Vm;

    protected BaseCommandHandler(VirtualMachine vm)
    {
        Vm = vm;
    }

    public abstract IReadOnlySet<OpCode> HandledCodes { get; }
    public abstract bool Execute(Instruction inst);

    protected GameState State => Vm.State;
    protected ConfigManager Config => Vm.Config;
    protected TweenManager Tweens => Vm.Tweens;
    protected ErrorReporter Reporter => Vm.Reporter;
    protected string CurrentScriptFile => Vm.CurrentScriptFile;
    protected bool ValidateArgs(Instruction inst, int minArgs) => Vm.ValidateArgs(inst, minArgs);
    protected bool EvaluateCondition(Condition condition) => Vm.EvaluateCondition(condition);
    protected bool EvaluateCondition(IReadOnlyList<string>? condTokens) => Vm.EvaluateCondition(condTokens);
    protected bool IsOn(string token) => Vm.IsOn(token);
    protected int GetVal(string arg) => Vm.GetVal(arg);
    protected int GetReg(string reg) => Vm.GetReg(reg);
    protected float GetFloat(string arg, Instruction inst, float fallback = 0f) => Vm.GetFloat(arg, inst, fallback);
    protected string GetString(string arg) => Vm.GetString(arg);
    protected void SetReg(string reg, int value) => Vm.SetReg(reg, value);
    protected void SetStr(string reg, string value) => Vm.SetStr(reg, value);
    protected void SetSystemButton(string name, bool visible) => Vm.SetSystemButton(name, visible);
    protected void MarkPersistentDirty() => Vm.MarkPersistentDirty();
    protected void JumpTo(string label) => Vm.JumpTo(label);
    protected void ClearCompatUiSprites() => Vm.ClearCompatUiSprites();
    protected void ApplyUiTheme(string themeName) => Vm.ApplyUiTheme(themeName);
    protected int AllocateCompatUiSpriteId() => Vm.AllocateCompatUiSpriteId();
    protected void TrackCompatUiSprite(int spriteId) => Vm.TrackCompatUiSprite(spriteId);
    protected bool HasAnyVisibleButton() => Vm.HasAnyVisibleButton();
    protected void AddBacklogEntry() => Vm.AddBacklogEntry();
}
