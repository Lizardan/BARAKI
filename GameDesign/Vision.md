---
doc_id: vision
version: 0.4
status: locked
depends_on: []
provides: [pillars, audience, scope, non_goals]
---

# Vision

## Elevator pitch

**BARAKI** — мультиплеерная FFA-стратегия на **Windows**: экономика и оборона базы, армии сами идут в бой. **2–8** игроков. **Только люди**, без ботов. Запуск из главного меню (info hub): друзья, профиль, создание/вход в лобби.

## Референс vs наш продукт

| | WC3 Survival Chaos | BARAKI |
|---|------------------|--------|
| Жанр | Tug of War FFA | То же |
| Платформа | WC3 custom map | **Windows** + Unity 6.5 Standalone |
| Игроки | 4 FFA | **2–8 FFA**, только люди |
| Расы | 22 (WC3 фэнтези) | **4 на Early Access**, уникальные |
| Боты | VI для одиночки | **Нет** — только PvP |
| Управление | База, герои, башни | То же |
| Длительность | 40–50 min | Ориентиры playtest по N (см. Balance); без time cap |

Референс по **механикам**, не по контенту: [sur5al](https://www.w3sur5al.com/Home/surchaos), [Probably Dance](https://probablydance.com/2020/03/28/a-new-strategy-genre-grows-up-survival-chaos-my-new-favorite-game/).

## Design pillars

1. **Macro over micro** — нет приказов юнитам в бою.
2. **Three-lane politics** — flank = zero-sum с соседом; center = non-zero-sum (при N≥3).
3. **Scalable FFA** — 2 игрока = дуэль на 3 коридорах; 8 = кольцо с насыщенным центром.
4. **Humans only** — каждый слот = реальный игрок; честная политика FFA.
5. **Defense matters** — потеря barracks crippling, не elimination.
6. **Original identity** — свои расы, лор, визуал; WC3 только как жанровый ориентир.

## Целевая аудитория

- Игроки жанра Tug of War / Castle Fight
- RTS-игроки без любви к APM-микро
- Компания друзей: дуэли 1v1 и вечерний FFA на 6–8

## Scope

### In scope (MVP)

- **Windows Standalone** — основной клиент
- **2–8** игроков; хост = listen-server (Unity Lobby + Relay)
- Процедурная топология: `TOPOLOGY_DUEL` + `TOPOLOGY_RING`
- **2 расы:** Люди + Жуки; barracks **level 1–4**
- Core loop: spawn, combat, gold, upgrades, hero, towers
- Elimination: **все здания** базы уничтожены (не только main)
- Join code create/join (Friends hub — следующим этапом)

### Phase 2 — Hub + Early Access features

- Main Menu info hub: профиль (Cloud Save), друзья (UGS Friends), инвайты
- Force update через GitHub Releases
- **4+ уникальные расы** (старт: 2 → рост по контенту)
- Поддержка **N = 3, 5, 6, 7, 8** в casual лобби
- **Reconnect** + **full host migration** (pause → transfer → resume)
- **Рейтинг** только для **N=2** и **N=4**
- **Eliminated spectator** — FoW off, free camera

### Phase 3+ (post-EA)

- Расширение пула рас beyond 4
- Реплей / **post-match** spectator для не участвовавших (optional)

### Out of scope (non-goals)

- **Discord Activity** / Embedded App SDK
- **Unity WebGL** ship client
- Dedicated server per match (production)
- Отдельный launcher
- **Боты / VI / skirmish против ИИ**
- Hotseat на одном ПК (optional очень поздно)
- Прямое управление юнитами
- Копирование рас/имён/ассетов WC3
- Кампания

## Успех MVP

| Критерий | Метрика |
|----------|---------|
| Два Windows-клиента завершают дуэль через Lobby+Relay | 0 critical net bugs |
| 4 человека — ring topology корректна | LaneGraph tests N=4 |
| Core loop без туториала WC3 | Playtest понимает 3 lane |
| Новая раса = data + art | Без правок combat core |

## Решения (зафиксировано)

- [x] Расы — **уникальные**, не WC3
- [x] **Early Access: 4+ расы** (старт с 2)
- [x] **Только PvP**, ботов нет
- [x] **2–8 игроков** casual; рейтинг только **2 и 4**
- [x] **Reconnect / host migration** — не в MVP, запланированы
- [x] **Название: BARAKI**
- [x] **Disconnect grace: 90 сек** на MVP (без reconnect)
- [x] **Баланс рас:** симметричные базовые статы + асимметричные способности/герои/апгрейды
- [x] **Platform:** Windows + UGS Lobby/Relay; Discord/WebGL abandoned

## Locked decisions

| Решение | Значение |
|---------|----------|
| Primary platform | **Windows x64 Standalone** |
| Net model | **Host-as-server** + Unity Lobby + Relay |
| Discord / WebGL | **Out of scope** |
| Player count | **2–8** (как в core GDD) |
| Экономика рас | **Симметричная** (одинаковые bounty/costs); отличия через abilities и hero |
| Уникальность рас | **2+ / 1−** passives, **tower** upgrades, **magic** (main) → **уникальные заклинания магов** |
| Distribution | **GitHub Actions → GitHub Releases → in-game force update** |
