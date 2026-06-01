using System.Text.Json.Serialization;
using AriaEngine.Core;

namespace AriaEngine.Scripting;

[JsonSourceGenerationOptions(WriteIndented = false)]
[JsonSerializable(typeof(CompiledScriptBundle))]
[JsonSerializable(typeof(CompiledScript))]
[JsonSerializable(typeof(CompiledInstruction))]
[JsonSerializable(typeof(FunctionInfo))]
[JsonSerializable(typeof(ParameterInfo))]
[JsonSerializable(typeof(StructDefinition))]
[JsonSerializable(typeof(StructField))]
[JsonSerializable(typeof(EnumDefinition))]
internal sealed partial class AriaScriptJsonContext : JsonSerializerContext
{
}
