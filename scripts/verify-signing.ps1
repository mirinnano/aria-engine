param(
    [Parameter(Mandatory=$true)]
    [string]$Path,
    [string]$OutputPath = "",
    [switch]$RequireSigned
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $Path)) {
    throw "Signing target not found: $Path"
}

$target = (Resolve-Path $Path).Path
$files = if (Test-Path $target -PathType Container) {
    Get-ChildItem -LiteralPath $target -Recurse -File -Include *.exe,*.dll,*.msi
} else {
    Get-Item -LiteralPath $target
}

if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $baseDir = if (Test-Path $target -PathType Container) {
        $target
    } else {
        Split-Path -Parent $target
    }
    $OutputPath = Join-Path $baseDir "signature-audit.json"
}

$records = foreach ($file in $files) {
    $sig = Get-AuthenticodeSignature -FilePath $file.FullName
    [ordered]@{
        path = $file.FullName
        relativePath = if (Test-Path $target -PathType Container) { [IO.Path]::GetRelativePath($target, $file.FullName).Replace("\", "/") } else { $file.Name }
        status = $sig.Status.ToString()
        statusMessage = $sig.StatusMessage
        signer = $sig.SignerCertificate?.Subject
        thumbprint = $sig.SignerCertificate?.Thumbprint
    }
}

$unsigned = @($records | Where-Object { $_.status -ne "Valid" })
$payload = [ordered]@{
    generatedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    target = $target
    requireSigned = [bool]$RequireSigned
    total = @($records).Count
    signed = @($records | Where-Object { $_.status -eq "Valid" }).Count
    unsigned = $unsigned.Count
    files = @($records)
}

$json = $payload | ConvertTo-Json -Depth 8
Set-Content -Path $OutputPath -Value $json -Encoding UTF8
Write-Host "Signature audit written: $OutputPath"

if ($RequireSigned -and $unsigned.Count -gt 0) {
    $summary = ($unsigned | Select-Object -First 8 | ForEach-Object { "$($_.relativePath): $($_.status)" }) -join "; "
    throw "Unsigned artifacts found: $summary"
}
