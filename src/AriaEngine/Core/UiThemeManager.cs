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
        _state.DefaultTextboxCornerRadius = UIThemeDefaults.TextboxCornerRadius;
        _state.DefaultTextboxBorderWidth = UIThemeDefaults.TextboxBorderWidth;
        _state.DefaultTextboxBorderColor = UIThemeDefaults.TextboxBorderColor;
        _state.DefaultTextboxBorderOpacity = UIThemeDefaults.TextboxBorderOpacity;
        _state.DefaultTextboxShadowOffsetX = UIThemeDefaults.TextboxShadowOffsetX;
        _state.DefaultTextboxShadowOffsetY = UIThemeDefaults.TextboxShadowOffsetY;
        _state.DefaultTextboxShadowColor = UIThemeDefaults.TextboxShadowColor;
        _state.DefaultTextboxShadowAlpha = UIThemeDefaults.TextboxShadowAlpha;
        _state.DefaultTextboxPaddingX = UIThemeDefaults.TextboxPaddingX;
        _state.DefaultTextboxPaddingY = UIThemeDefaults.TextboxPaddingY;
        _state.DefaultTextboxBgColor = UIThemeDefaults.TextboxBgColor;
        _state.DefaultTextboxBgAlpha = UIThemeDefaults.TextboxBgAlpha;

        _state.ChoiceWidth = UIThemeDefaults.ChoiceWidth;
        _state.ChoiceHeight = UIThemeDefaults.ChoiceHeight;
        _state.ChoiceSpacing = UIThemeDefaults.ChoiceSpacing;
        _state.ChoiceFontSize = UIThemeDefaults.ChoiceFontSize;
        _state.ChoiceBgColor = UIThemeDefaults.ChoiceBgColor;
        _state.ChoiceBgAlpha = UIThemeDefaults.ChoiceBgAlpha;
        _state.ChoiceTextColor = UIThemeDefaults.ChoiceTextColor;
        _state.ChoiceCornerRadius = UIThemeDefaults.ChoiceCornerRadius;
        _state.ChoiceBorderColor = UIThemeDefaults.ChoiceBorderColor;
        _state.ChoiceBorderWidth = UIThemeDefaults.ChoiceBorderWidth;
        _state.ChoiceBorderOpacity = UIThemeDefaults.ChoiceBorderOpacity;
        _state.ChoiceHoverColor = UIThemeDefaults.ChoiceHoverColor;
        _state.ChoicePaddingX = UIThemeDefaults.ChoicePaddingX;

        _state.MenuFillColor = UIThemeDefaults.MenuFillColor;
        _state.MenuFillAlpha = UIThemeDefaults.MenuFillAlpha;
        _state.MenuLineColor = UIThemeDefaults.MenuLineColor;
        _state.MenuTextColor = UIThemeDefaults.MenuTextColor;
        _state.MenuCornerRadius = UIThemeDefaults.MenuCornerRadius;
    }

    private void ApplyClassicTheme()
    {
        _state.DefaultTextboxCornerRadius = 6;
        _state.DefaultTextboxBorderWidth = 2;
        _state.DefaultTextboxBorderColor = "#d1d5db";
        _state.DefaultTextboxBorderOpacity = 120;
        _state.DefaultTextboxShadowOffsetX = 0;
        _state.DefaultTextboxShadowOffsetY = 2;
        _state.DefaultTextboxShadowColor = "#000000";
        _state.DefaultTextboxShadowAlpha = 120;
        _state.DefaultTextboxPaddingX = 22;
        _state.DefaultTextboxPaddingY = 18;

        _state.ChoiceWidth = 620;
        _state.ChoiceHeight = 60;
        _state.ChoiceSpacing = 16;
        _state.ChoiceFontSize = 30;
        _state.ChoiceBgColor = "#202020";
        _state.ChoiceBgAlpha = 240;
        _state.ChoiceTextColor = "#ffffff";
        _state.ChoiceCornerRadius = 6;
        _state.ChoiceBorderColor = "#d1d5db";
        _state.ChoiceBorderWidth = 2;
        _state.ChoiceBorderOpacity = 120;
        _state.ChoiceHoverColor = "#303030";
        _state.ChoicePaddingX = 18;
    }

    private void ApplySoftTheme()
    {
        _state.DefaultTextboxCornerRadius = 22;
        _state.DefaultTextboxBorderWidth = 1;
        _state.DefaultTextboxBorderColor = "#b8c6d1";
        _state.DefaultTextboxBorderOpacity = 82;
        _state.DefaultTextboxShadowOffsetX = 0;
        _state.DefaultTextboxShadowOffsetY = 7;
        _state.DefaultTextboxShadowColor = "#000000";
        _state.DefaultTextboxShadowAlpha = 110;
        _state.DefaultTextboxPaddingX = 30;
        _state.DefaultTextboxPaddingY = 24;
        _state.DefaultTextboxBgColor = "#14161b";
        _state.DefaultTextboxBgAlpha = 202;

        _state.ChoiceWidth = 640;
        _state.ChoiceHeight = 62;
        _state.ChoiceSpacing = 18;
        _state.ChoiceFontSize = 28;
        _state.ChoiceBgColor = "#1a2026";
        _state.ChoiceBgAlpha = 222;
        _state.ChoiceTextColor = "#f7f5ef";
        _state.ChoiceCornerRadius = 20;
        _state.ChoiceBorderColor = "#d2b982";
        _state.ChoiceBorderWidth = 1;
        _state.ChoiceBorderOpacity = 84;
        _state.ChoiceHoverColor = "#2b3339";
        _state.ChoicePaddingX = 24;

        _state.MenuFillColor = "#14161b";
        _state.MenuFillAlpha = 232;
        _state.MenuLineColor = "#d2b982";
        _state.MenuTextColor = "#f7f5ef";
        _state.MenuCornerRadius = 22;
    }

    private void ApplyGlassTheme()
    {
        _state.DefaultTextboxCornerRadius = 24;
        _state.DefaultTextboxBorderWidth = 1;
        _state.DefaultTextboxBorderColor = "#9ad9d4";
        _state.DefaultTextboxBorderOpacity = 96;
        _state.DefaultTextboxShadowOffsetX = 0;
        _state.DefaultTextboxShadowOffsetY = 8;
        _state.DefaultTextboxShadowColor = "#000000";
        _state.DefaultTextboxShadowAlpha = 132;
        _state.DefaultTextboxPaddingX = 34;
        _state.DefaultTextboxPaddingY = 26;
        _state.DefaultTextboxBgColor = "#0d171b";
        _state.DefaultTextboxBgAlpha = 188;

        _state.MenuFillColor = "#0d171b";
        _state.MenuFillAlpha = 224;
        _state.MenuLineColor = "#9ad9d4";
        _state.MenuTextColor = "#f4f7f8";
        _state.MenuCornerRadius = 18;
        _state.ChoiceBgColor = "#132329";
        _state.ChoiceBgAlpha = 218;
        _state.ChoiceBorderColor = "#9ad9d4";
        _state.ChoiceBorderOpacity = 96;
        _state.ChoiceHoverColor = "#254148";
    }

    private void ApplyMonoTheme()
    {
        _state.DefaultTextboxCornerRadius = 0;
        _state.DefaultTextboxBorderWidth = 1;
        _state.DefaultTextboxBorderColor = "#ffffff";
        _state.DefaultTextboxBorderOpacity = 255;
        _state.DefaultTextboxShadowOffsetX = 2;
        _state.DefaultTextboxShadowOffsetY = 2;
        _state.DefaultTextboxShadowColor = "#ffffff";
        _state.DefaultTextboxShadowAlpha = 120;
        _state.DefaultTextboxPaddingX = 20;
        _state.DefaultTextboxPaddingY = 20;
        _state.DefaultTextboxBgColor = "#000000";
        _state.DefaultTextboxBgAlpha = 240;

        _state.ChoiceWidth = 600;
        _state.ChoiceHeight = 50;
        _state.ChoiceSpacing = 12;
        _state.ChoiceFontSize = 28;
        _state.ChoiceBgColor = "#000000";
        _state.ChoiceBgAlpha = 255;
        _state.ChoiceTextColor = "#ffffff";
        _state.ChoiceCornerRadius = 0;
        _state.ChoiceBorderColor = "#ffffff";
        _state.ChoiceBorderWidth = 1;
        _state.ChoiceBorderOpacity = 255;
        _state.ChoiceHoverColor = "#333333";
        _state.ChoicePaddingX = 20;

        _state.MenuFillColor = "#000000";
        _state.MenuFillAlpha = 238;
        _state.MenuLineColor = "#ffffff";
        _state.MenuTextColor = "#ffffff";
        _state.MenuCornerRadius = 0;
    }
}
