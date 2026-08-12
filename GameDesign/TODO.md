---
doc_id: todo
version: 0.5
status: locked
depends_on: [vision, technical, map_topology, platform]
provides: [backlog, priorities, acceptance_criteria]
---

# TODO

> **Для агента:** PvP-only, без ботов. Windows + Lobby/Relay. **TDD:** тесты → код → `run_tests` → done только при green.

---

## Phase 0 — GDD

| ID | Task | Status | Acceptance |
|----|------|--------|------------|
| GDD-001 | Структура GameDesign | done | |
| GDD-002 | Топология 2–8 | done | |
| GDD-003 | PvP-only, уникальные расы | done | |
| GDD-010 | Пивот: Windows hub + Lobby/Relay + R2 | done | Platform.md; Vision/Technical/TODO updated |
| GDD-011 | Discord/WebGL/CF → removed | done | GitHub-only distribution + legal |

---

## Phase 1 — MVP online (Windows host-as-server)

| ID | Task | Status | Acceptance |
|----|------|--------|------------|
| MVP-N10 | UGS Auth + Lobby + Relay packages | done | multiplayer/auth/friends/cloudsave in manifest |
| MVP-N11 | `UnityLobbyRelaySessionBackend` + StartAsHost | done | relay-host/relay endpoints; AllocationUtils |
| MVP-N12 | Remove Discord/WebGL ship path from bootstrap/UI | done | LocalDev/NetDev/UGS; Discord bridge removed |
| MVP-N02 | Lobby slots / ready / start | done | NetworkLobbyState + mid-match reserve |
| MVP-N03 | Server-authoritative gold + spawn | done | Snapshots + command RPCs + HUD gold |

### Map / data / match (carry-over)

| ID | Task | Status |
|----|------|--------|
| MVP-001..004 | Arena / LaneGraph / greybox | done |
| MVP-010..012 | 2 races SO | done |
| MVP-020..022 | MatchController / waves / gold | done |
| MVP-023 | Elimination + disconnect grace | done |
| MVP-024 | Hero summon + tower targeting | done |
| MVP-030..033 | Match HUD / results / selection | done |

### Cancelled (Discord / FREE-*)

| ID | Task | Status |
|----|------|--------|
| ~~MVP-D01..D06~~ | Discord Pages / tunnel / Oracle | cancelled |
| ~~MVP-N01 WebSocket Discord~~ | WSS Discord path | cancelled |

---

## Phase 1b — Hero system (XP / abilities)

| ID | Task | Status | Acceptance |
|----|------|--------|------------|
| HERO-001 | TT_King = слот 1 (HERO_HUMAN_1) | done | GDD Heroes.md; UnitVisualCatalog hero prefab = Human_Hero (nested TT_King + TtUnitTeamColor + Human_Melee.controller) |
| HERO-002 | XP/Level: HeroLevelRules, слот-уровень, XP за килл героем и за здания | done | HeroLevelRulesTests, HeroMatchControllerTests |
| HERO-003 | Рост статов с уровнем + редеплой с сохранённым уровнем | done | ResolveHeroStats tests |
| HERO-004 | Снапшот v11: HeroLevel/HeroXp/HeroXpNext | done | snapshot tests |
| HERO-005 | 4 способности (1/4/7/10): авто-каст + VFX + аура + самобафф ульта | done | HeroAbilityRulesTests |
| HERO-006 | UI ContextStrip: уровень + XP-прогресс | done | hero strip shows TT_King, level, XP progress; formatting tests |

---

## Phase 2 — Hub + social

| ID | Task | Status | Acceptance |
|----|------|--------|------------|
| HUB-001 | Main Menu info hub layout | done | Hub panel in MainMenu.uxml |
| HUB-002 | Cloud Save profile (nick; rank/points stub) | done | PlayerProfileService |
| HUB-003 | UGS Friends + presence | done | FriendsHubService |
| HUB-004 | Invite friend → game lobby | done | Presence lobbyCode + join code |

---

## Phase 3 — CI + force update

| ID | Task | Status | Acceptance |
|----|------|--------|------------|
| DIST-001 | GHA tag `v*` → Windows zip → GitHub Release | done | deploy-windows.yml |
| DIST-002 | In-game version check + block Play | done | GameUpdateService + hub gate |
| DIST-003 | Download + ApplyUpdate.bat restart | done | Tooling/BuildSupport/ApplyUpdate.bat |

---

## Phase 4 — Resilience (post-MVP)

| ID | Task | Status | Acceptance |
|----|------|--------|------------|
| RES-001 | Mid-match host migration (pause → full state → resume) | done | Pause/elect/capture/rebinding phases; Relay rebind notify hooked |
| RES-002 | Player reconnect into active match | done | Grace reserve + session token ClaimReconnect |

---

## Phase EA — Early Access content

| ID | Task | Status |
|----|------|--------|
| EA-001 | Расы #3+ | deferred |
| EA-002 | Lobby N=3,5,6,7,8 casual | deferred |
| EA-004 | Ranked Duel (N=2) | deferred |
| EA-005 | Ranked FFA4 (N=4) | deferred |

---

## Log

| Date | Note |
|------|------|
| 2026-08-12 | Hero visual: Human_Hero.prefab (guid ac87b9a4) заменён с greybox-примитивов на nested TT_King + TtUnitTeamColor (4 текстуры команд) + Animator (Human_Melee.controller, ApplyRootMotion=off); тесты green (803 passed) |
| 2026-08-11 | Hero system (XP/levels): TT_King = слот 1; XP за убийства юнитов героем + убийства зданий твоими юнитами; уровень слота переживает смерть/redeploy; статы растут с уровнем; 4 способности (удар/хил/аура/ульта) на 1/4/7/10, самобафф ульта +50%/8с; снапшот v11 (HeroLevel/HeroXp/HeroXpNext) |
| 2026-08-11 | Caster spell VFX + Frost freeze: событие каста уходит в снапшот (**v9** `SpellCasts`, serial-dedup) → клиентский presenter; лейбл названия над кастером, зелёный/жёлтый «+» (Heal над HP-полоской, Resurrect на месте возрождения), синий AoE-круг Frost, подъём+fade (primitives + legacy TextMesh, URP Unlit); Frost замораживает жертв **1,5s** (full stun — `Frozen` behavior, не двигается/не атакует); EditMode green (772+ тестов) |
| 2026-08-11 | Caster mana economy: заклинания тратят ману (**50/75/150**), каст только при достаточной мане, реген **5/с** (пул 200, clamp); `Mana` сериализована в снапшот (v8) — клиентская полоска маны теперь корректна; GDD Balance/Economy синхронизированы |
| 2026-08-11 | Caster spells implemented: `UNIT_TYPE_CASTER` авто-кастует Heal/Frost/Resurrect (`UPG_MAIN_MAGIC` slot 1/2/3; приоритет Heal→Frost→Resurrect; per-caster CD; transient host корпусы ≤20s; событие `SpellCast` для VFX); каждый слот +3 dmg автоатаки (`MagicDamagePerLevel`); `UPG_CASTER_HEAL` убран из дизайна и кода; полный EditMode green (769 тестов) |
| 2026-08-02 | WC3 reference extract (Survival Chaos v1.58c): 811 units + 881 abilities + wave/economy JASS analysis; docs `WC3 Reference - Unit Stats.md`, `WC3 Reference - Wave Mechanics.md`; Balance.md сопоставление + кандидаты на правки (#1 быстрая первая волна, #3 bounty базовых) |
| 2026-07-20 | Playtest UX: barracks manual call (gold+charges), own timers only, building HP bars, idle hero park, defensive building auto-fire + RMB target, tooltips on top; race pick Humans-only (Bugs grey) |
| 2026-07-20 | Listen-host polish: fixed 30 Hz tick, client unit lerp, command ack/fail UI, host migration end-to-end + last-good snapshot, debug checksum; GDD locks listen-host+migration (no dedicated/lockstep) |
| 2026-07-20 | MVP online close-out: command RPCs + snapshot gold/HUD, disconnect grace, heroes/towers, results rematch/menu, host-migration/reconnect wiring |
| 2026-07-16 | Full GitHub migration: Releases + Pages; removed Cloudflare/Discord/WebGL infra and legacy code |

---

## User decisions

- [x] Windows Standalone, без Discord/WebGL
- [x] Один проект, Main Menu = hub (без отдельного лаунчера)
- [x] Host-as-server + Unity Lobby + Relay
- [x] Friends + Cloud Save (UGS)
- [x] GHA → GitHub Releases → force update
- [x] Full host migration + reconnect — фундамент listen-host (не optional)
- [x] Dedicated server — rejected
- [x] WC3 lockstep — rejected
- [x] Playtest race gate — только Люди (Жуки disabled)
- [x] Уникальные расы, без ботов, 2–8 игроков
