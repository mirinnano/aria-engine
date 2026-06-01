# CLI ツール群

このドキュメントでは、AriaEngineに同梱される **8 つの CLI サブコマンド** について詳しく説明します。各ツールはエンジン機能を独立して実行する独立した `static class` として実装されています。

## 概要

```
dotnet run --project src/AriaEngine -- <command> [args]
```

| コマンド | 役割 | 規模 | アーティファクト |
|---------|------|-----:|---------------|
| `aria-lint` | 静的解析 (E001-E012, W001-W008) | 1183 lines | stdout (Issue 単位) |
| `aria-compile` | スクリプト暗号化コンパイル | 69 lines | `scripts.ariac` |
| `aria-pack` | アセットパッキング (3 サブコマンド) | 520 lines | `data.pak` / `*.patch` |
| `aria-doc` | ドキュメント生成 | 214 lines | JSON ドキュメント |
| `aria-format` | コードフォーマット | 206 lines | stdout / in-place |
| `aria-save` | セーブデータ操作 (4 サブコマンド) | 278 lines | stdout |
| `aria-flow-check` | フロー解析 (静的+実行) | 363 lines | stdout (Issue 単位) |
| `aria-i18n-check` | 多言語キー整合性 | 308 lines | stdout (Issue 単位) |

### 共通仕様

**エントリポイント**: 全コマンドが `public static int Run(string[] args)` を持つ。

**終了コード**:
- `0`: 成功
- `1`: 引数エラー / 一般的エラー
- `2`: ファイル / ディレクトリ未発見
- `3`: 不正な状態 (例: aria-pack の InvalidOperationException)
- `4`: 予期しない例外

**`--help` / `-h`**: 全コマンドで標準サポート。

**環境変数**:
- `ARIA_PACK_KEY` — 暗号化キー (aria-compile / aria-pack で使用)

## aria-lint

**役割**: `.aria` スクリプトの静的解析。E001-E012 (エラー) + W001-W008 (警告) を検出。

**ファイル**: `Tools/AriaLintCommand.cs` (1183 lines)

### 使い方

```bash
dotnet run --project src/AriaEngine -- aria-lint <file.aria> [file2.aria ...] [--verbose]
```

**終了コード**: 0 (警告のみ/clean), 2 (引数未指定), 1 (エラーあり)

### 出力形式

```
{FilePath}:{Line}:{Column}: {severity}: [{Rule}] {Message}
```

例:
```
main.aria:42:5: error: [E001] Type mismatch: expected int, got string for %foo
main.aria:100:1: warning: [W002] btnwait result not saved to named register
```

**severity**: `error` / `warning` / `info`

### ルール

| コード | 種別 | 説明 |
|-------|------|------|
| E001 | error | 数値レジスタ `%` と文字列レジスタ `$` の混同 |
| E002 | error | 未定義の変数参照 |
| E003 | error | sprite 型 vs int 型 / flag 型 の混同 (`@` / `&`) |
| E004 | error | ラベル未定義 |
| E005 | error | 引数の型不一致 |
| E006 | error | `readonly` への再代入 |
| E007 | error | `owned` sprite の二重 drop |
| E008 | error | `scope` 内 sprite のスコープ外参照 |
| E009 | error | `func` シグネチャ不一致 |
| E010 | error | v2 strict 構文違反 |
| E011 | error | 静的解析の致命的エラー |
| E012 | error | 内部不変条件違反 |
| W001 | warning | 未使用変数 |
| W002 | warning | `btnwait %0` 等の一時レジスタ直接参照 |
| W003 | warning | 到達不能コード |
| W004 | warning | `goto` の連鎖 |
| W005 | warning | 深いネスト |
| W006 | warning | マクロ未展開 |
| W007 | warning | 冗長な代入 |
| W008 | warning | 推奨されない記法 |

**主要モデル**:

```csharp
public enum LintSeverity { Info, Warning, Error }

public sealed class LintIssue
{
    public string FilePath { get; }
    public int Line { get; }
    public int Column { get; }
    public LintSeverity Severity { get; }
    public string Rule { get; }
    public string Message { get; }
}

public sealed class LintResult
{
    public List<LintIssue> Issues { get; } = new();
    public int ErrorCount => Issues.Count(i => i.Severity == LintSeverity.Error);
    public int WarningCount => Issues.Count(i => i.Severity == LintSeverity.Warning);
    public int InfoCount => Issues.Count(i => i.Severity == LintSeverity.Info);
    public bool HasErrors => ErrorCount > 0;
}
```

### 関連ファイル

- `Tools/AriaLintCommand.cs` (1183 lines)
- 詳細仕様: [docs/reference/opcodes/](../reference/opcodes/)

## aria-compile

**役割**: 1 つ以上の `.aria` スクリプトを `CompiledScriptBundle` にまとめ、`ARIAC1` バイナリ形式 (`.ariac`) に暗号化して書き出す。Release モードの VM が直接ロードする形式。

**ファイル**: `Tools/AriaCompileCommand.cs` (69 lines)

### 使い方

```bash
dotnet run --project src/AriaEngine -- aria-compile \
  --init init.aria \
  --main assets/scripts/main.aria \
  --out build/scripts.ariac \
  [--key <secret>]
```

| 引数 | デフォルト | 説明 |
|------|-----------|------|
| `--init` | `init.aria` | 初期化スクリプトパス |
| `--main` | `assets/scripts/main.aria` | メインスクリプトパス |
| `--out` | `build/scripts.ariac` | 出力パス |
| `--key` | `$ARIA_PACK_KEY` | 暗号化キー (省略時は平文 JSON) |

### 動作

```csharp
public static int Run(string[] args)
{
    string initPath = "init.aria";
    string mainPath = "assets/scripts/main.aria";
    string outputPath = Path.Combine("build", "scripts.ariac");
    string? key = Environment.GetEnvironmentVariable("ARIA_PACK_KEY");

    // ... 引数解析 ...

    var reporter = new ErrorReporter();
    var parser = new Parser(reporter);
    var provider = new DiskAssetProvider(Directory.GetCurrentDirectory());
    var compiler = new ScriptCompiler(parser, reporter, provider);

    CompiledScriptBundle bundle = compiler.CompileBundle(initPath, mainPath);
    // エラーチェック → ログファイル出力 → return 2
    CompiledBundleCodec.Save(outputPath, bundle, key);
    // 出力情報表示 → return 0
}
```

### 出力

```
Compiled scripts: 12
Output: build/scripts.ariac
Output encrypted.
```

(または `Warning: output is not encrypted (no --key provided).`)

### 関連ファイル

- `Tools/AriaCompileCommand.cs`
- `Scripting/ScriptCompiler.cs`
- `Scripting/CompiledBundleCodec.cs`
- [architecture/scripting-pipeline.md](scripting-pipeline.md)

## aria-pack

**役割**: アセットディレクトリ (`assets/`) を `data.pak` 形式にパッケージング。3 つのサブコマンドで **build / diff / apply** のリリースパイプラインを支える。

**ファイル**: `Tools/AriaPackCommand.cs` (520 lines)

### 使い方

```bash
# Build
dotnet run --project src/AriaEngine -- aria-pack build \
  --input assets \
  [--init init.aria] \
  [--compiled build/scripts.ariac] \
  --output build/data.pak \
  [--key <secret>] \
  [--verbose]

# Diff
dotnet run --project src/AriaEngine -- aria-pack diff \
  --base build/old.pak \
  --new build/new.pak \
  --out build/patch.patch \
  [--key <secret>] \
  [--verbose]

# Apply
dotnet run --project src/AriaEngine -- aria-pack apply \
  --base build/old.pak \
  --patch build/patch.patch \
  --out build/updated.pak \
  [--key <secret>] \
  [--verbose]
```

### サブコマンド詳細

#### `build`

| 引数 | デフォルト | 説明 |
|------|-----------|------|
| `--input` | `assets` | パック対象ディレクトリ |
| `--init` | (なし) | 初期化スクリプト |
| `--compiled` | (なし) | 埋め込み済み `.ariac` |
| `--output` | `build/data.pak` | 出力 `.pak` ファイル |
| `--key` | `$ARIA_PACK_KEY` | 暗号化キー |
| `--format` | `v2` | Pak フォーマット (v2 / v3 split) |
| `--split` | `false` | スクリプト / アセット分割 |

#### `diff`

2 つの `.pak` 間の差分 `*.patch` を生成 (差分パッチによるダウンロードサイズ削減用)。

| 引数 | 説明 |
|------|------|
| `--base` | 旧 `.pak` |
| `--new` | 新 `.pak` |
| `--out` | 出力 `.patch` |
| `--key` | 復号キー (両ファイルが暗号化されている場合) |

#### `apply`

ベース `.pak` に `.patch` を適用して更新版 `.pak` を生成。

| 引数 | 説明 |
|------|------|
| `--base` | 旧 `.pak` |
| `--patch` | 差分 `.patch` |
| `--out` | 出力更新版 `.pak` |
| `--key` | 復号キー |

### 終了コード

- `0`: 成功
- `1`: 引数未指定 (`PrintUsage`)
- `2`: `DirectoryNotFoundException` / `FileNotFoundException`
- `3`: `InvalidOperationException` (不明なサブコマンド等)
- `4`: その他予期しない例外

### 依存モジュール

```csharp
using AriaEngine.Packaging;
using AriaEngine.Packaging.Compression;
```

- `PakWriter` / `PakReader` (`AriaEngine.Packaging`)
- 圧縮ユーティリティ (`AriaEngine.Packaging.Compression`)

### 関連ファイル

- `Tools/AriaPackCommand.cs` (520 lines)
- `Packaging/PakManifest.cs` (v2/v3 split 形式定義)

## aria-doc

**役割**: `.aria` スクリプトをパースし、定義された `func` / `struct` を JSON ドキュメントに書き出す。IDE / LSP サーバー / Web ドキュメント生成のインプット。

**ファイル**: `Tools/AriaDocCommand.cs` (214 lines)

### 使い方

```bash
dotnet run --project src/AriaEngine -- aria-doc <script.aria> --out <output_dir/>
```

| 引数 | 必須 | 説明 |
|------|------|------|
| `<script.aria>` | ✓ | 入力スクリプト |
| `--out` | ✓ | 出力ディレクトリ |

### 出力スキーマ

```json
{
  "file": "main.aria",
  "functions": [
    {
      "name": "my_module::show_message",
      "shortName": "show_message",
      "doc": "メッセージを表示する",
      "parameters": [
        { "name": "msg", "type": "string" }
      ],
      "returnType": "void"
    }
  ],
  "structs": [
    {
      "name": "Player",
      "fields": [
        { "name": "name", "type": "string" },
        { "name": "hp", "type": "int" }
      ]
    }
  ]
}
```

### 動作

```csharp
public static int Run(string[] args)
{
    // 引数解析: --out <dir>, <script.aria>
    Directory.CreateDirectory(outputDir);

    var reporter = new ErrorReporter();
    var parser = new Parser(reporter);
    string[] lines = File.ReadAllLines(scriptPath);
    var result = parser.Parse(lines, scriptPath);

    var docOutput = new DocOutput
    {
        File = Path.GetFileName(scriptPath),
        Functions = result.Functions.Select(f => new FunctionDoc { ... }),
        Structs = result.Structs.Select(s => new StructDoc { ... })
    };

    File.WriteAllText(
        Path.Combine(outputDir, Path.GetFileNameWithoutExtension(scriptPath) + ".json"),
        JsonSerializer.Serialize(docOutput, ...));
}
```

### 関連ファイル

- `Tools/AriaDocCommand.cs`
- `Core/FunctionInfo` / `Core/ParameterInfo` / `Core/StructDefinition` (パース結果)

## aria-format

**役割**: `.aria` スクリプトのインデント・空行・ブロック構造を整形。CI に組み込んでスタイル強制が可能。

**ファイル**: `Tools/AriaFormatCommand.cs` (206 lines)

### 使い方

```bash
# 標準出力に整形結果
dotnet run --project src/AriaEngine -- aria-format <script.aria>

# ファイルに上書き
dotnet run --project src/AriaEngine -- aria-format <script.aria> --write
```

### ブロック認識

```csharp
private static readonly HashSet<string> BlockOpeners = new(StringComparer.OrdinalIgnoreCase)
{
    "if", "while", "func", "scope", "match", "try", "switch", "for"
};

private static readonly HashSet<string> BlockClosers = new(StringComparer.OrdinalIgnoreCase)
{
    "endif", "wend", "endfunc", "end_scope", "endmatch", "endtry", "endswitch", "next"
};
```

**動作**:
- ブロック開始は次の行を 1 段インデント
- ブロック終了は現在の段から 1 段戻す
- 空行は 1 個まで (連続する空行を削除)
- ラベル (`*name`) は独立した行として保持

### 関連ファイル

- `Tools/AriaFormatCommand.cs` (206 lines)

## aria-save

**役割**: セーブデータディレクトリの内容を検査・検証・移行。セーブスロットの健全性チェックとフォーマットマイグレーション。

**ファイル**: `Tools/AriaSaveCommand.cs` (278 lines)

### 使い方

```bash
dotnet run --project src/AriaEngine -- aria-save <subcommand> [args] [--dir <saves_dir>]
```

### サブコマンド

| サブコマンド | 引数 | 説明 |
|------------|------|------|
| `list` | `--dir` | 全スロットを一覧 (スロット番号 + 作成日時 + 容量) |
| `info` | `info <slot>`, `--dir` | 指定スロットの詳細 (レジスタ / フラグ / プログラムカウンタ / セクション) |
| `validate` | `--dir` | 全スロットを整合性チェック (破損検出) |
| `migrate` | `--no-backup`, `--dir` | 旧バージョンのセーブデータを現行形式にマイグレーション |

`MaxSlots = 10` (スロット 0-9)。

### 終了コード

- `0`: 成功
- `1`: 一般的エラー
- `2`: 不正な引数 (スロット範囲外、不明なサブコマンド)

### 動作

```csharp
public static int Run(string[] args)
{
    // --help / -h 処理
    // --dir <dir> 抽出
    string subcommand = args[0].ToLowerInvariant();
    return subcommand switch
    {
        "list"     => RunList(saveDir),
        "info"     => RunInfo(slot, saveDir),       // slot 検証 (0-9)
        "validate" => RunValidate(saveDir),
        "migrate"  => RunMigrate(noBackup, saveDir),
        _          => 2 (unknown command)
    };
}
```

### 関連ファイル

- `Tools/AriaSaveCommand.cs`
- `Core/SaveManager` (実行時のセーブ/ロード)

## aria-flow-check

**役割**: パッケージされたビジュアルノベルスクリプトのフロー (チャプター遷移・到達性) を解析。**静的解析** と **実行シミュレーション** の 2 モード。

**ファイル**: `Tools/AriaFlowCheckCommand.cs` (363 lines)

### 使い方

```bash
dotnet run --project src/AriaEngine -- aria-flow-check \
  [--root .] \
  [--main assets/scripts/main.aria] \
  [--chapters 6] \
  [--max-steps 20000] \
  [--execute]
```

| 引数 | デフォルト | 説明 |
|------|-----------|------|
| `--root` | `.` | ルートディレクトリ |
| `--main` | `assets/scripts/main.aria` | メインスクリプト |
| `--chapters` | `6` | 期待されるチャプター数 |
| `--max-steps` | `20000` | 実行モードの最大ステップ数 |
| `--execute` | (なし) | 実行シミュレーションモードを有効化 |

### 動作

```csharp
public static int Run(string[] args)
{
    // ... 引数解析 ...
    var provider = new DiskAssetProvider(rootPath);
    var expanded = ScriptPreprocessor.ExpandIncludes(main, provider);
    var reporter = new ErrorReporter();
    var parseResult = new Parser(reporter).Parse(expanded.Lines, main);

    // パースエラー収集
    CheckFlow(expanded.Lines, parseResult, chapterCount, issues);
    if (execute && issues.Count == 0)
    {
        ExecuteFlow(parseResult, main, rootPath, chapterCount, maxSteps, issues);
    }
}
```

**解析項目**:
- ラベル到達性 (`*chapter_1` 〜 `*chapter_N` の全到達)
- 死コード (未参照ラベル)
- 無限ループ (`--max-steps` を超過した実行)
- チャプター遷移グラフの連結性

**使用モジュール**:
- `Scripting/ScriptPreprocessor.ExpandIncludes`
- `Core/Parser.Parse`
- `Scripting/RunMode.Dev` の ScriptLoader (実行モード時)
- `Core/VirtualMachine` (実行モード時)

### 関連ファイル

- `Tools/AriaFlowCheckCommand.cs` (363 lines)
- [architecture/scripting-pipeline.md](scripting-pipeline.md)

## aria-i18n-check

**役割**: 多言語リソース (UI 翻訳 / シナリオ) の整合性チェック。

**ファイル**: `Tools/AriaI18nCheckCommand.cs` (308 lines)

### 使い方

```bash
dotnet run --project src/AriaEngine -- aria-i18n-check \
  [--root .] \
  [--manifest assets/i18n/locales.json] \
  [--scripts <path>...] \
  [--code <path>...] \
  [--verbose|-v]
```

| 引数 | デフォルト | 説明 |
|------|-----------|------|
| `--root` | `.` | ルートディレクトリ |
| `--manifest` | `assets/i18n/locales.json` | manifest ファイル |
| `--scripts` | (なし) | スクリプト入力 (ディレクトリまたは .aria ファイル) |
| `--code` | (なし) | コード入力 (.cs ファイル) |
| `--verbose` / `-v` | (なし) | 詳細出力 |

**注意**: `--scripts` 未指定時は `assets/scripts` をデフォルト。

### 検出ルール

**スクリプト中の参照キー**:
```regex
\b(?:loc_get|tr|loc_format)\s+[^,\r\n]+,\s*"([^"]+)"
```
例: `loc_get menu, "menu.save"` → `menu.save`

**コード中の参照キー**:
```regex
\b(?:T|F|Get|Format)\s*\(\s*"([^"]+)"
```
例: `T("menu.save")` → `menu.save`

**ドッド表記リテラル** (特定プレフィックス):
```regex
"([a-z][a-z0-9_]*(?:\.[a-z][a-z0-9_]*)+)"
```
プレフィックス: `backlog.`, `common.`, `confirm.`, `extra.`, `gallery.`, `menu.`, `save.`, `settings.`

**include ディレクティブ** (シナリオ内):
```regex
^\s*include\s+"([^"]+)"
```

### チェック項目

| 種別 | 検出 |
|------|------|
| `error: missing resource: {lang} {path}` | manifest 定義されたリソース JSON が存在しない |
| `error: invalid resource: {lang} {path} ({ex})` | リソース JSON がパース失敗 |
| `error: missing key: {lang} {key}` | スクリプト/コード参照はあるがリソースに無い |
| `warning: unused key: {lang} {key}` | リソースにあるがどこからも参照されない |
| `error: missing scenario file: {lang} {path}` | `manifest.ScenarioFiles` のロケール別ファイルが無い |
| `error: missing scenario include: {lang} {file} -> {include}` | シナリオ内 include の参照先が無い |

### 終了コード

- `0`: 全チェック pass (警告は許容)
- `1`: エラーあり (`errorCount > 0`)
- `2`: manifest 未発見 / JSON パース失敗

### 動作 (主要部分)

```csharp
public static int Run(string[] args)
{
    // 引数解析
    LocalizationManifest manifest = JsonSerializer.Deserialize(
        File.ReadAllText(manifestFullPath),
        AriaCoreJsonContext.Default.LocalizationManifest) ?? new LocalizationManifest();

    var referencedKeys = CollectReferencedKeys(rootFullPath, scriptInputs);
    foreach (string key in CollectCodeReferencedKeys(rootFullPath, codeInputs))
        referencedKeys.Add(key);

    foreach (string language in manifest.Languages)
    {
        // 各言語ごとに:
        // 1. リソース JSON ロード → availableKeys 構築
        // 2. referencedKeys ⊄ availableKeys → error: missing key
        // 3. availableKeys ⊄ referencedKeys → warning: unused key
        // 4. ValidateScenarioFiles() でシナリオ存在 + include 整合
    }
}
```

### 関連ファイル

- `Tools/AriaI18nCheckCommand.cs` (308 lines)
- `Core/LocalizationManager.cs`
- `Core/LocalizationResource.cs` (LocalizationManifest モデル)
- `Core/AriaCoreJsonContext.cs` (JSON コンテキスト)
- [architecture/text-subsystem.md](text-subsystem.md)

## サブコマンド一覧 (まとめ表)

| コマンド | サブコマンド | 入力 | 出力 | 暗号化 |
|---------|------------|------|------|-------|
| `aria-lint` | (なし) | `.aria` ファイル列 | Issue リスト (stdout) | - |
| `aria-compile` | (なし) | `init.aria` + `main.aria` | `scripts.ariac` | オプション |
| `aria-pack` | `build` / `diff` / `apply` | `assets/` または `.pak` | `.pak` / `.patch` | オプション |
| `aria-doc` | (なし) | `.aria` ファイル | JSON ドキュメント | - |
| `aria-format` | (なし) | `.aria` ファイル | 整形済みテキスト (stdout or 上書き) | - |
| `aria-save` | `list` / `info` / `validate` / `migrate` | saves/ ディレクトリ | 検査結果 (stdout) | - |
| `aria-flow-check` | (なし, `--execute` フラグ) | `main.aria` | フロー問題リスト | - |
| `aria-i18n-check` | (なし) | `locales.json` + 翻訳 + シナリオ | 整合性問題リスト | - |

## CI 統合例

`.github/workflows/aria-lint.yml`:

```yaml
name: aria-lint
on: [push, pull_request]
jobs:
  lint:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: 8.0.x
      - run: dotnet build src/AriaEngine
      - name: aria-lint
        run: |
          dotnet run --project src/AriaEngine -- aria-lint assets/scripts/*.aria
      - name: aria-i18n-check
        run: |
          dotnet run --project src/AriaEngine -- aria-i18n-check
      - name: aria-flow-check (--execute)
        run: |
          dotnet run --project src/AriaEngine -- aria-flow-check --execute
```

## 拡張ガイド

### 新しい CLI コマンドを追加する

1. `Tools/Aria<Name>Command.cs` を `public static class` で作成
2. `public static int Run(string[] args)` を実装
3. 終了コードを統一 (0=成功, 1=引数, 2=IO, 3=状態, 4=例外)
4. `--help` / `-h` をサポート
5. エラーは `Console.Error.WriteLine` で出力
6. 標準出力はパイプ可能に (機械可読)
7. 既存のコマンドヘルパー (引数解析ループ) を参考にする

### 新しいルールを aria-lint に追加する

1. `LintIssue` モデルを生成するヘルパーを `AriaLintCommand.cs` に追加
2. `LintFile` の主要ループから呼び出し
3. `Rule` 文字列に新コード (例: `E013`) を設定
4. 対応するテストを `src/AriaEngine.Tests/` に追加

### 新しい aria-pack サブコマンドを追加する

1. `AriaPackCommand.Run` の switch に case を追加
2. 必要に応じて `Packaging/` 配下に新機能追加
3. `PrintUsage` に使用方法追記
4. 終了コードを `AriaPackCommand.Run` の try-catch で適切に処理

## 関連ドキュメント

- [アーキテクチャ概要](overview.md) — 全体構成
- [Platform](platform.md) — クロスプラットフォーム抽象化
- [Scripting Pipeline](scripting-pipeline.md) — スクリプトパイプライン
- [Text Subsystem](text-subsystem.md) — 多言語サブシステム
- [VM](vm.md), [Parser](parser.md), [Rendering](rendering.md)
- [how-to-guides/](../how-to-guides/) — リリースビルド / デバッグ使用方法
