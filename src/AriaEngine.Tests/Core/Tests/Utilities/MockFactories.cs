using System.Collections.Generic;
using AriaEngine.Core;

namespace AriaEngine.Tests.Core.Tests.Utilities;

public static class MockFactories
{
    public static GameState CreateMockGameState() => new();

    public static Instruction CreateTestInstruction(OpCode op, params string[] args)
    {
        return new Instruction
        {
            Op = op,
            Arguments = new List<string>(args),
            SourceLine = 0
        };
    }
}
