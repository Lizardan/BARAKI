/**
 * Ring-buffer diagnostics for Discord Activity playtest debugging.
 * GET  /api/v1/diag  -> recent events
 * POST /api/v1/diag  -> append { source, event, detail? }
 */

const DIAG_KEY = "diag_events";
const DIAG_MAX = 80;

export async function appendDiag(env, entry) {
  if (!env.BARAKI_STORE) {
    return;
  }

  const event = {
    t: new Date().toISOString(),
    source: String(entry.source || "unknown").slice(0, 64),
    event: String(entry.event || "event").slice(0, 64),
    detail: entry.detail == null ? null : String(entry.detail).slice(0, 500),
  };

  let list = [];
  try {
    const raw = await env.BARAKI_STORE.get(DIAG_KEY);
    if (raw) {
      list = JSON.parse(raw);
    }
  } catch {
    list = [];
  }

  if (!Array.isArray(list)) {
    list = [];
  }

  list.push(event);
  if (list.length > DIAG_MAX) {
    list = list.slice(list.length - DIAG_MAX);
  }

  await env.BARAKI_STORE.put(DIAG_KEY, JSON.stringify(list));
}

export async function readDiag(env) {
  if (!env.BARAKI_STORE) {
    return { events: [], tunnel: null, public_wss: null };
  }

  let events = [];
  try {
    const raw = await env.BARAKI_STORE.get(DIAG_KEY);
    if (raw) {
      events = JSON.parse(raw);
    }
  } catch {
    events = [];
  }

  if (!Array.isArray(events)) {
    events = [];
  }

  const tunnel = await env.BARAKI_STORE.get("tunnel_wss");
  return {
    events,
    tunnel,
    count: events.length,
  };
}

export async function clearDiag(env) {
  if (!env.BARAKI_STORE) {
    return;
  }

  await env.BARAKI_STORE.delete(DIAG_KEY);
}
