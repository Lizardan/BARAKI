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
- Титан: runtime-scale `UnitGreyboxVisuals.TitanVsCreepScale` (**×2** к creep), aura —
  `MatchCombatPresenter.AttachTitanDivineAura` (TT burning_small + soft point light).
  Масштаб применяется при любом spawn (park на базе и deploy с barracks).
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
