# Добавление tower tracks новой расе (параллельная таблица)

Паттерн: новая раса получает собственную параллельную таблицу треков рядом с `TowerTrackRules` (Human), используя те же индексы 0..8 → те же UI-слоты 4–12. Wire-контракт (`TowerTrackLevels[]` в Player-секции snapshot v22) не меняется.

## Шаги

### 1. Id константы
Добавить в `GameIds.Upgrades` (в `GameIds.cs`):
```csharp
public const string Tower<Race><TrackName> = "UPG_TOWER_<RACE>_<TRACK_NAME>";
```

### 2. Параллельная таблица правил
Создать `Gameplay/Match/<Race>TowerTrackRules.cs`:
- `TrackCount = 9`
- `TrackIds[]` — 9 id в каноническом порядке
- Константы эффектов (armor, pen, slow, reduction, debuff, AS, splash, MS)
- `RoleMatches(int, UnitRole)` — ролевые гейты
- `TryGetTrackIndex(string, out int)` — резолюция id в индекс

### 3. Спавн-статы
Создать `Gameplay/Combat/<Race>TowerTrackUnitRules.cs`:
- `Apply(UnitCombatStats, MatchPlayerState)` — броня, maxHP, скорость атаки/движения
- Фильтр: не применять к Hero/Titan

### 4. Подключение fallback
В `TowerTrackRules.TryGetTrackIndex` добавить fallback:
```csharp
return <Race>TowerTrackRules.TryGetTrackIndex(upgradeId, out index);
```

### 5. Race-dispatch спавн-статов
В `TowerTrackUnitRules.Apply` добавить диспатч по `player.RaceId`:
```csharp
if (player.RaceId == GameIds.Races.<Race>)
    return <Race>TowerTrackUnitRules.Apply(stats, player);
```

### 6. Runtime-хуки в MatchCombatSystem
Добавить в `MatchCombatSystem`:
- Хелперы `Get<Race>...` (armor pen, reduction, splash, и т.д.)
- Хуки в `ApplyDamage`, `ResolveProjectileImpact`, `ResolveMeleeImpact`
- Decay-таймеры в `TickTowerTrackStatus` (если нужны slow/debuff)
- `GetEffectiveMarchSpeed` для slow-эффектов
- Новые transient-поля в `MatchUnitState` (если нужны)

### 7. UI race-aware
Обновить `MatchUpgradeLabelRules`:
- `GetTowerTrackTitle(int, string raceId)` — dispatch по расе
- `GetTowerTrackEffect(int, string raceId)` — dispatch по расе
- `FormatTowerTrackButton/Tooltip` — пробросить raceId

Обновить `MatchInspectorController.PopulateTowerTrackCommand`:
- Выбрать правильный `TrackIds[]` по `player.RaceId`
- Передать raceId в `FormatTowerTrackButton/Tooltip`

### 8. Тесты
Создать `Tests/<Race>TowerTrackTests.cs`:
- Спавн-статы (броня, AS, MS)
- Armor pen / reduction / debuff
- On-death эффекты
- Splash / void hardening
- Snapshot round-trip (если менялся wire)

### 9. Документация
- `wiki/rules/tower-tracks.md` — добавить таблицу треков новой расы
- `GameDesign/Races.md` — обновить канон

## Правила
- Не расширять `TowerTrackRules.TrackIds` — только параллельная таблица
- Wire-контракт 9 слотов не меняется
- Треки не должны дублировать механики бонусов (`faceless-unit-bonuses.md` / `human-unit-bonuses.md`)
- Срезы входящего урона указывать явно (источник: юниты/герои vs здания/башни)
