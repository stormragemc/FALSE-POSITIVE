import test from 'node:test';
import assert from 'node:assert/strict';
import {createVoiceEngine} from '../src/voice.js';

const line='So. What\'s the last thing you remember?';
function fixture(){
 let audio;const timers=new Map();let id=0;
 globalThis.window={speechSynthesis:{cancel(){}}};
 globalThis.Audio=class {constructor(){audio=this;this.duration=12;this.currentTime=0;}play(){return Promise.resolve();}pause(){this.paused=true;}};
 const engine=createVoiceEngine({playbackSetTimer:(fn,ms)=>{timers.set(++id,{fn,ms});return id;},playbackClearTimer:id=>timers.delete(id)});
 return {engine,timers,get audio(){return audio;}};
}
test('Spassky reply resolves after actual end, not after play starts',async()=>{
 const f=fixture();let settled=false;const reply=f.engine.speak(line).then(v=>{settled=true;return v;});
 await Promise.resolve();assert.equal(settled,false);
 f.audio.onplaying();assert.equal([...f.timers.values()][0].ms,15000);
 f.audio.onended();assert.equal((await reply).status,'completed');assert.equal(f.timers.size,0);
});
test('Skip reply settles the current voice promise and stops its audio',async()=>{
 const f=fixture(),reply=f.engine.speak(line);f.engine.stopSpeaking();
 assert.equal((await reply).status,'cancelled');assert.equal(f.audio.paused,true);assert.equal(f.timers.size,0);
});
test('a stalled reply has a finite watchdog and cannot lock the game',async()=>{
 const f=fixture(),reply=f.engine.speak(line);const timer=[...f.timers.values()][0];
 assert.ok(timer.ms>=18000&&timer.ms<=180000);timer.fn();
 assert.equal((await reply).status,'cancelled');assert.equal(f.audio.paused,true);
});
