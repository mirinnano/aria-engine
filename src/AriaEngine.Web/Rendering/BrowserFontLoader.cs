#nullable enable

using AriaEngine.Core;

namespace AriaEngine.Web.Rendering;

public sealed record BrowserFontFace(string Family, string SourceUrl)
{
    public string CssDeclaration => $"font-family: '{Family}'; src: url('{SourceUrl}');";
}

public static class BrowserFontLoader
{
    public static BrowserFontFace Resolve(LocalizationManager localization, string fallbackFontPath)
    {
        string? localeFont = localization.GetFontForLanguage(localization.CurrentLanguage);
        string source = NormalizeAssetUrl(string.IsNullOrWhiteSpace(localeFont) ? fallbackFontPath : localeFont);
        return new BrowserFontFace("AriaRuntime", source);
    }

    private static string NormalizeAssetUrl(string path)
    {
        return path.Replace('\\', '/').TrimStart('/');
    }
}
