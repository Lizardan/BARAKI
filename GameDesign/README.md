# GameDesign — AI-Ready GDD (BARAKI)

> **Для агента:** читай документы в порядке ниже. Не реализуй то, что помечено `status: deferred` или `mvp: false`, пока не обновлён `TODO.md`.

## Что это

**BARAKI** — мультиплеерная FFA на **Windows**. Игрок управляет **базой**, не юнитами. **2–8** игроков. **Только PvP**, без ботов. Главное меню — info hub (профиль, друзья, обновления).

## Формат AI-Ready

Каждый `.md` начинается с YAML front matter (`doc_id`, `version`, `status`, `depends_on`, `provides`).

Сущности — блоки ` ```entity ` с полем `id` (SCREAMING_SNAKE).

## Карта документов

| Файл | Содержание | Читать когда |
|------|------------|--------------|
| [Vision.md](Vision.md) | Пиллары, аудитория, scope | Старт проекта |
| [Core Gameplay.md](Core%20Gameplay.md) | Петля, lanes, автобой | Любая геймплейная задача |
| [Map Topology.md](Map%20Topology.md) | Карта 2–8 игроков, LaneGraph | Карта / lanes |
| [Match Flow.md](Match%20Flow.md) | Фазы матча, лобби, disconnect | GameSession |
| [Economy.md](Economy.md) | Золото, доход | Экономика |
| [Units.md](Units.md) | Типы юнитов, статы | Combat |
| [Heroes.md](Heroes.md) | Герои, summon | Hero system |
| [Races.md](Races.md) | 4 расы EA, пайплайн | Race select |
| [Buildings.md](Buildings.md) | Структуры базы | Buildings |
| [Upgrades.md](Upgrades.md) | Дерево исследований | Tech tree |
| [AI.md](AI.md) | Автономия юнитов (не боты) | Unit behavior |
| [Balance.md](Balance.md) | Числа | Тюнинг |
| [Technical.md](Technical.md) | Unity, netcode | Реализация |
| [Platform.md](Platform.md) | Windows hub, UGS, GitHub Releases/Pages | Платформа / дистрибуция |
| [TODO.md](TODO.md) | Backlog | Планирование |

## MVP

**Windows Standalone**, **2–8** игроков, **2 расы**, host-as-server + Unity Lobby + Relay. См. `TODO.md`, `Platform.md`.

## Открытые вопросы

См. `## Locked decisions` в документах.
