# Структура ScriptableObject-контента

Контент расы лежит **по роли**: открыл `Melee` / `Caster` / `Hero1` — сразу видно
базовый def и (если есть) `Bonus/` + `Abilities/`.

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
        │   ├── Hero1/                     # def + Abilities
        │   ├── Hero2/
        │   └── Hero3/
        └── Units/
            ├── Melee/
            │   ├── UNIT_HUMAN_MELEE.asset
            │   └── Bonus/UNIT_HUMAN_MELEE_BONUS.asset
            ├── Ranged/ … Bonus/
            ├── Caster/
            │   ├── UNIT_HUMAN_CASTER.asset
            │   ├── Abilities/             # Mend / Frost / Resurrect
            │   └── Bonus/UNIT_HUMAN_CASTER_BONUS.asset
            ├── Siege/
            │   ├── UNIT_HUMAN_SIEGE.asset
            │   └── Bonus/
            │       ├── UNIT_HUMAN_SIEGE_BONUS.asset
            │       └── Abilities/         # siege-regen-aura
            ├── Flying/ … Bonus/
            ├── Super/ … Bonus/
            └── Titan/
                └── Abilities/             # Rally / Stomp / Slam / Colossus
```

Тот же шаблон у префабов и портретов:

```text
Prefabs/Races/Humans/
├── Units/{Role}/Human_{Role}.prefab (+ .controller рядом)
│            └── Bonus/Human_{Role}_BONUS.prefab
├── Heroes/HeroN/Human_HeroN.prefab
└── Buildings/

Art/UI/UnitPortraits/Humans/
├── Units/{Melee|Ranged|…|Titan}.png
├── Heroes/{Hero1|Hero2|Hero3}.png
└── Bonus/{Melee|…|Super}.png
```

## Правила

- Новая раса: `Races/<PluralRaceName>/` с теми же категориями `Heroes/` и `Units/{Role}/`.
- Бонусные варианты **всегда** в `Bonus/` под ролью — не плоско рядом с базой.
- Папка существует только если в ней есть ассеты. Пустой scaffold (`Enhanced`, `Bonuses`,
  `AI`, `Tech`, `Passives`, `Buildings`, общие `Controllers/`) **не создавать заранее**.
- Герой/кит с abilities — папка владельца (`Heroes/Hero1`, `Units/Caster`); abilities внутри
  `Abilities/`. Аура бонусного Siege — в `Units/Siege/Bonus/Abilities/`.
- `Catalogs` — только lookup: `RaceCatalog`, `UnitVisualCatalog`, `UnitAbilityCatalog`.
- `Shared` — только действительно одинаковый межрасовый контент.
- Все editor-пути — из `ContentAssetPaths` / `UnitVisualPrefabBuilder`. Перемещения —
  через `AssetDatabase.MoveAsset` (сохраняются `.meta`, GUID и ссылки).
  Миграция: меню `BARAKI/Content/Migrate To Role Folders`.
- `RaceCatalog` / `UnitVisualCatalog` — записи по `raceId`; Human-builders обновляют только свою.
- `UnitAbilityCatalog` глобальный: ability id уникален между расами.
- Имена definition-ассетов стабильны (`UNIT_HUMAN_*`, `HERO_HUMAN_*`); ability-файлы —
  kebab-case от `DisplayName`.

## Титан / анимации / снаряды

- Титан: runtime-scale `UnitGreyboxVisuals.TitanVsCreepScale` (**×3** к creep), aura —
  `MatchCombatPresenter.AttachTitanDivineAura`. Attack range: `TitanRules.AttackRange` = **3**.
- Контроллеры собирает `TtUnitVisualSetup` (`BARAKI/Units/Rebuild TT Prefabs`) **рядом** с
  префабом (не в общей `Controllers/`).
- Attack/Cast пулы и тайминги удара — как раньше (`AbilityAnimRules`,
  `CombatAttackRules.SwingImpactNormalizedTime`).
- Визуалы снарядов (`CombatAttackVisualBuilder`, `Resources/Art/`):
  - лучник / flying / башни — `ProjectileBolt`;
  - балиста (Super) / main / barracks — `ProjectileBoltLvl3`;
  - катапульта (Super BONUS, `AppliesSplashAoe`) — `ProjectileCatapultRock`;
  - кастер / герои — fireball + trail (`ProjectileFire`).
