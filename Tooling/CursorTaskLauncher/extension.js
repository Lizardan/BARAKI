const vscode = require("vscode");
const { execFile, exec } = require("child_process");
const path = require("path");
const fs = require("fs");

const VERSION = "0.0.52";
const STALE_RUNNING_HOURS = 8;
// сколько секунд тишины в терминале считаем «агент закончил» (сессия ещё открыта)
const AGENT_IDLE_MS = 60000;
const output = vscode.window.createOutputChannel("BARAKI Task Launcher");

function logError(ctx, e) {
  output.appendLine(`[${new Date().toISOString()}] ${ctx}: ${e && e.stack ? e.stack : e}`);
}

// ---------- tool resolution ----------
// Запуск через shell с кавычками: opencode на Windows — npm-шим (.cmd),
// execFile без shell его не выполняет.
// Каждый аргумент квотится: значения с пробелами (например, -c "комментарий
// апрува") иначе разваливаются на отдельные слова и gh падает
// с «accepts 1 arg(s), received N».
function quoteShellArg(value) {
  const s = String(value);
  return /^[\w\-.,:=@%+/\\]*$/.test(s) ? s : `"${s.replace(/"/g, '\\"')}"`;
}

function runTool(cmdPath, args, timeoutMs) {
  return new Promise((res, rej) => {
    exec(`"${cmdPath}" ${args.map(quoteShellArg).join(" ")}`,
      { cwd: root(), windowsHide: true, timeout: timeoutMs || 30000, maxBuffer: 8 * 1024 * 1024 },
      (e, out, err) => e ? rej(new Error(err || e.message)) : res(out));
  });
}

let _gh, _oc;
function resolveTool(key, getCached, setCached, candidates) {
  if (getCached()) return getCached();
  const p = (async () => {
    const existing = candidates.filter((c) => c && fs.existsSync(c));
    // ни один кандидат не найден на диске — пробуем голое имя через PATH
    const tries = existing.length ? existing : [key];
    for (const c of tries) {
      const ok = await runTool(c, ["--version"], 10000).then(() => true).catch(() => false);
      if (ok) { output.appendLine(`${key}: ${c}`); return c; }
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
  path.join(process.env["APPDATA"] || "", "npm", "opencode.cmd"),
  path.join(process.env["USERPROFILE"] || "", ".opencode", "bin", "opencode.exe")
]);

function ghExec(args, timeoutMs) {
  return gh().then((p) => runTool(p, args, timeoutMs || 30000));
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
// номер issue → id opencode-сессии (для «открыть сессию» после перезапуска Cursor)
function sessions() { return _state.get("bar.sessions", {}); }
function setSessions(v) { return _state.update("bar.sessions", v); }

// активность агента в открытой сессии: время последнего вывода терминала.
// Тишина дольше AGENT_IDLE_MS = агент закончил → задача падает в «СДЕЛАННЫЕ»,
// новый вывод (продолжили доделывать) → обратно «в процессе».
const _actMem = {};
let _actFlushTimer = null;
let _lastActivityRefresh = 0;

function persistedActivity() { return _state ? _state.get("bar.activity", {}) : {}; }
function noteTerminalActivity(num) {
  _actMem[num] = Date.now();
  if (_actFlushTimer) return;
  _actFlushTimer = setTimeout(() => {
    _actFlushTimer = null;
    if (_state) _state.update("bar.activity", { ..._actMem });
    const now = Date.now();
    if (provider && now - _lastActivityRefresh > 4000) {
      _lastActivityRefresh = now;
      provider.refresh();
    }
  }, 3000);
}
function clearActivity(num) {
  delete _actMem[num];
  if (_state) {
    const a = persistedActivity();
    delete a[num];
    _state.update("bar.activity", a);
  }
}
/** true — агент сейчас работает; false — тихо (закончил); null — сигналов не было */
function isAgentActive(num) {
  const t = _actMem[num] ?? persistedActivity()[num];
  if (!t) return null;
  return Date.now() - t < AGENT_IDLE_MS;
}

// сессия opencode завершилась (Ctrl+C, выход, закрытие терминала)
function sessionEnded(terminalName, exitCode) {
  const m = /^opencode #(\d+)$/.exec(terminalName || "");
  if (!m) return;
  const num = Number(m[1]);
  const r = running();
  if (!r[num]) return;
  delete r[num];
  setRunning(r);
  clearActivity(num);
  // любой выход из сессии = агент сейчас не работает → «ждёт апрува»
  const v = verified();
  v[num] = new Date().toISOString();
  setVerified(v);
  provider && provider.refresh();
}

// ---------- gh wrappers ----------
async function listIssues(st, extra) {
  const repo = vscode.workspace.getConfiguration("barakiTaskLauncher").get("repo");
  const label = vscode.workspace.getConfiguration("barakiTaskLauncher").get("label");
  const args = ["issue", "list", "-R", repo, "--state", st, "--limit", "100",
    "--json", "number,title,body,closedAt,labels"];
  if (label) args.splice(4, 0, "--label", label);
  if (extra) args.push(...extra);
  return JSON.parse(await ghExec(args));
}

function ocExec(args, timeoutMs) {
  return oc().then((p) => runTool(p, args, timeoutMs || 20000));
}

// ---------- предпросмотр задачи: нативный markdown-preview (как превью .md в Cursor) ----------
// Карточка-дерево остаётся пультом (статус/запуск), а полный вид «как на GitHub»
// открывается во вкладке-превью на всю ширину: TextDocumentContentProvider отдаёт
// тело issue как markdown, встроенный markdown.showPreview рисует его.
const ISSUE_SCHEME = "baraki-issue";
const _issueMdCache = new Map(); // номер issue → markdown

class IssueContentProvider {
  provideTextDocumentContent(uri) {
    const m = /(\d+)\.md$/.exec(uri.path || String(uri));
    const num = m ? Number(m[1]) : null;
    if (!num || !_issueMdCache.has(num)) return "# Задача не загружена\n\nОткрой превью из панели BARAKI Задачи.";
    return _issueMdCache.get(num);
  }
}

async function previewIssue(num) {
  try {
    const repo = vscode.workspace.getConfiguration("barakiTaskLauncher").get("repo");
    const data = JSON.parse(await ghExec(
      ["issue", "view", String(num), "-R", repo, "--json", "number,title,body"]));
    const md = `# [#${data.number}] ${data.title}\n\n> Открыть на GitHub: https://github.com/${repo}/issues/${data.number}\n\n${data.body || ""}`;
    _issueMdCache.set(num, md);
    const uri = vscode.Uri.parse(`${ISSUE_SCHEME}:issue-${num}.md`);
    await vscode.commands.executeCommand("markdown.showPreview", uri, { preserveFocus: false });
  } catch (e) {
    vscode.window.showErrorMessage(`[${VERSION}] Превью #${num}: ${e.message}`);
  }
}

// ---------- resume opencode-сессии ----------
// Сессия ищется по заголовку: промпт задачи содержит «#<num>» и ID-префикс
// («[PRE-007] …»), opencode называет сессию по началу промта. Совпадение по
// словам заголовка — слабый сигнал; по #num / ID-токену — сильный.
function titleMatcher(num, title) {
  const idTokens = [...String(title || "").matchAll(/\b([A-Z]{2,10}-\d+)\b/g)].map((m) => m[1]);
  const words = new Set(String(title || "").replace(/\[[^\]]*\]/g, " ")
    .toLowerCase().split(/[^\p{L}\p{N}]+/u).filter((w) => w.length >= 4));
  return (t) => {
    if (!t) return 0;
    let score = 0;
    if (new RegExp(`#${num}\\b`).test(t)) score += 100;
    const tl = t.toLowerCase();
    for (const tok of idTokens) if (tl.includes(tok.toLowerCase())) score += 60;
    for (const w of words) if (tl.includes(w)) score += 1;
    return score;
  };
}

async function resolveSessionId(num, title) {
  const saved = sessions()[num];
  try {
    const raw = await ocExec(["session", "list", "--format", "json", "--max-count", "300"]);
    const all = JSON.parse(raw);
    const dir = root().toLowerCase();
    const scoreOf = titleMatcher(num, title);
    let best = null;
    for (const s of Array.isArray(all) ? all : []) {
      if (!s || typeof s.id !== "string") continue;
      if (String(s.directory || "").toLowerCase() !== dir) continue;
      const sc = scoreOf(String(s.title || ""));
      if (sc <= 0) continue;
      if (!best || sc > best.sc) best = { id: s.id, sc };
    }
    // сохранённый id перебиваем только сильным совпадением (#num);
    // без сохранённого нужен порог — одно слово заголовка («BARAKI») не счёт
    if (best && best.sc >= 4 && (!saved || best.sc >= 100)) {
      const m = sessions();
      m[num] = best.id;
      await setSessions(m);
      return best.id;
    }
  } catch (e) {
    logError("resolveSession", e);
  }
  return saved || null;
}

// ---------- приоритеты и метки ----------
// Роль цветных меток: priority/* и P0–P3 задают группу сортировки,
// blocked уводит задачу в отдельную группу в конце списка.
const TIERS = [
  {
    key: "critical", header: "ГОРИТ — СДЕЛАТЬ СЕЙЧАС",
    icon: "flame", themeColor: "charts.red",
    match: (n) => /\bcritical\b|\burgent\b|^p0$|срочн|горит/.test(n),
  },
  {
    key: "high", header: "ВЫСОКИЙ ПРИОРИТЕТ",
    icon: "arrow-up", themeColor: "charts.green",
    match: (n) => /^p1$|priority\/high|\bhigh\b|важн/.test(n),
  },
  {
    key: "low", header: "НИЗКИЙ ПРИОРИТЕТ",
    icon: "arrow-down", themeColor: "charts.blue",
    match: (n) => /\bp3\b|priority\/low|^low$|nice-to-have|низк/.test(n),
  },
];
const NORMAL_TIER = {
  key: "normal", header: "ПО ОЧЕРЕДИ",
  icon: "circle-filled", themeColor: "charts.foreground",
  match: () => true,
};
const BLOCKED_TIER = {
  key: "blocked", header: "ЗАБЛОКИРОВАНО",
  icon: "debug-pause", themeColor: "charts.red",
};

function labelsOf(issue) {
  return Array.isArray(issue.labels) ? issue.labels : [];
}

function tierOf(issue) {
  const names = labelsOf(issue).map((l) => String(l.name || "").toLowerCase());
  if (names.some((n) => n === "blocked")) return BLOCKED_TIER;
  for (const t of TIERS) {
    if (names.some((n) => t.match(n))) return t;
  }
  return NORMAL_TIER;
}

// цветная точка метки — svg data-uri с настоящим цветом GitHub
function labelNamesOf(issue, limit) {
  const names = labelsOf(issue).map((l) => String(l.name || "")).filter(Boolean);
  return typeof limit === "number" ? names.slice(0, limit) : names;
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

// Через 30 с после запуска задачи фиксируем id её opencode-сессии (создана после
// запуска, каталог проекта): дальше «открыть сессию» работает по точному id,
// а не только по совпадению заголовка.
function captureSessionIdLater(num, title, sinceMs) {
  setTimeout(() => {
    ocExec(["session", "list", "--format", "json", "--max-count", "50"])
      .then((raw) => JSON.parse(raw))
      .then(async (all) => {
        const dir = root().toLowerCase();
        const scoreOf = titleMatcher(num, title);
        let best = null;
        for (const s of Array.isArray(all) ? all : []) {
          if (!s || typeof s.id !== "string") continue;
          if (String(s.directory || "").toLowerCase() !== dir) continue;
          if (Number(s.created) < sinceMs - 5000) continue;
          const sc = scoreOf(String(s.title || ""));
          if (!best || sc > best.sc
            || (sc === best.sc && Number(s.created) > best.created)) {
            best = { id: s.id, sc, created: Number(s.created) };
          }
        }
        if (best) {
          const m = sessions();
          m[num] = best.id;
          await setSessions(m);
          output.appendLine(`session captured #${num}: ${best.id}`);
        }
      })
      .catch((e) => logError("captureSession", e));
  }, 30000);
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

// markdown → читаемые строки для дерева: списки «•», таблицы «a · b»,
// заголовки КАПСОМ, ссылки → текст, код с отступом
function mdClean(s) {
  if (!s) return "";
  const lines = [];
  let inCode = false;
  for (let raw of s.split(/\r?\n/)) {
    const line = raw.trimEnd();
    if (/^\s*```/.test(line)) { inCode = !inCode; continue; }
    if (inCode) { lines.push(line ? "      " + line : ""); continue; }
    let t = line.trim();
    if (!t || /^[-=_*]{3,}$/.test(t)) continue;
    if (/^\|[\s:-]+\|/.test(t)) continue; // разделитель таблицы
    t = t.replace(/!\[[^\]]*\]\([^)]*\)/g, ""); // картинки
    t = t.replace(/\[([^\]]+)\]\([^)]*\)/g, "$1"); // ссылки → текст
    t = t.replace(/\*\*([^*]+)\*\*/g, "$1"); // жирный
    t = t.replace(/\*([^*]+)\*/g, "$1"); // курсив
    t = t.replace(/~~([^~]+)~~/g, "$1"); // зачёркнутый
    t = t.replace(/`/g, "");
    if (t.startsWith("#")) {
      t = t.replace(/^#{1,6}\s*/, "").toUpperCase();
      if (!t) continue;
      lines.push("", "▼ " + t);
      continue;
    }
    if (t.startsWith("|")) {
      t = t.split("|").map((c) => c.trim()).filter(Boolean).join(" · ");
      lines.push("• " + t);
      continue;
    }
    t = t.replace(/^[-*+]\s+/, "• ");
    t = t.replace(/^>\s?/, "„ ");
    lines.push(t);
  }
  return lines.join("\n").replace(/\n{3,}/g, "\n\n").trim();
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

// запуск opencode TUI с префиллом промта + автопередача через задержку
// (запасной путь, если быстрый `run -i` не поднял интерактив)
function sendOpencodePrompt(term, command) {
  term.sendText(command, true);
  setTimeout(() => {
    try {
      if (!term.disposed) term.sendText("\r");
    } catch (_) { /* терминал уже закрыт */ }
  }, 5000);
}

// запуск полноценного opencode TUI (как раньше на кнопке «Анализ»): пользователь
// оказывается внутри оболочки opencode, промт подставлен в поле ввода, через 5 с
// расширение шлёт Enter — задача уходит сама, без ручного нажатия.
// (`opencode run -i` не используем: это минимальный режим со стримингом в консоль,
// а не оболочка TUI.)
function startSession(term, tmpFile) {
  sendOpencodePrompt(term, `opencode --agent plan --prompt (Get-Content '${tmpFile}' -Raw)`);
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
    // мгновенный запуск сессии с промтом (с автофолбэком на TUI)
    startSession(t, promptFile);
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

// все ли чекбоксы Acceptance в issue отмечены — сигнал «работа сделана, ждёт апрува»
function allAcceptanceDone(body) {
  const parsed = parseBody(body);
  return parsed.acceptance.length > 0 && parsed.acceptance.every((a) => a.done);
}

/**
 * Жизненный цикл: в процессе (спиннер) → сделана, ждёт апрува (⚠) → закрыта.
 * «Сделана» пока сессия открыта = агент закончил (тишина в терминале);
 * новый вывод терминала возвращает задачу «в процессе».
 */
function classifyTask(num, body) {
  const r = running();
  const v = verified();
  const sessionOpen = !!r[num];
  const manuallyDone = !!v[num];
  const allChecked = allAcceptanceDone(body);
  const active = isAgentActive(num);

  // Жизненный цикл: не начата → выполняется (агент активен) → ждёт апрува
  // (агент перестал работать / сессия закрыта) → закрыта (апрув).
  // Статуса «прервана» больше нет: сессия умерла = агент сейчас не работает.
  const started = sessionOpen || manuallyDone || !!sessions()[num];
  const isRunning = sessionOpen && active !== false; // null = сигнала нет, считаем работой
  const isDone = !isRunning && (started || allChecked);

  return {
    sessionOpen,
    isRunning,
    isDone,
    isInterrupted: false,
  };
}

// ---------- body parser ----------

// первый «#NN» в тексте строки → номер issue для клика-перехода
function refOf(text) {
  const m = /#(\d+)\b/.exec(String(text || ""));
  return m ? Number(m[1]) : null;
}

// парсим ВСЕ секции `## Заголовок`, как их показывает GitHub:
// Суть и Acceptance — как раньше, остальные (Подзадачи, Правила/Ограничения,
// Refs…) — списком пунктов; `- [ ]` → чекбокс-пункт.
// Agent context (EN) в панель НЕ выводим — служебная секция для агента,
// в промте и на GitHub она остаётся
function parseBody(body) {
  const r = { essence: "", acceptance: [], extra: [] };
  if (!body) return r;
  const parts = String(body).split(/^##\s+/m).slice(1);
  for (const part of parts) {
    const nl = part.indexOf("\n");
    const title = (nl === -1 ? part : part.slice(0, nl)).trim();
    const content = nl === -1 ? "" : part.slice(nl + 1);
    if (/^Agent context\b/i.test(title)) continue;
    if (/^refs?\b|^ссылк/i.test(title)) continue; // служебные ссылки — не для панели
    if (/^Суть/i.test(title)) { r.essence = content.trim(); continue; }
    if (/^Acceptance/i.test(title)) {
      for (const m of content.matchAll(/^\s*-\s*\[( |x|X)\]\s*(.+)$/gm))
        r.acceptance.push({ done: m[1].toLowerCase() === "x", text: m[2].trim() });
      continue;
    }
    // хвостовые строки «Refs: …» в любом месте тела — в панель не выводим
    const clean = content.replace(/^\s*Refs?\b:.*$/gim, "");
    const items = [];
    let rest = clean;
    for (const m of clean.matchAll(/^\s*-\s*\[( |x|X)\]\s*(.+)$/gm)) {
      items.push({ done: m[1].toLowerCase() === "x", text: m[2].trim(), ref: refOf(m[2]) });
      rest = rest.replace(m[0], "");
    }
    if (!items.length) {
      // секция без чекбоксов → один пункт с очищенным markdown-текстом
      const text = mdClean(clean);
      if (text) items.push({ done: null, text, ref: null });
      if (!text) continue;
    } else {
      const tail = mdClean(rest.replace(/^\s*[-*+]\s+.*$/gm, "").trim());
      if (tail) items.push({ done: null, text: tail, ref: null });
    }
    r.extra.push({ title, items });
  }
  return r;
}

// иконки секций по названию (как на GitHub — свои заголовки)
const SECTION_ICONS = [
  [/подзадач/i, "checklist", "charts.green"],
  [/правил|ограничен/i, "law", "charts.purple"],
  [/refs|ссылк/i, "references", "charts.orange"],
];

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

      // clean verified: issue closed
      const v = verified();
      let vDirty = false;
      for (const k of Object.keys(v)) {
        if (!nums.has(Number(k))) { delete v[k]; vDirty = true; }
      }
      if (vDirty) await setVerified(v);

      // clean sessions: issue closed
      const ses = sessions();
      let sDirty = false;
      for (const k of Object.keys(ses)) {
        if (!nums.has(Number(k))) { delete ses[k]; sDirty = true; }
      }
      if (sDirty) await setSessions(ses);

      this.error = null;
    } catch (e) {
      logError("load", e);
      this.error = String(e.message || e);
    }
    this._emitter.fire();
  }

  getTreeItem(row) {
    if (row.kind === "error") return new vscode.TreeItem(`⚠ ${row.label}`);
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
    if (row.kind === "group") {
      // раскрывающаяся группа (приоритет / сделанные)
      const g = new vscode.TreeItem(
        row.label,
        row.expanded ? vscode.TreeItemCollapsibleState.Expanded : vscode.TreeItemCollapsibleState.Collapsed);
      g.iconPath = new vscode.ThemeIcon(row.icon, new vscode.ThemeColor(row.color));
      g.contextValue = "group";
      return g;
    }
    // ---- issue row: плоская строка-пульт, раскрытия нет ----
    const tier = row.prio || NORMAL_TIER;
    const cls = row.cls || {};
    const isRunning = !!cls.isRunning;
    const inDonePool = !!cls.isDone;

    const item = new vscode.TreeItem(`#${row.num} ${row.title}`, vscode.TreeItemCollapsibleState.None);
    // справа: прогресс чеклиста (✓ n/m) + метки — весь сигнал в одной строке
    const descParts = [];
    if (row.accTotal > 0) descParts.push(`✓ ${row.accDone}/${row.accTotal}`);
    descParts.push(...labelNamesOf(row, 2));
    if (descParts.length > 0) {
      item.description = descParts.join(" · ");
    }
    // состояние+номер в contextValue: инлайн-кнопки на строке и when-фильтры
    item.contextValue = inDonePool ? `issue:review:${row.num}`
      : isRunning ? `issue:running:${row.num}`
      : `issue:ready:${row.num}`;
    // клик по задаче → markdown-превью на всю ширину вкладки
    item.command = {
      command: "barakiTaskLauncher.previewIssue",
      arguments: [row.num],
      title: "Предпросмотр задачи",
    };

    const labels = labelsOf(row);
    const labelNames = labels.map((l) => `\`${l.name}\``).join(" · ");
    // тултип при наведении: статус + «Суть» из issue — быстрый взгляд без превью
    let statusText;
    if (inDonePool) {
      item.iconPath = new vscode.ThemeIcon("warning", new vscode.ThemeColor("charts.yellow"));
      statusText = "**Ожидает проверки и апрува** — ⇱ откроет сессию opencode; ✕ закроет задачу и сессию";
    } else if (isRunning) {
      item.iconPath = new vscode.ThemeIcon("sync~spin", new vscode.ThemeColor("charts.yellow"));
      statusText = "**Выполняется** — агент работает в сессии opencode";
    } else if (tier.key === "blocked") {
      item.iconPath = new vscode.ThemeIcon("circle-slash", new vscode.ThemeColor("charts.red"));
      statusText = "**Заблокирована** — ждёт другую задачу/фазу";
    } else {
      item.iconPath = new vscode.ThemeIcon(tier.icon, new vscode.ThemeColor(tier.themeColor));
      statusText = `**${tier.header}**`;
    }
    // тултип = статус + «Суть» (быстрый взгляд без открытия превью)
    const tipParts = [statusText];
    if (labelNames) tipParts.push(`Метки: ${labelNames}`);
    if (row.essence) tipParts.push("---\n\n" + String(row.essence).slice(0, 800));
    const tip = new vscode.MarkdownString(tipParts.join("\n\n"));
    tip.isTrusted = false;
    item.tooltip = tip;
    return item;
  }

  async getChildren(element) {
    if (this.error) return [{ kind: "error", label: this.error.slice(0, 100) }];
    if (!this._loaded) { await this.load(); this._loaded = true; }


    // ---- children of a collapsible group ----
    if (element && element.kind === "group") {
      return element.items;
    }

    // задачи не раскрываются: строка = весь пульт (клик → markdown-превью),
    // детали — в тултипе при наведении, полный вид — во вкладке-превью

    // ---- top level: раскрывающиеся группы по приоритету + пул «сделанные» ----
    // текстовая шапка «ЗАДАЧИ — N» не нужна: имя секции уже рисует Cursor
    const out = [];

    const buckets = new Map();
    const put = (key, row) => {
      if (!buckets.has(key)) buckets.set(key, []);
      buckets.get(key).push(row);
    };

    for (const i of this.open) {
      const tier = tierOf(i);
      const cls = classifyTask(i.number, i.body);
      const parsed = parseBody(i.body);
      const accDone = parsed.acceptance.filter((a) => a.done).length;
      const row = {
        kind: "open",
        num: i.number,
        title: i.title,
        labels: labelsOf(i),
        prio: tier,
        cls,
        accDone,
        accTotal: parsed.acceptance.length,
        essence: parsed.essence ? mdClean(parsed.essence) : "",
      };
      put(cls.isDone ? "done" : tier.key, row);
    }

    const pushGroup = (label, icon, color, key, expanded) => {
      const rows = buckets.get(key);
      if (!rows || rows.length === 0) return;
      rows.sort((a, b) => a.num - b.num);
      out.push({
        kind: "group",
        label: `${label} — ${rows.length}`,
        icon, color, expanded,
        items: rows,
      });
    };

    pushGroup("ГОРИТ — СДЕЛАТЬ СЕЙЧАС", "flame", "charts.red", TIERS[0].key, true);
    pushGroup(TIERS[1].header, TIERS[1].icon, TIERS[1].themeColor, TIERS[1].key, true);
    pushGroup("ПО ОЧЕРЕДИ", NORMAL_TIER.icon, NORMAL_TIER.themeColor, NORMAL_TIER.key, true);
    pushGroup("НИЗКИЙ ПРИОРИТЕТ", TIERS[2].icon, TIERS[2].themeColor, TIERS[2].key, false);
    pushGroup("ЗАБЛОКИРОВАНО", BLOCKED_TIER.icon, "charts.red", BLOCKED_TIER.key, false);
    pushGroup("СДЕЛАННЫЕ — ЖДУТ АПРУВА", "warning", "charts.yellow", "done", true);
    return out;
  }
}

// ---------- быстрый ввод: свой промт → opencode (вьюха под списком задач) ----------
const QUICK_TERM = "opencode задача";

class QuickPromptView {
  // actions: { last, summary, refresh } — квадратные кнопки справа от «ОТКРЫТЬ В OPENCODE»
  constructor(actions) { this._actions = actions || {}; }

  resolveWebviewView(view) {
    view.webview.options = { enableScripts: true };
    view.webview.html = `<!DOCTYPE html>
<html>
<head>
<meta charset="utf-8">
<style>
  :root { --gap: 8px; }
  body {
    margin: var(--gap);
    display: flex; flex-direction: column; gap: var(--gap);
    font-family: var(--vscode-font-family); font-size: var(--vscode-font-size);
    color: var(--vscode-foreground);
  }
  textarea {
    width: 100%; box-sizing: border-box; min-height: 64px; resize: vertical;
    color: var(--vscode-input-foreground); background: var(--vscode-input-background);
    border: 1px solid var(--vscode-input-border, transparent); border-radius: 2px;
    padding: 6px; outline: none; font-family: inherit;
  }
  textarea:focus { border-color: var(--vscode-focusBorder); }
  .row { display: flex; align-items: center; gap: var(--gap); }
  button {
    flex: none;
    color: var(--vscode-button-foreground); background: var(--vscode-button-background);
    border: none; border-radius: 2px; padding: 5px 12px; cursor: pointer;
    font-family: inherit; font-size: var(--vscode-font-size);
  }
  button:hover { background: var(--vscode-button-hoverBackground); }
  /* квадратные кнопки действий — тот же ghost-стиль, что и в списке задач */
  .sq {
    width: 26px; height: 26px; padding: 0;
    display: inline-flex; align-items: center; justify-content: center;
    font-size: 13px; line-height: 1;
    color: var(--vscode-foreground);
    background: var(--vscode-button-secondaryBackground, rgba(128,128,128,.17));
    border: 1px solid var(--vscode-widget-border, transparent);
  }
  .sq:hover {
    background: var(--vscode-button-secondaryHoverBackground, rgba(128,128,128,.25));
    border-color: var(--vscode-focusBorder, transparent);
  }
  .sp { flex: 1; }
</style>
</head>
<body>
  <textarea id="prompt" placeholder="Что сделать? Опишите задачу для opencode… (Ctrl+Enter — отправить)"></textarea>
  <div class="row">
    <button id="go">ОТКРЫТЬ В OPENCODE</button>
    <span class="sp"></span>
    <button id="last" class="sq" title="Открыть последнюю сессию opencode">↺</button>
    <button id="summary" class="sq" title="Анализ проекта и выбор следующей задачи">✦</button>
    <button id="refresh" class="sq" title="Обновить список задач">↻</button>
  </div>
<script>
  const vscode = acquireVsCodeApi();
  const ta = document.getElementById("prompt");
  function send() {
    const text = ta.value.trim();
    if (!text) return;
    vscode.postMessage({ type: "launch", text });
    ta.value = "";
  }
  document.getElementById("go").addEventListener("click", send);
  ta.addEventListener("keydown", (e) => {
    if ((e.ctrlKey || e.metaKey) && e.key === "Enter") { e.preventDefault(); send(); }
  });
  for (const id of ["last", "summary", "refresh"])
    document.getElementById(id).addEventListener("click", () => vscode.postMessage({ cmd: id }));
</script>
</body>
</html>`;
    view.webview.onDidReceiveMessage((m) => {
      if (!m) return;
      if (m.type === "launch" && typeof m.text === "string" && m.text.trim()) {
        try {
          const tmpFile = path.join(require("os").tmpdir(), "baraki-custom-prompt.txt");
          fs.writeFileSync(tmpFile, m.text.trim(), "utf-8");
          let t = vscode.window.terminals.find((x) => x.name === QUICK_TERM);
          if (!t) {
            t = vscode.window.createTerminal({
              name: QUICK_TERM,
              cwd: root(),
              location: { viewColumn: vscode.ViewColumn.One }
            });
          }
          t.show(true);
          startSession(t, tmpFile);
        } catch (e) {
          logError("quick-prompt-launch", e);
          vscode.window.showErrorMessage(`Быстрая задача: ${e.message}`);
        }
        return;
      }
      if (typeof m.cmd === "string") {
        const fn = this._actions[m.cmd];
        if (fn) Promise.resolve(fn()).catch(() => {});
      }
    });
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

    // терминал открывается вкладкой в editor area; мгновенный запуск сессии с промтом
    const term = vscode.window.createTerminal({
      name: `opencode #${num}`,
      cwd: root(),
      location: { viewColumn: vscode.ViewColumn.One },
      iconPath: new vscode.ThemeIcon("rocket")
    });
    term.show(true);
    const tmpFile = path.join(require("os").tmpdir(), `baraki-task-${num}.txt`);
    fs.writeFileSync(tmpFile, prompt, "utf-8");
    startSession(term, tmpFile);
    captureSessionIdLater(num, data ? data.title : "", Date.now());

    vscode.window.setStatusBarMessage(`opencode запущен: #${num}`, 5000);
    provider.refresh();
  }

  // ---- открыть существующую сессию opencode (без промта, без новой сессии) ----
  // Живой терминал → просто фокус. После перезапуска Cursor — резолвим id сессии
  // через `opencode session list` и открываем `opencode --session <id>`.
  async function resumeTask(arg) {
    const num = numFrom(arg);
    if (!num) { vscode.window.showErrorMessage(`[${VERSION}] Выберите строку задачи и нажмите ⇱`); return; }

    const term = vscode.window.terminals.find((t) => t.name === `opencode #${num}`);
    if (term) {
      term.show(true);
      vscode.window.setStatusBarMessage(`opencode: сессия #${num} уже открыта`, 3000);
      return;
    }

    const issue = provider.open.find((i) => i.number === num);
    const sid = await resolveSessionId(num, issue ? issue.title : "");
    if (!sid) {
      vscode.window.showInformationMessage(
        `Сессия opencode для #${num} не найдена — запустите задачу заново (▶).`);
      return;
    }

    // открытие сессии НЕ меняет статус: агент ещё ничего не делает в ней.
    // «Выполняется» включится сам, как только пойдёт активность в терминале.
    const t2 = vscode.window.createTerminal({
      name: `opencode #${num}`,
      cwd: root(),
      location: { viewColumn: vscode.ViewColumn.One },
      iconPath: new vscode.ThemeIcon("rocket")
    });
    t2.show(true);
    t2.sendText(`opencode --session ${sid}`, true);

    vscode.window.setStatusBarMessage(`opencode: сессия #${num} восстановлена`, 5000);
    provider.refresh();
  }

  // ---- открыть последнюю сессию opencode ----
  // «Последняя» = ПЕРВАЯ СВЕРХУ в списке `opencode session list` (как её
  // показывает сам opencode). Привязываем к задаче по сохранённым id — тогда
  // статус панели обновляется тем же путём, что у ⇱; непривязанная сессия
  // открывается как есть.
  async function resumeLastTask() {
    let sid = null;
    try {
      const raw = await ocExec(["session", "list", "--format", "json", "--max-count", "300"]);
      const all = JSON.parse(raw);
      const dir = root().toLowerCase();
      const mine = (Array.isArray(all) ? all : []).filter((s) =>
        s && typeof s.id === "string" && String(s.directory || "").toLowerCase() === dir);
      if (!mine.length) {
        vscode.window.showInformationMessage("Сессии opencode для этого проекта не найдены.");
        return;
      }
      sid = mine[0].id;
    } catch (e) {
      vscode.window.showErrorMessage(`[${VERSION}] Список сессий opencode: ${e.message}`);
      return;
    }

    const m = sessions();
    const numStr = Object.keys(m).find((k) => m[k] === sid);
    const num = numStr ? Number(numStr) : null;

    if (num != null) {
      const term = vscode.window.terminals.find((t) => t.name === `opencode #${num}`);
      if (term) {
        term.show(true);
        vscode.window.setStatusBarMessage(`opencode: сессия #${num} уже открыта`, 3000);
        return;
      }
      // открытие сессии НЕ меняет статус — см. resumeTask
      const t2 = vscode.window.createTerminal({
        name: `opencode #${num}`,
        cwd: root(),
        location: { viewColumn: vscode.ViewColumn.One },
        iconPath: new vscode.ThemeIcon("rocket")
      });
      t2.show(true);
      t2.sendText(`opencode --session ${sid}`, true);
      vscode.window.setStatusBarMessage(`opencode: последняя сессия (#${num}) открыта`, 5000);
      provider.refresh();
      return;
    }

    // сессия не привязана к задаче из панели — открываем без смены статусов
    const t3 = vscode.window.createTerminal({
      name: "opencode last",
      cwd: root(),
      location: { viewColumn: vscode.ViewColumn.One },
      iconPath: new vscode.ThemeIcon("rocket")
    });
    t3.show(true);
    t3.sendText(`opencode --session ${sid}`, true);
    vscode.window.setStatusBarMessage("opencode: последняя сессия открыта", 5000);
  }

  // ---- close issue: gh issue close (+ закрыть сессию opencode, если открыта) ----
  async function closeIssue(arg) {
    const num = toNum(arg);
    if (!num) return;
    const confirm = await vscode.window.showWarningMessage(
      `Апрув: закрыть issue #${num}? Открытая сессия opencode тоже будет закрыта.`,
      "Закрыть", "Отмена"
    );
    if (confirm !== "Закрыть") return;
    try {
      await ghExec(["issue", "close", String(num), "-R", cfg().get("repo"), "-c", "Закрыто из панели BARAKI Задачи"]);
      const term = vscode.window.terminals.find((t) => t.name === `opencode #${num}`);
      if (term) term.dispose();
      clearActivity(num);
      vscode.window.setStatusBarMessage(`#${num} закрыт (апрув)`, 3000);
      provider.refresh();
    } catch (e) {
      vscode.window.showErrorMessage(`Ошибка: ${e.message}`);
    }
  }

  const treeView = vscode.window.createTreeView("barakiTasks.taskList", {
    treeDataProvider: provider, showCollapseAll: false
  });
  context.subscriptions.push(
    vscode.window.registerWebviewViewProvider("barakiTasks.quickPrompt", new QuickPromptView({
      last: () => resumeLastTask(),
      summary: () => generateSummaryInTerminal(),
      refresh: () => provider.refresh(),
    }))
  );

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

  // активность агента: вывод терминала opencode. Тишина > AGENT_IDLE_MS → «сделана»,
  // новый вывод → обратно «в процессе». API proposed — в стабильном Cursor недоступен
  // и кидает при обращении: доступ только через try, иначе фолбэк по чекбоксам.
  try {
    if (typeof vscode.window.onDidWriteTerminalData === "function") {
      context.subscriptions.push(vscode.window.onDidWriteTerminalData((e) => {
        const m = /^opencode #(\d+)$/.exec((e && e.terminal && e.terminal.name) || "");
        if (!m) return;
        noteTerminalActivity(Number(m[1]));
      }));
      output.appendLine("terminal activity monitor: on");
    } else {
      output.appendLine("terminal activity monitor: unavailable (stable API) — checkbox fallback");
    }
  } catch (e) {
    output.appendLine("terminal activity monitor unavailable: " + e);
  }

  context.subscriptions.push(
    vscode.workspace.registerTextDocumentContentProvider(ISSUE_SCHEME, new IssueContentProvider()),
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
      closeIssue(num);
    }),
    vscode.commands.registerCommand("barakiTaskLauncher.closeTask", (arg) => {
      const num = numFrom(arg);
      if (!num) { vscode.window.showErrorMessage(`[${VERSION}] Выберите строку задачи и нажмите ✕`); return; }
      closeIssue(num);
    }),
    vscode.commands.registerCommand("barakiTaskLauncher.previewIssue", (arg) => {
      const num = typeof arg === "number" ? arg : numFrom(arg);
      if (!num) { vscode.window.showErrorMessage(`[${VERSION}] Выберите задачу и нажмите 📖`); return; }
      previewIssue(num);
    }),
    vscode.commands.registerCommand("barakiTaskLauncher.resumeTask", (arg) => resumeTask(arg)),
    vscode.commands.registerCommand("barakiTaskLauncher.resumeLastTask", () => resumeLastTask()),
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
