"use strict";

// Follow-up карточки реализации asymmetry kit Древних (FACELESS-008, дизайн).
// Дизайн — GameDesign/Races.md § Древние. Сами карточки — только реализация после GATE.

const { HacknPlanClient } = require("./client.js");

const IDS = {
  boardNow: 685920,
  importance: { Urgent: 1, High: 2, Normal: 3, Low: 4 },
  category: { Programming: 1, Design: 3 },
  milestone: { EarlyAccess: 4 },
  gdm: { Races: 20, Abilities: 26 },
};

const ITEMS = [
  {
    key: "FACELESS-015",
    title: "[FACELESS-015] Стартовые пассивы Древних: вампиризм, AS, хрупкие здания",
    description: [
      "## Суть",
      "Реализовать стартовые пассивы Древних (2+/1−) по канону `GameDesign/Races.md` § «Стартовые пассивы — Древние»:",
      "- `PASSIVE_FACELESS_ABYSSAL_HUNGER` — вампиризм 10% от нанесённого урона, только **юниты** (не герои/титан/здания/башни).",
      "- `PASSIVE_FACELESS_RELENTLESS_TIDE` — +10% скорости атаки, только **юниты**.",
      "- `PASSIVE_FACELESS_BLEAK_FOUNDATIONS` — −10% max HP всем **зданиям** (ретро + новые, синк по уровням).",
      "",
      "## Правила/Ограничения",
      "- Пассивы применяются **только** `RACE_FACELESS`; Human-пассивы не меняются.",
      "- Вампиризм считается от реального урона после брони цели (как бонус «Hunger of the Old One», слот 1).",
      "- Множитель HP зданий живёт рядом с `Stone Masonry` (PRE-006b) и учитывается при апгрейде/синке уровней.",
      "- **Не включать** `RACE_FACELESS` в `SelectableRaceIds`, не снимать гейты бонус-кита — это FACELESS-014.",
      "- Заблокировано до **GATE-001** (playtest) и **GATE-002** (чекап).",
      "",
      "## Acceptance criteria",
      "- [ ] `PassiveDefinition` ×3 для Faceless, применение на старте матча",
      "- [ ] Вампиризм 10% работает на юнитах и не трогает героев/титана/здания",
      "- [ ] −10% max HP зданий применяется к существующим и новым зданиям, переживает апгрейд",
      "- [ ] Race pick tooltip показывает +2/−1 для Древних",
      "- [ ] EditMode тесты green",
      "",
      "## Agent context (EN)",
      "Design canon: `GameDesign/Races.md` (§ Start passives — Древние), `wiki/rules/faceless-unit-bonuses.md`.",
      "Precedent for lifesteal: Human bonus slot 1 `Hunger of the Old One` (post-armor damage).",
      "Precedent for retro building HP scaling: Human unique `Stone Masonry` (PRE-006b).",
      "Passives must be race-scoped; do not touch `SelectableRaceIds` or `NoBonusKitRaceIds` in this card.",
    ].join("\n"),
    categoryId: IDS.category.Programming,
    importanceLevelId: IDS.importance.High,
    designElementId: IDS.gdm.Races,
  },
  {
    key: "FACELESS-016",
    title: "[FACELESS-016] Кастер-кит Древних: 3 заклинания (Blighting Gaze / Void Drain / Raise the Drowned)",
    description: [
      "## Суть",
      "Реализовать кастер-кит Древних по канону `GameDesign/Races.md` § «Magic — Древние»:",
      "- `SPELL_FACELESS_1` Гниющий взор (Blighting Gaze) — одиночная цель: 30 dmg + дот 4 dmg/с × 4 с; range 6, CD 10s; приоритет = враг с.max HP в радиусе.",
      "- `SPELL_FACELESS_2` Вытягивание жизни (Void Drain) — AoE r5: 40 dmg, кастер лечится на 30% нанесённого; range 6, CD 14s; приоритет = плотный кластер.",
      "- `SPELL_FACELESS_3` Поднять павшего (Raise the Drowned) — труп **любой** стороны age ≤15 s → мини-меле под контролем владельца (статы ×0.5 от `UNIT_FACELESS_MELEE`, масштаб ×0.67); range 6, CD 30s.",
      "",
      "## Правила/Ограничения",
      "- Открытие — `UPG_MAIN_MAGIC` slot 1/2/3 (main level 1/2/3); +3 dmg автоатаке кастера за слот, экономика общая (500/750/1000g, 60/90/135s).",
      "- Кит **только** для `RACE_FACELESS`; Human SPELL_* не меняются. Снять пустой кастер-кит Фазы 1 (гейт FACELESS-009) — в этой карточке.",
      "- Мини-меле — те же статы, что у бонуса `Call of the Abyss` (слот 3): HP 60, dmg 4–5, броня 0.",
      "- Заблокировано до **GATE-001** и **GATE-002**.",
      "",
      "## Acceptance criteria",
      "- [ ] 3 `AbilityDefinition` + авто-каст `UNIT_FACELESS_CASTER` (как у Human, см. `AI.md`)",
      "- [ ] Дот и AoE-дрейн работают в host-симе, эффекты видны клиенту",
      "- [ ] Raise the Drowned поднимает и вражеские трупы, мини-меле на стороне владельца кастера",
      "- [ ] VFX/лейблы каста по образцу Human (SpellCasts в снапшоте)",
      "- [ ] EditMode тесты green",
      "",
      "## Agent context (EN)",
      "Design canon: `GameDesign/Races.md` § Magic — Древние. Human reference: `SPELL_HUMAN_1..3` (heal / frost nova / resurrect) and `wiki/ARCHITECTURE.md`.",
      "Empty Faceless caster kit gate (Phase 1) lives in the race-aware `CreateForSpawn(raceId, ...)` path — remove for `RACE_FACELESS` here.",
      "Corpse handling: reuse Human resurrect corpse window plumbing; Faceless accepts corpses of any side.",
    ].join("\n"),
    categoryId: IDS.category.Programming,
    importanceLevelId: IDS.importance.High,
    designElementId: IDS.gdm.Abilities,
  },
  {
    key: "FACELESS-017",
    title: "[FACELESS-017] Tower-треки Древних ×9 (L1–L3)",
    description: [
      "## Суть",
      "Реализовать 9 tower-треков Древних по канону `GameDesign/Races.md` § «Tower upgrades — Древние»:",
      "1. `UPG_TOWER_FACELESS_RAVENOUS_STRIKES` — Melee+Super: вампиризм +5/10/15%",
      "2. `UPG_TOWER_FACELESS_WITHERING_BOLTS` — Ranged+Flying: on-hit дот 2/4/6 dmg/с × 3 с",
      "3. `UPG_TOWER_FACELESS_FESTERING_DEATH` — Melee+Flying: при смерти дот 3/5/7 dmg/с × 3 с, r=3",
      "4. `UPG_TOWER_FACELESS_ABYSSAL_CALL` — Caster: при добивании кастером шанс 25/50/75% спавн мини-меле",
      "5. `UPG_TOWER_FACELESS_VOID_SHROUD` — Ranged+Caster: промах по юниту 5/8/12%",
      "6. `UPG_TOWER_FACELESS_FRENZY_OF_THE_DEEP` — Melee+Siege: +10/15/20% AS",
      "7. `UPG_TOWER_FACELESS_MADDENING_GROWTH` — Caster+Super: при убийстве +6/12/18% урона на 4 с (стаки до 3)",
      "8. `UPG_TOWER_FACELESS_HOLLOW_BONES` — Siege+Flying: +8/16/24% скорости движения",
      "9. `UPG_TOWER_FACELESS_DEVOUR` — все юниты: при убийстве лечение 5/10/15% max HP",
      "",
      "## Правила/Ограничения",
      "- Эффекты — **только юниты** (не герои, не титан, не DPS башен). Экономика общая: 500/800/1200g, 45/90/135s; L2 после L1, L3 после L2; 4 башни, очередь 1 на башню.",
      "- Треки Faceless идут **своей таблицей правил рядом** (`TowerTrackRules` не расширять индексами — см. `wiki/rules/tower-tracks.md`), id `UPG_TOWER_FACELESS_*`.",
      "- Треки стакаются с бонусами слотов 1–12 (FACELESS-010..014) — разные системы.",
      "- Заблокировано до **GATE-001** и **GATE-002**.",
      "",
      "## Acceptance criteria",
      "- [ ] 9 треков в research UI башни на слотах 4–12 для `RACE_FACELESS`",
      "- [ ] L1–L3, гейты очереди/уровней как у Human",
      "- [ ] Все 9 эффектов работают в host-симе, фильтр Hero/Titan соблюдён",
      "- [ ] Уровни треков едут в Players-секции снапшота (паттерн v22, 9 байт)",
      "- [ ] EditMode тесты green (`TowerTrack*Tests` + новые кейсы)",
      "",
      "## Agent context (EN)",
      "Design canon: `GameDesign/Races.md` § Tower upgrades — Древние. Implementation precedent: `wiki/rules/tower-tracks.md` (TowerTrackRules, MatchEconomyRules, TowerTrackUnitRules, MatchCombatSystem, MatchSnapshot v22).",
      "Do NOT extend `TowerTrackRules.TrackIds`/`RoleMatches` index arrays — add a race-specific rules table next to it (precedent `HumanBonusUnitRules`).",
      "Combat hooks needed: lifesteal, damage-over-time, on-death AoE dot, summon-on-kill, miss chance, attack speed buff, damage buff on kill, move speed, heal on kill.",
    ].join("\n"),
    categoryId: IDS.category.Programming,
    importanceLevelId: IDS.importance.High,
    designElementId: IDS.gdm.Races,
  },
];

(async () => {
  const c = HacknPlanClient.fromEnv();
  for (const spec of ITEMS) {
    const created = await c.createWorkItem({
      title: spec.title,
      description: spec.description,
      isStory: false,
      estimatedCost: 0,
      importanceLevelId: spec.importanceLevelId,
      categoryId: spec.categoryId,
      boardId: IDS.boardNow,
      designElementId: spec.designElementId,
      milestoneId: IDS.milestone.EarlyAccess,
    });
    console.log("created", spec.key, "→", created.workItemId, c.webItemUrl(created.workItemId));
  }
})().catch((e) => {
  console.error("FAIL:", e.message);
  process.exit(1);
});
