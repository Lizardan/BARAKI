# Способности главного здания (MAIN-001)

Две всегда-доступные активки main, гейт по уровню самого здания. Отдельная подсистема —
**не** слоты Divine Blessing (ids 1–2 и их механика пика не тронуты). Связанные правила:
`abilities.md` (Divine Blessing, снапшот-касты), `snapshot-wire.md` (v23).

## Канон

| | Слот UI | Гейт | Мана | CD | Эффект |
|---|---|------|------|----|--------|
| **Ледяное кольцо** | 9 | Main L1+ | 50 | 60 c | ground-target: все наземные враги в круге `CasterSpellRules.FrostRadius` (5) получают 300 урона + заморозка 3 с (`FrozenRemainingSeconds`) |
| **Волна света** | 10 | Main L2+ | 150 | 180 c | инстант: жёлтая волна от базы расширяется за 1 с до радиуса `dist(main→барак центр) × 3`, урон 1500 наносится фронтом (каждый враг — один раз при достижении) |

- Цели — **только наземные юниты** (`UnitRole.Flying` исключён; герои/титан ходят — бьются).
- Ледяное кольцо кастуется только в радиусе от базы `dist(main→барак) × 3` — иначе отказ.
- Баланс-числа — плейсхолдеры (решение пользователя 2026-08-26); финальный тюнинг при плейтесте.
- Раскладка панели main: 0–5 апгрейды, 6–8 герои, **9 кольцо, 10 волна, 11 Благословение**
  (`MainExtraAbilityRules.CommandSlotIndex = 11`).

## Карта кода

| Файл | Роль |
|------|------|
| `Gameplay/Match/BuildingAbilityRules.cs` | ids 1/2, тюнинг, гейты, range math, `AffectsUnit`, тик кулдаунов |
| `MatchPlayerState.cs` | `IceRingCooldownRemaining` / `WaveOfLightCooldownRemaining` |
| `MatchController.cs` | `TryCastBuildingAbility(slot, abilityId, center)`, `TickBuildingAbilityCooldowns` |
| `Networking/MatchNetworkAuthority.cs` | `RequestCastBuildingAbilityServerRpc(abilityId, x, y, z)` |
| `Networking/MatchNetworkCommands.cs` | фасад `RequestCastBuildingAbility` |
| `Combat/AbilityIds.cs` | spell-FX ids **102** (`MainIceRing`) / **103** (`MainWaveOfLight`) |
| `Combat/MainExtraAbilityFxCatalog.cs` | поля `_iceRing` / `_waveOfLight` (+`EditorSetFx` для Studio) |
| `Combat/MainExtraAbilityFxDefs.cs` | `GetForBuildingAbility(id)`, runtime FX-defs (клиентский фолбэк резолва) |
| `MatchSelectionBridge.cs` | ground-target режим: рейкаст в плоскость земли, `_aimInCastRange` |
| `Match/BuildingAbilityCursor.cs` | процедурный круговой курсор: синий в радиусе / красный вне |
| `MatchMainExtraTargetingRingPresenter.cs` | AoE-кольцо под указателем (синее/красное), режим ice ring |
| `UI/Runtime/Controllers/MatchInspectorController.cs` | кнопки слотов 9–10, locked-состояние по уровню |
| `Vfx/AbilityFxMechanicRules.cs` | Studio-shapes: AreaOnGround (кольцо) / BurstAroundSelf (волна) |

## Прицел ледяного кольца

Курсор заменяется кругом; кольцо радиуса способности рисуется под указателем на земле.
В разрешённой зоне от базы курсор и кольцо **синие**, вне — **красные**; клик вне зоны
игнорируется (режим прицела не сбрасывается). Отмена: RMB / Esc / повторный клик по кнопке.

## Волна света (расширение)

- Хост: `MatchController._buildingWaves` — фронт `radius × elapsed/1s`; каждый наземный враг
  получает 1500 урона один раз, когда фронт его достаёт (`HitUnitIds`). Волна снимается
  после полного расширения.
- Клиент: `MatchCombatPresenter.StartWaveOfLightFx` — жёлтое ground-кольцо
  (`SelectionRingMeshBuilder.BuildAnnulus(1, 0.05)`, scale = текущий радиус) + burst-префаб,
  масштабируемый от 5% до финального (`MaxRadius / IceRingRadius` × authored base). Кольцо
  показывает реальный радиус фронта; хвост **0.5 с** после полного расширения (1 c рост +
  0.5 c хвост = 1.5 c суммарно), burst гаснет вместе с кольцом.
- Ледяное кольцо: визуал живёт **1 с** (`AbilityVfxPlacement.MainIceRingLifetimeSeconds`),
  заморозка юнитов при этом остаётся 3 с.

## Wire

- Кулдауны едут в Players-секции **v23** (2 float) → `CurrentVersion = 23`, чексумма учитывает.
  Handshake версии в лобби закрывает смешанные билды.
- Каст реплицируется существующим событием `EventAbilityCast` (позиция + id + радиус уже есть)
  — без нового типа события и без бампа. Клиентский def-резолв — фолбэк
  `MainExtraAbilityFxDefs.TryGet` в `ApplyAuthoritativeSpellCasts`.
- Волна света не требует прицела: кнопка шлёт RPC сразу, хост центрирует волну на базе.

## Тесты

`BuildingAbilityRulesTests`, `BuildingAbilityMatchControllerTests`,
`RoundTrip_V23_PreservesBuildingAbilityCooldowns` (в `MatchSnapshotCodecTests`).
После правок: `run_tests` EditMode green.

## VFX

Префабы настраиваются в BARAKI Studio (`ability-fx.md`): группа «Main Building»,
ids 102/103, каталог `Assets/Game/Resources/Fx/MainExtraAbilityFxCatalog.asset`.
Сиды (решение пользователя 2026-08-26): кольцо — **точный FX кастера Frost**
(`CFXR3 Magic Aura A (Runic)`, frost blue, якорь Impact); волна — тот же префаб,
тинт жёлтый (`1, 0.84, 0.28`), расширяется кодом презентера. Автосид пустых префабов
при открытии Studio: кольцо — `CFXR3 Hit Ice B (Air)`, волна — `CFXR3 Hit Light B (Air)`.
