# GameDesign — AI-Ready GDD (BARAKI)

> **Для агента:** читай документы в порядке ниже. Не реализуй то, что помечено `status: deferred` или `mvp: false`, пока не обновлён `TODO.md`.

## Что это

**BARAKI** — мультиплеерная FFA в **Discord Activity** (desktop). Игрок управляет **базой**, не юнитами. **2–8** игроков в голосовом канале. **Только PvP**, без ботов.

## Формат AI-Ready

Каждый `.md` начинается с YAML front matter:

| Поле | Назначение |
|------|------------|
| `doc_id` | Стабильный идентификатор файла |
| `version` | Версия документа |
| `status` | `draft` \| `review` \| `locked` |
| `depends_on` | Список `doc_id`, которые нужно прочитать раньше |
| `provides` | Что документ определяет для других систем |

Сущности описываются блоками ` ```entity ` с полем `id` (SCREAMING_SNAKE).  
Агент **обязан** использовать эти `id` в коде, ScriptableObject, адресаблах и тестах.

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
| [Balance.md](Balance.md) | Числа; **фиксированные** для всех N | Тюнинг |
| [Technical.md](Technical.md) | Unity, netcode | Реализация |
| [Discord Platform.md](Discord%20Platform.md) | Activity, WebGL, backend | Запуск в Discord |
| [TODO.md](TODO.md) | Backlog | Планирование |

## MVP

**Discord Activity** (desktop), **2–8** игроков, **2 расы** (Люди / Жуки), WebGL + dedicated server. См. `TODO.md`, `Discord Platform.md`.

## Соглашения (код)

```
Assets/Game/
├── GameDesign/
├── Data/
├── Prefabs/
└── Scripts/Runtime/Gameplay/
```

## Открытые вопросы

См. `## Locked decisions` в документах. Новые расы — через `Races.md` pipeline.
