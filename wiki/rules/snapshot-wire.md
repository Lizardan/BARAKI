# Snapshot wire v22 (секционный формат) и контракт презентации

Связанные правила: `match-network.md` (старт матча, интерполяция, миграция),
`abilities.md` (SpellCasts), `human-unit-bonuses.md` (BonusSlot/ауры).

## Зачем существует эта глава

До v21 снапшот был плоским потоком полей: строки на каждый юнит каждый паблик,
полное состояние 30 раз/сек, а новый визуальный эффект часто оказывался «виден
только на хосте» — презентер читал host-only поля сима. Формат v21 решает три
задачи: **трафик** (static/dynamic split), **расширяемость** (EventStream,
skip-by-length) и **устойчивость к рассинхрону** (handshake версии в лобби +
толерантный декодер).

## Wire-формат v22

Кодек: `Assets/Game/Scripts/Runtime/Gameplay/Networking/MatchSnapshot.cs`
(`MatchSnapshotCodec`, `MatchSnapshotWireContext`), `CurrentVersion = 22`
(v22: уровни tower-треков в Players-секции — см. `tower-tracks.md`).
Легаси-читатели ≤v20 **удалены** — смешанные билды закрываются handshake'ом (ниже).

```
[int32 version = 22]
Header: [int playerCount][int phase][float matchTime][int winnerSlot]
        [float bonusPickDeadline][bool rosterReset]
Секции (каждая): [uint16 sectionId][int32 payloadLength][payload...]
Финал: секция Checksum ([uint32])
```

| sectionId | Секция | Когда пишется |
|-----------|--------|---------------|
| 1 | StringTable | если есть строки |
| 2 | Players | есть игроки |
| 3 | Buildings | есть здания |
| 4 | UnitsStatic | юниты, чья статика изменилась с прошлого паблика |
| 5 | UnitsDynamic | всегда (живые юниты) |
| 6 | RemovedUnits | есть удаления с прошлого паблика |
| 7 | Research | есть очереди |
| 8 | Barracks | есть казармы |
| 9 | CenterLanes | есть центральные линии |
| 10 | Heroes | есть ростер героев |
| 11 | Events | есть события |
| 12 | Checksum | всегда |

### Ключевые свойства формата

- **Незнакомая секция / тип события пропускается по длине** — добавление данных
  не ломает пиров, которые ещё не знают новую секцию (forward+backward compat).
- **StringTable инлайном** в каждый снапшот (~0.5 КБ): все def/lane/building id —
  `ushort` индексы. Персистентность не нужна → host migration safe.
- **UnitsStatic vs UnitsDynamic**: статика (def, lane, owner, heroSlot, bonusSlot,
  level/xp, aura radius/color/**abilityId**) едет только при изменении сигнатуры;
  динамика (позиция short-см, facing byte-угол, health, mana, behaviorState,
  swingSerial ushort, targetUnitId, targetBuildingInstanceId, флаги
  attackCommitted/parkedAtBase) — каждый паблик (~29 Б/юнит вместо ~74).
- **Мёртвые юниты не сериализуются**: смерть = запись в RemovedUnits. На клиенте
  мёртвый юнит исчезает из merged `snapshot.Units`.
- **Чексумма** считается по полям, которые проходят wire без искажений
  (int/bool/float-exact), поэтому квантование позиции/facing ей не мешает.

### MatchSnapshotWireContext

- Хост держит один инстанс (`MatchNetworkAuthority._wire`): `Encode` диффит
  статику между паблишами. `ResetEncode()` — при спавне и реванше, а также **каждые ~10 s** для periodic full resync.
- **Periodic full snapshot** (каждые 10 s): `PublishSnapshotNow` проверяет `Time.realtimeSinceStartup - _lastFullSnapshotRealtime >= 10f`, вызывает `_wire.ResetEncode()`. Это обеспечивает:
  1. Resync всех клиентов после packet loss / lag spike (self-contained snapshot).
  2. Host migration safety — `LastNetworkSnapshotBytes` всегда содержит актуальный полный snapshot для capture.
  3. Защита от накопления desyncs в incremental encoding через shared context.
- Клиент декодирует тем же контекстом: `Decode` накапливает static-кэш.
- **Свежий контекст = self-contained payload** (вся статика + rosterReset=true):
  так работают статические `Serialize`/`Deserialize` (тесты) и live-capture миграции.
- Инкрементальные байты миграции декодируются **тёплым контекстом получателя**
  (`MatchNetworkAuthority.DecodeStateTransferBytes`) — все пиры видели тот же стрим.

### Анти-рассинхрон стек

1. **Handshake в лобби**: хост пишет `MatchSnapshotCodec.CurrentVersion` в
   `NetworkLobbyState._snapshotCodecVersion`. Mismatch → подзаголовок лобби
   «Версия игры не совпадает…», старт заблокирован (`TryStart` гейтится
   `SnapshotVersionGate.CanStartMatch`). Правила: `SnapshotSyncRules.cs`.
2. **Graceful decode failure**: `ApplySnapshotClientRpc` ловит исключение декода →
   warn + событие `MatchNetworkAuthority.SnapshotDecodeFailed` (не чаще раза в 10 c)
   → клиент остаётся на последнем валидном состоянии, не замирает с исключением.
3. **Checksum в release**: mismatch → клиент рапортует
   `ReportChecksumMismatchServerRpc`; сервер: ≥3 репорта за окно 60 c → немедленный
   re-publish; ≥8 → kick клиента. Правила: `SnapshotDesyncRules`.
4. Битый last-good при миграции = продолжить без него (норма по `match-network.md`),
   не ронять миграцию.

## Контракт презентации (обязателен)

> Презентер имеет право читать только:
> 1. поля снапшота (`MatchUnitSnapshot` merged view, Buildings, Players, …);
> 2. каналы Events (AbilityCast / ProjectileSpawn / будущие);
> 3. диффы реплицированных полей (IsRuins, удаление юнита из списка).
>
> Чтение любых полей `MatchUnitState`, отсутствующих в wire, и подписка на
> C#-события хостового сима (`UnitKilled`, колбэки импактов) в презентере
> **запрещены** — такие эффекты физически невозможны на клиентах.

Исторический пример бага: `SpawnMeleeImpactFx` читал `unit.CurrentTargetId` —
host-sim поле вне снапшота → вся melee-кровь была невидима клиентам. Починено
полями `TargetUnitId` / `TargetBuildingInstanceId` в UnitsDynamic.

## Как добавить визуал (два легальных пути)

| Эффект | Путь | Шаги |
|--------|------|------|
| Разовый (каст, прок, прилёт, разрушение) | Новое событие EventStream | 1) Новый `typeId` в `MatchSnapshotSections` + payload-структура. 2) Хост эмитит при свершении (в сим-коде). 3) Декодер парсит в объектную модель + презентер играет. 4) Тест: host tick → capture → client apply → данные дошли |
| Непрерывное состояние (аура, ammo-hide, бафф-индикатор) | Поле Static/Dynamic блока | 1) Поле в `MatchUnitSnapshot` + заполнение в `Capture`. 2) Чтение в `UpsertAuthoritativeUnit` → `MatchUnitState`. 3) Презентер рисует по состоянию. 4) Тест roundtrip + apply |

Новый тип события **не требует** бампа версии: старые клиенты пропускают его по
длине. Бамп версии нужен только при изменении смысла существующих секций.

Guard-тесты, которые ловят нарушения:
- `MatchSnapshotWireContractTests` — фрейминг, skip, truncation, budget;
- `PresentationCatalogTests` — каждый abilityId каталога и main-extra имеет
  `VfxPrefab != null && Color.a > 0`;
- `MatchSnapshotApplyTests.ApplyAuthoritativeSnapshot_CarriesMeleeTargetToClient`
  и соседние — host→client доставка каждого типа данных.
