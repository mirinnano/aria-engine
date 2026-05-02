param(
    [string]$Project = "src/AriaEngine/AriaEngine.csproj",
    [string]$EngineDir = "src/AriaEngine",
    [string]$InitScript = "init.aria",
    [string]$MainScript = "assets/scripts/main.aria",
    [string]$SaveDir = ".tmp/release-save-validation",
    [switch]$Strict,
    [switch]$SkipLint,
    [switch]$SkipSaveValidate
)

$ErrorActionPreference = "Stop"
$issues = New-Object System.Collections.Generic.List[string]
$warnings = New-Object System.Collections.Generic.List[string]

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

function Add-Issue([string]$Message) { $script:issues.Add($Message) | Out-Null }
function Add-Warning([string]$Message) { $script:warnings.Add($Message) | Out-Null }
function Add-GateWarning([string]$Message) {
    if ($Strict) { Add-Issue $Message } else { Add-Warning $Message }
}

function Invoke-Checked {
    param([string]$File, [string[]]$Arguments, [switch]$WarningOnly, [switch]$AdvisoryOnly)
    Write-Host ("$File " + ($Arguments -join " "))
    & $File @Arguments
    if ($LASTEXITCODE -ne 0) {
        if ($AdvisoryOnly) {
            Add-Warning "Advisory command returned ${LASTEXITCODE}: $File $($Arguments -join ' ')"
        } elseif ($WarningOnly) {
            Add-GateWarning "Command returned ${LASTEXITCODE}: $File $($Arguments -join ' ')"
        } else {
            Add-Issue "Command returned ${LASTEXITCODE}: $File $($Arguments -join ' ')"
        }
    }
}

Initialize-AriaHostEnvironment

$enginePath = Resolve-Path $EngineDir
$initPath = Join-Path $enginePath $InitScript
$mainPath = Join-Path $enginePath $MainScript

foreach ($required in @($initPath, $mainPath, (Join-Path $enginePath "assets"), (Join-Path $enginePath "config.json"))) {
    if (-not (Test-Path $required)) {
        Add-Issue "Missing required path: $required"
    }
}

Invoke-Checked dotnet @("build", $Project, "-c", "Release", "--no-restore", "/p:NuGetAudit=false")

Push-Location $enginePath
try {
    Invoke-Checked dotnet @("run", "-c", "Release", "--no-build", "--project", "AriaEngine.csproj", "--", "aria-compile", "--init", $InitScript, "--main", $MainScript, "--out", "build/doctor-check.ariac")

    if (-not $SkipLint) {
        $scripts = Get-ChildItem -Path "assets/scripts" -Filter "*.aria" -Recurse -File | ForEach-Object { $_.FullName }
        if ($scripts.Count -gt 0) {
            Write-Host "aria-lint is advisory for include-based projects; aria-compile is the blocking script gate."
            Invoke-Checked dotnet (@("run", "-c", "Release", "--no-build", "--project", "AriaEngine.csproj", "--", "aria-lint") + $scripts) -AdvisoryOnly
        }
    }

    if (-not $SkipSaveValidate) {
        New-Item -ItemType Directory -Force -Path $SaveDir | Out-Null
        Invoke-Checked dotnet @("run", "-c", "Release", "--no-build", "--project", "AriaEngine.csproj", "--", "aria-save", "--dir", $SaveDir, "migrate")
        Invoke-Checked dotnet @("run", "-c", "Release", "--no-build", "--project", "AriaEngine.csproj", "--", "aria-save", "--dir", $SaveDir, "validate")
    }

    $scriptText = Get-ChildItem -Path "assets/scripts" -Filter "*.aria" -Recurse -File | ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }
    $assetMatches = [regex]::Matches(($scriptText -join "`n"), '"(assets/[^"]+)"')
    foreach ($match in $assetMatches) {
        $assetPath = $match.Groups[1].Value
        if (-not (Test-Path $assetPath)) {
            Add-GateWarning "Referenced asset not found: $assetPath"
        }
    }
}
finally {
    Pop-Location
}

foreach ($warning in $warnings) { Write-Warning $warning }
foreach ($issue in $issues) { Write-Error $issue -ErrorAction Continue }

if ($issues.Count -gt 0) {
    throw "aria doctor failed with $($issues.Count) issue(s)."
}

Write-Host "aria doctor passed with $($warnings.Count) warning(s)."
