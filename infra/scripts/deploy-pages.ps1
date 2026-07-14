# Deploy Activity shell + WebGL to Cloudflare Pages from a clean staging folder
# (avoids junk like .wrangler cache or accidental .exe files).
#
# Usage (PowerShell), after WebGL is in web/activity-shell/Build/:
#   .\infra\scripts\deploy-pages.ps1
$ErrorActionPreference = "Stop"
$root = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$shell = Join-Path $root "web\activity-shell"
$project = if ($env:PAGES_PROJECT) { $env:PAGES_PROJECT } else { "baraki" }

$required = @(
    "index.html",
    "boot.js",
    "config.js",
    "Build\BARAKI.loader.js",
    "Build\BARAKI.data.unityweb",
    "Build\BARAKI.framework.js.unityweb",
    "Build\BARAKI.wasm.unityweb",
    "vendor\discord-embedded-app-sdk.js"
)

foreach ($rel in $required) {
    $path = Join-Path $shell $rel
    if (-not (Test-Path $path)) {
        throw "Missing required file: $path"
    }
    $size = (Get-Item $path).Length
    $limit = 25 * 1024 * 1024
    if ($size -gt $limit) {
        throw "File exceeds Cloudflare Pages 25 MiB limit: $path ($([math]::Round($size/1MB,2)) MiB)"
    }
}

# Refuse to deploy if unexpected large binaries are sitting in the shell tree.
$bad = Get-ChildItem $shell -Recurse -Force -File -ErrorAction SilentlyContinue | Where-Object {
    $_.Extension -match '\.(exe|dll|msi|bat|cmd|ps1)$' -or
    ($_.Length -gt (25 * 1024 * 1024) -and $_.FullName -notlike "*\Build\BARAKI.*")
}
if ($bad) {
    Write-Host "Refusing deploy - unexpected / oversized files in activity-shell:" -ForegroundColor Red
    $bad | ForEach-Object { Write-Host ("  {0} ({1:N2} MiB)" -f $_.FullName, ($_.Length / 1MB)) }
    throw "Remove the files above, then retry."
}

$stage = Join-Path $env:TEMP ("baraki-pages-stage-" + [guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $stage | Out-Null
try {
    Copy-Item (Join-Path $shell "index.html") $stage
    Copy-Item (Join-Path $shell "boot.js") $stage
    Copy-Item (Join-Path $shell "config.js") $stage
    New-Item -ItemType Directory -Path (Join-Path $stage "Build") | Out-Null
    Copy-Item (Join-Path $shell "Build\BARAKI.*") (Join-Path $stage "Build")
    New-Item -ItemType Directory -Path (Join-Path $stage "vendor") | Out-Null
    Copy-Item (Join-Path $shell "vendor\*") (Join-Path $stage "vendor") -Recurse

    Write-Host "Staging clean deploy from: $stage"
    Write-Host "Project: $project"
    Get-ChildItem $stage -Recurse -File | ForEach-Object {
        Write-Host ("  {0:N2} MiB  {1}" -f ($_.Length / 1MB), $_.FullName.Substring($stage.Length + 1))
    }

    Push-Location $stage
    try {
        npx --yes wrangler pages deploy . --project-name $project --commit-dirty=true
    } finally {
        Pop-Location
    }
} finally {
    Remove-Item $stage -Recurse -Force -ErrorAction SilentlyContinue
}
