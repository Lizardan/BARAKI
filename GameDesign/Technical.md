---
doc_id: technical
version: 0.6
status: locked
depends_on: [vision, core_gameplay, match_flow, map_topology, platform]
provides: [unity_architecture, assemblies, scenes, networking, data_pipeline, agent_rules]
---

# Technical

## Stack

| Layer | Choice |
|-------|--------|
| Engine | Unity 6.5, URP 17.5 |
| **Ship client** | **Windows x64 Standalone** |
| Async | UniTask |
| UI | UI Toolkit |
| **Multiplayer** | **Netcode for GameObjects** + **host-as-server** |
| **Session / NAT** | Unity Lobby + Unity Relay (UGS) |
| **Social / profile** | UGS Friends + Cloud Save |
| **Distribution** | GitHub Actions → GitHub Releases → in-game force update |
| Input | Input System 1.19 |

> Platform spec: **`Platform.md`**

## Scenes

| Scene | Purpose |
|-------|---------|
| `Bootstrap.unity` | Persistent |
| `MainMenu.unity` | Info hub (profile, friends, create/join, updates) |
| `Lobby.unity` | Match lobby, ready, race pick |
| `Game.unity` | Gameplay (arena generated at runtime) |

## Runtime architecture

**Implemented (local MVP):** `MatchController`, `MatchArenaGenerator`, `LaneGraph`, `BarracksWaveScheduler`, `MatchCombatSystem`, `BuildingRegistry`, `EliminationService`, `MatchRuntime`.

**Online path:** `MatchNetworkAuthority`, snapshots, `NetworkLobbyState`, `IMatchSessionBackend` → Unity Lobby/Relay.

```
MatchController (server tick via MatchNetworkAuthority)
├── MatchArenaGenerator
├── LaneGraph
├── BarracksWaveScheduler
├── MatchCombatSystem
├── BuildingRegistry
├── EliminationService
└── (no BotOrchestrator)

MatchSessionService → UnityLobbyRelaySessionBackend | LocalDevSessionBackend
MatchNetworkBootstrap → StartAsHost / StartAsClient (+ Relay UTP)
```

### Assembly boundaries

| Assembly | Folders |
|----------|---------|
| `Game.Core` | Session, scene flow, update check rules |
| `Game.Gameplay` | `Match/`, `Lanes/`, `Combat/`, `Buildings/`, `Networking/` |
| `Game.UI` | Hub / Lobby / MatchHud |

## Networking

### Production (Windows)

Хост лобби = NGO **StartAsHost** (server + local client). Остальные = **StartAsClient**. Discovery через Unity Lobby; NAT через Unity Relay (UTP UDP).

```entity
id: NET_PRODUCTION
model: host_as_server
clients: Windows_Standalone
tick_rate: 30 Hz
package: Netcode_for_GameObjects
discovery: Unity_Lobby
nat: Unity_Relay
mvp: true
```

### Dev / local

```entity
id: NET_DEV_LOCAL
model: local_registry
endpoint: local://ROOM
use: Editor_offline_smoke
mvp: true
```

```entity
id: NET_TICK_RATE
value: 30
unit: Hz
interval_ms: 33
mvp: true
```

```entity
id: NET_UNIT_AUTHORITY
movement: server_sim
combat: server_sim
client: render_only
client_prediction: false
mvp: true
```

| Система | Authority |
|---------|-----------|
| Spawn / waves | Host (server) |
| Gold / purchases | Host |
| Building damage | Host |
| Tower target / hero summon | Client request → host validate |
| Unit positions | Host sim |

MVP netcode:
- Windows host-as-server + Lobby join code + Relay
- LocalDev offline path retained
- Reconnect / host migration: **post-MVP** (contracts in code + GDD)

## Distribution

See `Platform.md` § Distribution. Client reads latest GitHub Release `version.json`; outdated → block Play → download zip → `ApplyUpdate.bat`.

## MatchArenaGenerator

```csharp
void Generate(int playerCount, IReadOnlyList<PlayerSlot> slots)
{
    var topology = playerCount == 2 ? Topology.Duel : Topology.Ring;
}
```

## Data pipeline

1. GDD `GameDesign/`
2. SO `Assets/Game/ScriptableObjects/`
3. `GameIds.cs` — стабильные id

## Testing without bots

| Test | Как |
|------|-----|
| LaneGraph N=2,4,8 | Edit Mode |
| Session / lobby / update / migration rules | Edit Mode pure C# |
| Full match | 2 Windows builds / ParrelSync + Relay |

## Agent rules

1. Не реализовывать bot opponents.
2. Не возвращать Discord Activity / WebGL ship path.
3. `MatchArenaGenerator` + `LaneGraph` — единый путь для всех N.
4. Любой геймплейный тест multiplayer — минимум 2 human connections.

## Locked decisions

| Решение | Значение |
|---------|----------|
| Ship client | **Windows x64 Standalone** |
| Host-as-server | **Production path** |
| Discord / WebGL | **Non-goals** |
| Transport (online) | **UTP via Unity Relay** (UDP) |
| Netcode tick rate | **30 Hz** |
| Unit authority | **Server/host sim** — clients render only |
| Ranked backend | Cloud Save stubs → validate later |
