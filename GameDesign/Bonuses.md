---
doc_id: bonuses
version: 0.2
status: draft
depends_on: [match_flow, races, units, heroes]
provides: [bonus_pick_rules, bonus_slots, replacement_policy]
---

# Bonuses

> **Статус:** все **12 слотов реализованы** (Люди). PRE-006a: слоты 1–6 (юнитовые бонусы). PRE-006b: слоты 7–12 — ветераны (герои ×3, титан) и расовые уники (March Discipline / Stone Masonry). Канон: `wiki/rules/human-unit-bonuses.md`. См. `TODO.md`.

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

| # | Слот | Смысл каркаса | Эффект | Статус |
|---|------|---------------|--------|--------|
| 1 | `BONUS_SLOT_MELEE` | Melee | Замена на усиленную версию: доп. статы + Cleave (on-hit AoE) | ✅ PRE-006a |
| 2 | `BONUS_SLOT_RANGED` | Ranged | То же + Deadeye (крит ×2 on-hit 15%) | ✅ PRE-006a |
| 3 | `BONUS_SLOT_CASTER` | Caster | То же + Battlemace (melee-булава + staff) | ✅ PRE-006a |
| 4 | `BONUS_SLOT_SIEGE` | Siege | То же + Siege Regen Aura (+1 HP/с, r=8) | ✅ PRE-006a |
| 5 | `BONUS_SLOT_FLYING` | Flying | То же + Last Call (спавн Ranged при смерти 25%) | ✅ PRE-006a |
| 6 | `BONUS_SLOT_SUPER` | Super | То же + Catapult (парабола + splash в точке прилёта) | ✅ PRE-006a |
| 7 | `BONUS_SLOT_HERO_1` | Король-ветеран | Тот же кит ×~1.35, ульта → **King's Command** (+30% урона всей армии 8 с); morale +15% dmg | ✅ PRE-006b |
| 8 | `BONUS_SLOT_HERO_2` | Паладин-ветеран | Тот же кит ×~1.35, Shield → **Aegis** (+броня + щит 25% max HP союзникам рядом); morale +15% AS | ✅ PRE-006b |
| 9 | `BONUS_SLOT_HERO_3` | Жрец-ветеран | Тот же кит ×~1.35, Greater Heal → **Sanctuary** (зона следует за жрецом); morale +15% брони | ✅ PRE-006b |
| 10 | `BONUS_SLOT_TITAN` | Титан-ветеран | Статы ×1.4/×1.35/+2 armor поверх 3×; Colossus → **Greater Colossus** (+25% max HP армии) | ✅ PRE-006b |
| 11 | `BONUS_SLOT_RACE_UNIQUE_1` | March Discipline | **+10% скорости передвижения** всем войскам (юниты+герои+титан) | ✅ PRE-006b |
| 12 | `BONUS_SLOT_RACE_UNIQUE_2` | Stone Masonry | **+20% HP зданий**, ретроактивно всем стоящим (текущий HP масштабируется пропорционально) | ✅ PRE-006b |

### Ветераны (слоты 7–10)

- Формат: **тот же кит**, остальные 3 способности с числами ×~1.35; **одна сигнатура** заменяет базовую способность.
- Статы: HP ×1.4, урон ×1.35, броня +2 (титан — поверх сида 3× героя 1). Цена выпуска титана не меняется (2500g).
- Визуал: та же модель + флаг (`TT_RTS_Banner_plain`) на спине, командная покраска как обычно; префабы `Prefabs/Races/Humans/BonusHeroes/{Hero1..3,Titan}/`, портреты автопекутся (`UnitPortraitBaker`).
- Morale-аура ветерана: +15% (vs +10% у базового героя), тот же стат.
- Применение пика: только будущие hire/deploy/redeploy героя и summon/redeploy титана (общая replacement policy).

### Расовые уники Людей (слоты 11–12)

- **March Discipline (11):** множитель скорости применяется при спавне (волны/найм/герои/титан) — уже заспавненные войска не ускоряются (replacement policy).
- **Stone Masonry (12):** при пике все стоящие здания владельца получают ×1.2 max HP, текущий HP масштабируется пропорционально; новые/апгрейднутые здания тоже ×1.2 (синк по уровням учитывает пик).

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
| Умения слотов 1–12 | Invent + fill = **PRE-006**; обязательны до EA-001 |
| Stack | Каждый бонус = **+1 линия** усиления; **стакается** с tower upgrades и прочим tech |
| Late game | Матчи ~**40–60 мин** — у всех мощные юниты и сильно усиленная армия |

## Open

- [x] Конкретные статы/умения enhanced-юнитов слотов 1–6 (PRE-006a) — `wiki/rules/human-unit-bonuses.md`
- [x] Умения слотов 7–12: усиленные герои ×3, титан, уники Людей (PRE-006b, 2026-08-25) — King's Command / Aegis / Sanctuary / Greater Colossus / March Discipline / Stone Masonry
- [x] UI оверлея и сетевой state (PRE-001)
