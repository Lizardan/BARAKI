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
$Root = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
if ([string]::IsNullOrWhiteSpace($EnvFile)) {
    $EnvFile = Join-Path $Root "infra\playtest.env"
}

if (-not (Get-Command cloudflared -ErrorAction SilentlyContinue)) {
    throw "cloudflared not found on PATH."
}

Write-Host "Ensuring Cloudflare login (browser may open)..."
& cloudflared tunnel login
if ($LASTEXITCODE -ne 0) {
    throw "cloudflared tunnel login failed."
}

$existing = (& cloudflared tunnel list 2>&1 | Out-String)
if ($existing -notmatch [regex]::Escape($TunnelName)) {
    Write-Host "Creating tunnel: $TunnelName"
    & cloudflared tunnel create $TunnelName
    if ($LASTEXITCODE -ne 0) {
        throw "cloudflared tunnel create failed."
    }
}
else {
    Write-Host "Tunnel already exists: $TunnelName"
}

$listRaw = & cloudflared tunnel list --output json 2>&1 | Out-String
$listJson = $listRaw | ConvertFrom-Json
$tunnel = $listJson | Where-Object { $_.name -eq $TunnelName } | Select-Object -First 1
if (-not $tunnel) {
    throw "Could not resolve tunnel id for '$TunnelName' after create/list."
}
$tunnelId = [string]$tunnel.id
Write-Host "Tunnel id: $tunnelId"

if ([string]::IsNullOrWhiteSpace($Hostname)) {
    $Hostname = "$tunnelId.cfargotunnel.com"
    Write-Host "Using default hostname: $Hostname"
}
else {
    Write-Host "Routing DNS $Hostname -> tunnel $TunnelName"
    & cloudflared tunnel route dns $TunnelName $Hostname
    if ($LASTEXITCODE -ne 0) {
        throw "cloudflared tunnel route dns failed."
    }
}

$cloudflaredDir = Join-Path $env:USERPROFILE ".cloudflared"
if (-not (Test-Path $cloudflaredDir)) {
    New-Item -ItemType Directory -Path $cloudflaredDir | Out-Null
}
$configPath = Join-Path $cloudflaredDir "config.yml"
$credPath = Join-Path $cloudflaredDir ($tunnelId + ".json")

$configLines = @(
    "tunnel: $tunnelId"
    "credentials-file: $credPath"
    ""
    "ingress:"
    "  - hostname: $Hostname"
    "    service: http://127.0.0.1:$Port"
    "  - service: http_status:404"
)
Set-Content -Path $configPath -Value $configLines -Encoding ASCII
Write-Host "Wrote $configPath"

$example = Join-Path $Root "infra\playtest.env.example"
if (-not (Test-Path $EnvFile)) {
    if (Test-Path $example) {
        Copy-Item $example $EnvFile
        Write-Host "Created $EnvFile from example - fill REGISTER_SECRET."
    }
    else {
        throw "Missing $example"
    }
}

function Upsert-EnvKey {
    param(
        [string]$Path,
        [string]$Key,
        [string]$Value
    )

    $lines = @(Get-Content -Path $Path -ErrorAction Stop)
    $found = $false
    $pattern = "^\s*" + [regex]::Escape($Key) + "\s*="
    $out = New-Object System.Collections.Generic.List[string]
    foreach ($line in $lines) {
        if ($line -match $pattern) {
            $found = $true
            [void]$out.Add("$Key=$Value")
        }
        else {
            [void]$out.Add($line)
        }
    }
    if (-not $found) {
        [void]$out.Add("$Key=$Value")
    }
    Set-Content -Path $Path -Value $out.ToArray() -Encoding UTF8
}

Upsert-EnvKey -Path $EnvFile -Key "WSS_HOST" -Value $Hostname
Upsert-EnvKey -Path $EnvFile -Key "TUNNEL_NAME" -Value $TunnelName
Upsert-EnvKey -Path $EnvFile -Key "PORT" -Value "$Port"

Write-Host ""
Write-Host "DONE - Discord Developer Portal -> URL Mappings (once):"
Write-Host "  Prefix: /wss"
Write-Host "  Target: $Hostname"
Write-Host ""
Write-Host "Also set GitHub variable WSS_PROXY_TARGET (or config.js) to the same host."
Write-Host "IMPORTANT: set REGISTER_SECRET in playtest.env (not replace-me)."
Write-Host "Then: Unity dedicated build -> Start-Playtest.bat each evening."
