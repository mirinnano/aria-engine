using System.Text.Json;
using System.Globalization;
using AriaEngine.Assets;

namespace AriaEngine.Core;

public sealed class LocalizationManager
{
    private readonly Dictionary<string, Dictionary<string, string>> _resources;

    public static LocalizationManager Empty { get; } = new(
        new LocalizationManifest(),
        new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase));

    public LocalizationManifest Manifest { get; }
    public string CurrentLanguage { get; private set; }
    public string FallbackLanguage => Manifest.FallbackLanguage;

    private LocalizationManager(LocalizationManifest manifest, Dictionary<string, Dictionary<string, string>> resources)
    {
        Manifest = manifest;
        _resources = resources;
        CurrentLanguage = string.IsNullOrWhiteSpace(manifest.DefaultLanguage) ? "ja-JP" : manifest.DefaultLanguage;
    }

    public static LocalizationManager Load(IAssetProvider provider, string manifestPath)
    {
        var manifest = JsonSerializer.Deserialize(provider.ReadAllText(manifestPath), AriaCoreJsonContext.Default.LocalizationManifest)
            ?? new LocalizationManifest();
        if (string.IsNullOrWhiteSpace(manifest.FallbackLanguage))
        {
            manifest.FallbackLanguage = manifest.DefaultLanguage;
        }

        string root = Path.GetDirectoryName(manifestPath)?.Replace('\\', '/') ?? "";
        var resources = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        foreach (string language in manifest.Languages.DefaultIfEmpty(manifest.DefaultLanguage))
        {
            if (string.IsNullOrWhiteSpace(language)) continue;
            var merged = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string resource in manifest.Resources)
            {
                string path = $"{root}/{resource}.{language}.json";
                if (!provider.Exists(path)) continue;
                var table = JsonSerializer.Deserialize(provider.ReadAllText(path), AriaCoreJsonContext.Default.DictionaryStringString);
                if (table == null) continue;
                foreach (var pair in table)
                {
                    merged[pair.Key] = pair.Value;
                }
            }
            resources[language] = merged;
        }

        return new LocalizationManager(manifest, resources);
    }

    public void SetLanguage(string language)
    {
        if (string.IsNullOrWhiteSpace(language)) return;
        CurrentLanguage = _resources.ContainsKey(language) || Manifest.Languages.Contains(language)
            ? language
            : Manifest.FallbackLanguage;
    }

    public string Get(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return "";
        if (_resources.TryGetValue(CurrentLanguage, out var current) && current.TryGetValue(key, out string? value))
        {
            return value;
        }
        if (_resources.TryGetValue(Manifest.FallbackLanguage, out var fallback) && fallback.TryGetValue(key, out value))
        {
            return value;
        }
        return key;
    }

    public string Format(string key, params object[] args)
    {
        string template = Get(key);
        if (args.Length == 0) return template;

        try
        {
            return string.Format(CultureInfo.InvariantCulture, template, args);
        }
        catch (FormatException)
        {
            return template;
        }
    }

    public string GetDateFormat()
    {
        if (Manifest.DateFormat.TryGetValue(CurrentLanguage, out string? format) &&
            !string.IsNullOrWhiteSpace(format))
        {
            return format;
        }

        if (Manifest.DateFormat.TryGetValue(Manifest.FallbackLanguage, out format) &&
            !string.IsNullOrWhiteSpace(format))
        {
            return format;
        }

        return "yyyy/MM/dd HH:mm";
    }

    public IEnumerable<string> EnumerateTextForGlyphs()
    {
        return _resources.Values.SelectMany(table => table.Values);
    }

    public IReadOnlyList<string> GetAvailableLanguages()
    {
        if (Manifest.Languages.Count > 0) return Manifest.Languages;
        return new[] { CurrentLanguage };
    }

    public string? GetFontForLanguage(string language)
    {
        return Manifest.Fonts.TryGetValue(language, out string? font) ? font : null;
    }
}
