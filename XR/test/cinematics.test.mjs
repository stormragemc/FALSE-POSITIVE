import test from 'node:test';
import assert from 'node:assert/strict';
import { CINEMATICS, SEQUENCES, CINEMATIC_SOURCE_FILES, createCinematicPlayer } from '../src/cinematics.js';

const immediate = { setTimer: fn => { queueMicrotask(fn); return 1; }, clearTimer: () => {} };

test('all 24 canonical variants and four connective recipes have actual authored beats', () => {
  assert.equal(Object.keys(CINEMATICS).length, 28);
  const canonical = new Set(Object.values(CINEMATICS).map(x => x.canonId).filter(x => x.startsWith('CS-')));
  assert.equal(canonical.size, 24);
  assert.equal(CINEMATIC_SOURCE_FILES.length, 51);
  for (const scene of Object.values(CINEMATICS)) {
    assert.ok(scene.beats.length);
    for (const beat of scene.beats) {
      assert.ok(Number.isFinite(beat.duration) && beat.duration > 0);
      assert.ok(!beat.audio || /^\/media\/(vo|sfx)\/[^/]+\.(wav|mp3)$/.test(beat.audio));
    }
  }
});

test('story hooks cover every recipe and preserve the dialogue between the P3 memory pair', () => {
  assert.deepEqual(new Set(Object.values(SEQUENCES).flat()), new Set(Object.keys(CINEMATICS)));
  assert.deepEqual(SEQUENCES.verdictMemory, ['P3Photograph','GoodYears','P3AfterGoodYears','WhenItWentWrong','P3WhoDavid']);
  assert.equal(CINEMATICS.TheCarry.beats[3].text, 'Yes. All night.');
  assert.equal(CINEMATICS.TheCarry.beats[4].speaker, 'AARON');
});

test('missing audio never traps progression; every authored hook completes', async () => {
  for (const hook of Object.keys(SEQUENCES)) {
    const seen = [];
    const player = createCinematicPlayer({ ...immediate, AudioClass: null, onBeat: x => seen.push(x.cutscene) });
    assert.equal((await player.play(hook)).status, 'completed');
    assert.deepEqual([...new Set(seen)], SEQUENCES[hook]);
  }
});

test('a never-settling audio playback promise is bounded by the watchdog', async () => {
  const delays = [];
  class BrokenAudio { play() { return new Promise(() => {}); } pause() {} }
  const player = createCinematicPlayer({ AudioClass: BrokenAudio, maxBeatSeconds: 3,
    setTimer: (fn, ms) => { delays.push(ms); queueMicrotask(fn); return 1; }, clearTimer: () => {} });
  assert.equal((await player.play('start')).status, 'completed');
  assert.ok(delays.every(ms => ms > 0 && ms <= 3000));
});

test('skip advances the requested hook once and releases the current audio', async () => {
  let finished = 0, paused = 0;
  class HeldAudio { play() { return Promise.resolve(); } pause() { paused += 1; } }
  const player = createCinematicPlayer({ ...immediate, AudioClass: HeldAudio, onFinish: () => { finished += 1; } });
  const running = player.play('start');
  assert.equal(player.skip(), true);
  assert.equal((await running).status, 'skipped');
  assert.equal(finished, 1);
  assert.ok(paused > 0);
  assert.equal(player.playing, false);
});

test('reset cancellation and a replacement sequence cannot finish an old cinematic', async () => {
  const finished = [];
  const player = createCinematicPlayer({ ...immediate, AudioClass: null, onFinish: x => finished.push(x.sequence) });
  const old = player.play('start');
  const next = player.play('radio');
  assert.equal((await old).status, 'cancelled');
  assert.equal((await next).status, 'completed');
  assert.deepEqual(finished, [SEQUENCES.radio]);
  const third = player.play('morningEnd');
  player.cancel();
  assert.equal((await third).status, 'cancelled');
  assert.equal(finished.length, 1);
});

test('pause freezes the playback deadline and resumes the remaining time', async () => {
  let clock = 0, nextId = 0, finishes = 0;
  const timers = new Map();
  const player = createCinematicPlayer({ AudioClass: null, now: () => clock,
    setTimer: (fn, ms) => { timers.set(++nextId, {fn, ms}); return nextId; },
    clearTimer: id => timers.delete(id), onFinish: () => { finishes += 1; } });
  const running = player.play('FlashbackIvy');
  assert.equal([...timers.values()][0].ms, 5000);
  clock = 1000;
  assert.equal(player.pause(), true);
  assert.equal(player.paused, true);
  assert.equal(timers.size, 0);
  clock = 11000;
  assert.equal(finishes, 0);
  assert.equal(player.resume(), true);
  assert.equal([...timers.values()][0].ms, 4000);
  [...timers.values()][0].fn();
  assert.equal((await running).status, 'completed');
  assert.equal(finishes, 1);
});

test('cancel while paused immediately releases the waiting sequence without advancing', async () => {
  const player = createCinematicPlayer({AudioClass:null,setTimer:()=>1,clearTimer:()=>{}});
  const running = player.play('morningEnd');
  player.pause();
  player.cancel();
  assert.equal((await running).status, 'cancelled');
  assert.equal(player.playing,false);
  assert.equal(player.paused,false);
});

test('a queued audio or timer completion cannot advance behind a paused screen', async () => {
  const scheduled = [];
  let finished=0;
  const player=createCinematicPlayer({AudioClass:null,setTimer:fn=>{scheduled.push(fn);return scheduled.length;},clearTimer:()=>{},onFinish:()=>finished++});
  const running=player.play('FlashbackIvy');
  player.pause();
  scheduled[0]();
  await Promise.resolve();
  assert.equal(finished,0);
  assert.equal(player.playing,true);
  player.resume();
  scheduled.at(-1)();
  assert.equal((await running).status,'completed');
  assert.equal(finished,1);
});
