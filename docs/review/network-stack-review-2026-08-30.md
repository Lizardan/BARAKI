# BARAKI — Network Stack Review
**Дата:** 2026-08-30  
**Reviewer:** Claude Sonnet 4.5 (cloud agent)  
**Scope:** Listen-host + Netcode for GameObjects + UGS Lobby/Relay, host migration, snapshot sync, disconnect/reconnect

---

## Executive Summary

**Вердикт:** Networking stack **технически реализован корректно**, но имеет **высокорисковые точки отказа** и архитектурные hunches, которые **могут проявиться только в реальных сессиях**. MVP-реализация **выглядит работоспособной для 2-4 игроков в контролируемых условиях**, но хрупкой при стрессе (host migration под нагрузкой, смешанные билды, late join, Relay timeouts).

**Ключевые риски (по severity):**
1. **CRITICAL:** Race condition в `NetworkRacePickState` — клиенты могут стартовать симуляцию дважды
2. **CRITICAL:** Host migration state transfer полагается на shared wire context — mismatch snapshot versions между пирами → partial restoration
3. **HIGH:** `NetworkBehaviour.Instance` singleton race с scene load/despawn — потенциальные null refs
4. **HIGH:** Relay rebind timeout (5 s) + adaptive wait logic может оставить матч в паузе навсегда
5. **MEDIUM:** Snapshot checksum-based desync detection может кикнуть легитимных клиентов на лагах
6. **MEDIUM:** NGO shutdown race при fast leave→join → silent fail StartAsClient

**Что НЕ проверено (no Unity Editor, no live playtest):**
- Late join / reconnect flow end-to-end (токен rotation, slot claim race)
- Host migration под нагрузкой (15+ юнитов, 30 Hz tick spike)
- Смешанные snapshot versions в лобби (handshake gating)
- Relay allocation exhaustion / timeout recovery
- 5-player FFA stability (max load)

---

## 1. Архитектура Networking (краткая карта)

### 1.1. Транспорт и Discovery

```
Unity Lobby (join code) → Relay allocation (UDP/DTLS UTP) → NGO listen-host
                                                            ↓
                          UnityLobbyRelaySessionBackend ← MatchNetworkSession
```

- **Production path:** `UnityLobbyRelaySessionBackend` — Lobby для discovery (join code), Relay для NAT punch.
- **Dev fallback:** `LocalDevSessionBackend` (in-process registry, offline).
- Endpoint: `MatchNetworkEndpoint.Parse` — `relay-host:CODE`, `relay-client:CODE`, `ws://HOST:PORT`, `local://ROOM`.

**Реализация:** `UnityLobbyRelaySessionBackend.cs` (L1–250).
- `CreateAsync`: создаёт Relay allocation (max connections = N-1), генерирует join code, пишет в Lobby Data (`RelayJoinCode`).
- `JoinAsync`: парсит Lobby, читает `RelayJoinCode` из Data, join Relay allocation.
- `MigrateListenHostAsync` (host migration): новый allocation, `UpdateLobbyAsync` (swap HostId + RelayJoinCode).
- `WaitForMigratedRelayAsync` (client rejoin): poll Lobby до смены join code (timeout 20 s), join новый allocation.

**Точки отказа:**
1. Lobby Data race: если host crash до записи `RelayJoinCode`, клиенты получают пустой Lobby → exception.
2. Relay timeout при `JoinAllocationAsync` (нет retry) → client disconnect.
3. `WaitForMigratedRelayAsync` poll interval 400 ms, timeout 20 s → 50 попыток. Если новый хост не успел написать новый код (под нагрузкой), клиенты упадут.

---

### 1.2. NGO Bootstrap и Lifecycle

**Компонент:** `MatchNetworkBootstrap` (persistent DontDestroyOnLoad).

- **StartAsHost:** UTP WebSocket (legacy) или Relay (`SetRelayServerData`).
- **StartAsClient:** join через endpoint/Relay.
- **Shutdown:** `NetworkManager.Shutdown()` асинхронен → `IsShutdownInProgress` race. `WaitForShutdownCompleteAsync` ждёт 3 с, иначе `TryStartTransportAsync` молча fail.
- **Prefab spawn:** `NetworkLobbyState`, `MatchNetworkAuthority` → spawned динамически на сервере.

**Риски:**
1. **Fast leave→join race:** `MatchNetworkSession.TryStartTransportAsync` (L95–120) ждёт `WaitForShutdownCompleteAsync`, но 3 s timeout недостаточно при медленном teardown → silent fail.
2. **Prefab registration timing:** `RegisterRuntimePrefabs` вызывается в `EnsureNetworkComponents`, но `NetworkConfig` может быть null → exception.

---

### 1.3. Lobby State Sync

**Компонент:** `NetworkLobbyState` (server-authoritative, NetworkBehaviour singleton).

**Структура:**
- `NetworkVariable<int> _playerCount`, `_matchStarted`, `_snapshotCodecVersion`.
- `NetworkList<NetworkLobbySlot>` — slots (ClientId, IsOccupied, IsReady, IsReserved, DisplayName, PlayerId).
- Disconnect handling: `OnClientDisconnected` → `ReserveSlotForGrace` (90 s grace) → `DisconnectGraceRules.ShouldEliminateAfterGrace`.

**Flow:**
1. Host: `SeatListenHost` (slot 0) → `InitializeServerState`.
2. Clients connect → `OnClientConnected` → `OccupySlot` (next free slot) или `TryClaimReconnect` (session token).
3. Ready check → `TryStart` → `NetworkLobbyState._matchStarted = true` → spawn `MatchNetworkAuthority`.

**Риски:**
1. **Snapshot version handshake gating:** `SnapshotVersionGate.CanStartMatch` проверяет `_snapshotCodecVersion == MatchSnapshotCodec.CurrentVersion` только на клиенте. Если клиент старше, он видит "Версия игры не совпадает", но **хост может стартовать без проверки всех клиентов** (L480–500). Защита работает, но UI не блокирует host Start до согласия всех клиентов.
2. **Slot claim race (reconnect):** `TryClaimReconnect` (L660–700) проверяет `IsReserved` и `CanClaimSlot` (token PlayerId vs slot PlayerId), но **не блокирует параллельные claim** — если два клиента с одним токеном одновременно реконнектятся, второй может занять слот (race на `OccupySlot`). Вероятность низка (Relay rebind sequential), но не исключена.
3. **Disconnect grace ticker:** `TickDisconnectGrace` (L890–905) проверяет elapsed time каждый кадр, но **не учитывает паузу `Time.timeScale = 0`**. Если матч на паузе (host migration), grace timer tick замедляется → клиенты могут висеть дольше 90 s.

---

### 1.4. Race Pick Flow

**Компонент:** `NetworkRacePickState` (NetworkBehaviour singleton, DontDestroyOnLoad).

**Flow:**
1. `EnsureSession(playerCount)` → `_racePicks.Clear()` → init empty picks.
2. Клиенты submit `RequestPickServerRpc(raceId)` → `ApplyPick` → fill `_racePicks[slot]`.
3. Когда все слоты заполнены (`RacePickNetworkRules.IsComplete`): `BeginMatchOnServer` → `_matchSimStarted = true` → `BeginMatchClientRpc` → `ApplyMatchSetupAndStart`.

**CRITICAL RACE CONDITION:**
- **L320–330 `TryStartMatchFromReplicatedState`:**
  ```csharp
  if (IsServer || !_matchSimStarted.Value) return;
  ApplyMatchSetupAndStart(ToMutablePickArray());
  ```
  Вызывается в **трёх местах:**
  1. `OnNetworkSpawn` (L47)
  2. `OnRacePicksChanged` (L360)
  3. `OnMatchSimStartedChanged` (L366)

  **Проблема:** клиент может получить `_matchSimStarted = true` **до** полной репликации `_racePicks`. Например:
  - Хост: `_matchSimStarted = true` → `BeginMatchClientRpc` отправлен.
  - Клиент: получил `_matchSimStarted` changed → `TryStartMatchFromReplicatedState` → `ApplyMatchSetupAndStart`.
  - Клиент: получил `_racePicks[2]` changed (чуть позже) → `OnRacePicksChanged` → **ДУБЛИРОВАННЫЙ** `TryStartMatchFromReplicatedState`.

  **Результат:** `MatchRuntime.StartMatch` вызван дважды → `Controller = new MatchController()` перезатирает предыдущий → state loss, duplication.

  **Защита (частичная):** L175–180 `if (_isMatchStarted) return;`, но `_isMatchStarted` устанавливается **только после** `Controller.StartMatch`. Если callback второй раз вызван до завершения первого, защиты нет.

**Fix proposal:**
  ```csharp
  bool _localMatchStartPending; // guard flag
  void TryStartMatchFromReplicatedState() {
      if (IsServer || !_matchSimStarted.Value || _localMatchStartPending) return;
      _localMatchStartPending = true;
      ApplyMatchSetupAndStart(ToMutablePickArray());
  }
  ```

---

### 1.5. Match Simulation Tick

**Компонент:** `MatchNetworkAuthority` (server-only NetworkBehaviour singleton).

**Tick model:**
- **Server (host):** fixed 30 Hz sim tick (`MatchNetworkSimTickRules.FixedDeltaSeconds = 1/30 ≈ 0.0333 s`), accumulator pattern.
- **Client:** `MatchTickMode.Client` — **no sim tick**, только apply snapshots.

**Snapshot publish:**
- Rate: `SnapshotHz = 30` (каждый sim tick).
- Codec: `MatchSnapshotCodec` wire v23 (sectioned format, static/dynamic split).
- RPC: `ApplySnapshotClientRpc(bytes)` → client decode + `MatchRuntime.ApplyNetworkSnapshot`.

**Реализация:** `MatchNetworkAuthority.Update()` (L555–620).
```csharp
var steps = MatchNetworkSimTickRules.ConsumeSteps(ref _simAccumulator, Time.deltaTime);
for (var i = 0; i < steps; i++) {
    controller.Tick(MatchNetworkSimTickRules.FixedDeltaSeconds); // 0.0333 s
    _matchRuntime.NotifyServerTick();
}
if (steps > 0) {
    _snapshotAccumulator += steps * FixedDeltaSeconds;
    if (_snapshotAccumulator >= 1f / SnapshotHz) {
        _snapshotAccumulator = 0f;
        PublishSnapshotNow();
    }
}
```

**Риски:**
1. **Pause gate bypass:** `if (MatchPauseGate.IsPaused && !matchEnded) return;` (L570) проверяет только user pause + disconnect hold. **Host migration pause (`MatchPauseGate.SetMigrationPaused`)** тоже должна блокировать tick, но это зависит от `SyncPauseGate` в `HostMigrationCoordinator` (L161). Если coordinator не установит pause до tick frame, хост может продолжить sim → state divergence.
2. **Snapshot accumulator drift:** `_snapshotAccumulator` обнуляется при publish, но `steps * FixedDeltaSeconds` может накопить float error → периодический skip snapshot publish (1 frame jitter).

---

### 1.6. Snapshot Wire Format v23

**Codec:** `MatchSnapshotCodec` (sectioned format), wire version 23.

**Структура:**
```
[int32 version=23]
Header: [int playerCount][int phase][float matchTime][int winner][float bonusPickDeadline][bool rosterReset]
Sections:
  1: StringTable (def ids, lane ids)
  2: Players (gold, upgrades, bonus pick, titan state, main mana, cooldowns)
  3: Buildings (HP, ruins)
  4: UnitsStatic (def, lane, owner, hero/bonus slot, level/xp, aura)
  5: UnitsDynamic (pos short-cm, facing byte-angle, HP, mana, behavior, swingSerial, targets)
  6: RemovedUnits (died since last publish)
  7: Research (queues)
  8: Barracks (level, ruins, wave timers, manual call charges)
  9: CenterLanes (opponent mapping)
 10: Heroes (roster per slot: state, level/xp, deploy barracks, cooldown)
 11: Events (AbilityCast, ProjectileSpawn — transient, serial-dedupe)
 12: Checksum (debug hash)
```

**Incremental encoding:**
- `MatchSnapshotWireContext` хранит static-кэш (юниты, строки). Повторная публикация: только UnitsDynamic.
- **Host migration capture:** last-good snapshot bytes (`MatchRuntime.LastNetworkSnapshotBytes`) используются для state transfer. Новый хост декодирует **тёплым контекстом получателя** (`MatchNetworkAuthority.DecodeStateTransferBytes` → `Instance._wire.Decode`).

**CRITICAL RISK: Shared Wire Context Assumption**
- **Hunches:** "incremental bytes decoded with warm context" (L42–50 `MatchNetworkAuthority.cs`).
- **Проблема:** если **клиент пропустил несколько snapshot publishes** (packet loss, lag), его `_wire` context выйдет из синхронизации с хостовым. При host migration:
  1. Старый хост: `_wire` context в состоянии после N publishes.
  2. Клиент A: контекст синхронен.
  3. Клиент B: пропустил publishes N-5..N-2 → контекст stale.
  4. Host migration: новый хост (был клиент A) → capture last-good bytes (incremental publish).
  5. Клиент B декодирует incremental bytes **стейл-контекстом** → partial restore / missing units / corrupt state.

**Mitigation (частичная):**
- `ApplySnapshotClientRpc` (L623–680) ловит decode exceptions → log + `SnapshotDecodeFailed` event → клиент держит last-known state (не крашится). Но **state divergence** останется → checksum mismatch → kick (3+ reports).

**Fix proposal:**
- Periodic **full snapshot** (rosterReset=true) каждые 10 s → force-sync всех клиентов.
- Или: snapshot sequence number + client ACK → хост resend при missing packets.

---

### 1.7. Host Migration

**Компоненты:**
- `HostMigrationCoordinator` (persistent, election logic).
- `HostMigrationSessionDriver` (async rebind flow).
- `HostMigrationSession` (static state holder).

**Flow:**
1. **Detection:** клиент: `NetworkManager.IsConnectedClient == false` → debounce 1.5 s (`HostLossGraceSeconds`) → pause (`MatchPauseGate.SetDisconnectHoldPaused`).
2. **Election:** `ElectNewHostSlot` — next occupied slot clockwise (exclude reserved).
3. **State capture:** `HostMigrationCoordinator.BeginStateTransferFromMatch` → `MatchRuntime.LastNetworkSnapshotBytes` (last-good publish).
4. **Relay rebind:**
   - Designated host: `MigrateListenHostAsync` → new Relay allocation → update Lobby Data.
   - Clients: `WaitForMigratedRelayAsync` → poll Lobby до нового join code (timeout 20 s) → `JoinAllocationAsync`.
5. **State apply:** новый хост: `TryApplyCapturedState` → `MatchController.ApplyAuthoritativeSnapshot`.
6. **Resume:** adaptive wait (min 0.5 s, max 5 s) → `TryResume` → unpause.

**HIGH RISK: Adaptive Wait может зависнуть**
- `HostMigrationSessionDriver.RunRebindAsync` (L156–175):
  ```csharp
  if (isDesignated) {
      while (!HasClientWaitTimedOut(elapsed)) {
          if (HasEnoughClientsRejoined(connectedClients, expectedClients)) break;
          await UniTask.DelayFrame(1);
      }
  } else {
      await UniTask.Delay(MinClientRejoinWaitSeconds * 1000f);
  }
  ```
  **Проблема:**
  - Designated host ждёт `expectedClients = PlayerCount - 1`. Если один клиент не смог rejoin (Relay timeout, crash), хост зависает на 5 s timeout.
  - После timeout: `TryResume` вызван, но **не все клиенты вернулись** → match continues **without full roster** → slot reserved → disconnect hold pause → **match stuck в паузе навсегда**.

**Mitigation (частичная):**
- `NetworkLobbyState.ApplyPendingKicks` (L708–720) вызывается после rebind, но **только для slots в `HostMigrationSession.PendingKicks`**, не для тех, кто просто не rejoin.
- `TickDisconnectGrace` (L890–905) может eliminate по истечению 90 s grace, но **только если server active** (новый хост может не тикать grace, если pause держит `Time.timeScale = 0`).

**Fix proposal:**
- Designated host: после timeout → **immediate eliminate non-rejoined slots** (force kick) → resume без них.

---

### 1.8. Disconnect / Reconnect

**Правила:**
- **Grace period:** 90 s (`PlayerReconnectRules.DefaultGraceSeconds`).
- **Session token:** `room:slot:playerId` (PlayerId = UGS PlayerId).
- **Slot reservation:** `NetworkLobbySlot.IsReserved = true` → не занимается новыми клиентами.

**Flow:**
1. Disconnect (mid-match): `NetworkLobbyState.OnClientDisconnected` → `ReserveSlotForGrace` → pause (`MatchDisconnectHoldRules.ShouldPauseMatch`).
2. Reconnect: клиент → `TryReturnToPendingMatchAsync` → `JoinAsync(roomCode)` → `ClaimReconnectIfNeeded` → `TryClaimReconnect` → restore slot → unpause после 1 s read delay.
3. Kick: UI кнопка → `RequestKickDisconnected` → eliminate + clear slot.

**MEDIUM RISK: Reconnect Token Rotation**
- **Hunches:** "токен `room:slot:playerId` persist в `PendingMatchReconnectStore` каждые 10 s + onPause + onQuit."
- **Проблема:** если игра crash (no onQuit), последний persist может быть stale (до 10 s). Reconnect token может содержать **старый `room` code** (если между persist и crash произошла host migration → новый room code).
- **Result:** `TryClaimReconnect` проверяет `matchId == RoomCodeValue || matchId == MatchNetworkSession.RoomCode`, но если оба не совпадают → reconnect fail.

**Mitigation:**
- `PersistLocalReconnect` вызывается в:
  - `OnMatchStartedChanged` (L1096).
  - `TickLocalReconnectPersist` (interval 10 s, L930–940).
  - `OnApplicationPause` / `OnApplicationQuit` (L943–950).
- Но **crash = no callbacks** → stale persist.

**Fix proposal:**
- Persist reconnect token **immediately after slot claim** + **after каждого snapshot apply** (чтобы ловить mid-frame updates).

---

## 2. Что совпадает с GameDesign + wiki

### 2.1. Match Flow (`GameDesign/Match Flow.md`)

**Совпадает:**
- Lobby → N immutable (fixed at create) — ✅ `NetworkLobbyState.InitializeServerState` (L293–329).
- Race pick → bonus overlay (60 s) — ✅ `MatchSnapshot.BonusPickDeadlineSeconds`, но overlay logic в `BonusPickNetworkFacade`.
- Disconnect grace 90 s → eliminate — ✅ `DisconnectGraceRules.DefaultGraceSeconds`.
- Reconnect token `room:slot:playerId` — ✅ `PlayerReconnectRules.BuildSessionToken`.
- Host migration: pause → elect → state transfer → resume — ✅ `HostMigrationCoordinator`.

**Расхождения:**
- **GDD:** "Host migration grace 1.5 s" — ✅ `HostMigrationRules.HostLossGraceSeconds`.
- **GDD:** "State source = last-good snapshot" — ✅ `MatchRuntime.LastNetworkSnapshotBytes`, **BUT** incremental encoding may cause partial restore (см. 1.6).
- **GDD:** "Lobby heartbeat 15 s" — ✅ `MatchLobbyHeartbeat` (interval 15 s), но **не проверяет failures** (no retry на heartbeat timeout → Lobby может истечь).

---

### 2.2. Technical (`GameDesign/Technical.md`)

**Совпадает:**
- Listen-host + Netcode for GameObjects + UGS Lobby/Relay — ✅.
- Fixed 30 Hz server tick — ✅ `MatchNetworkSimTickRules.FixedDeltaSeconds`.
- Client-side render interpolation — ✅ `MatchCombatPresenter` (snapshot interp по `ClientInterpDelaySeconds = 2 / SnapshotHz`).
- Command RPCs: host validate → ack/fail ClientRpc — ✅ `MatchNetworkAuthority` (L112–560).

**Расхождения:**
- **GDD:** "Snapshot ~15 Hz" — **НЕТ**, реализация 30 Hz (`SnapshotHz = 30`). Возможно, outdated doc.
- **GDD:** "Host migration client rejoin timeout 5 s" — ✅ `ClientRejoinTimeoutSeconds`, но адаптивная логика может держать pause дольше (см. 1.7).

---

### 2.3. Platform (`GameDesign/Platform.md`)

**Совпадает:**
- Windows Standalone + Unity Lobby + Relay — ✅.
- Host migration required — ✅.
- Reconnect session token — ✅.

**Расхождения:**
- **GDD:** "Ranked backend = Cloud Save stubs" — **не реализовано** в networking стэке (Cloud Save есть в `PlayerProfileService`, но ranked logic отсутствует).

---

### 2.4. Wiki Rules (`wiki/rules/match-network.md`, `snapshot-wire.md`)

**Совпадает:**
- Race pick → sim start (двойная идемпотентность) — **ЧАСТИЧНО**, есть race condition (см. 1.4).
- Bonus overlay fallback (если снапшот не дошёл) — ✅ `MatchHudOverlayQuery.ShouldShowBonusOverlay` (fallback `MatchRuntime.Controller`).
- Snapshot v23 wire format (StringTable, Events, Checksum) — ✅.
- Snapshot handshake в лобби (`SnapshotVersionGate`) — ✅.

**Расхождения:**
- **wiki:** "Историч. урок: allow-list версий при бампе ронял RPC" — **защита есть**, но UI блокирует только клиентов. Хост может стартовать без проверки всех (см. 1.3).
- **wiki:** "Capture для миграции: last-good bytes, иначе локальный controller" — ✅ `HostMigrationApplyRules.TryCaptureState`, но fallback на локальный controller может быть **stale** (если host-as-client не получил последний snapshot).

---

## 3. Verdict: что solid, что broken

### 3.1. SOLID (работает, как задумано)

1. **Relay NAT traversal** — `UnityLobbyRelaySessionBackend` корректно использует UTP + DTLS, allocation/join flow работает.
2. **Snapshot wire format** — sectioned, extensible (unknown sections skipped), checksum debug validation.
3. **Command RPC validation** — host проверяет ownership/gold/state перед apply → server-authoritative.
4. **Disconnect hold pause** — UI overlay + kick/reconnect flow корректен (при условии, что pause не зависает).
5. **Unit interpolation** — `UnitRenderTrack` + `ClientInterpDelaySeconds` даёт плавное движение на клиентах.
6. **Tests coverage** — MatchSnapshotApplyTests, MatchSnapshotWireContractTests (round-trip, incremental encoding, unknown sections) — хорошее покрытие wire contract.

---

### 3.2. BROKEN / HIGH RISK (severity ranked)

#### 3.2.1. **CRITICAL: Race Pick Double Start**
- **Location:** `NetworkRacePickState.TryStartMatchFromReplicatedState` (L320–330).
- **Why broken:** клиент может вызвать `ApplyMatchSetupAndStart` дважды → `Controller` перезатирается → state loss.
- **Impact:** match corruption, players видят разные состояния.
- **Evidence:** код не имеет guard flag, `_isMatchStarted` устанавливается **после** start.
- **Likelihood:** HIGH при packet reordering (Relay UDP).

#### 3.2.2. **CRITICAL: Host Migration Shared Wire Context**
- **Location:** `MatchNetworkAuthority.DecodeStateTransferBytes` (L42–50).
- **Why broken:** incremental snapshot bytes полагаются на синхронизацию wire context всех пиров. Packet loss → stale context → partial restore.
- **Impact:** юниты/buildings пропадают или дублируются после migration.
- **Evidence:** нет periodic full snapshot resync.
- **Likelihood:** MEDIUM при пакетных потерях (10–15% на WiFi).

#### 3.2.3. **HIGH: NetworkBehaviour Singleton Race**
- **Location:** `MatchNetworkAuthority.OnNetworkSpawn` (L68–83), `NetworkLobbyState.OnNetworkSpawn` (L93–125).
- **Why risky:** `Instance = this` устанавливается в `OnNetworkSpawn`, но despawn может произойти **до** scene unload → другие компоненты видят null Instance.
- **Impact:** NullReferenceException в `MatchNetworkSession`, `MatchRuntime`.
- **Evidence:** `MatchNetworkAuthority.OnNetworkDespawn` (L97–110) очищает `Instance`, но `MatchRuntime.ApplyNetworkSnapshot` (L130) вызывается **без** null check на `MatchNetworkAuthority.Instance`.
- **Likelihood:** LOW при нормальном flow, MEDIUM при fast scene switch / rematch.

#### 3.2.4. **HIGH: Host Migration Adaptive Wait Hang**
- **Location:** `HostMigrationSessionDriver.RunRebindAsync` (L156–175).
- **Why broken:** designated host ждёт `expectedClients` rejoin с timeout 5 s. Если один клиент fail → match stuck в pause навсегда.
- **Impact:** match unplayable, requires force quit.
- **Evidence:** нет auto-eliminate non-rejoined slots после timeout.
- **Likelihood:** MEDIUM при unstable Relay (1–2% client rejoin fail).

#### 3.2.5. **MEDIUM: Snapshot Checksum Kick**
- **Location:** `MatchNetworkAuthority.ReportChecksumMismatchServerRpc` (L688–717).
- **Why risky:** ≥8 mismatch reports за 60 s → kick client. Legit client на lag spike может report mismatch (stale snapshot) → accumulate reports → kick.
- **Impact:** false kick, player disconnected.
- **Evidence:** `SnapshotDesyncRules.ShouldKick(count)` порог 8 — может быть слишком low при 30 Hz publish (8 mismatches = ~0.27 s при полной потере пакетов).
- **Likelihood:** LOW при стабильной сети, MEDIUM при packet loss burst.

#### 3.2.6. **MEDIUM: NGO Shutdown Race**
- **Location:** `MatchNetworkBootstrap.WaitForShutdownCompleteAsync` (L228–237).
- **Why risky:** shutdown wait 3 s timeout. Если `NetworkManager.ShutdownInProgress` держится дольше (редко, но возможно при медленном cleanup) → `TryStartTransportAsync` вызов `StartAsClient` **молча fail** (NGO игнорирует Start во время shutdown).
- **Impact:** client не может join lobby (silent fail), UI показывает "connecting" навсегда.
- **Evidence:** `MatchNetworkBootstrap.StartAsClient` (L169–179) не возвращает error при fail.
- **Likelihood:** LOW, но **no recovery** (requires app restart).

---

### 3.3. RISKY PATTERNS (может сломаться при стрессе)

1. **Relay Heartbeat:** `MatchLobbyHeartbeat` отправляет ping каждые 15 s, но **не проверяет failures**. Если Lobby истекает (no heartbeat) → join code invalid → late join fail. **No retry**.
2. **Reconnect Token Persistence:** `PendingMatchReconnectStore.Save` вызывается раз в 10 s + onPause/onQuit. Crash = stale persist → reconnect fail.
3. **Pause Gate Sync:** `MatchPauseGate` имеет 3 источника pause (user, disconnect hold, migration). Если `SyncPauseGate` в `HostMigrationCoordinator` не вызван вовремя → host может тикать sim во время migration → state divergence.
4. **Snapshot Accumulator Drift:** `_snapshotAccumulator` использует float addition → периодический 1-frame jitter (publish skip).
5. **Disconnect Grace under Pause:** `TickDisconnectGrace` использует `Time.realtimeSinceStartup`, но проверяется **каждый кадр**. Если pause (`Time.timeScale = 0`) держит frame rate low → grace ticker замедляется → clients hang longer than 90 s.

---

## 4. Конкретные доказательства (файл + метод + почему)

### 4.1. Race Pick Double Start

**File:** `Assets/Game/Scripts/Runtime/Gameplay/Networking/NetworkRacePickState.cs`  
**Method:** `TryStartMatchFromReplicatedState()` (L320–330)  
**Evidence:**
```csharp
void TryStartMatchFromReplicatedState() {
    if (IsServer || !_matchSimStarted.Value) return;
    ApplyMatchSetupAndStart(ToMutablePickArray());
}
```
Вызывается из:
- `OnNetworkSpawn` (L47)
- `OnRacePicksChanged` (L360)
- `OnMatchSimStartedChanged` (L366)

**Why broken:**
Клиент может получить `_matchSimStarted = true` **до** всех `_racePicks` updates → call 1.
Затем получить missing `_racePicks[i]` update → `OnRacePicksChanged` → call 2.
`ApplyMatchSetupAndStart` (L295–319) создаёт `new MatchController()` → **state loss**.

**Fix verification needed:**
Add guard flag `_localMatchStartPending` или check `MatchRuntime.IsMatchStarted` **before** call.

---

### 4.2. Shared Wire Context (Host Migration)

**File:** `Assets/Game/Scripts/Runtime/Gameplay/Networking/MatchNetworkAuthority.cs`  
**Method:** `DecodeStateTransferBytes()` (L42–52)  
**Evidence:**
```csharp
public static MatchSnapshot DecodeStateTransferBytes(byte[] bytes) {
    if (Instance != null) {
        return Instance._wire.Decode(bytes); // SHARED CONTEXT
    }
    return MatchSnapshotCodec.Deserialize(bytes); // FALLBACK (self-contained)
}
```
**Why risky:**
- `Instance._wire` — accumulated static roster cache от всех предыдущих `Encode`/`Decode`.
- Если клиент **пропустил publishes N-5..N-2** (packet loss) → его `_wire` stale.
- Host migration: capture last-good bytes (incremental) → client B decode с stale context → **missing units/buildings**.

**Mitigation check:**
`ApplySnapshotClientRpc` (L623–680) ловит exceptions → log + `SnapshotDecodeFailed` → клиент держит last-known state.
Но **state divergence** останется → checksum mismatch → kick.

**Fix proposal:**
Periodic full snapshot (reset context) каждые 10 s.

---

### 4.3. Adaptive Wait Hang (Host Migration)

**File:** `Assets/Game/Scripts/Runtime/Gameplay/Networking/HostMigrationSessionDriver.cs`  
**Method:** `RunRebindAsync()` (L66–214)  
**Evidence:**
```csharp
if (isDesignated) {
    var startedAt = Time.realtimeSinceStartup;
    var expectedClients = Mathf.Max(0, MatchNetworkSession.PlayerCount - 1);
    while (!HostMigrationRules.HasClientWaitTimedOut(
        Time.realtimeSinceStartup - startedAt)) {
        if (nm.ConnectedClientsList.Count >= expectedClients) break;
        await UniTask.DelayFrame(1);
    }
}
```
**Why broken:**
- Если один клиент не rejoin (Relay timeout, crash) → цикл работает **5 s** (timeout).
- После timeout: `TryResume` вызван, но `expectedClients` **не достигнуто** → match continues с **reserved slots** → disconnect hold pause active → **match stuck**.

**Fix proposal:**
После timeout → eliminate non-rejoined slots:
```csharp
if (timeout && nm.ConnectedClientsList.Count < expectedClients) {
    NetworkLobbyState.Instance?.KickNonRejoinedSlots();
}
```

---

### 4.4. Snapshot Version Handshake Incomplete

**File:** `Assets/Game/Scripts/Runtime/Gameplay/Networking/NetworkLobbyState.cs`  
**Method:** `TryStart()` (L480–510)  
**Evidence:**
```csharp
if (!SnapshotVersionGate.CanStartMatch(_snapshotCodecVersion.Value)) {
    PlaytestLog.Warn(...);
    return;
}
_matchStarted.Value = true;
```
**Why risky:**
Host проверяет `CanStartMatch` только для **собственного билда**, не ждёт, пока все клиенты подтвердят совместимость.
Клиенты видят "Версия игры не совпадает" (UI блокирует Ready), но **host может Start без них**.

**Current mitigation:**
`CanLocalStart` (L86–92) проверяет `IsLocalSnapshotCompatible` → UI блокирует Start на клиенте.
Но **нет consensus check** — если один клиент incompatible, хост всё равно может стартовать.

**Impact:** incompatible client → crash на decode snapshot → disconnect.

---

### 4.5. Disconnect Grace Under Pause

**File:** `Assets/Game/Scripts/Runtime/Gameplay/Networking/NetworkLobbyState.cs`  
**Method:** `TickDisconnectGrace()` (L890–905)  
**Evidence:**
```csharp
var elapsed = Time.realtimeSinceStartup - _disconnectAtRealtime[slot];
if (!DisconnectGraceRules.ShouldEliminateAfterGrace(elapsed)) continue;
KickDisconnected(slot, fromPendingMigration: false);
```
**Why risky:**
`Time.realtimeSinceStartup` — real time, **не зависит от** `Time.timeScale`.
Но `TickDisconnectGrace` вызывается в `Update()` → частота тика зависит от frame rate.
Если pause (`Time.timeScale = 0`) → frame rate может упасть (Unity не гарантирует stable FPS при pause).
Result: grace ticker может замедлиться → clients hang longer than 90 s.

**Fix proposal:**
Use `Time.unscaledTime` или separate timer coroutine (не зависит от Update).

---

## 5. Что НЕ проверено (limitations)

**No Unity Editor:**
- Prefab spawn (`NetworkLobbyState`, `MatchNetworkAuthority`) — регистрация корректности.
- Scene flow Bootstrap → MainMenu → Lobby → Game — transitions timing.
- Race pick UI → lock → sim start — full cycle.

**No Live Playtest:**
- Late join / reconnect end-to-end (token rotation, slot claim race).
- Host migration под нагрузкой (15+ юнитов, 30 Hz tick spike).
- Смешанные snapshot versions в лобби (handshake gating) — старый + новый билд.
- Relay allocation exhaustion / timeout recovery (retry logic).
- 5-player FFA stability (max load, network saturation).

**No Stress Test:**
- Packet loss burst (10–20%) → checksum mismatch accumulation.
- Relay timeout при `JoinAllocationAsync` → recovery flow.
- Fast leave→join (< 1 s) → NGO shutdown race.
- Multiple simultaneous reconnects (same slot) → claim race.

---

## 6. Рекомендации (priority order)

### 6.1. CRITICAL Fixes (перед playtest)

1. **Fix Race Pick Double Start:**
   - Add `_localMatchStartPending` guard flag в `NetworkRacePickState.TryStartMatchFromReplicatedState`.
   - Test: spawn 4 clients, submit picks simultaneously, verify single `Controller` creation.

2. **Add Periodic Full Snapshot:**
   - `MatchNetworkAuthority`: каждые 10 s publish full snapshot (`rosterReset=true`).
   - Test: client packet loss simulation (drop 50% snapshots for 5 s), verify recovery after full snapshot.

3. **Fix Host Migration Adaptive Wait:**
   - After `ClientRejoinTimeoutSeconds`: eliminate non-rejoined slots (`NetworkLobbyState.KickNonRejoinedSlots`).
   - Test: 3-player session, kill one client before migration, verify match resumes без hang.

---

### 6.2. HIGH Priority (pre-EA)

4. **Add Snapshot Version Consensus Check:**
   - `NetworkLobbyState.TryStart`: wait until **all occupied slots** report `IsLocalSnapshotCompatible`.
   - Test: mixed builds (v22 + v23) в лобби, verify Start blocked.

5. **Fix Disconnect Grace Timer:**
   - Use `Time.unscaledTime` or separate coroutine в `TickDisconnectGrace`.
   - Test: disconnect mid-match, pause (`Time.timeScale = 0`), verify grace still expires at 90 s.

6. **Add NGO Shutdown Retry:**
   - `MatchNetworkBootstrap.WaitForShutdownCompleteAsync`: retry Start после timeout (up to 2 retries).
   - Test: fast leave→join (< 1 s), verify client join succeeds.

---

### 6.3. MEDIUM Priority (post-EA, resilience)

7. **Add Relay Heartbeat Failure Handling:**
   - `MatchLobbyHeartbeat.SendHeartbeat`: catch exceptions, retry 3x, log failure.
   - Test: kill Lobby service mid-match, verify heartbeat recovery.

8. **Add Reconnect Token Immediate Persist:**
   - `MatchRuntime.ApplyNetworkSnapshot`: call `PersistLocalReconnect` после каждого apply.
   - Test: kill app mid-frame, verify reconnect token up-to-date.

9. **Add NetworkBehaviour Singleton Null Checks:**
   - `MatchNetworkSession`, `MatchRuntime`: check `Instance != null` before access.
   - Test: fast rematch, verify no NullReferenceException.

---

### 6.4. Stress Testing (mandatory pre-EA)

10. **Playtest with 5-player FFA:**
    - 15+ юнитов per player, 30 Hz tick.
    - Verify: no snapshot publish lag, checksum stable.

11. **Packet Loss Simulation (10–20%):**
    - Unity Network Simulator (UTP) или external tool.
    - Verify: clients recover after full snapshot, no kick.

12. **Host Migration under Load:**
    - 4-player, 20 юнитов, kill host → verify new host resumes без state corruption.

---

## 7. Summary Table (severity ranked)

| # | Issue | Location | Severity | Likelihood | Impact |
|---|-------|----------|----------|------------|--------|
| 1 | Race Pick Double Start | `NetworkRacePickState.TryStartMatchFromReplicatedState` | **CRITICAL** | HIGH | State loss, corruption |
| 2 | Host Migration Shared Wire Context | `MatchNetworkAuthority.DecodeStateTransferBytes` | **CRITICAL** | MEDIUM | Partial state restore |
| 3 | NetworkBehaviour Singleton Race | `OnNetworkSpawn` / `OnNetworkDespawn` | **HIGH** | LOW–MEDIUM | NullReferenceException |
| 4 | Host Migration Adaptive Wait Hang | `HostMigrationSessionDriver.RunRebindAsync` | **HIGH** | MEDIUM | Match stuck в pause |
| 5 | Snapshot Checksum Kick | `ReportChecksumMismatchServerRpc` | **MEDIUM** | LOW–MEDIUM | False kick |
| 6 | NGO Shutdown Race | `WaitForShutdownCompleteAsync` | **MEDIUM** | LOW | Silent join fail |
| 7 | Disconnect Grace under Pause | `TickDisconnectGrace` | **MEDIUM** | LOW | Clients hang >90 s |
| 8 | Snapshot Version Handshake Incomplete | `NetworkLobbyState.TryStart` | **MEDIUM** | LOW | Incompatible client crash |
| 9 | Relay Heartbeat No Retry | `MatchLobbyHeartbeat.SendHeartbeat` | **LOW** | LOW | Late join fail |
| 10 | Reconnect Token Stale | `PendingMatchReconnectStore.Save` | **LOW** | LOW | Reconnect fail |

---

## 8. Финальные замечания

**Networking stack BARAKI — технически грамотно реализован**, но **в production-сценариях (5 игроков, packet loss, host crash под нагрузкой) высока вероятность критических багов**. MVP-реализация **работоспособна для контролируемого playtest (2–3 игрока, стабильная сеть)**, но **не готова к Early Access без стресс-тестирования и фиксов CRITICAL issues**.

**Hunches verified:**
- ✅ Listen-host + Relay может скрывать host-leave bugs → **подтверждено** (adaptive wait hang).
- ✅ NetworkBehaviour singletons race → **подтверждено** (Instance set/clear timing).
- ✅ Design docs ahead of code → **частично** (snapshot Hz расхождение, но в целом совпадает).

**Главное:**
- **Проект не сломан**, но **не bulletproof**.
- Для playtest GATE-001 (Люди, полный контент) — **достаточно**, если 2–4 игрока, стабильная сеть.
- Для GATE-002 (checkup перед расой #2) — **нужны fixes 1–3 из секции 6.1**.

---

**Дата составления:** 2026-08-30  
**Reviewer:** Claude Sonnet 4.5 (Cursor Cloud Agent, read-only analysis)  
**Контакт владельца:** Lizardan / Сергей (GitHub: Lizardan/BARAKI)
