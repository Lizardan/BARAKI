---
doc_id: heroes
version: 0.7
status: draft
depends_on: [units, economy, core_gameplay, races, buildings]
provides: [hero_hire, hero_deploy, hero_morale, hero_ai, hero_roster]
---

# Heroes

## Design role

До **3 героев** на расу — **лимит найма = `main_level`** главного здания (1/2/3).  
**Выпускаются** из **живого** barracks — **мгновенно**, в lane этого barracks.  
Игрок **не микроит** в бою.

## Roster size

```entity
id: HERO_ROSTER_SIZE
max_heroes_per_race: 3
max_hired: main_building_level    # L1→1, L2→2, L3→3
hero_slots: [1, 2, 3]
mvp: true
```

Каждый герой — **независимое** состояние (idle / deployed / dead).

## Hero states (per hero slot)

```entity
id: HERO_STATE_NONE
description: Ещё не нанят

id: HERO_STATE_IDLE_AT_BASE
description: На базе — даёт morale bonus (если настроен для слота)

id: HERO_STATE_DEPLOYED
description: В lane — бьётся; morale bonus **нет**

id: HERO_STATE_DEAD
description: Убит — morale **нет**; после CD можно deploy снова
```

## Economy

```entity
id: HERO_HIRE
building: BUILDING_MAIN
hire_gold: 500
hire_research_sec: 25
once_per_hero: true
mvp: true

id: HERO_DEPLOY
building: BUILDING_BARRACKS
deploy_gold: 1000
cast_time: 0              # мгновенный spawn у rally barracks
cooldown_after_death: 300
rehire_required: false
mvp: true
```

## Flow

### Hire — `BUILDING_MAIN`

```
1. Player picks hero slot (1..3) → "Hire"
2. Pay 500 gold (per hero, once per match); research **25 s**
3. Hero → IDLE_AT_BASE; morale active (если есть для слота)
```

### Deploy — **живой** barracks

```
1. Player selects barracks → picks hero slot → "Deploy"
2. Pay 1000 gold
3. Hero **мгновенно** spawns у rally этого barracks → lane barracks
4. Morale bonus этого героя **off**
5. Пока deployed — deploy этого героя **недоступен** из других barracks
```

### Death

```
1. Hero dies → DEAD; morale **off**
2. CD 300 s
3. Deploy снова из любого **живого** barracks (1000g); re-hire не нужен
```

## Leveling & XP

```entity
id: HERO_LEVELING
max_level: 10
xp_source_unit_kill: hero_credits_kill_of_enemy_unit
xp_source_building_kill: own_units_kill_enemy_building
level_persist: within_match    # переживает смерть и redeploy; сброс в новом матче
mvp: true

id: HERO_LEVEL_STATS
hp_per_level: 40
damage_per_level: 3
armor_per_level: 0.5
mvp: true
```

- Опыт получает **герой-слот** (уровень живёт в слоте, не на юните): при убийстве героем **вражеского юнита** (XP = bounty жертвы) и при убийстве твоими юнитами **вражеского здания** (фикс. XP) — независимо от состояния героя (idle/deployed/dead).
- При deploy/redeploy герой спавнится с **уровнем слота** (не с 1).
- Статы растут с уровнем: `base + (level-1) * per_level`.

## Hero abilities

Каждый герой — **4 способности**, открываются на **одних и тех же** уровнях (авто-каст, игрок не микроит). Кит уникален per slot.

```entity
id: HERO_ABILITY_UNLOCK_LEVELS
ability_1: 1
ability_2: 4
ability_3: 7
ability_4: 10
mvp: true
```

> Активки — CD-only (без маны). Аура слота 3 — глобальный бафф владельца (без радиуса), пока герой жив. Ауры разных героев **стакаются** (разные статы).

### Slot 1 — TT_King

| Unlock | Ability | Механика |
|--------|---------|----------|
| lvl 1 | Strike | AoE урон вокруг героя, CD |
| lvl 4 | Heal | Лечение героя + союзников в радиусе, CD |
| lvl 7 | Aura | Пассив: **+10% урона** армии владельца |
| lvl 10 | Ultimate | Большой AoE урон + самоусиление (+50% урона, 8 с), длинный CD |

```entity
id: HERO_HUMAN_1_ABILITIES
hero_id: HERO_HUMAN_1
ability_1: strike_aoe
ability_2: heal_aoe
ability_3: aura_damage_percent
ability_4: ultimate_aoe_self_buff
mvp: true
```

### Slot 2 — TT_Mounted_Paladin

| Unlock | Ability | Механика |
|--------|---------|----------|
| lvl 1 | Smite | Кара: высокий урон по **ближайшему** врагу, CD |
| lvl 4 | Shield | Щит: +броня герою и союзникам в радиусе на время, CD |
| lvl 7 | Aura | Пассив: **+10% attack speed** армии владельца |
| lvl 10 | Consecration | Освящение: AoE урон + краткий stun врагов, длинный CD |

```entity
id: HERO_HUMAN_2_ABILITIES
hero_id: HERO_HUMAN_2
ability_1: smite_single
ability_2: shield_armor_buff
ability_3: aura_attack_speed_percent
ability_4: consecration_aoe_stun
mvp: true
```

### Slot 3 — TT_Mounted_Priest

| Unlock | Ability | Механика |
|--------|---------|----------|
| lvl 1 | Holy Nova | Вспышка: небольшой урон врагам **и** хил союзникам вокруг, CD |
| lvl 4 | Greater Heal | Сильное лечение героя + союзников в радиусе, CD |
| lvl 7 | Aura | Пассив: **+10% armor** армии владельца |
| lvl 10 | Revive | Возрождение ближайшего союзного трупа + хил-пульс, длинный CD |

```entity
id: HERO_HUMAN_3_ABILITIES
hero_id: HERO_HUMAN_3
ability_1: holy_nova
ability_2: greater_heal
ability_3: aura_armor_percent
ability_4: revive_corpse_heal
mvp: true
```

## Morale bonus (боевой дух)

| Состояние | Bonus |
|-----------|-------|
| Не нанят | — |
| Idle на базе | По таблице слота |
| Deployed | **Нет** |
| Dead (CD) | **Нет** |

Bonuses **стакаются** от всех idle-героев (deployed/dead не дают).

| Slot | Idle bonus (MVP baseline) | Post-MVP |
|------|---------------------------|----------|
| **1** | **+10% damage** юнитам | Уникально per race |
| **2** | **+10% attack speed** юнитам | Уникально per race |
| **3** | **+10% armor** юнитам | Уникально per race |

```entity
id: HERO_MORALE_SLOT_1
effect: +10% unit damage
scope: all_units_owner
mvp: true

id: HERO_MORALE_SLOT_2
effect: +10% unit attack_speed
scope: all_units_owner
mvp: true

id: HERO_MORALE_SLOT_3
effect: +10% unit armor
scope: all_units_owner
mvp: true
```

> MVP: все расы — **одинаковые** baseline-бонусы. Позже — уникальные per race (контент).

## Deploy rules

```entity
id: HERO_DEPLOY_RULES
spawn: instant_at_barracks_rally
lane: barracks lane_binding
one_barracks_per_deploy: true    # deployed герой не вызывается с другого barracks
max_deployed_per_hero: 1
mvp: true
```

## Hero AI (autonomy)

| Priority | Behavior |
|----------|----------|
| 1 | Attack nearest enemy threatening self |
| 2 | Attack nearest enemy building in lane if no units |
| 3 | Towers targeting hero → hero prioritizes that tower |

Classic RTS — см. `Units.md` / `AI.md`.

## Roster — Human (MVP)

```entity
id: HERO_HUMAN_1
slot: 1
race_id: RACE_HUMAN
hero_name: TT_King
visual_prefab: Human_Hero1
idle_morale: HERO_MORALE_SLOT_1
max_hp: 600
armor: 4
damage: 35-45
gold_bounty: 80
mvp: true

id: HERO_HUMAN_2
slot: 2
race_id: RACE_HUMAN
hero_name: TT_Mounted_Paladin
visual_prefab: Human_Hero2
idle_morale: HERO_MORALE_SLOT_2
max_hp: 600
armor: 4
damage: 35-45
gold_bounty: 80
mvp: true

id: HERO_HUMAN_3
slot: 3
race_id: RACE_HUMAN
hero_name: TT_Mounted_Priest
visual_prefab: Human_Hero3
idle_morale: HERO_MORALE_SLOT_3
max_hp: 600
armor: 4
damage: 35-45
gold_bounty: 80
mvp: true
```

Баланс героев (HP/броня/урон и т.д.) — на префабе (`UnitBalanceSettings`); `HeroDefinition` — фолбек и GDD-зеркало.

## Bonus hero variants

Слоты бонусов 7–9 (`BONUS_SLOT_HERO_*`) — тот же hero slot, другой `HeroDefinition` (`*_BONUS`). Умения TBD. См. `Bonuses.md`.

## Titan (отдельный тип, не 4-й hero slot)

```entity
id: TITAN_RULES
unit_type: UNIT_TYPE_TITAN
research_building: BUILDING_MAIN
research_sec: 180
research_bar: above_main_building
research_gates:
  main_level: 3
  heroes_hired: all_3
  heroes_idle_at_base: all_3    # только пока все 3 героя одновременно на базе
freeze_on: [hero_deployed, hero_dead]   # прогресс замораживается, не сбрасывается
research_gold: 0               # пассивная полоска, без оплаты и без hire в main
appears_at_base_on_complete: true
deploy_building: BUILDING_BARRACKS
deploy_gold: 2500
deploy_cast_time: 0            # мгновенный spawn у rally barracks
cooldown_after_death: 300
redeploy_cost_again: true      # повторный выпуск после CD снова 2500g
re_research_after_death: false # полоску 180s набирать снова не нужно
xp_leveling: same_as_hero
stat_multiplier_vs_hero: 3.0    # сид на префаб Human_Titan (UnitBalanceSettings); рантайм не множит снова
visual: TT_Peasant
visual_prefab: Human_Titan
bonus_slot: BONUS_SLOT_TITAN
mvp: true
```

Титан **не нанимается в main как герой**. Пока **одновременно** `main_level >= 3`, наняты **все 3 героя** и все трое `IdleAtBase` — над главным зданием идёт **полоска 180 с**. Любой герой deployed/dead — прогресс **замораживается** (не сбрасывается); когда все 3 снова на базе — полоска продолжается.

По заполнении титан **появляется на базе** (`IdleAtBase`, parked). Выпуск из **живого** barracks **мгновенно за 2500g** (как hero deploy). Смерть → CD **300 s** per barracks, как у героя; повторный выпуск после CD — **снова 2500g**, **без** новой полоски 180 с. XP/уровни — как у героя (`HERO_LEVELING`). Базовые статы сидятся как **3× героя** на префаб `Human_Titan` (`UnitBalanceSettings`); дальше титан балансится на префабе независимо (рантайм **не** умножает снова).

## Titan research flow

```
1. main_level >= 3 + все 3 героя наняты + все 3 IdleAtBase → полоска над main идёт
2. Любой герой deployed/dead → прогресс ЗАМОРОЖЕН (значение сохраняется)
3. Все 3 снова на базе → полоска продолжается
4. progress >= 180 s → титан IdleAtBase (появляется на базе)
```

### Deploy — **живой** barracks

```
1. Player selects barracks → "Выпуск титана"
2. Pay 2500 gold
3. Титан **мгновенно** spawns у rally этого barracks → lane barracks
4. Пока deployed — повторный выпуск недоступен
```

### Death

```
1. Титан dies → CD 300 s per barracks (как hero death)
2. Выпуск снова из любого **живого** barracks (2500g); полоску 180s не повторять
```

## Locked decisions (confirmed)

| Решение | Значение |
|---------|----------|
| Героев на расу | Max **3**; hire cap = **main level** (1/2/3); 500g each; research **25 s** |
| Deploy | **1000g**; **мгновенно** из выбранного **живого** barracks |
| CD после смерти | **300 s** |
| Deployed / dead | Morale **нет**; re-hire **не нужен** |
| Idle bonuses (MVP) | Slot1 +10% dmg, slot2 +10% AS, slot3 +10% armor; **стакаются** |
| Idle bonuses (post-MVP) | **Уникальные** per race |
| XP / leveling | **Есть**: max lvl 10; XP за убийства героем юнитов + убийства зданий твоими юнитами; уровень слота переживает смерть/redeploy; статы растут; 4 способности на 1/4/7/10, кит уникален per hero slot |
| Титан | Отдельный тип; **не hire в main**; полоска **180 s** над main (L3 + все 3 героя на базе; deployed/dead → заморозка) → появляется на базе; выпуск **2500g** из живого barracks; CD 300 s как герой (без повторной полоски); XP как герой; визуал `TT_Peasant` / `Human_Titan`; статы сид 3× на префаб, дальше баланс на `UnitBalanceSettings`; PRE-002 |

## Open

- [x] Статы/имена/визуал героев slot 2–3 (MVP = symmetric baseline stats; PRE-004)
