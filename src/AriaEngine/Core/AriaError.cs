namespace AriaEngine.Core;

public enum AriaErrorLevel { Warning, Error, Fatal }

public class AriaError
{
    public string Message { get; }
    public int LineNumber { get; }
    public string ScriptFile { get; }
    public AriaErrorLevel Level { get; }

    public AriaError(string message, int lineNumber = -1, string scriptFile = "", AriaErrorLevel level = AriaErrorLevel.Error)
    {
        Message = message;
        LineNumber = lineNumber;
        ScriptFile = scriptFile;
        Level = level;
    }

    public override string ToString()
    {
        string location = LineNumber >= 0 ? $"Line {LineNumber}" : "Global";
        if (!string.IsNullOrEmpty(ScriptFile)) location += $" in {ScriptFile}";
        return $"[{Level.ToString().ToUpper()}] {location}: {Message}";
    }
}
