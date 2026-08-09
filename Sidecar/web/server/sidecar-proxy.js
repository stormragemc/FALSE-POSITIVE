import { createHmac, randomUUID, timingSafeEqual } from "node:crypto";

export const COOKIE_NAME = "fp_session";

const SESSION_TTL_MS = 2 * 60 * 60 * 1000;
const DEFAULT_MAX_REQUEST_BYTES = 700_000;
const DEFAULT_MAX_AUDIO_SECONDS = 20;
const TARGET_SAMPLE_RATE = 16_000;
const UPSTREAM_TIMEOUT_MS = 60_000;

export const SCENE_INSTRUCTIONS = Object.freeze({
  p2: [
    "PHASE: P2_RECALL. Ask what David remembers about the night.",
    "Cover drinking by the fire, the argument, Nick leaving, the door, the lock, the blackout, and the morning.",
    "Treat emotion as pressure, never proof. Do not say the witness is lying.",
  ].join("\n"),
  p3: [
    "PHASE: P3_VERDICT. David must defend himself and name Aaron, Ivy, or Priya.",
    "Ask for reasoning grounded in observed clues. Do not reveal the ground truth.",
    "Treat affect as pressure, never proof of guilt or deception.",
  ].join("\n"),
});

function json(payload, status = 200, extraHeaders = {}) {
  return Response.json(payload, {
    status,
    headers: {
      "Cache-Control": "no-store",
      "X-Content-Type-Options": "nosniff",
      ...extraHeaders,
    },
  });
}

function boundedNumber(value, fallback, minimum, maximum) {
  const parsed = Number(value);
  if (!Number.isFinite(parsed)) return fallback;
  return Math.min(maximum, Math.max(minimum, parsed));
}

function getBaseUrl(env) {
  const configured = String(env.SIDECAR_BASE_URL || "").trim().replace(/\/$/, "");
  if (!configured) throw new Error("SIDECAR_BASE_URL is not configured");

  let url;
  try {
    url = new URL(configured);
  } catch (_error) {
    throw new Error("SIDECAR_BASE_URL is invalid");
  }
  const loopback = ["localhost", "127.0.0.1", "::1"].includes(url.hostname);
  if (url.protocol !== "https:" && !(loopback && url.protocol === "http:")) {
    throw new Error("SIDECAR_BASE_URL must use HTTPS");
  }
  if (url.username || url.password || url.search || url.hash) {
    throw new Error("SIDECAR_BASE_URL must not include credentials, query, or fragment");
  }
  return configured;
}

function getPrivateConfig(env) {
  const clientKey = String(env.FP_CLIENT_KEY || "").trim();
  const signingSecret = String(env.SESSION_SIGNING_SECRET || "").trim();
  if (!clientKey) throw new Error("FP_CLIENT_KEY is not configured");
  if (signingSecret.length < 32) {
    throw new Error("SESSION_SIGNING_SECRET must contain at least 32 characters");
  }
  return {
    baseUrl: getBaseUrl(env),
    clientKey,
    signingSecret,
    maxRequestBytes: Math.floor(boundedNumber(
      env.MAX_TURN_REQUEST_BYTES,
      DEFAULT_MAX_REQUEST_BYTES,
      65_536,
      2_000_000,
    )),
    maxAudioBytes: Math.floor(
      boundedNumber(
        env.SIDECAR_MAX_AUDIO_SECONDS,
        DEFAULT_MAX_AUDIO_SECONDS,
        1,
        DEFAULT_MAX_AUDIO_SECONDS,
      ) * TARGET_SAMPLE_RATE * 2,
    ),
  };
}

function signatureFor(value, secret) {
  return createHmac("sha256", secret).update(value).digest("base64url");
}

export function createSessionToken(secret, options = {}) {
  const now = options.now ?? Date.now();
  const sessionId = options.sessionId || `web-${randomUUID()}`;
  const expiresAt = now + SESSION_TTL_MS;
  const value = `${sessionId}.${expiresAt}`;
  return `${value}.${signatureFor(value, secret)}`;
}

export function verifySessionToken(token, secret, options = {}) {
  try {
    const now = options.now ?? Date.now();
    const [sessionId, expiresText, suppliedSignature, extra] = String(token || "").split(".");
    if (extra || !sessionId?.startsWith("web-") || !/^\d+$/.test(expiresText || "")) return "";
    const expiresAt = Number(expiresText);
    if (!Number.isSafeInteger(expiresAt) || expiresAt <= now) return "";
    const value = `${sessionId}.${expiresAt}`;
    const expectedSignature = signatureFor(value, secret);
    const supplied = Buffer.from(suppliedSignature || "", "utf8");
    const expected = Buffer.from(expectedSignature, "utf8");
    if (supplied.length !== expected.length || !timingSafeEqual(supplied, expected)) return "";
    return sessionId;
  } catch (_error) {
    return "";
  }
}

function readCookie(request, name) {
  const cookies = request.headers.get("cookie") || "";
  for (const part of cookies.split(";")) {
    const separator = part.indexOf("=");
    if (separator < 0) continue;
    if (part.slice(0, separator).trim() === name) {
      try {
        return decodeURIComponent(part.slice(separator + 1).trim());
      } catch (_error) {
        return "";
      }
    }
  }
  return "";
}

function sessionCookie(token, maxAge = Math.floor(SESSION_TTL_MS / 1000)) {
  return [
    `${COOKIE_NAME}=${encodeURIComponent(token)}`,
    "Path=/",
    `Max-Age=${maxAge}`,
    "HttpOnly",
    "Secure",
    "SameSite=Lax",
  ].join("; ");
}

function sameOrigin(request) {
  const origin = request.headers.get("origin");
  if (!origin) return true;
  try {
    return new URL(origin).origin === new URL(request.url).origin;
  } catch (_error) {
    return false;
  }
}

function estimatedFormSize(form) {
  let total = 0;
  const encoder = new TextEncoder();
  for (const [name, value] of form.entries()) {
    total += encoder.encode(name).byteLength;
    total += value instanceof Blob
      ? value.size
      : encoder.encode(String(value)).byteLength;
  }
  return total;
}

function serverError(error, operation) {
  console.error(`[sidecar-proxy] ${operation} failed`, error instanceof Error ? error.message : error);
  return json({ ok: false, error: "Live AI is temporarily unavailable." }, 503);
}

function requestSession(request, signingSecret) {
  const currentToken = readCookie(request, COOKIE_NAME);
  const currentSession = verifySessionToken(currentToken, signingSecret);
  const sessionId = currentSession || `web-${randomUUID()}`;
  const token = createSessionToken(signingSecret, { sessionId });
  return { sessionId, token };
}

async function upstreamFetch(fetchImpl, url, options) {
  const signal = AbortSignal.timeout(UPSTREAM_TIMEOUT_MS);
  return fetchImpl(url, { ...options, signal });
}

export async function handleTurn(request, env = process.env, fetchImpl = fetch) {
  if (!sameOrigin(request)) return json({ ok: false, error: "invalid origin" }, 403);

  let config;
  try {
    config = getPrivateConfig(env);
  } catch (error) {
    return serverError(error, "configuration");
  }

  const declaredLength = Number(request.headers.get("content-length") || 0);
  if (declaredLength > config.maxRequestBytes) {
    return json({ ok: false, error: "recording request is too large" }, 413);
  }

  let incoming;
  try {
    incoming = await request.formData();
  } catch (_error) {
    return json({ ok: false, error: "invalid multipart request" }, 400);
  }
  if (estimatedFormSize(incoming) > config.maxRequestBytes) {
    return json({ ok: false, error: "recording request is too large" }, 413);
  }

  const purpose = String(incoming.get("purpose") || "");
  if (!Object.hasOwn(SCENE_INSTRUCTIONS, purpose)) {
    return json({ ok: false, error: "invalid interrogation phase" }, 400);
  }
  const audio = incoming.get("audio");
  if (!(audio instanceof Blob) || audio.size === 0) {
    return json({ ok: false, error: "a recording is required" }, 400);
  }
  if (audio.size % 2 !== 0) {
    return json({ ok: false, error: "recording must be aligned PCM16" }, 400);
  }
  if (audio.size > config.maxAudioBytes) {
    return json({ ok: false, error: "recording is too long" }, 413);
  }

  const { sessionId, token } = requestSession(request, config.signingSecret);
  const outgoing = new FormData();
  outgoing.append("session_id", sessionId);
  outgoing.append("sample_rate", String(TARGET_SAMPLE_RATE));
  outgoing.append("onset_delay_ms", "0");
  outgoing.append("scene_instruction", SCENE_INSTRUCTIONS[purpose]);
  outgoing.append("audio", audio, "utterance.pcm");

  try {
    const upstream = await upstreamFetch(fetchImpl, `${config.baseUrl}/turn`, {
      method: "POST",
      headers: { "X-FP-Client-Key": config.clientKey },
      body: outgoing,
    });
    return new Response(upstream.body, {
      status: upstream.status,
      headers: {
        "Cache-Control": "no-store",
        "Content-Type": upstream.headers.get("content-type") || "application/json",
        "Set-Cookie": sessionCookie(token),
        "X-Content-Type-Options": "nosniff",
      },
    });
  } catch (error) {
    return serverError(error, "turn");
  }
}

export async function handleHealth(request, env = process.env, fetchImpl = fetch) {
  let baseUrl;
  try {
    baseUrl = getBaseUrl(env);
  } catch (error) {
    return serverError(error, "health configuration");
  }
  try {
    const upstream = await upstreamFetch(fetchImpl, `${baseUrl}/health`, {
      headers: { Accept: "application/json" },
    });
    return new Response(upstream.body, {
      status: upstream.status,
      headers: {
        "Cache-Control": "no-store",
        "Content-Type": upstream.headers.get("content-type") || "application/json",
        "X-Content-Type-Options": "nosniff",
      },
    });
  } catch (error) {
    return serverError(error, "health");
  }
}

export async function handleReset(request, env = process.env, fetchImpl = fetch) {
  if (!sameOrigin(request)) return json({ ok: false, error: "invalid origin" }, 403);

  let config;
  try {
    config = getPrivateConfig(env);
  } catch (error) {
    return serverError(error, "reset configuration");
  }

  const token = readCookie(request, COOKIE_NAME);
  const sessionId = verifySessionToken(token, config.signingSecret);
  const clearCookie = sessionCookie("", 0);
  if (!sessionId) return json({ ok: true }, 200, { "Set-Cookie": clearCookie });

  const outgoing = new FormData();
  outgoing.append("session_id", sessionId);
  try {
    const upstream = await upstreamFetch(fetchImpl, `${config.baseUrl}/session/reset`, {
      method: "POST",
      headers: { "X-FP-Client-Key": config.clientKey },
      body: outgoing,
    });
    return new Response(upstream.body, {
      status: upstream.status,
      headers: {
        "Cache-Control": "no-store",
        "Content-Type": upstream.headers.get("content-type") || "application/json",
        "Set-Cookie": clearCookie,
        "X-Content-Type-Options": "nosniff",
      },
    });
  } catch (error) {
    return serverError(error, "reset");
  }
}
