import { AssetManager, Mesh, PlaneGeometry, MeshBasicMaterial, SRGBColorSpace, Quaternion, Vector3 } from '@iwsdk/core';
import { setupCastActor } from './cast.js';
import { StoryPerformances } from './story-performances.js';

/** Real cast, stable viewpoints and authored blocking for the Unity story beats. */
export class CinematicPresentation {
 constructor(world,{officerActor}={}){
  this.world=world;this.objects=Object.fromEntries(['room','cabin','desk','chair','officer','radio','key','photo','memory-door','sofa','controls'].map(id=>[id,world.requireSceneObject(id)]));
  this.cast=Object.fromEntries(['aaron','ivy','nick','priya'].map(role=>{const root=world.requireSceneObject(role);root.visible=false;return [role,{root,actor:setupCastActor(root,{role})}];}));
  // Reuse the authored furniture, including its actual seat height. Priya's
  // seated memory must have a chair; shared geometry is never disposed here.
  this.memoryChair=this.objects.chair.clone(true);this.memoryChair.name='Priya memory chair';this.memoryChairEntity=world.createTransformEntity(this.memoryChair);
  this.pillow=this.objects.sofa.getObjectByName('Seat cushion').clone();this.pillow.name='Nick head cushion';this.pillow.scale.set(.5,.4,.4);this.pillowEntity=world.createTransformEntity(this.pillow);
  this.sofaScale=this.objects.sofa.scale.clone();this.objects.sofa.scale.x=this.sofaScale.x*1.12;
  this.sofaPosition=this.objects.sofa.position.clone();this.sofaRotation=this.objects.sofa.quaternion.clone();
  this.pillowLocalRotation=this.pillow.quaternion.clone();
  this.fade=new Mesh(new PlaneGeometry(8,8),new MeshBasicMaterial({color:0x000000,transparent:true,opacity:0,depthTest:false,depthWrite:false}));this.fade.renderOrder=10000;this.fade.position.z=-.1;world.camera.add(this.fade);this.fadeAge=9;
  const texture=AssetManager.getTexture('cast-photo');if(texture){texture.colorSpace=SRGBColorSpace;const face=this.objects.photo.getObjectByName('Photograph image');if(face){face.material=face.material.clone();face.material.map=texture;face.material.color.set('#ffffff');face.material.needsUpdate=true;}}
  this.controlsPosition=this.objects.controls.position.clone();this.controlsQuaternion=this.objects.controls.quaternion.clone();
  this.motion=new StoryPerformances(this,officerActor);this.clear();
 }
 rest(phase){this.clear();this.objects.desk.visible=phase!=='morning';const cups=this.objects.cabin.getObjectByName('Cabin cups');if(cups)cups.visible=phase==='night';if(phase==='night'||phase==='morning'){this.show({cutscene:'rest-'+phase,stage:phase,speaker:''});this.active=false;this.fade.material.opacity=0;}this.cinematicUI(false);}
 cinematicUI(active){
  const ui=this.objects.controls;if(!this.controlsPosition)return;
  if(active){ui.position.set(0,.875,-.42);ui.rotation.set(-Math.PI*65/180,0,0);}else{ui.position.copy(this.controlsPosition);ui.quaternion.copy(this.controlsQuaternion);}
  for(const id of ['objective','mode-status'])ui.getElementById(id)?.setProperties({display:active?'none':'flex'});
  ui.getElementById('officer-line')?.setProperties({fontSize:active?3.3:2.5});
 }
 clear(){this.cinematicUI(false);this.motion?.clear();this.active=false;this.current=null;this.lastScene='';for(const v of Object.values(this.cast))v.root.visible=false;this.objects.sofa.visible=false;this.memoryChair.visible=false;this.pillow.visible=false;this.fade.material.opacity=0;}
 place(role,position,pose='standing',yaw=0){const {root,actor}=this.cast[role];root.visible=true;root.position.set(...position);root.rotation.y=yaw;actor.setPose(pose);return root;}
 show(beat){
  this.active=true;this.cinematicUI(true);this.current=beat;this.speaker=String(beat.speaker||'').toLowerCase();
  const sceneKey=beat.cutscene+':'+beat.stage;
  this.motion.onBeat(beat);
  if(this.lastScene===sceneKey)return;
  this.lastScene=sceneKey;this.elapsed=0;this.fadeAge=0;
  const stage=beat.stage||'interrogation';this.stage=stage;this.setDoorOpen(['night-door','outside','carry','sofa'].includes(stage));
  for(const v of Object.values(this.cast))v.root.visible=false;
  this.objects.sofa.position.copy(this.sofaPosition);this.objects.sofa.quaternion.copy(this.sofaRotation);
  const interview=stage.includes('interrogation')||stage.startsWith('ending')||stage==='interrogation';
  this.objects.room.visible=interview;this.objects.cabin.visible=!interview;this.objects.officer.visible=interview;this.objects.chair.visible=interview;this.objects.desk.visible=interview||stage==='good-years'||stage==='went-wrong'||stage==='night';
  this.objects.radio.visible=stage==='night';this.objects.key.visible=false;this.objects.photo.visible=beat.cutscene==='P3Photograph';this.objects['memory-door'].visible=!interview;this.objects.sofa.visible=['sofa','morning','morning-reveal'].includes(stage);this.memoryChair.visible=false;this.pillow.visible=false;
  const cups=this.objects.cabin.getObjectByName('Cabin cups');if(cups)cups.visible=this.objects.desk.visible;
  if(stage==='night'&&beat.cutscene==='NightArgument'){this.place('nick',[.9,0,-2.0],'guarded',-.6);}
  // David hears the closing door but does not see who went out. Preserve that trap.
  if(['morning','morning-reveal','outside','carry','sofa'].includes(stage)){
   this.place('priya',[-.9,0,-1.85],'guarded',.25);
   this.place('ivy',[.5,0,-2.45],'guarded',-.15);
   this.place('aaron',[1.15,0,-2.2],'standing',-.4);
   this.place('nick',[-.1,.025,-1.1],'lying');
   if(stage==='carry'){
    // David (the first-person witness) holds the shoulders. Aaron faces him
    // at the far end, with both upturned palms directly under Nick's ankles.
    // The head remains >0.7m from the stationary seated viewpoint.
    // A diagonal exposes the supporting palms; straight-on blocking hid both
    // hands behind Nick's torso and made the supported body look suspended.
    this.place('aaron',[.525,0,-2.894],'carry',-.4);
    this.place('nick',[.4,1,-2.60],'lying',Math.PI-.4);
    this.place('priya',[-1.3,0,-2.8],'guarded',.25);
    this.place('ivy',[1.2,0,-2.8],'guarded',-.25);
   }
   if(stage==='sofa'){
    const sofa=this.objects.sofa;sofa.position.set(0,0,-2.35);sofa.rotation.set(0,0,0);sofa.updateWorldMatrix(true,false);
    const body=this.place('nick',[0,0,0],'lying');
    body.position.copy(sofa.localToWorld(new Vector3(.83,.605,.12)));body.parent?.worldToLocal(body.position);
    const yaw=new Quaternion().setFromAxisAngle(new Vector3(0,1,0),Math.PI/2);
    body.quaternion.copy(sofa.getWorldQuaternion(new Quaternion())).multiply(yaw);
    if(body.parent)body.quaternion.premultiply(body.parent.getWorldQuaternion(new Quaternion()).invert());
    // The back of the coat touches the actual seat top (~0.538m). A small
    // existing cushion supports the skull, rather than leaving it hovering.
    this.pillow.visible=true;this.pillow.position.copy(sofa.localToWorld(new Vector3(-.75,.58,.12)));this.pillow.parent?.worldToLocal(this.pillow.position);
    this.pillow.quaternion.copy(sofa.getWorldQuaternion(new Quaternion())).multiply(this.pillowLocalRotation);
    if(this.pillow.parent)this.pillow.quaternion.premultiply(this.pillow.parent.getWorldQuaternion(new Quaternion()).invert());
    this.place('aaron',[-1.5,0,-2.9],'guarded',.35);
    this.place('priya',[1.55,0,-2.05],'standing',-.35);
    this.place('ivy',[.45,0,-3.0],'standing',-.15);
   }
  }
  if(stage==='good-years'){
   this.place('priya',[-1.12,0,-2.4],'toast',.3);this.place('nick',[-.4,0,-2.1],'toast',.1);this.place('ivy',[.4,0,-2.1],'toast',-.1);this.place('aaron',[1.12,0,-2.4],'toast',-.3);
  }
  if(stage==='went-wrong'){
   this.place('priya',[-1.4,0,-2.65],'sitting',.5);this.place('nick',[-.35,0,-1.75],'guarded',-.55);this.place('aaron',[.65,0,-2],'guarded',.55);this.place('ivy',[1.3,0,-2.7],'guarded',-.3);
   this.memoryChair.visible=true;this.memoryChair.position.set(-1.4-Math.sin(.5)*.1,0,-2.65-Math.cos(.5)*.1);this.memoryChair.rotation.y=.5;
  }
  if(stage.startsWith('accusation-'))this.place(stage.slice(11),[0,0,-2.2],'guarded');
  if(stage.startsWith('ending-')&&stage!=='ending-david')this.place(stage.slice(7),[1,0,-2.25],'guarded',-.3);
  this.motion.start(beat);
 }
 setDoorOpen(open){const hinge=this.objects['memory-door'].getObjectByName('Door hinge');if(hinge)hinge.rotation.y=open?-1.15:0;}
 update(delta,time,paused=false){
  if(!this.active)return;if(!paused){this.elapsed+=delta;this.fadeAge+=delta;}
  // A brief fade replaces forced head motion. Tracking always owns the camera.
  this.fade.material.opacity=this.current?.endsBlack?1:Math.max(0,1-this.fadeAge/.7);
  const ending=this.stage?.startsWith('ending-');
  const handled=this.motion.update(paused?0:delta,this.elapsed,paused);
  for(const [role,{root,actor}] of Object.entries(this.cast)){
   if(!root.visible)continue;
   if(handled.has(role))continue;
   const carry=this.stage==='carry',carrying=carry&&this.elapsed>.8&&this.elapsed<4.8;
   const walking=(ending&&role===this.stage.slice(7)&&this.elapsed>2&&this.elapsed<7)||(carrying&&role==='aaron');
   if(carry&&(role==='aaron'||role==='nick')){
    const step=Math.min(.1,Math.max(0,this.elapsed-.8)*.025);
    root.position.z=(role==='aaron'?-2.894:-2.60)+step;
   }
   if(walking&&ending){const t=Math.min(1,(this.elapsed-2)/5);root.position.set(1-3*t,0,-2.25-.8*t);root.rotation.y=-Math.PI/2;}
   actor.update({time:this.elapsed,delta:paused?0:delta,talking:this.audioSpeaking&&this.speaker===role,walking,dead:role==='nick'&&['outside','carry','sofa','morning','morning-reveal','truth-morning'].includes(this.stage)});
  }
 }
 dispose(){this.clear();this.motion.dispose();this.fade.removeFromParent();this.fade.geometry.dispose();this.fade.material.dispose();this.memoryChairEntity.dispose({disposeResources:false});this.pillowEntity.dispose({disposeResources:false});this.objects.sofa.scale.copy(this.sofaScale);this.objects.sofa.position.copy(this.sofaPosition);this.objects.sofa.quaternion.copy(this.sofaRotation);for(const {actor}of Object.values(this.cast))actor.restore();}
}
