# Арена — дуэли каждые 15 минут

Раз в 15 игровых минут матч встаёт на паузу, камеры всех игроков улетают на отдельную
арену, игроки выбирают бойца, идут дуэли 1v1, после чего камера возвращается и матч
продолжается. Четыре арены за матч, награда победителю дуэли 1000/2000/3000/4000.

## Тайминг

| Параметр | Значение | Где |
|---|---|---|
| Интервал между аренами | `900` игровых секунд | `ArenaRules.IntervalSeconds` |
| Выбор бойца | `30` с | `ArenaRules.PickSeconds` |
| Презентация пары | `5` с | `ArenaRules.IntroSeconds` |
| Показ итога дуэли | `3` с | `ArenaRules.ResultSeconds` |
| Страховка дуэли | `120` с | `ArenaRules.MaxDuelSeconds` |
| Арена титанов | № `4` | `ArenaRules.TitanArenaIndex` |

Отсчёт идёт по `MatchController.MatchTimeSeconds` — игровому времени. Пока арена
активна, время матча стоит, поэтому интервал между аренами всегда 15 минут игрового
времени, а не реального.

## Кто участвует

- Участник — не выбывший игрок, у которого есть **нанятый** боец. Состояние
  героя (idle/deployed/dead/на кулдауне) неважно — важен сам факт найма в главном
  здании (`HeroLifecycleState != None` для героев, `TitanState.IsUnlocked` для титанов).
- Герой, уже выступавший на прошлой арене, **заблокирован** — маска
  `ArenaRules.UsedHeroKey(slot, heroSlot)` в `ArenaNetState.UsedHeroMask`. Три героя
  на три геройские арены; титанская арена использует титана и маску не трогает.
- Меньше двух участников — арена «сгорает»: номер растёт, отсчёт идёт к следующей.

## Пары

Участники сортируются по `Gold` по убыванию (тай-брейк — меньший слот):
топ-1 против топ-2, топ-3 против топ-4. При нечётном числе последний становится
**претендентом** и в окне выбора дополнительно указывает соперника — любого из
остальных участников. Его дуэль идёт последней, когда все остальные уже подрались.
Победитель получает золото; проигравший просто остаётся без награды — герой **не**
умирает и **не** уходит в кулдаун.

## Пауза: чем отличается от обычной

`MatchPauseGate.SetArenaPaused(true)` выставляет `IsArenaPaused`. Он входит в
`IsSimulationPaused`, но **не** в `IsPaused`:

- `IsPaused` → `Time.timeScale = 0` (пауза игрока, миграция, дисконнект).
- `IsSimulationPaused` → блокирует тик матча (`MatchRuntime.Update`,
  `MatchNetworkAuthority.Update`) и команды (`IsCommandsBlocked`).

Арена **не** обнуляет `timeScale`: иначе встали бы анимации, Cinemachine и VFX.
Все таймеры арены идут на `Time.unscaledDeltaTime`.

Следствия, которые надо помнить:

1. Пока арена активна, `MatchNetworkAuthority.Update` не публикует снапшоты.
   Золото начисляется в `MatchPlayerState`, поэтому после награды вызывается
   явный `MatchNetworkAuthority.Instance?.PublishSnapshotNow()`.
2. Команды игроков заблокированы — это желаемое поведение. Выбор бойца идёт
   через отдельный RPC (`SubmitArenaPickServerRpc`) и **не** проходит через
   `IsCommandsBlocked`, иначе выбор был бы невозможен.

## Симуляция дуэли

`ArenaDuelSim` поднимает **отдельный экземпляр** `MatchCombatSystem` с синтетическим
`LaneGraph` из двух встречных путей `ARENA_A` / `ARENA_B` через центр арены.
Там реальные статы героя (`ResolveArenaHeroStats` / `ResolveArenaTitanStats` с уровнем
слота), его способности с префаба и обычные формулы урона. Изоляция полная: в списке
юнитов только два бойца, `BuildingRegistry` не задан, `WalkableSurface` не задан
(коридорный кламп по пути). Основная карта на дуэль не влияет и наоборот.

Для новой дуэли симуляция пересоздаётся — никаких остаточных состояний.

## Сеть

- Состояние арены — **одна** `NetworkVariable<ArenaNetState>` (`ArenaNetState` :
  `INetworkSerializable`) на `MatchNetworkAuthority`. Без массивов в wire-формате.
  NGO реплицирует её независимо от паузы матча — это главная причина выбрать
  NetworkVariable, а не секцию снапшота.
- Клиент → сервер: `ArenaNetworkBridge.RequestPick(heroSlot, challengerTarget)` →
  `SubmitArenaPickServerRpc` → `ApplyArenaPick(ResolveSenderSlot(...), ...)` →
  `ArenaDirector.SubmitPick`. Слот берётся из `rpcParams`, а не от клиента.
- Офлайн (authority не заспавнен) состояние хранится в статике `ArenaNetworkBridge`,
  и `RequestPick` применяется сразу к локальному `ArenaDirector`.
- Хост миграции: состояние арены в снапшот **не** попадает. Если хост упадёт во время
  арены, арена прервётся (пауза снимается в `OnDestroy`/`AbortArena`).
- Сброс перед новым матчем: `MatchNetworkAuthority.ResetArenaState()` вызывается из
  `ApplyReturnToLobby` (хост) и `NotifyReturnToLobbyClientRpc` (клиенты) — снимает
  `IsArenaPaused`, обнуляет NetworkVariable/статику и зовёт
  `ArenaDirector.ResetForNewMatch()` (индекс арены, маска использованных героев,
  позиция камеры). Без этого следующий матч унаследует залипшую паузу.
- `ArenaNetworkBridge.Publish` — no-op на клиенте: состояние только реплицируется.
- Конец матча во время арены: `ArenaDirector.Update` видит `MatchPhase.End` и вызывает
  `AbortArena()` (снимает паузу, очищает дуэли, публикует пустое состояние).

## Камера

Арена стоит в `ArenaRules.Center` = `(0, 0, -4000)` — далеко за пределами карты.
`GameplayCameraPanController` клампит панорамирование радиусом 152, поэтому на время
арены кламп снимается (`SetPanBoundsEnabled(false)`), позиция и yaw сохраняются и
восстанавливаются после (`ArenaDirector.TakeCamera` / `ReleaseCamera`). Ввод
блокируется `SetPanInputLocked(true)`.

## UI

Всё живёт в `MatchHud.uxml` / `ArenaHud.uss` (без правок сцены — UIDocument уже есть):

- `ArenaTimerBadge` — **внутри верхней полоски HUD** (`TopBar`), сразу после
  `TimeLabel`: горизонтальный ряд «отсчёт + подпись справа». Своей рамки/подложки нет —
  использует хром полоски. Пока арен не осталось (`Index == 0`), элемент скрыт.
  - `Idle` → `15:00` + «ДО АРЕНЫ N»;
  - активная арена → «АРЕНА N» + «ДУЭЛЬ k/N» либо «ВЫБОР БОЙЦА».
- `ArenaPickOverlay` — выбор бойца (герои с пометками «не нанят» / «уже выступал»),
  плюс блок выбора соперника для претендента.
- `ArenaIntroOverlay` — «Игрок X · Герой K VS Игрок Y · Герой M» и отсчёт 5 с.
- `ArenaDuelOverlay` — полосы HP обоих бойцов.
- `ArenaResultOverlay` — победитель и награда.

Логика — `#region Арена` в `MatchHudController`. Всё на polling состояния
(`ArenaNetworkBridge.Current`), как остальной HUD.

## Файлы

| Файл | Роль |
|---|---|
| `Match/ArenaRules.cs` | Числа, фазы, упаковка масок/пиков |
| `Match/ArenaPairing.cs` | Участники, сортировка по золоту, пары, претендент |
| `Match/ArenaDuelSim.cs` | Изолированная симуляция дуэли |
| `Match/ArenaDirector.cs` | Автомат фаз, пауза, награда, камера |
| `Match/ArenaDuelPresenter.cs` | Визуал двух бойцов |
| `Match/ArenaStage.cs` | Круг-арена, строится в рантайме |
| `Networking/ArenaNet.cs` | `ArenaFighterNet`, `ArenaNetState` |
| `Networking/ArenaNetworkBridge.cs` | Чтение/запись состояния, запрос выбора |
| `UI/Runtime/USS/ArenaHud.uss` | Стили арены |

## Связанные правила

`rules/match-network.md` (снапшоты, пауза, RPC), `rules/unity-ui.md`,
`rules/hero-order-pick.md` (похожий оверлей выбора).
