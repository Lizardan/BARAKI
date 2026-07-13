---
doc_id: match_flow
version: 0.5
status: draft
depends_on: [vision, core_gameplay, map_topology]
provides: [phases, win_conditions, timings, player_slots, lobby]
---

# Match Flow

## Player slots

```entity
id: MATCH_FFA
players_min: 2
players_max: 8
mode: free_for_all
win_condition: last_standing    # последний игрок, не выбывший
elimination_rule: all_buildings_destroyed
time_cap: none
fill_mode: humans_only
bots_allowed: false
mvp: true
```

### Топология по N

| N | `topology_id` | Описание |
|---|---------------|----------|
| 2 | `TOPOLOGY_DUEL` | Дуэль, 3 параллельных коридора |
| 3–8 | `TOPOLOGY_RING` | Базы на N-угольнике, см. `Map Topology.md` |

Соседство и сплайны **генерируются** при старте — не хардкод таблицы на 4 игрока.

### Пример: N = 4 (slot 0..3 по часовой)

| Slot | Left opponent | Center target (march) | Right opponent |
|------|---------------|----------------------|----------------|
| 0 | 3 | **2** (напротив) | 1 |
| 1 | 0 | **3** | 2 |
| 2 | 1 | **0** | 3 |
| 3 | 2 | **1** | 0 |

Формула: `left = (i-1+N)%N`, `right = (i+1)%N`, `center_march = (i + N//2) % N`. В арене — бой со **всеми** врагами.

## Lobby flow (MVP)

```
Main Menu → Play → Create / Join
  → Create: Mode Select (N=2..8 tiles; MVP selectable: 2 and 4)
  → Join: room code (LocalDev) / Discord instanceId (Activity)
  → Lobby: N slot rows, Ready per player, Host Start when all occupied+ready
  → Load Game.unity → Race pick (each human) → match
```

> **Race pick** — на сцене `Game.unity` после Start из лобби (не в Lobby).
> **N immutable:** после Create число игроков не меняется.
> **Discord:** UI тот же; `IMatchSessionBackend` → matchmaker `ensure/join` по `instanceId` (dedicated + WSS). LocalDev — in-process registry + код комнаты.

```entity
id: LOBBY_RULES
min_players: 2
max_players: 8
player_count: fixed_at_create
mvp_selectable_modes: [2, 4]
empty_slots: closed
bot_fill: never
local_standins: LocalDev_only
disconnect_policy: eliminate_after_grace
disconnect_grace_seconds: 90
mvp: true
```

## Phases

```entity
id: PHASE_LOBBY
actions: [pick_race, ready_check]
mvp: true

id: PHASE_START
duration: 5s
actions: [generate_topology, reveal_bases, grant_starting_gold]
starting_gold: 500
mvp: true

id: PHASE_EARLY
time_range: 0–8 min
mvp: true

id: PHASE_MID
time_range: 8–18 min
mvp: true

id: PHASE_LATE
time_range: 18+ min
mvp: partial

id: PHASE_END
trigger: one_player_not_eliminated
mvp: true
```

## Win & elimination

```entity
id: WIN_LAST_STANDING
condition: count(players where not eliminated) == 1
time_cap: none
mvp: true

id: PLAYER_ELIMINATION
trigger: all_buildings_destroyed     # main + 3 barracks + 4 towers (ruins = destroyed)
buildings_required:
  - BUILDING_MAIN
  - BUILDING_BARRACKS_LEFT
  - BUILDING_BARRACKS_CENTER
  - BUILDING_BARRACKS_RIGHT
  - BUILDING_TOWER_NW
  - BUILDING_TOWER_NE
  - BUILDING_TOWER_SW
  - BUILDING_TOWER_SE
on_eliminate:
  spawn: off
  units: despawn
  state: eliminated_spectator
  center_retarget: next_alive_clockwise
exceptions:
  surrender: immediate
  disconnect_after_grace: immediate
mvp: true
```

> **Main alone ≠ elimination.** Пока жив хотя бы один barracks или tower — игрок **ещё в матче** (spawn/оборона продолжаются по правилам здания).

## Wave tick

**Per-barracks**, не глобально.  
- **Alive:** `SQUAD_BARRACKS_L{barracks_level}` + interval по level.  
- **Destroyed (ruins):** squad по **frozen** `barracks_level`, interval **всегда L1** (`BARRACKS_DESTROYED`).

Интервал: `Balance.md` (`FORMULA_BARRACKS_WAVE_INTERVAL`). Старт: у каждой казармы полный interval (различие только от level / race passive / ruins).

**Elimination** — только когда **все 8 зданий** игрока уничтожены (ruins). Тогда spawn **off**, юниты **despawn**, center **retarget** (см. `Map Topology.md`).

## Eliminated — spectator (E3)

Выбывший игрок **остаётся в матче** как наблюдатель до конца:

```entity
id: ELIMINATED_SPECTATOR
fog_of_war: disabled          # видит всю карту
camera: free_move             # свободное перемещение камеры
watch_targets: all_players    # можно смотреть за любым
player_control: none          # без управления юнитами/зданиями
exit_to_results: on_match_end # results screen только когда матч закончен
mvp: true
```

Живые игроки: **fog of war включён** (MVP baseline — стандартный RTS FoW, детали TBD в playtest).

## Surrender / disconnect

| Case | MVP | Early Access / Phase 2 |
|------|-----|------------------------|
| Surrender | Немедленная elimination | — |
| Disconnect | Матч **не на паузе**; grace **90s** → **elimination** | **Reconnect** в окне grace |
| Reconnect | **Не в MVP** | Восстановление слота, state sync |

```entity
id: DISCONNECT_POLICY_MVP
grace_seconds: 90
global_pause: false           # матч продолжается во время grace
reconnect: false
eliminate_on_expiry: true
status: locked
mvp: true

id: DISCONNECT_POLICY_EA
grace_seconds: 90
reconnect: true
eliminate_on_expiry: true
mvp: false
```

> MVP: заложить в netcode **session token + player slot id**, чтобы reconnect не требовал переписывания. Реализацию отложить.

## Рейтинг (не в MVP)

Только для **N=2** (дуэль) и **N=4** (FFA). Режимы 3, 5, 6, 7, 8 — **casual only**, без рейтинга.

```entity
id: QUEUE_RANKED_DUEL
player_count: 2
rated: true
mvp: false

id: QUEUE_RANKED_FFA4
player_count: 4
rated: true
mvp: false

id: QUEUE_CASUAL
player_count: 2..8
rated: false
mvp: true
```

### Рейтинговая модель (черновик, Phase 2)

- **Две независимые лadder:** `RATING_DUEL` и `RATING_FFA4`
- Матч засчитывается только при полном составе людей
- Алгоритм: ELO или Glicko-2 — TBD при реализации
- UI: выбор «Ranked Duel» / «Ranked 4-FFA» / «Casual» в меню

## Match state machine

```
Lobby → Countdown → GenerateArena → InProgress → Ended
```

## UI flow

1. Main Menu → **Play Online**
2. Lobby: **создать/войти** в лобби с фиксированным N, race pick, Ready
3. Match HUD
4. Results → rematch / lobby

## Locked decisions

| Решение | Значение |
|---------|----------|
| Pause при disconnect | **Нет** — матч продолжается во время grace; EA reconnect без global pause |
| Center при elimination | Retarget → **след. alive слот по CW** |
| Player count N | **Фиксировано при создании лобби**; смена N = **новое лобби** |
| Spectator (eliminated) | **Да** — FoW **off**, свободная камера, смотреть за всеми |
| Win condition | **Last standing** — без time cap |
| Elimination | **Все 8 зданий** (main + 3 barracks + **4 towers**); main alone **не** выбывание |
| Баланс по N | **Одинаковый** для N=2…8 (статы, economy, spawn); меняется только **карта** |
| Networking (MVP) | **Dedicated server** + WebGL; **FREE-2** infra |
| MMR decay / seasons | **Deferred Phase 2** — первый ranked season без decay; seasons TBD |
