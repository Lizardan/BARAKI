/**
 * Unit tests for the UGS JWT verification used by the chat Worker.
 * Run: node --test test/
 */
import test from "node:test";
import assert from "node:assert/strict";
import { webcrypto as crypto } from "node:crypto";
import { verifyUgsJwt } from "../src/index.js";

const NOW = 1_700_000_000;

function base64UrlEncode(obj) {
  return Buffer.from(JSON.stringify(obj), "utf8")
    .toString("base64")
    .replace(/\+/g, "-")
    .replace(/\//g, "_")
    .replace(/=+$/, "");
}

async function makeKeyPair(kid) {
  const pair = await crypto.subtle.generateKey(
    { name: "RSASSA-PKCS1-v1_5", modulusLength: 2048, publicExponent: new Uint8Array([1, 0, 1]), hash: "SHA-256" },
    true,
    ["sign", "verify"],
  );
  const jwk = await crypto.subtle.exportKey("jwk", pair.publicKey);
  jwk.kid = kid;
  jwk.alg = "RS256";
  return { privateKey: pair.privateKey, jwk };
}

async function signJwt(privateKey, header, payload) {
  const signingInput = `${base64UrlEncode(header)}.${base64UrlEncode(payload)}`;
  const signature = await crypto.subtle.sign(
    "RSASSA-PKCS1-v1_5",
    privateKey,
    Buffer.from(signingInput, "utf8"),
  );
  const sigB64 = Buffer.from(signature).toString("base64").replace(/\+/g, "-").replace(/\//g, "_").replace(/=+$/, "");
  return `${signingInput}.${sigB64}`;
}

const PAYLOAD = {
  sub: "abcdefghij1234567890klmnop",
  aud: "42d17af4-ef64-44ff-ad3f-665c4f394a4d",
  iss: "unity-auth",
  iat: NOW - 60,
  exp: NOW + 3600,
};

test("valid token passes and returns payload", async () => {
  const { privateKey, jwk } = await makeKeyPair("k1");
  const keys = new Map([["k1", jwk]]);
  const token = await signJwt(privateKey, { alg: "RS256", kid: "k1", typ: "JWT" }, PAYLOAD);
  const result = await verifyUgsJwt(token, {
    fetchJwks: async () => keys,
    nowSec: NOW,
    expectedProjectId: PAYLOAD.aud,
  });
  assert.ok(result);
  assert.equal(result.sub, PAYLOAD.sub);
});

test("expired token is rejected", async () => {
  const { privateKey, jwk } = await makeKeyPair("k1");
  const keys = new Map([["k1", jwk]]);
  const token = await signJwt(privateKey, { alg: "RS256", kid: "k1" }, { ...PAYLOAD, exp: NOW - 10 });
  const result = await verifyUgsJwt(token, { fetchJwks: async () => keys, nowSec: NOW });
  assert.equal(result, null);
});

test("signature from a foreign key is rejected", async () => {
  const signer = await makeKeyPair("k1");
  const other = await makeKeyPair("k2");
  const token = await signJwt(signer.privateKey, { alg: "RS256", kid: "k2" }, PAYLOAD);
  const result = await verifyUgsJwt(token, {
    fetchJwks: async () => new Map([["k2", other.jwk]]),
    nowSec: NOW,
  });
  assert.equal(result, null);
});

test("audience mismatch is rejected when project id is enforced", async () => {
  const { privateKey, jwk } = await makeKeyPair("k1");
  const token = await signJwt(privateKey, { alg: "RS256", kid: "k1" }, PAYLOAD);
  const fetchJwks = async () => new Map([["k1", jwk]]);
  const bad = await verifyUgsJwt(token, {
    fetchJwks,
    nowSec: NOW,
    expectedProjectId: "00000000-0000-0000-0000-000000000000",
  });
  assert.equal(bad, null);
  const good = await verifyUgsJwt(token, {
    fetchJwks,
    nowSec: NOW,
    expectedProjectId: PAYLOAD.aud,
  });
  assert.ok(good);
});

test("non-RS256 algorithm is rejected without touching keys", async () => {
  const token = `${base64UrlEncode({ alg: "HS256" })}.${base64UrlEncode(PAYLOAD)}.c2ln`;
  const result = await verifyUgsJwt(token, {
    fetchJwks: async () => new Map(),
    nowSec: NOW,
  });
  assert.equal(result, null);
});

test("malformed tokens are rejected", async () => {
  const fetchJwks = async () => new Map();
  for (const token of ["", "a.b", "a.b.c.d", "!!!.!!!.!!!"]) {
    assert.equal(await verifyUgsJwt(token, { fetchJwks, nowSec: NOW }), null);
  }
});

test("unknown kid triggers one forced JWKS refresh", async () => {
  const { privateKey, jwk } = await makeKeyPair("rotated");
  let refreshes = 0;
  const fetchJwks = async (force) => {
    if (force) refreshes++;
    return force ? new Map([["rotated", jwk]]) : new Map();
  };
  const token = await signJwt(privateKey, { alg: "RS256", kid: "rotated" }, PAYLOAD);
  const result = await verifyUgsJwt(token, { fetchJwks, nowSec: NOW });
  assert.ok(result);
  assert.equal(refreshes, 1);
});
