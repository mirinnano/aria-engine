using System;
using System.Collections.Generic;
using System.IO;
using AriaEngine.Packaging;

namespace AriaEngine.Tools;

public static class AriaPackCommand
{
    public static int Run(string[] args)
    {
        if (args.Length == 0 || !args[0].Equals("build", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("Usage: aria-pack build --input assets --compiled build/scripts.ariac --output build/data.pak [--key secret]");
            return 1;
        }

        string inputDir = "assets";
        string? compiledPath = null;
        string outputPath = Path.Combine("build", "data.pak");
        string? keyMaterial = Environment.GetEnvironmentVariable("ARIA_PACK_KEY");

        for (int i = 1; i < args.Length; i++)
        {
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

        var entries = new List<(string LogicalPath, string Type, byte[] Data)>();
        string fullInput = Path.GetFullPath(inputDir);
        if (!Directory.Exists(fullInput))
            throw new DirectoryNotFoundException($"Input directory not found: {fullInput}");

        foreach (string file in Directory.GetFiles(fullInput, "*", SearchOption.AllDirectories))
        {
            string rel = Path.GetRelativePath(fullInput, file).Replace('\\', '/');
            string logical = $"{PakArchive.NormalizePath(inputDir)}/{rel}";
            byte[] data = File.ReadAllBytes(file);
            entries.Add((logical, GuessType(file), data));
        }

        if (!string.IsNullOrWhiteSpace(compiledPath))
        {
            byte[] compiled = File.ReadAllBytes(compiledPath);
            entries.Add(("scripts/scripts.ariac", "script", compiled));
        }

        byte[]? key = string.IsNullOrWhiteSpace(keyMaterial) ? null : CryptoHelper.DeriveKey(keyMaterial);
        PakArchive.Write(outputPath, entries, key);
        Console.WriteLine($"Pak written: {outputPath}");
        Console.WriteLine($"Entries: {entries.Count}");
        return 0;
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
