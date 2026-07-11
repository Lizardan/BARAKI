---
doc_id: buildings
version: 1.1
status: draft
depends_on: [core_gameplay, economy, match_flow, units]
provides: [building_types, hp, player_controls, destruction_effects, barracks_levels]
---

# Buildings

## Building schema

```yaml
id: string
building_type: enum    # Main | Barracks | Tower | Workshop
lane_binding: enum     # Left | Center | Right | None
max_hp: float
armor: float
is_destroyed: bool
prefab: string              # alive | ruins

# Main only
main_level: int            # 1..3

# Barracks only
barracks_level: int         # 1..4 — squad; frozen при уничтожении
wave_interval: float
wave_timer: float

# Tower only (alive)
player_control: target_priority

grants: SquadCompositionId   # barracks only
```

## Standard base layout (per player)

**8 зданий** для elimination: main + 3 barracks + **4 towers**.  
Референс: **main (●) центр · towers (■) квадрат · barracks (▲) снаружи · чёрные линии = lane splines от barracks**.

> **Жёсткое правило:** у каждого игрока **ровно 3 дороги (lane splines)** — не больше и не меньше.  
> **1 barracks = 1 lane.** Четвёртой дороги с тыла, от main или от башен **нет**.

> **Ориентация на карте:** база стоит **на периметре**; **тыл** (сторона без barracks) смотрит **наружу — к краю карты**; **перед** (3 barracks + lanes) — **внутрь**, к центру карты и врагам.

Пусть **`d`** = расстояние `MAIN` → каждая **башня** (все равны).

### Расстановка (local space; +Y = к центру карты, −Y = к краю / тыл)

```
         ▲ к центру карты / врагам
              [Barr C] ─────── center lane ───────→
                  │
         [T NW]───┼───[T NE]
                  │
         [T SW]──[MAIN]──[T SE]
                  │
           ▼ тыл → край карты
      (нет barracks / нет lane)

[Barr L] ── left ──→              ←── right ── [Barr R]
```

| Элемент | Правило |
|---------|---------|
| **MAIN** | Центр базы |
| **4 towers** | Квадрат вокруг main (NW / NE / SW / SE); расстояние **`d`** |
| **3 barracks** | Середина стороны **между двумя башнями** + **`d` наружу** к lane (= main→tower) |
| **Lanes** | Сплайн **от barracks** наружу (как чёрные линии на схеме) |
| **Тыл** | SW + SE башни; **без** barracks и **без** дороги; **к краю карты** |

| Barracks | Между башнями | Lane |
|----------|---------------|------|
| CENTER | T_NW ↔ T_NE | Center → arena |
| LEFT | T_NW ↔ T_SW | Left flank |
| RIGHT | T_NE ↔ T_SE | Right flank |

### Local coords (`MatchArenaGenerator`)

`MAIN` = `(0,0)`. Towers `(±d, ±d)`. Barracks — грань + **`d` наружу**: CENTER `(0, 2d)`, LEFT `(-2d, 0)`, RIGHT `(2d, 0)`.  
Local **+Y** = к **центру карты**; **−Y** = **тыл** к **краю карты**. Генератор ставит базу на периметре и поворачивает так, чтобы −Y смотрел **от центра** (наружу).

MVP: Main L1 + 3× Barracks L1 + 4× Tower.

```entity
id: BASE_LAYOUT
buildings_total: 8
map_placement: perimeter_edge
base_orientation:
  front_local_axis: +Y
  front_faces: map_center
  rear_local_axis: -Y
  rear_faces: map_edge
main: BUILDING_MAIN
barracks: [BUILDING_BARRACKS_LEFT, BUILDING_BARRACKS_CENTER, BUILDING_BARRACKS_RIGHT]
towers: [BUILDING_TOWER_NW, BUILDING_TOWER_NE, BUILDING_TOWER_SW, BUILDING_TOWER_SE]
tower_placement: square_around_main
main_to_tower_distance: d
barracks_placement: edge_midpoint_between_towers_plus_outward
barracks_outward_distance: d
rear_side: no_barracks_no_lane
lane_spline_origin: barracks
lanes_per_player: 3              # hard cap — only LEFT, CENTER, RIGHT
lanes_from_base: 3
max_lane_splines: 3              # no 4th road from towers / main / rear
barracks_to_lane: one_to_one
barracks_edges:
  CENTER: [TOWER_NW, TOWER_NE] → LANE_CENTER
  LEFT: [TOWER_NW, TOWER_SW] → LANE_LEFT
  RIGHT: [TOWER_NE, TOWER_SE] → LANE_RIGHT
mvp: true
```

## Barracks levels (1 → 4) — живой barracks

```entity
id: BARRACKS_LEVEL_RULES
min_level: 1
max_level: 4
start_level: 1
upgrade_costs: [1000, 1500, 2500]   # L1→2, L2→3, L3→4
spawn: SQUAD_BARRACKS_L{barracks_level}
wave_interval: FORMULA_BARRACKS_WAVE_INTERVAL
mvp: true
```

| Level | Squad | Interval (sec) | Spawn speed vs L1 |
|-------|-------|---------------------|-------------------|
| 1 | 4 | **35.0** | 100% |
| 2 | 7 | 33.3 | +5% |
| 3 | 10 | 31.8 | +10.25% |
| 4 | 14 | 30.2 | +15.76% |

Каждый level barracks: **+5%** скорости spawn (см. `Balance.md`).

## Building definitions (MVP)

```entity
id: BUILDING_MAIN
building_type: Main
main_level: 1
max_level: 3
max_hp: 2000
armor: 5
abilities: [HERO_HIRE, UPG_MAIN_BUILDING_LEVEL, UPG_MAIN_PASSIVE_GOLD, UPG_MAIN_MAGIC]
player_actions: [hire_hero, upgrade_main_level, upgrade_main_passive_gold, upgrade_main_magic]
upgrade_costs: [2000, 3000]
gates:
  stat_upgrade_max: main_level * 3
  heroes_hire_max: main_level
  magic_upgrade_max: main_level
note: Magic unlocks **race caster spells**; не active abilities на main
destroyed_effect: main_ruins    # НЕ elimination — см. PLAYER_ELIMINATION
on_destroy:
  is_destroyed: true
  disable: [hero_hire, upgrade_main_level, upgrade_main_passive_gold, upgrade_main_magic]
  passive_gold: off
  barracks_spawn: continues
  player_eliminated: false
mvp: true

id: BUILDING_BARRACKS
building_type: Barracks
barracks_level: 1
is_destroyed: false
max_hp: 800
armor: 2
grants: SQUAD_BARRACKS_L1
mvp: true

id: BUILDING_TOWER
building_type: Tower
position: NW | NE | SW | SE    # квадрат вокруг main
lane_binding: None
max_hp: 600
armor: 3
damage: 15-20
range: 12
attack_speed: 0.5
player_control: target_priority
player_actions: [set_target_priority, upgrade_tower_race]
count_per_base: 4
mvp: true
```

## Destruction — barracks (ruins, spawn continues)

```entity
id: BARRACKS_DESTROYED
trigger: BUILDING_BARRACKS HP → 0
on_destroy:
  barracks_level: frozen
  is_destroyed: true
  spawn: continues
  squad: SQUAD_BARRACKS_L{barracks_level}
  wave_interval: L1 speed (35s)
  no_upgrade: true
  no_restore: true
  player_actions: none
  visual: ruins
mvp: true
```

| Был level | Squad | Timer |
|-----------|-------|-------|
| 3 | L3 (10 юн.) | **35 s** (L1 speed) |
| 4 | L4 (14 юн.) | **35 s** (L1 speed) |

## Destruction — tower (ruins, no function)

```entity
id: TOWER_DESTROYED
trigger: BUILDING_TOWER HP → 0
on_destroy:
  is_destroyed: true
  no_restore: true
  no_targeting: true           # игрок не переключает режим
  no_research: true            # апгрейды через tower недоступны
  visual: tower_ruins          # подножье / руины башни
  combat: none
mvp: true
```

> Башня **не спавнит** и **не чинится**. Остаётся визуальное подножье.

## Destruction — main (ruins, не elimination)

```entity
id: MAIN_DESTROYED
trigger: BUILDING_MAIN HP → 0
on_destroy:
  is_destroyed: true
  visual: main_ruins
  no_restore: true
  disable: [hero_hire, upgrade_main_level, upgrade_main_passive_gold, upgrade_main_magic]
  passive_gold: off
  barracks_spawn: continues      # пока есть barracks (alive или ruins)
  tower_combat: continues       # пока башни живы
  player_eliminated: false      # только когда все 8 зданий уничтожены
mvp: true
```

## Player elimination (все здания)

```entity
id: PLAYER_ELIMINATED
trigger: all of [MAIN, BARRACKS×3, TOWER×4] is_destroyed
on_eliminate:
  spawn: off
  units: despawn
  state: eliminated_spectator
mvp: true
```

## Player building controls

| Building | Player can |
|----------|------------|
| Main (alive) | Upgrade main level, passive gold, **magic**, hire heroes |
| Main (ruins) | **Ничего** |
| Barracks (alive) | Upgrade level, stat research, **Deploy hero** (1000g, instant) |
| Barracks (ruins) | **Ничего** |
| Tower (alive) | Target mode + **race tower upgrades** |
| Tower (ruins) | **Ничего** |

Stat research — только через **живой** barracks.

## Destruction effects (summary)

| Building | On destroy |
|----------|------------|
| Barracks | Ruins: frozen squad + **L1 timer**; no upgrade/restore |
| Tower | Ruins (подножье); **нет** обороны, targeting, research |
| Main | Ruins; **нет** hire/main upgrades/passive gold; **не** elimination |
| **All 8 destroyed** | **Elimination** — spawn off, units despawn, spectator |

## Locked decisions

| Решение | Значение |
|---------|----------|
| Barracks upgrade cost | **1000 / 1500 / 2500** gold (L1→2→3→4) |
| Base spawn interval | **35 s** (level 1) |
| Spawn speed per level | **+5%** за каждый level barracks |
| Tower destroyed | **Ruins** (подножье); без функций; не чинится |
| Main hall abilities (MVP) | **Нет** — Phase 2 |
| Main building levels | **1–3**; gates stat cap, hero hire & passive gold cap |
| Main passive gold | **+0/30s** без прокачки; **+25g/level**; max **9** |
| Main magic | **1/2/3** slots = main level; unlocks **race caster spells** |
| Tower upgrades | **5 tracks × L1–3**; race-wide; см. `Races.md` |
| Elimination | **Все 8 зданий** уничтожены; main alone **не** выбывание |
| Base towers | **4** — квадрат NW/NE/SW/SE вокруг main (расст. **d**) |
| Barracks layout | **3 lane** (L/C/R); тыл **к краю карты**; перед **к центру** |
