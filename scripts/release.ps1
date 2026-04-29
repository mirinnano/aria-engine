param(
    [string]$Version = "dev",
    [string]$Runtime = "",
    [string]$Project = "src/AriaEngine/AriaEngine.csproj",
    [string]$InitScript = "init.aria",
    [string]$MainScript = "assets/scripts/main.aria",
    [switch]$SkipSmoke,
    [switch]$SkipDoctor,
    [switch]$SkipPackage,
    [switch]$NoZip
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

function Invoke-Step {
    param([string]$Name, [scriptblock]$Body)
    Write-Host ""
    Write-Host "== $Name =="
    & $Body
}

Initialize-AriaHostEnvironment

Invoke-Step "release metadata" {
    Write-Host "Version: $Version"
    $runtimeLabel = if ([string]::IsNullOrWhiteSpace($Runtime)) { "portable" } else { $Runtime }
    Write-Host "Runtime: $runtimeLabel"
}

if (-not $SkipDoctor) {
    Invoke-Step "doctor" {
        & ./scripts/doctor.ps1 -Project $Project -InitScript $InitScript -MainScript $MainScript
    }
}

if (-not $SkipSmoke) {
    Invoke-Step "smoke" {
        & ./scripts/smoke.ps1
    }
}

Invoke-Step "release build" {
    if ([string]::IsNullOrWhiteSpace($Runtime)) {
        & dotnet restore $Project /p:NuGetAudit=false
    } else {
        & dotnet restore $Project -r $Runtime /p:NuGetAudit=false
    }
    if ($LASTEXITCODE -ne 0) { throw "dotnet restore failed" }
    & dotnet build $Project -c Release --no-restore /p:NuGetAudit=false
    if ($LASTEXITCODE -ne 0) { throw "dotnet build failed" }
}

if (-not $SkipPackage) {
    Invoke-Step "package" {
        & ./scripts/package.ps1 -Project $Project -Version $Version -Runtime $Runtime -InitScript $InitScript -MainScript $MainScript -NoZip:$NoZip
    }
}

Write-Host ""
Write-Host "Release pipeline completed."
