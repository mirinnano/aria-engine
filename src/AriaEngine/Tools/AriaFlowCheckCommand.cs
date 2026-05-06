using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AriaEngine.Assets;
using AriaEngine.Core;
using AriaEngine.Rendering;
using AriaEngine.Scripting;

namespace AriaEngine.Tools;

/// <summary>
/// Static route checker for packaged visual novel scripts.
/// </summary>
public static class AriaFlowCheckCommand
{
    public static int Run(string[] args)
    {
        string root = ".";
        string main = "assets/scripts/main.aria";
        int chapterCount = 6;
        bool execute = false;
        int maxSteps = 20000;

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
                case "--main" when i + 1 < args.Length:
                    main = args[++i];
                    break;
                case "--chapters" when i + 1 < args.Length && int.TryParse(args[i + 1], out int parsed):
                    chapterCount = parsed;
                    i++;
                    break;
                case "--execute":
                    execute = true;
                    break;
                case "--max-steps" when i + 1 < args.Length && int.TryParse(args[i + 1], out int parsedMaxSteps):
                    maxSteps = parsedMaxSteps;
                    i++;
                    break;
                default:
                    Console.Error.WriteLine($"aria-flowcheck: unknown or incomplete argument '{args[i]}'");
                    PrintHelp();
                    return 2;
            }
        }

        var issues = new List<string>();
        try
        {
            string rootPath = Path.GetFullPath(root);
            var provider = new DiskAssetProvider(rootPath);
            var expanded = ScriptPreprocessor.ExpandIncludes(main, provider);
            var reporter = new ErrorReporter();
            var parseResult = new Parser(reporter).Parse(expanded.Lines, main);

            foreach (var error in reporter.Errors.Where(e => e.Level is AriaErrorLevel.Error or AriaErrorLevel.Fatal))
            {
                issues.Add($"parse:{error.LineNumber}: {error.Message}");
            }

            CheckFlow(expanded.Lines, parseResult, chapterCount, issues);
            if (execute && issues.Count == 0)
            {
                ExecuteFlow(parseResult, main, chapterCount, Math.Max(1, maxSteps), issues);
            }
        }
        catch (Exception ex)
        {
            issues.Add(ex.Message);
        }

        foreach (string issue in issues)
        {
            Console.Error.WriteLine($"aria-flowcheck: {issue}");
        }

        if (issues.Count > 0)
        {
            Console.Error.WriteLine($"aria-flowcheck failed: {issues.Count} issue(s)");
            return 2;
        }

        string suffix = execute ? ", runtime executed" : "";
        Console.WriteLine($"aria-flowcheck passed: {chapterCount} chapter route(s){suffix}");
        return 0;
    }

    private static void CheckFlow(string[] lines, ParseResult parseResult, int chapterCount, List<string> issues)
    {
        if (!parseResult.Labels.ContainsKey("chapter_select"))
        {
            issues.Add("missing label: *chapter_select");
        }

        for (int i = 1; i <= chapterCount; i++)
        {
            string scenario = $"scenario_{i:00}";
            string chapterFlag = $"chapter_{i:00}";
            int buttonId = 99 + i;

            if (!parseResult.Labels.ContainsKey(scenario))
            {
                issues.Add($"missing label: *{scenario}");
                continue;
            }

            if (!ContainsRoute(lines, buttonId, scenario))
            {
                issues.Add($"chapter_select does not route button {buttonId} to *{scenario}");
            }

            var section = GetLabelSection(lines, scenario);
            if (section.Count == 0)
            {
                issues.Add($"empty section: *{scenario}");
                continue;
            }

            RequireContains(section, $@"\bset_sflag\s+{Regex.Escape(scenario)}_started\b", $"{scenario} does not set start save flag", issues);
            RequireContains(section, $@"\bset_pflag\s+{Regex.Escape(chapterFlag)}\b", $"{scenario} does not unlock {chapterFlag}", issues);
            RequireContains(section, @"^\s*nvl\s*(?:\(\))?\s*$", $"{scenario} never enters NVL mode", issues);
            RequireContains(section, @"^\s*adv\s*(?:\(\))?\s*$", $"{scenario} never enters ADV mode", issues);
            RequireContains(section, @"\bgoto\s+\*chapter_select\b", $"{scenario} does not return to chapter select", issues);

            if (i < chapterCount)
            {
                string nextFlag = $"chapter_{i + 1:00}";
                RequireContains(section, $@"\bset_pflag\s+{Regex.Escape(nextFlag)}\b", $"{scenario} does not unlock {nextFlag}", issues);
            }
        }
    }

    private static bool ContainsRoute(string[] lines, int buttonId, string scenario)
    {
        string pattern = $@"\bif\s+%0\s*==\s*{buttonId}\b.*\bgoto\s+\*{Regex.Escape(scenario)}\b";
        return lines.Any(line => Regex.IsMatch(StripComment(line), pattern, RegexOptions.IgnoreCase));
    }

    private static List<string> GetLabelSection(string[] lines, string label)
    {
        var section = new List<string>();
        bool inSection = false;
        foreach (string line in lines)
        {
            string stripped = StripComment(line).Trim();
            var labelMatch = Regex.Match(stripped, @"^\*([A-Za-z_][A-Za-z0-9_]*)\b");
            if (labelMatch.Success)
            {
                if (inSection && IsChapterScenarioLabel(labelMatch.Groups[1].Value))
                    break;
                if (!inSection)
                {
                    inSection = labelMatch.Groups[1].Value.Equals(label, StringComparison.OrdinalIgnoreCase);
                }
            }

            if (inSection) section.Add(line);
        }
        return section;
    }

    private static void RequireContains(List<string> lines, string pattern, string message, List<string> issues)
    {
        if (!lines.Any(line => Regex.IsMatch(StripComment(line), pattern, RegexOptions.IgnoreCase)))
        {
            issues.Add(message);
        }
    }

    private static bool IsChapterScenarioLabel(string label)
    {
        return Regex.IsMatch(label, @"^scenario_\d{2}$", RegexOptions.IgnoreCase);
    }

    private static void ExecuteFlow(ParseResult parseResult, string main, int chapterCount, int maxSteps, List<string> issues)
    {
        for (int i = 1; i <= chapterCount; i++)
        {
            string scenario = $"scenario_{i:00}";
            if (!parseResult.Labels.TryGetValue(scenario, out int pc))
            {
                issues.Add($"runtime missing label: *{scenario}");
                continue;
            }

            ExecuteScenario(parseResult, main, scenario, pc, maxSteps, issues);
        }
    }

    private static void ExecuteScenario(ParseResult parseResult, string main, string scenario, int pc, int maxSteps, List<string> issues)
    {
        string runtimeRoot = Path.Combine(Path.GetTempPath(), "aria-flowcheck-runtime-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(runtimeRoot);

        try
        {
            var reporter = new ErrorReporter();
            var config = new ConfigManager(
                reporter,
                Path.Combine(runtimeRoot, "config.json"),
                Path.Combine(runtimeRoot, "saves", "persistent.ariasav"));
            var saves = new SaveManager(reporter, Path.Combine(runtimeRoot, "saves"));
            var vm = new VirtualMachine(reporter, new TweenManager(), saves, config, runtimeRoot);
            vm.LoadScript(parseResult, main);
            vm.State.Execution.ProgramCounter = pc;
            vm.State.Execution.State = VmState.Running;
            vm.State.Execution.CallStack.Clear();

            for (int step = 0; step < maxSteps; step++)
            {
                if (HasRuntimeError(reporter, out string runtimeError))
                {
                    issues.Add($"{scenario} runtime error: {runtimeError}");
                    return;
                }

                if (HasReturnedToChapterHub(vm))
                {
                    return;
                }

                switch (vm.State.Execution.State)
                {
                    case VmState.Running:
                        vm.Step();
                        break;
                    case VmState.WaitingForClick:
                        ResumeHeadlessClick(vm);
                        break;
                    case VmState.WaitingForDelay:
                        vm.Update(Math.Max(1f, vm.State.Execution.DelayTimerMs + 1f));
                        break;
                    case VmState.WaitingForAnimation:
                        ResumeHeadlessAnimation(vm);
                        break;
                    case VmState.WaitingForButton:
                        if (!ResumeHeadlessButton(vm))
                        {
                            issues.Add($"{scenario} waited for a button with no registered button");
                            return;
                        }
                        break;
                    case VmState.FadingIn:
                    case VmState.FadingOut:
                        vm.FinishFade();
                        break;
                    case VmState.Ended:
                        issues.Add($"{scenario} ended before returning to *chapter_select");
                        return;
                    default:
                        issues.Add($"{scenario} reached unsupported runtime state: {vm.State.Execution.State}");
                        return;
                }
            }

            issues.Add($"{scenario} exceeded runtime step limit ({maxSteps}) before returning to *chapter_select");
        }
        finally
        {
            try
            {
                Directory.Delete(runtimeRoot, recursive: true);
            }
            catch
            {
                // Best-effort cleanup only. The runtime gate must report script issues, not temp cleanup noise.
            }
        }
    }

    private static bool HasRuntimeError(ErrorReporter reporter, out string message)
    {
        var error = reporter.Errors.FirstOrDefault(e => e.Level is AriaErrorLevel.Error or AriaErrorLevel.Fatal);
        if (error == null)
        {
            message = "";
            return false;
        }

        message = string.IsNullOrWhiteSpace(error.Code)
            ? error.Message
            : $"{error.Code}: {error.Message}";
        return true;
    }

    private static bool HasReturnedToChapterHub(VirtualMachine vm)
    {
        return vm.State.Execution.State == VmState.WaitingForButton &&
               vm.TryGetCurrentLabelAndOffset(out string label, out _) &&
               (label.Equals("chapter_select", StringComparison.OrdinalIgnoreCase) ||
                label.Equals("chapter_loop", StringComparison.OrdinalIgnoreCase));
    }

    private static void ResumeHeadlessClick(VirtualMachine vm)
    {
        vm.State.TextRuntime.DisplayedTextLength = vm.State.TextRuntime.CurrentTextBuffer.Length;
        if (vm.State.TextRuntime.IsWaitingPageClear)
        {
            vm.State.TextRuntime.CurrentTextBuffer = "";
            vm.State.TextRuntime.DisplayedTextLength = 0;
            vm.State.TextRuntime.IsWaitingPageClear = false;
        }

        vm.ResumeFromClick();
    }

    private static void ResumeHeadlessAnimation(VirtualMachine vm)
    {
        vm.State.TextRuntime.DisplayedTextLength = vm.State.TextRuntime.CurrentTextBuffer.Length;
        vm.Tweens.FinishAll(vm.State);
        vm.Update(60000f);
    }

    private static bool ResumeHeadlessButton(VirtualMachine vm)
    {
        var buttonId = vm.State.Interaction.SpriteButtonMap
            .Where(pair => vm.State.Render.Sprites.TryGetValue(pair.Key, out var sprite) && sprite.Visible && sprite.IsButton)
            .OrderBy(pair => pair.Value)
            .ThenBy(pair => pair.Key)
            .Select(pair => (int?)pair.Key)
            .FirstOrDefault();

        if (buttonId == null) return false;
        vm.ResumeFromButton(buttonId.Value);
        return true;
    }

    private static string StripComment(string line)
    {
        bool inQuotes = false;
        for (int i = 0; i < line.Length; i++)
        {
            if (line[i] == '"') inQuotes = !inQuotes;
            if (!inQuotes)
            {
                if (line[i] == ';') return line[..i];
                if (i + 1 < line.Length && line[i] == '/' && line[i + 1] == '/') return line[..i];
            }
        }
        return line;
    }

    private static void PrintHelp()
    {
        Console.WriteLine("aria-flowcheck - static scenario route checker");
        Console.WriteLine("Usage: aria-flowcheck [--root <dir>] [--main <path>] [--chapters <count>] [--execute] [--max-steps <count>]");
    }
}
