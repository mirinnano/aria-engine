using System;
using System.Collections.Generic;
using System.Linq;
using AriaEngine.Assets;
using AriaEngine.Core;

namespace AriaEngine.Scripting;

public enum RunMode
{
    Dev,
    Release
}

public sealed class ScriptLoader
{
    private readonly Parser _parser;
    private readonly IAssetProvider _provider;
    private readonly RunMode _mode;
    private readonly CompiledScriptBundle? _bundle;

    public ScriptLoader(Parser parser, IAssetProvider provider, RunMode mode, CompiledScriptBundle? bundle = null)
    {
        _parser = parser;
        _provider = provider;
        _mode = mode;
        _bundle = bundle;
    }

    public ParseResult LoadScript(string path)
    {
        string normalized = ScriptPreprocessor.NormalizePath(path);
        TraceLoad($"start {normalized} mode={_mode} bundle={_bundle is not null}");
        // Use compiled bundle if available (v2 single-pak or explicit compiled bundle)
        if (_mode == RunMode.Release && _bundle is not null)
        {
            TraceLoad($"compiled-lookup {normalized} scripts={_bundle.Scripts.Count}");
            if (!_bundle.Scripts.TryGetValue(normalized, out var compiled))
                throw new InvalidOperationException($"Compiled script not found in bundle: {normalized}");

            TraceLoad($"compiled-found {normalized} instructions={compiled.Instructions.Count}");
            var instructions = compiled.Instructions.Select(x =>
                new Instruction((OpCode)x.Op, x.Arguments, x.SourceLine, x.Condition)).ToList();

            TraceLoad($"compiled-materialized {normalized}");
            return new ParseResult
            {
                Instructions = instructions,
                Labels = new Dictionary<string, int>(compiled.Labels, StringComparer.OrdinalIgnoreCase),
                Functions = compiled.Functions,
                Structs = compiled.Structs,
                Enums = compiled.Enums,
                OwnedSprites = new HashSet<string>(compiled.OwnedSprites, StringComparer.OrdinalIgnoreCase),
                SourceLines = compiled.SourceLines
            };
        }

        // v3 split pak: .aria scripts are stored directly in scenario.aris; parse plain text
        TraceLoad($"expand-includes {normalized}");
        var expanded = ScriptPreprocessor.ExpandIncludes(normalized, _provider);
        TraceLoad($"parse {normalized} lines={expanded.Lines.Length}");
        return _parser.Parse(expanded.Lines, normalized);
    }

    private static void TraceLoad(string marker)
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("ARIA_STARTUP_TRACE"), "1", StringComparison.Ordinal))
        {
            return;
        }

        try
        {
            File.AppendAllText(
                Path.Combine(AppContext.BaseDirectory, "startup_trace.log"),
                $"{DateTime.UtcNow:O} script-loader {marker}{Environment.NewLine}");
        }
        catch
        {
            // Startup diagnostics must never affect script loading.
        }
    }
}
