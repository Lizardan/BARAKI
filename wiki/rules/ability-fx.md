# Ability FX Viewer

Визуалы способностей настраиваются в редакторе, пишутся в `AbilityFx`
(`Color` + `VfxPrefab` + `Anchor` + `AnimKind` + `AnimState` + `AnimVariant` + `Scale`)
и играют в матче тем же пайплайном, что касты (`ShowAbilityFx` / пассивные ауры).
Что видно во вьюере — то играет в бою.

## Окно

Меню: **`BARAKI/Ability FX Viewer`** (также `BARAKI/Abilities/Ability FX Viewer`).
Кнопка **Ability FX** на верхней панели Unity — static factory
`[MainToolbarElement]` на методе (не наследование sealed `MainToolbarButton`).

Раскладка **master-detail**:

- **Слева:** поиск и список, сгруппированный по киту (`AbilityVfxKindRules.KitLabel`).
  Строка = цветовой свач + имя. Клик выбирает одну способность.
- **Справа:** одно живое превью (`AbilityFxPreviewSession`):
  слева **реальная модель** владельца кита (`UnitVisualCatalog` /
  `AbilityFxPreviewCasterRules`), справа **мечник** (Human Melee) или
  **главное здание** для Кара зданий. Подписи поверх RT. ЛКМ — орбита, колёсико — зум.
- Настройки: RGB, **сетка живых превью** префаба (`AbilityVfxPrefabPickerWindow`),
  слайдер **Масштаб** (0.25–8, `0` на ассете = 1×), **Кастер / Цель / Земля / Удар**,
  dropdown **клипа из AnimatorController этого юнита**, Replay.
  У аур якорь скрыт («всегда на носителе»). Здание без аниматора — «нет клипов».

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
На время сессии culling asset-а `CullNone` (без сохранения на диск). One-shot Graph крутится ~0.9 с.
Пикер кадрирует Slash ortho ~0.48 (не perspective с дистанции 6 м — дуга схлопывалась в точку).
После каждого `Reinit` снова тинт / lift чёрного `FirstColor`.

Animator: `enabled = false`, только `Update(dt)`, иначе `camera.Render` удваивает клип.
Свет как у портретов: `lights[0]` key, `lights[1]` fill, плюс Directional Light в preview-сцене (URP Lit/Toon иначе чёрные).

Удар/каст спавнится в точке якоря теми же формулами, что матч (`AbilityVfxPlacement`):
кастер = ноги (`Root.position`), цель = ноги + `BodyHeight` 0.9, земля = абсолютный `GroundY` 0.1,
Impact = `Elevate(CenterPosition)` (порог y 0.05 → 0.6).
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
Вьюер пишет `AbilityFx.AnimState` (имя стейта) и `AnimVariant` (индекс child). Пустой `AnimState` →
старое поведение: `AbilityAnimRules.ResolveAnim` по `AnimKind` / `ResolveKind`, случайный A/B.

`AnimKind` выводится из стейта (Attack / Cast / None) для длительности `CastLock`.
Матч: `ApplyAbilityAnimLock` + `UnitCombatAnimatorDriver.Tick(..., stateOverride, variantOverride)` —
без random, если клип задан. Презентер читает pending casts / `CastLockAnimState` в тот же кадр, что VFX.
Трейты по-прежнему не лочат юнита в бою (прок на ударе); превью клип всё равно играет.

Replay / смена способности заново CrossFade с начала клипа. `applyRootMotion = false`.

### Пикер префаба

Кнопка «Эффект» открывает `AbilityVfxPrefabPickerWindow`: сетка крупных квадратов (макс. 5 колонок),
над каждым имя, внутри живой `AbilityVfxThumbSession`. One-shot в пикере **зацикливается**
(`ParticleSystem.loop` + local space; VFX Graph — `Play` каждые ~0.9 с). `Slash_*` кадрируются
отдельным ortho ~0.48 спереди, чтобы дуга заполняла клетку. Это **разные пресеты** (~8 графов,
свои текстуры/цвета/параметры), не дубликаты: в пикере остаются все, кроме mesh-only `SlashMesh`.
Поиск по имени, чипы Cast / Hit / Aura / Все. Виртуализация: пул ~20 сессий на видимые ячейки.
Первая ячейка — «нет». `ObjectField` остаётся запасным путём. Палитра (фильтр, не поле на SO):

| Kind | Способности | Префабы |
|------|-------------|---------|
| Aura | пассивные ауры 13/23/33/43/50 | Hyper Casual `Area/*`, `Shine/*`; CFXR Magic Aura / LightGlow Loop / Shiny Item |
| Hit | Strike, Ultimate, Smite, Consecration, Slam, Stomp, Cleave, Deadeye, Battlemace, Catapult | Adjustable Slash `Slash_*`; CFXR Impacts / Hit / Sword Trails / Explosions / Firewall; Hyper Casual `Flash/*` |
| Cast | хилы, баффы, резы, Frost, Last Call, Divine Blessing | остальные CFXR + Sparkle/Confetti/Water/Dust |

Скан папок: `Adjustable Slash VFX Pack/Prefabs`, `JMO Assets/Cartoon FX Remaster/CFXR Prefabs`,
`Lana Studio/Hyper Casual FX/Prefabs`. ObjectField биндится к фактическому префабу и не затирает
ссылку вне трёх папок.

## Якорь спавна (`AbilityVfxAnchor`)

Где играет эффект, а не эвристика `TargetUnitId > 0`.

| Значение | Где | Примеры сида |
|----------|-----|----------------|
| `Unspecified` (0) | ещё не задан; runtime/viewer берут `ResolveDefaultAnchor` | существующие ассеты до первой записи |
| `Caster` | у ног / вокруг кастера | ауры, Strike, Slam, Stomp, Ultimate, Cleave |
| `Target` | на цели | Smite, Mend, Deadeye, Кара юнитов |
| `Ground` | кольцо на земле у кастера | Frost, Consecration |
| `Impact` | `cast.CenterPosition` (splash / спавн / здание) | Catapult, Last Call, Кара зданий |

`Unspecified` нужен потому что `Caster` не может быть 0: иначе нельзя отличить
«пользователь выбрал кастера» от «поле ещё пустое». Сид не затирает уже сохранённый якорь
(`AbilityFx.WithPreservedAuthored`). Ауры в матче по-прежнему parent к юниту (`AuraFxVisuals.Attach`).

Превью Impact: Last Call → ноги кастера (root, не hover); Catapult / Кара зданий → ноги цели.

## Данные

- Активки и пассивы юнитов: `UnitAbilityDef.Fx`. **Build Ability Defs сохраняет** уже назначенные
  `Color` / `VfxPrefab` / `Anchor` / `AnimKind` / `AnimState` / `AnimVariant` / `Scale`
  (`AbilityFx.WithPreservedAuthored`). Хардкод CFXR в билдере — только сид, если префаб null.
  Якорь и `AnimKind` сидятся через `ResolveDefaultAnchor` / `ResolveKind`, если ещё `Unspecified`.
  Пустой `AnimState` и `Scale == 0` не затирают уже заданные значения, но и не сидятся сами.
  Геройские ауры 13/23/33/43 сидятся тем же Runic, что `MatchFxCatalog._auraRunicLoop`.
- Divine Blessing 100/101: `MainExtraAbilityFxCatalog` (`Assets/Game/Resources/Fx/MainExtraAbilityFxCatalog.asset`,
  `Resources.Load("Fx/MainExtraAbilityFxCatalog")`). `MainExtraAbilityFxDefs` подмешивает FX при `Get`.
  Сид: Кара зданий = CFXR3 Fire Explosion B, Кара юнитов = CFXR Hit A (Red). Пустой префаб досиживается при открытии вьюера.
- Тинт: `Game.Gameplay.Vfx.AbilityVfxTint` — ParticleSystem (HSV-retint) + Visual Effect Graph
  (`FirstColor` / `SecondColor` / `ThirdColor`). Во вьюере one-shot: `Apply` после `Play`/`Reinit`
  (Graph сбрасывает exposed colors; частицы красятся один раз на спавне, не каждый тик).

Building destroyed / кровь / Titan body rays живут в `MatchFxCatalog`, не в этом вьюере.

## Рантайм

- Каст: `MatchCombatPresenter.ShowAbilityFx` → instantiate `VfxPrefab` в точке `AbilityVfxPlacement.ResolveWorld`,
  `AbilityVfxTint.Apply`, `localScale = prefabScale × AbilityFx.ResolveScale` (в т.ч. Frost — без × radius).
  One-shot живёт 3 с; Frost — `StunSeconds` (1.5). Ground-якорь = `GroundY` 0.1.
  Viewer использует те же константы/формулы. Impact в презентере = `Elevate(cast.CenterPosition)`.
- Пассивные ауры: префаб и цвет с `AbilityCatalog.Find(abilityId).Fx`, fallback на `MatchFxCatalog` Runic +
  `PassiveAuraFxRules`. Всегда на носителе.
- Трейты эмитят `EmitCast` в тот же `SpellCasts` снапшот: Cleave (прок), Deadeye (крит), Battlemace
  (hybrid melee hit), Last Call (спавн после смерти), Catapult (splash на прилёте). Примитивный splash-диск
  катапульты больше не рисуется — играет префаб способности.
