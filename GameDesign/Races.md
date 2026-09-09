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

### Древние (FACELESS-008)

```entity
id: PASSIVE_FACELESS_ABYSSAL_HUNGER
effect: lifesteal 10% of damage dealt
applies_to: [units]                 # не герои, не титан, не здания, не башни
scope: race_wide
apply: match_start
mvp: false

id: PASSIVE_FACELESS_RELENTLESS_TIDE
effect: +10% attack speed
applies_to: [units]
scope: race_wide
apply: match_start
mvp: false

id: PASSIVE_FACELESS_BLEAK_FOUNDATIONS
effect: -10% max HP
applies_to: [buildings]             # все здания, включая башни; как Stone Masonry, но ×0.9
scope: race_wide
apply: match_start_and_on_building_level_change
mvp: false
```

| | Пассив | Эффект |
|---|--------|--------|
| **+** | `PASSIVE_FACELESS_ABYSSAL_HUNGER` | **Вампиризм 10%**: юниты лечатся на 10% от нанесённого урона (после брони цели) |
| **+** | `PASSIVE_FACELESS_RELENTLESS_TIDE` | **+10% скорость атаки** юнитам |
| **−** | `PASSIVE_FACELESS_BLEAK_FOUNDATIONS` | **−10% max HP** всем **зданиям** (ретро + новые, по уровням) |

**Идентичность:** Люди — **сырые статы и оборона** (урон/защита, штраф по золоту).
Древние — **темп и самоподпитка** (вампиризм + скорость атаки) ценой **хрупкой базы**.
Ось урона/защиты Древние не трогают — отличие другого рода, не «зеркало» Human-пассивов.

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

### Древние (FACELESS-008)

Открываются тем же `UPG_MAIN_MAGIC` (slot 1/2/3 = main level 1/2/3); каждый слот — **+3 dmg**
к автоатаке кастера, как у Людей. Экономика магии **общая** (500/750/1000g, 60/90/135s).

```entity
id: SPELL_FACELESS_1
name: Гниющий взор / Blighting Gaze
unlock: UPG_MAIN_MAGIC slot_1
target: single_enemy_unit
effect: magic_damage + damage_over_time
damage: 30
dot: 4 dmg/s x 4 s
cast_range: 6.0
cooldown: 10.0
priority: highest_hp_enemy_in_range
mvp: false

id: SPELL_FACELESS_2
name: Вытягивание жизни / Void Drain
unlock: UPG_MAIN_MAGIC slot_2
target: ground_aoe
effect: magic_damage + lifesteal_to_caster
damage: 40
radius: 5.0
lifesteal: 30% of damage dealt
cast_range: 6.0
cooldown: 14.0
priority: densest_enemy_cluster
mvp: false

id: SPELL_FACELESS_3
name: Поднять павшего / Raise the Drowned
unlock: UPG_MAIN_MAGIC slot_3
target: single_corpse_any_side
effect: summon
summons: 1 minion (role Melee, stats x0.5 of UNIT_FACELESS_MELEE, prefab scale x0.67)
owner: caster owner
cast_range: 6.0
corpse_max_age: 15.0
cooldown: 30.0
priority: highest_value_recent_corpse
mvp: false
```

| Slot | ID | Эффект | Числа |
|------|-----|--------|-------|
| 1 | `SPELL_FACELESS_1` | **Урон + дот** по одной цели | **30** dmg + **4** dmg/с × **4 с**, range **6**, CD **10s** |
| 2 | `SPELL_FACELESS_2` | **AoE-урон + вампиризм кастеру** | radius **5**, **40** dmg, лечение **30%** урона, CD **14s** |
| 3 | `SPELL_FACELESS_3` | **Призыв из трупа** (любая сторона) | corpse **≤15s**, мини-меле ×0.5 статов / ×0.67 масштаб, CD **30s** |

**Асимметрия к Людям:** Люди **сохраняют своих** (хил, воскрешение союзника в полном HP).
Древние **питаются чужим** (дот, вытягивание жизни, подъём **любого** трупа — включая вражеский —
в подконтрольного мини-меле). Мини-меле — те же статы, что у бонуса `Call of the Abyss`
(слот 3, FACELESS-010): HP 60, dmg 4–5, броня 0.

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

### Древние — 9 треков (FACELESS-008)

Правила те же: **только юниты** (не герои, не титан, не DPS башен), **L1–L3** последовательно,
экономика **500/800/1200g**, **45/90/135s**, UI-слоты **4–12**. Порядок = порядок слотов.
Экономика tower/magic — **общая** с Людьми (см. `RACE_TOWER_UPGRADES`).

**Главное ограничение: трек не повторяет механику бонуса.** Бонусы Древних (FACELESS-010)
дают **вампиризм, дот on-hit, взрыв при смерти, on-kill бафы юнитов и уклонение** —
ничего из этого в треках нет. Проверка при изменении: новый эффект сверять с таблицей
`wiki/rules/faceless-unit-bonuses.md`.

> **Исключение для servant-осей (FACELESS-017, Plan0909 §5.6):** треки 5 и 7 используют
> servant-рост — спавн при смерти и пир на герое. Они не дублируют бонусы: слот 3 Call of
> the Abyss призывает servant **on-kill** (кастером), слот 4 Death Explosion — **урон** при
> смерти; трек 5 — вероятностный спавн servant на месте гибели любого юнита владельца,
> трек 7 — **бафф** servants при убийстве героя/титана. «Призыв мини-меле» для этих двух
> осей снят с запрета; для остальных треков и рас правило остаётся в силе.

| # | Track id | Имя | Роли | L1 / L2 / L3 |
|---|----------|-----|------|--------------|
| 1 | `UPG_TOWER_FACELESS_CHITINOUS_HIDE` | Chitinous Hide / Панцирь глубин | Melee + Super | **+1 / +2 / +3** брони; на L3 ещё **+15% max HP** при спавне |
| 2 | `UPG_TOWER_FACELESS_HOLLOW_BARBS` | Hollow Barbs / Полые жала | Ranged + Flying | Пробитие: атаки игнорируют **1 / 2 / 3** брони цели |
| 3 | `UPG_TOWER_FACELESS_VACUUM_COLLAPSE` | Vacuum Collapse / Вакуумное схлопывание | Melee + Flying | При смерти: враги в r=3 получают **−15% / −25% / −35%** скорости на 3 с |
| 4 | `UPG_TOWER_FACELESS_RITUAL_OF_THE_DEEP` | Ritual of the Deep / Ритуал глубин | Caster | Пока кастер жив: союзники в r=8 получают **−6% / −10% / −15%** урона от **юнитов и героев** |
| 5 | `UPG_TOWER_FACELESS_UNNERVING_AIM` | Swarm at Death Site / Рой на месте гибели | Ranged + Caster | При смерти обычного юнита владельца шанс **5% / 10% / 15%**: спавн **servant** на месте гибели (труп консьюмится; servant не цепляет каскад). Legacy-ид #5, механика заменена (FACELESS-017) |
| 6 | `UPG_TOWER_FACELESS_FRENZY_OF_THE_DEEP` | Frenzy of the Deep | Melee + Siege | **+10% / +15% / +20%** скорости атаки |
| 7 | `UPG_TOWER_FACELESS_SPLASH_OF_THE_DEEP` | Feast on Heroes / Пир на герое | Caster + Super | Servants владельца в **r=8** от места гибели героя/титана: **+30% урона и +30% max HP на 10 с**; декей ⅓ max HP за 10 с. Legacy-ид #7, механика заменена (FACELESS-017) |
| 8 | `UPG_TOWER_FACELESS_HOLLOW_BONES` | Hollow Bones | Siege + Flying | **+8% / +16% / +24%** скорости движения |
| 9 | `UPG_TOWER_FACELESS_VOID_HARDENING` | Void Hardening / Закалка пустотой | все юниты | **−10% / −15% / −20%** урона от **зданий и башен** |

**Покрытие ролей:** Melee (1, 3, 6) · Ranged (2, 5) · Caster (4, 5, 7) · Siege (6, 8) ·
Flying (2, 3, 8) · Super (1, 7) · все юниты (9). Пары ролей не повторяются.

**Разграничение срезов урона:** #4 режет урон от **юнитов и героев** (аура, пока кастер жив),
#9 — только от **зданий и башен** (пассивно). Это разные источники, стакаются.

**Асимметрия к Людям:** у Людей треки — **про статы и выживаемость** (броня, дальность, реген,
урон при низком HP, поджог). У Древних — **про контроль и servant-рост**: своя броня, пробитие
брони, облако замедления на месте смерти, защитная аура кастера, спавн servant при гибели,
пир на герое, стойкость к осаде. С бонусами слотов 1–12 (FACELESS-010) треки **не
пересекаются по механикам** (исключение для servant-осей см. выше).

**Новые механики #5/#7** (FACELESS-017, 2026-09-09): старый дебаф брони (#5 Unnerving Aim)
и сплеш (#7 Splash of the Deep) **заменены** на servant-рост — «Рой на месте гибели» и
«Пир на герое»; legacy-иды в `GameIds` не переименовываются, семантика живёт в
`FacelessTowerTrackRules`. Остальные треки — спавн-статы и пассивные множители.

Авоспособности без маны: Vacuum Collapse (#3, по смерти), Swarm at Death Site (#5, по смерти
случайный шанс), Feast on Heroes (#7, по убийству героя/титана). Остальное — спавн-статы и
пассивные множители.

### Servant (прислужник) — единый профиль (Plan0909 Фаза 1)

Servant — отдельный юнит `UNIT_FACELESS_SERVANT`: **HP 60 / броня 0 / dmg 4–5 / AS 1 /
range 1.5 / speed 4 / bounty 0**, префаб `Faceless_Servant` (scale 1.25, меш без оружия),
маркер `SummonBonusSlot = 13` (не игровой слот). Источники: бонус 3 Call of the Abyss
(on-kill кастером), Raise the Drowned, способность героев/титана Call of the Deep (Фаза 5),
трек #5 «Рой на месте гибели» (Фаза 6). Servant не цепляет трек-ролл #5 и не является героем
для пира #7. Канон с числами — `wiki/rules/faceless-unit-bonuses.md`.

### Расовые уники — Древние (слоты 11–12)

Player-level модификаторы, без замены юнитов. Утверждены в **FACELESS-010** и входят в kit
FACELESS-008; канон с числами — `wiki/rules/faceless-unit-bonuses.md`.

| # | Слот | Имя (EN) | Эффект |
|---|------|----------|--------|
| 11 | `BONUS_SLOT_RACE_UNIQUE_1` | **Shadow of the Void** | Все войска владельца (юниты+герои+титан): **8%** шанс полностью избежать атаки (при спавне) |
| 12 | `BONUS_SLOT_RACE_UNIQUE_2` | **Void Bastion** | Все здания владельца: атаки по ним **промахиваются 20%** (ретро + новые) |

Асимметрия к Людям: March Discipline (+10% скорости) → Shadow of the Void (уклонение);
Stone Masonry (+20% HP зданий) → Void Bastion (промах по зданиям). Древние **не получают**
сырой прочности — они делают цели **неуловимыми**; это компенсирует пассив
`PASSIVE_FACELESS_BLEAK_FOUNDATIONS` (−10% HP зданий).

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

## RACE_FACELESS (Древние) — играбельный контент-кэтчеп (PRE-RACE2)

```entity
id: RACE_FACELESS
display_name: Древние
display_name_en: Faceless
fantasy_hook: Древние богоподобные сущности; бирюзовый ретекстур WC3-пака
start_passives:               # FACELESS-008; игровой unlock после GATE
  positive: [PASSIVE_FACELESS_ABYSSAL_HUNGER, PASSIVE_FACELESS_RELENTLESS_TIDE]
  negative: PASSIVE_FACELESS_BLEAK_FOUNDATIONS
start_gold: 250               # как Human (штраф по золоту — только у Людей)
units:
  melee: UNIT_FACELESS_MELEE
  ranged: UNIT_FACELESS_RANGED
  caster: UNIT_FACELESS_CASTER   # без заклинаний: пустой кит (гейт FACELESS-009)
  siege: UNIT_FACELESS_SIEGE
  flying: UNIT_FACELESS_FLYING
  super: UNIT_FACELESS_SUPER
heroes: [HERO_FACELESS_1, HERO_FACELESS_2, HERO_FACELESS_3]
bonus_slots: 12            # все 12 слотов: дизайн утверждён (FACELESS-010); реализация + снятие гейта — follow-up
buildings: BUILDING_SET_FACELESS   # здания Nazjatar Houses by Ageron (FACELESS-007); race-keyed каталог, fallback на Human-скин
upgrades: UPGRADE_TREE_FACELESS # stat-дерево как у Human (статы = Human роль→роль); tower/magic — свои треки
tower_tracks:                     # FACELESS-008; порядок = слоты 4–12 UI башни; механики не дублируют бонусы
  - UPG_TOWER_FACELESS_CHITINOUS_HIDE       # Melee+Super: броня +1/2/3; L3 +15% max HP
  - UPG_TOWER_FACELESS_HOLLOW_BARBS         # Ranged+Flying: пробитие брони 1/2/3
  - UPG_TOWER_FACELESS_VACUUM_COLLAPSE      # Melee+Flying: при смерти враги r3 −15/25/35% скорости 3 с
  - UPG_TOWER_FACELESS_RITUAL_OF_THE_DEEP   # Caster: аура r8 −6/10/15% урона от юнитов и героев
  - UPG_TOWER_FACELESS_UNNERVING_AIM        # Ranged+Caster: Рой на месте гибели — on-death 5/10/15%: спавн servant (FACELESS-017)
  - UPG_TOWER_FACELESS_FRENZY_OF_THE_DEEP   # Melee+Siege: скорость атаки +10/15/20%
  - UPG_TOWER_FACELESS_SPLASH_OF_THE_DEEP   # Caster+Super: Пир на герое — servants r8 +30% dmg/+30% max HP 10 с (FACELESS-017)
  - UPG_TOWER_FACELESS_HOLLOW_BONES         # Siege+Flying: скорость движения +8/16/24%
  - UPG_TOWER_FACELESS_VOID_HARDENING       # все юниты: −10/15/20% урона от зданий и башен
magic_spells:                     # FACELESS-008
  - SPELL_FACELESS_1
  - SPELL_FACELESS_2
  - SPELL_FACELESS_3
mvp: true
note: Контент (SO/prefabs/каталоги) и runtime-гейты готовы (FACELESS-001..009). Все 12 бонус-слотов (юниты 1–6, ветераны 7–10, уники 11–12) — дизайн утверждён (FACELESS-010, канон wiki/rules/faceless-unit-bonuses.md); реализация и снятие гейта HasBonusKit=false — follow-up карточки FACELESS-011..014. Полный asymmetry kit (пассивы 2+/1−, кастер-кит ×3, tower-треки ×9, уники 11–12) — FACELESS-008 (2026-09-08, дизайн). Игровой unlock — только после GATE; `SelectableRaceIds` этой карточкой не менялся.
```

Статы юнитов Faceless = **копия Human роль→роль** (по решению пользователя). Титан = `hero1 ×3` (`TitanRules.BaseStatMultiplier`), как у Human. `_Review`-префабы удалены после сверки маппинга.

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
| Раса #2 (Faceless) | Играбельна на стартовых гейтах (статы Human, без kit/бонусов, без магии); здания свои (FACELESS-007); бонусы 1–12 — `FACELESS-010`; **asymmetry kit (пассивы 2+/1−, магия ×3, tower-треки ×9, уники 11–12) — `FACELESS-008`, дизайн 2026-09-08**. Игровой unlock после GATE; `SelectableRaceIds` не менялся |

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
- [x] Раса #2 (Faceless) — контент-кэтчеп играбелен (FACELESS-001..009); `RACE_FACELESS` в каталоге
- [x] Здания Древних (FACELESS-007): Nazjatar Houses by Ageron, 3 префаба `Prefabs/Races/Faceless/Buildings/` (04=TownHall, 05=Tower, 03=Barracks), race-keyed `BuildingVisualCatalog` с fallback на Human
- [x] Полный asymmetry kit Faceless — **FACELESS-008** (2026-09-08): пассивы 2+/1−, кастер-кит ×3, tower-треки ×9, уники 11–12 записаны выше; реализация — follow-up после GATE
- [x] Бонусы Древних: дизайн всех 12 слотов утверждён — **FACELESS-010** (2026-09-08; канон `wiki/rules/faceless-unit-bonuses.md`; реализация — follow-up)
