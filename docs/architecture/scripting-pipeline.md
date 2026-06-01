# スクリプトパイプライン

このドキュメントでは、AriaEngineの **`.aria` スクリプト処理パイプライン** について詳しく説明します。プリプロセス → バンドルコンパイル → 暗号化 → 起動時ロードの4段階を支える `Scripting/` 配下のコンポーネントを扱います。

## 概要

AriaEngineは `.aria` スクリプトを以下のパイプラインで処理します:

```
┌─────────────────────────────────────────────────────┐
│  1. ScriptPreprocessor.ExpandIncludes               │
│     - include "path.aria" 解決                       │
│     - 循環参照検出                                    │
│     - 結果: ExpandedScript (Lines + Dependencies)    │
└──────────────────────┬──────────────────────────────┘
                       │
                       ▼
┌─────────────────────────────────────────────────────┐
│  2. Parser.Parse                                     │
│     - トークン化 + 命令生成                            │
│     - ラベル解決                                      │
│     - 結果: ParseResult                                │
│     (Instructions, Labels, Functions,                 │
│      Structs, Enums, OwnedSprites)                    │
└──────────────────────┬──────────────────────────────┘
                       │
        ┌──────────────┴──────────────┐
        │                             │
        ▼                             ▼
┌──────────────────────┐  ┌─────────────────────────┐
│ [Dev モード]          │  │ [Release モード]         │
│ ScriptLoader          │  │ ScriptCompiler           │
│ (Parser.Parse 直接)   │  │  + CompiledBundleCodec   │
│                       │  │  (CompiledScriptBundle   │
│                       │  │   → ARIAC1 バイナリ)     │
└────────┬──────────────┘  └──────────┬──────────────┘
         │                            │
         │                            ▼
         │                  ┌──────────────────────────┐
         │                  │ ScriptLoader             │
         │                  │ (バンドルから Instructions│
         │                  │  復元)                    │
         │                  └─────────┬────────────────┘
         │                            │
         └─────────────┬──────────────┘
                       │
                       ▼
┌─────────────────────────────────────────────────────┐
│  4. VirtualMachine.Step                              │
│     - ParseResult.Instructions 実行                  │
│     - GameState 更新                                  │
└─────────────────────────────────────────────────────┘
```

### ディレクトリ構成

```
src/AriaEngine/Scripting/
├── ScriptPreprocessor.cs       # 97 lines
├── ScriptCompiler.cs           # 72 lines
├── ScriptLoader.cs             # 83 lines
├── CompiledScriptBundle.cs     # 34 lines
├── CompiledBundleCodec.cs      # 57 lines
└── AriaScriptJsonContext.cs    # 17 lines
```

## ScriptPreprocessor — include 展開

**役割**: コンパイル前のテキスト前処理。`include "path.aria"` ディレクティブを再帰的に解決し、循環参照を検出する。

**ファイル**: `Scripting/ScriptPreprocessor.cs` (97 lines)

### API

```csharp
public sealed class ExpandedScript
{
    public string ScriptPath { get; init; } = "";
    public string[] Lines { get; init; } = Array.Empty<string>();
    public HashSet<string> Dependencies { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

public static class ScriptPreprocessor
{
    public static ExpandedScript ExpandIncludes(string scriptPath, IAssetProvider provider);
    public static string NormalizePath(string path);
}
```

### 動作

```csharp
public static ExpandedScript ExpandIncludes(string scriptPath, IAssetProvider provider)
{
    string normalizedRoot = NormalizePath(scriptPath);
    var lines = new List<string>();
    var deps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var stack = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    ExpandCore(normalizedRoot, provider, lines, deps, stack);

    return new ExpandedScript
    {
        ScriptPath = normalizedRoot,
        Lines = lines.ToArray(),
        Dependencies = deps
    };
}
```

### 内部実装

```csharp
private static void ExpandCore(
    string scriptPath, IAssetProvider provider,
    List<string> output, HashSet<string> deps, HashSet<string> stack)
{
    string normalized = NormalizePath(scriptPath);
    if (!provider.Exists(normalized))
        throw new FileNotFoundException($"Script file not found: {normalized}");

    if (stack.Contains(normalized))
        throw new InvalidOperationException($"include cycle detected: {normalized}");

    stack.Add(normalized);
    deps.Add(normalized);

    string[] lines = provider.ReadAllLines(normalized);
    string baseDir = GetDirectory(normalized);

    foreach (string raw in lines)
    {
        string line = raw.Trim();
        if (TryParseInclude(line, out string includePath))
        {
            string resolved = NormalizePath(ResolveRelative(baseDir, includePath));
            ExpandCore(resolved, provider, output, deps, stack);
            continue;
        }
        output.Add(raw);
    }

    stack.Remove(normalized);
}
```

### include パース

```csharp
private static bool TryParseInclude(string line, out string includePath)
{
    includePath = "";
    if (!line.StartsWith("include ", StringComparison.OrdinalIgnoreCase))
        return false;

    int firstQuote = line.IndexOf('"');
    if (firstQuote < 0) return false;
    int secondQuote = line.IndexOf('"', firstQuote + 1);
    if (secondQuote < 0) return false;
    includePath = line.Substring(firstQuote + 1, secondQuote - firstQuote - 1);
    return !string.IsNullOrWhiteSpace(includePath);
}
```

**注**: ディレクティブは `include "path.aria"` (スペース区切り)。NScripterの `#include` ではない。

### パス正規化

```csharp
public static string NormalizePath(string path) => path.Replace('\\', '/').TrimStart('/');
```

- `\` → `/` (Windows/Unix 透過)
- 先頭の `/` 除去 (provider ルートからの相対に統一)

### 相対パス解決

```csharp
private static string ResolveRelative(string baseDir, string includePath)
{
    if (Path.IsPathRooted(includePath)) return includePath;
    if (string.IsNullOrEmpty(baseDir)) return includePath;
    return Path.Combine(baseDir, includePath);
}
```

- 絶対パスはそのまま
- 相対パスは `baseDir` からの相対
- 空 baseDir は includePath そのまま

### 例

```
main.aria
└── include "ui/main_ui.aria"
    └── include "../shared/buttons.aria"
```

実行後、`ExpandedScript.Lines` には:
- `main.aria` の全行 (include 行は除去)
- `ui/main_ui.aria` の全行 (include 行は除去)
- `shared/buttons.aria` の全行

`Dependencies` には:
- `main.aria`
- `ui/main_ui.aria`
- `shared/buttons.aria`

### スクリプト例

```aria
*chapter_1
    bg "forest.png", 0
    text "森の中に入った。"

include "scenarios/common_actions.aria"
include "scenarios/choices.aria"

*chapter_2
    text "分岐へ"
```

## ScriptCompiler — バンドルコンパイル

**役割**: 複数スクリプトを `CompiledScriptBundle` に一括コンパイル。`aria-compile` CLI や Release ビルド時のプリコンパイルで使用。

**ファイル**: `Scripting/ScriptCompiler.cs` (72 lines)

### API

```csharp
public sealed class ScriptCompiler
{
    public ScriptCompiler(Parser parser, ErrorReporter reporter, IAssetProvider provider);
    public CompiledScriptBundle CompileBundle(string initPath, string mainPath);
}
```

### 動作

```csharp
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
            Functions = parsed.Functions,
            Structs = parsed.Structs,
            Enums = parsed.Enums,
            OwnedSprites = new HashSet<string>(parsed.OwnedSprites, StringComparer.OrdinalIgnoreCase),
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
```

### 設計上の重要事項

**includes は本体に展開済み**: `ScriptPreprocessor.ExpandIncludes` が `include` ディレクティブを再帰的に解決するため、include ファイルを再コンパイルすると「本体では解決済みのラベルが、include ファイル単体では未定義」という偽の未解決ラベルエラーが発生する。これを避けるため、Compiler は BFS でルートスクリプトのみをキューに入れ、include は親スクリプトの一部として扱う。

### 出力

```csharp
new CompiledScriptBundle
{
    Version = "1",
    CreatedAtUtc = DateTime.UtcNow,
    InitPath = "init.aria",
    MainPath = "assets/scripts/main.aria",
    Scripts = new Dictionary<string, CompiledScript>(StringComparer.OrdinalIgnoreCase)
    {
        ["init.aria"] = ...,
        ["assets/scripts/main.aria"] = ...,
    }
}
```

## CompiledScriptBundle — バンドルデータモデル

**役割**: コンパイル済みバンドルのデータモデル。`Scripting/` 配下と `AriaScriptJsonContext` の中核。

**ファイル**: `Scripting/CompiledScriptBundle.cs` (34 lines)

### 3 つのクラス

```csharp
public sealed class CompiledScriptBundle
{
    public string Version { get; set; } = "1";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public string InitPath { get; set; } = "init.aria";
    public string MainPath { get; set; } = "assets/scripts/main.aria";
    public Dictionary<string, CompiledScript> Scripts { get; set; }
        = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class CompiledScript
{
    public string Path { get; set; } = "";
    public List<CompiledInstruction> Instructions { get; set; } = new();
    public Dictionary<string, int> Labels { get; set; }
        = new(StringComparer.OrdinalIgnoreCase);
    public List<FunctionInfo> Functions { get; set; } = new();
    public List<StructDefinition> Structs { get; set; } = new();
    public List<EnumDefinition> Enums { get; set; } = new();
    public HashSet<string> OwnedSprites { get; set; }
        = new(StringComparer.OrdinalIgnoreCase);
    public string[] SourceLines { get; set; } = Array.Empty<string>();
}

public sealed class CompiledInstruction
{
    public int Op { get; set; }                          // OpCode の int 値
    public List<string> Arguments { get; set; } = new();
    public int SourceLine { get; set; }
    public List<string>? Condition { get; set; }        // if ブロック用
}
```

### 主要フィールド

| クラス | フィールド | 用途 |
|--------|-----------|------|
| `CompiledScriptBundle` | `Version` | バンドルフォーマットバージョン (現在 `"1"`) |
| | `CreatedAtUtc` | ビルドタイムスタンプ |
| | `InitPath` / `MainPath` | 起動時のエントリ指定 |
| | `Scripts` | スクリプトパス → コンパイル結果 |
| `CompiledScript` | `Path` | 元スクリプトパス |
| | `Instructions` | 命令列 |
| | `Labels` | ラベル名 → 命令 index |
| | `Functions` / `Structs` / `Enums` | v2 strict 型情報 |
| | `OwnedSprites` | 自動解放対象スプライト |
| | `SourceLines` | デバッグ用ソース行 |
| `CompiledInstruction` | `Op` | `OpCode` enum の int 値 |
| | `Arguments` | 文字列引数リスト |
| | `SourceLine` | 元の行番号 (エラー報告用) |
| | `Condition` | `if` ブロックの条件トークン |

## CompiledBundleCodec — ARIAC1 バイナリ

**役割**: `CompiledScriptBundle` ↔ `ARIAC1` バイナリ変換。オプションで AES 暗号化。

**ファイル**: `Scripting/CompiledBundleCodec.cs` (57 lines)

### バイナリ形式

```
[6 bytes] Magic "ARIAC1"
[1 byte ] Encrypted flag (0 = 平文 JSON, 1 = 暗号化 JSON)
[4 bytes] Payload length (little-endian int)
[? bytes] Payload (JSON, 暗号化時 AES)
```

### API

```csharp
public static class CompiledBundleCodec
{
    public static void Save(string outputPath, CompiledScriptBundle bundle, string? keyMaterial);
    public static CompiledScriptBundle Load(Stream stream, string? keyMaterial);
}
```

### Save 実装

```csharp
private static readonly byte[] Magic = Encoding.ASCII.GetBytes("ARIAC1");

public static void Save(string outputPath, CompiledScriptBundle bundle, string? keyMaterial)
{
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath)) ?? ".");

    byte[] json = JsonSerializer.SerializeToUtf8Bytes(bundle, AriaScriptJsonContext.Default.CompiledScriptBundle);
    bool enc = !string.IsNullOrWhiteSpace(keyMaterial);
    byte[] payload = enc ? CryptoHelper.Encrypt(json, CryptoHelper.DeriveKey(keyMaterial!)) : json;

    using var fs = File.Create(outputPath);
    fs.Write(Magic, 0, Magic.Length);
    fs.WriteByte(enc ? (byte)1 : (byte)0);
    fs.Write(BitConverter.GetBytes(payload.Length), 0, sizeof(int));
    fs.Write(payload, 0, payload.Length);
}
```

### Load 実装

```csharp
public static CompiledScriptBundle Load(Stream stream, string? keyMaterial)
{
    byte[] magic = new byte[Magic.Length];
    stream.ReadExactly(magic);
    if (!magic.AsSpan().SequenceEqual(Magic))
        throw new InvalidOperationException("Invalid ARIAC header.");

    int encFlag = stream.ReadByte();
    if (encFlag < 0) throw new InvalidOperationException("Invalid ARIAC payload flag.");

    byte[] lenBuf = new byte[sizeof(int)];
    stream.ReadExactly(lenBuf);
    int len = BitConverter.ToInt32(lenBuf, 0);
    if (len <= 0) throw new InvalidOperationException("Invalid ARIAC payload length.");

    byte[] payload = new byte[len];
    stream.ReadExactly(payload);

    byte[] plain = payload;
    if (encFlag == 1)
    {
        if (string.IsNullOrWhiteSpace(keyMaterial))
            throw new InvalidOperationException("Encrypted ARIAC requires --key.");
        plain = CryptoHelper.Decrypt(payload, CryptoHelper.DeriveKey(keyMaterial!));
    }

    return JsonSerializer.Deserialize(plain, AriaScriptJsonContext.Default.CompiledScriptBundle)
        ?? throw new InvalidOperationException("Failed to deserialize ARIAC.");
}
```

### 暗号化

`AriaEngine.Packaging.CryptoHelper` を使用:
- `CryptoHelper.Encrypt(json, key)` — JSON → AES 暗号化バイト列
- `CryptoHelper.Decrypt(payload, key)` — バイト列 → JSON
- `CryptoHelper.DeriveKey(material)` — 文字列 → 鍵 (PBKDF2 等の KDF 経由)

`keyMaterial` は CLI 引数 `--key` または環境変数 `ARIA_PACK_KEY` で指定。

### フォーマット判断

暗号化ヘッダ (1 byte) で平文と暗号化を区別:
- `0`: 平文 JSON
- `1`: 暗号化 (復号に `--key` 必須)

これにより **Dev/Release で同じデコーダを使用しつつ、Release だけ鍵必須** というフローが可能。

## AriaScriptJsonContext — System.Text.Json 設定

**役割**: NativeAOT 互換の JSON シリアライズ設定。`CompiledBundle` 系のすべてのデータモデルをソース生成でカバー。

**ファイル**: `Scripting/AriaScriptJsonContext.cs` (17 lines)

```csharp
[JsonSourceGenerationOptions(WriteIndented = false)]
[JsonSerializable(typeof(CompiledScriptBundle))]
[JsonSerializable(typeof(CompiledScript))]
[JsonSerializable(typeof(CompiledInstruction))]
[JsonSerializable(typeof(FunctionInfo))]
[JsonSerializable(typeof(ParameterInfo))]
[JsonSerializable(typeof(StructDefinition))]
[JsonSerializable(typeof(StructField))]
[JsonSerializable(typeof(EnumDefinition))]
internal sealed partial class AriaScriptJsonContext : JsonSerializerContext
{
}
```

**AOT 互換**: `System.Text.Json` の source generator がリフレクションを回避し、NativeAOT ビルドで動作。

**設定**: `WriteIndented = false` (バイナリサイズ削減)。

## ScriptLoader — 起動時ロード

**役割**: 起動時のスクリプトロードを `RunMode` で分岐。Dev モードでは `ScriptPreprocessor` + `Parser`、Release モードでは `CompiledScriptBundle` から直接復元。

**ファイル**: `Scripting/ScriptLoader.cs` (83 lines)

### RunMode

```csharp
public enum RunMode
{
    Dev,
    Release
}
```

- **Dev**: ソース `.aria` を直接パース (含 include 展開)。`RunMode.Dev` 固定の Web ランタイム。
- **Release**: コンパイル済み `.ariac` バンドルから `Instructions` 復元。

### API

```csharp
public sealed class ScriptLoader
{
    public ScriptLoader(Parser parser, IAssetProvider provider, RunMode mode, CompiledScriptBundle? bundle = null);
    public ParseResult LoadScript(string path);
}
```

### ロード処理

```csharp
public ParseResult LoadScript(string path)
{
    string normalized = ScriptPreprocessor.NormalizePath(path);
    TraceLoad($"start {normalized} mode={_mode} bundle={_bundle is not null}");

    // Release モード: バンドルから復元
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

    // Dev モード: プリプロセス + パース
    TraceLoad($"expand-includes {normalized}");
    var expanded = ScriptPreprocessor.ExpandIncludes(normalized, _provider);
    TraceLoad($"parse {normalized} lines={expanded.Lines.Length}");
    return _parser.Parse(expanded.Lines, normalized);
}
```

### 起動トレース

```csharp
private static void TraceLoad(string marker)
{
    if (!string.Equals(Environment.GetEnvironmentVariable("ARIA_STARTUP_TRACE"), "1", StringComparison.Ordinal))
        return;

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
```

**使用方法**:
```bash
# Windows (PowerShell)
$env:ARIA_STARTUP_TRACE=1
dotnet run --project src/AriaEngine
# → startup_trace.log が生成される
```

**用途**: 起動パフォーマンス分析 (include 展開 / パース / バンドル復元の所要時間計測)。

## 起動シーケンス

### Dev モード (Web + Dev ビルド)

```
[App.razor / Program.cs]
  ↓
WebRuntimeHost.Boot(provider, options)
  ↓
Parser / ScriptLoader(parser, provider, RunMode.Dev) 生成
  ↓
LoadInitAndMain
  ├─ loader.LoadScript("init.aria")
  │  └─ ScriptPreprocessor.ExpandIncludes("init.aria", provider)
  │     └─ Parser.Parse(lines, "init.aria")
  │  → ParseResult (init)
  ├─ vm.LoadScript(init, "init.aria")
  ├─ RunUntilStopped
  ├─ vm.SetIncludeResolver(path => loader.LoadScript(path))
  ├─ loader.LoadScript(mainScript)  ← VM が動的 include 要求時のみ
  │  └─ ScriptPreprocessor.ExpandIncludes
  │     └─ Parser.Parse
  ├─ vm.LoadScript(main, mainScript)
  └─ RunUntilInteractive
```

### Release モード (Native + 暗号化 .ariac)

```
[Program.cs]
  ↓
ScriptCompiler.CompileBundle(initPath, mainPath)
  ├─ ScriptPreprocessor.ExpandIncludes (各スクリプト)
  ├─ Parser.Parse (各スクリプト)
  └─ CompiledScriptBundle メモリ保持
  ↓
CompiledBundleCodec.Save(scripts.ariac, bundle, key)  (開発時)
  ↓
[起動時]
ScriptLoader(parser, provider, RunMode.Release, bundle)
  ↓
loader.LoadScript(path)  ← バンドルから復元
```

## v3 split Pak 形式との連携

**注**: 現状の `ScriptLoader` は `CompiledScriptBundle` 単一ファイルからの復元が基本。`v3 split` 形式 (`scripts.ariac` + `scenario.aris`) の場合は `ScriptLoader` の bundle 経路ではなく、`scenario.aris` を直接 `Parser.Parse` に通す。

```csharp
// v3 split pak: .aria scripts are stored directly in scenario.aris; parse plain text
TraceLoad($"expand-includes {normalized}");
var expanded = ScriptPreprocessor.ExpandIncludes(normalized, _provider);
TraceLoad($"parse {normalized} lines={expanded.Lines.Length}");
return _parser.Parse(expanded.Lines, normalized);
```

つまり **v3 split では `RunMode.Release` でもバンドル経由ではなく、展開した `.aria` テキストを直接パース** する。

## データモデル全体図

```
┌────────────────────────────────────────────────────────────┐
│ CompiledScriptBundle (Version, CreatedAtUtc, InitPath,    │
│                        MainPath, Scripts)                  │
│                                                            │
│ Scripts:                                                   │
│  ┌──────────────────────────────────────────────┐         │
│  │ CompiledScript (Path, Instructions, Labels,  │         │
│  │                 Functions, Structs, Enums,   │         │
│  │                 OwnedSprites, SourceLines)   │         │
│  │                                              │         │
│  │ Instructions:                                 │         │
│  │  ┌────────────────────────────────────┐      │         │
│  │  │ CompiledInstruction (Op,           │      │         │
│  │  │                       Arguments,   │      │         │
│  │  │                       SourceLine,  │      │         │
│  │  │                       Condition)   │      │         │
│  │  └────────────────────────────────────┘      │         │
│  │  ...                                         │         │
│  └──────────────────────────────────────────────┘         │
│  ...                                                       │
└────────────────────────────────────────────────────────────┘
```

## セキュリティ

### 暗号化

- AES (CBC モード想定) で `.ariac` ペイロードを暗号化
- 鍵は `CryptoHelper.DeriveKey(keyMaterial)` で KDF (PBKDF2) 経由で導出
- `--key` 未指定時は平文 JSON (Dev 用途)
- 平文/暗号化の区別はヘッダ 1 byte で行うため、**同じデコーダで両方処理可能**

### 不正入力の防御

- マジック不一致 → `InvalidOperationException("Invalid ARIAC header.")`
- 暗号化フラグが不正 → `InvalidOperationException("Invalid ARIAC payload flag.")`
- 長さフィールドが ≤ 0 → `InvalidOperationException("Invalid ARIAC payload length.")`
- 復号鍵欠落 → `InvalidOperationException("Encrypted ARIAC requires --key.")`
- JSON デコード失敗 → `InvalidOperationException("Failed to deserialize ARIAC.")`

### Dev/Release の使い分け

| 側面 | Dev | Release |
|------|-----|---------|
| 入力 | ソース `.aria` | `.ariac` バイナリ |
| 性能 | 遅い (毎起動パース) | 高速 (デコードのみ) |
| 機密性 | 低い (平文) | 高い (暗号化可) |
| 編集 | 直接編集 → すぐ反映 | 再コンパイル必要 |
| 使用例 | 開発 / Web | 配布 |

## 拡張ガイド

### 新しいプリプロセッサディレクティブを追加する

1. `ScriptPreprocessor.TryParseInclude` と同様のヘルパーを追加
2. `ExpandCore` のループ内で分岐
3. 結果として `ExpandedScript` の Lines に変換後の行を追加

例: `#define` 展開を追加する場合:
```csharp
private static bool TryParseDefine(string line, out string macro, out string value)
{
    if (!line.StartsWith("#define ", StringComparison.OrdinalIgnoreCase)) { ... }
    // ... tokenize ...
}
```

### 新しいバンドルフォーマットバージョンを追加する

1. `CompiledScriptBundle.Version` の判定を `Load` 時に追加
2. 互換性のない変更は `Version = "2"` にバンプ
3. 旧 Version も読み込めるよう `switch` 分岐

### 新しい命令カテゴリを追加する

1. `OpCode.cs` に enum 追加 (末尾に追加して既存番号を保護)
2. `CommandRegistry.cs` に canonical name 登録
3. 対応する `*CommandHandler` で `HandledCodes` + `Execute` 実装
4. (必要なら) `aria-lint` の E005 (引数の型不一致) 検査を更新

## 関連ドキュメント

- [アーキテクチャ概要](overview.md) — 全体構成
- [Parser](parser.md) — スクリプト解析 (`ParseResult` の出力元)
- [VM](vm.md) — 仮想マシン (`ParseResult.Instructions` の実行)
- [Platform](platform.md) — IAssetProvider 抽象
- [Tools](tools.md) — `aria-compile` / `aria-pack` の CLI
