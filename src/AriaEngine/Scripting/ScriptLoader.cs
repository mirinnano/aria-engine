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
        if (_mode == RunMode.Release)
        {
            if (_bundle is null) throw new InvalidOperationException("Release mode requires compiled script bundle.");
            if (!_bundle.Scripts.TryGetValue(normalized, out var compiled))
                throw new InvalidOperationException($"Compiled script not found in bundle: {normalized}");

            var instructions = compiled.Instructions.Select(x =>
                new Instruction((OpCode)x.Op, x.Arguments, x.SourceLine, x.Condition)).ToList();

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

        var expanded = ScriptPreprocessor.ExpandIncludes(normalized, _provider);
        return _parser.Parse(expanded.Lines, normalized);
    }
}
