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
| `rules/ability-fx.md` | Ability FX Viewer: клип, масштаб, пикер, VFX Graph Slash_* |
| `rules/fog.md` | Fog of War: симуляция, Divine Blessing, оверлей на миникарте |
| `rules/match-network.md` | Старт матча, снапшоты, бонус-оверлей, host drop / split-brain |
| `rules/content-assets.md` | Контент по категории: Units / BonusUnits / Heroes(+Titan), портреты Humans/{Units\|BonusUnits\|Heroes}, Catalogs/Shared |
| `rules/human-unit-bonuses.md` | PRE-006a: BonusSlot, 6 механик Людей, UI оверлея, контент-пайплайн |
| `rules/arena-buildings.md` | Ориентация зданий на базе: main→дорога, barracks→выход крипов |

## Что здесь не дублируется

Общие принципы живут вне wiki и попадают в контекст автоматически:

- язык общения (русский) — глобальный `~/.config/opencode/AGENTS.md`
- коммиты, тесты, CI, GameDesign — корневой `AGENTS.md`
- описания MCP-инструментов — системный промпт сервера `unityMCP`

Правила wiki содержат только проект-специфику, которую нельзя угадать.
