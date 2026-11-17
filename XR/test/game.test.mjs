import test from 'node:test';
import assert from 'node:assert/strict';
import { createGame, transition, submitStatement, getView, detectAccusation, evaluateEnding } from '../src/game.js';
import { encodePcm16, monoSamples, createVoiceEngine } from '../src/voice.js';

function recall() {
  let state = transition(createGame(), { type: 'START' });
  state = transition(state, { type: 'TUNE_RADIO', value: 0.5 });
  state = transition(state, { type: 'OPEN_NIGHT_DOOR' });
  return transition(state, { type: 'CONTINUE' });
}
function verdict() {
  let state = recall();
  state = submitStatement(state, 'We drank by the fire. Nick and I argued. He left to go outside.');
  state = submitStatement(state, 'I called at the door. I left the door unlocked, then passed out on the sofa. Priya screamed in the morning.');
  state = transition(state, { type: 'CONTINUE' });
  state = transition(state, { type: 'OBSERVE', id: 'glass' });
  state = transition(state, { type: 'TRY_MORNING_DOOR' });
  state = transition(state, { type: 'TAKE_KEY' });
  state = transition(state, { type: 'UNLOCK_DOOR' });
  return transition(state, { type: 'CONTINUE' });
}

test('the whole case can finish without audio or a microphone', () => {
  let state = verdict();
  assert.equal(state.phase, 'verdict');
  state = submitStatement(state, 'I think it was Aaron. The door was locked in the morning and the key was inside.');
  assert.equal(getView(state).continueAvailable, true);
  state = transition(state, { type: 'CONTINUE' });
  assert.equal(state.phase, 'ending');
  assert.equal(state.ending.id, 'E_AARON');
  assert.equal(state.statements.every(turn => turn.input === 'text'), true);
  assert.equal(state.ending.quotes.at(-1), 'I think it was Aaron. The door was locked in the morning and the key was inside.');
});

test('memory tasks block early skipping and never require prior microphone access', () => {
  let state = transition(createGame(), { type: 'START' });
  state = transition(state, { type: 'OPEN_NIGHT_DOOR' });
  assert.equal(state.nightDoorOpened, false);
  state = transition(state, { type: 'CONTINUE' });
  assert.equal(state.phase, 'night');
  state = transition(state, { type: 'TUNE_RADIO', value: 1 });
  assert.equal(state.radioTuned, false);
  state = transition(state, { type: 'TUNE_RADIO', value: 0.5 });
  state = transition(state, { type: 'OPEN_NIGHT_DOOR' });
  assert.equal(getView(state).continueAvailable, true);
});

test('the morning latch cannot open without the key', () => {
  let state = recall();
  state = submitStatement(state, 'I remember the fire.');
  state = submitStatement(state, 'I do not remember anything after the door.');
  state = transition(state, { type: 'CONTINUE' });
  assert.equal(state.phase, 'morning');
  state = transition(state, { type: 'UNLOCK_DOOR' });
  assert.equal(state.morningDoorUnlocked, false);
  state = transition(state, { type: 'TAKE_KEY' });
  state = transition(state, { type: 'UNLOCK_DOOR' });
  assert.equal(state.morningDoorUnlocked, true);
});

test('uncertainty and another persons quoted alibi do not become an accusation', () => {
  assert.equal(detectAccusation('Maybe Aaron or Ivy. I do not know.'), '');
  assert.equal(detectAccusation('Ivy said she was upstairs with Aaron.'), '');
  assert.equal(detectAccusation('Aaron and Ivy'), '');
  assert.equal(detectAccusation('I do not think it was Aaron.'), '');
  assert.equal(detectAccusation('It was Aaron, because Ivy gave him an alibi.'), 'aaron');
});

test('mutually incompatible accusations cannot produce a confident positive resolution', () => {
  let state = verdict();
  state = submitStatement(state, 'I think it was Aaron. The locked door and key were inside.');
  state = submitStatement(state, 'I accuse Ivy. She offered the alibi.');
  assert.equal(evaluateEnding(state).id, 'E_DAVID');
  assert.match(evaluateEnding(state).reason, /more than one/);
});

test('all four authored endings remain reachable from testimony, without an accusation menu', () => {
  for (const [statement, expected] of [
    ['I think it was Aaron. The locked door and key were inside.', 'E_AARON'],
    ['I accuse Ivy. Her alibi did not explain the locked door and key.', 'E_IVY'],
    ['I accuse Priya. The locked door and key make me think she knew.', 'E_PRIYA'],
    ['I do not know who did it.', 'E_DAVID'],
  ]) {
    const state = submitStatement(verdict(), statement);
    assert.equal(evaluateEnding(state).id, expected);
  }
});

test('contradicting a precise prior claim is recorded, but acknowledging a correction is a revision', () => {
  let state = submitStatement(recall(), 'I left the door unlocked.');
  const conflict = submitStatement(state, 'I locked the door.');
  assert.equal(conflict.contradictions.length, 1);
  const revision = submitStatement(state, 'I was wrong. I locked the door.');
  assert.equal(revision.contradictions.length, 0);
  assert.equal(revision.revisions.length, 1);
});

test('silent and oversized statements do not consume turns, and recall is capped', () => {
  let state = recall();
  state = submitStatement(state, '   ');
  state = submitStatement(state, 'x'.repeat(2001));
  assert.equal(state.statements.length, 0);
  for (let i = 0; i < 5; i++) state = submitStatement(state, 'I cannot remember.');
  assert.equal(state.statements.length, 4);
  assert.equal(getView(state).canSubmit, false);
  assert.equal(getView(state).continueAvailable, true);
});

test('live mode never silently commits a scripted answer when the backend fails', () => {
  let state = transition(recall(), { type: 'SET_MODE', mode: 'live' });
  state = submitStatement(state, 'I left it unlocked.');
  assert.equal(state.statements.length, 0);
  assert.match(state.notice, /did not arrive/);
  state = submitStatement(state, 'I left it unlocked.', { reply: 'How do you remember that?', input: 'text', affect: { tension: 0.99 } });
  assert.equal(state.statements.length, 1);
  assert.equal(state.statements[0].source, 'live');
  assert.equal(Object.hasOwn(state.statements[0], 'affect'), false);
});

test('vocal tension cannot change an ending and reset clears the complete account', () => {
  let state = submitStatement(verdict(), 'It was Aaron. The locked door and key were inside.', { input: 'voice', affect: { tension: 1 } });
  const base = evaluateEnding(state).id;
  state.statements[0].affect = { tension: 0.01 };
  assert.equal(evaluateEnding(state).id, base);
  const reset = transition(state, { type: 'RESET' });
  assert.deepEqual(reset, createGame());
});

test('pause blocks memory actions and submission without losing the case', () => {
  const original = recall();
  let paused = transition(original, { type: 'PAUSE' });
  paused = submitStatement(paused, 'I remember the argument.');
  assert.equal(paused.statements.length, 0);
  paused = transition(paused, { type: 'CONTINUE' });
  assert.equal(paused.phase, 'recall');
  assert.equal(transition(paused, { type: 'RESUME' }).paused, false);
  assert.equal(original.paused, false);
});

test('PCM conversion obeys little-endian mono16k transport and clips samples safely', () => {
  const pcm = encodePcm16(new Float32Array([-2, -1, 0, 1, 2]), 16000);
  const view = new DataView(pcm);
  assert.deepEqual(Array.from({ length: 5 }, (_, i) => view.getInt16(i * 2, true)), [-32768, -32768, 0, 32767, 32767]);
  assert.equal(encodePcm16(new Float32Array(48000), 48000).byteLength, 32000);
  assert.deepEqual([...monoSamples({ length: 2, numberOfChannels: 2, getChannelData: n => new Float32Array(n ? [1, -1] : [-1, 1]) })], [0, 0]);
});

test('text requests send only purpose and statement through the same-origin gateway', async () => {
  let received;
  const voice = createVoiceEngine({ fetchImpl: async (path, options) => { received = { path, options }; return Response.json({ ok: true, reply_text: 'What happened next?' }); } });
  const reply = await voice.submitText('I left the door unlocked.', 'p2');
  assert.equal(received.path, '/api/statement');
  assert.deepEqual([...received.options.body.keys()], ['purpose', 'statement']);
  assert.equal(received.options.body.get('statement'), 'I left the door unlocked.');
  assert.equal(received.options.credentials, 'same-origin');
  assert.equal(reply.reply_text, 'What happened next?');
  await assert.rejects(() => voice.submitText('hello', 'night'), /not accepting/);
});

test('uncertainty always reaches an ending within the phase turn caps, in either mode', () => {
  for (const mode of ['scripted', 'live']) {
    let state = transition(recall(), { type: 'SET_MODE', mode });
    const meta = mode === 'live' ? { reply: 'What else can you remember?' } : {};
    assert.equal(transition(state, { type: 'CONTINUE' }).phase, 'recall');
    for (let i = 0; i < 4; i++) state = submitStatement(state, 'I do not remember. I cannot say for sure.', meta);
    assert.equal(getView(state).canSubmit, false);
    assert.equal(getView(state).continueAvailable, true);
    assert.equal(getView(state).turnsRemaining, 0);
    assert.match(getView(state).objective, /complete/);
    state = transition(state, { type: 'CONTINUE' });
    state = transition(state, { type: 'TRY_MORNING_DOOR' });
    state = transition(state, { type: 'TAKE_KEY' });
    state = transition(state, { type: 'UNLOCK_DOOR' });
    state = transition(state, { type: 'CONTINUE' });
    for (let i = 0; i < 4; i++) state = submitStatement(state, 'Maybe Aaron or Ivy. I do not know.', meta);
    assert.equal(getView(state).canSubmit, false);
    assert.equal(getView(state).continueAvailable, true);
    assert.match(getView(state).objective, /finish your statement now/);
    state = transition(state, { type: 'CONTINUE' });
    assert.equal(state.phase, 'ending');
    assert.equal(state.ending.id, 'E_DAVID');
  }
});

test('two uncertain verdict statements permit finishing without guessing a suspect', () => {
  let state = submitStatement(verdict(), 'I do not know who did it.');
  assert.equal(getView(state).continueAvailable, false);
  state = submitStatement(state, 'I am not prepared to accuse someone.');
  assert.equal(getView(state).continueAvailable, true);
  state = transition(state, { type: 'CONTINUE' });
  assert.equal(state.phase, 'ending');
  assert.equal(state.ending.id, 'E_DAVID');
});

test('failed AI replies can recover through labelled scripted mode without resetting memories', () => {
  let state = transition(recall(), { type: 'SET_MODE', mode: 'live' });
  state = submitStatement(state, 'I cannot remember.', { reply: '' });
  assert.equal(state.statements.length, 0);
  assert.equal(state.nightDoorOpened, true);
  state = transition(state, { type: 'SET_MODE', mode: 'scripted' });
  assert.equal(getView(state).modeLabel, 'Scripted experience');
  state = submitStatement(state, 'I cannot remember.');
  state = submitStatement(state, 'I do not know what happened.');
  state = transition(state, { type: 'CONTINUE' });
  state = transition(state, { type: 'TRY_MORNING_DOOR' });
  state = transition(state, { type: 'TAKE_KEY' });
  state = transition(state, { type: 'UNLOCK_DOOR' });
  state = transition(state, { type: 'CONTINUE' });
  state = submitStatement(state, 'I cannot name anyone.');
  state = submitStatement(state, 'I will not guess.');
  state = transition(state, { type: 'CONTINUE' });
  assert.equal(state.phase, 'ending');
  assert.equal(state.statements.every(turn => turn.source === 'scripted'), true);
});

test('reset from interrupted or completed phases clears all gates and allows a fresh full run', () => {
  const states = [createGame(), transition(createGame(), { type: 'START' }), recall(), verdict()];
  let done = submitStatement(verdict(), 'I think it was Aaron. The door was locked and the key was inside.');
  done = transition(done, { type: 'CONTINUE' });
  states.push(done);
  for (const state of states) {
    const paused = transition(state, { type: 'PAUSE' });
    const fresh = transition(paused, { type: 'RESET' });
    assert.deepEqual(fresh, createGame());
    assert.equal(transition(fresh, { type: 'START' }).phase, 'night');
  }
});
