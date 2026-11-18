import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { registerHooks } from 'node:module';
import { Bone, Group, Matrix4, Vector3 } from 'three';
const hooks=registerHooks({resolve(s,c,next){if(s==='@iwsdk/core'&&c.parentURL?.includes('/XR/src/'))return next('three',c);return next(s,c);}});
const {setupOfficer}=await import('../src/officer.js');hooks.deregister();
function actualRig(){
 const b=readFileSync(new URL('../../Assets/_Project/Art/NewCop.glb',import.meta.url));
 const j=JSON.parse(b.subarray(20,20+b.readUInt32LE(12)).toString());const joints=new Set(j.skins.flatMap(s=>s.joints));
 const objects=j.nodes.map((n,i)=>{const o=joints.has(i)?new Bone():new Group();o.name=n.name||'';if(n.matrix)new Matrix4().fromArray(n.matrix).decompose(o.position,o.quaternion,o.scale);else{if(n.translation)o.position.fromArray(n.translation);if(n.rotation)o.quaternion.fromArray(n.rotation);if(n.scale)o.scale.fromArray(n.scale);}return o;});
 j.nodes.forEach((n,i)=>(n.children||[]).forEach(k=>objects[i].add(objects[k])));const root=new Group();for(const i of j.scenes[j.scene||0].nodes)root.add(objects[i]);return root;
}
test('actual officer skeleton keeps both resting hands at the tabletop through breathing and gaze',()=>{
 const root=actualRig();root.position.z=-1.5;root.updateWorldMatrix(true,true);
 const officer=setupOfficer(root,{handHeight:.827});
 for(let t=0;t<12;t+=.2){officer.update(true,.2,t,.08);for(const side of ['Left','Right']){
  const p=officer.bones.get(side+'Hand').getWorldPosition(new Vector3());
  assert.ok(Math.abs(p.y-.827)<.006,`${side} hand height ${p.y} at ${t}`);
  assert.ok(p.z> -1.4&&p.z<-.43,`${side} hand is over the actual tabletop`);
 }}officer.restore();
});
