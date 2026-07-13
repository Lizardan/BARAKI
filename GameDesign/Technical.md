---
doc_id: technical
version: 0.5
status: draft
depends_on: [vision, core_gameplay, match_flow, map_topology, discord_platform]
provides: [unity_architecture, assemblies, scenes, networking, data_pipeline, agent_rules]
---

# Technical

## Stack

| Layer | Choice |
|-------|--------|
| Engine | Unity 6.5, URP 17.5 |
| **Ship client** | **Unity WebGL** (Discord Activity iframe) |
| Async | UniTask |
| UI | UI Toolkit |
| **Multiplayer** | **Netcode for GameObjects** + **dedicated server** (production) |
| **Discord** | Embedded App SDK (JS shell → Unity via `.jslib`) |
| Input | Input System 1.19 |

> Platform spec: **`Discord Platform.md`**

## Scenes

| Scene | Purpose |
|-------|---------|
| `Bootstrap.unity` | Persistent |
| `MainMenu.unity` | Menu |
| `Lobby.unity` | Matchmaking, player count |
| `Game.unity` | Gameplay (arena generated at runtime; файл сцены — `Assets/Game/Scenes/Game.unity`) |

## Runtime architecture

**Implemented (local MVP):** `MatchController`, `MatchArenaGenerator`, `LaneGraph`, `BarracksWaveScheduler`, `MatchCombatSystem`, `BuildingRegistry`, `EliminationService`, `MatchRuntime` (scene bridge).

**Planned (netcode MVP-N01):** `MatchEconomyService` (passive gold sync), `MatchNetworkAuthority`, client render sync.

```
MatchController (pure C# today → NetworkBehaviour server tick planned)
├── MatchArenaGenerator     — TOPOLOGY_DUEL | TOPOLOGY_RING from player_count
├── LaneGraph               — built at runtime, replicated as config
├── BarracksWaveScheduler   — server only; per-barracks timers
├── MatchCombatSystem       — unit sim + siege → BuildingRegistry
├── BuildingRegistry        — 8 buildings/player, HP/ruins (implemented)
├── EliminationService      — last standing win (implemented)
└── (no BotOrchestrator)

MatchEconomyService         — planned: passive gold, purchases (server only)

LobbyController
├── SlotManager             — humans only, 2..8
└── RaceSelectionSync       — planned online; MVP interim: race pick on Game.unity
```

### Assembly boundaries

| Assembly | Folders |
|----------|---------|
| `Game.Core` | Session, scene flow |
| `Game.Gameplay` | `Match/`, `Lanes/`, `Combat/`, `Buildings/`, `Networking/` |
| `Game.UI` | `Lobby/`, `MatchHud/` |

## Networking

### Production (Discord Activity)

**Dedicated server** на матч; все клиенты — **WebGL** в Discord. См. `Discord Platform.md`.

```entity
id: NET_PRODUCTION
model: dedicated_server_per_match
clients: WebGL
server: headless_authoritative
tick_rate: 30 Hz
package: Netcode_for_GameObjects
mvp: true
```

### Dev / local (не shipping)

**Host-client** для быстрых тестов без backend (ParrelSync, Editor).

```entity
id: NET_DEV_HOST
model: host_client
use: local_development_only
dedicated_server: false
mvp: false
note: Не использовать как production path для Discord
```

```entity
id: NET_TICK_RATE
value: 30
unit: Hz
interval_ms: 33
note: Server sim + sync; spawn/wave timers — отдельные per-barracks, не на tick
mvp: true
```

```entity
id: NET_UNIT_AUTHORITY
movement: server_sim
combat: server_sim
client: render_only              # интерполяция позиций для плавности — optional MVP
client_prediction: false
note: Spline march + spread считаются на server; клиент не двигает юнитов локально
mvp: true
```

**Обязательно с первого играбельного билда** — ботов нет.

| Система | Authority |
|---------|-----------|
| Spawn / waves | Server |
| Gold / purchases | Server |
| Building damage | Server |
| Tower target / hero summon | Client request → server validate |
| Unit positions | Server sim (authoritative transform sync) |

MVP scope netcode:
- **Production:** WebGL client + dedicated server; 2–8 players
- **Dev:** ParrelSync / local host-client
- **Reconnect-ready:** persist `player_slot_id` + `session_token` (EA)

## MatchArenaGenerator

```csharp
void Generate(int playerCount, IReadOnlyList<PlayerSlot> slots)
{
    var topology = playerCount == 2 ? Topology.Duel : Topology.Ring;
    // place bases, build LaneGraph splines, spawn base prefabs per race
}
```

Unit tests без сети: вызов генератора с N=2,4,8 → assert neighbor indices.

## Data pipeline

1. GDD `GameDesign/`
2. SO `Assets/Game/ScriptableObjects/` (runtime data; не `Assets/Game/Data/`)
3. `GameIds.cs` — стабильные id

### ScriptableObjects

`RaceDefinition`, `UnitDefinition`, `BuildingDefinition`, `UpgradeDefinition`, `SquadComposition`, `HeroDefinition`, `MatchArenaSettings` (radius, arena size per N).

## Testing without bots

| Test | Как |
|------|-----|
| LaneGraph N=2,4,8 | Edit Mode unit tests |
| Economy | Unit tests |
| Full match | 2+ Netcode test clients / ParrelSync |
| Editor sandbox | `MatchSandbox` spawns waves без lobby (dev only) |

Удалить из плана: `BotBrain_*` tests.

## Performance (8 players peak)

- 8 × 3 lanes × up to **14** units (barracks L4) — async spawn размазывает пик
- Pooling обязателен
- Center arena: spatial partition для targeting

## Agent rules

1. Не реализовывать bot opponents.
2. `MatchArenaGenerator` + `LaneGraph` — единый путь для всех N.
3. Любой геймплейный тест multiplayer — минимум 2 human connections.

## Locked decisions

| Решение | Значение |
|---------|----------|
| Dedicated server (production) | **Да** — headless server per match; WebGL clients in Discord |
| Host-client | **Dev/local only** — не shipping path |
| Discord Activity | **Primary** — desktop; WebGL + Embedded App SDK |
| Infra | **FREE-2** — PC+Tunnel → Oracle Always Free; **$0** |
| Transport (production) | **WebSocket / WSS** on client **and** server |
| Netcode tick rate | **30 Hz**; спавн — per-barracks server timers, не привязаны к tick |
| Unit authority | **Server sim** — движение/бой на server; клиенты **render only** |
| Ranked backend (Phase 2) | **TBD** при реализации (Steam / PlayFab / custom); не блокирует MVP |
