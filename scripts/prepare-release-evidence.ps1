param(
    [string]$ReleasePackageDir = "artifacts/release/AriaEngine-v1.0.0-win-x64/app",
    [string]$NativePackageDir = "",
    [string]$WebPackageDir = "artifacts/web/AriaEngine-v1.0.0-web",
    [string]$WebDeviceQaManifest = "artifacts/release/readiness/web-device-qa-manifest.json",
    [string]$WebVisualCompareManifest = "artifacts/release/readiness/web-native-visual-compare.json",
    [string]$SignatureAudit = "",
    [string]$OutputPath = "artifacts/release/readiness/release-readiness-audit.json",
    [string]$ReportPath = ""
)

$ErrorActionPreference = "Stop"

function Ensure-ParentDirectory {
    param([string]$Path)

    $parent = Split-Path -Parent $Path
    if (-not [string]::IsNullOrWhiteSpace($parent)) {
        New-Item -ItemType Directory -Force -Path $parent | Out-Null
    }
}

if ([string]::IsNullOrWhiteSpace($SignatureAudit)) {
    $SignatureAudit = Join-Path $ReleasePackageDir "signature-audit.json"
}

if ([string]::IsNullOrWhiteSpace($ReportPath)) {
    $ReportPath = [IO.Path]::ChangeExtension($OutputPath, ".md")
}

Ensure-ParentDirectory $SignatureAudit
if (Test-Path -LiteralPath $ReleasePackageDir -PathType Container) {
    & "$PSScriptRoot\verify-signing.ps1" -Path $ReleasePackageDir -OutputPath $SignatureAudit
} else {
    Write-Host "Skipping signing audit generation because release package is missing: $ReleasePackageDir"
}

$auditParams = @{
    ReleasePackageDir = $ReleasePackageDir
    NativePackageDir = $NativePackageDir
    WebPackageDir = $WebPackageDir
    WebDeviceQaManifest = $WebDeviceQaManifest
    WebVisualCompareManifest = $WebVisualCompareManifest
    SignatureAudit = $SignatureAudit
    OutputPath = $OutputPath
}

$auditFailed = $false
$auditError = $null
try {
    & "$PSScriptRoot\release-readiness-audit.ps1" @auditParams
} catch {
    $auditFailed = $true
    $auditError = $_
}

if (Test-Path -LiteralPath $OutputPath -PathType Leaf) {
    & "$PSScriptRoot\release-readiness-report.ps1" -AuditPath $OutputPath -OutputPath $ReportPath
}

if ($auditFailed) {
    throw $auditError
}
