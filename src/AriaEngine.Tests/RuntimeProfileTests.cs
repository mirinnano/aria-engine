using AriaEngine.Core;
using FluentAssertions;
using Xunit;

namespace AriaEngine.Tests;

public sealed class RuntimeProfileTests
{
    [Fact]
    public void ParseRunOptions_DefaultsToDebugProfile()
    {
        var options = Program.ParseRunOptions([], new ErrorReporter());

        options.Profile.Should().Be(RuntimeProfile.Debug);
    }

    [Theory]
    [InlineData("debug", RuntimeProfile.Debug)]
    [InlineData("demo", RuntimeProfile.Demo)]
    [InlineData("release", RuntimeProfile.Release)]
    public void ParseRunOptions_ParsesExplicitProfile(string value, RuntimeProfile expected)
    {
        var options = Program.ParseRunOptions(["--profile", value], new ErrorReporter());

        options.Profile.Should().Be(expected);
        options.ProfileExplicit.Should().BeTrue();
    }

    [Fact]
    public void ApplyRuntimeProfilePolicy_InjectsBrowserAllowlistForProductionProfiles()
    {
        var settings = new EngineSettingsState();
        var options = new Program.RunOptions { Profile = RuntimeProfile.Demo };

        Program.ApplyRuntimeProfilePolicy(settings, options);

        settings.ProductionMode.Should().BeTrue();
        settings.BrowserOpenAllowlist.Should().Contain("store.steampowered.com");
        settings.BrowserOpenAllowlist.Should().Contain("twitter.com");
        settings.BrowserOpenAllowlist.Should().Contain("x.com");
        settings.BrowserOpenAllowlist.Should().Contain("ponkotsu-soft.vercel.app");
        settings.BrowserOpenAllowlist.Should().NotContain("ponkotusoft.example");
    }
}
