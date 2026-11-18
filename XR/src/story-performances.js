import { AssetManager, Group, Mesh, BoxGeometry, PlaneGeometry, MeshStandardMaterial, MeshBasicMaterial, DoubleSide, Vector3, Quaternion, Euler, Points, BufferGeometry, Float32BufferAttribute, PointsMaterial, PointLight, Color } from '@iwsdk/core';
import { audioGain } from './audio-gains.js';

const smooth=x=>{x=Math.max(0,Math.min(1,x));return x*x*(3-2*x);};
const ramp=(t,a,b)=>smooth((t-a)/(b-a));
const mix=(a,b,x)=>a+(b-a)*x;
const material=(color,roughness=.8)=>new MeshStandardMaterial({color,roughness});
function box(parent,name,size,position,mat){const o=new Mesh(new BoxGeometry(...size),mat);o.name=name;o.position.set(...position);parent.add(o);return o;}

/** Native 3D performances: no movie planes, no camera animation, one bone owner. */
export class StoryPerformances {
 constructor(presentation,officerActor){
  this.p=presentation;this.officer=officerActor;this.world=presentation.world;this.sounds=[];this.cues=new Set();
  this.extra=new Group();this.extra.name='Authored story staging';this.entity=this.world.createTransformEntity(this.extra);
  this.glassSet=new Group();this.extra.add(this.glassSet);
  const metal=material('#263334',.45),timber=material('#473627');
  const tint=new MeshStandardMaterial({color:'#789d9f',transparent:true,opacity:.11,roughness:.2,metalness:.1,side:DoubleSide,depthWrite:false});
  this.glass=new Mesh(new PlaneGeometry(3.3,2.1),tint);this.glass.position.set(0,1.2,-1.05);this.glassSet.add(this.glass);
  for(const x of [-1.69,1.69])box(this.glassSet,'Observation partition',[.06,2.5,.07],[x,1.25,-1.05],metal);
  box(this.glassSet,'Observation sill',[3.4,.06,.10],[0,.7,-1.05],metal);
  box(this.glassSet,'Observation top',[3.4,.06,.10],[0,2.25,-1.05],metal);
  this.folderHinge=new Group();this.folderHinge.position.set(-.59,.812,-.99);this.extra.add(this.folderHinge);
  this.folderCover=box(this.folderHinge,'Closing case cover',[.30,.006,.4],[.15,0,0],material('#77694b'));
  this.phone=box(this.extra,'Priya phone',[.073,.145,.014],[0,0,0],material('#151b1c',.3));
  this.outside=new Group();this.extra.add(this.outside);
  const snow=material('#adbcc4',1),wood=material('#503c2d');
  const tex=AssetManager.getTexture('cabin-wood');if(tex){wood.map=tex;wood.needsUpdate=true;}
  box(this.outside,'Snow',[9,.05,9],[0,-.028,-2.8],snow);
  // A window aperture with a real grille; no opening a person could climb through.
  for(const [size,pos] of [[[2.5,3,.15],[-2.15,1.5,-3.5]],[[2.5,3,.15],[2.15,1.5,-3.5]],[[1.8,.7,.15],[0,.35,-3.5]],[[1.8,1,.15],[0,2.5,-3.5]]])box(this.outside,'Exterior timber',size,pos,wood);
  box(this.outside,'Dark room beyond window',[1.8,1.3,.025],[0,1.35,-3.62],material('#101b20'));
  for(const x of [-.92,.92])box(this.outside,'Window frame',[.08,1.4,.18],[x,1.36,-3.44],timber);
  for(const y of [.65,2.07])box(this.outside,'Window frame',[1.94,.08,.18],[0,y,-3.44],timber);
  for(let i=-3;i<=3;i++)box(this.outside,'Intact window grille',[.022,1.3,.04],[i*.25,1.36,-3.32],metal);
  box(this.outside,'Window grille crossbar',[1.8,.022,.04],[0,1.36,-3.32],metal);
  this.windowGlass=new Mesh(new PlaneGeometry(1.75,1.3),new MeshStandardMaterial({color:'#7796a5',transparent:true,opacity:.32,side:DoubleSide,roughness:.15,depthWrite:false}));this.windowGlass.position.set(0,1.36,-3.29);this.outside.add(this.windowGlass);
  this.shards=new Group();this.outside.add(this.shards);
  for(let i=0;i<12;i++){const shard=new Mesh(new PlaneGeometry(.025+(i%3)*.018,.045+(i%4)*.015),tint);shard.position.set((i%4-.5)*.16,.015,-3.56-Math.floor(i/4)*.08);shard.rotation.set(-Math.PI/2,0,i*.71);this.shards.add(shard);}
  const geometry=new BufferGeometry();this.flakeData=new Float32Array(180*3);
  for(let i=0;i<180;i++){this.flakeData[i*3]=(i*1.618%1-.5)*8;this.flakeData[i*3+1]=(i*.371%1)*4;this.flakeData[i*3+2]=-.45-(i*.173%1)*6;}
  this.flakeOrigins=this.flakeData.slice();geometry.setAttribute('position',new Float32BufferAttribute(this.flakeData,3));
  this.snow=new Points(geometry,new PointsMaterial({color:'#d7e4ee',size:.023,transparent:true,opacity:.65,depthWrite:false}));this.outside.add(this.snow);
  this.coldLight=new PointLight('#759ec3',8,9,2);this.coldLight.position.set(-1,2,-1);this.outside.add(this.coldLight);
  this.landing=new Group();this.extra.add(this.landing);
  box(this.landing,'Stair landing',[1.25,.1,.9],[-1.7,.5,-2.8],timber);
  for(let i=0;i<4;i++)box(this.landing,'Stair tread',[1,.12,.26],[-1.7,.06+i*.13,-1.45-i*.28],timber);
  this.keyHook=box(this.landing,'Cabin key hook',[.022,.04,.25],[1.04,1.36,-3.13],metal);
  this.baseDoor=presentation.objects['memory-door'].position.clone();
  this.baseKey=presentation.objects.key.position.clone();this.keyQ=presentation.objects.key.quaternion.clone();this.baseChair=presentation.objects.chair.position.clone();
  this.basePhoto=presentation.objects.photo.position.clone();this.photoQ=presentation.objects.photo.quaternion.clone();
  this.installFurniture();this.clear();
 }
 installFurniture(){
  for(const name of ['desk','chair','sofa']){
   const model=AssetManager.getGLTF(`${name}-detail`)?.scene;if(!model)continue;const root=this.p.objects[name];
   for(const child of root.children){if(name==='desk'&&/folder|Paper|Printed|Pencil/.test(child.name))continue;child.visible=false;}
   model.name=`Original detailed ${name}`;root.add(model);model.traverse(o=>{if(o.isMesh){o.castShadow=true;o.receiveShadow=true;}});
  }
 }
 clear(){
  for(const {audio}of this.sounds){audio.pause();audio.currentTime=0;}this.sounds=[];this.cues.clear();
  this.extra.visible=false;this.p.objects.photo.position.copy(this.basePhoto);this.p.objects.photo.quaternion.copy(this.photoQ);
  this.p.objects['memory-door'].position.copy(this.baseDoor);
  this.p.objects.key.position.copy(this.baseKey);this.p.objects.key.quaternion.copy(this.keyQ);this.p.objects.chair.position.copy(this.baseChair);
 }
 sound(file,{loop=false,gain=1}={}){
  if(typeof Audio==='undefined')return;
  const path='/media/sfx/'+file;const audio=new Audio(path);audio.loop=loop;audio.volume=Math.min(1,audioGain(path)*gain);audio.play().catch(()=>{});this.sounds.push({audio,wasPlaying:true});return audio;
 }
 cue(id,t,at,file,gain=1){if(t>=at&&!this.cues.has(id)){this.cues.add(id);this.sound(file,{gain});}}
 onBeat(beat){this.beat=beat;if(beat.cutscene==='WhenItWentWrong'&&beat.text==='I need some air.')this.departureAt=this.p.elapsed+.1;}
 start(beat){
  this.clear();this.beat=beat;this.stage=beat.stage;this.extra.visible=true;this.departureAt=Infinity;this.snow.material.opacity=.65;this.coldLight.color.set('#759ec3');this.coldLight.intensity=8;
  this.glassSet.visible=this.stage?.startsWith('ending-')&&this.stage!=='ending-david';
  this.outside.visible=['truth-window','truth-cold','truth-coats','outside'].includes(this.stage);
  this.landing.visible=this.stage==='truth-lock';this.folderHinge.visible=this.stage==='ending-david';this.phone.visible=beat.cutscene==='TheSofa';
  const {objects,cast}=this.p;
  if(this.glassSet.visible){
   objects.desk.visible=false;objects.officer.visible=false;objects.photo.visible=false;
   objects.chair.visible=true;objects.chair.position.set(0,0,-2.3);
   const role=this.stage.slice(7);this.p.place(role,[0,0,-2.2],'sitting');
  }else objects.chair.position.set(0,0,-1.6);
  if(this.stage==='ending-david'){objects.photo.visible=false;this.folderHinge.rotation.z=.72;}
  if(this.stage==='outside'){objects.room.visible=false;objects.cabin.visible=false;objects.desk.visible=false;objects['memory-door'].visible=false;this.windowGlass.visible=false;this.shards.visible=true;this.coldLight.color.set('#adc6d5');this.snow.material.opacity=.35;this.p.place('nick',[.85,.086,-3],'lying',-Math.PI/2);this.sound('menu_storm_bed.mp3',{loop:true,gain:.5});}
  if(beat.cutscene==='SomeoneLeft')this.p.setDoorOpen(true);
  if(this.stage?.startsWith('truth-')){
   for(const {root}of Object.values(cast))root.visible=false;
   objects.room.visible=false;objects.cabin.visible=this.stage==='truth-lock'||this.stage==='truth-morning';objects.officer.visible=false;objects.desk.visible=false;objects.chair.visible=false;objects.photo.visible=false;objects.radio.visible=false;objects.sofa.visible=false;
   objects['memory-door'].visible=this.stage==='truth-lock';objects.key.visible=false;
   if(this.outside.visible){
    this.sound('menu_storm_bed.mp3',{loop:true,gain:.65});
    this.windowGlass.visible=this.stage==='truth-window'||this.stage==='truth-coats';this.shards.visible=!this.windowGlass.visible;
   }
   if(this.stage==='truth-coats'){this.p.place('nick',[0,0,-2.4],'guarded',.1);}
   if(this.stage==='truth-window'){this.p.place('nick',[-.4,0,-2.75],'guarded',0);this.p.place('aaron',[-.4,0,-2.17],'guarded',Math.PI);}
   if(this.stage==='truth-lock'){this.p.place('aaron',[1.52,0,-2.85],'standing',Math.PI);this.p.place('ivy',[-1.7,.55,-2.8],'standing',.25);this.p.setDoorOpen(false);objects.key.visible=true;}
   // The broadside silhouette stays above the low caption panel. The old
   // head-toward-camera blocking hid most of Nick behind the Skip controls.
   if(this.stage==='truth-cold')this.p.place('nick',[.85,.086,-3],'lying',-Math.PI/2);
   if(this.stage==='truth-morning'){
    objects.sofa.visible=true;objects.sofa.position.set(0,0,-2.35);objects.sofa.rotation.set(0,0,0);
    const body=this.p.place('nick',[.83,.605,-2.23],'lying',Math.PI/2);
    this.p.place('priya',[-1.35,0,-1.9],'guarded',.15);this.p.place('aaron',[1.4,0,-2.8],'guarded',-.3);
   }
   if(this.stage==='truth-conclusion'){objects.room.visible=true;objects.officer.visible=true;objects.chair.visible=true;objects.chair.position.set(0,0,-1.6);objects.desk.visible=true;}
  }
 }
 update(delta,t,paused){
  const {cast,objects}=this.p;const handled=new Set();const stage=this.stage;
  if(this.paused!==paused){for(const s of this.sounds){if(paused){s.wasPlaying=!s.audio.paused;s.audio.pause();}else if(s.wasPlaying)s.audio.play().catch(()=>{});}this.paused=paused;}
  if(paused)return new Set(Object.keys(cast));
  if(stage==='outside'){const n=cast.nick;n.actor.update({time:t,delta,dead:true});n.actor.rig.quaternion.copy(n.actor.restRig.quaternion).multiply(new Quaternion().setFromEuler(new Euler(Math.PI/2,0,0)));handled.add('nick');}
  if(this.beat?.cutscene==='SomeoneLeft'){
   const hinge=objects['memory-door'].getObjectByName('Door hinge');if(hinge)hinge.rotation.y=mix(-.65,0,ramp(t,.1,1.0));this.cue('latch',t,.946,'door_latch_close.mp3');
  }
  if(objects.officer.visible){
   let performance={};
   if(['SpasskyAnswer','P3Photograph'].includes(this.beat?.cutscene)){
    const slide=ramp(t,3.5,5.3),reach=ramp(t,2.8,3.8)-ramp(t,6.2,7.4);
    performance={lean:.22*(ramp(t,2.3,3.5)-ramp(t,8,9.5)),right:[mix(-.205,-.12,reach),.827,mix(.31,.34+.27*slide,reach)],gaze:.07*(1-slide)};
    objects.photo.visible=true;objects.photo.position.set(-.12,.797,-1.16+.27*slide);objects.photo.rotation.set(-Math.PI/2,0,0);
   }
   if(stage==='ending-david'){
    const close=ramp(t,4,6.2);this.folderHinge.rotation.z=mix(.72,0,close);const reach=ramp(t,3.1,4.2)-ramp(t,6.4,7.5);
    performance={lean:.14*(ramp(t,3.2,4.3)-ramp(t,7,8.2)),right:[mix(-.205,-.40,reach),.827+.1*reach*(1-close),.43],gaze:.35*ramp(t,7.8,9.4)};
    this.cue('folder',t,5.9,'chair_creak.mp3',.55);this.cue('guards',t,7.9,'footsteps_wood.mp3',.45);
   }
   this.officer?.update(this.p.audioSpeaking&&this.p.speaker==='spassky',delta,t,undefined,performance);
  }
  if(this.glassSet.visible){
   const role=stage.slice(7),actor=cast[role]?.actor;
   if(actor){
    const wring=role==='priya'?.018*Math.sin(t*2.7):0;
    actor.update({time:t,delta,talking:this.p.audioSpeaking&&this.p.speaker===role,performance:{lean:role==='ivy'?.20:role==='priya'?.11:.015,gaze:role==='priya'?.16*Math.sin(t*.6):role==='ivy'?-.12:.02,nod:role==='ivy'?.13:0,left:[.085+wring,.645,.20],right:[-.085+wring,.65,.21]}});handled.add(role);
   }
  }
  if(this.beat?.cutscene==='WhenItWentWrong'){
   const nick=cast.nick,t0=Number.isFinite(this.departureAt)?t-this.departureAt:-100;
   if(nick.root.visible){const depart=ramp(t0,.9,6.3);if(depart>.01)nick.actor.setPose('standing');nick.root.position.set(mix(-.35,1.5,depart),0,mix(-1.75,-3.65,depart));nick.root.rotation.y=mix(-.55,Math.atan2(1.85,-1.9),ramp(t0,0,1.1));nick.actor.update({time:t,delta,talking:this.p.audioSpeaking&&this.p.speaker==='nick',walking:depart>.01&&depart<.98,walkPhase:Math.hypot(1.85,1.9)*depart/.5});handled.add('nick');const hinge=objects['memory-door'].getObjectByName('Door hinge');if(hinge)hinge.rotation.y=-.9*(ramp(t0,2.7,3.7)-ramp(t0,6.4,7.2));this.cue('departure-latch',t0,7.146,'door_latch_close.mp3');}
  }
  if(this.beat?.cutscene==='TheSofa'){
   const priya=cast.priya,lift=ramp(t,2.4,4.6);priya.actor.update({time:t,delta,talking:this.p.audioSpeaking&&this.p.speaker==='priya',performance:{right:[-.16,mix(1.02,1.56,lift),mix(.24,.12,lift)],gaze:.12}});handled.add('priya');
   const hand=priya.actor.bones.get('RightHand');if(hand){hand.getWorldPosition(this.phone.position);this.phone.position.y+=.025;this.phone.quaternion.copy(priya.root.quaternion);}
  }
  if(stage?.startsWith('truth-')){
   if(this.outside.visible){
    const a=this.snow.geometry.attributes.position;for(let i=0;i<180;i++){a.array[i*3]=this.flakeOrigins[i*3]+.28*Math.sin(t*.6+i);a.array[i*3+1]=((this.flakeOrigins[i*3+1]-t*(.55+i%5*.07))%4+4)%4;}a.needsUpdate=true;
   }
   if(stage==='truth-coats'){cast.nick.actor.update({time:t,delta,talking:false,performance:{right:[-.23,1.20,.20],gaze:-.15}});handled.add('nick');}
   if(stage==='truth-window'){
    const hit=ramp(t,1.7,2.6),fall=ramp(t,2.7,5.7),aaron=cast.aaron,nick=cast.nick;
    nick.root.position.set(mix(-.4,.85,fall),.086*fall,-2.75-.48*hit+.23*fall);nick.root.rotation.y=-Math.PI/2*fall;
    nick.actor.update({time:t,delta,dead:fall>.5,performance:{lean:-.10*hit}});
    nick.actor.rig.quaternion.copy(nick.actor.restRig.quaternion).multiply(new Quaternion().setFromEuler(new Euler(Math.PI/2*fall,0,0)));nick.actor.rig.position.copy(nick.actor.restRig.position);nick.actor.rig.position.y+=.125*fall;
    nick.root.updateWorldMatrix(true,true);handled.add('nick');
    // A lateral stance exposes Nick and the impact from the seated view.
    aaron.root.position.set(-.78,0,-2.30-.48*hit+.50*ramp(t,3.0,5.8));aaron.root.rotation.y=Math.PI;aaron.root.updateWorldMatrix(true,true);
    const contact=ramp(t,1.2,1.8)*(1-ramp(t,2.65,3.5));
    const shoulder=nick.actor.bones.get('RightArm').getWorldPosition(new Vector3());const local=aaron.actor.rig.worldToLocal(shoulder);
    aaron.actor.update({time:t,delta,performance:{right:[mix(-.23,local.x,contact),mix(.98,local.y,contact),mix(.04,local.z,contact)],lean:.06*hit}});handled.add('aaron');
    this.windowGlass.visible=t<2.5;this.shards.visible=t>=2.5;this.cue('impact',t,2.5,'glass_crunch.mp3',.65);
   }
   if(stage==='truth-lock'){
    const actor=cast.aaron.actor,key=objects.key,hang=ramp(t,3.7,5.3);
    key.position.set(mix(1.74,1.04,hang),mix(1.12,1.36,hang),-3.01);key.rotation.set(0,0,Math.PI/2*ramp(t,2.2,3.0));
    cast.aaron.root.position.x=1.52-.70*hang-1.0*ramp(t,5.8,7);cast.aaron.root.rotation.y=mix(Math.PI,-1.8,ramp(t,5.5,6.5));cast.aaron.root.updateWorldMatrix(true,true);
    const reach=ramp(t,.8,1.9)*(1-ramp(t,5.3,5.8)),local=actor.rig.worldToLocal(key.position.clone().add(new Vector3(0,-.025,.025)));
    actor.update({time:t,delta,walking:(t>3.7&&t<5.3)||t>5.8,walkPhase:.70*hang/.5,performance:{right:[mix(-.23,local.x,reach),mix(.98,local.y,reach),mix(.04,local.z,reach)]}});handled.add('aaron');
    this.cue('lock',t,2.732,'door_bolt_lock.mp3');this.cue('stairs',t,5.8,'footsteps_stairs.mp3',.65);
   }
   if(stage==='truth-cold'){
    const nick=cast.nick;nick.root.position.x=.85+.004*(1-ramp(t,0,7))*Math.sin(t*19);nick.actor.update({time:t,delta,dead:true});nick.actor.rig.quaternion.copy(nick.actor.restRig.quaternion).multiply(new Quaternion().setFromEuler(new Euler(Math.PI/2,0,0)));handled.add('nick');
    this.coldLight.color.copy(new Color('#41618e')).lerp(new Color('#adc6d5'),ramp(t,5.5,10));this.coldLight.intensity=mix(4,10,ramp(t,5.5,10));
    this.snow.material.opacity=mix(.78,.35,ramp(t,6,10));
   }
  }
  return handled;
 }
 dispose(){this.clear();this.entity.dispose({disposeResources:false});this.extra.traverse(o=>{if(o.isMesh||o.isPoints)o.geometry?.dispose();});}
}
