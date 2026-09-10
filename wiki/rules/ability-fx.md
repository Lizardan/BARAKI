# BARAKI Studio

Единственное editor-окно для **настройки визуала и боевого радиуса** способностей.
Что видно в превью — то играет в матче тем же пайплайном (`ShowAbilityFx` / пассивные ауры).

Это **не** отдельный игровой UI и не второе окно палитры. Старые имена Ability FX Viewer /
Ability FX Studio / FX Studio — то же окно; кнопка тулбара: **`BARAKI Studio`**.

Связанные правила: `abilities.md` (def-ы, каст, rebuild), `content-assets.md` (киты и префабы).

## Как открыть

| Путь | Детали |
|------|--------|
| Меню | `BARAKI/Abilities/BARAKI Studio` |
| Хоткей | **Ctrl+Shift+F** (`%#f`) |
| Тулбар Unity | кнопка **BARAKI Studio** по центру (`AbilityFxStudioToolbarButton`, `[MainToolbarElement("BARAKI Studio")]`) |

Класс окна: `Game.Editor.AbilityFxStudioWindow` (`Assets/Game/Scripts/Editor/AbilityFx/`).
IMGUI `EditorWindow` (не UI Toolkit — игровой UI Toolkit на editor tools не распространяется).

## Зачем окно существует

Автор VFX должен понять **как бьёт способность** (аура на себе / круг на земле / вспышка / точка),
поставить префаб в правильную точку (едет с моделью или остаётся) и увидеть **реальный боевой радиус**
относительно юнитов того же масштаба, что в матче.

Пишет в ассет:

| Поле | Куда | UI в Studio |
|------|------|-------------|
| `AbilityFx` (цвет, префаб, якорь, клип, Scale, Euler) | `UnitAbilityDef.Fx` | вид / где / поворот / палитра / анимация |
| `Radius` | `UnitAbilityDef.Radius` | слайдер **Радиус** (AoE) |
| `CastRange` | `UnitAbilityDef.CastRange` | слайдер **Досягаемость** (Mend / Resurrect) |

Divine Blessing 100/101 живут не в ките юнита, а в `MainExtraAbilityFxCatalog`
(`Assets/Game/Resources/Fx/MainExtraAbilityFxCatalog.asset`). Радиус этих двух **не** крутится
в слайдере Studio. Имена в списке Studio — `MainExtraAbilityFxDefs.GetDisplayName`
(«Кара зданий» / «Кара юнитов»), не поле runtime-SO.

## Три разных «где» — не путать

| Понятие | Класс | Вопрос | Пример Frost |
|---------|-------|--------|--------------|
| **Механика** | `AbilityFxMechanicRules` | Как бьёт бой? Круг едет / штампуется / точка? | область на земле **у скопления врагов**, r из def |
| **Якорь VFX** | `AbilityVfxAnchor` + `AbilityVfxKindRules` | Куда спавнить префаб и едет ли он? | сид **Под собой** (земля у кастера) — можно сменить |
| **Палитра** | `AbilityVfxKind` | Какой набор thumbs предложить? | Cast (лёд / хилы), не Hit-слэши |

Механика **не** перезаписывает якорь. Якорь **не** меняет боевой радиус.
Боевой радиус **не** масштабирует one-shot префаб (`ResolveOneShotLocalScale` = prefab × `AbilityFx.Scale`).

## Окно (три колонки)

Ширины и свёрнутость палитры — `SessionState`
(`BARAKI.AbilityFxStudio.ListWidth` / `PaletteWidth` / `PaletteCollapsed` / `SelectedId`).
`PaletteWidth` клампится в `[220, min(1100, окно − список − запас)]` — палитру
можно раздвинуть, сетка до 8 колонок (`MinCellWidth` 132). Центральная колонка
(`DrawPreviewColumn`) — всегда `GUILayout.ExpandWidth(true)`; ряд анимаций и
карточки настроек тоже `ExpandWidth`, без `MinWidth` на всю длину клипа — иначе
центр вылезает на сплиттер и палитру не схватить.
Две ручки-сплиттера: ширина от **абсолютного** `mousePosition.x` относительно
захвата (`_dragStartMouseX` / `_dragStartWidth`), не `evt.delta` и не
`SetWantsMouseJumping` — иначе при Repaint орбиты/тиков панель скачет.

```
[ список китов ] | [ превью + панель настроек ] | [ палитра thumbs ]
```

- **Слева:** поиск, группы `AbilityVfxKindRules.KitLabel` (King / Paladin / Caster / BONUS / …).
  Строка = свач цвета + `DisplayName`.
- **Центр:** `AbilityFxPreviewSession` — слева кастер кита (`AbilityFxPreviewCasterRules`),
  справа dummy (`AbilityFxPreviewTargetRules`). Кара 100/101 — **без кастера**, одна цель
  в центре: Кара зданий = барак (через 0.25 с рушится как в матче: `BuildingRuinsVisual` +
  взрыв/`BuildingBurning` из `MatchFxCatalog`, горящие руины **2 с**, затем цикл),
  Кара юнитов = один мечник, без зданий. Остальные способности — мечник справа.
  ЛКМ орбита, колёсико зум, точки в сцене = якорь.
- **Справа:** встроенная `AbilityVfxPrefabPalette` (не `EditorWindow`). Сворачивается шевроном.
  Фильтры (поиск, Все / Cast / Hit / Aura / Кастом, ObjectField) живут **в шапке палитры**, не над превью.
  Чипы фильтра **не** сбрасываются при смене способности.

Под превью — **транспорт** (`DrawTransportBar`) и полоса настроек (`DrawSettings` + `AbilityFxStudioMechanicUi`):

0. Транспорт: «Ещё раз» / «Пауза» (заморозка кадра — орбита и зум продолжают работать) /
   «Стоп» (убрать FX-инстанс, кольцо радиуса остаётся) / «Кадр» (сброс орбиты + авто-кадр по радиусу).
   Прогресс-бар `elapsed / duration` (для аур — «∞ аура»), справа статус: имя префаба /
   «нет префаба — выбери в палитре» / «эффект убран». После «Стоп» `SetEffect` не респавнит
   эффект, пока не изменится визуальный параметр (флаг `_stopped`); правка радиуса при «Стоп»
   обновляет только кольцо. Состояние живёт в `AbilityFxPreviewSession`
   (`_paused` / `_stopped` / `PlaybackDuration`).
Панель настроек = **две строки** (`DrawSettings`):

1. **Четыре рамки одной высоты** (`OpenCard`: Width + Height/Min/Max 228,
   без `BeginArea` — на Layout у GetRect ширина ~0 и карточки пропадали).
   Ширина каждой = `(окно − список − палитра − зазоры) / 4` **в этом кадре**
   (не ширина превью с прошлого Repaint — тот `MinWidth` блокировал сплиттер).
   `Width+MaxWidth`, `MinWidth(0)`. Ряд тянется с палитрой. Внутри кнопок
   тоже `MinWidth(0)`.
   низ — `DrawFillHint`. **Инфо** схема 148. **Вид и размер** / **Где** / **Поворот**
   как раньше. Анимация: **одна строка**, кнопка 42 pt / две строки —
   `StateName` (Cast/Attack) и имя клипа (`*_cast_A`), ширина по длинной подписи.
   Радиус в «Вид и размер» с отдельным триггером сохранения.
2. **Анимация** — постоянная полоса на всю ширину (helpBox + ярлык «Анимация» слева),
   **без foldout**; все клипы в один ряд, кнопка не уже текста.

Схема — мини-арена ¾, не вид сверху: эллипс пола с мягкой заливкой сторон
(кастер / цель), силуэты (голова + тело, смотрят друг на друга), пилюли «я»/«цель»
под полом. AoE — приплюснутое кольцо на земле у хозяина (как кольцо в превью),
точка — искра на груди, удар — ещё тик «полёта». Подпись снизу = `Motion`
(едет / остаётся / вспышка). Пунктир кастер→цель не рисуем — шумит.
**Не ставить `FontStyle.Bold` / `EditorStyles.boldLabel` кириллическим `GUI.Label`**
(в т.ч. заголовок имени в карточке — `_nameHeaderStyle` = `EditorStyles.label` + fontSize).
Bold-вариант динамического шрифта теряет кириллицу (текст просто не рисуется);
размер крутить только через `fontSize`.

У аур карточки якоря и поворота **не** прячутся (лейаут не прыгает) — показывают хинты
«всегда на носителе» / «у аур не применяется».

## Превью радиуса 1:1

Кольцо в сцене = **боевые метры**, не схема. `AbilityFxMechanicRules.PreviewRingRadius(r) == r`.
Модели — `UnitGreyboxVisuals.ResolveAnimatedPresenterScale`, как презентер матча.
Frost `r = 5` вокруг цели — круг диаметром 10 м; кастер в превью стоит ~3 м левее и часто
попадает **внутрь** круга — это физически верно на такой дистанции.

Камера и пол подгоняются под круг (`FitGround` / `FrameCameraToMechanic`); зум пользователя
не сбрасывается, пока не сменится способность или радиус.
Слайдер Радиус (0.5–16) пишет `ApplyRadius` + Undo; превью читает `def.Radius` каждый кадр
(менять поле в инспекторе SO при открытом Studio тоже обновляет круг).

`Radius == 0` в бою и в Studio = фолбэк кита (Frost снова 5). Smite / Mend **не** рисуют круг:
их число — досягаемость поиска, не AoE.

## Карта файлов

### Editor (`Game.Editor`, папка `Scripts/Editor/AbilityFx/`)

| Файл | Роль |
|------|------|
| `AbilityFxStudioWindow.cs` | Окно, список, сплиттеры, настройки, запись Fx/радиуса |
| `AbilityFxStudioToolbarButton.cs` | Кнопка main toolbar |
| `AbilityFxStudioMechanicUi.cs` | Схема + чип + описание |
| `AbilityFxPreviewSession.cs` | PreviewRenderUtility: модели, VFX, кольцо AoE, камера, транспорт (пауза/стоп/прогресс/фокус) |
| `AbilityVfxPrefabPalette.cs` | Сетка живых thumbs |
| `AbilityVfxThumbSession.cs` | Одна ячейка палитры |
| `AbilityVfxPreviewPlayback.cs` | Particle / VFX Graph тик в preview-сцене |
| `AbilityVfxPrefabIndex.cs` | Скан Slash / CFXR / Hyper Casual / Custom |
| `CustomAbilityFxPrefabBuilder.cs` | Сборка `Prefabs/Fx/Custom` (`SkyBeam`) |
| `AbilityAnimClipIndex.cs` | Клипы с Animator кита |

Билдер: `UnitAbilityAssetBuilder` — preserve Fx + ненулевых Radius/CastRange/SecondaryRadius.

### Runtime (`Game.Gameplay`)

| Файл | Роль |
|------|------|
| `Gameplay/Vfx/AbilityFxMechanicRules.cs` | Shape, радиус, русские подписи, 1:1 preview radius |
| `Gameplay/Vfx/AbilityVfxKindRules.cs` | Палитра Cast/Hit/Aura, `KitLabel`, `ResolveDefaultAnchor` |
| `Gameplay/Vfx/AbilityVfxAnchor.cs` | Caster / Target / Ground / Impact / Unspecified |
| `Gameplay/Vfx/AbilityVfxPlacement.cs` | Общие формулы спавна (матч = Studio) |
| `Gameplay/Vfx/AbilityVfxTint.cs` | Particle HSV + VFX Graph First/Second/ThirdColor |
| `Gameplay/Vfx/AbilityFxPreviewCasterRules.cs` | Какая модель слева; Кара — кастер скрыт |
| `Gameplay/Vfx/AbilityFxPreviewTargetRules.cs` | Dummy: барак / мечник; соло-цель; collapse 2 с |
| `Gameplay/Data/UnitAbilityDef.cs` | `ApplyFx` / `ApplyRadius` / `ApplyCastRange` |
| `Gameplay/Combat/MainExtraAbilityFxCatalog.cs` | Fx Кары 100/101 |
| `Match/MatchCombatPresenter.cs` | `ShowAbilityFx` в бою |
| `Match/AuraFxVisuals.cs` | Пассивные ауры на носителе |

`AbilityFx` — структура на def (Color, VfxPrefab, Anchor, AnimKind, AnimState, AnimVariant, Scale, Euler).

## Механика (`AbilityFxMechanicShape`)

| Shape | UI | Смысл | Примеры |
|-------|----|--------|---------|
| `AuraAroundSelf` | Аура вокруг себя · едет | Пассивное кольцо на носителе | ауры 13/23/33/43/50 |
| `AreaOnGround` | Область на земле · остаётся | Штамп в кадр каста | Greater Heal, Consecration; Frost — у врагов |
| `BurstAroundSelf` | Вспышка вокруг себя | Разовый круг у кастера | Strike, Slam, Stomp, Group Heal, Shield, Cleave |
| `BurstAroundTarget` | Область у цели | Разовый круг у якоря | Holy Nova |
| `BurstAtImpact` | Вспышка в точке удара | Splash прилёта | Catapult |
| `PointOnTarget` / `PointOnSelf` | без круга | Один юнит / здание | Smite, Mend, Deadeye, Last Call, Кара зданий, Кара юнитов |

## Якорь спавна (`AbilityVfxAnchor`)

Caster не может быть 0 — `Unspecified = 0` значит «ещё не задан», сид `ResolveDefaultAnchor`.
Уже записанный якорь rebuild не затирает (`AbilityFx.WithPreservedAuthored`).

| Enum | UI | Поведение |
|------|----|-----------|
| `Unspecified` | — | взять сид по ability id |
| `Caster` | На себе | Parent к кастеру, центр тела (`BodyHeight` 0.9). Едет |
| `Target` | На цели | Parent к цели, центр тела. Едет |
| `Ground` | Под собой | Мир, `GroundY` 0.1 у ног кастера. Не едет |
| `Impact` | Под целью | Мир, `cast.CenterPosition` / preview impact. Не едет |

Превью Impact: Last Call → ноги кастера (root, не hover +4); Catapult → ноги цели.
Кара 100/101 — якорь **На цели** (луч на бараке / мечнике), не Impact.
В превью кастера нет: Кара зданий — только барак, Кара юнитов — только мечник.
Ауры в матче всегда `AuraFxVisuals.Attach` на носителе, независимо от кнопок якоря.

## Поворот и масштаб визуала

- `Euler` — local градусы. `(0,0,0)` = как в префабе (не задан). Пресеты: Горизонталь / Вертикаль / В пол.
- `Scale` — множитель префаба. `0` = не задан, runtime берёт 1.
- Боевой `Radius` **не** входит в size one-shot. Аура: `PassiveAuraFxRules.ResolveScale(radius)` × `AbilityFx.Scale`.

## Палитра префабов

Скан: `Adjustable Slash VFX Pack/Prefabs`, `JMO Assets/Cartoon FX Remaster/CFXR Prefabs`,
`Lana Studio/Hyper Casual FX/Prefabs`, **`Assets/Game/Prefabs/Fx/Custom`**. Первая ячейка — «нет».
ObjectField не затирает префаб вне этих папок.

Меши CFXR (`**/CFXR Assets/**/*.asset`) в паке должны быть Unity 6 (`serializedVersion` ≥ 10).
Старый Cartoon FX Remaster приходит с version 9 — консоль пишет
«Mesh object at version 9, below the supported minimum (10)». Лечится
`AssetDatabase.ForceReserializeAssets` / Open+Save в Editor, YAML руками не трогать.

| Kind | Способности | Типичные префабы |
|------|-------------|------------------|
| Aura | пассивные 13/23/33/43/50 | Hyper Casual Area/Shine; CFXR Magic Aura / LightGlow Loop |
| Hit | Strike, Ultimate, Smite, Consecration, Slam, Stomp, Cleave, Deadeye, Battlemace, Catapult | Slash_*; CFXR Impacts / Explosions; Hyper Casual Flash |
| Cast | хилы, баффы, резы, Frost, Last Call, Divine Blessing | остальные CFXR + Sparkle/Confetti/Water |
| Custom | любой пикер; чип **Кастом** | проектные префабы (`SkyBeam` — луч сверху для Кары 100/101) |

Кастомные эффекты собирает `CustomAbilityFxPrefabBuilder` (`BARAKI/Abilities/Rebuild Custom FX Prefabs`).
Не удалять пак Slash_* и не схлопывать Slash_1–30 в один пресет.

## Анимации

Клипы с Animator модели кита (`AbilityAnimClipIndex`: layer 0, BlendTree → child).
Пишет `AnimState` + `AnimVariant`. Пустой стейт → `AbilityAnimRules` (старый random A/B).
Матч: `ApplyAbilityAnimLock` + `Tick(..., stateOverride, variantOverride)` без random, если клип задан.
Трейты в бою клип не лочат (прок на ударе); в Studio клип всё равно играет. `applyRootMotion = false`.

## Сид и rebuild

`BARAKI/Abilities/Build Ability Defs` (`UnitAbilityAssetBuilder`):

- **Сохраняет:** весь `AbilityFx` через `WithPreservedAuthored`; ненулевые `Radius` / `CastRange` / `SecondaryRadius`.
- **Сидит заново:** имена, описания, урон/хил/CD и прочий тюнинг из `AbilityKitDefaults`.
- Пустой префаб / `Unspecified` якорь / нулевой Scale·Euler — досиживаются дефолтом, уже заданное не трогают.
- Геройские ауры 13/23/33/43 сидятся тем же Runic, что `MatchFxCatalog._auraRunicLoop`.
- Кара 100/101: сид `SkyBeam` (кастомный луч сверху). Пустой префаб или старый CFXR
  Explosion B / Hit A (Red) досиживается при открытии Studio; другой выбранный префаб не трогают.

Новый ассет (радиус 0) берёт кит. После правки слайдера значение живёт на SO.

Кара 100/101: `Resources.Load("Fx/MainExtraAbilityFxCatalog")`. Не путать с pick id 1/2 в Divine Blessing UI.

Вне Studio: building destroyed, кровь, Titan body rays — только `MatchFxCatalog`.

## Рантайм

Клиент играет `AbilityFx.VfxPrefab` из **своего** `UnitAbilityCatalog` (`Find(abilityId)`),
не с хоста. Пустой префаб после Find логируется один раз на abilityId (`EmptyVfxPrefab`).
Def без `Behaviour`, но с `VfxPrefab`, на клиенте не дропается.

- Каст: `MatchCombatPresenter.ShowAbilityFx` → instantiate `VfxPrefab` →
  `AbilityVfxPlacement.ApplyOneShotTransform` → `AbilityVfxTint.Apply` → scale prefab × `AbilityFx.Scale`.
  Lifetime 3 с; Frost — `StunSeconds`. Ground = `GroundY` 0.1. Impact = `Elevate(cast.CenterPosition)`.
- Ауры: Fx с каталога, fallback Runic. Parent к unscaled корню носителя, `local (0, 0.05, 0)`.
- Трейты эмитят `EmitCast` в `SpellCasts`: Cleave, Deadeye, Battlemace, Last Call, Catapult.
  Примитивный splash-диск катапульты больше не рисуется.

Константы высоты/земли общие: `AbilityVfxPlacement` (Studio и матч).

### Тряска камеры запрещена

CFXR-префабы несут `CFXR_Effect.cameraShake`, который смещает `Camera.main` в
`OnPreRenderCamera` и возвращает назад в `OnPostRenderCamera` — то есть дёргает камеру
**в обход Cinemachine**. В RTS этого быть не должно.

- Рантайм: `AbilityFxCameraShakeGuard.Strip(instance)` вызывается сразу после каждого
  `Instantiate` FX — в `MatchCombatPresenter` (`SpawnVfx`, burst Ледяного кольца и общий
  `SpawnFx` для крови/импактов), `MatchBuildingFxPresenter` и `AuraFxVisuals.SoftenLoop`
  (покрывает `Attach` / `AttachBodyRays`). Поля читаются рефлексией — пак сторонний.
- Не требуют гарда: болты/камни снарядов (`CombatAttackVisualBuilder` грузит
  `Resources/Art/Projectile*`) и визуалы юнитов — это не CFXR.
- Данные: у всех 51 префаба пака `cameraShake.enabled = 0`. Если в Studio назначат
  новый FX с включённой тряской, рантайм-гард её всё равно погасит, но лучше сразу
  поправить префаб.
- `AuraFxVisuals.PrepareEditorPreview` использует тот же гард (раньше — своя рефлексия).

**Добавляешь новую точку спавна FX — вызывай `AbilityFxCameraShakeGuard.Strip`.**

## Превью: что нельзя ломать

`PreviewRenderUtility` без обычного `Update` и без LightmapSettings игровой сцены.

- Частицы: `Pause` + `Simulate(dt * simulationSpeed, withChildren: false)`. Не `withChildren: true` по дереву.
- `dt <= 0` пропускать, clamp сверху ~0.05 с, **без** пола 0.001.
- VFX Graph (`Slash_*`): `pause = false`, `Reinit` + `Play`. **Не** `cameraType = Game`, **не**
  `PreviewRenderUtility.Render(true)` — URP падает (`LightmapSettings is NULL`).
- После `VFXManager.PrepareCamera` вернуть ortho. После `Reinit` снова тинт / lift чёрного `FirstColor`.
- Animator: `enabled = false`, только `Update(dt)` — иначе `camera.Render` удваивает клип.
- Свет: `lights[0]` key, `lights[1]` fill + Directional в preview-сцене (URP Lit/Toon иначе чёрные).
- Корни юнитов на `LaneHeight` 0.15, пол превью на **y = 0**, чтобы GroundY 0.1 был виден.
- CFXR в превью: `PrepareEditorPreview` (`clearBehavior = None`, без shake, mute audio).
- Сплиттер: `TickActiveSplitterDrag` в начале `OnGUI`; ширина от абсолютной мыши,
  не `delta` (иначе скачет из‑за Repaint орбиты).

## Тесты (`Game.Tests`)

| Тест | Что закрывает |
|------|----------------|
| `AbilityFxMechanicRulesTests` | shape, follows vs ground, Frost у врагов, Smite без круга, 1:1 radius |
| `AbilityVfxKindRulesTests` | палитра Cast/Hit/Aura, сид якоря |
| `AbilityVfxPlacementTests` | BodyHeight, GroundY, Impact, preview impact, scale без × radius |
| `AbilityFxPreserveTests` | `WithPreservedAuthored` |
| `UnitAbilityDefApplyTests` | `ApplyRadius` / `ApplyCastRange` |
| `AbilityFxPreviewCasterRulesTests` | кастер скрыт у Кары; dummy = барак / один мечник |
| `AbilityVfxPrefabIndexTests` | классификация папок (в т.ч. Custom / SkyBeam) |

После правки Studio: `read_console` на compile, затем эти EditMode-тесты.

## Как добавить способность в Studio

После шагов из `abilities.md` (id, кит, `Build Ability Defs`, seed):

1. Добавить id в `AbilityFxMechanicRules.ResolveShape` / `ResolveRadius` (иначе будет «точка на цели»).
2. При необходимости — `AbilityVfxKindRules` (палитра + сид якоря) и `AbilityFxPreviewCasterRules`.
3. Открыть Studio, выбрать умение, поставить префаб и радиус.
4. Тесты на shape/якорь.

## Вне скоупа окна

Боевая логика (`UnitAbilityBehaviour`), статы юнита (`Sync Balance to Prefabs`), FoW,
building destroyed / кровь / Titan rays, Canvas/uGUI.
