# UnioTasks — Автоматическая установка

## Инструкция для AI-агента

Прочитай этот файл и выполни **ВСЕ** шаги последовательно. Каждый шаг — PowerShell скрипт. Если шаг не выполнился автоматически — следуй инструкции по исправлению.

**Не пропускай шаги.** Не спрашивай подтверждения — просто выполняй.

---

## Шаг 0: Подготовка

```powershell
# Определяем корневую папку проекта (где лежит INSTALL.md)
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Write-Host "Рабочая папка: $scriptDir"

# Проверяем что .vsix существует
$vsixFile = Get-ChildItem $scriptDir -Filter "*.vsix" | Select-Object -First 1
if (-not $vsixFile) {
    Write-Host "ОШИБКА: Файл .vsix не найден рядом с INSTALL.md"
    Write-Host "Убедись что папка содержит:"
    Write-Host "  - uniotasks-1.0.4.vsix"
    Write-Host "  - INSTALL.md"
    exit 1
}
Write-Host "OK: Найден $($vsixFile.Name)"
```

---

## Шаг 1: Очистка старых версий

```powershell
# Удалить все старые версии (BARAKI + старые UniGames)
$extensionsDir = "$env:USERPROFILE\.cursor\extensions"
$oldNames = @("baraki", "uniotasks")
$removed = 0

Get-ChildItem $extensionsDir -Directory -ErrorAction SilentlyContinue | ForEach-Object {
    $shouldRemove = $false
    foreach ($pattern in $oldNames) {
        if ($_.Name -match $pattern) { $shouldRemove = $true; break }
    }
    if ($shouldRemove) {
        Write-Host "Удаляю: $($_.Name)"
        Remove-Item -Path $_.FullName -Recurse -Force -ErrorAction SilentlyContinue
        $removed++
    }
}

if ($removed -gt 0) {
    Write-Host "OK: Удалено $removed старых версий"
} else {
    Write-Host "OK: Старых версий нет"
}

# Проверить что чисто
$remaining = Get-ChildItem $extensionsDir -Directory -ErrorAction SilentlyContinue | Where-Object { $_.Name -match "baraki|uniogames" }
if ($remaining) {
    Write-Host "ОШИБКА: Не удалось удалить:"
    $remaining | ForEach-Object { Write-Host "  - $($_.Name)" }
    Write-Host "Удали вручную и перезапусти Cursor"
    exit 1
}
```

---

## Шаг 2: Установка расширения

```powershell
# Попытка 1: через CLI
Write-Host "Устанавливаю через cursor --install-extension..."
& cursor --install-extension $vsixFile.FullName --force 2>&1

$installed = & cursor --list-extensions 2>&1 | Select-String "uniogames"
if ($installed) {
    Write-Host "OK: Расширение установлено через CLI"
} else {
    Write-Host "CLI не сработал, копирую вручную..."
    
    # Попытка 2: распаковка .vsix (это ZIP) и копирование
    $destDir = "$extensionsDir\UnioGames.uniotasks-1.0.4"
    
    # Очистить если есть
    if (Test-Path $destDir) { Remove-Item -Path $destDir -Recurse -Force }
    
    # Распаковать .vsix
    $extractDir = "$env:TEMP\vsix-extract-$(Get-Random)"
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [System.IO.Compression.ZipFile]::ExtractToDirectory($vsixFile.FullName, $extractDir)
    
    # Найти папку extension внутри
    $extFolder = Get-ChildItem $extractDir -Directory -Recurse | Where-Object { $_.Name -eq "extension" } | Select-Object -First 1
    if ($extFolder) {
        New-Item -Path $destDir -ItemType Directory -Force | Out-Null
        Copy-Item -Path "$($extFolder.FullName)\*" -Destination $destDir -Recurse -Force
        Write-Host "OK: Расширение скопировано в $destDir"
    } else {
        Write-Host "ОШИБКА: Папка extension не найдена в .vsix"
        Remove-Item -Path $extractDir -Recurse -Force -ErrorAction SilentlyContinue
        exit 1
    }
    
    # Очистить
    Remove-Item -Path $extractDir -Recurse -Force -ErrorAction SilentlyContinue
    
    # Проверить
    $installed = & cursor --list-extensions 2>&1 | Select-String "uniogames"
    if ($installed) {
        Write-Host "OK: Расширение установлено через копирование"
    } else {
        Write-Host "ОШИБКА: Расширение не появилось в списке"
        Write-Host "Перезапусти Cursor и проверь: Ctrl+Shift+X → поищи UniGames"
        exit 1
    }
}
```

---

## Шаг 3: Установка GitHub CLI (gh)

```powershell
$ghVersion = gh --version 2>&1
if ($ghVersion -match "gh version") {
    Write-Host "OK: gh уже установлен: $ghVersion"
} else {
    Write-Host "Устанавливаю gh..."
    
    # Попытка через winget
    winget install --id GitHub.cli -e --source winget --accept-package-agreements --accept-source-agreements 2>&1
    
    # Обновить PATH
    $env:PATH = [System.Environment]::GetEnvironmentVariable("PATH", "Machine") + ";" + [System.Environment]::GetEnvironmentVariable("PATH", "User")
    
    $ghVersion = gh --version 2>&1
    if ($ghVersion -match "gh version") {
        Write-Host "OK: gh установлен: $ghVersion"
    } else {
        # Попытка через scoop
        Write-Host "winget не сработал, пробую scoop..."
        scoop install gh 2>&1
        
        $ghVersion = gh --version 2>&1
        if ($ghVersion -match "gh version") {
            Write-Host "OK: gh установлен через scoop: $ghVersion"
        } else {
            Write-Host "НЕ УДАЛОСЬ установить gh автоматически."
            Write-Host "УСТАНОВИ ВРУЧНУЮ:"
            Write-Host "  1. Скачай: https://cli.github.com/"
            Write-Host "  2. Запусти installer"
            Write-Host "  3. Перезапусти терминал/Cursor"
            Write-Host "  4. Повтори этот скрипт"
            exit 1
        }
    }
}
```

---

## Шаг 4: Установка opencode

```powershell
$ocVersion = opencode --version 2>&1
if ($LASTEXITCODE -eq 0) {
    Write-Host "OK: opencode уже установлен"
} else {
    Write-Host "Устанавливаю opencode..."
    
    npm install -g opencode 2>&1
    
    $ocVersion = opencode --version 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Host "OK: opencode установлен"
    } else {
        Write-Host "НЕ УДАЛОСЬ установить opencode автоматически."
        Write-Host "УСТАНОВИ ВРУЧНУЮ:"
        Write-Host "  1. Установи Node.js: https://nodejs.org/"
        Write-Host "  2. Перезапусти терминал"
        Write-Host "  3. Выполни: npm install -g opencode"
        exit 1
    }
}
```

---

## Шаг 5: Авторизация GitHub

```powershell
$ghAuth = gh auth status 2>&1
if ($ghAuth -match "Logged in") {
    Write-Host "OK: GitHub уже авторизован"
} else {
    Write-Host "GitHub не авторизован. Создаю токен..."
    
    # Открыть страницу создания токена
    Write-Host ""
    Write-Host "=== СОЗДАНИЕ ТОКЕНА ==="
    Write-Host "Откроется страница GitHub. Создай токен:"
    Write-Host ""
    Write-Host "1. Token name: cursor-task-launcher"
    Write-Host "2. Expiration: 90 дней (или дольше)"
    Write-Host "3. Repository access: Only select repositories → выбери свой репозиторий"
    Write-Host "4. Permissions → Repository permissions:"
    Write-Host "   - Issues: Read and write"
    Write-Host "   - Metadata: Read-only"
    Write-Host "5. Generate token"
    Write-Host "6. Скопируй токен"
    Write-Host ""
    
    # Открыть страницу токена в браузере
    Start-Process "https://github.com/settings/personal-access-tokens/new"
    
    $token = Read-Host "Вставь скопированный токен"
    
    if ($token) {
        $token | gh auth login --with-token 2>&1
        $ghAuth = gh auth status 2>&1
        if ($ghAuth -match "Logged in") {
            Write-Host "OK: Авторизован через токен"
        } else {
            Write-Host "ОШИБКА: Токен не сработал."
            Write-Host "Проверь:"
            Write-Host "  - Токен скопирован без пробелов"
            Write-Host "  - Права: Issues → Read and write"
            Write-Host ""
            Write-Host "Попробуй вручную:"
            Write-Host "  gh auth login"
            exit 1
        }
    } else {
        Write-Host "Токен не введён. Авторизуй вручную:"
        Write-Host "  1. gh auth login"
        Write-Host "  2. GitHub.com → HTTPS → Paste token"
        exit 1
    }
}

# Проверить что можем читать issues
Write-Host "Проверяю доступ к issues..."
$testIssues = gh issue list --limit 1 2>&1
if ($LASTEXITCODE -eq 0) {
    Write-Host "OK: Issues доступны"
} else {
    Write-Host "ПРЕДУПРЕЖДЕНИЕ: Не могу прочитать issues."
    Write-Host "Проверь:"
    Write-Host "  - Токен имеет право Issues: Read and write"
    Write-Host "  - Репозиторий существует"
}
```

---

## Шаг 6: Настройка репозитория

```powershell
# Попробовать определить из git remote
$repo = $null
$gitRemote = git remote get-url origin 2>&1
if ($gitRemote -match "github\.com") {
    $match = [regex]::Match($gitRemote, "github\.com[:/](.+?)(?:\.git)?$")
    if ($match.Success) {
        $repo = $match.Groups[1].Value
        Write-Host "OK: Репозиторий из git remote: $repo"
    }
}

if (-not $repo) {
    # Спросить у пользователя
    Write-Host "Не удалось определить репозиторий из git remote."
    $repo = Read-Host "Введи репозиторий (Owner/Repo)"
}

if ($repo) {
    # Сохранить в настройки Cursor
    $settingsPath = "$env:APPDATA\Cursor\User\settings.json"
    if (Test-Path $settingsPath) {
        $settingsRaw = Get-Content $settingsPath -Raw
        # Проверить есть ли уже настройка
        if ($settingsRaw -match "uniotasks") {
            Write-Host "OK: Настройка repo уже существует в settings.json"
        } else {
            # Добавить в конец объекта
            $settingsRaw = $settingsRaw.TrimEnd()
            if ($settingsRaw.EndsWith("}")) {
                $settingsRaw = $settingsRaw.Substring(0, $settingsRaw.Length - 1) + ','
                $settingsRaw += "`n  `"uniotasks.repo`": `"$repo`"`n}"
                Set-Content -Path $settingsPath -Value $settingsRaw -Encoding UTF8
                Write-Host "OK: Настройка repo сохранена: $repo"
            }
        }
    } else {
        Write-Host "WARN: settings.json не найден. Настрой вручную:"
        Write-Host '  "uniotasks.repo": "' + $repo + '"'
    }
}

# Сохранить repo в переменную для следующих шагов
if (-not $repo) { $repo = "" }
```

---

## Шаг 7: Настройка меток (labels)

Метки нужны для сортировки задач в расширении. Создаются автоматически.

```powershell
if (-not $repo) {
    Write-Host "WARN: repo не определён, пропускаю создание меток"
} else {
    Write-Host "Создаю метки в $repo..."
    
    # Проверить какие метки уже есть
    $existingLabels = gh label list --repo $repo 2>&1
    
    # Метки для создания: Имя → Цвет
    $labels = @{
        "todo-task"         = "ededed"   # серый — основная метка задачи
        "priority/critical" = "d93f0b"   # красный — горит
        "priority/high"     = "0e8a16"   # зелёный — высокий
        "priority/low"      = "fbca04"   # жёлтый — низкий
        "blocked"           = "e11d48"   # розовый — заблокировано
        "deferred"          = "c5def5"   # фиолетовый — морозилка
        "roadmap"           = "0075ca"   # синий — дорожная карта
        "bug"               = "d73a4a"   # красный — баг
    }
    
    foreach ($name in $labels.Keys) {
        $color = $labels[$name]
        # Проверить существует ли метка
        if ($existingLabels -match $name) {
            Write-Host "  skip: $name (уже есть)"
        } else {
            gh label create $name --color $color --repo $repo 2>&1 | Out-Null
            Write-Host "  + $name"
        }
    }
    
    Write-Host "OK: Метки настроены"
    
    # Показать финальный список
    Write-Host ""
    Write-Host "Доступные метки:"
    gh label list --repo $repo 2>&1
}
```

---

## Шаг 8: Создание шаблона issue

```powershell
if (-not $repo) {
    Write-Host "WARN: repo не определён, пропускаю шаблон"
} else {
    $templateDir = ".github/ISSUE_TEMPLATE"
    
    # Проверить есть ли уже шаблоны
    if (Test-Path "$templateDir/task.md") {
        Write-Host "OK: Шаблон task.md уже существует"
    } else {
        # Создать папку
        New-Item -Path $templateDir -ItemType Directory -Force | Out-Null
        
        # Создать шаблон
        $template = @"
---
name: Задача
about: Новая задача для выполнения
title: '[ID] '
labels: todo-task
assignees: ''
---

## Суть
Что делаем и зачем

## Правила/Ограничения
(если есть)

## Acceptance criteria
- [ ] Критерий 1
- [ ] Критерий 2

## Контекст
Ссылки на файлы/классы/документацию
"@
        
        Set-Content -Path "$templateDir/task.md" -Value $template -Encoding UTF8
        Write-Host "OK: Создан $templateDir/task.md"
    }
}
```

---

## Шаг 9: Знания для AI-агента

**Прочитай эту секцию.** Это правила создания задач которые нужно знать AI-агенту (opencode / Cursor Agent) для корректной работы с расширением.

### Правила создания issues

1. **Каждая задача должна иметь метку `todo-task`** — без неё расширение её не покажет

2. **Заголовок = `[PREFIX] Описание на русском`**
   - Префиксы: `PRE-`, `GATE-`, `EA-`, `BUG-`, `CHAT-`, `DIST-`
   - Пример: `[PRE-010] Добавить системуAchievement`

3. **Обязательные секции в теле issue:**
   - `## Суть` — что делаем и зачем (по-русски)
   - `## Acceptance criteria` — чекбоксы `- [ ]` (по-русски)
   - `## Agent context` — технический контекст для AI (по-английски)

4. **Приоритет через метки:**
   - `priority/critical` — горит, срочно
   - `priority/high` — важная задача
   - `priority/low` — когда будет время
   - Без метки приоритета = обычный приоритет

5. **Спецметки:**
   - `blocked` — ждёт другую задачу
   - `deferred` — морозилка (отложено)
   - `roadmap` — обзорная задача

### Пример создания задачи через gh

```bash
gh issue create \
  --title "[PRE-010] Добавить систему достижений" \
  --label "todo-task,priority/high" \
  --body "## Суть
Добавить систему достижений для мотивации игроков.

## Acceptance criteria
- [ ] Меню достижений в HUD
- [ ] Отслеживание прогресса
- [ ] Уведомление при получении

## Agent context
- UI: UI Toolkit, см. Assets/Game/UI/Runtime/
- Данные: ScriptableObject в Catalogs/
- Структура: Game.Gameplay namespace"
```

### Как AI-агент должен создавать задачи

При создании issue AI-агент должен:

1. Определить префикс из названия задачи
2. Добавить метку `todo-task` (обязательно)
3. Добавить метку приоритета если есть
4. Заполнить все секции тела (Суть, Acceptance, Agent context)
5. Использовать шаблон из `task.md`

### Где в проекте правила

Полные правила воркфлоу: `wiki/rules/github-issues-workflow.md`
Структура issue: `wiki/rules/github-issues-workflow.md` § Структура issue

---

## Шаг 10: Финальная проверка

```powershell
Write-Host ""
Write-Host "========================================="
Write-Host "         РЕЗУЛЬТАТ УСТАНОВКИ"
Write-Host "========================================="
Write-Host ""

$ok = 0
$warn = 0
$fail = 0

# 1. Расширение
$ext = & cursor --list-extensions 2>&1 | Select-String "uniogames"
if ($ext) { Write-Host "[OK]   Расширение установлено"; $ok++ }
else { Write-Host "[FAIL] Расширение не найдено"; $fail++ }

# 2. gh
$gh = gh --version 2>&1
if ($gh -match "gh version") { Write-Host "[OK]   gh CLI: $gh"; $ok++ }
else { Write-Host "[FAIL] gh не установлен"; $fail++ }

# 3. Авторизация
$auth = gh auth status 2>&1
if ($auth -match "Logged in") { Write-Host "[OK]   GitHub авторизован"; $ok++ }
else { Write-Host "[WARN] GitHub не авторизован (gh auth login)"; $warn++ }

# 4. opencode
$oc = opencode --version 2>&1
if ($LASTEXITCODE -eq 0) { Write-Host "[OK]   opencode установлен"; $ok++ }
else { Write-Host "[FAIL] opencode не установлен"; $fail++ }

# 5. Git remote
$remote = git remote get-url origin 2>&1
if ($remote -match "github\.com") { Write-Host "[OK]   Git remote: $remote"; $ok++ }
else { Write-Host "[WARN] Git remote не настроен"; $warn++ }

# 6. Настройка repo
$settingsPath = "$env:APPDATA\Cursor\User\settings.json"
if (Test-Path $settingsPath) {
    $settingsRaw = Get-Content $settingsPath -Raw
    if ($settingsRaw -match "uniotasks.repo") {
        Write-Host "[OK]   Настройка repo в settings.json"; $ok++
    } else {
        Write-Host "[WARN] Настройка repo не найдена"; $warn++
    }
} else {
    Write-Host "[WARN] settings.json не найден"; $warn++
}

# 7. Метки
if ($repo) {
    $hasTodo = gh label list --repo $repo 2>&1 | Select-String "todo-task"
    if ($hasTodo) { Write-Host "[OK]   Метка todo-task существует"; $ok++ }
    else { Write-Host "[WARN] Метка todo-task не найдена"; $warn++ }
} else {
    Write-Host "[SKIP] Метки (repo не определён)"
}

# 8. Шаблон issue
if (Test-Path ".github/ISSUE_TEMPLATE/task.md") {
    Write-Host "[OK]   Шаблон task.md создан"; $ok++
} else {
    Write-Host "[WARN] Шаблон task.md не найден"; $warn++
}

Write-Host ""
Write-Host "========================================="
Write-Host "OK: $ok | WARN: $warn | FAIL: $fail"
Write-Host "========================================="
Write-Host ""

if ($fail -gt 0) {
    Write-Host "ИСПРАВЬ ОШИБКИ и повтори скрипт."
} elseif ($warn -gt 0) {
    Write-Host "ЕСТЬ ПРЕДУПРЕЖДЕНИЯ. Исправь по инструкции выше."
} else {
    Write-Host "ВСЁ ГОТОВО!"
}

Write-Host ""
Write-Host "ПЕРЕЗАПУСТИ CURSOR для применения расширения."
Write-Host "После перезапуска в сайдбаре появится иконка UnioTasks."
```

---

## Структура поставки

Папка для друга:
```
TaskLauncher/
├── uniotasks-1.0.4.vsix
└── INSTALL.md
```

## Использование

Другу нужно:
1. Скопировать папку к себе
2. Открыть Cursor
3. В терминале Cursor выполнить:
```powershell
cd "путь/к/папке/TaskLauncher"
. .\INSTALL.md  # или скопировать команды из шагов
```

Или просто попросить AI-агента: *"Прочитай INSTALL.md и установи расширение"*

## Что делает расширение

- GitHub Issues в сайдбаре (группы по приоритету)
- ▶ запускает opencode для задачи
- ⇱ восстанавливает сессию
- Автоматический статус (выполняется / ждёт / сделано)
- Быстрый ввод промта в сайдбаре
- Анализ проекта (кнопка ✦)
