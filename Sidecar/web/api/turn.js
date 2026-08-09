import { handleTurn } from "../server/sidecar-proxy.js";

export const maxDuration = 70;

export async function POST(request) {
  return handleTurn(request);
}
