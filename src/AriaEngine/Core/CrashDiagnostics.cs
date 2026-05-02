using System;
using System.IO;
using System.IO.Compression;
using System.Text.Json;

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

        var summary = new
        {
            generatedAt = DateTimeOffset.Now,
            workingDirectory = Environment.CurrentDirectory,
            os = Environment.OSVersion.ToString(),
            dotnet = Environment.Version.ToString(),
            exception = exception?.ToString(),
            state = state == null ? null : new
            {
                state.Execution.ProgramCounter,
                state.Execution.State,
                state.EngineSettings.MainScript,
                state.SaveRuntime.CurrentChapter,
                state.SaveRuntime.CurrentProgress,
                state.EngineSettings.ProductionMode,
                SpriteCount = state.Render.Sprites.Count,
                TextLength = state.TextRuntime.CurrentTextBuffer.Length
            }
        };

        File.WriteAllText(Path.Combine(workDir, "summary.json"), JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true }));
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
