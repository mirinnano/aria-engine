using System;
using System.Collections.Generic;
using System.Linq;
using AriaEngine.Assets;
using AriaEngine.Core;

namespace AriaEngine.Scripting;

public sealed class ScriptCompiler
{
    private readonly Parser _parser;
    private readonly ErrorReporter _reporter;
    private readonly IAssetProvider _provider;

    public ScriptCompiler(Parser parser, ErrorReporter reporter, IAssetProvider provider)
    {
        _parser = parser;
        _reporter = reporter;
        _provider = provider;
    }

    public CompiledScriptBundle CompileBundle(string initPath, string mainPath)
    {
        string normalizedInit = ScriptPreprocessor.NormalizePath(initPath);
        string normalizedMain = ScriptPreprocessor.NormalizePath(mainPath);

        var bundle = new CompiledScriptBundle
        {
            InitPath = normalizedInit,
            MainPath = normalizedMain
        };

        var queue = new Queue<string>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        queue.Enqueue(normalizedInit);
        queue.Enqueue(normalizedMain);

        while (queue.Count > 0)
        {
            string scriptPath = queue.Dequeue();
            if (!visited.Add(scriptPath)) continue;

            var expanded = ScriptPreprocessor.ExpandIncludes(scriptPath, _provider);
            var parsed = _parser.Parse(expanded.Lines, scriptPath);

            var compiled = new CompiledScript
            {
                Path = scriptPath,
                Labels = new Dictionary<string, int>(parsed.Labels, StringComparer.OrdinalIgnoreCase),
                SourceLines = expanded.Lines,
                Instructions = parsed.Instructions.Select(i => new CompiledInstruction
                {
                    Op = (int)i.Op,
                    Arguments = i.Arguments.ToList(),
                    SourceLine = i.SourceLine,
                    Condition = i.Condition.IsEmpty ? null : i.Condition.ToTokenList()
                }).ToList()
            };

            bundle.Scripts[scriptPath] = compiled;

            // Includes are already expanded into the owning script. Compiling included
            // files again as standalone scripts creates false unresolved-label errors.
        }

        return bundle;
    }
}
