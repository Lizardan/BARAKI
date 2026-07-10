---
doc_id: economy
version: 0.4
status: draft
depends_on: [core_gameplay, units]
provides: [gold_rules, costs, income_formulas]
---

# Economy

## Ресурсы

MVP — только **Gold**. Phase 2: Lumber/Mana для спеллов (optional).

```entity
id: RES_GOLD
name: Gold
min: 0
max: 99999
display: integer
mvp: true
```

## Income — **2 источника** (MVP)

### 1. Kill bounty

```entity
id: INCOME_KILL
formula: gold = floor(unit.gold_bounty * killer_owner_multiplier)
killer_owner_multiplier: 1.0
assist: false
mvp: true
```

| Правило | Описание |
|---------|----------|
| Lane attribution | Gold получает владелец юнита, нанёсшего killing blow |
| Center lane | Один kill может обогатить одного игрока |
| Building kill | No gold (MVP) |
| Hero kill | `gold_bounty * 2` |

### 2. Passive gold — прокачка главного здания

**Прирост** каждые **30s**. Без прокачки: **+0**. Каждый level `UPG_MAIN_PASSIVE_GOLD` в `BUILDING_MAIN`: **+25g** к тику (max **9** levels).

```entity
id: INCOME_MAIN_PASSIVE
tick_interval: 30.0
unit: seconds
base_growth_per_tick: 0
bonus_per_upgrade_level: 25
formula: gold_per_tick = upgrade_level * 25
examples:
  upgrade_0: 0
  upgrade_1: 25
  upgrade_9: 225
max_upgrade_level: 9
cap_by_main: main_level * 3    # L1→3, L2→6, L3→9
upgrade_location: BUILDING_MAIN
mvp: true
```

### Starting gold (кошелёк)

**500g** в кошельке при старте матча — **не** passive income.

```entity
id: ECON_START
gold: 500
race_modifiers:
  RACE_HUMAN: -250    # PASSIVE_HUMAN_LEVY_TAX → 250g
  RACE_BUG: 0
mvp: true
```

## Costs (MVP)

| Action | Cost | Time |
|--------|------|------|
| `UPG_MAIN_BUILDING_LEVEL` | **2000 / 3000** | **120 / 180 s** |
| `UPG_MAIN_PASSIVE_GOLD` (per level) | **200** | **25 s** |
| `UPG_MAIN_MAGIC` slot 1/2/3 | **800 / 1500 / 2500** | **60 / 90 / 135 s** |
| `UPG_BARRACKS_LEVEL` | **1000 / 1500 / 2500** | **45 / 90 / 135 s** |
| `UPG_TOWER_*` (per track level) | **500 / 800 / 1200** | **45 / 90 / 135 s** |
| `UPG_MELEE_DMG` L1…L9 | **75…275** (см. `Upgrades.md`) | **8…24 s** |
| `UPG_RANGED_DMG` L1…L9 | **75…275** | **8…24 s** |
| `UPG_ARMOR` L1…L9 | **60…220** | **6…22 s** |
| `UPG_CASTER_HEAL` L1…L9 | **90…290** | **10…26 s** |
| `HERO_HIRE` | 500 | per hero, once |
| `HERO_DEPLOY` | 1000 | instant |

## Spending rules

- Нельзя уйти в минус.
- Очередь исследований: 1 active per building type (MVP).
- Refund on cancel: 100% (MVP, для UX).

## Economic decisions (design intent)

| Trade-off | Описание |
|-----------|----------|
| Upgrades vs hero | Hero spike now vs stronger every wave |
| Defense vs pressure | Tower repair / spell vs tier-2 rush |
| Center vs side | Feed risk in center for multi-opponent gold |
| Save vs spend | Passive gold vs kill pressure vs апгрейды |

## Locked decisions

| Решение | Значение |
|---------|----------|
| Shared team gold | **Нет** — FFA, каждый игрок свой кошелёк |
| Источники gold | **2:** kill bounty + passive (**0** base, **+25g/level** каждые 30s) |
| Starting gold | **500g** base; **Люди 250g** (−250 passive) |
| Interest на кошелёк | **Нет** |
| Passive cap | Max level **9**; gate = **main level × 3** |
| Passive gold upgrade | **200g**, **25s** per level |
| Magic unlock | **800 / 1500 / 2500g**; **60 / 90 / 135 s** |
| Main level upgrade | **2000 / 3000g**; **120 / 180 s** |
| Barracks level | **1000 / 1500 / 2500g**; **45 / 90 / 135 s** |
| Tower upgrade (per level) | **500 / 800 / 1200g**; **45 / 90 / 135 s** |
| Stat upgrades | **+3%** or **+10% heal** per level; см. `Upgrades.md` |
