param(
    [string]$PackageDir = "artifacts/visual-regression/package-run-v1.0.0",
    [string]$OutputDir = "artifacts/visual/native",
    [int]$WaitSeconds = 8,
    [int]$StabilizeSeconds = 6,
    [int]$CaptureRetrySeconds = 20,
    [double]$MinNonBlankRatio = 0.005
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

if (-not ("AriaNativeVisualCaptureWin32" -as [type])) {
    Add-Type @"
using System;
using System.Runtime.InteropServices;

public static class AriaNativeVisualCaptureWin32
{
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    public static extern bool GetClientRect(IntPtr hWnd, out RECT rect);

    [DllImport("user32.dll")]
    public static extern bool ClientToScreen(IntPtr hWnd, ref POINT point);

    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    public static extern void mouse_event(int flags, int dx, int dy, int data, UIntPtr extraInfo);
}
"@
}

$packagePath = (Resolve-Path $PackageDir).Path
$tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
$capturePackagePath = Join-Path $tempRoot ("aria-native-capture-package-" + [Guid]::NewGuid().ToString("N"))
if (-not ([IO.Path]::GetFullPath($capturePackagePath).StartsWith($tempRoot, [StringComparison]::OrdinalIgnoreCase))) {
    throw "Refusing to create native capture package outside temp root: $capturePackagePath"
}
Copy-Item -LiteralPath $packagePath -Destination $capturePackagePath -Recurse -Force
foreach ($statePath in @(
    (Join-Path $capturePackagePath "saves"),
    (Join-Path $capturePackagePath "config.json"),
    (Join-Path $capturePackagePath "aria_error_ai.json")
)) {
    if (Test-Path -LiteralPath $statePath) {
        Remove-Item -LiteralPath $statePath -Recurse -Force
    }
}

$exePath = Join-Path $capturePackagePath "AriaEngine.exe"
if (-not (Test-Path -LiteralPath $exePath -PathType Leaf)) {
    throw "Packaged executable not found: $exePath"
}

New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null
$captures = New-Object System.Collections.Generic.List[object]

$args = @("--run-mode", "release")
$pakPath = Join-Path $capturePackagePath "data.pak"
$compiledPath = Join-Path $capturePackagePath "scripts/scripts.ariac"
if ((Test-Path -LiteralPath $pakPath -PathType Leaf) -and (Test-Path -LiteralPath $compiledPath -PathType Leaf)) {
    $args += @("--pak", "data.pak", "--compiled", "scripts/scripts.ariac")
}

$process = $null
$process = Start-Process -FilePath $exePath -ArgumentList $args -WorkingDirectory $capturePackagePath -PassThru
try {
    $deadline = (Get-Date).AddSeconds($WaitSeconds)
    do {
        Start-Sleep -Milliseconds 250
        $process.Refresh()
        if ($process.HasExited) {
            throw "Packaged app exited before native visual capture. Exit code: $($process.ExitCode)"
        }
    } while ($process.MainWindowHandle -eq 0 -and (Get-Date) -lt $deadline)

    if ($process.MainWindowHandle -eq 0) {
        throw "Packaged app window was not created before timeout."
    }

    [AriaNativeVisualCaptureWin32]::SetForegroundWindow($process.MainWindowHandle) | Out-Null
    Start-Sleep -Seconds $StabilizeSeconds

    function Get-ClientMetrics {
        $rect = New-Object AriaNativeVisualCaptureWin32+RECT
        if (-not [AriaNativeVisualCaptureWin32]::GetClientRect($process.MainWindowHandle, [ref]$rect)) {
            throw "Could not read packaged app client bounds."
        }

        $width = $rect.Right - $rect.Left
        $height = $rect.Bottom - $rect.Top
        if ($width -le 0 -or $height -le 0) {
            throw "Invalid packaged app client bounds: ${width}x${height}."
        }

        $origin = New-Object AriaNativeVisualCaptureWin32+POINT
        $origin.X = 0
        $origin.Y = 0
        if (-not [AriaNativeVisualCaptureWin32]::ClientToScreen($process.MainWindowHandle, [ref]$origin)) {
            throw "Could not map packaged app client area to screen coordinates."
        }

        [pscustomobject]@{
            X = $origin.X
            Y = $origin.Y
            Width = $width
            Height = $height
        }
    }

    function CaptureClient {
        param([string]$Name)

        $deadline = (Get-Date).AddSeconds($CaptureRetrySeconds)
        $lastRatio = 0
        do {
            $metrics = Get-ClientMetrics
            $bitmap = [System.Drawing.Bitmap]::new($metrics.Width, $metrics.Height)
            $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
            try {
                $graphics.CopyFromScreen($metrics.X, $metrics.Y, 0, 0, $bitmap.Size)

                $base = $bitmap.GetPixel(0, 0)
                $samples = 0
                $changed = 0
                $stepX = [Math]::Max(1, [int]($metrics.Width / 80))
                $stepY = [Math]::Max(1, [int]($metrics.Height / 45))
                for ($y = 0; $y -lt $metrics.Height; $y += $stepY) {
                    for ($x = 0; $x -lt $metrics.Width; $x += $stepX) {
                        $pixel = $bitmap.GetPixel($x, $y)
                        $delta = [Math]::Max([Math]::Abs($pixel.R - $base.R), [Math]::Max([Math]::Abs($pixel.G - $base.G), [Math]::Abs($pixel.B - $base.B)))
                        $samples++
                        if ($delta -gt 4) { $changed++ }
                    }
                }

                $ratio = $changed / [double]$samples
                $lastRatio = $ratio
                if ($ratio -ge $MinNonBlankRatio) {
                    $capturePath = Join-Path $OutputDir $Name
                    $bitmap.Save($capturePath, [System.Drawing.Imaging.ImageFormat]::Png)
                    $captures.Add([ordered]@{
                        name = $Name
                        path = $capturePath
                        width = $metrics.Width
                        height = $metrics.Height
                        bytes = (Get-Item -LiteralPath $capturePath).Length
                        nonBlankRatio = $ratio
                    })
                    Write-Host "Captured native visual: $capturePath"
                    return
                }
            }
            finally {
                if ($graphics) { $graphics.Dispose() }
                if ($bitmap) { $bitmap.Dispose() }
            }

            Start-Sleep -Seconds 1
        } while ((Get-Date) -lt $deadline)

        throw "Native visual capture '$Name' looks blank. Last non-blank sample ratio $lastRatio is below $MinNonBlankRatio."
    }

    function ClickClientLogical {
        param([int]$X, [int]$Y, [switch]$Right)

        $metrics = Get-ClientMetrics
        [AriaNativeVisualCaptureWin32]::SetForegroundWindow($process.MainWindowHandle) | Out-Null
        $screenX = $metrics.X + [int][Math]::Round($X * $metrics.Width / 1280.0)
        $screenY = $metrics.Y + [int][Math]::Round($Y * $metrics.Height / 720.0)
        [AriaNativeVisualCaptureWin32]::SetCursorPos($screenX, $screenY) | Out-Null
        Start-Sleep -Milliseconds 80
        if ($Right) {
            [AriaNativeVisualCaptureWin32]::mouse_event(0x0008, 0, 0, 0, [UIntPtr]::Zero)
            Start-Sleep -Milliseconds 80
            [AriaNativeVisualCaptureWin32]::mouse_event(0x0010, 0, 0, 0, [UIntPtr]::Zero)
        } else {
            [AriaNativeVisualCaptureWin32]::mouse_event(0x0002, 0, 0, 0, [UIntPtr]::Zero)
            Start-Sleep -Milliseconds 80
            [AriaNativeVisualCaptureWin32]::mouse_event(0x0004, 0, 0, 0, [UIntPtr]::Zero)
        }
    }

    CaptureClient "title.png"
    ClickClientLogical 640 320
    Start-Sleep -Seconds 4
    CaptureClient "text.png"
    ClickClientLogical 1000 500 -Right
    Start-Sleep -Seconds 1
    CaptureClient "menu.png"
}
finally {
    if ($process -and -not $process.HasExited) {
        $process.CloseMainWindow() | Out-Null
        if (-not $process.WaitForExit(5000)) {
            $process.Kill()
            $process.WaitForExit()
        }
    }
    if (Test-Path -LiteralPath $capturePackagePath) {
        Remove-Item -LiteralPath $capturePackagePath -Recurse -Force
    }
}

$ready = $captures.Count -eq 3
$manifest = [ordered]@{
    generatedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    ready = [bool]$ready
    packageDir = $packagePath
    capturePackageDir = $capturePackagePath
    outputDir = (Resolve-Path $OutputDir).Path
    captures = @($captures.ToArray())
}
$manifest | ConvertTo-Json -Depth 8 | Set-Content -Path (Join-Path $OutputDir "native-capture-manifest.json") -Encoding UTF8
Write-Host "Native visual captures written: $OutputDir"
if (-not $ready) { throw "Native visual capture did not produce title.png, text.png, and menu.png." }
