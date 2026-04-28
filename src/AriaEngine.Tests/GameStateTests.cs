using System;
using FluentAssertions;
using Xunit;
using AriaEngine.Core;

namespace AriaEngine.Tests;

public sealed class GameStateTests
{
    [Fact]
    public void GetReadRate_NoScriptLines_ReturnsZero()
    {
        var state = new GameState();
        state.TotalScriptLines = 0;

        var rate = state.GetReadRate();

        rate.Should().Be(0);
    }

    [Fact]
    public void GetReadRate_NoReadKeys_ReturnsZero()
    {
        var state = new GameState();
        state.TotalScriptLines = 100;

        var rate = state.GetReadRate();

        rate.Should().Be(0);
    }

    [Fact]
    public void GetReadRate_HalfRead_ReturnsFifty()
    {
        var state = new GameState();
        state.TotalScriptLines = 100;
        // Simulate ReadKeys with 50 entries
        for (int i = 0; i < 50; i++)
        {
            state.ReadKeys.Add($"line_{i}");
        }

        var rate = state.GetReadRate();

        rate.Should().Be(50);
    }

    [Fact]
    public void GetReadRate_AllRead_ReturnsHundred()
    {
        var state = new GameState();
        state.TotalScriptLines = 100;
        for (int i = 0; i < 100; i++)
        {
            state.ReadKeys.Add($"line_{i}");
        }

        var rate = state.GetReadRate();

        rate.Should().Be(100);
    }

    [Fact]
    public void GetReadRate_SomeRead_ReturnsCorrectPercentage()
    {
        var state = new GameState();
        state.TotalScriptLines = 33;
        for (int i = 0; i < 11; i++)
        {
            state.ReadKeys.Add($"line_{i}");
        }

        var rate = state.GetReadRate();

        // 11/33 = 33.33...% rounded to 33%
        rate.Should().Be(33);
    }

    [Fact]
    public void UnlockCg_ValidId_AddsToUnlockedCgs()
    {
        var state = new GameState();

        state.UnlockCg("cg_001");

        state.UnlockedCgs.Should().Contain("cg_001");
    }

    [Fact]
    public void UnlockCg_EmptyId_DoesNotAdd()
    {
        var state = new GameState();
        var initialCount = state.UnlockedCgs.Count;

        state.UnlockCg("");

        state.UnlockedCgs.Count.Should().Be(initialCount);
    }

    [Fact]
    public void UnlockCg_WhitespaceId_DoesNotAdd()
    {
        var state = new GameState();
        var initialCount = state.UnlockedCgs.Count;

        state.UnlockCg("   ");

        state.UnlockedCgs.Count.Should().Be(initialCount);
    }

    [Fact]
    public void IsCgUnlocked_UnlockedCg_ReturnsTrue()
    {
        var state = new GameState();
        state.UnlockCg("cg_001");

        var result = state.IsCgUnlocked("cg_001");

        result.Should().BeTrue();
    }

    [Fact]
    public void IsCgUnlocked_LockedCg_ReturnsFalse()
    {
        var state = new GameState();

        var result = state.IsCgUnlocked("cg_999");

        result.Should().BeFalse();
    }

    [Fact]
    public void GetCgUnlockRate_ZeroTotal_ReturnsZero()
    {
        var state = new GameState();

        var rate = state.GetCgUnlockRate(0);

        rate.Should().Be(0);
    }

    [Fact]
    public void GetCgUnlockRate_NoUnlocked_ReturnsZero()
    {
        var state = new GameState();

        var rate = state.GetCgUnlockRate(10);

        rate.Should().Be(0);
    }

    [Fact]
    public void GetCgUnlockRate_HalfUnlocked_ReturnsFifty()
    {
        var state = new GameState();
        state.UnlockCg("cg_001");
        state.UnlockCg("cg_002");
        state.UnlockCg("cg_003");
        state.UnlockCg("cg_004");
        state.UnlockCg("cg_005");

        var rate = state.GetCgUnlockRate(10);

        rate.Should().Be(50);
    }

    [Fact]
    public void GetCgUnlockRate_AllUnlocked_ReturnsHundred()
    {
        var state = new GameState();
        state.UnlockCg("cg_001");
        state.UnlockCg("cg_002");

        var rate = state.GetCgUnlockRate(2);

        rate.Should().Be(100);
    }

    [Fact]
    public void GetCgUnlockRate_SomeUnlocked_ReturnsCorrectPercentage()
    {
        var state = new GameState();
        state.UnlockCg("cg_001");
        state.UnlockCg("cg_002");
        state.UnlockCg("cg_003");

        var rate = state.GetCgUnlockRate(9);

        // 3/9 = 33.33...% rounded to 33%
        rate.Should().Be(33);
    }

    [Fact]
    public void ReadKeys_TracksReadLinesCorrectly()
    {
        var state = new GameState();
        state.ReadKeys.Should().BeEmpty();

        state.ReadKeys.Add("script.aria:10");
        state.ReadKeys.Add("script.aria:20");
        state.ReadKeys.Add("script.aria:30");

        state.ReadKeys.Count.Should().Be(3);
        state.ReadKeys.Should().Contain("script.aria:10");
        state.ReadKeys.Should().Contain("script.aria:20");
        state.ReadKeys.Should().Contain("script.aria:30");
    }

    [Fact]
    public void ReadKeys_DoesNotDuplicate()
    {
        var state = new GameState();

        state.ReadKeys.Add("script.aria:10");
        state.ReadKeys.Add("script.aria:10");
        state.ReadKeys.Add("script.aria:10");

        state.ReadKeys.Count.Should().Be(1);
    }

    [Fact]
    public void TotalScriptLines_CanBeSetAndRetrieved()
    {
        var state = new GameState();
        state.TotalScriptLines = 500;

        state.TotalScriptLines.Should().Be(500);
    }

    [Fact]
    public void UnlockedCgs_IsCaseInsensitive()
    {
        var state = new GameState();
        state.UnlockCg("CG_001");

        state.IsCgUnlocked("cg_001").Should().BeTrue();
        state.IsCgUnlocked("CG_001").Should().BeTrue();
        state.IsCgUnlocked("Cg_001").Should().BeTrue();
    }

    [Fact]
    public void ReadKeys_IsCaseInsensitive()
    {
        var state = new GameState();
        state.TotalScriptLines = 10;
        state.ReadKeys.Add("SCRIPT.ARIA:10");

        state.ReadKeys.Contains("script.aria:10").Should().BeTrue();
        state.ReadKeys.Contains("Script.aria:10").Should().BeTrue();
    }
}