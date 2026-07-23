param(
    [string]$Project = "src/AriaEngine.Wasm/AriaEngine.Wasm.csproj",
    [string]$Version = "dev",
    [string]$Configuration = "Release",
    [string]$OutputRoot = "artifacts/web-wasm",
    [switch]$SkipRestore,
    [switch]$SkipRaylibBuild,
    [switch]$SkipFontSubset
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$lockPath = Join-Path $repoRoot "native/raylib-wasm/raylib-wasm.lock.json"
$lock = Get-Content -LiteralPath $lockPath -Raw | ConvertFrom-Json
$projectPath = Join-Path $repoRoot $Project
$publishDir = Join-Path $repoRoot "artifacts/obj/web-wasm-publish/$Version"
$packageDir = Join-Path $repoRoot (Join-Path $OutputRoot "AriaEngine-$Version-raylib-wasm")
$distDir = Join-Path $repoRoot (Join-Path $OutputRoot "dist")
$zipPath = Join-Path $distDir "AriaEngine-$Version-raylib-wasm.zip"
$raylibDir = Join-Path $repoRoot "artifacts/native/raylib-5.5"
$raylibArchive = Join-Path $raylibDir "libraylib.a"

if (-not $SkipRaylibBuild) {
    & (Join-Path $PSScriptRoot "build-raylib-wasm.ps1") -RequireLockedHash
    if ($LASTEXITCODE -ne 0) { throw "raylib WASM build failed." }
}
if (-not (Test-Path -LiteralPath $raylibArchive -PathType Leaf)) {
    throw "raylib WASM archive not found: $raylibArchive"
}

foreach ($path in @($publishDir, $packageDir)) {
    if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path -Recurse -Force }
}
if (Test-Path -LiteralPath $zipPath) { Remove-Item -LiteralPath $zipPath -Force }
New-Item -ItemType Directory -Force -Path $publishDir, $packageDir, $distDir | Out-Null

$publishArgs = @(
    "publish", $projectPath,
    "-c", $Configuration,
    "-o", $publishDir,
    "/p:NuGetAudit=false",
    "/p:WasmBuildNative=true",
    "/p:RaylibWasmArchive=$raylibArchive"
)
if ($SkipRestore) { $publishArgs += "--no-restore" }
& dotnet @publishArgs
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed for Raylib WASM." }

$wwwroot = Join-Path $publishDir "wwwroot"
if (-not (Test-Path -LiteralPath $wwwroot -PathType Container)) {
    throw "Raylib WASM publish output missing wwwroot: $wwwroot"
}

if (-not $SkipFontSubset) {
    if (-not (Get-Command pyftsubset -ErrorAction SilentlyContinue)) {
        throw "pyftsubset is required. Install fonttools==$($lock.fontToolsVersion)."
    }
    $fontToolsVersion = (& python -c "import fontTools; print(fontTools.__version__)").Trim()
    if ($fontToolsVersion -ne $lock.fontToolsVersion) {
        throw "fonttools version mismatch. Expected $($lock.fontToolsVersion), got $fontToolsVersion"
    }

    $glyphTextPath = Join-Path $publishDir "web-font-glyphs.txt"
    & python (Join-Path $PSScriptRoot "web-wasm-package.py") glyphs --repo-root $repoRoot --output $glyphTextPath
    if ($LASTEXITCODE -ne 0) { throw "Failed to collect web font glyphs." }

    $sourceFont = Join-Path $repoRoot "src/AriaEngine/assets/fonts/NotoSansJP-Regular.ttf"
    $webFont = Join-Path $wwwroot "assets/fonts/NotoSansJP-Regular.ttf"
    $subsetArgs = @(
        $sourceFont,
        "--text-file=$glyphTextPath",
        "--output-file=$webFont",
        "--layout-features=*",
        "--glyph-names",
        "--symbol-cmap",
        "--legacy-cmap",
        "--notdef-glyph",
        "--recommended-glyphs",
        "--name-IDs=*",
        "--name-legacy",
        "--name-languages=*"
    )
    & pyftsubset @subsetArgs
    if ($LASTEXITCODE -ne 0) { throw "Japanese web font subsetting failed." }
}

# Recalculate integrity after font subsetting, validate complete group coverage,
# and inject a versioned offline framework shell.
$packageMetadataJson = & python (Join-Path $PSScriptRoot "web-wasm-package.py") finalize --wwwroot $wwwroot
if ($LASTEXITCODE -ne 0) { throw "Failed to finalize Raylib WASM package metadata." }
$packageMetadata = $packageMetadataJson | ConvertFrom-Json

Copy-Item -Path (Join-Path $wwwroot "*") -Destination $packageDir -Recurse -Force
$required = @("index.html", "main.js", "service-worker.js", "manifest.webmanifest", "aria-web-assets.json", "_framework")
$missing = @($required | Where-Object { -not (Test-Path -LiteralPath (Join-Path $packageDir $_)) })
if ($missing.Count -gt 0) { throw "Raylib WASM package missing required output: $($missing -join ', ')" }

$packageManifest = [ordered]@{
    version = $Version
    target = "raylib-wasm-pwa-preview"
    project = $Project.Replace("\", "/")
    raylibVersion = $lock.raylibVersion
    emscriptenVersion = $lock.emscriptenVersion
    assetManifestSha256 = $packageMetadata.assetManifestSha256
    cacheVersion = $packageMetadata.cacheVersion
    generatedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
}
$packageManifest | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $packageDir "manifest.json") -Encoding UTF8

Get-ChildItem -Path $packageDir -Recurse -File | Sort-Object FullName | ForEach-Object {
    $relative = [IO.Path]::GetRelativePath($packageDir, $_.FullName).Replace("\", "/")
    $hash = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  $relative"
} | Set-Content -LiteralPath (Join-Path $packageDir "checksums.sha256") -Encoding ASCII

Compress-Archive -Path (Join-Path $packageDir "*") -DestinationPath $zipPath -Force
Write-Host "Raylib WASM package written: $packageDir"
Write-Host "Raylib WASM package zip written: $zipPath"
