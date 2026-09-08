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
