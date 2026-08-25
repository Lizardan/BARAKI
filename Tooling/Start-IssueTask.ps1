#Requires -Version 7
<#
.SYNOPSIS
    Запуск задачи из GitHub Issues в новой сессии opencode.

.DESCRIPTION
    Тянет issue через gh CLI, собирает промпт (Суть + Acceptance + Agent context)
    и открывает новое окно Windows Terminal с интерактивной сессией opencode.
    Закрытие issue остаётся за агентом по wiki/rules/github-issues-workflow.md.

.EXAMPLE
    ./Tooling/Start-IssueTask.ps1 -Issue 14
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory, Position = 0)]
    [int]$Issue,

    [string]$Repo = "Lizardan/BARAKI",

    # Внутренний: запустить opencode в текущей консоли (вызывается лаунчером)
    [switch]$Execute,

    [switch]$NoInteractive,

    # Только показать собранный промпт, ничего не запускать
    [switch]$PrintPrompt
)

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path -Parent $PSScriptRoot

function Get-IssueData {
    param([int]$Number, [string]$Repository)

    $json = gh issue view $Number -R $Repository --json number,title,body,state,url,assignees 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "Не удалось получить issue #$Number : $json"
    }
    return ($json | ConvertFrom-Json)
}

function New-TaskPrompt {
    param($Data)

    $sb = [System.Text.StringBuilder]::new()
    [void]$sb.AppendLine("Работай над задачей из GitHub Issues репозитория $Repo.")
    [void]$sb.AppendLine()
    [void]$sb.AppendLine("# [$($Data.Number)] $($Data.Title)")
    [void]$sb.AppendLine()
    if (-not [string]::IsNullOrWhiteSpace($Data.body)) {
        [void]$sb.AppendLine($Data.body.TrimEnd())
        [void]$sb.AppendLine()
    }
    [void]$sb.AppendLine("---")
    [void]$sb.AppendLine("Правила выполнения (github-issues-workflow):")
    [void]$sb.AppendLine("- Секция 'Agent context (EN)' — технический контекст для тебя; остальное — постановка задачи.")
    [void]$sb.AppendLine("- Выполняй чекбоксы Acceptance criteria; следуй AGENTS.md и wiki/rules/.")
    [void]$sb.AppendLine("- После правок кода проверь компиляцию (read_console) и прогони затронутые тесты.")
    [void]$sb.AppendLine("- Коммиты не делать без явной просьбы пользователя.")
    [void]$sb.AppendLine("- Когда все acceptance выполнены и тесты green: закрой issue командой")
    [void]$sb.AppendLine("  gh issue close $($Data.Number) -R $Repo -c '<краткая сводка по-русски>'.")
    return $sb.ToString()
}

if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
    throw "gh CLI не найден в PATH."
}

$data = Get-IssueData -Number $Issue -Repository $Repo

if ($data.state -ne "OPEN") {
    throw "Issue #$Issue уже закрыт ($($data.state))."
}

if (-not $Execute) {
    if ($PrintPrompt) {
        New-TaskPrompt -Data $data
        exit 0
    }
    # Лаунчер: открыть НОВОЕ окно терминала и там выполнить этот же скрипт с -Execute
    $scriptPath = $PSCommandPath
    $innerArgs = @(
        "-NoProfile", "-ExecutionPolicy", "Bypass",
        "-File", $scriptPath,
        "-Issue", $Issue,
        "-Repo", $Repo,
        "-Execute"
    )
    if ($NoInteractive) { $innerArgs += "-NoInteractive" }

    # Все элементы с пробелами квотим вручную — wt парсит одну строку команды
    $quoted = $innerArgs | ForEach-Object {
        if ($_ -like "* *") { "`"$_`"" } else { $_ }
    }
    $commandLine = "pwsh " + ($quoted -join " ")

    if (Get-Command wt.exe -ErrorAction SilentlyContinue) {
        Start-Process wt.exe -ArgumentList @(
            "new-tab",
            "--title", "`"ISSUE #$($data.number)`"",
            "-d", "`"$ProjectRoot`"",
            $commandLine
        ) -Wait:$false
    }
    else {
        Start-Process pwsh -ArgumentList $innerArgs -WorkingDirectory $ProjectRoot
    }
    exit 0
}

# --- Режим Execute: работаем в текущей консоли ---
$opencodeArgs = @("run", (New-TaskPrompt -Data $data), "--title", "[#$($data.number)] $($data.title)")
if (-not $NoInteractive) { $opencodeArgs += "--interactive" }

Write-Host "Запуск opencode по issue #$($data.number): $($data.title)" -ForegroundColor Cyan
& opencode @opencodeArgs
exit $LASTEXITCODE
