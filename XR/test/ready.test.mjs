import test from 'node:test';
import assert from 'node:assert/strict';
import { GET } from '../api/ready.js';

test('readiness accepts authenticated 405 without host-stripped Allow and rejects failures', async () => {
 const old={...process.env};const original=globalThis.fetch;
 Object.assign(process.env,{FP_CLIENT_KEY:'test-backend-key',SESSION_SIGNING_SECRET:'test-signing-secret-with-more-than-32-characters',SIDECAR_BASE_URL:'https://backend.example'});
 try{
  for(const status of [405,401,404,500]){
   globalThis.fetch=async(url,options)=>{assert.equal(url,'https://backend.example/session/reset');assert.equal(options.method,'GET');assert.equal(options.headers['X-FP-Client-Key'],'test-backend-key');return new Response('',{status});};
   const response=await GET();assert.deepEqual(await response.json(),{ready:status===405});
  }
 }finally{globalThis.fetch=original;process.env=old;}
});
