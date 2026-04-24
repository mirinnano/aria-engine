param(
    [string]$Project = "src/AriaEngine/AriaEngine.csproj",
    [string]$Runtime = "",
    [string]$OutDir = "artifacts/publish",
    [string]$InitScript = "init.aria",
    [string]$MainScript = "assets/scripts/main.aria"
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($env:ARIA_PACK_KEY)) {
    throw "ARIA_PACK_KEY environment variable is required."
}

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

function Invoke-DotNet {
    param([string[]]$DotnetArgs)
    Write-Host ("dotnet " + ($DotnetArgs -join " "))
    & dotnet @DotnetArgs
    if ($LASTEXITCODE -ne 0) {
        throw ("dotnet command failed: dotnet " + ($DotnetArgs -join " "))
    }
}

if ([string]::IsNullOrWhiteSpace($Runtime)) {
    Invoke-DotNet @("restore", $Project)
} else {
    Invoke-DotNet @("restore", $Project, "-r", $Runtime)
}
Invoke-DotNet @("build", $Project, "-c", "Release", "--no-restore")
if ([string]::IsNullOrWhiteSpace($Runtime)) {
    Invoke-DotNet @("publish", $Project, "-c", "Release", "--no-restore", "-o", $OutDir, "/p:AriaCompileOnPublish=false")
} else {
    Invoke-DotNet @("publish", $Project, "-c", "Release", "--no-restore", "-r", $Runtime, "--self-contained", "false", "-o", $OutDir, "/p:AriaCompileOnPublish=false")
}

Push-Location (Split-Path -Parent $Project)
Invoke-DotNet @("run", "-c", "Release", "--no-build", "--project", "AriaEngine.csproj", "--", "aria-compile", "--init", $InitScript, "--main", $MainScript, "--out", "../../$OutDir/build/scripts.ariac")
Invoke-DotNet @("run", "-c", "Release", "--no-build", "--project", "AriaEngine.csproj", "--", "aria-pack", "build", "--input", "assets", "--compiled", "../../$OutDir/build/scripts.ariac", "--output", "../../$OutDir/data.pak")
Pop-Location

Write-Host "CI/CD pipeline finished. Output: $OutDir"
