namespace AriaEngine.Core.Commands;

public interface ICommandHandler
{
    IReadOnlySet<OpCode> HandledCodes { get; }
    bool Execute(Instruction inst);
}
