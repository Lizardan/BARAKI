const statusEl = document.getElementById("status");
const config = window.BARAKI_CONFIG || {};

function setStatus(text) {
  if (statusEl) {
    statusEl.textContent = text;
  }
  console.log("[BARAKI]", text);
}

function isInsideDiscord() {
  try {
    return window.location.hostname.includes("discordsays.com")
      || window.location.search.includes("frame_id");
  } catch {
    return false;
  }
}

function withTimeout(promise, ms, label) {
  return new Promise((resolve, reject) => {
    const timer = setTimeout(
      () => reject(new Error(`${label} timed out after ${ms}ms`)),
      ms);
    promise.then(
      (value) => {
        clearTimeout(timer);
        resolve(value);
      },
      (err) => {
        clearTimeout(timer);
        reject(err);
      });
  });
}

function hostnameFromWssUrl(wssUrl) {
  try {
    if (!wssUrl) {
      return "";
    }
    return new URL(wssUrl).hostname;
  } catch {
    return "";
  }
}

async function loadDiscordSdk() {
  // Local vendor only — Discord Activity CSP blocks unpkg/CDN imports.
  return import("./vendor/discord-embedded-app-sdk.js");
}

async function applyWssProxyMapping(patchUrlMappings, wssHost) {
  const target = (wssHost || config.WSS_PROXY_TARGET || "").trim();
  if (!target || typeof patchUrlMappings !== "function") {
    return;
  }

  // Portal mapping: /wss → stable Worker host (once). Worker proxies to evening tunnel.
  // This rewrites Unity WebSocket connects to that host through Discord CSP.
  patchUrlMappings([{ prefix: "/wss", target }]);
  setStatus(`WSS proxy → ${target}`);
}

async function initDiscord() {
  if (!isInsideDiscord()) {
    setStatus("Browser smoke mode");
    return {
      instanceId: "local-dev",
      userId: "local-user",
      displayName: "Local",
    };
  }

  const clientId = config.DISCORD_CLIENT_ID;
  if (!clientId || clientId.startsWith("YOUR_")) {
    setStatus("Set DISCORD_CLIENT_ID in config.js");
    return {
      instanceId: "local-dev",
      userId: "local-user",
      displayName: "Local",
    };
  }

  setStatus("Loading Discord SDK…");
  let DiscordSDK;
  let patchUrlMappings;
  try {
    ({ DiscordSDK, patchUrlMappings } = await loadDiscordSdk());
  } catch (err) {
    console.warn("Discord SDK import failed", err);
    setStatus(`Discord SDK failed: ${err.message}`);
    return {
      instanceId: "sdk-import-failed",
      userId: "discord-user",
      displayName: "Player",
    };
  }

  // Apply stable WSS mapping early when known (named tunnel).
  await applyWssProxyMapping(patchUrlMappings, config.WSS_PROXY_TARGET);

  const sdk = new DiscordSDK(clientId);
  try {
    setStatus("Waiting for Discord ready…");
    await withTimeout(sdk.ready(), 8000, "sdk.ready");
    setStatus("Discord SDK ready");
  } catch (err) {
    console.warn("sdk.ready failed/timed out", err);
    setStatus(`Discord ready skipped: ${err.message}`);
    return {
      instanceId: sdk.instanceId || "ready-timeout",
      userId: "discord-user",
      displayName: "Player",
      sdk,
      patchUrlMappings,
    };
  }

  let userId = "discord-user";
  let displayName = "Player";
  try {
    setStatus("Authorizing…");
    const { code } = await withTimeout(
      sdk.commands.authorize({
        client_id: clientId,
        response_type: "code",
        state: "",
        prompt: "none",
        scope: ["identify", "guilds"],
      }),
      12000,
      "authorize");

    // Full OAuth token exchange needs a Worker; FREE-0 continues without it.
    try {
      const auth = await withTimeout(
        sdk.commands.authenticate({ access_token: code }),
        5000,
        "authenticate");
      userId = auth?.user?.id || userId;
      displayName = auth?.user?.username || displayName;
    } catch (err) {
      console.warn("authenticate skipped/failed", err);
    }
  } catch (err) {
    console.warn("authorize skipped/failed", err);
    setStatus(`Authorize skipped: ${err.message}`);
  }

  return {
    instanceId: sdk.instanceId || "unknown-instance",
    userId,
    displayName,
    sdk,
    patchUrlMappings,
  };
}

async function postDiag(source, event, detail) {
  try {
    const base = (config.MATCHMAKER_BASE || "/api").replace(/\/+$/, "");
    await fetch(`${base}/v1/diag`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ source, event, detail: detail || null }),
    });
  } catch (err) {
    console.warn("diag post failed", err);
  }
}

async function ensureMatch(session) {
  const base = (config.MATCHMAKER_BASE || "/api").replace(/\/+$/, "");
  const res = await fetch(`${base}/v1/match/ensure`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({
      instance_id: session.instanceId,
      player_count: config.DEFAULT_PLAYER_COUNT || 2,
      discord_user_id: session.userId,
      display_name: session.displayName,
    }),
  });

  if (!res.ok) {
    const text = await res.text();
    throw new Error(`ensure failed ${res.status}: ${text}`);
  }

  return res.json();
}

function pushSessionToUnity(unityInstance, payload) {
  try {
    unityInstance.SendMessage(
      "DiscordActivityBridge",
      "ReceiveSessionJson",
      JSON.stringify(payload));
  } catch (err) {
    console.warn("SendMessage DiscordActivityBridge failed", err);
  }
}

async function loadUnity(sessionPayload) {
  setStatus("Loading WebGL…");
  const canvas = document.querySelector("#unity-canvas");
  const loaderUrl = config.UNITY_LOADER || "./Build/BARAKI.loader.js";

  await new Promise((resolve, reject) => {
    const script = document.createElement("script");
    script.src = loaderUrl;
    script.onload = () => resolve();
    script.onerror = () => reject(new Error(`Failed to load ${loaderUrl}. Build WebGL into web/activity-shell/Build/`));
    document.body.appendChild(script);
  });

  if (typeof createUnityInstance !== "function") {
    throw new Error("createUnityInstance missing — Unity loader not present");
  }

  const instance = await createUnityInstance(canvas, {
    dataUrl: config.UNITY_DATA || "./Build/BARAKI.data",
    frameworkUrl: config.UNITY_FRAMEWORK || "./Build/BARAKI.framework.js",
    codeUrl: config.UNITY_CODE || "./Build/BARAKI.wasm",
    streamingAssetsUrl: "StreamingAssets",
    companyName: "BARAKI",
    productName: "BARAKI",
    productVersion: "0.1.0",
  }, (progress) => setStatus(`Loading WebGL… ${Math.round(progress * 100)}%`));

  pushSessionToUnity(instance, sessionPayload);
  setStatus("Running");
  return instance;
}

async function main() {
  try {
    setStatus("Starting…");
    const discord = await initDiscord();
    let match = null;
    if (isInsideDiscord()) {
      try {
        setStatus("Calling matchmaker…");
        match = await withTimeout(ensureMatch(discord), 8000, "ensureMatch");
        setStatus(`Match slot ${match.slot} → ${match.wss_url}`);
        await postDiag(
          "shell",
          "ensure_ok",
          `slot=${match.slot} wss=${match.wss_url} host=${window.location.host}`);
        // Always remap Unity WSS through Discord /wss proxy (Portal target must match).
        const fromMatch = hostnameFromWssUrl(match.wss_url) || config.WSS_PROXY_TARGET;
        if (fromMatch) {
          await applyWssProxyMapping(discord.patchUrlMappings, fromMatch);
          await postDiag("shell", "wss_mapped", fromMatch);
        } else {
          console.warn("No WSS host for patchUrlMappings - set WSS_PROXY_TARGET or ensure wss_url");
          await postDiag("shell", "wss_map_missing", "");
        }
      } catch (err) {
        // OK without dedicated/tunnel for menu smoke.
        setStatus(`Matchmaker unavailable: ${err.message}`);
        console.warn(err);
        await postDiag("shell", "ensure_fail", String(err && err.message ? err.message : err));
      }
    }

    const sessionPayload = {
      instanceId: discord.instanceId,
      userId: discord.userId,
      displayName: discord.displayName,
      wssUrl: match?.wss_url || "",
      slot: match?.slot ?? -1,
      playerCount: match?.player_count || config.DEFAULT_PLAYER_COUNT || 2,
      roomCode: match?.room_code || "",
      joinToken: match?.join_token || "",
    };

    window.__BARAKI_DISCORD_SESSION__ = sessionPayload;

    await loadUnity(sessionPayload);
  } catch (err) {
    setStatus(String(err && err.message ? err.message : err));
    console.error(err);
  }
}

main();
