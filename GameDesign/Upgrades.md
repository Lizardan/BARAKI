---
doc_id: upgrades
version: 0.6
status: draft
depends_on: [units, buildings, economy]
provides: [upgrade_tree, research_rules, mvp_upgrades, barracks_level_upgrade, main_building_gates]
---

# Upgrades

## Типы прокачки

| Тип | Scope | Max / gate |
|-----|-------|------------|
| **Main building level** | База | **3** |
| **Main passive gold** | База | **9** (cap = main × 3) |
| **Main magic** | База, **race-unique** | **= main level** (1 / 2 / 3) |
| **Unit stat research** | Global для расы | **9** per track (cap = main × 3); UI в **main** |
| **Divine Blessing** | База | 1× per match (research) |
| **Main extra ability** | База | 1× pick (после blessing) |
| **Barracks level** | Per-barracks | **4** |
| **Tower upgrades** | Per tower (alive), **race-unique** | **5 tracks × L3**; **500/800/1200g**; **45/90/135s** |

## Main building level

```entity
id: UPG_MAIN_BUILDING_LEVEL
building: BUILDING_MAIN
start_level: 1
max_level: 3
costs_gold: [2000, 3000]    # L1→2, L2→3
research_time_sec: [120, 180]
player_action: at BUILDING_MAIN
mvp: true
```

### Что открывает level главного здания

| Main level | Max stat level | Max heroes | Max **magic** upgrades |
|------------|----------------|------------|-------------------------|
| **1** | **3** | **1** | **1** |
| **2** | **6** | **2** | **2** |
| **3** | **9** | **3** | **3** |

```entity
id: MAIN_BUILDING_GATES
formula: max_stat_level = main_level * 3
formula: max_heroes_hired = main_level
formula: max_magic_upgrades = main_level
absolute_stat_cap: 9
mvp: true
```

## Barracks level upgrade

```entity
id: UPG_BARRACKS_LEVEL
scope: per_barracks_instance
requires: barracks alive (not destroyed)
costs_gold: [1000, 1500, 2500]   # L1→2, L2→3, L3→4
research_time_sec: [3, 3, 3]     # код: MatchEconomyRules
effects:
  - barracks_level++ (max 4)
  - spawn_speed +5%
  - unlock SQUAD_BARRACKS_L{barracks_level}
mvp: true
```

> В **живом** barracks: только **level**, **manual call**, **deploy** героя/титана. Stat research — в **main**.

## Unit stat research (global, levels 1–9)

Исследуются через **BUILDING_MAIN** UI; действуют **на всю расу**.  
Каждый track: **max 9 уровней**; фактический cap = `main_level × 3`.

```entity
id: RESEARCH_RULES_MVP
scope: race_wide
research_building: BUILDING_MAIN
queue_per_building: 1
cancel_refund: 1.0
mvp: true
```

### Stat tracks (confirmed)

| Track id | Эффект per level | Max level |
|----------|------------------|-----------|
| `UPG_MELEE_DMG` | **+3%** melee damage | 9 |
| `UPG_RANGED_DMG` | **+3%** ranged damage | 9 |
| `UPG_ARMOR` | **+3%** armor / damage reduction | 9 |

```entity
id: UPG_STAT_LEVEL_EFFECTS
UPG_MELEE_DMG: +3% per level
UPG_RANGED_DMG: +3% per level
UPG_ARMOR: +3% per level
scope: race_wide
mvp: true
```

```entity
id: UPG_STAT_LEVEL_ECONOMY
UPG_MELEE_DMG:
  costs_gold: [75, 100, 125, 150, 175, 200, 225, 250, 275]
  research_time_sec: [8, 10, 12, 14, 16, 18, 20, 22, 24]
UPG_RANGED_DMG:
  costs_gold: [75, 100, 125, 150, 175, 200, 225, 250, 275]
  research_time_sec: [8, 10, 12, 14, 16, 18, 20, 22, 24]
UPG_ARMOR:
  costs_gold: [60, 80, 100, 120, 140, 160, 180, 200, 220]
  research_time_sec: [6, 8, 10, 12, 14, 16, 18, 20, 22]
mvp: true
```

### Main building — passive gold (confirmed)

| Track id | Эффект | Max level |
|----------|--------|-----------|
| `UPG_MAIN_PASSIVE_GOLD` | **+25g/30s** за level (без прокачки **+0**) | 9 |

Cap по main level: **×3** (L1→3, L2→6, L3→9).

```entity
id: UPG_MAIN_PASSIVE_GOLD_ECONOMY
cost_gold: 200              # flat per level (levels 1..9)
research_time_sec: 25
effect_per_level: +25g per 30s tick
mvp: true
```

## Divine Blessing + main extra ability

```entity
id: UPG_MAIN_DIVINE_BLESSING
building: BUILDING_MAIN
requires_main_level: 2
cost_gold: tbd
research_time_sec: tbd
on_complete:
  - fog_of_war_self: disabled    # видимость всей карты для владельца
  - replaces_button: main_extra_ability_menu
mvp: false
note: PRE-003; FoW — MatchFogOfWar
```

После исследования кнопка благословения **заменяется** на меню **одной** доп. способности main на весь матч. Меню можно **закрыть и открыть снова** — ждать грейды, пока откроются более сильные варианты. Выбрал одну — **навсегда**, сменить нельзя.

### Гейты способностей main (blessing menu)

Уровни треков в **главном здании**:

- `UPG_MELEE_DMG`
- `UPG_RANGED_DMG`
- `UPG_ARMOR`
- `UPG_MAIN_PASSIVE_GOLD`
- `UPG_MAIN_MAGIC`

Башенные апгрейды **не** входят (отложено).

```entity
id: MAIN_EXTRA_ABILITY_PICK
max_picks: 1
persist: whole_match
deferred_choice: true          # можно закрыть меню и вернуться
ability_list: tbd
example: tbd                   # напр. выстрел по вражескому зданию при stats 6 + magic 2
mvp: false
```

## Main magic upgrades (race-unique spells)

Прокачка **в** `BUILDING_MAIN`. Каждый слот открывает/усиливает **уникальное заклинание магов** (`UNIT_TYPE_CASTER`) данной расы.

```entity
id: UPG_MAIN_MAGIC
building: BUILDING_MAIN
scope: race_unique
max_purchased: main_level    # L1→1, L2→2, L3→3 total
slots:
  slot_1: requires main_level >= 1
  slot_2: requires main_level >= 2
  slot_3: requires main_level >= 3
effect: unlock_caster_spell   # one purchase = unlock slot spell
costs_gold: [800, 1500, 2500]    # slot 1, 2, 3
research_time_sec: [60, 90, 135]
requires_main_level: [1, 2, 3]
mvp: true
```

> Заклинания кастуют **маги** в бою. Список: **`Races.md` § Magic**.  
> Каждый слот дополнительно даёт магу **+3 dmg** к автоатаке (flat, `MagicDamagePerLevel`). Отдельного стат-трека для мага **нет** — прокачка мага = только `UPG_MAIN_MAGIC`.

| Main magic slot | Human |
|-----------------|-------|
| 1 (main L1) | `SPELL_HUMAN_1` Heal |
| 2 (main L2) | `SPELL_HUMAN_2` Frost AoE |
| 3 (main L3) | `SPELL_HUMAN_3` Resurrect |

## Tower upgrades (race-unique)

**5 треков** per race; **max level 3**; **4 башни** на базе. Эффект **race-wide**. См. **`Races.md` § Tower upgrades**.

```entity
id: UPG_TOWER_RACE
building: BUILDING_TOWER
requires: tower alive (not ruins)
scope: race_unique
tracks_per_race: 5
max_level_per_track: 3
towers_per_base: 4
queue_per_tower: 1
parallel: up_to_4_different_tracks
costs_gold: [500, 800, 1200]    # L1, L2, L3 per track level
research_time_sec: [45, 90, 135]
mvp: true
```

| Race | 5 tracks (L1→L3) |
|------|-------------------|
| Human | Steel Temper, Hold the Line, Ballista Overdraw, Arcane Relay, Last Stand |

## UI

- **Main:** upgrade main level, passive gold, stat tracks, **magic**, hire heroes; полоска титана над зданием (main L3 + 3 героя на базе, не кнопка), Divine Blessing / extra ability menu
- **Barracks (alive):** barracks level + manual call + deploy hero (1000g) / deploy titan (2500g, после появления на базе)
- **Tower (alive):** target mode + **race tower upgrades**

## Locked decisions (confirmed)

| Решение | Значение |
|---------|----------|
| Stat upgrades | **Global для расы**; UI в **main**; **9** max per track |
| Stat cap | Main L1 → **3**, L2 → **6**, L3 → **9** per track |
| Main upgrade cost | **2000** (→2), **3000** (→3); time **120 / 180 s** |
| Hero hire cap | **= main level** (1 / 2 / 3 героя); hire research **25 s** |
| Magic unlock | **800 / 1500 / 2500g**; **60 / 90 / 135 s**; gate = main level |
| Divine Blessing | Main **L2+**; FoW off; → меню 1 extra ability; PRE-003 |
| Tower upgrades | **5 tracks × 3 levels**; **4 towers**; race-wide; **500/800/1200g**; **45/90/135s** |
| Barracks level | Per-barracks, max 4; costs **1000/1500/2500**; time **3/3/3 s** |
| Stat upgrades | **+3%** dmg/armor per level; costs см. `UPG_STAT_LEVEL_ECONOMY` |
| Passive gold | **200g**, **25s** per level; **+25g/30s** per level |

## Open

- [x] Gold/time за **tower** upgrades
- [x] Gold/time за **magic** upgrades (main)
- [x] Gold/time и эффект **stat** upgrades
- [x] Gold/time **passive gold** и **main/barracks** research
- [ ] Gold/time **Divine Blessing** (TBD)
- [ ] Список **main extra abilities** после blessing (TBD)
