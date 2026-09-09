# Faceless (Древние) — бонусы юнитов (FACELESS-010)

Дизайн утверждён пользователем **2026-09-08** (сессия 1 юнит за юнитом + слоты 11–12). Все **12 слотов**. Полный asymmetry kit (пассивы/магия/треки башен) — **FACELESS-008**, дизайн 2026-09-08: `GameDesign/Races.md` § Древние.

> **Runtime-гейт снят (FACELESS-014, 2026-09-09):** `NoBonusKitRaceIds` пуст —
> `BonusKitRules.HasBonusKit(RACE_FACELESS)` = true. Race-aware
> `EffectiveBonusSlotForRole/Hero/Titan` возвращают бонус-слот, `UnitStatsResolver.ResolveBase`
> разблокировал bonus/veteran-ветки: Faceless получает свои бонус-префабы и ветеранские
> множители (×1.4/×1.35/+2). Слои `MatchCombatSystem` (волна) и `MatchController` (ручной найм)
> стали race-aware (`player.RaceId`). Выбор расы в лобби (`SelectableRaceIds`) — по-прежнему
> отдельный гейт, не трогается в этой карточке.

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
(применяется для Faceless после снятия гейта FACELESS-014).

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
| `Combat/AbilityIds.cs` | def-маркеры 64–69 (`FacelessHunger`..`FacelessFeast`) |
| `Combat/AbilityKitDefaults.cs` | `CreateFacelessBonus(role)` — Passive-маркер на роль (Caster: спеллы + Call of the Abyss) |
| `Editor/UnitAbilityAssetBuilder.cs` | билд def-ассетов в `Faceless*/BonusUnits/*/Abilities` |
| `Editor/UnitAbilitySeeder.cs` | запись дефов в `_abilities` бонус-префабов |
| `Editor/FacelessBonusPrefabBuilder.cs` | сам канон бонус-префабов (см. «Канон префабов…» ниже) |

**Def-маркеры (2026-09-08):** механика — хуки, но у каждого бонус-слота 1–6 есть пассивный
`UnitAbilityDef` (IDs 64–69), как у Human (Cleave/Deadeye/…): карточка видна в инспекторе
`UnitCombatSettings` на `Faceless_*_BONUS.prefab` и ведёт к редактируемому SO (`UnitAbilityDefEditor`).
Caster-бонус (3) = `CreateFacelessCaster()` + маркер «Call of the Abyss» (зеркало `CreateCasterBonus`).
Рантайм-киты бонус-юнитов теперь непустые (`MatchesUnit` → `CreateFacelessBonus(role)`).
FX проков подбираются позже в BARAKI Studio (отдельный таб; пока дефы несут только цвет).

### Канон префабов бонусных юнитов (вид + структура папок)

Источник истины — `Editor/FacelessBonusPrefabBuilder.cs` (меню `BARAKI/Faceless/Build Bonus
Prefabs`), зеркало Human-`VeteranPrefabBuilder` по формату папок и контракту.

**Вид (как задумано):** у бонус-юнита — **заменённая (отдельная) модель**, как у Human
(`Human_*_BONUS` — своя моделька на каждый слот 1–6/7–10). Бонусный префаб **не** должен
показывать базовую модель.

**Вид (сейчас, временная заглушка):** новых моделей для Древних пока нет, поэтому бонусный
префаб клонируется из **базовой** модели той же роли (тот же меш), а единственный признак
усиленного юнита — **`BonusFlameMarker`** (синее свечение) в дочернем `BonusFlame` над головой
(позиция = `bounds.max.y + 0.15`). Это **заглушка до появления новых моделей**, не канон.
Когда модели появятся — бонус-префаб получает свою модель (replacement), свечение убирается.
Билдер идемпотентен (повторный прогон не копит маркеры).

**Контракт корня (компоненты на `Faceless_*_BONUS.prefab`):**
- `SkinnedMeshRenderer` + `Animator` → **задумано: свой `.controller` рядом** с бонус-префабом
  (`Faceless_*_BONUS.controller`) — под собственную (новую) модель, как у Human
  (`Human_*_BONUS.controller`). **Сейчас (заглушка):** переиспользуется контроллер базового юнита
  той же роли (`Faceless_Melee_BONUS` → `Faceless_Melee.controller`), своего `.controller` нет.
- `FacelessUnitTeamColor` (командный цвет, как у базовых).
- `UnitCombatSettings` — инспектируемый баланс + `_abilities[]` (см. def-маркеры выше).
- Ориентация/скейл — как у базовой роли (клонирование без смены): юниты Melee `1.75`, Flying
  `1.0`, остальные `1.5`; герои/титан `2.0`; yaw 270° (таблица `faceless-assets.md`).
  С появлением новых моделей скейл/yaw выверяются под каждую бонус-модель заново.

**Структура папок** — единый шаблон расы (как Human, `content-assets.md`), бонусы идут
категориями `BonusUnits/` (слоты 1–6) и `BonusHeroes/` (слоты 7–10), не в `Units/{Role}/Bonus/`:

```text
Prefabs/Races/Faceless/
├── Units/{Role}/Faceless_{Role}.prefab (+ Faceless_{Role}.controller)   # база
├── BonusUnits/{Role}/Faceless_{Role}_BONUS.prefab                        # бонусы 1–6 (заглушка: берёт базовый контроллер; задумано: свой Faceless_{Role}_BONUS.controller)
├── Heroes/{HeroN|Titan}/… (+ .controller)
└── BonusHeroes/{HeroN|Titan}/Faceless_{…}_BONUS.prefab                   # ветераны 7–10

ScriptableObjects/Races/Faceless/BonusUnits/{Role}/Abilities/*.asset      # def-маркеры 64–69 (только Caster — 4 шт.)
```

Билд (`Build Bonus Prefabs`) создаёт префабы, потом `BARAKI/Units/Sync Balance to Prefabs`
(баланс) и `BARAKI/Units/Seed Unit Abilities` (дефы в `_abilities[]`). После сида имена
бонус-префабов и их `_BONUS`-суффикс сохраняются; `UnitVisualPrefabBuilder` регистрирует их в
`UnitVisualCatalog` по `raceId`/роли.

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

Гейт снят (FACELESS-014): `HasBonusKit(RACE_FACELESS)` = true — бонус-слот присваивается юнитам
и волной, и ручным наймом. Тесты задают `bonusSlot` вручную через `SpawnUnit`.

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

- `FacelessImplementedSlots` — полный набор {1..12}: юниты 1–6 (FACELESS-011),
  ветераны 7–10 (FACELESS-012), уники 11–12 (FACELESS-013).

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

**Гейт снят (FACELESS-014, 2026-09-09):** `NoBonusKitRaceIds` пуст — слоты 7–10 реально
навешивают ветеранские статы и киты для Faceless:
- `BonusPickRules.FacelessImplementedSlots = {1..12}` — все слоты доступны в оверлее.
- `CreateForSpawn(raceId, …)` возвращает ветеранский кит по `BonusSlot`
  (`EffectiveBonusSlotForHero/Titan` больше не гейтирует расу).
- Хуки в `ApplyDamage` включены расой и наличием живого ветерана/титана (были готовы заранее).

### Ветеранские статы (префаб-синк) и портреты (2026-09-08)

- **`UnitBalanceSetup.SyncRace`** — ветеранский блок 7–10 (`CopyFromVeteran`) теперь общий для
  обеих рас: раньше Faceless-ветка выходила по `return` до ветеранов. Бонус-юниты 1–6
  синхронизируются только для Human (`includeHumanExtras`) — у Faceless они маркерные клоны базы.
- **Титан-ветеран = сид 3× героя 1, и уже поверх него ×1.4 HP / ×1.35 dmg / +2 брони**
  (HP `×3×1.4`, dmg `×3×1.35`, armor `базовый×3+2`; range 3, цена 2500g без изменений).
  Латентный фикс касается и Human (`Human_Titan_BONUS` теперь 2520 HP / 14 брони / 141.75–182.25 dmg
  вместо ошибочных 6/47.25). Fallback-путь `UnitStatsResolver.ResolveBase` (3× сид →
  `ApplyVeteranMultipliers`) был корректен изначально — расхождение источников устранено.
  Итог ветеранов Faceless: герои 840 HP / 6 брони / 47.25–60.75 dmg; титан 2520 / 14 / 141.75–182.25.
- **Портреты:** `UnitPortraitBaker` печёт бонус-портреты для **обеих** рас — новые папки
  `Faceless/BonusUnits/{Role}.png` (1–6) и `Faceless/BonusHeroes/{Hero1..3,Titan}.png` (7–10),
  пути `ContentAssetPaths.FacelessPortraitBonusUnits/BonusHeroes`. Запечь —
  `BARAKI/Faceless/Update Visual Catalog`.
- **Fallback снят:** `UnitVisualCatalog.TryGetBonusPortrait` больше не подменяет Faceless-портреты
  базовыми; `GetBonusPortraitFallback` удалён. Тест `BonusPortraits_AreAssignedPerRace`
  (слоты 1–10 × обе расы) закрепляет контракт.
- **Пламя в портрете:** `BonusFlameMarker.BuildFlame()` — публичный идемпотентный метод
  (очищает старые `FlamePlane_*`, работает и в edit-mode через `DestroyImmediate`);
  `UnitPortraitBaker.RenderPrefabThumbnail` вызывает его на превью-инстансе, поэтому ветераны
  Faceless отличимы от базы даже без отдельных моделей. Без `[ExecuteAlways]` — префабы не засоряются.
- **VFX маркеров 64–69:** presentation-контракт (`PresentationCatalogTests`) требует `VfxPrefab`
  на каждом def каталога — назначены тематические CFXR (Blood Shape Splash / Poison Cloud /
  Magic Poof / WW Enemy Explosion / Wind Trails / Souls Escape); тинт — из `AbilityFx.Color`.
  Дальнейший подбор — BARAKI Studio (замены сохраняются).

## Реализация слотов 11–12 (FACELESS-013, 2026-09-09)

Расовые уники (player-level, как March Discipline / Stone Masonry у Людей), без замены юнитов.

| Файл | Роль |
|------|------|
| `Combat/FacelessBonusUnitRules.cs` | `ShadowEvadeChance = 0.08f`, `VoidBastionMissChance = 0.20f`, `HasShadowOfTheVoid(player)` / `HasVoidBastion(player)` (Faceless + слот 11 / 12) |
| `Combat/MatchUnitState.cs` | `ShadowEvadeActive` (bool; host-only, флаг при спавне) |
| `Combat/MatchCombatSystem.cs` | `ApplySpawnUniqueModifiers(unit)` (+вызовы в `SpawnUnit`, `CommitPendingSpawn`, `TrySpawnFlyingBonusOnDeath`, `ResurrectUnit`, `ApplyAuthoritativeUnits`); roll в `ApplyDamage`; `RollVoidBastionMiss(buildingInstanceId)` |
| `Match/BonusPickRules.cs` | `FacelessImplementedSlots = {1..12}` (UI-оверлей: слоты доступны, не «в разработке») |
| `Tests/FacelessRaceUniqueBonusTests.cs` | 11 тестов: пик 11/12, флаг при спавне (replacement policy), герои/титан, roll evade (seed-прок), Void Bastion (меле/снаряд vs здания), race-гейт March Discipline / Stone Masonry |
| `Combat/HumanBonusUnitRules.cs` | **race-гейт** `ApplyMarchDiscipline`: только `RaceId == Human` |
| `Combat/MatchController.cs` | **race-гейт** `ResolveBuildingMaxHp`: только `RaceId == Human` |

Механики:
- **Shadow of the Void (11)**: roll в `ApplyDamage` после гейта Area of Miss (`ShadowEvadeActive &&
  RollProc(0.08f)` → `return 0f`) — весь входящий урон (юниты/герои/титан). Флаг выставляется
  при спавне (`ApplySpawnUniqueModifiers`), поэтому работает replacement policy: только юниты,
  заспавненные после пика (включая героев/титана), получают флаг. Снапшот-путь
  `ApplyAuthoritativeUnits` пересчитывает флаг — задокументированное приближение.
- **Void Bastion (12)**: протектор-перед попыткой урона зданию на двух прямых ветках
  (`ResolveMeleeImpact` / `ResolveProjectileImpact`, `RollVoidBastionMiss` → ранний return;
  пропускает и снаряд-сплэш катапульты). AoE-сплэш и способности зданиям не роллят.
  Применяется ретро + будущие здания (решает владелец здания на момент удара).

**Важно — race-гейт Human-уников:** ранее задачи уникальных слотов Людей (11 March Discipline,
12 Stone Masonry) вешались по слоту без проверки расы — Faceless с пиками 11/12 унаследовал бы
чужие бонусы. Теперь оба гейтированы расой (`RaceId == Human`), Faceless-уники не конфликтуют.

Тесты: полный EditMode-прогон `Game.Tests` — 1336 passed (2026-09-09).

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
- [x] Сидер исправлен (2026-09-08): `UnitAbilitySeeder.SeedFaceless` сидирует базовые герои/титан
      Faceless **базовыми** китами (`AbilityKitDefaults.Create(Hero, slot)` / `Create(Titan)`),
      guid-в-guid с Human-префабами; прежний прогон через `CreateForSpawn` (пустой кит из-за
      гейта FACELESS-014) затирал способности на базовых префабах. Runtime-фолбэк
      `CreateForSpawn` для не-кастера остаётся пустым (базовые киты — отдельные карточки) —
      префаб авторитетен.
- [x] Ветеранские статы/портреты/контент FACELESS-012 (2026-09-08): ветеранский синк 7–10 в
      `SyncRace` для обеих рас (фикс титан-ветерана = сид 3× + кит, касался и Human), бонус-портреты
      1–10 для Faceless, снятие `TryGetBonusPortrait`-fallback, VFX на маркерах 64–69. Тесты 1324
      passed (EditMode). Гейт `HasBonusKit` снят не был (follow-up FACELESS-014).
- [x] Снятие runtime-гейта — FACELESS-014 (2026-09-09): `NoBonusKitRaceIds` пуст;
      `HasBonusKit(RACE_FACELESS)` = true; эффективные слоты 1–6 / 7–9 / 10; `ResolveBase`
      разблокирует ветеранские множители и бонус-префабы Faceless. Слои волны и ручного
      найма race-aware (`player.RaceId` — Human без изменений). Добавлен тест числового
      трансфера ветеранов Faceless (fallback). EditMode 1336 passed. Выбор расы в лобби
      (`SelectableRaceIds`) остаётся отдельным гейтом (`GameDesign/Races.md`, EA-001).
- [x] Реализация слотов 11–12 (FACELESS-013): Shadow of the Void, Void Bastion — 2026-09-09.
- [x] Полный asymmetry kit (пассивы, кастер-кит, magic upgrades, tower-треки Faceless) — FACELESS-008, дизайн 2026-09-08 (`GameDesign/Races.md`).

Связанные правила: `wiki/rules/human-unit-bonuses.md` (формат ветеранов/китов/UI как Люди),
`wiki/rules/faceless-assets.md`, `wiki/rules/abilities.md`.