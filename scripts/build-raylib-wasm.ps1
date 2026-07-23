param(
    [string]$SourceDir = "artifacts/obj/raylib-5.5-src",
    [string]$OutputDir = "artifacts/native/raylib-5.5",
    [switch]$RefreshSource,
    [switch]$RequireLockedHash
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$lockPath = Join-Path $repoRoot "native/raylib-wasm/raylib-wasm.lock.json"
$lock = Get-Content -LiteralPath $lockPath -Raw | ConvertFrom-Json
$sourcePath = Join-Path $repoRoot $SourceDir
$outputPath = Join-Path $repoRoot $OutputDir
$objectPath = Join-Path $outputPath "obj"

foreach ($tool in @("git", "emcc", "emar")) {
    if (-not (Get-Command $tool -ErrorAction SilentlyContinue)) {
        throw "$tool is required. Activate Emscripten $($lock.emscriptenVersion) before running this script."
    }
}

$emccVersion = (& emcc --version | Select-Object -First 1)
if ($emccVersion -notmatch [regex]::Escape($lock.emscriptenVersion)) {
    throw "Emscripten version mismatch. Expected $($lock.emscriptenVersion), got: $emccVersion"
}

if ($RefreshSource -and (Test-Path -LiteralPath $sourcePath)) {
    Remove-Item -LiteralPath $sourcePath -Recurse -Force
}
if (-not (Test-Path -LiteralPath $sourcePath)) {
    New-Item -ItemType Directory -Force -Path (Split-Path $sourcePath -Parent) | Out-Null
    & git clone --depth 1 --branch $lock.raylibTag https://github.com/raysan5/raylib.git $sourcePath
    if ($LASTEXITCODE -ne 0) { throw "Failed to clone raylib $($lock.raylibTag)." }
}

$sourceCommit = (& git -C $sourcePath rev-parse HEAD).Trim()
if ($sourceCommit -ne $lock.raylibCommit) {
    throw "raylib source mismatch. Expected $($lock.raylibCommit), got $sourceCommit"
}

if (Test-Path -LiteralPath $outputPath) { Remove-Item -LiteralPath $outputPath -Recurse -Force }
New-Item -ItemType Directory -Force -Path $objectPath | Out-Null

$sourceRoot = Join-Path $sourcePath "src"
$modules = @("rcore", "rshapes", "rtextures", "rtext", "rmodels", "raudio")
$objects = @()
foreach ($module in $modules) {
    $object = Join-Path $objectPath "$module.o"
    $args = @("-c", "$module.c", "-o", $object) + @($lock.compileFlags) + @(
        "-I.",
        "-Iexternal/glfw/include"
    )
    Push-Location $sourceRoot
    try { & emcc @args }
    finally { Pop-Location }
    if ($LASTEXITCODE -ne 0) { throw "emcc failed while compiling $module.c" }
    $objects += $object
}

$webArchive = Join-Path $outputPath "libraylib.web.a"
& emar rcsD $webArchive @objects
if ($LASTEXITCODE -ne 0) { throw "emar failed while creating libraylib.web.a" }

# Raylib-cs imports the logical native library name "raylib". Keep the explicit
# web artifact and a linker-facing basename with the same bytes.
$linkArchive = Join-Path $outputPath "libraylib.a"
Copy-Item -LiteralPath $webArchive -Destination $linkArchive -Force

$archiveHash = (Get-FileHash -LiteralPath $webArchive -Algorithm SHA256).Hash.ToLowerInvariant()
$expectedHash = [string]$lock.expectedArchiveSha256
if (-not [string]::IsNullOrWhiteSpace($expectedHash) -and $archiveHash -ne $expectedHash.ToLowerInvariant()) {
    throw "raylib archive hash mismatch. Expected $expectedHash, got $archiveHash"
}
if ($RequireLockedHash -and [string]::IsNullOrWhiteSpace($expectedHash)) {
    throw "expectedArchiveSha256 is not locked in $lockPath (actual: $archiveHash)."
}

$buildRecord = [ordered]@{
    raylibVersion = $lock.raylibVersion
    raylibCommit = $sourceCommit
    emscriptenVersion = $lock.emscriptenVersion
    compileFlags = @($lock.compileFlags)
    archive = "libraylib.web.a"
    archiveSha256 = $archiveHash
}
$buildRecord | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $outputPath "build-record.json") -Encoding UTF8
Set-Content -LiteralPath (Join-Path $outputPath "libraylib.web.a.sha256") -Value "$archiveHash  libraylib.web.a" -Encoding ASCII

Write-Host "raylib WASM archive: $webArchive"
Write-Host "SHA-256: $archiveHash"
