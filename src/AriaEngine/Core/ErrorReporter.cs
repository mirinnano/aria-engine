using System.Collections.Generic;
using System.IO;
using System;
using System.Linq;
using System.Text.Json;

namespace AriaEngine.Core;

public class ErrorReporter
{
    private const int MaxStoredErrors = 1000;
    private readonly List<AriaError> _errors = new();
    private readonly Dictionary<string, int> _dedupe = new(StringComparer.Ordinal);
    private int _droppedCount;
    public bool HasFatalError => _errors.Any(e => e.Level == AriaErrorLevel.Fatal);
    public IReadOnlyList<AriaError> Errors => _errors;

    public void Report(AriaError error)
    {
        string key = $"{error.Level}|{error.Code}|{error.ScriptFile}|{error.LineNumber}|{error.Message}";
        _dedupe.TryGetValue(key, out int count);
        _dedupe[key] = count + 1;

        if (count == 0)
        {
            if (_errors.Count < MaxStoredErrors)
            {
                _errors.Add(error);
            }
            else
            {
                _droppedCount++;
            }

            Console.WriteLine(error.ToString());
            if (!string.IsNullOrWhiteSpace(error.Hint)) Console.WriteLine($"  hint: {error.Hint}");
        }
    }

    public void ReportException(
        string code,
        Exception exception,
        string context,
        AriaErrorLevel level = AriaErrorLevel.Error,
        string scriptFile = "",
        int lineNumber = -1,
        string hint = "")
    {
        Report(new AriaError(
            context,
            lineNumber,
            scriptFile,
            level,
            code,
            exception.ToString(),
            hint,
            exception.GetType().FullName ?? exception.GetType().Name));
    }

    public void WriteLogFile(string path = "aria_error.log")
    {
        if (_errors.Count == 0) return;

        using var writer = new StreamWriter(path);
        writer.WriteLine("===================================");
        writer.WriteLine("  ARIA Engine Error Log");
        writer.WriteLine($"  {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        writer.WriteLine("===================================\n");

        foreach (var err in _errors)
        {
            writer.WriteLine(err.ToString());
            if (!string.IsNullOrWhiteSpace(err.ExceptionType)) writer.WriteLine($"Exception: {err.ExceptionType}");
            if (!string.IsNullOrWhiteSpace(err.Hint)) writer.WriteLine($"Hint: {err.Hint}");
            if (!string.IsNullOrWhiteSpace(err.Details)) writer.WriteLine(err.Details);
            writer.WriteLine();
        }

        if (_droppedCount > 0)
        {
            writer.WriteLine($"Dropped duplicated/overflow errors: {_droppedCount}");
        }

        WriteAiCopyLog(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(path)) ?? "", "aria_error_ai.txt"));
        WriteJsonLog(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(path)) ?? "", "aria_error_ai.json"));
    }

    private void WriteAiCopyLog(string path)
    {
        using var writer = new StreamWriter(path);
        writer.WriteLine("=== ARIA AI DEBUG PACK START ===");
        writer.WriteLine($"Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss zzz}");
        writer.WriteLine($"OS: {Environment.OSVersion}");
        writer.WriteLine($".NET: {Environment.Version}");
        writer.WriteLine($"WorkingDirectory: {Environment.CurrentDirectory}");
        writer.WriteLine($"ErrorCount: {_errors.Count}");
        writer.WriteLine($"DroppedCount: {_droppedCount}");
        writer.WriteLine();

        for (int i = 0; i < _errors.Count; i++)
        {
            var err = _errors[i];
            writer.WriteLine($"[{i + 1}] Level={err.Level} Code={err.Code}");
            writer.WriteLine($"Location: {(string.IsNullOrWhiteSpace(err.ScriptFile) ? "(global)" : err.ScriptFile)}:{err.LineNumber}");
            writer.WriteLine($"Message: {err.Message}");
            if (!string.IsNullOrWhiteSpace(err.Hint)) writer.WriteLine($"Hint: {err.Hint}");
            if (!string.IsNullOrWhiteSpace(err.ExceptionType)) writer.WriteLine($"ExceptionType: {err.ExceptionType}");
            if (!string.IsNullOrWhiteSpace(err.Details))
            {
                writer.WriteLine("Details:");
                writer.WriteLine(err.Details);
            }
            writer.WriteLine();
        }

        writer.WriteLine("=== ARIA AI DEBUG PACK END ===");
    }

    private void WriteJsonLog(string path)
    {
        var payload = new
        {
            time = DateTimeOffset.Now,
            os = Environment.OSVersion.ToString(),
            dotnet = Environment.Version.ToString(),
            workingDirectory = Environment.CurrentDirectory,
            droppedCount = _droppedCount,
            errors = _errors.Select(e => new
            {
                e.Level,
                e.Code,
                e.Message,
                e.ScriptFile,
                e.LineNumber,
                e.Hint,
                e.ExceptionType,
                e.Details,
                e.Timestamp
            })
        };

        File.WriteAllText(path, JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));
    }

    public void Clear()
    {
        _errors.Clear();
        _dedupe.Clear();
        _droppedCount = 0;
    }
}
