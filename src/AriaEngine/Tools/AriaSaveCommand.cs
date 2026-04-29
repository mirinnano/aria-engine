using System;
using System.IO;
using System.Linq;
using AriaEngine.Core;

namespace AriaEngine.Tools;

/// <summary>
/// aria-save CLI tool for inspecting and validating save files
/// </summary>
public static class AriaSaveCommand
{
    private const int MaxSlots = 10;

    public static int Run(string[] args)
    {
        if (args.Length == 0 || args[0].Equals("--help", StringComparison.OrdinalIgnoreCase) || args[0].Equals("-h", StringComparison.OrdinalIgnoreCase))
        {
            PrintHelp();
            return 0;
        }

        var subcommand = args[0].ToLowerInvariant();

        switch (subcommand)
        {
            case "list":
                return RunList();

            case "help":
            case "--help":
            case "-h":
                PrintHelp();
                return 0;

            case "info":
                if (args.Length < 2)
                {
                    Console.Error.WriteLine("aria-save: missing slot argument");
                    Console.Error.WriteLine("Usage: aria-save info <slot>");
                    return 2;
                }
                if (!int.TryParse(args[1], out int slot) || slot < 0 || slot >= MaxSlots)
                {
                    Console.Error.WriteLine($"aria-save: invalid slot '{args[1]}'. Must be 0-{MaxSlots - 1}");
                    return 2;
                }
                return RunInfo(slot);

            case "validate":
                return RunValidate();

            default:
                Console.Error.WriteLine($"aria-save: unknown command '{subcommand}'");
                Console.Error.WriteLine("Run 'aria-save help' for usage.");
                return 2;
        }
    }

    private static int RunList()
    {
        var reporter = new ErrorReporter();
        var saveManager = new SaveManager(reporter);
        var saves = saveManager.GetAllSaveSlots();

        Console.WriteLine($"Save Directory: {GetSaveDirectory()}");
        Console.WriteLine();
        Console.WriteLine($"{"Slot",-6} {"Chapter",-20} {"Saved At",-20} {"Play Time",-10} {"Preview"}");
        Console.WriteLine(new string('-', 80));

        for (int i = 0; i < saves.Count; i++)
        {
            var save = saves[i];
            if (save.SaveTime == DateTime.MinValue)
            {
                Console.WriteLine($"{i,-6} [empty]");
            }
            else
            {
                string chapter = Truncate(save.ChapterTitle, 18);
                string time = save.SaveTime.ToString("yyyy-MM-dd HH:mm");
                string playTime = FormatPlayTime(save.PlayTime);
                string preview = Truncate(save.PreviewText.Replace("\n", " ").Replace("\r", ""), 40);
                Console.WriteLine($"{i,-6} {chapter,-20} {time,-20} {playTime,-10} {preview}");
            }
        }

        return 0;
    }

    private static int RunInfo(int slot)
    {
        var reporter = new ErrorReporter();
        var saveManager = new SaveManager(reporter);
        var save = saveManager.GetSaveData(slot);

        if (save == null)
        {
            Console.WriteLine($"Slot {slot}: [empty]");
            return 0;
        }

        Console.WriteLine($"Slot {slot}:");
        Console.WriteLine($"  Chapter:     {save.ChapterTitle}");
        Console.WriteLine($"  Script:     {save.ScriptFile}");
        Console.WriteLine($"  Saved:      {save.SaveTime:yyyy-MM-dd HH:mm:ss}");
        Console.WriteLine($"  Play Time:  {FormatPlayTime(save.PlayTime)}");
        Console.WriteLine($"  Preview:    {save.PreviewText.Replace("\n", " ").Replace("\r", "")}");
        Console.WriteLine($"  Screenshot: {save.ScreenshotPath}");

        return 0;
    }

    private static int RunValidate()
    {
        var reporter = new ErrorReporter();
        var saveManager = new SaveManager(reporter);
        string saveDir = GetSaveDirectory();

        if (!Directory.Exists(saveDir))
        {
            Console.WriteLine($"Save directory does not exist: {saveDir}");
            return 0;
        }

        int errorCount = 0;

        for (int i = 0; i < MaxSlots; i++)
        {
            string path = Path.Combine(saveDir, $"slot_{i:00}.ariasav");
            string jsonPath = Path.Combine(saveDir, $"slot_{i:00}.json");

            if (File.Exists(path))
            {
                if (!ValidateSaveFile(path, "AriaSave v3"))
                {
                    errorCount++;
                }
            }
            else if (File.Exists(jsonPath))
            {
                if (!ValidateSaveFile(jsonPath, "AriaSave JSON"))
                {
                    errorCount++;
                }
            }
            else
            {
                Console.WriteLine($"Slot {i}: [empty]");
            }
        }

        if (errorCount > 0)
        {
            Console.Error.WriteLine();
            Console.Error.WriteLine($"Validation failed: {errorCount} corrupt file(s)");
            return 1;
        }

        Console.WriteLine($"All save files validated successfully.");
        return 0;
    }

    private static bool ValidateSaveFile(string path, string formatName)
    {
        try
        {
            // Try to load via SaveManager
            var reporter = new ErrorReporter();
            var saveManager = new SaveManager(reporter);

            int slot = ExtractSlotFromPath(path);
            var (data, success) = saveManager.Load(slot);

            if (success && data != null)
            {
                Console.WriteLine($"Slot {slot}: OK ({formatName})");
                return true;
            }
            else
            {
                Console.WriteLine($"Slot {slot}: FAILED ({formatName}) - load failed");
                return false;
            }
        }
        catch (Exception ex)
        {
            int slot = ExtractSlotFromPath(path);
            Console.WriteLine($"Slot {slot}: FAILED ({formatName}) - {ex.Message}");
            return false;
        }
    }

    private static int ExtractSlotFromPath(string path)
    {
        string filename = Path.GetFileName(path);
        // slot_00.ariasav -> extract "00" -> int 0
        if (filename.StartsWith("slot_", StringComparison.OrdinalIgnoreCase))
        {
            string numStr = filename.Substring(5, 2);
            if (int.TryParse(numStr, out int slot))
                return slot;
        }
        return -1;
    }

    private static string GetSaveDirectory()
    {
        return "saves";
    }

    private static string FormatPlayTime(TimeSpan playTime)
    {
        if (playTime == TimeSpan.Zero)
            return "-";

        if (playTime.TotalHours >= 1)
            return $"{(int)playTime.TotalHours}h {playTime.Minutes}m";
        if (playTime.TotalMinutes >= 1)
            return $"{playTime.Minutes}m {playTime.Seconds}s";
        return $"{playTime.Seconds}s";
    }

    private static string Truncate(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text))
            return "";
        if (text.Length <= maxLength)
            return text;
        return text.Substring(0, maxLength - 1) + "…";
    }

    private static void PrintHelp()
    {
        Console.WriteLine("aria-save - Save file inspection and validation tool");
        Console.WriteLine();
        Console.WriteLine("Usage: aria-save <command> [arguments]");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  list              List all save slots with metadata");
        Console.WriteLine("  info <slot>       Show detailed info for a specific slot (0-9)");
        Console.WriteLine("  validate          Validate all save files for corruption");
        Console.WriteLine("  help              Show this help message");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  aria-save list");
        Console.WriteLine("  aria-save info 3");
        Console.WriteLine("  aria-save validate");
        Console.WriteLine();
        Console.WriteLine("Exit codes:");
        Console.WriteLine("  0 - Success (or all saves valid for validate)");
        Console.WriteLine("  1 - Validation errors found");
        Console.WriteLine("  2 - Invalid arguments or command");
    }
}