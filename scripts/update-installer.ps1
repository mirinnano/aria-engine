param(
    [Parameter(Mandatory = $true)]
    [string]$Patch,
    [string]$Version = "dev",
    [string]$OutputDir = "artifacts/update-installer",
    [string]$InstallerProject = "src/AriaInstaller/AriaInstaller.csproj"
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

if (-not (Test-Path $Patch)) { throw "Patch not found: $Patch" }

$name = "AriaUpdate-$Version"
$workDir = Join-Path $OutputDir $name
$zipPath = "$workDir.zip"
if (Test-Path $workDir) { Remove-Item -LiteralPath $workDir -Recurse -Force }
if (Test-Path $zipPath) { Remove-Item -LiteralPath $zipPath -Force }
New-Item -ItemType Directory -Force -Path $workDir | Out-Null

dotnet publish $InstallerProject -c Release -o $workDir /p:NuGetAudit=false
if ($LASTEXITCODE -ne 0) { throw "Aria Update GUI publish failed" }

Copy-Item -LiteralPath $Patch -Destination (Join-Path $workDir "update.patch") -Force
Compress-Archive -Path (Join-Path $workDir "*") -DestinationPath $zipPath -Force

Write-Host "Aria Update installer ready: $zipPath"
