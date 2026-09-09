# Human unit bonuses (PRE-006a) и ветераны/уники (PRE-006b)

Шесть юнитовых бонусов Людей (слоты 1–6) + ауры радиусом 8.

## Пик → спавн

- `MatchPlayerState.BonusPickSlot` пишется в `MatchController.TrySetBonusPick` / host-apply снапшота.
- `HandleWave` / manual call / pending spawn передают
  `BonusKitRules.EffectiveBonusSlotForRole(raceId, pick, role)` → только юниты **совпадающей** роли
  получают `BonusSlot` / bonus-статы / bonus-префаб.
- `UnitStatsResolver.Resolve(…, bonusSlot)` берёт bonus-префаб или `RaceDefinition.GetUnitBonus`, затем стакает `RaceUpgradeStatsRules.Apply`.
- Казармы UI: кнопка выкупа роли показывает **bonus-портрет**, если пик совпадает с ролью.
- Клиент: снапшот v21 (`BonusSlot` / `AuraRadius` / `AuraColorPacked` в UnitsStatic); презентер берёт префаб через `TryGetPrefab(…, bonusSlot)`.

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
что казармы / главное здание). Портреты 1–10 + tooltip; уники 11–12 — текст + tooltip.
Все 12 слотов активны для пика (PRE-006b).

## Ветераны и уники (PRE-006b, слоты 7–12)

| Слот | Имя (EN) | Эффект |
|------|----------|--------|
| 7 | King Veteran | Тот же кит ×~1.35; ульта → **King's Command** (id 56): вся армия владельца +30% урона 8 с (реюз `UltimateBuff*` на каждом юните); morale-аура 15% dmg |
| 8 | Paladin Veteran | Тот же кит ×~1.35; Shield → **Aegis** (id 57): +броня И щит-абсорбция 25% max HP (`AbsorbRemaining` в `MatchUnitState`, поглощение до HP в `ApplyDamage`, истекает по длительности); morale 15% AS |
| 9 | Priest Veteran | Тот же кит ×~1.35; Greater Heal → **Sanctuary** (id 58): `HeroHealZoneState.FollowUnitId` — зона следует за живым кастером, умирает с ним; morale 15% брони |
| 10 | Titan Veteran | Статы ×1.4/×1.35/+2 поверх 3× сида; Colossus → **Greater Colossus** (id 59): аура MaxHp 25% (обычный `AuraBehaviour`); цена выпуска 2500g без изменений |
| 11 | March Discipline | +10% скорости всем войскам: `HumanBonusUnitRules.ApplyMarchDiscipline` в `RaceUpgradeStatsRules.Apply` (статы) и в `HandleWave` (march speed волн) |
| 12 | Stone Masonry | +20% max HP зданий: `MatchController.ResolveBuildingMaxHp` учитывает пик; `BuildingState.SetMaxHp(scaleCurrentProportionally: true)` — текущий HP масштабируется пропорционально; применяется при пике (ретро) и при каждом синке уровней |

> **Race-гейт (FACELESS-013, 2026-09-09):** `ApplyMarchDiscipline` и `ResolveBuildingMaxHp`
> дополнительно требуют `player.RaceId == GameIds.Races.Human` — иначе инопланетная раса с пиком
> 11/12 унаследовала бы чужие уники. Детали — `faceless-unit-bonuses.md`.

Общее:

- Статы ветеранов: HP ×1.4, dmg ×1.35, броня +2 (`BonusKitRules.Veteran*`). Префаб-сеттингс
  авторитетен; фолбэк без префаба — `UnitStatsResolver.ResolveBase` × `BonusKitRules.ApplyVeteranMultipliers`.
- Слот героя N усиливается пиком `6+N`; титан — пиком 10 (`BonusKitRules.EffectiveBonusSlotForHero/ForTitan`,
  race-aware версия с `raceId` гейтует расы без кита).
  `BonusSlot` юнита едет в UnitsStatic v21 без изменений кодека — клиентские визуал/портреты
  резолвятся тем же `TryGetPrefab/TryGetBonusPortrait` (каталог расширен слотами 7–10).
- Ветеранские киты: `AbilityKitDefaults.CreateKingBonus/PaladinBonus/PriestBonus/TitanBonus`
  (+ `CreateVeteranKit(slot)`), сидятся тем же `Build Ability Defs` → `Seed Unit Abilities`;
  balance — `Sync Balance to Prefabs` (`UnitCombatSettings.CopyFromVeteran`).
- Префабы: `BARAKI/Units/Build Veteran Prefabs` (`VeteranPrefabBuilder`) — копия базового
  героя/титана + child `VeteranBanner` (`TT_RTS_Banner_plain`, сид 0.6× роста тела, спина = **−Z**:
  у TT-бипеда forward после baked yaw = **+Z**, плащ/спина = −Z). Пересборка **сохраняет** ручную
  подгонку флага (pos/rot/scale) и синхронизированные `UnitCombatSettings`/abilities существующего
  префаба — сид применяется только к новому префабу.
- Портреты: `UnitPortraitBaker` печёт `Art/UI/UnitPortraits/Humans/BonusHeroes/{Hero1..3,Titan}.png`.

**Правило контента:** имена (DisplayName, названия бонусов/уников) — **английские**,
описания — **русские**. У всего контента (defs, слоты бонусов, тултипы).

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

Ветераны (7–10) — те же модели/аним-сеты, что базовые герои/титан, + child `VeteranBanner`
(собирает `VeteranPrefabBuilder`). Аним-контроллер наследуется от базового префаба.

## UI выбора

Нижняя context-strip / inspector: при `unit.BonusSlot` (1–10, `MatchesUnit`) → `TryGetBonusPortrait` +
title `«Роль · усиленный»` / `«Роль · ветеран»`. Казармы (deploy героя) и main (титан) показывают
ветеранский портрет, когда пик совпадает со слотом.
