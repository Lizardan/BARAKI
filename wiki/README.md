# BARAKI — Wiki · единый справочник документации

В контекст сессии загружается **только этот файл-оглавление**
(`instructions: ["wiki/README.md"]` в `opencode.json`). Ни `wiki/rules/*.md`,
ни `wiki/ARCHITECTURE.md`, ни `GameDesign/*.md` в контекст **не загружаются** —
читаются через `Read` по требованию.

## Как пользоваться справочником (обязательно)

1. Определи область задачи по таблицам ниже (UI / Networking / Abilities / Snapshot / Chat / GDD…).
2. **Перед работой** открой нужные файлы через `Read`. Не предзагружай всё и не читай «на всякий случай».
3. Прочитанное правило — обязательная инструкция для этой задачи, читай целиком.
4. Если файл ссылается на другие («Связанные правила») — читай и их, пока они относятся к задаче.
5. Сводка верхнего уровня — корневой `AGENTS.md` (в контексте всегда) + `wiki/ARCHITECTURE.md`
   (читай при незнакомой задаче).

## wiki/rules — технические правила проекта

| Файл | Когда читать |
|------|--------------|
| `wiki/ARCHITECTURE.md` | Любая незнакомая работа: тех-стек, сцены, карта модулей, Cameras/Input/MCP |
| `rules/unity-async.md` | Async-код: где `Awaitable` (bootstrap), где `UniTask` (gameplay/UI) |
| `rules/unity-core.md` | Любой новый/правленый C#: нейминг, MonoBehaviour, сериализация, lifecycle |
| `rules/code-organization.md` | Новые файлы/папки/asmdef/пакеты, перенос ассетов |
| `rules/unity-reactive.md` | UniRx: `ReactiveProperty`, disposal, UITK-биндинги |
| `rules/unity-ui.md` | Любая UI-работа: UI Toolkit, UIBindingScope, UXML/USS, Controller/ViewModel, палитра |
| `rules/unity-mcp.md` | Операции через Unity MCP: политика, группы инструментов, paging, чтение состояния |
| `rules/abilities.md` | Система способностей: def-ы, поведения, каст, киты, снапшот, добавление/изменение |
| `rules/building-abilities.md` | MAIN-001: Ледяное кольцо и Волна света, wire v23, ground-target прицел |
| `rules/ability-fx.md` | BARAKI Studio, AbilityFx, радиусы, якоря, палитра, сид/rebuild |
| `rules/fog.md` | Fog of War, Divine Blessing, миникарта, презентационный cull в тумане |
| `rules/match-network.md` | Сеть матча: старт, снапшот-интерполяция, host migration, конец/реванш |
| `rules/snapshot-wire.md` | Wire v22: секции, EventStream, контракт презентации, добавление нового визуала/данных |
| `rules/runtime-debug-console.md` | Runtime-консоль и хоткеи |
| `rules/chat.md` | Чат: Cloudflare (общий/друзья/ЛС) и матч-чат Netcode, сокет, композеры |
| `rules/content-assets.md` | Контент: структура `ScriptableObjects/` и `Prefabs/`, портреты, правила папок |
| `rules/faceless-assets.md` | Раса Древние (Faceless): review-сцена, конвертер MDX, материалы/шейдеры, runtime-гейты Фазы 1 (кастер-кит, бонус-пик) |
| `rules/human-unit-bonuses.md` | Юнитовые бонусы/ветераны/уники Людей (PRE-006a/b): механики, ауры, UI |
| `rules/faceless-unit-bonuses.md` | Бонусы Древних (FACELESS-010): слоты 1–10, дизайн, механики, статус гейта |
| `rules/tower-tracks.md` | Апгрейды башен Людей (PRE-007): 9 треков, экономика, wire v22 |
| `rules/arena-buildings.md` | Ориентация зданий на базе, pick/клик-выбор, руины |
| `rules/distribution.md` | Распространение клиента: апдейтер, GitHub Releases, лендинг Cloudflare |
| `rules/github-issues-workflow.md` | Трекинг задач: HacknPlan + UnioTasks (GitHub Issues — архив) |

## GameDesign — GDD (AI-ready)

Полный формат и карта — `GameDesign/README.md` (front matter `status`/`mvp`, сущности
` ```entity id=SCREAMING_SNAKE `, индекс — там же). Таблица-выжимка:

| Файл | Когда читать |
|------|--------------|
| `GameDesign/Vision.md` | Пиллары, аудитория, scope — старт новых направлений |
| `GameDesign/Core Gameplay.md` | Петля, lanes, автобой — любая геймплейная задача |
| `GameDesign/Map Topology.md` | Карта 2–5 игроков, LaneGraph — карта/lanes |
| `GameDesign/Match Flow.md` | Фазы матча, лобби, disconnect — GameSession |
| `GameDesign/Bonuses.md` | Выбор бонуса после race pick — PRE-006 |
| `GameDesign/Economy.md` | Золото, доход — экономика |
| `GameDesign/Units.md` | Типы юнитов, статы — Combat |
| `GameDesign/Heroes.md` | Герои, титан, summon — hero system |
| `GameDesign/Races.md` | Расы, апгрейды башен, пайплайн — race select / PRE-007 |
| `GameDesign/Buildings.md` | Структуры базы — buildings |
| `GameDesign/Upgrades.md` | Дерево исследований / blessing abilities — tech tree |
| `GameDesign/AI.md` | Автономия юнитов (не боты) — unit behavior |
| `GameDesign/Balance.md` | Числа — тюнинг |
| `GameDesign/Technical.md` | Unity, netcode — реализация |
| `GameDesign/Platform.md` | Windows hub, UGS, GitHub Releases/Pages — платформа/дистрибуция |
| `GameDesign/TODO.md` | Карта фаз PRE→GATE→EA и лог; бэклог — HacknPlan |

**Гейт:** не реализовывать `status: deferred` / `mvp: false` без обновления `TODO.md`.

## Что здесь не дублируется

Общие принципы попадают в контекст автоматически и не повторяются в справочнике:
- язык общения (русский) — глобальный `~/.config/opencode/AGENTS.md`
- коммиты, тесты, CI, структура ассемблей, MCP — корневой `AGENTS.md`
- описания MCP-инструментов — системный промпт сервера `unityMCP`

Правила wiki и доки GameDesign содержат только проект-специфику, которую нельзя угадать.