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
    // OpenAPI is deliberately disabled on this backend. A method-only probe
    // passes its auth middleware without running an LLM turn or changing a
    // session. Existing POST route => 405/Allow:POST; missing route => 404.
    const response = await fetch(`${base}/statement`, {
      method: 'OPTIONS',
      headers: { 'X-FP-Client-Key': key },
      cache: 'no-store',
      signal: AbortSignal.timeout(5000),
    });
    const allowed = (response.headers.get('allow') || '').split(',').map(value => value.trim().toUpperCase());
    return Response.json({ ready: response.status === 405 && allowed.includes('POST') }, { headers });
  } catch {
    return Response.json({ ready: false }, { headers });
  }
}
