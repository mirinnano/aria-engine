param(
    [string]$Project = "src/AriaEngine/AriaEngine.csproj",
    [string]$Version = "dev",
    [string]$Runtime = "win-x64",
    [string]$Configuration = "Release",
    [string]$OutputRoot = "artifacts/release",
    [string]$InitScript = "init.aria",
    [string]$MainScript = "assets/scripts/main.aria",
    [string]$PublishFlavor = "win-x64-fd-singlefile",
    [ValidateSet("Debug", "Demo", "Release")]
    [string]$Profile = "Release",
    [bool]$SelfContained = $false,
    [bool]$SingleFile = $true,
    [bool]$PublishTrimmed = $false,
    [bool]$PublishAot = $false,
    [switch]$KeepRawAssets,
    [switch]$SkipRestore,
    [switch]$SkipPublish,
    [switch]$NoZip,
    [string]$ReleaseNotes = "",
    [switch]$Sign,
    [switch]$SteamBuild,
    [string]$SteamAppId = ""
)

$ErrorActionPreference = "Stop"

function Initialize-AriaHostEnvironment {
    $userProfile = if ([string]::IsNullOrWhiteSpace($env:USERPROFILE)) { "C:\Users\Default" } else { $env:USERPROFILE }
    if ([string]::IsNullOrWhiteSpace($env:SystemRoot)) { $env:SystemRoot = "C:\WINDOWS" }
    if ([string]::IsNullOrWhiteSpace($env:OS)) { $env:OS = "Windows_NT" }
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

function Invoke-AriaCli {
    param([string]$CliPath, [string[]]$Arguments, [string]$WorkingDirectory = "")

    if ([IO.Path]::GetExtension($CliPath).Equals(".dll", [StringComparison]::OrdinalIgnoreCase)) {
        Invoke-Checked dotnet (@($CliPath) + $Arguments) $WorkingDirectory
        return
    }

    Write-Host ("$CliPath " + ($Arguments -join " "))
    if ([string]::IsNullOrWhiteSpace($WorkingDirectory)) {
        $process = Start-Process -FilePath $CliPath -ArgumentList $Arguments -Wait -PassThru
    } else {
        $process = Start-Process -FilePath $CliPath -ArgumentList $Arguments -WorkingDirectory $WorkingDirectory -Wait -PassThru
    }
    if ($process.ExitCode -ne 0) {
        throw "Command failed: $CliPath $($Arguments -join ' ')"
    }
}

function Copy-IfExists {
    param([string]$Path, [string]$Destination)
    if (Test-Path $Path) {
        Copy-Item -LiteralPath $Path -Destination $Destination -Recurse -Force
    }
}

function Remove-WindowsPackageNoise {
    param([string]$Root)

    foreach ($name in @("libzstd.dylib", "libzstd.so")) {
        Get-ChildItem -LiteralPath $Root -Recurse -File -Filter $name -ErrorAction SilentlyContinue |
            ForEach-Object { Remove-Item -LiteralPath $_.FullName -Force }
    }

    Get-ChildItem -LiteralPath $Root -Recurse -File -Filter "*.pdb" -ErrorAction SilentlyContinue |
        ForEach-Object { Remove-Item -LiteralPath $_.FullName -Force }
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
$effectiveSelfContained = [bool]($SelfContained -or $PublishAot)
$effectivePublishTrimmed = [bool]($PublishTrimmed -or $PublishAot)
$publishSingleFileForSdk = [bool]($SingleFile -and -not $PublishAot)
$artifactSingleFile = [bool]($SingleFile -or $PublishAot)
$profileLabel = $Profile.ToLowerInvariant()
$profileProductionMode = -not $Profile.Equals("Debug", [StringComparison]::OrdinalIgnoreCase)
$browserOpenAllowlist = @(
    "store.steampowered.com",
    "twitter.com",
    "x.com",
    "ponkotsu-soft.vercel.app"
)

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
            $selfContainedValue = $effectiveSelfContained.ToString().ToLowerInvariant()
            if (-not $SkipRestore) {
                $restoreArgs = @("restore", $Project, "-r", $Runtime, "/p:NuGetAudit=false")
                if ($PublishAot) {
                    $restoreArgs += "/p:PublishAot=true"
                }
                Invoke-Checked dotnet $restoreArgs
            }
            $publishArgs = @(
                "publish", $Project,
                "-c", $Configuration,
                "-r", $Runtime,
                "--self-contained", $selfContainedValue,
                "--no-restore",
                "-o", $publishStageDir,
                "/p:AriaCompileOnPublish=false",
                "/p:NuGetAudit=false",
                "/p:PublishSingleFile=$($publishSingleFileForSdk.ToString().ToLowerInvariant())",
                "/p:PublishTrimmed=$($effectivePublishTrimmed.ToString().ToLowerInvariant())",
                "/p:PublishAot=$($PublishAot.ToString().ToLowerInvariant())",
                "/p:IncludeNativeLibrariesForSelfExtract=true",
                "/p:DebugType=none",
                "/p:DebugSymbols=false"
            )
            Invoke-Checked dotnet $publishArgs
        }

        Copy-Item -Path (Join-Path $publishStageDir "*") -Destination $publishDir -Recurse -Force
        if ($PublishAot -and (Test-Path (Join-Path $publishDir "coreclr.dll"))) {
            throw "NativeAOT publish did not produce native output. Run restore with /p:PublishAot=true on a matching Windows host before packaging."
        }
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

if ($SteamBuild -and -not [string]::IsNullOrWhiteSpace($SteamAppId)) {
    Set-Content -Path (Join-Path $publishDir "steam_appid.txt") -Value $SteamAppId -Encoding ASCII
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
$publishedDll = Join-Path $publishDir "AriaEngine.dll"
$publishedExe = Join-Path $publishDir "AriaEngine.exe"
$cliHostFile = ""
if (Test-Path $publishedDll) {
    $cliHostFile = (Resolve-Path $publishedDll).Path
} elseif (Test-Path $publishedExe) {
    $cliHostFile = (Resolve-Path $publishedExe).Path
} else {
    $cliHostFile = Resolve-AriaCliAssembly $engineDir $Configuration $Runtime
}
if ([string]::IsNullOrWhiteSpace($cliHostFile)) {
    throw "AriaEngine.dll or AriaEngine.exe for CLI packaging was not found. Disable SingleFile or run restore/publish before packaging."
}
Invoke-AriaCli $cliHostFile $compileArgs $publishDir

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

$localizationManifest = Join-Path $publishDir "assets\i18n\locales.json"
if (-not (Test-Path $localizationManifest)) {
    throw "Localization manifest was not found: assets\i18n\locales.json"
}
$localizationConfig = Get-Content -Raw -LiteralPath $localizationManifest | ConvertFrom-Json
$localizationLanguages = @($localizationConfig.languages)
$scenarioFiles = @($localizationConfig.scenarioFiles)
$scenarioStatus = [ordered]@{}
if ($localizationConfig.scenarioStatus) {
    foreach ($property in $localizationConfig.scenarioStatus.PSObject.Properties) {
        $scenarioStatus[$property.Name] = [string]$property.Value
    }
}
$steamSubtitleLanguages = @(
    $scenarioStatus.GetEnumerator() |
        Where-Object { $_.Value -eq "source" -or $_.Value -eq "complete" } |
        ForEach-Object { $_.Key }
)

Invoke-AriaCli $cliHostFile $packArgs $publishDir

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

Remove-WindowsPackageNoise $publishDir

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
    targetRuntime = $runtimeLabel
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
    profile = $Profile.ToLowerInvariant()
    content = [ordered]@{
        isDemo = $Profile.Equals("Demo", [StringComparison]::OrdinalIgnoreCase)
        demoEndLabel = "demo_end"
        lastDemoScenario = "scenario_05"
    }
    runtime = [ordered]@{
        runMode = "release"
        productionMode = [bool]$profileProductionMode
        devHotkeys = -not $profileProductionMode
    }
    security = [ordered]@{
        browserOpenPolicy = [ordered]@{
            schemes = @("https", "http")
            allowlist = $browserOpenAllowlist
        }
    }
    packaging = [ordered]@{
        publishFlavor = $PublishFlavor
        selfContained = [bool]$effectiveSelfContained
        singleFile = [bool]$artifactSingleFile
    publishTrimmed = [bool]$effectivePublishTrimmed
        publishAot = [bool]$PublishAot
        rawAssetsIncluded = [bool]$KeepRawAssets
        pakEncrypted = [bool]$pakEncrypted
        compiledScripts = $null
        pak = "v3-split"
    }
    localization = [ordered]@{
        manifest = "assets/i18n/locales.json"
        defaultLanguage = $localizationConfig.defaultLanguage
        languages = $localizationLanguages
        scenarioRoot = $localizationConfig.scenarioRoot
        scenarioFiles = $scenarioFiles
        scenarioStatus = $scenarioStatus
        steamSubtitleLanguages = $steamSubtitleLanguages
    }
    steam = [ordered]@{
        steamCompatible = [bool]$SteamBuild
        appId = $SteamAppId
        appIdFile = if ($SteamBuild -and -not [string]::IsNullOrWhiteSpace($SteamAppId)) { "steam_appid.txt" } else { "" }
        cloudSavePath = "saves/"
    }
    signing = [ordered]@{
        requested = [bool]$Sign
        signed = [bool]$allSigned
        trusted = [bool]$allTrusted
        files = $signatureStatus
    }
    productionRunArgs = @("--run-mode", "release", "--profile", $Profile.ToLowerInvariant())
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
