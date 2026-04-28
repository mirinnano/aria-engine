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
        _state.DefaultTextboxBorderColor = "#ffffff";
        _state.DefaultTextboxBorderOpacity = 70;
        _state.DefaultTextboxShadowOffsetX = 0;
        _state.DefaultTextboxShadowOffsetY = 5;
        _state.DefaultTextboxShadowColor = "#000000";
        _state.DefaultTextboxShadowAlpha = 90;
        _state.DefaultTextboxPaddingX = 30;
        _state.DefaultTextboxPaddingY = 24;

        _state.ChoiceWidth = 640;
        _state.ChoiceHeight = 62;
        _state.ChoiceSpacing = 18;
        _state.ChoiceFontSize = 28;
        _state.ChoiceBgColor = "#101010";
        _state.ChoiceBgAlpha = 220;
        _state.ChoiceTextColor = "#ffffff";
        _state.ChoiceCornerRadius = 20;
        _state.ChoiceBorderColor = "#ffffff";
        _state.ChoiceBorderWidth = 1;
        _state.ChoiceBorderOpacity = 70;
        _state.ChoiceHoverColor = "#242424";
        _state.ChoicePaddingX = 24;
    }

    private void ApplyGlassTheme()
    {
        _state.DefaultTextboxCornerRadius = 26;
        _state.DefaultTextboxBorderWidth = 1;
        _state.DefaultTextboxBorderColor = "#ffffff";
        _state.DefaultTextboxBorderOpacity = 110;
        _state.DefaultTextboxShadowOffsetX = 0;
        _state.DefaultTextboxShadowOffsetY = 8;
        _state.DefaultTextboxShadowColor = "#000000";
        _state.DefaultTextboxShadowAlpha = 120;
        _state.DefaultTextboxPaddingX = 34;
        _state.DefaultTextboxPaddingY = 26;
        _state.DefaultTextboxBgColor = "#050505";
        _state.DefaultTextboxBgAlpha = 168;

        _state.MenuFillColor = "#050505";
        _state.MenuFillAlpha = 218;
        _state.MenuLineColor = "#ffffff";
        _state.MenuTextColor = "#ffffff";
        _state.MenuCornerRadius = 24;
        _state.ChoiceHoverColor = "#2b2b2b";
    }

    private void ApplyMonoTheme()
    {
        _state.MenuFillColor = "#000000";
        _state.MenuFillAlpha = 238;
        _state.MenuLineColor = "#ffffff";
        _state.MenuTextColor = "#ffffff";
        _state.MenuCornerRadius = 16;
    }
}
