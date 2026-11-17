/** The short XR case. No browser, renderer, microphone, or vendor dependency. */
export const PHASES = Object.freeze(['menu', 'night', 'recall', 'morning', 'verdict', 'ending']);
export const RADIO_TARGET = 0.5;
export const RADIO_TOLERANCE = 0.06;
export const MAX_STATEMENT_CHARS = 2000;
export const MAX_PHASE_TURNS = 4;

export const ENDINGS = Object.freeze({
  E_DAVID: { title: 'David is taken.', line: "You were the only one who couldn't tell me where you were.", recording: 'SPASSKY-053' },
  E_AARON: { title: 'Aaron is taken.', line: 'He locked it. You unlocked it. Only one of those was a decision.', recording: 'SPASSKY-054' },
  E_IVY: { title: 'Ivy is taken.', line: "She agreed with you. That's not the same as it being true.", recording: 'SPASSKY-055' },
  E_PRIYA: { title: 'Priya is taken.', line: "She's the one who called us. Sit with that.", recording: 'SPASSKY-056' },
});

const INTRO = "I'm Officer Spassky. Nick is dead, and right now you're one of the suspects. I've already spoken to the others. Take your time and tell me everything you remember from last night.";
const RECALL_OPENING = "So. What's the last thing you remember?";
const VERDICT_OPENING = 'Who, David?';
// These are fixed authored lines. Selecting scripted mode never claims an adaptive AI reply.
const SCRIPTED_RECALL = [
  'Tell me about the argument with Nick. What was it really about?',
  'What happened to Nick?',
  'Was the door locked when you opened it?',
  'Walk me through the morning. Start with Priya\'s scream.',
];
const MARKS = [
  ['m_fire', /\b(drink|drinking|drank|beer|fire|fireplace|cups?)\b/i],
  ['m_argument', /\b(argue[ds]?|arguing|argument|fight|told him|said to him)\b/i],
  ['m_nick_left', /\b(left|went out|outside|door closed|gone)\b/i],
  ['m_door', /\b(door|opened|called|shouted|yelled|his name)\b/i],
  ['m_lock', /\b(lock|locked|unlocked|bolt|key)\b/i],
  ['m_sleep', /\b(sofa|slept|sleep|passed out|blacked out|blackout|don.t remember|do not remember)\b/i],
  ['m_morning', /\b(scream|morning|window|carried|body|aaron)\b/i],
];
const OBSERVATIONS = {
  clock: { phase: 'night', text: '00:52. You notice the time on the mantel clock.' },
  cups: { phase: 'night', text: 'Five cups. All five of you were here.' },
  coat: { phase: 'night', text: "You are wearing Nick's heavy coat. He has your thin jacket." },
  glass: { phase: 'morning', text: 'The broken glass is on the inside of the cabin.' },
  grille: { phase: 'morning', text: 'The grille is intact. Nobody could climb through that opening.' },
  body: { phase: 'morning', text: 'Nick is outside, still wearing your thin jacket.' },
  photo: { phase: 'verdict', text: 'Old friends. An anniversary. Five glasses. Nick and Ivy look at each other.' },
};
const CLUES = [
  ['door_unlocked', s => s.nightDoorOpened, /\b(unlocked|left (?:it|the door) open|didn.t lock|did not lock)\b/i],
  ['door_locked', s => s.morningDoorTried, /\b(locked|lock|bolt)\b/i],
  ['key_inside', s => s.keyTaken, /\bkey\b/i],
  ['glass_inside', s => s.observations.includes('glass'), /\b(glass|shards)\b/i],
  ['grille_intact', s => s.observations.includes('grille'), /\b(grille|bars|grating)\b/i],
  ['coat_swap', s => s.observations.includes('coat'), /\b(coat|jacket|parka)\b/i],
  ['ivy_alibi', s => s.morningDoorUnlocked, /\b(alibi|all night|ivy said|ivy claimed|upstairs)\b/i],
];

export function createGame({ mode = 'scripted' } = {}) {
  if (!['scripted', 'live'].includes(mode)) throw new RangeError('Mode must be scripted or live.');
  return {
    version: 1, mode, phase: 'menu', paused: false,
    radio: 0.2, radioTuned: false, nightDoorOpened: false,
    morningDoorTried: false, keyTaken: false, morningDoorUnlocked: false,
    observations: [], statements: [], conversation: [], marks: [],
    assertions: {}, contradictions: [], unsupportedDetails: [], revisions: [],
    lastReply: INTRO, notice: '', ending: null,
  };
}

const notice = (state, text) => ({ ...state, notice: text });
const phaseTurns = (state, phase = state.phase) => state.statements.filter(turn => turn.phase === phase);
const addUnique = (array, values) => [...new Set([...array, ...values])];

function enterPhase(state, phase, opening = '') {
  return {
    ...state, phase, notice: '', lastReply: opening,
    conversation: opening
      ? [...state.conversation, { speaker: 'Spassky', text: opening, phase, source: 'authored' }]
      : state.conversation,
  };
}

/** All mutations return a new state. Invalid/early actions leave progress unchanged. */
export function transition(state, action) {
  if (!state || !action || typeof action.type !== 'string') throw new TypeError('A state and an action type are required.');
  if (action.type === 'RESET') return createGame({ mode: action.mode || state.mode });
  if (action.type === 'SET_MODE') {
    if (!['scripted', 'live'].includes(action.mode)) return notice(state, 'Choose Live AI or the labelled scripted experience.');
    return notice({ ...state, mode: action.mode }, action.mode === 'scripted' ? 'Scripted mode: Spassky uses fixed authored lines.' : 'Live AI mode selected. Your statement is sent only when you submit it.');
  }
  if (action.type === 'PAUSE') return { ...state, paused: true, notice: 'Paused. Your statement has not been submitted.' };
  if (action.type === 'RESUME') return { ...state, paused: false, notice: '' };
  if (state.paused) return notice(state, 'Resume the case first.');

  switch (action.type) {
    case 'START':
      return state.phase === 'menu'
        ? enterPhase(state, 'night')
        : notice(state, 'The case has already started.');
    case 'TUNE_RADIO': {
      if (state.phase !== 'night') return notice(state, 'The radio is part of your night memory.');
      if (state.radioTuned) return state;
      const value = Number(action.value);
      if (!Number.isFinite(value)) return notice(state, 'Turn the radio dial slowly.');
      const radio = Math.max(0, Math.min(1, value));
      const radioTuned = Math.abs(radio - RADIO_TARGET) <= RADIO_TOLERANCE;
      return { ...state, radio, radioTuned, notice: radioTuned ? '“A snow storm. Please stay indoors during these times.” The latch clicks. The door is closing.' : 'Static. Turn the dial until the voice becomes clear.' };
    }
    case 'OPEN_NIGHT_DOOR':
      if (state.phase !== 'night') return notice(state, 'You are no longer in that memory.');
      if (!state.radioTuned) return notice(state, 'Clear the radio signal first.');
      return { ...state, nightDoorOpened: true, notice: 'You open the door and call for Nick. No answer. You leave it unlocked, return to the sofa, and black out.' };
    case 'OBSERVE': {
      const item = OBSERVATIONS[action.id];
      if (!item || item.phase !== state.phase) return notice(state, 'That detail is not visible in this memory.');
      return { ...state, observations: addUnique(state.observations, [action.id]), notice: item.text };
    }
    case 'TRY_MORNING_DOOR':
      return state.phase === 'morning'
        ? { ...state, morningDoorTried: true, notice: "It's locked. The key hangs on the hook beside the frame." }
        : notice(state, 'That door belongs to the morning memory.');
    case 'TAKE_KEY':
      return state.phase === 'morning'
        ? { ...state, keyTaken: true, notice: 'You take the key from its hook inside the cabin.' }
        : notice(state, 'The key is not here.');
    case 'UNLOCK_DOOR':
      if (state.phase !== 'morning') return notice(state, 'That door belongs to the morning memory.');
      if (!state.keyTaken) return notice(state, 'Take the key from the hook first.');
      return { ...state, morningDoorTried: true, morningDoorUnlocked: true, notice: 'You unlock it. Aaron asks you to carry Nick inside. Ivy says she was upstairs with Aaron all night. Priya asks, “All night?” Aaron cuts her off.' };
    case 'SUBMIT_STATEMENT':
      return submitStatement(state, action.text, action.meta);
    case 'CONTINUE': {
      if (!getView(state).continueAvailable) return notice(state, getView(state).objective);
      if (state.phase === 'night') return enterPhase(state, 'recall', RECALL_OPENING);
      if (state.phase === 'recall') return enterPhase(state, 'morning');
      if (state.phase === 'morning') return enterPhase(state, 'verdict', VERDICT_OPENING);
      if (state.phase === 'verdict') {
        const ending = evaluateEnding(state);
        return { ...enterPhase(state, 'ending', ENDINGS[ending.id].line), ending };
      }
      return state;
    }
    default:
      return state;
  }
}

/** A name in quoted testimony is not automatically an accusation. */
export function detectAccusation(text) {
  const clean = String(text || '').replace(/[’]/g, "'").toLowerCase();
  if (/\b(?:maybe|possibly|perhaps|i don.t know|not sure|unsure)\b/.test(clean)) return '';
  if (/\b(?:aaron|ivy|priya)\b\s*(?:,\s*)?(?:or|and\/or|and)\s+\b(?:aaron|ivy|priya)\b/.test(clean)) return '';
  const patterns = [
    /\b(?:it was|it is|it's|i accuse|i blame|i suspect|i think it was|i think|i believe it was)\s+(aaron|ivy|priya)\b/g,
    /\b(aaron|ivy|priya)\s+(?:did it|killed nick|murdered nick|is guilty|was responsible)\b/g,
  ];
  const accused = [];
  for (const pattern of patterns) {
    for (const match of clean.matchAll(pattern)) {
      const preceding = clean.slice(Math.max(0, match.index - 28), match.index);
      if (/\b(?:not|never|don't|do not|didn't|did not)\s+(?:think\s+)?(?:that\s+)?$/.test(preceding)) continue;
      accused.push(match[1]);
    }
  }
  if (new Set(accused).size === 1) return accused[0];
  if (new Set(accused).size > 1) return '';
  return /^(aaron|ivy|priya)[.!]?\s*$/.exec(clean.trim())?.[1] || '';
}

function explicitAssertions(text) {
  const result = {};
  if (/\b(?:i left (?:it|the door) unlocked|i did not lock (?:it|the door)|i didn.t lock (?:it|the door))\b/i.test(text)) result.nightLock = 'unlocked';
  else if (/\b(?:i locked (?:it|the door)|i left (?:it|the door) locked)\b/i.test(text)) result.nightLock = 'locked';
  if (/\b(?:no answer|no reply|nick didn.t answer|nick did not answer|nothing came back|i heard nothing)\b/i.test(text)) result.nickAnswered = false;
  else if (/\b(?:nick answered me|nick replied|i heard him answer)\b/i.test(text)) result.nickAnswered = true;
  return result;
}

function recordAssertions(state, text) {
  const incoming = explicitAssertions(text);
  const assertions = { ...state.assertions };
  const contradictions = [...state.contradictions];
  const revisions = [...state.revisions];
  const correction = /\b(?:i was wrong|i need to correct|let me correct|i misremembered|i take that back)\b/i.test(text);
  for (const [id, value] of Object.entries(incoming)) {
    if (Object.hasOwn(assertions, id) && assertions[id] !== value) {
      const change = { id, before: assertions[id], after: value, turn: state.statements.length + 1 };
      (correction ? revisions : contradictions).push(change);
    }
    assertions[id] = value;
  }
  return { assertions, contradictions, revisions };
}

export function submitStatement(state, text, meta = {}) {
  if (state.paused) return notice(state, 'Resume before submitting your statement.');
  if (!['recall', 'verdict'].includes(state.phase)) return notice(state, 'The officer is not taking a statement yet.');
  const clean = String(text || '').trim();
  if (!clean) return notice(state, 'Speak or write a statement first.');
  if (clean.length > MAX_STATEMENT_CHARS) return notice(state, `Keep each statement under ${MAX_STATEMENT_CHARS} characters.`);
  const count = phaseTurns(state).length;
  if (count >= MAX_PHASE_TURNS) return notice(state, 'Spassky has heard enough. Continue the case.');
  const live = state.mode === 'live';
  if (live && !String(meta.reply || '').trim()) return notice(state, 'The AI reply did not arrive. Retry, or choose the labelled scripted mode.');
  const accusation = state.phase === 'verdict' ? detectAccusation(clean) : '';
  const defaultReply = state.phase === 'recall'
    ? SCRIPTED_RECALL[count]
    : count + 1 >= MAX_PHASE_TURNS
      ? "That's enough from you. I've heard enough."
      : accusation ? `So you think it's ${accusation[0].toUpperCase() + accusation.slice(1)}, huh? Tell me why.` : VERDICT_OPENING;
  const reply = live ? String(meta.reply).trim() : defaultReply;
  const turn = { phase: state.phase, text: clean, input: meta.input === 'voice' ? 'voice' : 'text', source: live ? 'live' : 'scripted', accusation };
  // Affect is a soft voice cue only. Text submissions cannot silently inherit a vocal reading.
  if (turn.input === 'voice' && live && meta.affect) turn.affect = meta.affect;
  const checked = recordAssertions(state, clean);
  const unsupported = Array.isArray(meta.unsupportedDetails) && live
    ? meta.unsupportedDetails.filter(id => typeof id === 'string').slice(0, 6)
    : [];
  const next = {
    ...state, ...checked,
    statements: [...state.statements, turn],
    marks: addUnique(state.marks, MARKS.filter(([, pattern]) => pattern.test(clean)).map(([id]) => id)),
    unsupportedDetails: addUnique(state.unsupportedDetails, unsupported),
    conversation: [...state.conversation, { speaker: 'David', text: clean, phase: state.phase, source: turn.input }, { speaker: 'Spassky', text: reply, phase: state.phase, source: turn.source }],
    lastReply: reply,
    notice: !live ? 'Scripted response. Your statement still shapes the case outcome.' : '',
  };
  return next;
}

/** Game resolution, not a truth/deception classifier. Affect is deliberately absent. */
export function evaluateEnding(state) {
  const verdict = phaseTurns(state, 'verdict');
  const accusations = verdict.map(turn => turn.accusation).filter(Boolean);
  const unique = [...new Set(accusations)];
  const accused = unique.length === 1 ? unique[0] : '';
  const supportText = verdict.map(turn => turn.text).join(' ');
  const citedClues = CLUES.filter(([, available, pattern]) => available(state) && pattern.test(supportText)).map(([id]) => id);
  const credibility = Math.max(0, Math.min(1, 0.2 + 0.6 * (state.marks.length / MARKS.length) - 0.18 * (state.contradictions.length + state.unsupportedDetails.length)));
  let id = 'E_DAVID';
  let reason = 'The account did not support one clear accusation.';
  if (unique.length > 1) reason = 'The witness accused more than one person.';
  else if (state.contradictions.length + state.unsupportedDetails.length >= 2) reason = 'The account contains multiple unsupported or conflicting details.';
  else if (credibility < 0.6) reason = 'The account leaves too much of the sequence unexplained.';
  else if (accused === 'aaron' && citedClues.length >= 2) { id = 'E_AARON'; reason = 'A clear accusation was supported by observed details.'; }
  else if (accused === 'aaron') reason = 'Naming Aaron was not enough to support the accusation.';
  else if (accused === 'ivy') { id = 'E_IVY'; reason = 'The officer accepted the accusation of Ivy.'; }
  else if (accused === 'priya') { id = 'E_PRIYA'; reason = 'The officer accepted the accusation of Priya.'; }
  return { id, accused, citedClues, credibility, reason, ...ENDINGS[id], closing: '14:20 — Ivy has asked to make a second statement. She is still waiting.', quotes: state.statements.filter(turn => turn.text).slice(-3).map(turn => turn.text) };
}

export function getView(state) {
  const count = phaseTurns(state).length;
  const common = {
    phase: state.phase, scene: ['night', 'morning'].includes(state.phase) ? state.phase : 'interrogation',
    officer: state.lastReply, mode: state.mode, modeLabel: state.mode === 'live' ? 'Live AI' : 'Scripted experience',
    notice: state.notice, paused: state.paused, progress: PHASES.indexOf(state.phase) / (PHASES.length - 1),
    statementCount: count, maxStatements: MAX_PHASE_TURNS,
    turnsRemaining: Math.max(0, MAX_PHASE_TURNS - count),
    minimumTurnsRemaining: ['recall', 'verdict'].includes(state.phase) ? Math.max(0, 2 - count) : 0,
    canSubmit: !state.paused && ['recall', 'verdict'].includes(state.phase) && count < MAX_PHASE_TURNS,
    ending: state.ending,
  };
  const views = {
    menu: { heading: 'FALSE POSITIVE', objective: 'You are David. Remember what you can. Tell Spassky what you know.', continueAvailable: false, continueLabel: 'Begin case', purpose: '' },
    night: { heading: '00:52. The cabin.', objective: !state.radioTuned ? 'Turn the radio dial until the storm warning becomes clear.' : !state.nightDoorOpened ? 'Open the latch and call into the storm.' : 'Nothing answers. Return to the interrogation.', continueAvailable: state.radioTuned && state.nightDoorOpened, continueLabel: 'Return to Spassky', purpose: '' },
    recall: { heading: 'What do you remember?', objective: count >= MAX_PHASE_TURNS ? 'This part of your statement is complete. Continue to the morning.' : count < 2 ? `Give your account in your own words. ${2 - count} statement${count === 0 ? 's' : ''} before the next memory. Admitting uncertainty is allowed.` : 'Continue to the morning, or add another detail to your statement.', continueAvailable: count >= 2, continueLabel: 'Remember the morning', purpose: 'p2' },
    morning: { heading: '09:00. Someone screams.', objective: !state.morningDoorTried ? 'Try the front door.' : !state.keyTaken ? 'Take the key hanging inside the cabin.' : !state.morningDoorUnlocked ? 'Use the key to unlock the door.' : 'Remember what Ivy and Aaron say. Return to Spassky.', continueAvailable: state.morningDoorUnlocked, continueLabel: 'Give your final statement', purpose: '' },
    verdict: { heading: 'Who, David?', objective: count >= 2 ? 'You can finish your statement now. An uncertain account still receives an ending.' : 'Defend your account. If you accuse someone, explain what you actually observed. You may also admit that you do not know.', continueAvailable: count >= 2 || (count >= 1 && Boolean(detectAccusation(phaseTurns(state).at(-1)?.text)) && CLUES.filter(([, available, pattern]) => available(state) && pattern.test(phaseTurns(state).at(-1)?.text || '')).length >= 2), continueLabel: 'Finish your statement', purpose: 'p3' },
    ending: { heading: state.ending?.title || 'The statement is over.', objective: state.ending?.closing || '', continueAvailable: false, continueLabel: 'Start again', purpose: '' },
  };
  return { ...common, ...views[state.phase], continueAvailable: !state.paused && views[state.phase].continueAvailable };
}
