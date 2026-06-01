# アーキテクチャ概要

このドキュメントでは、AriaEngineの全体アーキテクチャについて説明します。コード寄りの詳細は各サブドキュメントを参照し、ここでは **全体構成・ランタイム構造・コンポーネント関係** の俯瞰を優先します。

## 設計目標

1. **クロスプラットフォーム**: Native (Raylib) + Web (Blazor WASM) の 2 ランタイムで同じゲームロジックを実行
2. **スクリプト駆動**: ゲームロジックは `.aria` スクリプトで記述し、エンジンは薄い実行器
3. **型安全な v2 strict**: `%` (int) / `$` (string) / `@` (sprite) / `&` (flag) の混同をコンパイル時に検出
4. **多言語ファースト**: ロケール別シナリオ + UI 翻訳リソースを構造化
5. **保守性**: 各コンポーネントが明確に分離され、テスト可能
6. **拡張性**: 新しいコマンド・スプライトタイプ・プラットフォームターゲットを追加可能

## エンジンの全体構成

### ディレクトリ構成 (AriaEngine コア)

```
src/AriaEngine/
├── Core/                  # コアシステム
│   ├── VirtualMachine.cs          # 仮想マシン
│   ├── Parser.cs                  # パーサー
│   ├── OpCode.cs                  # オペコード定義
│   ├── CommandRegistry.cs         # コマンド名→OpCode 解決
│   ├── GameState.cs               # ゲーム状態管理
│   ├── Sprite.cs                  # スプライトモデル
│   ├── UiThemeManager.cs          # テーマ管理
│   ├── TextCommandHandler.cs      # Text カテゴリの命令実行
│   ├── LocalizationManager.cs     # 多言語管理
│   ├── LocalizationResource.cs    # LocalizationManifest モデル
│   ├── AriaCoreJsonContext.cs     # System.Text.Json コンテキスト (3 partial class)
│   ├── ErrorReporter.cs           # エラー報告
│   └── Config.cs                  # 設定管理
├── Scripting/             # スクリプト処理パイプライン
│   ├── ScriptPreprocessor.cs      # include 展開 (97 lines)
│   ├── ScriptCompiler.cs          # バンドルコンパイル (72 lines)
│   ├── ScriptLoader.cs            # 起動時ロード (83 lines)
│   ├── CompiledScriptBundle.cs    # バンドルモデル (34 lines)
│   ├── CompiledBundleCodec.cs     # ARIAC1 バイナリ codec (57 lines)
│   └── AriaScriptJsonContext.cs   # Scripting 用 JSON コンテキスト
├── Platform/              # プラットフォーム抽象化
│   ├── PlatformServices.cs        # サービスロケータ (静的)
│   ├── IClock.cs                  # クロック (float NowMilliseconds)
│   ├── IRandomSource.cs           # 乱数 (NextInclusive)
│   ├── IWindowService.cs          # ウィンドウ操作
│   ├── IScreenshotService.cs      # スクリーンショット
│   ├── IBrowserService.cs         # 外部 URL 起動
│   ├── AriaTextureFilter.cs       # テクスチャフィルタ enum
│   ├── RaylibClock.cs             # Raylib 実装: クロック
│   ├── RaylibRandomSource.cs      # Raylib 実装: 乱数
│   ├── RaylibWindowService.cs     # Raylib 実装: ウィンドウ
│   ├── RaylibScreenshotService.cs # Raylib 実装: スクリーンショット
│   └── NativeBrowserService.cs    # ネイティブ実装: ブラウザ
├── Rendering/             # レンダリングシステム
│   ├── SpriteRenderer.cs          # スプライトレンダラー
│   ├── TransitionManager.cs       # トランジション
│   └── TweenManager.cs            # Tween アニメーション
├── Input/                 # 入力システム
│   └── InputHandler.cs            # 入力処理 (押下状態追跡)
├── Audio/                 # オーディオシステム
│   └── AudioManager.cs            # BGM/SE 再生
├── UI/                    # メニューシステム
│   └── MenuSystem.cs              # Save/Load/Title/Config (1078 lines)
├── Tools/                 # CLI ツール群 (8 コマンド)
│   ├── AriaLintCommand.cs         # 静的解析 (1053 lines)
│   ├── AriaCompileCommand.cs      # 暗号化コンパイル
│   ├── AriaPackCommand.cs         # アセットパッキング
│   ├── AriaDocCommand.cs          # ドキュメント生成
│   ├── AriaFormatCommand.cs       # コードフォーマット
│   ├── AriaSaveCommand.cs         # セーブデータ操作
│   ├── AriaFlowCheckCommand.cs    # フロー解析
│   └── AriaI18nCheckCommand.cs    # 多言語キー整合性チェック
├── assets/                # ゲームアセット
│   ├── fonts/                     # フォントファイル
│   ├── bg/                        # 背景画像
│   ├── ch/                        # キャラクター画像
│   ├── scripts/
│   │   ├── main.aria              # メインスクリプト
│   │   ├── scenario_01-06.aria    # シナリオ
│   │   └── scenario/              # ロケール別シナリオ
│   │       ├── en-US/             # 英語シナリオ
│   │       ├── ja-JP/             # 日本語シナリオ
│   │       ├── zh-CN/             # 中国語簡体シナリオ
│   │       └── zh-TW/             # 中国語繁体シナリオ
│   └── i18n/                      # 多言語リソース
│       ├── locales.json           # ロケール定義
│       ├── ui.en-US.json          # 英語 UI 翻訳
│       ├── ui.ja-JP.json          # 日本語 UI 翻訳
│       ├── ui.zh-CN.json          # 中国語簡体 UI 翻訳
│       └── ui.zh-TW.json          # 中国語繁体 UI 翻訳
├── init.aria             # エンジン初期化スクリプト
├── config.json           # ユーザー設定 (自動生成)
└── Program.cs            # エントリーポイント (Native)
```

### ディレクトリ構成 (AriaEngine.Web PWA ターゲット)

```
src/AriaEngine.Web/       # Blazor WebAssembly ターゲット
├── Program.cs                       # Blazor WebAssembly エントリ
├── App.razor                        # ルートコンポーネント (canvas ホスト)
├── _Imports.razor                   # グローバル using
├── AriaEngine.Web.csproj            # プロジェクトファイル
├── Assets/
│   └── PreloadedWebAssetProvider.cs # プリロードアセット提供
├── Input/
│   └── BrowserInputMapper.cs        # ポインタ/キーボード → VM 入力
├── Rendering/
│   ├── BrowserFontLoader.cs         # Web フォント解決
│   ├── BrowserRenderer.cs           # BrowserDrawCommand 生成
│   └── CanvasScaleMapper.cs         # 高 DPI マッピング
├── Runtime/
│   └── WebRuntimeHost.cs            # 起動・初期化・フレーム生成 (618 lines)
├── Storage/
│   ├── BrowserStorageOperation.cs   # ストレージ操作抽象
│   ├── IndexedDbSaveStore.cs        # IndexedDB セーブストア
│   ├── OpfsAssetStore.cs            # Origin Private File System
│   └── SaveExportImport.cs          # セーブエクスポート/インポート
└── wwwroot/
    ├── index.html                   # エントリ HTML
    ├── service-worker.js            # PWA サービスワーカー (開発)
    ├── service-worker.published.js  # PWA サービスワーカー (公開)
    ├── assets/web-text-assets.json  # Web 用プリロードアセット一覧
    ├── css/app.css                  # アプリケーションスタイル
    └── js/aria-web-runtime.js       # ランタイム JS (描画/入力/計測)
```

> **WIP マーカー**: `[WIP]` は未コミット (ユーザ作業中) の領域を示します。CI 上はビルドに含まれないか、含まれていても features フラグで隔離されます。

## ランタイム構成

AriaEngineは2つのランタイムターゲットを持ちます。

### Native (Windows / macOS / Linux) ランタイム

```
┌─────────────────────────────────────┐
│            Program.cs               │  エントリーポイント
│  (Raylib ウィンドウ初期化)         │
└──────────────┬──────────────────────┘
               │
               ▼
┌─────────────────────────────────────┐
│   init.aria 実行 (splash screen)   │
└──────────────┬──────────────────────┘
               │
               ▼
┌─────────────────────────────────────┐
│   main script (assets/scripts/)    │  仮想マシン実行
└──────────────┬──────────────────────┘
               │
       ┌───────┼───────┐
       │       │       │
       ▼       ▼       ▼
   ┌──────┐ ┌──────┐ ┌──────┐
   │  VM  │ │Render│ │Audio │
   │      │ │ +    │ │      │
   │      │ │Input │ │      │
   └──┬───┘ └──┬───┘ └──┬───┘
      │        │        │
      └────┬───┴────┬───┘
           │        │
           ▼        ▼
   ┌────────────────────────┐
   │  PlatformServices       │  サービスロケータ
   │  (Raylib 実装に固定)   │
   └────────┬───────────────┘
            │
            ▼
     ┌──────────┐
     │  Raylib  │  グラフィックス/オーディオ/入力
     └──────────┘
```

### Web (PWA / Blazor WebAssembly) ランタイム

```
┌─────────────────────────────────────┐
│      wwwroot/index.html            │  PWA エントリ
│  (Blazor WebAssembly ロード)       │
└──────────────┬──────────────────────┘
               │
               ▼
┌─────────────────────────────────────┐
│   AriaEngine.Web/Program.cs        │  Blazor App
└──────────────┬──────────────────────┘
               │
               ▼
┌─────────────────────────────────────┐
│   App.razor (Blazor コンポーネント) │
│   - <canvas id="aria-canvas">      │
│   - OnAfterRenderAsync で起動      │
└──────────────┬──────────────────────┘
               │
               ▼
┌─────────────────────────────────────┐
│   WebRuntimeHost.Boot()            │  起動・初期化
│   - PreloadedWebAssetProvider      │  (アセットプリロード)
│   - ScriptLoader (RunMode.Dev)     │
│   - VirtualMachine + SaveManager   │
│   - LocalizationManager.Load       │
│   - init.aria / main.aria 実行     │
└──────────────┬──────────────────────┘
               │
               ▼
┌─────────────────────────────────────┐
│   WebRuntimeFrame                  │  1 フレーム生成
│   - VmState / LogicalWidth/Height  │
│   - BrowserFontFace                │
│   - BrowserDrawCommand[]           │  (Canvas 2D 描画プリミティブ)
└──────────────┬──────────────────────┘
               │
               ▼
┌─────────────────────────────────────┐
│   wwwroot/js/aria-web-runtime.js   │  JS ランタイム
│   - measure() → CSS サイズ取得     │
│   - renderFrame() → Canvas 描画    │
│   - applyStorageOperation()        │
│   - サービスワーカー連携           │
└──────────────┬──────────────────────┘
               │
               ▼
   ┌────────────────────────┐
   │  Browser DOM           │
   │  + Canvas 2D           │
   │  + IndexedDB           │  (セーブ永続化)
   │  + OPFS                │  (アセット)
   │  + Service Worker      │  (オフライン対応)
   └────────────────────────┘
```

### 起動シーケンスの対比

| ステップ | Native | Web |
|---------|--------|-----|
| 1 | `Program.cs` エントリ | `wwwroot/index.html` → `Program.cs` (Blazor) |
| 2 | Raylib ウィンドウ生成 | Blazor WASM 起動 |
| 3 | `init.aria` 解析+実行 | `App.razor` `OnAfterRenderAsync` |
| 4 | `main.aria` 解析+実行 | `WebRuntimeHost.Boot(provider, options)` |
| 5 | メインループ (60 FPS) | `CreateFrame()` → JS `renderFrame()` ループ |
| 6 | `PlatformServices` = Raylib impl | `PlatformServices` = Browser impl に差し替え |
| 7 | セーブ = ファイルシステム | セーブ = IndexedDB (`aria-engine` DB) |

## コンポーネント間の関係

```
┌─────────────────┐
│   Program.cs    │  ← エントリーポイント (Native)
│   Program.cs    │  ← Blazor App (Web)
│   App.razor     │  ← Blazor コンポーネント (Web)
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│ ScriptPre-      │  ← プリプロセッサ
│ processor       │  (include 解決、cycle 検出)
└────────┬────────┘
         │  ExpandedScript (Lines + Dependencies)
         ▼
┌─────────────────┐
│  Parser.cs      │  ← 構文解析
│  (Tokenize →    │  → ParseResult
│   Parse)        │  (Instructions, Labels, Functions, ...)
└────────┬────────┘
         │
   ┌─────┴──────┐
   │            │
   ▼            ▼
┌───────┐  ┌────────────┐
│Script │  │ScriptLoader│  ← ランタイムロード
│Compi- │  │            │  (Dev: parse / Release: bundle lookup)
│ler    │  └─────┬──────┘
│       │        │
│Bundle │        │ ParseResult
│生成   │        │
└───┬───┘        │
    │            │
    ▼            │
┌──────────────┐ │
│CompiledBundle│ │
│Codec         │ │  ← ARIAC1 バイナリ I/O
│(.ariac)      │ │  + オプション暗号化
└──────────────┘ │
                 │
                 ▼
┌─────────────────────────────────┐
│      VirtualMachine             │  ← 実行
│  - Step() で命令実行             │
│  - GameState 更新               │
│  - VmState 遷移管理             │
└────────┬────────────────────────┘
         │
    ┌────┴────────────────┐
    │                     │
    ▼                     ▼
┌─────────┐         ┌──────────┐
│ Sprite  │         │  Audio   │
│Renderer │         │ Manager  │
└────┬────┘         └────┬─────┘
     │                  │
     │  (Web の場合)     │
     ▼                  ▼
┌────────────────────────────┐
│  BrowserRenderer           │  ← Web 用ラスタ化
│  (BrowserDrawCommand[])    │  → aria-web-runtime.js
└────────┬───────────────────┘
         │
         └─────┬─────┘
               │
               ▼
       ┌────────────────┐
       │ Platform        │  ← プラットフォーム抽象化
       │ Services        │  (Native: Raylib / Web: Browser)
       └────────┬───────┘
                │
                ▼
         ┌──────────┐
         │ Raylib/  │  ← グラフィックス/オーディオ
         │ Browser  │
         └──────────┘
```

## 主要クラスの役割

### Core/

#### VirtualMachine (仮想マシン)

**役割**: パース済み命令列を実行する NScripter 互換インタプリタ。

**主な機能**:
- パースされた命令の実行
- プログラムカウンタ (PC) の管理
- コールスタックの管理
- ゲーム状態 (`GameState`) の管理
- 制御フロー（ジャンプ、条件分岐、ループ）
- ハンドラテーブルによるディスパッチ最適化
- 待機状態 (`VmState.WaitingFor*`) の遷移

**関連ファイル**: `Core/VirtualMachine.cs` → 詳細は [vm.md](vm.md)

#### Parser (パーサー)

**役割**: `.aria` スクリプトを `List<Instruction>` + ラベル辞書 + 関数定義に変換。

**主な機能**:
- テキストファイルのトークン化
- 命令の生成 (`OpCode` 解決)
- ラベルの解析と解決
- 構文エラーの検出
- インライン構文の展開 (`Name「Text」` → `textclear` + `text` + 自動 `\` 付与)
- テキスト制御文字 (`\` `@`) の分割
- v2 strict 構文 (`func` / `scope` / `owned` / `readonly` / `mut` / `local` / `global`) の平坦化

**関連ファイル**: `Core/Parser.cs` → 詳細は [parser.md](parser.md)

#### GameState (ゲーム状態)

**役割**: VM から操作される全ての実行時状態を保持する中央コンテナ。

**主な機能**:
- レジスタ管理 (`%0-%9` + 名前付き)
- 文字列レジスタ (`$name`)
- スプライト辞書 (`Dictionary<int, Sprite>`)
- フラグ (`persistent` / `save` / `volatile` の 3 種)
- カウンター
- VM 状態 (`Running` / `WaitingForClick` / `WaitingForButton` / ...)
- テキスト状態 (`CurrentText` / `TextVisible` / 縦位置)
- 多言語状態 (`Localization` プロパティ → `LocalizationManager`)
- オーディオ状態
- UI 状態

**関連ファイル**: `Core/GameState.cs`

#### Sprite (スプライト)

**役割**: Image / Text / Rect の 3 種類を統一的に扱うビジュアル要素。

**主な機能**:
- 位置 / スケール / 回転 / 透明度
- Z オーダー (小さいほど奥)
- 装飾効果 (枠線、影、角丸、グラデーション)
- ボタン化 (`IsButton` + `ButtonFeel` + `IsPressed` ランタイムフラグ)
- ホバーエフェクト
- テキストラップ (幅指定時)

**関連ファイル**: `Core/Sprite.cs`

#### CommandRegistry (コマンドレジストリ)

**役割**: コマンド名 (canonical + alias) → `OpCode` の単一マッピング。

**関連ファイル**: `Core/CommandRegistry.cs`

#### LocalizationManager (多言語管理)

**役割**: アプリケーション文字列をロケール別に管理し、実行時に切替可能にする。

**主な機能**:
- `IAssetProvider` 経由の manifest + リソース JSON ロード (Native + Web 共通)
- キーによる翻訳検索 (`Get(key)`)
- プレースホルダ付き書式化 (`Format(key, args)`)
- 動的ロケール切替 (`SetLanguage(language)`)
- フォールバック (欠落キー → fallback language → キーそのまま)
- 言語別フォント指定 (`GetFontForLanguage`)
- 言語別日付フォーマット (`GetDateFormat`)
- 起動時のグリフ列挙 (`EnumerateTextForGlyphs` → フォントロード用文字セット)

**関連ファイル**:
- `Core/LocalizationManager.cs` (127 lines)
- `Core/LocalizationResource.cs` (`LocalizationManifest` モデル, 14 lines)
- `Core/AriaCoreJsonContext.cs` (System.Text.Json source-gen コンテキスト)

**データモデル** (`LocalizationManifest`):
- `DefaultLanguage` / `FallbackLanguage`
- `Languages` (利用可能ロケールリスト)
- `Resources` (ロケール別 JSON リソースのベース名)
- `Fonts` (ロケール別フォントパス)
- `DateFormat` (ロケール別日付フォーマット)
- `ScenarioRoot` / `ScenarioFiles` / `ScenarioStatus` (ロケール別シナリオ解決)

#### AriaCoreJsonContext (System.Text.Json コンテキスト)

**役割**: NativeAOT 互換の JSON シリアライズ設定。3 つの `partial class` で構成:

- `AriaCoreJsonContext` (コンパクト、設定・永続・パッケージング用)
- `AriaCoreIndentedJsonContext` (インデント、エラーログ・クラッシュ診断用)
- `AriaSaveJsonContext` (セーブデータ、全 State クラス用)

**関連ファイル**: `Core/AriaCoreJsonContext.cs` (93 lines)

### Scripting/

スクリプト処理パイプライン。**Dev モード** は `Parser` への直接入力、**Release モード** は `CompiledScriptBundle` 経由。詳細は [scripting-pipeline.md](scripting-pipeline.md)。

#### ScriptPreprocessor

**役割**: コンパイル前のテキスト前処理。

**主な機能**:
- `include "path.aria"` ディレクティブの解決 (再帰)
- 循環参照検出
- パス正規化 (`\` → `/`、先頭 `/` 除去)
- 結果として `ExpandedScript` (Lines + Dependencies) を返す

**関連ファイル**: `Scripting/ScriptPreprocessor.cs` (97 lines)

#### ScriptCompiler

**役割**: 複数スクリプトを `CompiledScriptBundle` に一括コンパイル。

**主な機能**:
- `init.aria` + `main.aria` を起点に BFS 展開
- 各スクリプトを `ExpandedScript` → `Parser.Parse` → `CompiledScript` に変換
- include は本体に展開済みのため、include ファイルを再コンパイルしない (二重ラベル解決防止)

**関連ファイル**: `Scripting/ScriptCompiler.cs` (72 lines)

#### ScriptLoader

**役割**: 起動時のスクリプトロードを `RunMode` で分岐。

**主な機能**:
- `RunMode.Dev`: `ScriptPreprocessor.ExpandIncludes` → `Parser.Parse`
- `RunMode.Release` + bundle: `CompiledScriptBundle` から `Instructions` を復元
- 起動トレース (`ARIA_STARTUP_TRACE=1` 環境変数で `startup_trace.log` 出力)
- 結果を `ParseResult` として VM に渡す

**関連ファイル**: `Scripting/ScriptLoader.cs` (83 lines)

#### CompiledScriptBundle

**役割**: コンパイル済みバンドルのデータモデル。

**データモデル**:
- `CompiledScriptBundle`: `Version`, `CreatedAtUtc`, `InitPath`, `MainPath`, `Scripts` (dict)
- `CompiledScript`: `Path`, `Instructions`, `Labels`, `Functions`, `Structs`, `Enums`, `OwnedSprites`, `SourceLines`
- `CompiledInstruction`: `Op` (int), `Arguments`, `SourceLine`, `Condition`

**関連ファイル**: `Scripting/CompiledScriptBundle.cs` (34 lines)

#### CompiledBundleCodec

**役割**: `CompiledScriptBundle` ↔ `ARIAC1` バイナリ変換。

**バイナリ形式**:
```
[6 bytes] Magic "ARIAC1"
[1 byte ] Encrypted flag (0/1)
[4 bytes] Payload length (little-endian int)
[? bytes] Payload (JSON, optional AES)
```

**関連ファイル**: `Scripting/CompiledBundleCodec.cs` (57 lines) + `AriaEngine.Packaging.CryptoHelper`

### Platform/

プラットフォーム抽象化レイヤー。Native (Raylib) と Web (Browser) で同じインターフェースを実装する。

#### サービスロケータ

```csharp
public static class PlatformServices
{
    public static IClock Clock { get; set; } = new RaylibClock();
    public static IRandomSource Random { get; set; } = new RaylibRandomSource();
    public static IWindowService Window { get; set; } = new RaylibWindowService();
    public static IScreenshotService Screenshot { get; set; } = new RaylibScreenshotService();
    public static IBrowserService Browser { get; set; } = new NativeBrowserService();
}
```

> Web ターゲットでは起動時に Browser 実装に差し替え。

#### インターフェース

| インターフェース | 役割 | 主要 API |
|---------------|------|---------|
| `IClock` | 高精度時刻 | `float NowMilliseconds` |
| `IRandomSource` | シード可能乱数 | `int NextInclusive(int min, int max)` |
| `IWindowService` | ウィンドウ制御 | `ScreenWidth/Height`, `ToggleFullscreen`, `SetWindowSize`, マルチモニター |
| `IScreenshotService` | スクリーンショット | `byte[]? CaptureThumbnail(int w, int h)` |
| `IBrowserService` | 外部 URL 起動 | `bool OpenExternal(Uri uri)` |

#### Raylib 実装 (Native)

- `RaylibClock`: `Raylib.GetTime() * 1000f`
- `RaylibRandomSource`: `Raylib.GetRandomValue(min, max)`
- `RaylibWindowService`: `Raylib.GetScreenWidth/Height` 等への委譲
- `RaylibScreenshotService`: `LoadImageFromScreen` → リサイズ → 一時 PNG エクスポート → バイト列
- `NativeBrowserService`: `Process.Start(UseShellExecute=true)`

**関連ファイル**: `Platform/*.cs` (12 ファイル) → 詳細は [platform.md](platform.md)

### Rendering/

#### SpriteRenderer (スプライトレンダラー)

**役割**: Raylib への描画指示。

**主な機能**:
- スプライトの Z オーダーソート (`Z` 昇順、同値は `Id`)
- テキスト描画 + 自動ラップ
- フォントロード + キャッシュ
- カラー解析 (16進数 `#RRGGBB` / `#RRGGBBAA`)
- 地震エフェクト (一時オフセット)
- トランジションオーバーレイ
- ボタン押下ビジュアル (スケール + カラー + オフセット)

**関連ファイル**: `Rendering/SpriteRenderer.cs` → 詳細は [rendering.md](rendering.md)

#### TransitionManager (トランジションマネージャー)

**役割**: 画面遷移時のオーバーレイ演出 (Fade / Slide / Scale)。

**関連ファイル**: `Rendering/TransitionManager.cs`

#### TweenManager (Tween マネージャー)

**役割**: スプライトプロパティの補間アニメーション (位置 / 透明度 / スケール / カラー)。

**関連ファイル**: `Rendering/TweenManager.cs`

### Input/

#### InputHandler (入力ハンドラー)

**役割**: マウス / キーボード入力のポーリング → VM 状態反映。

**主な機能**:
- クリック / ボタン押下 / ホバー検出
- 押下状態の追跡 (`_pressedButtonId` フィールドで 1 フレーム限定)
- 右クリックメニュー (F2 / 右クリックで起動)
- デバッグモードトグル (F3)
- 終了リクエスト (ESC)

**関連ファイル**: `Input/InputHandler.cs`

### Audio/

#### AudioManager (オーディオマネージャー)

**役割**: BGM / SE / 動画の再生と管理 (Raylib オーディオ)。

**関連ファイル**: `Audio/AudioManager.cs`

### UI/

#### MenuSystem (メニューシステム)

**役割**: Save / Load / Title / Config のフルスクリーン UI を構築。

**主な機能**:
- セーブスロットのグリッド表示
- スクリーンショット付きサムネイル
- フルスクリーンオーバーレイ
- テーマ適用 (`UiThemeManager`)
- 言語切替
- グリッドレイアウト、ナビゲーション

**関連ファイル**: `UI/MenuSystem.cs` (1078 lines, 規模大)

### Tools/ (CLI ツール群)

`dotnet run --project src/AriaEngine -- <command> [args]` で実行される 8 サブコマンド。

| コマンド | 役割 | 規模 |
|---------|------|-----|
| `aria-lint` | 静的解析 (型・所有権・寿命・未使用変数) | 1053 lines |
| `aria-compile` | スクリプト暗号化コンパイル (`.ariac` 生成) | 62 lines |
| `aria-pack` | アセットパッキング (`.pak` 生成) | 468 lines |
| `aria-doc` | ドキュメント生成 | 191 lines |
| `aria-format` | コードフォーマット | 177 lines |
| `aria-save` | セーブデータ操作 | 242 lines |
| `aria-flow-check` | フロー解析 | 324 lines |
| `aria-i18n-check` | 多言語キー整合性チェック | 308 lines |

**関連ファイル**: `Tools/*.cs` (8 ファイル) → 詳細は [tools.md](tools.md)

### AriaEngine.Web/ (Web ランタイム)

Blazor WebAssembly で Native と同じ `Core` / `Scripting` / `Rendering` / `Platform` を共有する。

#### Program.cs (Web エントリ)

標準 Blazor WASM テンプレート。`#app` に `App.razor` をマウント。

**関連ファイル**: `AriaEngine.Web/Program.cs` (17 lines)

#### App.razor (Blazor コンポーネント)

**役割**: `<canvas>` ホスト + JSInterop で Web ランタイムと接続。

**ライフサイクル**:
1. `OnAfterRenderAsync(firstRender)`: 
   - `WebRuntimeHost.Boot(PreloadedWebAssetProvider, options)` でランタイム起動
   - `ariaWebRuntime.boot(canvas, dotnetRef)` で JS 側初期化
2. `[JSInvokable] HandlePointerDown(x, y, w, h, button)`:
   - button=0 → `_host.HandlePointerPress` (左クリック)
   - button=2 → `_host.HandleContextMenu` (右クリック)
3. `RenderFrameAsync`:
   - `ariaWebRuntime.measure(canvas)` → CSS サイズ取得
   - `_host.CreateFrame(width, height)` → `WebRuntimeFrame` 取得
   - `ariaWebRuntime.renderFrame(canvas, frame)` → Canvas 描画
4. `ProcessStorageOperationsAsync`:
   - `_host.DrainStorageOperations()` を JS に適用
   - Read 結果は `_host.ApplyLoadedStorage` で VM に反映

**関連ファイル**: `AriaEngine.Web/App.razor` (102 lines)

#### WebRuntimeHost

**役割**: ランタイム起動、入力処理、フレーム生成、ストレージ操作管理。

**起動シーケンス** (`Boot`):
```
IAssetProvider 受信
  ↓
ErrorReporter / Parser / ScriptLoader(RunMode.Dev) 生成
  ↓
ConfigManager + SaveManager (usePortableJsonXxx = true)
  ↓
VirtualMachine 生成
  ↓
locales.json 存在時 → LocalizationManager.Load(provider, ...) + SyncLocalizationRuntimeState
  ↓
LoadInitAndMain (init.aria / main.aria 実行)
  ↓
WebRuntimeHost 返却
```

**1 フレーム生成** (`CreateFrame`):
```
RunUntilInteractive (VM が WaitingFor* になるまで step)
  ↓
CanvasScaleMapper.Create(cssWidth, cssHeight) で座標系マッピング
  ↓
BrowserRenderer(mapper) で全スプライト → BrowserDrawCommand[] に変換
  ↓
クリックカーソルコマンド追加
  ↓
WebRuntimeFrame(VmState, LogicalWidth, Height, Font, DrawCommands) 返却
```

**関連ファイル**: `AriaEngine.Web/Runtime/WebRuntimeHost.cs` (618 lines)

#### BrowserRenderer

**役割**: `Sprite` を `BrowserDrawCommand[]` (Canvas 2D 命令) に変換。

**描画プリミティブ**: `Image` / `Text` / `Rect` / `Triangle`

**座標系**: 2 種類を保持 — `CssX/Y/Width/Height` (CSS ピクセル) と `LogicalX/Y/Width/Height` (論理座標)。`CanvasScaleMapper` で変換。

**関連ファイル**: `AriaEngine.Web/Rendering/BrowserRenderer.cs` (127 lines)

#### IndexedDbSaveStore

**役割**: IndexedDB へのセーブ I/O を `BrowserStorageOperation` として抽象化。

**ストア構成**:
- DB: `aria-engine`
- Object Store: `saves` (key: `save:000` 〜 `save:999`)
- Object Store: `settings` (key: `settings:<name>`)

**関連ファイル**: `AriaEngine.Web/Storage/IndexedDbSaveStore.cs` (38 lines)

#### その他の Web ストレージ

- `OpfsAssetStore`: Origin Private File System (アセットキャッシュ)
- `SaveExportImport`: セーブのエクスポート/インポート (JSON)
- `BrowserStorageOperation`: ストレージ操作の統一データモデル (Area / Kind / DB / Store / Key / Payload)

#### aria-web-runtime.js (JS ランタイム)

**役割**: Canvas 2D 描画 + 入力イベント + ストレージ操作の JS 実装。

**主要 API**:
- `ariaWebRuntime.boot(canvas, dotnetRef)`: サービスワーカー登録 + イベントリスナー設定
- `ariaWebRuntime.measure(canvas)`: `CanvasSize { Width, Height }` 返却
- `ariaWebRuntime.renderFrame(canvas, frame)`: `BrowserDrawCommand[]` を Canvas 2D コンテキストで描画
- `ariaWebRuntime.applyStorageOperation(op)`: IndexedDB / OPFS 操作を実行し結果を返す
- `ariaWebRuntime.showError(canvas, message)`: フォールバックエラーメッセージ

**関連ファイル**: `AriaEngine.Web/wwwroot/js/aria-web-runtime.js` (342 lines)

## データフロー

### スクリプト実行の全体フロー (Native + Web 共通)

```
1. スクリプトファイル (.aria)
   ├─ [Dev] assets/scripts/main.aria から直接
   └─ [Release] scripts/scripts.ariac (暗号化バンドル) から
   ↓
2. ScriptPreprocessor.ExpandIncludes (前処理)
   - include "path.aria" 解決
   - 循環参照検出
   - ExpandedScript (Lines + Dependencies) 返却
   ↓
3. Parser.Parse (構文解析)
   - ParseResult (Instructions, Labels, Functions, ...) 返却
   ↓
4. [Release のみ] ScriptCompiler.CompileBundle
   - init.aria + main.aria を BFS 展開
   - 各スクリプトを CompiledScript に変換
   - CompiledScriptBundle としてメモリ保持
   ↓
5. [Release のみ] CompiledBundleCodec.Save
   - ARIAC1 バイナリに書き出し (オプション AES 暗号化)
   ↓
6. [Release のみ] ScriptLoader が CompiledScriptBundle から
   - CompiledScript を Instructions に復元
   ↓
7. VirtualMachine.Step (実行)
   - 命令を順次実行
   - GameState を更新
   ↓
8. Rendering
   - Native: SpriteRenderer が GameState.Sprites を Raylib で描画
   - Web: BrowserRenderer が BrowserDrawCommand[] を生成 → JS が Canvas 描画
   ↓
9. Audio
   - AudioManager が BGM/SE を再生
   ↓
10. 画面表示
    - Native: Raylib ウィンドウ
    - Web: HTML5 Canvas
```

### メインゲームループ (Native)

```
while (!WindowShouldClose):
    1. Update(deltaTime)
       - VM の状態更新 (Running 状態なら命令を実行)
       - 入力処理 (InputHandler)
       - アニメーション更新 (TweenManager)
       - オーディオ更新 (AudioManager)
       - トランジション更新 (TransitionManager)
       - 多言語更新 (必要に応じて LocalizationManager.SetLanguage)

    2. Step VM (Running 状態の場合)
       - 次の命令を実行
       - 状態遷移を管理

    3. Render()
       - 画面クリア
       - スプライト描画 (Z オーダー順)
       - トランジション描画
       - デバッグ情報描画 (F3)
```

### メインループ (Web)

`App.razor` が Blazor イベントループで駆動:

```
[OnAfterRenderAsync]
  WebRuntimeHost.Boot(...) → init.aria / main.aria ロード → 最初の RenderFrameAsync

[ユーザー操作]
  HandlePointerDown (JSInvokable) → WebRuntimeHost.HandlePointerPress/Menu
  → 必要に応じて ProcessStorageOperationsAsync (IndexedDB)
  → RenderFrameAsync

[RenderFrameAsync]
  ariaWebRuntime.measure(canvas) → CSS サイズ取得
  WebRuntimeHost.CreateFrame(w, h) → WebRuntimeFrame 取得
  ariaWebRuntime.renderFrame(canvas, frame) → Canvas 2D 描画
```

## スクリプト言語の処理

### パース処理

1. **ファイル読み込み**: `.aria` ファイルを行単位で読み込み
2. **プリプロセッサ**: `include "path.aria"` 解決
3. **コメント除去**: `;` で始まる行をコメントとして除去
4. **トークン化**: 行をトークンに分割
5. **命令生成**: トークンを `Instruction` オブジェクトに変換
6. **ラベル解決**: ラベルのアドレスを解決

### 実行処理

1. **命令フェッチ**: プログラムカウンタから命令を取得
2. **オペコード解析**: 命令の種類を特定
3. **引数評価**: 引数を評価
4. **命令実行**: オペコードに対応する処理を実行
5. **状態更新**: ゲーム状態を更新
6. **プログラムカウンタ更新**: 次の命令へ

## プラットフォーム抽象化

### 目的

Native (Raylib) と Web (Browser DOM/Canvas) で同じゲームロジックを実行するため、エンジン内部のプラットフォーム依存呼び出しを `Platform/` 配下のインターフェースに抽象化する。

### サービスロケータ

```csharp
public static class PlatformServices
{
    public static IClock Clock { get; set; } = new RaylibClock();
    public static IRandomSource Random { get; set; } = new RaylibRandomSource();
    public static IWindowService Window { get; set; } = new RaylibWindowService();
    public static IScreenshotService Screenshot { get; set; } = new RaylibScreenshotService();
    public static IBrowserService Browser { get; set; } = new NativeBrowserService();
}
```

### 実装の差し替え

- **Native ビルド**: Raylib 実装がデフォルトのまま
- **Web ビルド**: `WebRuntimeHost.Boot` の初期化中に Browser 実装に差し替え

### IAssetProvider

両ランタイムでアセット読み込みを統一するインターフェース:

- **Native**: ファイルシステム (`File.OpenRead` 等)
- **Web**: プリロード済み辞書 (`PreloadedWebAssetProvider` が `web-text-assets.json` の `preload` リストに従って全アセットを `Dictionary<string, string>` 化)

これにより `LocalizationManager.Load` / `ScriptPreprocessor.ExpandIncludes` / `ScriptLoader` 等はプラットフォームを意識しない。

## 国際化 (i18n) サブシステム

### アセット構成

```
assets/i18n/
├── locales.json         # LocalizationManifest
├── ui.en-US.json        # 英語 UI 翻訳
├── ui.ja-JP.json        # 日本語 UI 翻訳
├── ui.zh-CN.json        # 中国語簡体 UI 翻訳
└── ui.zh-TW.json        # 中国語繁体 UI 翻訳
```

### フロー

1. **起動**: `LocalizationManager.Load(provider, "assets/i18n/locales.json")` が manifest + 全言語リソースをロード
2. **検索**: スクリプト/UI がキー (`"menu.save"`) で `LocalizationManager.Get(key)` を呼ぶ
3. **フォールバック**: 欠落キー → `Manifest.FallbackLanguage` → キーそのまま返却
4. **動的切替**: `LocalizationManager.SetLanguage(language)` → 次回 `Get` から新言語
5. **VM 同期**: `vm.Localization = ...; vm.SyncLocalizationRuntimeState()` で VM 内部状態を更新

### スクリプトからの利用

```aria
# aria-version: 2.0
strict on

*chapter_1
    text "$chapter_1.greeting"  ; ロケール依存テキスト (将来: T / loc_get)
    wait
```

> 注: 現状は `GameState.StringRegisters["$chapter_1.greeting"]` に事前に翻訳を入れる運用が主。`T(key)` / `loc_get(key)` 等の組み込み命令は WIP 領域。

### シナリオローカライゼーション

シナリオディレクトリ `assets/scripts/scenario/<locale>/` でロケール別シナリオを提供。`LocalizationManifest.ScenarioRoot` / `ScenarioFiles` で解決。

### 整合性チェック

`aria-i18n-check` で以下を検出:
- スクリプト中の `loc_get` / `tr` / `loc_format` 使用キー
- コード中の `T("key")` / `F("key")` / `Get("key")` / `Format("key")` 使用キー
- リソース JSON に存在しないキー
- リソース JSON で未使用のキー

## ツール (CLI)

CLI 経由でエンジン機能を独立して実行する 8 つのサブコマンド。詳細は [tools.md](tools.md) を参照。

```
dotnet run --project src/AriaEngine -- <command> [args]

利用可能なサブコマンド:
  aria-lint            静的解析
  aria-compile         スクリプト暗号化コンパイル
  aria-pack            アセットパッキング
  aria-doc             ドキュメント生成
  aria-format          コードフォーマット
  aria-save            セーブデータ操作
  aria-flow-check      フロー解析
  aria-i18n-check      多言語キー整合性チェック
```

## 拡張性

### 新しいオペコードの追加

1. `OpCode.cs` に新しいオペコードを追加
2. `CommandRegistry.cs` に canonical name、alias、category、minimum args を登録
3. 対応する `*CommandHandler` の `HandledCodes` と `Execute` に実装を追加
4. `docs/reference/opcodes/<command>.md` を追加

### 新しいスプライトタイプの追加

1. `Sprite.cs` に新しいタイプを追加
2. `SpriteRenderer.cs` に描画ロジックを追加
3. パーサーに新しいコマンドを追加

### 新しいイージング関数の追加

1. `TweenManager.cs` に関数を追加
2. イージング名をマッピングに追加

### 新しいプラットフォームターゲットの追加

1. `Platform/` 配下に新しいインターフェース実装を追加
2. エントリポイント (`Program.cs`) でサービスロケータを差し替え
3. 必要に応じてレンダラ抽象 (`AriaEngine.Web/BrowserRenderer` のような) を追加
4. ランタイムホスト (`WebRuntimeHost` のような) を実装

## まとめ

AriaEngineのアーキテクチャは以下の特徴を持っています：

1. **クロスプラットフォーム**: Native (Raylib) + Web (Blazor WASM) の 2 ランタイム
2. **モジュール化**: Core / Scripting / Platform / Rendering / Input / Audio / UI / Tools / Web が明確に分離
3. **スクリプト駆動**: ゲームロジックは `.aria` スクリプトで記述
4. **型安全な v2 strict**: コンパイル時型・所有権・寿命検査
5. **多言語ファースト**: ロケール別リソース・スクリプトの構造化
6. **拡張性**: 新しい機能・プラットフォームターゲットを簡単に追加可能
7. **パフォーマンス**: 効率的なレンダリングとメモリ管理
8. **保守性**: コードが整理され、理解しやすい

## 関連ドキュメント

- [言語理念](language-philosophy.md) — 設計思想
- [VM](vm.md) — 仮想マシン
- [Parser](parser.md) — スクリプト解析
- [Rendering](rendering.md) — 描画システム
- [Platform](platform.md) — プラットフォーム抽象化 *(予定)*
- [Tools](tools.md) — CLI ツール群 *(予定)*
- [Scripting Pipeline](scripting-pipeline.md) — スクリプト処理パイプライン *(予定)*
- [Text Subsystem](text-subsystem.md) — テキスト/多言語サブシステム *(予定)*
