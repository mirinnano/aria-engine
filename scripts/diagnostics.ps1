param(
    [string]$OutputDir = "diagnostics",
    [string]$Name = ""
)

$ErrorActionPreference = "Stop"

New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null
$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$baseName = if ([string]::IsNullOrWhiteSpace($Name)) { "aria-diagnostics-$stamp" } else { $Name }
$workDir = Join-Path $OutputDir $baseName
$zipPath = "$workDir.zip"

if (Test-Path $workDir) { Remove-Item -LiteralPath $workDir -Recurse -Force }
if (Test-Path $zipPath) { Remove-Item -LiteralPath $zipPath -Force }
New-Item -ItemType Directory -Force -Path $workDir | Out-Null

$summary = [ordered]@{
    generatedAt = (Get-Date).ToString("o")
    cwd = (Get-Location).Path
    os = [System.Environment]::OSVersion.ToString()
    dotnet = (& dotnet --version 2>$null)
    files = @()
}

foreach ($path in @("aria_error.log", "aria_error_ai.txt", "aria_error_ai.json", "config.json", "chapters.json")) {
    if (Test-Path $path) {
        Copy-Item -LiteralPath $path -Destination $workDir -Force
        $summary.files += $path
    }
}

foreach ($dir in @("saves", "artifacts/release")) {
    if (Test-Path $dir) {
        Copy-Item -LiteralPath $dir -Destination (Join-Path $workDir (Split-Path -Leaf $dir)) -Recurse -Force
        $summary.files += $dir
    }
}

$summary | ConvertTo-Json -Depth 4 | Set-Content -Path (Join-Path $workDir "summary.json") -Encoding UTF8
Compress-Archive -Path (Join-Path $workDir "*") -DestinationPath $zipPath -Force
Remove-Item -LiteralPath $workDir -Recurse -Force

Write-Host "Diagnostics zip ready: $zipPath"
