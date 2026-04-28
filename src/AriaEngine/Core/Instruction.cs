using System.Collections.Generic;

namespace AriaEngine.Core;

public readonly struct Instruction
{
    public OpCode Op { get; init; }
    public IReadOnlyList<string> Arguments { get; init; }
    public int SourceLine { get; init; }
    public Condition Condition { get; init; }
    public string ScriptFile { get; init; }

    public Instruction(OpCode op, IReadOnlyList<string> arguments, int sourceLine, Condition condition = default)
    {
        Op = op;
        Arguments = arguments;
        SourceLine = sourceLine;
        Condition = condition;
        ScriptFile = "";
    }

    /// <summary>
    /// 旧式コンストラクタ（移行期間用）
    /// </summary>
    public Instruction(OpCode op, IReadOnlyList<string> arguments, int sourceLine, IReadOnlyList<string>? conditionTokens)
    {
        Op = op;
        Arguments = arguments;
        SourceLine = sourceLine;
        Condition = conditionTokens != null ? Condition.FromTokens(conditionTokens) : default;
        ScriptFile = "";
    }
}
