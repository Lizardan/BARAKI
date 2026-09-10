# Faceless (Древние) — review-ассеты и runtime-гейты

Раса в UI — **Древние**. Папки и id — **Faceless** (`RACE_FACELESS`).

## Статус (Фаза 1–2 завершены)

Раса **играбельна** в playtest-гите: `PlayableRaceIds`/`SelectableRaceIds` содержат
`RACE_FACELESS` (`RacePickRules`), контент (SO, префабы, каталоги, портреты) собран.
ХакnPlan-задача FACELESS-009 фиксирует runtime-гейты; дизайн бонусов — FACELESS-010
(юнит за юнитом с пользователем, **завершён** — канон `wiki/rules/faceless-unit-bonuses.md`;
реализация — follow-up карточки). Work items в HacknPlan — Urgent.

### Runtime-гейты Фазы 1 (закрыты)

- **Кастер-кит**: `AbilityKitDefaults.CreateForSpawn(string raceId, ...)` для
  `RACE_FACELESS` возвращает **пустой кит** (способностей у расы нет). Оба вызова в
  `MatchCombatSystem` — race-aware через `GetPlayerRaceId(unit.OwnerSlot)`, иначе
  Faceless-кастер унаследовал бы Human-кит из fallback `CreateForSpawn`.
- **Бонус-пик**: Faceless не имеет бонус-кита. `BonusKitRules.HasBonusKit(raceId)`
  = false для `RACE_FACELESS` (список `NoBonusKitRaceIds`). Race-aware
  `EffectiveBonusSlotForRole/Hero/Titan(string raceId, ...)` всегда дают 0; 7 вызовов в
  `MatchController` прокидывают `player.RaceId`; `UnitStatsResolver.ResolveBase`
  охраняет bonus/veteran-ветки. Иначе юнит-бонус давал базового юнита, а veteran —
  множители (×1.4/×1.35/+2).
- **Tower-треки** (`TowerTrackRules`): глобальные стат-эффекты по ролям, способностей
  не дают — работают для Faceless без изменений (не блокер).
- **Titan**: SO-ассета нет ни у одной расы (у Human тоже) — титан = `CopyFrom(hero1,
  TitanRules.BaseStatMultiplier=3f, AttackRange=3f)`, это норма.

Раскладка — как у людей (единый шаблон расы): production-префабы в
`Prefabs/Races/Faceless/{Units,Heroes}/{Role}/Faceless_{Role}.prefab`, контроллер
лежит **рядом** с префабом (`Faceless_{Role}.controller`), клипы/меши/материалы —
в `Art/Races/Faceless/Production/{Anim,Meshes,Mats}/`. Ревью-сборка — отдельно:

```text
Art/Races/Faceless/          # OBJ + PNG из WC3 MDX/BLP
Art/Races/Faceless/Meshes/   # skinned Mesh из MDX (review)
Art/Races/Faceless/Anim/     # review Stand/Walk/Attack .anim + .controller (01_…12_)
Art/Races/Faceless/Production/
├── Anim/                    # production клипы (Stand/Walk/Attack/Death/Cast)
├── Meshes/                  # production skinned Mesh
└── Mats/                    # production материалы
Prefabs/Races/Faceless/
├── Units/{Role}/Faceless_{Role}.prefab + Faceless_{Role}.controller
└── Heroes/{HeroN,Titan}/Faceless_{…}.prefab + .controller
Scenes/Dev/FacelessReview.unity   # не в Build Settings
```

Конвертер статики: `Tooling/MdxReview/convert_mdx_to_obj.py` (MDX v800).
Скин + клипы: меню `BARAKI/Faceless/Rebuild Review Anims` (`FacelessReviewAnimBuilder`).
На `FacelessReview` в Play — кнопки Idle / Бег / Удар (клавиши 1/2/3) через
`Animator.CrossFade` (как боевые юниты), без AnyState. Team color на просмотре —
слот 1 (синий).

MDX держат **несколько слоёв** на материал (WC3 `LAYS`): снизу replaceable
**team color** (цвет слота игрока), сверху диффуз с альфой. Где альфа диффуза = 0,
просвечивает цвет игрока — это не вырезы. Старый `clip(luma)` дырявил тёмную
броню и кожу. Review-шейдер `Game/Faceless/ReviewUnlit`: `lerp(albedo, _TeamColor, 1-a)`,
без luma-клипа. FilterMode Transparent (1) без team color — **alpha clip**, но не
для скина `FacelessOneUnbrokenV2`: JPEG-альфа дырявит тело (`08_FacelessKing`).
FilterMode Blend (2) без team color — полупрозрачность. Cutout — чужие WC3-текстуры
(крылья, волосы), не V2.

Геосеты **только из replaceable team color** (без диффуза) — **сохраняются** как
чисrый team color: стем `TeamColor` (белая текстура + `_BaseColor` слота),
`twoSided=true`. Это не подложка под FX, а настоящая геометрия — середина рог
`08_FacelessKing` (12 tris). Единственный такой геосет во всех 12 моделях; FX-подложки
(без диффуза и без team) по-прежнему отсекаются по `IsFxTexture`/JUNK.
`HeroAvatarFlame` на короле — **не FX**, а kitbash (броня / лава / рога с атласа);
без этих геосетов в теле дыры. Альфу JPEG не клипать (как V2). Пустой Magos-фон
атласа `(130,147,178)` почти как небо review-сцены — `_AtlasClip` перекрашивает
его в тёмную бирюзу (не `clip`: иначе дыры).

**Прозрачные треугольники V2-скина** (`FacelessOneUnbrokenV2`, крупные геосеты,
не team/blend/clip) расщепляются при сборке в два submesh'а: непрозрачная часть
+ clip-часть (`twoSided=true`, `ClipBlack=true`) по альфе центроида UV < 0.45
(`SplitV2TransparentTriangles` в `FacelessReviewAnimBuilder`). Так JPEG-дыры
(наплечники `01`, рога/шипы `08`, лицо `11`) становятся вырезом вместо чёрных
пятен, тело остаётся целым. Мелкие V2-геосеты FilterMode 1
(<80 вершин: грива/цепи/шипы) — cutout на уровне геосета, иначе чёрные карточки атласа.
Текстуры WC3/V2 — `Wrap Repeat`. `rootBone` — `Bone_Root`, не `gutz`.

**Наконечник копья короля**: `ForgottenOne.png` регион U[0.74,1.0] × V[0,0.29]
перекрашен в стальной серый (luma-нейтральный, idempotent) — меню
`BARAKI/Faceless/Recolor ForgottenOne Tip`; вызывается и в `Rebuild Review Anims`
до `AssetDatabase.Refresh`. `ForgottenOne` использует только `FacelessKing`.

Цвета: `05–07` колдуны почти целиком на оригинальных WC3-атласах
(Guldan / Priestess / …). `01/02/03/04/08–12` — в основном на `FacelessOneUnbrokenV2`
(бирюзовый ретекстур пака). Это не баг декода BLP.
Скин V2 и flame-kitbash на review **двусторонние** (`Cull Off`).

Материалы после `CreateAsset` нужно сразу `LoadAssetAtPath`, иначе Unity пишет
белый `_BaseMap` (fileID 0) — «сломанные» белые модели. Меши — `ImportAsset`
синхронно до `SaveAsPrefabAsset`, иначе у префаба `sharedMesh = null`.

JPEG BLP1 — 4 компоненты inverted BGRA (PIL видит CMYK). Декод:
`Tooling/MdxReview/blp1.py`. WC3 PNG: `extract_wc3_textures.py` из локального
`G:\Games\Warcraft III iCCup\*.mpq` в `Art/Races/Faceless/Wc3/` — только для
review, не класть в клиентский билд.

Исходники остаются в `Assets/FacelessRetexture_V2` до финализации.

Нумерация:

| # | Файл | Префаб |
|---|------|--------|
| 01 | FacelessOne_G | 01_FacelessOne |
| 02 | FacelessOneBerserker_G | 02_FacelessOneBerserker |
| 03 | RangedFacelessone_G | 03_RangedFacelessone |
| 04 | FacelessOneReaper_G | 04_FacelessOneReaper |
| 05–07 | FacelessOneSorcerer_G1..G3 | 05–07 |
| 08 | FacelessKing_G | 08_FacelessKing |
| 09 | FacelessThanatos_G | 09_FacelessThanatos |
| 10 | Unbroken_Izual | 10_Unbroken_Izual |
| 11 | FacelessOneWorker_G | 11_FacelessOneWorker |
| 12 | FacelessOneWorker_G_Portrait | 12_FacelessOneWorker_Portrait (WC3-голова) |

Маппинг номер→роль выполнен (`AssetDatabase.MoveAsset` в категории как у людей,
`_Review` удалён). Раса играбельна — раскладка сделана в `Prefabs/Races/Faceless/`
(`Units/`, `Heroes/`; bonuses — дизайн в FACELESS-010, канон `wiki/rules/faceless-unit-bonuses.md`,
реализация follow-up), каталоги зарегистрированы,
production-контроллеры лежат рядом с префабами.

При пересборке production-префабов (`BARAKI/Faceless/Rebuild Unit Prefabs`) контроллеры
создаются рядом с префабом (`Faceless_Melee.controller` и т.д.), а устаревшие
`.controller` из `Art/Races/Faceless/Production/Anim/` удаляются — клипы не трогаются.

### FACELESS-006 — верификация анимаций и тайминги (done)

Все 10 production-префабов верифицированы (Edit mode, Editor-замер): у каждого
`Animator` → свой `Faceless_{Role}.controller` **рядом** с префабом, `applyRootMotion=false`,
`SkinnedMeshRenderer.sharedMesh` загружен, на корне `FacelessUnitTeamColor` +
`UnitCombatSettings`. Состояния всех контроллеров непустые и привязаны к production-клипам:
`Stand / Walk / Attack / Death / Cast` (`Production/Anim/{Model}_{State}`).

Поликаунт — приемлемо для RTS-камеры, декimation не требуется (max — Titan/`10`):
`10_Unbroken_Izual` 2843 verts / 2312 tris, остальные 0.37–1.15k verts
(самый лёгкий — `03_RangedFacelessone` 369 / 295).

Длительности Attack-клипов (факт, из `Production/Anim`, 30 fps):

| Роль | Модель | Attack |
|------|--------|--------|
| Melee | 11 | 0.99s |
| Ranged | 03 | 1.49s |
| Caster | 05 | 1.06s |
| Siege | 01 | 0.96s |
| Flying | 09 | 0.99s |
| Super | 04 | 0.99s |
| Hero1 (King) | 08 | 0.96s |
| Hero2 | 07 | 1.06s |
| Hero3 | 02 | 1.16s |
| Titan | 10 | 1.32s |

**Race-aware тайминги удара:** клипы Faceless из MDX v800 короче TT_RTS (1.5s/1s),
поэтому `AbilityAnimRules.ResolveAttackClipSeconds(role, heroSlot, bonusSlot, raceId)` для
`raceId == RACE_FACELESS` возвращает **фактическую** длину клипа (таблица выше), не
изменяя Human-результат. `MatchCombatPresenter.DriveAnimator` прокидывает raceId через
`ResolveRaceId(unit, controller)` (`players[ownerSlot].RaceId`, fallback `RACE_HUMAN`).
Иначе `Animator.speed = len/interval` гнал бы клип за ~2/3 интервала и рассинхронизировал
`ResolveSwingImpactDelay`.

### FACELESS-006b — размер и ориентация в бою (baked в префаб, эталон со сцены)

Проблема: production-префабы Древних по габаритам/позе не совпадали с Human и модель
могла стоять с наклоном. Размер выверен вручную на тестовой сцене
`Scenes/Dev/Faceless_ScaleTest.unity` (эталон: `Human_Melee` scale 1.0/rot 0, рядом
все Faceless), значения запечены в префаб на сборке — рантайм не трогали.

- **Масштаб** — `ResolveFacelessRoleScale(role)` в `FacelessReviewAnimBuilder`
  (вместо прежней нормировки `ResolveFacelessHeightScale` к Human-высоте):

  | Роль | prefab localScale |
  |------|-------------------|
  | Melee | 1.75 |
  | Ranged / Caster / Siege / Super | 1.5 |
  | Flying | 1.0 |
  | Hero1 / Hero2 / Hero3 / Titan | 2.0 |

  `instance.localScale = prefab.localScale * presenterScale` (равно для обеих рас) —
  множители сохраняются и в бою.
- **Ориентация** — `root.transform.rotation = Quaternion.Euler(0, 270, 0)` в
  `BuildProductionPrefab` (сразу после `CreateSkeleton`, до BuildMesh — bindPose
  корректен). Вместо прежнего `ResolveModelCorrection` (спина→+Y + лицо→+Z):
  теперь модель просто стоит ровно и смотрит в ту же сторону, что и Human
  (`GetAnimatedHumanModelEuler`-yaw). `ResolveModelCorrection`/`ResolveTargetHeight`/
  `ResolveFacelessHeightScale` удалены как неиспользуемые.

Верифицировано после пересборки (инстанс префаба): у всех ролей euler = (≈0, 270, ≈0),
localScale = таблице, высоты 1.6 (Melee)…4.3 (Titan). Консоль чистая; тесты —
1263 passed (`Game.Tests`, EditMode). Тестовая сцена после фикса удалена.

**Источник масштаба с 2026-09** — размер юнита теперь одно число в `UnitDefinition.VisualScale` /
`HeroDefinition.VisualScale` и равен **скейлу в игре** (никаких перемножений в рантайме):
значение = prefab root scale × role-фактор (`UnitGreyboxVisuals.ResolveAnimatedPresenterScale(role)`:
крипы ×1.25, герой ×1.4375, титан ×3.75). Примеры: Faceless Melee = 1.75×1.25 = **2.188**,
Faceless Servant = 1.25×1.25 = **1.563**, Human Melee = 1.0×1.25 = **1.25**,
Faceless герой = 2.0×1.4375 = **2.875**, титан Faceless = 2.0×3.75 = **7.5**.
Запекание: `BARAKI/Units/Assign Visual Scales (from prefabs)` заполняет только путые (0) —
ручной тюнинг не трогает; `BARAKI/Units/Bake Visual Scales (final, role-multiplied)` — принудительно
пересчитывает все с нуля (одноразовая миграция). В бою
`UnitGreyboxVisuals.ResolveAuthorVisualScale` берёт `VisualScale` из def (приоритет unit, затем
hero; если &gt; 0) — иначе fallback на prefab root scale; presenter-множитель к самому юниту больше
не применяется (остался только в VFX-плейсменте Titan-body-Rays). На инспекторе префаба карточка
статов показывает «Визуальный масштаб (в бою)» **read-only** (источник: ассет или префаб);
правка — только в ассете через кнопку «Открыть». Правка масштаба
юнита — только в ассете, префаб не трогаем; `ResolveFacelessRoleScale` остался для rebuilt-префабов.
`Faceless_Servant` (scale 1.563): меш/материалы как у Melee, оружие скрыто через
`AxHandle01_16` scale 0; самостоятельный ассет меша удалён.

Контроллеры, клипы и материалы не менялись. Rebuild: `BARAKI/Faceless/Rebuild Unit
Prefabs` (`RebuildProductionPrefabs`).

### FACELESS-006c — кастер «лежал» в Walk + сброс статов при пересборке (fixed)

Два бага, найденные после пересборки production-префабов:

- **Кастер лежал в анимации бега**: у `FacelessOneSorcerer_G1.mdx` кость `Bone_Root`
  (в префабе `Bone_Root_26`) не имеет ключей поворота внутри Walk-интервала
  (23900–24567 мс). `BakeQuatCurves` сэмплил **глобальный** список ключей кости
  (Death roll 131°…Decay Flesh 131°), получая постоянный наклон ~131° на всех
  кадрах walk-клипа. Фикс (`BakeQuatCurves` в `FacelessReviewAnimBuilder`):
  ключи фильтруются по `[start,end]` последовательности; пустой интервал →
  `Quaternion.identity`. Аналогично `BakeVectorCurves` (пусто → restPos/fallback).
  У Ranged (`RangedFacelessone_G.mdx`) ключи корня в walk-интервале **есть** — его
  ~30° наклон это данные модели, не баг.
- **Ренджер/кастер/flying не стреляли на дистанции**: `BuildProductionPrefab`
  пересоздаёт префаб через `DeleteAsset` + `SaveAsPrefabAsset`, что меняет GUID и
  **рвёт ссылки `UnitVisualCatalog`** (NULL) на эти префабы. Поэтому
  ре-присвоение статов (`UnitStatsSourceAssigner.AssignRace`) не находило префабы и
  статы оставались дефолтными (range=1.5). Фикс: в конце `RebuildProductionPrefabs`
  перед `AssignRace()` вызывается `UnitVisualPrefabBuilder.UpdateFacelessCatalog()`
  (пере-привязка каталога + переснапка портретов). После пересборки префаб ссылается
  на `RaceCatalog`-default (Ranged range=8, Caster range=6/mana=200, Flying range=6,
  Super range=10, Hero3 range=12, Titan hp=1800/range=3).

Верифицировано: walk-клип кастера — корень identity (`maxAbs Z-roll=0`), каталог
ссылается на все 10 префабов, консоль чистая, тесты 1263 passed (EditMode).

Play-скриншот review-раскладки: `Assets/Screenshots/screenshot-20260907-030915.png`
(в `FacelessReview` **нет Animator** — это статичная раскладка мешей, runtime-прогон
боевых анимаций там не применим; верификация по данным выше).

### FACELESS-006d — Super и Flying Древних = ближний бой (mechanica via `UnitCombatIdentity`)

Решение: **полностью ближний бой без арт-лимитов** для Faceless Super (крип) и Flying.
Атака — меле-удар (`CombatMeleeStrikeState`, без снаряда), минимальная дистанция Super
сброшена (0, бьёт вплотную), Flying по-прежнему может атаковать **летающие** цели.

Реализация — новая модель «роль+раса», `UnitRole` не тронут (не сломать
wire/снапшот-контракты):

- `Data/UnitCombatIdentity.cs` — readonly struct `{RaceId, Role, IsHero, HeroSlot,
  BonusSlot}` + методы правил: `UsesMeleeStrike`, `UsesProjectile`,
  `GetMinAttackRange()`, `CanAttackTarget(targetRole)`.
  Faceless Super/Flying → melee delivery, min-range Super = 0, Faceless Flying бьёт
  по летающим. Human роли — без изменений (делегирование в `CombatRules`/`CombatAttackRules`).
- `Combat/UnitCombatIdentityFactory.cs` — построение из `MatchUnitState` + raceId.
- `MatchCombatSystem.IdentityOf(unit)` — единый хелпер (race через
  `GetPlayerRaceId(ownerSlot)`); все проверки `UsesMeleeStrike`/`UsesProjectile`/
  `CanAttackTarget`/`IsWithinAttackBand`/building-band переведены на identity.
- `MatchCombatPresenter` — `UnitVisual.RaceId` (из `ResolveRaceId`); swing-impact
  тайминг меле (`ResolveSwingImpactDelay(interval, identity)` — Faceless Super не
  release-0.1, а mid-swing 0.5); `SpawnDeathFx` для Faceless Super — Blood
  (не `MachineDestroyed`).
- Статы `UNIT_FACELESS_SUPER` / `UNIT_FACELESS_FLYING`: `_attackRange` 10/6 → **1.5**
  (меле). Правка вносится **прямо в UnitDefinition-ассеты** (`ScriptableObjects/Races/Faceless/…`)
  и применяется автоматически: префабы не хранят чисел, `UnitStatsResolver` читает
  `UnitCombatSettings.UnitDefinition` (приоритет), каталог — fallback;
  консистентность ссылки и деф-ассета обязательна (при сбое каталога Super не должен
  вернуться в артиллерию range=10).

Тесты: `UnitCombatIdentityTests` (9 кейсов). Тесты EditMode: **1272 passed**.

> ⚠️ Faceless Super — **наземный** меле: летающие цели не атакует (это правило).
> Снаряды/аммо-объекты для него не создаются (`AmmoObjects` = null).

### FACELESS-007 — здания Древних (Nazjatar Houses, done)

Исходники — `Assets/Nazjatar_Houses_By_Ageron/` (5 MDX v800 статик-зданий + 35 BLP1;
вложенная папка «Сжатые … текстуры» — дубликаты меньшего размера, **не используются**).
Зданий в Faceless-паке юнитов не было — раса #2 использует собственный building set.

Пайплайн:

- **Текстуры:** `Tooling/MdxReview/convert_nazjatar_buildings_textures.py` (BLP1→PNG по
  TEXS-ссылкам MDX) → `Art/Races/Faceless/Buildings/Review/Textures/` (35 PNG, 512²).
- **Review:** меню `BARAKI/Faceless/Rebuild Building Review` (`FacelessBuildingReviewSetup`) —
  статик-меши через `FacelessMdxDocument` (геосеты без скиннинга, submesh на материал,
  flip winding), материалы `Game/Faceless/ReviewUnlit`, review-префабы
  `Prefabs/Races/Faceless/_ReviewBuildings/01..05_FacelessBuilding`.
- **Production:** меню `BARAKI/Faceless/Rebuild Building Prefabs` (`FacelessBuildingPrefabSetup`)
  → `Art/Races/Faceless/Buildings/Production/{Meshes,Mats}/` + префабы
  `Prefabs/Races/Faceless/Buildings/{Faceless_TownHall,Faceless_Tower,Faceless_Barracks}.prefab`.

Маппинг (решение пользователя по нумерованной review-сцене): **04 = Main (TownHall),
05 = Tower, 03 = Barracks**; 01/02 не используются. Масштаб/высота выверены пользователем
на сцене `Scenes/Dev/TestBuildings.unity` и запечены в билдер: TownHall (4, 2.5, 4),
Tower (3, 2.5, 3), Barracks (6, 4, 6); yaw модели **270°** (как у юнитов Faceless — WC3
фронт совпадает с Human после поворота).

Контракт префаба — как у TT-зданий: корень + `Model` (MeshFilter/MeshRenderer, скейл/yaw
запечены, скейл корня 1) + `Foundation` (**процедурный каменный цоколь** — Cube по
`MatchPickFootprint.GetBuildingDiameter(id, margin:1)×0.72`, inactive; **не Cylinder** —
иначе `BuildingRuinsVisual.RemoveLegacyCylinderFoundation` удалит его) +
`FacelessUnitTeamColor` (командный цвет через MPB `_TeamColor` в review-материалах).
Руины: `ApplyRuins` прячет `Model`, показывает `Foundation`; скейл корня 1 — серые
`CreateBuildingMarker`-мутации не трогают запечённый масштаб.

**Race-keyed каталог:** `BuildingVisualCatalog.TryGetPrefab(buildingId, raceId)` — race set
`RACE_FACELESS` в `Resources/Buildings/BuildingVisualCatalog.asset`
(меню `BARAKI/Faceless/Register Building Catalog`); раса без set'а или с пустым слотом —
**fallback на Human** (default set `_main/_tower/_barracks`). Прокидка: `MatchRuntime.StartMatch`
(`config.RaceIds`) → `MatchArenaGreybox.Configure(playerCount, raceIds, radius)` →
`MatchArenaGreyboxBuilder.Populate(+raceIds, +catalog)` → `CreateBuildingMarker(raceId)`.
До `StartMatch` (Awake greybox, preview) — Human. `GameIds.Buildings.SetFaceless =
"BUILDING_SET_FACELESS"`.

Тесты: `BuildingVisualCatalogRaceTests` (6 кейсов: race set / fallback / routing / Populate).
EditMode **1278 passed**. Скрин production vs Human:
`Assets/Screenshots/faceless_production_vs_human.png` (Tower Faceless парит ~0.4 — глянуть
при тюнинге; значения — константы в `FacelessBuildingPrefabSetup.Buildings`).
