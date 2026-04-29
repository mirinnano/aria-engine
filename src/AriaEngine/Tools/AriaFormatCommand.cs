using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace AriaEngine.Tools;

public static class AriaFormatCommand
{
    private static readonly HashSet<string> BlockOpeners = new(StringComparer.OrdinalIgnoreCase)
    {
        "if", "while", "func", "scope", "match", "try", "switch", "for"
    };

    private static readonly HashSet<string> BlockClosers = new(StringComparer.OrdinalIgnoreCase)
    {
        "endif", "wend", "endfunc", "end_scope", "endmatch", "endtry", "endswitch", "next"
    };

    public static int Run(string[] args)
    {
        bool write = false;
        string? inputPath = null;

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--write")
                write = true;
            else if (!args[i].StartsWith("--") && string.IsNullOrEmpty(inputPath))
                inputPath = args[i];
        }

        if (string.IsNullOrEmpty(inputPath))
        {
            Console.WriteLine("Usage: aria-format <script.aria> [--write]");
            return 1;
        }

        if (!File.Exists(inputPath))
        {
            Console.Error.WriteLine($"File not found: {inputPath}");
            return 1;
        }

        string[] lines = File.ReadAllLines(inputPath);
        var formatted = FormatLines(lines);
        string output = string.Join(Environment.NewLine, formatted) + Environment.NewLine;

        if (write)
        {
            File.WriteAllText(inputPath, output);
            Console.Error.WriteLine($"Formatted: {inputPath}");
        }
        else
        {
            Console.Write(output);
        }

        return 0;
    }

    public static List<string> FormatLines(string[] lines)
    {
        var result = new List<string>();
        int depth = 0;
        string? pendingLabel = null;

        for (int i = 0; i < lines.Length; i++)
        {
            string raw = lines[i];
            string trimmed = raw.TrimEnd();

            // Empty line handling
            if (string.IsNullOrWhiteSpace(raw))
            {
                if (result.Count > 0 && pendingLabel == null)
                {
                    // Skip duplicate blank lines, but keep one before logical blocks
                    int lastNonEmpty = result.Count - 1;
                    while (lastNonEmpty >= 0 && string.IsNullOrWhiteSpace(result[lastNonEmpty]))
                        lastNonEmpty--;
                    if (lastNonEmpty >= 0)
                    {
                        string last = result[lastNonEmpty];
                        bool lastIsLabel = last.TrimStart().StartsWith("*");
                        bool lastIsBlockEnd = IsBlockCloser(last.TrimStart());
                        if (lastIsLabel || lastIsBlockEnd)
                        {
                            result.Add("");
                        }
                    }
                }
                continue;
            }

            // Detect comments
            bool isComment = trimmed.StartsWith(";");

            // Detect label
            bool isLabel = trimmed.StartsWith("*");

            // Strip and parse the line content
            string content = trimmed;
            string firstToken = "";
            if (!isComment && !isLabel)
            {
                firstToken = GetFirstToken(content);
            }

            // Track block depth
            if (!isComment && !isLabel && BlockClosers.Contains(firstToken))
            {
                depth = Math.Max(0, depth - 1);
            }

            // Build formatted line
            string formatted;
            if (isLabel)
            {
                pendingLabel = null;
                formatted = raw.TrimStart();
            }
            else if (isComment)
            {
                // Align comment with current depth
                int commentDepth = depth;
                formatted = raw;
            }
            else
            {
                // Regular command - apply indentation
                string indent = new string(' ', depth * 4);
                formatted = indent + NormalizeSpaces(content);
            }

            result.Add(formatted);

            if (!isComment && !isLabel && BlockOpeners.Contains(firstToken))
            {
                depth++;
            }
        }

        // Trim trailing blank lines (keep max 1)
        while (result.Count > 0 && string.IsNullOrWhiteSpace(result[^1]))
            result.RemoveAt(result.Count - 1);
        if (result.Count > 0)
            result.Add("");

        return result;
    }

    private static bool IsBlockCloser(string content)
    {
        string first = GetFirstToken(content);
        return BlockClosers.Contains(first);
    }

    private static string GetFirstToken(string content)
    {
        content = content.TrimStart();
        int spaceIdx = content.IndexOf(' ');
        int braceIdx = content.IndexOf('{');
        int endIdx = spaceIdx >= 0 ? (braceIdx >= 0 ? Math.Min(spaceIdx, braceIdx) : spaceIdx)
                                  : (braceIdx >= 0 ? braceIdx : content.Length);
        return content.Substring(0, endIdx).ToLowerInvariant();
    }

    private static string NormalizeSpaces(string content)
    {
        content = content.Trim();

        // Handle quoted strings - preserve internal spacing
        var result = new StringBuilder();
        bool inQuote = false;
        char prev = '\0';

        for (int i = 0; i < content.Length; i++)
        {
            char c = content[i];

            if (c == '"')
            {
                inQuote = !inQuote;
                result.Append(c);
            }
            else if (inQuote)
            {
                result.Append(c);
            }
            else if (char.IsWhiteSpace(c))
            {
                // Collapse multiple spaces to single, but preserve at least one
                if (prev != ' ' && prev != '\t')
                    result.Append(' ');
            }
            else
            {
                result.Append(c);
            }
            prev = c;
        }

        return result.ToString().Trim();
    }
}
