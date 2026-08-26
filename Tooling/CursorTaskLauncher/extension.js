const vscode = require("vscode");
const { execFile } = require("child_process");
const path = require("path");
const fs = require("fs");

const VERSION = "0.0.7";
const STALE_RUNNING_HOURS = 8;
const output = vscode.window.createOutputChannel("BARAKI Task Launcher");

function logError(ctx, e) {
  output.appendLine(`[${new Date().toISOString()}] ${ctx}: ${e && e.stack ? e.stack : e}`);
}

// ---------- tool resolution ----------
let _gh, _oc;
function resolveTool(key, getCached, setCached, candidates) {
  if (getCached()) return getCached();
  const p = (async () => {
    for (const c of candidates) {
      if (!fs.existsSync(c)) continue;
      const ok = await new Promise((r) =>
        execFile(c, ["--version"], { windowsHide: true, timeout: 10000 }, (e) => r(!e ? c : null))
      );
      if (ok) { output.appendLine(`${key}: ${ok}`); return ok; }
    }
    throw new Error(`${key} не найден в PATH. Установи и перезапусти Cursor.`);
  })().catch((e) => { setCached(null); throw e; });
  setCached(p);
  return p;
}

const gh = () => resolveTool("gh", () => _gh, (v) => (_gh = v), [
  "gh",
  path.join(process.env["ProgramFiles"] || "", "GitHub CLI", "gh.exe"),
  path.join(process.env["LOCALAPPDATA"] || "", "Microsoft", "WinGet", "Links", "gh.exe")
]);
const oc = () => resolveTool("opencode", () => _oc, (v) => (_oc = v), [
  "opencode",
  path.join(process.env["USERPROFILE"] || "", ".opencode", "bin", "opencode.exe")
]);

function ghExec(args, timeoutMs) {
  return gh().then((p) => new Promise((res, rej) =>
    execFile(p, args, { cwd: root(), windowsHide: true, timeout: timeoutMs || 30000, maxBuffer: 8 * 1024 * 1024 },
      (e, out, err) => e ? rej(new Error(err || e.message)) : res(out))
  ));
}

function root() {
  return vscode.workspace.workspaceFolders?.[0]?.uri.fsPath || process.cwd();
}

// ---------- state ----------
let _state;
function running() { return _state.get("bar.running", {}); }
function setRunning(v) { return _state.update("bar.running", v); }
function verified() { return _state.get("bar.verified", {}); }
function setVerified(v) { return _state.update("bar.verified", v); }
function interrupted() { return _state.get("bar.interrupted", {}); }
function setInterrupted(v) { return _state.update("bar.interrupted", v); }

// сессия opencode завершилась (Ctrl+C, выход, закрытие терминала)
function sessionEnded(terminalName, exitCode) {
  const m = /^opencode #(\d+)$/.exec(terminalName || "");
  if (!m) return;
  const num = Number(m[1]);
  const r = running();
  if (!r[num]) return;
  delete r[num];
  setRunning(r);
  if (exitCode === 0) {
    // штатный выход из TUI — считаем задачу выполненной (зелёная галочка)
    const v = verified();
    v[num] = new Date().toISOString();
    setVerified(v);
  } else {
    // прервана — жёлтая пауза, можно запустить заново
    const it = interrupted();
    it[num] = new Date().toISOString();
    setInterrupted(it);
  }
  provider && provider.refresh();
}

// ---------- gh wrappers ----------
async function listIssues(st, extra) {
  const repo = vscode.workspace.getConfiguration("barakiTaskLauncher").get("repo");
  const label = vscode.workspace.getConfiguration("barakiTaskLauncher").get("label");
  const args = ["issue", "list", "-R", repo, "--state", st, "--limit", "100",
    "--json", "number,title,body,closedAt"];
  if (label) args.splice(4, 0, "--label", label);
  if (extra) args.push(...extra);
  return JSON.parse(await ghExec(args));
}

function buildPrompt(data) {
  const sb = [
    `Работай над задачей из GitHub Issues репозитория ${vscode.workspace.getConfiguration("barakiTaskLauncher").get("repo")}.`,
    "",
    `# [${data.Number}] ${data.Title}`,
    "",
    data.body || "",
    "",
    "---",
    "Правила выполнения (github-issues-workflow):",
    "- Секция 'Agent context (EN)' — технический контекст для тебя; остальное — постановка задачи.",
    "- Выполняй чекбоксы Acceptance criteria; следуй AGENTS.md и wiki/rules/.",
    "- После правок кода проверь компиляцию (read_console) и прогони затронутые тесты.",
    "- Коммиты не делать без явной просьбы пользователя.",
    "- НЕ ЗАКРЫВАЙ issue командой gh issue close — пользователь закроет сам после проверки.",
  ];
  return sb.join("\n");
}

// ---------- resolve issue number from various arg shapes ----------
function toNum(arg) {
  if (typeof arg === "number") return arg;
  if (arg && typeof arg === "object") {
    if (typeof arg.number === "number") return arg.number;
    if (typeof arg.num === "number") return arg.num;
    // инлайн-кнопки передают TreeItem с contextValue "issue:<state>:<N>"
    const cv = typeof arg.contextValue === "string" ? arg.contextValue : "";
    const m = cv.match(/^issue:[a-z]+:(\d+)/);
    if (m) return Number(m[1]);
  }
  return null;
}

function parseNumFromLabel(label) {
  if (typeof label === "string") {
    const m = label.match(/^#(\d+)/);
    if (m) return Number(m[1]);
  }
  return null;
}

// чистим markdown для читаемого отображения в дереве
function mdClean(s) {
  return (s || "")
    .replace(/\*\*/g, "")
    .replace(/^#{1,6}\s*/gm, "")
    .replace(/`/g, "")
    .replace(/^>\s?/gm, "")
    .replace(/\[( |x|X)\]/g, "")
    .replace(/\s+/g, " ")
    .trim();
}

// перенос длинного текста на строки ~width символов
function wrapText(s, width) {
  const words = s.split(" ");
  const lines = [];
  let cur = "";
  for (const w of words) {
    if (cur && (cur + " " + w).length > width) {
      lines.push(cur);
      cur = w;
    } else {
      cur = cur ? cur + " " + w : w;
    }
  }
  if (cur) lines.push(cur);
  return lines;
}

// ---------- анализ проекта: opencode-сессия в режиме планирования ----------
const SUMMARY_TERM = "BARAKI анализ";
let _summaryActive = false;

// промпт анализа проекта лежит в отдельном файле — правится без пересборки расширения
const SUMMARY_PROMPT_FILE = path.join(__dirname, "summary-prompt.md");
function loadSummaryPrompt() {
  try { return fs.readFileSync(SUMMARY_PROMPT_FILE, "utf-8"); }
  catch (_) { return "Проанализируй состояние проекта и порекомендуй следующую задачу."; }
}

// ✨ — открывает opencode-сессию (режим plan) с промптом анализа проекта
async function generateSummaryInTerminal() {
  if (_summaryActive) {
    vscode.window.setStatusBarMessage("Анализ уже идёт — смотрите терминал «BARAKI анализ»", 4000);
    return;
  }
  try {
    const promptFile = path.join(require("os").tmpdir(), "baraki-summary-prompt.txt");
    fs.writeFileSync(promptFile, loadSummaryPrompt(), "utf-8");

    let t = vscode.window.terminals.find((x) => x.name === SUMMARY_TERM);
    if (!t) {
      t = vscode.window.createTerminal({
        name: SUMMARY_TERM,
        cwd: root(),
        location: { viewColumn: vscode.ViewColumn.One }
      });
    }
    t.show(true);
    _summaryActive = true;
    t.sendText(`opencode --agent plan --prompt (Get-Content '${promptFile}' -Raw)`, true);
  } catch (e) {
    logError("summary-launch", e);
    vscode.window.showErrorMessage(`Анализ: ${e.message}`);
    _summaryActive = false;
  }
}

// выход из сессии анализа — просто снимаем флаг
function summaryCommandEnded() {
  if (_summaryActive) {
    _summaryActive = false;
    provider.refresh();
  }
}

// ---------- body parser ----------
function parseBody(body) {
  const r = { essence: "", acceptance: [] };
  if (!body) return r;
  const em = body.match(/##\s*Суть\s*\n([\s\S]*?)(?=\n##\s|$)/i);
  if (em) r.essence = em[1].trim();
  const am = body.match(/##\s*Acceptance[^\n]*\n([\s\S]*?)(?=\n##\s|$)/i);
  if (am) {
    for (const m of am[1].matchAll(/^\s*-\s*\[( |x|X)\]\s*(.+)$/gm))
      r.acceptance.push({ done: m[1].toLowerCase() === "x", text: m[2].trim() });
  }
  return r;
}

// ================================================================
// PANEL
// ================================================================
class TasksProvider {
  constructor() {
    this._emitter = new vscode.EventEmitter();
    this.onDidChangeTreeData = this._emitter.event;
    this.open = [];
    this.error = null;
    this._loaded = false;
    this._lastSet = null;
  }

  refresh() { this.load().catch((e) => logError("refresh", e)); }

  async load() {
    try {
      this.open = (await listIssues("open")).sort((a, b) => a.number - b.number);

      // clean running: issue closed or stale
      const r = running();
      const nums = new Set(this.open.map((i) => i.number));
      let dirty = false;
      for (const k of Object.keys(r)) {
        const n = Number(k);
        if (!nums.has(n) || Date.now() - Date.parse(r[k]) > STALE_RUNNING_HOURS * 3600000) {
          delete r[k]; dirty = true;
        }
      }
      if (dirty) await setRunning(r);

      // clean verified/interrupted: issue closed
      const v = verified();
      let vDirty = false;
      for (const k of Object.keys(v)) {
        if (!nums.has(Number(k))) { delete v[k]; vDirty = true; }
      }
      if (vDirty) await setVerified(v);
      const it = interrupted();
      let itDirty = false;
      for (const k of Object.keys(it)) {
        if (!nums.has(Number(k))) { delete it[k]; itDirty = true; }
      }
      if (itDirty) await setInterrupted(it);

      this.error = null;
    } catch (e) {
      logError("load", e);
      this.error = String(e.message || e);
    }
    this._emitter.fire();
  }

  getTreeItem(row) {
    if (row.kind === "error") return new vscode.TreeItem(`⚠ ${row.label}`);
    if (row.kind === "header") {
      const h = new vscode.TreeItem(row.label, vscode.TreeItemCollapsibleState.None);
      h.contextValue = "header";
      return h;
    }
    if (row.kind === "sechead") {
      // заголовок секции: цветная иконка + капс
      const h = new vscode.TreeItem(row.label.toUpperCase(), vscode.TreeItemCollapsibleState.None);
      h.iconPath = new vscode.ThemeIcon(row.icon, new vscode.ThemeColor(row.color));
      h.contextValue = "detail";
      return h;
    }
    if (row.kind === "spacer") {
      const sp = new vscode.TreeItem("\u00A0", vscode.TreeItemCollapsibleState.None);
      sp.contextValue = "detail";
      return sp;
    }
    if (row.kind === "acc") {
      // пункт чеклиста: цветная галочка/кружок
      const a = new vscode.TreeItem("    " + row.text, vscode.TreeItemCollapsibleState.None);
      a.iconPath = row.done
        ? new vscode.ThemeIcon("check", new vscode.ThemeColor("charts.green"))
        : new vscode.ThemeIcon("circle-large-outline", new vscode.ThemeColor("charts.foreground"));
      if (row.tooltip) a.tooltip = new vscode.MarkdownString(row.tooltip);
      a.contextValue = "detail";
      return a;
    }
    if (row.kind === "detail") {
      const d = new vscode.TreeItem(row.label, vscode.TreeItemCollapsibleState.None);
      d.contextValue = "detail";
      if (row.tooltip) d.tooltip = new vscode.MarkdownString(String(row.tooltip));
      return d;
    }

    // ---- issue row ----
    const r = running();
    const v = verified();
    const it = interrupted();
    const isRunning = !!r[row.num];
    const isVerified = !!v[row.num];
    const isInterrupted = !isRunning && !!it[row.num];

    const item = new vscode.TreeItem(`#${row.num} ${row.title}`, vscode.TreeItemCollapsibleState.Collapsed);
    // состояние+номер в contextValue: инлайн-кнопки на строке и when-фильтры
    item.contextValue = isRunning ? `issue:running:${row.num}`
      : isInterrupted ? `issue:interrupted:${row.num}`
      : isVerified ? `issue:verified:${row.num}`
      : `issue:ready:${row.num}`;

    if (isRunning) {
      item.iconPath = new vscode.ThemeIcon("sync~spin", new vscode.ThemeColor("charts.yellow"));
      item.tooltip = new vscode.MarkdownString("**Выполняется** — сессия opencode открыта");
    } else if (isInterrupted) {
      item.iconPath = new vscode.ThemeIcon("debug-pause", new vscode.ThemeColor("charts.yellow"));
      item.tooltip = new vscode.MarkdownString("**Прервана** — сессия остановена, можно запустить заново");
    } else if (isVerified) {
      item.iconPath = new vscode.ThemeIcon("check-all", new vscode.ThemeColor("charts.green"));
      item.tooltip = new vscode.MarkdownString("**Готово** — можно проверить и закрыть");
    } else {
      item.iconPath = new vscode.ThemeIcon("circle-large-outline", new vscode.ThemeColor("charts.foreground"));
      item.tooltip = new vscode.MarkdownString("**Не начата** — клик для подробностей");
    }
    return item;
  }

  async getChildren(element) {
    if (this.error) return [{ kind: "error", label: this.error.slice(0, 100) }];
    if (!this._loaded) { await this.load(); this._loaded = true; }


    // ---- issue children ----
    // ВАЖНО: сюда приходит ИСХОДНЫЙ объект-строка ({kind,num,title}), не TreeItem
    if (element && element.kind === "open") {
      const num = element.num;
      const issue = this.open.find((i) => i.number === num);
      if (!issue) return [];

      const parsed = parseBody(issue.body);
      const r = running();
      const v = verified();
      const it = interrupted();
      const isRunning = !!r[num];
      const isVerified = !!v[num];
      const isInterrupted = !isRunning && !!it[num];
      const out = [];

      // ── Описание ──
      if (parsed.essence) {
        out.push({ kind: "sechead", label: "ОПИСАНИЕ", icon: "text", color: "charts.blue" });
        for (const line of wrapText(mdClean(parsed.essence), 100)) {
          out.push({ kind: "detail", label: "    " + line, tooltip: "" });
        }
      }

      // ── Чеклист ──
      if (parsed.acceptance.length > 0) {
        out.push({ kind: "spacer" });
        const doneCount = parsed.acceptance.filter((a) => a.done).length;
        out.push({
          kind: "sechead",
          label: `ЧЕКЛИСТ  ${doneCount}/${parsed.acceptance.length}`,
          icon: "checklist", color: "charts.green"
        });
        for (const acc of parsed.acceptance) {
          out.push({
            kind: "acc",
            done: acc.done,
            text: acc.text.slice(0, 115),
            tooltip: acc.text
          });
        }
      }

      // ── Статус ──
      out.push({ kind: "spacer" });
      out.push({ kind: "sechead", label: "СТАТУС", icon: "pulse", color: "charts.orange" });
      if (isRunning) {
        out.push({ kind: "detail", label: "    ⏳ Выполняется в opencode…" });
      } else if (isInterrupted) {
        out.push({ kind: "detail", label: "    ⏸ Прервана — ▶ заново, ⃠ сброс, ✕ закрыть" });
      } else if (isVerified) {
        out.push({ kind: "detail", label: "    ✔ Готово — проверьте и закройте" });
      } else {
        out.push({ kind: "detail", label: "    Не начата — ▶ запустит в opencode" });
      }
      return out;
    }

    // ---- top level: только открытые задачи, по возрастанию номеров ----
    const out = [];
    out.push({ kind: "header", label: `ЗАДАЧИ — ${this.open.length} · v${VERSION}` });
    for (const i of this.open) out.push({ kind: "open", num: i.number, title: i.title });
    return out;
  }
}

// ================================================================
// ACTIVATE
// ================================================================
function activate(context) {
  _state = context.globalState;
  const cfg = () => vscode.workspace.getConfiguration("barakiTaskLauncher");

  const provider = new TasksProvider();

  // ---- launch: build prompt → create terminal → send opencode run --interactive ----
  async function launch(arg) {
    const num = toNum(arg);
    if (!num) { vscode.window.showErrorMessage(`[${VERSION}] Не удалось определить номер задачи`); return; }

    const r = running();
    r[num] = new Date().toISOString();
    await setRunning(r);

    const data = await listIssues("open").then((arr) => arr.find((i) => i.number === num));
    const prompt = data ? buildPrompt({ Number: data.number, Title: data.title, body: data.body })
      : `Работай над задачей #${num} из ${cfg().get("repo")}. Не закрывай issue.`;

    // терминал открывается вкладкой в editor area; запускаем полноценный TUI
    // с промптом (--prompt), а не run -i — пользователь попадает внутрь сессии
    const term = vscode.window.createTerminal({
      name: `opencode #${num}`,
      cwd: root(),
      location: { viewColumn: vscode.ViewColumn.One },
      iconPath: new vscode.ThemeIcon("rocket")
    });
    term.show(true);
    const tmpFile = path.join(require("os").tmpdir(), `baraki-task-${num}.txt`);
    fs.writeFileSync(tmpFile, prompt, "utf-8");
    term.sendText(`opencode --agent plan --prompt (Get-Content '${tmpFile}' -Raw)`, true);

    vscode.window.setStatusBarMessage(`opencode запущен: #${num}`, 5000);
    provider.refresh();
  }

  // ---- mark done: remove from running, add to verified ----
  async function markDone(arg) {
    const num = toNum(arg);
    if (!num) return;
    const r = running();
    delete r[num];
    await setRunning(r);
    const it = interrupted();
    delete it[num];
    await setInterrupted(it);
    const v = verified();
    v[num] = new Date().toISOString();
    await setVerified(v);
    vscode.window.setStatusBarMessage(`#${num} помечен как готовый`, 3000);
    provider.refresh();
  }

  // ---- сброс прерванной задачи: убрать ⏸, вернуть в «не начата» ----
  async function resetTask(num) {
    const it = interrupted();
    delete it[num];
    await setInterrupted(it);
    vscode.window.setStatusBarMessage(`#${num} сброшен — не начата`, 3000);
    provider.refresh();
  }

  // ---- close issue: gh issue close ----
  async function closeIssue(arg) {
    const num = toNum(arg);
    if (!num) return;
    const confirm = await vscode.window.showWarningMessage(
      `Закрыть issue #${num}?`, "Закрыть", "Отмена"
    );
    if (confirm !== "Закрыть") return;
    try {
      await ghExec(["issue", "close", String(num), "-R", cfg().get("repo"), "-c", "Закрыто из панели BARAKI Задачи"]);
      const v = verified();
      delete v[num];
      await setVerified(v);
      const it = interrupted();
      delete it[num];
      await setInterrupted(it);
      vscode.window.setStatusBarMessage(`#${num} закрыт`, 3000);
      provider.refresh();
    } catch (e) {
      vscode.window.showErrorMessage(`Ошибка: ${e.message}`);
    }
  }

  const treeView = vscode.window.createTreeView("barakiTasks.taskList", {
    treeDataProvider: provider, showCollapseAll: false
  });

  const secs = Number(cfg().get("autoRefreshSeconds")) || 0;
  const timer = secs >= 10 ? setInterval(() => provider.refresh(), secs * 1000) : null;
  provider.refresh();

  // номер из аргумента команды или из выделенной строки дерева
  function numFrom(arg) {
    const direct = toNum(arg);
    if (direct) return direct;
    for (const s of treeView.selection || []) {
      const n = toNum(s);
      if (n) return n;
    }
    return null;
  }

  context.subscriptions.push(
    treeView,
    { dispose: () => timer && clearInterval(timer) },
    // завершение сессии opencode / команды сводки
    vscode.window.onDidCloseTerminal((t) => {
      if (t && t.name === SUMMARY_TERM) summaryCommandEnded(-1);
      else sessionEnded(t.name, undefined);
    }),
    ...(vscode.window.onDidEndTerminalShellExecution
      ? [vscode.window.onDidEndTerminalShellExecution((e) => {
          if (!e || e.exitCode === undefined) return;
          const name = e.terminal && e.terminal.name;
          if (name === SUMMARY_TERM) summaryCommandEnded(e.exitCode);
          else sessionEnded(name, e.exitCode);
        })]
      : []),
    vscode.commands.registerCommand("barakiTaskLauncher.refresh", () => provider.refresh()),
    vscode.commands.registerCommand("barakiTaskLauncher.startTask", (arg) => {
      const num = numFrom(arg);
      if (!num) { vscode.window.showErrorMessage(`[${VERSION}] Выберите строку задачи и нажмите ▶`); return; }
      launch(num);
    }),
    vscode.commands.registerCommand("barakiTaskLauncher.doneTask", (arg) => {
      const num = numFrom(arg);
      if (!num) { vscode.window.showErrorMessage(`[${VERSION}] Выберите строку задачи и нажмите ✔`); return; }
      markDone(num);
    }),
    vscode.commands.registerCommand("barakiTaskLauncher.closeTask", (arg) => {
      const num = numFrom(arg);
      if (!num) { vscode.window.showErrorMessage(`[${VERSION}] Выберите строку задачи и нажмите ✕`); return; }
      closeIssue(num);
    }),
    vscode.commands.registerCommand("barakiTaskLauncher.resetTask", (arg) => {
      const num = numFrom(arg);
      if (!num) { vscode.window.showErrorMessage(`[${VERSION}] Выберите строку задачи и нажмите сброс`); return; }
      resetTask(num);
    }),
    vscode.commands.registerCommand("barakiTaskLauncher.refreshSummary", () => generateSummaryInTerminal()),
    vscode.commands.registerCommand("barakiTaskLauncher.launchIssue", async () => {
      const items = (await listIssues("open").catch(() => []))
        .sort((a, b) => a.number - b.number)
        .map((i) => ({ label: `#${i.number} ${i.title}`, number: i.number }));
      const pick = await vscode.window.showQuickPick(items, { placeHolder: "Задача для запуска" });
      if (pick) launch(pick.number);
    }),
  );
}

function deactivate() {}
module.exports = { activate, deactivate };
