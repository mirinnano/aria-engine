using System;
using FluentAssertions;
using Xunit;
using AriaEngine.Core;

namespace AriaEngine.Tests;

public sealed class UiThemeManagerTests
{
    private static GameState NewState() => new GameState();

    // T2 UX Quick Wins: 各テーマは ButtonFeel を構成し、テーマ固有の値を設定する

    [Fact]
    public void ApplyTheme_Classic_ConfiguresButtonFeel()
    {
        var state = NewState();
        var manager = new UiThemeManager(state);

        manager.ApplyTheme("classic");

        state.ButtonFeel.Should().NotBeNull();
        state.ButtonFeel.HoverColor.Should().Be("#303030");
        state.ButtonFeel.PressedColor.Should().Be("#181818");
        state.ButtonFeel.PressedOffsetY.Should().Be(1.5f);
        state.ButtonFeel.PressedScale.Should().Be(0.97f);
    }

    [Fact]
    public void ApplyTheme_Soft_ConfiguresButtonFeel_WithLargerSink()
    {
        var state = NewState();
        var manager = new UiThemeManager(state);

        manager.ApplyTheme("soft");

        state.ButtonFeel.Should().NotBeNull();
        // Soft テーマは押下感がやや大きめ（押しやすさ重視）
        state.ButtonFeel.PressedOffsetY.Should().Be(1.8f);
        state.ButtonFeel.PressedScale.Should().Be(0.96f);
    }

    [Fact]
    public void ApplyTheme_Glass_ConfiguresButtonFeel_WithShortAnimation()
    {
        var state = NewState();
        var manager = new UiThemeManager(state);

        manager.ApplyTheme("glass");

        state.ButtonFeel.Should().NotBeNull();
        state.ButtonFeel.HoverColor.Should().Be("#254148");
        state.ButtonFeel.AnimationDurationMs.Should().Be(80f);
    }

    [Fact]
    public void ApplyTheme_Mono_ConfiguresButtonFeel_WithSharpSubtleSink()
    {
        var state = NewState();
        var manager = new UiThemeManager(state);

        manager.ApplyTheme("mono");

        state.ButtonFeel.Should().NotBeNull();
        // Mono テーマはシャープさを保つため沈み込み小さめ
        state.ButtonFeel.PressedOffsetY.Should().Be(1.0f);
        state.ButtonFeel.PressedScale.Should().Be(0.98f);
        state.ButtonFeel.AnimationDurationMs.Should().Be(60f);
        state.ButtonFeel.PressedColor.Should().Be("#000000");
    }

    [Fact]
    public void ResetToDefaults_ResetsButtonFeel_ToFactoryDefaults()
    {
        var state = NewState();
        state.ButtonFeel = new ButtonFeel
        {
            HoverColor = "#ff00ff",
            PressedColor = "#00ffff",
            PressedOffsetY = 10f,
            PressedScale = 0.5f
        };
        var manager = new UiThemeManager(state);

        manager.ResetToDefaults();

        // デフォルトの ButtonFeel に戻る（PressedOffsetY = 1.5f, PressedScale = 0.97f）
        state.ButtonFeel.PressedOffsetY.Should().Be(1.5f);
        state.ButtonFeel.PressedScale.Should().Be(0.97f);
        state.ButtonFeel.HoverColor.Should().Be("");
        state.ButtonFeel.PressedColor.Should().Be("");
    }

    [Fact]
    public void ApplyTheme_AllThemes_ProduceNonDefaultButtonFeel()
    {
        // 全テーマで ButtonFeel がテーマ固有の HoverColor を持つことを確認
        // (ResetToDefaults = "" と区別するため)
        var themes = new[] { "classic", "soft", "glass", "mono" };
        foreach (var theme in themes)
        {
            var state = NewState();
            var manager = new UiThemeManager(state);
            manager.ApplyTheme(theme);
            state.ButtonFeel.HoverColor.Should().NotBeNullOrEmpty(
                $"テーマ '{theme}' は ButtonFeel.HoverColor を設定するべき");
        }
    }
}
