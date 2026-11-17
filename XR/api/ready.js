export const maxDuration=10;
export async function GET(){
 const env=process.env;
 if(!env.SIDECAR_BASE_URL||!env.FP_CLIENT_KEY||String(env.SESSION_SIGNING_SECRET||'').length<32)return Response.json({ready:false},{headers:{'Cache-Control':'no-store'}});
 try{const base=env.SIDECAR_BASE_URL.replace(/\/$/,'');const r=await fetch(base+'/openapi.json',{signal:AbortSignal.timeout(5000)});const schema=await r.json();return Response.json({ready:r.ok&&Boolean(schema.paths?.['/statement'])},{headers:{'Cache-Control':'no-store'}});}catch{return Response.json({ready:false},{headers:{'Cache-Control':'no-store'}});}
}
