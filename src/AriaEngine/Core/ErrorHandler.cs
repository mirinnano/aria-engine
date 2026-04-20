using System;
using System.Collections.Generic;

namespace AriaEngine.Core;

/// <summary>
/// エラーハンドラーと警告システム
/// スクリプト実行中のエラーを収集し、グレースフルな回復を提供します。
/// </summary>
public class ErrorHandler
{
    private readonly List<ErrorEntry> _errors = new();
    private readonly List<ErrorEntry> _warnings = new();
    private readonly object _lock = new();

    /// <summary>
    /// エラーが発生したかどうか
    /// </summary>
    public bool HasErrors => _errors.Count > 0;

    /// <summary>
    /// 警告が発生したかどうか
    /// </summary>
    public bool HasWarnings => _warnings.Count > 0;

    /// <summary>
    /// エラーの総数
    /// </summary>
    public int ErrorCount => _errors.Count;

    /// <summary>
    /// 警告の総数
    /// </summary>
    public int WarningCount => _warnings.Count;

    /// <summary>
    /// 最近のエラー（null許容）
    /// </summary>
    public ErrorEntry? LastError => _errors.Count > 0 ? _errors[^1] : null;

    /// <summary>
    /// 最近の警告（null許容）
    /// </summary>
    public ErrorEntry? LastWarning => _warnings.Count > 0 ? _warnings[^1] : null;

    /// <summary>
    /// エラーを追加します。
    /// </summary>
    public void AddError(string message, ErrorSeverity severity = ErrorSeverity.Error, int? line = null, string? file = null)
    {
        lock (_lock)
        {
            var entry = new ErrorEntry
            {
                Message = message,
                Severity = severity,
                Timestamp = DateTime.Now,
                Line = line,
                File = file
            };

            _errors.Add(entry);
            Console.WriteLine($"[ERROR] {entry}");
        }
    }

    /// <summary>
    /// 警告を追加します。
    /// </summary>
    public void AddWarning(string message, int? line = null, string? file = null)
    {
        lock (_lock)
        {
            var entry = new ErrorEntry
            {
                Message = message,
                Severity = ErrorSeverity.Warning,
                Timestamp = DateTime.Now,
                Line = line,
                File = file
            };

            _warnings.Add(entry);
            Console.WriteLine($"[WARNING] {entry}");
        }
    }

    /// <summary>
    /// すべてのエラーを取得します。
    /// </summary>
    public List<ErrorEntry> GetAllErrors()
    {
        lock (_lock)
        {
            return new List<ErrorEntry>(_errors);
        }
    }

    /// <summary>
    /// すべての警告を取得します。
    /// </summary>
    public List<ErrorEntry> GetAllWarnings()
    {
        lock (_lock)
        {
            return new List<ErrorEntry>(_warnings);
        }
    }

    /// <summary>
    /// エラーと警告のサマリーを取得します。
    /// </summary>
    public string GetSummary()
    {
        lock (_lock)
        {
            if (_errors.Count == 0 && _warnings.Count == 0)
                return "No errors or warnings.";

            var summary = $"Errors: {_errors.Count}, Warnings: {_warnings.Count}";

            if (_errors.Count > 0)
            {
                summary += "\n\nRecent Errors:";
                foreach (var error in _errors.Take(5))
                {
                    summary += $"\n  {error}";
                }
            }

            if (_warnings.Count > 0)
            {
                summary += "\n\nRecent Warnings:";
                foreach (var warning in _warnings.Take(5))
                {
                    summary += $"\n  {warning}";
                }
            }

            return summary;
        }
    }

    /// <summary>
    /// エラーと警告をクリアします。
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _errors.Clear();
            _warnings.Clear();
        }
    }

    /// <summary>
    /// スタックトレース付きエラーを追加します。
    /// </summary>
    public void AddErrorWithStack(string message, Exception? exception = null, int? line = null, string? file = null)
    {
        var fullMessage = message;
        if (exception != null)
        {
            fullMessage += $"\nException: {exception.GetType().Name}: {exception.Message}";
            fullMessage += $"\nStack Trace:\n{exception.StackTrace}";
        }

        AddError(fullMessage, ErrorSeverity.Error, line, file);
    }

    /// <summary>
    /// 未定義ラベルの警告を追加します。
    /// </summary>
    public void AddUndefinedLabelWarning(string label, int? line = null, string? file = null)
    {
        var message = $"Undefined label: '{label}'. This may cause unexpected behavior.";
        AddWarning(message, line, file);
    }

    /// <summary>
    /// 安全なコードブロック実行。
    /// </summary>
    public bool SafeExecute(Action action, string context = "Unknown operation")
    {
        try
        {
            action();
            return true;
        }
        catch (Exception ex)
        {
            AddErrorWithStack($"Exception in {context}", ex);
            return false;
        }
    }

    /// <summary>
    /// 安全なコードブロック実行（戻り値あり）。
    /// </summary>
    public T SafeExecute<T>(Func<T> func, T defaultValue, string context = "Unknown operation")
    {
        try
        {
            return func();
        }
        catch (Exception ex)
        {
            AddErrorWithStack($"Exception in {context}", ex);
            return defaultValue;
        }
    }
}

/// <summary>
/// エラーレベル
/// </summary>
public enum ErrorSeverity
{
    Info,
    Warning,
    Error,
    Critical
}

/// <summary>
/// エラーエントリー
/// </summary>
public class ErrorEntry
{
    public string Message { get; set; } = string.Empty;
    public ErrorSeverity Severity { get; set; }
    public DateTime Timestamp { get; set; }
    public int? Line { get; set; }
    public string? File { get; set; }

    public override string ToString()
    {
        var location = string.IsNullOrEmpty(File) ? $"Line {Line}" : $"{File}:{Line}";
        return $"[{Severity}] {Timestamp:HH:mm:ss} {location} - {Message}";
    }
}
