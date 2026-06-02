using System;
using System.IO;
using AriaEngine.Scripting;

namespace AriaEngine;

/// <summary>
/// Pak v3 release auto-detection (bug fix for "dev ビルドのはずなのに release の
/// pak が読み込まれる" UX issue).
///
/// Pure function (no I/O inside the test surface). Callers pass the directory
/// to inspect and the current <see cref="RunOptions"/>; this class returns the
/// upgraded options (or the original if no auto-detection applies).
///
/// Detection rules:
/// <list type="bullet">
///   <item>v3 split: <c>boot.arib</c> AND <c>scenario.aris</c> both present.</item>
///   <item>v2 single-pak: <c>data.pak</c> AND <c>scripts/scripts.ariac</c> both present.</item>
///   <item>Opt-out: <c>ARIA_AUTO_RELEASE=0</c> disables auto-detection entirely.</item>
/// </list>
///
/// The previous behaviour was an <c>Any()</c> over the v3 files which flipped to
/// Release on a single stray <c>data.arid</c> left in the dev output directory.
/// </summary>
internal static class AutoReleaseDetector
{
    public enum AutoReleaseKind
    {
        None,
        V3Split,
        V2SinglePak
    }

    public readonly struct Result
    {
        public AutoReleaseKind Kind { get; init; }
        public bool Detected => Kind != AutoReleaseKind.None;
    }

    /// <summary>
    /// Inspect <paramref name="exeDir"/> for v3 / v2 release paks. Returns the kind
    /// detected (or <see cref="AutoReleaseKind.None"/> when auto-detection does
    /// not apply). The caller is responsible for the I/O — this method only
    /// consults <paramref name="fileExists"/>.
    /// </summary>
    public static Result Detect(
        string exeDir,
        Func<string, bool>? fileExists = null)
    {
        fileExists ??= File.Exists;

        string bootPath = Path.Combine(exeDir, "boot.arib");
        string scenarioPath = Path.Combine(exeDir, "scenario.aris");
        bool v3Detected = fileExists(bootPath) && fileExists(scenarioPath);
        if (v3Detected)
        {
            return new Result { Kind = AutoReleaseKind.V3Split };
        }

        string autoPak = Path.Combine(exeDir, "data.pak");
        string autoCompiled = Path.Combine(exeDir, "scripts", "scripts.ariac");
        bool v2Detected = fileExists(autoPak) && fileExists(autoCompiled);
        if (v2Detected)
        {
            return new Result { Kind = AutoReleaseKind.V2SinglePak };
        }

        return new Result { Kind = AutoReleaseKind.None };
    }

    /// <summary>
    /// Apply the auto-detection result to <paramref name="runOptions"/>. Mutates
    /// the passed <see cref="Program.RunOptions"/> in place (Mode, PakPath,
    /// CompiledPath, Key) when detection succeeds. Returns the same instance for
    /// chaining.
    /// </summary>
    public static Program.RunOptions Apply(
        Program.RunOptions runOptions,
        Result detection,
        string exeDir,
        string? envAutoRelease,
        Func<string, string?>? readKeyFile = null,
        Func<string, bool>? fileExists = null)
    {
        fileExists ??= File.Exists;
        readKeyFile ??= File.ReadAllText;

        // Opt-out via env var.
        if (string.Equals(envAutoRelease, "0", StringComparison.Ordinal))
        {
            return runOptions;
        }

        // Only auto-detect in dev mode without an explicit pak.
        if (runOptions.Mode != RunMode.Dev)
        {
            return runOptions;
        }
        if (!string.IsNullOrWhiteSpace(runOptions.PakPath))
        {
            return runOptions;
        }

        if (!detection.Detected)
        {
            return runOptions;
        }

        runOptions.Mode = RunMode.Release;

        if (detection.Kind == AutoReleaseKind.V2SinglePak)
        {
            runOptions.PakPath = "data.pak";
        }

        string autoCompiled = Path.Combine(exeDir, "scripts", "scripts.ariac");
        if (fileExists(autoCompiled))
        {
            runOptions.CompiledPath = "scripts/scripts.ariac";
        }

        string keyFile = Path.Combine(exeDir, "aria.key");
        if (fileExists(keyFile) && string.IsNullOrEmpty(runOptions.Key))
        {
            runOptions.Key = (readKeyFile(keyFile) ?? string.Empty).Trim();
        }

        return runOptions;
    }
}
