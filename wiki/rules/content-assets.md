# Структура ScriptableObject-контента

`Assets/Game/ScriptableObjects` организован **по владельцу кита**: открыл `Hero1` /
`Caster` / `Titan` — сразу видно definition и abilities этого юнита/героя.

```text
ScriptableObjects/
├── Catalogs/                              # только глобальные индексы
├── Shared/                                # реально межрасовое
│   ├── Squads/
│   └── Upgrades/
└── Races/
    └── Humans/
        ├── RACE_HUMAN.asset
        ├── Heroes/
        │   ├── Hero1/                     # def + Abilities героя 1
        │   ├── Hero2/
        │   └── Hero3/
        └── Units/
            ├── UNIT_HUMAN_MELEE.asset     # юниты без кита — плоско
            ├── UNIT_HUMAN_RANGED.asset
            ├── …
            ├── Caster/                    # def + Abilities кастера
            └── Titan/                     # Abilities титана
                └── Abilities/
```

## Правила

- Новая раса: `Races/<PluralRaceName>/` с теми же категориями `Heroes/` и `Units/`.
- Титан: runtime-scale `UnitGreyboxVisuals.TitanVsCreepScale` (**×3** к creep), aura —
  `MatchCombatPresenter.AttachTitanDivineAura` (TT burning_small + soft point light).
  Масштаб применяется при любом spawn (park на базе и deploy с barracks).
  Walk: `UnitCombatAnimatorDriver` крутит `Animator.speed` =
  `(moveSpeed / 4) / visualScaleVsCreep` (титан ≈ ×⅓ к крипу при той же скорости);
  Attack: `clipLength / attackInterval` (реальная длина TT-клипа: пехота/staff **1.5 с**,
  кавалерия/баллиста **1 с**; Haste Aura ускоряет клип); Cast/Death/Stand = 1.
  Удар/вылет всегда на `interval × 0.5` (= середина ускоренного клипа).
  Attack range: `TitanRules.AttackRange` = **3** (сид/sync с Hero1 ×3 не затирает;
  vs-titan hit reach через `CombatRules.GetUnitAttackReach` — мили бьют с ≥3).

## TT unit anim pools

Контроллеры собирает `TtUnitVisualSetup` (`BARAKI/Units/Rebuild TT Prefabs`):

- **Attack** — пул A/B где есть (`infantry/archer/staff_04_attack_*`); один клип у siege/flying/super/hero2/hero3.
- **Cast** — у caster/heroes/titan: staff/cav_staff cast A/B, titan Rally → `infantry_07_punch_A/B`,
  king Group Heal → `staff_07_cast_*`, paladin Shield → `cav_staff_07_cast_*`.
- Вариант выбирается на свинг (`AttackVariant`) / вход в Cast (`CastVariant`).
- Автоатака: урон/вылет снаряда в `SwingImpactNormalizedTime` (0.5) от интервала атаки;
  лучники — на **0.25**, кастер — на **0.35**. Кастер Attack только `staff_04_attack_B` (без sword `attack_A`).
  Длины клипов — `AbilityAnimRules.ResolveAttackClipSeconds` / cast-lock.
- Папка существует только если в ней есть ассеты. Пустой scaffold (`Enhanced`, `Bonuses`,
  `AI`, `Tech`, `Passives`, `Buildings`) **не создавать заранее**.
- Герой/кит с abilities — отдельная папка владельца (`Heroes/Hero1`, `Units/Caster`),
  abilities внутри `Abilities/`. Так не смешиваются киты разных героев.
- Обычные юниты без abilities лежат плоско в `Units/`.
- `Catalogs` — только lookup: `RaceCatalog`, `UnitVisualCatalog`, `UnitAbilityCatalog`.
- `Shared` — только действительно одинаковый межрасовый контент. Human caster spells не shared.
- Все editor-пути — из `ContentAssetPaths`. Перемещения — через `AssetDatabase.MoveAsset`
  (сохраняются `.meta`, GUID и ссылки).
- `RaceCatalog` / `UnitVisualCatalog` — записи по `raceId`; Human-builders обновляют только свою.
- `UnitAbilityCatalog` глобальный: ability id уникален между расами.
- Имена definition-ассетов стабильны (`UNIT_HUMAN_*`, `HERO_HUMAN_*`); ability-файлы —
  kebab-case от `DisplayName`.
