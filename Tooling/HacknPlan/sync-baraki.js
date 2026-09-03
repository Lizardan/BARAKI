"use strict";

const fs = require("fs");
const path = require("path");
const { execFileSync } = require("child_process");
const { HacknPlanClient } = require("./client");

const ROOT = path.resolve(__dirname, "..", "..");
const IDS_PATH = path.join(__dirname, "ids.json");

function readMd(rel) {
  const p = path.join(ROOT, rel);
  if (!fs.existsSync(p)) return "";
  return fs.readFileSync(p, "utf8");
}

function wrapDoc(opts) {
  const parts = [];
  if (opts.purpose) parts.push("## Purpose\n\n" + opts.purpose.trim());
  if (opts.status) parts.push("## Implementation Status\n\n" + opts.status.trim());
  if (opts.conflict) parts.push("## Conflict\n\n" + opts.conflict.trim());
  if (opts.source) parts.push("## Source\n\n`" + opts.source + "`");
  if (opts.body) {
    let body = opts.body.trim();
    if (body.length > 24000) body = body.slice(0, 24000) + "\n\n… (truncated; full text on disk)";
    parts.push("## Document\n\n" + body);
  }
  return parts.join("\n\n");
}

function githubUrl(n) {
  return "https://github.com/Lizardan/BARAKI/issues/" + n;
}

function parseAcceptance(body) {
  const titles = [];
  const m = String(body || "").match(/##\s*Acceptance[\s\S]*?(?=\n##\s|$)/i)
    || String(body || "").match(/##\s*Критерии[\s\S]*?(?=\n##\s|$)/i);
  const block = m ? m[0] : String(body || "");
  for (const x of block.matchAll(/^\s*-\s*\[[ xX]\]\s*(.+)$/gm)) titles.push(x[1].trim());
  return titles;
}

function importanceFromLabels(labels) {
  const names = (labels || []).map((l) => String(l.name || l).toLowerCase());
  if (names.some((n) => n.includes("critical"))) return 1;
  if (names.some((n) => n.includes("priority/high") || n === "high")) return 2;
  if (names.some((n) => n.includes("priority/low") || n === "low")) return 4;
  return 3;
}

function categoryFromTitle(title, labels, cats) {
  const t = String(title || "");
  const names = (labels || []).map((l) => String(l.name || l).toLowerCase());
  const ui = cats && cats.UI;
  const design = (cats && cats.Design) || 3;
  const programming = (cats && cats.Programming) || 1;
  const bug = (cats && cats.Bug) || 8;
  if (names.includes("bug") || /^\[BUG/i.test(t)) return bug;
  if (/^\[UI-/i.test(t)) return ui || design;
  if (/^\[GDD-/i.test(t) || /^\[DOC-/i.test(t)) return design;
  if (/^\[CHAT-/i.test(t) || /^\[DIST-/i.test(t) || /^\[HUB-/i.test(t)) return programming;
  return programming;
}

function tid(types, preferred, fallback) {
  return (types && types[preferred]) || (types && types[fallback]) || fallback;
}

function mapNameToId(list, nameKey, idKey) {
  const o = {};
  for (const x of list || []) {
    if (x && x[nameKey] != null && x[idKey] != null) o[x[nameKey]] = x[idKey];
  }
  return o;
}

async function ensureNamed(list, name, createFn) {
  const hit = (list || []).find((x) => x && x.name === name);
  if (hit) return hit;
  try {
    const created = await createFn();
    console.log("created", name);
    return created;
  } catch (e) {
    console.log("skip", name, e.status || "", String(e.message || e).slice(0, 160));
    return null;
  }
}

function isStoryTitle(title) {
  return /\[(GATE-001|GATE-002|EA-001|MAIN-001|PRE-007)\]/.test(title);
}

function ghIssues() {
  const raw = execFileSync("gh", [
    "issue", "list", "-R", "Lizardan/BARAKI", "--state", "all", "--limit", "200",
    "--json", "number,title,state,body,labels,url",
  ], { encoding: "utf8", cwd: ROOT, timeout: 60000 });
  return JSON.parse(raw);
}

async function ensureElement(client, existingFlat, spec) {
  const hit = existingFlat.find((e) => e.name === spec.name && (
    !spec.parentId || (e.parent && e.parent.designElementId === spec.parentId) || e.parentId === spec.parentId
  ));
  if (hit) {
    await client.patch(client.p(`/designelements/${hit.designElementId}`), {
      description: spec.description,
      designElementTypeId: spec.typeId,
    }).catch(() => client.patch(client.p(`/designelements/${hit.designElementId}`), { description: spec.description }));
    console.log("gdm ~", spec.name, hit.designElementId);
    return hit.designElementId;
  }
  const created = await client.createDesignElement({
    name: spec.name,
    designElementTypeId: spec.typeId,
    parentId: spec.parentId || 0,
    description: spec.description,
  });
  const id = created.designElementId;
  existingFlat.push({ ...created, name: spec.name, parent: { designElementId: spec.parentId } });
  console.log("gdm +", spec.name, id);
  return id;
}

async function main() {
  const client = HacknPlanClient.fromEnv();
  const ids = JSON.parse(fs.readFileSync(IDS_PATH, "utf8"));
  const roots = ids.gdmRoots;

  const typesCache = await client.getDesignElementTypes();
  for (const name of ["Race", "Unit", "Building", "Economy", "Platform", "Technical"]) {
    await ensureNamed(typesCache, name, async () => {
      const created = await client.createDesignElementType({ name });
      typesCache.push(created);
      return created;
    });
  }
  const catsCache = await client.getCategories();
  for (const spec of [
    { name: "UI", color: "#a2eeef", icon: "desktop" },
    { name: "Production", color: "#e99695", icon: "cogs" },
  ]) {
    await ensureNamed(catsCache, spec.name, async () => {
      const created = await client.createCategory(spec);
      catsCache.push(created);
      return created;
    });
  }

  ids.types = mapNameToId(await client.getDesignElementTypes(), "name", "designElementTypeId");
  ids.categories = mapNameToId(await client.getCategories(), "name", "categoryId");
  ids.tags = mapNameToId(await client.getTags(), "name", "tagId");
  ids.stages = mapNameToId(await client.getStages(), "name", "stageId");
  const T = ids.types;

  await client.del(client.p("/designelements/8"), { deleteWorkItems: false }).then(
    () => console.log("deleted probe element 8"),
    (e) => console.log("probe element:", e.status, String(e.message).slice(0, 120))
  );

  await client.patch(client.p("/milestones/2"), { name: "PRE-RACE2" }).then(
    () => console.log("milestone 2 → PRE-RACE2"),
    (e) => console.log("milestone 2:", e.status, e.message)
  );
  await client.patch(client.p("/milestones/4"), { name: "Early Access" }).then(
    () => console.log("milestone 4 → Early Access"),
    (e) => console.log("milestone 4:", e.status, e.message)
  );

  let flat = client.flattenDesignElements(await client.getDesignElements());

  const gdmSpecs = [
    {
      name: "Vision", parentId: roots.Overview, typeId: T.Chapter,
      description: wrapDoc({
        purpose: "Пиллары, аудитория, scope и non-goals BARAKI.",
        status: "GDD locked. MVP: Windows, 2–5 PvP, 1 раса (Люди).",
        source: "GameDesign/Vision.md",
        body: readMd("GameDesign/Vision.md"),
      }),
    },
    {
      name: "Core Loop", parentId: roots.Gameplay, typeId: T.Mechanic,
      description: wrapDoc({
        purpose: "Макро-петля: база, золото, автономные армии, 3 lanes.",
        status: "Реализовано в MatchController / Combat / Economy.",
        source: "GameDesign/Core Gameplay.md",
        body: readMd("GameDesign/Core Gameplay.md"),
      }),
    },
    {
      name: "Match Flow", parentId: roots.Gameplay, typeId: T.System,
      description: wrapDoc({
        purpose: "Фазы матча, лобби, elimination, disconnect.",
        status: "Реализовано. Ranked очереди — mvp:false.",
        source: "GameDesign/Match Flow.md",
        body: readMd("GameDesign/Match Flow.md"),
      }),
    },
    {
      name: "Economy", parentId: roots.Gameplay, typeId: tid(T, "Economy", "System"),
      description: wrapDoc({
        purpose: "Золото, bounty, мана кастера / main.",
        status: "Реализовано. Lumber — Phase 2.",
        source: "GameDesign/Economy.md",
        body: readMd("GameDesign/Economy.md"),
      }),
    },
    {
      name: "Upgrades", parentId: roots.Gameplay, typeId: T.System,
      description: wrapDoc({
        purpose: "Stat / magic / Divine Blessing / tower tracks.",
        status: "Wiki: PRE-007 Humans ×9 done. GDD Races.md всё ещё помечает часть tower как mvp:false.",
        conflict: "GDD `RACE_TOWER_UPGRADES` / `UPG_TOWER_*` имеют mvp:false, wiki/rules/tower-tracks.md описывает PRE-007 как done. GitHub #6 закрыт.",
        source: "GameDesign/Upgrades.md",
        body: readMd("GameDesign/Upgrades.md"),
      }),
    },
    {
      name: "Bonuses", parentId: roots.Gameplay, typeId: T.Mechanic,
      description: wrapDoc({
        purpose: "12 слотов бонуса после race pick.",
        status: "Контент 12/12 в wiki human-unit-bonuses.md. В Bonuses.md сущности всё ещё mvp:false.",
        conflict: "GDD Bonuses.md: mvp:false на слотах 1–12. Реализация и wiki говорят PRE-006 done.",
        source: "GameDesign/Bonuses.md",
        body: readMd("GameDesign/Bonuses.md"),
      }),
    },
    {
      name: "Combat AI", parentId: roots.Gameplay, typeId: T.Mechanic,
      description: wrapDoc({
        purpose: "Автономия юнитов и героев. Боты rejected.",
        status: "Реализовано. BOT_OPPONENTS rejected.",
        source: "GameDesign/AI.md",
        body: readMd("GameDesign/AI.md"),
      }),
    },
    {
      name: "Balance", parentId: roots.Gameplay, typeId: T.System,
      description: wrapDoc({
        purpose: "Числа: интервалы, золото, scaling.",
        status: "Draft, числа живут и в коде AbilityKitDefaults / Balance.md.",
        source: "GameDesign/Balance.md",
        body: readMd("GameDesign/Balance.md"),
      }),
    },
    {
      name: "Units", parentId: roots.Characters, typeId: tid(T, "Unit", "Character"),
      description: wrapDoc({
        purpose: "Роли юнитов, статы, сквады L1–4.",
        status: "Реализовано для Людей.",
        source: "GameDesign/Units.md",
        body: readMd("GameDesign/Units.md"),
      }),
    },
    {
      name: "Heroes", parentId: roots.Characters, typeId: T.Character,
      description: wrapDoc({
        purpose: "3 героя, XP, титан.",
        status: "Реализовано (wiki + Heroes.md draft).",
        source: "GameDesign/Heroes.md",
        body: readMd("GameDesign/Heroes.md"),
      }),
    },
    {
      name: "Buildings", parentId: roots.Characters, typeId: tid(T, "Building", "Object"),
      description: wrapDoc({
        purpose: "8 зданий базы, ruins, elimination.",
        status: "Реализовано. MAIN-001 (ледяное кольцо / волна света) есть в wiki/rules/building-abilities.md и закрытом GitHub #50; в Buildings.md сущности нет.",
        conflict: "MAIN-001 отсутствует в GameDesign/Buildings.md. TODO.md всё ещё называет MAIN-001 следующей задачей, GitHub #50 closed.",
        source: "GameDesign/Buildings.md",
        body: readMd("GameDesign/Buildings.md") + "\n\n---\n\n" + readMd("wiki/rules/building-abilities.md"),
      }),
    },
    {
      name: "Races", parentId: roots.Characters, typeId: tid(T, "Race", "Character"),
      description: wrapDoc({
        purpose: "Асимметрия рас. Люди — единственная playable. Раса #2 = EA-001.",
        status: "Люди done. Слоты 3–4 TBD. EA-001 blocked до GATE.",
        source: "GameDesign/Races.md",
        body: readMd("GameDesign/Races.md"),
      }),
    },
    {
      name: "Map Topology", parentId: roots.Levels, typeId: T.Level,
      description: wrapDoc({
        purpose: "TOPOLOGY_DUEL и TOPOLOGY_RING, LaneGraph.",
        status: "Реализовано, тесты LaneGraph.",
        source: "GameDesign/Map Topology.md",
        body: readMd("GameDesign/Map Topology.md"),
      }),
    },
    {
      name: "Unity Architecture", parentId: roots.Technical, typeId: tid(T, "Technical", "System"),
      description: wrapDoc({
        purpose: "Стек, asmdef, сцены, камеры, input.",
        status: "Канон: wiki/ARCHITECTURE.md + GameDesign/Technical.md.",
        source: "wiki/ARCHITECTURE.md",
        body: readMd("wiki/ARCHITECTURE.md") + "\n\n---\n\n" + readMd("GameDesign/Technical.md"),
      }),
    },
    {
      name: "Networking", parentId: roots.Technical, typeId: tid(T, "Technical", "System"),
      description: wrapDoc({
        purpose: "Listen-host, 30 Hz, host migration, reconnect.",
        status: "Реализовано. Closed: RES-001/002.",
        source: "wiki/rules/match-network.md",
        body: readMd("wiki/rules/match-network.md"),
      }),
    },
    {
      name: "Snapshot Wire", parentId: roots.Technical, typeId: tid(T, "Technical", "System"),
      description: wrapDoc({
        purpose: "Секционный snapshot codec, handshake версии.",
        status: "Код: CurrentVersion = 23.",
        conflict: "Код v23; wiki/rules/snapshot-wire.md пишет v22; wiki/README.md всё ещё «Wire v21». Не выбирать канон без решения владельца — поправить доки или подтвердить версию.",
        source: "wiki/rules/snapshot-wire.md",
        body: readMd("wiki/rules/snapshot-wire.md"),
      }),
    },
    {
      name: "UI Architecture", parentId: roots.Technical, typeId: T.Menu,
      description: wrapDoc({
        purpose: "Только UI Toolkit, UniRx, UIBindingScope.",
        status: "Реализовано. Codecks sample на uGUI — не интегрирован, конфликт с политикой UITK.",
        conflict: "Assets/Codecks_io импортирован с UnityEngine.UI / Canvas. Не часть трекера, пока нет решения: выкинуть, переписать на UITK, или отдельный in-game reporter.",
        source: "wiki/rules/unity-ui.md",
        body: readMd("wiki/rules/unity-ui.md"),
      }),
    },
    {
      name: "Abilities", parentId: roots.Technical, typeId: T.System,
      description: wrapDoc({
        purpose: "Data-driven abilities, BARAKI Studio VFX.",
        status: "Реализовано.",
        source: "wiki/rules/abilities.md",
        body: readMd("wiki/rules/abilities.md") + "\n\n---\n\n" + readMd("wiki/rules/ability-fx.md"),
      }),
    },
    {
      name: "Fog of War", parentId: roots.Technical, typeId: T.System,
      description: wrapDoc({
        purpose: "Локальная симуляция тумана, Divine Blessing, миникарта.",
        status: "Реализовано.",
        source: "wiki/rules/fog.md",
        body: readMd("wiki/rules/fog.md"),
      }),
    },
    {
      name: "Content Pipeline", parentId: roots.Technical, typeId: T.System,
      description: wrapDoc({
        purpose: "Раскладка SO/префабов Units, BonusUnits, Heroes.",
        status: "Канон wiki/rules/content-assets.md.",
        source: "wiki/rules/content-assets.md",
        body: readMd("wiki/rules/content-assets.md"),
      }),
    },
    {
      name: "Conventions", parentId: roots.Technical, typeId: tid(T, "Technical", "System"),
      description: wrapDoc({
        purpose: "Нейминг, async, UniRx, организация кода.",
        status: "Канон для агентов через opencode.json → wiki/rules/*.md.",
        source: "wiki/rules/code-organization.md",
        body: [
          readMd("wiki/rules/code-organization.md"),
          readMd("wiki/rules/unity-core.md"),
          readMd("wiki/rules/unity-async.md"),
          readMd("wiki/rules/unity-reactive.md"),
        ].join("\n\n---\n\n"),
      }),
    },
    {
      name: "Chat", parentId: roots.Technical, typeId: T.System,
      description: wrapDoc({
        purpose: "Cloudflare WS в меню + NGO в матче.",
        status: "CHAT-003 done. CHAT-001 closed. CHAT-002 open (ротация ключа).",
        source: "wiki/rules/chat.md",
        body: readMd("wiki/rules/chat.md"),
      }),
    },
    {
      name: "MCP and AI agents", parentId: roots.Technical, typeId: tid(T, "Technical", "System"),
      description: wrapDoc({
        purpose: "Unity MCP + UnioTasks (Cursor extension, не в git) поверх HacknPlan.",
        status: "unityMCP :6400. UnioTasks живёт только как расширение Cursor.",
        conflict: "wiki/rules/unity-mcp.md говорит «VFX Graph не установлен»; wiki/ARCHITECTURE.md — VFX Graph 17.6 установлен.",
        source: "wiki/rules/unity-mcp.md",
        body: readMd("wiki/rules/unity-mcp.md") + "\n\n" + readMd("AGENTS.md"),
      }),
    },
    {
      name: "Platform", parentId: roots.Business, typeId: tid(T, "Platform", "System"),
      description: wrapDoc({
        purpose: "Windows hub, UGS, GitHub Releases, апдейтер, лендинг.",
        status: "Реализовано (DIST/HUB closed).",
        source: "GameDesign/Platform.md",
        body: readMd("GameDesign/Platform.md") + "\n\n---\n\n" + readMd("wiki/rules/distribution.md"),
      }),
    },
    {
      name: "Roadmap", parentId: roots.Overview, typeId: T.Chapter,
      description: wrapDoc({
        purpose: "Фазы PRE → GATE → EA. Этот элемент — карта, не бэклог.",
        status: "PRE-001..007 и MAIN-001 закрыты на GitHub. Открыты GATE-001/002 и EA-*. TODO.md устарел: всё ещё пишет «следующая MAIN-001».",
        conflict: "GameDesign/TODO.md: «следующая — MAIN-001 (#50); затем PRE-007». GitHub: #50 и #6 closed. Актуальная очередь: GATE-001, GATE-002, затем EA-001.",
        source: "GameDesign/TODO.md",
        body: readMd("GameDesign/TODO.md"),
      }),
    },
    {
      name: "PvP only", parentId: roots.Story, typeId: T.Chapter,
      description: wrapDoc({
        purpose: "У BARAKI нет кампании и сюжетных миссий. Контент — FFA 2–5 на Windows.",
        status: "Non-goal. Папка Story в GDM оставлена пустой по смыслу, чтобы не плодить фейковый нарратив.",
        source: "GameDesign/Vision.md",
        body: "Humans only, no bots, no campaign. See Vision.md non-goals.",
      }),
    },
  ];

  const gdmIds = {};
  for (const spec of gdmSpecs) {
    gdmIds[spec.name] = await ensureElement(client, flat, spec);
  }

  const issues = ghIssues();
  const existing = await client.listAllWorkItems();
  const byGithub = new Map();
  const byTitle = new Map();
  for (const w of existing) {
    byTitle.set(w.title, w);
    const m = String(w.description || "").match(/github\.com\/Lizardan\/BARAKI\/issues\/(\d+)/i);
    if (m) byGithub.set(Number(m[1]), w);
  }

  async function createItem(values) {
    try {
      return await client.createWorkItem(values);
    } catch (e) {
      const slim = { ...values };
      delete slim.subTasks;
      delete slim.tagIds;
      try {
        return await client.createWorkItem(slim);
      } catch (e2) {
        console.log("create fail", values.title, e.status, e2.status, String(e2.message).slice(0, 220));
        throw e2;
      }
    }
  }

  const gdmForIssue = (title) => {
    if (/\[CHAT-/.test(title)) return gdmIds.Chat;
    if (/\[GATE-/.test(title) || /\[EA-/.test(title)) return gdmIds.Roadmap;
    if (/\[MAIN-/.test(title)) return gdmIds.Buildings;
    if (/\[PRE-007/.test(title)) return gdmIds.Upgrades;
    if (/\[UI-/.test(title)) return gdmIds["UI Architecture"];
    if (/\[HERO-/.test(title)) return gdmIds.Heroes;
    if (/\[HUB-/.test(title) || /\[DIST-/.test(title)) return gdmIds.Platform;
    if (/\[RES-/.test(title) || /\[MVP-N/.test(title)) return gdmIds.Networking;
    if (/\[MVP-/.test(title)) return gdmIds["Core Loop"];
    if (/\[GDD-/.test(title)) return gdmIds.Vision;
    return gdmIds.Roadmap;
  };

  const createdMap = new Map(byGithub);
  for (const issue of issues) {
    const closed = String(issue.state).toLowerCase() === "closed";
    try {
    if (createdMap.has(issue.number) || byTitle.has(issue.title)) {
      const existingItem = createdMap.get(issue.number) || byTitle.get(issue.title);
      createdMap.set(issue.number, existingItem);
      if (closed && existingItem && existingItem.workItemId) {
        await client.patchWorkItem(existingItem.workItemId, { stageId: ids.stages.Completed }).catch((e) => {
          console.log("complete skip", issue.number, e.status, String(e.message).slice(0, 120));
        });
      }
      console.log("issue skip", issue.number);
      continue;
    }
    const desc = [
      issue.body || "",
      "",
      "---",
      "Migrated from GitHub. GitHub issue was **not** edited or closed.",
      githubUrl(issue.number),
    ].join("\n");
    const subTasks = parseAcceptance(issue.body);
    const values = {
      title: issue.title,
      description: desc,
      isStory: isStoryTitle(issue.title),
      estimatedCost: 0,
      importanceLevelId: importanceFromLabels(issue.labels),
      categoryId: categoryFromTitle(issue.title, issue.labels, ids.categories),
      boardId: ids.boardNowId,
      designElementId: gdmForIssue(issue.title) || 0,
      tagIds: [ids.tags.github].filter(Boolean),
      subTasks: subTasks.length ? subTasks : undefined,
    };
    const names = (issue.labels || []).map((l) => String(l.name || l).toLowerCase());
    if (names.includes("deferred") && ids.tags.deferred) values.tagIds.push(ids.tags.deferred);
    if (names.includes("bug") && ids.tags.bug) values.tagIds.push(ids.tags.bug);

    const created = await createItem(values);
    const wid = created.workItemId;
    if (closed) {
      await client.patchWorkItem(wid, { stageId: ids.stages.Completed });
    } else if (names.includes("status/doing")) {
      await client.patchWorkItem(wid, { stageId: ids.stages["In progress"] });
    } else if (names.includes("status/review")) {
      await client.patchWorkItem(wid, { stageId: ids.stages.Testing });
    }
    createdMap.set(issue.number, created);
    byTitle.set(issue.title, created);
    console.log("issue +", issue.number, "→", wid, closed ? "completed" : "open");
  } catch (e) {
    console.log("issue fail", issue.number, e.status || "", String(e.message || e).slice(0, 220));
  }
  }

  const depPairs = [
    [9, 7], [9, 8], [9, 6],
    [7, 6], [7, 50],
    [14, 13],
  ];
  for (const [fromGh, toGh] of depPairs) {
    const from = createdMap.get(fromGh);
    const to = createdMap.get(toGh);
    if (!from || !to) {
      console.log("dep skip", fromGh, "→", toGh);
      continue;
    }
    const fid = from.workItemId;
    const tid = to.workItemId;
    await client.addDependency(fid, tid).then(
      () => console.log("dep", fromGh, "→", toGh, `(${fid}→${tid})`),
      (e) => console.log("dep fail", fromGh, e.status, String(e.message).slice(0, 160))
    );
  }

  const extraTasks = [
    {
      title: "[DOC-001] TODO.md устарел: MAIN-001 и PRE-007 уже закрыты",
      designElementId: gdmIds.Roadmap,
      description: [
        "## Суть",
        "GameDesign/TODO.md пишет, что следующая задача — MAIN-001 (#50), затем PRE-007 (#6).",
        "На GitHub оба issue closed; wiki это подтверждает. Не закрывать конфликт молча: поправить TODO.md под HacknPlan/GATE.",
        "",
        "## Acceptance criteria",
        "- [ ] Шапка TODO.md указывает на HacknPlan, не на GitHub Issues как SoT",
        "- [ ] Строка «следующая — MAIN-001» убрана или помечена как выполненная",
        "",
        "## Agent context (EN)",
        "Do not reopen GitHub issues. Source of truth for tasks is HacknPlan project 242091.",
      ].join("\n"),
    },
    {
      title: "[DOC-002] Snapshot wire: код v23 vs wiki v22 vs индекс v21",
      designElementId: gdmIds["Snapshot Wire"],
      description: [
        "## Суть",
        "Код CurrentVersion = 23. wiki/rules/snapshot-wire.md пишет v22. wiki/README.md всё ещё «Wire v21».",
        "Не выбирать канон без решения: либо поднять доки до v23, либо подтвердить, что индекс/wiki отстают намеренно.",
        "",
        "## Acceptance criteria",
        "- [ ] Одна каноническая версия wire зафиксирована в wiki и README",
        "- [ ] Расхождение с кодом либо устранено, либо явно описано",
      ].join("\n"),
    },
    {
      title: "[DOC-003] VFX Graph: contradiction unity-mcp.md vs ARCHITECTURE.md",
      designElementId: gdmIds["MCP and AI agents"],
      description: [
        "## Суть",
        "wiki/rules/unity-mcp.md говорит, что VFX Graph не установлен.",
        "wiki/ARCHITECTURE.md — VFX Graph 17.6 установлен (Adjustable Slash + AbilityVfxTint).",
        "",
        "## Acceptance criteria",
        "- [ ] Одно из двух правил поправлено, второе согласовано",
      ].join("\n"),
    },
  ];
  const existingTitles = new Set((await client.listAllWorkItems()).map((w) => w.title));
  for (const task of extraTasks) {
    if (existingTitles.has(task.title)) {
      console.log("extra skip", task.title);
      continue;
    }
    const created = await client.createWorkItem({
      title: task.title,
      description: task.description,
      isStory: false,
      estimatedCost: 0,
      importanceLevelId: 2,
      categoryId: (ids.categories && ids.categories.Design) || 3,
      boardId: ids.boardNowId,
      designElementId: task.designElementId || 0,
    });
    console.log("extra +", task.title, "→", created.workItemId);
  }

  const milestones = await client.getMilestones();
  ids.milestones = Object.fromEntries(milestones.map((m) => [m.name, m.milestoneId]));
  ids.gdm = gdmIds;
  fs.writeFileSync(IDS_PATH, JSON.stringify(ids, null, 2), "utf8");
  console.log("done. items", createdMap.size, "gdm", Object.keys(gdmIds).length);
}

main().catch((e) => {
  console.error(e && e.stack ? e.stack : e);
  process.exit(1);
});
