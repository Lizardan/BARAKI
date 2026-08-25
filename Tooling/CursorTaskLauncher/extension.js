const vscode = require("vscode");
const { execFile, spawn } = require("child_process");
const path = require("path");

function activate(context) {
  const cfg = () => vscode.workspace.getConfiguration("barakiTaskLauncher");
  const root = () =>
    (vscode.workspace.workspaceFolders && vscode.workspace.workspaceFolders[0]
      ? vscode.workspace.workspaceFolders[0].uri.fsPath
      : process.cwd());

  function gh(args) {
    return new Promise((resolve, reject) => {
      execFile(
        "gh",
        args,
        { cwd: root(), windowsHide: true, maxBuffer: 4 * 1024 * 1024 },
        (err, stdout, stderr) => {
          if (err) {
            reject(new Error(stderr || err.message));
          } else {
            resolve(stdout);
          }
        }
      );
    });
  }

  function detectActiveIssueNumber() {
    const ed = vscode.window.activeTextEditor;
    if (!ed) {
      return null;
    }
    const uri = ed.document.uri.toString();
    const name = path.basename(ed.document.uri.path);
    let m = uri.match(/issue[\/\-](\d+)/i) || `${uri} ${name}`.match(/#(\d+)/);
    return m ? parseInt(m[1], 10) : null;
  }

  async function listIssues() {
    const repo = cfg().get("repo");
    const args = [
      "issue", "list", "-R", repo,
      "--state", "open", "--limit", "100",
      "--json", "number,title"
    ];
    const label = cfg().get("label");
    if (label) {
      args.splice(4, 0, "--label", label);
    }
    return JSON.parse(await gh(args));
  }

  function launch(issueNumber) {
    const script = path.resolve(root(), "Tooling", "Start-IssueTask.ps1");
    if (!require("fs").existsSync(script)) {
      vscode.window.showErrorMessage(`Не найден ${script}`);
      return;
    }
    const child = spawn(
      "pwsh",
      ["-NoProfile", "-ExecutionPolicy", "Bypass", "-File", script, "-Issue", String(issueNumber)],
      { cwd: root(), detached: true, stdio: "ignore", windowsHide: false }
    );
    child.unref();
    vscode.window.setStatusBarMessage(`opencode запущен по issue #${issueNumber}`, 5000);
  }

  async function pickAndLaunch() {
    const preselected = detectActiveIssueNumber();
    let issues = [];
    try {
      issues = await listIssues();
    } catch (e) {
      vscode.window.showErrorMessage(`gh issue list не удался: ${e.message}`);
    }

    const items = issues.map((i) => ({
      label: `#${i.number} ${i.title}`,
      number: i.number
    }));

    const manual = { label: "$(keyboard) Ввести номер вручную…", number: null };
    items.unshift(manual);

    const pick = await vscode.window.showQuickPick(items, {
      placeHolder:
        preselected != null
          ? `Запустить в opencode (из активного редактора распознан #${preselected})`
          : "Выбери задачу для запуска в opencode"
    });

    if (!pick) {
      return;
    }
    if (pick.number != null) {
      launch(pick.number);
      return;
    }
    vscode.commands.executeCommand("barakiTaskLauncher.launchIssueFromInput");
  }

  async function launchFromInput() {
    const value = await vscode.window.showInputBox({
      prompt: "Номер GitHub issue для запуска в opencode",
      validateInput: (v) => (/^\d+$/.test(v) ? null : "Только число")
    });
    if (value) {
      launch(parseInt(value, 10));
    }
  }

  context.subscriptions.push(
    vscode.commands.registerCommand("barakiTaskLauncher.launchIssue", pickAndLaunch),
    vscode.commands.registerCommand("barakiTaskLauncher.launchIssueFromInput", launchFromInput)
  );
}

function deactivate() {}

module.exports = { activate, deactivate };
