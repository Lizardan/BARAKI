---
doc_id: map_topology
version: 0.4
status: draft
depends_on: [vision, core_gameplay]
provides: [arena_layout, lane_graph_rules, player_count_scaling, duel_mode]
---

# Map Topology

> Как карта и маршруты масштабируются от **дуэли (2)** до **FFA (8)** без отдельной карты на каждый размер.

## Принцип: кольцо + центр

**Lane budget:** у **каждого** игрока на карте — **только 3 исходящие дороги** (`LANE_LEFT`, `LANE_CENTER`, `LANE_RIGHT`). Сплайн стартует **от barracks**; башни и main **не** порождают lane.

Для **N ≥ 3** игроков базы стоят на **периметре** — вершины **правильного N-угольника** вдоль **края карты**. **Тыл** каждой базы (без barracks) смотрит **наружу, к краю**; **3 lane** и barracks — **внутрь**, к центру и соседям. У каждого игрока всегда **3 исходящих lane**:

| Lane | Противники | Gold model |
|------|------------|------------|
| `LANE_LEFT` | сосед против часовой (−1 mod N) | zero-sum |
| `LANE_RIGHT` | сосед по часовой (+1 mod N) | zero-sum |
| `LANE_CENTER` | **слот напротив** (+ merge в арене) | non-zero-sum (при N≥3) |

```entity
id: TOPOLOGY_RING
players_min: 3
players_max: 8
layout: regular_polygon
slot_assignment: clockwise_from_host
mvp: true
```

### Формулы соседства

```
left_opponent(i)  = (i - 1 + N) % N
right_opponent(i) = (i + 1) % N
center_primary_target(i) = (i + N // 2) % N   # слот «напротив» по кольцу
center_opponents(i) = { j | j != i }           # в арене — бой со всеми
```

### Схемы по N

**N = 3 (треугольник)**
```
        P1
       /  \
   cen /    \ cen
     /  hub  \
   P3 -------- P2
  flank      flank
```

**N = 4 (классика референса)**
```
      P2
       |
   P3--+--P1
       |
      P4
```

**N = 8 (октагон)**
```
   P8—P1—P2
  /         \
 P7   ARENA  P3
  \         /
   P6—P5—P4
```
Flank lane — по ребрам кольца. Center — от каждой базы к центральной арене.

---

## Режим дуэли (N = 2)

При двух игроках кольцо вырождается: оба соседа — один и тот же враг. Используем отдельную топологию:

```entity
id: TOPOLOGY_DUEL
players_min: 2
players_max: 2
layout: mirror_corridors
lanes_per_player: 3
mvp: true
```

**Три параллельных коридора** между двумя базами (слева / центр / справа с точки зрения каждого игрока):

```
    [P1]  ═══ L ═══  [P2]
          ═══ C ═══
          ═══ R ═══
```

| Lane | Поведение в дуэли |
|------|-------------------|
| L / C / R | Все ведут к единственному сопернику |
| Gold model | Все **zero-sum** (non-zero-sum нет при N=2) |
| Стратегия | Разная длина коридора, угол подхода к башням, уязвимость flank |

**Зачем 3 lane в дуэли:** привычка к flank/center решениям, разный риск (центр короче — быстрее давление, flank — обход башен).

Карта: две базы на **противоположных сторонах периметра**; **тыл** каждой — к **своему** краю карты; **перед** — друг к другу через арену. Коридоры не пересекаются с «чужими» базами.

---

## Процедурная раскладка (рекомендация для кода)

Одна сцена `Match.unity` + генерация слотов при старте лобби:

```yaml
MatchArenaGenerator:
  player_count: 2..8
  topology: DUEL if count==2 else RING
  radius: float              # расстояние баз от центра
  center_arena_radius: float # зона слияния center lanes
  base_prefab: per race skin
```

### Шаги генерации

1. `N =` число занятых слотов в лобби (2–8, без ботов).
2. Выбрать `TOPOLOGY_DUEL` или `TOPOLOGY_RING`.
3. Для каждого `i ∈ [0, N)`:
   - `angle = 2π * i / N` (дуэль: i=0 → 0°, i=1 → 180°).
   - Позиция базы: `(cos(angle), sin(angle)) * radius` — **на краю кольца / периметра**.
   - Поворот базы: local **+Y** → **центр карты** (0,0); local **−Y** → **край карты** (тыл без lane).
4. Построить `LaneGraph` (см. `BASE_LAYOUT` в `Buildings.md`):
   - **Flank:** сплайн **от** `BUILDING_BARRACKS_LEFT` / `BUILDING_BARRACKS_RIGHT` **внутрь** вдоль дуги между соседними базами.
   - **Center:** сплайн **от** `BUILDING_BARRACKS_CENTER` → **Central Arena** → подход к базе `center_primary_target(i)`. Все center-потоки сходятся в арене → **массовые стычки в центре** (задумка).
   - **Тыл базы** (−Y, к краю карты): **без** barracks и **без** lane spline.

### Center lane — march & merge

**Маршрут:** center barracks → **Central Arena** → база игрока **напротив** (`center_primary_target`).

```
P1_center ──┐
P2_center ──┼──► [ARENA] ──► базы «напротив» (P1→P3, P2→P4, …)
P3_center ──┘
         ▲
    все center-потоки пересекаются здесь → бой в центре
```

- **Цель марша** (куда идёт волна после арены): один слот — **напротив**.
- **Бой в арене:** юнит атакует **любого** вражеского юнита в `lane_id = CENTER` в зоне арены (FFA — союзников нет).
- При **N=2** (дуэль): «напротив» = единственный соперник; арена — узкий коридор.

### Center lane — выбыл игрок (E1)

Если `center_primary_target` **eliminated**, center-волны, шедшие к нему, **переназначают цель** на **следующий слот по часовой** от выбывшего, **пропуская eliminated** слоты:

```
next_center_target(e) = first_alive_slot((e + 1) % N, clockwise)
```

- Переназначение: **ongoing** юниты на center spline + **новые** волны с обновлённым target.
- Flank lanes (**L/R**) не меняются — только соседи ±1.
- Spawn выбывшего игрока **off**; юниты **на spline** продолжают марш по маршруту; остальные despawn (см. `Match Flow.md`).

5. Разместить декоративное кольцо / пропсы по N.

При большом N центр нагружен сильнее → см. `Balance.md`.

## LaneGraph (данные)

```yaml
LaneGraph:
  topology_id: TOPOLOGY_DUEL | TOPOLOGY_RING
  player_count: int
  slots:
    - slot_index: 0
      base_position: Vector3
      base_rotation: float
      neighbors:
        left: slot_index
        right: slot_index
      lanes:
        - id: LANE_LEFT
          opponent_slot: int
          spline: SplineAsset
        - id: LANE_CENTER
          primary_target_slot: int       # center_primary_target(i)
          opponent_slots: int[]          # все j != i (combat в арене)
          spline: SplineAsset
        - id: LANE_RIGHT
          opponent_slot: int
          spline: SplineAsset
```

Агент **не хардкодит** таблицу 4 игроков — только алгоритм + unit-тесты на N ∈ {2,3,4,6,8}.

---

## Лобби и слоты

```entity
id: MATCH_FFA
players_min: 2
players_max: 8
fill_mode: humans_only      # пустые слоты не заполняются ботами
player_count: fixed_at_create
start_condition: all_slots_filled_and_ready
min_players_to_start: 2
mvp: true
```

| Режим | Создание лобби |
|-------|----------------|
| Дуэль | Лобби с **N = 2** |
| Малый FFA | **N = 3–4** |
| Большой FFA | **N = 5–8** |

Старт матча только когда **все N слотов заняты людьми** и все Ready. **N не меняется** после создания — для другого размера нужно **новое лобби**.

---

## Масштабирование карты по N

| N | `radius` (отн.) | `center_arena_radius` | Примечание |
|---|-----------------|-------------------------|------------|
| 2 | 1.0 | 0.3 (узкий) | Коридоры длиннее по оси |
| 3 | 0.9 | 0.35 | |
| 4 | 1.0 | 0.4 | Референсный размер |
| 6 | 1.1 | 0.45 | |
| 8 | 1.2 | 0.5 | Шире арена, больше места в центре |

Точные метры — в greybox после первого playtest.

---

## Визуальные подсказки

- Цвет/иконка flank lane: цвет соседа (левая/правая база подсвечена на миникарте).
- Center: иконка «все враги» / мульти-портрет при N>3.

---

## N=4 greybox geometry (production spec)

MVP default **N=4** использует hand-tuned квадратный периметр (`N4RoadReferenceSpec`), не generic ring.

| Элемент | Размер | Код |
|---------|--------|-----|
| Дороги | Прямоугольники + fillet/corner arcs, **ширина 20** | `MatchArenaGreyboxBuilder.RoadWidth` |
| CenterArena | Платформа **50 × 50** | `N4RoadReferenceSpec.CenterArenaDiameter` |
| BaseArena | Платформа **40 × 30** (ширина × глубь) | `BaseArenaWidth`, `BaseArenaDepth` |

**Path-on-mesh:** waypoint-полилинии (`LanePath`, `N4RoadCenterlineBuilder`) совпадают с centerline дорог; юниты физически остаются на дороге (`RoadWidth/2`) или в center arena (`UnitLocomotionRules.ClampToWalkable`) — без отдельных march/combat drift.

**N≠4 (2,3,5..8):** procedural circular ring fallback — playable greybox, без parity с N=4. Generalization — отдельный reference spec per N (см. `Technical.md`).

---

## Locked decisions

| Решение | Значение |
|---------|----------|
| Host меняет N | **Нет** — N задаётся **при создании** лобби; другой N → **новое лобби** |
| Ranked pools | **Только N=2 и N=4** rated; N=8 и прочие — casual (см. `Match Flow.md`) |
| Spectator для eliminated | **Да** — FoW **off**, свободная камера, весь матч до results |
| Center march target | Слот **напротив**; merge и бой в **Central Arena** |
| Center при elimination | Target → **след. alive слот по CW** от выбывшего |
| Base на карте | **Периметр**; **тыл (−Y) → край карты**; **перед (+Y) → центр** |
| Lanes per player | **Ровно 3** (L / C / R); от barracks |
