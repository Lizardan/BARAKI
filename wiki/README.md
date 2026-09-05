# BARAKI — Wiki

Справочные материалы проекта. Правила агентов подключены через `opencode.json`
(`instructions: ["wiki/rules/*.md"]`) — загружаются в каждую сессию.

## Содержание

| Файл | Назначение |
|------|-----------|
| `ARCHITECTURE.md` | Тех-стек, сцены, карта модулей, Cameras/Input/MCP |
| `rules/unity-async.md` | Awaitable vs UniTask по ассемблеям |
| `rules/unity-core.md` | Нейминг C#/MonoBehaviour, сериализация |
| `rules/code-organization.md` | Раскладка `Assets/Game/`, asmdef, сцены, пакеты |
| `rules/unity-reactive.md` | UniRx — реактивные свойства, disposal |
| `rules/unity-ui.md` | UI Toolkit, UIBindingScope, USS-стили |
| `rules/unity-mcp.md` | Unity MCP — политика и воркфлоу |
| `rules/abilities.md` | Система способностей: def-ы, поведения, каст, снапшот |
| `rules/building-abilities.md` | MAIN-001: Ледяное кольцо (L1) и Волна света (L2), wire v23, прицел ground-target |
| `rules/ability-fx.md` | BARAKI Studio: одно окно VFX+радиус, механика AoE, якоря, палитра |
| `rules/fog.md` | Fog of War: симуляция, Divine Blessing, миникарта, cull презентации в тумане |
| `rules/match-network.md` | Старт матча, снапшот-интерполяция, host migration, конец матча |
| `rules/snapshot-wire.md` | Wire v21: секции, static/dynamic split, EventStream, контракт презентации |
| `rules/runtime-debug-console.md` | Хоткеи runtime-консоли (`~` открыть, Esc закрыть) |
| `rules/chat.md` | Чат: Cloudflare (общий / друзья / ЛС) + матч Netcode |
| `rules/content-assets.md` | Контент по категории: Units / BonusUnits / Heroes(+Titan), портреты Humans/{Units\|BonusUnits\|Heroes}, Catalogs/Shared |
| `rules/faceless-assets.md` | Раса Древние (Faceless): review 01–12, конвертер MDX, пока без ролей |
| `rules/human-unit-bonuses.md` | PRE-006a: BonusSlot, 6 механик Людей, UI оверлея, контент-пайплайн |
| `rules/tower-tracks.md` | PRE-007: 9 треков апгрейдов башен Людей, экономика, wire v22, эффекты |
| `rules/arena-buildings.md` | Ориентация зданий на базе: main→дорога, barracks→выход крипов |
| `rules/distribution.md` | Установщик Inno, GitHub Releases, лендинг Cloudflare Pages |
| `rules/github-issues-workflow.md` | Трекинг: HacknPlan + UnioTasks (GitHub Issues — архив) |

## Что здесь не дублируется

Общие принципы живут вне wiki и попадают в контекст автоматически:

- язык общения (русский) — глобальный `~/.config/opencode/AGENTS.md`
- коммиты, тесты, CI, GameDesign — корневой `AGENTS.md`
- описания MCP-инструментов — системный промпт сервера `unityMCP`

Правила wiki содержат только проект-специфику, которую нельзя угадать.
