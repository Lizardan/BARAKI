# One-time named Cloudflare Tunnel setup for BARAKI FREE-0.
# Creates/updates tunnel + prints Discord /wss target. Credentials stay in ~/.cloudflared (not git).
#
# Prerequisites: cloudflared on PATH, Cloudflare account login.
#
# Usage:
#   .\infra\scripts\setup-named-tunnel.ps1
#   .\infra\scripts\setup-named-tunnel.ps1 -TunnelName baraki-game -Hostname game.example.com
#
# If -Hostname is omitted, uses <tunnel-id>.cfargotunnel.com (no custom DNS needed).

param(
    [string]$TunnelName = "baraki-game",
    [string]$Hostname = "",
    [int]$Port = 7777,
    [string]$EnvFile = ""
)

$ErrorActionPreference = "Stop"
$Root = Resolve-Path (Join-Path $PSScriptRoot "..\..")
if ([string]::IsNullOrWhiteSpace($EnvFile)) {
    $EnvFile = Join-Path $Root "infra\playtest.env"
}

if (-not (Get-Command cloudflared -ErrorAction SilentlyContinue)) {
    throw "cloudflared not found on PATH."
}

Write-Host "Ensuring Cloudflare login (browser may open)…"
cloudflared tunnel login

$existing = cloudflared tunnel list 2>$null | Out-String
if ($existing -notmatch [regex]::Escape($TunnelName)) {
    Write-Host "Creating tunnel: $TunnelName"
    cloudflared tunnel create $TunnelName
} else {
    Write-Host "Tunnel already exists: $TunnelName"
}

$listJson = cloudflared tunnel list --output json | ConvertFrom-Json
$tunnel = $listJson | Where-Object { $_.name -eq $TunnelName } | Select-Object -First 1
if (-not $tunnel) {
    throw "Could not resolve tunnel id for '$TunnelName' after create/list."
}
$tunnelId = $tunnel.id
Write-Host "Tunnel id: $tunnelId"

if ([string]::IsNullOrWhiteSpace($Hostname)) {
    $Hostname = "$tunnelId.cfargotunnel.com"
    Write-Host "Using default hostname: $Hostname"
} else {
    Write-Host "Routing DNS $Hostname → tunnel $TunnelName"
    cloudflared tunnel route dns $TunnelName $Hostname
}

$cloudflaredDir = Join-Path $env:USERPROFILE ".cloudflared"
$configPath = Join-Path $cloudflaredDir "config.yml"
$credPath = Join-Path $cloudflaredDir "$tunnelId.json"

$config = @"
tunnel: $tunnelId
credentials-file: $credPath

ingress:
  - hostname: $Hostname
    service: http://127.0.0.1:$Port
  - service: http_status:404
"@
Set-Content -Path $configPath -Value $config -Encoding utf8
Write-Host "Wrote $configPath"

# Ensure playtest.env exists and update WSS_HOST / TUNNEL_NAME.
$example = Join-Path $Root "infra\playtest.env.example"
if (-not (Test-Path $EnvFile)) {
    if (Test-Path $example) {
        Copy-Item $example $EnvFile
        Write-Host "Created $EnvFile from example — fill REGISTER_SECRET."
    } else {
        throw "Missing $example"
    }
}

function Upsert-EnvKey([string]$path, [string]$key, [string]$value) {
    $lines = @(Get-Content $path)
    $found = $false
    $out = foreach ($line in $lines) {
        if ($line -match ("^\s*" + [regex]::Escape($key) + "\s*=")) {
            $found = $true
            "$key=$value"
        } else {
            $line
        }
    }
    if (-not $found) {
        $out += "$key=$value"
    }
    Set-Content -Path $path -Value $out -Encoding utf8
}

Upsert-EnvKey $EnvFile "WSS_HOST" $Hostname
Upsert-EnvKey $EnvFile "TUNNEL_NAME" $TunnelName
Upsert-EnvKey $EnvFile "PORT" "$Port"

Write-Host ""
Write-Host "DONE — Discord Developer Portal → URL Mappings (once):"
Write-Host "  Prefix: /wss"
Write-Host "  Target: $Hostname"
Write-Host ""
Write-Host "Also set web/activity-shell/config.js WSS_PROXY_TARGET to the same host"
Write-Host "(or let CI inject it). Then double-click Start-Playtest.bat each evening."
