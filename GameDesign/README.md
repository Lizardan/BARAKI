# GameDesign — AI-Ready GDD (BARAKI)

> **Для агента:** читай документы в порядке ниже. Не реализуй то, что помечено `status: deferred` или `mvp: false`, пока не обновлён `TODO.md`.

## Что это

**BARAKI** — мультиплеерная FFA на **Windows**. Игрок управляет **базой**, не юнитами. **2–5** игроков. **Только PvP**, без ботов. Главное меню — info hub (профиль, друзья, обновления).

## Формат AI-Ready

Каждый `.md` начинается с YAML front matter (`doc_id`, `version`, `status`, `depends_on`, `provides`).

Сущности — блоки ` ```entity ` с полем `id` (SCREAMING_SNAKE).

## Карта документов

| Файл | Содержание | Читать когда |
|------|------------|--------------|
| [Vision.md](Vision.md) | Пиллары, аудитория, scope | Старт проекта |
| [Core Gameplay.md](Core%20Gameplay.md) | Петля, lanes, автобой | Любая геймплейная задача |
| [Map Topology.md](Map%20Topology.md) | Карта 2–5 игроков, LaneGraph | Карта / lanes |
| [Match Flow.md](Match%20Flow.md) | Фазы матча, лобби, disconnect | GameSession |
| [Bonuses.md](Bonuses.md) | Выбор бонуса после race pick | PRE-006 / гейт до расы #2 |
| [Economy.md](Economy.md) | Золото, доход | Экономика |
| [Units.md](Units.md) | Типы юнитов, статы | Combat |
| [Heroes.md](Heroes.md) | Герои, титан, summon | Hero system |
| [Races.md](Races.md) | Расы, tower ×9, пайплайн | Race select / PRE-007 |
| [Buildings.md](Buildings.md) | Структуры базы | Buildings |
| [Upgrades.md](Upgrades.md) | Дерево исследований / blessing abilities | Tech tree / PRE-005 |
| [AI.md](AI.md) | Автономия юнитов (не боты) | Unit behavior |
| [Balance.md](Balance.md) | Числа | Тюнинг |
| [Technical.md](Technical.md) | Unity, netcode | Реализация |
| [Platform.md](Platform.md) | Windows hub, UGS, GitHub Releases/Pages | Платформа / дистрибуция |
| [TODO.md](TODO.md) | Карта фаз PRE→GATE→EA и лог; бэклог — HacknPlan | Планирование |

## MVP

**Windows Standalone**, **2–5** игроков, **1 раса** (Люди), host-as-server + Unity Lobby + Relay. См. `TODO.md`, `Platform.md`.

## Открытые вопросы

См. `## Locked decisions` в документах.
