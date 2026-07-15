# FREE-0 one-click playtest evening (Windows host PC).
# Reads infra/playtest.env (gitignored).
#
# Tunnel mode:
#   - WSS_HOST set (required) -> named tunnel: cloudflared tunnel run $TUNNEL_NAME
#   - ALLOW_QUICK_TUNNEL=1 + empty WSS_HOST -> quick tunnel (dev only; Discord /wss every run)
#
# Setup once:
#   1. Copy infra/playtest.env.example -> infra/playtest.env; fill REGISTER_SECRET + WSS_HOST
#   2. .\infra\scripts\setup-named-tunnel.ps1 (Discord Portal /wss = same host)
#   3. Unity -> BARAKI -> Build -> Windows Dedicated Server (Headless)
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

$script:ServerProc = $null
$script:TunnelProc = $null
$script:CleaningUp = $false

function Write-Step([string]$msg) {
    Write-Host ""
    Write-Host "==> $msg" -ForegroundColor Cyan
}

function Write-Ok([string]$msg) {
    Write-Host "  [OK] $msg" -ForegroundColor Green
}

function Write-WarnLine([string]$msg) {
    Write-Host "  [WARN] $msg" -ForegroundColor Yellow
}

function Write-Fail([string]$msg) {
    Write-Host "  [FAIL] $msg" -ForegroundColor Red
}

function Read-PlaytestEnv([string]$path) {
    if (-not (Test-Path $path)) {
        throw "Missing $path - copy infra/playtest.env.example to infra/playtest.env and fill values."
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

function Get-OptionalKey($map, [string]$key, [string]$default = "") {
    if ($map.ContainsKey($key) -and -not [string]::IsNullOrWhiteSpace($map[$key])) {
        return $map[$key].Trim()
    }
    return $default
}

function Resolve-CloudflaredExe($cfg) {
    $fromEnv = Get-OptionalKey $cfg "CLOUDFLARED_EXE"
    if ($fromEnv -and (Test-Path $fromEnv)) {
        return (Resolve-Path $fromEnv).Path
    }
    $cmd = Get-Command cloudflared -ErrorAction SilentlyContinue
    if ($cmd) {
        return $cmd.Source
    }
    $candidates = @(
        "C:\Tools\cloudflared\cloudflared.exe",
        "$env:LOCALAPPDATA\cloudflared\cloudflared.exe",
        "$env:ProgramFiles\cloudflared\cloudflared.exe",
        "${env:ProgramFiles(x86)}\cloudflared\cloudflared.exe"
    )
    foreach ($c in $candidates) {
        if (Test-Path $c) { return $c }
    }
    throw "cloudflared not found. Install from Cloudflare downloads or set CLOUDFLARED_EXE in playtest.env"
}

function Test-PortListening([int]$port) {
    # Avoid Test-NetConnection: refused ports can hang ~20s and burn the whole wait budget.
    $client = $null
    try {
        $client = New-Object System.Net.Sockets.TcpClient
        $async = $client.BeginConnect("127.0.0.1", $port, $null, $null)
        if (-not $async.AsyncWaitHandle.WaitOne(400)) {
            return $false
        }
        $client.EndConnect($async)
        return $client.Connected
    } catch {
        return $false
    } finally {
        if ($client) {
            try { $client.Close() } catch {}
        }
    }
}

function Wait-PortListening([int]$port, [int]$timeoutSec = 60) {
    $deadline = (Get-Date).AddSeconds($timeoutSec)
    while ((Get-Date) -lt $deadline) {
        if (Test-PortListening $port) { return $true }
        Start-Sleep -Milliseconds 400
    }
    return $false
}

function Stop-PlaytestProcesses {
    if ($script:CleaningUp) { return }
    $script:CleaningUp = $true
    Write-Step "Cleanup"
    if ($script:TunnelProc -and -not $script:TunnelProc.HasExited) {
        Write-Host "  Stopping tunnel PID $($script:TunnelProc.Id)"
        try { $script:TunnelProc.Kill() } catch {}
        try { $script:TunnelProc.WaitForExit(3000) | Out-Null } catch {}
    }
    if ($script:ServerProc -and -not $script:ServerProc.HasExited) {
        Write-Host "  Stopping dedicated server PID $($script:ServerProc.Id)"
        try { $script:ServerProc.Kill() } catch {}
        try { $script:ServerProc.WaitForExit(3000) | Out-Null } catch {}
    }
    Get-ChildItem (Join-Path $env:TEMP "baraki-cloudflared-*") -ErrorAction SilentlyContinue |
        ForEach-Object { try { Remove-Item $_.FullName -Force -ErrorAction SilentlyContinue } catch {} }
}

function Test-VpnWarn {
    $names = @("AmneziaVPN", "amnezia", "openvpn", "wireguard", "nordvpn", "expressvpn")
    $procs = Get-Process -ErrorAction SilentlyContinue | Where-Object {
        $n = $_.ProcessName
        foreach ($hint in $names) {
            if ($n -like "*$hint*") { return $true }
        }
        return $false
    }
    if ($procs) {
        $list = ($procs | Select-Object -ExpandProperty ProcessName -Unique) -join ", "
        Write-WarnLine "VPN-like process running ($list). If tunnel fails on port 7844, disconnect VPN and retry."
    }
}

function Get-MergedTunnelLog([string]$outLog, [string]$errLog) {
    $chunks = @()
    if (Test-Path $outLog) {
        $chunks += Get-Content $outLog -Raw -ErrorAction SilentlyContinue
    }
    if (Test-Path $errLog) {
        $chunks += Get-Content $errLog -Raw -ErrorAction SilentlyContinue
    }
    return ($chunks -join "`n")
}

function Find-TryCloudflareHost([string]$text) {
    if ([string]::IsNullOrWhiteSpace($text)) { return $null }
    # Avoid complex character classes for PS 5.1 encoding safety.
    if ($text -match 'https://([a-zA-Z0-9\-]+)\.trycloudflare\.com') {
        return $Matches[1] + ".trycloudflare.com"
    }
    return $null
}

function Start-DedicatedServer([string]$exe, [int]$port, [int]$players) {
    Write-Step "Starting dedicated server"
    if (Test-PortListening $port) {
        throw "Port $port already in use. Stop the other BARAKI.exe (or whatever listens there), then retry."
    }
    if (-not (Test-Path $exe)) {
        throw "SERVER_EXE not found: $exe. Build via Unity: BARAKI -> Build -> Windows Dedicated Server (Headless)"
    }
    $exeDir = Split-Path -Parent $exe
    $serverLog = Join-Path $env:TEMP "baraki-dedicated-server.log"
    Write-Host "  $exe"
    Write-Host "  cwd: $exeDir"
    Write-Host "  args: -listenPort $port -players $players"
    Write-Host "  log: $serverLog"
    $env:BARAKI_SERVER = "1"
    $script:ServerProc = Start-Process -FilePath $exe -PassThru -WorkingDirectory $exeDir -ArgumentList @(
        "-batchmode", "-nographics", "-barakiServer",
        "-listenPort", "$port", "-players", "$players",
        "-logFile", $serverLog
    )
    Write-Host "  Waiting for TCP 127.0.0.1:$port ..."
    $waitSec = 60
    if (-not (Wait-PortListening $port $waitSec)) {
        $exited = $script:ServerProc.HasExited
        $exitInfo = if ($exited) { "exited code=$($script:ServerProc.ExitCode)" } else { "still running PID=$($script:ServerProc.Id)" }
        $tail = ""
        if (Test-Path $serverLog) {
            $tail = (Get-Content $serverLog -Tail 40 -ErrorAction SilentlyContinue) -join "`n"
        }
        throw "Server did not listen on port $port within ${waitSec}s ($exitInfo). Log: $serverLog`n$tail"
    }
    Write-Ok "server listening on 127.0.0.1:$port (PID $($script:ServerProc.Id))"
}

function Start-NamedTunnel([string]$cloudflaredExe, [string]$tunnelName, [string]$wssHost, [int]$port) {
    Write-Step "Starting named tunnel '$tunnelName'"
    Write-Host "  -> http://127.0.0.1:$port  public wss://$wssHost"
    $stamp = [guid]::NewGuid().ToString("N")
    $outLog = Join-Path $env:TEMP "baraki-cloudflared-$stamp.out.log"
    $errLog = Join-Path $env:TEMP "baraki-cloudflared-$stamp.err.log"

    $env:TUNNEL_TRANSPORT_PROTOCOL = "http2"
    $script:TunnelProc = Start-Process -FilePath $cloudflaredExe -PassThru `
        -ArgumentList @("tunnel", "run", $tunnelName) `
        -WindowStyle Hidden `
        -RedirectStandardOutput $outLog `
        -RedirectStandardError $errLog

    Start-Sleep -Seconds 3
    if ($script:TunnelProc.HasExited) {
        $tail = Get-MergedTunnelLog $outLog $errLog
        throw "Named tunnel exited early.`n$tail"
    }
    Write-Ok "named tunnel process running (PID $($script:TunnelProc.Id))"
    return $wssHost
}

function Start-QuickTunnel([string]$cloudflaredExe, [int]$port) {
    Write-Step "Starting quick tunnel (WSS_HOST empty)"
    Write-Host "  cloudflared tunnel --url http://127.0.0.1:$port (http2)"
    $stamp = [guid]::NewGuid().ToString("N")
    $outLog = Join-Path $env:TEMP "baraki-cloudflared-$stamp.out.log"
    $errLog = Join-Path $env:TEMP "baraki-cloudflared-$stamp.err.log"

    $env:TUNNEL_TRANSPORT_PROTOCOL = "http2"
    $script:TunnelProc = Start-Process -FilePath $cloudflaredExe -PassThru `
        -ArgumentList @("tunnel", "--url", "http://127.0.0.1:$port") `
        -WindowStyle Hidden `
        -RedirectStandardOutput $outLog `
        -RedirectStandardError $errLog

    Write-Host "  Waiting for trycloudflare hostname (up to 45s)..."
    $deadline = (Get-Date).AddSeconds(45)
    $hostName = $null
    $registered = $false
    while ((Get-Date) -lt $deadline) {
        if ($script:TunnelProc.HasExited) {
            Start-Sleep -Milliseconds 400
            $tail = Get-MergedTunnelLog $outLog $errLog
            throw "Quick tunnel exited early.`n$tail"
        }

        $text = Get-MergedTunnelLog $outLog $errLog
        if ($text) {
            $found = Find-TryCloudflareHost $text
            if ($found) { $hostName = $found }
            if ($text -match 'Registered tunnel connection') {
                $registered = $true
            }
            if ($text -match 'hard_fail=true') {
                throw "cloudflared connectivity pre-check failed (often VPN/firewall blocking 7844). Disconnect Amnezia/VPN and retry."
            }
        }

        if ($hostName -and $registered) { break }
        Start-Sleep -Milliseconds 400
    }

    if (-not $hostName) {
        $tail = Get-MergedTunnelLog $outLog $errLog
        throw "Timed out waiting for trycloudflare hostname.`n$tail"
    }

    if ($registered) {
        Write-Ok "quick tunnel registered: $hostName"
    } else {
        Write-WarnLine "hostname $hostName seen; Registered not yet - continuing"
        Write-Ok "quick tunnel host: $hostName"
    }
    return $hostName
}

function Register-Tunnel([string]$matchmakerUrl, [string]$secret, [string]$wssUrl) {
    Write-Step "Registering tunnel with matchmaker"
    Write-Host "  POST $matchmakerUrl/api/v1/admin/register-tunnel"
    Write-Host "  $wssUrl"
    $body = @{ wss_url = $wssUrl } | ConvertTo-Json
    $result = Invoke-RestMethod `
        -Method Post `
        -Uri "$matchmakerUrl/api/v1/admin/register-tunnel" `
        -Headers @{ Authorization = "Bearer $secret" } `
        -ContentType "application/json" `
        -Body $body `
        -TimeoutSec 20
    Write-Ok ("register: " + ($result | ConvertTo-Json -Compress))
}

function Get-MatchmakerHealth([string]$matchmakerUrl) {
    return Invoke-RestMethod -Uri "$matchmakerUrl/api/v1/health" -TimeoutSec 15
}

function Show-ReadyBlock([string]$wssHost, [string]$wssUrl, [bool]$isQuick, [string]$matchmakerUrl) {
    $proxyHost = ""
    try {
        $proxyHost = ([Uri]$matchmakerUrl).Host
    } catch {
        $proxyHost = ""
    }

    Write-Host ""
    Write-Host "============================================================" -ForegroundColor Green
    Write-Host " READY for Discord Activity" -ForegroundColor Green
    Write-Host "============================================================" -ForegroundColor Green
    Write-Host "  tunnel  : $wssUrl"
    if ($proxyHost) {
        Write-Host "  public  : wss://$proxyHost  (clients / Discord /wss)"
    }
    Write-Host ""
    if ($isQuick) {
        Write-Host "  QUICK TUNNEL backend OK - Discord Portal /wss stays on Worker." -ForegroundColor Green
        if ($proxyHost) {
            Write-Host "  Portal /wss target (set once): $proxyHost" -ForegroundColor Green
        }
        Write-Host "  Do NOT paste trycloudflare into Discord - Worker proxies for you." -ForegroundColor Cyan
        Write-Host "  Launch BARAKI Activity in a voice channel." -ForegroundColor Green
    } else {
        Write-Host "  Named tunnel: backend host $wssHost" -ForegroundColor Green
        if ($proxyHost) {
            Write-Host "  Discord /wss should still be Worker: $proxyHost" -ForegroundColor Green
        }
        Write-Host "  Launch BARAKI Activity in a voice channel." -ForegroundColor Green
    }
    Write-Host ""
    Write-Host "  Keep this window open. Ctrl+C stops server + tunnel." -ForegroundColor Cyan
    Write-Host "============================================================" -ForegroundColor Green
    Write-Host ""
}

function Invoke-StatusLoop(
    [System.Diagnostics.Process]$serverProc,
    [System.Diagnostics.Process]$tunnelProc,
    [int]$port,
    [string]$matchmakerUrl
) {
    Write-Step "Status monitor (Ctrl+C to stop)"
    while ($true) {
        $serverOk = $serverProc -and -not $serverProc.HasExited -and (Test-PortListening $port)
        $tunnelOk = $tunnelProc -and -not $tunnelProc.HasExited
        $healthOk = $false
        $healthDetail = "n/a"
        try {
            $h = Get-MatchmakerHealth $matchmakerUrl
            $healthOk = [bool]$h.has_tunnel
            $healthDetail = "has_tunnel=$($h.has_tunnel)"
        } catch {
            $healthDetail = "error"
        }

        $s1 = if ($serverOk) { "[OK] server :$port" } else { "[FAIL] server" }
        $s2 = if ($tunnelOk) { "[OK] tunnel" } else { "[FAIL] tunnel" }
        $s3 = if ($healthOk) { "[OK] matchmaker $healthDetail" } else { "[WARN] matchmaker $healthDetail" }
        $c1 = if ($serverOk) { "Green" } else { "Red" }
        $c2 = if ($tunnelOk) { "Green" } else { "Red" }
        $c3 = if ($healthOk) { "Green" } else { "Yellow" }
        $ts = Get-Date -Format "HH:mm:ss"
        Write-Host -NoNewline "[$ts] "
        Write-Host -NoNewline "$s1" -ForegroundColor $c1
        Write-Host -NoNewline " | "
        Write-Host -NoNewline "$s2" -ForegroundColor $c2
        Write-Host -NoNewline " | "
        Write-Host "$s3" -ForegroundColor $c3

        if (-not $serverOk -or -not $tunnelOk) {
            Write-Fail "server or tunnel died - shutting down"
            break
        }
        Start-Sleep -Seconds 5
    }
}

# --- main ---
try {
    Write-Host ""
    Write-Host " BARAKI evening playtest" -ForegroundColor Cyan
    Write-Host " Env: $EnvFile"
    Write-Host ""

    Write-Step "Prefight"
    $cfg = Read-PlaytestEnv $EnvFile
    $MatchmakerUrl = (Require-Key $cfg "MATCHMAKER_URL").TrimEnd("/")
    $RegisterSecret = Require-Key $cfg "REGISTER_SECRET"
    $WssHostCfg = Get-OptionalKey $cfg "WSS_HOST"
    $TunnelName = Get-OptionalKey $cfg "TUNNEL_NAME" "baraki-game"
    $AllowQuick = Get-OptionalKey $cfg "ALLOW_QUICK_TUNNEL" "0"
    $ServerExe = Get-OptionalKey $cfg "SERVER_EXE"
    $Port = if ($cfg.ContainsKey("PORT") -and $cfg["PORT"]) { [int]$cfg["PORT"] } else { 7777 }
    $Players = if ($cfg.ContainsKey("PLAYERS") -and $cfg["PLAYERS"]) { [int]$cfg["PLAYERS"] } else { 2 }

    if ($RegisterSecret -eq "replace-me") {
        throw "Set a real REGISTER_SECRET in infra/playtest.env (same as wrangler secret)."
    }
    if ($WssHostCfg -match "^(https?|wss?)://") {
        throw "WSS_HOST must be hostname only (no scheme), e.g. game.example.com"
    }
    if ([string]::IsNullOrWhiteSpace($ServerExe)) {
        throw "playtest.env missing SERVER_EXE"
    }

    # Quick-tunnel hostnames are ephemeral - never treat *.trycloudflare.com as named tunnel.
    if ($WssHostCfg -match '(?i)\.trycloudflare\.com$') {
        Write-WarnLine "WSS_HOST is a trycloudflare hostname; ignoring it and using quick tunnel"
        $WssHostCfg = ""
    }

    $isQuick = [string]::IsNullOrWhiteSpace($WssHostCfg) -or ($AllowQuick -eq "1")
    if ([string]::IsNullOrWhiteSpace($WssHostCfg) -and $AllowQuick -ne "1") {
        throw @"
WSS_HOST is required for Discord playtest (named tunnel).
Run: .\infra\scripts\setup-named-tunnel.ps1
Set WSS_HOST=<hostname> in infra/playtest.env (same host as Discord Portal /wss).
Dev-only escape hatch: ALLOW_QUICK_TUNNEL=1 (Worker proxies Discord /wss; no Portal edits).
"@
    }
    if ($AllowQuick -eq "1") {
        # Always rediscover hostname; stale WSS_HOST must not pin a dead quick tunnel.
        $isQuick = $true
        $WssHostCfg = ""
    }

    $cloudflaredExe = Resolve-CloudflaredExe $cfg
    Write-Ok "cloudflared: $cloudflaredExe"
    Write-Ok "matchmaker: $MatchmakerUrl"
    Write-Ok "server exe: $ServerExe"
    Write-Ok "port=$Port players=$Players"
    if ($AllowQuick -eq "1") {
        Write-Ok "tunnel mode: QUICK (Worker proxies Discord /wss; no Portal edits)"
    } else {
        Write-Ok ("tunnel mode: named ({0} -> {1})" -f $TunnelName, $WssHostCfg)
    }
    Test-VpnWarn

    Start-DedicatedServer $ServerExe $Port $Players

    if ($isQuick) {
        $wssHost = Start-QuickTunnel $cloudflaredExe $Port
    } else {
        $wssHost = Start-NamedTunnel $cloudflaredExe $TunnelName $WssHostCfg $Port
    }
    $wssUrl = "wss://$wssHost"

    Register-Tunnel $MatchmakerUrl $RegisterSecret $wssUrl

    Write-Step "Health check"
    $health = Get-MatchmakerHealth $MatchmakerUrl
    Write-Host ("  " + ($health | ConvertTo-Json -Compress))
    if (-not $health.has_tunnel) {
        throw "Matchmaker health has_tunnel=false after register."
    }
    Write-Ok "has_tunnel=True"

    Show-ReadyBlock $wssHost $wssUrl $isQuick $MatchmakerUrl

    try {
        Invoke-StatusLoop $script:ServerProc $script:TunnelProc $Port $MatchmakerUrl
    } catch {
        if ($_.Exception.Message -notmatch "canceled|cancelled|interrupted") {
            Write-Fail $_.Exception.Message
        }
    }
}
catch {
    Write-Fail $_.Exception.Message
    exit 1
}
finally {
    Stop-PlaytestProcesses
}
