using System.Collections.Generic;

namespace AriaEngine.Core;

public readonly struct Instruction
{
    public OpCode Op { get; init; }
    public IReadOnlyList<string> Arguments { get; init; }
    public int SourceLine { get; init; }
    public IReadOnlyList<string>? Condition { get; init; } // for inline if

    public Instruction(OpCode op, IReadOnlyList<string> arguments, int sourceLine, IReadOnlyList<string>? condition = null)
    {
        Op = op;
        Arguments = arguments;
        SourceLine = sourceLine;
        Condition = condition;
    }
}
