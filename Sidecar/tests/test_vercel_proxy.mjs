import assert from "node:assert/strict";
import { test } from "node:test";

import {
  COOKIE_NAME,
  SCENE_INSTRUCTIONS,
  createSessionToken,
  handleHealth,
  handleReset,
  handleTurn,
  verifySessionToken,
} from "../web/server/sidecar-proxy.js";

const ENV = {
  SIDECAR_BASE_URL: "https://sidecar.example",
  FP_CLIENT_KEY: "server-only-key",
  SESSION_SIGNING_SECRET: "test-signing-secret-with-enough-entropy",
  SIDECAR_MAX_AUDIO_SECONDS: "20",
  MAX_TURN_REQUEST_BYTES: "700000",
};

function cookieValue(response) {
  const header = response.headers.get("set-cookie") || "";
  return header.match(new RegExp(`${COOKIE_NAME}=([^;]+)`))?.[1] || "";
}

function turnRequest({ purpose = "p2", audio = new Uint8Array([0, 0, 1, 0]), headers = {} } = {}) {
  const form = new FormData();
  form.append("purpose", purpose);
  form.append("session_id", "attacker-controlled-session");
  form.append("scene_instruction", "ATTACKER CONTROLLED PROMPT");
  form.append("sample_rate", "48000");
  form.append("audio", new Blob([audio], { type: "application/octet-stream" }), "utterance.pcm");
  return new Request("https://game.example/api/turn", {
    method: "POST",
    headers: { Origin: "https://game.example", ...headers },
    body: form,
  });
}

test("signed sessions survive verification and reject tampering or expiry", () => {
  const now = 1_800_000_000_000;
  const token = createSessionToken(ENV.SESSION_SIGNING_SECRET, {
    now,
    sessionId: "web-fixed-session",
  });

  assert.equal(
    verifySessionToken(token, ENV.SESSION_SIGNING_SECRET, { now }),
    "web-fixed-session",
  );
  assert.equal(
    verifySessionToken(`${token}x`, ENV.SESSION_SIGNING_SECRET, { now }),
    "",
  );
  assert.equal(
    verifySessionToken(token, ENV.SESSION_SIGNING_SECRET, { now: now + 7_200_001 }),
    "",
  );
});

test("turn proxy owns credentials, session identity, and scene instructions", async () => {
  let upstream = null;
  const response = await handleTurn(turnRequest(), ENV, async (url, options) => {
    upstream = { url, options };
    return Response.json({
      ok: true,
      transcript: "I remember the fire.",
      reply_text: "And after the argument?",
      audio_b64: "AAA=",
      audio_sample_rate: 16000,
      session_ended: false,
    });
  });

  assert.equal(response.status, 200);
  assert.equal(upstream.url, "https://sidecar.example/turn");
  assert.equal(upstream.options.headers["X-FP-Client-Key"], ENV.FP_CLIENT_KEY);
  assert.equal(upstream.options.body.get("sample_rate"), "16000");
  assert.equal(upstream.options.body.get("onset_delay_ms"), "0");
  assert.equal(upstream.options.body.get("scene_instruction"), SCENE_INSTRUCTIONS.p2);
  assert.notEqual(upstream.options.body.get("session_id"), "attacker-controlled-session");
  assert.match(upstream.options.body.get("session_id"), /^web-/);
  assert.equal(upstream.options.body.get("audio").size, 4);
  assert.match(response.headers.get("cache-control"), /no-store/);
  assert.match(response.headers.get("set-cookie"), /HttpOnly/);
  assert.match(response.headers.get("set-cookie"), /Secure/);
  assert.match(response.headers.get("set-cookie"), /SameSite=Lax/);
  assert.ok(cookieValue(response));
});

test("turn proxy reuses a valid signed cookie", async () => {
  let firstSession = "";
  const first = await handleTurn(turnRequest(), ENV, async (_url, options) => {
    firstSession = options.body.get("session_id");
    return Response.json({ ok: true });
  });
  const token = cookieValue(first);

  let secondSession = "";
  const secondRequest = turnRequest({ headers: { Cookie: `${COOKIE_NAME}=${token}` } });
  await handleTurn(secondRequest, ENV, async (_url, options) => {
    secondSession = options.body.get("session_id");
    return Response.json({ ok: true });
  });

  assert.equal(secondSession, firstSession);
});

test("malformed cookies are replaced instead of crashing the public function", async () => {
  let sessionId = "";
  const request = turnRequest({ headers: { Cookie: `${COOKIE_NAME}=%` } });
  const response = await handleTurn(request, ENV, async (_url, options) => {
    sessionId = options.body.get("session_id");
    return Response.json({ ok: true });
  });

  assert.equal(response.status, 200);
  assert.match(sessionId, /^web-/);
  assert.ok(cookieValue(response));
});

test("turn proxy rejects cross-origin, invalid purpose, malformed audio, and large audio", async () => {
  const neverFetch = async () => {
    assert.fail("invalid requests must not reach the paid backend");
  };

  const crossOrigin = turnRequest({ headers: { Origin: "https://attacker.example" } });
  assert.equal((await handleTurn(crossOrigin, ENV, neverFetch)).status, 403);
  assert.equal((await handleTurn(turnRequest({ purpose: "p1" }), ENV, neverFetch)).status, 400);
  assert.equal((await handleTurn(turnRequest({ audio: new Uint8Array([1]) }), ENV, neverFetch)).status, 400);
  assert.equal(
    (await handleTurn(turnRequest({ audio: new Uint8Array(640_002) }), ENV, neverFetch)).status,
    413,
  );
});

test("health proxy checks the configured Sidecar without exposing credentials", async () => {
  let upstream = null;
  const request = new Request("https://game.example/api/health");
  const response = await handleHealth(request, ENV, async (url, options) => {
    upstream = { url, options };
    return Response.json({ ok: true, models_loaded: true });
  });

  assert.equal(response.status, 200);
  assert.equal(upstream.url, "https://sidecar.example/health");
  assert.equal(upstream.options.headers.Accept, "application/json");
  assert.equal(upstream.options.headers["X-FP-Client-Key"], undefined);
  assert.deepEqual(await response.json(), { ok: true, models_loaded: true });
});

test("reset uses only the signed cookie session and clears it", async () => {
  const token = createSessionToken(ENV.SESSION_SIGNING_SECRET, {
    sessionId: "web-reset-session",
  });
  const request = new Request("https://game.example/api/session/reset", {
    method: "POST",
    headers: {
      Origin: "https://game.example",
      Cookie: `${COOKIE_NAME}=${token}`,
    },
  });
  let upstreamSession = "";
  const response = await handleReset(request, ENV, async (url, options) => {
    assert.equal(url, "https://sidecar.example/session/reset");
    assert.equal(options.headers["X-FP-Client-Key"], ENV.FP_CLIENT_KEY);
    upstreamSession = options.body.get("session_id");
    return Response.json({ ok: true });
  });

  assert.equal(response.status, 200);
  assert.equal(upstreamSession, "web-reset-session");
  assert.match(response.headers.get("set-cookie"), /Max-Age=0/);
});

test("reset without a valid cookie succeeds locally without spending a backend request", async () => {
  const response = await handleReset(
    new Request("https://game.example/api/session/reset", {
      method: "POST",
      headers: { Origin: "https://game.example" },
    }),
    ENV,
    async () => assert.fail("no signed session means nothing to reset upstream"),
  );

  assert.equal(response.status, 200);
  assert.deepEqual(await response.json(), { ok: true });
});
