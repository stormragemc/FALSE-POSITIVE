import { handleHealth } from "../server/sidecar-proxy.js";

export async function GET(request) {
  return handleHealth(request);
}
