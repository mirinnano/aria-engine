param(
    [string]$WebPackageDir = "artifacts/web/AriaEngine-dev-web",
    [string]$OutputDir = "artifacts/release/readiness",
    [string]$NativeCaptureDir = "artifacts/visual/native",
    [string]$WebCaptureDir = "artifacts/visual/web",
    [switch]$AllowMissingCaptures,
    [switch]$InstallBrowsers,
    [switch]$SkipWebVisualCapture
)

$ErrorActionPreference = "Stop"
New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null
$browserManifests = New-Object System.Collections.Generic.List[string]

foreach ($entry in @(
    @{ Browser = "Chrome"; Viewport = "desktop-16x9"; Width = 1280; Height = 720 },
    @{ Browser = "Edge"; Viewport = "desktop-16x9"; Width = 1280; Height = 720 },
    @{ Browser = "Safari"; Viewport = "desktop-16x9"; Width = 1280; Height = 720 },
    @{ Browser = "mobile"; Viewport = "mobile-portrait"; Width = 390; Height = 844 }
)) {
    $manifestPath = Join-Path $OutputDir ("web-browser-qa-$($entry.Browser).json").ToLowerInvariant()
    $browserQaArgs = @{
        WebPackageDir = $WebPackageDir
        OutputPath = $manifestPath
        Browser = $entry.Browser
        ViewportName = $entry.Viewport
        ViewportWidth = $entry.Width
        ViewportHeight = $entry.Height
    }
    if ($InstallBrowsers -and $browserManifests.Count -eq 0) {
        $browserQaArgs.InstallBrowsers = $true
    }
    & (Join-Path $PSScriptRoot "web-browser-qa.ps1") @browserQaArgs
    $browserManifests.Add($manifestPath)
}

if (-not $SkipWebVisualCapture) {
    $captureArgs = @{
        WebPackageDir = $WebPackageDir
        OutputDir = $WebCaptureDir
        Browser = "Chrome"
        ViewportWidth = 1280
        ViewportHeight = 720
    }
    if ($InstallBrowsers) {
        $captureArgs.InstallBrowsers = $true
    }
    & (Join-Path $PSScriptRoot "web-capture-visuals.ps1") @captureArgs
}

$visualManifest = Join-Path $OutputDir "web-native-visual-compare.json"
if ($AllowMissingCaptures) {
    & (Join-Path $PSScriptRoot "web-native-visual-compare.ps1") -NativeCaptureDir $NativeCaptureDir -WebCaptureDir $WebCaptureDir -OutputPath $visualManifest -AllowMissingCaptures
} else {
    & (Join-Path $PSScriptRoot "web-native-visual-compare.ps1") -NativeCaptureDir $NativeCaptureDir -WebCaptureDir $WebCaptureDir -OutputPath $visualManifest
}

$browserReady = $true
foreach ($manifest in $browserManifests) {
    $data = Get-Content -Raw -LiteralPath $manifest | ConvertFrom-Json
    if ($data.ready -ne $true) { $browserReady = $false }
}
$visualData = Get-Content -Raw -LiteralPath $visualManifest | ConvertFrom-Json
$visualReady = $visualData.ready -eq $true

$payload = [ordered]@{
    ready = [bool]($browserReady -and $visualReady)
    browserReady = [bool]$browserReady
    visualReady = [bool]$visualReady
    webPackageDir = $WebPackageDir
    nativeCaptureDir = $NativeCaptureDir
    webCaptureDir = $WebCaptureDir
    browserManifests = @($browserManifests.ToArray())
    visualManifest = $visualManifest
}
$outputPath = Join-Path $OutputDir "web-device-qa-manifest.json"
$payload | ConvertTo-Json -Depth 8 | Set-Content -Path $outputPath -Encoding UTF8
Write-Host "Web device QA manifest written: $outputPath"
