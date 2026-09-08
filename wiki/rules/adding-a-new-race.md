# Добавление новой расы (бонус-кит и гейт)

Рас-агностичный слой бонус-слотов вынесен в `BonusKitRules.cs` (`Assets/Game/Scripts/Runtime/Gameplay/Combat/`).
Новая раса НЕ трогает общий слой — только добавляет свою расовую константы-класс в `GameAssembly`/`Game.Gameplay`.

Рефакторинг: `HumanBonusUnitRules` и `FacelessBonusUnitRules` сокращены до **уникальной** расовой логики
(константы эффектов + маппинги слотов на механики). Слоты 1–12, veteran-множители, effective-слот резолверы,
`HasBonusKit` / `NoBonusKitRaceIds`, `MatchesUnit`, `ApplyVeteranMultipliers`, `RollProc` — все живут в `BonusKitRules`.

## Топология слотов (`BonusKitRules`)

| Диапазон | Слоты | Что это |
|----------|-------|---------|
| `MinBonusSlot..MaxBonusSlot` | 1–6 | юнитовые бонусы (Melee/Ranged/Caster/Siege/Flying/Super) |
| `Hero1..3BonusSlot` | 7–9 | ветераны-герои (hero slot = слот − `HeroBonusSlotOffset`) |
| `TitanBonusSlot` | 10 | ветеран-титан |
| `RaceUnique1Slot..2Slot` | 11–12 | расовые уники (player-level модификаторы) |

- `BonusSlotForRole(role)` / `RoleForBonusSlot(slot)` — карта роль↔слот(1–6).
- `BonusSlotForHeroSlot(heroSlot)` = `HeroBonusSlotOffset + heroSlot`.
- Veteran-множители: `VeteranHpMultiplier=1.4f`, `VeteranDamageMultiplier=1.35f`,
  `VeteranArmorBonus=2f` — общие для всех рас с китом (как Human PRE-006b).

## Чек-лист новой расы

1. **Материалы** (если уже есть): префабы, саунда, портреты — по шаблону `Prefabs/Races/<Race>/`,
   `Art/Races/<Race>/`; детали и правила папок — `wiki/rules/content-assets.md`.

2. **Расовые константы**: создать `<Race>BonusUnitRules.cs` (например `ElvenBonusUnitRules.cs`),
   namespace `Game.Gameplay.Combat`:
   - константы эффектов слотов 1–6 (проц-чансы, урон, радиусы, хил/стаки);
   - константы сигнатур ветеранов 7–10 (self-buff, радиусы/длительности зон, aura-хил-фракции);
   - уники 11–12 (модификаторы движения/HP зданий и т.п.).
   - Уникальные маппинги слот→механика этой расы. Общего ничего не копировать — брать из `BonusKitRules`.

3. **Ability defs / киты** (`AbilityIds.cs`, `AbilityKitDefaults.cs`):
   - новые `AbilityId` для сигнатур ветеранов (как `CreateAncientMantle/AreaOfMiss/FeastZone/AuraOfHunger`);
   - `Create<Race>VeteranKit(slot)` + поддержка в `CreateForSpawn(raceId, ...)` для совпадающего слота.

4. **Боевые хуки** (`MatchCombatSystem.cs`): on-hit / on-kill / aura-хуки этой расы, гейтованные расой
   (`GetPlayerRaceId(unit.OwnerSlot) == RACE_<N>`).

5. **Гейт статуса** (`BonusKitRules.NoBonusKitRaceIds`):
   - **Пока кита/визуала нет** — добавить id расы в `NoBonusKitRaceIds`: `HasBonusKit` → false,
     все race-aware `EffectiveBonusSlotFor*` дают 0, `UnitStatsResolver.ResolveBase` не навесит
     бонус/ветеран-статы. Бонус-пик в UI покажет слоты (картинки/тултипы — из `BonusPickRules`,
     Faceless-ветка есть, для новой расы добавить свою), но выбрать реально нельзя до снятия гейта.
   - **Приготовить к релизу** — убрать id из `NoBonusKitRaceIds`.

6. **UI (`BonusPickRules.cs`)**: для новой расы добавить `GetSlotDisplayName` / `GetSlotDescription`
   ветку (список канона слотов 1–12 на EN + RU-описание). Слоты без механики — серые через
   `FacelessImplementedSlots`-аналог (массив реализованных слотов расы) + `IsSlotImplemented`/`IsSlotAvailable`.

7. **Тесты** (`Game.Tests`): скопировать шаблон `FacelessBonusUnitRulesTests` (механики 1–6) и
   `FacelessVeteranBonusTests` (киты/effective-слоты). Обязательно: гейт — «раса без кита не получает
   множители/бонус-статы» (как `HasBonusKit`-тесты), и «пик применяется только к совпадающей роли».

## Правило «новой расы» при рефакторинге

- Общий слой (`BonusKitRules`) — **единственное** место для: топологии слотов, множителей ветеранов,
  `HasBonusKit`, маппингов role↔slot. Дубликации не плодить — иначе новая раса получится дорогой и багоопасной.
- Рас-класс (`*BonusUnitRules`) — только то, что уникально для расы. Если в двух расах одинаково —
  это кандидат в `BonusKitRules` (или в shared helper).
- Гейт (список в `NoBonusKitRaceIds`) — отдельная строка на расу; снятие гейта = отдельная
  следящая карточка, не побочный эффект контент-коммита.

Связанные правила: `wiki/rules/faceless-unit-bonuses.md` (реализация слотов 1–10 + гейт),
`wiki/rules/human-unit-bonuses.md` (формат ветеранов/уников), `wiki/rules/abilities.md`,
`wiki/rules/content-assets.md`.