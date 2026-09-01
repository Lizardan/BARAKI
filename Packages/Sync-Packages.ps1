# Sync-Packages.ps1 — синхронизирует runtime-зависимости между manifest.json и manifest.ci.json
#
# CI подменяет manifest.json на manifest.ci.json (без MCP/Cursor-пакетов).
# Этот скрипт копирует все runtime-зависимости из manifest.json в manifest.ci.json,
# сохраняя исключения (MCP, Cursor IDE, Unity Pipeline, Project Auditor).
#
# Запуск: pwsh -File Packages/Sync-Packages.ps1

param(
    [string]$Manifest = "Packages/manifest.json",
    [string]$CiManifest = "Packages/manifest.ci.json"
)

$ErrorActionPreference = "Stop"

$excludePrefixes = @(
    "com.boxqkrtm.ide.cursor",
    "com.coplaydev.unity-mcp",
    "com.unity.pipeline",
    "com.unity.project-auditor-rules"
)

$manifestJson = Get-Content $Manifest -Raw | ConvertFrom-Json
$ciJson = Get-Content $CiManifest -Raw | ConvertFrom-Json

$ciDeps = @{}
foreach ($prop in $ciJson.dependencies.PSObject.Properties) {
    $skip = $false
    foreach ($prefix in $excludePrefixes) {
        if ($prop.Name.StartsWith($prefix)) { $skip = $true; break }
    }
    if ($skip) { continue }
    $ciDeps[$prop.Name] = $prop.Value
}

foreach ($prop in $manifestJson.dependencies.PSObject.Properties) {
    $name = $prop.Name
    $skip = $false
    foreach ($prefix in $excludePrefixes) {
        if ($name.StartsWith($prefix)) { $skip = $true; break }
    }
    if ($skip) { continue }
    $ciDeps[$name] = $prop.Value
}

$ciJson.dependencies = [PSCustomObject]@{}
foreach ($key in ($ciDeps.Keys | Sort-Object)) {
    $ciJson.dependencies | Add-Member -NotePropertyName $key -NotePropertyValue $ciDeps[$key]
}

$ciJson | ConvertTo-Json -Depth 10 | Set-Content $CiManifest -Encoding UTF8
Write-Host "Synced $CiManifest from $Manifest (excluded: $($excludePrefixes -join ', '))"
