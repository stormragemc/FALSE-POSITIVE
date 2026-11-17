import { AssetManager, Mesh, PlaneGeometry, MeshBasicMaterial, SRGBColorSpace } from '@iwsdk/core';
import { setupCastActor } from './cast.js';

/** Real cast, stable viewpoints and authored blocking for the Unity story beats. */
export class CinematicPresentation {
 constructor(world){
  this.world=world;this.objects=Object.fromEntries(['room','cabin','desk','chair','officer','radio','key','photo','memory-door','sofa'].map(id=>[id,world.requireSceneObject(id)]));
  this.cast=Object.fromEntries(['aaron','ivy','nick','priya'].map(role=>{const root=world.requireSceneObject(role);root.visible=false;return [role,{root,actor:setupCastActor(root,{role})}];}));
  this.fade=new Mesh(new PlaneGeometry(8,8),new MeshBasicMaterial({color:0x000000,transparent:true,opacity:0,depthTest:false,depthWrite:false}));this.fade.renderOrder=10000;this.fade.position.z=-.1;world.camera.add(this.fade);this.fadeAge=9;
  const texture=AssetManager.getTexture('cast-photo');if(texture){texture.colorSpace=SRGBColorSpace;const face=this.objects.photo.getObjectByName('Photograph image');if(face){face.material=face.material.clone();face.material.map=texture;face.material.color.set('#ffffff');face.material.needsUpdate=true;}}
  this.clear();
 }
 rest(phase){this.clear();if(phase==='night'||phase==='morning'){this.show({cutscene:'rest-'+phase,stage:phase,speaker:''});this.active=false;this.fade.material.opacity=0;}}
 clear(){this.active=false;this.current=null;this.lastScene='';for(const v of Object.values(this.cast))v.root.visible=false;this.objects.sofa.visible=false;this.fade.material.opacity=0;}
 place(role,position,pose='standing',yaw=0){const {root,actor}=this.cast[role];root.visible=true;root.position.set(...position);root.rotation.y=yaw;actor.setPose(pose);return root;}
 show(beat){
  this.active=true;this.current=beat;this.speaker=String(beat.speaker||'').toLowerCase();
  if(this.lastScene===beat.cutscene)return;
  this.lastScene=beat.cutscene;this.elapsed=0;this.fadeAge=0;
  const stage=beat.stage||'interrogation';this.stage=stage;
  for(const v of Object.values(this.cast))v.root.visible=false;
  const interview=stage.includes('interrogation')||stage.startsWith('ending')||stage==='interrogation';
  this.objects.room.visible=interview;this.objects.cabin.visible=!interview;this.objects.officer.visible=interview;this.objects.chair.visible=interview;this.objects.desk.visible=interview||stage==='good-years'||stage==='went-wrong'||stage==='night';
  this.objects.radio.visible=stage==='night';this.objects.key.visible=false;this.objects.photo.visible=beat.cutscene==='P3Photograph';this.objects['memory-door'].visible=!interview;this.objects.sofa.visible=['sofa','carry','morning','morning-reveal'].includes(stage);
  if(stage==='night'){this.place('nick',[.9,0,-2.0],'guarded',-.6);}
  if(stage==='night-door'){this.place('nick',[.8,0,-3.2],'standing',Math.PI);}
  if(['morning','morning-reveal','outside','carry','sofa'].includes(stage)){
   this.place('priya',[-.9,0,-1.85],'guarded',.25);
   this.place('ivy',[.5,0,-2.45],'guarded',-.15);
   this.place('aaron',[1.15,0,-2.2],stage==='carry'?'carry':'standing',-.4);
   const body=stage==='sofa'?[1.82,.51,-2.42]:stage==='carry'?[.1,.72,-2.15]:[-.1,.03,-1.1];
   this.place('nick',body,'lying',stage==='sofa'?Math.PI/2-.35:stage==='carry'?Math.PI:0);
  }
  if(stage==='good-years'){
   this.place('priya',[-1.12,0,-2.4],'toast',.3);this.place('nick',[-.4,0,-2.1],'toast',.1);this.place('ivy',[.4,0,-2.1],'toast',-.1);this.place('aaron',[1.12,0,-2.4],'toast',-.3);
  }
  if(stage==='went-wrong'){
   this.place('priya',[-1.4,0,-2.65],'sitting',.5);this.place('nick',[-.35,0,-1.75],'guarded',-.55);this.place('aaron',[.65,0,-2],'guarded',.55);this.place('ivy',[1.3,0,-2.7],'guarded',-.3);
  }
  if(stage.startsWith('accusation-'))this.place(stage.slice(11),[0,0,-2.2],'guarded');
  if(stage.startsWith('ending-')&&stage!=='ending-david')this.place(stage.slice(7),[1,0,-2.25],'guarded',-.3);
 }
 update(delta,time,paused=false){
  if(!this.active)return;if(!paused){this.elapsed+=delta;this.fadeAge+=delta;}
  // A brief fade replaces forced head motion. Tracking always owns the camera.
  this.fade.material.opacity=this.current?.endsBlack?1:Math.max(0,1-this.fadeAge/.7);
  const ending=this.stage?.startsWith('ending-');
  for(const [role,{root,actor}] of Object.entries(this.cast)){
   if(!root.visible)continue;
   const walking=ending&&role===this.stage.slice(7)&&this.elapsed>2&&this.elapsed<7;
   if(walking){const t=Math.min(1,(this.elapsed-2)/5);root.position.set(1-3*t,0,-2.25-.8*t);root.rotation.y=-Math.PI/2;}
   actor.update({time,delta:paused?0:delta,talking:this.speaker===role,walking,dead:role==='nick'&&['outside','carry','sofa','morning','morning-reveal'].includes(this.stage)});
  }
 }
 dispose(){this.clear();this.fade.removeFromParent();this.fade.geometry.dispose();this.fade.material.dispose();for(const {actor}of Object.values(this.cast))actor.restore();}
}
