# Match network — старт, снапшоты, host migration

Listen-host (host-as-server) + NGO. Клиенты **не** тикают симуляцию (`MatchTickMode.Client`).

## Старт матча (race pick → sim)

1. `NetworkLobbyState.MatchStarted` — все грузят `Game.unity` (локальный `SceneManager`, не NGO scene manager).
2. `NetworkRacePickState` живёт на лобби-префабе (DDOL), не на authority — иначе клиенты пропускают race pick.
3. Когда все слоты выбрали расу, сервер ставит `_matchSimStarted` и вызывает `StartMatch`.
4. Клиенты стартуют симуляцию **идемпотентно** (guard flag `_localMatchStartPending`):
   - `NetworkVariable` `_matchSimStarted` + реплицированный `_racePicks` (`TryStartMatchFromReplicatedState`)
   - запасной `BeginMatchClientRpc`
   - Guard flag предотвращает двойной вызов `ApplyMatchSetupAndStart` при быстрой репликации обоих путей.
5. `EnsureSession` **не** имеет права сбрасывать пики после `_matchSimStarted` (поздний RPC иначе обнуляет матч).

## Бонус-оверлей и HUD

- Хост читает `MatchController` напрямую.
- Клиент предпочитает снапшот v13, но **если снапшота ещё нет** — fallback на локальный controller после `StartMatch`. Иначе оверлей пустой, а часы зависают на `00:00`.
- Снапшот публикуется сразу в `BeginMatchOnServer` (`PublishSnapshotNow`), не только по таймеру `SnapshotHz`.
- `MatchSnapshotCodec` читает только текущую версию (v21+); смешанные билды закрываются handshake'ом версии в лобби (`SnapshotVersionGate`, см. `snapshot-wire.md`). Исторический урок: хардкод allow-list версий при бампе ронял каждый клиентский RPC — не возвращать.

## Start → Early

Камера летит к базе (`IsFocusInProgress`). Early **не** ждёт race-pick pan lock. Таймаут `MatchRules.StartPhaseMaxWaitSeconds` (5 с, GDD `PHASE_START`), чтобы застрявшая камера не держала часы на нуле.

## Host drop и миграция

`MatchNetworkAuthority.OnNetworkDespawn` при живом session handle оставляет `TickMode.Client` — **не** `Offline`. Иначе каждый клиент начинает свою симуляцию (split-brain), бонус-оверлей «внезапно появляется», и они играют не друг с другом.

Capture для миграции: last-good bytes, иначе **локальный** `MatchController` любого пира (не только слот бывшего хоста). Пустой last-good на клиентах — норма, если снапшот не дошёл.

**Rejoin timeout:** `HostMigrationSessionDriver.RunRebindAsync` ждёт (`HostMigrationRules.ClientRejoinTimeoutSeconds`) всех клиентов после Relay rebind. Если timeout истёк, а не все rejoined — designated host вызывает `NetworkLobbyState.EliminateNonRejoinedSlots()`, которая kick все reserved (не-занятые) слоты через `KickDisconnected(fromPendingMigration: true)`. Матч может продолжиться с меньшей ростером.

**Reconnect token:** persist immediately при slot claim (`TryClaimReconnect`) и после host migration (`TryMigrateAsListenHostAsync`, `TryRejoinMigratedHostAsync`), не только по 10 s таймеру. Это защищает от crash сразу после migration с устаревшим room code.

## Плавный рендер юнитов (snapshot interpolation)

Хост тикает симуляцию 30 Гц, снапшоты — `MatchNetworkAuthority.SnapshotHz = 30` (два сэмпла
в буфере, меньше ощущаемый лаг, чем на 15 Гц). Прямое применение без задержки дёргает юнитов.
Рендер **клиентов и хоста** построен на **snapshot interpolation** по серверному времени:

- `MatchCombatSystem.RecordLocalRenderSamples` записывает sim positions в `UnitRenderTrack` после каждого Tick (хост) или при `ApplyAuthoritativeUnits` (клиенты).
- `MatchCombatSystem.ApplyAuthoritativeUnits(..., matchTimeSeconds)` пишет каждый снапшот юнита в
  `UnitRenderTrack` (кольцевой буфер до 8 сэмплов, дубликаты/обратные таймстампы отбрасываются).
  Один track на `unitId`, чистится при удалении юнита.
- **Adaptive interpolation delay:** `MatchRuntime.AdaptiveInterpDelaySeconds` вычисляется из последних 16 snapshot arrival intervals. Целевой delay = 3× avg interval + jitter spike, clamped to [~100ms, 150ms] (3–4.5 snapshots at 30 Hz). Это защищает от underruns при Relay jitter.
- Презентер (`MatchCombatPresenter`) на **клиенте** семплирует пару по `renderTime`:
  `serverTimeEstimate = snapshot.MatchTimeSeconds + (Time.time - MatchRuntime.LastSnapshotArrivalRealtime)`,
  `renderTime = serverTimeEstimate - AdaptiveInterpDelaySeconds`.
- Презентер на **хосте** семплирует по `renderTime = MatchTimeSeconds - MinInterpDelaySeconds` (~100ms) для буферизации.
  Позиция — `Vector3.Lerp(prev, next, alpha)`, поворот — `ResolveRenderFacing` (анти-crossing по world-up),
  `BehaviorState` / `AttackSwingSerial` — из ближайшего по `alpha` сэмпла (анимации тоже плавные).
- Первый спавн визуала всегда — мгновенный snap (без interpolation).
- `LastSnapshotArrivalRealtime` сбрасывается на `OnSessionStarted` и не выставляется вне `ApplyNetworkSnapshot`.

Снаряды **не** интерполируют снапшотные позиции (в wire — one-shot spawn). Меш летит по
известной баллистике: `CombatProjectileState.ResolvePresentationProgress` от `SpawnRealtime`
(wall-clock `Time.time - SpawnRealtime`, не 30 Hz `Elapsed` — гладко на ~60 fps).
Урон по-прежнему на 30 Гц `Elapsed` (authoritative). Событие снаряда несёт `AppliesSplashAoe` —
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
- **Выход из лобби/матча обязан покидать UGS Lobby**: `MatchNetworkSession.Shutdown` fire-and-forget зовёт `IMatchSessionBackend.LeaveAsync(lobbyId)` (реализация — `RemovePlayerAsync` со своим PlayerId; в UGS Lobbies нет self-leave). Без этого повторный `JoinLobbyByCodeAsync` падает 409 «already in lobby» до рестарта приложения.
- Быстрый leave→join: NGO шатдаун асинхронен, `StartAsClient/Host` молча отказывают. `TryStartTransportAsync` ждёт `MatchNetworkBootstrap.WaitForShutdownCompleteAsync()` (≤3 s) перед стартом. Если shutdown всё ещё in progress после 3 s, `WaitForShutdownCompleteAsync` возвращает `false`, и `TryStartTransportAsync` возвращает `false` (UI может показать retry или error).
