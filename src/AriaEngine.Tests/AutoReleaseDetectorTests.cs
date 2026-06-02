using System;
using System.Collections.Generic;
using System.IO;
using AriaEngine.Scripting;
using FluentAssertions;
using Xunit;

namespace AriaEngine.Tests;

public class AutoReleaseDetectorTests
{
    private static Program.RunOptions DefaultDevOptions() => new()
    {
        Mode = RunMode.Dev,
        PakPath = null,
        CompiledPath = "scripts/scripts.ariac",
        Key = null,
    };

    [Fact]
    public void Detect_NoFiles_ReturnsNone()
    {
        var result = AutoReleaseDetector.Detect("/non/existent/path");
        result.Kind.Should().Be(AutoReleaseDetector.AutoReleaseKind.None);
    }

    [Fact]
    public void Detect_OnlyDataArid_ReturnsNone_StraySinglePak()
    {
        // The original bug: a single stray v3 pak used to flip to Release.
        // Now we require the mandatory pair (boot.arib + scenario.aris).
        var existing = new HashSet<string>(StringComparer.Ordinal)
        {
            "/some/dir/data.arid",
        };
        var result = AutoReleaseDetector.Detect(
            "/some/dir",
            fileExists: p => existing.Contains(p));
        result.Kind.Should().Be(AutoReleaseDetector.AutoReleaseKind.None);
    }

    [Fact]
    public void Detect_OnlyBootPak_ReturnsNone()
    {
        var existing = new HashSet<string>(StringComparer.Ordinal)
        {
            "/some/dir/boot.arib",
        };
        var result = AutoReleaseDetector.Detect(
            "/some/dir",
            fileExists: p => existing.Contains(p));
        result.Kind.Should().Be(AutoReleaseDetector.AutoReleaseKind.None);
    }

    [Fact]
    public void Detect_OnlyScenarioPak_ReturnsNone()
    {
        var existing = new HashSet<string>(StringComparer.Ordinal)
        {
            "/some/dir/scenario.aris",
        };
        var result = AutoReleaseDetector.Detect(
            "/some/dir",
            fileExists: p => existing.Contains(p));
        result.Kind.Should().Be(AutoReleaseDetector.AutoReleaseKind.None);
    }

    [Fact]
    public void Detect_BothBootAndScenario_ReturnsV3Split()
    {
        var existing = new HashSet<string>(StringComparer.Ordinal)
        {
            Path.Combine("/some/dir", "boot.arib"),
            Path.Combine("/some/dir", "scenario.aris"),
        };
        var result = AutoReleaseDetector.Detect(
            "/some/dir",
            fileExists: p => existing.Contains(p));
        result.Kind.Should().Be(AutoReleaseDetector.AutoReleaseKind.V3Split);
    }

    [Fact]
    public void Detect_V3FullSet_ReturnsV3Split()
    {
        // All 6 v3 paks present — still v3 split detection.
        var existing = new HashSet<string>(StringComparer.Ordinal)
        {
            Path.Combine("/some/dir", "boot.arib"),
            Path.Combine("/some/dir", "scenario.aris"),
            Path.Combine("/some/dir", "data.arid"),
            Path.Combine("/some/dir", "stream.arim"),
            Path.Combine("/some/dir", "voice.ariv"),
        };
        var result = AutoReleaseDetector.Detect(
            "/some/dir",
            fileExists: p => existing.Contains(p));
        result.Kind.Should().Be(AutoReleaseDetector.AutoReleaseKind.V3Split);
    }

    [Fact]
    public void Detect_OnlyDataPak_ReturnsNone()
    {
        // v2 requires BOTH data.pak AND scripts.ariac.
        var existing = new HashSet<string>(StringComparer.Ordinal)
        {
            "/some/dir/data.pak",
        };
        var result = AutoReleaseDetector.Detect(
            "/some/dir",
            fileExists: p => existing.Contains(p));
        result.Kind.Should().Be(AutoReleaseDetector.AutoReleaseKind.None);
    }

    [Fact]
    public void Detect_DataPakAndScripts_ReturnsV2SinglePak()
    {
        var existing = new HashSet<string>(StringComparer.Ordinal)
        {
            Path.Combine("/some/dir", "data.pak"),
            Path.Combine("/some/dir", "scripts", "scripts.ariac"),
        };
        var result = AutoReleaseDetector.Detect(
            "/some/dir",
            fileExists: p => existing.Contains(p));
        result.Kind.Should().Be(AutoReleaseDetector.AutoReleaseKind.V2SinglePak);
    }

    [Fact]
    public void Apply_V3Detection_UpgradesModeToRelease()
    {
        var options = DefaultDevOptions();
        var detection = new AutoReleaseDetector.Result { Kind = AutoReleaseDetector.AutoReleaseKind.V3Split };
        AutoReleaseDetector.Apply(options, detection, "/some/dir", envAutoRelease: null,
            fileExists: _ => false);
        options.Mode.Should().Be(RunMode.Release);
        options.PakPath.Should().BeNull("v3 split does not use the legacy v2 PakPath field");
    }

    [Fact]
    public void Apply_V2Detection_UpgradesModeAndSetsPakPath()
    {
        var options = DefaultDevOptions();
        var detection = new AutoReleaseDetector.Result { Kind = AutoReleaseDetector.AutoReleaseKind.V2SinglePak };
        AutoReleaseDetector.Apply(options, detection, "/some/dir", envAutoRelease: null,
            fileExists: _ => false);
        options.Mode.Should().Be(RunMode.Release);
        options.PakPath.Should().Be("data.pak");
    }

    [Fact]
    public void Apply_EnvZero_DisablesAutoRelease()
    {
        var options = DefaultDevOptions();
        var detection = new AutoReleaseDetector.Result { Kind = AutoReleaseDetector.AutoReleaseKind.V3Split };
        AutoReleaseDetector.Apply(options, detection, "/some/dir", envAutoRelease: "0",
            fileExists: _ => true);
        options.Mode.Should().Be(RunMode.Dev);
    }

    [Fact]
    public void Apply_ExplicitReleaseMode_DoesNotDoubleApply()
    {
        // If user passed --run-mode release, do not touch options further.
        var options = new Program.RunOptions { Mode = RunMode.Release, PakPath = "explicit.pak" };
        var detection = new AutoReleaseDetector.Result { Kind = AutoReleaseDetector.AutoReleaseKind.V3Split };
        AutoReleaseDetector.Apply(options, detection, "/some/dir", envAutoRelease: null,
            fileExists: _ => true);
        options.Mode.Should().Be(RunMode.Release);
        options.PakPath.Should().Be("explicit.pak", "explicit pak path must be preserved");
    }

    [Fact]
    public void Apply_ExplicitPakPath_DoesNotOverwrite()
    {
        var options = new Program.RunOptions { Mode = RunMode.Dev, PakPath = "my.pak" };
        var detection = new AutoReleaseDetector.Result { Kind = AutoReleaseDetector.AutoReleaseKind.V2SinglePak };
        AutoReleaseDetector.Apply(options, detection, "/some/dir", envAutoRelease: null,
            fileExists: _ => true);
        options.PakPath.Should().Be("my.pak", "explicit PakPath on Dev mode opts out of auto-detection");
    }

    [Fact]
    public void Apply_KeyFile_LoadsEncryptionKey()
    {
        var options = DefaultDevOptions();
        var detection = new AutoReleaseDetector.Result { Kind = AutoReleaseDetector.AutoReleaseKind.V3Split };
        var keyFile = Path.Combine(Path.GetTempPath(), "aria.key");
        AutoReleaseDetector.Apply(
            options,
            detection,
            Path.GetTempPath(),
            envAutoRelease: null,
            readKeyFile: _ => "test-secret-key\n",
            fileExists: p => p == keyFile);
        options.Key.Should().Be("test-secret-key", "Key should be loaded and trimmed");
    }

    [Fact]
    public void Apply_ExistingKey_NotOverwritten()
    {
        var options = new Program.RunOptions { Mode = RunMode.Dev, Key = "user-supplied" };
        var detection = new AutoReleaseDetector.Result { Kind = AutoReleaseDetector.AutoReleaseKind.V3Split };
        AutoReleaseDetector.Apply(
            options,
            detection,
            Path.GetTempPath(),
            envAutoRelease: null,
            readKeyFile: _ => "file-key",
            fileExists: _ => true);
        options.Key.Should().Be("user-supplied", "user-supplied key takes precedence");
    }
}
