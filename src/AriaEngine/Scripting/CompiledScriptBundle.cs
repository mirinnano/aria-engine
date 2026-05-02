using System;
using System.Collections.Generic;
using AriaEngine.Core;

namespace AriaEngine.Scripting;

public sealed class CompiledScriptBundle
{
    public string Version { get; set; } = "1";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public string InitPath { get; set; } = "init.aria";
    public string MainPath { get; set; } = "assets/scripts/main.aria";
    public Dictionary<string, CompiledScript> Scripts { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class CompiledScript
{
    public string Path { get; set; } = "";
    public List<CompiledInstruction> Instructions { get; set; } = new();
    public Dictionary<string, int> Labels { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<FunctionInfo> Functions { get; set; } = new();
    public List<StructDefinition> Structs { get; set; } = new();
    public List<EnumDefinition> Enums { get; set; } = new();
    public HashSet<string> OwnedSprites { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public string[] SourceLines { get; set; } = Array.Empty<string>();
}

public sealed class CompiledInstruction
{
    public int Op { get; set; }
    public List<string> Arguments { get; set; } = new();
    public int SourceLine { get; set; }
    public List<string>? Condition { get; set; }
}
