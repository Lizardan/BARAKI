import assert from "node:assert/strict";
import { describe, it } from "node:test";
import { buildPublicWssUrl, resolveClientWssUrl } from "../src/publicWss.js";

describe("buildPublicWssUrl", () => {
  it("builds wss from bare hostname", () => {
    assert.equal(
      buildPublicWssUrl("baraki-matchmaker.lizard268.workers.dev"),
      "wss://baraki-matchmaker.lizard268.workers.dev");
  });

  it("strips schemes and trailing slash", () => {
    assert.equal(
      buildPublicWssUrl("https://baraki-matchmaker.lizard268.workers.dev/"),
      "wss://baraki-matchmaker.lizard268.workers.dev");
  });

  it("rejects paths and empty", () => {
    assert.equal(buildPublicWssUrl(""), "");
    assert.equal(buildPublicWssUrl("host.example/game"), "");
  });
});

describe("resolveClientWssUrl", () => {
  it("prefers public host over tunnel", () => {
    assert.equal(
      resolveClientWssUrl(
        "baraki-matchmaker.lizard268.workers.dev",
        "wss://random-words.trycloudflare.com"),
      "wss://baraki-matchmaker.lizard268.workers.dev");
  });

  it("falls back to tunnel when public host unset", () => {
    assert.equal(
      resolveClientWssUrl("", "wss://random-words.trycloudflare.com"),
      "wss://random-words.trycloudflare.com");
  });
});
