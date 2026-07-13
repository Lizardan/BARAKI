# FREE-0 playtest evening helper (Windows host PC).
# Starts Cloudflare quick tunnel to local game server and registers wss_url with Workers.
#
# Prerequisites:
#   - cloudflared on PATH
#   - dedicated server already listening on -Port (default 7777)
#   - MATCHMAKER_URL + REGISTER_SECRET env vars
#
# Example:
#   $env:MATCHMAKER_URL = "https://baraki-matchmaker.YOUR.workers.dev"
#   $env:REGISTER_SECRET = "..."
#   .\infra\scripts\playtest-evening.ps1 -Port 7777

param(
    [int]$Port = 7777,
    [string]$MatchmakerUrl = $env:MATCHMAKER_URL,
    [string]$RegisterSecret = $env:REGISTER_SECRET,
    [string]$ServerExe = ""
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($MatchmakerUrl)) {
    throw "Set MATCHMAKER_URL (e.g. https://baraki-matchmaker.xxx.workers.dev)"
}
if ([string]::IsNullOrWhiteSpace($RegisterSecret)) {
    throw "Set REGISTER_SECRET (same as wrangler secret / GitHub MATCHMAKER_REGISTER_SECRET)"
}

$MatchmakerUrl = $MatchmakerUrl.TrimEnd("/")

if (-not [string]::IsNullOrWhiteSpace($ServerExe)) {
    if (-not (Test-Path $ServerExe)) {
        throw "ServerExe not found: $ServerExe"
    }
    Write-Host "Starting dedicated server: $ServerExe"
    $env:BARAKI_SERVER = "1"
    Start-Process -FilePath $ServerExe -ArgumentList @(
        "-batchmode", "-nographics", "-barakiServer", "-port", "$Port", "-players", "2"
    )
    Start-Sleep -Seconds 3
}

Write-Host "Starting cloudflared quick tunnel → http://127.0.0.1:$Port"
$psi = New-Object System.Diagnostics.ProcessStartInfo
$psi.FileName = "cloudflared"
$psi.Arguments = "tunnel --url http://127.0.0.1:$Port"
$psi.RedirectStandardError = $true
$psi.RedirectStandardOutput = $true
$psi.UseShellExecute = $false
$psi.CreateNoWindow = $true
$proc = [System.Diagnostics.Process]::Start($psi)

$tunnelHost = $null
$deadline = [datetime]::UtcNow.AddMinutes(2)
while ([datetime]::UtcNow -lt $deadline -and -not $tunnelHost) {
    $line = $proc.StandardError.ReadLine()
    if ($null -eq $line) {
        Start-Sleep -Milliseconds 200
        continue
    }
    Write-Host $line
    if ($line -match "https://([a-z0-9-]+\.trycloudflare\.com)") {
        $tunnelHost = $Matches[1]
    }
}

if (-not $tunnelHost) {
    try { $proc.Kill() } catch {}
    throw "Could not parse trycloudflare.com URL from cloudflared output"
}

$wssUrl = "wss://$tunnelHost"
Write-Host "Registering tunnel: $wssUrl"
$body = @{ wss_url = $wssUrl } | ConvertTo-Json
Invoke-RestMethod `
    -Method Post `
    -Uri "$MatchmakerUrl/api/v1/admin/register-tunnel" `
    -Headers @{ Authorization = "Bearer $RegisterSecret" } `
    -ContentType "application/json" `
    -Body $body | Out-Host

Write-Host ""
Write-Host "READY for Discord Activity friends."
Write-Host "  wss: $wssUrl"
Write-Host "  Keep this window open (cloudflared PID $($proc.Id))."
Write-Host "  Ctrl+C to stop tunnel."
Write-Host ""

# Stream remaining logs until process exits
while (-not $proc.HasExited) {
    $out = $proc.StandardError.ReadLine()
    if ($null -ne $out) { Write-Host $out }
    else { Start-Sleep -Milliseconds 200 }
}
