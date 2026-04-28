using System.Collections.Generic;

namespace AriaEngine.Core;

using System;

public readonly struct Instruction
{
    public OpCode Op { get; init; }
    public IReadOnlyList<string> Arguments { get; init; }
    public int SourceLine { get; init; }
    public Condition Condition { get; init; }
    public string ScriptFile { get; init; }
    // Storage scope for the instruction (local/global/persistent/save/volatile)
    public AriaEngine.Core.StorageScope Scope { get; init; }

    public Instruction(OpCode op, IReadOnlyList<string> arguments, int sourceLine, Condition condition = default, AriaEngine.Core.StorageScope scope = AriaEngine.Core.StorageScope.Local)
    {
        Op = op;
        Arguments = arguments;
        SourceLine = sourceLine;
        Condition = condition;
        ScriptFile = "";
        Scope = scope;
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
        Scope = AriaEngine.Core.StorageScope.Local;
    }
}
