using System.Text.Json;
using System.Text.Json.Serialization;
using AriaEngine.Packaging;
using AriaEngine.Text;

namespace AriaEngine.Core;

internal static class AriaJson
{
    public static JsonSerializerOptions CompactOptions { get; } = Create(writeIndented: false);
    public static JsonSerializerOptions IndentedOptions { get; } = Create(writeIndented: true);
    public static JsonSerializerOptions CaseInsensitiveOptions { get; } = Create(writeIndented: false, propertyNameCaseInsensitive: true);

    private static JsonSerializerOptions Create(bool writeIndented, bool propertyNameCaseInsensitive = false)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = writeIndented,
            PropertyNameCaseInsensitive = propertyNameCaseInsensitive
        };
        options.TypeInfoResolverChain.Add(AriaCoreJsonContext.Default);
        return options;
    }
}

[JsonSourceGenerationOptions(WriteIndented = false, PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(LocalizationManifest))]
[JsonSerializable(typeof(AppConfig))]
[JsonSerializable(typeof(AssetGcConfig))]
[JsonSerializable(typeof(PersistentGameData))]
[JsonSerializable(typeof(ChapterData))]
[JsonSerializable(typeof(ChapterInfo))]
[JsonSerializable(typeof(CharacterData))]
[JsonSerializable(typeof(CharacterInfo))]
[JsonSerializable(typeof(PakManifest))]
[JsonSerializable(typeof(PakManifestEntry))]
[JsonSerializable(typeof(PakPatchManifest))]
[JsonSerializable(typeof(PakPatchEntry))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(Dictionary<string, int>))]
[JsonSerializable(typeof(Dictionary<string, bool>))]
[JsonSerializable(typeof(List<string>))]
internal sealed partial class AriaCoreJsonContext : JsonSerializerContext
{
}

[JsonSourceGenerationOptions(WriteIndented = true, PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(AppConfig))]
[JsonSerializable(typeof(AssetGcConfig))]
[JsonSerializable(typeof(ChapterData))]
[JsonSerializable(typeof(ErrorLogPayload))]
[JsonSerializable(typeof(ErrorLogEntry))]
[JsonSerializable(typeof(CrashDiagnosticsSummary))]
[JsonSerializable(typeof(CrashDiagnosticsState))]
internal sealed partial class AriaCoreIndentedJsonContext : JsonSerializerContext
{
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(SaveFile))]
[JsonSerializable(typeof(SaveData))]
[JsonSerializable(typeof(SaveMeta))]
[JsonSerializable(typeof(GameState))]
[JsonSerializable(typeof(RegisterState))]
[JsonSerializable(typeof(VmExecutionState))]
[JsonSerializable(typeof(InteractionState))]
[JsonSerializable(typeof(RenderState))]
[JsonSerializable(typeof(AudioState))]
[JsonSerializable(typeof(TextWindowState))]
[JsonSerializable(typeof(ChoiceStyleState))]
[JsonSerializable(typeof(TextRuntimeState))]
[JsonSerializable(typeof(PlaybackControlState))]
[JsonSerializable(typeof(MenuRuntimeState))]
[JsonSerializable(typeof(UiRuntimeState))]
[JsonSerializable(typeof(UiCompositionState))]
[JsonSerializable(typeof(EngineSettingsState))]
[JsonSerializable(typeof(LocalizationRuntimeState))]
[JsonSerializable(typeof(UiQualityState))]
[JsonSerializable(typeof(SceneRuntimeState))]
[JsonSerializable(typeof(SaveRuntimeState))]
[JsonSerializable(typeof(FlagRuntimeState))]
[JsonSerializable(typeof(Sprite))]
[JsonSerializable(typeof(FastSpriteDictionary))]
[JsonSerializable(typeof(Dictionary<int, Sprite>))]
[JsonSerializable(typeof(BacklogEntry))]
[JsonSerializable(typeof(BacklogStateSnapshot))]
[JsonSerializable(typeof(List<BacklogEntry>))]
[JsonSerializable(typeof(RightMenuEntry))]
[JsonSerializable(typeof(GalleryEntry))]
[JsonSerializable(typeof(ChapterInfo))]
[JsonSerializable(typeof(TextSegment))]
[JsonSerializable(typeof(TextStyle))]
internal sealed partial class AriaSaveJsonContext : JsonSerializerContext
{
}
