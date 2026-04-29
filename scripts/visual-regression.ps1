param(
    [string]$BaselineDir = "artifacts/visual-regression/baseline",
    [string]$CurrentDir = "artifacts/visual-regression/current",
    [switch]$PromoteCurrent
)

$ErrorActionPreference = "Stop"

New-Item -ItemType Directory -Force -Path $BaselineDir, $CurrentDir | Out-Null

if ($PromoteCurrent) {
    Copy-Item -Path (Join-Path $CurrentDir "*") -Destination $BaselineDir -Recurse -Force
    Write-Host "Current captures promoted to baseline: $BaselineDir"
    exit 0
}

$checklist = Join-Path $CurrentDir "capture-checklist.md"
@"
# Visual Regression Capture Checklist

- title screen
- chapter select
- ADV textbox
- NVL screen
- save menu
- load menu
- backlog menu
- right-click menu
- settings screen
- gallery screen

Save screenshots in this directory, then compare against:

$BaselineDir
"@ | Set-Content -Path $checklist -Encoding UTF8

Write-Host "Visual regression capture directory: $CurrentDir"
Write-Host "Checklist: $checklist"
