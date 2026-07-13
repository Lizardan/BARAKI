/**
 * BARAKI FREE-0 matchmaker (single active match).
 * Host registers wss_url; Discord clients ensure/join by instance_id.
 */

const MATCH_KEY = "active_match";
const TUNNEL_KEY = "tunnel_wss";

export default {
  async fetch(request, env) {
    const url = new URL(request.url);
    const path = url.pathname.replace(/\/+$/, "") || "/";

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
        return cors(await getMatch(instanceId, env));
      }

      if (request.method === "GET" && path === "/api/v1/health") {
        return cors(json({ ok: true }));
      }

      return cors(json({ error: "not_found" }, 404));
    } catch (err) {
      return cors(json({ error: String(err && err.message ? err.message : err) }, 500));
    }
  },
};

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

  await env.BARAKI_KV.put(TUNNEL_KEY, wssUrl);
  return json({ ok: true, wss_url: wssUrl });
}

async function ensureMatch(request, env) {
  const body = await request.json();
  const instanceId = String(body.instance_id || "").trim();
  const playerCount = clampPlayerCount(body.player_count);
  const discordUserId = String(body.discord_user_id || body.display_name || "player").trim();

  if (!instanceId) {
    return json({ error: "instance_id required" }, 400);
  }

  const wssUrl = await env.BARAKI_KV.get(TUNNEL_KEY);
  if (!wssUrl) {
    return json({ error: "tunnel_not_registered", hint: "POST /api/v1/admin/register-tunnel first" }, 503);
  }

  let match = await readMatch(env);
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
  await writeMatch(env, match);

  return json({
    match_id: match.match_id,
    wss_url: match.wss_url,
    join_token: `${match.match_id}:${slot}`,
    slot,
    player_count: match.player_count,
    room_code: match.instance_id.slice(0, 8).toUpperCase(),
  });
}

async function getMatch(instanceId, env) {
  const match = await readMatch(env);
  if (!match || match.instance_id !== instanceId) {
    return json({ error: "not_found" }, 404);
  }

  return json({
    match_id: match.match_id,
    wss_url: match.wss_url,
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

async function readMatch(env) {
  const raw = await env.BARAKI_KV.get(MATCH_KEY);
  return raw ? JSON.parse(raw) : null;
}

async function writeMatch(env, match) {
  await env.BARAKI_KV.put(MATCH_KEY, JSON.stringify(match));
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
