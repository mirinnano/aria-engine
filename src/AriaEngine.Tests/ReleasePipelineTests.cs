using System;
using System.IO;
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

        script.Should().Contain("releaseNotes");
        script.Should().Contain("signing");
        script.Should().Contain("signed");
        script.Should().Contain("trusted");
        script.Should().Contain("Get-AuthenticodeSignature");
        script.Should().Contain("$compileArgs += @(\"--key\", $env:ARIA_PACK_KEY)");
        script.Should().Contain("$packArgs += @(\"--key\", $env:ARIA_PACK_KEY)");
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
    }

    [Fact]
    public void Installer_UsesProductNameAndVersionInWindowAndReceipt()
    {
        string source = File.ReadAllText(Path.Combine(RepoRoot, "src", "AriaInstaller", "Program.cs"));

        source.Should().Contain("umikaze Installer");
        source.Should().Contain("Arguments = \"--list-runtimes\"");
        source.Should().Contain("NativeInstallerWindow");
        source.Should().Contain("CreateShortcut");
    }
}
