---
doc_id: core_gameplay
version: 0.5
status: locked
depends_on: [vision, map_topology]
provides: [core_loop, lanes, player_actions, unit_autonomy]
---

# Core Gameplay

## Core loop

```
┌─────────────┐     kills      ┌──────────────┐
│ Enemy units │ ──────────────►│ Gold income  │
└─────────────┘                └──────┬───────┘
                                      │
                    spend             ▼
              ┌───────────────────────────────┐
              │ Upgrades · Buildings · Heroes │
              └───────────────┬───────────────┘
                              │
                              ▼
              ┌───────────────────────────────┐
              │ Stronger waves · defense tools │
              └───────────────┬───────────────┘
                              │
                              ▼
              ┌───────────────────────────────┐
              │ Pressure lanes → eliminate foes │
              └───────────────────────────────┘
```

**Игрок никогда не выделяет юнитов.** Влияние только через здания, апгрейды, героев, оборону.

## Карта и lanes (2–8 игроков)

Полная спецификация: **`Map Topology.md`**.

Кратко:

| N | Топология | Flank lanes | Center lane |
|---|-----------|-------------|-------------|
| 2 | `TOPOLOGY_DUEL` | 1 соперник (дублируется L/R) | 1 соперник, zero-sum |
| 3–8 | `TOPOLOGY_RING` | Сосед ±1 по кольцу | **Напротив** (+ merge в арене; non-zero-sum) |

```entity
id: LANE_LEFT
name: Left Flank
fights: [ring_neighbor_ccw]
gold_model: zero_sum
mvp: true

id: LANE_CENTER
name: Center
march_target: [opposite_slot]          # center_primary_target(i)
arena_combat: [all_opponents]          # бой в Central Arena
gold_model: non_zero_sum_if_n_gte_3
on_target_eliminated: next_alive_clockwise
mvp: true

id: LANE_RIGHT
name: Right Flank
fights: [ring_neighbor_cw]
gold_model: zero_sum
mvp: true
```

### Политика lane по размеру матча

- **Дуэль (2):** все три lane — разные коридоры к одному врагу; center не даёт «тройного дохода», но быстрее доставляет волну.
- **FFA (3+):** center — march к **игроку напротив** через **Central Arena**; все center-потоки сходятся → **массовый бой в центре**; flank — дуэли с соседями.
- **8 игроков:** center перегружен **геометрией** (все сходятся в арену); статы те же, что при N=2.

## Автоспавн и состав отряда

Каждый **barracks — независимый спавнер** (level **1–4**, старт level 1). Волны **не синхронизированы** между lane.

Состав **кумулятивный** по level barracks (см. `Units.md`):

| Level | Melee | Ranged | Caster | Siege | Flying | Super | **Всего** |
|-------|-------|--------|--------|-------|--------|-------|-----------|
| 1 | 2 | 1 | 1 | — | — | — | **4** |
| 2 | +1 | — | — | +2 | — | — | **7** |
| 3 | — | +1 | +1 | — | +1 | — | **10** |
| 4 | +1 | +1 | — | +1 | — | +1 | **14** |

Level ↑ также **ускоряет** spawn. Уничтоженный barracks → **ruins**: squad заморожен, interval как L1, level **не качается**, руины **не чинятся**.

## Автоповедение юнитов

См. `AI.md` (unit autonomy). Кратко: spawn → march по сплайну lane → engage → bounty.

## Player actions

| Действие | MVP |
|----------|-----|
| Research upgrade | ✓ |
| Upgrade barracks level | ✓ (1→4, per lane) |
| Upgrade main level | ✓ (2000g → L2, 3000g → L3) |
| Main magic | ✓ (до `main_level` слотов; race spells) |
| Hire hero (main) | ✓ (500g; cap = main level) |
| Deploy hero (barracks) | ✓ |
| Tower target mode + race upgrades | ✓ |

## Failure spectrum

| Урон | Эффект |
|------|--------|
| Barracks уничтожен | **Ruins** — spawn frozen level на L1 timer; level не качается, не чинится |
| Main уничтожен | **Ruins** — нет hire/main upgrades/passive gold; **игрок ещё в матче** |
| Все здания уничтожены | **Elimination** — **8** зданий: main + 3 barracks + **4 towers** |

## Camera

Изометрия.

## Locked decisions

| Решение | Значение |
|---------|----------|
| Интервал волн | **Per-barracks** — у каждого barracks свой таймер; **без глобальной синхронности** |
| Уничтоженный barracks | Ruins: frozen squad + L1 speed; level не качается, не чинится |
| Eliminated игрок | **Все 8 зданий** уничтожены → spawn off, despawn, center retarget, spectator |
