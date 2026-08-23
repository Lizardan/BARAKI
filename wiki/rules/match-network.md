# Match network — старт, снапшоты, host migration

Listen-host (host-as-server) + NGO. Клиенты **не** тикают симуляцию (`MatchTickMode.Client`).

## Старт матча (race pick → sim)

1. `NetworkLobbyState.MatchStarted` — все грузят `Game.unity` (локальный `SceneManager`, не NGO scene manager).
2. `NetworkRacePickState` живёт на лобби-префабе (DDOL), не на authority — иначе клиенты пропускают race pick.
3. Когда все слоты выбрали расу, сервер ставит `_matchSimStarted` и вызывает `StartMatch`.
4. Клиенты стартуют симуляцию **дважды-идемпотентно**:
   - `NetworkVariable` `_matchSimStarted` + реплицированный `_racePicks` (`TryStartMatchFromReplicatedState`)
   - запасной `BeginMatchClientRpc`
5. `EnsureSession` **не** имеет права сбрасывать пики после `_matchSimStarted` (поздний RPC иначе обнуляет матч).

## Бонус-оверлей и HUD

- Хост читает `MatchController` напрямую.
- Клиент предпочитает снапшот v13, но **если снапшота ещё нет** — fallback на локальный controller после `StartMatch`. Иначе оверлей пустой, а часы зависают на `00:00`.
- Снапшот публикуется сразу в `BeginMatchOnServer` (`PublishSnapshotNow`), не только по таймеру `SnapshotHz`.
- `MatchSnapshotCodec.Deserialize` принимает любую версию `1..CurrentVersion`. Хардкод allow-list (`1 or 2 or … or 17`) при бампе версии роняет **каждый** клиентский RPC (`Unsupported snapshot version`) — оверлей бонуса пустой, часы `00:00`, после выхода хоста клиенты уходят в Offline split-brain.

## Start → Early

Камера летит к базе (`IsFocusInProgress`). Early **не** ждёт race-pick pan lock. Таймаут `MatchRules.StartPhaseMaxWaitSeconds` (5 с, GDD `PHASE_START`), чтобы застрявшая камера не держала часы на нуле.

## Host drop

`MatchNetworkAuthority.OnNetworkDespawn` при живом session handle оставляет `TickMode.Client` — **не** `Offline`. Иначе каждый клиент начинает свою симуляцию (split-brain), бонус-оверлей «внезапно появляется», и они играют не друг с другом.

Capture для миграции: last-good bytes, иначе **локальный** `MatchController` любого пира (не только слот бывшего хоста). Пустой last-good на клиентах — норма, если снапшот не дошёл.

## Плавный рендер юнитов (snapshot interpolation)

Хост тикает симуляцию 30 Гц, снапшоты — `MatchNetworkAuthority.SnapshotHz = 30` (два сэмпла
в буфере, меньше ощущаемый лаг, чем на 15 Гц). Прямое применение без задержки дёргает юнитов.
Рендер клиентов построен на **snapshot interpolation** по серверному времени:

- `MatchCombatSystem.ApplyAuthoritativeUnits(..., matchTimeSeconds)` пишет каждый снапшот юнита в
  `UnitRenderTrack` (кольцевой буфер до 8 сэмплов, дубликаты/обратные таймстампы отбрасываются).
  Один track на `unitId`, чистится при удалении юнита.
- Презентер (`MatchCombatPresenter`) на клиенте семплирует пару по `renderTime`:
  `serverTimeEstimate = snapshot.MatchTimeSeconds + (Time.time - MatchRuntime.LastSnapshotArrivalRealtime)`,
  `renderTime = serverTimeEstimate - NetworkUnitVisualRules.ClientInterpDelaySeconds`
  (`2 / SnapshotHz` ≈ 0.067 с).
  Позиция — `Vector3.Lerp(prev, next, alpha)`, поворот — `ResolveRenderFacing` (анти-crossing по world-up),
  `BehaviorState` / `AttackSwingSerial` — из ближайшего по `alpha` сэмпла (анимации тоже плавные).
- Хост/оффлайн: позиция догоняется `StepToward(HostCatchUpPerSecond = 40f)` (тики 30 Гц «ступенчатые»),
  поворот — прежний `Slerp(8f * dt)`. Первый спавн визуала всегда — мгновенный snap.
- `LastSnapshotArrivalRealtime` сбрасывается на `OnSessionStarted` и не выставляется вне `ApplyNetworkSnapshot`.

Снаряды **не** интерполируют снапшотные позиции (в wire — one-shot spawn). Меш летит по
известной баллистике: `CombatProjectileState.ResolvePresentationProgress` от `SpawnRealtime`
(кадры / subframe), урон по-прежнему на 30 Гц `Elapsed`. v20 пишет `AppliesSplashAoe` —
клиентский бонус-Super рисует камень катапульты, не болт.

`QualitySettings.vSyncCount = 1` (ритм монитора, без тиринга; не `targetFrameRate = 60`).

## Конец матча и реванш

- В `EndMatch` сразу `PublishSnapshotNow` + короткий `NotifyMatchEndedClientRpc(winnerSlot)`.
  Пока фаза `End`, снапшоты с `WinnerSlot` продолжают уходить; пауза **не** глушит публикацию End.
- HUD: если `Phase == End` и оверлей ещё не показан — `ShouldShowEndResultsFallback`.
- «Реванш» в сети: `MatchNetworkSession.RequestReturnToLobby` → ServerRpc → всем грузить Lobby
  **без** `Shutdown()` NGO. `MatchRematchRules.IsMatchInProgressForHostMigration(End) == false`.
- «В меню» остаётся локальным выходом (`LeaveMatch`).

## Прочее

- `MatchLobbyHeartbeat.Ensure()` вне Play Mode возвращает `null` (нельзя `DontDestroyOnLoad` в EditMode). Вызовы `MatchNetworkSession` (`ApplyHandle`/`Shutdown`) используют `?.`.
