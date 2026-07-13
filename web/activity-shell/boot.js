import { DiscordSDK, patchUrlMappings } from "https://unpkg.com/@discord/embedded-app-sdk@2.4.0/output/index.mjs";

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

async function initDiscord() {
  const clientId = config.DISCORD_CLIENT_ID;
  if (!clientId || clientId.startsWith("YOUR_")) {
    setStatus("Set DISCORD_CLIENT_ID in config.js");
    return {
      instanceId: "local-dev",
      userId: "local-user",
      displayName: "Local",
    };
  }

  if (isInsideDiscord()) {
    // Map external hosts through Discord proxy (adjust to your Pages/Workers/Tunnel).
    patchUrlMappings([
      { prefix: "/api", target: new URL(config.MATCHMAKER_BASE || "/api", window.location.origin).host || undefined },
    ].filter((m) => m.target));
  }

  const sdk = new DiscordSDK(clientId);
  await sdk.ready();
  setStatus("Discord SDK ready");

  const { code } = await sdk.commands.authorize({
    client_id: clientId,
    response_type: "code",
    state: "",
    prompt: "none",
    scope: ["identify", "guilds"],
  });

  // Token exchange should go through your Worker in production.
  // FREE-0 test: use instance + user from SDK without full OAuth exchange when possible.
  let userId = "discord-user";
  let displayName = "Player";
  try {
    const auth = await sdk.commands.authenticate({ access_token: code });
    userId = auth?.user?.id || userId;
    displayName = auth?.user?.username || displayName;
  } catch (err) {
    console.warn("authenticate skipped/failed", err);
  }

  return {
    instanceId: sdk.instanceId || "unknown-instance",
    userId,
    displayName,
    sdk,
  };
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
    script.onload = resolve;
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
    const discord = await initDiscord();
    let match = null;
    try {
      match = await ensureMatch(discord);
      setStatus(`Match slot ${match.slot} → ${match.wss_url}`);
    } catch (err) {
      setStatus(`Matchmaker unavailable: ${err.message}`);
      console.warn(err);
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

    // Expose for Unity .jslib sync reads
    window.__BARAKI_DISCORD_SESSION__ = sessionPayload;

    await loadUnity(sessionPayload);
  } catch (err) {
    setStatus(String(err && err.message ? err.message : err));
    console.error(err);
  }
}

main();
