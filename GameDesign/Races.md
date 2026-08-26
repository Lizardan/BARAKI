---
doc_id: races
version: 1.0
status: draft
depends_on: [units, buildings, upgrades]
provides: [race_definitions, original_factions, content_pipeline, roster, race_asymmetry]
---

# Races

## Принцип

Все фракции — **оригинальные**: свой лор, визуал, **асимметричные** бонусы и апгрейды.

Архитектура **data-driven**: новая раса = assets + SO + запись здесь, без правок combat core.

## Асимметрия рас (G1 — зафиксировано)

Каждая раса отличается **четырьмя осями**:

| Ось | Описание |
|-----|----------|
| **Стартовые пассивы** | **2 положительных** + **1 отрицательный** с начала матча |
| **Tower upgrades** | Уникальные **9** треков **в башнях** → пассивы/умения **только юнитам** (не герои/титан) |
| **Маги (casters)** | В отряде; **уникальные заклинания** per race |
| **Magic upgrades (main)** | Прокачка **в главном здании** → открывает/усиливает заклинания магов |
| **Match bonus** | **12 слотов** per race (10 replacement + 2 unique); см. `Bonuses.md` |

```entity
id: RACE_START_PASSIVES
positive_count: 2
negative_count: 1
scope: race_wide
apply: match_start
examples_human: PASSIVE_HUMAN_* (см. ниже)
mvp: true
```

```entity
id: RACE_TOWER_UPGRADES
location: BUILDING_TOWER   # alive only
scope: race_unique
effect: unit_passive_or_extra_ability   # units only; not heroes/titan/tower DPS
tracks_per_race: 9
max_level_per_track: 3
level_gate: sequential
ui_command_slots: [4, 5, 6, 7, 8, 9, 10, 11, 12]
costs_gold: [500, 800, 1200]       # per track level L1, L2, L3
research_time_sec: [45, 90, 135]
mvp: false
note: PRE-007 done; Human kit = FLAMING_ARROWS…LAST_STAND (см. ниже). Исключение: UPG_TOWER_HUMAN_FLAMING_ARROWS распространяется и на выстрелы живых башен владельца (решение пользователя 2026-08-26)
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
| **MVP / старт** | **1** | `RACE_HUMAN` — passives, magic; blessing abilities = PRE-005; bonuses = PRE-006; tower ×9 = PRE-007 |
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

## Стартовые пассивы — Human (confirmed)

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
formula: start_gold = 250   # playtest baseline
mvp: true
```

| | Пассив | Эффект |
|---|--------|--------|
| **+** | `PASSIVE_HUMAN_STEEL_ARMS` | **+10% урон** юнитов и **башен** |
| **+** | `PASSIVE_HUMAN_FORTIFIED_LINE` | **+10% защита** юнитов и **зданий** |
| **−** | `PASSIVE_HUMAN_LEVY_TAX` | **−250g** к старту (250) |

## Magic — заклинания магов (confirmed)

Открываются **UPG_MAIN_MAGIC** в main (slot 1/2/3 = main level 1/2/3). Кастуют **UNIT_TYPE_CASTER** автоматически (см. `AI.md`).

> Каждый слот также даёт магу **+3 dmg** к автоатаке (flat, `MagicDamagePerLevel`). Отдельного стат-трека для мага **нет**.

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

## Tower upgrades — прокачка в башне

**9 треков** per race; каждая **levels 1–3** (L2 только после L1, L3 после L2). Исследование в **живой** `BUILDING_TOWER` (**4 башни** — своя очередь). Эффект **race-wide** на **юнитов**; прогресс трека **общий**. Старый kit из 5 треков (Steel Temper … Last Stand) — **scrap**.

**Цели эффектов:** только **юниты** (роли melee/ranged/caster/siege/flying/super). **Не** герои, **не** титан, **не** урон/статы самих башен. Типы: (а) пассив на тип(ы) юнитов; (б) доп. умение юниту (пример: siege даёт бафф союзникам). Сила растёт по L1–L3.

**Исключение (Люди):** `UPG_TOWER_HUMAN_FLAMING_ARROWS` действует и на выстрелы живых `BUILDING_TOWER` владельца — горящие стрелы + поджог цели.

**UI (12 command slots):** слоты **1–3** пустые (заглушку «Апгрейд» на слоте 1 убрать); **9** треков на **слотах 4–12** (1-based = `CommandSlot3`…`CommandSlot11`).

**Стоимость / время** (одинаково для всех треков и рас, за каждый level):

| Level | Gold | Research time |
|-------|------|---------------|
| **L1** | **500** | **45 s** |
| **L2** | **800** | **90 s** |
| **L3** | **1200** | **135 s** |

```entity
id: UPG_TOWER_TRACK_RULES
tracks_per_race: 9
max_level_per_track: 3
level_gate: sequential          # L2 requires L1 of same track; L3 requires L2
towers_per_base: 4
tower_positions: [NW, NE, SW, SE]
research_building: BUILDING_TOWER
requires: tower alive
scope: race_wide
applies_to: units_only          # not heroes, not titan, not tower DPS
queue_per_tower: 1
parallel: up_to_4_towers_different_tracks
ui_command_slots: [4, 5, 6, 7, 8, 9, 10, 11, 12]   # 1-based; slots 1–3 empty
costs_gold: [500, 800, 1200]
research_time_sec: [45, 90, 135]
mvp: false
note: PRE-007 done; Human kit — см. «Люди — 9 треков»
```

```entity
id: UPG_TOWER_LEVEL_ECONOMY
costs_gold: [500, 800, 1200]    # L1, L2, L3 per track
research_time_sec: [45, 90, 135]
scope: all_race_tower_tracks
mvp: true
```

### Люди — 9 треков (PRE-007)

Имена — английские, описания — русские. Все эффекты **только на юниты** (не герои, не титан); исключение — Flaming Arrows (см. выше). Стакается с бонусами PRE-006.

| # | Track id | Имя | Роли | L1 / L2 / L3 |
|---|----------|-----|------|--------------|
| 1 | `UPG_TOWER_HUMAN_FLAMING_ARROWS` | Flaming Arrows | Ranged + Flying + башни | Стрелы поджигают: 2/4/6 dmg/с горения в течение 2 с; снаряды этих ролей и башен получают огненный визуал |
| 2 | `UPG_TOWER_HUMAN_BULWARK` | Bulwark | Melee + Siege | +1/+2/+3 брони; на L3 ещё блок: −20% урона в ближнем бою |
| 3 | `UPG_TOWER_HUMAN_BLOODRAGE` | Bloodrage | Melee + Flying | После убийства +15%/+25%/+40% скорости атаки на 3 с |
| 4 | `UPG_TOWER_HUMAN_BATTERING_RAMS` | Battering Rams | Siege + Super | +25%/+50%/+75% урона по зданиям; на L3 ещё +1 радиус splash |
| 5 | `UPG_TOWER_HUMAN_ARCANE_FOCUS` | Arcane Focus | Caster | Кулдауны умений кастера ×0.88/×0.76/×0.64 |
| 6 | `UPG_TOWER_HUMAN_SKIRMISHERS` | Skirmishers | Ranged + Caster | +0.5/+1.0/+1.5 дальности атаки |
| 7 | `UPG_TOWER_HUMAN_FORCED_MARCH` | Forced March | Melee + Siege + Caster | +8%/+16%/+24% скорости движения |
| 8 | `UPG_TOWER_HUMAN_FIELD_MEDICS` | Field Medics | все юниты | +1/+2/+3 HP/с регенерации |
| 9 | `UPG_TOWER_HUMAN_LAST_STAND` | Last Stand | все юниты | При HP < 30%: +20%/+30%/+40% урона |

Авоспособности без маны: поджог (#1, on-hit), Bloodrage (#3, по условию «убийство»), Last Stand (#9, по условию «мало HP»), Bulwark L3 (блок).

> **4 башни** — до **4 параллельных** исследований (разные треки; очередь 1 на башню). **9** треков → выбор, что качать за матч.

## Roster — старт (1 раса)

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
bonus_slots: 12               # BONUS_SLOT_*; 2 unique TBD per race
buildings: BUILDING_SET_HUMAN
upgrades: UPGRADE_TREE_HUMAN
tower_tracks:                     # PRE-007; порядок = слоты 4–12 UI башни
  - UPG_TOWER_HUMAN_FLAMING_ARROWS    # Ranged+Flying+башни: поджог on-hit
  - UPG_TOWER_HUMAN_BULWARK           # Melee+Siege: броня; L3 блок melee
  - UPG_TOWER_HUMAN_BLOODRAGE         # Melee+Flying: +AS после убийства
  - UPG_TOWER_HUMAN_BATTERING_RAMS    # Siege+Super: урон по зданиям; L3 splash+
  - UPG_TOWER_HUMAN_ARCANE_FOCUS      # Caster: кулдауны ×0.88/0.76/0.64
  - UPG_TOWER_HUMAN_SKIRMISHERS       # Ranged+Caster: дальность атаки
  - UPG_TOWER_HUMAN_FORCED_MARCH      # Melee+Siege+Caster: скорость движения
  - UPG_TOWER_HUMAN_FIELD_MEDICS      # все юниты: HP/с регенерация
  - UPG_TOWER_HUMAN_LAST_STAND        # все юниты: урон при HP<30%
magic_spells: [SPELL_HUMAN_1, SPELL_HUMAN_2, SPELL_HUMAN_3]
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

- MVP: **1** раса в контенте (Люди); **playtest pick gate:** только Люди (`SelectableRaceIds`), будущие расы скрыты
- Показ **passives** (+2/−1) в tooltip при выборе
- Будущие: `Coming Soon` или скрыты до релиза контента
- `RaceCatalog` ScriptableObject

## Locked decisions

| Решение | Значение |
|---------|----------|
| Стартовый roster | **1 раса:** Люди (+2 слота TBD) |
| Стартовые пассивы | **2+ / 1−** per race; уникальные |
| Tower upgrades | **9 tracks × L1–3**; sequential levels; **4 башни**; race-wide; **только юниты**; UI слоты **4–12** |
| Tower upgrade economy | **500/800/1200g**; **45/90/135s** per level |
| Base layout | **8 зданий**; **3 lane**; тыл **к краю карты**, перед **к центру** |
| Маги | **Casters** в волне; **уникальные заклинания** per race |
| Magic (main) | **1 / 2 / 3** слота = main level; **500/750/1000g**; **60/90/135s** |
| Match bonus | **12** слотов per race; см. `Bonuses.md` |
| Squad structure | **Одинакова** по составу — `SQUAD_BARRACKS_L1..L4` |
| MVP asymmetry | **Passives + magic + tower tracks** (1 раса; асимметрия — с добавлением рас) |

```entity
id: UPG_MAIN_MAGIC_ECONOMY
costs_gold: [500, 750, 1000]    # slot 1, 2, 3 (unlock spell)
research_time_sec: [60, 90, 135]
requires_main_level: [1, 2, 3]
mvp: true
```

## Open

- [x] Passives Human (+2/−1)
- [x] Magic spells Human (×3)
- [x] Tower tracks Human (**×9**, L1–3) — invent + implement = **PRE-007** (2026-08-26); старые ×5 scrap удалены
- [x] Gold/time за **tower** upgrades — **500/800/1200g**, **45/90/135s**
- [x] Gold/time за **magic** upgrades — **500/750/1000g**, **60/90/135s**
- [x] Числа заклинаний (heal, frost, CD, egg HP, resurrect window)
- [ ] Раса #2 — только после PRE-001..007 + GATE (`TODO.md`)
