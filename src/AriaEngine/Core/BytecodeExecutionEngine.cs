#if ARIA_BYTECODE
using System;
using System.Collections.Generic;
using System.IO;
using AriaEngine.Assets;
using AriaEngine.Compiler;
using AriaEngine.Core;

namespace AriaEngine.Core;

/// <summary>
/// バイトコード実行エンジン
/// .ariaスクリプトからバイトコードへの変換と実行を統合
/// </summary>
public sealed class BytecodeExecutionEngine : IDisposable
{
    private readonly ErrorReporter _reporter;
    private readonly Parser _parser;
    private readonly BytecodeCompiler _compiler;
    private readonly OptimizedBytecodeVM _vm;
    private readonly Dictionary<string, BytecodeFile> _compiledScripts = new();
    private readonly IAssetProvider _assetProvider;

    public BytecodeExecutionEngine(ErrorReporter reporter, IAssetProvider assetProvider)
    {
        _reporter = reporter;
        _assetProvider = assetProvider;
        _parser = new Parser(reporter);
        _compiler = new BytecodeCompiler(reporter);
        _vm = new OptimizedBytecodeVM();

        // VMのイベントハンドラを設定
        _vm.OnDebugLog += msg => Console.WriteLine($"[VM] {msg}");
        _vm.OnError += (loc, ex) => _reporter.ReportException($"VM_ERROR_{loc}", ex, $"VM error at {loc}");
    }

    /// <summary>
    /// スクリプトをコンパイルして実行
    /// </summary>
    public void ExecuteScript(string scriptPath)
    {
        // コンパイル済みスクリプトを取得またはコンパイル
        var bytecodeFile = GetOrCompileScript(scriptPath);

        // VMにロードして実行
        _vm.Load(bytecodeFile);
        _vm.Run();
    }

    /// <summary>
    /// バイトコードファイルを直接実行
    /// </summary>
    public void ExecuteBytecode(string bytecodePath)
    {
        using var stream = _assetProvider.OpenRead(bytecodePath);
        byte[] bytecodeBytes = new byte[stream.Length];
        stream.Read(bytecodeBytes, 0, (int)stream.Length);

        _vm.Load(bytecodeBytes);
        _vm.Run();
    }

    /// <summary>
    /// スクリプトをコンパイル（キャッシュに保存）
    /// </summary>
    private BytecodeFile GetOrCompileScript(string scriptPath)
    {
        // キャッシュチェック
        if (_compiledScripts.TryGetValue(scriptPath, out var cachedFile))
            return cachedFile;

        // スクリプトを読み込み
        string[] lines = _assetProvider.ReadAllLines(scriptPath);
        var parseResult = _parser.Parse(lines, scriptPath);

        // バイトコードにコンパイル
        var bytecodeFile = _compiler.Compile(parseResult.Instructions, Path.GetFileNameWithoutExtension(scriptPath));

        // キャッシュに保存
        _compiledScripts[scriptPath] = bytecodeFile;

        return bytecodeFile;
    }

    /// <summary>
    /// キャッシュをクリア
    /// </summary>
    public void ClearCache()
    {
        _compiledScripts.Clear();
    }

    /// <summary>
    /// VMへの直接アクセス
    /// </summary>
    public OptimizedBytecodeVM VM => _vm;

    public void Dispose()
    {
        _vm?.Dispose();
        _compiledScripts.Clear();
    }
}
#endif
