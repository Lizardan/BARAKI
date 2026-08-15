---
doc_id: units
version: 0.5
status: locked
depends_on: [core_gameplay, balance, races]
provides: [unit_types, stats_schema, combat_behavior, bounties, squad_compositions]
---

# Units

## Unit types (roles)

```entity
id: UNIT_TYPE_MELEE
combat_role: frontline
targets: nearest_enemy_in_lane
mvp: true

id: UNIT_TYPE_RANGED
combat_role: backline
range: medium
mvp: true

id: UNIT_TYPE_CASTER
combat_role: support_dps
abilities: [heal_or_slow_mvp]
mvp: true

id: UNIT_TYPE_SIEGE
combat_role: structure_pressure
targets: enemy_buildings_in_lane
unlock: barracks_level_2
mvp: true

id: UNIT_TYPE_FLYING
combat_role: ranged_air
attack_class: ranged
movement: air_lane_spline
unlock: barracks_level_3
targetable_by: [UNIT_TYPE_RANGED, UNIT_TYPE_FLYING, UNIT_TYPE_CASTER, UNIT_TYPE_SUPER, BUILDING_TOWER, heroes]
not_targetable_by: [UNIT_TYPE_MELEE, UNIT_TYPE_SIEGE]
mvp: true

id: UNIT_TYPE_SUPER
combat_role: siege_ranged
attack_class: ranged
targets: enemy_buildings_in_lane
unlock: barracks_level_4
note: MVP — усиленный дальний осадный юнит
mvp: true

id: UNIT_TYPE_TITAN
combat_role: elite_hero_scale
targets: nearest_enemy_in_lane
unlock: titan_research_complete    # полоска 180s над main (main L3 + 3 героя на базе) → idle на базе
deploy_building: BUILDING_BARRACKS
deploy_gold: 2500
cooldown_after_death: 300
stat_multiplier_vs_hero: 3.0    # сид на префаб Human_Titan; рантайм не множит снова
attack_range: 3.0               # TitanRules.AttackRange; vs-titan hit reach = max(attacker, 3)
xp_leveling: same_as_hero
visual: TT_Peasant
visual_prefab: Human_Titan
note: Отдельный тип (не 4-й hero slot); полоска над main, появление на базе, выпуск 2500g как герой; CD 300s без повторной полоски; баланс на префабе; см. Heroes.md
mvp: true
```

## Stat schema (ScriptableObject)

```yaml
id: string              # UNIT_HUMAN_MELEE
race_id: string         # RACE_HUMAN
unit_type: enum         # Melee | Ranged | Caster | Siege | Flying | Super | Titan

# Combat
max_hp: float
armor: float
damage_min: float
damage_max: float
attack_speed: float
attack_range: float
move_speed: float

# Economy
gold_bounty: int

# Presentation
prefab: string
icon: string
```

## Barracks level → squad (кумулятивно)

Состав **накапливается** с каждым уровнем barracks. Структура **одинакова для всех рас**; конкретные юниты — из `RaceDefinition` (см. `Races.md`).

| Level | Добавляется за апгрейд | Итого за волну |
|-------|------------------------|----------------|
| **1** | 2 melee, 1 ranged, 1 caster | 4 |
| **2** | +2 siege, +1 melee | 7 |
| **3** | +1 flying, +1 caster, +1 ranged | 10 |
| **4** | +1 super, +1 siege, +1 melee, +1 ranged | **14** |

```entity
id: SQUAD_BARRACKS_L1
barracks_level: 1
composition:
  melee: 2
  ranged: 1
  caster: 1
total_units: 4

id: SQUAD_BARRACKS_L2
barracks_level: 2
adds:
  siege: 2
  melee: 1
cumulative:
  melee: 3
  ranged: 1
  caster: 1
  siege: 2
total_units: 7

id: SQUAD_BARRACKS_L3
barracks_level: 3
adds:
  flying: 1
  caster: 1
  ranged: 1
cumulative:
  melee: 3
  ranged: 2
  caster: 2
  siege: 2
  flying: 1
total_units: 10

id: SQUAD_BARRACKS_L4
barracks_level: 4
adds:
  super: 1
  siege: 1
  melee: 1
  ranged: 1
cumulative:
  melee: 4
  ranged: 3
  caster: 2
  siege: 3
  flying: 1
  super: 1
total_units: 14
spawn_offset: wedge_formation
march_spread: lateral_offset_by_slot + stagger_along_spline
note: Позиция = spline(t) + offset(slot); server-authoritative, без NavMesh
```

`SquadComposition` SO: `race_id` + `barracks_level` → резолвит `{race}_MELEE` и т.д.

## Human roster (placeholder stats)

```entity
id: UNIT_HUMAN_MELEE
race_id: RACE_HUMAN
unit_type: UNIT_TYPE_MELEE
max_hp: 120
armor: 1
damage: 8-10
attack_speed: 1.0
attack_range: 1.5
move_speed: 4.0
gold_bounty: 8

id: UNIT_HUMAN_RANGED
race_id: RACE_HUMAN
unit_type: UNIT_TYPE_RANGED
max_hp: 70
damage: 6-8
attack_range: 8.0
move_speed: 3.5
gold_bounty: 6

id: UNIT_HUMAN_CASTER
race_id: RACE_HUMAN
unit_type: UNIT_TYPE_CASTER
max_hp: 60
damage: 4-5
attack_range: 6.0
spells: [SPELL_HUMAN_1, SPELL_HUMAN_2, SPELL_HUMAN_3]   # unlock via UPG_MAIN_MAGIC
ability_mvp_placeholder: removed
gold_bounty: 7

id: UNIT_HUMAN_SIEGE
race_id: RACE_HUMAN
unit_type: UNIT_TYPE_SIEGE
max_hp: 200
damage: 12-16
attack_range: 10.0
move_speed: 2.5
gold_bounty: 15
note: Приоритет — здания в lane

id: UNIT_HUMAN_FLYING
race_id: RACE_HUMAN
unit_type: UNIT_TYPE_FLYING
max_hp: 90
damage: 8-10
attack_range: 6.0
move_speed: 5.5
gold_bounty: 10
note: Дальний бой; бьёт только ranged/flying/towers

id: UNIT_HUMAN_SUPER
race_id: RACE_HUMAN
unit_type: UNIT_TYPE_SUPER
max_hp: 500
armor: 2
damage: 30-40
attack_speed: 0.5
attack_range: 12.0
move_speed: 2.0
gold_bounty: 50
note: Сильный осадный дальний бой; приоритет — здания
```

> **MVP baseline:** одинаковые **базовые** статы юнитов; **асимметрия** — passives, magic, tower tracks (`Races.md`).

## Bonus-enhanced variants

При выборе бонуса replacement-слоты (1–10) подменяют def на `*_BONUS` — те же роли, усиленные статы + способности TBD. Уже вышедшие юниты не меняются. См. `Bonuses.md`.

## Combat resolution (MVP)

- Target: ближайший валидный враг в том же `lane_id` (scan **0.2 s**, sticky target), иначе **Move** по маршруту lane.
- **Siege:** приоритет `BUILDING_*` в lane, если в range.
- **Flying:** дальний бой по air spline; **melee и siege не могут атаковать** flying.
- **Super:** как siege — приоритет здания; **дальний** бой (`attack_range` 12).
- **Melee:** в `attack_range` → **Stop & hit**; иначе **Chase** — прямо к цели + ally avoidance
- Damage: `rand(damage_min, damage_max) - armor_reduction` (min 1).
- No friendly fire.
- Death: despawn + `OnUnitKilled` event.

## Upgrades affecting units

См. `Upgrades.md` — flat +% damage/armor/hp per type (глобально для расы).

## Locked decisions

| Решение | Значение |
|---------|----------|
| Движение по lane | **Route follow** (lookahead по `LaneRoute`) + **local ally avoidance**; NavMesh **не** используется (MVP) |
| Толпа / anti-merge | **Spread:** formation spawn + **ally avoidance**; **server-authoritative** |
| Ближний бой | **Classic RTS:** в радиусе атаки — **стоит и бьёт**; вне радиуса — **прямой chase** к цели (без attack slots) |
| Состав волны | **Кумулятивный** по `barracks_level` 1→4; см. таблицу выше |
| Flying | **Только ranged/flying/super/towers** могут атаковать flying; melee/siege — нет |
| Super (MVP) | **Сильный осадный дальний** юнит; приоритет зданий |
| MVP расы | **Идентичны** по статам и механикам |
| Стартовые расы | **1:** `RACE_HUMAN` (+2 слота TBD) |
| Титан | Отдельный тип `UNIT_TYPE_TITAN`; полоска 180s над main → idle на базе; выпуск 2500g; CD 300s как герой; визуал `TT_Peasant`; статы сид 3× на префаб, дальше баланс на префабе |
| Бонусы | 12 слотов per race; см. `Bonuses.md` |
