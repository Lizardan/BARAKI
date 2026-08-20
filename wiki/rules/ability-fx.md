# BARAKI Studio

Визуалы способностей настраиваются в редакторе, пишутся в `AbilityFx`
(`Color` + `VfxPrefab` + `Anchor` + `AnimKind` + `AnimState` + `AnimVariant` + `Scale` + `Euler`)
и играют в матче тем же пайплайном, что касты (`ShowAbilityFx` / пассивные ауры).
Что видно в Studio — то играет в бою.

## Окно

Меню: **`BARAKI/Abilities/BARAKI Studio`**. Быстрый доступ: кнопка **BARAKI Studio**
на верхней панели Unity и **Ctrl+Shift+F**.
`[MainToolbarElement("BARAKI Studio")]` — `AbilityFxStudioToolbarButton`.

Одно окно, три колонки и две ручки-сплиттера (ширины и свёрнутость палитры в `SessionState`):

- **Слева:** поиск и список по киту (`AbilityVfxKindRules.KitLabel`). Строка = свач + имя.
- **Центр:** живое превью (`AbilityFxPreviewSession`): слева модель владельца кита,
  справа мечник / главное здание. Кольцо в превью — **боевой радиус 1:1**
  (те же метры, что `def.Radius` и презентер юнита). Камера отъезжает, чтобы круг
  влез. Под превью: схема механики + описание, чип «аура / земля / вспышка / точка»,
  цвет, масштаб, якоря 2×2, поворот, **редактируемый радиус**, кубики клипов, Replay / Ping.
- **Справа:** палитра живых thumbs (`AbilityVfxPrefabPalette`). Не отдельное окно.
  Клик сразу пишет `VfxPrefab` в текущую способность. Ручка сворачивает палитру —
  превью на всю ширину визуала.

**Сверху палитры** (не над превью): поиск, чипы Все / Cast / Hit / Aura, ObjectField, счётчик.
Тулбар Studio — обновление индекса и toggle «Палитра». Сетка thumbs с одинаковым отступом слева и справа.

Divine Blessing спавнит главное здание из `BuildingVisualCatalog` как кастер.
Кара зданий — цель тоже Main.

Аура вешается через `AuraFxVisuals.Attach` на **unscaled** корень кастера
(как в матче: `local (0, 0.05, 0)` + `PassiveAuraFxRules.ResolveScale`).
Если на SO нет префаба — тот же фолбэк `MatchFxCatalog` Runic, что `SyncAuraDisc`.
В `PreviewRenderUtility` нет `Update`, поэтому частицы крутятся вручную:
`Pause` + `Simulate(dt * simulationSpeed, withChildren: false, restart: false)` (первый кадр — restart).
Каждый ParticleSystem тикается один раз (не `withChildren: true` по всему дереву — иначе дети бегут быстрее матча).
`dt <= 0` пропускается, сверху clamp ~0.05 с — без пола 0.001 (иначе editor-update накручивает время).
**VFX Graph** (Adjustable Slash `Slash_*`): не паузить (`pause = false`), `Reinit` + `Play`,
кадрировать `camera.Render` как ParticleSystem. Не ставить `cameraType = Game` и не звать
`PreviewRenderUtility.Render(true)` из сетки превью — preview-сцена без LightmapSettings,
URP падает с `GetManagerFromContext ... LightmapSettings is NULL`.
На время сессии culling asset-а `CullNone` (без сохранения на диск). One-shot Graph в большом
превью крутится ~0.9 с; в пикере дуга переигрывается ~0.38 с и кадрируется на пике (~0.1 с),
ortho ~0.26, после `VFXManager.PrepareCamera` проекция камеры возвращается в ortho.
После каждого `Reinit` снова тинт / lift чёрного `FirstColor`. Чипы Все / Cast / Hit / Aura
в шапке палитры не сбрасываются при смене способности.

Animator: `enabled = false`, только `Update(dt)`, иначе `camera.Render` удваивает клип.
Свет как у портретов: `lights[0]` key, `lights[1]` fill, плюс Directional Light в preview-сцене (URP Lit/Toon иначе чёрные).

Удар/каст ставится через `AbilityVfxPlacement.ApplyOneShotTransform` — те же формулы, что матч:
**На себе / На цели** parent к корню юнита (едет с моделью); **Под собой / Под целью** мир
на земле / `CenterPosition` (не едет). На себе и на цели = центр тела (`BodyHeight` 0.9 от ног),
земля = абсолютный `GroundY` 0.1, Impact = `Elevate(CenterPosition)` (порог y 0.05 → 0.6).
Корни юнитов в превью стоят на `N4PerimeterLaneGeometry.LaneHeight` (0.15), пол превью на **y = 0**,
чтобы кольца GroundY 0.1 были видны.
Летающие получают `GetModelLocalOffset` (+4 hover), герой +0.15 — как презентер.
One-shot (Frost, Resurrect, Consecration, удары…): `localScale = prefabScale × AbilityFx.Scale`
(`AbilityVfxPlacement.ResolveOneShotLocalScale`). Боевой радиус **не** множит визуал —
один префаб и один Scale выглядят одинаково на любом заклинании.
Frost: только lifetime = stun (1.5 с) и якорь Ground (`GroundY` 0.1). Стрип `Runes` убран.
Аура: `AuraFxVisuals.Attach` + `PassiveAuraFxRules.ResolveScale(radius)` × `AbilityFx.Scale`
(подгонка под halo, не one-shot). У текущих аур радиус общий, поэтому Scale+префаб совпадают между собой.
One-shot переигрывается через 3 с (`Destroy` safety-net); Frost — через stun, даже если префаб loop.
CFXR в превью глушится (`AuraFxVisuals.PrepareEditorPreview`: `clearBehavior = None`,
без shake, mute audio). Яркость огней one-shot не режется (в матче SoftenLoop только у аур).

### Анимации

Список клипов строится с контроллера модели кита (`AbilityAnimClipIndex`: layer 0, BlendTree → строка на child).
Studio пишет `AbilityFx.AnimState` (имя стейта) и `AnimVariant` (индекс child). Пустой `AnimState` →
старое поведение: `AbilityAnimRules.ResolveAnim` по `AnimKind` / `ResolveKind`, случайный A/B.

`AnimKind` выводится из стейта (Attack / Cast / None) для длительности `CastLock`.
Матч: `ApplyAbilityAnimLock` + `UnitCombatAnimatorDriver.Tick(..., stateOverride, variantOverride)` —
без random, если клип задан. Презентер читает pending casts / `CastLockAnimState` в тот же кадр, что VFX.
Трейты по-прежнему не лочат юнита в бою (прок на ударе); превью клип всё равно играет.

Replay / смена способности заново CrossFade с начала клипа. `applyRootMotion = false`.

### Палитра префабов

Встроена в Studio (`AbilityVfxPrefabPalette`), не `EditorWindow`. Первая ячейка — «нет».
ObjectField и чипы Cast / Hit / Aura / Все — в шапке самой палитры.
One-shot в палитре зацикливается; `Slash_*` кадрируются ortho ~0.48. Это разные пресеты (~8 графов),
кроме mesh-only `SlashMesh`. Виртуализация: пул сессий на видимые ячейки.

| Kind | Способности | Префабы |
|------|-------------|---------|
| Aura | пассивные ауры 13/23/33/43/50 | Hyper Casual `Area/*`, `Shine/*`; CFXR Magic Aura / LightGlow Loop / Shiny Item |
| Hit | Strike, Ultimate, Smite, Consecration, Slam, Stomp, Cleave, Deadeye, Battlemace, Catapult | Adjustable Slash `Slash_*`; CFXR Impacts / Hit / Sword Trails / Explosions / Firewall; Hyper Casual `Flash/*` |
| Cast | хилы, баффы, резы, Frost, Last Call, Divine Blessing | остальные CFXR + Sparkle/Confetti/Water/Dust |

Скан папок: `Adjustable Slash VFX Pack/Prefabs`, `JMO Assets/Cartoon FX Remaster/CFXR Prefabs`,
`Lana Studio/Hyper Casual FX/Prefabs`. ObjectField биндится к фактическому префабу и не затирает
ссылку вне трёх папок.

## Механика способности (не якорь)

`AbilityFxMechanicRules` говорит, **как бьёт способность**, чтобы выбрать эффект.
Это не `AbilityVfxAnchor` (куда спавнится префаб), а боевая форма:

| Shape | UI | Что значит | Примеры |
|-------|----|------------|---------|
| `AuraAroundSelf` | Аура вокруг себя · едет с носителем | Пассивное кольцо на герое | ауры 13/23/33/43/50 |
| `AreaOnGround` | Область на земле · остаётся | Круг штампуется в кадр каста | Greater Heal, Consecration; Frost — у скопления врагов |
| `BurstAroundSelf` | Вспышка вокруг себя | Разовый круг у кастера | Strike, Slam, Stomp, Group Heal, Shield, Cleave |
| `BurstAroundTarget` | Область у цели | Разовый круг у якоря | Holy Nova |
| `BurstAtImpact` | Вспышка в точке удара | Splash там, куда прилетело | Catapult, Кара зданий |
| `PointOnTarget` / `PointOnSelf` | без круга | Один юнит; радиус поиска — не AoE | Smite, Mend, Deadeye |

Превью рисует диск+обод в **боевых метрах** (`PreviewRingRadius` = `Radius`).
Модели — тот же `ResolveAnimatedPresenterScale`, что в матче. Frost r=5 вокруг
цели накрывает ~5 м от её ног; камера/пол подгоняются под круг.
Слайдер **Радиус** в Studio пишет `UnitAbilityDef.Radius` (Undo + dirty). Mend/Resurrect
правят `CastRange` («Досягаемость»). `Radius = 0` по-прежнему фолбэк на кит.
**Build Ability Defs сохраняет** ненулевой `Radius` / `CastRange` / `SecondaryRadius`.
Smite и Mend **не** показывают круг: их `Radius`/`CastRange` — досягаемость, не AoE.

## Якорь спавна (`AbilityVfxAnchor`)

Где играет эффект и **следует ли он за моделью**. Внутри те же 4 enum-значения (без миграции ассетов).
Studio показывает сетку 2×2 и точки в превью (клик = тот же выбор).

| Значение | UI | Поведение | Примеры сида |
|----------|-----|-----------|----------------|
| `Unspecified` (0) | — | ещё не задан; runtime/Studio берут `ResolveDefaultAnchor` | существующие ассеты до первой записи |
| `Caster` | **На себе** | Parent к корню кастера, центр тела. Едет с моделью. | ауры, Strike, Slam, Stomp, Ultimate, Cleave |
| `Target` | **На цели** | Parent к цели, центр тела. Едет с целью. | Smite, Mend, Deadeye, Кара юнитов |
| `Ground` | **Под собой** | Мир, `GroundY` у ног кастера в кадр каста. Не едет. | Frost, Consecration |
| `Impact` | **Под целью** | Мир, `cast.CenterPosition` (splash / спавн / здание). Не едет. | Catapult, Last Call, Кара зданий |

`Unspecified` нужен потому что `Caster` не может быть 0: иначе нельзя отличить
«пользователь выбрал кастера» от «поле ещё пустое». Сид не затирает уже сохранённый якорь
(`AbilityFx.WithPreservedAuthored`). Ауры в матче по-прежнему parent к юниту (`AuraFxVisuals.Attach`);
кнопки якоря в Studio у аур скрыты.

Превью Impact: Last Call → ноги кастера (root, не hover); Catapult / Кара зданий → ноги цели.

## Поворот (`AbilityFx.Euler`)

Градусы local euler. `(0,0,0)` = как в префабе. `WithPreservedAuthored` сохраняет ненулевой euler
(как Scale: ноль = не задан). Спавн: `localRotation = Quaternion.Euler(Euler) * prefab.rotation`.
Пресеты Studio: Горизонталь `(0,0,0)`, Вертикаль `(0,0,90)`, В пол `(90,0,0)`.

## Данные

- Активки и пассивы юнитов: `UnitAbilityDef.Fx`. **Build Ability Defs сохраняет** уже назначенные
  `Color` / `VfxPrefab` / `Anchor` / `AnimKind` / `AnimState` / `AnimVariant` / `Scale` / `Euler`
  (`AbilityFx.WithPreservedAuthored`). Ненулевые `Radius` / `CastRange` / `SecondaryRadius`
  тоже сохраняются (слайдер Studio). Хардкод CFXR в билдере — только сид, если префаб null.
  Якорь и `AnimKind` сидятся через `ResolveDefaultAnchor` / `ResolveKind`, если ещё `Unspecified`.
  Пустой `AnimState`, `Scale == 0` и `Euler == 0` не затирают уже заданные значения, но и не сидятся сами.
  Геройские ауры 13/23/33/43 сидятся тем же Runic, что `MatchFxCatalog._auraRunicLoop`.
- Divine Blessing 100/101: `MainExtraAbilityFxCatalog` (`Assets/Game/Resources/Fx/MainExtraAbilityFxCatalog.asset`,
  `Resources.Load("Fx/MainExtraAbilityFxCatalog")`). `MainExtraAbilityFxDefs` подмешивает FX при `Get`.
  Сид: Кара зданий = CFXR3 Fire Explosion B, Кара юнитов = CFXR Hit A (Red). Пустой префаб досиживается при открытии Studio.
- Тинт: `Game.Gameplay.Vfx.AbilityVfxTint` — ParticleSystem (HSV-retint) + Visual Effect Graph
  (`FirstColor` / `SecondColor` / `ThirdColor`). В Studio one-shot: `Apply` после `Play`/`Reinit`
  (Graph сбрасывает exposed colors; частицы красятся один раз на спавне, не каждый тик).

Building destroyed / кровь / Titan body rays живут в `MatchFxCatalog`, не в этом окне.

## Рантайм

- Каст: `MatchCombatPresenter.ShowAbilityFx` → instantiate `VfxPrefab`,
  `AbilityVfxPlacement.ApplyOneShotTransform` (parent Caster/Target, мир Ground/Impact),
  `AbilityVfxTint.Apply`, `localScale = prefabScale × AbilityFx.ResolveScale` (в т.ч. Frost — без × radius).
  One-shot живёт 3 с; Frost — `StunSeconds` (1.5). Ground-якорь = `GroundY` 0.1.
  Studio использует те же константы/формулы. Impact в презентере = `Elevate(cast.CenterPosition)`.
- Пассивные ауры: префаб и цвет с `AbilityCatalog.Find(abilityId).Fx`, fallback на `MatchFxCatalog` Runic +
  `PassiveAuraFxRules`. Всегда на носителе.
- Трейты эмитят `EmitCast` в тот же `SpellCasts` снапшот: Cleave (прок), Deadeye (крит), Battlemace
  (hybrid melee hit), Last Call (спавн после смерти), Catapult (splash на прилёте). Примитивный splash-диск
  катапульты больше не рисуется — играет префаб способности.
