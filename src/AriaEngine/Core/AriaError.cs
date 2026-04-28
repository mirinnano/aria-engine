namespace AriaEngine.Core;

public enum AriaErrorLevel { Info, Warning, Error, Fatal }

public class AriaError
{
    public string Message { get; }
    public int LineNumber { get; }
    public string ScriptFile { get; }
    public AriaErrorLevel Level { get; }
    public string Code { get; }
    public string Details { get; }
    public string Hint { get; }
    public string ExceptionType { get; }
    public DateTime Timestamp { get; }

    public AriaError(
        string message,
        int lineNumber = -1,
        string scriptFile = "",
        AriaErrorLevel level = AriaErrorLevel.Error,
        string code = "ARIA000",
        string details = "",
        string hint = "",
        string exceptionType = "")
    {
        Message = message;
        LineNumber = lineNumber;
        ScriptFile = scriptFile;
        Level = level;
        Code = string.IsNullOrWhiteSpace(code) ? "ARIA000" : code;
        Details = details;
        Hint = hint;
        ExceptionType = exceptionType;
        Timestamp = DateTime.Now;
    }

    public override string ToString()
    {
        string location = LineNumber >= 0 ? $"Line {LineNumber}" : "Global";
        if (!string.IsNullOrEmpty(ScriptFile)) location += $" in {ScriptFile}";
        string code = string.IsNullOrEmpty(Code) ? "" : $" {Code}";
        return $"[{Level.ToString().ToUpper()}{code}] {location}: {Message}";
    }
}
