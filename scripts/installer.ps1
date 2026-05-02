param(
    [string]$PackageDir = "",
    [string]$Version = "dev",
    [string]$Runtime = "win-x64",
    [bool]$SelfContained = $false,
    [bool]$SingleFile = $true,
    [switch]$SkipRestore,
    [string]$OutputDir = "artifacts/installer",
    [string]$InstallerProject = "src/AriaInstaller/AriaInstaller.csproj",
    [switch]$Sign
)

$ErrorActionPreference = "Stop"

function Initialize-AriaHostEnvironment {
    $userProfile = if ([string]::IsNullOrWhiteSpace($env:USERPROFILE)) { "C:\Users\Default" } else { $env:USERPROFILE }
    if ([string]::IsNullOrWhiteSpace($env:SystemRoot)) { $env:SystemRoot = "C:\WINDOWS" }
    if ([string]::IsNullOrWhiteSpace($env:windir)) { $env:windir = $env:SystemRoot }
    if ([string]::IsNullOrWhiteSpace($env:ComSpec)) { $env:ComSpec = Join-Path $env:SystemRoot "system32\cmd.exe" }
    if ([string]::IsNullOrWhiteSpace($env:HOMEDRIVE)) { $env:HOMEDRIVE = Split-Path -Qualifier $userProfile }
    if ([string]::IsNullOrWhiteSpace($env:HOMEPATH)) { $env:HOMEPATH = $userProfile.Substring($env:HOMEDRIVE.Length) }
    if ([string]::IsNullOrWhiteSpace($env:APPDATA)) { $env:APPDATA = Join-Path $userProfile "AppData\Roaming" }
    if ([string]::IsNullOrWhiteSpace($env:LOCALAPPDATA)) { $env:LOCALAPPDATA = Join-Path $userProfile "AppData\Local" }
    if ([string]::IsNullOrWhiteSpace($env:ProgramData)) { $env:ProgramData = "C:\ProgramData" }
    if ([string]::IsNullOrWhiteSpace($env:ALLUSERSPROFILE)) { $env:ALLUSERSPROFILE = "C:\ProgramData" }
    if ([string]::IsNullOrWhiteSpace($env:ProgramFiles)) { $env:ProgramFiles = "C:\Program Files" }
    if ([string]::IsNullOrWhiteSpace($env:ProgramW6432)) { $env:ProgramW6432 = "C:\Program Files" }
    if ([string]::IsNullOrWhiteSpace(${env:ProgramFiles(x86)})) { ${env:ProgramFiles(x86)} = "C:\Program Files (x86)" }
    if ([string]::IsNullOrWhiteSpace($env:CommonProgramFiles)) { $env:CommonProgramFiles = "C:\Program Files\Common Files" }
    if ([string]::IsNullOrWhiteSpace(${env:CommonProgramFiles(x86)})) { ${env:CommonProgramFiles(x86)} = "C:\Program Files (x86)\Common Files" }
    if ([string]::IsNullOrWhiteSpace($env:DOTNET_CLI_HOME)) { $env:DOTNET_CLI_HOME = $userProfile }
    if ([string]::IsNullOrWhiteSpace($env:NUGET_PACKAGES)) { $env:NUGET_PACKAGES = Join-Path $userProfile ".nuget\packages" }
    $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
}

Initialize-AriaHostEnvironment

if ([string]::IsNullOrWhiteSpace($PackageDir)) {
    $packageArgs = @{
        Version = $Version
        Runtime = $Runtime
        SelfContained = $SelfContained
        SingleFile = $SingleFile
        NoZip = $true
    }
    if ($SkipRestore) { $packageArgs.SkipRestore = $true }
    & "$PSScriptRoot\package.ps1" @packageArgs
    if ($LASTEXITCODE -ne 0) { throw "Package generation failed" }
    $PackageDir = "artifacts/release/AriaEngine-$Version-$Runtime/app"
}

if (-not (Test-Path $PackageDir)) {
    throw "Package directory not found: $PackageDir"
}

function Remove-InstallerPayloadExtras {
    param([string]$AppDir)

    $patterns = @(
        "*.pdb",
        "*.xml",
        "*.log",
        "aria_error.log",
        "aria_error_ai.txt",
        "aria_error_ai.json",
        "run.log",
        "temp_test.aria",
        "temp_test2.aria",
        "init_nscr_test.aria",
        "init.core.aria.example",
        "main.sample.aria",
        "00_converted.aria",
        "debug_ui.aria",
        "save_ui.aria",
        "load_ui.aria",
        "backlog_ui.aria"
    )

    foreach ($pattern in $patterns) {
        Get-ChildItem -Path $AppDir -Recurse -Force -File -Filter $pattern -ErrorAction SilentlyContinue |
            Remove-Item -Force
    }

    foreach ($dirName in @("saves", "build", "diagnostics", ".tmp")) {
        Get-ChildItem -Path $AppDir -Recurse -Force -Directory -Filter $dirName -ErrorAction SilentlyContinue |
            Remove-Item -Recurse -Force
    }
}

$name = "AriaEngine-$Version-$Runtime-installer"
$workDir = Join-Path $OutputDir $name
$zipPath = "$workDir.zip"
if (Test-Path $workDir) { Remove-Item -LiteralPath $workDir -Recurse -Force }
if (Test-Path $zipPath) { Remove-Item -LiteralPath $zipPath -Force }
New-Item -ItemType Directory -Force -Path $workDir | Out-Null

$guiDir = Join-Path $workDir "gui"
$guiPublishDir = Join-Path ([IO.Path]::GetTempPath()) ("aria-installer-publish-" + [Guid]::NewGuid().ToString("N"))
$restoreArgs = @()
if ($SkipRestore) { $restoreArgs += "--no-restore" }
try {
    dotnet publish $InstallerProject -c Release -r $Runtime --self-contained $SelfContained @restoreArgs -o $guiPublishDir /p:NuGetAudit=false /p:PublishSingleFile=$($SingleFile.ToString().ToLowerInvariant()) /p:IncludeNativeLibrariesForSelfExtract=true /p:DebugType=none /p:DebugSymbols=false
    if ($LASTEXITCODE -ne 0) { throw "GUI installer publish failed" }
    Copy-Item -LiteralPath $guiPublishDir -Destination $guiDir -Recurse -Force
    Copy-Item -LiteralPath $PackageDir -Destination (Join-Path $guiDir "app") -Recurse -Force
    Remove-InstallerPayloadExtras (Join-Path $guiDir "app")
} finally {
    if (Test-Path $guiPublishDir) {
        Remove-Item -LiteralPath $guiPublishDir -Recurse -Force -ErrorAction SilentlyContinue
    }
}

if ($Sign) {
    & "$PSScriptRoot\sign.ps1" -FilePath (Join-Path $guiDir "AriaInstaller.exe")
}

# Zip the contents of gui/ directory directly
Compress-Archive -Path (Join-Path $guiDir "*") -DestinationPath $zipPath -Force
Write-Host "Installer zip ready: $zipPath"
Write-Host "GUI installer: $(Join-Path $guiDir 'AriaInstaller.exe')"
