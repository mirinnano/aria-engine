param(
    [string]$BaselineDir = "artifacts/visual-regression/baseline",
    [string]$CurrentDir = "artifacts/visual-regression/current",
    [string]$OutputDir = "artifacts/visual-regression/diff",
    [int]$Tolerance = 4,
    [double]$MaxDiffRatio = 0.001
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null
$failures = @()
$results = @()

$baselineFiles = Get-ChildItem -Path $BaselineDir -Filter "*.png" -File -ErrorAction SilentlyContinue
foreach ($base in $baselineFiles) {
    $current = Join-Path $CurrentDir $base.Name
    if (-not (Test-Path $current)) {
        $failures += "Missing current image: $($base.Name)"
        continue
    }

    $b = [System.Drawing.Bitmap]::new($base.FullName)
    $c = [System.Drawing.Bitmap]::new((Resolve-Path $current).Path)
    try {
        if ($b.Width -ne $c.Width -or $b.Height -ne $c.Height) {
            $failures += "Size mismatch: $($base.Name)"
            continue
        }

        $diff = [System.Drawing.Bitmap]::new($b.Width, $b.Height)
        $changed = 0
        for ($y = 0; $y -lt $b.Height; $y++) {
            for ($x = 0; $x -lt $b.Width; $x++) {
                $bp = $b.GetPixel($x, $y)
                $cp = $c.GetPixel($x, $y)
                $delta = [Math]::Max([Math]::Abs($bp.R - $cp.R), [Math]::Max([Math]::Abs($bp.G - $cp.G), [Math]::Abs($bp.B - $cp.B)))
                if ($delta -gt $Tolerance) {
                    $changed++
                    $diff.SetPixel($x, $y, [System.Drawing.Color]::FromArgb(255, 255, 0, 255))
                } else {
                    $gray = [int](($cp.R + $cp.G + $cp.B) / 3)
                    $diff.SetPixel($x, $y, [System.Drawing.Color]::FromArgb(255, $gray, $gray, $gray))
                }
            }
        }

        $ratio = $changed / [double]($b.Width * $b.Height)
        $diffPath = Join-Path $OutputDir $base.Name
        $diff.Save($diffPath, [System.Drawing.Imaging.ImageFormat]::Png)
        $results += [ordered]@{ file = $base.Name; changedPixels = $changed; diffRatio = $ratio; diff = $diffPath }
        if ($ratio -gt $MaxDiffRatio) {
            $failures += "Visual diff too large: $($base.Name) ratio=$ratio"
        }
    }
    finally {
        $b.Dispose()
        $c.Dispose()
        if ($diff) { $diff.Dispose() }
    }
}

$results | ConvertTo-Json -Depth 4 | Set-Content -Path (Join-Path $OutputDir "visual-compare.json") -Encoding UTF8
foreach ($failure in $failures) { Write-Error $failure -ErrorAction Continue }
if ($failures.Count -gt 0) { throw "visual compare failed with $($failures.Count) failure(s)." }
Write-Host "Visual compare passed. Results: $(Join-Path $OutputDir 'visual-compare.json')"
