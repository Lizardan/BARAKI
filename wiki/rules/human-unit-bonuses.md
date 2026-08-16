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

Диск под носителем: `y = 0.12`, `renderQueue = Transparent+80`, `sortingOrder = 32`, ZWrite off —
поверх земли без мерцания z-fight.

## Механики (`HumanBonusUnitRules` + ability defs)

| Слот | AbilityId | DisplayName | Эффект |
|------|-----------|-------------|--------|
| 1 Melee | 51 Cleave | Passive | On-hit 15%: AoE = raw удара, враги, радиус 2 |
| 2 Ranged | 52 Deadeye | Passive | On-hit 15%: урон прилёта ×2 |
| 3 Caster | 1–3 + 53 Battlemace | Active+Passive | спеллы кастера; `< 2 м` — melee 8–10 × MeleeDamageLevel + **infantry AttackVariant 1** (staff ranged=0); посох и булава видны одновременно |
| 4 Siege | 50 Siege Regen Aura | Passive | +1 HP/с в радиусе 8 |
| 5 Flying | 54 Last Call | Passive | On-death 25%: спавн базового Ranged |
| 6 Super | 55 Catapult | Passive | Парабола + splash 50% raw в радиусе 3 |

Пассивы бонусов — обычные `UnitAbilityDef` на BONUS-префабах (`UnitCombatSettings`),
папка `Units/{Role}/Bonus/Abilities/`. Базовые melee/ranged/siege/flying/super без бонуса —
без способностей.

## UI

Оверлей BonusPick: панель **350×** как command-dock, слоты `ui-btn--square` (те же пропорции,
что казармы / главное здание). Портреты 1–6 + tooltip; слоты 7–12 permanently disabled.

## Контент

- Defs / префабы / портреты — по роли: `Units/{Role}/` + `Bonus/` (см. `wiki/rules/content-assets.md`).
- Меню: `Rebuild TT Prefabs` → `Build Ability Defs` → `Sync Balance` → `Seed Unit Abilities`.
- Миграция раскладки: `BARAKI/Content/Migrate To Role Folders`.

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
