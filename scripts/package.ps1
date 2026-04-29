param(
    [string]$Project = "src/AriaEngine/AriaEngine.csproj",
    [string]$Version = "dev",
    [string]$Runtime = "",
    [string]$Configuration = "Release",
    [string]$OutputRoot = "artifacts/release",
    [string]$InitScript = "init.aria",
    [string]$MainScript = "assets/scripts/main.aria",
    [switch]$SkipPublish,
    [switch]$NoZip,
    [switch]$Sign
)

$ErrorActionPreference = "Stop"

function Initialize-AriaHostEnvironment {
    $userProfile = if ([string]::IsNullOrWhiteSpace($env:USERPROFILE)) { "C:\Users\Default" } else { $env:USERPROFILE }
    if ([string]::IsNullOrWhiteSpace($env:HOMEDRIVE)) { $env:HOMEDRIVE = Split-Path -Qualifier $userProfile }
    if ([string]::IsNullOrWhiteSpace($env:HOMEPATH)) { $env:HOMEPATH = $userProfile.Substring($env:HOMEDRIVE.Length) }
    if ([string]::IsNullOrWhiteSpace($env:APPDATA)) { $env:APPDATA = Join-Path $userProfile "AppData\Roaming" }
    if ([string]::IsNullOrWhiteSpace($env:LOCALAPPDATA)) { $env:LOCALAPPDATA = Join-Path $userProfile "AppData\Local" }
    if ([string]::IsNullOrWhiteSpace($env:ProgramData)) { $env:ProgramData = "C:\ProgramData" }
    if ([string]::IsNullOrWhiteSpace($env:ALLUSERSPROFILE)) { $env:ALLUSERSPROFILE = "C:\ProgramData" }
    if ([string]::IsNullOrWhiteSpace($env:ProgramFiles)) { $env:ProgramFiles = "C:\Program Files" }
    if ([string]::IsNullOrWhiteSpace(${env:ProgramFiles(x86)})) { ${env:ProgramFiles(x86)} = "C:\Program Files (x86)" }
    if ([string]::IsNullOrWhiteSpace($env:CommonProgramFiles)) { $env:CommonProgramFiles = "C:\Program Files\Common Files" }
    if ([string]::IsNullOrWhiteSpace(${env:CommonProgramFiles(x86)})) { ${env:CommonProgramFiles(x86)} = "C:\Program Files (x86)\Common Files" }
}

function Invoke-Checked {
    param([string]$File, [string[]]$Arguments)
    Write-Host ("$File " + ($Arguments -join " "))
    & $File @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed: $File $($Arguments -join ' ')"
    }
}

function Copy-IfExists {
    param([string]$Path, [string]$Destination)
    if (Test-Path $Path) {
        Copy-Item -LiteralPath $Path -Destination $Destination -Recurse -Force
    }
}

Initialize-AriaHostEnvironment

$repoRoot = (Resolve-Path ".").Path
$projectPath = Resolve-Path $Project
$engineDir = Split-Path -Parent $projectPath
$runtimeLabel = if ([string]::IsNullOrWhiteSpace($Runtime)) { "portable" } else { $Runtime }
$releaseName = "AriaEngine-$Version-$runtimeLabel"
$releaseDir = Join-Path $OutputRoot $releaseName
$publishDir = Join-Path $releaseDir "app"
$distDir = Join-Path $releaseDir "dist"
$zipPath = Join-Path $distDir "$releaseName.zip"

if (Test-Path $releaseDir) {
    Remove-Item -LiteralPath $releaseDir -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $publishDir, $distDir | Out-Null

if (-not $SkipPublish) {
    if ([string]::IsNullOrWhiteSpace($Runtime)) {
        Invoke-Checked dotnet @("restore", $Project, "/p:NuGetAudit=false")
        Invoke-Checked dotnet @("publish", $Project, "-c", $Configuration, "--no-restore", "-o", $publishDir, "/p:AriaCompileOnPublish=false", "/p:NuGetAudit=false")
    } else {
        Invoke-Checked dotnet @("restore", $Project, "-r", $Runtime, "/p:NuGetAudit=false")
        Invoke-Checked dotnet @("publish", $Project, "-c", $Configuration, "-r", $Runtime, "--self-contained", "false", "--no-restore", "-o", $publishDir, "/p:AriaCompileOnPublish=false", "/p:NuGetAudit=false")
    }
}

Copy-IfExists (Join-Path $engineDir $InitScript) $publishDir
Copy-IfExists (Join-Path $engineDir "assets") (Join-Path $publishDir "assets")
Copy-IfExists (Join-Path $engineDir "chapters.json") $publishDir
Copy-IfExists (Join-Path $engineDir "characters.json") $publishDir
Copy-IfExists (Join-Path $engineDir "hints.txt") $publishDir

$configSource = Join-Path $engineDir "config.json"
if (Test-Path $configSource) {
    Copy-Item -LiteralPath $configSource -Destination (Join-Path $publishDir "config.template.json") -Force
}

if ($Sign) {
    $exes = Get-ChildItem -Path $publishDir -Filter "*.exe" -Recurse
    $dlls = Get-ChildItem -Path $publishDir -Filter "*.dll" -Recurse
    foreach ($file in $exes + $dlls) {
        & "$PSScriptRoot\sign.ps1" -FilePath $file.FullName
    }
}

$manifest = [ordered]@{
    name = "AriaEngine"
    version = $Version
    runtime = $runtimeLabel
    configuration = $Configuration
    generatedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    initScript = $InitScript
    mainScript = $MainScript
    productionRunArgs = @("--run-mode", "release", "--pak", "data.pak", "--compiled", "scripts/scripts.ariac")
    files = @()
}

$files = Get-ChildItem -Path $publishDir -Recurse -File | Sort-Object FullName
foreach ($file in $files) {
    $relative = [IO.Path]::GetRelativePath($publishDir, $file.FullName).Replace("\", "/")
    $hash = Get-FileHash -Algorithm SHA256 -LiteralPath $file.FullName
    $manifest.files += [ordered]@{
        path = $relative
        bytes = $file.Length
        sha256 = $hash.Hash.ToLowerInvariant()
    }
}

$manifestPath = Join-Path $publishDir "manifest.json"
$manifest | ConvertTo-Json -Depth 6 | Set-Content -Path $manifestPath -Encoding UTF8

$checksumPath = Join-Path $publishDir "checksums.txt"
Get-ChildItem -Path $publishDir -Recurse -File |
    Sort-Object FullName |
    ForEach-Object {
        $relative = [IO.Path]::GetRelativePath($publishDir, $_.FullName).Replace("\", "/")
        $hash = Get-FileHash -Algorithm SHA256 -LiteralPath $_.FullName
        "$($hash.Hash.ToLowerInvariant())  $relative"
    } | Set-Content -Path $checksumPath -Encoding ASCII

if (-not $NoZip) {
    if (Test-Path $zipPath) { Remove-Item -LiteralPath $zipPath -Force }
    Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $zipPath -Force
}

Write-Host "Package ready: $publishDir"
if (-not $NoZip) { Write-Host "Zip ready: $zipPath" }
