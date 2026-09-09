# Ветка «Порядок героев» (hero order pick)

Пост-бонусная панель: после закрытия окна бонусов игрок перетаскиванием задаёт порядок
героев 1/2/3 слева направо, затем «Подтвердить». **Позиция в порядке = уровень главного
здания**, на котором герой открывается для найма (позиция 1 → main level 1, и т.д.).
Идентичность героя **всегда** остаётся привязана к `heroSlot` (1/2/3); меняется только
гейтинг найма. Дефолт `[1,2,3]` сохраняет legacy-поведение.
Связанные правила: `snapshot-wire.md` (v25), `match-network.md` (применение снапшота).

## Канон

- Источник истины: `GameDesign/` (участок «порядок героев» задан в задаче, npm-тикет
  HacknPlan). Правила без race-специфики.
- `HeroOrderPickRules`: `DefaultOrder() = [1,2,3]`, `IsValidOrder` (перестановка 1..3),
  `GetPosition(order, heroSlot)` (1..3, 0 если нет; невалидный order → дефолт),
  `IsUnlockedAtLevel(order, heroSlot, mainLevel)` = `позиция <= mainLevel`.
- Гейтинг найма: `HeroRules.CanHire` / `ShouldShowHire` получают опциональный
  `int[] heroOrder = null` (null/дефолт = legacy). `MatchController.TryStartHeroHireResearch`
  и `MatchInspectorController` передают `player.HeroOrder`.
- **Отображение найма в главном здании — по порядку:** панель здания
  (`PopulateHeroHireCommands`) строит кнопки найма героев по **позиции в `HeroOrder`**, а не
  по `heroSlot`: слоты команд 6–8 = позиции 1/2/3 (левая кнопка = первый герой, средняя =
  второй, правая = третий), `MatchMainHireSlotRules.TryGetHeroHireSlotByPosition(position)`.
  Сам герой (найм `hero_hire:{heroSlot}`, иконка, подпись «Герой N», tooltip) привязан к
  реальному `heroSlot`, меняется только визуальный порядок кнопок. Доступность —
  `ShouldShowHire(..., player.HeroOrder)` — уже учитывает порядок.
- **Казармы тоже следуют порядку:** `PopulateHeroDeployCommands` раскладывает кнопки деплоя
  героев по позициям `HeroOrder` (слоты команд 9–11 = позиции 1/2/3),
  `MatchBarracksCallSlotRules.TryGetHeroDeploySlotByPosition(position)`. `_heroDeploySlots`
  (slot → heroSlot) остаётся привязан к реальному герою.
- **Перерисовка панели:** `BuildCommandsFingerprint` в `MatchInspectorController` включает
  `HeroOrder` (`heroOrderKey`). Без этого панель не перестраивалась, если порядок применялся
  после первой отрисовки (order пришёл со снапшотом/host migration позже) — кнопки держали
  старый порядок до смены другого поля fingerprint (например, пока золото не пересекло
  стоимость найма).
- Подтверждение: `MatchPlayerState.ConfirmHeroOrder(order)` (валидация правила).
  Сервер строк: при показе панели окно бонуса уже закрыто (pick2 выбран или дедлайн истёк).
- **Авто-подтверждение**: по истечении `_bonusPickDeadlineSeconds` в
  `MatchController.TickBonusPickDeadline` все неподтвердившие получают `DefaultOrder()` +
  confirmed (аналог автозаполнения бонуса).
- UI: поставщик `HeroOrderHudRules` (снапшот приоритетен на чистом клиенте,
  `IsOrderWindowOpen(confirmed, bonusWindowOpen)`).

## Wire v25

- `MatchSnapshot.CurrentVersion = 25`. В запись игрока (в КОНЕЦ, после TowerTrackLevels)
  добавлены: `HeroOrder` (3 байта, decode-кламц 1..3 → всегда валидный, невалид → дефолт)
  + `HeroOrderConfirmed` (bool). `MatchSnapshotChecksum` миксит длину и элементы order +
  confirmed. Применение в `ApplyAuthoritativeSnapshot` игнорирует невалидный order
  (сохраняет текущий).
- Требование совпадения версии у пиров — инкремент версии ломает старые клиенты
  (v24 → v25).

## Preview (3D-idle)

- Слой **`HeroPreview` (индекс 9)** — добавлен в `TagManager.asset`. Главная камера
  (Game.unity) исключает его из culling mask (`-513`); превью-камеры рендерят только слой 9.
- `HeroOrderPreviewRuntime` (Game.UI): по RenderTexture 256×256 + ортокамера на героя,
  спавн `UnitVisualCatalog.TryGetPrefab(raceId, UnitRole.Hero, heroSlot, bonusSlot)`,
  `bonusSlot = BonusKitRules.EffectiveBonusSlotForHero(raceId, pick1, pick2, heroSlot)`
(ветеран-варианты). Idle — `UnitCombatAnimatorDriver.TickStand` каждый кадр. RT назначается
   как `backgroundImage = Background.FromRenderTexture(rt)`. Вкл/выкл только при видимости
   панели; teardown в OnDestroy.
- **Изоляция превью (важно):** все герои лежат на слое 9, камера режет по слою, а не по
   объекту, поэтому героев нельзя спавнить в одну точку — иначе каждая превью-камера покажет
   всех троих сразу. Спавн по оси X: `x = index * extent * 6` (`PreviewSlotSpacingFactor = 6`,
   `extent = GetHeroExtent(hero)` по **объединённым** bounds), камеру кадрирует `FrameHero` из
   `bounds` **после** смещения (и на порядке «сначала позиция, потом frame» стоит не менять).
   Сосед героя на ≥5×extent при полуширине камеры ~1.15×extent — не в кадре.
- **Центрирование (важно):** кадр строится по **объединённым** world-AABB всех рендереров
   героя (`GetHeroBounds` — Encapsulate по `GetComponentsInChildren<Renderer>`), а не по первому
   рендереру: у Людей тело разбито на десятки частей (колчан, щиты, голова…), и самый первый
   рендерер (`quiver_A`) уводил камеру вбок. У Древних обвязка один SkinnedMeshRenderer — там
   объединённые bounds совпадают с первым.
- **Ориентация героя:** `FrameHero` ставит камеру на −Z (центр + `(0, 0.2e, −2.4e)`).
   `OrientHeroToCamera` поворачивает героя `Quaternion.LookRotation(toCamera) * baseRotation`,
   где `baseRotation = hero.transform.localRotation` — **авторский yaw префаба, его нельзя
   сбрасывать в identity**: Древние имеют вшитый baked 270° на корне (компенсация «меш
   авторится на +X, в бою «лицо» на +Z» — см. `UnitGreyboxVisuals.AnimatedHumanModelYawDegrees`),
   и `TrySpawnHero` раньше затирал его → герои Древних не поворачивались к камере. Композиция
   на базе вращения повторяет боевую схему (`visual.Root.rotation = LookRotation(facing)` поверх
   baked-yaw модели). Затем `FrameHero` повторно кадрирует повёрнутые bounds.
   Порядок: спавн → позиция → FrameHero → orient → FrameHero.

## UI Toolkit

- `Assets/Game/UI/Runtime/UXML/HeroOrderPick.uxml` + `USS/HeroOrderPick.uss` (оверлей как
`BonusPick`: backdrop `picking-mode=Ignore`, карточки ловят pointer). Блок центрирован,
   но приподнят (`.hero-order__center { position: relative; top: -10% }`):
   без этого кнопка «ПОДТВЕРДИТЬ» уезжает на нижний HUD-док и клики перехватывает док.
- `HeroOrderPickController` (Game.UI): строит 3 карточки (превью + бейдж позиции + бейдж
  «ВЕТЕРАН» + название), drag&drop через PointerCapture (свап = перенос корня карточки в
  `HeroOrderRow`, превью переезжает вместе с карточкой), подтверждение через
  `HeroOrderNetworkFacade.TryRequestHeroOrder` (ServerRpc → `NetworkBonusPickState.RequestHeroOrder`),
  фолбэк `controller.TrySetHeroOrder(localSlot, order)`.
- Объект сцены: `--- UI ---` → `HeroOrderPick` (UIDocument + DefaultPanelSettings +
  HeroOrderPickController + HeroOrderPreviewRuntime).

## Карта кода

| Файл | Роль |
|------|------|
| `Scripts/Runtime/Gameplay/Match/HeroOrderPickRules.cs` | правила порядка (создан) |
| `Scripts/Runtime/Gameplay/Match/HeroOrderHudRules.cs` | поставщик UI (создан) |
| `UI/Runtime/MatchMainHireSlotRules.cs` | позиция order → command slot 6–8 (изменён) |
| `UI/Runtime/MatchBarracksCallSlotRules.cs` | позиция order → деплой-слот 9–11 (изменён) |
| `UI/Runtime/Controllers/MatchInspectorController.cs` | найм и деплой по order-позиции + fingerprint `HeroOrder` (изменён) |
| `Scripts/Runtime/Gameplay/Networking/HeroOrderNetworkFacade.cs` | мост NetworkBehaviour↔UI (создан) |
| `Scripts/Runtime/Gameplay/Match/HeroRules.cs` | CanHire/ShouldShowHire + `heroOrder` (изменён) |
| `Scripts/Runtime/Gameplay/Match/MatchPlayerState.cs` | HeroOrder/HeroOrderConfirmed/ConfirmHeroOrder (изменён) |
| `Scripts/Runtime/Gameplay/Match/MatchController.cs` | TrySetHeroOrder, авто-подтверждение, apply (изменён) |
| `Scripts/Runtime/Gameplay/Networking/MatchSnapshot.cs` | v25 (изменён) |
| `Scripts/Runtime/Gameplay/Networking/NetworkBonusPickState.cs` | RequestHeroOrder + ServerRpc (изменён) |
| `UI/Runtime/Controllers/HeroOrderPickController.cs` | панель, DnD (создан) |
| `UI/Runtime/Controllers/HeroOrderPreviewRuntime.cs` | 3D-idle превью (создан) |
| `ProjectSettings/TagManager.asset` | слой HeroPreview = 9 (изменён) |
| `Assets/Game/Scenes/Game.unity` | объект HeroOrderPick + маска камеры (изменён) |

Тесты: `HeroOrderPickRulesTests.cs` (создан), `MatchSnapshotCodecTests.cs` (v25 +
round-trip), `MatchSnapshotApplyTests.cs` (применение/игнор невалидного),
`HeroRulesTests.cs` (гейтинг по order).

## Sorting оверлеев

- Оверлеи выбора (HeroOrderPick, RacePick, BonusPick) ставят `sortingOrder = 100` в Awake
  (`_uiDocument.sortingOrder`), HUD и остальные панели сидят на 0. Это гарантирует, что
  реген-фреймы казарм, ability cooldown circles и прочий HUD-контент не рисуются поверх
  меню. Если нужен оверлей ещё выше — sortingOrder > 100.