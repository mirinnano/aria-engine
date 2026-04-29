param(
    [Parameter(Mandatory=$true)]
    [string]$FilePath,
    
    [string]$CertName = "AriaEngineDev",
    [string]$CertStorePath = "Cert:\CurrentUser\My"
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $FilePath)) {
    throw "File not found: $FilePath"
}

Write-Host "Checking for existing certificate: $CertName"
$cert = Get-ChildItem -Path $CertStorePath | Where-Object { $_.Subject -match "CN=$CertName" } | Select-Object -First 1

if (-not $cert) {
    Write-Host "Generating new self-signed certificate: $CertName"
    $cert = New-SelfSignedCertificate -Subject "CN=$CertName" -Type CodeSigningCert -CertStoreLocation $CertStorePath
    Write-Host "Created certificate with thumbprint: $($cert.Thumbprint)"
    
    # Note: To avoid untrusted warnings locally, the cert should ideally be in Root as well.
    # But for a simple self-signed flow, we just use it to sign.
} else {
    Write-Host "Found existing certificate: $($cert.Thumbprint)"
}

Write-Host "Signing $FilePath..."
$timestampServer = "http://timestamp.digicert.com"
$result = Set-AuthenticodeSignature -FilePath $FilePath -Certificate $cert -TimestampServer $timestampServer

if ($result.Status -ne 'Valid') {
    if ($result.Status -eq 'UnknownError') {
        # Sometimes timestamp server fails, try without it
        Write-Host "Timestamping failed, trying without timestamp..."
        $result = Set-AuthenticodeSignature -FilePath $FilePath -Certificate $cert
    }
}

if ($result.Status -ne 'Valid') {
    Write-Warning "Signature status: $($result.Status). The file was signed but might not be fully trusted (expected for self-signed certs)."
} else {
    Write-Host "Successfully signed $FilePath."
}
