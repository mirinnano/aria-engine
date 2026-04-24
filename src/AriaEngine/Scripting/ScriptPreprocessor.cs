using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AriaEngine.Assets;

namespace AriaEngine.Scripting;

public sealed class ExpandedScript
{
    public string ScriptPath { get; init; } = "";
    public string[] Lines { get; init; } = Array.Empty<string>();
    public HashSet<string> Dependencies { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

public static class ScriptPreprocessor
{
    public static ExpandedScript ExpandIncludes(string scriptPath, IAssetProvider provider)
    {
        string normalizedRoot = NormalizePath(scriptPath);
        var lines = new List<string>();
        var deps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var stack = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        ExpandCore(normalizedRoot, provider, lines, deps, stack);

        return new ExpandedScript
        {
            ScriptPath = normalizedRoot,
            Lines = lines.ToArray(),
            Dependencies = deps
        };
    }

    private static void ExpandCore(
        string scriptPath,
        IAssetProvider provider,
        List<string> output,
        HashSet<string> deps,
        HashSet<string> stack)
    {
        string normalized = NormalizePath(scriptPath);
        if (!provider.Exists(normalized))
            throw new FileNotFoundException($"Script file not found: {normalized}");

        if (stack.Contains(normalized))
            throw new InvalidOperationException($"include cycle detected: {normalized}");

        stack.Add(normalized);
        deps.Add(normalized);

        string[] lines = provider.ReadAllLines(normalized);
        string baseDir = GetDirectory(normalized);

        foreach (string raw in lines)
        {
            string line = raw.Trim();
            if (TryParseInclude(line, out string includePath))
            {
                string resolved = NormalizePath(ResolveRelative(baseDir, includePath));
                ExpandCore(resolved, provider, output, deps, stack);
                continue;
            }
            output.Add(raw);
        }

        stack.Remove(normalized);
    }

    private static bool TryParseInclude(string line, out string includePath)
    {
        includePath = "";
        if (!line.StartsWith("include ", StringComparison.OrdinalIgnoreCase))
            return false;

        int firstQuote = line.IndexOf('"');
        if (firstQuote < 0) return false;
        int secondQuote = line.IndexOf('"', firstQuote + 1);
        if (secondQuote < 0) return false;
        includePath = line.Substring(firstQuote + 1, secondQuote - firstQuote - 1);
        return !string.IsNullOrWhiteSpace(includePath);
    }

    private static string ResolveRelative(string baseDir, string includePath)
    {
        if (Path.IsPathRooted(includePath)) return includePath;
        if (string.IsNullOrEmpty(baseDir)) return includePath;
        return Path.Combine(baseDir, includePath);
    }

    private static string GetDirectory(string path)
    {
        int idx = path.LastIndexOf('/');
        return idx < 0 ? "" : path[..idx];
    }

    public static string NormalizePath(string path) => path.Replace('\\', '/').TrimStart('/');
}
