param(
    [string]$Project = "src/AriaEngine.Web/AriaEngine.Web.csproj",
    [string]$Version = "dev",
    [string]$Configuration = "Release",
    [string]$OutputRoot = "artifacts/web",
    [ValidateSet("Debug", "Demo", "Release")]
    [string]$Profile = "Demo",
    [switch]$SkipRestore
)

$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$projectPath = Join-Path $repoRoot $Project
$publishDir = Join-Path $repoRoot "artifacts/obj/web-publish/$Version"
$packageDir = Join-Path $repoRoot (Join-Path $OutputRoot "AriaEngine-$Version-web")
$distDir = Join-Path $repoRoot (Join-Path $OutputRoot "dist")
$zipPath = Join-Path $distDir "AriaEngine-$Version-web.zip"

if (-not (Test-Path -LiteralPath $projectPath -PathType Leaf)) {
    throw "Web project not found: $Project"
}

if (Test-Path -LiteralPath $publishDir) { Remove-Item -LiteralPath $publishDir -Recurse -Force }
if (Test-Path -LiteralPath $packageDir) { Remove-Item -LiteralPath $packageDir -Recurse -Force }
if (Test-Path -LiteralPath $zipPath) { Remove-Item -LiteralPath $zipPath -Force }
New-Item -ItemType Directory -Force -Path $publishDir, $packageDir, $distDir | Out-Null

$publishArgs = @("publish", $projectPath, "-c", $Configuration, "-o", $publishDir, "/p:NuGetAudit=false")
if ($SkipRestore) { $publishArgs += "--no-restore" }
& dotnet @publishArgs
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed for Web/PWA package."
}

$wwwroot = Join-Path $publishDir "wwwroot"
if (-not (Test-Path -LiteralPath $wwwroot -PathType Container)) {
    throw "Web publish output missing wwwroot: $wwwroot"
}

Copy-Item -Path (Join-Path $wwwroot "*") -Destination $packageDir -Recurse -Force

$required = @("index.html", "manifest.webmanifest", "service-worker.js", "_framework", "js/aria-web-runtime.js", "assets/web-text-assets.json")
$missing = @($required | Where-Object { -not (Test-Path -LiteralPath (Join-Path $packageDir $_)) })
if ($missing.Count -gt 0) {
    throw "Static Web/PWA package missing required output: $($missing -join ', ')"
}

$manifest = [ordered]@{
    version = $Version
    target = "web-pwa"
    profile = $Profile.ToLowerInvariant()
    project = $Project.Replace("\", "/")
    generatedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    packageDir = $packageDir
    zipPath = $zipPath
    files = $required
}
$manifest | ConvertTo-Json -Depth 6 | Set-Content -Path (Join-Path $packageDir "manifest.json") -Encoding UTF8

Get-ChildItem -Path $packageDir -Recurse -File | ForEach-Object {
    $relative = [IO.Path]::GetRelativePath($packageDir, $_.FullName).Replace("\", "/")
    "$relative`t$($_.Length)"
} | Set-Content -Path (Join-Path $packageDir "checksums.txt") -Encoding UTF8

Compress-Archive -Path (Join-Path $packageDir "*") -DestinationPath $zipPath -Force
Write-Host "Static Web/PWA package written: $packageDir"
Write-Host "Static Web/PWA package zip written: $zipPath"
