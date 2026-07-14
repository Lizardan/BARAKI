// Fill before deploy. Do not commit real secrets.
window.BARAKI_CONFIG = {
  DISCORD_CLIENT_ID: "1526279588479631420",
  // Relative paths work inside Discord proxy after URL mappings:
  MATCHMAKER_BASE: "/api",
  // Named-tunnel hostname (no scheme). Must match Discord Portal /wss and playtest.env WSS_HOST.
  // CI injects GitHub var WSS_PROXY_TARGET on deploy when set.
  WSS_PROXY_TARGET: "",
  // When developing outside Discord iframe, browser smoke mode stays local.
  DEFAULT_PLAYER_COUNT: 2,
  UNITY_BUILD_URL: "./Build",
  UNITY_LOADER: "./Build/BARAKI.loader.js",
  UNITY_DATA: "./Build/BARAKI.data.unityweb",
  UNITY_FRAMEWORK: "./Build/BARAKI.framework.js.unityweb",
  UNITY_CODE: "./Build/BARAKI.wasm.unityweb",
};
