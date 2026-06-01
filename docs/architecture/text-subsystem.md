# テキスト / 多言語サブシステム

このドキュメントでは、AriaEngineの **多言語 (i18n) サブシステム** について詳しく説明します。ロケール管理、翻訳リソース、シナリオローカライゼーション、整合性チェックを扱います。

## 概要

AriaEngineは **ロケール対応** を第一級サポートしています。アプリケーション文字列・シナリオ・スクリプト内 include を言語別に切替可能で、CI で整合性を自動検証できます。

### サポートロケール

| コード | 言語 | フォント |
|-------|------|---------|
| `ja-JP` | 日本語 (デフォルト) | `NotoSansJP-Regular.ttf` |
| `en-US` | 英語 (US) | `NotoSansJP-Regular.ttf` (CJK 共有) |
| `zh-CN` | 中国語 (簡体) | `NotoSansJP-Regular.ttf` |
| `zh-TW` | 中国語 (繁体) | `NotoSansJP-Regular.ttf` |

### ディレクトリ構成

```
src/AriaEngine/
├── Core/
│   ├── LocalizationManager.cs     # 127 lines
│   ├── LocalizationResource.cs    # 14 lines (LocalizationManifest モデル)
│   └── AriaCoreJsonContext.cs     # 93 lines (JSON コンテキスト)
├── Tools/
│   └── AriaI18nCheckCommand.cs    # 308 lines
└── assets/
    ├── i18n/
    │   ├── locales.json           # 35 lines (manifest)
    │   ├── ui.ja-JP.json          # 85 lines (日本語 UI 翻訳)
    │   ├── ui.en-US.json          # 85 lines
    │   ├── ui.zh-CN.json          # 85 lines
    │   └── ui.zh-TW.json          # 85 lines
    └── scripts/
        └── scenario/              # ロケール別シナリオ
            ├── ja-JP/             # (シンボリック / 親シナリオの include 経由)
            ├── en-US/
            │   ├── scenario_01.aria
            │   ├── scenario_02.aria
            │   └── ...scenario_08.aria
            ├── zh-CN/
            └── zh-TW/
```

## LocalizationManifest — manifest モデル

**役割**: 利用可能ロケール、リソースファイル、シナリオファイル、フォント、日付フォーマットを定義する単一ファイル。

**ファイル**: `Core/LocalizationResource.cs` (14 lines)

```csharp
public sealed class LocalizationManifest
{
    public string DefaultLanguage { get; set; } = "ja-JP";
    public string FallbackLanguage { get; set; } = "ja-JP";
    public List<string> Languages { get; set; } = new();
    public List<string> Resources { get; set; } = new();
    public Dictionary<string, string> Fonts { get; set; }
        = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> DateFormat { get; set; }
        = new(StringComparer.OrdinalIgnoreCase);
    public string ScenarioRoot { get; set; } = "";
    public List<string> ScenarioFiles { get; set; } = new();
    public Dictionary<string, string> ScenarioStatus { get; set; }
        = new(StringComparer.OrdinalIgnoreCase);
}
```

### 実例 (`assets/i18n/locales.json`)

```json
{
  "defaultLanguage": "ja-JP",
  "fallbackLanguage": "ja-JP",
  "languages": ["ja-JP", "en-US", "zh-CN", "zh-TW"],
  "resources": ["ui"],
  "dateFormat": {
    "ja-JP": "yyyy/MM/dd HH:mm",
    "en-US": "MM/dd/yyyy HH:mm",
    "zh-CN": "yyyy/MM/dd HH:mm",
    "zh-TW": "yyyy/MM/dd HH:mm"
  },
  "fonts": {
    "ja-JP": "assets/fonts/NotoSansJP-Regular.ttf",
    "en-US": "assets/fonts/NotoSansJP-Regular.ttf",
    "zh-CN": "assets/fonts/NotoSansJP-Regular.ttf",
    "zh-TW": "assets/fonts/NotoSansJP-Regular.ttf"
  },
  "scenarioRoot": "assets/scripts/scenario",
  "scenarioFiles": [
    "scenario_01.aria", "scenario_02.aria", "scenario_03.aria", "scenario_04.aria",
    "scenario_05.aria", "scenario_06.aria", "scenario_07.aria", "scenario_08.aria"
  ],
  "scenarioStatus": {
    "ja-JP": "source",
    "en-US": "pending-translation",
    "zh-CN": "pending-translation",
    "zh-TW": "pending-translation"
  }
}
```

### フィールド説明

| フィールド | 型 | 説明 |
|-----------|---|------|
| `DefaultLanguage` | string | 起動時のロケール (`ja-JP`) |
| `FallbackLanguage` | string | キー欠落時のフォールバックロケール |
| `Languages` | string[] | 利用可能ロケール (順序 = 優先度) |
| `Resources` | string[] | 翻訳リソースのベース名 (`["ui"]` → `ui.<lang>.json`) |
| `Fonts` | dict | ロケール別フォントパス (フォールバックは `EngineSettings.FontPath`) |
| `DateFormat` | dict | ロケール別日付フォーマット文字列 (`string.Format` 互換) |
| `ScenarioRoot` | string | シナリオディレクトリのルート |
| `ScenarioFiles` | string[] | 翻訳対象のシナリオファイル名 (拡張子含む) |
| `ScenarioStatus` | dict | ロケール別シナリオ状態 (`source` / `pending-translation` 等) |

## LocalizationManager — 多言語管理

**役割**: アプリケーション文字列のロケール別管理、翻訳検索、フォールバック処理、動的ロケール切替。

**ファイル**: `Core/LocalizationManager.cs` (127 lines)

### API

```csharp
public sealed class LocalizationManager
{
    public static LocalizationManager Empty { get; }
    public LocalizationManifest Manifest { get; }
    public string CurrentLanguage { get; private set; }
    public string FallbackLanguage => Manifest.FallbackLanguage;

    public static LocalizationManager Load(IAssetProvider provider, string manifestPath);
    public void SetLanguage(string language);
    public string Get(string key);
    public string Format(string key, params object[] args);
    public string GetDateFormat();
    public IEnumerable<string> EnumerateTextForGlyphs();
    public IReadOnlyList<string> GetAvailableLanguages();
    public string? GetFontForLanguage(string language);
}
```

### ロード処理

```csharp
public static LocalizationManager Load(IAssetProvider provider, string manifestPath)
{
    var manifest = JsonSerializer.Deserialize(
        provider.ReadAllText(manifestPath),
        AriaCoreJsonContext.Default.LocalizationManifest) ?? new LocalizationManifest();
    if (string.IsNullOrWhiteSpace(manifest.FallbackLanguage))
    {
        manifest.FallbackLanguage = manifest.DefaultLanguage;
    }

    string root = Path.GetDirectoryName(manifestPath)?.Replace('\\', '/') ?? "";
    var resources = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

    foreach (string language in manifest.Languages.DefaultIfEmpty(manifest.DefaultLanguage))
    {
        if (string.IsNullOrWhiteSpace(language)) continue;
        var merged = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string resource in manifest.Resources)
        {
            string path = $"{root}/{resource}.{language}.json";
            if (!provider.Exists(path)) continue;
            var table = JsonSerializer.Deserialize(
                provider.ReadAllText(path),
                AriaCoreJsonContext.Default.DictionaryStringString);
            if (table == null) continue;
            foreach (var pair in table)
            {
                merged[pair.Key] = pair.Value;
            }
        }
        resources[language] = merged;
    }

    return new LocalizationManager(manifest, resources);
}
```

**ポイント**:
- `IAssetProvider` 経由でロード (Native: ファイルシステム / Web: `PreloadedWebAssetProvider`)
- `DefaultIfEmpty(DefaultLanguage)` で `Languages` 空の場合のフォールバック
- 複数リソースファイルのマージ (`resources=["ui", "extra"]` 等) — 後に書いたリソースが優先
- パスは `root` からの相対 (manifest 自身と同階層を期待)

### Get (キー検索)

```csharp
public string Get(string key)
{
    if (string.IsNullOrWhiteSpace(key)) return "";
    if (_resources.TryGetValue(CurrentLanguage, out var current) && current.TryGetValue(key, out string? value))
        return value;
    if (_resources.TryGetValue(Manifest.FallbackLanguage, out var fallback) && fallback.TryGetValue(key, out value))
        return value;
    return key;
}
```

**検索順序**:
1. 現在のロケール
2. フォールバックロケール
3. キーそのまま返却 (デバッグ時の可視化)

### Format (プレースホルダ書式化)

```csharp
public string Format(string key, params object[] args)
{
    string template = Get(key);
    if (args.Length == 0) return template;

    try
    {
        return string.Format(CultureInfo.InvariantCulture, template, args);
    }
    catch (FormatException)
    {
        return template;
    }
}
```

**使用例**:
```json
"confirm.save_slot": "スロット{0:00}を上書きしますか？"
```
```csharp
manager.Format("confirm.save_slot", 3);  // → "スロット03を上書きしますか？"
```

`FormatException` 発生時はテンプレートをそのまま返す (ログ汚染を避ける)。

### SetLanguage (動的切替)

```csharp
public void SetLanguage(string language)
{
    if (string.IsNullOrWhiteSpace(language)) return;
    CurrentLanguage = _resources.ContainsKey(language) || Manifest.Languages.Contains(language)
        ? language
        : Manifest.FallbackLanguage;
}
```

**使用例** (Web メニューからの切替):
```csharp
if (key == "F2")  // 言語切替キー
{
    string newLang = _vm.Localization.CurrentLanguage == "ja-JP" ? "en-US" : "ja-JP";
    _vm.Localization.SetLanguage(newLang);
    _vm.SyncLocalizationRuntimeState();
}
```

### GetDateFormat

```csharp
public string GetDateFormat()
{
    if (Manifest.DateFormat.TryGetValue(CurrentLanguage, out string? format) &&
        !string.IsNullOrWhiteSpace(format))
        return format;

    if (Manifest.DateFormat.TryGetValue(Manifest.FallbackLanguage, out format) &&
        !string.IsNullOrWhiteSpace(format))
        return format;

    return "yyyy/MM/dd HH:mm";
}
```

**使用例**:
```csharp
string dateText = DateTime.Now.ToString(
    _vm.Localization.GetDateFormat(),
    CultureInfo.InvariantCulture);
```

### EnumerateTextForGlyphs

```csharp
public IEnumerable<string> EnumerateTextForGlyphs()
{
    return _resources.Values.SelectMany(table => table.Values);
}
```

**用途**: 起動時に全翻訳文字列を列挙 → フォントロード時に必要な文字セット (`char[]`) を抽出。
**使用例**:
```csharp
var glyphChars = string.Concat(manager.EnumerateTextForGlyphs()).Distinct().ToArray();
var font = Raylib.LoadFontEx(fontPath, 32, glyphChars, glyphChars.Length);
```

### GetFontForLanguage

```csharp
public string? GetFontForLanguage(string language)
{
    return Manifest.Fonts.TryGetValue(language, out string? font) ? font : null;
}
```

**用途**: 言語別フォント指定。Web ランタイム (`BrowserFontLoader`) が使用。

```csharp
// AriaEngine.Web/Rendering/BrowserFontLoader.cs
public static BrowserFontFace Resolve(LocalizationManager localization, string fallbackFontPath)
{
    string? localeFont = localization.GetFontForLanguage(localization.CurrentLanguage);
    string source = NormalizeAssetUrl(string.IsNullOrWhiteSpace(localeFont) ? fallbackFontPath : localeFont);
    return new BrowserFontFace("AriaRuntime", source);
}
```

### Empty インスタンス

```csharp
public static LocalizationManager Empty { get; } = new(
    new LocalizationManifest(),
    new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase));
```

**用途**: テスト用 / 初期化前の代替 / 翻訳未設定の作品。

## AriaCoreJsonContext — JSON コンテキスト

**役割**: `LocalizationManager` のロード/セーブで使う System.Text.Json 設定。

**ファイル**: `Core/AriaCoreJsonContext.cs` (93 lines)

`AriaCoreJsonContext` partial class には以下の型が登録:

```csharp
[JsonSourceGenerationOptions(WriteIndented = false, PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(LocalizationManifest))]    // ← ここ
[JsonSerializable(typeof(AppConfig))]
[JsonSerializable(typeof(PersistentGameData))]
[JsonSerializable(typeof(ChapterData))]
[JsonSerializable(typeof(ChapterInfo))]
[JsonSerializable(typeof(CharacterData))]
[JsonSerializable(typeof(CharacterInfo))]
[JsonSerializable(typeof(PakManifest))]
[JsonSerializable(typeof(PakManifestEntry))]
[JsonSerializable(typeof(PakPatchManifest))]
[JsonSerializable(typeof(PakPatchEntry))]
[JsonSerializable(typeof(Dictionary<string, string>))]   // ← 翻訳テーブル
[JsonSerializable(typeof(Dictionary<string, int>))]
[JsonSerializable(typeof(Dictionary<string, bool>))]
[JsonSerializable(typeof(List<string>))]
internal sealed partial class AriaCoreJsonContext : JsonSerializerContext
{
}
```

**ポイント**:
- `PropertyNameCaseInsensitive = true` で大文字小文字無視
- `WriteIndented = false` (バイナリサイズ削減)

## VM 統合

### vm.Localization プロパティ

`Core/GameState.cs` (の `Localization` プロパティ) に `LocalizationManager` インスタンスを保持。

```csharp
// 起動時に設定
vm.Localization = LocalizationManager.Load(provider, "assets/i18n/locales.json");
```

### vm.SyncLocalizationRuntimeState

VM 内部状態 (UI レンダリング、テキストバッファ等) を現在のロケールに同期。

**呼び出しタイミング**:
- 起動時 (Boot)
- ロケール切替後 (`SetLanguage` 直後)

```csharp
// AriaEngine.Web/Runtime/WebRuntimeHost.cs
if (provider.Exists("assets/i18n/locales.json"))
{
    vm.Localization = LocalizationManager.Load(provider, "assets/i18n/locales.json");
    vm.SyncLocalizationRuntimeState();
}
```

## シナリオローカライゼーション

### 構成

ロケール別シナリオディレクトリ:

```
assets/scripts/scenario/
├── ja-JP/                        # ソースロケール (日本語)
├── en-US/
│   ├── scenario_01.aria
│   ├── scenario_02.aria
│   └── ...scenario_08.aria
├── zh-CN/
└── zh-TW/
```

### shim パターン

翻訳未完了のロケールでは、**親 (`ja-JP/`) のシナリオを include する shim** を配置:

```aria
; en-US pending translation shim. Uses ja-JP source until approved localization is authored.
include "../../scenario_01.aria"
```

**動作**:
- `ScenarioStatus: "pending-translation"` のロケールでは shim を使う
- 翻訳完了後は実翻訳シナリオに置き換え、`ScenarioStatus: "source"` に変更
- `aria-i18n-check` がシナリオ存在と include 整合を自動検証

### ロケール別シナリオの解決

```csharp
// LocalizationManifest 経由
string scenarioRoot = manifest.ScenarioRoot;          // "assets/scripts/scenario"
string[] scenarioFiles = manifest.ScenarioFiles;       // ["scenario_01.aria", ...]
string language = "en-US";
string scenarioPath = $"{scenarioRoot}/{language}/{scenarioFiles[0]}";
// → "assets/scripts/scenario/en-US/scenario_01.aria"
```

## 翻訳リソースの実例

### ui.ja-JP.json (85 keys)

主要キー:
- `menu.*` — Save/Load/Backlog/Skip/Settings 等のラベルと説明 (10 keys)
- `common.*` — ON/OFF (2 keys)
- `confirm.*` — 確認ダイアログ (タイトル、save/load slot メッセージ、yes/no/ok/back)
- `save.*` — セーブ画面のタイトル/サブタイトル/ヒント/ステータス
- `backlog.*` — バックログの検索/empty/no_matches/hint
- `settings.*` — CONFIG 画面のタイトル/カテゴリ/ヒント/保存通知
- `gallery.*` — ギャラリー empty/hint
- `extra.*` — EXTRA 画面 (タイトル/サブタイトル/既読率/CG 解放)
- `demo_end.*` — 体験版エンディング (タイトル/サブタイトル/本文/アクション)
- `promo.*` — 共有/プロモ (シェアテキスト/URL/ハッシュタグ/Steam/公式サイト/X)

**プレースホルダ例**:
```json
"confirm.save_slot": "スロット{0:00}を上書きしますか？"
"confirm.load_slot": "スロット{0:00}をロードしますか？"
```

これらは `manager.Format("confirm.save_slot", slot)` で `スロット03を上書き` のような文字列になる。

### 命名規則

| プレフィックス | 用途 |
|--------------|------|
| `menu.*` | メニュー項目ラベル |
| `menu.desc.*` | メニュー項目説明 |
| `menu.hint.*` | メニュー操作ヒント |
| `common.*` | 共通 (ON/OFF) |
| `confirm.*` | 確認ダイアログ |
| `save.*` | セーブ画面 |
| `backlog.*` | バックログ画面 |
| `settings.*` | 設定画面 |
| `gallery.*` | ギャラリー画面 |
| `extra.*` | EXTRA 画面 |
| `demo_end.*` | 体験版エンド |
| `promo.*` | 共有・プロモ |

## aria-i18n-check — 整合性検証

**役割**: 翻訳リソースの整合性 / シナリオファイル / include 解決を静的解析。

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

### 検出パターン

**スクリプト中のキー参照**:
```regex
\b(?:loc_get|tr|loc_format)\s+[^,\r\n]+,\s*"([^"]+)"
```

例:
```aria
loc_get menu, "menu.save"           ; → "menu.save"
tr backlog, "backlog.empty"         ; → "backlog.empty"
loc_format confirm, "confirm.save_slot", 3  ; → "confirm.save_slot"
```

**コード中のキー参照**:
```regex
\b(?:T|F|Get|Format)\s*\(\s*"([^"]+)"
```

例:
```csharp
T("menu.save")           // → "menu.save"
Format("confirm.save_slot", slot)  // → "confirm.save_slot"
```

**ドッド表記リテラル** (特定プレフィックス):
```regex
"([a-z][a-z0-9_]*(?:\.[a-z][a-z0-9_]*)+)"
```

**対象プレフィックス**:
```csharp
private static readonly string[] ResourceKeyPrefixes =
{
    "backlog.", "common.", "confirm.", "extra.",
    "gallery.", "menu.", "save.", "settings."
};
```

**include ディレクティブ** (シナリオ内):
```regex
^\s*include\s+"([^"]+)"
```

### 検出ルール

| 種別 | メッセージ | 重要度 |
|------|-----------|-------|
| ファイル欠落 | `error: missing resource: {lang} {path}` | error |
| JSON 不正 | `error: invalid resource: {lang} {path} ({ex})` | error |
| キー欠落 | `error: missing key: {lang} {key}` | error |
| 未使用キー | `warning: unused key: {lang} {key}` | warning |
| シナリオ欠落 | `error: missing scenario file: {lang} {path}` | error |
| include 欠落 | `error: missing scenario include: {lang} {file} -> {include}` | error |

### 終了コード

- `0`: 全 pass
- `1`: エラーあり
- `2`: manifest 未発見 / JSON パース失敗

### 動作フロー

```
1. manifest (locales.json) ロード
   ↓
2. 参照キー収集
   ├─ スクリプト (.aria ファイル列挙 → LocalizationKeyPattern マッチ)
   └─ コード (.cs ファイル列挙 → CodeLocalizationKeyPattern + DottedStringLiteralPattern マッチ)
   ↓
3. 各言語ごとに:
   ├─ 翻訳リソース JSON ロード → availableKeys 構築
   ├─ referencedKeys ⊄ availableKeys → error: missing key
   ├─ availableKeys ⊄ referencedKeys → warning: unused key
   └─ ValidateScenarioFiles(manifest.ScenarioRoot, manifest.ScenarioFiles):
       ├─ {lang}/{file} 存在チェック
       └─ シナリオ内 include 解決チェック
   ↓
4. Issue レポート + 終了コード返却
```

### サンプル出力

```
error: missing key: en-US menu.new_action
error: missing scenario file: zh-TW assets/scripts/scenario/zh-TW/scenario_05.aria
warning: unused key: ja-JP menu.deprecated
aria-i18n-check failed: 2 error(s), 1 warning(s)
```

## 設計上のポイント

### 不変条件

- `LocalizationManager` は **immutable** (生成後のリソース変更不可)
- ロケール切替は `SetLanguage` のみ
- 翻訳リソースは **case-insensitive** (`OrdinalIgnoreCase`)

### スレッドセーフ

- `LocalizationManager` は不変なので **読み取りは安全**
- ただし `SetLanguage` は書き込みを伴うため、複数スレッドから呼ばない前提

### メモリ効率

- リソースを `_resources[lang][key] = value` のネスト辞書で保持
- 言語ごとに翻訳テーブル全体をメモリにロード (高速検索)
- 起動時のメモリ使用量 = Σ(全言語の翻訳 JSON サイズ)

### フォールバック戦略

| 状況 | 返却値 |
|------|-------|
| 現在の言語にキーあり | その値 |
| 現在の言語にキーなし、`FallbackLanguage` にあり | フォールバック値 |
| どちらにもない | キーそのまま |
| `key` が null/空 | `""` |
| `args.Length == 0` | テンプレートそのまま |
| `string.Format` 失敗 | テンプレートそのまま |

## スクリプトからの利用

### 直接呼び出し (現時点)

```aria
*chapter_1
    ; 翻訳キー → StringRegister 経由で text 表示
    $greeting = $chapter_1.greeting
    text $greeting
    wait
```

> 注: 現状は `GameState.StringRegisters["$chapter_1.greeting"]` に事前に翻訳を入れる運用が主。

### 組み込み命令 (将来)

`T(key)` / `loc_get(key)` / `loc_format(key, args)` 等の命令を `OpCode` に追加する計画あり (WIP 領域)。

例:
```aria
text T("chapter_1.greeting")          ; キーから直接翻訳取得
text loc_format("confirm.save_slot", %slot)
```

## 拡張ガイド

### 新しいロケールを追加する

1. `locales.json` の `languages` 配列に新ロケールコードを追加
2. `fonts` にフォントパスを追加 (省略時は `EngineSettings.FontPath` フォールバック)
3. `dateFormat` に日付フォーマットを追加
4. `scenarioStatus` に `source` / `pending-translation` 等のステータスを設定
5. `assets/i18n/ui.<new>.json` を作成
6. `assets/scripts/scenario/<new>/` を作成 (shim ファイル)
7. `aria-i18n-check` で整合性確認

### 新しいリソースを追加する

1. `locales.json` の `resources` 配列に新ベース名を追加 (例: `"dialog"`)
2. `assets/i18n/dialog.<lang>.json` を作成
3. `aria-i18n-check` で整合性確認

### 新しいプレースホルダ書式を使う

`Format(key, args)` は `string.Format(CultureInfo.InvariantCulture, ...)` 互換。
- `{0}` / `{1}` — インデックス
- `{0:00}` — 数値書式 (2 桁ゼロ埋め)
- `{0:yyyy/MM/dd}` — 日付書式

### 新しい整合性チェックを追加する

`AriaI18nCheckCommand.cs` の `Run` メソッドに新チェックを追加:
- 新パターン → `Regex` フィールド追加
- 新検証ロジック → `Run` の foreach 内に追加
- 新エラーコード → `error: ...` プレフィックスで統一

## 関連ドキュメント

- [アーキテクチャ概要](overview.md) — 全体構成
- [Platform](platform.md) — IAssetProvider 抽象
- [Tools](tools.md) — aria-i18n-check CLI 詳細
- [Scripting Pipeline](scripting-pipeline.md) — スクリプトパイプライン
- [VM](vm.md), [Parser](parser.md)
- [reference/scripting/](../reference/scripting/) — 多言語機能の reference
