param(
    [string]$Project = "src/AriaEngine/AriaEngine.csproj",
    [string]$Version = "dev",
    [string]$Runtime = "win-x64",
    [string]$Configuration = "Release",
    [string]$OutputRoot = "artifacts/release",
    [string]$InitScript = "init.aria",
    [string]$MainScript = "assets/scripts/main.aria",
    [bool]$SelfContained = $false,
    [bool]$SingleFile = $true,
    [switch]$KeepRawAssets,
    [switch]$SkipRestore,
    [switch]$SkipPublish,
    [switch]$NoZip,
    [string]$ReleaseNotes = "",
    [switch]$Sign
)

$ErrorActionPreference = "Stop"

function Initialize-AriaHostEnvironment {
    $userProfile = if ([string]::IsNullOrWhiteSpace($env:USERPROFILE)) { "C:\Users\Default" } else { $env:USERPROFILE }
    if ([string]::IsNullOrWhiteSpace($env:SystemRoot)) { $env:SystemRoot = "C:\WINDOWS" }
    if ([string]::IsNullOrWhiteSpace($env:windir)) { $env:windir = $env:SystemRoot }
    if ([string]::IsNullOrWhiteSpace($env:ComSpec)) { $env:ComSpec = Join-Path $env:SystemRoot "system32\cmd.exe" }
    if ([string]::IsNullOrWhiteSpace($env:HOMEDRIVE)) { $env:HOMEDRIVE = Split-Path -Qualifier $userProfile }
    if ([string]::IsNullOrWhiteSpace($env:HOMEPATH)) { $env:HOMEPATH = $userProfile.Substring($env:HOMEDRIVE.Length) }
    if ([string]::IsNullOrWhiteSpace($env:APPDATA)) { $env:APPDATA = Join-Path $userProfile "AppData\Roaming" }
    if ([string]::IsNullOrWhiteSpace($env:LOCALAPPDATA)) { $env:LOCALAPPDATA = Join-Path $userProfile "AppData\Local" }
    if ([string]::IsNullOrWhiteSpace($env:ProgramData)) { $env:ProgramData = "C:\ProgramData" }
    if ([string]::IsNullOrWhiteSpace($env:ALLUSERSPROFILE)) { $env:ALLUSERSPROFILE = "C:\ProgramData" }
    if ([string]::IsNullOrWhiteSpace($env:ProgramFiles)) { $env:ProgramFiles = "C:\Program Files" }
    if ([string]::IsNullOrWhiteSpace($env:ProgramW6432)) { $env:ProgramW6432 = "C:\Program Files" }
    if ([string]::IsNullOrWhiteSpace(${env:ProgramFiles(x86)})) { ${env:ProgramFiles(x86)} = "C:\Program Files (x86)" }
    if ([string]::IsNullOrWhiteSpace($env:CommonProgramFiles)) { $env:CommonProgramFiles = "C:\Program Files\Common Files" }
    if ([string]::IsNullOrWhiteSpace(${env:CommonProgramFiles(x86)})) { ${env:CommonProgramFiles(x86)} = "C:\Program Files (x86)\Common Files" }
    if ([string]::IsNullOrWhiteSpace($env:DOTNET_CLI_HOME)) { $env:DOTNET_CLI_HOME = $userProfile }
    if ([string]::IsNullOrWhiteSpace($env:NUGET_PACKAGES)) { $env:NUGET_PACKAGES = Join-Path $userProfile ".nuget\packages" }
    $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
}

function Invoke-Checked {
    param([string]$File, [string[]]$Arguments, [string]$WorkingDirectory = "")
    Write-Host ("$File " + ($Arguments -join " "))
    if ([string]::IsNullOrWhiteSpace($WorkingDirectory)) {
        & $File @Arguments
        if ($LASTEXITCODE -ne 0) {
            throw "Command failed: $File $($Arguments -join ' ')"
        }
    } else {
        Push-Location $WorkingDirectory
        try {
            & $File @Arguments
            if ($LASTEXITCODE -ne 0) {
                throw "Command failed: $File $($Arguments -join ' ')"
            }
        } finally {
            Pop-Location
        }
    }
}

function Copy-IfExists {
    param([string]$Path, [string]$Destination)
    if (Test-Path $Path) {
        Copy-Item -LiteralPath $Path -Destination $Destination -Recurse -Force
    }
}

function Resolve-AriaCliAssembly {
    param(
        [string]$EngineDirectory,
        [string]$Configuration,
        [string]$Runtime
    )

    $binDir = Join-Path $EngineDirectory "bin"
    if (-not (Test-Path $binDir)) {
        return ""
    }

    $candidates = Get-ChildItem -Path $binDir -Filter "AriaEngine.dll" -Recurse -File |
        Where-Object {
            $_.FullName -like "*\bin\$Configuration\*" -and
            ([string]::IsNullOrWhiteSpace($Runtime) -or $_.FullName -like "*\$Runtime\*")
        } |
        Sort-Object LastWriteTimeUtc -Descending

    if ($candidates.Count -eq 0) {
        return ""
    }
    return $candidates[0].FullName
}

function Get-AriaRelativePath {
    param([string]$BasePath, [string]$Path)

    $baseFull = [IO.Path]::GetFullPath($BasePath).TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
    $pathFull = [IO.Path]::GetFullPath($Path)
    $baseUri = [Uri]::new($baseFull)
    $pathUri = [Uri]::new($pathFull)
    return [Uri]::UnescapeDataString($baseUri.MakeRelativeUri($pathUri).ToString()).Replace("/", "\")
}

Initialize-AriaHostEnvironment

$repoRoot = (Resolve-Path ".").Path
$projectPath = Resolve-Path $Project
$engineDir = Split-Path -Parent $projectPath
$runtimeLabel = if ([string]::IsNullOrWhiteSpace($Runtime)) { "portable" } else { $Runtime }
$releaseName = "AriaEngine-$Version-$runtimeLabel"
$releaseDir = Join-Path $OutputRoot $releaseName
$publishDir = Join-Path $releaseDir "app"
$distDir = Join-Path $releaseDir "dist"
$zipPath = Join-Path $distDir "$releaseName.zip"

if (Test-Path $releaseDir) {
    Remove-Item -LiteralPath $releaseDir -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $publishDir, $distDir | Out-Null

if (-not $SkipPublish) {
    $publishStageDir = Join-Path ([IO.Path]::GetTempPath()) ("aria-publish-" + [Guid]::NewGuid().ToString("N"))
    New-Item -ItemType Directory -Force -Path $publishStageDir | Out-Null
    try {
        if ([string]::IsNullOrWhiteSpace($Runtime)) {
            if (-not $SkipRestore) {
                Invoke-Checked dotnet @("restore", $Project, "/p:NuGetAudit=false")
            }
            Invoke-Checked dotnet @("publish", $Project, "-c", $Configuration, "--no-restore", "-o", $publishStageDir, "/p:AriaCompileOnPublish=false", "/p:NuGetAudit=false", "/p:DebugType=none", "/p:DebugSymbols=false")
        } else {
            $selfContainedValue = $SelfContained.ToString().ToLowerInvariant()
            if (-not $SkipRestore) {
                Invoke-Checked dotnet @("restore", $Project, "-r", $Runtime, "/p:NuGetAudit=false")
            }
            Invoke-Checked dotnet @("publish", $Project, "-c", $Configuration, "-r", $Runtime, "--self-contained", $selfContainedValue, "--no-restore", "-o", $publishStageDir, "/p:AriaCompileOnPublish=false", "/p:NuGetAudit=false", "/p:PublishSingleFile=$($SingleFile.ToString().ToLowerInvariant())", "/p:IncludeNativeLibrariesForSelfExtract=true", "/p:DebugType=none", "/p:DebugSymbols=false")
        }

        Copy-Item -Path (Join-Path $publishStageDir "*") -Destination $publishDir -Recurse -Force
    }
    finally {
        if (Test-Path $publishStageDir) {
            Remove-Item -LiteralPath $publishStageDir -Recurse -Force
        }
    }
}

Copy-IfExists (Join-Path $engineDir $InitScript) $publishDir
Copy-IfExists (Join-Path $engineDir "assets") (Join-Path $publishDir "assets")
Copy-IfExists (Join-Path $engineDir "chapters.json") $publishDir
Copy-IfExists (Join-Path $engineDir "characters.json") $publishDir
Copy-IfExists (Join-Path $engineDir "hints.txt") $publishDir

if ([string]::IsNullOrWhiteSpace($ReleaseNotes)) {
    $candidateNotes = Join-Path $repoRoot "docs/release/release-notes-$Version.md"
    if (Test-Path $candidateNotes) {
        $ReleaseNotes = $candidateNotes
    }
}
if (-not [string]::IsNullOrWhiteSpace($ReleaseNotes)) {
    if (-not (Test-Path $ReleaseNotes)) {
        throw "Release notes file not found: $ReleaseNotes"
    }
    Copy-Item -LiteralPath $ReleaseNotes -Destination (Join-Path $publishDir "release-notes.md") -Force
}

$packageReadme = Join-Path $repoRoot "docs/release/package-readme.md"
if (-not (Test-Path $packageReadme)) {
    throw "Package README file not found: $packageReadme"
}
Copy-Item -LiteralPath $packageReadme -Destination (Join-Path $publishDir "README.md") -Force

$configSource = Join-Path $engineDir "config.json"
if (Test-Path $configSource) {
    Copy-Item -LiteralPath $configSource -Destination (Join-Path $publishDir "config.template.json") -Force
}

$engineExecutable = Join-Path $publishDir "AriaEngine.exe"
if (-not (Test-Path $engineExecutable)) {
    $engineExecutable = Join-Path $publishDir "AriaEngine.dll"
}
if (-not (Test-Path $engineExecutable)) {
    throw "Published engine executable was not found."
}
$engineExecutable = (Resolve-Path $engineExecutable).Path

$compiledDir = Join-Path $publishDir "scripts"
$compiledOut = Join-Path $compiledDir "scripts.ariac"
$pakOut = Join-Path $publishDir "data.pak"
New-Item -ItemType Directory -Force -Path $compiledDir | Out-Null

$pakEncrypted = -not [string]::IsNullOrWhiteSpace($env:ARIA_PACK_KEY)
$initFullPath = Join-Path $engineDir $InitScript
$compileArgs = @("aria-compile", "--init", $InitScript, "--main", $MainScript, "--out", "scripts/scripts.ariac")
$packArgs = @("aria-pack", "build", "--input", "assets", "--init", $initFullPath, "--compiled", "scripts/scripts.ariac", "--format", "v3", "--split", "--output", "data.pak")
if ($pakEncrypted) {
    $compileArgs += @("--key", $env:ARIA_PACK_KEY)
    $packArgs += @("--key", $env:ARIA_PACK_KEY)
}
$cliAssembly = Resolve-AriaCliAssembly $engineDir $Configuration $Runtime
if ([string]::IsNullOrWhiteSpace($cliAssembly)) {
    $publishedDll = Join-Path $publishDir "AriaEngine.dll"
    if (Test-Path $publishedDll) {
        $cliAssembly = (Resolve-Path $publishedDll).Path
    }
}
# SingleFile publish produces AriaEngine.exe instead of AriaEngine.dll
if ([string]::IsNullOrWhiteSpace($cliAssembly)) {
    $publishedExe = Join-Path $publishDir "AriaEngine.exe"
    if (Test-Path $publishedExe) {
        $cliAssembly = (Resolve-Path $publishedExe).Path
    }
}
if ([string]::IsNullOrWhiteSpace($cliAssembly)) {
    throw "AriaEngine.dll or AriaEngine.exe for CLI packaging was not found. Disable SingleFile or run restore/publish before packaging."
}
$dotnetCompileArgs = @($cliAssembly) + $compileArgs
$dotnetPackArgs = @($cliAssembly) + $packArgs
Invoke-Checked dotnet $dotnetCompileArgs $publishDir

if (-not $KeepRawAssets) {
    $rawScriptsOut = Join-Path $publishDir "assets\scripts"
    if (Test-Path $rawScriptsOut) {
        Remove-Item -LiteralPath $rawScriptsOut -Recurse -Force
    }
    $rawInitOut = Join-Path $publishDir $InitScript
    if (Test-Path $rawInitOut) {
        Remove-Item -LiteralPath $rawInitOut -Force
    }
}

Invoke-Checked dotnet $dotnetPackArgs $publishDir

# v3 split pak does not require compiled script bundle; .aria files are stored directly in scenario.aris
# if (-not (Test-Path $compiledOut)) {
#     throw "Compiled script bundle was not generated."
# }
$v3PakFiles = Get-ChildItem -Path $publishDir -Filter "*.ari?" -File
if ($v3PakFiles.Count -eq 0) {
    throw "No v3 split pak files were generated."
}

if (-not $KeepRawAssets) {
    $assetsOut = Join-Path $publishDir "assets"
    if (Test-Path $assetsOut) { Remove-Item -LiteralPath $assetsOut -Recurse -Force }
}

if ($Sign) {
    $exes = Get-ChildItem -Path $publishDir -Filter "*.exe" -Recurse
    $dlls = Get-ChildItem -Path $publishDir -Filter "*.dll" -Recurse
    foreach ($file in @($exes) + @($dlls)) {
        & "$PSScriptRoot\sign.ps1" -FilePath $file.FullName
    }
}

$signatureFiles = Get-ChildItem -Path $publishDir -Include "*.exe","*.dll" -Recurse -File | Sort-Object FullName
$signatureStatus = @()
foreach ($file in $signatureFiles) {
    $sig = Get-AuthenticodeSignature -FilePath $file.FullName
    $signatureStatus += [ordered]@{
        path = (Get-AriaRelativePath $publishDir $file.FullName).Replace("\", "/")
        status = $sig.Status.ToString()
        signer = if ($sig.SignerCertificate) { $sig.SignerCertificate.Subject } else { "" }
    }
}
$allSigned = $signatureStatus.Count -gt 0 -and ($signatureStatus | Where-Object { [string]::IsNullOrWhiteSpace($_.signer) }).Count -eq 0
$allTrusted = $signatureStatus.Count -gt 0 -and ($signatureStatus | Where-Object { $_.status -ne "Valid" }).Count -eq 0

$manifest = [ordered]@{
    name = "AriaEngine"
    version = $Version
    runtime = $runtimeLabel
    configuration = $Configuration
    generatedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    initScript = $InitScript
    mainScript = $MainScript
    releaseNotes = if (Test-Path (Join-Path $publishDir "release-notes.md")) { "release-notes.md" } else { "" }
    compatibility = [ordered]@{
        saveSchema = 3
        persistentSchema = 2
        configSchema = 1
        reservedEngineActions = @("save", "load", "backlog", "lookback", "rmenu")
    }
    packaging = [ordered]@{
        rawAssetsIncluded = [bool]$KeepRawAssets
        pakEncrypted = [bool]$pakEncrypted
        compiledScripts = $null
        pak = "v3-split"
    }
    signing = [ordered]@{
        requested = [bool]$Sign
        signed = [bool]$allSigned
        trusted = [bool]$allTrusted
        files = $signatureStatus
    }
    productionRunArgs = @("--run-mode", "release")
    files = @()
}

$files = Get-ChildItem -Path $publishDir -Recurse -File | Sort-Object FullName
foreach ($file in $files) {
    $relative = (Get-AriaRelativePath $publishDir $file.FullName).Replace("\", "/")
    $hash = Get-FileHash -Algorithm SHA256 -LiteralPath $file.FullName
    $manifest.files += [ordered]@{
        path = $relative
        bytes = $file.Length
        sha256 = $hash.Hash.ToLowerInvariant()
    }
}

$manifestPath = Join-Path $publishDir "manifest.json"
$manifest | ConvertTo-Json -Depth 6 | Set-Content -Path $manifestPath -Encoding UTF8

$checksumPath = Join-Path $publishDir "checksums.txt"
Get-ChildItem -Path $publishDir -Recurse -File |
    Sort-Object FullName |
    ForEach-Object {
        $relative = (Get-AriaRelativePath $publishDir $_.FullName).Replace("\", "/")
        $hash = Get-FileHash -Algorithm SHA256 -LiteralPath $_.FullName
        "$($hash.Hash.ToLowerInvariant())  $relative"
    } | Set-Content -Path $checksumPath -Encoding ASCII

if (-not $NoZip) {
    if (Test-Path $zipPath) { Remove-Item -LiteralPath $zipPath -Force }
    Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $zipPath -Force
}

Write-Host "Package ready: $publishDir"
if (-not $NoZip) { Write-Host "Zip ready: $zipPath" }
