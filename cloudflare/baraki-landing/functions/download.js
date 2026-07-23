const InstallerKey = "BARAKI-Setup.exe";
const InstallerFileName = "BARAKI-Setup.exe";

export async function onRequest(context) {
  if (context.request.method !== "GET" && context.request.method !== "HEAD") {
    return new Response("Method Not Allowed", {
      status: 405,
      headers: { Allow: "GET, HEAD" },
    });
  }

  const installer = await context.env.BARAKI_RELEASES.get(InstallerKey);
  if (installer === null) {
    return new Response("Installer is not published yet.", { status: 404 });
  }

  const headers = new Headers();
  installer.writeHttpMetadata(headers);
  headers.set("Content-Type", "application/vnd.microsoft.portable-executable");
  headers.set("Content-Disposition", `attachment; filename="${InstallerFileName}"`);
  headers.set("Cache-Control", "public, max-age=300");
  headers.set("ETag", installer.httpEtag);

  return new Response(context.request.method === "HEAD" ? null : installer.body, {
    headers,
  });
}
