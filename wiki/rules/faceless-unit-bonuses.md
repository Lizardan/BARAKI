# Faceless (Древние) — бонусы юнитов (FACELESS-010)

Дизайн утверждён пользователем **2026-09-08** (сессия 1 юнит за юнитом + слоты 11–12). Все **12 слотов**. Полный asymmetry kit (пассивы/магия/треки башен) — **FACELESS-008**, дизайн 2026-09-08: `GameDesign/Races.md` § Древние.

> **Runtime-гейт Фазы 1 активен:** `BonusKitRules.HasBonusKit(raceId)` = false для
> `RACE_FACELESS` (`NoBonusKitRaceIds`); race-aware `EffectiveBonusSlotForRole/Hero/Titan`
> → 0; `UnitStatsResolver.ResolveBase` охраняет bonus/veteran-ветки. Faceless **не получает**
> множители ветеранов (veteran ×1.4/×1.35/+2 — только расы с китом). Снятие гейта и реализация —
> **отдельные follow-up карточки** (код здесь не менялся).

## Боевые идентичности (контекст дизайна)

- Super (крип) и Flying — **меле-удар** без снаряда; Super бьёт вплотную (min range 0),
  Flying бьёт и летающих (`UnitCombatIdentity`).
- Siege — меле-удар с приоритетом зданий.
- Caster — заклинаний нет (пустой кастер-кит Фазы 1); бонус даёт кастеру «фишку» без манá.
- Герои: Hero1 Король (меле), Hero2 Колдун (меле), Hero3 Берсерк (**дальний**, range 12).
- Титан = король ×3 (`TitanRules.BaseStatMultiplier`).

## Слоты 1–6 (юнитовые бонусы)

| Слот | UnitId (база) | Имя (EN) | Эффект |
|------|---------------|----------|--------|
| 1 Melee | `UNIT_FACELESS_MELEE` | **Hunger of the Old One** | Пассив. On-hit **15%**: лечение = **50% урона** удара (вампиризм) |
| 2 Ranged | `UNIT_FACELESS_RANGED` | **Tainting Bolt** | Пассив. On-hit **15%**: дот **3 dmg/с × 3 с** (≈9–10 итог) |
| 3 Caster | `UNIT_FACELESS_CASTER` | **Call of the Abyss** | On-**kill** (добивание кастером, шанс **100%**): спавн **1 мини-меле** со статами **×0.5 от Melee** (HP 60, dmg 4–5, броня 0), масштаб **×0.67** (в 1.5 раза меньше) |
| 4 Siege | `UNIT_FACELESS_SIEGE` | **Death Explosion** | При смерти: взрыв, урон = **10% max HP** вражеским **юнитам** в радиусе **3**; здания **не** задевает |
| 5 Flying | `UNIT_FACELESS_FLYING` | **Hungering Flight** | On-kill: **+15% AS** на 3 с, стакается до 3 |
| 6 Super | `UNIT_FACELESS_SUPER` | **Feast on the Fallen** | On-kill: **+80 HP**, **+10% AS** на 3 с, стакается до 3 |

### Заметки по механикам

- **Вампиризм (1)**: лечение только от нанесённого урона удара (не от промаха), до реального
  урона после брони цели.
- **Call of the Abyss (3)**: мини-юнит — меле (роль Melee, статы ×0.5), команда владельца,
  спавн рядом с кастером, масштаб префаба ×0.67 от `Faceless_Melee`. Добивание — только убийство
  от этого кастера (не союзников, не дот-бонуса другого юнита).
- **Death Explosion (4)**: срабатывает при любой смерти бонус-юнита (убит юнитом/зданием/героем).
  Урон фикс = 10% max HP бонусного осадника (≈20 при базе 200). Здания не задевает — только
  вражеские юниты, включая летающих.
- Спавн-бонусы (3, 5, 6) и on-hit (1, 2) — работают по replacement policy: применяются только
  к будущим спавнам после пика.

## Слоты 7–9 (ветераны-герои) и 10 (титан)

Формат — **как у Human PRE-006b**: статы ×1.4 HP / ×1.35 dmg / +2 брони, morale +15% (тот же
стат, что у базового героя), остальные 3 способности с числами ×~1.35, заменяется **одна сигнатура**.
Префаб-сеттингс авторитетен; фолбэк без префаба — `BonusKitRules.ApplyVeteranMultipliers`
(заглушка для Faceless не применяется до снятия гейта).

| Слот | Герой | Имя (EN) | Сигнатурная замена | Morale |
|------|-------|----------|--------------------|--------|
| 7 | Hero1 Король | **Ancient Mantle** | Ульта-замена: сам герой **+50% dmg** и **+2 брони** на 8 с, AoE-удар вокруг **+30%** | +15% dmg |
| 8 | Hero2 Колдун | **Area of Miss** | Замена Shield: каст **на область** — враги в радиусе **5** на **4 с** при атаках **промахиваются (100%)** (урон не наносится) | +15% AS |
| 9 | Hero3 Берсерк | **Feast Zone** | Замена Greater Heal: зона **10 с** следует за героем; союзники внутри лечатся на **30% от нанесённого ими урона** | +15% брони |
| 10 | Titan | **Aura of Hunger** | Замена Colossus: пока титан жив, вся армия владельца лечится на **15% от нанесённого урона** | — (титан без morale) |

Титан: статы ×1.4/×1.35/+2 **поверх** сида 3× героя 1. Цена выпуска 2500g не меняется.

## Слоты 11–12 (расовые уники)

Player-level модификаторы (как March Discipline / Stone Masonry у Людей), без замены юнитов.
Применение: будущие спавны/наймы (11) и ретро + будущие здания (12).

| Слот | Имя (EN) | Эффект |
|------|----------|--------|
| 11 | **Shadow of the Void** | Все войска владельца (юниты+герои+титан): **8%** шанс **полностью избежать** атаки (промах по ним), применяется при спавне |
| 12 | **Void Bastion** | Все здания владельца: атаки по ним **промахиваются на 20%** (применяется ко всем зданиям, включая существующие при пике) |

Флейвор: «тень» делает войска неосязаемыми; «оплот» — тьма укрепляет структуры (асимметрия
к March Discipline / Stone Masonry Людей).

## Реализация слотов 1–6 (FACELESS-011, 2026-09-08)

| Файл | Роль |
|------|------|
| `Combat/FacelessBonusUnitRules.cs` | константы эффектов, slot↔role маппинг, `IsFacelessBonus`, `RollProc` |
| `Combat/MatchCombatSystem.cs` | хуки бонусов (см. ниже) |
| `Combat/MatchUnitState.cs` | `FeastStacks` / `FeastRemainingSeconds` / `FeastAttackSpeedPerStack` (стаки 5–6) |
| `Tests/FacelessBonusUnitRulesTests.cs` | 8 тестов: вампиризм, дот, мини-меле, взрыв, стаки AS, экспара стаков, гейт расы/слота |

Хуки в `MatchCombatSystem`:

| Бонус | Точка входа | Механика |
|-------|-------------|----------|
| 1 вампиризм | `ResolveMeleeImpact` → `TryApplyHungerOfTheOldOne` | 15% прок, лечение = 50% фактического урона (`ApplyDamage` теперь возвращает урон) |
| 2 дот | `ResolveProjectileImpact` → `TryApplyTaintingBolt` | 15% прок, переиспользует burn-таймеры (3 dmg/с × 3 с) |
| 3 мини-меле | `ApplyDamage` (on-kill) → `TryApplyCallOfTheAbyss` | `SummonMinion(..., Melee, ×0.5)` |
| 4 взрыв | `ApplyDamage` (смерть) → `TryApplyDeathExplosion` | `ApplySplashDamage` 10% max HP, радиус 3, только юниты |
| 5 стаки AS | `ApplyDamage` (on-kill) → `TryApplyHungeringFlight` | +15%/стак, кап 3, 3 с |
| 6 лечение+AS | `ApplyDamage` (on-kill) → `TryApplyFeastOnTheFallen` | +80 HP, +10%/стак, кап 3, 3 с |

AS-стаки: множитель `GetFacelessFeastAsMultiplier` встроен в `GetUnitAttackInterval`
(делит интервал, как Bloodrage); decay — в `TickTowerTrackStatus`.

Гейт: механика работает, но `HasBonusKit(RACE_FACELESS)` всё ещё `false` — бонус-слот
юнитам присваивается только после снятия гейта (FACELESS-014, #70). Тесты задают
`bonusSlot` вручную через `SpawnUnit`.

### UI бонус-пика (race-aware)

`BonusPickRules.GetSlotDisplayName/GetSlotDescription(slot, raceId)` — Faceless-ветка по канону
(дефолт `raceId = RACE_HUMAN` сохраняет Human-тексты). Слоты без механики **серые**:

- `FacelessImplementedSlots = { 1..6 }` — что реально реализовано (FACELESS-011).
- `IsSlotImplemented(slot, raceId)` / `IsSlotAvailable(slot, raceId)` — гейт доступности.
- `GetRandomSlot(random, raceId)` и `FillTimeoutPicks(picks, random, raceIds)` — таймаут-пик
  роллит только доступные слоты.
- `MatchController.TrySetBonusPick` авторитетно отвергает недоступный слот.
- `BonusPickController` красит недоступные слоты классом `bonus-pick__slot--locked` и
  дописывает в тултип «(в разработке)».

После FACELESS-013 (11–12) расширить `FacelessImplementedSlots` до всех 12 слотов.

## Реализация слотов 7–10 (FACELESS-012, 2026-09-08)

Формат зеркалит Human PRE-006b: статы ×1.4 HP / ×1.35 dmg / +2 брони, morale-аура
+15% (базовая геройская аура = 10%), одна сигнатура заменяет базовую способность.

| Файл | Роль |
|------|------|
| `Combat/FacelessBonusUnitRules.cs` | константы эффектов (юниты 1–6, ветераны 7–10) и slot↔mechanic маппинг; общие слоты/множители/резолверы — в `BonusKitRules` |
| `Combat/BonusKitRules.cs` | **рас-агностичный слой**: топология слотов 1–12, veteran-множители ×1.4/×1.35/+2, `HasBonusKit`/`NoBonusKitRaceIds` (гейт), `MatchesUnit`, `EffectiveBonusSlotForRole/Hero/Titan`, `HeroSlotForBonusSlot/ForHeroSlot`, `BonusSlotForRole/RoleForBonusSlot`, `ApplyVeteranMultipliers`, `RollProc` |
| `Combat/AbilityIds.cs` | сигнатурные id: `AncientMantle=60`, `AreaOfMiss=61`, `FeastZone=62`, `AuraOfHunger=63` |
| `Combat/AbilityKitDefaults.cs` | `CreateAncientMantle/AreaOfMiss/FeastZone/AuraOfHunger` + `CreateVeteranKit(raceId, slot)`; `CreateForSpawn(raceId,...)` отдаёт ветеранский кит при совпадении слота |
| `Combat/Abilities/AreaOfMissBehaviour.cs` | накладывает `EvadeRemainingSeconds` на врагов в радиусе (100% промах) |
| `Combat/Abilities/FeastZoneBehaviour.cs` | зона `HealFractionOfDamageDealt` следует за кастером |
| `Combat/Abilities/AuraOfHungerBehaviour.cs` | пассивный маркер (хилит сам MatchCombatSystem-хук) |
| `Combat/MatchUnitState.cs` | `EvadeRemainingSeconds` |
| `Combat/HeroAbilityRules.cs` | `HeroHealZoneState.HealFractionOfDamageDealt` |
| `Combat/MatchCombatSystem.cs` | гейт промаха в `ApplyDamage`; хуки `TryApplyAuraOfHunger` / `TryApplyFeastZone` (хил 15%/30% от нанесённого урона); decay `EvadeRemainingSeconds` в `TickUnit`; пропуск плоского хила в `TickHealZones` для зон с `HealFractionOfDamageDealt>0` |
| `Tests/FacelessVeteranBonusTests.cs` | маппинг китов, `MatchesUnit` (из `BonusKitRules`), effective-слоты, Aura of Hunger, гейт промаха, Feast Zone |

Сигнатуры (как в дизайне выше):

| Слот | Герой | Сигнатура (id) | Механика |
|------|-------|----------------|----------|
| 7 | Hero1 Король | Ancient Mantle (60) | `GroundAoe(applyUltimateSelfBuff:true)`: сам +50% урона на 8 с, AoE-удар ×1.3; morale +15% dmg |
| 8 | Hero2 Колдун | Area of Miss (61) | `AreaOfMissBehaviour`: враги в радиусе 5 на 4 с промахиваются (атаки 0 урона) |
| 9 | Hero3 Берсерк | Feast Zone (62) | `FeastZoneBehaviour`: зона 10 с следует за героем, союзники внутри лечатся на 30% от урона; morale +15% брони |
| 10 | Titan | Aura of Hunger (63) | `AuraOfHungerBehaviour` (маркер) + хук `TryApplyAuraOfHunger`: пока титан жив, каждый юнит армии лечится на 15% от нанесённого им урона |

**Важно — гейт:** механика реализована, но `HasBonusKit(RACE_FACELESS)` всё ещё `false`
(`HumanBonusUnitRules.NoBonusKitRaceIds`). Пока гейт стоит:
- `BonusPickRules.FacelessImplementedSlots` расширен до **{1..10}** — слоты 7–10 доступны
  в оверлее (не серые), но реально не навешивают статы/киты до снятия гейта (FACELESS-014).
- `CreateForSpawn` уже вернёт ветеранский кит, как только `BonusSlot` начнёт проставляться
  (его проставляет резолвер по `BonusKitRules.EffectiveBonusSlotForHero/Titan`, заблокированный гейтом).
- Сами хуки в `ApplyDamage` гейтированы расой и наличием живого ветерана/титана, поэтому
  до FACELESS-014 не срабатывают.

## Статус

- [x] Дизайн слотов 1–12 утверждён пользователем (2026-09-08).
- [x] Реализация слотов 1–6 (FACELESS-011) — 2026-09-08.
- [x] Реализация слотов 7–10 (FACELESS-012) — киты, сигнатурные способности, боевые хуки,
      расширение `FacelessImplementedSlots` до {1..10} — 2026-09-08. Гейт `HasBonusKit` снят
      не был (follow-up FACELESS-014).
- [x] Префабы обычных героев Древних синхронизированы с Human (2026-09-08): `Faceless_Hero1/2/3`
      + `Faceless_Titan` заполнены базовыми геройскими способностями (10/11/12/13, 21/22/20/23,
      31/32/30/33, 40/41/42/43 — те же Human-дефы, что использует кит ветеранов). Префаб-сеттинг
      авторитетен; обычный герой = база, ветеран = база с заменой одной сигнатуры. Eдиный канон
      подтверждён карточкой FACELESS-012 (#68).
- [ ] Снятие runtime-гейта (`NoBonusKitRaceIds`) — FACELESS-014: разблокировать проставление
      `BonusSlot` для Faceless (резолвер + `MatchController`) и подключить `FacelessBonusUnitRules`
      в `UnitStatsResolver.ResolveBase` (ветеранские множители и префабы). Критерий
      «Do not enable selection before visual+kit ready».
- [ ] Реализация слотов 11–12 (FACELESS-013): Shadow of the Void, Void Bastion.
- [x] Полный asymmetry kit (пассивы, кастер-кит, magic upgrades, tower-треки Faceless) — FACELESS-008, дизайн 2026-09-08 (`GameDesign/Races.md`).

Связанные правила: `wiki/rules/human-unit-bonuses.md` (формат ветеранов/китов/UI как Люди),
`wiki/rules/faceless-assets.md`, `wiki/rules/abilities.md`.