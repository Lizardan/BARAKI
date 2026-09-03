"use strict";

const fs = require("fs");
const path = require("path");
const { HacknPlanClient } = require("./client");

function summarize(obj) {
  if (Array.isArray(obj)) {
    return obj.map((x) => {
      if (!x || typeof x !== "object") return x;
      return {
        id: x.categoryId || x.stageId || x.milestoneId || x.boardId || x.tagId
          || x.importanceLevelId || x.designElementTypeId || x.designElementId || x.workItemId,
        name: x.name || x.title,
        status: x.status || x.stage && x.stage.name,
        extra: x.isDefault || x.isStory || x.isBlocked || undefined,
      };
    });
  }
  return obj;
}

async function main() {
  const client = HacknPlanClient.fromEnv();
  const projectId = client.projectId;
  const project = await client.get(`/projects/${projectId}`);
  const [
    boards, categories, stages, milestones, tags, importance, types, gdm,
  ] = await Promise.all([
    client.get(`/projects/${projectId}/boards`),
    client.get(`/projects/${projectId}/categories`),
    client.get(`/projects/${projectId}/stages`),
    client.get(`/projects/${projectId}/milestones`),
    client.get(`/projects/${projectId}/tags`),
    client.get(`/projects/${projectId}/importancelevels`),
    client.get(`/projects/${projectId}/designelementtypes`),
    client.get(`/projects/${projectId}/designelements`),
  ]);
  const workItems = await client.listAllWorkItems();
  const dump = {
    project: {
      id: project.id || project.projectId,
      name: project.name,
      defaultBoardId: project.defaultBoardId,
      moduleConfig: project.moduleConfig,
    },
    boards: summarize(boards),
    categories: summarize(categories),
    stages,
    milestones: summarize(milestones),
    tags: summarize(tags),
    importance,
    designElementTypes: types,
    designElementsCount: Array.isArray(gdm) ? gdm.length : (gdm && gdm.length) || 0,
    designElements: Array.isArray(gdm) ? gdm.map((e) => ({
      id: e.designElementId, name: e.name, type: e.type && e.type.name, parent: e.parent && e.parent.designElementId,
    })) : gdm,
    workItemsCount: workItems.length,
    workItems: workItems.map((w) => ({
      id: w.workItemId,
      title: w.title,
      isStory: w.isStory,
      stage: w.stage && w.stage.name,
      stageStatus: w.stage && w.stage.status,
      category: w.category && w.category.name,
      board: w.board && w.board.name,
      importance: w.importanceLevel && w.importanceLevel.name,
      blocked: w.isBlocked,
    })),
  };
  const outDir = path.join(__dirname, "dump");
  fs.mkdirSync(outDir, { recursive: true });
  fs.writeFileSync(path.join(outDir, "summary.json"), JSON.stringify(dump, null, 2), "utf8");
  fs.writeFileSync(path.join(outDir, "raw-project.json"), JSON.stringify({
    project, boards, categories, stages, milestones, tags, importance, types,
  }, null, 2), "utf8");
  console.log(JSON.stringify({
    name: dump.project.name,
    boards: dump.boards.length,
    categories: dump.categories,
    stages: (stages || []).map((s) => ({ id: s.stageId, name: s.name, status: s.status })),
    milestones: dump.milestones,
    tags: dump.tags,
    importance: (importance || []).map((i) => ({ id: i.importanceLevelId, name: i.name, isDefault: i.isDefault })),
    types: Array.isArray(types) ? types.map((t) => ({ id: t.designElementTypeId, name: t.name })) : types,
    designElements: dump.designElementsCount,
    workItems: dump.workItemsCount,
  }, null, 2));
}

main().catch((e) => {
  console.error(e && e.stack ? e.stack : e);
  process.exit(1);
});
