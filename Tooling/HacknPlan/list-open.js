"use strict";

// Печать открытых work items HacknPlan (stage != Completed), сгруппированных
// так же, как панель UnioTasks. Read-only: ничего не меняет.
//
//   node Tooling/HacknPlan/list-open.js            — сводка по группам
//   node Tooling/HacknPlan/list-open.js --all      — все, включая Completed
//   node Tooling/HacknPlan/list-open.js --json     — машиночитаемый вывод

const { HacknPlanClient } = require("./client.js");

const args = new Set(process.argv.slice(2));
const wantAll = args.has("--all");
const wantJson = args.has("--json");

function nameOf(list, id, key) {
  const hit = (list || []).find((x) => x && (x[key] === id || x.id === id));
  return hit ? (hit.name || hit.title || String(id)) : String(id);
}

(async () => {
  const c = HacknPlanClient.fromEnv();

  const [stages, cats, imp, boards, tags, project] = await Promise.all([
    c.getStages().catch(() => []),
    c.getCategories().catch(() => []),
    c.getImportanceLevels().catch(() => []),
    c.getBoards().catch(() => []),
    c.getTags().catch(() => []),
    c.getProject().catch(() => null),
  ]);

  const items = await c.listAllWorkItems();

  const doneStageIds = new Set(
    (stages || [])
      .filter((s) => /complet|done|closed/i.test(s.name || ""))
      .map((s) => s.stageId != null ? s.stageId : s.id)
  );

  // ВАЖНО: API v0 отдаёт stage / importanceLevel / category / board вложенными
  // объектами, а не плоскими id. Плоских полей (stageId, categoryId) в ответе нет.
  const rows = items.map((it) => {
    const id = it.workItemId != null ? it.workItemId : it.id;
    const stage = it.stage || {};
    const level = it.importanceLevel || {};
    const cat = it.category || {};
    const board = it.board || {};
    return {
      id,
      title: it.title || "(без заголовка)",
      stageId: stage.stageId,
      stage: stage.name || nameOf(stages, stage.stageId, "stageId"),
      importance: level.name || nameOf(imp, level.importanceLevelId, "importanceLevelId"),
      category: cat.name || nameOf(cats, cat.categoryId, "categoryId"),
      board: board.name || nameOf(boards, board.boardId, "boardId"),
      isBlocked: !!(it.isBlocked || it.blocked),
      isStory: !!it.isStory,
      tags: (it.tags || []).map((t) => t.name || nameOf(tags, t.tagId, "tagId")),
      url: c.webItemUrl(id),
    };
  });

  const open = wantAll ? rows : rows.filter((r) => !doneStageIds.has(r.stageId));
  const deferred = (r) => r.tags.some((t) => /deferred/i.test(t || ""));

  const groups = [
    ["Выполняется (In progress)", open.filter((r) => /progress/i.test(r.stage) && !deferred(r))],
    ["Ждут апрува (Testing)", open.filter((r) => /test/i.test(r.stage) && !deferred(r))],
    ["Горит (Urgent)", open.filter((r) => /urgent/i.test(r.importance) && !deferred(r) && !/progress|test/i.test(r.stage))],
    ["Высокий (High)", open.filter((r) => /^high/i.test(r.importance) && !deferred(r) && !/progress|test|urgent/i.test(r.stage + r.importance))],
    ["Очередь (Planned/Normal)", open.filter((r) => /planned/i.test(r.stage) && !deferred(r) && !/urgent|high|low/i.test(r.importance))],
    ["Низкий (Low)", open.filter((r) => /^low/i.test(r.importance) && !deferred(r))],
    ["Заблокировано", open.filter((r) => r.isBlocked && !deferred(r))],
    ["Отложено (deferred)", open.filter(deferred)],
  ];

  if (wantJson) {
    process.stdout.write(JSON.stringify({ project: project && (project.name || null), total: rows.length, open: open.length, rows: open }, null, 2) + "\n");
    return;
  }

  console.log(`Проект: ${(project && project.name) || "?"} (${c.projectId})`);
  console.log(`Всего карточек: ${rows.length}, открытых: ${open.length}\n`);

  const shown = new Set();
  for (const [label, list] of groups) {
    if (!list.length) continue;
    console.log(`## ${label} — ${list.length}`);
    for (const r of list) {
      shown.add(r.id);
      const flags = [r.isBlocked ? "BLOCKED" : null, r.isStory ? "story" : null].filter(Boolean);
      console.log(`  #${r.id} [${r.category}] ${r.title}${flags.length ? "  (" + flags.join(", ") + ")" : ""}`);
      console.log(`      ${r.url}`);
    }
    console.log("");
  }

  const rest = open.filter((r) => !shown.has(r.id));
  if (rest.length) {
    console.log(`## Прочее — ${rest.length}`);
    for (const r of rest) console.log(`  #${r.id} [${r.category}/${r.stage}/${r.importance}] ${r.title}`);
  }
})().catch((e) => {
  console.error("FAIL:", e.message);
  process.exit(1);
});
