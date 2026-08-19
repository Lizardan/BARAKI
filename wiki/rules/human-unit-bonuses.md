# Human unit bonuses (PRE-006a)

Шесть юнитовых бонусов Людей (слоты 1–6) + ауры радиусом 8.

## Пик → спавн

- `MatchPlayerState.BonusPickSlot` пишется в `MatchController.TrySetBonusPick` / host-apply снапшота.
- `HandleWave` / manual call / pending spawn передают
  `HumanBonusUnitRules.EffectiveBonusSlotForRole(pick, role)` → только юниты **совпадающей** роли
  получают `BonusSlot` / bonus-статы / bonus-префаб.
- `UnitStatsResolver.Resolve(…, bonusSlot)` берёт bonus-префаб или `RaceDefinition.GetUnitBonus`, затем стакает `RaceUpgradeStatsRules.Apply`.
- Казармы UI: кнопка выкупа роли показывает **bonus-портрет**, если пик совпадает с ролью.
- Клиент: снапшот v19 (`BonusSlot` / `AuraRadius` / `AuraColorPacked`); презентер берёт префаб через `TryGetPrefab(…, bonusSlot)`.

## Ауры (визуал)

Пассивные ауры — loop-префаб под носителем (по умолчанию CFXR Runic, но можно сменить в Studio):

| Носитель | AbilityId | Prefab (сид) | Тинт (сид) |
|----------|-----------|--------------|------------|
| King | 13 Damage | CFXR3 Magic Aura A (Runic) | crimson |
| Paladin | 23 Haste | CFXR3 Magic Aura A (Runic) **без Rays** | green |
| Priest | 33 Armor | CFXR3 Magic Aura A (Runic) | silver-blue |
| Titan | 43 MaxHp | CFXR3 Magic Aura A (Runic) **без Rays** | warm orange |
| Siege BONUS | 50 Regen | CFXR3 Magic Aura A (Runic) **без Rays** | **holy yellow** |

Источник истины визуала — `UnitAbilityDef.Fx` (BARAKI Studio). Если `VfxPrefab` null —
fallback `MatchFxCatalog` Runic + `PassiveAuraFxRules`. Child `Rays` снимается.
У Titan отдельно на **корне визуала** всегда `TitanBodyRays` (только Rays из Runic, тинт MaxHp).
Подробности: `wiki/rules/ability-fx.md`.

`TryGetAuraVisual` рисует ауру только для `AuraBehaviour` (например Siege Regen). Пассивы-трейты
с `Radius` для AoE/splash (Cleave, Catapult) не показывают.

## Механики (`HumanBonusUnitRules` + ability defs)

| Слот | AbilityId | DisplayName | Эффект |
|------|-----------|-------------|--------|
| 1 Melee | 51 Cleave | Passive | On-hit 15%: AoE = raw удара, враги, радиус 2 |
| 2 Ranged | 52 Deadeye | Passive | On-hit 15%: урон прилёта ×2 |
| 3 Caster | 1–3 + 53 Battlemace | Active+Passive | спеллы кастера; `< 2 м` — melee 8–10 × MeleeDamageLevel + **infantry AttackVariant 1** (staff ranged=0); посох и булава видны одновременно |
| 4 Siege | 50 Siege Regen Aura | Passive | +1 HP/с в радиусе 8 |
| 5 Flying | 54 Last Call | Passive | On-death 25%: спавн базового Ranged |
| 6 Super | 55 Catapult | Passive | Парабола + splash 50% raw в радиусе 3 **в точке прилёта**; диск splash 1 с |

## Super artillery

- Base Super range **10**, BONUS **12**; оба с **min range 5** (атака только в полосе 5…max).
- Ближе 5 — отступают; дальше max — chase.
- После `BeginAttack` Super держит Attack до конца интервала атаки даже если цель умерла;
  снаряд всё равно вылетает в locked aim.

Пассивы бонусов — обычные `UnitAbilityDef` на BONUS-префабах (`UnitCombatSettings`),
папка `BonusUnits/{Role}/Abilities/`. Базовые melee/ranged/siege/flying/super без бонуса —
без способностей.

## UI

Оверлей BonusPick: панель **350×** как command-dock, слоты `ui-btn--square` (те же пропорции,
что казармы / главное здание). Портреты 1–6 + tooltip; слоты 7–12 permanently disabled.

## Контент

- Defs / префабы / портреты — по категории: `Units/{Role}/`, `BonusUnits/{Role}/`,
  `Heroes/{HeroN|Titan}/` (см. `wiki/rules/content-assets.md`).
- Меню: `Rebuild TT Prefabs` → `Build Ability Defs` → `Sync Balance` → `Seed Unit Abilities`.
- Миграция раскладки: `BARAKI/Content/Migrate Content Folders`.

## Bonus-модели и анимации (`TtUnitVisualSetup`)

| Слот | Модель | Anim set | Idle / Run / Attack / Death |
|------|--------|----------|-----------------------------|
| 1 Melee | `TT_Halberdier` | `animation_infantry/Polearm` | polearm_* |
| 2 Ranged | `TT_Crossbowman` | `animation_infantry/Crossbow` | crossbow_* |
| 3 Caster | `TT_HighPriest` | `animation_infantry/Staff` (+ infantry mace AttackVariant 1) | staff_* / infantry_04_attack_A |
| 4 Siege | `TT_Paladin` (пеший) | `animation_infantry/Shield` | shield_* — **не** `cavalry_shield` |
| 5 Flying | `Fly_Hors_Archer` | `animation_cavalry/cavalry_archer` | cav_archer_* — **не** bare `cavalry` |
| 6 Super | `machines/TT_Catapult_lvl1` | `animation_machines/Catapult` | catapult_* — **не** Ballista |

Siege BONUS — пеший: `AbilityAnimRules.ResolveAttackClipSeconds(Siege, bonusSlot:4)` = infantry 1.5s.

## UI выбора

Нижняя context-strip / inspector: при `unit.BonusSlot` → `TryGetBonusPortrait` + title `«Роль · усиленный»`.
