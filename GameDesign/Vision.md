---
doc_id: vision
version: 0.3
status: locked
depends_on: []
provides: [pillars, audience, scope, non_goals]
---

# Vision

## Elevator pitch

**BARAKI** — мультиплеерная FFA-стратегия в **Discord Activity** (desktop): экономика и оборона базы, армии сами идут в бой. **2–8** игроков в голосовом канале. **Только люди**, без ботов.

## Референс vs наш продукт

| | WC3 Survival Chaos | BARAKI |
|---|------------------|--------|
| Жанр | Tug of War FFA | То же |
| Платформа | WC3 custom map | **Discord Activity** (desktop) + Unity 6.5 WebGL |
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

- **Discord Activity** (desktop Discord) — основной способ запуска
- **2–8** игроков в одной activity instance (голосовой канал)
- Процедурная топология: `TOPOLOGY_DUEL` + `TOPOLOGY_RING`
- **2 расы:** Люди + Жуки; barracks **level 1–4**
- Core loop: spawn, combat, gold, upgrades, hero, towers
- Elimination: **все здания** базы уничтожены (не только main)

### Phase 2 — Early Access

- **4+ уникальные расы** (старт: 2 → рост по контенту)
- Поддержка **N = 3, 5, 6, 7, 8** в casual лобби
- Race bonuses, special units, ultimate
- **Reconnect** после disconnect (см. `Match Flow.md`)
- **Рейтинг** только для **N=2** и **N=4** (отдельные очереди)
- **Eliminated spectator** — FoW off, free camera (см. `Match Flow.md`)

### Phase 3+ (post-EA)

- Расширение пула рас beyond 4
- Реплей / **post-match** spectator для не участвовавших (optional)

### Out of scope (non-goals)

- **Discord mobile** Activity
- **Боты / VI / skirmish против ИИ**
- Hotseat на одном ПК (optional очень поздно)
- Прямое управление юнитами
- Копирование рас/имён/ассетов WC3
- Кампания

## Успех MVP

| Критерий | Метрика |
|----------|---------|
| Два клиента завершают дуэль без десинка | 0 critical net bugs |
| 4 человека — ring topology корректна | LaneGraph tests N=4 |
| Core loop без туториала WC3 | Playtest понимает 3 lane |
| Новая раса = data + art | Без правок combat core |

## Решения (зафиксировано)

- [x] Расы — **уникальные**, не WC3
- [x] **Early Access: 4+ расы** (старт с 2)
- [x] **Только PvP**, ботов нет
- [x] **2–8 игроков** casual; рейтинг только **2 и 4**
- [x] **Reconnect** — не в MVP, запланирован на EA/Phase 2
- [x] **Название: BARAKI**
- [x] **Disconnect grace: 90 сек** на MVP (без reconnect)
- [x] **Баланс рас:** симметричные базовые статы + асимметричные способности/герои/апгрейды

## Locked decisions

| Решение | Значение |
|---------|----------|
| Primary platform | **Discord Activity**, **desktop only** |
| Infra | **FREE-2** — PC+Tunnel → Oracle Always Free (**$0**) |
| Discord mobile | **Out of scope** |
| Player count | **2–8** (как в core GDD) |
| Экономика рас | **Симметричная** (одинаковые bounty/costs); отличия через abilities и hero |
| Уникальность рас | **2+ / 1−** passives, **tower** upgrades, **magic** (main) → **уникальные заклинания магов** |
