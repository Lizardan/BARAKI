/**
 * Stable Discord /wss target (Worker hostname). Clients never see ephemeral trycloudflare URLs.
 * @param {string|undefined|null} host
 * @returns {string} wss://host or empty if unset
 */
export function buildPublicWssUrl(host) {
  const trimmed = String(host || "").trim();
  if (!trimmed) {
    return "";
  }

  const withoutScheme = trimmed
    .replace(/^(wss?|https?):\/\//i, "")
    .replace(/\/+$/, "");
  if (!withoutScheme || withoutScheme.includes("/")) {
    return "";
  }

  return `wss://${withoutScheme}`;
}

/**
 * Prefer stable public URL when configured; otherwise fall back to registered tunnel.
 * @param {string|undefined|null} publicHost
 * @param {string|undefined|null} tunnelWssUrl
 */
export function resolveClientWssUrl(publicHost, tunnelWssUrl) {
  const publicUrl = buildPublicWssUrl(publicHost);
  if (publicUrl) {
    return publicUrl;
  }

  return String(tunnelWssUrl || "").trim();
}
