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
- Снапшот публикуется сразу в `BeginMatchOnServer` (`PublishSnapshotNow`), не только на 15 Hz.
- `MatchSnapshotCodec.Deserialize` принимает любую версию `1..CurrentVersion`. Хардкод allow-list (`1 or 2 or … or 17`) при бампе версии роняет **каждый** клиентский RPC (`Unsupported snapshot version`) — оверлей бонуса пустой, часы `00:00`, после выхода хоста клиенты уходят в Offline split-brain.

## Start → Early

Камера летит к базе (`IsFocusInProgress`). Early **не** ждёт race-pick pan lock. Таймаут `MatchRules.StartPhaseMaxWaitSeconds` (5 с, GDD `PHASE_START`), чтобы застрявшая камера не держала часы на нуле.

## Host drop

`MatchNetworkAuthority.OnNetworkDespawn` при живом session handle оставляет `TickMode.Client` — **не** `Offline`. Иначе каждый клиент начинает свою симуляцию (split-brain), бонус-оверлей «внезапно появляется», и они играют не друг с другом.

Capture для миграции: last-good bytes, иначе **локальный** `MatchController` любого пира (не только слот бывшего хоста). Пустой last-good на клиентах — норма, если снапшот не дошёл.

## Прочее

- `MatchLobbyHeartbeat.Ensure()` вне Play Mode возвращает `null` (нельзя `DontDestroyOnLoad` в EditMode). Вызовы `MatchNetworkSession` (`ApplyHandle`/`Shutdown`) используют `?.`.
