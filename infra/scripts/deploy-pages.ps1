# Deploy Activity shell + WebGL to Cloudflare Pages.
# Usage (PowerShell), after WebGL build is copied into web/activity-shell/Build/:
#   .\infra\scripts\deploy-pages.ps1
$ErrorActionPreference = "Stop"
$root = Resolve-Path (Join-Path $PSScriptRoot "..\..")
Set-Location (Join-Path $root "web\activity-shell")
$project = if ($env:PAGES_PROJECT) { $env:PAGES_PROJECT } else { "baraki-activity" }
npx --yes wrangler pages deploy . --project-name $project
