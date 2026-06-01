param(
    [string]$WebPackageDir = "artifacts/web/AriaEngine-dev-web",
    [string]$OutputDir = "artifacts/visual/web",
    [string]$Browser = "Chrome",
    [int]$ViewportWidth = 1280,
    [int]$ViewportHeight = 720,
    [switch]$InstallBrowsers
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$runner = Join-Path $PSScriptRoot "web-capture-visuals.mjs"

if (-not (Test-Path -LiteralPath $runner -PathType Leaf)) {
    throw "Web visual capture runner missing: $runner"
}

$repoNodeModules = Join-Path $repoRoot "node_modules"
$toolNodeModules = Join-Path $repoRoot "artifacts/obj/web-browser-qa-node/node_modules"
if (Test-Path -LiteralPath (Join-Path $repoNodeModules "playwright")) {
    $env:ARIA_PLAYWRIGHT_NODE_MODULES = $repoNodeModules
} else {
    if (-not (Test-Path -LiteralPath (Join-Path $toolNodeModules "playwright"))) {
        $toolRoot = Split-Path -Parent $toolNodeModules
        New-Item -ItemType Directory -Force -Path $toolRoot | Out-Null
        & npm install --prefix $toolRoot --no-save playwright@1.49.1
        if ($LASTEXITCODE -ne 0) { throw "npm install playwright failed." }
    }
    $env:ARIA_PLAYWRIGHT_NODE_MODULES = $toolNodeModules
}

if ($InstallBrowsers) {
    $playwrightCli = Join-Path $env:ARIA_PLAYWRIGHT_NODE_MODULES ".bin/playwright.cmd"
    if (Test-Path -LiteralPath $playwrightCli) {
        & $playwrightCli install chromium webkit
    } else {
        & npx playwright install chromium webkit
    }
    if ($LASTEXITCODE -ne 0) { throw "npx playwright install failed." }
}

& node $runner `
    --webPackageDir $WebPackageDir `
    --outputDir $OutputDir `
    --browser $Browser `
    --viewportWidth $ViewportWidth `
    --viewportHeight $ViewportHeight

if ($LASTEXITCODE -ne 0) {
    throw "Web visual capture failed."
}
