"use strict";

const fs = require("fs");
const path = require("path");
const https = require("https");

const BASE = "https://api.hacknplan.com/v0";
const DEFAULT_PROJECT_ID = 242091;

function readLocalSecrets() {
  const candidates = [
    path.join(__dirname, ".secrets.json"),
  ];
  for (const p of candidates) {
    try {
      if (fs.existsSync(p)) {
        const j = JSON.parse(fs.readFileSync(p, "utf8"));
        if (j && j.apiKey) return j;
      }
    } catch (_) { /* ignore */ }
  }
  return {};
}

function flattenDesignElements(nodes, acc) {
  const list = Array.isArray(nodes) ? nodes : [];
  for (const n of list) {
    if (!n) continue;
    acc.push(n);
    if (Array.isArray(n.children) && n.children.length) flattenDesignElements(n.children, acc);
  }
  return acc;
}

class HacknPlanClient {
  constructor(opts) {
    opts = opts || {};
    this.apiKey = String(opts.apiKey || "").trim();
    this.projectId = Number(opts.projectId || DEFAULT_PROJECT_ID);
    if (!this.apiKey) throw new Error("HacknPlan API key is empty");
    if (!this.projectId) throw new Error("HacknPlan projectId is empty");
    this._queue = Promise.resolve();
  }

  static fromEnv() {
    const secrets = readLocalSecrets();
    const apiKey = process.env.HACKNPLAN_API_KEY || secrets.apiKey || "";
    const projectId = process.env.HACKNPLAN_PROJECT_ID || secrets.projectId || DEFAULT_PROJECT_ID;
    return new HacknPlanClient({ apiKey, projectId });
  }

  request(method, urlPath, body) {
    const run = () => this._requestOnce(method, urlPath, body);
    const queued = this._queue.then(async () => {
      await new Promise((r) => setTimeout(r, 220));
      let last;
      for (let i = 0; i < 5; i++) {
        try { return await run(); }
        catch (e) {
          last = e;
          if (e && e.status === 429) {
            await new Promise((r) => setTimeout(r, 800 * (i + 1)));
            continue;
          }
          throw e;
        }
      }
      throw last;
    });
    this._queue = queued.then(() => {}, () => {});
    return queued;
  }

  _requestOnce(method, urlPath, body) {
    const payload = body === undefined ? null : JSON.stringify(body);
    return new Promise((resolve, reject) => {
      const url = new URL(BASE + urlPath);
      const req = https.request({
        protocol: url.protocol,
        hostname: url.hostname,
        path: url.pathname + url.search,
        method,
        headers: {
          Authorization: "ApiKey " + this.apiKey,
          Accept: "application/json",
          "Content-Type": "application/json",
          ...(payload ? { "Content-Length": Buffer.byteLength(payload) } : {}),
        },
      }, (res) => {
        const chunks = [];
        res.on("data", (c) => chunks.push(c));
        res.on("end", () => {
          const text = Buffer.concat(chunks).toString("utf8");
          const ok = res.statusCode >= 200 && res.statusCode < 300;
          if (!ok) {
            const err = new Error(`HacknPlan ${method} ${urlPath} → ${res.statusCode}: ${text.slice(0, 500)}`);
            err.status = res.statusCode;
            err.body = text;
            return reject(err);
          }
          if (!text) return resolve(null);
          try { resolve(JSON.parse(text)); }
          catch (e) { reject(new Error("HacknPlan JSON parse: " + e.message + " / " + text.slice(0, 200))); }
        });
      });
      req.on("error", reject);
      if (payload) req.write(payload);
      req.end();
    });
  }

  get(p) { return this.request("GET", p); }
  post(p, body) { return this.request("POST", p, body); }
  put(p, body) { return this.request("PUT", p, body); }
  patch(p, body) { return this.request("PATCH", p, body); }
  del(p, body) { return this.request("DELETE", p, body); }

  p(suffix) { return `/projects/${this.projectId}${suffix}`; }

  getProject() { return this.get(this.p("")); }
  getBoards() { return this.get(this.p("/boards")); }
  getCategories() { return this.get(this.p("/categories")); }
  getStages() { return this.get(this.p("/stages")); }
  getMilestones() { return this.get(this.p("/milestones")); }
  getTags() { return this.get(this.p("/tags")); }
  getImportanceLevels() { return this.get(this.p("/importancelevels")); }
  getDesignElementTypes() { return this.get(this.p("/designelementtypes")); }
  getDesignElements() { return this.get(this.p("/designelements")); }
  getDesignElement(id) { return this.get(this.p(`/designelements/${id}`)); }

  async listAllWorkItems(query) {
    const items = [];
    let offset = 0;
    const limit = 100;
    for (;;) {
      const qs = new URLSearchParams({ offset: String(offset), limit: String(limit), ...(query || {}) });
      const page = await this.get(this.p("/workitems?" + qs.toString()));
      const batch = page && Array.isArray(page.items) ? page.items : (Array.isArray(page) ? page : []);
      items.push(...batch);
      const total = page && page.totalCount != null ? page.totalCount : items.length;
      offset += batch.length;
      if (!batch.length || offset >= total) break;
    }
    return items;
  }

  getWorkItem(id) { return this.get(this.p(`/workitems/${id}`)); }
  createWorkItem(values) { return this.post(this.p("/workitems"), values); }
  patchWorkItem(id, values) { return this.patch(this.p(`/workitems/${id}`), values); }
  getDependencies(id) { return this.get(this.p(`/workitems/${id}/dependencies`)); }
  addDependency(id, dependencyId) {
    const body = typeof dependencyId === "number" || typeof dependencyId === "string"
      ? { dependencyId: Number(dependencyId) }
      : dependencyId;
    return this.post(this.p(`/workitems/${id}/dependencies`), body);
  }
  getSubtasks(id) { return this.get(this.p(`/workitems/${id}/subtasks`)); }
  addSubtask(id, title) { return this.post(this.p(`/workitems/${id}/subtasks`), { title }); }
  patchSubtask(workItemId, subTaskId, values) {
    return this.patch(this.p(`/workitems/${workItemId}/subtasks/${subTaskId}`), values);
  }
  addComment(id, text) { return this.post(this.p(`/workitems/${id}/comments`), { text }); }
  addTag(id, tagId) { return this.post(this.p(`/workitems/${id}/tags`), { tagId }); }
  removeTag(id, tagId) { return this.del(this.p(`/workitems/${id}/tags/${tagId}`)); }

  createCategory(values) { return this.post(this.p("/categories"), values); }
  createTag(values) { return this.post(this.p("/tags"), values); }
  createMilestone(values) { return this.post(this.p("/milestones"), values); }
  createBoard(values) { return this.post(this.p("/boards"), values); }
  createDesignElementType(values) { return this.post(this.p("/designelementtypes"), values); }
  createDesignElement(values) { return this.post(this.p("/designelements"), values); }
  createImportanceLevel(values) { return this.post(this.p("/importancelevels"), values); }

  flattenDesignElements(tree) {
    return flattenDesignElements(Array.isArray(tree) ? tree : [], []);
  }

  webItemUrl(workItemId) {
    return `https://app.hacknplan.com/p/${this.projectId}/kanban?taskid=${workItemId}`;
  }
  projectUrl() {
    return `https://app.hacknplan.com/p/${this.projectId}/dashboards/project`;
  }
}

module.exports = { HacknPlanClient, DEFAULT_PROJECT_ID, flattenDesignElements };
