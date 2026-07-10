---
doc_id: races
version: 0.9
status: draft
depends_on: [units, buildings, upgrades]
provides: [race_definitions, original_factions, content_pipeline, roster, race_asymmetry]
---

# Races

## Принцип

Все фракции — **оригинальные**: свой лор, визуал, **асимметричные** бонусы и апгрейды. WC3 — только жанровый референс.

Архитектура **data-driven**: новая раса = assets + SO + запись здесь, без правок combat core.

## Асимметрия рас (G1 — зафиксировано)

Каждая раса отличается **четырьмя осями**:

| Ось | Описание |
|-----|----------|
| **Стартовые пассивы** | **2 положительных** + **1 отрицательный** с начала матча |
| **Tower upgrades** | Уникальные для расы улучшения **в башнях** → статы или способности юнитам |
| **Маги (casters)** | В отряде; **уникальные заклинания** per race |
| **Magic upgrades (main)** | Прокачка **в главном здании** → открывает/усиливает заклинания магов |

```entity
id: RACE_START_PASSIVES
positive_count: 2
negative_count: 1
scope: race_wide
apply: match_start
examples_human: PASSIVE_HUMAN_* (см. ниже)
examples_bug: PASSIVE_BUG_* (см. ниже)
mvp: true
```

```entity
id: RACE_TOWER_UPGRADES
location: BUILDING_TOWER   # alive only
scope: race_unique
effect: unit_stat_or_ability
tracks_per_race: 5
costs_gold: [500, 800, 1200]       # per track level L1, L2, L3
research_time_sec: [45, 90, 135]
mvp: true
```

```entity
id: RACE_CASTER_SPELLS
unit_type: UNIT_TYPE_CASTER
spells: race_unique_list
unlock: UPG_MAIN_MAGIC       # см. Upgrades.md
mvp: true
```

## Milestones

| Этап | Рас | Примечание |
|------|-----|------------|
| **MVP / старт** | **2** | `RACE_HUMAN`, `RACE_BUG` — passives, magic, tower kit **confirmed** |
| Рост контента | +N | Полный asymmetry kit per race |
| Early Access (цель) | **4+** | Каждая с уникальным набором passives / tower / magic |

## Race schema

```yaml
id: string                 # RACE_HUMAN
display_name: string
description: string
fantasy_hook: string

start_passives:
  positive: [PassiveId, PassiveId]
  negative: PassiveId

unit_roster:               # 6 типов на расу
  melee: UnitDefinition
  ranged: UnitDefinition
  caster: UnitDefinition   # маги — уникальные заклинания расы
  siege: UnitDefinition
  flying: UnitDefinition
  super: UnitDefinition
hero_roster: HeroDefinition[]
building_skin: BuildingSet
upgrade_tree: UpgradeTree   # stat + tower + magic tracks

theme_color: Color
mvp: bool
```

## Стартовые пассивы — Human / Bug (confirmed)

### Люди

```entity
id: PASSIVE_HUMAN_STEEL_ARMS
effect: +10% damage
applies_to: [units, BUILDING_TOWER]
mvp: true

id: PASSIVE_HUMAN_FORTIFIED_LINE
effect: +10% defense
applies_to: [units, buildings]
note: armor / damage reduction — ×1.1 armor baseline (playtest tune)
mvp: true

id: PASSIVE_HUMAN_LEVY_TAX
effect: -250 starting gold
formula: start_gold = ECON_START - 250   # 500 → 250
mvp: true
```

| | Пассив | Эффект |
|---|--------|--------|
| **+** | `PASSIVE_HUMAN_STEEL_ARMS` | **+10% урон** юнитов и **башен** |
| **+** | `PASSIVE_HUMAN_FORTIFIED_LINE` | **+10% защита** юнитов и **зданий** |
| **−** | `PASSIVE_HUMAN_LEVY_TAX` | **−250g** к старту (250 вместо 500) |

### Жуки

```entity
id: PASSIVE_BUG_FRENZY
effect: +10% attack_speed, +10% move_speed
applies_to: units
mvp: true

id: PASSIVE_BUG_BROOD_SURGE
effect: +10% barracks spawn speed
formula: wave_interval /= 1.10
applies_to: all_barracks
mvp: true

id: PASSIVE_BUG_GLASS_CHITIN
effect: -10% max_hp
applies_to: units
formula: max_hp *= 0.90
mvp: true
```

| | Пассив | Эффект |
|---|--------|--------|
| **+** | `PASSIVE_BUG_FRENZY` | **+10% скорость атаки** и **бега** юнитов |
| **+** | `PASSIVE_BUG_BROOD_SURGE` | **+10% скорость spawn** в barracks |
| **−** | `PASSIVE_BUG_GLASS_CHITIN` | **−10% HP** юнитов |

## Magic — заклинания магов (confirmed)

Открываются **UPG_MAIN_MAGIC** в main (slot 1/2/3 = main level 1/2/3). Кастуют **UNIT_TYPE_CASTER** автоматически (см. `AI.md`).

### Люди

```entity
id: SPELL_HUMAN_1
name: Лечение
unlock: UPG_MAIN_MAGIC slot_1
target: single_ally_unit
effect: restore_hp
amount: 80
cast_range: 6.0
cooldown: 10.0
priority: lowest_hp_ally_in_range
mvp: true

id: SPELL_HUMAN_2
name: Ледяной взрыв
unlock: UPG_MAIN_MAGIC slot_2
target: ground_aoe
effect: magic_damage
damages: all_enemy_units_in_radius
radius: 5.0
damage: 40
cast_range: 6.0
cooldown: 14.0
priority: densest_enemy_cluster
mvp: true

id: SPELL_HUMAN_3
name: Воскрешение
unlock: UPG_MAIN_MAGIC slot_3
target: single_ally_corpse
effect: resurrect
restores: full_hp
cast_range: 6.0
corpse_max_age: 20.0
cooldown: 30.0
priority: highest_value_recent_corpse
mvp: true
```

| Slot | ID | Эффект | Числа |
|------|-----|--------|-------|
| 1 | `SPELL_HUMAN_1` | **Хил** 1 союзника | **80 HP**, range **6**, CD **10s** |
| 2 | `SPELL_HUMAN_2` | **Ледяной взрыв** | radius **5**, **40** dmg, CD **14s** |
| 3 | `SPELL_HUMAN_3` | **Воскрешение** | corpse **≤20s**, CD **30s** |

### Жуки

```entity
id: SPELL_BUG_1
name: Заражение
unlock: UPG_MAIN_MAGIC slot_1
target: single_enemy_unit
effect: on_death_spawn
spawn_unit: UNIT_BUG_MELEE
spawn_owner: caster_owner
debuff: infected_until_death
cast_range: 6.0
cooldown: 12.0
priority: nearest_enemy_in_range
mvp: true

id: SPELL_BUG_2
name: Яйцо
unlock: UPG_MAIN_MAGIC slot_2
target: ground_point
effect: spawn_egg
hatch_delay: 30.0
hatch_unit: UNIT_BUG_MELEE
egg: stationary
egg_hp: 120
egg_destroyable: true
hatch_on: timer_or_zero_hp   # 0 HP → no hatch
cast_range: 6.0
cooldown: 18.0
priority: forward_lane_point
mvp: true

id: SPELL_BUG_3
name: Мутация
unlock: UPG_MAIN_MAGIC slot_3
target: single_ally_bug_nearby
effect: mutate
stat_bonus: +10% max_hp, +10% damage
visual_scale: increased
duration: until_death
cast_range: 6.0
cooldown: 16.0
priority: nearest_unmutated_ally
mvp: true
```

| Slot | ID | Эффект | Числа |
|------|-----|--------|-------|
| 1 | `SPELL_BUG_1` | **Заражение** → жук при смерти | CD **12s** |
| 2 | `SPELL_BUG_2` | **Яйцо** → melee через **30s** | **120 HP**, уничтожимо; CD **18s** |
| 3 | `SPELL_BUG_3` | **Мутация** +10% HP/урон | CD **16s** |

> **Яйцо:** стоит на месте; через **30s** → `UNIT_BUG_MELEE`. Если **HP = 0** раньше — вылупления **нет**. Юнит из яйца / заражения: **`UNIT_BUG_MELEE`** baseline (playtest может сменить).

## Tower upgrades — прокачка в башне (confirmed)

**5 способностей** per race; каждая **levels 1–3**. Исследование в **живой** `BUILDING_TOWER` (**4 башни** — своя очередь). Эффект **race-wide**, прогресс трека **общий**.

**Стоимость / время** (одинаково для всех треков и рас, за каждый level):

| Level | Gold | Research time |
|-------|------|---------------|
| **L1** | **500** | **45 s** |
| **L2** | **800** | **90 s** |
| **L3** | **1200** | **135 s** |

```entity
id: UPG_TOWER_TRACK_RULES
tracks_per_race: 5
max_level_per_track: 3
towers_per_base: 4
tower_positions: [NW, NE, SW, SE]
research_building: BUILDING_TOWER
requires: tower alive
scope: race_wide
queue_per_tower: 1
parallel: up_to_4_towers_different_tracks
costs_gold: [500, 800, 1200]
research_time_sec: [45, 90, 135]
mvp: true
```

```entity
id: UPG_TOWER_LEVEL_ECONOMY
costs_gold: [500, 800, 1200]    # L1, L2, L3 per track
research_time_sec: [45, 90, 135]
scope: all_race_tower_tracks
mvp: true
```

### Люди — 5 способностей

| # | ID | Тип | L1 | L2 | L3 |
|---|-----|-----|----|----|-----|
| 1 | `UPG_TOWER_HUMAN_STEEL_TEMPER` | stat | **+3%** урон юнитам | **+6%** | **+10%** |
| 2 | `UPG_TOWER_HUMAN_HOLD_THE_LINE` | stat | **+5%** защита юнитам | **+10%** | **+15%** |
| 3 | `UPG_TOWER_HUMAN_BALLISTA_OVERDRAW` | tower | **+15%** урон **всех башен** | **+25%** | **+35%** |
| 4 | `UPG_TOWER_HUMAN_ARCANE_RELAY` | spell | **−10%** CD заклинаний магов | **−15%** | **−20%** |
| 5 | `UPG_TOWER_HUMAN_LAST_STAND` | ability | Юниты **<30% HP:** **+10%** защита | **+15%** | **+20%** |

```entity
id: UPG_TOWER_HUMAN_STEEL_TEMPER
max_level: 3
effect: unit_damage_percent
values: [3, 6, 10]
scope: race_wide

id: UPG_TOWER_HUMAN_HOLD_THE_LINE
max_level: 3
effect: unit_defense_percent
values: [5, 10, 15]
scope: race_wide

id: UPG_TOWER_HUMAN_BALLISTA_OVERDRAW
max_level: 3
effect: tower_damage_percent
values: [15, 25, 35]
applies_to: all_BUILDING_TOWER

id: UPG_TOWER_HUMAN_ARCANE_RELAY
max_level: 3
effect: caster_spell_cooldown_reduction
values: [0.10, 0.15, 0.20]
applies_to: [SPELL_HUMAN_1, SPELL_HUMAN_2, SPELL_HUMAN_3]

id: UPG_TOWER_HUMAN_LAST_STAND
max_level: 3
effect: low_hp_defense_bonus
hp_threshold: 0.30
values: [10, 15, 20]
scope: race_wide
mvp: true
```

### Жуки — 5 способностей

| # | ID | Тип | L1 | L2 | L3 |
|---|-----|-----|----|----|-----|
| 1 | `UPG_TOWER_BUG_ADRENAL_GLAND` | stat | **+4%** attack speed | **+8%** | **+12%** |
| 2 | `UPG_TOWER_BUG_CARAPACE_WEAVE` | stat | **+4%** HP юнитов | **+8%** | **+12%** |
| 3 | `UPG_TOWER_BUG_NEUROTOXIN` | tower | Выстрел башни: **−8%** move, 2s | **−12%** | **−16%** |
| 4 | `UPG_TOWER_BUG_HATCHERY_PULSE` | spell | Яйцо: **27s** hatch; infect **+10%** spawn stats | **24s**; **+20%** | **21s**; **+30%** |
| 5 | `UPG_TOWER_BUG_ACID_SAC` | stat | **+10%** урон по **зданиям** | **+20%** | **+30%** |

```entity
id: UPG_TOWER_BUG_ADRENAL_GLAND
max_level: 3
effect: unit_attack_speed_percent
values: [4, 8, 12]
scope: race_wide

id: UPG_TOWER_BUG_CARAPACE_WEAVE
max_level: 3
effect: unit_max_hp_percent
values: [4, 8, 12]
scope: race_wide

id: UPG_TOWER_BUG_NEUROTOXIN
max_level: 3
effect: tower_on_hit_slow
move_speed_reduction: [0.08, 0.12, 0.16]
duration_sec: 2.0
applies_to: all_BUILDING_TOWER

id: UPG_TOWER_BUG_HATCHERY_PULSE
max_level: 3
effect: spell_bug_egg_hatch_seconds
values: [27, 24, 21]
bonus: infected_spawn_stat_percent
bonus_values: [10, 20, 30]
applies_to: [SPELL_BUG_1, SPELL_BUG_2]

id: UPG_TOWER_BUG_ACID_SAC
max_level: 3
effect: building_damage_percent
values: [10, 20, 30]
applies_to: all_unit_types_vs_BUILDING
scope: race_wide
mvp: true
```

> **4 башни** — до **4 параллельных** исследований (разные треки). Всего **5** треков → нужен выбор, что качать первым.

## Roster — старт (2 расы)

```entity
id: RACE_HUMAN
display_name: Люди
display_name_en: Humans
fantasy_hook: Сталь, дисциплина, классическая оборона базы
start_passives:
  positive: [PASSIVE_HUMAN_STEEL_ARMS, PASSIVE_HUMAN_FORTIFIED_LINE]
  negative: PASSIVE_HUMAN_LEVY_TAX
start_gold: 250
mvp: true
units:
  melee: UNIT_HUMAN_MELEE
  ranged: UNIT_HUMAN_RANGED
  caster: UNIT_HUMAN_CASTER
  siege: UNIT_HUMAN_SIEGE
  flying: UNIT_HUMAN_FLYING
  super: UNIT_HUMAN_SUPER
heroes: [HERO_HUMAN_1, HERO_HUMAN_2, HERO_HUMAN_3]
buildings: BUILDING_SET_HUMAN
upgrades: UPGRADE_TREE_HUMAN
tower_tracks: [UPG_TOWER_HUMAN_STEEL_TEMPER, UPG_TOWER_HUMAN_HOLD_THE_LINE, UPG_TOWER_HUMAN_BALLISTA_OVERDRAW, UPG_TOWER_HUMAN_ARCANE_RELAY, UPG_TOWER_HUMAN_LAST_STAND]
magic_spells: [SPELL_HUMAN_1, SPELL_HUMAN_2, SPELL_HUMAN_3]

id: RACE_BUG
display_name: Жуки
display_name_en: Bugs
fantasy_hook: Рой, число, биомасса и осадные кислоты
start_passives:
  positive: [PASSIVE_BUG_FRENZY, PASSIVE_BUG_BROOD_SURGE]
  negative: PASSIVE_BUG_GLASS_CHITIN
mvp: true
units:
  melee: UNIT_BUG_MELEE
  ranged: UNIT_BUG_RANGED
  caster: UNIT_BUG_CASTER
  siege: UNIT_BUG_SIEGE
  flying: UNIT_BUG_FLYING
  super: UNIT_BUG_SUPER
heroes: [HERO_BUG_1, HERO_BUG_2, HERO_BUG_3]
buildings: BUILDING_SET_BUG
upgrades: UPGRADE_TREE_BUG
tower_tracks: [UPG_TOWER_BUG_ADRENAL_GLAND, UPG_TOWER_BUG_CARAPACE_WEAVE, UPG_TOWER_BUG_NEUROTOXIN, UPG_TOWER_BUG_HATCHERY_PULSE, UPG_TOWER_BUG_ACID_SAC]
magic_spells: [SPELL_BUG_1, SPELL_BUG_2, SPELL_BUG_3]
```

## Будущие расы (слоты)

```entity
id: RACE_SLOT_3
display_name: TBD
mvp: false
note: Полный kit: 2+/1− passives, tower upgrades, 3 magic spells

id: RACE_SLOT_4
display_name: TBD
mvp: false
```

## Контент-пайплайн новой расы

1. `entity` блок в этом файле (`id`, лор, `fantasy_hook`)
2. **Start passives:** 2 positive + 1 negative (`PassiveDefinition` ×3)
3. `UnitDefinition` ×6 + `HeroDefinition` ×3
4. **Caster spells** ×3 (привязка к `UPG_MAIN_MAGIC` slots)
5. **Tower upgrade tracks** — race-unique (`UpgradeDefinition` в башне)
6. `BuildingSet` (скин базы)
7. `UpgradeTree` — stat + tower + magic
8. Prefabs + lobby portrait

**Критерий готовности расы:** отличима визуально **и** по passives / tower / magic без чтения лора.

## Race select UI

- MVP: **2** активных портрета (Люди / Жуки)
- Показ **passives** (+2/−1) в tooltip при выборе
- Будущие: `Coming Soon` или скрыты до релиза контента
- `RaceCatalog` ScriptableObject

## Locked decisions

| Решение | Значение |
|---------|----------|
| Стартовый roster | **2 расы:** Люди + Жуки |
| Стартовые пассивы | **2+ / 1−** per race; уникальные |
| Tower upgrades | **5 tracks × L1–3**; **4 башни**; race-wide |
| Tower upgrade economy | **500/800/1200g**; **45/90/135s** per level |
| Base layout | **8 зданий**; **3 lane**; тыл **к краю карты**, перед **к центру** |
| Маги | **Casters** в волне; **уникальные заклинания** per race |
| Magic (main) | **1 / 2 / 3** слота = main level; **800/1500/2500g**; **60/90/135s** |
| Squad structure | **Одинакова** по составу — `SQUAD_BARRACKS_L1..L4` |
| MVP asymmetry Human/Bug | **Passives + magic + tower tracks** |

```entity
id: UPG_MAIN_MAGIC_ECONOMY
costs_gold: [800, 1500, 2500]    # slot 1, 2, 3 (unlock spell)
research_time_sec: [60, 90, 135]
requires_main_level: [1, 2, 3]
mvp: true
```

## Open

- [x] Passives Human / Bug (+2/−1)
- [x] Magic spells Human / Bug (×3 each)
- [x] Tower tracks Human / Bug (**×5**, L1–3)
- [x] Gold/time за **tower** upgrades — **500/800/1200g**, **45/90/135s**
- [x] Gold/time за **magic** upgrades — **800/1500/2500g**, **60/90/135s**
- [x] Числа заклинаний (heal, frost, CD, egg HP, resurrect window)
