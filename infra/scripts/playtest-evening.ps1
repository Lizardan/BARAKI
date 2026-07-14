# FREE-0 one-click playtest evening (Windows host PC).
# Reads infra/playtest.env (gitignored). Prefer named Cloudflare tunnel so Discord /wss stays fixed.
#
# Setup once:
#   1. Copy infra/playtest.env.example → infra/playtest.env and fill secrets/hosts
#   2. .\infra\scripts\setup-named-tunnel.ps1
#   3. Discord Portal URL Mapping /wss → WSS_HOST (once)
#   4. Unity → BARAKI → Build → Windows Dedicated Server (Headless)
#
# Every evening:
#   Double-click infra\scripts\Start-Playtest.bat

param(
    [string]$EnvFile = ""
)

$ErrorActionPreference = "Stop"
$Root = Resolve-Path (Join-Path $PSScriptRoot "..\..")
if ([string]::IsNullOrWhiteSpace($EnvFile)) {
    $EnvFile = Join-Path $Root "infra\playtest.env"
}

function Read-PlaytestEnv([string]$path) {
    if (-not (Test-Path $path)) {
        throw "Missing $path — copy infra/playtest.env.example to infra/playtest.env and fill values."
    }
    $map = @{}
    Get-Content $path | ForEach-Object {
        $line = $_.Trim()
        if ($line -eq "" -or $line.StartsWith("#")) { return }
        $idx = $line.IndexOf("=")
        if ($idx -lt 1) { return }
        $key = $line.Substring(0, $idx).Trim()
        $val = $line.Substring($idx + 1).Trim()
        $map[$key] = $val
    }
    return $map
}

function Require-Key($map, [string]$key) {
    if (-not $map.ContainsKey($key) -or [string]::IsNullOrWhiteSpace($map[$key])) {
        throw "playtest.env missing required key: $key"
    }
    return $map[$key].Trim()
}

$cfg = Read-PlaytestEnv $EnvFile
$MatchmakerUrl = (Require-Key $cfg "MATCHMAKER_URL").TrimEnd("/")
$RegisterSecret = Require-Key $cfg "REGISTER_SECRET"
$WssHost = Require-Key $cfg "WSS_HOST"
$TunnelName = if ($cfg.ContainsKey("TUNNEL_NAME") -and $cfg["TUNNEL_NAME"]) { $cfg["TUNNEL_NAME"].Trim() } else { "baraki-game" }
$ServerExe = if ($cfg.ContainsKey("SERVER_EXE")) { $cfg["SERVER_EXE"].Trim() } else { "" }
$Port = if ($cfg.ContainsKey("PORT") -and $cfg["PORT"]) { [int]$cfg["PORT"] } else { 7777 }
$Players = if ($cfg.ContainsKey("PLAYERS") -and $cfg["PLAYERS"]) { [int]$cfg["PLAYERS"] } else { 2 }

if ($RegisterSecret -eq "replace-me") {
    throw "Set a real REGISTER_SECRET in infra/playtest.env (same as wrangler secret)."
}
if ($WssHost -match "^(https?|wss?)://") {
    throw "WSS_HOST must be hostname only (no scheme), e.g. game.example.com"
}

if (-not (Get-Command cloudflared -ErrorAction SilentlyContinue)) {
    throw "cloudflared not found on PATH. Install: https://developers.cloudflare.com/cloudflare-one/connections/connect-networks/downloads/"
}

$serverProc = $null
if (-not [string]::IsNullOrWhiteSpace($ServerExe)) {
    if (-not (Test-Path $ServerExe)) {
        throw "SERVER_EXE not found: $ServerExe`nBuild via Unity: BARAKI → Build → Windows Dedicated Server (Headless)"
    }
    Write-Host "Starting dedicated server: $ServerExe"
    $env:BARAKI_SERVER = "1"
    $serverProc = Start-Process -FilePath $ServerExe -PassThru -ArgumentList @(
        "-batchmode", "-nographics", "-barakiServer", "-port", "$Port", "-players", "$Players"
    )
    Start-Sleep -Seconds 3
}

Write-Host "Starting named tunnel '$TunnelName' → http://127.0.0.1:$Port (public wss://$WssHost)"
$psi = New-Object System.Diagnostics.ProcessStartInfo
$psi.FileName = "cloudflared"
$psi.Arguments = "tunnel run $TunnelName"
$psi.RedirectStandardError = $true
$psi.RedirectStandardOutput = $true
$psi.UseShellExecute = $false
$psi.CreateNoWindow = $true
$tunnelProc = [System.Diagnostics.Process]::Start($psi)

# Give tunnel a moment, then register stable WSS with matchmaker.
Start-Sleep -Seconds 2
$wssUrl = "wss://$WssHost"
Write-Host "Registering tunnel: $wssUrl"
$body = @{ wss_url = $wssUrl } | ConvertTo-Json
try {
    Invoke-RestMethod `
        -Method Post `
        -Uri "$MatchmakerUrl/api/v1/admin/register-tunnel" `
        -Headers @{ Authorization = "Bearer $RegisterSecret" } `
        -ContentType "application/json" `
        -Body $body | Out-Host
} catch {
    Write-Host "Register failed: $($_.Exception.Message)" -ForegroundColor Red
    try { $tunnelProc.Kill() } catch {}
    if ($serverProc -and -not $serverProc.HasExited) { try { $serverProc.Kill() } catch {} }
    throw
}

try {
    $health = Invoke-RestMethod -Uri "$MatchmakerUrl/api/v1/health" -TimeoutSec 15
    Write-Host ("Health: " + ($health | ConvertTo-Json -Compress))
} catch {
    Write-Host "Health check failed (non-fatal): $($_.Exception.Message)" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "READY for Discord Activity."
Write-Host "  wss: $wssUrl"
Write-Host "  Keep this window open."
Write-Host "  Ctrl+C to stop tunnel (+ server if started by this script)."
Write-Host ""

try {
    while (-not $tunnelProc.HasExited) {
        $errLine = $tunnelProc.StandardError.ReadLine()
        if ($null -ne $errLine) { Write-Host $errLine }
        else { Start-Sleep -Milliseconds 200 }
    }
} finally {
    if ($serverProc -and -not $serverProc.HasExited) {
        Write-Host "Stopping dedicated server PID $($serverProc.Id)"
        try { $serverProc.Kill() } catch {}
    }
}
