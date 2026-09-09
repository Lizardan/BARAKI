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

`BonusHeroes/` — категория ветеранских чемпионов (PRE-006b): префабы через
`BARAKI/Units/Build Veteran Prefabs`, ability defs — сигнатуры слотов 7–10
(`kings-command`, `aegis`, `sanctuary`, `greater-colossus`).

Тот же шаблон у префабов; портреты плоские внутри категории (один PNG на роль):

```text
Prefabs/Races/Humans/
├── Units/{Role}/Human_{Role}.prefab (+ Human_{Role}.controller рядом)
├── BonusUnits/{Role}/Human_{Role}_BONUS.prefab (+ .controller рядом)
├── Heroes/{HeroN|Titan}/Human_{HeroN|Titan}.prefab (+ .controller рядом)
│   └── BonusHeroes/{HeroN|Titan}/Human_{HeroN|Titan}_BONUS.prefab
└── Buildings/

Prefabs/Fx/Custom/                             # BARAKI Studio, чип «Кастом»
└── SkyBeam.prefab                             # луч сверху (Divine Blessing / Кара)

Art/UI/UnitPortraits/Humans/
├── Units/{Melee|Ranged|Caster|Siege|Flying|Super}.png
├── BonusUnits/{Melee|…|Super}.png
├── Heroes/{Hero1|Hero2|Hero3|Titan}.png
└── BonusHeroes/{Hero1|Hero2|Hero3|Titan}.png

Art/UI/UnitPortraits/Faceless/                 # та же раскладка, что Humans:
├── Units/…  ├── BonusUnits/…                  # пекутся тем же UnitPortraitBaker
├── Heroes/… └── BonusHeroes/…                 # (BARAKI/Faceless/Update Visual Catalog)
```

**Единый шаблон расы** (все расы — одинаково, в т.ч. Faceless):
префаб и его `AnimatorController` лежат рядом, имя контроллера = имя префаба.
**Особый случай — бонус-префабы Faceless:** `Faceless_*_BONUS.prefab` лежит в `BonusUnits/{Role}/`.
**Задумано:** у бонуса своя (заменённая) модель + свой `Faceless_*_BONUS.controller` рядом (как
`Human_*_BONUS`). **Сейчас (временная заглушка, пока нет моделей Древних):** клонируется базовая
модель и переиспользуется её контроллер, признак усиленного юнита — `BonusFlameMarker`.
Канон — `wiki/rules/faceless-unit-bonuses.md` § «Канон префабов бонусных юнитов».
Арт-ассеты (клипы, меши, материалы) — централизованно в `Art/Races/<Race>/`
(у самого арта из внешнего пакета — клипы не кладутся в `Assets/Game`).

```text
Prefabs/Races/<Race>/                         # только префабы + контроллеры
└── {Units|BonusUnits|Heroes|BonusHeroes}/{Role}/<Race>_{Role}[_BONUS].prefab

Art/Races/<Race>/                             # клипы/меши/материалы расы
└── Production/{Anim,Meshes,Mats}/            # рабочие ассеты (клипы Stand/Walk/Attack/Death/Cast)
```

## Правила

- Новая раса: `Races/<PluralRaceName>/` с категориями `Units/`, `BonusUnits/`, `Heroes/`
  (и `BonusHeroes/` только когда появятся ассеты). Контроллеры — рядом с префабами
  (имя = имя префаба); клипы/меши/материалы — в `Art/Races/<Race>/Production/`.
- Бонусные юниты — **соседняя** категория `BonusUnits/{Role}/`, не `Units/{Role}/Bonus/`.
- Титан — в `Heroes/Titan/`, не в `Units/`.
- Папка существует только если в ней есть ассеты. Пустой scaffold (`Enhanced`, `Bonuses`,
  `AI`, `Tech`, `Passives`, `Buildings` у SO, общие `Controllers/`) **не создавать заранее**.
- Герой/кит с abilities — папка владельца (`Heroes/Hero1`, `Units/Caster`,
  `BonusUnits/Siege`); abilities внутри `Abilities/`.
- `Catalogs` — только lookup: `RaceCatalog`, `UnitVisualCatalog`, `UnitAbilityCatalog`.
- `Shared` — только действительно одинаковый межрасовый контент.
- Все editor-пути — из `ContentAssetPaths` / `UnitVisualPrefabBuilder`. Перемещения —
  через `AssetDatabase.MoveAsset` (сохраняются `.meta`, GUID и ссылки).
  Миграция: меню `BARAKI/Content/Migrate Content Folders`.
- `RaceCatalog` / `UnitVisualCatalog` — записи по `raceId`; Human-builders обновляют только свою.
- **Язык контента:** имена (DisplayName, названия бонусов/уников/слотов) — **английские**;
  описания (Description, тултипы) — **русские**. У всего контента.
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
