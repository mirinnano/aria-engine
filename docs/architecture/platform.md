# プラットフォーム抽象化（V3.1）

> このページの旧Raylib/Blazor詳細は現行C#版の比較資料です。新規V3.1
> プロジェクトの正式な境界は [`v3-native-first.md`](v3-native-first.md) と
> [`../spec/aria-v3-runtime.md`](../spec/aria-v3-runtime.md) です。

このドキュメントでは、AriaEngineの **クロスプラットフォーム抽象化レイヤー** について詳しく説明します。Native (Raylib)、現行Web (Blazor WebAssembly + Canvas 2D)、検証中のRaylib WASMで同じゲームロジックを実行する構成を扱います。

> `AriaEngine.Web` は比較・退避用の現行ターゲットです。Raylib共通ランタイムへの移行検証は `AriaEngine.Wasm` で並行して行います。検証版の詳細は [Raylib WASMプレビュー](web-raylib-wasm.md) を参照してください。

## 概要

AriaEngineは1つの C# コードベースから2つのランタイムターゲットを生成します:

| ランタイム | UI ライブラリ | エントリ | 用途 |
|-----------|-------------|---------|------|
| **Native** | Raylib (C ライブラリ) | `Program.cs` | Windows / macOS / Linux 配布 |
| **Web** | Blazor WebAssembly + Canvas 2D | `App.razor` | PWA ブラウザ配布 |
| **Web preview** | .NET browser-wasm + Raylib 5.5 | `AriaEngine.Wasm/Program.cs` | Raylib共通化の検証成果物 |

エンジン内部のプラットフォーム依存呼び出しは `Platform/` 配下のインターフェースに抽象化されています。Native ビルドでは Raylib 実装、Web ビルドでは Browser 実装がサービスロケータ経由で注入されます。

### ディレクトリ構成

```
src/AriaEngine/
└── Platform/                          # インターフェース + Native 実装
    ├── PlatformServices.cs            # サービスロケータ
    ├── IClock.cs
    ├── IRandomSource.cs
    ├── IWindowService.cs
    ├── IScreenshotService.cs
    ├── IBrowserService.cs
    ├── AriaTextureFilter.cs           # enum
    ├── RaylibClock.cs                 # Native 実装
    ├── RaylibRandomSource.cs
    ├── RaylibWindowService.cs
    ├── RaylibScreenshotService.cs
    └── NativeBrowserService.cs

src/AriaEngine.Web/                    # Blazor WebAssembly ターゲット
├── Program.cs                         # Blazor エントリ
├── App.razor                          # ルートコンポーネント
├── _Imports.razor                     # グローバル using
├── AriaEngine.Web.csproj              # プロジェクトファイル
├── Assets/
│   └── PreloadedWebAssetProvider.cs
├── Input/
│   └── BrowserInputMapper.cs
├── Rendering/
│   ├── BrowserFontLoader.cs
│   ├── BrowserRenderer.cs
│   └── CanvasScaleMapper.cs
├── Runtime/
│   └── WebRuntimeHost.cs              # 起動・フレーム・入力
├── Storage/
│   ├── BrowserStorageOperation.cs
│   ├── IndexedDbSaveStore.cs
│   ├── OpfsAssetStore.cs
│   └── SaveExportImport.cs
└── wwwroot/
    ├── index.html
    ├── service-worker.js
    ├── service-worker.published.js
    ├── assets/web-text-assets.json
    ├── css/app.css
    └── js/aria-web-runtime.js
```

## Platform/ サービスロケータ

### PlatformServices (静的サービスロケータ)

```csharp
namespace AriaEngine.Platform;

public static class PlatformServices
{
    public static IClock Clock { get; set; } = new RaylibClock();
    public static IRandomSource Random { get; set; } = new RaylibRandomSource();
    public static IWindowService Window { get; set; } = new RaylibWindowService();
    public static IScreenshotService Screenshot { get; set; } = new RaylibScreenshotService();
    public static IBrowserService Browser { get; set; } = new NativeBrowserService();
}
```

**初期化タイミング**:
- **Native**: モジュール初期化時にデフォルト (Raylib 実装) で立ち上がる。ゲームコードはそのまま使用可能。
- **Web**: `WebRuntimeHost.Boot` 内で Browser 実装に差し替え予定 (WIP)。

**利用例**:

```csharp
float now = PlatformServices.Clock.NowMilliseconds;
int value = PlatformServices.Random.NextInclusive(0, 100);
byte[]? thumb = PlatformServices.Screenshot.CaptureThumbnail(160, 90);
```

**関連ファイル**: `Platform/PlatformServices.cs` (10 lines)

## Platform/ インターフェース

### IClock — 高精度時刻

```csharp
public interface IClock
{
    float NowMilliseconds { get; }
}
```

**用途**: ゲームループの `deltaTime` 計算やアニメーション駆動。
**Native 実装**: `RaylibClock` → `Raylib.GetTime() * 1000f`
**関連ファイル**: `Platform/IClock.cs`, `Platform/RaylibClock.cs`

### IRandomSource — 乱数

```csharp
public interface IRandomSource
{
    int NextInclusive(int min, int max);
}
```

**用途**: スクリプトの `rnd` 命令、地震エフェクトのオフセット等。
**Native 実装**: `RaylibRandomSource` → `Raylib.GetRandomValue(min, max)`
**関連ファイル**: `Platform/IRandomSource.cs`, `Platform/RaylibRandomSource.cs`

### IWindowService — ウィンドウ制御

```csharp
public interface IWindowService
{
    int ScreenWidth { get; }
    int ScreenHeight { get; }
    void ToggleFullscreen();
    int CurrentMonitor { get; }
    int GetMonitorWidth(int monitor);
    int GetMonitorHeight(int monitor);
    void SetWindowSize(int width, int height);
}
```

**用途**: フルスクリーン切替、マルチモニター対応、ウィンドウリサイズ。
**Native 実装**: `RaylibWindowService` → 各 API を `Raylib_cs` に委譲。
**関連ファイル**: `Platform/IWindowService.cs`, `Platform/RaylibWindowService.cs`

### IScreenshotService — スクリーンショット

```csharp
public interface IScreenshotService
{
    byte[]? CaptureThumbnail(int width, int height);
}
```

**用途**: セーブスロットのサムネイル生成。
**Native 実装**: `RaylibScreenshotService` → 画面イメージ取得 → リサイズ → 一時 PNG エクスポート → バイト列。

```csharp
public byte[]? CaptureThumbnail(int width, int height)
{
    if (!Raylib.IsWindowReady()) return null;

    var image = Raylib.LoadImageFromScreen();
    try
    {
        Raylib.ImageResize(ref image, width, height);
        string tempPath = Path.Combine(Path.GetTempPath(), $"aria_thumb_{Guid.NewGuid():N}.png");
        try
        {
            Raylib.ExportImage(image, tempPath);
            return File.ReadAllBytes(tempPath);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }
    finally
    {
        Raylib.UnloadImage(image);
    }
}
```

**関連ファイル**: `Platform/IScreenshotService.cs`, `Platform/RaylibScreenshotService.cs` (31 lines)

### IBrowserService — 外部 URL 起動

```csharp
public interface IBrowserService
{
    bool OpenExternal(Uri uri);
}
```

**用途**: ゲーム内リンク (HP / Twitter / ストアページ等) の起動。
**Native 実装**: `NativeBrowserService` → `Process.Start(new ProcessStartInfo { UseShellExecute = true })`。
**関連ファイル**: `Platform/IBrowserService.cs`, `Platform/NativeBrowserService.cs`

### AriaTextureFilter — テクスチャフィルタ列挙

```csharp
public enum AriaTextureFilter
{
    Point,
    Bilinear,
    Trilinear
}
```

**用途**: スプライト / フォントのテクスチャフィルタ指定 (ピクセルアート ↔ アンチエイリアス)。
**関連ファイル**: `Platform/AriaTextureFilter.cs`

## AriaEngine.Web/ プロジェクト

### csproj (Blazor WebAssembly 構成)

```xml
<Project Sdk="Microsoft.NET.Sdk.BlazorWebAssembly">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <ServiceWorkerAssetsManifest>service-worker-assets.js</ServiceWorkerAssetsManifest>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\AriaEngine\AriaEngine.csproj"
                      AdditionalProperties="AriaEngineAsLibrary=true" />
    <PackageReference Include="Microsoft.AspNetCore.Components.WebAssembly" Version="8.0.22" />
    <PackageReference Include="Microsoft.AspNetCore.Components.WebAssembly.DevServer" Version="8.0.22"
                      PrivateAssets="all" />
  </ItemGroup>

  <ItemGroup>
    <Content Include="..\AriaEngine\init.aria" Link="wwwroot\init.aria" CopyToOutputDirectory="PreserveNewest" />
    <Content Include="..\AriaEngine\assets\**\*" Link="wwwroot\assets\%(RecursiveDir)%(Filename)%(Extension)" CopyToOutputDirectory="PreserveNewest" />
    <ServiceWorker Include="wwwroot\service-worker.js" PublishedContent="wwwroot\service-worker.published.js" />
  </ItemGroup>
</Project>
```

**ポイント**:
- `AriaEngineAsLibrary=true` でエンジン本体を参照ライブラリとして埋め込み
- `init.aria` と `assets/**` を Blazor の `wwwroot/` にコピー (プリロード対象)
- `service-worker.js` を PWA 用に発行時に差し替え

**関連ファイル**: `AriaEngine.Web/AriaEngine.Web.csproj` (20 lines)

### Program.cs (Blazor エントリ)

```csharp
public static class Program
{
    public static async Task Main(string[] args)
    {
        WebAssemblyHostBuilder builder = WebAssemblyHostBuilder.CreateDefault(args);
        builder.RootComponents.Add<App>("#app");
        builder.RootComponents.Add<HeadOutlet>("head::after");
        builder.Services.AddScoped(_ => new HttpClient
        {
            BaseAddress = new Uri(builder.HostEnvironment.BaseAddress)
        });
        await builder.Build().RunAsync();
    }
}
```

標準的な Blazor WASM テンプレート。`#app` に `App.razor` をマウント。

**関連ファイル**: `AriaEngine.Web/Program.cs` (17 lines)

### App.razor (ルートコンポーネント)

**役割**: `<canvas>` ホスト + JSInterop で Web ランタイムと接続。

```razor
@inject IJSRuntime Js
@inject HttpClient Http
@implements IAsyncDisposable

<main class="aria-shell">
    <canvas id="aria-canvas" @ref="_canvas" width="1280" height="720"></canvas>
</main>

@code {
    private ElementReference _canvas;
    private WebRuntimeHost? _host;
    private DotNetObjectReference<App>? _self;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;
        try
        {
            _host = WebRuntimeHost.Boot(
                new PreloadedWebAssetProvider(await LoadTextAssetsAsync()),
                new WebRuntimeOptions("web-runtime"));
            _self = DotNetObjectReference.Create(this);
            await Js.InvokeVoidAsync("ariaWebRuntime.boot", _canvas, _self);
            await RenderFrameAsync();
        }
        catch (Exception ex)
        {
            await Js.InvokeVoidAsync("ariaWebRuntime.showError", _canvas, ex.Message);
        }
    }

    [JSInvokable]
    public async Task HandlePointerDown(double x, double y, double width, double height, int button)
    {
        try
        {
            if (_host is null) return;
            bool handled = button == 2
                ? _host.HandleContextMenu(x, y, width, height)
                : button == 0 && _host.HandlePointerPress(x, y, width, height);
            if (handled)
            {
                await ProcessStorageOperationsAsync();
                await RenderFrameAsync();
            }
        }
        catch (Exception ex)
        {
            await Js.InvokeVoidAsync("ariaWebRuntime.showError", _canvas, ex.Message);
        }
    }
    // ...
}
```

**ライフサイクル**:

1. `OnAfterRenderAsync(firstRender)`:
   - `WebRuntimeHost.Boot(PreloadedWebAssetProvider, options)` でランタイム起動
   - `ariaWebRuntime.boot(canvas, dotnetRef)` で JS 側初期化 + サービスワーカー登録
2. `[JSInvokable] HandlePointerDown(x, y, w, h, button)`:
   - `button == 0` → `_host.HandlePointerPress` (左クリック = VM 進行)
   - `button == 2` → `_host.HandleContextMenu` (右クリック = Web メニュー)
3. `RenderFrameAsync`:
   - `ariaWebRuntime.measure(canvas)` → CSS サイズ取得
   - `_host.CreateFrame(width, height)` → `WebRuntimeFrame` 取得
   - `ariaWebRuntime.renderFrame(canvas, frame)` → Canvas 描画
4. `ProcessStorageOperationsAsync`:
   - `_host.DrainStorageOperations()` を JS に適用
   - Read 結果は `_host.ApplyLoadedStorage` で VM に反映
5. `DisposeAsync`: DotNetObjectReference 解放

**関連ファイル**: `AriaEngine.Web/App.razor` (102 lines)

## Runtime/ — Web ランタイムホスト

### WebRuntimeHost

**役割**: ランタイム起動、入力処理、フレーム生成、ストレージ操作管理の中核。618 lines。

**主要 API**:

```csharp
public sealed record WebRuntimeOptions(string RuntimeDataRoot = "");

public sealed record WebRuntimeFrame(
    VmState ExecutionState,
    int LogicalWidth,
    int LogicalHeight,
    BrowserFontFace Font,
    IReadOnlyList<BrowserDrawCommand> DrawCommands)
{
    public int ProgramCounter { get; init; }
    public string CurrentText { get; init; } = "";
}

public sealed class WebRuntimeHost
{
    public static WebRuntimeHost Boot(IAssetProvider provider, WebRuntimeOptions options);
    public WebRuntimeFrame CreateFrame(double cssWidth, double cssHeight);
    public bool HandlePointerPress(double cssX, double cssY, double cssWidth, double cssHeight);
    public bool HandleContextMenu(double cssX, double cssY, double cssWidth, double cssHeight);
    public IReadOnlyList<BrowserStorageOperation> DrainStorageOperations();
    public bool ApplyLoadedStorage(BrowserStorageOperation operation, string? payload);
}
```

### 起動シーケンス (`Boot`)

```
IAssetProvider 受信
  ↓
ErrorReporter 生成
  ↓
Parser / ScriptLoader(RunMode.Dev) 生成
  ↓
ConfigManager (usePortableJsonPersistent = true)  ← Web 用: OS 依存しない portable JSON
  ↓
SaveManager (usePortableJsonSaves = true)
  ↓
VirtualMachine 生成
  ↓
locales.json 存在時:
  LocalizationManager.Load(provider, "assets/i18n/locales.json")
  → vm.Localization = ...
  → vm.SyncLocalizationRuntimeState()
  ↓
LoadInitAndMain (init.aria / main.aria 実行)
  ↓
WebRuntimeHost 返却
```

**永続化先** (Web 用 temporary root):
- `web-runtime/config.json` — ユーザー設定
- `web-runtime/save/persistent.ariasav` — 永続データ
- `web-runtime/saves/slot_<NN>.ariasav` — セーブスロット

これらは IndexedDB へ最終的に同期される (Run-time 中は一時ファイル)。

### 1 フレーム生成 (`CreateFrame`)

```csharp
public WebRuntimeFrame CreateFrame(double cssWidth, double cssHeight)
{
    RunUntilInteractive();
    var mapper = CanvasScaleMapper.Create(cssWidth, cssHeight);
    var renderer = new BrowserRenderer(mapper);
    BrowserFontFace font = BrowserFontLoader.Resolve(_vm.Localization, _vm.State.EngineSettings.FontPath);
    var drawCommands = renderer.ToDrawCommands(CollectRenderableSprites()).ToList();
    AddClickCursorCommand(drawCommands, mapper);

    return new WebRuntimeFrame(
        _vm.State.Execution.State,
        _vm.State.EngineSettings.WindowWidth,
        _vm.State.EngineSettings.WindowHeight,
        font,
        drawCommands)
    {
        ProgramCounter = _vm.State.Execution.ProgramCounter,
        CurrentText = _vm.State.TextRuntime.CurrentTextBuffer
    };
}
```

**内部状態遷移**:

```
RunUntilInteractive
  ├─ RunUntilStopped (MaxStepBatches = 256 回)
  │  ├─ VmState.Running           → _vm.Step()
  │  ├─ VmState.FadingIn/Out      → _vm.FinishFade()
  │  └─ VmState.WaitingForDelay/Animation/Timer → _vm.Tweens.Update + _vm.Update
  └─ IsInteractive && Tweens.IsAnimating → _vm.Tweens.FinishAll + _vm.Update
```

`CanAutoAdvance(state)`: `Running / WaitingForDelay / WaitingForAnimation / WaitingForTimer / FadingIn / FadingOut`
`IsInteractive(state)`: `WaitingForButton / WaitingForClick`

### クリック処理 (`HandlePointerPress`)

```csharp
public bool HandlePointerPress(double cssX, double cssY, double cssWidth, double cssHeight)
{
    var mapper = CanvasScaleMapper.Create(cssWidth, cssHeight);
    LogicalPoint logical = mapper.MapCssToLogical(cssX, cssY);

    if (_webMenuOpen) return HandleWebMenuPress(logical);

    if (_vm.State.Execution.State == VmState.WaitingForClick)
    {
        _vm.ResumeFromClick();
        RunUntilInteractive();
        return true;
    }

    if (_vm.State.Execution.State != VmState.WaitingForButton) return false;

    int? buttonId = FindButtonAt(logical);
    if (buttonId is null) return false;

    _vm.ResumeFromButton(buttonId.Value);
    RunUntilInteractive();
    return true;
}
```

**ボタン検索** (`FindButtonAt`):
- `_vm.State.Interaction.SpriteButtonMap.Keys` を取得
- 表示中 (`Visible: true`) でフィルタ
- Z オーダー降順 (最前面優先)
- `Contains(sprite, logical)` でヒット判定 → `sprite.Id` 返却

**ヒット判定** (`Contains`):
- `ClickAreaW > 0` なら `ClickAreaX/Y/W/H`、それ以外は `X/Y/Width*ScaleX/Height*ScaleY`

### Web メニュー (`_webMenuOpen`)

右クリック (`HandleContextMenu`) で開く Web 専用フルスクリーンメニュー。

**構造** (sprite ベースで Canvas に描画):
- 背景: `WebMenuBackdropSpriteId = -9100` (全画面, alpha=246, "#06070a")
- 行アイコン: `WebMenuRowBaseSpriteId - i` = `-9200 - i`
- 行テキスト: `WebMenuTextBaseSpriteId - i` = `-9300 - i`
- フォーカス下線: `WebMenuTextBaseSpriteId - 100 - i` = `-9400 - i`
- クリックカーソル: `WebClickCursorSpriteId = -9400`

**メニュー項目** (デフォルト + `vm.State.MenuRuntime.RightMenuEntries`):
- `save` / `load` / `backlog` / `skip` / `settings` / `gallery` / `reset` / `end`
- `localizeMenuLabel()` で `_vm.Localization.Get("menu.<action>")` を試行
- アイコンは Unicode グリフ (■ □ ● ▶ ◆ ▷ ▲ ▼)

**save アクション**: `QueueWebSave(slot: 0)` → `_vm.SaveGame(0)` → ファイルから JSON 読込 → `IndexedDbSaveStore.WriteSave(0, payload)` をペンディングキューへ
**load アクション**: `IndexedDbSaveStore.ReadSave(0)` をペンディングキューへ (結果が ApplyLoadedStorage で返ってくる)

### ストレージ操作キュー

`DrainStorageOperations()` → `App.razor.ProcessStorageOperationsAsync` → `ariaWebRuntime.applyStorageOperation` (JS) → `ApplyLoadedStorage` で Read 結果を VM にフィードバック。

これにより Blazor の同期 JSInterop 制約を回避し、async I/O を VM の同期ループに統合。

### 互換テキストウィンドウ描画

旧 `.aria` スクリプト (`CompatAutoUi = true`) 向けに、`AddCompatTextWindowSprites` で固定テキスト矩形スプライト (`-9000`, `-8999`) を毎フレーム追加。

**関連ファイル**: `AriaEngine.Web/Runtime/WebRuntimeHost.cs` (618 lines)

## Rendering/ — ブラウザレンダリング

### BrowserDrawCommand — Canvas 描画プリミティブ

```csharp
public enum BrowserDrawKind
{
    Image = 0,
    Text = 1,
    Rect = 2,
    Triangle = 3
}

public sealed class BrowserDrawCommand
{
    public BrowserDrawKind Kind { get; set; }
    public int SpriteId { get; set; }

    // CSS 座標 (最終的に Canvas 2D に渡される値)
    public double CssX { get; set; }
    public double CssY { get; set; }
    public double CssWidth { get; set; }
    public double CssHeight { get; set; }

    // 論理座標 (CSS 変換前のゲーム内座標)
    public double LogicalX { get; set; }
    public double LogicalY { get; set; }
    public double LogicalWidth { get; set; }
    public double LogicalHeight { get; set; }

    // Image
    public string ImagePath { get; set; } = "";
    public bool UseNaturalImageSize { get; set; }

    // Text
    public string Text { get; set; } = "";
    public int FontSize { get; set; }
    public string TextAlign { get; set; } = "left";
    public string TextVAlign { get; set; } = "top";
    public string Color { get; set; } = "#ffffff";
    public string TextShadowColor { get; set; } = "";
    public int TextShadowX { get; set; }
    public int TextShadowY { get; set; }

    // Rect
    public string FillColor { get; set; } = "#000000";
    public int FillAlpha { get; set; } = 255;
    public int CornerRadius { get; set; }
    public string BorderColor { get; set; } = "";
    public int BorderWidth { get; set; }
    public int BorderOpacity { get; set; } = 255;

    // Shadow (Rect/Text 共通)
    public string ShadowColor { get; set; } = "";
    public int ShadowOffsetX { get; set; }
    public int ShadowOffsetY { get; set; }
    public int ShadowAlpha { get; set; } = 128;

    // 共通
    public double Opacity { get; set; } = 1d;
    public int Z { get; set; }
}
```

> JS 側 (`aria-web-runtime.js`) はキャメルケースとパスカルケースの両方を許容する (`command.cssX ?? command.CssX` 形式)。

**関連ファイル**: `AriaEngine.Web/Rendering/BrowserRenderer.cs` (127 lines)

### BrowserRenderer — Sprite → BrowserDrawCommand 変換

```csharp
public IReadOnlyList<BrowserDrawCommand> ToDrawCommands(IEnumerable<Sprite> sprites)
{
    return sprites
        .Where(sprite => sprite.Visible)
        .OrderBy(sprite => sprite.Z)
        .Select(ToDrawCommand)
        .ToList();
}
```

**`ToDrawCommand` 変換ルール**:
- `SpriteType.Image` → `BrowserDrawKind.Image` (パスは `assets/` プレフィックス正規化)
- `SpriteType.Text` → `BrowserDrawKind.Text` (TextAlign/TextVAlign は `ToLowerInvariant()`)
- `SpriteType.Rect` (その他) → `BrowserDrawKind.Rect`
- `Width=0 && Height=0 && Type=Image` → `UseNaturalImageSize = true`

**画像パス正規化** (`NormalizeImagePathForStaticWeb`):
- `\` → `/`、先頭 `/` 除去
- `assets/` 始まり、絶対 URL、data URI はそのまま
- それ以外は `assets/` プレフィックス付与

**関連ファイル**: `AriaEngine.Web/Rendering/BrowserRenderer.cs` (127 lines)

### CanvasScaleMapper — 座標系マッピング

```csharp
public const double NativeWidth = 1280d;
public const double NativeHeight = 720d;

public sealed class CanvasScaleMapper
{
    private CanvasScaleMapper(double cssWidth, double cssHeight)
    {
        CssWidth = cssWidth;
        CssHeight = cssHeight;
        Scale = Math.Min(cssWidth / NativeWidth, cssHeight / NativeHeight);
        OffsetX = (cssWidth - (NativeWidth * Scale)) / 2d;
        OffsetY = (cssHeight - (NativeHeight * Scale)) / 2d;
    }

    public double Scale { get; }
    public double OffsetX { get; }
    public double OffsetY { get; }

    public CssPoint MapLogicalToCss(double x, double y) =>
        new(OffsetX + x * Scale, OffsetY + y * Scale);

    public LogicalPoint MapCssToLogical(double x, double y) =>
        new((x - OffsetX) / Scale, (y - OffsetY) / Scale);
}
```

**設計**:
- ネイティブ解像度 1280×720 を基準に CSS サイズに **アスペクト比保持でスケーリング**
- 余白は `OffsetX/Y` で中央寄せ (letterbox/pillarbox)
- 論理座標 (1280×720) ↔ CSS 座標 (可変) を双方向変換

**座標型**:
```csharp
public readonly record struct CssPoint(double X, double Y);
public readonly record struct LogicalPoint(double X, double Y);
public readonly record struct LogicalRect(double X, double Y, double Width, double Height);
```

**関連ファイル**: `AriaEngine.Web/Rendering/CanvasScaleMapper.cs` (43 lines)

### BrowserFontLoader — フォント解決

```csharp
public sealed record BrowserFontFace(string Family, string SourceUrl)
{
    public string CssDeclaration => $"font-family: '{Family}'; src: url('{SourceUrl}');";
}

public static class BrowserFontLoader
{
    public static BrowserFontFace Resolve(LocalizationManager localization, string fallbackFontPath)
    {
        string? localeFont = localization.GetFontForLanguage(localization.CurrentLanguage);
        string source = NormalizeAssetUrl(string.IsNullOrWhiteSpace(localeFont) ? fallbackFontPath : localeFont);
        return new BrowserFontFace("AriaRuntime", source);
    }
}
```

**動作**:
- 現在のロケールに対応するフォントを `LocalizationManager.GetFontForLanguage(language)` で取得
- フォールバックは `EngineSettings.FontPath`
- 全て `AriaRuntime` という固定ファミリ名で `@font-face` 定義 → JS が `<style>` タグで注入

**関連ファイル**: `AriaEngine.Web/Rendering/BrowserFontLoader.cs` (25 lines)

## Input/ — ブラウザ入力マッピング

### BrowserInputMapper

```csharp
public sealed class BrowserInputMapper
{
    private readonly CanvasScaleMapper _mapper;

    public BrowserInputMapper(CanvasScaleMapper mapper) { _mapper = mapper; }

    public LogicalPoint MapPointerToLogical(double clientX, double clientY, double canvasLeft, double canvasTop) =>
        _mapper.MapCssToLogical(clientX - canvasLeft, clientY - canvasTop);

    public bool IsPointerInside(LogicalRect rect, double clientX, double clientY, double canvasLeft, double canvasTop)
    {
        LogicalPoint point = MapPointerToLogical(clientX, clientY, canvasLeft, canvasTop);
        return point.X >= rect.X && point.X <= rect.X + rect.Width &&
               point.Y >= rect.Y && point.Y <= rect.Y + rect.Height;
    }
}
```

**役割**: ブラウザの `clientX/Y` (ウィンドウ座標) + キャンバス左上原点 → 論理座標 (1280×720 基準) に変換。
**現状**: `WebRuntimeHost` 内で直接 `CanvasScaleMapper` を使うため、補助的に使用 (将来フック用)。

**関連ファイル**: `AriaEngine.Web/Input/BrowserInputMapper.cs` (27 lines)

## Assets/ — プリロードアセット提供

### PreloadedWebAssetProvider

```csharp
public sealed class PreloadedWebAssetProvider : IAssetProvider
{
    private static readonly string[] ExternalAssetExtensions =
    {
        ".png", ".jpg", ".jpeg", ".webp", ".gif", ".bmp",
        ".ogg", ".mp3", ".wav", ".flac",
        ".ttf", ".otf", ".woff", ".woff2"
    };

    private readonly Dictionary<string, byte[]> _preloaded;

    public PreloadedWebAssetProvider(IReadOnlyDictionary<string, string> textAssets)
    {
        _preloaded = textAssets.ToDictionary(
            pair => Normalize(pair.Key),
            pair => Encoding.UTF8.GetBytes(pair.Value),
            StringComparer.OrdinalIgnoreCase);
    }

    public long PreloadedByteCount => _preloaded.Values.Sum(bytes => (long)bytes.Length);
    public bool CanMaterializeToFile => false;

    public bool Exists(string path)
    {
        string normalized = Normalize(path);
        return _preloaded.ContainsKey(normalized) ||
               IsExternalAssetPath(normalized) ||
               IsExternalAssetPath(WithAssetsPrefix(normalized));
    }

    public string[] ReadAllLines(string path) =>
        ReadAllText(path).Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');

    public string ReadAllText(string path) => Encoding.UTF8.GetString(ReadAllBytes(path));

    public byte[] ReadAllBytes(string path)
    {
        string normalized = Normalize(path);
        if (_preloaded.TryGetValue(normalized, out byte[]? bytes)) return bytes;
        throw new FileNotFoundException($"Preloaded web asset not found: {normalized}");
    }

    public Stream OpenRead(string path) => new MemoryStream(ReadAllBytes(path), writable: false);

    public string MaterializeToFile(string path) =>
        throw new PlatformNotSupportedException($"Web asset cannot be materialized: {path}");

    private static bool IsExternalAssetPath(string path)
    {
        string extension = Path.GetExtension(path);
        return path.StartsWith("assets/", StringComparison.OrdinalIgnoreCase) &&
               ExternalAssetExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
    }

    private static string WithAssetsPrefix(string path) =>
        path.StartsWith("assets/", StringComparison.OrdinalIgnoreCase) ? path : $"assets/{path}";

    private static string Normalize(string path) => path.Replace('\\', '/').TrimStart('/');
}
```

**動作**:
- 起動時に `App.razor.LoadTextAssetsAsync` が `web-text-assets.json` の `preload` リストに従いテキスト系アセットを全て取得
- バイナリ (画像 / 音声 / フォント) は `Exists` のみ true を返し、JS 側で直接 HTTP fetch
- `MaterializeToFile` は未サポート (`PlatformNotSupportedException`)

**`IAssetProvider` 契約** との整合性:
- `ReadAllBytes/ReadAllText/ReadAllLines/OpenRead` — プリロード済み辞書から返す
- `Exists` — プリロード済み OR `assets/` 配下のバイナリ
- `MaterializeToFile` — 失敗 (Web では一時ファイル化しない)

**プリロード対象** (`wwwroot/assets/web-text-assets.json`):
```json
{
  "preload": [
    "init.aria",
    "assets/i18n/locales.json",
    "assets/i18n/ui.ja-JP.json", "assets/i18n/ui.en-US.json",
    "assets/i18n/ui.zh-CN.json", "assets/i18n/ui.zh-TW.json",
    "assets/scripts/main.aria",
    "assets/scripts/ui_presets.aria",
    "assets/scripts/settings_ui.aria",
    "assets/scripts/gallery_ui.aria",
    "assets/scripts/omake_ui.aria",
    "assets/scripts/scenario_01-08.aria"
  ]
}
```

**関連ファイル**: `AriaEngine.Web/Assets/PreloadedWebAssetProvider.cs` (71 lines)

## Storage/ — ブラウザストレージ抽象

### BrowserStorageOperation

```csharp
public enum BrowserStorageArea { IndexedDb, Opfs, Download, FilePicker }
public enum BrowserStorageOperationKind { Read, Write, Export, Import }

public sealed record BrowserStorageOperation(
    BrowserStorageArea Area,
    BrowserStorageOperationKind Kind,
    string DatabaseName,
    string StoreName,
    string Key,
    string Payload = "",
    long ContentLength = 0,
    string MimeType = "");
```

**役割**: Web ストレージ操作の統一データモデル。C# → JS への片方向シリアライズ。

**関連ファイル**: `AriaEngine.Web/Storage/BrowserStorageOperation.cs` (27 lines)

### IndexedDbSaveStore

```csharp
public static class IndexedDbSaveStore
{
    public const string DatabaseName = "aria-engine";

    public static BrowserStorageOperation WriteSave(int slot, string json) =>
        new(BrowserStorageArea.IndexedDb, BrowserStorageOperationKind.Write,
            DatabaseName, "saves", $"save:{slot:000}", json);

    public static BrowserStorageOperation ReadSave(int slot) =>
        new(BrowserStorageArea.IndexedDb, BrowserStorageOperationKind.Read,
            DatabaseName, "saves", $"save:{slot:000}");

    public static BrowserStorageOperation WriteSetting(string name, string json) =>
        new(BrowserStorageArea.IndexedDb, BrowserStorageOperationKind.Write,
            DatabaseName, "settings", $"settings:{name}", json);
}
```

**ストア構成**:
- DB: `aria-engine` (DB_VERSION = 1)
- Object Store `saves`: key = `save:000` 〜 `save:999` (ペイロードはセーブ JSON)
- Object Store `settings`: key = `settings:<name>` (個別設定 JSON)

**関連ファイル**: `AriaEngine.Web/Storage/IndexedDbSaveStore.cs` (38 lines)

### OpfsAssetStore — Origin Private File System

```csharp
public static class OpfsAssetStore
{
    public static BrowserStorageOperation WriteFile(string path, byte[] content)
    {
        string normalized = path.Replace('\\', '/').TrimStart('/');
        return new BrowserStorageOperation(
            BrowserStorageArea.Opfs, BrowserStorageOperationKind.Write,
            "origin-private-file-system", "assets",
            $"assets/{normalized}",
            ContentLength: content.LongLength);
    }
}
```

**用途**: バイナリアセット (画像 / 音声) のブラウザ内キャッシュ。WASM ヒープを圧迫しないよう OPFS へ逃がす。
**関連ファイル**: `AriaEngine.Web/Storage/OpfsAssetStore.cs` (16 lines)

### SaveExportImport — エクスポート / インポート

```csharp
public static class SaveExportImport
{
    public static BrowserStorageOperation CreateExport(string fileName, string json) =>
        new(BrowserStorageArea.Download, BrowserStorageOperationKind.Export,
            "", "downloads", fileName, json,
            MimeType: "application/vnd.aria.save+json");

    public static BrowserStorageOperation CreateImportRequest(string extension) =>
        new(BrowserStorageArea.FilePicker, BrowserStorageOperationKind.Import,
            "", "file-picker", extension);
}
```

**用途**: セーブデータの `.ariasav` エクスポート (`<a download>` 経由) とインポート (FilePicker 経由)。
**MIME タイプ**: `application/vnd.aria.save+json`
**関連ファイル**: `AriaEngine.Web/Storage/SaveExportImport.cs` (26 lines)

## wwwroot/ — 静的アセット

### index.html — PWA エントリ HTML

```html
<!doctype html>
<html lang="ja">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1.0, viewport-fit=cover" />
  <title>Aria Engine Web</title>
  <base href="/" />
  <link rel="manifest" href="manifest.webmanifest" />
  <link rel="stylesheet" href="css/app.css" />
</head>
<body>
  <div id="app">Loading...</div>
  <script src="js/aria-web-runtime.js"></script>
  <script src="_framework/blazor.webassembly.js"></script>
  <script>
    navigator.serviceWorker?.register("service-worker.js");
  </script>
</body>
</html>
```

**シーケンス**:
1. ロード中表示: `Loading...`
2. `aria-web-runtime.js` (Canvas / IndexedDB / OPFS / Service Worker 連携)
3. Blazor WebAssembly 起動
4. サービスワーカー登録 (オフライン対応)

**関連ファイル**: `AriaEngine.Web/wwwroot/index.html` (19 lines)

### css/app.css — キャンバススタイル

```css
html, body, #app {
  width: 100%;
  height: 100%;
  margin: 0;
  background: #050607;
  overflow: hidden;
}

.aria-shell {
  width: 100vw;
  height: 100vh;
  display: grid;
  place-items: center;
  background: #050607;
}

#aria-canvas {
  width: min(100vw, calc(100vh * 16 / 9));
  height: min(100vh, calc(100vw * 9 / 16));
  image-rendering: auto;
  touch-action: manipulation;
  outline: none;
}
```

**設計**:
- `16:9` アスペクト比で全画面表示 (letterbox/pillarbox なし、CSS で調整)
- `touch-action: manipulation` でピンチズームを無効化
- `image-rendering: auto` で Canvas 補間を許可

**関連ファイル**: `AriaEngine.Web/wwwroot/css/app.css` (25 lines)

### service-worker.published.js — PWA オフライン対応

```javascript
self.addEventListener("install", event => {
  event.waitUntil((async () => {
    const cache = await caches.open("aria-engine-v1");
    await cache.addAll(["./", "index.html", "manifest.webmanifest"]);
    self.skipWaiting();
  })());
});

self.addEventListener("activate", event => {
  event.waitUntil(self.clients.claim());
});

self.addEventListener("fetch", event => {
  event.respondWith(
    caches.match(event.request).then(response => response || fetch(event.request))
  );
});
```

**戦略**:
- **install**: ルート HTML / manifest をキャッシュ
- **fetch**: キャッシュ優先、フォールバックでネットワーク
- **activate**: 既存クライアントを即座に制御

**関連ファイル**: `AriaEngine.Web/wwwroot/service-worker.published.js` (17 lines)

### aria-web-runtime.js — JS ランタイム

**役割**: Canvas 2D 描画 + ポインタイベント + ストレージ操作 + Service Worker 登録。376 lines。

**`window.ariaWebRuntime` 名前空間 API**:

| 関数 | 役割 |
|------|------|
| `boot(canvas, dotnet)` | 起動: キャンバスのポインタイベント登録 + サービスワーカー登録 |
| `measure(canvas)` | キャンバス CSS サイズ (`{ width, height }`) を返却 |
| `fitCanvas(canvas)` | 高 DPI 対応で Canvas ピクセルバッファを再設定 + 2D コンテキスト取得 |
| `renderFrame(canvas, frame)` | `WebRuntimeFrame.drawCommands` を Canvas 2D で描画 |
| `showError(canvas, message)` | フォールバックエラー描画 |
| `applyStorageOperation(op)` | `BrowserStorageOperation` を IndexedDB / OPFS / Download / FilePicker で実行 |
| `ensureFont(font)` | `@font-face` を `<style>` タグで動的注入 (重複防止) |

**`fitCanvas` の高 DPI 対応**:

```javascript
function fitCanvas(canvas) {
  const size = measure(canvas);
  const dpr = Math.max(1, window.devicePixelRatio || 1);
  const width = Math.max(1, Math.round(size.width * dpr));
  const height = Math.max(1, Math.round(size.height * dpr));
  if (canvas.width !== width) canvas.width = width;
  if (canvas.height !== height) canvas.height = height;
  const ctx = canvas.getContext("2d");
  ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
  ctx.imageSmoothingEnabled = true;
  ctx.imageSmoothingQuality = "high";
  return { ...size, dpr, ctx };
}
```

**描画プリミティブ**:
- `drawRect(ctx, command)` — 影 → 塗り → 角丸 → 枠線
- `drawImage(ctx, command, frame)` — プリロード `Map<path, Image>` から取得 → キャッシュ
- `drawText(ctx, command, frame)` — `wrapText()` で行ラップ + アライン + 影
- `drawTriangle(ctx, command)` — クリックカーソル用三角形

**ポインタイベント**:

```javascript
canvas.addEventListener("pointerdown", (event) => {
  const rect = canvas.getBoundingClientRect();
  const x = event.clientX - rect.left;
  const y = event.clientY - rect.top;
  dotNet.invokeMethodAsync("HandlePointerDown", x, y, rect.width, rect.height, event.button ?? 0)
    .catch(error => {
      window.__ariaWebLastInputError = String(error);
      showError(canvas, String(error));
    });
});

canvas.addEventListener("contextmenu", (event) => {
  event.preventDefault();
  // ... right-click → HandlePointerDown with button=2
});
```

**ウィンドウリサイズ対応**:
```javascript
window.addEventListener("resize", () => {
  if (lastFrame && lastCanvas) renderFrame(lastCanvas, lastFrame);
});
```

**関連ファイル**: `AriaEngine.Web/wwwroot/js/aria-web-runtime.js` (376 lines)

## Platform 間の差分

| 項目 | Native | Web |
|------|--------|-----|
| **ウィンドウ** | Raylib ウィンドウ (リサイズ可) | `<canvas>` (CSS 16:9) |
| **グラフィックス** | Raylib Draw API | Canvas 2D Context |
| **入力** | Raylib Input (マウス/キーボード) | PointerEvent / KeyboardEvent |
| **スクリーンショット** | `LoadImageFromScreen` → PNG | DOM → Canvas → `toDataURL` (将来) |
| **乱数** | `Raylib.GetRandomValue` | `Math.random()` (将来: サービス差し替え) |
| **時間** | `Raylib.GetTime()` | `performance.now()` (将来: サービス差し替え) |
| **セーブ** | ファイルシステム (`.ariasav`) | IndexedDB (`aria-engine` DB) |
| **アセット** | ファイルシステム | プリロード辞書 + HTTP fetch |
| **多言語** | 同一 (`LocalizationManager`) | 同一 |
| **スクリプト** | 同一 (`ScriptLoader`) | 同一 (`RunMode.Dev` でプリロードから) |
| **VM/Parser** | 同一 | 同一 |
| **IAssetProvider** | ファイルシステム実装 | `PreloadedWebAssetProvider` |
| **PWA** | なし | Service Worker + manifest |

## IAssetProvider 抽象

両ランタイムでアセット読み込みを統一する **キー抽象**。`Core/Assets/IAssetProvider.cs` (Native 側インターフェース) を Web 側 `PreloadedWebAssetProvider` が実装する。

**API 契約**:

```csharp
public interface IAssetProvider
{
    long PreloadedByteCount { get; }
    bool CanMaterializeToFile { get; }
    bool Exists(string path);
    string[] ReadAllLines(string path);
    string ReadAllText(string path);
    byte[] ReadAllBytes(string path);
    Stream OpenRead(string path);
    string MaterializeToFile(string path);  // Web: throw PlatformNotSupportedException
}
```

**利用例**:

```csharp
// 共通: 初期化時に provider を取得
var provider = new PreloadedWebAssetProvider(preloadedTextAssets);
// または
var provider = new FileSystemAssetProvider(assetsRoot);

// 共通: ロード
string text = provider.ReadAllText("assets/i18n/locales.json");
byte[] png = provider.ReadAllBytes("assets/bg/forest.png");
bool exists = provider.Exists("assets/scripts/main.aria");
```

これにより以下のコンポーネントがプラットフォームを意識しない:
- `LocalizationManager.Load` (i18n JSON ロード)
- `ScriptPreprocessor.ExpandIncludes` (include 解決)
- `ScriptLoader.LoadScript` (スクリプトロード)

**関連ファイル**:
- `Core/Assets/IAssetProvider.cs` (Native 側)
- `AriaEngine.Web/Assets/PreloadedWebAssetProvider.cs` (Web 側実装, 71 lines)

## JSInterop 契約

C# (`App.razor`) ↔ JS (`aria-web-runtime.js`) 間のデータ契約:

**C# → JS**:
- `ariaWebRuntime.boot(canvas, DotNetObjectReference)` — 起動
- `ariaWebRuntime.measure(canvas)` → `{ Width: double, Height: double }` — サイズ取得
- `ariaWebRuntime.renderFrame(canvas, WebRuntimeFrame)` — フレーム描画
- `ariaWebRuntime.showError(canvas, message)` — エラー表示
- `ariaWebRuntime.applyStorageOperation(BrowserStorageOperation)` → `string?` — ストレージ I/O

**JS → C#** (DotNetObjectReference):
- `HandlePointerDown(x, y, w, h, button)` — ポインタイベント
  - `button=0` → 左クリック
  - `button=2` → 右クリック
  - その他 (1=middle) → 無視

**データ形式の注意**:
- C# プロパティはパスカルケース (例: `CssX`, `LogicalWidth`)
- JS はキャメルケース (例: `cssX`, `logicalWidth`) とパスカルケースの両方を許容 (`??` フォールバック)
- これは Blazor JS 経由の JSON デシリアライズが元はキャメルケースで、C# 側の property names がパスカルケースであるための互換性維持

## PWA 機能

### Service Worker

- `service-worker.js` (開発用)
- `service-worker.published.js` (発行時に使用)
- キャッシュ戦略: cache-first (オフライン時はインストール時のキャッシュから応答)

### Manifest

`wwwroot/manifest.webmanifest` で PWA メタデータ (名前 / アイコン / 表示モード) を定義。
**注**: 本リポジトリでは未同梱。`index.html` からリンクされているが必要に応じて追加。

### オフライン対応

Service Worker インストール時に以下をキャッシュ:
- `./` (ルート HTML)
- `index.html`
- `manifest.webmanifest`

その他のアセット (Blazor WASM DLL, プリロードテキスト) は Service Worker の fetch ハンドラで初回はネットワーク、2回目以降は HTTP キャッシュ → Service Worker キャッシュの順にフォールバック。

## 拡張ガイド

### 新しいプラットフォームサービスを追加する

1. `Platform/I<Name>.cs` でインターフェース定義
2. `Platform/Raylib<Name>.cs` で Raylib 実装
3. `Platform/PlatformServices.cs` に `public static I<Name> <Name> { get; set; } = new Raylib<Name>();` を追加
4. (Web 対応する場合) `AriaEngine.Web/WebRuntimeHost.Boot` で Browser 実装に差し替え
5. 必要に応じて `AriaEngine.Web/Runtime/WebRuntimeHost.cs` に対応を追加

### 新しい Web ストレージを追加する

1. `AriaEngine.Web/Storage/BrowserStorageArea` enum に値を追加
2. ストアクラス (例: `IndexedDbSaveStore`) を作成
3. JS 側 (`aria-web-runtime.js`) の `applyStorageOperation` ディスパッチに case を追加
4. 必要に応じて `WebRuntimeHost` から呼び出し

### 新しいブラウザ入力イベントを追加する

1. `AriaEngine.Web/Input/BrowserInputMapper.cs` に変換ロジック追加
2. `aria-web-runtime.js` の `boot()` でイベントリスナー追加
3. `App.razor` に `[JSInvokable]` メソッド追加
4. `WebRuntimeHost` でハンドラー追加

## 関連ドキュメント

- [アーキテクチャ概要](overview.md) — 全体構成
- [VM](vm.md) — 仮想マシン
- [Parser](parser.md) — スクリプト解析
- [Rendering](rendering.md) — Raylib 描画
- [Tools](tools.md) — CLI ツール群
- [Scripting Pipeline](scripting-pipeline.md) — スクリプトパイプライン
- [Text Subsystem](text-subsystem.md) — 多言語サブシステム
