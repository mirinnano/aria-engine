#if ARIA_BYTECODE
using System;
using System.IO;
using AriaEngine.Assets;
using AriaEngine.Compiler;
using AriaEngine.Core;

namespace AriaEngine.Tools;

/// <summary>
/// .ariaファイルを.arib（バイトコード）ファイルにコンパイルするコマンド
/// </summary>
public static class AriaBytecodeCompileCommand
{
    public static int Run(string[] args)
    {
        string inputPath = "assets/scripts/main.aria";
        string outputPath = "build/scripts.arib";

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--input":
                case "-i":
                    inputPath = args[++i];
                    break;
                case "--output":
                case "-o":
                    outputPath = args[++i];
                    break;
                case "--help":
                case "-h":
                    PrintHelp();
                    return 0;
            }
        }

        if (!File.Exists(inputPath))
        {
            Console.Error.WriteLine($"Error: Input file not found: {inputPath}");
            return 1;
        }

        var reporter = new ErrorReporter();
        var parser = new Parser(reporter);
        var provider = new DiskAssetProvider(Directory.GetCurrentDirectory());

        Console.Error.WriteLine($"Compiling: {inputPath}");

        try
        {
            // スクリプトを読み込み
            string[] lines = provider.ReadAllLines(inputPath);
            var parseResult = parser.Parse(lines, inputPath);

            // バイトコードにコンパイル
            var bytecodeCompiler = new BytecodeCompiler(reporter);
            var bytecodeFile = bytecodeCompiler.Compile(parseResult.Instructions, Path.GetFileNameWithoutExtension(inputPath));

            // 出力ディレクトリを作成
            string outputDir = Path.GetDirectoryName(outputPath) ?? ".";
            if (!Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            // バイトコードファイルを保存
            byte[] bytecodeBytes = bytecodeFile.ToBytes();
            File.WriteAllBytes(outputPath, bytecodeBytes);

            // 統計情報を表示
            Console.Error.WriteLine($"Output: {outputPath}");
            Console.Error.WriteLine($"Size: {bytecodeBytes.Length} bytes");
            Console.Error.WriteLine($"Functions: {bytecodeFile.Functions.Count}");
            Console.Error.WriteLine($"Strings: {bytecodeFile.Strings.Count}");
            Console.Error.WriteLine($"Constants: {bytecodeFile.Constants.Count}");
            Console.Error.WriteLine($"Instructions: {parseResult.Instructions.Count}");

            // エラーがあればログを保存
            if (reporter.Errors.Count > 0)
            {
                reporter.WriteLogFile();
                Console.Error.WriteLine("Warning: Compilation completed with errors. See aria_error.log.");
                return 2;
            }

            return 0;
        }
        catch (Exception ex)
        {
            reporter.ReportException("BYTECODE_COMPILE", ex, "Bytecode compilation failed.", AriaErrorLevel.Fatal);
            reporter.WriteLogFile();
            Console.Error.WriteLine($"Error: {ex.Message}");
            Console.Error.WriteLine("See aria_error.log for details.");
            return 1;
        }
    }

    private static void PrintHelp()
    {
        Console.WriteLine("aria-bytecode-compile: Compile .aria scripts to .arib bytecode");
        Console.WriteLine();
        Console.WriteLine("Usage: dotnet AriaEngine.dll aria-bytecode-compile [options]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  -i, --input <path>    Input .aria file (default: assets/scripts/main.aria)");
        Console.WriteLine("  -o, --output <path>   Output .arib file (default: build/scripts.arib)");
        Console.WriteLine("  -h, --help            Show this help message");
        Console.WriteLine();
        Console.WriteLine("Example:");
        Console.WriteLine("  dotnet AriaEngine.dll aria-bytecode-compile -i main.aria -o output.arib");
    }
}
#endif
