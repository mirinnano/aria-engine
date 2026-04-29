param(
    [Parameter(Mandatory = $true)]
    [string]$BasePak,
    [Parameter(Mandatory = $true)]
    [string]$NewPak,
    [Parameter(Mandatory = $true)]
    [string]$Out,
    [string]$EngineExe = "src/AriaEngine/bin/Release/net8.0/AriaEngine.exe",
    [string]$Key = $env:ARIA_PACK_KEY
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $EngineExe)) { throw "AriaEngine.exe not found: $EngineExe" }
if (-not (Test-Path $BasePak)) { throw "Base pak not found: $BasePak" }
if (-not (Test-Path $NewPak)) { throw "New pak not found: $NewPak" }

$args = @("aria-pack", "diff", "--base", $BasePak, "--new", $NewPak, "--out", $Out)
if (-not [string]::IsNullOrWhiteSpace($Key)) {
    $args += @("--key", $Key)
}

& $EngineExe @args
if ($LASTEXITCODE -ne 0) { throw "patch publish failed" }

Write-Host "Aria Update patch published: $Out"
