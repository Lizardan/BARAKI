const InstallerUrl =
  "https://github.com/Lizardan/BARAKI/releases/latest";

export function onRequest() {
  return Response.redirect(InstallerUrl, 302);
}
