---
doc_id: arena
version: 0.1
status: draft
depends_on: [match_flow, heroes, economy, core_gameplay]
provides: [arena_timer, arena_pick, arena_duels, arena_reward]
---

# Arena

## Design role

Раз в **15 игровых минут** матч встаёт на паузу, камера всех игроков уходит на
отдельную **арену-круг** далеко от карты, игроки выставляют по одному бойцу и
сражаются **1v1**. Победитель каждой дуэли получает золото. После последней дуэли
камера возвращается, матч продолжается. Арена — способ конвертировать накопленное
золото в преимущество и дать всем зрителям общее событие.

## Timer

```entity
id: ARENA_TIMER
start: after_match_start
interval_sec: 900          # 15 минут игрового времени
arenas_per_match: 4
last_arena: 4              # после неё отсчётов больше нет
mvp: true
```

- Отсчёт идёт по **игровому** времени матча; пока идёт арена, время матча стоит.
- Четвёртая арена — последняя. После неё таймера нет.

## Participation

```entity
id: ARENA_PARTICIPATION
requires: hero_hired_in_main     # или титан, открытый исследованием
state_independent: true          # alive / deployed / dead / cooldown — не важно
excludes: eliminated_players
min_participants: 2
hero_lock: used_on_previous_arena  # герой, выступивший на арене, закрыт навсегда
mvp: true
```

- Участвует тот, у кого боец **куплен/активирован в главном здании**.
- Герой, уже выходивший на арену, в следующих аренах **заблокирован** (3 героя = 3
  геройские арены).
- Выбывшие из матча игроки не участвуют.
- Если участников меньше двух — арена сгорает, отсчёт идёт к следующей.

## Pick

```entity
id: ARENA_PICK
duration_sec: 30
choice: one_fighter_per_player
titan_arena: 4               # на арене 4 выбора нет — только титан
auto_pick_on_timeout: first_available
odd_player: also_chooses_opponent
mvp: true
```

Нечётный игрок (последний в рейтинге) неполной пары дополнительно выбирает,
**с кем** он будет драться — из остальных участников.

## Duels

```entity
id: ARENA_DUEL
pairing: sort_by_gold_desc        # топ1 vs топ2, топ3 vs топ4
tie_break: lower_slot
presentation_sec: 5               # «кто против кого» по центру
order: sequential
odd_player_duel: last
loser_penalty: none               # герой не умирает и не уходит в кулдаун
mvp: true

id: ARENA_REWARD
arena_1: 1000
arena_2: 2000
arena_3: 3000
arena_4: 4000
awarded_to: duel_winner
mvp: true
```

Бой полностью автономный (игрок не микроит): боец использует свой обычный кит
способностей. Проигравший теряет только возможность получить награду.

## Camera

```entity
id: ARENA_CAMERA
move_on: arena_start
focus: arena_center
input_locked: true
restore_on: arena_end          # позиция и ориентация возвращаются как были
mvp: true
```

## After arena

```entity
id: ARENA_RESUME
match_resume: true
next_countdown: 900
mvp: true
```

## Locked decisions (confirmed)

| Решение | Значение |
|---------|----------|
| Интервал | 15 игровых минут; 4 арены за матч |
| Награда | 1000 / 2000 / 3000 / 4000 победителю дуэли |
| Участие | Боец нанят в главном здании; состояние не важно; выбывшие — нет |
| Блокировка героев | Выступавший герой закрыт на следующих аренах |
| Пары | Сортировка по золоту; топ1 vs топ2, топ3 vs топ4 |
| Нечётный игрок | Выбирает соперника из остальных участников, его дуэль последняя |
| Проигравший | Без штрафа — герой не умирает, кулдауна нет |
| Выбор | 30 с, автовыбор первого доступного по таймауту |
| Арена 4 | Только титаны, выбора нет |
| Презентация | 5 с между дуэлями, камера заблокирована |
| Пустая арена | Сгорает, отсчёт идёт к следующей (награда растёт по номеру) |
