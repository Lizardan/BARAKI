---
doc_id: heroes
version: 0.5
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
2. Pay 500 gold (per hero, once per match)
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
idle_morale: HERO_MORALE_SLOT_1
max_hp: 600
armor: 4
damage: 35-45
gold_bounty: 80
mvp: true

id: HERO_HUMAN_2
slot: 2
race_id: RACE_HUMAN
idle_morale: HERO_MORALE_SLOT_2
max_hp: 600
armor: 4
damage: 35-45
gold_bounty: 80
mvp: true

id: HERO_HUMAN_3
slot: 3
race_id: RACE_HUMAN
idle_morale: HERO_MORALE_SLOT_3
max_hp: 600
armor: 4
damage: 35-45
gold_bounty: 80
mvp: true
```

## Roster — Bug (MVP)

```entity
id: HERO_BUG_1
slot: 1
race_id: RACE_BUG
idle_morale: HERO_MORALE_SLOT_1
mvp: true

id: HERO_BUG_2
slot: 2
idle_morale: HERO_MORALE_SLOT_2
mvp: true

id: HERO_BUG_3
slot: 3
idle_morale: HERO_MORALE_SLOT_3
mvp: true
```

> MVP: Human/Bug **идентичны** по статам; слоты 2–3 — placeholder до контента.

## Locked decisions (confirmed)

| Решение | Значение |
|---------|----------|
| Героев на расу | Max **3**; hire cap = **main level** (1/2/3); 500g each |
| Deploy | **1000g**; **мгновенно** из выбранного **живого** barracks |
| CD после смерти | **300 s** |
| Deployed / dead | Morale **нет**; re-hire **не нужен** |
| Idle bonuses (MVP) | Slot1 +10% dmg, slot2 +10% AS, slot3 +10% armor; **стакаются** |
| Idle bonuses (post-MVP) | **Уникальные** per race |
| XP / leveling | **Нет** |

## Open

- [ ] Статы/имена/визуал героев slot 2–3 (MVP = symmetric baseline stats)
