"use strict";

const { HacknPlanClient } = require("./client");
const ids = require("./ids.json");

function body(parts) {
  return parts.filter(Boolean).join("\n");
}

const ITEMS = [
  {
    key: "FACELESS-001",
    title: "[FACELESS-001] Импорт MDX и пронумерованные review-префабы",
    categoryId: ids.categories.Programming,
    description: body([
      "## Суть",
      "Конвертировать WC3 MDX/BLP из `Assets/FacelessRetexture_V2` в меш+PNG, сложить в `Assets/Game/Art/Races/Faceless/`, собрать пронумерованные префабы `_Review/01..12` и ряд для визуального просмотра. Маппинг на роли не делать.",
      "",
      "## Правила/Ограничения",
      "- Пока раса не зафиналена — importance Urgent (Горит), board Now.",
      "- Не регистрировать в `UnitVisualCatalog` / `RaceCatalog`.",
      "- Не включать в `SelectableRaceIds`.",
      "- Пустой scaffold категорий как у людей не создавать.",
      "",
      "## Acceptance criteria",
      "- [ ] MDX/BLP сконвертированы, арт лежит в `Assets/Game/Art/Races/Faceless/`",
      "- [ ] Префабы `Prefabs/Races/Faceless/_Review/01_…` … `12_…` открываются в Editor",
      "- [ ] Модели стоят в ряд с номерами 01–12, можно сделать скрин",
      "",
      "## Agent context (EN)",
      "Warcraft 3 MDX v800 + BLP1. Converter: `Tooling/MdxReview/convert_mdx_to_obj.py`.",
      "Review prefabs only: no Animator, no UnitCombatSettings, no catalog wiring.",
      "Numbering: 01 FacelessOne, 02 Berserker, 03 Ranged, 04 Reaper, 05-07 Sorcerer G1-G3, 08 King, 09 Thanatos, 10 Izual, 11 Worker, 12 Worker Portrait.",
    ]),
  },
  {
    key: "FACELESS-002",
    title: "[FACELESS-002] Маппинг номеров моделей на роли ростера",
    categoryId: ids.categories.Design,
    dependsOn: ["FACELESS-001"],
    description: body([
      "## Суть",
      "После визуального просмотра `_Review/01..12` зафиксировать номер → роль (Melee…Titan, Bonus, герои). Без этого не раскладывать папки как у людей.",
      "",
      "## Правила/Ограничения",
      "- Решение принимает пользователь; агент не угадывает.",
      "- Где нет второй модели — тот же меш + баннер ветерана (как BonusHeroes людей).",
      "",
      "## Acceptance criteria",
      "- [ ] Таблица номер → `UnitRole` / HeroN / Titan / skip записана в GDD или wiki",
      "- [ ] Пробелы (flying/siege/super/здания) явно помечены TBD",
      "",
      "## Agent context (EN)",
      "Blocked until the user assigns numbers after looking at review prefabs.",
      "Target layout after mapping: `Prefabs/Races/Faceless/{Units,BonusUnits,Heroes,BonusHeroes}/` mirroring Humans.",
    ]),
  },
  {
    key: "FACELESS-003",
    title: "[FACELESS-003] Боевые префабы Faceless зеркалом людей",
    categoryId: ids.categories.Programming,
    dependsOn: ["FACELESS-002"],
    description: body([
      "## Суть",
      "После маппинга перенести review-меши в категории как у людей: Units / BonusUnits / Heroes / BonusHeroes. Имена `Faceless_{Role}`. Контроллер рядом с префабом.",
      "",
      "## Правила/Ограничения",
      "- Канон: `wiki/rules/content-assets.md`. Пустых папок нет.",
      "- Переносы только `AssetDatabase.MoveAsset`.",
      "- `_Review` удалить после раскладки.",
      "",
      "## Acceptance criteria",
      "- [ ] Дерево `Prefabs/Races/Faceless/` зеркалит Humans по категориям",
      "- [ ] Пути добавлены в `ContentAssetPaths` / `UnitVisualPrefabBuilder`",
      "- [ ] Тест раскладки не ломает Human-ассерты",
      "",
      "## Agent context (EN)",
      "Do not copy Human ToonyTinyPeople rigs. Wire Faceless meshes; Animator can stay placeholder until FACELESS-006.",
    ]),
  },
  {
    key: "FACELESS-004",
    title: "[FACELESS-004] GDD entity RACE_FACELESS (Древние)",
    categoryId: ids.categories.Design,
    description: body([
      "## Суть",
      "Записать расу в `GameDesign/Races.md`: `RACE_FACELESS`, display Древние / Faceless, fantasy_hook. Kit passives/magic/tower — заглушки или TBD до FACELESS-008.",
      "",
      "## Правила/Ограничения",
      "- Имена контента EN, описания RU.",
      "- Не включать расу в playtest pick.",
      "",
      "## Acceptance criteria",
      "- [ ] Entity-блок `RACE_FACELESS` в Races.md",
      "- [ ] Слоты TBD в roster обновлены (раса #2 = Faceless)",
      "- [ ] `GameIds.Races` готов к id (можно в FACELESS-005)",
      "",
      "## Agent context (EN)",
      "Display RU Древние, assets Faceless, id RACE_FACELESS. Keep mvp/playable gate on Humans.",
    ]),
  },
  {
    key: "FACELESS-005",
    title: "[FACELESS-005] ScriptableObjects и каталоги Faceless (раса играбельна)",
    categoryId: ids.categories.Programming,
    dependsOn: ["FACELESS-003", "FACELESS-004"],
    description: body([
      "## Суть",
      "SO `UNIT_FACELESS_*`, `HERO_FACELESS_*`, `RACE_FACELESS`, запись в RaceCatalog / UnitVisualCatalog. Раса не selectable.",
      "",
      "## Правила/Ограничения",
      "- `RacePickRules.SelectableRaceIds` остаётся только `RACE_HUMAN`.",
      "- Ability id уникальны глобально (`UnitAbilityCatalog`).",
      "",
      "## Acceptance criteria",
      "- [ ] Каталоги содержат Faceless по `raceId`",
      "- [ ] Race pick UI по-прежнему только Люди",
      "- [ ] EditMode тесты зелёные",
      "",
      "## Agent context (EN)",
      "PlayableRaceIds may list Faceless for tests later; SelectableRaceIds must stay Humans-only until GATE + kit.",
    ]),
  },
  {
    key: "FACELESS-006",
    title: "[FACELESS-006] Анимации и оптимизация мешей Faceless",
    categoryId: ids.categories.Programming,
    dependsOn: ["FACELESS-001"],
    description: body([
      "## Суть",
      "Достать из MDX клипы Stand/Walk/Attack/Death/Cast (или ретаргет), оптимизировать меши. Сейчас review — статика.",
      "",
      "## Правила/Ограничения",
      "- Контроллеры рядом с префабом, как `TtUnitVisualSetup`.",
      "- ApplyRootMotion off. Тайминги удара — `AbilityAnimRules` / swing impact.",
      "",
      "## Acceptance criteria",
      "- [ ] Idle/Walk/Attack/Death на боевых префабах",
      "- [ ] Меши в разумном polycount для RTS-камеры",
      "",
      "## Agent context (EN)",
      "MDX v800 sequences exist in the files even if Unity cannot see them yet. Prefer extracting WC3 clips over retargeting TT_RTS.",
    ]),
  },
  {
    key: "FACELESS-007",
    title: "[FACELESS-007] Здания Faceless (нет исходников)",
    categoryId: ids.categories.Design,
    description: body([
      "## Суть",
      "В паке нет TownHall/Barracks/Tower. `BuildingVisualCatalog` сейчас не per-race — все базы выглядят как люди. Решить: новые модели, временно люди, или каталог по `raceId`.",
      "",
      "## Правила/Ограничения",
      "- Папку `Prefabs/Races/Faceless/Buildings/` не создавать пустой.",
      "",
      "## Acceptance criteria",
      "- [ ] Решение записано в GDD/wiki",
      "- [ ] Если свои здания — 3 префаба + race-keyed каталог",
      "",
      "## Agent context (EN)",
      "`BuildingVisualCatalog` has a single Main/Tower/Barracks. Humans builder: `TtBuildingVisualSetup`.",
    ]),
  },
  {
    key: "FACELESS-008",
    title: "[FACELESS-008] Асимметрия Древних: passives, magic, tower, уники",
    categoryId: ids.categories.Design,
    description: body([
      "## Суть",
      "Полный kit расы #2: 2+/1− passives, 3 caster spells, 9 tower tracks, 2 race-unique бонуса. Игровой unlock после GATE.",
      "",
      "## Правила/Ограничения",
      "- Не копировать Human kit 1:1.",
      "- Экономика tower/magic как в Races.md (общая).",
      "- Selectable только после GATE + готовности визуала и кита.",
      "",
      "## Acceptance criteria",
      "- [ ] Passives / spells / 9 tracks / 2 uniques записаны в GDD",
      "- [ ] Реализация отдельными follow-up карточками после GATE",
      "",
      "## Agent context (EN)",
      "Pipeline in GameDesign/Races.md «Контент-пайплайн новой расы». Do not add to SelectableRaceIds in this card.",
    ]),
  },
  {
    key: "FACELESS-010",
    title: "[FACELESS-010] Бонусы Древних: дизайн юнит за юнитом",
    categoryId: ids.categories.Design,
    dependsOn: ["FACELESS-009"],
    description: body([
      "## Суть",
      "Совместная дизайн-сессия: по каждому бонусному юниту Faceless (Melee/Ranged/Caster/Siege/Flying/Super, 3 героя, Titan) придумать и зафиксировать бонус, логичный для расы Древних и для этого юнита. Задача создана по явной просьбе пользователя — делать ПОСЛЕ завершения FACELESS-009.",
      "",
      "## Правила/Ограничения",
      "- Агент спрашивает пользователя по одному юниту и предлагает варианты, не угадывает.",
      "- Пока задача в работе — в `HumanBonusUnitRules` сохраняется нейтральная заглушка: Faceless без бонус-кита (`NoBonusKitRaceIds`) и без veteran-множителей humans.",
      "- Реализация бонусов — отдельные follow-up карточки после утверждения дизайна.",
      "",
      "## Acceptance criteria",
      "- [ ] Таблица юнит → бонус утверждена пользователем (все роли + 3 героя + Titan)",
      "- [ ] Записано в GDD (`GameDesign/Bonuses.md` или `GameDesign/Races.md`) и/или `wiki/rules/human-unit-bonuses.md`",
      "- [ ] Расовый бонус-деф в игре применяет свои множители, а не Human",
      "",
      "## Agent context (EN)",
      "Design session with the user, 1 unit at a time. Do not enable selection before visual+kit ready. Existing gate: HasBonusKit(raceId) returns false for RACE_FACELESS.",
    ]),
  },
];

async function main() {
  const client = HacknPlanClient.fromEnv();
  const existing = await client.listAllWorkItems();
  const byTitle = new Map(existing.map((w) => [w.title, w]));
  const created = {};

  for (const spec of ITEMS) {
    const hit = byTitle.get(spec.title);
    if (hit) {
      created[spec.key] = hit;
      console.log("skip", spec.key, "→", hit.workItemId);
      continue;
    }
    const createdItem = await client.createWorkItem({
      title: spec.title,
      description: spec.description,
      isStory: false,
      estimatedCost: 0,
      importanceLevelId: ids.importance.Urgent,
      categoryId: spec.categoryId,
      boardId: ids.boardNowId,
      designElementId: ids.gdm.Races,
      milestoneId: ids.milestones["Early Access"],
    });
    created[spec.key] = createdItem;
    byTitle.set(spec.title, createdItem);
    console.log("created", spec.key, "→", createdItem.workItemId, client.webItemUrl(createdItem.workItemId));
  }

  for (const spec of ITEMS) {
    const deps = spec.dependsOn || [];
    const from = created[spec.key];
    if (!from) continue;
    for (const key of deps) {
      const to = created[key];
      if (!to) {
        console.log("dep missing", spec.key, "→", key);
        continue;
      }
      try {
        await client.addDependency(from.workItemId, to.workItemId);
        console.log("dep", spec.key, "blocked by", key);
      } catch (e) {
        console.log("dep fail", spec.key, key, e.status || "", String(e.message || e).slice(0, 160));
      }
    }
  }
}

main().catch((e) => {
  console.error(e && e.stack ? e.stack : e);
  process.exit(1);
});
