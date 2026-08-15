# Структура ScriptableObject-контента

`Assets/Game/ScriptableObjects` организован по владельцу контента, а не по C#-типу:

```text
ScriptableObjects/
├── Catalogs/                         # глобальные индексы
├── Races/
│   └── Humans/
│       ├── RACE_HUMAN.asset
│       ├── Units/Base/
│       ├── Units/Enhanced/
│       ├── Heroes/Base/
│       ├── Heroes/Enhanced/{Hero1,Hero2,Hero3}/
│       └── Abilities/
│           ├── Heroes/{Hero1,Hero2,Hero3}/
│           ├── Caster/
│           ├── Titan/
│           ├── EnhancedUnits/
│           └── EnhancedHeroes/{Hero1,Hero2,Hero3}/
└── Shared/
    ├── Squads/
    └── Upgrades/
```

## Правила

- Новая раса получает собственный каталог `Races/<PluralRaceName>/` с теми же категориями.
- `Catalogs` содержит только общие lookup-ассеты: `RaceCatalog`, `UnitVisualCatalog`,
  `UnitAbilityCatalog`.
- `Shared` — только действительно одинаковый межрасовый контент. Human caster spells и текущие
  abilities не shared: по GDD магия уникальна для расы.
- Для будущего бонусного контента заранее созданы:
  `Units/Enhanced`, `Heroes/Enhanced/Hero1..3`, `Abilities/EnhancedUnits`,
  `Abilities/EnhancedHeroes/Hero1..3`, `Bonuses/Replacements`, `Bonuses/Unique`.
- Bonus definitions содержат выбор и ссылки. Статы лежат в `Units`/`Heroes`, способности —
  в `Abilities`; данные не дублируются.
- Все editor-пути брать из `ContentAssetPaths`. Перемещения выполнять через
  `AssetDatabase.MoveAsset`, чтобы сохранялись `.meta`, GUID и сериализованные ссылки.
- `RaceCatalog` и `UnitVisualCatalog` хранят массивы записей по `raceId`; Human-builders
  обновляют только свою запись и не удаляют контент других рас.
- `UnitAbilityCatalog` глобальный: ability id уникален между расами, а Human-builder сохраняет
  записи других рас.
- Имена definition-ассетов сохраняют стабильный id (`UNIT_HUMAN_*`, `HERO_HUMAN_*`);
  ability-файлы используют kebab-case от `DisplayName`.
