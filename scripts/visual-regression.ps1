param(
    [string]$BaselineDir = "tests/visual-regression/baseline",
    [string]$CurrentDir = "artifacts/visual-regression/current",
    [switch]$PromoteCurrent,
    [switch]$CaptureLaunch,
    [switch]$CaptureUiFlow,
    [string]$PackageDir = "artifacts/release/AriaEngine-v1.0.0-win-x64/app",
    [string]$CaptureName = "title-screen.png",
    [int]$WaitSeconds = 8,
    [int]$StabilizeSeconds = 8,
    [double]$MinNonBlankRatio = 0.01
)

$ErrorActionPreference = "Stop"

New-Item -ItemType Directory -Force -Path $BaselineDir, $CurrentDir | Out-Null

if ($PromoteCurrent) {
    $captures = Get-ChildItem -Path $CurrentDir -Filter "*.png" -File -ErrorAction SilentlyContinue
    if ($captures.Count -eq 0) {
        throw "No current captures found in $CurrentDir."
    }
    Copy-Item -Path (Join-Path $CurrentDir "*.png") -Destination $BaselineDir -Recurse -Force
    Write-Host "Current captures promoted to baseline: $BaselineDir"
    exit 0
}

if ($CaptureLaunch -or $CaptureUiFlow) {
    Add-Type -AssemblyName System.Drawing
    if (-not ("AriaVisualCaptureWin32" -as [type])) {
        Add-Type @"
using System;
using System.Runtime.InteropServices;

public static class AriaVisualCaptureWin32
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
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

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

    $packagePath = Resolve-Path $PackageDir
    $exePath = Join-Path $packagePath "AriaEngine.exe"
    if (-not (Test-Path $exePath)) {
        throw "Packaged executable not found: $exePath"
    }

    function Write-VisualPersistentState {
        param([string]$AppDir)

        $saveDir = Join-Path $AppDir "saves"
        New-Item -ItemType Directory -Force -Path $saveDir | Out-Null

        $data = [ordered]@{
            SchemaVersion = 2
            Registers = [ordered]@{}
            Flags = [ordered]@{}
            SaveFlags = [ordered]@{
                chapter_01 = $true
                chapter_02 = $true
                chapter_03 = $true
                chapter_04 = $true
                chapter_05 = $true
                chapter_06 = $true
            }
            Counters = [ordered]@{}
            ReadKeys = @()
            SkipUnread = $true
            UnlockedCgs = @()
        }

        $jsonBytes = [System.Text.Encoding]::UTF8.GetBytes(($data | ConvertTo-Json -Compress -Depth 8))
        $compressedStream = [System.IO.MemoryStream]::new()
        $gzip = [System.IO.Compression.GZipStream]::new($compressedStream, [System.IO.Compression.CompressionLevel]::SmallestSize, $true)
        try {
            $gzip.Write($jsonBytes, 0, $jsonBytes.Length)
        }
        finally {
            $gzip.Dispose()
        }
        $compressed = $compressedStream.ToArray()
        $compressedStream.Dispose()

        $sha = [System.Security.Cryptography.SHA256]::Create()
        $aes = [System.Security.Cryptography.Aes]::Create()
        try {
            $aes.Key = $sha.ComputeHash([System.Text.Encoding]::UTF8.GetBytes("AriaEngine.PersistentFlags.Format.v2"))
            $aes.GenerateIV()
            $aes.Mode = [System.Security.Cryptography.CipherMode]::CBC
            $aes.Padding = [System.Security.Cryptography.PaddingMode]::PKCS7
            $encryptor = $aes.CreateEncryptor()
            try {
                $cipher = $encryptor.TransformFinalBlock($compressed, 0, $compressed.Length)
            }
            finally {
                $encryptor.Dispose()
            }

            $persistentPath = Join-Path $saveDir "persistent.ariasav"
            $stream = [System.IO.File]::Create($persistentPath)
            $writer = [System.IO.BinaryWriter]::new($stream, [System.Text.Encoding]::UTF8, $false)
            try {
                $writer.Write([System.Text.Encoding]::ASCII.GetBytes("ARIAPERSIST2"))
                $writer.Write([int]2)
                $writer.Write([int]$aes.IV.Length)
                $writer.Write($aes.IV)
                $writer.Write([int]$cipher.Length)
                $writer.Write($cipher)
            }
            finally {
                $writer.Dispose()
            }
        }
        finally {
            $aes.Dispose()
            $sha.Dispose()
        }
    }

    if ($CaptureUiFlow) {
        Write-VisualPersistentState $packagePath
    }

    $args = @("--run-mode", "release", "--pak", "data.pak", "--compiled", "scripts/scripts.ariac")
    $process = Start-Process -FilePath $exePath -ArgumentList $args -WorkingDirectory $packagePath -PassThru
    try {
        $deadline = (Get-Date).AddSeconds($WaitSeconds)
        do {
            Start-Sleep -Milliseconds 250
            $process.Refresh()
            if ($process.HasExited) {
                throw "Packaged app exited before visual capture. Exit code: $($process.ExitCode)"
            }
        } while ($process.MainWindowHandle -eq 0 -and (Get-Date) -lt $deadline)

        if ($process.MainWindowHandle -eq 0) {
            throw "Packaged app window was not created before timeout."
        }

        [AriaVisualCaptureWin32]::SetForegroundWindow($process.MainWindowHandle) | Out-Null
        Start-Sleep -Seconds $StabilizeSeconds

        function Get-ClientMetrics {
            $rect = New-Object AriaVisualCaptureWin32+RECT
            if (-not [AriaVisualCaptureWin32]::GetClientRect($process.MainWindowHandle, [ref]$rect)) {
                throw "Could not read packaged app client bounds."
            }

            $width = $rect.Right - $rect.Left
            $height = $rect.Bottom - $rect.Top
            if ($width -le 0 -or $height -le 0) {
                throw "Invalid packaged app client bounds: ${width}x${height}."
            }
            $origin = New-Object AriaVisualCaptureWin32+POINT
            $origin.X = 0
            $origin.Y = 0
            if (-not [AriaVisualCaptureWin32]::ClientToScreen($process.MainWindowHandle, [ref]$origin)) {
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
                if ($ratio -lt $MinNonBlankRatio) {
                    throw "Visual capture '$Name' looks blank. Non-blank sample ratio $ratio is below $MinNonBlankRatio."
                }

                $capturePath = Join-Path $CurrentDir $Name
                $bitmap.Save($capturePath, [System.Drawing.Imaging.ImageFormat]::Png)
                Write-Host "Captured visual baseline candidate: $capturePath"
                Write-Host "Non-blank sample ratio for ${Name}: $ratio"
            }
            finally {
                if ($graphics) { $graphics.Dispose() }
                if ($bitmap) { $bitmap.Dispose() }
            }
        }

        function ClickClient {
            param([int]$X, [int]$Y, [switch]$Right)
            $metrics = Get-ClientMetrics
            [AriaVisualCaptureWin32]::SetForegroundWindow($process.MainWindowHandle) | Out-Null
            [AriaVisualCaptureWin32]::SetCursorPos($metrics.X + $X, $metrics.Y + $Y) | Out-Null
            Start-Sleep -Milliseconds 80
            if ($Right) {
                [AriaVisualCaptureWin32]::mouse_event(0x0008, 0, 0, 0, [UIntPtr]::Zero)
                Start-Sleep -Milliseconds 80
                [AriaVisualCaptureWin32]::mouse_event(0x0010, 0, 0, 0, [UIntPtr]::Zero)
            } else {
                [AriaVisualCaptureWin32]::mouse_event(0x0002, 0, 0, 0, [UIntPtr]::Zero)
                Start-Sleep -Milliseconds 80
                [AriaVisualCaptureWin32]::mouse_event(0x0004, 0, 0, 0, [UIntPtr]::Zero)
            }
        }

        CaptureClient $CaptureName

        if ($CaptureUiFlow) {
            ClickClient 640 420
            Start-Sleep -Seconds 2
            CaptureClient "config-screen.png"
            ClickClient 640 612
            Start-Sleep -Seconds 5

            ClickClient 640 470
            Start-Sleep -Seconds 2
            CaptureClient "extra-screen.png"
            ClickClient 640 286
            Start-Sleep -Seconds 2
            CaptureClient "gallery-screen.png"
            ClickClient 640 710
            Start-Sleep -Seconds 2
            ClickClient 640 584
            Start-Sleep -Seconds 5

            ClickClient 640 320
            Start-Sleep -Seconds 3
            CaptureClient "chapter-select.png"

            ClickClient 640 552
            Start-Sleep -Seconds 8
            CaptureClient "nvl-screen.png"

            for ($i = 0; $i -lt 14; $i++) {
                ClickClient 640 500
                Start-Sleep -Milliseconds 450
            }
            Start-Sleep -Seconds 1
            CaptureClient "adv-screen.png"

            ClickClient 1000 500 -Right
            Start-Sleep -Seconds 1
            CaptureClient "right-menu.png"

            ClickClient 88 68
            Start-Sleep -Seconds 1
            CaptureClient "save-menu.png"

            ClickClient 1000 500 -Right
            Start-Sleep -Milliseconds 500
            ClickClient 1000 500 -Right
            Start-Sleep -Milliseconds 800
            ClickClient 88 128
            Start-Sleep -Seconds 1
            CaptureClient "load-menu.png"

            ClickClient 1000 500 -Right
            Start-Sleep -Milliseconds 500
            ClickClient 1000 500 -Right
            Start-Sleep -Milliseconds 800
            ClickClient 104 188
            Start-Sleep -Seconds 1
            CaptureClient "backlog-menu.png"
        }
    }
    finally {
        if (-not $process.HasExited) {
            $process.CloseMainWindow() | Out-Null
            if (-not $process.WaitForExit(5000)) {
                $process.Kill()
                $process.WaitForExit()
            }
        }
    }
}

$checklist = Join-Path $CurrentDir "capture-checklist.md"
@"
# Visual Regression Capture Checklist

- title screen
- chapter select
- ADV textbox
- NVL screen
- save menu
- load menu
- backlog menu
- right-click menu
- settings screen
- gallery screen

Save screenshots in this directory, then compare against:

$BaselineDir
"@ | Set-Content -Path $checklist -Encoding UTF8

Write-Host "Visual regression capture directory: $CurrentDir"
Write-Host "Checklist: $checklist"
