"use strict";

const fs = require("fs");
const path = require("path");
const { HacknPlanClient } = require("./client");

function byName(list, name) {
  return (list || []).find((x) => String(x.name || "").toLowerCase() === String(name).toLowerCase());
}

async function ensureCategory(client, existing, name, color, icon) {
  const hit = byName(existing, name);
  if (hit) return hit;
  const created = await client.createCategory({ name, color, icon });
  existing.push(created);
  console.log("category +", name, created.categoryId);
  return created;
}

async function ensureTag(client, existing, name, color) {
  const hit = byName(existing, name);
  if (hit) return hit;
  const created = await client.createTag({
    name,
    color,
    displayIconOnly: false,
    icon: "tag",
  });
  existing.push(created);
  console.log("tag +", name, created.tagId);
  return created;
}

async function ensureType(client, existing, name) {
  const hit = byName(existing, name);
  if (hit) return hit;
  const created = await client.createDesignElementType({ name });
  existing.push(created);
  console.log("type +", name, created.designElementTypeId);
  return created;
}

async function patchMilestoneName(client, milestones, id, name) {
  const m = (milestones || []).find((x) => x.milestoneId === id);
  if (!m) throw new Error("milestone " + id + " missing");
  if (m.name === name) return m;
  await client.patch(client.p(`/milestones/${id}`), { name });
  m.name = name;
  console.log("milestone", id, "→", name);
  return m;
}

async function main() {
  const client = HacknPlanClient.fromEnv();
  const [categories, tags, types, milestones, boards] = await Promise.all([
    client.getCategories(),
    client.getTags(),
    client.getDesignElementTypes(),
    client.getMilestones(),
    client.getBoards(),
  ]);

  await ensureCategory(client, categories, "UI", "#9b59b6", "desktop");
  await ensureCategory(client, categories, "Production", "#16a085", "cog");

  await ensureTag(client, tags, "github", "#6e5494");
  await ensureTag(client, tags, "deferred", "#c5def5");
  await ensureTag(client, tags, "needs-triage", "#fbca04");
  await ensureTag(client, tags, "bug", "#d73a4a");

  for (const name of ["Race", "Unit", "Building", "Economy", "Platform", "Technical"]) {
    await ensureType(client, types, name);
  }

  await patchMilestoneName(client, milestones, 2, "PRE-RACE2");
  await patchMilestoneName(client, milestones, 3, "GATE");
  await patchMilestoneName(client, milestones, 4, "Early Access");

  const board = (boards || [])[0];
  if (!board) throw new Error("no board");
  const boardId = board.boardId;
  await client.patch(client.p(`/boards/${boardId}`), {
    name: "Now",
    milestoneId: 3,
    description: "Текущее окно работ BARAKI (GATE playtest).",
    generalInfo: "HacknPlan = source of truth. GitHub Issues не трогаем.",
  });
  console.log("board", boardId, "→ Now / GATE");

  const ids = {
    projectId: client.projectId,
    boardNowId: boardId,
    stages: { planned: 1, inProgress: 2, testing: 3, completed: 4 },
    importance: { urgent: 1, high: 2, normal: 3, low: 4 },
    categories: Object.fromEntries((await client.getCategories()).map((c) => [c.name, c.categoryId])),
    tags: Object.fromEntries((await client.getTags()).map((t) => [t.name, t.tagId])),
    types: Object.fromEntries((await client.getDesignElementTypes()).map((t) => [t.name, t.designElementTypeId])),
    milestones: Object.fromEntries((await client.getMilestones()).map((m) => [m.name, m.milestoneId])),
    gdmRoots: {
      Overview: 1,
      Gameplay: 2,
      Story: 3,
      Characters: 4,
      Levels: 5,
      Technical: 6,
      Business: 7,
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
