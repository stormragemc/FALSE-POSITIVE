export const maxDuration = 10;

export async function GET() {
  const env = process.env;
  const key = String(env.FP_CLIENT_KEY || '').trim();
  const secret = String(env.SESSION_SIGNING_SECRET || '').trim();
  const headers = { 'Cache-Control': 'no-store' };
  if (!env.SIDECAR_BASE_URL || !key || secret.length < 32) {
    return Response.json({ ready: false }, { headers });
  }
  try {
    const base = env.SIDECAR_BASE_URL.replace(/\/$/, '');
    // OpenAPI is deliberately disabled on this backend. A GET method-only probe
    // passes its auth middleware without running an LLM turn or changing a
    // session. Wrong keys are rejected by middleware with 401. The known
    // POST-only route returns 405 on GET; the host may strip Allow headers.
    const response = await fetch(`${base}/session/reset`, {
      method: 'GET',
      headers: { 'X-FP-Client-Key': key },
      cache: 'no-store',
      signal: AbortSignal.timeout(8000),
    });
    return Response.json({ ready: response.status === 405 }, { headers });
  } catch {
    return Response.json({ ready: false }, { headers });
  }
}
