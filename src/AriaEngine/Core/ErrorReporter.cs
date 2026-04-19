using System.Collections.Generic;
using System.IO;
using System;
using System.Linq;

namespace AriaEngine.Core;

public class ErrorReporter
{
    private readonly List<AriaError> _errors = new();
    public bool HasFatalError => _errors.Any(e => e.Level == AriaErrorLevel.Fatal);
    public IReadOnlyList<AriaError> Errors => _errors;

    public void Report(AriaError error)
    {
        _errors.Add(error);
        Console.WriteLine(error.ToString());
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
            writer.WriteLine();
        }
    }

    public void Clear() => _errors.Clear();
}
