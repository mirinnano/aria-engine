using System;
using System.IO;
using FluentAssertions;
using Xunit;

namespace AriaEngine.Tests;

public sealed class DemoFlowScriptTests
{
    private static string RepoRoot => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    [Fact]
    public void Scenario05_BranchesToDemoEndBeforeUnlockingDay5()
    {
        string scenario = File.ReadAllText(Path.Combine(RepoRoot, "src", "AriaEngine", "assets", "scripts", "scenario_05.aria"));

        scenario.Should().Contain("getprofile $runtime_profile");
        scenario.Should().Contain("if $runtime_profile == \"demo\" { goto *demo_end }");
        scenario.IndexOf("if $runtime_profile == \"demo\" { goto *demo_end }", StringComparison.Ordinal)
            .Should().BeLessThan(scenario.IndexOf("set_pflag chapter_06, 1", StringComparison.Ordinal));
    }

    [Fact]
    public void MainScript_DefinesLocalizedDemoEndWithBrowserOpenActions()
    {
        string main = File.ReadAllText(Path.Combine(RepoRoot, "src", "AriaEngine", "assets", "scripts", "main.aria"));

        main.Should().Contain("*demo_end");
        main.Should().Contain("set_pflag demo_clear, 1");
        main.Should().Contain("browser_open");
        main.Should().Contain("promo.share.demo_clear.text");
        main.Should().Contain("promo.links.steam");
        main.Should().Contain("promo.links.x");
        main.Should().Contain("browser_open $demo_x_url");
        main.Should().NotContain("ponkotusoft.example");
    }

    [Fact]
    public void LocalizationResources_UseKnownOfficialAndXLinks()
    {
        string i18nDir = Path.Combine(RepoRoot, "src", "AriaEngine", "assets", "i18n");
        foreach (string file in Directory.EnumerateFiles(i18nDir, "ui.*.json"))
        {
            string json = File.ReadAllText(file);

            json.Should().Contain("https://x.com/ponkotusoft");
            json.Should().Contain("https://ponkotsu-soft.vercel.app/");
            json.Should().NotContain("ponkotusoft.example");
        }
    }
}
