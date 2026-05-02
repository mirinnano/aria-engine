param(
    [Parameter(Mandatory=$true)]
    [string]$FilePath,
    
    [string]$CertName = "AriaEngineDev",
    [string]$CertStorePath = "Cert:\CurrentUser\My",
    [string]$PfxBase64 = $env:WINDOWS_CODESIGN_PFX_BASE64,
    [string]$PfxPassword = $env:WINDOWS_CODESIGN_PFX_PASSWORD
)

$ErrorActionPreference = "Stop"

function Find-AriaSignTool {
    $cmd = Get-Command signtool.exe -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($cmd) { return $cmd.Source }

    $roots = @(
        "C:\Program Files (x86)\Windows Kits",
        "C:\Program Files\Windows Kits"
    )
    foreach ($root in $roots) {
        if (-not (Test-Path $root)) { continue }
        $candidate = Get-ChildItem -Path $root -Recurse -Filter signtool.exe -ErrorAction SilentlyContinue |
            Where-Object { $_.FullName -match "\\x64\\signtool\.exe$" -or $_.FullName -match "\\App Certification Kit\\signtool\.exe$" } |
            Sort-Object FullName -Descending |
            Select-Object -First 1
        if ($candidate) { return $candidate.FullName }
    }

    return $null
}

function New-AriaCodeSigningPfx {
    param(
        [string]$SubjectName,
        [string]$PfxPath,
        [string]$Password
    )

    $rsa = [System.Security.Cryptography.RSA]::Create(3072)
    try {
        $subject = [System.Security.Cryptography.X509Certificates.X500DistinguishedName]::new("CN=$SubjectName")
        $request = [System.Security.Cryptography.X509Certificates.CertificateRequest]::new(
            $subject,
            $rsa,
            [System.Security.Cryptography.HashAlgorithmName]::SHA256,
            [System.Security.Cryptography.RSASignaturePadding]::Pkcs1)

        $request.CertificateExtensions.Add(
            [System.Security.Cryptography.X509Certificates.X509KeyUsageExtension]::new(
                [System.Security.Cryptography.X509Certificates.X509KeyUsageFlags]::DigitalSignature,
                $true))

        $eku = [System.Security.Cryptography.OidCollection]::new()
        [void]$eku.Add([System.Security.Cryptography.Oid]::new("1.3.6.1.5.5.7.3.3", "Code Signing"))
        $request.CertificateExtensions.Add(
            [System.Security.Cryptography.X509Certificates.X509EnhancedKeyUsageExtension]::new($eku, $true))

        $request.CertificateExtensions.Add(
            [System.Security.Cryptography.X509Certificates.X509BasicConstraintsExtension]::new($false, $false, 0, $true))

        $created = $request.CreateSelfSigned(
            [DateTimeOffset]::UtcNow.AddMinutes(-5),
            [DateTimeOffset]::UtcNow.AddYears(3))

        $pfx = $created.Export([System.Security.Cryptography.X509Certificates.X509ContentType]::Pfx, $Password)
        [IO.File]::WriteAllBytes($PfxPath, $pfx)
    } finally {
        $rsa.Dispose()
    }
}

function Invoke-AriaSignTool {
    param(
        [string]$SignTool,
        [string]$PfxPath,
        [string]$Password,
        [string]$TargetPath
    )

    Write-Host "Signing $TargetPath with signtool..."
    & $SignTool sign /f $PfxPath /p $Password /fd SHA256 /td SHA256 /tr http://timestamp.digicert.com $TargetPath
    if ($LASTEXITCODE -eq 0) { return }

    Write-Host "Timestamping failed, trying signtool without timestamp..."
    & $SignTool sign /f $PfxPath /p $Password /fd SHA256 $TargetPath
    if ($LASTEXITCODE -ne 0) {
        throw "signtool failed for $TargetPath"
    }
}

function New-AriaCodeSigningCertificate {
    param(
        [string]$SubjectName,
        [string]$StorePath
    )

    $certStore = $StorePath
    if (-not $certStore.StartsWith("Cert:\CurrentUser\My", [StringComparison]::OrdinalIgnoreCase)) {
        throw ".NET self-signed fallback supports Cert:\CurrentUser\My only. Current path: $StorePath"
    }

    $rsa = [System.Security.Cryptography.RSA]::Create(3072)
    try {
        $subject = [System.Security.Cryptography.X509Certificates.X500DistinguishedName]::new("CN=$SubjectName")
        $request = [System.Security.Cryptography.X509Certificates.CertificateRequest]::new(
            $subject,
            $rsa,
            [System.Security.Cryptography.HashAlgorithmName]::SHA256,
            [System.Security.Cryptography.RSASignaturePadding]::Pkcs1)

        $request.CertificateExtensions.Add(
            [System.Security.Cryptography.X509Certificates.X509KeyUsageExtension]::new(
                [System.Security.Cryptography.X509Certificates.X509KeyUsageFlags]::DigitalSignature,
                $true))

        $eku = [System.Security.Cryptography.OidCollection]::new()
        [void]$eku.Add([System.Security.Cryptography.Oid]::new("1.3.6.1.5.5.7.3.3", "Code Signing"))
        $request.CertificateExtensions.Add(
            [System.Security.Cryptography.X509Certificates.X509EnhancedKeyUsageExtension]::new($eku, $true))

        $request.CertificateExtensions.Add(
            [System.Security.Cryptography.X509Certificates.X509BasicConstraintsExtension]::new($false, $false, 0, $true))

        $created = $request.CreateSelfSigned(
            [DateTimeOffset]::UtcNow.AddMinutes(-5),
            [DateTimeOffset]::UtcNow.AddYears(3))

        $store = [System.Security.Cryptography.X509Certificates.X509Store]::new(
            [System.Security.Cryptography.X509Certificates.StoreName]::My,
            [System.Security.Cryptography.X509Certificates.StoreLocation]::CurrentUser)
        try {
            $store.Open([System.Security.Cryptography.X509Certificates.OpenFlags]::ReadWrite)
            $store.Add($created)
        } finally {
            $store.Close()
        }

        return $created
    } finally {
        $rsa.Dispose()
    }
}

if (-not (Test-Path $FilePath)) {
    throw "File not found: $FilePath"
}

$signTool = Find-AriaSignTool
if ($signTool) {
    $tempPfxPath = Join-Path ([IO.Path]::GetTempPath()) ("aria-codesign-" + [Guid]::NewGuid().ToString("N") + ".pfx")
    $tempPassword = if ([string]::IsNullOrWhiteSpace($PfxPassword)) { [Guid]::NewGuid().ToString("N") } else { $PfxPassword }
    try {
        if (-not [string]::IsNullOrWhiteSpace($PfxBase64)) {
            [IO.File]::WriteAllBytes($tempPfxPath, [Convert]::FromBase64String($PfxBase64))
        } else {
            Write-Host "Generating local self-signed PFX: $CertName"
            New-AriaCodeSigningPfx -SubjectName $CertName -PfxPath $tempPfxPath -Password $tempPassword
        }
        Invoke-AriaSignTool -SignTool $signTool -PfxPath $tempPfxPath -Password $tempPassword -TargetPath $FilePath
        return
    } finally {
        if (Test-Path $tempPfxPath) { Remove-Item -LiteralPath $tempPfxPath -Force -ErrorAction SilentlyContinue }
    }
}

if (-not [string]::IsNullOrWhiteSpace($PfxBase64)) {
    if ([string]::IsNullOrWhiteSpace($PfxPassword)) {
        throw "PFX password is required when PfxBase64 is provided."
    }

    $pfxPath = Join-Path ([IO.Path]::GetTempPath()) ("aria-codesign-" + [Guid]::NewGuid().ToString("N") + ".pfx")
    try {
        [IO.File]::WriteAllBytes($pfxPath, [Convert]::FromBase64String($PfxBase64))
        $securePassword = ConvertTo-SecureString $PfxPassword -AsPlainText -Force
        $imported = Import-PfxCertificate -FilePath $pfxPath -CertStoreLocation $CertStorePath -Password $securePassword
        $cert = $imported | Select-Object -First 1
        if (-not $cert) { throw "No certificate was imported from the supplied PFX." }
        Write-Host "Imported signing certificate: $($cert.Thumbprint)"
    } finally {
        if (Test-Path $pfxPath) { Remove-Item -LiteralPath $pfxPath -Force -ErrorAction SilentlyContinue }
    }
} else {
    Write-Host "Checking for existing certificate: $CertName"
    $cert = Get-ChildItem -Path $CertStorePath | Where-Object { $_.Subject -match "CN=$CertName" } | Select-Object -First 1

    if (-not $cert) {
        if (-not (Get-Command New-SelfSignedCertificate -ErrorAction SilentlyContinue)) {
            Write-Host "New-SelfSignedCertificate is unavailable; generating certificate with .NET fallback: $CertName"
            $cert = New-AriaCodeSigningCertificate -SubjectName $CertName -StorePath $CertStorePath
            Write-Host "Created certificate with thumbprint: $($cert.Thumbprint)"
        } else {
            Write-Host "Generating new self-signed certificate: $CertName"
            $cert = New-SelfSignedCertificate -Subject "CN=$CertName" -Type CodeSigningCert -CertStoreLocation $CertStorePath
            Write-Host "Created certificate with thumbprint: $($cert.Thumbprint)"
        }
    } else {
        Write-Host "Found existing certificate: $($cert.Thumbprint)"
    }
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
