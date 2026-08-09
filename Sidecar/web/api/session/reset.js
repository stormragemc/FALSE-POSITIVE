import { handleReset } from "../../server/sidecar-proxy.js";

export async function POST(request) {
  return handleReset(request);
}
