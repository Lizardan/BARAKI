---
doc_id: bonuses
version: 0.2
status: draft
depends_on: [match_flow, races, units, heroes]
provides: [bonus_pick_rules, bonus_slots, replacement_policy]
---

# Bonuses

> **Статус:** UI-оверлей **есть** (PRE-001 done). Геймплей/умения слотов — **пустые кнопки**. Invent + внедрение всех 12 = **PRE-005**, **блокер расы #2** (вместе с PRE-006 + GATE). См. `TODO.md`.

## Обзор

После выбора расы матч **стартует сразу** — волны идут, паузы нет. Поверх HUD — оверлей выбора бонуса на **60 с**.

```entity
id: BONUS_PICK_RULES
trigger: after_race_pick_on_game_scene
match_pause: false
overlay_duration_sec: 60
choices_per_player: 1
pool_size: 12
visibility: hidden_from_opponents
visibility_note: скрытность = отсутствие отображения в UI (чужие пики не показываются); данные реплицируются всем клиентам (снапшот v13), выбор переживает reconnect и host migration
timeout_pick: random_from_pool
ui_display_order: titan, race_unique_1, hero_1..3, units, race_unique_2
ui_display_order_note: только порядок кнопок оверлея; slot_index в данных и снапшоте v13 не меняется (1..12 стабильны)
replacement_scope: future_spawns_only
already_spawned_units: unchanged
mvp: false
note: Каркас; реализация PRE-001
```

### Replacement policy

- Уже заспавненные юниты остаются **базовыми**.
- Усиление применяется только к **следующим** волнам, **следующим** наймам (manual call) и **redeploy** героя/титана до конца матча.
- Enhanced-варианты — **data slots** (`*_BONUS`), не новые роли юнитов.

## 12 слотов на расу

| # | Слот | Смысл каркаса | Эффект |
|---|------|---------------|--------|
| 1 | `BONUS_SLOT_MELEE` | Melee | Замена на усиленную версию: доп. статы + 1–2 способности |
| 2 | `BONUS_SLOT_RANGED` | Ranged | То же |
| 3 | `BONUS_SLOT_CASTER` | Caster | То же |
| 4 | `BONUS_SLOT_SIEGE` | Siege | То же |
| 5 | `BONUS_SLOT_FLYING` | Flying | То же |
| 6 | `BONUS_SLOT_SUPER` | Super | То же |
| 7 | `BONUS_SLOT_HERO_1` | Герой слот 1 | Тот же слот, другой def (усиленный) |
| 8 | `BONUS_SLOT_HERO_2` | Герой слот 2 | То же |
| 9 | `BONUS_SLOT_HERO_3` | Герой слот 3 | То же |
| 10 | `BONUS_SLOT_TITAN` | Титан | Усиленный титан при summon |
| 11 | `BONUS_SLOT_RACE_UNIQUE_1` | Уникальный #1 | Не замена юнита; per race |
| 12 | `BONUS_SLOT_RACE_UNIQUE_2` | Уникальный #2 | Не замена юнита; per race |

```entity
id: BONUS_SLOT_MELEE
slot_index: 1
replacement_type: unit_role
target: UNIT_TYPE_MELEE
enhanced_def_suffix: _BONUS
abilities: tbd
mvp: false

id: BONUS_SLOT_RANGED
slot_index: 2
replacement_type: unit_role
target: UNIT_TYPE_RANGED
enhanced_def_suffix: _BONUS
abilities: tbd
mvp: false

id: BONUS_SLOT_CASTER
slot_index: 3
replacement_type: unit_role
target: UNIT_TYPE_CASTER
enhanced_def_suffix: _BONUS
abilities: tbd
mvp: false

id: BONUS_SLOT_SIEGE
slot_index: 4
replacement_type: unit_role
target: UNIT_TYPE_SIEGE
enhanced_def_suffix: _BONUS
abilities: tbd
mvp: false

id: BONUS_SLOT_FLYING
slot_index: 5
replacement_type: unit_role
target: UNIT_TYPE_FLYING
enhanced_def_suffix: _BONUS
abilities: tbd
mvp: false

id: BONUS_SLOT_SUPER
slot_index: 6
replacement_type: unit_role
target: UNIT_TYPE_SUPER
enhanced_def_suffix: _BONUS
abilities: tbd
mvp: false

id: BONUS_SLOT_HERO_1
slot_index: 7
replacement_type: hero_slot
target: hero_slot_1
enhanced_def_suffix: _BONUS
abilities: tbd
mvp: false

id: BONUS_SLOT_HERO_2
slot_index: 8
replacement_type: hero_slot
target: hero_slot_2
enhanced_def_suffix: _BONUS
abilities: tbd
mvp: false

id: BONUS_SLOT_HERO_3
slot_index: 9
replacement_type: hero_slot
target: hero_slot_3
enhanced_def_suffix: _BONUS
abilities: tbd
mvp: false

id: BONUS_SLOT_TITAN
slot_index: 10
replacement_type: titan
target: UNIT_TYPE_TITAN
enhanced_def_suffix: _BONUS
abilities: tbd
mvp: false

id: BONUS_SLOT_RACE_UNIQUE_1
slot_index: 11
replacement_type: race_unique
effect: tbd
mvp: false

id: BONUS_SLOT_RACE_UNIQUE_2
slot_index: 12
replacement_type: race_unique
effect: tbd
mvp: false
```

## Flow

```
Race pick → Match start (waves run) → Bonus overlay 60s
  → Player picks 1 of 12 (hidden) OR timeout → random
  → Future spawns/hires use enhanced defs where applicable
```

### Видимость выбора (важно)

- Все клиенты технически получают пики (снапшот v13) — сервер и клиенты «знают» выбор каждого.
- Скрытность реализуется **только на уровне UI**: в интерфейсе игрок видит свой выбор; чужие пики нигде не отображаются.
- Игрок узнаёт о чужих бонусах лишь по последствиям в игре (усиленные/заменённые юниты, уникальные эффекты).
- Т.к. пики в снапшоте — выбор корректно переживает **reconnect** и **host migration** без доп. механики.

## Locked decisions

| Решение | Значение |
|---------|----------|
| Таймер | **60 с**, без паузы матча |
| Выбор | Ровно **1** из **12** |
| Видимость | Чужие пики **не отображаются** в UI; данные технически знают все клиенты (снапшот v13) |
| Timeout | **Случайный** из 12 |
| Replacement | Только **будущие** спавны/наймы/redeploy |
| Умения слотов 1–12 | Invent + fill = **PRE-005**; обязательны до EA-001 |
| Stack | Каждый бонус = **+1 линия** усиления; **стакается** с tower upgrades и прочим tech |
| Late game | Матчи ~**40–60 мин** — у всех мощные юниты и сильно усиленная армия |

## Open

- [ ] Конкретные статы/умения enhanced-юнитов (PRE-005) — закрытие = критерий PRE-005 done
- [ ] Уникальные расовые бонусы слотов 11–12 Людей (PRE-005)
- [x] UI оверлея и сетевой state (PRE-001)
