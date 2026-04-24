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
            var (instructions, labels) = _parser.Parse(expanded.Lines, scriptPath);

            var compiled = new CompiledScript
            {
                Path = scriptPath,
                Labels = new Dictionary<string, int>(labels, StringComparer.OrdinalIgnoreCase),
                SourceLines = expanded.Lines,
                Instructions = instructions.Select(i => new CompiledInstruction
                {
                    Op = (int)i.Op,
                    Arguments = i.Arguments.ToList(),
                    SourceLine = i.SourceLine,
                    Condition = i.Condition?.ToList()
                }).ToList()
            };

            bundle.Scripts[scriptPath] = compiled;

            foreach (string dep in expanded.Dependencies)
            {
                if (!visited.Contains(dep))
                    queue.Enqueue(dep);
            }
        }

        return bundle;
    }
}
