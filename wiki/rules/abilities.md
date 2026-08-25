# Система способностей (Abilities)

Данные-ориентированная система способностей: тюнинг живёт в C#-дефолтах, генерируется в
ScriptableObject-ассеты (`UnitAbilityDef` + поведение-субассет), раздаётся по префабам героев
через единый `UnitCombatSettings`, а в рантайме кастуется единым циклом без switch-диспетчеризации.

## Ключевые понятия

| Термин | Что это |
|--------|---------|
| `AbilityIds` | Стабильные int-константы id способностей (`AbilityIds.cs`). Передаются в снапшот как `ushort` |
| `UnitAbilityDef` | SO-ассет одной способности: id, display name, описание, kind, unlock, все тюнинг-параметры, `AbilityFx`, поведение |
| `UnitAbilityBehaviour` | SO-субассет внутри def: структурная логика (`TryCast`, `QueryAura`, `DescribeParams`) |
| `UnitAbilityCatalog` | Общий список всех def-ов (29 шт.), сериализован в `Assets/Game/ScriptableObjects/Catalogs/UnitAbilityCatalog.asset` |
| `UnitCombatSettings` | Единственный боевой компонент префаба: runtime-снапшот статов + массив ссылок на def-ы. **Порядок списка = приоритет каста AI** |
| `AbilityKitDefaults` | Источник истины тюнинга: фабрики китов `CreateKing/Paladin/Priest/Titan/Caster` + runtime-фолбэк |

## Правила именования

- **`DisplayName` каждого def уникален** — не бывает двух способностей с одним названием
  («Aura», «Heal»). Имя — 1–2 слова по смыслу из описания (например, аура урона — «Attack Aura»,
  аура скорости атаки — «Haste Aura», аура брони — «Iron Aura», точечный хил — «Mend»).
  За нарушением следит `AbilityKitDefaultsTests` (не должно быть повторов).
- **Имя файла ассета** = kebab-case из `DisplayName` (`attack-aura.asset`). При нарушении
  уникальности имён билдер подстраховывается суффиксом `-{id}` (`aura-13.asset`).

## Хранение и генерация данных

- **Источник истины** — `AbilityKitDefaults.cs` (константы берутся из `HeroAbilityRules` /
  `CasterSpellRules`). Ручные правки готовых def-ассетов **затираются** при пересборке,
  кроме `AbilityFx` и ненулевых `Radius` / `CastRange` / `SecondaryRadius` (их пишет
  BARAKI Studio).
- **Меню-инструменты** (Game.Editor):
  - `BARAKI/Abilities/Build Ability Defs` — собирает уникальные способности из дефолтов, создаёт
    по одному `UnitAbilityDef.asset` на способность + каталог. Файлы кладутся в папку владельца
    (`Races/Humans/Heroes/Hero1/Abilities/`, `Units/Caster/Abilities/`,
    `BonusUnits/Melee/Abilities/`, `Heroes/Titan/Abilities/`, …). Имена — kebab-case из
    `DisplayName` (`mend.asset`, `holy-nova.asset`, `attack-aura.asset`); при совпадении имён
    добавляется суффикс `-{id}`. При смене DisplayName старый файл мигрируется по `AbilityId`
    (`AssetDatabase.MoveAsset` — GUID-ссылки из префабов/каталога сохраняются). Легаси-имена
    `Ability{id}.asset` также мигрируются автоматически. **`AbilityFx.Color` и `VfxPrefab` на
    существующих ассетах сохраняются** (не затираются дефолтами).
  - `BARAKI/Abilities/BARAKI Studio` — одно окно: список, живое превью 1:1, палитра VFX,
    якоря, клипы, слайдер боевого радиуса. Канон: `wiki/rules/ability-fx.md`.
  - `BARAKI/Units/Seed Unit Abilities` — записывает def-ссылки в `UnitCombatSettings` префабов
    (`UnitAbilitySeeder`).
- **Runtime-фолбэк**: если в prefab settings нет ссылок — `AbilityKitDefaults.Create(role, heroSlot)`
  (`MatchCombatSystem.AttachAbilities`).

## Ид-таблица (29 способностей)

| id | DisplayName | Кит | kind | Unlock |
|----|-------------|-----|------|--------|
| 1 | Mend | Caster | Active | Magic 1+ |
| 2 | Frost | Caster | Active | Magic 2+ |
| 3 | Resurrect | Caster | Active | Magic 3+ |
| 10 | Group Heal | King | Active | Hero lvl 4 |
| 11 | Ultimate | King | Active | Hero lvl 10 |
| 12 | Strike | King | Active | Hero lvl 1 |
| 13 | Attack Aura | King | Passive | Hero lvl 7 |
| 20 | Smite | Paladin | Active | Hero lvl 1 |
| 21 | Shield | Paladin | Active | Hero lvl 4 |
| 22 | Consecration | Paladin | Active | Hero lvl 10 |
| 23 | Haste Aura | Paladin | Passive | Hero lvl 7 |
| 30 | Holy Nova | Priest | Active | Hero lvl 1 |
| 31 | Greater Heal | Priest | Active | Hero lvl 4 |
| 32 | Revive | Priest | Active | Hero lvl 10 |
| 33 | Iron Aura | Priest | Passive | Hero lvl 7 |
| 40 | Rally | Titan | Active | Hero lvl 4 |
| 41 | Stomp | Titan | Active | Hero lvl 10 |
| 42 | Slam | Titan | Active | Hero lvl 1 |
| 43 | Colossus | Titan | Passive | Hero lvl 7 |
| 50 | Siege Regen Aura | Siege BONUS | Passive | Always |
| 51 | Cleave | Melee BONUS | Passive | Always |
| 52 | Deadeye | Ranged BONUS | Passive | Always |
| 53 | Battlemace | Caster BONUS | Passive | Always |
| 54 | Last Call | Flying BONUS | Passive | Always |
| 55 | Catapult | Super BONUS | Passive | Always |
| 56 | King's Command | King Veteran (слот 7) | Active | Hero lvl 10 |
| 57 | Aegis | Paladin Veteran (слот 8) | Active | Hero lvl 4 |
| 58 | Sanctuary | Priest Veteran (слот 9) | Active | Hero lvl 4 |
| 59 | Greater Colossus | Titan Veteran (слот 10) | Passive | Hero lvl 7 |

## Runtime-поток каста (`MatchCombatSystem`)

1. `AttachAbilityKit` (L2240): копирует ссылки def-ов с префаба героя в `MatchUnitState.Abilities`.
2. Кулдауны слотов тикает `TickAbilityCooldowns` **каждый тик** в начале `TickUnit` — в том числе
   во время cast-lock (эффективный кулдаун = значение def-а, без накидки на анимацию).
   Затем `TryCastKitAbilities` (L2299) по порядку кита для каждого активного def:
   `IsAbilitySlotUnlocked` → кулдаун слота → `TryCastAbility`.
   Кастованный слот **прерывает** остальные (приоритет = позиция в списке).
3. `IsAbilitySlotUnlocked` (L2280): `Always` / `HeroLevel` (`unit.Level >= UnlockValue`) /
   `MagicLevel` (`GetMagicLevel(ownerSlot) >= UnlockValue`).
4. Поведение решает, есть ли цель/условие (`TryCast`); кулдаун ставит хост через
   `ArmSlotCooldown`. Успешный каст уходит в снапшот через `IUnitAbilityHost.EmitCast`
   (`AbilityCastEvent { AbilityId, CasterUnitId, TargetUnitId, Position }`).
5. После успешного `TryCast` хост ставит **cast-lock** (`AbilityAnimRules`): юнит не ходит,
   не бьёт автоатакой и не кастует, пока `CastLockRemainingSeconds` > 0.
   - `AbilityAnimKind.Attack` (Strike, Slam, Smite, …) → `BehaviorState.Attack` + `AttackSwingSerial++`,
     лок = attack interval.
   - `AbilityAnimKind.Cast` (Mend, Group Heal, Rally, Shield, …) → `BehaviorState.Cast`,
     лок = `CastLockSeconds` (~1.1 с).
   Эффект умения применяется сразу; лок только на AI/анимацию.
   Когда cast-lock истекает в текущем тике, юнит **не** возвращается раньше времени: в том же тике
   кастует следующую готовую способность (кулдауны уже могли обнулиться — см. п.2).
6. Пассивные ауры кастуются всегда через `QueryAura` (см. ниже).

## Анимации способностей (`AbilityAnimRules`)

| Kind | AbilityIds | Клип |
|------|------------|------|
| Attack | Strike, Ultimate, Smite, Consecration, Slam, Stomp | пул Attack A/B |
| Cast | Mend, Frost, Resurrect, Group Heal, Holy Nova, Greater Heal, Revive, Shield, Rally | пул Cast A/B |
| None | пассивные ауры | — |

Автоатака (не умения): удар/вылет снаряда в середине клипа
(`CombatAttackRules.SwingImpactNormalizedTime` × attack interval).
`Animator.speed = AbilityAnimRules.ResolveAttackClipSeconds(role) / interval`
(пехота 1.5 с, баллиста/кавалерия 1 с). Снарядный урон — на прилёте.
Cast-lock: staff cast **1.5 с**, Rally punch **1 с**.

## World FX презентер (`MatchCombatPresenter` / `SpellFxFactory`)

- Имя способности: `SpellFxFactory.CreateLabel` — жирный TextMesh + чёрная 8-dir обводка,
  якорь `LowerCenter`, спавн **прямо над полоской HP** кастера
  (`StatusBars.TransformPoint(0, HealthBarTopLocalY + clearance, 0)`).
- Высота HP-полосок: `UnitVisualHeight.MeasureAboveFeet` меряет только `MeshRenderer` /
  `SkinnedMeshRenderer` у модели. Particle/trail (aura титана и т.п.) **не** поднимают бар.

## Поведения (`Combat/Abilities/`)

| Behaviour | Суть | Используется |
|-----------|------|--------------|
| `GroundAoeBehaviour` | Урон/оглушение по области вокруг кастера (флаг `applyUltimateSelfBuff` — бафф собств. урона) | Ultimate, Strike, Consecration, Frost, Stomp, Slam |
| `HealSingleBehaviour` | Лечение самого раненого союзника в радиусе каста | Caster Heal |
| `HealAreaBehaviour` | Лечение героя и союзников вокруг | King Heal |
| `GreaterHealZoneBehaviour` | Зона лечения (heal/s на время, `ReplaceHealZone`) | Greater Heal |
| `NovaBehaviour` | Вспышка у цели: лечит своих и бьёт врагов рядом | Holy Nova |
| `ArmorShoutBehaviour` | Временный бонус брони герою и союзникам рядом | Shield, Rally |
| `ReviveBehaviour` | Возрождает ближайший труп + лечение вокруг | Revive |
| `ResurrectCorpseBehaviour` | Поднимает свежий труп с полным HP | Resurrect |
| `DamageBurstBehaviour` | Урон по ближайшему врагу | Smite |
| `AuraBehaviour` | Пассивный %-бонус стата армии владельца (`AuraStat`: Damage/AttackSpeed/Armor/MaxHp/HpRegen) | Aura, Colossus |

## Ауры (радиус + реген + визуал)

- Ауры действуют на **живых юнитов владельца в радиусе** `HeroAbilityRules.AuraRadius` (8) от носителя
  (горизонтальная дистанция XZ; сам носитель всегда внутри). До v19 были «вся армия» — позиционная
  проверка добавлена в `MatchCombatSystem.GetAuraPercent(ownerSlot, stat, position)` (максимум по носителям).
  Если `def.Radius > 0` — берётся радиус из def (для аур задаётся при сборке кита).
- `AuraStat.HpRegen`: аура регенерации HP (использует `def.FlatBonus`, HP/с). Тикается каждый тик
  `TickAuraRegen` (после `TickHealZones`): хил через `HeroAbilityRules.ApplyHeal` / `GetEffectiveMaxHp`,
  лечит и носителя (дистанция 0).
- Визуал: полупрозрачный диск (α≈0.28) под носителем — `MatchCombatPresenter.SyncAuraDisc`
  (`RoadPlatformMesh.BuildDisc`, URP Unlit transparent, `_BaseColor`/`_Color`). Радиус/цвет/abilityId
  реплицируются в снапшоте (UnitsStatic) и рисуются **всем** клиентам; на хосте — через
  `combat.TryGetAuraVisual`.

## Снапшот (кодек)

- Формат снапшота — **v21**, секционный (см. `wiki/rules/snapshot-wire.md`). Способности в снапшоте —
  события `AbilityCast` в EventStream c `AbilityId` (`ushort`); в объектной модели — `Snapshot.SpellCasts`
  (имя осталось с legacy-версий, не менять без рефакторинга кодека+презентера).
- Ауры: `AuraRadius` / `AuraColorPacked` / `AuraAbilityId` в UnitsStatic; клиент рисует по явному
  `AuraAbilityId`, цвет — только для рендера диска. Бонусы: `BonusSlot` там же.
- Изменения способностей = изменение кол-ва def-ов/параметров; изменять **кодек не нужно**,
  пока id стабильны. Новый визуал = новое событие EventStream (без бампа версии).

## Инспекторы (Game.Editor)

- **`UnitCombatSettingsEditor`** — единый read-only viewer префаба: сверху красиво оформленные
  боевые статы и кнопка перехода к `UnitDefinition` / `HeroDefinition`, ниже — карточки способностей.
  Если у юнита нет способностей, секция способностей не рисуется вообще.
- Каждая карточка способности показывает номер+имя (цвет = kind), цвет FX-свач, описание,
  только ненулевые числовые параметры, правило unlock, поведение и FX. Кнопка **«Открыть»**
  выделяет и пингует `UnitAbilityDef`-ассет.
- **`UnitAbilityDefEditor`** на самом def-ассете показывает по умолчанию только **ненулевые** тюнинг-строки
  (ноль не мусорит — например, у не-хилящих нет поля «Heal»); toggle «Показать все» раскрывает нулевые.
  Read-only-сводку см. выше; урон/хил/CD пересобираются из дефолтов. `AbilityFx` и ненулевые
  `Radius` / `CastRange` **не** затираются — их правит BARAKI Studio (`ability-fx.md`).
- Статы и способности на префабе не редактируются: баланс правится в definition-ассете и переносится
  через `BARAKI/Units/Sync Balance to Prefabs`, способности — в def-ассетах и затем сидируются через
  `BARAKI/Units/Seed Unit Abilities`.

## Main extra ability (Divine Blessing)

После `UPG_MAIN_DIVINE_BLESSING` игрок открывает меню **2×3** и выбирает **одну** способность
через `MatchController.TryPickMainExtraAbility`. Выбор в `MatchPlayerState.MainExtraAbilityId`
и снапшоте **v18** (+ `MainMana`, `MainExtraAbilityCooldownRemaining`).
Исследование также снимает FoW владельцу (мир + оверлей миникарты) — см. `fog.md`.

| Id | Name | Effect | Gate |
|----|------|--------|------|
| 1 | Кара зданий | true 1200 dmg вражескому зданию; CD 180s; 200 mana | melee+ranged+armor ≥7 **и** magic ≥2 |
| 2 | Кара юнитов | 5000 dmg вражескому юниту; CD 180s; 200 mana | тот же |
| 3–6 | Скоро | stub | всегда locked |

Каст: слот 9 main → targeting mode (`MatchSelectionBridge.BeginMainExtraAbilityTargeting`) →
LMB по цели → `TryCastMainExtraAbility` / net `RequestCastMainExtraAbility`.
UX прицела: красный крестик (`MainExtraAbilityCursor`), красное ground-кольцо на валидном
hover (`MatchMainExtraTargetingRingPresenter`), tooltip имени у курсора (`TargetingTooltip` в MatchHud;
позиция через `MatchSelectionUiPointer.ScreenToPanelPosition` — Input System Y снизу, UITK сверху).
Отмена: RMB / Esc / клик в пустоту.
VFX: каст назначает `AbilityFx.VfxPrefab` в `UnitAbilityDef.Fx` (или `MainExtraAbilityFxCatalog`
для ids 100/101); презентер спавнит префаб + label. Видят все клиенты.
Мана main: `MainManaMax = 100 * MainLevel`, реген полный пул за 180 с.
HP зданий растут с уровнем (main 2000/2500/3000, barracks 800/1100/1400/1600).
Не путать с `UnitAbilityDef` юнитов (FX-defы — runtime `MainExtraAbilityFxDefs`).

## Как добавить способность

1. Добавить константу id в `AbilityIds.cs`.
2. Добавить тюнинг-константы в `HeroAbilityRules` / `CasterSpellRules`.
3. Вставить def в нужный кит `AbilityKitDefaults.CreateXxx()` (позиция в массиве = приоритет каста).
4. Если нужна новая механика — добавить `UnitAbilityBehaviour` в `Combat/Abilities/`.
5. Меню: `Build Ability Defs` → `Seed Unit Abilities`.
6. Открыть BARAKI Studio и назначить VFX + радиус (`ability-fx.md`).
7. Прогнать тесты `AbilityKitDefaultsTests` и затронутые (см. ниже).

## Как изменить тюнинг

Править **только** дефолты (`AbilityKitDefaults` + правила), затем `Build Ability Defs` +
`Seed Unit Abilities`. Прямые правки тюнинга def-ассетов в инспекторе — временные, на пересборке
затираются. Визуал (`AbilityFx`) и ненулевой `Radius` / `CastRange` правятся в BARAKI Studio
и сохраняются (`ability-fx.md`).

## Тесты

- `AbilityKitDefaultsTests` — киты совпадают с ожидаемыми способностями; проверки засеянных префабов
  (`HumanHero3Prefab_HasPriestKitWhenSeeded`, `HumanTitanPrefab_HasTitanKitWhenSeeded`).
- `AbilityVfxKindRulesTests`, `AbilityVfxPrefabIndexTests`, `AbilityFxPreserveTests`,
  `AbilityFxMechanicRulesTests`, `UnitAbilityDefApplyTests` — Studio: палитры, якоря, механика AoE,
  preserve Fx/радиуса при rebuild.
- `HumanBonusCombatTests` — EmitCast на проках Cleave / Deadeye / Catapult / Last Call.
- `HeroAbilityCombatTests`, `CasterSpellRulesTests`, `HeroLevelRulesTests` — логика каста/приоритета/unlock.
- `HeroAbilityRulesTests` — display names покрывают паладинов и жрецов.
- `MatchSnapshotCodecTests.RoundTrip_V16_PreservesSpellCasts` / `RoundTrip_V17_PreservesDivineBlessingFields` /
  `RoundTrip_V18_PreservesMainManaAndCooldown`,
  `MatchSnapshotApplyTests`, `MainExtraAbilityRulesTests`, `DivineBlessingMatchControllerTests`.
- `UnitVisualHeightTests` — высота HP-полоски не раздувается particle VFX (aura титана).
