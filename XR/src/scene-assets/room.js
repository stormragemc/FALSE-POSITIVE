import { Group, Mesh, BoxGeometry, CylinderGeometry, SphereGeometry, TorusGeometry, PlaneGeometry, MeshStandardMaterial, MeshBasicMaterial, DoubleSide, Shape, ExtrudeGeometry } from '@iwsdk/core';

const mat=(color,roughness=.8,metalness=0)=>new MeshStandardMaterial({color,roughness,metalness});
const concrete=mat('#313b3e'), dark=mat('#121a1c'), steel=mat('#677276',.4,.7), wood=mat('#543c2b'), leather=mat('#252b29'), paper=mat('#c6b99a');
const glow=new MeshBasicMaterial({color:'#e7e2cc'}), snow=new MeshBasicMaterial({color:'#7f9da8'});
const glass=new MeshStandardMaterial({color:'#4a656d',metalness:.7,roughness:.22});
function box(root,name,size,position,material){const m=new Mesh(new BoxGeometry(...size),material);m.name=name;m.position.set(...position);m.castShadow=true;m.receiveShadow=true;root.add(m);return m;}
function cyl(root,name,radius,height,position,material,segments=16){const m=new Mesh(new CylinderGeometry(radius,radius,height,segments),material);m.name=name;m.position.set(...position);root.add(m);return m;}
function rounded(root,name,w,h,d,x,y,z,material,r=.025){
 const s=new Shape(),a=-w/2,b=-h/2;
 s.moveTo(a+r,b);s.lineTo(a+w-r,b);s.quadraticCurveTo(a+w,b,a+w,b+r);s.lineTo(a+w,b+h-r);s.quadraticCurveTo(a+w,b+h,a+w-r,b+h);s.lineTo(a+r,b+h);s.quadraticCurveTo(a,b+h,a,b+h-r);s.lineTo(a,b+r);s.quadraticCurveTo(a,b,a+r,b);
 const g=new ExtrudeGeometry(s,{depth:d,bevelEnabled:true,bevelSegments:1,steps:1,bevelSize:.003,bevelThickness:.003,curveSegments:4});g.translate(0,0,-d/2);
 const m=new Mesh(g,material);m.name=name;m.position.set(x,y,z);m.castShadow=true;m.receiveShadow=true;root.add(m);return m;
}
export function interrogationRoom(){
 const g=new Group();g.name='Interrogation architecture';
 box(g,'Concrete floor',[7,.15,7],[0,-.1,-1],mat('#252d2d'));
 box(g,'Back wall',[7,3.2,.18],[0,1.5,-4],concrete);
 box(g,'Left wall',[.18,3.2,7],[-3.5,1.5,-1],concrete);
 box(g,'Right wall',[.18,3.2,7],[3.5,1.5,-1],concrete);
 box(g,'Ceiling',[7,.15,7],[0,3,-1],dark);
 for(let y=.35;y<3;y+=.42)box(g,'Concrete seam',[7,.012,.012],[0,y,-3.9],dark);
 box(g,'Observation frame',[2.7,1.2,.09],[0,1.65,-3.84],steel);
 box(g,'Observation glass',[2.53,1.04,.1],[0,1.65,-3.77],glass);
 box(g,'Lamp housing',[1.4,.08,.21],[0,2.55,-1],steel);
 box(g,'Fluorescent diffuser',[1.29,.025,.17],[0,2.50,-1],glow);
 for(const x of [-.6,.6])cyl(g,'Suspension wire',.007,.46,[x,2.79,-1],steel,6);
 box(g,'Wall skirting',[7,.12,.03],[0,.08,-3.89],dark);
 box(g,'Vent frame',[.8,.35,.04],[2.35,2.2,-3.85],steel);
 for(let i=0;i<9;i++)box(g,'Vent louvre',[.72,.012,.04],[2.35,2.06+i*.032,-3.81],dark);
 box(g,'Door frame',[1.05,2.35,.06],[-2.2,1.18,-3.81],steel);
 box(g,'Station door',[.9,2.22,.08],[-2.2,1.13,-3.75],dark);
 cyl(g,'Door handle',.025,.18,[-1.9,1,-3.66],steel).rotation.z=Math.PI/2;
 return g;
}
export function cabinRoom(){
 const g=new Group();g.name='Cabin fragment';const plank=mat('#5b4533');
 box(g,'Cabin floor',[7,.15,7],[0,-.1,-1],wood);
 for(let i=0;i<20;i++)box(g,'Cabin plank',[7,.12,.09],[0,i*.15+.08,-3.3],i%3===0?wood:plank);
 box(g,'Cabin side',[.16,3.2,7],[-3.5,1.5,-1],wood);
 box(g,'Window trim',[1.4,1.4,.12],[-1.9,1.7,-3.2],dark);
 box(g,'Snow outside window',[1.22,1.22,.03],[-1.9,1.7,-3.12],snow);
 box(g,'Window crossbar',[.035,1.25,.04],[-1.9,1.7,-3.08],dark);
 box(g,'Window crossbar',[1.25,.035,.04],[-1.9,1.7,-3.08],dark);
 for(let i=0;i<5;i++){
  const x=-.5+i*.24;const cup=cyl(g,'Cup '+(i+1),.047,.1,[x,.805,-1],mat('#bab7aa'),20);
  const rim=new Mesh(new TorusGeometry(.043,.004,6,20),steel);rim.rotation.x=Math.PI/2;rim.position.set(x,.857,-1);g.add(rim);
 }
 return g;
}
export function desk(){
 const g=new Group();g.name='Interview table';
 const top=rounded(g,'Tabletop',1.7,.94,.06,0,.74,0,steel);top.rotation.x=Math.PI/2;
 for(const x of [-.7,.7])for(const z of [-.34,.34])box(g,'Table leg',[.045,.7,.045],[x,.35,z],steel);
 box(g,'Cross brace',[1.4,.045,.045],[0,.2,.32],steel);
 rounded(g,'Case folder',.3,.015,.4,-.44,.79,-.09,mat('#70634b'));
 box(g,'Paper edge',[.27,.003,.36],[-.44,.8,-.09],paper);
 for(let i=0;i<6;i++)box(g,'Printed line',[.19,.001,.002],[-.45,.802,-.21+i*.027],mat('#625e50'));
 cyl(g,'Pencil',.004,.19,[.48,.79,-.05],mat('#857246'),8).rotation.z=Math.PI/2;
 return g;
}
export function chair(){
 const g=new Group();g.name='Officer chair';
 const seat=rounded(g,'Seat cushion',.51,.5,.075,0,.47,0,leather,.05);seat.rotation.x=Math.PI/2;
 rounded(g,'Chair back',.5,.48,.07,0,.78,-.2,leather,.05);
 for(const x of [-.21,.21])for(const z of [-.2,.2])cyl(g,'Chair leg',.016,.44,[x,.22,z],steel);
 return g;
}
export function photo(){const g=new Group();g.name='Offered photograph';rounded(g,'Photo paper',.25,.18,.003,0,0,0,paper,.006);box(g,'Photograph image',[.225,.148,.004],[0,.005,.003],mat('#4c5a59'));return g;}
export function radio(){
 const g=new Group();g.name='Cabin radio';rounded(g,'Radio cabinet',.4,.24,.15,0,.12,0,wood,.02);
 box(g,'Tuning display',[.17,.035,.012],[.075,.175,.083],mat('#d7c591'));
 for(let i=0;i<8;i++)box(g,'Speaker grille',[.008,.13,.01],[-.16+i*.018,.12,.084],dark);
 const knob=cyl(g,'Tuning knob',.033,.026,[.12,.075,.095],steel,24);knob.rotation.x=Math.PI/2;
 box(g,'Knob mark',[.004,.02,.006],[.12,.086,.112],paper);return g;
}
export function key(){const g=new Group();g.name='Cabin key';const r=new Mesh(new TorusGeometry(.025,.006,8,20),steel);g.add(r);box(g,'Key shaft',[.012,.09,.012],[0,-.06,0],steel);box(g,'Key tooth',[.025,.012,.012],[.008,-.097,0],steel);return g;}
export function memoryDoor(){
 const g=new Group();g.name='Cabin door fragment';
 box(g,'Door frame',[.83,2.1,.09],[0,1.05,0],wood);
 box(g,'Door slab',[.72,1.95,.11],[0,1.02,.05],mat('#463b2e'));
 for(const y of [.53,1.48])box(g,'Raised door panel',[.51,.64,.035],[0,y,.12],wood);
 const h=cyl(g,'Door latch',.028,.15,[-.24,.94,.18],steel);h.rotation.z=Math.PI/2;
 box(g,'Key plate',[.045,.11,.015],[-.26,.84,.12],steel);return g;
}
