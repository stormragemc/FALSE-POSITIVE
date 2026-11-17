import { handleStatement } from "../server/sidecar-proxy.js";

export const maxDuration = 70;

export async function POST(request) {
  return handleStatement(request);
}
