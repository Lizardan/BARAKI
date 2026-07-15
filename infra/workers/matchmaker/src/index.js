/**
 * BARAKI FREE-0 matchmaker (single active match).
 * Tunnel + match stored in KV (shared across isolates).
 * Host registers wss_url (ephemeral quick tunnel OK);
 * Discord clients get a stable PUBLIC_WSS_HOST and this Worker proxies /wss → tunnel.
 *
 * Discord URL Mapping `/api` strips that prefix, so Activity calls arrive as `/v1/...`.
 * Direct `*.workers.dev` calls use `/api/v1/...`. Accept both.
 * Discord URL Mapping `/wss` → this Worker hostname (once); WS upgrades are proxied.
 */

import { resolveClientWssUrl } from "./publicWss.js";
import { proxyGameWebSocket } from "./wsProxy.js";

const TUNNEL_KEY = "tunnel_wss";
const MATCH_KEY = "active_match";

export default {
  async fetch(request, env) {
    // Stable Discord /wss target: any WebSocket upgrade proxies to registered tunnel.
    if ((request.headers.get("Upgrade") || "").toLowerCase() === "websocket") {
      const tunnel = await getTunnel(env);
      if (!tunnel) {
        return cors(json(
          { error: "tunnel_not_registered", hint: "POST /api/v1/admin/register-tunnel first" },
          503));
      }

      return proxyGameWebSocket(request, tunnel);
    }

    const url = new URL(request.url);
    const path = normalizeApiPath(url.pathname);

    if (request.method === "OPTIONS") {
      return cors(new Response(null, { status: 204 }));
    }

    try {
      if (request.method === "POST" && path === "/api/v1/admin/register-tunnel") {
        return cors(await registerTunnel(request, env));
      }

      if (request.method === "POST" && path === "/api/v1/match/ensure") {
        return cors(await ensureMatch(request, env));
      }

      if (request.method === "GET" && path.startsWith("/api/v1/match/")) {
        const instanceId = decodeURIComponent(path.slice("/api/v1/match/".length));
        return cors(await getMatch(env, instanceId));
      }

      if (request.method === "GET" && path === "/api/v1/health") {
        const tunnel = await getTunnel(env);
        const publicWss = resolveClientWssUrl(env.PUBLIC_WSS_HOST, tunnel);
        return cors(json({
          ok: true,
          has_tunnel: Boolean(tunnel),
          public_wss: publicWss || null,
          proxy: Boolean(env.PUBLIC_WSS_HOST),
        }));
      }

      return cors(json({ error: "not_found" }, 404));
    } catch (err) {
      return cors(json({ error: String(err && err.message ? err.message : err) }, 500));
    }
  },
};

/** Discord `/api` mapping forwards `/api/v1/x` as `/v1/x`; normalize to `/api/v1/x`. */
function normalizeApiPath(pathname) {
  const trimmed = pathname.replace(/\/+$/, "") || "/";
  if (trimmed === "/v1" || trimmed.startsWith("/v1/")) {
    return "/api" + trimmed;
  }

  return trimmed;
}

async function getTunnel(env) {
  if (!env.BARAKI_STORE) {
    return null;
  }

  return env.BARAKI_STORE.get(TUNNEL_KEY);
}

async function setTunnel(env, wssUrl) {
  await env.BARAKI_STORE.put(TUNNEL_KEY, wssUrl);
}

async function getStoredMatch(env) {
  const raw = await env.BARAKI_STORE.get(MATCH_KEY);
  if (!raw) {
    return null;
  }

  try {
    return JSON.parse(raw);
  } catch {
    return null;
  }
}

async function setStoredMatch(env, match) {
  await env.BARAKI_STORE.put(MATCH_KEY, JSON.stringify(match));
}

async function registerTunnel(request, env) {
  const auth = request.headers.get("Authorization") || "";
  const secret = env.REGISTER_SECRET || "";
  if (!secret || auth !== `Bearer ${secret}`) {
    return json({ error: "unauthorized" }, 401);
  }

  const body = await request.json();
  const wssUrl = String(body.wss_url || "").trim();
  if (!wssUrl.startsWith("ws://") && !wssUrl.startsWith("wss://")) {
    return json({ error: "wss_url must start with ws:// or wss://" }, 400);
  }

  await setTunnel(env, wssUrl);
  const publicWss = resolveClientWssUrl(env.PUBLIC_WSS_HOST, wssUrl);
  return json({ ok: true, wss_url: wssUrl, public_wss: publicWss || null });
}

async function ensureMatch(request, env) {
  const body = await request.json();
  const instanceId = String(body.instance_id || "").trim();
  const playerCount = clampPlayerCount(body.player_count);
  const discordUserId = String(body.discord_user_id || body.display_name || "player").trim();

  if (!instanceId) {
    return json({ error: "instance_id required" }, 400);
  }

  const tunnelWss = await getTunnel(env);
  if (!tunnelWss) {
    return json({ error: "tunnel_not_registered", hint: "POST /api/v1/admin/register-tunnel first" }, 503);
  }

  const wssUrl = resolveClientWssUrl(env.PUBLIC_WSS_HOST, tunnelWss);

  let match = await getStoredMatch(env);
  if (!match || match.instance_id !== instanceId) {
    match = {
      match_id: crypto.randomUUID(),
      instance_id: instanceId,
      player_count: playerCount,
      wss_url: wssUrl,
      slots: {},
      created_at: Date.now(),
    };
  } else {
    match.wss_url = wssUrl;
    match.player_count = playerCount || match.player_count;
  }

  const slot = assignSlot(match, discordUserId);
  await setStoredMatch(env, match);

  return json({
    match_id: match.match_id,
    wss_url: match.wss_url,
    join_token: `${match.match_id}:${slot}`,
    slot,
    player_count: match.player_count,
    room_code: match.instance_id.slice(0, 8).toUpperCase(),
  });
}

async function getMatch(env, instanceId) {
  const match = await getStoredMatch(env);
  if (!match || match.instance_id !== instanceId) {
    return json({ error: "not_found" }, 404);
  }

  const tunnelWss = await getTunnel(env);
  const wssUrl = resolveClientWssUrl(env.PUBLIC_WSS_HOST, tunnelWss || match.wss_url);

  return json({
    match_id: match.match_id,
    wss_url: wssUrl,
    player_count: match.player_count,
    occupied_slots: Object.keys(match.slots).length,
  });
}

function assignSlot(match, discordUserId) {
  for (const [slot, userId] of Object.entries(match.slots)) {
    if (userId === discordUserId) {
      return Number(slot);
    }
  }

  const max = match.player_count || 2;
  for (let i = 0; i < max; i++) {
    if (match.slots[String(i)] == null) {
      match.slots[String(i)] = discordUserId;
      return i;
    }
  }

  throw new Error("lobby_full");
}

function clampPlayerCount(value) {
  const n = Number(value) || 2;
  if (n === 2 || n === 4) {
    return n;
  }

  return 2;
}

function json(data, status = 200) {
  return new Response(JSON.stringify(data), {
    status,
    headers: { "Content-Type": "application/json" },
  });
}

function cors(response) {
  const headers = new Headers(response.headers);
  headers.set("Access-Control-Allow-Origin", "*");
  headers.set("Access-Control-Allow-Methods", "GET,POST,OPTIONS");
  headers.set("Access-Control-Allow-Headers", "Content-Type,Authorization");
  return new Response(response.body, { status: response.status, headers });
}
