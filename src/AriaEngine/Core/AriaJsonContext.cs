using System.Text.Json.Serialization;
using AriaEngine.Scripting;

namespace AriaEngine.Core;

/// <summary>
/// JSON source generation context for Native AOT compatibility.
/// All types serialized/deserialized by the engine must be registered here.
/// </summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    ReferenceHandler = JsonKnownReferenceHandler.IgnoreCycles,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(SaveFile))]
[JsonSerializable(typeof(SaveMeta))]
[JsonSerializable(typeof(SaveData))]
[JsonSerializable(typeof(GameState))]
[JsonSerializable(typeof(AppConfig))]
[JsonSerializable(typeof(PersistentGameData))]
[JsonSerializable(typeof(ChapterInfo))]
[JsonSerializable(typeof(CharacterEntry))]
[JsonSerializable(typeof(CompiledScriptBundle))]
[JsonSerializable(typeof(CompiledScriptHeader))]
[JsonSerializable(typeof(CompiledScriptModule))]
[JsonSerializable(typeof(PakModels.PakManifest))]
[JsonSerializable(typeof(PakModels.PakEntry))]
[JsonSerializable(typeof(Dictionary<string, object>))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(Dictionary<string, int>))]
[JsonSerializable(typeof(Dictionary<string, bool>))]
[JsonSerializable(typeof(List<string>))]
public partial class AriaJsonContext : JsonSerializerContext
{
}
