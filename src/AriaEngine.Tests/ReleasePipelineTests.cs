using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
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
            .Should().BeLessThan(script.IndexOf("Invoke-AriaCli $cliHostFile $packArgs $publishDir", StringComparison.Ordinal));
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
    public void CiWorkflow_RunsProfileMatrixGates()
    {
        string workflow = File.ReadAllText(Path.Combine(RepoRoot, ".github", "workflows", "aria-cicd.yml"));

        workflow.Should().Contain("profile-gates");
        workflow.Should().Contain("profile: [Debug, Demo, Release]");
        workflow.Should().Contain("aria-i18n-check");
        workflow.Should().Contain("-Profile ${{ matrix.profile }}");
        workflow.Should().Contain("--profile");
    }

    [Fact]
    public void CiWorkflow_InstallsNsisBeforeReleaseScriptBuildsInstaller()
    {
        string workflow = File.ReadAllText(Path.Combine(RepoRoot, ".github", "workflows", "aria-cicd.yml"));

        workflow.IndexOf("Setup NSIS", StringComparison.Ordinal)
            .Should().BeLessThan(workflow.IndexOf("scripts/release.ps1", StringComparison.Ordinal));
        workflow.Should().Contain("-SkipInstaller");
    }

    [Fact]
    public void ReleaseScript_ExposesNativeBuildFlavorFlags()
    {
        string script = File.ReadAllText(Path.Combine(RepoRoot, "scripts", "release.ps1"));

        script.Should().Contain("[switch]$SelfContained");
        script.Should().Contain("[switch]$SingleFile");
        script.Should().Contain("[switch]$PublishTrimmed");
        script.Should().Contain("[switch]$PublishAot");
        script.Should().Contain("[switch]$SkipInstaller");
        script.Should().Contain("[switch]$FullQA");
    }

    [Fact]
    public void ReleaseScript_PassesNativeBuildFlavorFlagsToPackageScript()
    {
        string script = File.ReadAllText(Path.Combine(RepoRoot, "scripts", "release.ps1"));

        script.Should().Contain("SelfContained = $selfContainedEnabled");
        script.Should().Contain("$selfContainedEnabled = [bool]($SelfContained -or $PublishAot)");
        script.Should().Contain("SingleFile = $singleFileEnabled");
        script.Should().Contain("PublishTrimmed = $PublishTrimmed");
        script.Should().Contain("PublishAot = $PublishAot");
        script.Should().Contain("PublishFlavor = $PublishFlavor");
        script.Should().Contain("PublishTrimmed = [bool]$PublishTrimmed");
        script.Should().Contain("PublishAot = [bool]$PublishAot");
    }

    [Fact]
    public void PackageScripts_RecordRuntimeProfileAndBrowserOpenPolicy()
    {
        string package = File.ReadAllText(Path.Combine(RepoRoot, "scripts", "package.ps1"));
        string release = File.ReadAllText(Path.Combine(RepoRoot, "scripts", "release.ps1"));
        string installer = File.ReadAllText(Path.Combine(RepoRoot, "scripts", "installer.ps1"));
        string webPackage = File.ReadAllText(Path.Combine(RepoRoot, "scripts", "package-web.ps1"));

        package.Should().Contain("[ValidateSet(\"Debug\", \"Demo\", \"Release\")]");
        package.Should().Contain("[string]$Profile = \"Release\"");
        package.Should().Contain("profile = $Profile.ToLowerInvariant()");
        package.Should().Contain("isDemo = $Profile.Equals(\"Demo\"");
        package.Should().Contain("browserOpenPolicy");
        package.Should().Contain("ponkotsu-soft.vercel.app");
        package.Should().NotContain("ponkotusoft.example");
        package.Should().Contain("targetRuntime = $runtimeLabel");
        package.Should().NotContain("runtime = $runtimeLabel");
        package.Should().Contain("\"--profile\", $Profile.ToLowerInvariant()");

        release.Should().Contain("[ValidateSet(\"Debug\", \"Demo\", \"Release\")]");
        release.Should().Contain("Profile = $Profile");
        installer.Should().Contain("[ValidateSet(\"Debug\", \"Demo\", \"Release\")]");
        installer.Should().Contain("Profile = $Profile");
        webPackage.Should().Contain("[ValidateSet(\"Debug\", \"Demo\", \"Release\")]");
        webPackage.Should().Contain("profile = $Profile.ToLowerInvariant()");
    }

    [Fact]
    public void InstallerScript_CanGenerateNativeAotPackageBeforeNsis()
    {
        string script = File.ReadAllText(Path.Combine(RepoRoot, "scripts", "installer.ps1"));

        script.Should().Contain("[bool]$PublishTrimmed");
        script.Should().Contain("[bool]$PublishAot");
        script.Should().Contain("[string]$PublishFlavor");
        script.Should().Contain("PublishFlavor = $PublishFlavor");
        script.Should().Contain("SelfContained = [bool]($SelfContained -or $PublishAot)");
        script.Should().Contain("PublishTrimmed = $PublishTrimmed");
        script.Should().Contain("PublishAot = $PublishAot");
        script.Should().Contain("$artifactLabel");
    }

    [Fact]
    public void Program_LoadsCompiledBundleFromDistributionFileForNativeAotReleaseLaunch()
    {
        string program = File.ReadAllText(Path.Combine(RepoRoot, "src", "AriaEngine", "Program.cs"));

        program.Should().Contain("ResolveDistributionPath(options.CompiledPath)");
        program.Should().Contain("File.OpenRead(compiledDiskPath)");
    }

    [Fact]
    public void Program_SetsRuntimeWindowIconFromPackagedBrandingAsset()
    {
        string program = File.ReadAllText(Path.Combine(RepoRoot, "src", "AriaEngine", "Program.cs"));

        program.Should().Contain("TrySetWindowIcon");
        program.Should().Contain("assets/branding/umikaze-icon-master.png");
        program.Should().Contain("Raylib.SetWindowIcon");
        program.Should().Contain("assetProvider.MaterializeToFile");
    }

    [Fact]
    public void SigningVerifier_FailsRequiredUnsignedArtifactsAndCanAuditInstaller()
    {
        string script = File.ReadAllText(Path.Combine(RepoRoot, "scripts", "verify-signing.ps1"));

        script.Should().Contain("[switch]$RequireSigned");
        script.Should().Contain("Get-AuthenticodeSignature");
        script.Should().Contain("Set-Content");
        script.Should().Contain("signature-audit.json");
        script.Should().Contain("Unsigned artifacts found");
        script.Should().Contain("Status");
    }

    [Fact]
    public void SigningScript_RequiresExplicitProductionSigningConfiguration()
    {
        string script = File.ReadAllText(Path.Combine(RepoRoot, "scripts", "sign.ps1"));
        string windows = File.ReadAllText(Path.Combine(RepoRoot, "docs", "release", "windows-native.md"));
        string checklist = File.ReadAllText(Path.Combine(RepoRoot, "docs", "release", "production-checklist.md"));

        script.Should().Contain("Code signing is not configured");
        script.Should().Contain("ARIA_SIGNTOOL_PATH");
        script.Should().Contain("ARIA_SIGN_CERT_THUMBPRINT");
        script.Should().Contain("ARIA_SIGN_TIMESTAMP_URL");
        script.Should().Contain("ARIA_SIGN_ALLOW_SELF_SIGNED");
        script.Should().Contain("/fd SHA256");
        script.Should().Contain("/td SHA256");
        script.Should().Contain("/tr $TimestampUrl");
        script.Should().Contain("WINDOWS_CODESIGN_PFX_BASE64");
        script.Should().Contain("WINDOWS_CODESIGN_PFX_PASSWORD");
        windows.Should().Contain("WINDOWS_CODESIGN_PFX_BASE64");
        windows.Should().Contain("ARIA_SIGN_CERT_THUMBPRINT");
        checklist.Should().Contain("Code signing is not configured");
        checklist.Should().Contain("ARIA_SIGN_ALLOW_SELF_SIGNED");
    }

    [Fact]
    public void InstallerScript_VerifiesSignedSetupWhenSignIsRequested()
    {
        string script = File.ReadAllText(Path.Combine(RepoRoot, "scripts", "installer.ps1"));

        script.Should().Contain("verify-signing.ps1");
        script.Should().Contain("-RequireSigned");
        script.IndexOf("sign.ps1", StringComparison.Ordinal)
            .Should().BeLessThan(script.IndexOf("verify-signing.ps1", StringComparison.Ordinal));
    }

    [Fact]
    public void CiWorkflow_VerifiesSignedNativeArtifactsBeforeUpload()
    {
        string workflow = File.ReadAllText(Path.Combine(RepoRoot, ".github", "workflows", "aria-cicd.yml"));

        workflow.Should().Contain("Verify signed release package");
        workflow.Should().Contain("Verify signed installer");
        workflow.Should().Contain("scripts/verify-signing.ps1");
        workflow.Should().Contain("-RequireSigned");
        workflow.Should().Contain("signature-audit-release.json");
        workflow.Should().Contain("signature-audit-installer.json");
        workflow.Should().Contain("Get-ChildItem artifacts/installer");
        workflow.Should().Contain("*-installer.zip");
        workflow.IndexOf("Build release package", StringComparison.Ordinal)
            .Should().BeLessThan(workflow.IndexOf("Verify signed release package", StringComparison.Ordinal));
        workflow.IndexOf("Create installer zip", StringComparison.Ordinal)
            .Should().BeLessThan(workflow.IndexOf("Verify signed installer", StringComparison.Ordinal));
        workflow.IndexOf("Verify signed installer", StringComparison.Ordinal)
            .Should().BeLessThan(workflow.IndexOf("Upload artifact", StringComparison.Ordinal));
    }

    [Fact]
    public void CiWorkflow_ExportsCodeSigningSecretsToSigningScript()
    {
        string workflow = File.ReadAllText(Path.Combine(RepoRoot, ".github", "workflows", "aria-cicd.yml"));

        workflow.Should().Contain("WINDOWS_CODESIGN_PFX_BASE64: ${{ secrets.WINDOWS_CODESIGN_PFX_BASE64 }}");
        workflow.Should().Contain("WINDOWS_CODESIGN_PFX_PASSWORD: ${{ secrets.WINDOWS_CODESIGN_PFX_PASSWORD }}");
        workflow.Should().Contain("ARIA_SIGN_TIMESTAMP_URL");
        workflow.Should().Contain("WINDOWS_CODESIGN_PFX_BASE64 and WINDOWS_CODESIGN_PFX_PASSWORD are required");
        workflow.IndexOf("WINDOWS_CODESIGN_PFX_BASE64:", StringComparison.Ordinal)
            .Should().BeLessThan(workflow.IndexOf("Build release package", StringComparison.Ordinal));
    }

    [Fact]
    public void CiWorkflow_AlwaysUploadsReleaseReadinessReportEvidence()
    {
        string workflow = File.ReadAllText(Path.Combine(RepoRoot, ".github", "workflows", "aria-cicd.yml"));

        workflow.Should().Contain("Prepare release readiness report");
        workflow.Should().Contain("scripts/prepare-release-evidence.ps1");
        workflow.Should().Contain("release-readiness-audit.json");
        workflow.Should().Contain("release-readiness-report.md");
        workflow.Should().Contain("continue-on-error: true");
        workflow.Should().Contain("Upload release readiness report");
        workflow.Should().Contain("if: always()");
        workflow.Should().Contain("artifacts/release/readiness/**");
        workflow.IndexOf("Prepare release readiness report", StringComparison.Ordinal)
            .Should().BeLessThan(workflow.IndexOf("Upload release readiness report", StringComparison.Ordinal));
    }

    [Fact]
    public void CiWorkflow_BuildsNativeAotAndWebPackagesBeforeReleaseReadinessReport()
    {
        string workflow = File.ReadAllText(Path.Combine(RepoRoot, ".github", "workflows", "aria-cicd.yml"));

        workflow.Should().Contain("Build NativeAOT readiness package");
        workflow.Should().Contain("OutputRoot = \"artifacts/nativeaot\"");
        workflow.Should().Contain("PublishAot = $true");
        workflow.Should().Contain("scripts/package.ps1 @nativeAotArgs");
        workflow.Should().Contain("NativePackageDir = \"artifacts/nativeaot/AriaEngine-$version-win-x64/app\"");
        workflow.Should().Contain("Package Web/PWA readiness package");
        workflow.Should().Contain("scripts/package-web.ps1 -Version $version -OutputRoot artifacts/web");
        workflow.Should().Contain("Run Web/PWA readiness QA");
        workflow.Should().Contain("scripts/web-device-qa.ps1");
        workflow.Should().Contain("WebPackageDir = \"artifacts/web/AriaEngine-$version-web\"");
        workflow.Should().Contain("WebDeviceQaManifest = \"artifacts/release/readiness/web-device-qa-manifest.json\"");
        workflow.Should().Contain("WebVisualCompareManifest = \"artifacts/release/readiness/web-native-visual-compare.json\"");
        workflow.IndexOf("Build NativeAOT readiness package", StringComparison.Ordinal)
            .Should().BeLessThan(workflow.IndexOf("Package Web/PWA readiness package", StringComparison.Ordinal));
        workflow.IndexOf("Package Web/PWA readiness package", StringComparison.Ordinal)
            .Should().BeLessThan(workflow.IndexOf("Prepare release readiness report", StringComparison.Ordinal));
    }

    [Fact]
    public void CiWorkflow_OnlyRequestsSigningForReleaseBuilds()
    {
        string workflow = File.ReadAllText(Path.Combine(RepoRoot, ".github", "workflows", "aria-cicd.yml"));

        workflow.Should().Contain("$isReleaseBuild");
        workflow.Should().Contain("$releaseArgs += \"-Sign\"");
        workflow.Should().Contain("$installerArgs += \"-Sign\"");
        workflow.Should().NotContain("scripts/release.ps1 -Version $version -Runtime win-x64 -ReleaseNotes $notes -Sign -SkipInstaller");
        workflow.Should().NotContain("scripts/installer.ps1 -PackageDir \"artifacts/release/AriaEngine-$version-win-x64/app\" -OutputDir artifacts/installer -Version $version -Runtime win-x64 -Sign");
    }

    [Fact]
    public void WebPwaProject_IsFirstClassStaticWasmTarget()
    {
        string solution = File.ReadAllText(Path.Combine(RepoRoot, "engine.slnx"));
        string webProjectPath = Path.Combine(RepoRoot, "src", "AriaEngine.Web", "AriaEngine.Web.csproj");
        string webProgramPath = Path.Combine(RepoRoot, "src", "AriaEngine.Web", "Program.cs");
        string indexPath = Path.Combine(RepoRoot, "src", "AriaEngine.Web", "wwwroot", "index.html");
        string manifestPath = Path.Combine(RepoRoot, "src", "AriaEngine.Web", "wwwroot", "manifest.webmanifest");
        string serviceWorkerPath = Path.Combine(RepoRoot, "src", "AriaEngine.Web", "wwwroot", "service-worker.js");

        solution.Should().Contain("src/AriaEngine.Web/AriaEngine.Web.csproj");
        File.Exists(webProjectPath).Should().BeTrue();
        File.Exists(webProgramPath).Should().BeTrue();
        File.Exists(indexPath).Should().BeTrue();
        File.Exists(manifestPath).Should().BeTrue();
        File.Exists(serviceWorkerPath).Should().BeTrue();
    }

    [Fact]
    public void WebPackageAndQaScripts_ArePresentForStaticPwaRelease()
    {
        File.Exists(Path.Combine(RepoRoot, "scripts", "package-web.ps1")).Should().BeTrue();
        File.Exists(Path.Combine(RepoRoot, "scripts", "web-browser-qa.ps1")).Should().BeTrue();
        File.Exists(Path.Combine(RepoRoot, "scripts", "web-browser-qa.mjs")).Should().BeTrue();
        File.Exists(Path.Combine(RepoRoot, "scripts", "web-capture-visuals.ps1")).Should().BeTrue();
        File.Exists(Path.Combine(RepoRoot, "scripts", "web-capture-visuals.mjs")).Should().BeTrue();
        File.Exists(Path.Combine(RepoRoot, "scripts", "native-capture-visuals.ps1")).Should().BeTrue();
        File.Exists(Path.Combine(RepoRoot, "scripts", "web-device-qa.ps1")).Should().BeTrue();
        File.Exists(Path.Combine(RepoRoot, "scripts", "web-native-visual-compare.ps1")).Should().BeTrue();
        File.Exists(Path.Combine(RepoRoot, ".github", "workflows", "aria-web-pages.yml")).Should().BeTrue();
        File.Exists(Path.Combine(RepoRoot, ".github", "workflows", "aria-web-device-qa.yml")).Should().BeTrue();

        string browserQa = File.ReadAllText(Path.Combine(RepoRoot, "scripts", "web-browser-qa.ps1"));
        string browserRunner = File.ReadAllText(Path.Combine(RepoRoot, "scripts", "web-browser-qa.mjs"));
        string visualCapture = File.ReadAllText(Path.Combine(RepoRoot, "scripts", "web-capture-visuals.mjs"));
        string nativeCapture = File.ReadAllText(Path.Combine(RepoRoot, "scripts", "native-capture-visuals.ps1"));
        string deviceQa = File.ReadAllText(Path.Combine(RepoRoot, "scripts", "web-device-qa.ps1"));
        string packageWeb = File.ReadAllText(Path.Combine(RepoRoot, "scripts", "package-web.ps1"));
        string cicdWorkflow = File.ReadAllText(Path.Combine(RepoRoot, ".github", "workflows", "aria-cicd.yml"));
        string deviceWorkflow = File.ReadAllText(Path.Combine(RepoRoot, ".github", "workflows", "aria-web-device-qa.yml"));

        packageWeb.Should().Contain("AriaEngine-$Version-web.zip");
        packageWeb.Should().Contain("Compress-Archive -Path (Join-Path $packageDir \"*\") -DestinationPath $zipPath -Force");
        packageWeb.Should().Contain("zipPath");
        cicdWorkflow.Should().Contain("artifacts/web/dist/*.zip");
        browserQa.Should().Contain("web-browser-qa.mjs");
        browserQa.Should().Contain("npx");
        browserQa.Should().NotContain("runtime right-click coverage pending browser automation");
        browserQa.Should().NotContain("static gate; real browser gate pending");
        browserRunner.Should().Contain("playwright");
        browserRunner.Should().Contain("contextmenu");
        browserRunner.Should().Contain("applyStorageOperation");
        browserRunner.Should().Contain("consoleErrors");
        visualCapture.Should().Contain("title.png");
        visualCapture.Should().Contain("text.png");
        visualCapture.Should().Contain("menu.png");
        visualCapture.Should().Contain("screenshot");
        nativeCapture.Should().Contain("title.png");
        nativeCapture.Should().Contain("text.png");
        nativeCapture.Should().Contain("menu.png");
        nativeCapture.Should().Contain("CopyFromScreen");
        nativeCapture.Should().Contain("$capturePackagePath");
        nativeCapture.Should().Contain("Copy-Item -LiteralPath $packagePath -Destination $capturePackagePath -Recurse -Force");
        nativeCapture.Should().Contain("Remove-Item -LiteralPath $statePath -Recurse -Force");
        deviceQa.Should().Contain("web-capture-visuals.ps1");
        deviceWorkflow.Should().Contain("npx playwright install");
    }

    [Fact]
    public void WebNativeVisualCompare_DoesNotClaimReadyWhenMissingCapturesAreOnlyAllowedForAudit()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "aria-web-visual-missing-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            string outputPath = Path.Combine(tempDir, "web-native-visual-compare.json");
            var result = RunProcess(
                "pwsh",
                "-NoProfile",
                "-ExecutionPolicy",
                "Bypass",
                "-File",
                Path.Combine(RepoRoot, "scripts", "web-native-visual-compare.ps1"),
                "-NativeCaptureDir",
                Path.Combine(tempDir, "native"),
                "-WebCaptureDir",
                Path.Combine(tempDir, "web"),
                "-OutputPath",
                outputPath,
                "-AllowMissingCaptures");

            result.ExitCode.Should().Be(0);
            File.Exists(outputPath).Should().BeTrue();
            string manifest = File.ReadAllText(outputPath);
            manifest.Should().Contain("\"ready\": false");
            manifest.Should().Contain("\"missingCapturesAllowed\": true");
            manifest.Should().Contain("\"present\": false");
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void WebNativeVisualCompare_ComparesMatchingCapturesAndReportsPixelParity()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "aria-web-visual-match-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            string nativeDir = Path.Combine(tempDir, "native");
            string webDir = Path.Combine(tempDir, "web");
            Directory.CreateDirectory(nativeDir);
            Directory.CreateDirectory(webDir);
            foreach (string name in new[] { "title.png", "text.png", "menu.png" })
            {
                WriteOnePixelPng(Path.Combine(nativeDir, name));
                WriteOnePixelPng(Path.Combine(webDir, name));
            }

            string outputPath = Path.Combine(tempDir, "web-native-visual-compare.json");
            var result = RunProcess(
                "pwsh",
                "-NoProfile",
                "-ExecutionPolicy",
                "Bypass",
                "-File",
                Path.Combine(RepoRoot, "scripts", "web-native-visual-compare.ps1"),
                "-NativeCaptureDir",
                nativeDir,
                "-WebCaptureDir",
                webDir,
                "-OutputPath",
                outputPath);

            result.ExitCode.Should().Be(0);
            string manifest = File.ReadAllText(outputPath);
            manifest.Should().Contain("\"ready\": true");
            manifest.Should().Contain("\"pixelDiffRatio\": 0");
            manifest.Should().Contain("\"layoutParity\": \"passed\"");
            manifest.Should().Contain("\"fontParity\": \"passed\"");
            manifest.Should().Contain("\"hitTestParity\": \"covered-by-browser-qa\"");
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void WebNativeVisualCompare_AllowsBrowserFontRasterizationTolerance()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "aria-web-visual-tolerance-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            string nativeDir = Path.Combine(tempDir, "native");
            string webDir = Path.Combine(tempDir, "web");
            Directory.CreateDirectory(nativeDir);
            Directory.CreateDirectory(webDir);
            foreach (string name in new[] { "title.png", "text.png", "menu.png" })
            {
                WriteTolerancePng(Path.Combine(nativeDir, name), changedPixels: 0);
                WriteTolerancePng(Path.Combine(webDir, name), changedPixels: 300);
            }

            string outputPath = Path.Combine(tempDir, "web-native-visual-compare.json");
            var result = RunProcess(
                "pwsh",
                "-NoProfile",
                "-ExecutionPolicy",
                "Bypass",
                "-File",
                Path.Combine(RepoRoot, "scripts", "web-native-visual-compare.ps1"),
                "-NativeCaptureDir",
                nativeDir,
                "-WebCaptureDir",
                webDir,
                "-OutputPath",
                outputPath);

            result.ExitCode.Should().Be(0);
            string manifest = File.ReadAllText(outputPath);
            manifest.Should().Contain("\"ready\": true");
            manifest.Should().Contain("\"maxDiffRatio\": 0.05");
            manifest.Should().Contain("\"fontParity\": \"passed\"");
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ReleaseReadinessAuditScript_MapsNativeAndWebEvidenceToBlockingGates()
    {
        string scriptPath = Path.Combine(RepoRoot, "scripts", "release-readiness-audit.ps1");
        string checklist = File.ReadAllText(Path.Combine(RepoRoot, "docs", "release", "production-checklist.md"));

        File.Exists(scriptPath).Should().BeTrue("release-ready must be decided by artifact evidence, not memory");
        string script = File.ReadAllText(scriptPath);
        script.Should().Contain("ReleasePackageDir");
        script.Should().Contain("SignatureAudit");
        script.Should().Contain("NativePackageDir");
        script.Should().Contain("WebPackageDir");
        script.Should().Contain("WebDeviceQaManifest");
        script.Should().Contain("WebVisualCompareManifest");
        script.Should().Contain("Windows release package");
        script.Should().Contain("trusted signing audit");
        script.Should().Contain("NativeAOT package");
        script.Should().Contain("static Web/PWA package");
        script.Should().Contain("browser QA captures");
        script.Should().Contain("native/Web visual regression");
        script.Should().Contain("runtime profile manifest");
        script.Should().Contain("browserOpenPolicy");
        script.Should().Contain("scenarioStatus");
        script.Should().Contain("steamSubtitleLanguages");
        script.Should().Contain("runtimeconfig");
        script.Should().Contain("coreclr.dll");
        script.Should().Contain("AriaEngine.dll");
        script.Should().Contain("dotnet runtime dependency");
        script.Should().Contain("release-readiness-audit.json");
        script.Should().Contain("Windows Native/NativeAOT/Web/PWA release-ready");
        checklist.Should().Contain("scripts/prepare-release-evidence.ps1");
        checklist.Should().Contain("scripts/release-readiness-audit.ps1");
        checklist.Should().Contain("scripts/release-readiness-report.ps1");
        checklist.Should().Contain("web-browser-qa-chrome.json");
        checklist.Should().Contain("web-native-visual-compare.json");
    }

    [Fact]
    public void ReleaseReadinessAudit_FailsWhenNativeWebPackageOrTrustedSigningEvidenceIsMissing()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "aria-release-readiness-missing-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            string outputPath = Path.Combine(tempDir, "release-readiness-audit.json");
            string missingReleasePackageDir = Path.Combine(tempDir, "missing-release");
            string missingNativePackageDir = Path.Combine(tempDir, "missing-nativeaot");
            string missingWebPackageDir = Path.Combine(tempDir, "missing-web");

            var result = RunProcess(
                "pwsh",
                "-NoProfile",
                "-ExecutionPolicy",
                "Bypass",
                "-File",
                Path.Combine(RepoRoot, "scripts", "release-readiness-audit.ps1"),
                "-ReleasePackageDir",
                missingReleasePackageDir,
                "-NativePackageDir",
                missingNativePackageDir,
                "-WebPackageDir",
                missingWebPackageDir,
                "-WebDeviceQaManifest",
                Path.Combine(tempDir, "missing-web-device-qa-manifest.json"),
                "-WebVisualCompareManifest",
                Path.Combine(tempDir, "missing-web-native-visual-compare.json"),
                "-SignatureAudit",
                Path.Combine(tempDir, "missing-signature-audit.json"),
                "-OutputPath",
                outputPath);

            result.ExitCode.Should().NotBe(0);
            string output = result.StandardOutput + result.StandardError;
            output.Should().Contain("Windows release package");
            output.Should().Contain("trusted signing audit");
            output.Should().Contain("NativeAOT package");
            output.Should().Contain("static Web/PWA package");
            output.Should().Contain("browser QA captures");
            output.Should().Contain("native/Web visual regression");
            File.Exists(outputPath).Should().BeTrue();
            File.ReadAllText(outputPath).Should().Contain("\"ready\": false");
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ReleaseReadinessAudit_PassesWithCompleteArtifactEvidence()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "aria-release-readiness-complete-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            string releasePackageDir = Path.Combine(tempDir, "release");
            string nativePackageDir = Path.Combine(tempDir, "nativeaot");
            string webPackageDir = Path.Combine(tempDir, "web");
            string webDeviceQaManifest = Path.Combine(tempDir, "web-device-qa-manifest.json");
            string webVisualCompareManifest = Path.Combine(tempDir, "web-native-visual-compare.json");
            string signatureAudit = Path.Combine(tempDir, "signature-audit.json");
            string outputPath = Path.Combine(tempDir, "release-readiness-audit.json");
            WriteReleaseReadinessWindowsPackageEvidence(releasePackageDir);
            WriteReleaseReadinessNativeAotEvidence(nativePackageDir);
            WriteReleaseReadinessWebPackageEvidence(webPackageDir);
            WriteWebDeviceQaEvidence(tempDir, webDeviceQaManifest, webVisualCompareManifest);
            File.WriteAllText(signatureAudit, """
            {
              "total": 1,
              "signed": 1,
              "unsigned": 0,
              "files": [
                { "relativePath": "umikaze-setup.exe", "status": "Valid" }
              ]
            }
            """, Encoding.UTF8);

            var result = RunProcess(
                "pwsh",
                "-NoProfile",
                "-ExecutionPolicy",
                "Bypass",
                "-File",
                Path.Combine(RepoRoot, "scripts", "release-readiness-audit.ps1"),
                "-ReleasePackageDir",
                releasePackageDir,
                "-NativePackageDir",
                nativePackageDir,
                "-WebPackageDir",
                webPackageDir,
                "-WebDeviceQaManifest",
                webDeviceQaManifest,
                "-WebVisualCompareManifest",
                webVisualCompareManifest,
                "-SignatureAudit",
                signatureAudit,
                "-OutputPath",
                outputPath);

            result.ExitCode.Should().Be(0, result.StandardError + result.StandardOutput);
            File.ReadAllText(outputPath).Should().Contain("\"ready\": true");
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ReleaseEvidencePreparation_CreatesMissingSigningAuditAndNativeReadinessReport()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "aria-release-evidence-prep-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            string releasePackageDir = Path.Combine(tempDir, "release");
            string nativePackageDir = Path.Combine(tempDir, "nativeaot");
            string webPackageDir = Path.Combine(tempDir, "web");
            string webDeviceQaManifest = Path.Combine(tempDir, "web-device-qa-manifest.json");
            string webVisualCompareManifest = Path.Combine(tempDir, "web-native-visual-compare.json");
            string signatureAudit = Path.Combine(tempDir, "signature-audit.json");
            string outputPath = Path.Combine(tempDir, "release-readiness-audit.json");
            string reportPath = Path.Combine(tempDir, "release-readiness-report.md");

            WriteReleaseReadinessWindowsPackageEvidence(releasePackageDir);
            WriteReleaseReadinessNativeAotEvidence(nativePackageDir);
            WriteReleaseReadinessWebPackageEvidence(webPackageDir);
            WriteWebDeviceQaEvidence(tempDir, webDeviceQaManifest, webVisualCompareManifest);

            var result = RunProcess(
                "pwsh",
                "-NoProfile",
                "-ExecutionPolicy",
                "Bypass",
                "-File",
                Path.Combine(RepoRoot, "scripts", "prepare-release-evidence.ps1"),
                "-ReleasePackageDir",
                releasePackageDir,
                "-NativePackageDir",
                nativePackageDir,
                "-WebPackageDir",
                webPackageDir,
                "-WebDeviceQaManifest",
                webDeviceQaManifest,
                "-WebVisualCompareManifest",
                webVisualCompareManifest,
                "-SignatureAudit",
                signatureAudit,
                "-OutputPath",
                outputPath,
                "-ReportPath",
                reportPath);

            result.ExitCode.Should().NotBe(0);
            File.Exists(signatureAudit).Should().BeTrue();
            File.Exists(outputPath).Should().BeTrue();
            File.Exists(reportPath).Should().BeTrue();
            File.ReadAllText(reportPath).Should().Contain("Status: Not Ready").And.Contain("Remaining Blockers");
            string output = result.StandardOutput + result.StandardError;
            output.Should().Contain("trusted signing audit has unsigned or invalid files");
            output.Should().NotContain("trusted signing audit missing");
            output.Should().NotContain("static Web/PWA package: missing");
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ReleaseReadinessReport_ExplainsChecklistEvidenceAndRemainingBlockers()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "aria-release-readiness-report-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            string auditPath = Path.Combine(tempDir, "release-readiness-audit.json");
            string reportPath = Path.Combine(tempDir, "release-readiness-report.md");
            File.WriteAllText(auditPath, """
            {
              "ready": false,
              "objective": "Windows Native/NativeAOT/Web/PWA release-ready package and QA evidence",
              "checks": [
                { "name": "Windows release package", "passed": true, "evidence": "artifacts/native/app", "message": "required Windows package files present" },
                { "name": "static Web/PWA package", "passed": true, "evidence": "artifacts/web/app", "message": "required static Web/PWA package files present" },
                { "name": "browser QA captures", "passed": true, "evidence": "artifacts/release/readiness/web-device-qa-manifest.json", "message": "Chrome, Edge, Safari, and mobile browser QA passed" },
                { "name": "native/Web visual regression", "passed": true, "evidence": "artifacts/release/readiness/web-native-visual-compare.json", "message": "native/Web visual regression passed" },
                { "name": "trusted signing audit", "passed": false, "evidence": "artifacts/native/app/signature-audit.json", "message": "trusted signing audit has unsigned or invalid files" }
              ]
            }
            """, Encoding.UTF8);

            var result = RunProcess(
                "pwsh",
                "-NoProfile",
                "-ExecutionPolicy",
                "Bypass",
                "-File",
                Path.Combine(RepoRoot, "scripts", "release-readiness-report.ps1"),
                "-AuditPath",
                auditPath,
                "-OutputPath",
                reportPath);

            result.ExitCode.Should().Be(0, result.StandardError + result.StandardOutput);
            string report = File.ReadAllText(reportPath);
            report.Should().Contain("Status: Not Ready");
            report.Should().Contain("Prompt-To-Artifact Checklist");
            report.Should().Contain("Windows release package");
            report.Should().Contain("`artifacts/native/app`");
            report.Should().Contain("static Web/PWA package");
            report.Should().Contain("browser QA captures");
            report.Should().Contain("native/Web visual regression");
            report.Should().Contain("trusted signing audit has unsigned or invalid files");
            report.Should().Contain("`artifacts/native/app/signature-audit.json`");
            report.Should().Contain("Remaining Blockers");
            report.Should().Contain("`ready: true`");
            report.Should().NotContain("$evidence");
            report.Should().NotContain("$(");
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void PackageScript_RecordsPublishFlavorInManifest()
    {
        string script = File.ReadAllText(Path.Combine(RepoRoot, "scripts", "package.ps1"));

        script.Should().Contain("[string]$PublishFlavor");
        script.Should().Contain("publishFlavor");
        script.Should().Contain("selfContained");
        script.Should().Contain("singleFile");
        script.Should().Contain("publishTrimmed");
        script.Should().Contain("publishAot");
        script.Should().Contain("$env:OS = \"Windows_NT\"");
        script.Should().Contain("$restoreArgs += \"/p:PublishAot=true\"");
        script.Should().Contain("$effectivePublishTrimmed = [bool]($PublishTrimmed -or $PublishAot)");
        script.Should().Contain("$publishSingleFileForSdk = [bool]($SingleFile -and -not $PublishAot)");
        script.Should().Contain("singleFile = [bool]$artifactSingleFile");
        script.Should().Contain("NativeAOT publish did not produce native output");
    }

    [Fact]
    public void PackageScript_PrunesWindowsPackageNoiseBeforeManifestAndChecksums()
    {
        string script = File.ReadAllText(Path.Combine(RepoRoot, "scripts", "package.ps1"));

        script.Should().Contain("Remove-WindowsPackageNoise");
        script.Should().Contain("libzstd.dylib");
        script.Should().Contain("libzstd.so");
        script.Should().Contain("*.pdb");
        script.IndexOf("Remove-WindowsPackageNoise $publishDir", StringComparison.Ordinal)
            .Should().BeLessThan(script.IndexOf("$manifest = [ordered]@", StringComparison.Ordinal));
    }

    [Fact]
    public void NativeAotRelease_KeepsJsonAndV3SplitPakBootViable()
    {
        string project = File.ReadAllText(Path.Combine(RepoRoot, "src", "AriaEngine", "AriaEngine.csproj"));
        string program = File.ReadAllText(Path.Combine(RepoRoot, "src", "AriaEngine", "Program.cs"));
        string json = File.ReadAllText(Path.Combine(RepoRoot, "src", "AriaEngine", "Core", "AriaCoreJsonContext.cs"));
        string localization = File.ReadAllText(Path.Combine(RepoRoot, "src", "AriaEngine", "Core", "LocalizationManager.cs"));
        string config = File.ReadAllText(Path.Combine(RepoRoot, "src", "AriaEngine", "Core", "ConfigManager.cs"));
        string chapters = File.ReadAllText(Path.Combine(RepoRoot, "src", "AriaEngine", "Core", "ChapterManager.cs"));
        string characters = File.ReadAllText(Path.Combine(RepoRoot, "src", "AriaEngine", "Core", "CharacterManager.cs"));
        string saves = File.ReadAllText(Path.Combine(RepoRoot, "src", "AriaEngine", "Core", "SaveManager.cs"));
        string sprites = File.ReadAllText(Path.Combine(RepoRoot, "src", "AriaEngine", "Core", "FastSpriteDictionary.cs"));
        string docs = File.ReadAllText(Path.Combine(RepoRoot, "src", "AriaEngine", "Tools", "AriaDocCommand.cs"));
        string errors = File.ReadAllText(Path.Combine(RepoRoot, "src", "AriaEngine", "Core", "ErrorReporter.cs"));

        project.Should().Contain("JsonSerializerIsReflectionEnabledByDefault");
        program.Should().Contain("HasDistributionPakInBaseDirectory");
        program.Should().Contain("scenario.aris");
        program.Should().Contain("data.arid");
        program.Should().Contain("!HasDistributionPakInBaseDirectory()");
        json.Should().Contain("JsonSerializerContext");
        json.Should().Contain("LocalizationManifest");
        json.Should().Contain("AppConfig");
        json.Should().Contain("ChapterData");
        json.Should().Contain("CharacterData");
        localization.Should().Contain("AriaCoreJsonContext.Default.LocalizationManifest");
        config.Should().Contain("AriaCoreJsonContext.Default.AppConfig");
        config.Should().Contain("AriaCoreJsonContext.Default.PersistentGameData");
        chapters.Should().Contain("AriaCoreJsonContext.Default.ChapterData");
        characters.Should().Contain("AriaCoreJsonContext.Default.CharacterData");
        saves.Should().Contain("AriaSaveJsonContext.Default.SaveFile");
        saves.Should().Contain("AriaSaveJsonContext.Default.SaveData");
        sprites.Should().Contain("AriaSaveJsonContext.Default.DictionaryInt32Sprite");
        docs.Should().Contain("AriaDocJsonContext.Default.DocOutput");
        errors.Should().Contain("AriaCoreIndentedJsonContext.Default.ErrorLogPayload");
    }

    [Fact]
    public void PackageScript_RecordsSteamBuildMetadata()
    {
        string script = File.ReadAllText(Path.Combine(RepoRoot, "scripts", "package.ps1"));

        script.Should().Contain("[switch]$SteamBuild");
        script.Should().Contain("[string]$SteamAppId");
        script.Should().Contain("steam_appid.txt");
        script.Should().Contain("steam = [ordered]@{");
        script.Should().Contain("steamCompatible");
    }

    [Fact]
    public void ReleaseScript_PassesSteamBuildFlagsToPackageScript()
    {
        string script = File.ReadAllText(Path.Combine(RepoRoot, "scripts", "release.ps1"));

        script.Should().Contain("[switch]$SteamBuild");
        script.Should().Contain("[string]$SteamAppId");
        script.Should().Contain("$packageArgs.SteamBuild = $true");
        script.Should().Contain("$packageArgs.SteamAppId = $SteamAppId");
    }


    [Fact]
    public void PackageScript_KeepsLocalizationAssetsInProductionPackage()
    {
        string script = File.ReadAllText(Path.Combine(RepoRoot, "scripts", "package.ps1"));

        script.Should().Contain("assets\\i18n\\locales.json");
        script.Should().Contain("localization = [ordered]@{");
        script.Should().Contain("defaultLanguage = $localizationConfig.defaultLanguage");
        script.Should().Contain("scenarioStatus");
        script.Should().Contain("steamSubtitleLanguages");
    }

    [Fact]
    public void PackCommand_ClassifiesLocalizationJsonAsData()
    {
        string source = File.ReadAllText(Path.Combine(RepoRoot, "src", "AriaEngine", "Tools", "AriaPackCommand.cs"));

        source.Should().Contain("rel.StartsWith(\"i18n/\"");
        source.Should().Contain("ext == \".json\"");
        source.Should().Contain("dataEntries.Add((\"assets/\" + rel");
    }

    [Fact]
    public void PackageScript_InvokesPublishedExeDirectlyForSingleFileCli()
    {
        string script = File.ReadAllText(Path.Combine(RepoRoot, "scripts", "package.ps1"));

        script.Should().Contain("function Invoke-AriaCli");
        script.Should().Contain("if ([IO.Path]::GetExtension($CliPath).Equals(\".dll\", [StringComparison]::OrdinalIgnoreCase))");
        script.Should().Contain("Start-Process -FilePath $CliPath");
        script.Should().Contain("-Wait -PassThru");
        script.Should().Contain("$process.ExitCode");
        script.Should().NotContain("$dotnetCompileArgs = @($cliAssembly) + $compileArgs");
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
    public void ScriptOwnedUtilityScreens_UseLocalizationLookupForLabels()
    {
        string settings = File.ReadAllText(Path.Combine(RepoRoot, "src", "AriaEngine", "assets", "scripts", "settings_ui.aria"));
        string omake = File.ReadAllText(Path.Combine(RepoRoot, "src", "AriaEngine", "assets", "scripts", "omake_ui.aria"));

        settings.Should().Contain("loc_get");
        omake.Should().Contain("loc_get");
    }

    [Fact]
    public void LocalizationResources_IncludeChineseLocalesAndStoryFilePattern()
    {
        string manifest = File.ReadAllText(Path.Combine(RepoRoot, "src", "AriaEngine", "assets", "i18n", "locales.json"));
        string docs = File.ReadAllText(Path.Combine(RepoRoot, "docs", "scripting", "localization.md"));

        manifest.Should().Contain("zh-CN");
        manifest.Should().Contain("zh-TW");
        File.Exists(Path.Combine(RepoRoot, "src", "AriaEngine", "assets", "i18n", "ui.zh-CN.json")).Should().BeTrue();
        File.Exists(Path.Combine(RepoRoot, "src", "AriaEngine", "assets", "i18n", "ui.zh-TW.json")).Should().BeTrue();
        docs.Should().Contain("include \"scenario/en-US/scenario_01.aria\"");
        docs.Should().Contain("include \"scenario/zh-CN/scenario_01.aria\"");
        docs.Should().Contain("include \"scenario/zh-TW/scenario_01.aria\"");
        manifest.Should().Contain("\"scenarioRoot\"");
        manifest.Should().Contain("\"scenarioFiles\"");
        manifest.Should().Contain("\"scenarioStatus\"");

        string[] locales = ["ja-JP", "en-US", "zh-CN", "zh-TW"];
        for (int i = 1; i <= 8; i++)
        {
            foreach (string locale in locales)
            {
                File.Exists(Path.Combine(
                    RepoRoot,
                    "src",
                    "AriaEngine",
                    "assets",
                    "scripts",
                    "scenario",
                    locale,
                    $"scenario_{i:00}.aria")).Should().BeTrue();
            }
        }
    }

    [Fact]
    public void SteamReleaseDocs_AreTracked()
    {
        string steam = File.ReadAllText(Path.Combine(RepoRoot, "docs", "release", "steam.md"));

        steam.Should().Contain("Steam Cloud");
        steam.Should().Contain("steam_appid.txt");
        steam.Should().Contain("Depot");
    }

    [Fact]
    public void PlatformReleaseDocs_CoverWindowsWebLanguageAndSteamTargets()
    {
        string windows = File.ReadAllText(Path.Combine(RepoRoot, "docs", "release", "windows-native.md"));
        string qa = File.ReadAllText(Path.Combine(RepoRoot, "docs", "release", "qa-matrix.md"));
        string checklist = File.ReadAllText(Path.Combine(RepoRoot, "docs", "release", "production-checklist.md"));
        string governance = File.ReadAllText(Path.Combine(RepoRoot, "docs", "release", "release-governance.md"));

        windows.Should().Contain("win-x64-fd-singlefile");
        windows.Should().Contain("win-x64-sc-singlefile");
        windows.Should().Contain("NativeAOT stays experimental");
        windows.Should().Contain("Windows native remains the primary desktop runtime target");
        windows.Should().Contain("Web/PWA is an official browser target");
        qa.Should().Contain("zh-CN");
        qa.Should().Contain("zh-TW");
        qa.Should().Contain("Steam");
        qa.Should().Contain("Web/PWA");
        qa.Should().Contain("Chrome");
        qa.Should().Contain("Safari");
        qa.Should().Contain("mobile");
        checklist.Should().Contain("Steam builds");
        checklist.Should().Contain("NativeAOT artifacts remain experimental");
        checklist.Should().Contain("scripts/package-web.ps1");
        checklist.Should().Contain("scripts/web-device-qa.ps1");
        governance.Should().Contain("locale-specific scenario files");
        governance.Should().Contain("Windows native and Web/PWA are official runtime targets");
        governance.Should().Contain("official non-Windows release target");
        governance.Should().Contain("scripts/verify-signing.ps1 -RequireSigned");
    }

    [Fact]
    public void NativeUxPhase1_LocalizesMenuAndAddsImmediateUxPolish()
    {
        string menu = File.ReadAllText(Path.Combine(RepoRoot, "src", "AriaEngine", "UI", "MenuSystem.cs"));
        string input = File.ReadAllText(Path.Combine(RepoRoot, "src", "AriaEngine", "Input", "InputHandler.cs"));
        string renderer = File.ReadAllText(Path.Combine(RepoRoot, "src", "AriaEngine", "Rendering", "SpriteRenderer.cs"));
        string opcodes = File.ReadAllText(Path.Combine(RepoRoot, "src", "AriaEngine", "Core", "OpCode.cs"));
        string registry = File.ReadAllText(Path.Combine(RepoRoot, "src", "AriaEngine", "Core", "CommandRegistry.cs"));
        string locales = File.ReadAllText(Path.Combine(RepoRoot, "src", "AriaEngine", "assets", "i18n", "locales.json"));
        string ja = File.ReadAllText(Path.Combine(RepoRoot, "src", "AriaEngine", "assets", "i18n", "ui.ja-JP.json"));
        string en = File.ReadAllText(Path.Combine(RepoRoot, "src", "AriaEngine", "assets", "i18n", "ui.en-US.json"));

        menu.Should().Contain("T(\"menu.hint.close\")");
        menu.Should().Contain("T(\"save.status.saved\")");
        menu.Should().Contain("FormatDate(saveData.SaveTime)");
        menu.Should().Contain("RequestConfirmation(\"save_slot\"");
        input.Should().Contain("PlayUiSe(UiSeType.Hover)");
        input.Should().Contain("PlayUiSe(UiSeType.Click)");
        renderer.Should().Contain("DrawAutoModeIndicator");
        opcodes.Should().Contain("LocFormat");
        registry.Should().Contain("\"loc_format\"");
        locales.Should().Contain("\"dateFormat\"");
        ja.Should().Contain("\"confirm.save_slot\"");
        en.Should().Contain("\"confirm.save_slot\"");
    }

    [Fact]
    public void NativeUxPhase2_MenuSystemAddsGamepadNavigationForSteamDeck()
    {
        string menu = File.ReadAllText(Path.Combine(RepoRoot, "src", "AriaEngine", "UI", "MenuSystem.cs"));
        string input = File.ReadAllText(Path.Combine(RepoRoot, "src", "AriaEngine", "Input", "InputHandler.cs"));
        string vm = File.ReadAllText(Path.Combine(RepoRoot, "src", "AriaEngine", "Core", "VirtualMachine.cs"));

        menu.Should().Contain("UpdateGamepadMenuInput()");
        menu.Should().Contain("GamepadButton.MiddleRight");
        menu.Should().Contain("GamepadButton.RightFaceRight");
        menu.Should().Contain("GamepadButton.RightFaceDown");
        menu.Should().Contain("GamepadButton.LeftFaceUp");
        menu.Should().Contain("GamepadButton.LeftFaceDown");
        menu.Should().Contain("GamepadButton.LeftFaceLeft");
        menu.Should().Contain("GamepadButton.LeftFaceRight");
        menu.Should().Contain("MoveGamepadFocus");
        menu.Should().Contain("ActivateFocusedGamepadItem");
        menu.Should().Contain("AdjustFocusedSettingsValue");
        input.Should().Contain("GamepadButton.RightTrigger1");
        input.Should().Contain("IsGamepadButtonPressed(GamepadButton.RightFaceDown)");
        vm.Should().Contain("ToggleAutoMode");
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
        var result = RunProcess("git", arguments);
        if (result.ExitCode != 0)
            throw new InvalidOperationException($"git {string.Join(" ", arguments)} failed: {result.StandardError}");

        return result.StandardOutput;
    }

    private static (int ExitCode, string StandardOutput, string StandardError) RunProcess(string fileName, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
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

        return (process.ExitCode, stdout, stderr);
    }

    private static void WriteReleaseReadinessWindowsPackageEvidence(string packageDir)
    {
        Directory.CreateDirectory(packageDir);
        foreach (string relativePath in new[]
        {
            "AriaEngine.exe",
            "boot.arib",
            "scenario.aris",
            "data.arid",
            "voice.ariv",
            "scripts/scripts.ariac",
            "manifest.json",
            "checksums.txt",
            "README.md"
        })
        {
            string path = Path.Combine(packageDir, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, "ok", Encoding.UTF8);
        }
        File.WriteAllText(Path.Combine(packageDir, "manifest.json"), """
        {
          "profile": "release",
          "productionRunArgs": ["--run-mode", "release", "--profile", "release"],
          "security": {
            "browserOpenPolicy": {
              "schemes": ["https", "http"],
              "allowlist": ["store.steampowered.com", "twitter.com", "x.com", "ponkotsu-soft.vercel.app"]
            }
          },
          "localization": {
            "scenarioStatus": {
              "ja-JP": "source",
              "en-US": "pending-translation",
              "zh-CN": "pending-translation",
              "zh-TW": "pending-translation"
            },
            "steamSubtitleLanguages": ["ja-JP"]
          }
        }
        """, Encoding.UTF8);
    }

    private static void WriteReleaseReadinessNativeAotEvidence(string nativePackageDir)
    {
        Directory.CreateDirectory(nativePackageDir);
        File.WriteAllBytes(Path.Combine(nativePackageDir, "AriaEngine.exe"), new byte[] { 77, 90, 0, 0 });
        File.WriteAllText(Path.Combine(nativePackageDir, "manifest.json"), """
        {
          "runtime": "win-x64",
          "packaging": {
            "publishFlavor": "win-x64-nativeaot",
            "selfContained": true,
            "publishAot": true,
            "singleFile": true
          }
        }
        """, Encoding.UTF8);
    }

    private static void WriteReleaseReadinessWebPackageEvidence(string webPackageDir)
    {
        Directory.CreateDirectory(webPackageDir);
        foreach (string relativePath in new[]
        {
            "index.html",
            "manifest.webmanifest",
            "service-worker.js",
            "_framework/blazor.webassembly.js",
            "js/aria-web-runtime.js",
            "assets/web-text-assets.json",
            "manifest.json",
            "checksums.txt"
        })
        {
            string path = Path.Combine(webPackageDir, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, "ok", Encoding.UTF8);
        }

        File.WriteAllText(Path.Combine(webPackageDir, "manifest.json"), """
        {
          "target": "web-pwa",
          "version": "test"
        }
        """, Encoding.UTF8);
    }

    private static void WriteWebDeviceQaEvidence(string tempDir, string webDeviceQaManifest, string webVisualCompareManifest)
    {
        string chrome = Path.Combine(tempDir, "web-browser-qa-chrome.json");
        string edge = Path.Combine(tempDir, "web-browser-qa-edge.json");
        string safari = Path.Combine(tempDir, "web-browser-qa-safari.json");
        string mobile = Path.Combine(tempDir, "web-browser-qa-mobile.json");

        foreach ((string path, string browser) in new[]
        {
            (chrome, "Chrome"),
            (edge, "Edge"),
            (safari, "Safari"),
            (mobile, "mobile")
        })
        {
            File.WriteAllText(path, $$"""
            {
              "ready": true,
              "browser": "{{browser}}",
              "checks": [
                { "name": "layout16x9", "passed": true },
                { "name": "fontLoaded", "passed": true },
                { "name": "inputStart", "passed": true },
                { "name": "rightClick", "passed": true },
                { "name": "saveLoad", "passed": true },
                { "name": "consoleErrors", "passed": true }
              ]
            }
            """, Encoding.UTF8);
        }

        File.WriteAllText(webVisualCompareManifest, """
        {
          "ready": true,
          "comparisons": [
            { "screen": "title.png", "ready": true, "layoutParity": "passed", "fontParity": "passed", "hitTestParity": "covered-by-browser-qa" },
            { "screen": "text.png", "ready": true, "layoutParity": "passed", "fontParity": "passed", "hitTestParity": "covered-by-browser-qa" },
            { "screen": "menu.png", "ready": true, "layoutParity": "passed", "fontParity": "passed", "hitTestParity": "covered-by-browser-qa" }
          ]
        }
        """, Encoding.UTF8);

        File.WriteAllText(webDeviceQaManifest, $$"""
        {
          "ready": true,
          "browserReady": true,
          "visualReady": true,
          "browserManifests": [
            "{{chrome.Replace("\\", "\\\\")}}",
            "{{edge.Replace("\\", "\\\\")}}",
            "{{safari.Replace("\\", "\\\\")}}",
            "{{mobile.Replace("\\", "\\\\")}}"
          ],
          "visualManifest": "{{webVisualCompareManifest.Replace("\\", "\\\\")}}"
        }
        """, Encoding.UTF8);
    }

    private static void WriteOnePixelPng(string path)
    {
        const string png = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/p9sAAAAASUVORK5CYII=";
        File.WriteAllBytes(path, Convert.FromBase64String(png));
    }

    private static void WriteTolerancePng(string path, int changedPixels)
    {
        const int width = 100;
        const int height = 100;
        int rowStride = ((width * 3 + 3) / 4) * 4;
        byte[] pixels = new byte[rowStride * height];
        for (int i = 0; i < Math.Clamp(changedPixels, 0, width * height); i++)
        {
            int x = i % width;
            int y = height - 1 - (i / width);
            int offset = y * rowStride + x * 3;
            pixels[offset] = 255;
            pixels[offset + 1] = 255;
            pixels[offset + 2] = 255;
        }

        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write);
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: false);
        int fileSize = 54 + pixels.Length;
        writer.Write((byte)'B');
        writer.Write((byte)'M');
        writer.Write(fileSize);
        writer.Write(0);
        writer.Write(54);
        writer.Write(40);
        writer.Write(width);
        writer.Write(height);
        writer.Write((short)1);
        writer.Write((short)24);
        writer.Write(0);
        writer.Write(pixels.Length);
        writer.Write(2835);
        writer.Write(2835);
        writer.Write(0);
        writer.Write(0);
        writer.Write(pixels);
    }

}
