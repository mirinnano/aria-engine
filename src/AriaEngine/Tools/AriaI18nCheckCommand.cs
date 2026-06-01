using System.Text.Json;
using System.Text.RegularExpressions;
using AriaEngine.Core;

namespace AriaEngine.Tools;

public static class AriaI18nCheckCommand
{
    private static readonly string[] ResourceKeyPrefixes =
    {
        "backlog.",
        "common.",
        "confirm.",
        "extra.",
        "gallery.",
        "menu.",
        "save.",
        "settings."
    };

    private static readonly Regex LocalizationKeyPattern = new(
        @"\b(?:loc_get|tr|loc_format)\s+[^,\r\n]+,\s*""([^""]+)""",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex CodeLocalizationKeyPattern = new(
        @"\b(?:T|F|Get|Format)\s*\(\s*""([^""]+)""",
        RegexOptions.Compiled);
    private static readonly Regex DottedStringLiteralPattern = new(
        @"""([a-z][a-z0-9_]*(?:\.[a-z][a-z0-9_]*)+)""",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex IncludePattern = new(
        @"^\s*include\s+""([^""]+)""",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static int Run(string[] args)
    {
        string root = ".";
        string manifestPath = "assets/i18n/locales.json";
        var scriptInputs = new List<string>();
        var codeInputs = new List<string>();
        bool verbose = false;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--help":
                case "-h":
                    PrintHelp();
                    return 0;
                case "--root" when i + 1 < args.Length:
                    root = args[++i];
                    break;
                case "--manifest" when i + 1 < args.Length:
                    manifestPath = args[++i];
                    break;
                case "--scripts" when i + 1 < args.Length:
                    scriptInputs.Add(args[++i]);
                    break;
                case "--code" when i + 1 < args.Length:
                    codeInputs.Add(args[++i]);
                    break;
                case "--verbose":
                case "-v":
                    verbose = true;
                    break;
                default:
                    if (!args[i].StartsWith("--", StringComparison.Ordinal))
                    {
                        scriptInputs.Add(args[i]);
                    }
                    break;
            }
        }

        if (scriptInputs.Count == 0)
        {
            scriptInputs.Add("assets/scripts");
        }

        string rootFullPath = Path.GetFullPath(root);
        string manifestFullPath = ResolvePath(rootFullPath, manifestPath);
        if (!File.Exists(manifestFullPath))
        {
            Console.Error.WriteLine($"aria-i18n-check: manifest not found: {manifestFullPath}");
            return 2;
        }

        LocalizationManifest manifest;
        try
        {
            manifest = JsonSerializer.Deserialize(
                File.ReadAllText(manifestFullPath),
                AriaCoreJsonContext.Default.LocalizationManifest) ?? new LocalizationManifest();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"aria-i18n-check: failed to read manifest: {ex.Message}");
            return 2;
        }

        string manifestDir = Path.GetDirectoryName(manifestFullPath) ?? rootFullPath;
        var referencedKeys = CollectReferencedKeys(rootFullPath, scriptInputs);
        foreach (string key in CollectCodeReferencedKeys(rootFullPath, codeInputs))
        {
            referencedKeys.Add(key);
        }
        var issues = new List<string>();
        int errorCount = 0;
        int warningCount = 0;

        foreach (string language in manifest.Languages.DefaultIfEmpty(manifest.DefaultLanguage))
        {
            if (string.IsNullOrWhiteSpace(language)) continue;

            var availableKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string resource in manifest.Resources.DefaultIfEmpty("ui"))
            {
                string resourcePath = Path.Combine(manifestDir, $"{resource}.{language}.json");
                if (!File.Exists(resourcePath))
                {
                    errorCount++;
                    issues.Add($"error: missing resource: {language} {Path.GetRelativePath(rootFullPath, resourcePath)}");
                    continue;
                }

                Dictionary<string, string>? table;
                try
                {
                    table = JsonSerializer.Deserialize(
                        File.ReadAllText(resourcePath),
                        AriaCoreJsonContext.Default.DictionaryStringString);
                }
                catch (Exception ex)
                {
                    errorCount++;
                    issues.Add($"error: invalid resource: {language} {Path.GetRelativePath(rootFullPath, resourcePath)} ({ex.Message})");
                    continue;
                }

                if (table == null) continue;
                foreach (string key in table.Keys)
                {
                    availableKeys.Add(key);
                }
            }

            foreach (string key in referencedKeys.Order(StringComparer.OrdinalIgnoreCase))
            {
                if (!availableKeys.Contains(key))
                {
                    errorCount++;
                    issues.Add($"error: missing key: {language} {key}");
                }
            }

            foreach (string key in availableKeys.Order(StringComparer.OrdinalIgnoreCase))
            {
                if (!referencedKeys.Contains(key))
                {
                    warningCount++;
                    issues.Add($"warning: unused key: {language} {key}");
                }
            }

            ValidateScenarioFiles(
                rootFullPath,
                language,
                manifest,
                issues,
                ref errorCount);
        }

        foreach (string issue in issues)
        {
            Console.WriteLine(issue);
        }

        if (errorCount == 0)
        {
            string suffix = warningCount == 0 ? "" : $", {warningCount} warning(s)";
            Console.WriteLine($"aria-i18n-check passed: {referencedKeys.Count} referenced key(s){suffix}");
            return 0;
        }

        if (verbose)
        {
            Console.Error.WriteLine($"Referenced keys: {referencedKeys.Count}");
            Console.Error.WriteLine($"Issues: {issues.Count}");
        }

        Console.Error.WriteLine($"aria-i18n-check failed: {errorCount} error(s), {warningCount} warning(s)");
        return 1;
    }

    private static HashSet<string> CollectReferencedKeys(string rootFullPath, IEnumerable<string> scriptInputs)
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string input in scriptInputs)
        {
            string path = ResolvePath(rootFullPath, input);
            IEnumerable<string> files = Directory.Exists(path)
                ? Directory.EnumerateFiles(path, "*.aria", SearchOption.AllDirectories)
                : File.Exists(path)
                    ? new[] { path }
                    : Array.Empty<string>();

            foreach (string file in files)
            {
                foreach (string line in File.ReadLines(file))
                {
                    foreach (Match match in LocalizationKeyPattern.Matches(line))
                    {
                        keys.Add(match.Groups[1].Value);
                    }
                }
            }
        }

        return keys;
    }

    private static void ValidateScenarioFiles(
        string rootFullPath,
        string language,
        LocalizationManifest manifest,
        List<string> issues,
        ref int errorCount)
    {
        if (string.IsNullOrWhiteSpace(manifest.ScenarioRoot) || manifest.ScenarioFiles.Count == 0)
        {
            return;
        }

        string scenarioRoot = ResolvePath(rootFullPath, manifest.ScenarioRoot);
        foreach (string scenarioFile in manifest.ScenarioFiles)
        {
            if (string.IsNullOrWhiteSpace(scenarioFile)) continue;
            string localizedScenario = Path.GetFullPath(Path.Combine(scenarioRoot, language, scenarioFile));
            if (!File.Exists(localizedScenario))
            {
                errorCount++;
                issues.Add($"error: missing scenario file: {language} {Path.GetRelativePath(rootFullPath, localizedScenario)}");
                continue;
            }

            string scenarioDir = Path.GetDirectoryName(localizedScenario) ?? scenarioRoot;
            foreach (string line in File.ReadLines(localizedScenario))
            {
                Match match = IncludePattern.Match(line);
                if (!match.Success) continue;
                string includeTarget = Path.GetFullPath(Path.Combine(scenarioDir, match.Groups[1].Value));
                if (!File.Exists(includeTarget))
                {
                    errorCount++;
                    issues.Add($"error: missing scenario include: {language} {Path.GetRelativePath(rootFullPath, localizedScenario)} -> {match.Groups[1].Value}");
                }
            }
        }
    }

    private static HashSet<string> CollectCodeReferencedKeys(string rootFullPath, IEnumerable<string> codeInputs)
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string input in codeInputs)
        {
            string path = ResolvePath(rootFullPath, input);
            IEnumerable<string> files = Directory.Exists(path)
                ? Directory.EnumerateFiles(path, "*.cs", SearchOption.AllDirectories)
                : File.Exists(path)
                    ? new[] { path }
                    : Array.Empty<string>();

            foreach (string file in files)
            {
                foreach (string line in File.ReadLines(file))
                {
                    foreach (Match match in CodeLocalizationKeyPattern.Matches(line))
                    {
                        keys.Add(match.Groups[1].Value);
                    }
                    foreach (Match match in DottedStringLiteralPattern.Matches(line))
                    {
                        string key = match.Groups[1].Value;
                        if (ResourceKeyPrefixes.Any(prefix => key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
                        {
                            keys.Add(key);
                        }
                    }
                }
            }
        }

        return keys;
    }

    private static string ResolvePath(string rootFullPath, string path)
    {
        return Path.IsPathRooted(path)
            ? Path.GetFullPath(path)
            : Path.GetFullPath(Path.Combine(rootFullPath, path));
    }

    private static void PrintHelp()
    {
        Console.WriteLine("aria-i18n-check - localization key coverage checker");
        Console.WriteLine("Usage: aria-i18n-check [--root <dir>] [--manifest <path>] [--scripts <path>] [--code <path>]");
    }
}
