param(
    [string]$AuditPath = "artifacts/release/readiness/release-readiness-audit.json",
    [string]$OutputPath = ""
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $AuditPath -PathType Leaf)) {
    throw "Release readiness audit file not found: $AuditPath"
}

if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = [IO.Path]::ChangeExtension($AuditPath, ".md")
}

$audit = Get-Content -Raw -LiteralPath $AuditPath | ConvertFrom-Json
$checks = @($audit.checks)
$failed = @($checks | Where-Object { $_.passed -ne $true })
$status = if ($audit.ready -eq $true -and $failed.Count -eq 0) { "Ready" } else { "Not Ready" }

$lines = New-Object System.Collections.Generic.List[string]
$lines.Add("# Release Readiness Report")
$lines.Add("")
$lines.Add("Status: $status")
$lines.Add("")
$lines.Add("Objective: $($audit.objective)")
$lines.Add("")
$lines.Add("## Prompt-To-Artifact Checklist")
$lines.Add("")
$lines.Add("| Requirement | Status | Evidence | Message |")
$lines.Add("| --- | --- | --- | --- |")

foreach ($check in $checks) {
    $mark = if ($check.passed -eq $true) { "PASS" } else { "FAIL" }
    $name = ([string]$check.name).Replace("|", "\|")
    $evidence = ([string]$check.evidence).Replace("|", "\|")
    $message = ([string]$check.message).Replace("|", "\|")
    $lines.Add("| $name | $mark | ``$evidence`` | $message |")
}

$lines.Add("")
$lines.Add("## Remaining Blockers")
$lines.Add("")
if ($failed.Count -eq 0) {
    $lines.Add("- None")
} else {
    foreach ($check in $failed) {
        $lines.Add("- $($check.name): $($check.message) (``$($check.evidence)``)")
    }
}

$lines.Add("")
$lines.Add("## Completion Rule")
$lines.Add("")
$lines.Add("This report is complete only when every checklist row is PASS and the source audit has ``ready: true``.")

$parent = Split-Path -Parent $OutputPath
if (-not [string]::IsNullOrWhiteSpace($parent)) {
    New-Item -ItemType Directory -Force -Path $parent | Out-Null
}
$lines | Set-Content -Path $OutputPath -Encoding UTF8
Write-Host "Release readiness report written: $OutputPath"
