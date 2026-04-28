namespace AriaEngine.Core;

/// <summary>
/// Parserの解析結果
/// </summary>
public class ParseResult
{
    public List<Instruction> Instructions { get; set; } = new();
    public Dictionary<string, int> Labels { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<FunctionInfo> Functions { get; set; } = new();
    public List<StructDefinition> Structs { get; set; } = new();
    public List<EnumDefinition> Enums { get; set; } = new();
    public string[] SourceLines { get; set; } = Array.Empty<string>();

    // Explicitly declared variables and their storage scope
    // Key: variable name (including % or $), Value: storage scope as a lowercase string
    public Dictionary<string, string> DeclaredVariables { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    // Readonly variable declarations: (instruction index, variable name)
    // Used by static analysis to detect illegal reassignments
    public List<(int InstructionIndex, string VariableName)> ReadonlyDeclarations { get; set; } = new();

    // T13: Owned sprite/resource declarations
    // Variables declared with `owned sprite %id` are auto-cleaned up when their scope exits
    public HashSet<string> OwnedSprites { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
