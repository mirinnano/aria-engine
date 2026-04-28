using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using AriaEngine.Packaging;

namespace AriaEngine.Tools;

public static class AriaPackCommand
{
    public static int Run(string[] args)
    {
        if (args.Length == 0)
        {
            PrintUsage();
            return 1;
        }

        string subcommand = args[0];
        bool verbose = false;

        for (int i = 1; i < args.Length; i++)
        {
            if (args[i].Equals("--verbose", StringComparison.OrdinalIgnoreCase))
            {
                verbose = true;
                break;
            }
        }

        try
        {
            return subcommand.ToLowerInvariant() switch
            {
                "build" => RunBuild(args, verbose),
                "diff" => RunDiff(args, verbose),
                "apply" => RunApply(args, verbose),
                _ => throw new InvalidOperationException($"Unknown subcommand: {subcommand}")
            };
        }
        catch (DirectoryNotFoundException ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            if (verbose) Console.Error.WriteLine(ex.StackTrace);
            return 2;
        }
        catch (FileNotFoundException ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            if (verbose) Console.Error.WriteLine(ex.StackTrace);
            return 2;
        }
        catch (InvalidOperationException ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            if (verbose) Console.Error.WriteLine(ex.StackTrace);
            return 3;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Unexpected error: {ex.Message}");
            if (verbose) Console.Error.WriteLine(ex.StackTrace);
            return 4;
        }
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  aria-pack build --input <dir> [--compiled <path>] --output <pak> [--key <secret>] [--verbose]");
        Console.WriteLine("  aria-pack diff --base <old.pak> --new <new.pak> --out <patch.patch> [--key <secret>] [--verbose]");
        Console.WriteLine("  aria-pack apply --base <old.pak> --patch <patch.patch> --out <updated.pak> [--key <secret>] [--verbose]");
    }

    private static int RunBuild(string[] args, bool verbose)
    {
        string inputDir = "assets";
        string? compiledPath = null;
        string outputPath = Path.Combine("build", "data.pak");
        string? keyMaterial = Environment.GetEnvironmentVariable("ARIA_PACK_KEY");

        for (int i = 1; i < args.Length; i++)
        {
            if (args[i].Equals("--verbose", StringComparison.OrdinalIgnoreCase)) continue;
            switch (args[i])
            {
                case "--input":
                    inputDir = args[++i];
                    break;
                case "--compiled":
                    compiledPath = args[++i];
                    break;
                case "--output":
                    outputPath = args[++i];
                    break;
                case "--key":
                    keyMaterial = args[++i];
                    break;
            }
        }

        string fullInput = Path.GetFullPath(inputDir);
        if (!Directory.Exists(fullInput))
            throw new DirectoryNotFoundException($"Input directory not found: {fullInput}. Please verify the path and try again.");

        if (!string.IsNullOrWhiteSpace(compiledPath) && !File.Exists(compiledPath))
            throw new FileNotFoundException($"Compiled script not found: {compiledPath}. Run aria-compile first.");

        var entries = new List<(string LogicalPath, string Type, byte[] Data)>();

        foreach (string file in Directory.GetFiles(fullInput, "*", SearchOption.AllDirectories))
        {
            string rel = Path.GetRelativePath(fullInput, file).Replace('\\', '/');
            string logical = $"{PakArchive.NormalizePath(inputDir)}/{rel}";
            byte[] data = File.ReadAllBytes(file);
            entries.Add((logical, GuessType(file), data));
            if (verbose) Console.WriteLine($"  + {logical} ({data.Length} bytes)");
        }

        if (!string.IsNullOrWhiteSpace(compiledPath))
        {
            byte[] compiled = File.ReadAllBytes(compiledPath);
            entries.Add(("scripts/scripts.ariac", "script", compiled));
            if (verbose) Console.WriteLine($"  + scripts/scripts.ariac ({compiled.Length} bytes)");
        }

        byte[]? key = string.IsNullOrWhiteSpace(keyMaterial) ? null : CryptoHelper.DeriveKey(keyMaterial);
        PakArchive.Write(outputPath, entries, key);
        Console.WriteLine($"Pak written: {outputPath}");
        Console.WriteLine($"Entries: {entries.Count}");
        return 0;
    }

    private static int RunDiff(string[] args, bool verbose)
    {
        string? basePath = null;
        string? newPath = null;
        string? outputPath = null;
        string? keyMaterial = Environment.GetEnvironmentVariable("ARIA_PACK_KEY");

        for (int i = 1; i < args.Length; i++)
        {
            if (args[i].Equals("--verbose", StringComparison.OrdinalIgnoreCase)) continue;
            switch (args[i])
            {
                case "--base":
                    basePath = args[++i];
                    break;
                case "--new":
                    newPath = args[++i];
                    break;
                case "--out":
                    outputPath = args[++i];
                    break;
                case "--key":
                    keyMaterial = args[++i];
                    break;
            }
        }

        if (string.IsNullOrWhiteSpace(basePath))
            throw new InvalidOperationException("Missing required argument: --base <old.pak>");
        if (string.IsNullOrWhiteSpace(newPath))
            throw new InvalidOperationException("Missing required argument: --new <new.pak>");
        if (string.IsNullOrWhiteSpace(outputPath))
            throw new InvalidOperationException("Missing required argument: --out <patch.patch>");

        if (!File.Exists(basePath))
            throw new FileNotFoundException($"Base pak not found: {basePath}");
        if (!File.Exists(newPath))
            throw new FileNotFoundException($"New pak not found: {newPath}");

        ValidatePakHeader(basePath);
        ValidatePakHeader(newPath);

        byte[]? key = string.IsNullOrWhiteSpace(keyMaterial) ? null : CryptoHelper.DeriveKey(keyMaterial);
        PakPatch.Create(basePath, newPath, outputPath, key);
        Console.WriteLine($"Patch written: {outputPath}");
        return 0;
    }

    private static int RunApply(string[] args, bool verbose)
    {
        string? basePath = null;
        string? patchPath = null;
        string? outputPath = null;
        string? keyMaterial = Environment.GetEnvironmentVariable("ARIA_PACK_KEY");

        for (int i = 1; i < args.Length; i++)
        {
            if (args[i].Equals("--verbose", StringComparison.OrdinalIgnoreCase)) continue;
            switch (args[i])
            {
                case "--base":
                    basePath = args[++i];
                    break;
                case "--patch":
                    patchPath = args[++i];
                    break;
                case "--out":
                    outputPath = args[++i];
                    break;
                case "--key":
                    keyMaterial = args[++i];
                    break;
            }
        }

        if (string.IsNullOrWhiteSpace(basePath))
            throw new InvalidOperationException("Missing required argument: --base <old.pak>");
        if (string.IsNullOrWhiteSpace(patchPath))
            throw new InvalidOperationException("Missing required argument: --patch <patch.patch>");
        if (string.IsNullOrWhiteSpace(outputPath))
            throw new InvalidOperationException("Missing required argument: --out <updated.pak>");

        if (!File.Exists(basePath))
            throw new FileNotFoundException($"Base pak not found: {basePath}");
        if (!File.Exists(patchPath))
            throw new FileNotFoundException($"Patch file not found: {patchPath}");

        ValidatePakHeader(basePath);
        ValidatePatchHeader(patchPath);

        byte[]? key = string.IsNullOrWhiteSpace(keyMaterial) ? null : CryptoHelper.DeriveKey(keyMaterial);
        PakPatch.Apply(basePath, patchPath, outputPath, key);
        Console.WriteLine($"Updated pak written: {outputPath}");
        return 0;
    }

    private static void ValidatePakHeader(string path)
    {
        using var fs = File.OpenRead(path);
        byte[] magic = new byte[5];
        int read = fs.Read(magic, 0, 5);
        if (read < 5 || !magic.AsSpan().SequenceEqual(Encoding.ASCII.GetBytes("ARPK1")))
            throw new InvalidOperationException($"Invalid pak file header: {path}. Expected a valid .pak file.");
    }

    private static void ValidatePatchHeader(string path)
    {
        using var fs = File.OpenRead(path);
        byte[] magic = new byte[5];
        int read = fs.Read(magic, 0, 5);
        if (read < 5 || !magic.AsSpan().SequenceEqual(Encoding.ASCII.GetBytes("ARDP1")))
            throw new InvalidOperationException($"Invalid patch file header: {path}. Expected a valid .patch file.");
    }

    private static string GuessType(string filePath)
    {
        string ext = Path.GetExtension(filePath).ToLowerInvariant();
        return ext switch
        {
            ".ariac" or ".aria" => "script",
            ".png" or ".jpg" or ".jpeg" or ".bmp" or ".webp" => "image",
            ".ogg" or ".wav" or ".mp3" => "audio",
            ".mp4" or ".webm" or ".avi" => "video",
            _ => "binary"
        };
    }
}
