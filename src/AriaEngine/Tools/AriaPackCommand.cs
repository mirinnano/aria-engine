using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;
using AriaEngine.Packaging;
using AriaEngine.Packaging.Compression;

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
        string? initPath = null;
        string? compiledPath = null;
        string outputPath = Path.Combine("build", "data.pak");
        string? keyMaterial = Environment.GetEnvironmentVariable("ARIA_PACK_KEY");
        string format = "v2";
        bool split = false;

        for (int i = 1; i < args.Length; i++)
        {
            if (args[i].Equals("--verbose", StringComparison.OrdinalIgnoreCase)) continue;
            switch (args[i])
            {
                case "--input":
                    if (i + 1 >= args.Length) throw new InvalidOperationException($"Missing value for argument {args[i]}");
                    inputDir = args[++i];
                    break;
                case "--init":
                    if (i + 1 >= args.Length) throw new InvalidOperationException($"Missing value for argument {args[i]}");
                    initPath = args[++i];
                    break;
                case "--compiled":
                    if (i + 1 >= args.Length) throw new InvalidOperationException($"Missing value for argument {args[i]}");
                    compiledPath = args[++i];
                    break;
                case "--output":
                    if (i + 1 >= args.Length) throw new InvalidOperationException($"Missing value for argument {args[i]}");
                    outputPath = args[++i];
                    break;
                case "--key":
                    if (i + 1 >= args.Length) throw new InvalidOperationException($"Missing value for argument {args[i]}");
                    keyMaterial = args[++i];
                    break;
                case "--format":
                    if (i + 1 >= args.Length) throw new InvalidOperationException($"Missing value for argument {args[i]}");
                    format = args[++i];
                    break;
                case "--split":
                    split = true;
                    break;
            }
        }

        string fullInput = Path.GetFullPath(inputDir);
        if (!Directory.Exists(fullInput))
            throw new DirectoryNotFoundException($"Input directory not found: {fullInput}. Please verify the path and try again.");

        if (!string.IsNullOrWhiteSpace(compiledPath) && !File.Exists(compiledPath))
            throw new FileNotFoundException($"Compiled script not found: {compiledPath}. Run aria-compile first.");

        var entries = new List<(string LogicalPath, string Type, byte[] Data)>();

        // v3 packaging with category split support (experimental)
        if (string.Equals(format, "v3", StringComparison.OrdinalIgnoreCase) && split)
        {
            // Build per-category groups and write separate v3 paks.
            // We'll collect files per category according to the specification, compress per-category payloads, and write separate paks.

            // Prepare root input and 5MB threshold for streams/voices
            var bootLogical = "init.aria"; // boot file name if exists
            string bootPath = Path.Combine(fullInput, bootLogical);

            // Helper: 5MB threshold
            const long StreamVoiceSizeThreshold = 5 * 1024 * 1024; // 5MB

            // Data holders per category
            var bootEntries = new List<(string LogicalPath, byte[] Data)>();
            var scenarioEntries = new List<(string LogicalPath, byte[] Data)>();
            var dataEntries = new List<(string LogicalPath, byte[] Data)>();
            var streamEntries = new List<(string LogicalPath, byte[] Data)>();
            var voiceEntries = new List<(string LogicalPath, byte[] Data)>();

            // Boot: init.aria from --init arg first, then fallback to input dir
            if (!string.IsNullOrWhiteSpace(initPath) && File.Exists(initPath))
            {
                string initLogical = Path.GetFileName(initPath).Replace('\\', '/');
                bootEntries.Add((initLogical, File.ReadAllBytes(initPath)));
            }
            else if (File.Exists(bootPath))
            {
                bootEntries.Add((bootLogical, File.ReadAllBytes(bootPath)));
            }

            // Scenario: compiled script from --compiled arg
            if (!string.IsNullOrWhiteSpace(compiledPath) && File.Exists(compiledPath))
            {
                string compiledLogical = compiledPath.Replace('\\', '/');
                scenarioEntries.Add((compiledLogical, File.ReadAllBytes(compiledPath)));
            }

            // Scan all files under input dir (recursively) and categorize
            foreach (string file in Directory.GetFiles(fullInput, "*", SearchOption.AllDirectories))
            {
                // Skip the boot file if already added
                string rel = Path.GetRelativePath(fullInput, file).Replace('\\', '/');
                string ext = Path.GetExtension(file).ToLowerInvariant();
                long size = new FileInfo(file).Length;

                // Scenario: .aria or .ariac
                // Prefix with "assets/" to match engine request paths (e.g. assets/scripts/main.aria)
                if (ext == ".aria" || ext == ".ariac")
                {
                    // Skip init.aria - it belongs exclusively to boot category
                    if (rel == bootLogical) continue;
                    scenarioEntries.Add(("assets/" + rel, File.ReadAllBytes(file)));
                    continue;
                }

                // Data: image types + fonts
                // Prefix with "assets/" to match engine request paths (e.g. assets/fonts/NotoSansJP-Regular.ttf)
                if (rel.StartsWith("i18n/", StringComparison.OrdinalIgnoreCase) && ext == ".json")
                {
                    dataEntries.Add(("assets/" + rel, File.ReadAllBytes(file)));
                    continue;
                }

                if (ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".bmp" || ext == ".webp" || ext == ".ttf" || ext == ".otf")
                {
                    dataEntries.Add(("assets/" + rel, File.ReadAllBytes(file)));
                    continue;
                }

                // Stream: large media (.ogg, .wav, .mp3, .mp4, .webm, .avi) with size > 5MB
                // Prefix with "assets/" to match engine request paths
                if (ext == ".ogg" || ext == ".wav" || ext == ".mp3" || ext == ".mp4" || ext == ".webm" || ext == ".avi")
                {
                    if (size > StreamVoiceSizeThreshold)
                    {
                        streamEntries.Add(("assets/" + rel, File.ReadAllBytes(file)));
                        continue;
                    }
                    // smaller media could be voice; fall through to voice check
                }

                // Voice: audio formats with size <= 5MB
                // Prefix with "assets/" to match engine request paths
                if ((ext == ".ogg" || ext == ".wav" || ext == ".mp3") && size <= StreamVoiceSizeThreshold)
                {
                    voiceEntries.Add(("assets/" + rel, File.ReadAllBytes(file)));
                    continue;
                }
            }

            // Derive output directory from --output path: use parent if file, otherwise the path itself
            string outputDir = Path.GetDirectoryName(outputPath) ?? ".";
            if (string.IsNullOrWhiteSpace(outputDir)) outputDir = ".";

            // Write per-category v3 pak if non-empty
            void WriteCategoryPak(string categoryName, PakArchiveV3.Category category, List<(string LogicalPath, byte[] Data)> items, string extension, string displayLabel)
            {
                if (items == null || items.Count == 0) return;
                // Sort entries by PathHash64(LogicalPath) to satisfy requirement
                var sorted = items.Select(it => new {
                    LogicalPath = it.LogicalPath,
                    Data = it.Data,
                    PathHash = PakArchiveV3Reader.PathHash64(it.LogicalPath)
                }).OrderBy(x => x.PathHash).ToList();

                // Prepare manifest and payloads in sorted order
                var manifest = new PakManifestV3
                {
                    Entries = new List<PakManifestEntryV3>(sorted.Count),
                    PathStrings = new List<string>(sorted.Count)
                };
                var payloads = new List<byte[]>();
                long cumulativeOffset = 0;

                foreach (var s in sorted)
                {
                    string logicalPath = s.LogicalPath;
                    byte[] data = s.Data;
                    byte[] payload;
                    byte flags = 0x00;
                    uint originalSize = (uint)data.Length;

                    // Compression per category
                    bool compressed = false;
                    switch (category)
                    {
                        case PakArchiveV3.Category.Boot:
                        case PakArchiveV3.Category.Scenario:
                        {
                            var comp = CompressionHelper.Create(CompressionAlgorithm.Zstd).Compress(data, category == PakArchiveV3.Category.Boot ? 3 : 5);
                            if (comp.Length < data.Length)
                            {
                                payload = comp;
                                compressed = true;
                                flags = 0x02;
                            }
                            else
                            {
                                payload = data;
                            }
                            break;
                        }
                        case PakArchiveV3.Category.Data:
                        {
                            var comp = CompressionHelper.Create(CompressionAlgorithm.Lz4).Compress(data, 3);
                            payload = comp;
                            // Determine if compressed by inspecting first int for negative size (LZ4 wrapper)
                            bool isUncompressed = false;
                            try
                            {
                                int first = BitConverter.ToInt32(payload, 0);
                                if (first < 0) isUncompressed = true;
                            }
                            catch
                            {
                                // If we can't read header, assume compressed
                            }
                            if (!isUncompressed && payload.Length > data.Length) isUncompressed = false; // defensive
                            compressed = !isUncompressed;
                            flags = compressed ? (byte)0x02 : (byte)0x00;
                            break;
                        }
                        case PakArchiveV3.Category.Stream:
                        {
                            // No compression for streams
                            payload = data;
                            compressed = false;
                            // Flags remain 0
                            break;
                        }
                        case PakArchiveV3.Category.Voice:
                        {
                            var comp = CompressionHelper.Create(CompressionAlgorithm.Lz4).Compress(data, 3);
                            payload = comp;
                            bool isUncompressed = false;
                            try
                            {
                                int first = BitConverter.ToInt32(payload, 0);
                                if (first < 0) isUncompressed = true;
                            }
                            catch { }
                            compressed = !isUncompressed;
                            flags = compressed ? (byte)0x02 : (byte)0x00;
                            break;
                        }
                        default:
                            payload = data;
                            break;
                    }

                    if (manifest.Entries == null) manifest.Entries = new List<PakManifestEntryV3>();
                    if (manifest.PathStrings == null) manifest.PathStrings = new List<string>();

                    manifest.Entries.Add(new PakManifestEntryV3
                    {
                        PathHash = PakArchiveV3Reader.PathHash64(logicalPath),
                        Offset = (ulong) cumulativeOffset,
                        Size = (uint) payload.Length,
                        OriginalSize = (uint) data.Length,
                        Flags = flags
                    });
                    manifest.PathStrings.Add(logicalPath);
                    payloads.Add(payload);
                    cumulativeOffset += payload.Length;
                }

                // Write to disk
                Directory.CreateDirectory(outputDir);
                string outPath = Path.Combine(outputDir, $"{categoryName.ToLowerInvariant()}.{extension}");
                using var outStream = File.Create(outPath);
                PakArchiveV3.Write(outStream, manifest, payloads.ToArray(), category, pakVersion: 1, flags: 0x00);
                Console.WriteLine($"Wrote {outPath} with {manifest.Entries.Count} entries");
            }

            // Execute per-category writes
            WriteCategoryPak("boot", PakArchiveV3.Category.Boot, bootEntries, "arib", "Boot");
            WriteCategoryPak("scenario", PakArchiveV3.Category.Scenario, scenarioEntries, "aris", "Scenario");
            WriteCategoryPak("data", PakArchiveV3.Category.Data, dataEntries, "arid", "Data");
            WriteCategoryPak("stream", PakArchiveV3.Category.Stream, streamEntries, "arim", "Stream");
            WriteCategoryPak("voice", PakArchiveV3.Category.Voice, voiceEntries, "ariv", "Voice");

            // No explicit Update category handling per requirements (not used in build)

            // Finished v3 split packaging
            return 0;
        }
        else if (string.Equals(format, "v3", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("v3 non-split mode is not yet supported. Use --split with --format v3.");
        }

        foreach (string file in Directory.GetFiles(fullInput, "*", SearchOption.AllDirectories))
        {
            string rel = Path.GetRelativePath(fullInput, file).Replace('\\', '/');
            string logical = $"{PakArchive.NormalizePath(inputDir)}/{rel}";
            byte[] data = File.ReadAllBytes(file);
            entries.Add((logical, GuessType(file), data));
            if (verbose) Console.Error.WriteLine($"  + {logical} ({data.Length} bytes)");
        }

        if (!string.IsNullOrWhiteSpace(compiledPath))
        {
            byte[] compiled = File.ReadAllBytes(compiledPath);
            entries.Add(("scripts/scripts.ariac", "script", compiled));
            if (verbose) Console.Error.WriteLine($"  + scripts/scripts.ariac ({compiled.Length} bytes)");
        }

        byte[]? key = string.IsNullOrWhiteSpace(keyMaterial) ? null : CryptoHelper.DeriveKey(keyMaterial);
        PakArchive.Write(outputPath, entries, key);
        Console.Error.WriteLine($"Pak written: {outputPath}");
        Console.Error.WriteLine($"Entries: {entries.Count}");
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
                    if (i + 1 >= args.Length) throw new InvalidOperationException($"Missing value for argument {args[i]}");
                    basePath = args[++i];
                    break;
                case "--new":
                    if (i + 1 >= args.Length) throw new InvalidOperationException($"Missing value for argument {args[i]}");
                    newPath = args[++i];
                    break;
                case "--out":
                    if (i + 1 >= args.Length) throw new InvalidOperationException($"Missing value for argument {args[i]}");
                    outputPath = args[++i];
                    break;
                case "--key":
                    if (i + 1 >= args.Length) throw new InvalidOperationException($"Missing value for argument {args[i]}");
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
        Console.Error.WriteLine($"Patch written: {outputPath}");
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
                    if (i + 1 >= args.Length) throw new InvalidOperationException($"Missing value for argument {args[i]}");
                    basePath = args[++i];
                    break;
                case "--patch":
                    if (i + 1 >= args.Length) throw new InvalidOperationException($"Missing value for argument {args[i]}");
                    patchPath = args[++i];
                    break;
                case "--out":
                    if (i + 1 >= args.Length) throw new InvalidOperationException($"Missing value for argument {args[i]}");
                    outputPath = args[++i];
                    break;
                case "--key":
                    if (i + 1 >= args.Length) throw new InvalidOperationException($"Missing value for argument {args[i]}");
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
        Console.Error.WriteLine($"Updated pak written: {outputPath}");
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
