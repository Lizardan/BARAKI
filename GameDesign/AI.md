---
doc_id: unit_ai
version: 0.4
status: locked
depends_on: [units, heroes, core_gameplay]
provides: [unit_autonomy, hero_autonomy, combat_targeting]
---

# AI (Unit & Hero autonomy)

> **Не боты-противники.** Игра только PvP (`Vision.md`). Этот документ описывает **автономное поведение юнитов и героев** на поле боя.

## Out of scope

```entity
id: BOT_OPPONENTS
status: rejected
reason: Только матчи против людей; пустые слоты не заполняются ИИ
```

Скриптовые противники (VI), сложность, diplomacy-модули — **не делаем**.

---

## Unit brain (MVP)

```entity
id: UNIT_AI_MARCH
states: [Spawn, March, Engage, Dead]
mvp: true
```

| State | Поведение |
|-------|-----------|
| Spawn | Появление у rally point barracks |
| March | Движение по сплайну `lane_id` к врагу |
| Engage | Ближайшая вражеская цель в радиусе `aggro_radius` |
| Dead | Событие kill → золото убийце |

### Target selection

1. Фильтр: `owner != self`, тот же `lane_id` (или shared arena для CENTER).
2. Приоритет по `combat_role`:
   - Melee → в `attack_range`: **стоит и бьёт**; иначе **бежит** к ближайшей цели; **не атакует** `UNIT_TYPE_FLYING`
   - Ranged → ближайший в `attack_range` (включая flying)
   - Caster → **race spells** (см. `Races.md` § Magic) по CD/AI; baseline attack если нет CD
   - **Siege** → здания в lane; **не атакует** flying
   - **Flying** → дальний бой; цели: ground/ranged/flying
   - **Super** → приоритет `BUILDING_*`; дальний осадный
3. Нет целей → продолжить March к `deep_point` вражеской базы по lane.

```entity
id: COMBAT_FLYING_TARGETING
valid_attackers: [UNIT_TYPE_RANGED, UNIT_TYPE_FLYING, UNIT_TYPE_CASTER, UNIT_TYPE_SUPER, BUILDING_TOWER, heroes]
invalid_attackers: [UNIT_TYPE_MELEE, UNIT_TYPE_SIEGE]
mvp: true
```

### Center arena (N ≥ 3)

- **March:** после spawn center-юнит идёт по сплайну через **Central Arena** к базе **напротив** (`center_primary_target`).
- **Combat в арене:** может атаковать **любого** вражеского юнита в зоне арены с `lane_id = CENTER`.
- **Elimination:** если march target eliminated → retarget **next alive clockwise** (пропуская eliminated).

---

## Hero brain

См. `Heroes.md`. Дополнительно:

```entity
id: HERO_AI_TOWER_DRAW
behavior: Если башня ведёт огонь по герою, повысить приоритет атаки этой башни
weight: 0.8
mvp: true
```

---

## Отладка без ботов

| Инструмент | Назначение |
|------------|------------|
| `MatchSandbox` (Editor) | Два spawn point, волны без сети |
| Unit tests | Lane routing, targeting, bounty |
| Playmode + 2 клиента | Основной способ проверки боя |

---

## Locked decisions

| Решение | Значение |
|---------|----------|
| Stuck на сплайне | **MVP: не обрабатываем** — при хорошем spread не должно случаться; если всплывёт на playtest — фиксим отдельно |
| Friendly fire | **Нет** — FFA only; team modes вне scope |
