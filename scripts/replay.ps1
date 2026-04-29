param(
    [string]$Spec = "artifacts/replay/replay.json",
    [string]$OutputDir = "artifacts/replay/results"
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $Spec)) {
    throw "Replay spec not found: $Spec"
}

New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null
$payload = Get-Content -LiteralPath $Spec -Raw | ConvertFrom-Json
$failures = @()
$results = @()

foreach ($case in $payload.cases) {
    $name = if ($case.name) { $case.name } else { "case-$($results.Count + 1)" }
    $cwd = if ($case.cwd) { $case.cwd } else { "." }
    $expectedExit = if ($null -ne $case.expectedExitCode) { [int]$case.expectedExitCode } else { 0 }
    $stdout = Join-Path $OutputDir "$name.stdout.txt"
    $stderr = Join-Path $OutputDir "$name.stderr.txt"

    Push-Location $cwd
    try {
        $cmd = [string]$case.command
        Write-Host "Replay: $name -> $cmd"
        try {
            Invoke-Expression $cmd *> $stdout
            $exitCode = if ($LASTEXITCODE -is [int]) { $LASTEXITCODE } else { 0 }
            Set-Content -Path $stderr -Value "" -Encoding UTF8
        }
        catch {
            $_ | Out-String | Set-Content -Path $stderr -Encoding UTF8
            $exitCode = 1
        }
    }
    finally {
        Pop-Location
    }

    $ok = $exitCode -eq $expectedExit
    if (-not $ok) { $failures += "Replay '$name' exit $exitCode expected $expectedExit" }
    $results += [ordered]@{ name = $name; command = $case.command; exitCode = $exitCode; expectedExitCode = $expectedExit; ok = $ok; stdout = $stdout; stderr = $stderr }
}

$results | ConvertTo-Json -Depth 5 | Set-Content -Path (Join-Path $OutputDir "replay-results.json") -Encoding UTF8
foreach ($failure in $failures) { Write-Error $failure -ErrorAction Continue }
if ($failures.Count -gt 0) {
    & ./scripts/diagnostics.ps1 -OutputDir "diagnostics" -Name "aria-replay-failure"
    throw "replay failed with $($failures.Count) failure(s)."
}

Write-Host "Replay passed. Results: $(Join-Path $OutputDir 'replay-results.json')"
