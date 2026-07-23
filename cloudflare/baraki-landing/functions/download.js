const InstallerUrl =
  "https://github.com/Lizardan/BARAKI/releases/download/updater-v0.1.0/BARAKI-Setup.exe";

export function onRequest() {
  return Response.redirect(InstallerUrl, 302);
}
