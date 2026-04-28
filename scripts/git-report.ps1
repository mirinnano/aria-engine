param(
    [switch]$IncludeDiffStat
)

$ErrorActionPreference = "Stop"

function Invoke-Git {
    param([string[]]$GitArgs)

    & git @GitArgs
    if ($LASTEXITCODE -ne 0) {
        throw ("git command failed: git " + ($GitArgs -join " "))
    }
}

Write-Host "== branch =="
Invoke-Git @("branch", "--show-current")

Write-Host ""
Write-Host "== status =="
Invoke-Git @("status", "--short")

Write-Host ""
Write-Host "== latest commit =="
Invoke-Git @("log", "-1", "--oneline")

Write-Host ""
Write-Host "== remotes =="
Invoke-Git @("remote", "-v")

if ($IncludeDiffStat) {
    Write-Host ""
    Write-Host "== diff stat =="
    Invoke-Git @("diff", "--stat")
}
