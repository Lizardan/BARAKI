var __defProp = Object.defineProperty;
var __name = (target, value) => __defProp(target, "name", { value, configurable: true });

// src/index.js
var TUNNEL_KEY = "tunnel_wss";
var MATCH_KEY = "active_match";
var index_default = {
  async fetch(request, env) {
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
        return cors(json({ ok: true, has_tunnel: Boolean(tunnel) }));
      }
      return cors(json({ error: "not_found" }, 404));
    } catch (err) {
      return cors(json({ error: String(err && err.message ? err.message : err) }, 500));
    }
  }
};
function normalizeApiPath(pathname) {
  const trimmed = pathname.replace(/\/+$/, "") || "/";
  if (trimmed === "/v1" || trimmed.startsWith("/v1/")) {
    return "/api" + trimmed;
  }
  return trimmed;
}
__name(normalizeApiPath, "normalizeApiPath");
async function getTunnel(env) {
  if (!env.BARAKI_STORE) {
    return null;
  }
  return env.BARAKI_STORE.get(TUNNEL_KEY);
}
__name(getTunnel, "getTunnel");
async function setTunnel(env, wssUrl) {
  await env.BARAKI_STORE.put(TUNNEL_KEY, wssUrl);
}
__name(setTunnel, "setTunnel");
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
__name(getStoredMatch, "getStoredMatch");
async function setStoredMatch(env, match) {
  await env.BARAKI_STORE.put(MATCH_KEY, JSON.stringify(match));
}
__name(setStoredMatch, "setStoredMatch");
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
  return json({ ok: true, wss_url: wssUrl });
}
__name(registerTunnel, "registerTunnel");
async function ensureMatch(request, env) {
  const body = await request.json();
  const instanceId = String(body.instance_id || "").trim();
  const playerCount = clampPlayerCount(body.player_count);
  const discordUserId = String(body.discord_user_id || body.display_name || "player").trim();
  if (!instanceId) {
    return json({ error: "instance_id required" }, 400);
  }
  const wssUrl = await getTunnel(env);
  if (!wssUrl) {
    return json({ error: "tunnel_not_registered", hint: "POST /api/v1/admin/register-tunnel first" }, 503);
  }
  let match = await getStoredMatch(env);
  if (!match || match.instance_id !== instanceId) {
    match = {
      match_id: crypto.randomUUID(),
      instance_id: instanceId,
      player_count: playerCount,
      wss_url: wssUrl,
      slots: {},
      created_at: Date.now()
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
    room_code: match.instance_id.slice(0, 8).toUpperCase()
  });
}
__name(ensureMatch, "ensureMatch");
async function getMatch(env, instanceId) {
  const match = await getStoredMatch(env);
  if (!match || match.instance_id !== instanceId) {
    return json({ error: "not_found" }, 404);
  }
  return json({
    match_id: match.match_id,
    wss_url: match.wss_url,
    player_count: match.player_count,
    occupied_slots: Object.keys(match.slots).length
  });
}
__name(getMatch, "getMatch");
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
__name(assignSlot, "assignSlot");
function clampPlayerCount(value) {
  const n = Number(value) || 2;
  if (n === 2 || n === 4) {
    return n;
  }
  return 2;
}
__name(clampPlayerCount, "clampPlayerCount");
function json(data, status = 200) {
  return new Response(JSON.stringify(data), {
    status,
    headers: { "Content-Type": "application/json" }
  });
}
__name(json, "json");
function cors(response) {
  const headers = new Headers(response.headers);
  headers.set("Access-Control-Allow-Origin", "*");
  headers.set("Access-Control-Allow-Methods", "GET,POST,OPTIONS");
  headers.set("Access-Control-Allow-Headers", "Content-Type,Authorization");
  return new Response(response.body, { status: response.status, headers });
}
__name(cors, "cors");
export {
  index_default as default
};
//# sourceMappingURL=index.js.map
