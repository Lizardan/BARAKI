---
doc_id: todo
version: 0.3
status: locked
depends_on: [vision, technical, map_topology]
provides: [backlog, priorities, acceptance_criteria]
---

# TODO

> **Для агента:** PvP-only, без ботов. MVP netcode обязателен. **TDD:** тесты → код → `run_tests` → done только при green (см. `.cursor/rules/agent-guidelines.mdc` §5).

---

## Phase 0 — GDD

| ID | Task | Status | Acceptance |
|----|------|--------|------------|
| GDD-001 | Структура GameDesign | done | |
| GDD-002 | Топология 2–8 (`Map Topology.md`) | done | |
| GDD-003 | PvP-only, уникальные расы | done | Vision locked items |
| GDD-004 | 4 расы на Early Access | done | Races.md |
| GDD-005 | Lock open questions + unify ids | done | All docs `locked`; Races.md draft (lore TBD) |
| GDD-006 | 2 расы, barracks L1–4, squad table | done | Human + Bug; cumulative spawn |
| GDD-008 | Magic + stat + passive economy; spell numbers | done | Races/Upgrades/Economy/Balance |

---

## Phase 1 — MVP (Discord Activity, 2–8 online)

### Discord & infra (FREE-2)

| ID | Task | Status | Acceptance |
|----|------|--------|------------|
| MVP-D01 | Cloudflare Pages — WebGL + Activity shell | in progress | shell + deploy script; WebGL build copy required |
| MVP-D02 | Embedded App SDK — instanceId, participants | in progress | activity-shell boot.js + DiscordActivityBridge |
| MVP-D03 | Workers matchmaker — ensure/join by instanceId | in progress | infra/workers/matchmaker (register-tunnel) |
| MVP-D04 | **FREE-0:** PC headless + Cloudflare Tunnel | in progress | DedicatedServerEntry + FREE0.md runbook |
| MVP-D05 | Linux ARM64 server build + Docker image | pending | Same image runs local + Oracle |
| MVP-D06 | **FREE-1:** Oracle Always Free deploy | deferred | 24/7 without PC |

### Networking & lobby

| ID | Task | Status | Acceptance |
|----|------|--------|------------|
| MVP-N01 | Netcode NGO + WebSocket transport | in progress | Bootstrap + UseWebSockets; tunnel smoke TBD |
| MVP-N02 | `Lobby.unity` — slots, race pick, N=2 or 4 | in progress | NetworkLobbyState + LocalDev offline |
| MVP-N03 | Server-authoritative gold + spawn | in progress | Snapshot publish/apply ghosts |

### Map & lanes

| ID | Task | Status | Acceptance |
|----|------|--------|------------|
| MVP-001 | `MatchArenaGenerator` TOPOLOGY_DUEL | done | Tests N=2, 3 corridors |
| MVP-002 | `MatchArenaGenerator` TOPOLOGY_RING | done | Tests N=4,6,8 neighbors |
| MVP-003 | `LaneGraph` runtime + splines | done | LanePath + builder; 8 LaneGraphTests |
| MVP-004 | Greybox arena in `Game.unity` | done | MatchArenaGreybox N=4, lanes + bases |

### Data (2 races)

| ID | Task | Status | Acceptance |
|----|------|--------|------------|
| MVP-010 | `RACE_HUMAN` + `RACE_BUG` — 6 units + hero SO each | done | RaceCatalog + RaceContentBuilder |
| MVP-011 | Barracks level 1–4 + stat upgrades SO | done | SquadComposition + StatUpgradeTrack |
| MVP-012 | `GameIds.cs` | done | |

### Match runtime

| ID | Task | Status | Acceptance |
|----|------|--------|------------|
| MVP-020 | `MatchController` + phases | done | MatchController, MatchRuntime, MatchRules; tests |
| MVP-021 | `BarracksWaveScheduler` per-barracks + level 1–4 | done | BarracksWaveRules, scheduler, MatchController hook |
| MVP-022a | Kill bounty (unit kills) | done | MatchCombatSystem.GrantGold + tests |
| MVP-022b | Passive gold (main upgrade) | done | MatchEconomyRules + MatchController tick; tests |
| MVP-023 | Elimination + disconnect grace | in progress | EliminationService + last standing; disconnect pending |
| MVP-024 | Hero summon + tower targeting | pending | |

### UI

| ID | Task | Status | Acceptance |
|----|------|--------|------------|
| MVP-030 | Match HUD | in progress | фаза, время, gold, таймеры над казармами, results overlay |
| MVP-032 | Results screen | done | Results overlay persists on Phase.End |
| MVP-033 | Selection UI | done | minimap · portrait/stats/research · 3×4 command grid |

### QA

| ID | Task | Status | Acceptance |
|----|------|--------|------------|
| MVP-050 | LaneGraph tests N∈{2,4,8} | done | LaneGraphTests |
| MVP-051 | 2-human duel playtest | pending | Log |
| MVP-052 | 4-human FFA playtest | pending | Log |

### Removed (was bots)

| ID | Task | Status |
|----|------|--------|
| ~~MVP-030 BotBrain~~ | cancelled | humans only |

---

## Phase 2 — Early Access

| ID | Task | Status |
|----|------|--------|
| EA-001 | Расы #3+ (контент + SO) | deferred |
| EA-002 | Lobby N=3,5,6,7,8 casual | deferred |
| EA-003 | **Reconnect** | deferred |
| EA-004 | **Ranked Duel (N=2)** | deferred |
| EA-005 | **Ranked FFA4 (N=4)** | deferred |
| EA-006 | Race bonuses, special, ultimate | deferred |
| EA-007 | ~~Dedicated server~~ | cancelled | FREE-2 Oracle (MVP-D06) |

---

## Log

| Date | Note |
|------|------|
| 2026-07-05 | Initial GDD |
| 2026-07-05 | Название BARAKI; disconnect grace 90s locked |
| 2026-07-05 | GDD-005: locked decisions, UNIT_PROTO_* unified |
| 2026-07-05 | Per-barracks wave_interval; desync; tier accelerates spawn |
| 2026-07-05 | 2 расы (Люди/Жуки); barracks 4 levels; cumulative squad |
| 2026-07-05 | Barracks ruins: frozen squad level, L1 spawn speed, no upgrade |
| 2026-07-05 | MVP-003 LaneGraph + LanePath; 21 tests green |
| 2026-07-05 | Scene flow: Bootstrap→MainMenu→Lobby→Game; WC-style menu UI |
| 2026-07-05 | MVP-010/011 RaceCatalog, units, heroes, squads, stat tracks |
| 2026-07-07 | Race pick UI на Game: матч стартует после выбора расы |
| 2026-07-09 | BuildingRegistry, EliminationService, gold HUD, siege→buildings |
| 2026-07-13 | Match Entry Create/Join + ModeSelect; Lobby Ready; LocalDev session API; MatchSnapshot |
| 2026-07-13 | FREE-0 Phase B/A scaffolding: NGO WSS bootstrap, headless entry, Workers matchmaker, activity-shell, Cloudflare Pages deploy scripts |
| 2026-07-13 | CI: GitHub Actions WebGL→Pages + Workers; playtest-evening.ps1; infra/CI.md |

---

## User decisions

- [x] Уникальные расы, не WC3
- [x] Без ботов
- [x] 2–8 игроков (casual)
- [x] **Early Access: 4 расы**
- [x] **Reconnect** — после MVP (заложить в netcode дизайн)
- [x] **Рейтинг** — только N=2 и N=4, после MVP
- [x] **Название игры: BARAKI**
- [x] **Disconnect grace 90s** на MVP (без reconnect)
- [x] **2 стартовые расы:** Люди + Жуки
- [x] **Barracks level 1–4**, кумулятивный squad, per-lane upgrade
- [x] **Нет восстановления barracks** — ruins, авто-spawn frozen level + L1 timer
- [x] **Tower → ruins** (подножье); без функций
- [x] **MVP расы идентичны**; flying = ranged-only targets; super = siege ranged
- [x] **Infra FREE-2:** FREE-0 (PC+Tunnel) → FREE-1 (Oracle); **$0**
