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
    public string[] SourceLines { get; set; } = Array.Empty<string>();
}
