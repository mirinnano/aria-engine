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

    public (List<Instruction> Instructions, Dictionary<string, int> Labels, string[] SourceLines) LoadScript(string path)
    {
        string normalized = ScriptPreprocessor.NormalizePath(path);
        if (_mode == RunMode.Release)
        {
            if (_bundle is null) throw new InvalidOperationException("Release mode requires compiled script bundle.");
            if (!_bundle.Scripts.TryGetValue(normalized, out var compiled))
                throw new InvalidOperationException($"Compiled script not found in bundle: {normalized}");

            var instructions = compiled.Instructions.Select(x =>
                new Instruction((OpCode)x.Op, x.Arguments, x.SourceLine, x.Condition)).ToList();

            return (instructions, new Dictionary<string, int>(compiled.Labels, StringComparer.OrdinalIgnoreCase), compiled.SourceLines);
        }

        var expanded = ScriptPreprocessor.ExpandIncludes(normalized, _provider);
        var (inst, labels) = _parser.Parse(expanded.Lines, normalized);
        return (inst, labels, expanded.Lines);
    }
}
