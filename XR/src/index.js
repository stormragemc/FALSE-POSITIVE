import './style.css';
import { setupEmulation } from './emulation.js';
const status=document.querySelector('#load-status');
try {
 await setupEmulation();
 const [{World}, {default:options}, {Experience}]=await Promise.all([import('@iwsdk/core'),import('virtual:iwsdk-project'),import('./experience.js')]);
 const world=await World.create(document.querySelector('#scene-container'),options);
 world.registerSystem(Experience);
 document.documentElement.dataset.ready='true';
 status.textContent='';document.querySelector('#loading').hidden=true;
} catch(error){console.error(error);status.textContent=`The scene could not load: ${error.message}. Reload to retry.`;document.querySelector('#retry').hidden=false;}
