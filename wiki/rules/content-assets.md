# Структура ScriptableObject-контента

Контент расы лежит **по категории**: открыл `Units` — все обычные юниты; `BonusUnits` —
все усиленные юниты; `Heroes` — герои и титан. Внутри категории — папка роли
(`Melee`, `Hero1`, `Titan`) с def и `Abilities/` если есть кит.

```text
ScriptableObjects/
├── Catalogs/                              # только глобальные индексы
├── Shared/                                # реально межрасовое
│   ├── Squads/
│   └── Upgrades/
└── Races/
    └── Humans/
        ├── RACE_HUMAN.asset
        ├── Units/                         # обычные юниты (без титана)
        │   ├── Melee/                     # UNIT_HUMAN_MELEE
        │   ├── Ranged/
        │   ├── Caster/                    # + Abilities (Mend / Frost / Resurrect)
        │   ├── Siege/
        │   ├── Flying/
        │   └── Super/
        ├── BonusUnits/                    # усиленные юниты слотов 1–6
        │   ├── Melee/                     # + Abilities (Cleave)
        │   ├── Ranged/                    # Deadeye
        │   ├── Caster/                    # Battlemace
        │   ├── Siege/                     # siege-regen-aura
        │   ├── Flying/                    # Last Call
        │   └── Super/                     # Catapult
        └── Heroes/                        # чемпионы + титан
            ├── Hero1/                     # def + Abilities
            ├── Hero2/
            ├── Hero3/
            └── Titan/                     # Abilities (Rally / Stomp / Slam / Colossus)
```

`BonusHeroes/` (усиленные герои и титан) — **не создавать заранее**; папка появится
вместе с первыми ассетами.

Тот же шаблон у префабов; портреты плоские внутри категории (один PNG на роль):

```text
Prefabs/Races/Humans/
├── Units/{Role}/Human_{Role}.prefab (+ .controller рядом)
├── BonusUnits/{Role}/Human_{Role}_BONUS.prefab
├── Heroes/{HeroN|Titan}/Human_{HeroN|Titan}.prefab
└── Buildings/

Prefabs/Fx/Custom/                             # BARAKI Studio, чип «Кастом»
└── SkyBeam.prefab                             # луч сверху (Divine Blessing / Кара)

Art/UI/UnitPortraits/Humans/
├── Units/{Melee|Ranged|Caster|Siege|Flying|Super}.png
├── BonusUnits/{Melee|…|Super}.png
└── Heroes/{Hero1|Hero2|Hero3|Titan}.png
```

## Правила

- Новая раса: `Races/<PluralRaceName>/` с категориями `Units/`, `BonusUnits/`, `Heroes/`
  (и `BonusHeroes/` только когда появятся ассеты).
- Бонусные юниты — **соседняя** категория `BonusUnits/{Role}/`, не `Units/{Role}/Bonus/`.
- Титан — в `Heroes/Titan/`, не в `Units/`.
- Папка существует только если в ней есть ассеты. Пустой scaffold (`BonusHeroes`,
  `Enhanced`, `Bonuses`, `AI`, `Tech`, `Passives`, `Buildings` у SO, общие
  `Controllers/`) **не создавать заранее**.
- Герой/кит с abilities — папка владельца (`Heroes/Hero1`, `Units/Caster`,
  `BonusUnits/Siege`); abilities внутри `Abilities/`.
- `Catalogs` — только lookup: `RaceCatalog`, `UnitVisualCatalog`, `UnitAbilityCatalog`.
- `Shared` — только действительно одинаковый межрасовый контент.
- Все editor-пути — из `ContentAssetPaths` / `UnitVisualPrefabBuilder`. Перемещения —
  через `AssetDatabase.MoveAsset` (сохраняются `.meta`, GUID и ссылки).
  Миграция: меню `BARAKI/Content/Migrate Content Folders`.
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
