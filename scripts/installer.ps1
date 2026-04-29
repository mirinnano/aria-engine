param(
    [string]$PackageDir = "artifacts/release/AriaEngine-dev-portable/app",
    [string]$Version = "dev",
    [string]$OutputDir = "artifacts/installer",
    [string]$InstallerProject = "src/AriaInstaller/AriaInstaller.csproj",
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

Initialize-AriaHostEnvironment

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

$name = "AriaEngine-$Version-installer"
$workDir = Join-Path $OutputDir $name
$zipPath = "$workDir.zip"
if (Test-Path $workDir) { Remove-Item -LiteralPath $workDir -Recurse -Force }
if (Test-Path $zipPath) { Remove-Item -LiteralPath $zipPath -Force }
New-Item -ItemType Directory -Force -Path $workDir | Out-Null

Copy-Item -LiteralPath $PackageDir -Destination (Join-Path $workDir "app") -Recurse -Force
Remove-InstallerPayloadExtras (Join-Path $workDir "app")

$guiDir = Join-Path $workDir "gui"
dotnet publish $InstallerProject -c Release -o $guiDir /p:NuGetAudit=false
if ($LASTEXITCODE -ne 0) { throw "GUI installer publish failed" }

if ($Sign) {
    & "$PSScriptRoot\sign.ps1" -FilePath (Join-Path $guiDir "AriaInstaller.exe")
}

Copy-Item -LiteralPath (Join-Path $workDir "app") -Destination (Join-Path $guiDir "app") -Recurse -Force

# Zip the contents of gui/ directory directly
Compress-Archive -Path (Join-Path $guiDir "*") -DestinationPath $zipPath -Force
Write-Host "Installer zip ready: $zipPath"
Write-Host "GUI installer: $(Join-Path $guiDir 'AriaInstaller.exe')"
