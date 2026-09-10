# Память проекта BARAKI

## Общение
- Пользователь явно подтвердил (2026-09-08): **все ответы на русском**. Код, команды,
  пути, идентификаторы — как есть, без перевода.

## Unity MCP
- Сервер `unityMCP` (мост на `:6400`) описан в `C:\Users\Lizardan\.config\opencode\opencode.json`
  (opencode) и продублирован в `~/.workbuddy-ai/mcp.json` для WorkBuddy.
- Запуск: `uvx --from mcpforunityserver==10.1.2 mcp-for-unity --transport stdio`
  (`C:\Users\Lizardan\.local\bin\uvx.exe`). После добавления конфига нужен «Trust» в
  интерфейсе коннекторов — сам не активируется.
- Через него: `read_console` (ошибки компиляции), `run_tests` / `get_test_job` (EditMode),
  состояние редактора. Пока редактор открыт, batch-прогон Unity из CLI использовать нельзя
  (блокирует проект) — только MCP.

## Трекинг задач
- Источник правды по задачам — HacknPlan проект 242091; UnioTasks — только панель
  (Cursor extension, в git BARAKI не лежит).
- Чтение backlog агентом: `node Tooling/HacknPlan/list-open.js`.
- Детали и формат API — `wiki/rules/github-issues-workflow.md`.

## Паттерны (skills)
- `add-race-tower-tracks` — добавление tower tracks новой расе (параллельная таблица,
  wire-контракт 9 слотов не меняется). Создан при FACELESS-017.

## Документация: где что писать
- Конвенция из `AGENTS.md`: технические правила → `wiki/rules/*.md` (+ строка в
  `wiki/README.md`), геймдизайн → `GameDesign/*.md` (+ строка в `GameDesign/README.md`).
  Поэтому у одной механики **нормально** живут два документа — технический и GDD.
- `wiki/` и корневые `*.md` Unity **не импортирует** (нет `.meta` и не должно быть).
  `GameDesign/` тоже вне asset database (в `Assets/` нет симлинка/junction), но там
  лежат legacy-`.meta` из git; часть доков (`Bonuses.md`, `Platform.md`, `Arena.md`)
  их не имеет. **Не выдумывать `.meta` руками** — файлы не в Unity-пайплайне.

## Правила, выясненные на практике
- **Пауза ≠ остановка всего.** `MatchPauseGate.IsPaused` роняет `Time.timeScale = 0`
  (игрок/миграция/дисконнект). Для режимов, которые должны жить на паузе матча,
  есть `IsArenaPaused`/`IsSimulationPaused` — блокируют тик матча и команды, но
  не timeScale. Подробности — `wiki/rules/arena.md`.
- **Batch-компиляция Unity невозможна, пока редактор открыт** — падает с
  «another Unity instance is running». Вместо этого читать `Logs/Editor.log`
  (проектный лог; `%LOCALAPPDATA%/Unity/Editor/Editor.log` только указывает на него)
  и смотреть ошибки `error CS` + свежесть `Library/ScriptAssemblies/*.dll`.
- **Правки USS не видны в уже запущенной Play-сессии.** Стилевой лист применяется при
  создании панели `UIDocument`, поэтому после правки `.uss` нужно выйти и снова войти
  в Play mode. Симптом: стили «не применились» (текст дефолтного размера, элемент в
  потоке) хотя файл корректен. Диагностика — по `Logs/Editor.log`: сравнить номер
  строки последнего `Importing ... <файл>.uss` с последним `Entering Playmode`;
  если импорт позже — сессия stale.
