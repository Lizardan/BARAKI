---
doc_id: vision
version: 0.5
status: locked
depends_on: []
provides: [pillars, audience, scope, non_goals]
---

# Vision

## Elevator pitch

**BARAKI** — мультиплеерная FFA-стратегия в духе Tug of War на **Windows**: экономика и оборона базы, армии сами идут в бой. **2–5** игроков. **Только люди**, без ботов. Запуск из главного меню (info hub): друзья, профиль, создание/вход в лобби.

## Design pillars

1. **Macro over micro** — нет приказов юнитам в бою.
2. **Three-lane politics** — flank = zero-sum с соседом; center = non-zero-sum (при N≥3).
3. **Scalable FFA** — 2 игрока = дуэль на 3 коридорах; 3–5 = кольцо с насыщенным центром.
4. **Humans only** — каждый слот = реальный игрок; честная политика FFA.
5. **Defense matters** — потеря barracks crippling, не elimination.
6. **Original identity** — свои расы, лор, визуал; уникальные бонусы и апгрейды per race.

## Целевая аудитория

- Игроки жанра Tug of War / Castle Fight
- RTS-игроки без любви к APM-микро
- Компания друзей: дуэли 1v1 и вечерний FFA на 2–5

## Scope

### In scope (MVP)

- **Windows Standalone** — основной клиент
- **2–5** игроков; хост = listen-server (Unity Lobby + Relay)
- Процедурная топология: `TOPOLOGY_DUEL` + `TOPOLOGY_RING`
- **1 раса (MVP):** Люди (+2 слота TBD); barracks **level 1–4**
- Core loop: spawn, combat, gold, upgrades, hero, towers
- Elimination: **все здания** базы уничтожены (не только main)
- Main Menu hub: профиль, друзья, инвайты, force update
- Host migration + reconnect

### Phase 2 — Early Access content

- **4+ уникальные расы** (старт: 2 → рост по контенту)
- Поддержка **N = 3, 5** в casual лобби
- **Рейтинг** только для **N=2** и **N=4**
- **Eliminated spectator** — FoW off, free camera
- Бонус после пика расы, титан, Divine Blessing — см. `Bonuses.md`, `TODO.md` Phase PRE-RACE2

### Phase 3+ (post-EA)

- Расширение пула рас beyond 4
- Реплей / **post-match** spectator для не участвовавших (optional)

### Out of scope (non-goals)

- Отдельный launcher exe
- **Боты / VI / skirmish против ИИ**
- Hotseat на одном ПК (optional очень поздно)
- Прямое управление юнитами
- Копирование чужого IP / ассетов
- Кампания

## Успех MVP

| Критерий | Метрика |
|----------|---------|
| Два Windows-клиента завершают дуэль через Lobby+Relay | 0 critical net bugs |
| 4 человека — ring topology корректна | LaneGraph tests N=4 |
| Core loop понятен без внешнего туториала | Playtest понимает 3 lane |
| Новая раса = data + art | Без правок combat core |

## Решения (зафиксировано)

- [x] Расы — **уникальные**, оригинальный контент
- [x] **Early Access: 4+ расы** (старт с 2)
- [x] **Только PvP**, ботов нет
- [x] **2–5 игроков** casual; рейтинг только **2 и 4**
- [x] **Reconnect / host migration** — реализованы
- [x] **Название: BARAKI**
- [x] **Disconnect grace: 90 сек** + reconnect
- [x] **Баланс рас:** симметричные базовые статы + асимметричные способности/герои/апгрейды
- [x] **Platform:** Windows + UGS Lobby/Relay

## Locked decisions

| Решение | Значение |
|---------|----------|
| Primary platform | **Windows x64 Standalone** |
| Net model | **Host-as-server** + Unity Lobby + Relay |
| Player count | **2–5** |
| Экономика рас | **Симметричная** (одинаковые bounty/costs); отличия через abilities и hero |
| Уникальность рас | **2+ / 1−** passives, **tower** upgrades, **magic** (main) → **уникальные заклинания магов** |
| Distribution | **GitHub Actions → GitHub Releases → in-game force update** |
