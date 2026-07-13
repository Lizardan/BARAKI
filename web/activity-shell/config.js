// Fill before deploy. Do not commit real secrets.
window.BARAKI_CONFIG = {
  DISCORD_CLIENT_ID: "YOUR_DISCORD_APPLICATION_CLIENT_ID",
  // Relative paths work inside Discord proxy after URL mappings:
  MATCHMAKER_BASE: "/api",
  // When developing outside Discord iframe, set absolute Workers URL:
  // MATCHMAKER_BASE: "https://baraki-matchmaker.YOUR_SUBDOMAIN.workers.dev",
  DEFAULT_PLAYER_COUNT: 2,
  UNITY_BUILD_URL: "./Build",
  UNITY_LOADER: "./Build/BARAKI.loader.js",
  UNITY_DATA: "./Build/BARAKI.data",
  UNITY_FRAMEWORK: "./Build/BARAKI.framework.js",
  UNITY_CODE: "./Build/BARAKI.wasm",
};
