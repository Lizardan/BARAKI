# Tower upgrade tracks (PRE-007)

Расовые апгрейды башен: 9 треков × L1–L3, исследование в **живой** `BUILDING_TOWER`.
Связанные правила: `snapshot-wire.md` (v22), `human-unit-bonuses.md` (прецедент стака).

## Канон

- Источник истины: `GameDesign/Races.md` § Tower upgrades (`RACE_TOWER_UPGRADES`,
  `UPG_TOWER_TRACK_RULES`, список треков + таблица эффектов), `GameDesign/Upgrades.md`
  (`UPG_TOWER_RACE`). Экономика: **500/800/1200g**, **45/90/135s**.
- Эффекты — только на обычные юниты (не Hero1–3, не Titan, не DPS башен).
  **Исключение:** `UPG_TOWER_HUMAN_FLAMING_ARROWS` действует и на выстрелы живых
  башен владельца (решение пользователя 2026-08-26).
- Гейты: башня жива (`IsIntact` — общий гейт `MatchController.TryStartResearch`),
  последовательные уровни (projected = current + queued), кап 3.
- Очередь **1 на башню** (`MatchController.GetResearchQueueLimit`: tower = 1,
  остальные = `MatchResearchQueue.MaxQueueLength`); 4 башни = до 4 разных треков параллельно.
- Старые id `UPG_TOWER_HUMAN_STEEL_TEMPER … HOLD_THE_LINE/BALLISTA_OVERDRAW/ARCANE_RELAY`
  удалены из канона и из `GameIds` (не возвращать).

## 9 треков Людей (порядок = UI слоты 4–12)

| # | Id | Роли | Эффект |
|---|----|------|--------|
| 1 | `UPG_TOWER_HUMAN_FLAMING_ARROWS` | Ranged+Flying+башни | поджог on-hit 2/4/6 dmg/с · 2 с; огненный трейл снарядов |
| 2 | `_BULWARK` | Melee+Siege | +1/2/3 брони (спавн-статы); L3 блок −20% melee-урона получаемого |
| 3 | `_BLOODRAGE` | Melee+Flying | после убийства +15/25/40% AS на 3 с |
| 4 | `_BATTERING_RAMS` | Siege+Super | +25/50/75% урона по зданиям; L3 +1 радиус splash катапульты |
| 5 | `_ARCANE_FOCUS` | Caster | кулдауны умений ×0.88/×0.76/×0.64 |
| 6 | `_SKIRMISHERS` | Ranged+Caster | +0.5/1.0/1.5 дальность атаки |
| 7 | `_FORCED_MARCH` | Melee+Siege+Caster | +8/16/24% скорость движения |
| 8 | `_FIELD_MEDICS` | все юниты | +1/2/3 HP/с регенерации |
| 9 | `_LAST_STAND` | все юниты | при HP < 30% урон +20/30/40% |

## 9 треков Древних (Faceless) — FACELESS-017

Параллельная таблица `FacelessTowerTrackRules` (те же индексы 0..8, те же UI-слоты 4–12).

| # | Id | Роли | Эффект |
|---|----|------|--------|
| 1 | `UPG_TOWER_FACELESS_CHITINOUS_HIDE` | Melee+Super | +1/2/3 брони; L3 +15% макс. ХП |
| 2 | `UPG_TOWER_FACELESS_HOLLOW_BARBS` | Ranged+Flying | игнор +1/2/3 брони цели |
| 3 | `UPG_TOWER_FACELESS_VACUUM_COLLAPSE` | Melee+Flying | при смерти замедление 15/25/35% на 3 с в радиусе 3 |
| 4 | `UPG_TOWER_FACELESS_RITUAL_OF_THE_DEEP` | Caster | живой кастер в радиусе 8 снижает входящий урон на 6/10/15% |
| 5 | `UPG_TOWER_FACELESS_UNNERVING_AIM` | Ranged+Caster | −1/−2/−3 брони цели на 4 с |
| 6 | `UPG_TOWER_FACELESS_FRENZY_OF_THE_DEEP` | Melee+Siege | +10/15/20% скорости атаки |
| 7 | `UPG_TOWER_FACELESS_SPLASH_OF_THE_DEEP` | Caster+Super | splash 0.5/1.0/1.5 м, 25/35/50% урона |
| 8 | `UPG_TOWER_FACELESS_HOLLOW_BONES` | Siege+Flying | +8/16/24% скорости движения |
| 9 | `UPG_TOWER_FACELESS_VOID_HARDENING` | все юниты | −10/−15/−20% урона от атак башен/зданий |

## Карта кода

| Файл | Роль |
|------|------|
| `Gameplay/Match/TowerTrackRules.cs` | ids/порядок, роли, тюнинг эффектов (Human) |
| `Gameplay/Match/FacelessTowerTrackRules.cs` | ids/порядок, роли, тюнинг эффектов (Faceless) — FACELESS-017 |
| `MatchEconomyRules.cs` | `MaxTowerTrackLevel`, `TowerTrackCosts/DurationsSeconds`, `TryGetTowerTrackUpgrade` |
| `MatchPlayerState.cs` | `TowerTrackLevels[]`, `Get/SetTowerTrackLevels` (race-agnostic, 9 слотов) |
| `MatchController.cs` | tower-ветка `TryStartResearch` / `ApplyCompletedResearch`, `GetResearchQueueLimit`, apply снапшота |
| `MatchResearchQueue.cs` | перегрузки `HasSpace/TryEnqueue` с per-building лимитом |
| `Combat/TowerTrackUnitRules.cs` | спавн-статы Human (броня/дальность/скорость), фильтр Hero/Titan; хук в `UnitStatsResolver.Resolve` |
| `Combat/FacelessTowerTrackUnitRules.cs` | спавн-статы Faceless (броня/скорость атаки/движения, max HP L3) — FACELESS-017 |
| `Combat/MatchCombatSystem.cs` | burn/bloodrage/last stand/battering/arcane focus/medics (Human) + armor pen/slow/ritual/debuff/splash/void hardening (Faceless) |
| `Networking/MatchSnapshot.cs` | v22: 9 байт уровней в Players-секции |
| `UI/Runtime/Controllers/MatchInspectorController.cs` | слоты 4–12 (`PopulateTowerTrackCommands`), race-dispatch для Faceless |
| `UI/Runtime/MatchUpgradeLabelRules.cs` | лейблы кнопок/тултипов: race-aware `GetTowerTrackTitle/Effect` |

## Хостово-клиентский расклад

- Уровни треков едут в **Players-секции v22** (9 байт) → клиентские визуалы и UI
  читают их из локального `MatchPlayerState`. Активные исследования едут как раньше
  (Research-секция; новые id интернятся StringTable бесплатно).
- Burn/Bloodrage таймеры — host-only поля `MatchUnitState` (в wire не едут):
  HP падает через обычный damage-путь, клиенты видят результат по снапшотам.
- Огненный визуал: `MatchCombatPresenter.IsFlamingArrowsShot` по уровню трека +
  роли/башне-источнику; пул снарядов получил kind 3 (flaming bolt).

## Тесты

`TowerTrackRulesTests`, `TowerTrackResearchTests`, `TowerTrackCombatTests`,
`TowerTrackSnapshotTests`, `FacelessTowerTrackTests` (+ кейсы в `MatchUpgradeLabelRulesTests`).
После правок: `run_tests` EditMode green.

## Как добавить трек новой расе

Новая раса получает **собственную параллельную таблицу** правил рядом с `TowerTrackRules`
(прецедент: `FacelessTowerTrackRules` для FACELESS-017). Те же 9 индексов 0..8 → те же
UI-слоты 4–12; wire-контракт (`TowerTrackLevels[]` в Player-секции) не меняется.

Шаги:
1. Добавить id `UPG_TOWER_<RACE>_*` в `GameIds.Upgrades`.
2. Создать `<Race>TowerTrackRules.cs` (ids, роли, константы, `TryGetTrackIndex`).
3. Подключить `TryGetTrackIndex` в `TowerTrackRules.TryGetTrackIndex` (fallback).
4. Создать `<Race>TowerTrackUnitRules.cs` для спавн-статов и подключить в
   `TowerTrackUnitRules.Apply` (race-dispatch).
5. Добавить runtime-хуки в `MatchCombatSystem` (armor/slow/splash/...).
6. Обновить `MatchUpgradeLabelRules` (race-aware `GetTowerTrackTitle/Effect`).
7. Обновить `MatchInspectorController.PopulateTowerTrackCommand` (race-dispatch trackIds).
8. Добавить тесты (`<Race>TowerTrackTests.cs`).
9. Зафиксировать в `wiki/rules/tower-tracks.md` и `GameDesign/Races.md`.

### Трек не должен дублировать механику бонуса (правило 2026-09-08)

Треки башен и бонусы пика (12 слотов) — **разные системы**, они стакаются, но механика
эффекта не должна совпадать. Перед добавлением трека сверяться с каноном бонусов расы
(`wiki/rules/faceless-unit-bonuses.md` для Древних, `human-unit-bonuses.md` для Людей).

Запрещённые в треках механики Древних (заняты бонусами FACELESS-010): вампиризм,
дот on-hit, призыв мини-меле, урон/взрыв при смерти, on-kill бафы, уклонение и промах.

Свободные оси: броня, пробитие брони, дебаф брони, скорость атаки/движения, дальность,
сплеш, урон по зданиям, max HP, срез входящего урона (с явным указанием источника:
юниты/герои vs здания/башни), кулдауны, реген.

Источник урона для срезов указывать **явно** — иначе два трека молча дублируют друг друга
(прецедент: аура кастера срезала всё подряд и пересекалась с треком «−% от зданий»).
