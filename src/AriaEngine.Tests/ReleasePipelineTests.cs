using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using FluentAssertions;
using Xunit;

namespace AriaEngine.Tests;

public class ReleasePipelineTests
{
    private static string RepoRoot => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    [Fact]
    public void PackageScript_RecordsReleaseNotesAndSigningStateInManifest()
    {
        string script = File.ReadAllText(Path.Combine(RepoRoot, "scripts", "package.ps1"));

        script.Should().Contain("README.md");
        script.Should().Contain("docs/release/package-readme.md");
        script.Should().Contain("releaseNotes");
        script.Should().Contain("signing");
        script.Should().Contain("signed");
        script.Should().Contain("trusted");
        script.Should().Contain("Get-AuthenticodeSignature");
        script.Should().Contain("$compileArgs += @(\"--key\", $env:ARIA_PACK_KEY)");
        script.Should().Contain("$packArgs += @(\"--key\", $env:ARIA_PACK_KEY)");
    }

    [Fact]
    public void PackageScript_RemovesRawScriptsBeforeBuildingProductionPak()
    {
        string script = File.ReadAllText(Path.Combine(RepoRoot, "scripts", "package.ps1"));

        script.Should().Contain("$rawScriptsOut = Join-Path $publishDir \"assets\\scripts\"");
        script.Should().Contain("Remove-Item -LiteralPath $rawScriptsOut -Recurse -Force");
        script.Should().Contain("$rawInitOut = Join-Path $publishDir $InitScript");
        script.Should().Contain("Remove-Item -LiteralPath $rawInitOut -Force");
        script.IndexOf("$rawScriptsOut = Join-Path $publishDir \"assets\\scripts\"", StringComparison.Ordinal)
            .Should().BeLessThan(script.IndexOf("Invoke-Checked dotnet $dotnetPackArgs $publishDir", StringComparison.Ordinal));
    }

    [Fact]
    public void PackageZip_DoesNotIncludeItsOwnDistDirectory()
    {
        string script = File.ReadAllText(Path.Combine(RepoRoot, "scripts", "package.ps1"));

        script.Should().Contain("Compress-Archive -Path (Join-Path $publishDir \"*\") -DestinationPath $zipPath -Force");
        script.Should().NotContain("Compress-Archive -Path (Join-Path $releaseDir \"*\")");
    }

    [Fact]
    public void PackageScript_PublishesToTemporaryStagingDirectory()
    {
        string script = File.ReadAllText(Path.Combine(RepoRoot, "scripts", "package.ps1"));

        script.Should().Contain("$publishStageDir");
        script.Should().Contain("[IO.Path]::GetTempPath()");
        script.Should().Contain("-o\", $publishStageDir");
        script.Should().Contain("Copy-Item -Path (Join-Path $publishStageDir \"*\") -Destination $publishDir -Recurse -Force");
        script.Should().Contain("Remove-Item -LiteralPath $publishStageDir -Recurse -Force");
    }

    [Fact]
    public void ReleaseScript_UsesStrictDoctorForReleaseCandidates()
    {
        string script = File.ReadAllText(Path.Combine(RepoRoot, "scripts", "release.ps1"));

        script.Should().Contain("$doctorArgs.Strict = $true");
        script.Should().Contain("v1\\.0\\.0-rc");
    }

    [Fact]
    public void CiWorkflow_BuildsInstallerFromVersionedReleasePackage()
    {
        string workflow = File.ReadAllText(Path.Combine(RepoRoot, ".github", "workflows", "aria-cicd.yml"));

        workflow.Should().Contain("scripts/release.ps1");
        workflow.Should().Contain("scripts/installer.ps1");
        workflow.Should().Contain("AriaEngine-$version-win-x64");
        workflow.Should().Contain("choco install nsis");
    }

    [Fact]
    public void CicdScript_DelegatesProductionPackagingToPackageScript()
    {
        string script = File.ReadAllText(Path.Combine(RepoRoot, "scripts", "cicd.ps1"));

        script.Should().Contain("package.ps1");
        script.Should().NotContain("\"aria-pack\", \"build\"");
        script.Should().NotContain("\"aria-compile\", \"--init\"");
    }

    [Fact]
    public void VisualCompareGate_FailsWhenNoBaselineImagesExist()
    {
        string script = File.ReadAllText(Path.Combine(RepoRoot, "scripts", "visual-compare.ps1"));

        script.Should().Contain("[switch]$AllowEmpty");
        script.Should().Contain("tests/visual-regression/baseline");
        script.Should().Contain("No baseline images found");
        script.Should().Contain("-not $AllowEmpty");
    }

    [Fact]
    public void VisualRegressionScript_CanCapturePackagedLaunch()
    {
        string script = File.ReadAllText(Path.Combine(RepoRoot, "scripts", "visual-regression.ps1"));

        script.Should().Contain("[switch]$CaptureLaunch");
        script.Should().Contain("tests/visual-regression/baseline");
        script.Should().Contain("GetClientRect");
        script.Should().Contain("ClientToScreen");
        script.Should().Contain("CopyFromScreen");
        script.Should().Contain("StabilizeSeconds");
        script.Should().Contain("MinNonBlankRatio");
        script.Should().Contain("Packaged app exited before visual capture");
    }

    [Fact]
    public void VisualRegressionBaseline_IsTrackedForReleaseCompare()
    {
        string baselineDir = Path.Combine(RepoRoot, "tests", "visual-regression", "baseline");
        foreach (var fileName in new[]
        {
            "title-screen.png",
            "config-screen.png",
            "extra-screen.png",
            "gallery-screen.png",
            "chapter-select.png",
            "nvl-screen.png",
            "adv-screen.png",
            "right-menu.png",
            "save-menu.png",
            "load-menu.png",
            "backlog-menu.png"
        })
        {
            string baseline = Path.Combine(baselineDir, fileName);
            File.Exists(baseline).Should().BeTrue($"{fileName} should be tracked for release visual compare");
            new FileInfo(baseline).Length.Should().BeGreaterThan(0);
        }
    }

    [Fact]
    public void VisualRegressionScript_CanCaptureScriptOwnedUiFlow()
    {
        string script = File.ReadAllText(Path.Combine(RepoRoot, "scripts", "visual-regression.ps1"));

        script.Should().Contain("[switch]$CaptureUiFlow");
        script.Should().Contain("Write-VisualPersistentState");
        script.Should().Contain("config-screen.png");
        script.Should().Contain("gallery-screen.png");
        script.Should().Contain("chapter-select.png");
        script.Should().Contain("nvl-screen.png");
        script.Should().Contain("adv-screen.png");
        script.Should().Contain("right-menu.png");
        script.Should().Contain("save-menu.png");
        script.Should().Contain("load-menu.png");
        script.Should().Contain("backlog-menu.png");
        script.Should().Contain("ClickClient");
        script.Should().Contain("CaptureClient");
    }

    [Fact]
    public void TitleScript_ClearsTitleUiBeforeOpeningScriptOwnedUtilityScreens()
    {
        string script = File.ReadAllText(Path.Combine(RepoRoot, "src", "AriaEngine", "assets", "scripts", "main.aria"));

        script.Should().Contain("func title_ui_clear()");
        script.IndexOf("if %_title_choice == 3 { title_ui_clear() }", StringComparison.Ordinal)
            .Should().BeLessThan(script.IndexOf("if %_title_choice == 3 { settings_ui() }", StringComparison.Ordinal));
        script.IndexOf("if %_title_choice == 4 { title_ui_clear() }", StringComparison.Ordinal)
            .Should().BeLessThan(script.IndexOf("if %_title_choice == 4 { omake_ui() }", StringComparison.Ordinal));
    }

    [Fact]
    public void TitleScript_PreservesTitleSelectionAcrossScriptOwnedUtilityScreens()
    {
        string script = File.ReadAllText(Path.Combine(RepoRoot, "src", "AriaEngine", "assets", "scripts", "main.aria"));

        script.Should().Contain("let %_title_choice = %0");
        script.IndexOf("let %_title_choice = %0", StringComparison.Ordinal)
            .Should().BeLessThan(script.IndexOf("if %_title_choice == 3 { goto *return_title }", StringComparison.Ordinal));
        script.IndexOf("let %_title_choice = %0", StringComparison.Ordinal)
            .Should().BeLessThan(script.IndexOf("if %_title_choice == 4 { goto *return_title }", StringComparison.Ordinal));
    }

    [Fact]
    public void GalleryScript_OwnsScreenAndDoesNotOverlayOmakeUi()
    {
        string script = File.ReadAllText(Path.Combine(RepoRoot, "src", "AriaEngine", "assets", "scripts", "gallery_ui.aria"));

        script.IndexOf("csp -1", StringComparison.Ordinal)
            .Should().BeGreaterThan(script.IndexOf("*gallery_ui", StringComparison.Ordinal));
        script.IndexOf("csp -1", StringComparison.Ordinal)
            .Should().BeLessThan(script.IndexOf("ui_rect 9999", StringComparison.Ordinal));
        script.Should().Contain("ui sprite:500, z, 20");
        script.Should().Contain("ui sprite:501, z, 20");
    }

    [Fact]
    public void GalleryScript_UsesStringRegisterForPageText()
    {
        string script = File.ReadAllText(Path.Combine(RepoRoot, "src", "AriaEngine", "assets", "scripts", "gallery_ui.aria"));

        script.Should().Contain("let $pagestr = \"NO ENTRIES\"");
        script.Should().Contain("div %totalpages, 6");
        script.Should().Contain("let $pagestr = \"PAGE \" + (%page + 1) + \" / \" + %totalpages");
        script.Should().Contain("ui sprite:501, text, $pagestr");
        script.Should().NotContain("%pagestr");
        script.Should().NotContain("if %count > 0 {");
    }

    [Fact]
    public void GalleryScript_UsesDistinctSpriteIdsForNavigationButtonsAndLabels()
    {
        string script = File.ReadAllText(Path.Combine(RepoRoot, "src", "AriaEngine", "assets", "scripts", "gallery_ui.aria"));

        script.Should().Contain("csp 594");
        script.Should().Contain("csp 595");
        script.Should().Contain("ui_rect 598");
        script.Should().Contain("ui_text 597, \"PREV\"");
        script.Should().Contain("ui_rect 596");
        script.Should().Contain("ui_text 595, \"NEXT\"");
        script.Should().Contain("ui_rect 599");
        script.Should().Contain("ui_text 594, \"BACK\"");
        script.Should().NotContain("ui_text 598, \"BACK\"");
    }

    [Fact]
    public void ReleaseReplaySpec_IsTracked()
    {
        string spec = File.ReadAllText(Path.Combine(RepoRoot, "tests", "replay", "release-smoke.json"));

        spec.Should().Contain("\"cases\"");
        spec.Should().Contain("compile-main-script");
        spec.Should().Contain("lint-main-script");
        spec.Should().Contain("flowcheck-umikaze-routes");
        spec.Should().Contain("aria-flowcheck --root . --main assets/scripts/main.aria --chapters 6 --execute");
        spec.Should().Contain("validate-empty-release-saves");
    }

    [Fact]
    public void ReplayScript_ResolvesOutputDirectoryBeforeChangingCaseWorkingDirectory()
    {
        string script = File.ReadAllText(Path.Combine(RepoRoot, "scripts", "replay.ps1"));

        script.Should().Contain("$OutputDir = (New-Item -ItemType Directory -Force -Path $OutputDir).FullName");
        script.Should().Contain("Push-Location $cwd");
    }

    [Fact]
    public void InstallerPipeline_UsesNsisAndDoesNotBuildDeprecatedInstallers()
    {
        string installerScript = File.ReadAllText(Path.Combine(RepoRoot, "scripts", "installer.ps1"));
        string packageScript = File.ReadAllText(Path.Combine(RepoRoot, "scripts", "package.ps1"));
        string workflow = File.ReadAllText(Path.Combine(RepoRoot, ".github", "workflows", "aria-cicd.yml"));
        string solution = File.ReadAllText(Path.Combine(RepoRoot, "engine.slnx"));

        installerScript.Should().Contain("installer/umikaze.nsi");
        installerScript.Should().Contain("makensis");
        installerScript.Should().Contain("$setupPath = [IO.Path]::GetFullPath");
        installerScript.Should().NotContain("src/AriaInstaller/AriaInstaller.csproj");
        File.Exists(Path.Combine(RepoRoot, "installer", "umikaze.nsi")).Should().BeTrue();
        File.Exists(Path.Combine(RepoRoot, "scripts", "update-installer.ps1")).Should().BeFalse();

        packageScript.Should().NotContain("src/aria-installer");
        packageScript.Should().NotContain("cargo");
        packageScript.Should().NotContain("engine/ subdirectory");

        workflow.Should().NotContain("src/AriaInstaller");
        workflow.Should().NotContain("src/aria-installer");
        solution.Should().NotContain("AriaInstaller");
    }

    [Fact]
    public void ReleaseStartup_ResolvesPackagedDataFromInstallDirectory()
    {
        string program = File.ReadAllText(Path.Combine(RepoRoot, "src", "AriaEngine", "Program.cs"));
        string nsisScript = File.ReadAllText(Path.Combine(RepoRoot, "installer", "umikaze.nsi"));

        program.Should().Contain("ResolveDistributionPath(options.PakPath)");
        program.Should().Contain("AppContext.BaseDirectory");
        nsisScript.Should().Contain("\"${PRODUCT_NAME}\" \"$INSTDIR\"");
    }

    [Fact]
    public void Codebase_DoesNotKeepDeprecatedLoadScriptCompatibilityPath()
    {
        string vmSource = File.ReadAllText(Path.Combine(RepoRoot, "src", "AriaEngine", "Core", "VirtualMachine.cs"));
        string smokeSource = File.ReadAllText(Path.Combine(RepoRoot, "tests", "AriaEngine.SmokeTests", "Program.cs"));

        vmSource.Should().NotContain("[Obsolete(\"Use LoadScript(ParseResult, string) instead\")]");
        smokeSource.Should().NotContain(".Instructions,");
        smokeSource.Should().NotContain(".Labels,");
    }

    [Fact]
    public void RuntimeSaveData_IsIgnoredAndNotTracked()
    {
        string gitignore = File.ReadAllText(Path.Combine(RepoRoot, ".gitignore"));
        string trackedSaves = RunGit("ls-files", "saves/persistent.ariasav").Trim();

        gitignore.Should().Contain("saves/");
        trackedSaves.Should().BeEmpty("runtime save data must not ship as source-controlled release input");
    }

    [Fact]
    public void MainScript_DoesNotDescribeReleaseUtilityScreensAsBroken()
    {
        string script = File.ReadAllText(Path.Combine(RepoRoot, "src", "AriaEngine", "assets", "scripts", "main.aria"));

        script.Should().NotContain("CONFIGとEXTRAは壊れてるまま");
    }

    [Fact]
    public void TrackedFontAssets_AreRealFontFiles()
    {
        string fontDir = Path.Combine(RepoRoot, "src", "AriaEngine", "assets", "fonts");
        var fontFiles = Directory.GetFiles(fontDir, "*.ttf");

        fontFiles.Should().NotBeEmpty();
        foreach (string fontFile in fontFiles)
        {
            byte[] header = File.ReadAllBytes(fontFile).Take(4).ToArray();
            bool isSfntFont =
                header.SequenceEqual(new byte[] { 0x00, 0x01, 0x00, 0x00 }) ||
                header.SequenceEqual("OTTO"u8.ToArray()) ||
                header.SequenceEqual("ttcf"u8.ToArray()) ||
                header.SequenceEqual("true"u8.ToArray());

            isSfntFont.Should().BeTrue($"{Path.GetRelativePath(RepoRoot, fontFile)} must be a real TTF/OTF font asset");
        }
    }

    private static string RunGit(params string[] arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = RepoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start git");
        string stdout = process.StandardOutput.ReadToEnd();
        string stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
            throw new InvalidOperationException($"git {string.Join(" ", arguments)} failed: {stderr}");

        return stdout;
    }
}
