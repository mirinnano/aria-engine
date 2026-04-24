using System;
using System.Collections.Generic;
using System.IO;
using AriaEngine.Assets;
using AriaEngine.Core;
using AriaEngine.Scripting;

namespace AriaEngine.Tools;

public static class AriaCompileCommand
{
    public static int Run(string[] args)
    {
        string initPath = "init.aria";
        string mainPath = "assets/scripts/main.aria";
        string outputPath = Path.Combine("build", "scripts.ariac");
        string? key = Environment.GetEnvironmentVariable("ARIA_PACK_KEY");

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--init":
                    initPath = args[++i];
                    break;
                case "--main":
                    mainPath = args[++i];
                    break;
                case "--out":
                    outputPath = args[++i];
                    break;
                case "--key":
                    key = args[++i];
                    break;
            }
        }

        var reporter = new ErrorReporter();
        var parser = new Parser(reporter);
        var provider = new DiskAssetProvider(Directory.GetCurrentDirectory());
        var compiler = new ScriptCompiler(parser, reporter, provider);

        CompiledScriptBundle bundle = compiler.CompileBundle(initPath, mainPath);
        bool hasErrors = false;
        foreach (var err in reporter.Errors)
        {
            if (err.Level != AriaErrorLevel.Warning)
            {
                hasErrors = true;
                break;
            }
        }
        if (hasErrors)
        {
            reporter.WriteLogFile();
            Console.WriteLine("Compile failed due to script errors. See aria_error.log.");
            return 2;
        }

        CompiledBundleCodec.Save(outputPath, bundle, key);

        Console.WriteLine($"Compiled scripts: {bundle.Scripts.Count}");
        Console.WriteLine($"Output: {outputPath}");
        Console.WriteLine(string.IsNullOrWhiteSpace(key)
            ? "Warning: output is not encrypted (no --key provided)."
            : "Output encrypted.");
        return 0;
    }
}
