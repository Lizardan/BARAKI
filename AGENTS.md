# AGENTS.md — BARAKI (Unity)

## Проект
- RTS «BARAKI» — ремейк WC3-карты SurvivalChaos (listen-host, NGO, Lobby/Relay UGS).
- Unity `6000.5.5f1` (`F:\Unity\Editor\6000.5.5f1`), Input System, UGUI + UI Toolkit.
- Дистрибуция: полный клиент `BARAKI-{tag}.zip` + `BARAKI-Setup.exe` (Inno Setup) через Cloudflare landing (Pages + Workers).

## Коммуникация
- Общение с пользователем — **на русском** (код/команды/пути — как есть).

## Структура
- `Assets/Game/Scripts/Runtime/` — игровой код:
  - `Gameplay/Match/` — контроллеры, снапшоты, фазы, UI-презентеры.
  - `Gameplay/Combat/` — бой, юниты, башни, марши.
  - `Gameplay/Networking/` — NGO-сессии, лобби, host migration, reconnect.
  - `Core/` — консоль, логирование (PlaytestLog), утилиты.
- `Assets/Game/Scripts/Editor/` — редакторные утилиты (GitHub playtest, сборка).
- `Assets/Game/Scripts/Tests/` — тесты (asmdef `Game.Tests`, NUnit, EditMode-в основном).
- `GameDesign/` — GDD: `README.md` (индекс), `Match Flow.md`, `Platform.md`, `Technical.md`, `TODO.md`, `Vision.md`, референсы `wc3_*` (не трогать — справочные ассеты).
- `docs/` — HTML для GitHub Pages (генерируется из GameDesign, руками не править).
- `.github/workflows/` — `deploy-windows.yml`, `deploy-updater-windows.yml`, `deploy-github-pages.yml`.
- `infra/` — Cloudflare Workers (matchmaker), `cloudflare/` — landing.

## Git-правила
- `GameDesign/wc3_source/*.w3x` — **не трекаются** (файл на диске есть, из индекса удалён). Не добавлять в коммиты.
- Паттерн WIP: коммиты с сообщением `*` (плейсхолдер) — не амендить чужие/прошлые, при работе в открытой сессии сохранять локальные изменения без коммита.
- Коммитить только по явной просьбе.

## Тесты
- Запуск: Unity Test Runner, EditMode (asmdef `Game.Tests`). PlayMode — редко.
- CI-гейта на тесты **нет**; тесты — только для разработки (в репозитории).
- После правки кода: проверить консоль на ошибки компиляции (`read_console`) и прогнать затронутые тесты.

## Unity MCP
- Доступен сервер `unityMCP` (мост на `:6400`). Перед изменением состояния редактора читать ресурсы (`mcpforunity://editor/state` и др.), пути — относительно `Assets/`.
- Unity API проверять через `unity_reflect`/`unity_docs`, не полагаться на память модели.
