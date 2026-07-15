/**
 * Bidirectional WebSocket proxy: Discord/client ↔ registered game tunnel.
 * Keeps Discord Portal /wss on this Worker while the evening tunnel may change.
 */

function makeWebSocketKey() {
  const bytes = crypto.getRandomValues(new Uint8Array(16));
  let binary = "";
  for (let i = 0; i < bytes.length; i++) {
    binary += String.fromCharCode(bytes[i]);
  }
  return btoa(binary);
}

function pipeSockets(a, b) {
  a.addEventListener("message", (event) => {
    try {
      b.send(event.data);
    } catch {
      // peer already closed
    }
  });

  const closePeer = (code, reason) => {
    try {
      b.close(code, reason);
    } catch {
      // ignore
    }
  };

  a.addEventListener("close", (event) => {
    closePeer(event.code || 1000, event.reason || "");
  });
  a.addEventListener("error", () => {
    closePeer(1011, "peer error");
  });
}

/**
 * @param {Request} request
 * @param {string} tunnelWssUrl registered ws:// or wss:// game tunnel
 */
export async function proxyGameWebSocket(request, tunnelWssUrl) {
  if ((request.headers.get("Upgrade") || "").toLowerCase() !== "websocket") {
    return new Response("Expected Upgrade: websocket", { status: 426 });
  }

  const upstreamHttp = String(tunnelWssUrl).replace(/^ws/i, "http");
  let upstreamResp;
  try {
    upstreamResp = await fetch(upstreamHttp, {
      headers: {
        Upgrade: "websocket",
        Connection: "Upgrade",
        "Sec-WebSocket-Version": "13",
        "Sec-WebSocket-Key": makeWebSocketKey(),
      },
    });
  } catch (err) {
    return jsonError("upstream_connect_failed", String(err && err.message ? err.message : err), 502);
  }

  const upstream = upstreamResp.webSocket;
  if (!upstream) {
    return jsonError(
      "upstream_no_websocket",
      `status=${upstreamResp.status}`,
      502);
  }

  const pair = new WebSocketPair();
  const client = pair[0];
  const server = pair[1];

  upstream.accept();
  server.accept();
  pipeSockets(server, upstream);
  pipeSockets(upstream, server);

  return new Response(null, {
    status: 101,
    webSocket: client,
  });
}

function jsonError(error, detail, status) {
  return new Response(JSON.stringify({ error, detail }), {
    status,
    headers: { "Content-Type": "application/json" },
  });
}
