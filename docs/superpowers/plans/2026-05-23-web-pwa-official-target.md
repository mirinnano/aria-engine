# Web/PWA Official Target Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Restore Web/PWA as an official production target while keeping Windows Native/NativeAOT intact.

**Architecture:** Keep the Windows Raylib path as the native runtime and add a separate static WebAssembly target under `src/AriaEngine.Web`. The Web target hosts the existing Aria core through a browser renderer/input/storage adapter layer, then proves parity with scripts that compare browser screenshots and QA manifests against native output.

**Tech Stack:** .NET 8, Blazor WebAssembly/static PWA, browser canvas APIs via JS interop, IndexedDB, OPFS, PowerShell release scripts, xUnit, Playwright/browser QA.

---

### Task 1: Readiness Gate

**Files:**
- Create: `scripts/web-pwa-readiness-audit.ps1`
- Create: `docs/release/web-pwa-goal-audit.md`
- Test: `src/AriaEngine.Tests/WebPwaReadinessAuditTests.cs`

- [ ] **Step 1: Write the failing readiness tests**

```csharp
File.Exists(Path.Combine(RepoRoot, "scripts", "web-pwa-readiness-audit.ps1")).Should().BeTrue();
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test src/AriaEngine.Tests/AriaEngine.Tests.csproj --no-restore --filter "FullyQualifiedName~WebPwaReadinessAuditTests" /p:NuGetAudit=false`

Expected: FAIL because `scripts/web-pwa-readiness-audit.ps1` does not exist.

- [ ] **Step 3: Add the audit script**

Create `scripts/web-pwa-readiness-audit.ps1` with checks for:

```powershell
"src/AriaEngine.Web/AriaEngine.Web.csproj"
"src/AriaEngine.Web/wwwroot/index.html"
"src/AriaEngine.Web/wwwroot/manifest.webmanifest"
"src/AriaEngine.Web/wwwroot/service-worker.js"
"scripts/package-web.ps1"
".github/workflows/aria-web-pages.yml"
"scripts/web-browser-qa.ps1"
"scripts/web-native-visual-compare.ps1"
"scripts/web-device-qa.ps1"
".github/workflows/aria-web-device-qa.yml"
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test src/AriaEngine.Tests/AriaEngine.Tests.csproj --no-restore --filter "FullyQualifiedName~WebPwaReadinessAuditTests" /p:NuGetAudit=false`

Expected: PASS; the audit itself exits non-zero in the current repo because Web/PWA artifacts are missing, but the test confirms the failure is explicit and evidence-backed.

### Task 2: WebAssembly Project Skeleton

**Files:**
- Create: `src/AriaEngine.Web/AriaEngine.Web.csproj`
- Create: `src/AriaEngine.Web/Program.cs`
- Create: `src/AriaEngine.Web/wwwroot/index.html`
- Create: `src/AriaEngine.Web/wwwroot/manifest.webmanifest`
- Create: `src/AriaEngine.Web/wwwroot/service-worker.js`
- Modify: `engine.slnx`
- Test: `src/AriaEngine.Tests/ReleasePipelineTests.cs`

- [ ] **Step 1: Write failing solution/project tests**

Assert that `engine.slnx` contains `src/AriaEngine.Web/AriaEngine.Web.csproj`, and that the project uses `Microsoft.NET.Sdk.BlazorWebAssembly`.

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test src/AriaEngine.Tests/AriaEngine.Tests.csproj --no-restore --filter "FullyQualifiedName~ReleasePipelineTests" /p:NuGetAudit=false`

Expected: FAIL because Web project is absent.

- [ ] **Step 3: Add minimal project**

Use `Microsoft.NET.Sdk.BlazorWebAssembly`, reference `..\AriaEngine\AriaEngine.csproj`, and keep output static. `Program.cs` should create the app root and register browser services without touching Raylib native startup.

- [ ] **Step 4: Build Web project**

Run: `dotnet build src/AriaEngine.Web/AriaEngine.Web.csproj -c Release /p:NuGetAudit=false`

Expected: PASS and static `wwwroot` assets copied to publish output.

### Task 3: Browser Coordinate and Input Parity

**Files:**
- Create: `src/AriaEngine.Web/Rendering/CanvasScaleMapper.cs`
- Create: `src/AriaEngine.Web/Input/BrowserInputMapper.cs`
- Test: `src/AriaEngine.Tests/PlatformBoundaryTests.cs`

- [ ] **Step 1: Write failing mapper tests**

Test 1280x720 logical coordinates against 1920x1080, 1366x768 letterbox, and mobile portrait canvas sizes. Assert pointer hit-tests map back to the same logical sprite rectangles as native.

- [ ] **Step 2: Implement `CanvasScaleMapper`**

Expose `Scale`, `OffsetX`, `OffsetY`, `MapLogicalToCss`, and `MapCssToLogical` using contain-fit 16:9 scaling.

- [ ] **Step 3: Implement `BrowserInputMapper`**

Convert mouse/touch/pointer coordinates into logical coordinates before button hit testing.

- [ ] **Step 4: Run tests**

Run: `dotnet test src/AriaEngine.Tests/AriaEngine.Tests.csproj --no-restore --filter "FullyQualifiedName~PlatformBoundaryTests" /p:NuGetAudit=false`

Expected: PASS with native/Web logical coordinate parity.

### Task 4: Browser Renderer and Font Loading

**Files:**
- Create: `src/AriaEngine.Web/Rendering/BrowserRenderer.cs`
- Create: `src/AriaEngine.Web/Rendering/BrowserFontLoader.cs`
- Test: `src/AriaEngine.Tests/PlatformBoundaryTests.cs`

- [ ] **Step 1: Write failing renderer contract tests**

Assert text draw commands preserve logical x/y, font size, wrap width, color, and line spacing from the native `SpriteRenderer` input model.

- [ ] **Step 2: Implement renderer command model**

Convert `Sprite` instances into browser draw commands without changing `GameState` or native renderer behavior.

- [ ] **Step 3: Implement font loader**

Load locale-specific fonts from packaged assets and expose the active font family to the browser renderer.

- [ ] **Step 4: Run tests**

Run: `dotnet test src/AriaEngine.Tests/AriaEngine.Tests.csproj --no-restore --filter "FullyQualifiedName~PlatformBoundaryTests" /p:NuGetAudit=false`

Expected: PASS with layout/font command parity.

### Task 5: Browser Storage

**Files:**
- Create: `src/AriaEngine.Web/Storage/IndexedDbSaveStore.cs`
- Create: `src/AriaEngine.Web/Storage/OpfsAssetStore.cs`
- Create: `src/AriaEngine.Web/Storage/SaveExportImport.cs`
- Test: `src/AriaEngine.Tests/SaveManagerTests.cs`

- [ ] **Step 1: Write failing storage contract tests**

Assert settings and lightweight saves target IndexedDB, large imported files target OPFS, and export/import round-trips save JSON.

- [ ] **Step 2: Implement IndexedDB save/settings store**

Expose async read/write/delete/list methods with stable key names.

- [ ] **Step 3: Implement OPFS asset store**

Expose async large-file read/write/open methods for browser local assets.

- [ ] **Step 4: Run tests**

Run: `dotnet test src/AriaEngine.Tests/AriaEngine.Tests.csproj --no-restore --filter "FullyQualifiedName~SaveManagerTests" /p:NuGetAudit=false`

Expected: PASS with storage contracts verified.

### Task 6: Web Packaging and GitHub Pages

**Files:**
- Create: `scripts/package-web.ps1`
- Create: `.github/workflows/aria-web-pages.yml`
- Modify: `.github/workflows/aria-cicd.yml`
- Test: `src/AriaEngine.Tests/ReleasePipelineTests.cs`

- [ ] **Step 1: Write failing release pipeline tests**

Assert package script outputs a static Web artifact with `index.html`, `_framework`, `manifest.webmanifest`, `service-worker.js`, and asset bundles.

- [ ] **Step 2: Implement package script**

Run `dotnet publish src/AriaEngine.Web/AriaEngine.Web.csproj -c Release`, copy static output into `artifacts/web/AriaEngine-<version>-web`, and write a manifest/checksums file.

- [ ] **Step 3: Add GitHub Pages workflow**

Build the static Web package and upload the published folder as Pages artifact.

- [ ] **Step 4: Run tests**

Run: `dotnet test src/AriaEngine.Tests/AriaEngine.Tests.csproj --no-restore --filter "FullyQualifiedName~ReleasePipelineTests" /p:NuGetAudit=false`

Expected: PASS with Web packaging and Pages wiring covered.

### Task 7: Browser QA and Visual Regression

**Files:**
- Create: `scripts/web-browser-qa.ps1`
- Create: `scripts/web-native-visual-compare.ps1`
- Create: `scripts/web-device-qa.ps1`
- Create: `.github/workflows/aria-web-device-qa.yml`
- Test: `src/AriaEngine.Tests/ReleasePipelineTests.cs`

- [ ] **Step 1: Write failing QA gate tests**

Assert scripts emit JSON manifests for browser smoke, native/Web screenshot comparison, and device/browser matrix runs.

- [ ] **Step 2: Implement browser smoke script**

Launch packaged Web output, verify first screen, font availability, click advance, right click/menu behavior, save/load, and no console errors.

- [ ] **Step 3: Implement visual comparison script**

Compare native and browser screenshots for title, menu, text, save/load, and scenario screens with explicit tolerance.

- [ ] **Step 4: Implement device QA workflow**

Gate Chrome, Edge, Safari, and mobile browser evidence with uploaded manifests.

- [ ] **Step 5: Run final verification**

Run:

```powershell
dotnet test src/AriaEngine.Tests/AriaEngine.Tests.csproj --no-restore /p:NuGetAudit=false
dotnet build --no-restore src/AriaEngine/AriaEngine.csproj -c Release /p:NuGetAudit=false
scripts/web-pwa-readiness-audit.ps1 -Root . -OutputPath artifacts/release/readiness/web-pwa-readiness-audit.json
```

Expected: tests/build pass. The Web/PWA audit passes only after Tasks 2-7 are complete.
