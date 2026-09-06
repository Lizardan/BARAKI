# AGENTS.md — BARAKI (Unity)

## Проект
- Мультиплеерная FFA-стратегия BARAKI: 2–5 игроков, игрок строит базу, юниты автономны. listen-host (host-as-server) + Netcode for GameObjects + UGS (Lobby/Relay/Friends/Cloud Save).
- Unity `6000.6.0f1` (`F:\Unity\Editor\6000.6.0f1`), C# 12, URP 17.6, Cinemachine 6.6 (CM3 API), Input System 1.20.
- Асинхронность: `Awaitable` — только в bootstrap (`Game.Core`); Gameplay/UI — `UniTask`/`UniTaskVoid`. **Никогда** `Task`/`async void`.

## Коммуникация
- Общение с пользователем — **на русском** (код/команды/пути — как есть).
- Коммиты — только по явной просьбе; сообщения **на русском**, первая строка — повелительное наклонение («Добавить», «Исправить», «Удалить»), ≤72 симв. Английский — только для имён файлов, API и идентификаторов.
- Паттерн WIP: коммиты с сообщением `*` (плейсхолдер) — не амендить чужие/прошлые, при работе в открытой сессии сохранять локальные изменения без коммита.
- **Документирование после работы:** после любой завершённой работы (новая фича / рефакторинг / фикс / смена механики) — если появилось что-то новое или что-то переделано, зафиксировать это на будущее: короткая запись в `wiki/` (`wiki/rules/*.md` для технических правил, `wiki/README.md` — единый справочник) или в `GameDesign/` для геймдизайна. Цель — чтобы другие агенты и разработчики могли понять устройство без реверс-инжиниринга.
- **Загрузка документации:** в контекст автозагружается **только** единый справочник `wiki/README.md` (инструкция `opencode.json`). Файлы `wiki/rules/*.md` и `GameDesign/*.md` в контекст не грузятся — читать через `Read` по требованию по таблицам справочника, не предзагружать «на всякий случай».

## UI (критично)
- **Только UI Toolkit** (UXML/USS/UIDocument). Не добавлять `com.unity.ugui`, Canvas, `UnityEngine.UI` — в кодовой базе их нет.
- Реактивные биндинги — обязательно через `UIBindingScope` (`Assets/Game/UI/Runtime/Bindings/`), dispose в `OnDestroy`.
- Колбэки UI регистрировать в `OnEnable`, снимать в `OnDisable` (в `OnDestroy` `rootVisualElement` уже null — NRE).
- Стек: UniRx (`ReactiveProperty`/`ReactiveCommand`) + UniTask.

## Структура (asmdef = namespace)
- `Game.Core` — `Scripts/Runtime/Core/` (GameManager, GameSession, bootstrap).
- `Game.Gameplay` — `Scripts/Runtime/Gameplay/` (Combat, Match, Networking, Cameras, Vfx, Data).
- `Game.Input` — `Scripts/Runtime/Input/` (генерируется из `Assets/Game/Settings/Input/GameInputActions.inputactions`).
- `Game.UI` — `Assets/Game/UI/Runtime/`; `Game.Editor` — `Scripts/Editor/` (в т.ч. `Mcp/`); `Game.Tests` — `Scripts/Tests/`.
- Сцены: `Bootstrap` (build 0) → `MainMenu` (1) → `Lobby` → `Game`. Иерархия: `--- SYSTEMS ---` / `--- CAMERAS ---` / `--- LEVEL ---` / `--- UI ---` / `--- DYNAMIC ---`.
- Принципы: композиция вместо синглтонов. Исключения — `GameManager` (persist) и Netcode `NetworkBehaviour`-синглтоны (`NetworkLobbyState`, `MatchNetworkAuthority`, `NetworkRacePickState`, `HostMigrationCoordinator`): `Instance` устанавливается в `OnNetworkSpawn`, снимается в `OnNetworkDespawn`. Сценарные объекты (`MatchRuntime` и др.) — через static `Current` (set `OnEnable`, clear `OnDisable`), не `FindAnyObjectByType`. Детальные правила — `wiki/rules/*.md` (справочник — `wiki/README.md`).

## GameDesign (GDD)
- AI-ready формат: YAML front matter (`status`, `mvp`), сущности ` ```entity id=SCREAMING_SNAKE `. Индекс — `GameDesign/README.md` (выжимка и ссылки — в едином справочнике `wiki/README.md`).
- **Не реализовывать** `status: deferred` / `mvp: false` без обновления `TODO.md`.

## CI / Release (не сломать)
- Push в `main` по путям `Assets/**`, `Packages/**`, `ProjectSettings/**`, `Tooling/BuildSupport/**` → сборка + авто-bump + GitHub Release. `[skip release]` в сообщении — пропуск.
- Версия: Editor `bundleVersion` = последний GitHub tag + 1 patch. Смена линии (`0.1.*` → `0.2.*`) — выставить `X.Y.1`; CI снимет следующий релиз в `vX.Y.0`.
- **Теги:** `v*` — полный клиент (единственный `/releases/latest`); `updater-v*` — апдейтер (prerelease, никогда не latest; release-prune их не трогает).
- CI подменяет `Packages/manifest.json` на `Packages/manifest.ci.json` (без MCP/Cursor/Pipeline/Project Auditor). **При добавлении рантайм-зависимости править оба файла** или запустить `pwsh -File Packages/Sync-Packages.ps1`.
- Билд: `Game.Editor.WindowsCiBuild.Build`. Задачи — HacknPlan (проект 242091) + UnioTasks; см. `wiki/rules/github-issues-workflow.md`. GitHub Issues не редактировать.
- `Tooling/` — вспомогательная инфраструктура вне Unity-проекта: `Tooling/docs/` (простые HTML privacy/terms, деплой as-is), `Tooling/cloudflare/baraki-landing/` (Pages + `functions/download.js` — редирект на `updater-v*/BARAKI-Setup.exe`), `Tooling/BuildSupport/` (скрипты CI + Inno Setup апдейтера).

## Тесты
- asmdef `Game.Tests`, NUnit, EditMode в основном.
- Запуск: Unity Test Runner или MCP (`run_tests` / `get_test_job`, группа `testing`).
- После правки кода: проверить `read_console` на ошибки компиляции и прогнать затронутые тесты.

## Unity MCP
- Сервер `unityMCP` (мост на `:6400`). Пути — относительно `Assets/`. Перед мутацией редактора читать `mcpforunity://editor/state`, ждать `isCompiling: false`.
- Unity API проверять через `unity_reflect`/`unity_docs`, не полагаться на память модели.

## Unity Pipeline (HTTP API)
- Параллельный HTTP API Unity Editor (`com.unity.pipeline`). **Не конфликтует** с MCP (разные порты, протоколы, transports).
- Доступ: `unity command --project-path "F:\Unity Projects\BARAKI" <cmd>` через bash. Токен и порт автоматически через descriptor `Library/Pipeline/.unity-pipeline-port`.
- Использовать для команд, которых нет в MCP: `audit` (Project Auditor), `capture_game_view source=screen` (скриншот с Overlay UI), `get_performance_stats`, `set_autotick`.
- Не дублировать MCP-команды через Pipeline (manage_gameobject, manage_scene и т.д. — MCP быстрее и удобнее).
- Аудит: `audit` → сканирование ~30–60 с → `audit_status` → CSV в `Temp/pipeline-audit/`.
