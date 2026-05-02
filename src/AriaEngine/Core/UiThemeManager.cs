namespace AriaEngine.Core;

/// <summary>
/// UIテーマを管理するクラス
/// </summary>
public class UiThemeManager
{
    private readonly GameState _state;

    public UiThemeManager(GameState state)
    {
        _state = state;
    }

    /// <summary>
    /// 指定されたテーマを適用する
    /// </summary>
    public void ApplyTheme(string themeNameRaw)
    {
        string themeName = themeNameRaw.Trim().ToLowerInvariant();

        if (themeName == "classic")
        {
            ApplyClassicTheme();
            return;
        }

        if (themeName == "soft")
        {
            ApplySoftTheme();
            return;
        }

        if (themeName == "glass")
        {
            ApplyGlassTheme();
            return;
        }

        if (themeName is "steel" or "rugged" or "hard")
        {
            ResetToDefaults();
            return;
        }

        if (themeName == "mono")
        {
            ApplyMonoTheme();
            return;
        }

        // default: clean
        ResetToDefaults();
    }

    /// <summary>
    /// デフォルトのクリーンテーマを適用する
    /// </summary>
    public void ResetToDefaults()
    {
        _state.TextWindow.DefaultTextboxCornerRadius = UIThemeDefaults.TextboxCornerRadius;
        _state.TextWindow.DefaultTextboxBorderWidth = UIThemeDefaults.TextboxBorderWidth;
        _state.TextWindow.DefaultTextboxBorderColor = UIThemeDefaults.TextboxBorderColor;
        _state.TextWindow.DefaultTextboxBorderOpacity = UIThemeDefaults.TextboxBorderOpacity;
        _state.TextWindow.DefaultTextboxShadowOffsetX = UIThemeDefaults.TextboxShadowOffsetX;
        _state.TextWindow.DefaultTextboxShadowOffsetY = UIThemeDefaults.TextboxShadowOffsetY;
        _state.TextWindow.DefaultTextboxShadowColor = UIThemeDefaults.TextboxShadowColor;
        _state.TextWindow.DefaultTextboxShadowAlpha = UIThemeDefaults.TextboxShadowAlpha;
        _state.TextWindow.DefaultTextboxPaddingX = UIThemeDefaults.TextboxPaddingX;
        _state.TextWindow.DefaultTextboxPaddingY = UIThemeDefaults.TextboxPaddingY;
        _state.TextWindow.DefaultTextboxBgColor = UIThemeDefaults.TextboxBgColor;
        _state.TextWindow.DefaultTextboxBgAlpha = UIThemeDefaults.TextboxBgAlpha;

        _state.ChoiceStyle.ChoiceWidth = UIThemeDefaults.ChoiceWidth;
        _state.ChoiceStyle.ChoiceHeight = UIThemeDefaults.ChoiceHeight;
        _state.ChoiceStyle.ChoiceSpacing = UIThemeDefaults.ChoiceSpacing;
        _state.ChoiceStyle.ChoiceFontSize = UIThemeDefaults.ChoiceFontSize;
        _state.ChoiceStyle.ChoiceBgColor = UIThemeDefaults.ChoiceBgColor;
        _state.ChoiceStyle.ChoiceBgAlpha = UIThemeDefaults.ChoiceBgAlpha;
        _state.ChoiceStyle.ChoiceTextColor = UIThemeDefaults.ChoiceTextColor;
        _state.ChoiceStyle.ChoiceCornerRadius = UIThemeDefaults.ChoiceCornerRadius;
        _state.ChoiceStyle.ChoiceBorderColor = UIThemeDefaults.ChoiceBorderColor;
        _state.ChoiceStyle.ChoiceBorderWidth = UIThemeDefaults.ChoiceBorderWidth;
        _state.ChoiceStyle.ChoiceBorderOpacity = UIThemeDefaults.ChoiceBorderOpacity;
        _state.ChoiceStyle.ChoiceHoverColor = UIThemeDefaults.ChoiceHoverColor;
        _state.ChoiceStyle.ChoicePaddingX = UIThemeDefaults.ChoicePaddingX;

        _state.MenuRuntime.MenuFillColor = UIThemeDefaults.MenuFillColor;
        _state.MenuRuntime.MenuFillAlpha = UIThemeDefaults.MenuFillAlpha;
        _state.MenuRuntime.MenuLineColor = UIThemeDefaults.MenuLineColor;
        _state.MenuRuntime.MenuTextColor = UIThemeDefaults.MenuTextColor;
        _state.MenuRuntime.MenuCornerRadius = UIThemeDefaults.MenuCornerRadius;
    }

    private void ApplyClassicTheme()
    {
        _state.TextWindow.DefaultTextboxCornerRadius = 6;
        _state.TextWindow.DefaultTextboxBorderWidth = 2;
        _state.TextWindow.DefaultTextboxBorderColor = "#d1d5db";
        _state.TextWindow.DefaultTextboxBorderOpacity = 120;
        _state.TextWindow.DefaultTextboxShadowOffsetX = 0;
        _state.TextWindow.DefaultTextboxShadowOffsetY = 2;
        _state.TextWindow.DefaultTextboxShadowColor = "#000000";
        _state.TextWindow.DefaultTextboxShadowAlpha = 120;
        _state.TextWindow.DefaultTextboxPaddingX = 22;
        _state.TextWindow.DefaultTextboxPaddingY = 18;

        _state.ChoiceStyle.ChoiceWidth = 620;
        _state.ChoiceStyle.ChoiceHeight = 60;
        _state.ChoiceStyle.ChoiceSpacing = 16;
        _state.ChoiceStyle.ChoiceFontSize = 30;
        _state.ChoiceStyle.ChoiceBgColor = "#202020";
        _state.ChoiceStyle.ChoiceBgAlpha = 240;
        _state.ChoiceStyle.ChoiceTextColor = "#ffffff";
        _state.ChoiceStyle.ChoiceCornerRadius = 6;
        _state.ChoiceStyle.ChoiceBorderColor = "#d1d5db";
        _state.ChoiceStyle.ChoiceBorderWidth = 2;
        _state.ChoiceStyle.ChoiceBorderOpacity = 120;
        _state.ChoiceStyle.ChoiceHoverColor = "#303030";
        _state.ChoiceStyle.ChoicePaddingX = 18;
    }

    private void ApplySoftTheme()
    {
        _state.TextWindow.DefaultTextboxCornerRadius = 22;
        _state.TextWindow.DefaultTextboxBorderWidth = 1;
        _state.TextWindow.DefaultTextboxBorderColor = "#b8c6d1";
        _state.TextWindow.DefaultTextboxBorderOpacity = 82;
        _state.TextWindow.DefaultTextboxShadowOffsetX = 0;
        _state.TextWindow.DefaultTextboxShadowOffsetY = 7;
        _state.TextWindow.DefaultTextboxShadowColor = "#000000";
        _state.TextWindow.DefaultTextboxShadowAlpha = 110;
        _state.TextWindow.DefaultTextboxPaddingX = 30;
        _state.TextWindow.DefaultTextboxPaddingY = 24;
        _state.TextWindow.DefaultTextboxBgColor = "#14161b";
        _state.TextWindow.DefaultTextboxBgAlpha = 202;

        _state.ChoiceStyle.ChoiceWidth = 640;
        _state.ChoiceStyle.ChoiceHeight = 62;
        _state.ChoiceStyle.ChoiceSpacing = 18;
        _state.ChoiceStyle.ChoiceFontSize = 28;
        _state.ChoiceStyle.ChoiceBgColor = "#1a2026";
        _state.ChoiceStyle.ChoiceBgAlpha = 222;
        _state.ChoiceStyle.ChoiceTextColor = "#f7f5ef";
        _state.ChoiceStyle.ChoiceCornerRadius = 20;
        _state.ChoiceStyle.ChoiceBorderColor = "#d2b982";
        _state.ChoiceStyle.ChoiceBorderWidth = 1;
        _state.ChoiceStyle.ChoiceBorderOpacity = 84;
        _state.ChoiceStyle.ChoiceHoverColor = "#2b3339";
        _state.ChoiceStyle.ChoicePaddingX = 24;

        _state.MenuRuntime.MenuFillColor = "#14161b";
        _state.MenuRuntime.MenuFillAlpha = 232;
        _state.MenuRuntime.MenuLineColor = "#d2b982";
        _state.MenuRuntime.MenuTextColor = "#f7f5ef";
        _state.MenuRuntime.MenuCornerRadius = 22;
    }

    private void ApplyGlassTheme()
    {
        _state.TextWindow.DefaultTextboxCornerRadius = 24;
        _state.TextWindow.DefaultTextboxBorderWidth = 1;
        _state.TextWindow.DefaultTextboxBorderColor = "#9ad9d4";
        _state.TextWindow.DefaultTextboxBorderOpacity = 96;
        _state.TextWindow.DefaultTextboxShadowOffsetX = 0;
        _state.TextWindow.DefaultTextboxShadowOffsetY = 8;
        _state.TextWindow.DefaultTextboxShadowColor = "#000000";
        _state.TextWindow.DefaultTextboxShadowAlpha = 132;
        _state.TextWindow.DefaultTextboxPaddingX = 34;
        _state.TextWindow.DefaultTextboxPaddingY = 26;
        _state.TextWindow.DefaultTextboxBgColor = "#0d171b";
        _state.TextWindow.DefaultTextboxBgAlpha = 188;

        _state.MenuRuntime.MenuFillColor = "#0d171b";
        _state.MenuRuntime.MenuFillAlpha = 224;
        _state.MenuRuntime.MenuLineColor = "#9ad9d4";
        _state.MenuRuntime.MenuTextColor = "#f4f7f8";
        _state.MenuRuntime.MenuCornerRadius = 18;
        _state.ChoiceStyle.ChoiceBgColor = "#132329";
        _state.ChoiceStyle.ChoiceBgAlpha = 218;
        _state.ChoiceStyle.ChoiceBorderColor = "#9ad9d4";
        _state.ChoiceStyle.ChoiceBorderOpacity = 96;
        _state.ChoiceStyle.ChoiceHoverColor = "#254148";
    }

    private void ApplyMonoTheme()
    {
        _state.TextWindow.DefaultTextboxCornerRadius = 0;
        _state.TextWindow.DefaultTextboxBorderWidth = 1;
        _state.TextWindow.DefaultTextboxBorderColor = "#ffffff";
        _state.TextWindow.DefaultTextboxBorderOpacity = 255;
        _state.TextWindow.DefaultTextboxShadowOffsetX = 2;
        _state.TextWindow.DefaultTextboxShadowOffsetY = 2;
        _state.TextWindow.DefaultTextboxShadowColor = "#ffffff";
        _state.TextWindow.DefaultTextboxShadowAlpha = 120;
        _state.TextWindow.DefaultTextboxPaddingX = 20;
        _state.TextWindow.DefaultTextboxPaddingY = 20;
        _state.TextWindow.DefaultTextboxBgColor = "#000000";
        _state.TextWindow.DefaultTextboxBgAlpha = 240;

        _state.ChoiceStyle.ChoiceWidth = 600;
        _state.ChoiceStyle.ChoiceHeight = 50;
        _state.ChoiceStyle.ChoiceSpacing = 12;
        _state.ChoiceStyle.ChoiceFontSize = 28;
        _state.ChoiceStyle.ChoiceBgColor = "#000000";
        _state.ChoiceStyle.ChoiceBgAlpha = 255;
        _state.ChoiceStyle.ChoiceTextColor = "#ffffff";
        _state.ChoiceStyle.ChoiceCornerRadius = 0;
        _state.ChoiceStyle.ChoiceBorderColor = "#ffffff";
        _state.ChoiceStyle.ChoiceBorderWidth = 1;
        _state.ChoiceStyle.ChoiceBorderOpacity = 255;
        _state.ChoiceStyle.ChoiceHoverColor = "#333333";
        _state.ChoiceStyle.ChoicePaddingX = 20;

        _state.MenuRuntime.MenuFillColor = "#000000";
        _state.MenuRuntime.MenuFillAlpha = 238;
        _state.MenuRuntime.MenuLineColor = "#ffffff";
        _state.MenuRuntime.MenuTextColor = "#ffffff";
        _state.MenuRuntime.MenuCornerRadius = 0;
    }
}
