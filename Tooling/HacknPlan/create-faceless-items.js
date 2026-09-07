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
  {
    key: "FACELESS-011",
    title: "[FACELESS-011] Юнитовые бонусы Древних: реализация слотов 1–6",
    categoryId: ids.categories.Programming,
    description: body([
      "## Суть",
      "Реализовать юнитовые бонусы 1–6 по канону `wiki/rules/faceless-unit-bonuses.md`:",
      "- 1 Melee **Hunger of the Old One**: on-hit 15% — лечение = 50% урона удара (вампиризм, после брони цели).",
      "- 2 Ranged **Tainting Bolt**: on-hit 15% — дот 3 dmg/с × 3 с.",
      "- 3 Caster **Call of the Abyss**: on-kill (добивание кастером) 100% — спавн 1 мини-меле (роль Melee, статы ×0.5 от UNIT_FACELESS_MELEE, масштаб префаба ×0.67).",
      "- 4 Siege **Death Explosion**: при смерти — урон 10% max HP вражеским юнитам в радиусе 3 (здания не задевает).",
      "- 5 Flying **Hungering Flight**: on-kill +15% AS на 3 с, стаки до 3.",
      "- 6 Super **Feast on the Fallen**: on-kill +80 HP и +10% AS на 3 с, стаки до 3.",
      "",
      "## Правила/Ограничения",
      "- Канон: `wiki/rules/faceless-unit-bonuses.md`; формат def/каста — `wiki/rules/abilities.md`.",
      "- Race-scoped: бонусы применяются только к Faceless; Human kit/пассянды не менять.",
      "- Спавн/on-hit — по replacement policy: только к будущим спавнам после пика.",
      "- Гейт бонус-пика не снимать (FACELESS-014). Ветераны-сигнатуры — FACELESS-012.",
      "- Асинхронность в Gameplay: UniTask/UniTaskVoid (не Task/async void).",
      "",
      "## Acceptance criteria",
      "- [ ] Бонусы 1–6 зарегистрированы (defs) и работают в бою",
      "- [ ] Тесты EditMode/PlayMode на триггер и числа каждого бонуса",
      "- [ ] Human-бонусы не изменились",
      "- [ ] Канон wiki обновлён (mark реализовано)",
      "",
      "## Agent context (EN)",
      "Implement per faceless-unit-bonuses.md slots 1–6. Reuse the existing Human bonus framework (HumanBonusUnitRules, ability system) where sensible, but keep multipliers race-scoped. On-hit/on-kill hooks must not fire for non-Faceless. Do not enable bonus-pick UI.",
    ]),
  },
  {
    key: "FACELESS-012",
    title: "[FACELESS-012] Ветераны-герои (7–9) и титан (10) Древних",
    categoryId: ids.categories.Programming,
    dependsOn: ["FACELESS-011"],
    description: body([
      "## Суть",
      "Слоты 7–10 по канону: кит ветеранов статы ×1.4 HP / ×1.35 dmg / +2 брони, morale +15% (тот же стат, что у базового героя), заменяется одна сигнатура:",
      "- 7 Hero1 Король **Ancient Mantle**: ульта-замена — сам герой +50% dmg и +2 брони на 8 с, AoE-удар вокруг +30%.",
      "- 8 Hero2 Колдун **Area of Miss**: каст на область — враги в радиусе 5 на 4 с при атаках промахиваются (100%, урон не наносится).",
      "- 9 Hero3 Берсерк **Feast Zone**: зона 10 с у героя; союзники внутри лечатся на 30% от нанесённого ими урона.",
      "- 10 Titan **Aura of Hunger**: замена Colossus — пока титан жив, вся армия владельца лечится на 15% от нанесённого урона.",
      "Префабы ветеранов + портреты (как Human BonusHeroes PRE-006b).",
      "",
      "## Правила/Ограничения",
      "- Формат — Human PRE-006b (`wiki/rules/human-unit-bonuses.md`); числа и сигнатуры — свои (Faceless).",
      "- Титан: статы ×1.4/×1.35/+2 поверх сида 3× героя 1; цена 2500g не меняется.",
      "- Гейт бонус-пика не снимать до FACELESS-014.",
      "",
      "## Acceptance criteria",
      "- [ ] Префабы ветеранов (3 героя + титан) в `Prefabs/Races/Faceless/BonusHeroes/`, портреты готовы",
      "- [ ] Сигнатуры 4 реализованы и зарегистрированы",
      "- [ ] Множители статов и morale применяются от базового героя",
      "- [ ] EditMode тесты зелёные",
      "",
      "## Agent context (EN)",
      "Follow the Human veteran pattern (ApplyVeteranMultipliers + one signature replacement) but with Faceless identities/numbers, NOT Human signatures. Titan base = hero1 × 3 (TitanRules.BaseStatMultiplier) plus the ×1.4/×1.35/+2 kit. No bonus-pick gating in this card.",
    ]),
  },
  {
    key: "FACELESS-013",
    title: "[FACELESS-013] Уники Древних: Shadow of the Void и Void Bastion",
    categoryId: ids.categories.Programming,
    description: body([
      "## Суть",
      "Расовые уники 11–12 (player-level модификаторы):",
      "- 11 **Shadow of the Void**: все войска владельца — 8% шанс полностью избежать атаки (применяется к будущим спавнам).",
      "- 12 **Void Bastion**: все здания владельца — атаки по ним промахиваются на 20% (ретро ко всем существующим зданиям при пике и к новым).",
      "",
      "## Правила/Ограничения",
      "- Асимметрия к March Discipline / Stone Masonry Людей; Human-уники не менять.",
      "- Формат уников как PRE-006b: UI список — текст + tooltip (портретов нет).",
      "",
      "## Acceptance criteria",
      "- [ ] Оба уника реализованы и работают (ретро + новые юниты/здания)",
      "- [ ] UI бонус-оверлея показывает 2 уника",
      "- [ ] EditMode тесты зелёные",
      "",
      "## Agent context (EN)",
      "Reuse the hit/miss roll from Area of Miss where reasonable; buildings roll only against direct attacks. Shadow of the Void applies to future spawns only (replacement policy); Void Bastion applies retro to existing buildings.",
    ]),
  },
  {
    key: "FACELESS-014",
    title: "[FACELESS-014] Снятие гейта бонус-пика Древних",
    categoryId: ids.categories.Programming,
    dependsOn: ["FACELESS-011", "FACELESS-012", "FACELESS-013"],
    description: body([
      "## Суть",
      "Снять runtime-гейт Фазы 1 после готовности визуала и китов:",
      "- Убрать `RACE_FACELESS` из `NoBonusKitRaceIds` (`HumanBonusUnitRules`).",
      "- `HasBonusKit(RACE_FACELESS)` = true; `EffectiveBonusSlotForRole/Hero/Titan` для Faceless возвращают 1–6 / 7–9 / 10.",
      "- Включить Faceless в презентацию бонус-пика и ветеранов.",
      "",
      "## Правила/Ограничения",
      "- Assьure: Faceless применяет свои множители, а не Human (AC из FACELESS-010).",
      "- `SelectableRaceIds` по-прежнему только `RACE_HUMAN` (до GATE).",
      "- Включение только когда визуал и киты готовы (FACELESS-011..013 + FACELESS-006).",
      "",
      "## Acceptance criteria",
      "- [ ] `HasBonusKit(RACE_FACELESS)` = true; слоты видны в UI оверлея",
      "- [ ] Human пик и ветераны не тронуты",
      "- [ ] EditMode + PlayMode тесты зелёные; числовой трансфер подтверждён тестом",
      "",
      "## Agent context (EN)",
      "Flip the gate after visual+kit ready. Verify both races pick independently; Human bonus/veteran flow unchanged. Tests: per-race bonus-slot resolution, Faceless multiplier transfer, no regression for Human kit.",
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

  const eight = existing.find((w) => String(w.title || "").startsWith("[FACELESS-008]"));
  if (eight) {
    const text = [
      "## Agent summary",
      "Scope update (2026-09-08, FACELESS-010): бонусные уники 11–12 вынесены из этой карточки —",
      "дизайн в FACELESS-010 (канон wiki/rules/faceless-unit-bonuses.md), реализация в FACELESS-013.",
      "Здесь остаются: passives 2+/1−, 3 caster spells, 9 tower tracks, полный kit asymmetry.",
    ].join("\n");
    try {
      await client.addComment(eight.workItemId, text);
      console.log("comment → FACELESS-008");
    } catch (e) {
      console.log("comment fail FACELESS-008", e.status || "", String(e.message || e).slice(0, 160));
    }
  }
}

main().catch((e) => {
  console.error(e && e.stack ? e.stack : e);
  process.exit(1);
});
