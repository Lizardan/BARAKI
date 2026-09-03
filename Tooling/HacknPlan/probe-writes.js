"use strict";

const fs = require("fs");
const path = require("path");
const { HacknPlanClient } = require("./client");

function mapBy(list, keyName, idName) {
  const o = {};
  for (const x of list || []) {
    const k = x[keyName];
    if (k) o[k] = x[idName];
  }
  return o;
}

async function tryWrite(label, fn) {
  try {
    const r = await fn();
    console.log("OK", label);
    return r;
  } catch (e) {
    console.log("FAIL", label, e.status || "", String(e.message || e).slice(0, 180));
    return null;
  }
}

async function main() {
  const client = HacknPlanClient.fromEnv();
  const [categories, tags, types, milestones, boards, stages, importance, gdm] = await Promise.all([
    client.getCategories(),
    client.getTags(),
    client.getDesignElementTypes(),
    client.getMilestones(),
    client.getBoards(),
    client.getStages(),
    client.getImportanceLevels(),
    client.getDesignElements(),
  ]);

  const board = (boards || [])[0];
  const boardId = board && board.boardId;

  await tryWrite("tag github", () => client.createTag({ name: "github", color: "#6e5494", displayIconOnly: false, icon: "tag" }));
  await tryWrite("tag deferred", () => client.createTag({ name: "deferred", color: "#c5def5", displayIconOnly: false, icon: "tag" }));
  await tryWrite("tag needs-triage", () => client.createTag({ name: "needs-triage", color: "#fbca04", displayIconOnly: false, icon: "tag" }));
  await tryWrite("tag bug", () => client.createTag({ name: "bug", color: "#d73a4a", displayIconOnly: false, icon: "tag" }));

  await tryWrite("type Race", () => client.createDesignElementType({ name: "Race" }));
  await tryWrite("milestone PATCH", () => client.patch(client.p("/milestones/3"), { name: "GATE" }));
  await tryWrite("board PATCH Now", () => client.patch(client.p(`/boards/${boardId}`), {
    name: "Now",
    milestoneId: 3,
    description: "Current BARAKI work window",
  }));
  await tryWrite("design element", () => client.createDesignElement({
    name: "_probe_delete_me",
    designElementTypeId: 9,
    parentId: 6,
    description: "probe",
  }));
  await tryWrite("work item", () => client.createWorkItem({
    title: "_probe_delete_me",
    description: "probe",
    isStory: false,
    estimatedCost: 0,
    importanceLevelId: 3,
    categoryId: 1,
    boardId,
  }));

  const tags2 = await client.getTags();
  const types2 = await client.getDesignElementTypes();
  const gdm2 = await client.getDesignElements();
  const items = await client.listAllWorkItems();
  const probes = items.filter((w) => w.title === "_probe_delete_me");
  for (const w of probes) {
    await tryWrite("delete probe item " + w.workItemId, () => client.del(client.p(`/workitems/${w.workItemId}`)));
  }
  const flat = client.flattenDesignElements(gdm2);
  const probeEl = flat.find((e) => e.name === "_probe_delete_me");
  if (probeEl) {
    await tryWrite("delete probe element", () => client.del(client.p(`/designelements/${probeEl.designElementId}`)));
  }

  const ids = {
    projectId: client.projectId,
    boardNowId: boardId,
    stages: mapBy(stages, "name", "stageId"),
    importance: mapBy(importance, "name", "importanceLevelId"),
    categories: mapBy(categories, "name", "categoryId"),
    tags: mapBy(tags2, "name", "tagId"),
    types: mapBy(types2, "name", "designElementTypeId"),
    milestones: mapBy(await client.getMilestones(), "name", "milestoneId"),
    gdmRoots: Object.fromEntries(client.flattenDesignElements(gdm2).filter((e) => !e.parent || !e.parent.designElementId).map((e) => [e.name, e.designElementId])),
    writes: {
      tags: tags2.length,
      types: types2.length,
    },
  };
  const out = path.join(__dirname, "ids.json");
  fs.writeFileSync(out, JSON.stringify(ids, null, 2), "utf8");
  console.log("wrote", out);
  console.log(JSON.stringify(ids, null, 2));
}

main().catch((e) => {
  console.error(e && e.stack ? e.stack : e);
  process.exit(1);
});
