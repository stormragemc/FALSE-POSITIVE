import test from 'node:test';
import assert from 'node:assert/strict';
import { registerHooks } from 'node:module';
import { createGame, transition, getView } from '../src/game.js';
import { createCinematicPlayer, SEQUENCES } from '../src/cinematics.js';

// Execute the real Experience coordinator and real game/player. Only browser/renderer
// constructors are replaced; there is no copied phase or cinematic sequencing here.
const experienceUrl = new URL('../src/experience.js', import.meta.url).href;
const modules = {
  '@iwsdk/core': 'export const Pressed={},Grabbed={},VisibilityState={NonImmersive:0,Visible:1,VisibleBlurred:2};export function createSystem(){return class{}}',
  './officer.js': 'export function setupOfficer(){}',
  './voice.js': 'export function createVoiceEngine(){}',
  './cinematic-presentation.js': 'export class CinematicPresentation{}',
};
const hooks = registerHooks({ resolve(specifier, context, next) {
  if (context.parentURL === experienceUrl && Object.hasOwn(modules, specifier)) {
    return { url: `data:text/javascript,${encodeURIComponent(modules[specifier])}`, shortCircuit: true };
  }
  return next(specifier, context);
} });
const { Experience } = await import(experienceUrl);
hooks.deregister();

function fixture({ AudioClass = null, hold = false } = {}) {
  const elements = new Map();
  globalThis.document = { documentElement: { dataset: {} }, querySelector: id => {
    if (!elements.has(id)) elements.set(id, { textContent: '', value: '', hidden: false, disabled: false });
    return elements.get(id);
  } };
  const e = new Experience();
  Object.assign(e, { cineTicket: 0, inCinematic: false, seenAccusations: new Set(), generation: 0,
    state: createGame(), draft: '', writing: false, busy: false, recording: false, talking: false,
    error: '', clock: 0, message: '', liveReady: true,
  });
  const seen = [], finished = [], speech = [], pendingTimers = [];
  const scheduler = { hold };
  e.presentation = { rest() {}, setDoorOpen() {}, clear() {} };
  e.render = () => {};
  e.renderCinematic = () => {};
  e.voice = { stopSpeaking() {}, resetSession: async () => {}, speak: async (...args) => { speech.push(args); } };
  e.cinematic = createCinematicPlayer({ AudioClass, maxBeatSeconds: 0.05,
    onBeat: beat => { e.currentBeat = beat; seen.push(beat.cutscene); },
    onFinish: result => finished.push(result),
    onError: () => { e.cinematicAudioWarning = true; },
    setTimer: fn => { pendingTimers.push(fn); if (!scheduler.hold) queueMicrotask(fn); return pendingTimers.length; },
    clearTimer: () => {},
  });
  return { e, seen, finished, speech, scheduler, pendingTimers };
}

async function reachRecall(f) {
  const { e } = f;
  await e.dispatch({ type: 'START' });
  assert.equal(e.state.phase, 'night');
  await e.dispatch({ type: 'TUNE_RADIO', value: 0.5 });
  await e.dispatch({ type: 'OPEN_NIGHT_DOOR' });
  await e.dispatch({ type: 'CONTINUE' });
  assert.equal(e.state.phase, 'recall');
}

async function giveStatement(e, text) {
  e.draft = text;
  await e.submit();
  assert.equal(e.busy, false);
}

async function reachVerdict(f) {
  await reachRecall(f);
  const { e } = f;
  await giveStatement(e, 'We drank by the fire. Nick and I argued. He left to go outside.');
  await giveStatement(e, 'I called at the door. I left the door unlocked, then passed out on the sofa. Priya screamed in the morning.');
  assert.equal(getView(e.state).continueAvailable, true);
  await e.dispatch({ type: 'CONTINUE' });
  assert.equal(e.state.phase, 'morning');
  await e.dispatch({ type: 'TRY_MORNING_DOOR' });
  await e.dispatch({ type: 'TAKE_KEY' });
  await e.dispatch({ type: 'UNLOCK_DOOR' });
  await e.dispatch({ type: 'CONTINUE' });
  assert.equal(e.state.phase, 'verdict');
}

for (const [name, statement] of [
  ['Aaron', 'I think it was Aaron. The door was locked in the morning and the key was inside.'],
  ['Ivy', 'I accuse Ivy. Her alibi did not explain the locked door and key.'],
  ['Priya', 'I accuse Priya. The locked door and key make me think she knew.'],
  ['David', 'I do not know who did it.'],
]) {
  test(`the actual Experience reaches the ${name} ending after the canonical cinematic sequence`, async () => {
    const f = fixture();
    await reachVerdict(f);
    await giveStatement(f.e, statement);
    if (!getView(f.e.state).continueAvailable) await giveStatement(f.e, 'I will not invent an answer to fill the gaps.');
    assert.equal(getView(f.e.state).continueAvailable, true);
    await f.e.dispatch({ type: 'CONTINUE' });
    assert.equal(f.e.state.phase, 'ending');
    assert.equal(f.e.state.ending.id, `E_${name.toUpperCase()}`);
    assert.equal(f.e.inCinematic, false);
    const ordered = f.finished.flatMap(result => result.sequence);
    const expected = [
      ...SEQUENCES.start, ...SEQUENCES.wakeNight, ...SEQUENCES.radio, ...SEQUENCES.nightEnd,
      ...SEQUENCES.morningStart, ...SEQUENCES.morningEnd, ...SEQUENCES.verdictMemory,
      ...(SEQUENCES[`accusation${name}`] || []), ...SEQUENCES[`ending${name}`], ...SEQUENCES.truthReveal,
    ];
    assert.deepEqual(ordered, expected);
    assert.ok(f.finished.every(result => result.status === 'completed'));
    if (name !== 'David') assert.equal(ordered.filter(id => id === `Flashback${name}`).length, 1);
  });
}

test('repeated Skip presses advance the triggering game action exactly once', async () => {
  const f = fixture({ hold: true });
  const running = f.e.dispatch({ type: 'START' });
  assert.equal(f.e.state.phase, 'menu');
  assert.equal(f.e.inCinematic, true);
  f.e.primary();
  f.e.primary();
  await running;
  assert.equal(f.e.state.phase, 'night');
  assert.equal(f.e.inCinematic, false);
  assert.equal(f.finished.length, 1);
  assert.equal(f.finished[0].status, 'skipped');
  assert.equal(f.e.state.nightDoorOpened, false);
});

test('a hung cinematic audio start still releases the real START and CONTINUE gates', async () => {
  class NeverStarts { play() { return new Promise(() => {}); } pause() {} }
  const f = fixture({ AudioClass: NeverStarts });
  await reachRecall(f);
  assert.equal(f.e.inCinematic, false);
  assert.equal(getView(f.e.state).canSubmit, true);
  await giveStatement(f.e, 'I do not remember.');
  await giveStatement(f.e, 'I cannot be sure.');
  await f.e.dispatch({ type: 'CONTINUE' });
  assert.equal(f.e.state.phase, 'morning');
  assert.equal(f.e.inCinematic, false);
});

test('reset cancels an active sequence and queued callbacks cannot advance the old phase', async () => {
  const f = fixture({ hold: true });
  const running = f.e.dispatch({ type: 'START' });
  assert.equal(f.e.inCinematic, true);
  const staleTimers = [...f.pendingTimers];
  f.e.reset();
  await running;
  staleTimers.forEach(fn => fn());
  await Promise.resolve();
  assert.deepEqual(f.e.state, createGame());
  assert.equal(f.e.inCinematic, false);
  assert.equal(f.finished.length, 0);
  f.scheduler.hold = false;
  await f.e.dispatch({ type: 'START' });
  assert.equal(f.e.state.phase, 'night');
});

test('paused cinematic can be skipped and does not strand its pending state transition', async () => {
  const f = fixture({ hold: true });
  const running = f.e.dispatch({ type: 'START' });
  f.e.pause();
  assert.equal(f.e.cinematic.paused, true);
  assert.equal(f.e.state.paused, false);
  f.e.primary();
  await running;
  assert.equal(f.e.state.phase, 'night');
  assert.equal(f.e.state.paused, false);
  assert.equal(f.e.inCinematic, false);
});

test('reset during the accusation flashback never plays a stale reply over the new case', async () => {
  const f = fixture();
  await reachVerdict(f);
  f.scheduler.hold = true;
  f.e.draft = 'I think it was Aaron. The locked door and key were inside.';
  const running = f.e.submit();
  for(let i=0;i<6;i++)await Promise.resolve();
  assert.equal(f.e.inCinematic, true);
  const spoken = f.speech.length;
  f.e.reset();
  await running;
  assert.equal(f.e.state.phase, 'menu');
  assert.equal(f.e.state.statements.length, 0);
  assert.equal(f.speech.length, spoken);
});

test('a terminal live response arriving after reset cannot change the new case mode or draft', async () => {
  const f = fixture();
  await reachRecall(f);
  await f.e.dispatch({ type: 'SET_MODE', mode: 'live' });
  let release;
  f.e.voice.submitText = () => new Promise(resolve => { release = resolve; });
  f.e.draft = 'The old statement.';
  const running = f.e.submit();
  assert.equal(f.e.busy, true);
  f.e.reset();
  f.e.draft = 'The new draft.';
  release({ session_ended: true, transcript: '', reply_text: 'This session has ended.' });
  await running;
  assert.equal(f.e.state.phase, 'menu');
  assert.equal(f.e.state.mode, 'live');
  assert.equal(f.e.draft, 'The new draft.');
  assert.equal(f.e.error, '');
});

test('reply completion gates the next memory; Skip reply releases audio without advancing', async () => {
  const f = fixture();
  await reachRecall(f);
  let finish;
  f.e.voice.speak = () => new Promise(resolve => { finish=resolve; });
  f.e.voice.stopSpeaking = () => finish?.({status:'cancelled'});
  f.e.draft = 'I do not remember the night.';
  const running = f.e.submit();
  for (let i=0;i<6;i++) await Promise.resolve();
  assert.equal(f.e.state.statements.length,1);
  assert.equal(f.e.busy,false);
  assert.equal(f.e.replyPending,true);
  await f.e.dispatch({type:'CONTINUE'});
  assert.equal(f.e.state.phase,'recall');
  assert.equal(f.e.inCinematic,false);
  f.e.primary();
  await running;
  assert.equal(f.e.replyPending,false);
  assert.equal(f.e.state.phase,'recall');
});

test('duplicate scene input during a pending cinematic cannot queue another phase transition', async () => {
  const f = fixture();
  await reachRecall(f);
  await giveStatement(f.e, 'I do not remember.');
  await giveStatement(f.e, 'I cannot be certain.');
  f.scheduler.hold = true;
  const running = f.e.dispatch({ type: 'CONTINUE' });
  await f.e.dispatch({ type: 'CONTINUE' });
  f.e.primary();
  await running;
  assert.equal(f.e.state.phase, 'morning');
  assert.equal(f.e.state.morningDoorUnlocked, false);
  assert.equal(f.finished.filter(result => result.sequence[0] === 'FuzzyToMorning').length, 1);
});

test('a terminal live text reply switches to labelled scripted progression without losing the submitted statement', async () => {
  const f = fixture();
  await reachRecall(f);
  await f.e.dispatch({ type: 'SET_MODE', mode: 'live' });
  f.e.voice.submitText = async () => ({ session_ended: true, reply_text: 'This live session is closed.', audio_b64: 'old-closing-audio' });
  await giveStatement(f.e, 'I left the door unlocked and went to sleep.');
  assert.equal(f.e.state.mode, 'scripted');
  assert.equal(f.e.state.statements.length, 1);
  assert.equal(f.e.state.statements[0].text, 'I left the door unlocked and went to sleep.');
  assert.equal(f.e.state.statements[0].source, 'scripted');
  assert.equal(f.speech.at(-1)[1]?.audio_b64, undefined);
  await giveStatement(f.e, 'I do not remember anything else.');
  assert.equal(getView(f.e.state).continueAvailable, true);
  await f.e.dispatch({ type: 'CONTINUE' });
  assert.equal(f.e.state.phase, 'morning');
});

test('a terminal live voice reply with no transcript opens hand-text recovery and can finish the case', async () => {
  const f = fixture();
  await reachVerdict(f);
  await f.e.dispatch({ type: 'SET_MODE', mode: 'live' });
  f.e.voice.submitAudio = async () => ({ session_ended: true, transcript: '', reply_text: 'This live session is closed.' });
  await f.e.submit({ blob: new Blob(['test']) });
  assert.equal(f.e.state.mode, 'scripted');
  assert.equal(f.e.writing, true);
  assert.equal(f.e.busy, false);
  assert.equal(getView(f.e.state).canSubmit, true);
  assert.match(f.e.error, /Write your remaining statement/);
  await giveStatement(f.e, 'I cannot accuse anyone.');
  await giveStatement(f.e, 'I will not guess.');
  await f.e.dispatch({ type: 'CONTINUE' });
  assert.equal(f.e.state.phase, 'ending');
  assert.equal(f.e.state.ending.id, 'E_DAVID');
});

test('starting a cinematic from an open keyboard exposes Skip in desktop and spatial controls', () => {
  const f = fixture();
  const spatial = new Map();
  f.e.ui = { getElementById: id => {
    if (!spatial.has(id)) spatial.set(id, { properties: {}, setProperties(values) { Object.assign(this.properties, values); } });
    return spatial.get(id);
  } };
  f.e.objects = { controls: { visible: false }, keyboard: { visible: true } };
  f.e.inCinematic = true;
  f.e.currentBeat = { cutscene: 'FlashbackAaron', text: 'A figure crosses the landing.', speaker: '' };
  document.documentElement.dataset.immersive = 'true';
  document.querySelector('#primary').hidden = true;
  f.e.ui.getElementById('act-primary').setProperties({ display: 'none' });
  Experience.prototype.renderCinematic.call(f.e);
  assert.equal(document.querySelector('#primary').hidden, false);
  assert.equal(document.querySelector('#primary').textContent, 'Skip scene');
  assert.equal(f.e.ui.getElementById('act-primary').properties.display, 'flex');
  assert.equal(f.e.objects.controls.visible, true);
  assert.equal(f.e.objects.keyboard.visible, false);
});
