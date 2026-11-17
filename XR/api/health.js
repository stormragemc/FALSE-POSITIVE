import { handleHealth } from '../../Sidecar/web/server/sidecar-proxy.js';
export async function GET(request){return handleHealth(request);}
