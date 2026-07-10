# Launches Coplay MCP For Unity via uvx without a visible console window.
# Fallback when uvx.exe path is not written directly to mcp.json.
$ErrorActionPreference = 'Stop'

function Get-UvxExePath {
    $candidates = @(
        (Join-Path $env:USERPROFILE '.local\bin\uvx.exe'),
        (Join-Path $env:LOCALAPPDATA 'Programs\uv\uvx.exe')
    )
    foreach ($path in $candidates) {
        if (Test-Path -LiteralPath $path) {
            return $path
        }
    }

    throw 'uvx.exe not found. Install uv: https://docs.astral.sh/uv/'
}

function Format-ProcessArgument {
    param([string]$Value)

    if ($Value -match '[\s"]') {
        return '"' + ($Value -replace '"', '\"') + '"'
    }

    return $Value
}

$uvx = Get-UvxExePath
$argList = @('--from', '__MCP_SERVER_FROM__', 'mcp-for-unity', '--transport', 'stdio') + $args

$psi = New-Object System.Diagnostics.ProcessStartInfo
$psi.FileName = $uvx
$psi.Arguments = ($argList | ForEach-Object { Format-ProcessArgument $_ }) -join ' '
$psi.UseShellExecute = $false
$psi.CreateNoWindow = $true

$process = [System.Diagnostics.Process]::Start($psi)
if ($null -eq $process) {
    throw 'Failed to start MCP server.'
}

$process.WaitForExit()
exit $process.ExitCode
