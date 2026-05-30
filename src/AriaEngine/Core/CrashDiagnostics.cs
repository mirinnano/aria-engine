using System;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AriaEngine.Core;

public static class CrashDiagnostics
{
    public static string WriteZip(ErrorReporter reporter, GameState? state, Exception? exception = null, string outputDir = "diagnostics")
    {
        Directory.CreateDirectory(outputDir);
        string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        string workDir = Path.Combine(outputDir, $"aria-diagnostics-{stamp}");
        string zipPath = workDir + ".zip";

        if (Directory.Exists(workDir)) Directory.Delete(workDir, recursive: true);
        Directory.CreateDirectory(workDir);

        var summary = new CrashDiagnosticsSummary
        {
            GeneratedAt = DateTimeOffset.Now,
            WorkingDirectory = Environment.CurrentDirectory,
            Os = Environment.OSVersion.ToString(),
            Dotnet = Environment.Version.ToString(),
            Exception = exception?.ToString(),
            State = state == null ? null : new CrashDiagnosticsState
            {
                ProgramCounter = state.Execution.ProgramCounter,
                State = state.Execution.State,
                MainScript = state.EngineSettings.MainScript,
                CurrentChapter = state.SaveRuntime.CurrentChapter,
                CurrentProgress = state.SaveRuntime.CurrentProgress,
                ProductionMode = state.EngineSettings.ProductionMode,
                SpriteCount = state.Render.Sprites.Count,
                TextLength = state.TextRuntime.CurrentTextBuffer.Length
            }
        };

        File.WriteAllText(Path.Combine(workDir, "summary.json"), JsonSerializer.Serialize(summary, AriaCoreIndentedJsonContext.Default.CrashDiagnosticsSummary));
        CopyIfExists("aria_error.log", workDir);
        CopyIfExists("aria_error_ai.txt", workDir);
        CopyIfExists("aria_error_ai.json", workDir);
        CopyIfExists("config.json", workDir);
        CopyIfExists(Path.Combine("saves", "persistent.ariasav"), workDir);
        CopyDirectoryIfExists("saves", Path.Combine(workDir, "saves"));

        reporter.WriteLogFile(Path.Combine(workDir, "aria_error.log"));

        if (File.Exists(zipPath)) File.Delete(zipPath);
        ZipFile.CreateFromDirectory(workDir, zipPath, CompressionLevel.SmallestSize, includeBaseDirectory: true);
        Directory.Delete(workDir, recursive: true);
        return zipPath;
    }

    private static void CopyIfExists(string path, string destDir)
    {
        if (File.Exists(path)) File.Copy(path, Path.Combine(destDir, Path.GetFileName(path)), overwrite: true);
    }

    private static void CopyDirectoryIfExists(string path, string destDir)
    {
        if (!Directory.Exists(path)) return;
        Directory.CreateDirectory(destDir);
        foreach (string file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(path, file);
            string dest = Path.Combine(destDir, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(dest) ?? destDir);
            File.Copy(file, dest, overwrite: true);
        }
    }
}

internal sealed class CrashDiagnosticsSummary
{
    [JsonPropertyName("generatedAt")]
    public DateTimeOffset GeneratedAt { get; set; }
    [JsonPropertyName("workingDirectory")]
    public string WorkingDirectory { get; set; } = "";
    [JsonPropertyName("os")]
    public string Os { get; set; } = "";
    [JsonPropertyName("dotnet")]
    public string Dotnet { get; set; } = "";
    [JsonPropertyName("exception")]
    public string? Exception { get; set; }
    [JsonPropertyName("state")]
    public CrashDiagnosticsState? State { get; set; }
}

internal sealed class CrashDiagnosticsState
{
    public int ProgramCounter { get; set; }
    public VmState State { get; set; }
    public string MainScript { get; set; } = "";
    public string CurrentChapter { get; set; } = "";
    public int CurrentProgress { get; set; }
    public bool ProductionMode { get; set; }
    public int SpriteCount { get; set; }
    public int TextLength { get; set; }
}
