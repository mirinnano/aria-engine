param(
    [string]$ReleasePackageDir = "artifacts/release/AriaEngine-v1.0.0-win-x64/app",
    [string]$NativePackageDir = "",
    [string]$WebPackageDir = "artifacts/web/AriaEngine-v1.0.0-web",
    [string]$WebDeviceQaManifest = "artifacts/release/readiness/web-device-qa-manifest.json",
    [string]$WebVisualCompareManifest = "artifacts/release/readiness/web-native-visual-compare.json",
    [string]$SignatureAudit = "artifacts/dist/signature-audit-installer.json",
    [string]$OutputPath = "artifacts/release/readiness/release-readiness-audit.json"
)

$ErrorActionPreference = "Stop"

$checks = New-Object System.Collections.Generic.List[object]

function Add-ReadinessCheck {
    param(
        [string]$Name,
        [bool]$Passed,
        [string]$Evidence,
        [string]$Message = ""
    )

    $checks.Add([ordered]@{
        name = $Name
        passed = $Passed
        evidence = $Evidence
        message = $Message
    })
}

function Test-NonEmptyFile {
    param([string]$Path)
    return (Test-Path -LiteralPath $Path -PathType Leaf) -and ((Get-Item -LiteralPath $Path).Length -gt 0)
}

function Test-RequiredFiles {
    param(
        [string]$Root,
        [string[]]$RelativePaths
    )

    $missing = @()
    foreach ($relativePath in $RelativePaths) {
        $path = Join-Path $Root $relativePath
        if (-not (Test-NonEmptyFile $path)) {
            $missing += $relativePath
        }
    }
    return $missing
}

function Resolve-EvidencePath {
    param([string]$Path)

    if ([string]::IsNullOrWhiteSpace($Path)) {
        return $Path
    }
    if ([IO.Path]::IsPathRooted($Path)) {
        return $Path
    }
    return (Join-Path (Get-Location).Path $Path)
}

$requiredReleaseFiles = @(
    "AriaEngine.exe",
    "boot.arib",
    "scenario.aris",
    "data.arid",
    "voice.ariv",
    "scripts/scripts.ariac",
    "manifest.json",
    "checksums.txt",
    "README.md"
)

$missingReleaseFiles = Test-RequiredFiles -Root $ReleasePackageDir -RelativePaths $requiredReleaseFiles
Add-ReadinessCheck `
    -Name "Windows release package" `
    -Passed ($missingReleaseFiles.Count -eq 0) `
    -Evidence $ReleasePackageDir `
    -Message ($(if ($missingReleaseFiles.Count -eq 0) { "required Windows package files present" } else { "missing: " + ($missingReleaseFiles -join ", ") }))

$profileManifestPassed = $false
$profileManifestMessage = "runtime profile manifest missing"
$releaseManifestPath = Join-Path $ReleasePackageDir "manifest.json"
if (Test-NonEmptyFile $releaseManifestPath) {
    try {
        $releaseManifest = Get-Content -Raw -LiteralPath $releaseManifestPath | ConvertFrom-Json
        $profile = [string]$releaseManifest.profile
        $runArgs = @($releaseManifest.productionRunArgs)
        $allowlist = @($releaseManifest.security.browserOpenPolicy.allowlist)
        $schemes = @($releaseManifest.security.browserOpenPolicy.schemes)
        $scenarioStatus = $releaseManifest.localization.scenarioStatus
        $steamSubtitleLanguages = @($releaseManifest.localization.steamSubtitleLanguages)
        $validProfile = @("debug", "demo", "release") -contains $profile
        $hasRunProfile = ($runArgs -join " ") -match "--profile"
        $hasBrowserPolicy = ($schemes -contains "https") -and ($schemes -contains "http") -and ($allowlist -contains "x.com") -and ($allowlist -contains "ponkotsu-soft.vercel.app")
        $hasScenarioStatus = $null -ne $scenarioStatus -and $null -ne $scenarioStatus."ja-JP"
        $subtitleClaimsAreComplete = $true
        foreach ($language in $steamSubtitleLanguages) {
            $status = [string]$scenarioStatus.$language
            if ($status -ne "source" -and $status -ne "complete") {
                $subtitleClaimsAreComplete = $false
            }
        }
        $profileManifestPassed = $validProfile -and $hasRunProfile -and $hasBrowserPolicy -and $hasScenarioStatus -and $subtitleClaimsAreComplete
        $profileManifestMessage = if ($profileManifestPassed) {
            "runtime profile manifest records profile, production args, browserOpenPolicy, scenarioStatus, and steamSubtitleLanguages"
        } elseif (-not $validProfile) {
            "manifest profile is missing or invalid"
        } elseif (-not $hasRunProfile) {
            "manifest productionRunArgs missing --profile"
        } elseif (-not $hasBrowserPolicy) {
            "manifest browserOpenPolicy missing required schemes or allowlist"
        } elseif (-not $hasScenarioStatus) {
            "manifest localization scenarioStatus missing"
        } else {
            "manifest steamSubtitleLanguages includes an incomplete scenario locale"
        }
    } catch {
        $profileManifestMessage = "runtime profile manifest could not be read: $($_.Exception.Message)"
    }
}
Add-ReadinessCheck `
    -Name "runtime profile manifest" `
    -Passed $profileManifestPassed `
    -Evidence $releaseManifestPath `
    -Message $profileManifestMessage

$nativeAotPassed = $false
$nativeAotMessage = "NativeAOT package evidence missing"
if (-not [string]::IsNullOrWhiteSpace($NativePackageDir) -and (Test-Path -LiteralPath $NativePackageDir -PathType Container)) {
    $nativeExe = Join-Path $NativePackageDir "AriaEngine.exe"
    $nativeManifest = Join-Path $NativePackageDir "manifest.json"
    $forbiddenNames = @("coreclr.dll", "hostfxr.dll", "hostpolicy.dll", "AriaEngine.dll", "dotnet.exe")
    $forbiddenFiles = @(
        Get-ChildItem -LiteralPath $NativePackageDir -Recurse -File -ErrorAction SilentlyContinue |
            Where-Object {
                $forbiddenNames -contains $_.Name -or
                $_.Name.EndsWith(".runtimeconfig.json", [StringComparison]::OrdinalIgnoreCase)
            }
    )

    if (-not (Test-NonEmptyFile $nativeExe)) {
        $nativeAotMessage = "NativeAOT package missing AriaEngine.exe"
    } elseif (-not (Test-NonEmptyFile $nativeManifest)) {
        $nativeAotMessage = "NativeAOT package missing manifest.json"
    } elseif ($forbiddenFiles.Count -gt 0) {
        $nativeAotMessage = "NativeAOT package still has dotnet runtime dependency files: " + (($forbiddenFiles | ForEach-Object { $_.Name }) -join ", ")
    } else {
        try {
            $manifest = Get-Content -Raw -LiteralPath $nativeManifest | ConvertFrom-Json
            $manifestPublishAot = ($manifest.publishAot -eq $true) -or ($manifest.packaging.publishAot -eq $true)
            if ($manifestPublishAot) {
                $nativeAotPassed = $true
                $nativeAotMessage = "NativeAOT package has native exe and no coreclr.dll, AriaEngine.dll, or runtimeconfig dotnet runtime dependency files"
            } else {
                $nativeAotMessage = "NativeAOT package manifest does not record publishAot=true"
            }
        } catch {
            $nativeAotMessage = "NativeAOT package manifest could not be read: $($_.Exception.Message)"
        }
    }
}
Add-ReadinessCheck `
    -Name "NativeAOT package" `
    -Passed $nativeAotPassed `
    -Evidence $NativePackageDir `
    -Message $nativeAotMessage

$requiredWebFiles = @(
    "index.html",
    "manifest.webmanifest",
    "service-worker.js",
    "_framework/blazor.webassembly.js",
    "js/aria-web-runtime.js",
    "assets/web-text-assets.json",
    "manifest.json",
    "checksums.txt"
)
$missingWebFiles = Test-RequiredFiles -Root $WebPackageDir -RelativePaths $requiredWebFiles
$webPackagePassed = $missingWebFiles.Count -eq 0
if ($webPackagePassed) {
    try {
        $webManifestPath = Join-Path $WebPackageDir "manifest.json"
        $webManifest = Get-Content -Raw -LiteralPath $webManifestPath | ConvertFrom-Json
        $webPackagePassed = $webManifest.target -eq "web-pwa"
        $webPackageMessage = if ($webPackagePassed) {
            "required static Web/PWA package files present"
        } else {
            "Web/PWA package manifest does not record target=web-pwa"
        }
    } catch {
        $webPackagePassed = $false
        $webPackageMessage = "Web/PWA package manifest could not be read: $($_.Exception.Message)"
    }
} else {
    $webPackageMessage = "missing: " + ($missingWebFiles -join ", ")
}
Add-ReadinessCheck `
    -Name "static Web/PWA package" `
    -Passed $webPackagePassed `
    -Evidence $WebPackageDir `
    -Message $webPackageMessage

$browserQaPassed = $false
$browserQaMessage = "browser QA manifest missing"
$resolvedWebDeviceQaManifest = Resolve-EvidencePath $WebDeviceQaManifest
if (Test-Path -LiteralPath $resolvedWebDeviceQaManifest -PathType Leaf) {
    try {
        $browserQa = Get-Content -Raw -LiteralPath $resolvedWebDeviceQaManifest | ConvertFrom-Json
        $browserManifests = @($browserQa.browserManifests)
        $requiredBrowsers = @("Chrome", "Edge", "Safari", "mobile")
        $readyBrowsers = New-Object System.Collections.Generic.HashSet[string] ([StringComparer]::OrdinalIgnoreCase)
        $browserManifestFailures = New-Object System.Collections.Generic.List[string]
        foreach ($manifestPath in $browserManifests) {
            $resolvedManifestPath = Resolve-EvidencePath ([string]$manifestPath)
            if (-not (Test-Path -LiteralPath $resolvedManifestPath -PathType Leaf)) {
                $browserManifestFailures.Add("missing browser manifest: $manifestPath") | Out-Null
                continue
            }
            try {
                $browserManifest = Get-Content -Raw -LiteralPath $resolvedManifestPath | ConvertFrom-Json
                if ($browserManifest.ready -eq $true) {
                    [void]$readyBrowsers.Add([string]$browserManifest.browser)
                } else {
                    $browserManifestFailures.Add("browser manifest not ready: $manifestPath") | Out-Null
                }
            } catch {
                $browserManifestFailures.Add("browser manifest unreadable: $manifestPath") | Out-Null
            }
        }
        $missingBrowsers = @($requiredBrowsers | Where-Object { -not $readyBrowsers.Contains($_) })
        $browserQaPassed = $browserQa.ready -eq $true -and $browserQa.browserReady -eq $true -and $missingBrowsers.Count -eq 0 -and $browserManifestFailures.Count -eq 0
        $browserQaMessage = if ($browserQaPassed) {
            "Chrome, Edge, Safari, and mobile browser QA passed"
        } elseif ($missingBrowsers.Count -gt 0) {
            "missing ready browser QA: " + ($missingBrowsers -join ", ")
        } elseif ($browserManifestFailures.Count -gt 0) {
            ($browserManifestFailures.ToArray() -join "; ")
        } else {
            "browser QA aggregate is not ready"
        }
    } catch {
        $browserQaMessage = "browser QA manifest could not be read: $($_.Exception.Message)"
    }
}
Add-ReadinessCheck `
    -Name "browser QA captures" `
    -Passed $browserQaPassed `
    -Evidence $WebDeviceQaManifest `
    -Message $browserQaMessage

$visualPassed = $false
$visualMessage = "native/Web visual regression manifest missing"
$resolvedWebVisualCompareManifest = Resolve-EvidencePath $WebVisualCompareManifest
if (Test-Path -LiteralPath $resolvedWebVisualCompareManifest -PathType Leaf) {
    try {
        $visual = Get-Content -Raw -LiteralPath $resolvedWebVisualCompareManifest | ConvertFrom-Json
        $comparisons = @($visual.comparisons)
        $failedComparisons = @($comparisons | Where-Object {
            $_.ready -ne $true -or $_.layoutParity -ne "passed" -or $_.fontParity -ne "passed"
        })
        $visualPassed = $visual.ready -eq $true -and $comparisons.Count -gt 0 -and $failedComparisons.Count -eq 0
        $visualMessage = if ($visualPassed) {
            "native/Web visual regression passed"
        } elseif ($comparisons.Count -eq 0) {
            "native/Web visual regression has no comparisons"
        } else {
            "native/Web visual regression has failed comparisons"
        }
    } catch {
        $visualMessage = "native/Web visual regression manifest could not be read: $($_.Exception.Message)"
    }
}
Add-ReadinessCheck `
    -Name "native/Web visual regression" `
    -Passed $visualPassed `
    -Evidence $WebVisualCompareManifest `
    -Message $visualMessage

$signaturePassed = $false
$signatureMessage = "trusted signing audit missing"
if (Test-Path -LiteralPath $SignatureAudit -PathType Leaf) {
    try {
        $signature = Get-Content -Raw -LiteralPath $SignatureAudit | ConvertFrom-Json
        $files = @($signature.files)
        $invalidFiles = @($files | Where-Object { $_.status -ne "Valid" })
        $total = [int]$signature.total
        $unsigned = [int]$signature.unsigned
        $signaturePassed = $total -gt 0 -and $unsigned -eq 0 -and $invalidFiles.Count -eq 0
        $signatureMessage = if ($signaturePassed) {
            "trusted signing audit passed"
        } else {
            "trusted signing audit has unsigned or invalid files"
        }
    } catch {
        $signatureMessage = "trusted signing audit could not be read: $($_.Exception.Message)"
    }
}
Add-ReadinessCheck `
    -Name "trusted signing audit" `
    -Passed $signaturePassed `
    -Evidence $SignatureAudit `
    -Message $signatureMessage

$checkItems = @($checks.ToArray())
$failed = @($checkItems | Where-Object { $_.passed -ne $true })
$ready = [bool]($failed.Count -eq 0)
$payload = [ordered]@{
    generatedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    ready = $ready
    objective = "Windows Native/NativeAOT/Web/PWA release-ready package and QA evidence"
    checks = $checkItems
}

$parent = Split-Path -Parent $OutputPath
if (-not [string]::IsNullOrWhiteSpace($parent)) {
    New-Item -ItemType Directory -Force -Path $parent | Out-Null
}
$payload | ConvertTo-Json -Depth 8 | Set-Content -Path $OutputPath -Encoding UTF8
Write-Host "Release readiness audit written: $OutputPath"

if ($failed.Count -gt 0) {
    $summary = ($failed | ForEach-Object { "$($_.name): $($_.message)" }) -join "; "
    Write-Host "Release is not ready: $summary"
    throw "Release is not ready: $summary"
}

Write-Host "Windows Native/NativeAOT/Web/PWA release-ready audit passed."
