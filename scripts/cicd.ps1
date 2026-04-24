param(
    [string]$Project = "src/AriaEngine/AriaEngine.csproj",
    [string]$Runtime = "",
    [string]$OutDir = "artifacts/publish",
    [string]$InitScript = "init.aria",
    [string]$MainScript = "assets/scripts/main.aria",
    [switch]$SkipSmoke,
    [switch]$SkipPackage
)

$ErrorActionPreference = "Stop"

# Guard rails for broken host environments where machine-wide paths are empty.
$userProfile = if ([string]::IsNullOrWhiteSpace($env:USERPROFILE)) { "C:\Users\Default" } else { $env:USERPROFILE }
if ([string]::IsNullOrWhiteSpace($env:APPDATA)) { $env:APPDATA = Join-Path $userProfile "AppData\Roaming" }
if ([string]::IsNullOrWhiteSpace($env:LOCALAPPDATA)) { $env:LOCALAPPDATA = Join-Path $userProfile "AppData\Local" }
if ([string]::IsNullOrWhiteSpace($env:ProgramData)) { $env:ProgramData = "C:\ProgramData" }
if ([string]::IsNullOrWhiteSpace($env:ALLUSERSPROFILE)) { $env:ALLUSERSPROFILE = "C:\ProgramData" }
if ([string]::IsNullOrWhiteSpace($env:ProgramFiles)) { $env:ProgramFiles = "C:\Program Files" }
if ([string]::IsNullOrWhiteSpace(${env:ProgramFiles(x86)})) { ${env:ProgramFiles(x86)} = "C:\Program Files (x86)" }
if ([string]::IsNullOrWhiteSpace($env:CommonProgramFiles)) { $env:CommonProgramFiles = "C:\Program Files\Common Files" }
if ([string]::IsNullOrWhiteSpace(${env:CommonProgramFiles(x86)})) { ${env:CommonProgramFiles(x86)} = "C:\Program Files (x86)\Common Files" }

if (-not $SkipSmoke -and (Test-Path "scripts/smoke.ps1")) {
    & ./scripts/smoke.ps1
}

function Invoke-DotNet {
    param([string[]]$DotnetArgs)
    Write-Host ("dotnet " + ($DotnetArgs -join " "))
    & dotnet @DotnetArgs
    if ($LASTEXITCODE -ne 0) {
        throw ("dotnet command failed: dotnet " + ($DotnetArgs -join " "))
    }
}

if ([string]::IsNullOrWhiteSpace($Runtime)) {
    Invoke-DotNet @("restore", $Project, "/p:NuGetAudit=false")
} else {
    Invoke-DotNet @("restore", $Project, "-r", $Runtime, "/p:NuGetAudit=false")
}
Invoke-DotNet @("build", $Project, "-c", "Release", "--no-restore", "/p:NuGetAudit=false")
if ([string]::IsNullOrWhiteSpace($Runtime)) {
    Invoke-DotNet @("publish", $Project, "-c", "Release", "--no-restore", "-o", $OutDir, "/p:AriaCompileOnPublish=false", "/p:NuGetAudit=false")
} else {
    Invoke-DotNet @("publish", $Project, "-c", "Release", "--no-restore", "-r", $Runtime, "--self-contained", "false", "-o", $OutDir, "/p:AriaCompileOnPublish=false", "/p:NuGetAudit=false")
}

if ($SkipPackage -or [string]::IsNullOrWhiteSpace($env:ARIA_PACK_KEY)) {
    Write-Host "ARIA_PACK_KEY is not set or packaging was skipped. Build/publish completed without encrypted Pak generation."
    Write-Host "CI pipeline finished. Output: $OutDir"
    exit 0
}

Push-Location (Split-Path -Parent $Project)
Invoke-DotNet @("run", "-c", "Release", "--no-build", "--project", "AriaEngine.csproj", "--", "aria-compile", "--init", $InitScript, "--main", $MainScript, "--out", "../../$OutDir/build/scripts.ariac")
Invoke-DotNet @("run", "-c", "Release", "--no-build", "--project", "AriaEngine.csproj", "--", "aria-pack", "build", "--input", "assets", "--compiled", "../../$OutDir/build/scripts.ariac", "--output", "../../$OutDir/data.pak")
Pop-Location

Write-Host "CI/CD pipeline finished. Output: $OutDir"
