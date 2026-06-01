namespace AriaEngine.Core;

public sealed class LocalizationManifest
{
    public string DefaultLanguage { get; set; } = "ja-JP";
    public string FallbackLanguage { get; set; } = "ja-JP";
    public List<string> Languages { get; set; } = new();
    public List<string> Resources { get; set; } = new();
    public Dictionary<string, string> Fonts { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> DateFormat { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public string ScenarioRoot { get; set; } = "";
    public List<string> ScenarioFiles { get; set; } = new();
    public Dictionary<string, string> ScenarioStatus { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
