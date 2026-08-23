/**
 * BARAKI menu chat API (global + friends feed + DM).
 * Auth: optional CHAT_API_KEY secret; clients send X-Baraki-Key.
 * Identity: playtest-grade X-Baraki-Player-Id / X-Baraki-Player-Name headers.
 */
export class ChatHub {
  constructor(state, env) {
    this.state = state;
    this.env = env;
    this.global = [];
    this.feeds = new Map();
    this.dms = new Map();
    this.sessions = new Map();
  }

  async fetch(request) {
    const url = new URL(request.url);
    if (request.method === "OPTIONS") {
      return cors(new Response(null, { status: 204 }));
    }

    if (!authorize(request, this.env)) {
      return cors(json({ error: "unauthorized" }, 401));
    }

    const playerId = (request.headers.get("X-Baraki-Player-Id") || "").trim();
    const displayName = (request.headers.get("X-Baraki-Player-Name") || "Player").trim().slice(0, 64);
    if (!playerId || playerId.length < 4 || playerId.length > 128) {
      return cors(json({ error: "invalid_player" }, 400));
    }

    try {
      if (request.method === "POST" && url.pathname === "/v1/session") {
        const body = await request.json().catch(() => ({}));
        const friendIds = normalizeIdList(body.friendIds);
        this.sessions.set(playerId, { displayName, friendIds, lastSeen: Date.now() });
        return cors(json({ ok: true }));
      }

      if (url.pathname === "/v1/channels/global") {
        if (request.method === "GET") {
          return cors(json({ messages: after(this.global, url.searchParams.get("after")) }));
        }
        if (request.method === "POST") {
          const text = await readText(request);
          if (!text) return cors(json({ error: "empty" }, 400));
          const msg = push(this.global, makeMsg(playerId, displayName, text, "global"));
          return cors(json({ message: msg }));
        }
      }

      if (url.pathname === "/v1/channels/friends") {
        if (request.method === "GET") {
          const session = this.sessions.get(playerId);
          const friendIds =
            session?.friendIds ||
            normalizeIdList((url.searchParams.get("friends") || "").split(","));
          const authors = new Set([playerId, ...friendIds]);
          const merged = [];
          for (const author of authors) {
            const list = this.feeds.get(author) || [];
            for (const m of list) merged.push(m);
          }
          merged.sort((a, b) => a.ts.localeCompare(b.ts));
          return cors(json({ messages: after(merged, url.searchParams.get("after")) }));
        }
        if (request.method === "POST") {
          const text = await readText(request);
          if (!text) return cors(json({ error: "empty" }, 400));
          if (!this.feeds.has(playerId)) this.feeds.set(playerId, []);
          const list = this.feeds.get(playerId);
          const msg = push(list, makeMsg(playerId, displayName, text, "friends"));
          return cors(json({ message: msg }));
        }
      }

      const dmMatch = url.pathname.match(/^\/v1\/dm\/([^/]+)$/);
      if (dmMatch) {
        const peerId = decodeURIComponent(dmMatch[1]).trim();
        if (!peerId || peerId === playerId) return cors(json({ error: "invalid_peer" }, 400));
        const key = conversationKey(playerId, peerId);
        if (!this.dms.has(key)) this.dms.set(key, []);
        const list = this.dms.get(key);
        if (request.method === "GET") {
          return cors(json({ messages: after(list, url.searchParams.get("after")) }));
        }
        if (request.method === "POST") {
          const text = await readText(request);
          if (!text) return cors(json({ error: "empty" }, 400));
          const msg = push(list, makeMsg(playerId, displayName, text, "dm", peerId));
          return cors(json({ message: msg }));
        }
      }

      if (request.method === "GET" && url.pathname === "/health") {
        return cors(json({ ok: true }));
      }

      return cors(json({ error: "not_found" }, 404));
    } catch (err) {
      return cors(json({ error: String(err?.message || err) }, 500));
    }
  }
}

export default {
  async fetch(request, env) {
    const id = env.CHAT_HUB.idFromName("baraki-main");
    return env.CHAT_HUB.get(id).fetch(request);
  },
};

const MAX_TEXT = 280;
const MAX_BUFFER = 200;

function authorize(request, env) {
  const required = (env.CHAT_API_KEY || "").trim();
  if (!required) return true;
  const got = (request.headers.get("X-Baraki-Key") || "").trim();
  return got.length > 0 && got === required;
}

function makeMsg(playerId, displayName, text, channel, peerId = "") {
  return {
    id: `${Date.now()}-${Math.random().toString(36).slice(2, 8)}`,
    ts: new Date().toISOString(),
    playerId,
    displayName: displayName || "Player",
    text: String(text).trim().slice(0, MAX_TEXT),
    channel,
    peerId,
  };
}

function push(list, msg) {
  list.push(msg);
  if (list.length > MAX_BUFFER) list.splice(0, list.length - MAX_BUFFER);
  return msg;
}

function after(list, afterTs) {
  if (!afterTs) return list.slice(-50);
  return list.filter((m) => m.ts > afterTs).slice(0, 50);
}

async function readText(request) {
  const body = await request.json().catch(() => ({}));
  const text = typeof body.text === "string" ? body.text.trim() : "";
  return text.length > 0 && text.length <= MAX_TEXT ? text : "";
}

function normalizeIdList(value) {
  if (!Array.isArray(value)) return [];
  const out = [];
  const seen = new Set();
  for (const raw of value) {
    const id = String(raw || "").trim();
    if (!id || id.length < 4 || id.length > 128 || seen.has(id)) continue;
    seen.add(id);
    out.push(id);
    if (out.length >= 64) break;
  }
  return out;
}

function conversationKey(a, b) {
  return a < b ? `${a}|${b}` : `${b}|${a}`;
}

function json(data, status = 200) {
  return new Response(JSON.stringify(data), {
    status,
    headers: { "content-type": "application/json; charset=utf-8" },
  });
}

function cors(response) {
  const headers = new Headers(response.headers);
  headers.set("access-control-allow-origin", "*");
  headers.set(
    "access-control-allow-headers",
    "content-type,x-baraki-key,x-baraki-player-id,x-baraki-player-name"
  );
  headers.set("access-control-allow-methods", "GET,POST,OPTIONS");
  return new Response(response.body, { status: response.status, headers });
}