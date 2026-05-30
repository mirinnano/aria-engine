param(
    [Parameter(Mandatory=$true)]
    [string]$FilePath,

    [string]$SignToolPath = $env:ARIA_SIGNTOOL_PATH,
    [string]$PfxPath = $env:ARIA_SIGN_PFX_PATH,
    [string]$PfxBase64 = $env:WINDOWS_CODESIGN_PFX_BASE64,
    [string]$PfxPassword = $env:WINDOWS_CODESIGN_PFX_PASSWORD,
    [string]$CertThumbprint = $env:ARIA_SIGN_CERT_THUMBPRINT,
    [string]$CertStorePath = $(if ([string]::IsNullOrWhiteSpace($env:ARIA_SIGN_CERT_STORE_PATH)) { "Cert:\CurrentUser\My" } else { $env:ARIA_SIGN_CERT_STORE_PATH }),
    [string]$TimestampUrl = $(if ([string]::IsNullOrWhiteSpace($env:ARIA_SIGN_TIMESTAMP_URL)) { "http://timestamp.digicert.com" } else { $env:ARIA_SIGN_TIMESTAMP_URL }),
    [switch]$AllowSelfSigned
)

$ErrorActionPreference = "Stop"

function Test-AriaTruthy {
    param([string]$Value)
    return $Value -in @("1", "true", "yes", "on")
}

function Find-AriaSignTool {
    if (-not [string]::IsNullOrWhiteSpace($SignToolPath)) {
        if (-not (Test-Path $SignToolPath -PathType Leaf)) {
            throw "ARIA_SIGNTOOL_PATH does not point to a file: $SignToolPath"
        }
        return (Resolve-Path $SignToolPath).Path
    }

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
        [string]$OutputPath,
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
            [DateTimeOffset]::UtcNow.AddYears(1))

        [IO.File]::WriteAllBytes(
            $OutputPath,
            $created.Export([System.Security.Cryptography.X509Certificates.X509ContentType]::Pfx, $Password))
    } finally {
        $rsa.Dispose()
    }
}

function Get-AriaCertificateFromStore {
    param([string]$Thumbprint)

    $normalized = $Thumbprint.Replace(" ", "").ToUpperInvariant()
    $cert = Get-ChildItem -Path $CertStorePath -ErrorAction Stop |
        Where-Object { $_.Thumbprint -eq $normalized } |
        Select-Object -First 1
    if (-not $cert) {
        throw "Signing certificate was not found in $CertStorePath for ARIA_SIGN_CERT_THUMBPRINT=$Thumbprint"
    }
    if (-not $cert.HasPrivateKey) {
        throw "Signing certificate does not have a private key: $Thumbprint"
    }
    return $cert
}

function Invoke-AriaSignToolWithPfx {
    param(
        [string]$SignTool,
        [string]$ResolvedPfxPath,
        [string]$Password,
        [string]$TargetPath
    )

    Write-Host "Signing $TargetPath with signtool PFX."
    & $SignTool sign /f $ResolvedPfxPath /p $Password /fd SHA256 /td SHA256 /tr $TimestampUrl $TargetPath
    if ($LASTEXITCODE -ne 0) {
        throw "signtool failed for $TargetPath"
    }
}

function Invoke-AriaSignToolWithThumbprint {
    param(
        [string]$SignTool,
        [string]$Thumbprint,
        [string]$TargetPath
    )

    Write-Host "Signing $TargetPath with signtool certificate thumbprint."
    & $SignTool sign /sha1 $Thumbprint /fd SHA256 /td SHA256 /tr $TimestampUrl $TargetPath
    if ($LASTEXITCODE -ne 0) {
        throw "signtool failed for $TargetPath"
    }
}

function Invoke-AriaAuthenticodeSign {
    param(
        [System.Security.Cryptography.X509Certificates.X509Certificate2]$Certificate,
        [string]$TargetPath,
        [bool]$AllowUntrustedResult
    )

    Write-Host "Signing $TargetPath with Set-AuthenticodeSignature."
    $result = Set-AuthenticodeSignature -FilePath $TargetPath -Certificate $Certificate -TimestampServer $TimestampUrl
    if ($result.Status -ne "Valid") {
        if ($AllowUntrustedResult) {
            Write-Warning "Signature status: $($result.Status). This is allowed only for explicit self-signed local testing."
            return
        }
        throw "Authenticode signing did not produce a trusted signature. Status: $($result.Status)"
    }
}

if (-not (Test-Path $FilePath -PathType Leaf)) {
    throw "File not found: $FilePath"
}

$target = (Resolve-Path $FilePath).Path
$allowSelfSignedEffective = [bool]$AllowSelfSigned -or (Test-AriaTruthy $env:ARIA_SIGN_ALLOW_SELF_SIGNED)
$hasPfxBase64 = -not [string]::IsNullOrWhiteSpace($PfxBase64)
$hasPfxPath = -not [string]::IsNullOrWhiteSpace($PfxPath)
$hasThumbprint = -not [string]::IsNullOrWhiteSpace($CertThumbprint)

if (-not $hasPfxBase64 -and -not $hasPfxPath -and -not $hasThumbprint -and -not $allowSelfSignedEffective) {
    throw "Code signing is not configured. Set WINDOWS_CODESIGN_PFX_BASE64 and WINDOWS_CODESIGN_PFX_PASSWORD, ARIA_SIGN_PFX_PATH and WINDOWS_CODESIGN_PFX_PASSWORD, or ARIA_SIGN_CERT_THUMBPRINT. For local-only testing set ARIA_SIGN_ALLOW_SELF_SIGNED=1 or pass -AllowSelfSigned."
}

if (($hasPfxBase64 -or $hasPfxPath) -and [string]::IsNullOrWhiteSpace($PfxPassword)) {
    throw "WINDOWS_CODESIGN_PFX_PASSWORD is required when signing with a PFX."
}

$signTool = Find-AriaSignTool
$tempPfxPath = ""
$tempPassword = ""

try {
    if ($hasPfxBase64) {
        $tempPfxPath = Join-Path ([IO.Path]::GetTempPath()) ("aria-codesign-" + [Guid]::NewGuid().ToString("N") + ".pfx")
        [IO.File]::WriteAllBytes($tempPfxPath, [Convert]::FromBase64String($PfxBase64))
        $PfxPath = $tempPfxPath
        $hasPfxPath = $true
    }

    if (-not $hasPfxPath -and -not $hasThumbprint -and $allowSelfSignedEffective) {
        $tempPfxPath = Join-Path ([IO.Path]::GetTempPath()) ("aria-codesign-selfsigned-" + [Guid]::NewGuid().ToString("N") + ".pfx")
        $tempPassword = [Guid]::NewGuid().ToString("N")
        New-AriaCodeSigningPfx -SubjectName "AriaEngineDev" -OutputPath $tempPfxPath -Password $tempPassword
        $PfxPath = $tempPfxPath
        $PfxPassword = $tempPassword
        $hasPfxPath = $true
    }

    if ($signTool -and $hasPfxPath) {
        Invoke-AriaSignToolWithPfx -SignTool $signTool -ResolvedPfxPath (Resolve-Path $PfxPath).Path -Password $PfxPassword -TargetPath $target
        return
    }

    if ($signTool -and $hasThumbprint) {
        Invoke-AriaSignToolWithThumbprint -SignTool $signTool -Thumbprint $CertThumbprint -TargetPath $target
        return
    }

    if ($hasThumbprint) {
        $cert = Get-AriaCertificateFromStore -Thumbprint $CertThumbprint
        Invoke-AriaAuthenticodeSign -Certificate $cert -TargetPath $target -AllowUntrustedResult:$false
        return
    }

    if ($hasPfxPath) {
        $cert = [System.Security.Cryptography.X509Certificates.X509Certificate2]::new(
            (Resolve-Path $PfxPath).Path,
            $PfxPassword,
            [System.Security.Cryptography.X509Certificates.X509KeyStorageFlags]::Exportable)
        Invoke-AriaAuthenticodeSign -Certificate $cert -TargetPath $target -AllowUntrustedResult:$allowSelfSignedEffective
        return
    }

    throw "Code signing is not configured."
} finally {
    if (-not [string]::IsNullOrWhiteSpace($tempPfxPath) -and (Test-Path $tempPfxPath)) {
        Remove-Item -LiteralPath $tempPfxPath -Force -ErrorAction SilentlyContinue
    }
}
