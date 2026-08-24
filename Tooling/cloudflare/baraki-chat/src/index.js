/**
 * BARAKI menu chat API (global + friends feed + DM).
 * Auth: optional CHAT_API_KEY secret; clients send X-Baraki-Key.
 * Identity: playtest-grade X-Baraki-Player-Id / X-Baraki-Player-Name.
 *
 * Transport: WebSocket push at GET /v1/ws (send/sync frames); HTTP GET stays for
 * initial history and catch-up. All messages flow through the single ChatHub
 * Durable Object, which also owns socket fan-out.
 *
 * Retention: last 80 channel / 50 DM messages, drop older than 36h.
 * Alarm prunes Durable Object storage hourly so history cannot grow forever.
 */
const MAX_TEXT = 280;
const MAX_CHANNEL = 80;
const MAX_DM = 50;
const RETENTION_MS = 36 * 60 * 60 * 1000;
const SESSION_TTL_MS = 7 * 24 * 60 * 60 * 1000;
const ALARM_MS = 60 * 60 * 1000;

export class ChatHub {
  constructor(state, env) {
    this.state = state;
    this.env = env;
    this.ready = null;
    // playerId -> Set<WebSocket>. In-memory only: DO eviction drops sockets,
    // clients reconnect and catch up via the sync frame.
    this.sockets = new Map();
  }

  async ensureLoaded() {
    if (this.ready) {
      return this.ready;
    }
    this.ready = this.hydrate();
    return this.ready;
  }

  async hydrate() {
    const stored = await this.state.storage.get(["global", "feeds", "dms", "sessions"]);
    this.global = stored.get("global") || [];
    this.feeds = new Map(Object.entries(stored.get("feeds") || {}));
    this.dms = new Map(Object.entries(stored.get("dms") || {}));
    this.sessions = new Map(Object.entries(stored.get("sessions") || {}));
    this.pruneAll();
    if (!(await this.state.storage.getAlarm())) {
      await this.state.storage.setAlarm(Date.now() + 60_000);
    }
  }

  async persist() {
    await this.state.storage.put({
      global: this.global,
      feeds: Object.fromEntries(this.feeds),
      dms: Object.fromEntries(this.dms),
      sessions: Object.fromEntries(this.sessions),
    });
  }

  pruneAll() {
    const cutoff = Date.now() - RETENTION_MS;
    this.global = pruneList(this.global, MAX_CHANNEL, cutoff);
    pruneMap(this.feeds, MAX_CHANNEL, cutoff);
    pruneMap(this.dms, MAX_DM, cutoff);
    const sessionCutoff = Date.now() - SESSION_TTL_MS;
    for (const [id, session] of this.sessions) {
      if (!session || session.lastSeen < sessionCutoff) {
        this.sessions.delete(id);
      }
    }
  }

  async alarm() {
    await this.ensureLoaded();
    this.pruneAll();
    await this.persist();
    await this.state.storage.setAlarm(Date.now() + ALARM_MS);
  }

  async fetch(request) {
    const url = new URL(request.url);
    if (url.pathname === "/v1/ws") {
      return this.handleWebSocket(request, url);
    }

    await this.ensureLoaded();
    if (request.method === "OPTIONS") {
      return cors(new Response(null, { status: 204 }));
    }

    if (!authorize(request, this.env)) {
      return cors(json({ error: "unauthorized" }, 401));
    }

    const playerId = (request.headers.get("X-Baraki-Player-Id") || "").trim();
    const displayName = (request.headers.get("X-Baraki-Player-Name") || "Игрок").trim().slice(0, 64);
    if (!playerId || playerId.length < 4 || playerId.length > 128) {
      return cors(json({ error: "invalid_player" }, 400));
    }

    try {
      if (request.method === "POST" && url.pathname === "/v1/session") {
        const body = await request.json().catch(() => ({}));
        const friendIds = normalizeIdList(body.friendIds);
        this.sessions.set(playerId, { displayName, friendIds, lastSeen: Date.now() });
        this.pruneAll();
        await this.persist();
        return cors(json({ ok: true }));
      }

      if (url.pathname === "/v1/channels/global") {
        if (request.method === "GET") {
          this.pruneAll();
          return cors(json({ messages: after(this.global, url.searchParams.get("after"), MAX_CHANNEL) }));
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
          this.pruneAll();
          return cors(json({ messages: after(merged, url.searchParams.get("after"), MAX_CHANNEL) }));
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
          this.pruneAll();
          return cors(json({ messages: after(list, url.searchParams.get("after"), MAX_DM) }));
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

  // --- WebSocket ---

  async handleWebSocket(request, url) {
    if (request.headers.get("Upgrade") !== "websocket") {
      return cors(json({ error: "expected_websocket" }, 426));
    }
    if (!authorize(request, this.env)) {
      return cors(json({ error: "unauthorized" }, 401));
    }

    const playerId = (request.headers.get("X-Baraki-Player-Id") || "").trim();
    const displayName = (request.headers.get("X-Baraki-Player-Name") || "Игрок").trim().slice(0, 64);
    if (!playerId || playerId.length < 4 || playerId.length > 128) {
      return cors(json({ error: "invalid_player" }, 400));
    }

    await this.ensureLoaded();
    const pair = new WebSocketPair();
    this.registerSocket(playerId, pair[1]);
    const serverSide = pair[1];
    serverSide.accept();
    serverSide.send("pong");

    serverSide.addEventListener("message", async (event) => {
      try {
        await this.ensureLoaded();
        await this.handleClientFrame(serverSide, playerId, displayName, String(event.data ?? ""));
      } catch (err) {
        safeSend(serverSide, JSON.stringify({ type: "error", code: String(err?.message || err).slice(0, 120) }));
      }
    });
    serverSide.addEventListener("close", () => this.unregisterSocket(playerId, serverSide));
    serverSide.addEventListener("error", () => this.unregisterSocket(playerId, serverSide));

    return new Response(null, { status: 101, webSocket: pair[0] });
  }

  registerSocket(playerId, socket) {
    let set = this.sockets.get(playerId);
    if (!set) {
      set = new Set();
      this.sockets.set(playerId, set);
    }
    set.add(socket);
  }

  unregisterSocket(playerId, socket) {
    const set = this.sockets.get(playerId);
    if (!set) return;
    set.delete(socket);
    if (set.size === 0) this.sockets.delete(playerId);
  }

  async handleClientFrame(ws, playerId, displayName, raw) {
    if (raw === "ping") {
      safeSend(ws, "pong");
      return;
    }

    let frame;
    try {
      frame = JSON.parse(raw);
    } catch {
      safeSend(ws, JSON.stringify({ type: "error", code: "bad_json" }));
      return;
    }

    if (frame?.type === "sync") {
      this.sendSync(ws, playerId, frame);
      return;
    }

    if (frame?.type === "send") {
      const text = typeof frame.text === "string" ? frame.text.trim().slice(0, MAX_TEXT) : "";
      if (!text) {
        safeSend(ws, JSON.stringify({ type: "error", code: "empty" }));
        return;
      }

      let msg;
      if (frame.channel === "global") {
        msg = postGlobal(this, playerId, displayName, text);
      } else if (frame.channel === "friends") {
        msg = postFriends(this, playerId, displayName, text);
      } else if (frame.channel === "dm") {
        const peerId = String(frame.peerId || "").trim();
        if (!peerId || peerId === playerId) {
          safeSend(ws, JSON.stringify({ type: "error", code: "invalid_peer" }));
          return;
        }
        msg = postDm(this, playerId, displayName, text, peerId);
      } else {
        safeSend(ws, JSON.stringify({ type: "error", code: "bad_channel" }));
        return;
      }

      await this.persist();
      safeSend(ws, JSON.stringify({ type: "ack", message: msg }));
      return;
    }

    safeSend(ws, JSON.stringify({ type: "error", code: "bad_frame" }));
  }

  sendSync(ws, playerId, frame) {
    const globalAfter = typeof frame.globalAfter === "string" ? frame.globalAfter : "";
    const friendsAfter = typeof frame.friendsAfter === "string" ? frame.friendsAfter : "";
    const dmAfter = frame.dm && typeof frame.dm === "object" ? frame.dm : {};

    for (const m of after(this.global, globalAfter, MAX_CHANNEL)) {
      safeSend(ws, JSON.stringify({ type: "msg", message: m }));
    }

    const session = this.sessions.get(playerId);
    const friendIds = session?.friendIds || [];
    const authors = new Set([playerId, ...friendIds]);
    const friendsMerged = [];
    for (const author of authors) {
      for (const m of this.feeds.get(author) || []) friendsMerged.push(m);
    }
    friendsMerged.sort((a, b) => a.ts.localeCompare(b.ts));
    for (const m of after(friendsMerged, friendsAfter, MAX_CHANNEL)) {
      safeSend(ws, JSON.stringify({ type: "msg", message: m }));
    }

    for (const [peerId, cursor] of Object.entries(dmAfter)) {
      if (typeof cursor !== "string" || !peerId || peerId === playerId) continue;
      const key = conversationKey(playerId, peerId);
      for (const m of after(this.dms.get(key) || [], cursor, MAX_DM)) {
        safeSend(ws, JSON.stringify({ type: "msg", message: m }));
      }
    }
  }

  broadcast(msg) {
    let targets;
    if (msg.channel === "global") {
      targets = [...this.sockets.values()].flatMap((set) => [...set]);
    } else if (msg.channel === "friends") {
      const friendIds = this.sessions.get(msg.playerId)?.friendIds || [];
      targets = audienceSockets(this.sockets, [msg.playerId, ...friendIds]);
    } else if (msg.channel === "dm") {
      targets = audienceSockets(this.sockets, [msg.playerId, msg.peerId].filter(Boolean));
    } else {
      targets = [];
    }

    const payload = JSON.stringify({ type: "msg", message: msg });
    for (const socket of targets) {
      safeSend(socket, payload);
    }
  }
}

export default {
  async fetch(request, env) {
    const id = env.CHAT_HUB.idFromName("baraki-main");
    return env.CHAT_HUB.get(id).fetch(request);
  },
};

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
    displayName: displayName || "Игрок",
    text: String(text).trim().slice(0, MAX_TEXT),
    channel,
    peerId,
  };
}

function push(list, msg, max) {
  list.push(msg);
  const cutoff = Date.now() - RETENTION_MS;
  const pruned = pruneList(list, max, cutoff);
  list.length = 0;
  for (const item of pruned) list.push(item);
  return msg;
}

// Shared store+broadcast path used by socket "send" frames.
function postGlobal(hub, playerId, displayName, text) {
  const msg = push(hub.global, makeMsg(playerId, displayName, text, "global"), MAX_CHANNEL);
  hub.pruneAll();
  hub.broadcast(msg);
  return msg;
}

function postFriends(hub, playerId, displayName, text) {
  if (!hub.feeds.has(playerId)) hub.feeds.set(playerId, []);
  const msg = push(
    hub.feeds.get(playerId),
    makeMsg(playerId, displayName, text, "friends"),
    MAX_CHANNEL,
  );
  hub.pruneAll();
  hub.broadcast(msg);
  return msg;
}

function postDm(hub, playerId, displayName, text, peerId) {
  const key = conversationKey(playerId, peerId);
  if (!hub.dms.has(key)) hub.dms.set(key, []);
  const msg = push(
    hub.dms.get(key),
    makeMsg(playerId, displayName, text, "dm", peerId),
    MAX_DM,
  );
  hub.pruneAll();
  hub.broadcast(msg);
  return msg;
}

function safeSend(socket, text) {
  try {
    if (socket.readyState === 1 /* OPEN */) socket.send(text);
  } catch {
    // Dead socket: the close/error handler will unregister it.
  }
}

function audienceSockets(socketsByPlayer, playerIds) {
  const out = [];
  for (const id of playerIds) {
    const set = socketsByPlayer.get(id);
    if (!set) continue;
    for (const socket of set) out.push(socket);
  }
  return out;
}

function pruneList(list, max, cutoff) {
  const kept = (list || []).filter((m) => Date.parse(m.ts) >= cutoff);
  return kept.length > max ? kept.slice(-max) : kept;
}

function pruneMap(map, max, cutoff) {
  for (const [key, list] of map) {
    const next = pruneList(list, max, cutoff);
    if (next.length === 0) map.delete(key);
    else map.set(key, next);
  }
}

function after(list, afterTs, max) {
  const source = list || [];
  if (!afterTs) return source.slice(-max);
  return source.filter((m) => m.ts > afterTs).slice(0, max);
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
    "content-type,x-baraki-key,x-baraki-player-id,x-baraki-player-name",
  );
  headers.set("access-control-allow-methods", "GET,POST,OPTIONS");
  return new Response(response.body, { status: response.status, headers });
}
