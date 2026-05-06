using System;
using System.IO;
using System.Reflection;
using FluentAssertions;
using Xunit;

namespace AriaEngine.Tests;

public class FlowCheckTests
{
    [Fact]
    public void FlowCheck_Passes_WhenAllChapterRoutesAreCovered()
    {
        string root = CreateTempRoot();
        try
        {
            WriteScript(root, "assets/scripts/main.aria", """
                *start
                goto *chapter_select
                *chapter_select
                if %0 == 100 { goto *scenario_01 }
                if %0 == 101 { goto *scenario_02 }
                end
                include "scenario_01.aria"
                include "scenario_02.aria"
                """);
            WriteScript(root, "assets/scripts/scenario_01.aria", """
                *scenario_01
                set_sflag scenario_01_started, 1
                set_pflag chapter_01, 1
                nvl
                text "nvl"
                adv
                text "adv"
                set_pflag chapter_02, 1
                goto *chapter_select
                """);
            WriteScript(root, "assets/scripts/scenario_02.aria", """
                *scenario_02
                set_sflag scenario_02_started, 1
                set_pflag chapter_02, 1
                nvl
                text "nvl"
                adv
                text "adv"
                goto *scenario_02_tail
                *scenario_02_tail
                goto *chapter_select
                """);

            RunFlowCheck("--root", root, "--main", "assets/scripts/main.aria", "--chapters", "2").Should().Be(0);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void FlowCheck_Fails_WhenChapterRouteIsMissing()
    {
        string root = CreateTempRoot();
        try
        {
            WriteScript(root, "assets/scripts/main.aria", """
                *start
                goto *chapter_select
                *chapter_select
                if %0 == 100 { goto *scenario_01 }
                end
                include "scenario_01.aria"
                include "scenario_02.aria"
                """);
            WriteScript(root, "assets/scripts/scenario_01.aria", """
                *scenario_01
                set_sflag scenario_01_started, 1
                set_pflag chapter_01, 1
                nvl
                adv
                set_pflag chapter_02, 1
                goto *chapter_select
                """);
            WriteScript(root, "assets/scripts/scenario_02.aria", """
                *scenario_02
                set_sflag scenario_02_started, 1
                set_pflag chapter_02, 1
                nvl
                adv
                goto *chapter_select
                """);

            RunFlowCheck("--root", root, "--main", "assets/scripts/main.aria", "--chapters", "2").Should().Be(2);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void FlowCheck_ExecutePasses_WhenScenarioReturnsToChapterSelect()
    {
        string root = CreateTempRoot();
        try
        {
            WriteScript(root, "assets/scripts/main.aria", """
                *start
                goto *chapter_select
                *chapter_select
                btnwait %0
                if %0 == 100 { goto *scenario_01 }
                end
                include "scenario_01.aria"
                """);
            WriteScript(root, "assets/scripts/scenario_01.aria", """
                *scenario_01
                set_sflag scenario_01_started, 1
                set_pflag chapter_01, 1
                nvl
                text "nvl"
                \
                adv
                text "adv"
                goto *chapter_select
                """);

            RunFlowCheck("--root", root, "--main", "assets/scripts/main.aria", "--chapters", "1", "--execute").Should().Be(0);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void FlowCheck_ExecuteFails_WhenScenarioPanicsBeforeReturn()
    {
        string root = CreateTempRoot();
        try
        {
            WriteScript(root, "assets/scripts/main.aria", """
                *start
                goto *chapter_select
                *chapter_select
                btnwait %0
                if %0 == 100 { goto *scenario_01 }
                end
                include "scenario_01.aria"
                """);
            WriteScript(root, "assets/scripts/scenario_01.aria", """
                *scenario_01
                set_sflag scenario_01_started, 1
                set_pflag chapter_01, 1
                nvl
                panic "boom"
                adv
                goto *chapter_select
                """);

            RunFlowCheck("--root", root, "--main", "assets/scripts/main.aria", "--chapters", "1", "--execute").Should().Be(2);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    private static int RunFlowCheck(params string[] args)
    {
        var type = Type.GetType("AriaEngine.Tools.AriaFlowCheckCommand, AriaEngine");
        type.Should().NotBeNull();
        var run = type!.GetMethod("Run", BindingFlags.Public | BindingFlags.Static);
        run.Should().NotBeNull();
        return (int)run!.Invoke(null, new object[] { args })!;
    }

    private static string CreateTempRoot()
    {
        return Path.Combine(Path.GetTempPath(), "aria-flowcheck-" + Guid.NewGuid().ToString("N"));
    }

    private static void WriteScript(string root, string relativePath, string content)
    {
        string path = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }
}
