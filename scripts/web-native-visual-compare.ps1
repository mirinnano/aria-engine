param(
    [string]$NativeCaptureDir = "artifacts/visual/native",
    [string]$WebCaptureDir = "artifacts/visual/web",
    [string]$OutputPath = "artifacts/release/readiness/web-native-visual-compare.json",
    [double]$MaxPixelDelta = 0.02,
    [double]$MaxDiffRatio = 0.05,
    [switch]$AllowMissingCaptures
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

$screens = @("title.png", "text.png", "menu.png")
$diffDir = Join-Path (Split-Path -Parent $OutputPath) "web-native-diff"
New-Item -ItemType Directory -Force -Path $diffDir | Out-Null

function Compare-WebNativeCapture {
    param(
        [string]$Screen,
        [string]$Native,
        [string]$Web
    )

    $present = (Test-Path -LiteralPath $Native) -and (Test-Path -LiteralPath $Web)
    if (-not $present) {
        return [ordered]@{
            screen = $Screen
            native = $Native
            web = $Web
            present = $false
            ready = $false
            width = 0
            height = 0
            changedPixels = 0
            totalPixels = 0
            pixelDiffRatio = 1
            maxPixelDelta = $MaxPixelDelta
            maxDiffRatio = $MaxDiffRatio
            diff = ""
            layoutParity = "missing"
            fontParity = "missing"
            hitTestParity = "missing"
        }
    }

    $nativeBitmap = [System.Drawing.Bitmap]::new((Resolve-Path -LiteralPath $Native).Path)
    $webBitmap = [System.Drawing.Bitmap]::new((Resolve-Path -LiteralPath $Web).Path)
    $diffBitmap = $null
    try {
        $sameSize = $nativeBitmap.Width -eq $webBitmap.Width -and $nativeBitmap.Height -eq $webBitmap.Height
        if (-not $sameSize) {
            return [ordered]@{
                screen = $Screen
                native = $Native
                web = $Web
                present = $true
                ready = $false
                width = $nativeBitmap.Width
                height = $nativeBitmap.Height
                webWidth = $webBitmap.Width
                webHeight = $webBitmap.Height
                changedPixels = 0
                totalPixels = 0
                pixelDiffRatio = 1
                maxPixelDelta = $MaxPixelDelta
                maxDiffRatio = $MaxDiffRatio
                diff = ""
                layoutParity = "failed"
                fontParity = "blocked"
                hitTestParity = "covered-by-browser-qa"
            }
        }

        $changed = 0
        $total = $nativeBitmap.Width * $nativeBitmap.Height
        $diffBitmap = [System.Drawing.Bitmap]::new($nativeBitmap.Width, $nativeBitmap.Height)
        for ($y = 0; $y -lt $nativeBitmap.Height; $y++) {
            for ($x = 0; $x -lt $nativeBitmap.Width; $x++) {
                $nativePixel = $nativeBitmap.GetPixel($x, $y)
                $webPixel = $webBitmap.GetPixel($x, $y)
                $delta = [Math]::Max(
                    [Math]::Abs($nativePixel.R - $webPixel.R),
                    [Math]::Max(
                        [Math]::Abs($nativePixel.G - $webPixel.G),
                        [Math]::Max([Math]::Abs($nativePixel.B - $webPixel.B), [Math]::Abs($nativePixel.A - $webPixel.A))))
                if (($delta / 255.0) -gt $MaxPixelDelta) {
                    $changed++
                    $diffBitmap.SetPixel($x, $y, [System.Drawing.Color]::FromArgb(255, 255, 0, 255))
                } else {
                    $gray = [int](($webPixel.R + $webPixel.G + $webPixel.B) / 3)
                    $diffBitmap.SetPixel($x, $y, [System.Drawing.Color]::FromArgb(255, $gray, $gray, $gray))
                }
            }
        }

        $ratio = if ($total -gt 0) { $changed / [double]$total } else { 1 }
        $passed = $ratio -le $MaxDiffRatio
        $diffPath = Join-Path $diffDir $Screen
        $diffBitmap.Save($diffPath, [System.Drawing.Imaging.ImageFormat]::Png)

        return [ordered]@{
            screen = $Screen
            native = $Native
            web = $Web
            present = $true
            ready = [bool]$passed
            width = $nativeBitmap.Width
            height = $nativeBitmap.Height
            changedPixels = $changed
            totalPixels = $total
            pixelDiffRatio = $ratio
            maxPixelDelta = $MaxPixelDelta
            maxDiffRatio = $MaxDiffRatio
            diff = $diffPath
            layoutParity = "passed"
            fontParity = if ($passed) { "passed" } else { "failed" }
            hitTestParity = "covered-by-browser-qa"
        }
    }
    finally {
        if ($diffBitmap) { $diffBitmap.Dispose() }
        $nativeBitmap.Dispose()
        $webBitmap.Dispose()
    }
}

$comparisons = @($screens | ForEach-Object {
    Compare-WebNativeCapture -Screen $_ -Native (Join-Path $NativeCaptureDir $_) -Web (Join-Path $WebCaptureDir $_)
})
$failed = @($comparisons | Where-Object { $_.ready -ne $true })
$missing = @($comparisons | Where-Object { -not $_.present })
$allComparisonsReady = $failed.Count -eq 0
$payload = [ordered]@{
    ready = [bool]$allComparisonsReady
    missingCapturesAllowed = [bool]$AllowMissingCaptures
    missingCaptureCount = $missing.Count
    nativeCaptureDir = $NativeCaptureDir
    webCaptureDir = $WebCaptureDir
    diffDir = $diffDir
    comparisons = $comparisons
}
$parent = Split-Path -Parent $OutputPath
if (-not [string]::IsNullOrWhiteSpace($parent)) { New-Item -ItemType Directory -Force -Path $parent | Out-Null }
$payload | ConvertTo-Json -Depth 8 | Set-Content -Path $OutputPath -Encoding UTF8
Write-Host "Native/Web visual regression manifest written: $OutputPath"
if (-not $AllowMissingCaptures -and $failed.Count -gt 0) { throw "Native/Web visual regression gate failed." }
