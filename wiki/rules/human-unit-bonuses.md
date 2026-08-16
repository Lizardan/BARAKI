# Human unit bonuses (PRE-006a)

Шесть юнитовых бонусов Людей (слоты 1–6) + ауры радиусом 8.

## Пик → спавн

- `MatchPlayerState.BonusPickSlot` пишется в `MatchController.TrySetBonusPick` / host-apply снапшота.
- `HandleWave` / pending spawn передают `bonusSlot` в `SpawnUnit` → `MatchUnitState.BonusSlot`.
- `UnitStatsResolver.Resolve(…, bonusSlot)` берёт bonus-префаб или `RaceDefinition.GetUnitBonus`, затем стакает `RaceUpgradeStatsRules.Apply`.
- Клиент: снапшот v19 (`BonusSlot` / `AuraRadius` / `AuraColorPacked`); презентер берёт префаб через `TryGetPrefab(…, bonusSlot)`.

## Механики (`HumanBonusUnitRules`)

| Слот | Эффект |
|------|--------|
| 1 Melee | On-hit 15%: AoE = raw удара, враги, радиус 2 (цель не дублируется) |
| 2 Ranged | On-hit 15%: урон прилёта ×2 |
| 3 Caster | `< 2 м` — melee 8–10 × MeleeDamageLevel; иначе ranged |
| 4 Siege | Passive `AuraHpRegen` (id 50): +1 HP/с в радиусе 8 |
| 5 Flying | On-death 25%: спавн базового Ranged (`bonusSlot=0`) |
| 6 Super | Парабола + splash 50% raw в радиусе 3 (`AppliesSplashAoe`) |

## UI

Оверлей: портреты 1–6 + tooltip (`GetSlotDescription`); слоты 7–12 permanently disabled.

## Контент

- Defs / префабы / портреты — по роли: `Units/{Role}/` + `Bonus/` (см. `wiki/rules/content-assets.md`).
- Меню: `Rebuild TT Prefabs` → `Build Ability Defs` → `Sync Balance` → `Seed Unit Abilities`.
- Миграция раскладки: `BARAKI/Content/Migrate To Role Folders`.
