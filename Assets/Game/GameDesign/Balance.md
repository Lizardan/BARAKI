---
doc_id: balance
version: 0.7
status: draft
depends_on: [units, buildings, economy, match_flow, map_topology]
provides: [numeric_tables, player_count_scaling, tuning_knobs]
---

# Balance

## Global knobs

```entity
id: BALANCE_WAVE_INTERVAL_BASE
value: 35.0
unit: seconds
scope: per_barracks
note: Интервал level-1; каждый barracks — свой таймер

id: BALANCE_BARRACKS_SPAWN_SPEED_PER_LEVEL
value: 0.05
unit: percent
note: +5% скорости spawn за каждый level barracks (level 2..4)

id: BALANCE_STARTING_GOLD
value: 500

id: BALANCE_PASSIVE_GOLD_TICK
interval: 30.0
base_growth: 0
bonus_per_upgrade_level: 25
max_upgrade_level: 9
```

## Per-barracks wave interval

```entity
id: FORMULA_BARRACKS_WAVE_INTERVAL
formula: interval = BASE / (1.05 ^ (barracks_level - 1))
scope: per_barracks
destroyed_override: interval = BASE   # ruins — всегда L1 speed
examples:
  L1: 35.0
  L2: 33.33
  L3: 31.75
  L4: 30.24
  L4_ruins: 35.0
note: +5% spawn speed per level = делитель 1.05^(level-1); **без** множителя по N

id: BARRACKS_UPGRADE_COST
costs_gold: [1000, 1500, 2500]   # L1→2, L2→3, L3→4
research_time_sec: [45, 90, 135]

id: MAIN_BUILDING_UPGRADE
costs_gold: [2000, 3000]
research_time_sec: [120, 180]

id: MAIN_MAGIC_UNLOCK
costs_gold: [800, 1500, 2500]
research_time_sec: [60, 90, 135]

id: MAIN_PASSIVE_GOLD_UPGRADE
cost_gold: 200
research_time_sec: 25
```

## Player count (N) — без масштабирования статов

**Combat, economy, costs, spawn intervals — одинаковы для N=2…8.** Меняется только **топология карты** (см. `Map Topology.md`).

```entity
id: BALANCE_PLAYER_COUNT
combat_stats: fixed           # HP, dmg, bounty — без N
economy: fixed                # starting gold, costs, passive — без N
spawn_interval: fixed         # FORMULA_BARRACKS_WAVE_INTERVAL — без N
squad_composition: fixed      # без SCALE_SQUAD_COUNT
map_geometry: scales_by_N     # radius, center_arena — процедурная раскладка
mvp: true
```

```entity
id: SCALE_MATCH_DURATION_TARGET
note: Ориентиры playtest; **не** enforced time cap
minutes:
  N2: 12-18
  N4: 18-25
  N8: 25-35
```

## Unit combat (Human / Bug baseline)

| id | HP | Dmg | AS | Range | Bounty |
|----|-----|-----|-----|-------|--------|
| UNIT_*_MELEE | 120 | 9 | 1.0 | 1.5 | 8 |
| UNIT_*_RANGED | 70 | 7 | 1.0 | 8.0 | 6 |
| UNIT_*_CASTER | 60 | 5 | 0.8 | 6.0 | 7 |
| UNIT_*_SIEGE | 200 | 14 | 0.7 | 10.0 | 15 |
| UNIT_*_FLYING | 90 | 9 | 1.0 | 6.0 | 10 |
| UNIT_*_SUPER | 500 | 35 | 0.5 | 12.0 | 50 |
| HERO_*_CHAMPION | 600 | 40 | 0.8 | 1.8 | 80 |

> `*` = `HUMAN` или `BUG`; **одинаковый baseline**; расовые пассивы модифицируют effective stats.

## Caster spells (baseline numbers)

| id | Key values | CD |
|----|------------|-----|
| `SPELL_HUMAN_1` | heal **80**, range **6** | **10s** |
| `SPELL_HUMAN_2` | AoE r **5**, dmg **40** | **14s** |
| `SPELL_HUMAN_3` | resurrect, corpse **≤20s** | **30s** |
| `SPELL_BUG_1` | infect → spawn melee on death | **12s** |
| `SPELL_BUG_2` | egg **120 HP**, hatch **30s** (0 HP → no hatch) | **18s** |
| `SPELL_BUG_3` | mutate **+10%** HP/dmg | **16s** |

Cast range (all): **6**. См. `Races.md` для полных entity.

## Stat upgrade effects

| Track | Per level | Max (L9) |
|-------|-----------|----------|
| `UPG_MELEE_DMG` | +3% | +27% |
| `UPG_RANGED_DMG` | +3% | +27% |
| `UPG_ARMOR` | +3% | +27% |
| `UPG_CASTER_HEAL` | +10% heal | +90% |

## Buildings

| id | HP | Armor |
|----|-----|-------|
| BUILDING_MAIN | 2000 | 5 |
| BUILDING_BARRACKS | 800 | 2 | level 1..4 |
| BUILDING_TOWER | 600 | 3 | ×4 per base (NW/NE/SW/SE) |

## Center arena (map only)

Размер арены / radius — **геометрия карты** по N (`Map Topology.md`), **не** изменение статов юнитов.

## Playtest targets

| N | Target duration | Eliminations before win |
|---|-----------------|-------------------------|
| 2 | 15 min | 1 |
| 4 | 22 min | 2-3 |
| 8 | 30 min | 5-7 |

## Locked decisions

| Решение | Значение |
|---------|----------|
| Hard cap времени | **Нет** — last standing |
| Баланс по N | **Нет** — **одинаковые** статы/экономика/spawn для N=2…8 |
| Отдельный баланс дуэли | **Нет** — те же цифры; отличие только **геометрия** `TOPOLOGY_DUEL` |
| Синхронность волн | **Нет** — per-barracks; level 1..4 ускоряет свой barracks |
| Уничтоженный barracks | Squad frozen; interval L1; level не качается, руины не чинятся |
