using System.Text.Json.Serialization;

namespace AriaEngine.Wasm;

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(WebAssetManifest))]
[JsonSerializable(typeof(StorageSnapshot))]
internal sealed partial class WasmJsonContext : JsonSerializerContext
{
}
