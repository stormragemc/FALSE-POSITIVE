(() => {
  "use strict";

  const STORAGE_KEY = "false-positive.web-fallback.v1";
  const SESSION_KEY = "false-positive.web-fallback.client-key";
  const TRANSCRIPT_SESSION_KEY = "false-positive.web-fallback.transcripts";
  const STATE_VERSION = 1;
  const TARGET_SAMPLE_RATE = 16000;
  const MIN_RECORDING_MS = 550;
  const MAX_RECORDING_MS = 19000;
  const failedRecorders = new WeakSet();

  const CUTSCENES = [
    { id: "CS-01", scene: "p1", title: "Wake", duration: "8 sec" },
    { id: "CS-02", scene: "p1", title: "Spassky's answer", duration: "12 sec" },
    { id: "CS-03", scene: "p1", title: "Fuzzy rewind to night", duration: "3 sec" },
    { id: "CS-04", scene: "m1", title: "Stand from the chair", duration: "4 sec" },
    { id: "CS-05", scene: "m1", title: "Radio clears", duration: "6 sec" },
    { id: "CS-06", scene: "m1", title: "Someone left", duration: "4 sec" },
    { id: "CS-07", scene: "m1", title: "Call for Nick", duration: "10 sec" },
    { id: "CS-08", scene: "m1", title: "Fuzzy to interrogation", duration: "3 sec" },
    { id: "CS-09", scene: "p2", title: "Fuzzy to morning", duration: "3 sec" },
    { id: "CS-10", scene: "m2", title: "Priya screams / body reveal", duration: "14 sec" },
    { id: "CS-11", scene: "m2", title: "Ivy and Aaron come down", duration: "5 sec" },
    { id: "CS-12", scene: "m2", title: "Out into the snow", duration: "10 sec" },
    { id: "CS-13", scene: "m2", title: "The carry", duration: "25 sec" },
    { id: "CS-14", scene: "m2", title: "The sofa / Priya dials", duration: "8 sec" },
    { id: "CS-15", scene: "m2", title: "Fuzzy to verdict", duration: "3 sec" },
    { id: "CS-16A", scene: "p3", title: "The good years", duration: "13 sec" },
    { id: "CS-16B", scene: "p3", title: "When it went wrong", duration: "13 sec" },
    { id: "CS-17A", scene: "p3", title: "Aaron accusation flashback", duration: "3 sec" },
    { id: "CS-17B", scene: "p3", title: "Ivy accusation flashback", duration: "3 sec" },
    { id: "CS-17C", scene: "p3", title: "Priya accusation flashback", duration: "3 sec" },
    { id: "CS-18A", scene: "p4", title: "David is taken", duration: "15–25 sec" },
    { id: "CS-18B", scene: "p4", title: "Aaron is taken", duration: "15–25 sec" },
    { id: "CS-18C", scene: "p4", title: "Ivy is taken", duration: "15–25 sec" },
    { id: "CS-18D", scene: "p4", title: "Priya is taken", duration: "15–25 sec" },
  ];

  const SCENES = [
    {
      id: "s0",
      phase: "Intake",
      code: "S0 / MAIN MENU",
      time: "before statement",
      title: "Nobody went out.",
      summary: "Five friends entered the cabin. By morning one was dead, the door was locked, and the key was still inside.",
      rail: "Microphone consent and calibration",
    },
    {
      id: "p1",
      phase: "Waking",
      code: "S1 / P1_TUTORIAL",
      time: "interview room",
      title: "Who are you?",
      summary: "You wake beneath a fluorescent lamp. Officer Spassky already knows your name.",
      rail: "Learn to answer with your voice",
    },
    {
      id: "m1",
      phase: "Night memory",
      code: "S2 / M1_NIGHT",
      time: "00:50",
      title: "The last thing you remember.",
      summary: "A dying fire, five empty cups, a storm warning, and a door swinging shut.",
      rail: "Search the cabin before the blackout",
    },
    {
      id: "p2",
      phase: "Recall",
      code: "S1 / P2_RECALL",
      time: "interview room",
      title: "What's the last thing you remember?",
      summary: "Spassky follows omissions, contradictions, and the details you should not know.",
      rail: "Give your account under pressure",
    },
    {
      id: "m2",
      phase: "Morning memory",
      code: "S3 / M2_MORNING",
      time: "09:00",
      title: "The door was locked.",
      summary: "Priya screams. Nick lies outside the broken window. Someone wants the body moved.",
      rail: "Find the key and inspect the scene",
    },
    {
      id: "p3",
      phase: "Verdict",
      code: "S1 / P3_VERDICT",
      time: "interview room",
      title: "Tell me why I should spare your life.",
      summary: "Name Aaron, Ivy, or Priya—and explain what your memory actually proves.",
      rail: "Accuse one person with your voice",
    },
    {
      id: "p4",
      phase: "Outcome",
      code: "S1 / P4_ENDING",
      time: "14:20",
      title: "One statement changes.",
      summary: "The officer closes the folder. Somewhere in the station, Ivy is still waiting.",
      rail: "Read the outcome of your statement",
    },
  ];

  const EVIDENCE = [
    { id: "five_cups", label: "Five cups. Nobody else arrived." },
    { id: "storm_warning", label: "The radio warned everyone to stay inside." },
    { id: "thin_jacket", label: "Nick left in David's thin jacket." },
    { id: "clock", label: "The mantel clock read 00:52." },
    { id: "door_locked", label: "The front door was locked in the morning." },
    { id: "key_inside", label: "The key hung inside, left of the door." },
    { id: "grille_intact", label: "Glass fell inward; the grille still held." },
    { id: "affair", label: "Aaron learned about Nick and Ivy that night." },
    { id: "ivy_alibi", label: "Ivy volunteered Aaron's alibi unprompted." },
    { id: "aaron_move", label: "Aaron proposed moving Nick's body." },
  ];

  const STORY_MARKS = [
    { id: "m_fire", label: "Drinking by the fire", words: ["drink", "drank", "beer", "fire", "fireplace", "cup"] },
    { id: "m_argument", label: "The argument with Nick", words: ["argue", "argument", "fight", "told him", "said to him"] },
    { id: "m_nick_left", label: "Nick went outside", words: ["left", "went out", "outside", "door closed", "gone"] },
    { id: "m_door", label: "Calling at the door", words: ["door", "opened", "called", "shouted", "yelled", "his name"] },
    { id: "m_lock", label: "Whether the door was locked", words: ["lock", "locked", "unlocked", "bolt", "key"] },
    { id: "m_sleep", label: "The blackout", words: ["sofa", "slept", "passed out", "blacked out", "don't remember", "do not remember"] },
    { id: "m_morning", label: "What happened after the scream", words: ["scream", "morning", "window", "carried", "body", "aaron"] },
  ];

  const ENDINGS = {
    E_DAVID: {
      id: "CS-18A",
      label: "David is taken",
      copy: "You were the only one who couldn't tell me where you were.",
    },
    E_AARON: {
      id: "CS-18B",
      label: "Aaron is taken",
      copy: "He locked it. You unlocked it. Only one of those was a decision.",
    },
    E_IVY: {
      id: "CS-18C",
      label: "Ivy is taken",
      copy: "She agreed with you. That's not the same as it being true.",
    },
    E_PRIYA: {
      id: "CS-18D",
      label: "Priya is taken",
      copy: "She's the one who called us. Sit with that.",
    },
  };

  const SCENE_INSTRUCTIONS = {
    p2: [
      "PHASE: P2_RECALL. Ask what David remembers about the night.",
      "Cover drinking by the fire, the argument, Nick leaving, the door, the lock, the blackout, and the morning.",
      "Treat emotion as pressure, never proof. Do not say the witness is lying.",
    ].join("\n"),
    p3: [
      "PHASE: P3_VERDICT. David must defend himself and name Aaron, Ivy, or Priya.",
      "Ask for reasoning grounded in observed clues. Do not reveal the ground truth.",
      "Treat affect as pressure, never proof of guilt or deception.",
    ].join("\n"),
  };

  const OFFLINE_REPLIES = window.FalsePositiveSpeech?.offlineRecallLines || [];

  const DEFAULT_STATE = {
    version: STATE_VERSION,
    sceneIndex: 0,
    maxUnlocked: 0,
    mode: "offline",
    micConsent: false,
    captions: true,
    backendUrl: "",
    sessionId: "",
    evidence: {},
    marks: {},
    transcripts: [],
    cutsceneSources: {},
    phaseFlags: {},
    accusation: "",
    ending: "E_DAVID",
    calibrationPeak: 0.08,
  };

  const elements = {
    modeBadge: document.querySelector("#mode-badge"),
    progressLabel: document.querySelector("#progress-label"),
    progressCount: document.querySelector("#progress-count"),
    progress: document.querySelector(".case-progress"),
    progressBar: document.querySelector("#progress-bar"),
    phaseList: document.querySelector("#phase-list"),
    phaseRailStatus: document.querySelector("#phase-rail-status"),
    sceneContent: document.querySelector("#scene-content"),
    liveRegion: document.querySelector("#live-region"),
    evidenceList: document.querySelector("#evidence-list"),
    evidenceCount: document.querySelector("#evidence-count"),
    marksList: document.querySelector("#marks-list"),
    marksCount: document.querySelector("#marks-count"),
    transcriptList: document.querySelector("#transcript-list"),
    turnCount: document.querySelector("#turn-count"),
    settingsButton: document.querySelector("#settings-button"),
    settingsDialog: document.querySelector("#settings-dialog"),
    settingsForm: document.querySelector("#settings-form"),
    liveSettings: document.querySelector("#live-settings"),
    backendUrl: document.querySelector("#backend-url"),
    backendKey: document.querySelector("#backend-key"),
    captionsToggle: document.querySelector("#captions-toggle"),
    connectionTest: document.querySelector("#connection-test"),
    connectionResult: document.querySelector("#connection-result"),
    resetButton: document.querySelector("#reset-button"),
    resetDialog: document.querySelector("#reset-dialog"),
    confirmReset: document.querySelector("#confirm-reset"),
    notebookToggle: document.querySelector("#notebook-toggle"),
    notebookBody: document.querySelector("#notebook-body"),
  };

  const officerSpeech = window.FalsePositiveSpeech?.createOfficerSpeech(window) || null;
  let state = loadState();
  let micStream = null;
  let micReady = false;
  let audioContext = null;
  let analyser = null;
  let micSource = null;
  let meterFrame = 0;
  let meterBuffer = null;
  let mediaRecorder = null;
  let recordingChunks = [];
  let recordingStartedAt = 0;
  let recordingTimeout = 0;
  let recordingPeak = 0;
  let recordingPurpose = "";
  let recognition = null;
  let recognizedFinal = "";
  let recognizedInterim = "";
  let calibrationSamples = null;
  let pendingTurn = null;
  let activeTurnController = null;
  let experienceGeneration = 0;
  let suppressCutsceneRender = false;
  let currentError = "";
  let isProcessing = false;

  function loadState() {
    try {
      const raw = localStorage.getItem(STORAGE_KEY);
      if (!raw) {
        sessionStorage.removeItem(TRANSCRIPT_SESSION_KEY);
        return { ...DEFAULT_STATE, sessionId: createSessionId() };
      }
      const parsed = JSON.parse(raw);
      if (parsed.version !== STATE_VERSION) {
        sessionStorage.removeItem(TRANSCRIPT_SESSION_KEY);
        return { ...DEFAULT_STATE, sessionId: createSessionId() };
      }
      if (Object.hasOwn(parsed, "transcripts")) {
        delete parsed.transcripts;
        try { localStorage.setItem(STORAGE_KEY, JSON.stringify(parsed)); } catch (_error) { /* best-effort privacy migration */ }
      }
      return {
        ...DEFAULT_STATE,
        ...parsed,
        evidence: { ...parsed.evidence },
        marks: { ...parsed.marks },
        transcripts: loadSessionTranscripts(),
        cutsceneSources: { ...parsed.cutsceneSources },
        phaseFlags: { ...parsed.phaseFlags },
        sessionId: parsed.sessionId || createSessionId(),
      };
    } catch (_error) {
      sessionStorage.removeItem(TRANSCRIPT_SESSION_KEY);
      return { ...DEFAULT_STATE, sessionId: createSessionId() };
    }
  }

  function saveState() {
    const safeState = { ...state };
    delete safeState.transcripts;
    localStorage.setItem(STORAGE_KEY, JSON.stringify(safeState));
    sessionStorage.setItem(TRANSCRIPT_SESSION_KEY, JSON.stringify(state.transcripts.slice(-80)));
  }

  function loadSessionTranscripts() {
    try {
      const transcripts = JSON.parse(sessionStorage.getItem(TRANSCRIPT_SESSION_KEY) || "[]");
      if (!Array.isArray(transcripts)) return [];
      let davidTurnNumber = 0;
      return transcripts.slice(-80).map((turn) => {
        if (turn?.speaker !== "David") return turn;
        davidTurnNumber = Math.max(davidTurnNumber + 1, Number(turn.turnNumber) || 0);
        return { ...turn, turnNumber: davidTurnNumber };
      });
    } catch (_error) {
      return [];
    }
  }

  function createSessionId() {
    if (globalThis.crypto && typeof globalThis.crypto.randomUUID === "function") {
      return globalThis.crypto.randomUUID();
    }
    return `web-${Date.now()}-${Math.random().toString(16).slice(2)}`;
  }

  function escapeHtml(value) {
    return String(value)
      .replaceAll("&", "&amp;")
      .replaceAll("<", "&lt;")
      .replaceAll(">", "&gt;")
      .replaceAll('"', "&quot;")
      .replaceAll("'", "&#039;");
  }

  function announce(message) {
    elements.liveRegion.textContent = "";
    window.setTimeout(() => {
      elements.liveRegion.textContent = message;
    }, 20);
  }

  function setError(message) {
    currentError = message || "";
    if (message) announce(message);
  }

  function sceneShell(scene, objective, body) {
    return `
      <article class="scene-card" data-scene="${scene.id}">
        <div class="scene-kicker">
          <span>${escapeHtml(scene.code)}</span>
          <span class="scene-kicker__time">${escapeHtml(scene.time)}</span>
        </div>
        <header class="scene-heading">
          <h2>${escapeHtml(scene.title)}</h2>
          <p class="scene-heading__summary">${escapeHtml(scene.summary)}</p>
        </header>
        ${objective ? `
          <section class="objective-card" aria-label="Current objective">
            <div class="objective-card__header"><span>Current objective</span><span>active</span></div>
            <p>${escapeHtml(objective)}</p>
          </section>
        ` : ""}
        ${body}
        <div id="cutscenes" class="cutscene-stack"></div>
        <div id="scene-error"></div>
        <div id="next-scene"></div>
      </article>
    `;
  }

  function renderAll(options = {}) {
    const activeScene = SCENES[state.sceneIndex];
    const currentCard = document.querySelector(".scene-card");
    const stableCutscenes = currentCard?.dataset.scene === activeScene.id
      ? document.querySelector("#cutscenes")
      : null;
    document.body.dataset.captions = String(state.captions);
    document.body.dataset.scene = activeScene.id;
    elements.modeBadge.textContent = state.mode === "live" ? "Live · AI sidecar" : "Offline · scripted";
    elements.modeBadge.dataset.mode = state.mode;
    elements.progressLabel.textContent = activeScene.phase;
    elements.progressCount.textContent = `${String(state.sceneIndex + 1).padStart(2, "0")} / ${String(SCENES.length).padStart(2, "0")}`;
    elements.progress.setAttribute("aria-valuenow", String(state.sceneIndex + 1));
    elements.progressBar.style.width = `${((state.sceneIndex + 1) / SCENES.length) * 100}%`;
    elements.phaseRailStatus.textContent = `${state.maxUnlocked + 1} open`;

    renderPhaseRail();
    if (stableCutscenes) {
      renderScenePreservingCutscenes(stableCutscenes, activeScene.id);
    } else renderScene();
    renderNotebook();

    if (options.focusScene) {
      document.querySelector("#scene-stage")?.focus({ preventScroll: true });
      window.scrollTo({ top: 0, behavior: "smooth" });
    }
  }

  function renderScenePreservingCutscenes(stableCutscenes, sceneId) {
    const liveContent = elements.sceneContent;
    const liveCard = stableCutscenes.closest(".scene-card");
    const scratch = document.createElement("div");
    elements.sceneContent = scratch;
    suppressCutsceneRender = true;
    try {
      renderScene();
    } finally {
      suppressCutsceneRender = false;
      elements.sceneContent = liveContent;
    }

    const nextCard = scratch.querySelector(".scene-card");
    const nextCutscenes = nextCard?.querySelector("#cutscenes");
    if (!liveCard || !nextCard || !nextCutscenes) return;

    while (liveCard.firstChild && liveCard.firstChild !== stableCutscenes) liveCard.firstChild.remove();
    while (stableCutscenes.nextSibling) stableCutscenes.nextSibling.remove();
    while (nextCard.firstChild && nextCard.firstChild !== nextCutscenes) {
      liveCard.insertBefore(nextCard.firstChild, stableCutscenes);
    }
    while (nextCutscenes.nextSibling) liveCard.append(nextCutscenes.nextSibling);
    syncCutsceneActiveStates(stableCutscenes, sceneId);
  }

  function renderPhaseRail() {
    elements.phaseList.innerHTML = "";
    SCENES.forEach((scene, index) => {
      const item = document.createElement("li");
      const button = document.createElement("button");
      const phaseState = index === state.sceneIndex
        ? "active"
        : index <= state.maxUnlocked
          ? "complete"
          : "locked";
      button.type = "button";
      button.className = "phase-card";
      button.dataset.action = "open-phase";
      button.dataset.index = String(index + 1).padStart(2, "0");
      button.dataset.sceneIndex = String(index);
      button.dataset.state = phaseState;
      button.disabled = phaseState === "locked";
      if (phaseState === "active") button.setAttribute("aria-current", "step");

      const code = document.createElement("small");
      code.textContent = scene.code.split(" / ").at(-1);
      const title = document.createElement("strong");
      title.textContent = scene.phase;
      const summary = document.createElement("span");
      summary.textContent = scene.rail;
      button.append(code, title, summary);
      item.append(button);
      elements.phaseList.append(item);
    });
  }

  function renderScene() {
    const scene = SCENES[state.sceneIndex];
    switch (scene.id) {
      case "s0": renderIntake(scene); break;
      case "p1": renderTutorial(scene); break;
      case "m1": renderNightMemory(scene); break;
      case "p2": renderRecall(scene); break;
      case "m2": renderMorningMemory(scene); break;
      case "p3": renderVerdict(scene); break;
      case "p4": renderOutcome(scene); break;
      default: throw new Error(`Unknown scene ${scene.id}`);
    }

    renderSceneError();
  }

  function renderIntake(scene) {
    const micState = micReady ? "ready" : state.micConsent ? "idle" : "blocked";
    const micLabel = micReady ? "Microphone ready" : state.micConsent ? "Reconnect microphone" : "Permission not granted";
    const body = `
      <p class="story-copy">
        You are David. You remember an argument, a storm, and calling into the dark. By morning,
        Nick was dead outside a locked cabin. The officer will listen to what you say and how the
        pressure changes your voice. He cannot know why you sound afraid.
      </p>
      <section class="mic-panel">
        <div class="mic-status">
          <span class="mic-status__state" data-state="${micState}">${micLabel}</span>
          <span>16 kHz mono target</span>
        </div>
        <div class="level-meter" aria-hidden="true"><span id="level-fill"></span></div>
        <p class="mic-prompt">Speak normally for a few seconds. Say anything.</p>
        <div class="button-row">
          <button class="primary-button" type="button" data-action="enable-mic" ${isProcessing ? "disabled" : ""}>
            ${isProcessing ? "Calibrating…" : micReady ? "Calibrate again" : "Enable microphone"}
          </button>
          <button class="secondary-button" type="button" data-action="enter-story" ${micReady ? "" : "disabled"}>
            Enter interrogation
          </button>
        </div>
      </section>
      <section class="privacy-card">
        <strong>This game listens; it does not save recordings</strong>
        <p>Audio stays in memory for the current answer. Offline mode does not send the recording to the FALSE POSITIVE backend, but supported browsers may use their own speech service for the optional transcript. Live AI mode sends the finished answer to the configured Sidecar.</p>
      </section>
    `;
    elements.sceneContent.innerHTML = sceneShell(scene, "Enable and calibrate your microphone", body);
  }

  function renderTutorial(scene) {
    const answered = Boolean(state.phaseFlags.p1Answered);
    const body = `
      <p class="story-copy">Black. A voice says your name three times. Each time it gets closer.</p>
      <div class="spoken-line" data-speaker="Officer Spassky">
        <p>“David.” … “David.” … “David!”</p>
      </div>
      ${answered ? `
        <div class="spoken-line" data-speaker="Officer Spassky">
          <p>“I'm Officer Spassky. Nick is dead, and right now you're one of the suspects. I've already spoken to the others. Take your time and tell me everything you remember from last night.”</p>
        </div>
      ` : renderVoiceComposer("Say: “Who are you? Where am I?”", "p1")}
    `;
    elements.sceneContent.innerHTML = sceneShell(scene, answered ? "Follow the memory" : "Answer the officer out loud", body);
    renderCutscenes("p1");
    if (answered) renderNextScene(2, "Enter the cabin at night");
  }

  function renderNightMemory(scene) {
    const radioFixed = Boolean(state.evidence.storm_warning);
    const doorCalled = Boolean(state.phaseFlags.doorCalled);
    const objective = !radioFixed ? "Fix the radio" : !doorCalled ? "Go to the door and call for Nick" : "Return to the interview room";
    const body = `
      <p class="story-copy">The fire is dying. Bottles crowd the table. The radio spits static into the room while the storm claws at the windows.</p>
      <div class="evidence-grid">
        ${evidenceButton("radio", "Objective", "Tune the radio", radioFixed ? "The warning is clear." : "A voice is buried in the static.", radioFixed)}
        ${evidenceButton("cups", "Optional observation", "Count the cups", state.evidence.five_cups ? "Five. Everyone was already here." : "The table remembers who drank.", state.evidence.five_cups)}
        ${evidenceButton("clock", "Optional observation", "Read the mantel clock", state.evidence.clock ? "00:52." : "The hands are still visible.", state.evidence.clock)}
        ${evidenceButton("coat", "Optional observation", "Inspect the heavy coat", state.evidence.thin_jacket ? "Nick had your thin jacket." : "Nick swapped coats with you earlier.", state.evidence.thin_jacket)}
      </div>
      ${radioFixed && !doorCalled ? `
        <div class="spoken-line" data-speaker="Radio">
          <p>“…a snow storm. Please stay indoors during these times.”</p>
        </div>
        ${renderVoiceComposer("Call out for Nick. The storm is taking your voice.", "m1-call", true)}
      ` : ""}
      ${doorCalled ? `
        <div class="spoken-line" data-speaker="The storm">
          <p>The door opens onto white. Your voice disappears. Nothing answers.</p>
        </div>
      ` : ""}
    `;
    elements.sceneContent.innerHTML = sceneShell(scene, objective, body);
    renderCutscenes("m1");
    if (doorCalled) renderNextScene(3, "Return to Spassky");
  }

  function renderRecall(scene) {
    const recallTurns = state.transcripts.filter((turn) => turn.scene === "p2" && turn.speaker === "David").length;
    const foundMarks = STORY_MARKS.filter((mark) => state.marks[mark.id]).length;
    const body = `
      <div class="spoken-line" data-speaker="Officer Spassky">
        <p>“So. What's the last thing you remember?”</p>
      </div>
      <p class="story-copy">Speak freely. The officer will press on gaps. A precise answer about something you never saw can hurt more than uncertainty.</p>
      ${renderVoiceComposer("Give Spassky your account of the night.", "p2")}
      <div class="scene-actions">
        <button class="secondary-button" type="button" data-action="finish-recall">
          Continue to morning
        </button>
      </div>
    `;
    elements.sceneContent.innerHTML = sceneShell(scene, foundMarks === 7 ? "Spassky has covered every story mark" : `Account for the night · ${foundMarks}/7 marks covered`, body);
    renderCutscenes("p2");
    if (state.phaseFlags.p2Complete) window.setTimeout(continueIntoMorning, 0);
  }

  function continueIntoMorning() {
    state.phaseFlags.p2Complete = true;
    const transition = "Walk me through the morning. Start with Priya's scream.";
    const alreadyLogged = state.transcripts.some((turn) => turn.speaker === "Spassky" && turn.text === transition);
    if (!alreadyLogged) addTranscript("Spassky", transition, "m2");
    saveState();
    advanceTo(4);
  }

  function renderMorningMemory(scene) {
    const hasKey = Boolean(state.evidence.key_inside);
    const bodyMoved = Boolean(state.phaseFlags.bodyMoved);
    const objective = !state.evidence.door_locked
      ? "Get outside"
      : !hasKey
        ? "Find the key"
        : !bodyMoved
          ? "Open the door and reach Nick"
          : "Remember who asked you to move him";
    const body = `
      <div class="spoken-line" data-speaker="Priya">
        <p>“Guys! Help! What happened to Nick?”</p>
      </div>
      <p class="story-copy">Grey light. The curtains are open. Nick lies face-down beyond a broken pane, blood frozen behind his head.</p>
      <div class="evidence-grid">
        ${evidenceButton("morning-door", "Objective", "Try the front door", state.evidence.door_locked ? "Locked from inside." : "The handle does not turn.", state.evidence.door_locked)}
        ${evidenceButton("morning-key", hasKey ? "Observed" : "Nearby", "Inspect the hook", hasKey ? "The key was inside all night." : "Immediately left of the frame.", hasKey, !state.evidence.door_locked)}
        ${evidenceButton("morning-window", "Observation", "Inspect the broken window", state.evidence.grille_intact ? "Glass inside. Grille intact." : "The opening was never passable.", state.evidence.grille_intact)}
        ${evidenceButton("morning-carry", "Objective", "Go out and move the body", bodyMoved ? "Aaron suggested it. Ivy supplied his alibi." : "Aaron is already taking the legs.", bodyMoved, !hasKey)}
      </div>
      ${bodyMoved ? `
        <div class="spoken-line" data-speaker="Ivy">
          <p>“I don't know! I was with Aaron upstairs… all night.”</p>
        </div>
        <div class="spoken-line" data-speaker="Aaron">
          <p>“Priya. Not now. Lift on three.”</p>
        </div>
      ` : ""}
    `;
    elements.sceneContent.innerHTML = sceneShell(scene, objective, body);
    renderCutscenes("m2");
    if (bodyMoved) renderNextScene(5, "Face Spassky's verdict");
  }

  function renderVerdict(scene) {
    const hasAccusation = Boolean(state.accusation);
    const accusationName = state.accusation ? capitalize(state.accusation) : "";
    const body = `
      <div class="spoken-line" data-speaker="Officer Spassky">
        <p>“Tell me why I should spare your life.”</p>
      </div>
      <p class="story-copy">A group photograph turns into motion: old friends, a wedding anniversary, five glasses, a coat swap. Then the memory curdles. Aaron hears “two years.” Nick leaves in your thin jacket.</p>
      ${hasAccusation ? `
        <div class="spoken-line" data-speaker="Officer Spassky">
          <p>“${escapeHtml(window.FalsePositiveSpeech?.getOfflineVerdictReply(state.accusation) || `So you think it's ${accusationName}.`) }”</p>
        </div>
        <p class="story-copy">Your accusation is on the record. Spassky closes the folder.</p>
      ` : `
        ${renderVoiceComposer("If it wasn't you, David—who killed Nick? Name one person and explain why.", "p3")}
        <div class="scene-actions">
          <button class="secondary-button" type="button" data-action="continue-ending">Let Spassky decide</button>
        </div>
      `}
    `;
    elements.sceneContent.innerHTML = sceneShell(scene, hasAccusation ? `Accusation recorded: ${accusationName}` : "Name Aaron, Ivy, or Priya—unambiguously", body);
    const flashbackId = state.accusation === "aaron" ? "CS-17A" : state.accusation === "ivy" ? "CS-17B" : state.accusation === "priya" ? "CS-17C" : "";
    renderCutscenes("p3", flashbackId ? [flashbackId] : []);
    if (hasAccusation) renderNextScene(6, "Hear the outcome");
  }

  function renderOutcome(scene) {
    const activeEnding = ENDINGS[state.ending] || ENDINGS.E_DAVID;
    const davidQuotes = state.transcripts
      .filter((turn) => turn.speaker === "David" && turn.text && !turn.text.startsWith("["));
    const quotes = davidQuotes.slice(-3);
    const cards = Object.entries(ENDINGS).map(([id, ending]) => `
      <article class="ending-card" data-active="${id === state.ending}">
        <small>${escapeHtml(ending.id)}</small>
        <h3>${escapeHtml(ending.label)}</h3>
        <p>${escapeHtml(ending.copy)}</p>
      </article>
    `).join("");
    const quoteItems = quotes.length
      ? quotes.map((quote, index) => `<li>Turn ${quote.turnNumber || davidQuotes.length - quotes.length + index + 1}: “${escapeHtml(quote.text)}”</li>`).join("")
      : "<li>No reliable transcript was available. Your recording was not invented for this card.</li>";
    const body = `
      <div class="ending-grid">${cards}</div>
      <div class="spoken-line" data-speaker="Officer Spassky"><p>“${escapeHtml(activeEnding.copy)}”</p></div>
      <section class="outcome-card" aria-label="Final outcome">
        <div class="outcome-meta"><span>case status / statement pending</span><span>${escapeHtml(activeEnding.label)}</span></div>
        <p class="outcome-card__time">14:20 — Ivy has asked to make a second statement.</p>
        <p class="outcome-card__statement">She is still waiting.</p>
        <p class="outcome-card__title">FALSE<br>POSITIVE</p>
        <ul class="quotes-list">${quoteItems}</ul>
      </section>
      <div class="scene-actions">
        <button class="secondary-button" type="button" data-action="open-phase" data-scene-index="0">Review the case from intake</button>
        <button class="primary-button" type="button" data-action="request-reset">Start the night again</button>
      </div>
    `;
    elements.sceneContent.innerHTML = sceneShell(scene, "The statement is closed", body);
    renderCutscenes("p4", [activeEnding.id]);
  }

  function evidenceButton(action, label, title, detail, observed, disabled = false) {
    return `
      <button
        class="evidence-action"
        type="button"
        data-action="observe-evidence"
        data-evidence-action="${escapeHtml(action)}"
        data-observed="${Boolean(observed)}"
        ${observed || disabled ? "disabled" : ""}
      >
        <small>${escapeHtml(observed ? "Recorded" : label)}</small>
        <strong>${escapeHtml(title)}</strong>
        <span>${escapeHtml(detail)}</span>
      </button>
    `;
  }

  function renderVoiceComposer(prompt, purpose, requireLoud = false) {
    const recording = mediaRecorder && mediaRecorder.state === "recording" && recordingPurpose === purpose;
    const speechRecognition = getSpeechRecognitionConstructor();
    const recognitionCopy = speechRecognition
      ? "Browser transcription assists the offline path and may use your browser provider's speech service. You can correct it before recording."
      : "Automatic browser transcription is unavailable here. Type what you intend to say, then speak it into the microphone.";
    return `
      <section class="turn-composer" aria-label="Voice answer">
        <div class="mic-status">
          <span class="mic-status__state" data-state="${recording ? "recording" : micReady ? "ready" : "idle"}" id="mic-state">
            ${recording ? "Listening" : micReady ? "Microphone ready" : "Microphone disconnected"}
          </span>
          <span>${requireLoud ? "loudness gate" : state.mode === "live" ? "live AI turn" : "offline scripted turn"}</span>
        </div>
        <div class="level-meter" aria-hidden="true"><span id="level-fill"></span></div>
        <p class="mic-prompt">${escapeHtml(prompt)}</p>
        <label class="sr-only" for="transcript-repair">Transcript correction</label>
        <textarea
          class="transcript-repair"
          id="transcript-repair"
          rows="2"
          placeholder="Optional transcript correction for browsers without speech recognition"
        ></textarea>
        <p class="turn-hint" id="recognition-preview">${escapeHtml(recognitionCopy)}</p>
        <div class="turn-composer__actions">
          <p class="turn-hint">Press once to begin. Press again when you finish. Answers stop after 19 seconds and audio is not written to disk.</p>
          <button
            class="record-button"
            type="button"
            data-action="record"
            data-purpose="${escapeHtml(purpose)}"
            data-recording="${recording}"
            ${isProcessing ? "disabled" : ""}
          >
            ${isProcessing ? "Processing…" : recording ? "Finish answer" : "Begin speaking"}
          </button>
        </div>
        ${pendingTurn && pendingTurn.purpose === purpose ? `
          <div class="button-row">
            <button class="secondary-button" type="button" data-action="retry-pending">Retry the last recording</button>
            <button class="secondary-button" type="button" data-action="switch-offline">Use offline scripted mode</button>
          </div>
        ` : ""}
      </section>
    `;
  }

  function renderCutscenes(sceneId, activeIds = []) {
    const container = elements.sceneContent.querySelector("#cutscenes");
    if (!container || suppressCutsceneRender) return;
    const cutscenes = CUTSCENES.filter((cutscene) => cutscene.scene === sceneId);
    cutscenes.forEach((cutscene) => {
      const source = validateCutsceneUrl(state.cutsceneSources[cutscene.id] || "");
      const article = document.createElement("article");
      article.className = "cutscene-frame";
      article.dataset.cutsceneId = cutscene.id;
      article.dataset.active = String(activeIds.includes(cutscene.id));

      const meta = document.createElement("div");
      meta.className = "cutscene-frame__meta";
      meta.innerHTML = `
        <span class="cutscene-frame__id">${escapeHtml(cutscene.id)}</span>
        <span class="cutscene-frame__title">${escapeHtml(cutscene.title)}</span>
        <span class="cutscene-frame__duration">${escapeHtml(cutscene.duration)}</span>
      `;

      const viewport = document.createElement("div");
      viewport.className = "cutscene-frame__viewport";
      const empty = document.createElement("div");
      empty.className = "cutscene-frame__empty";
      empty.innerHTML = `CUTSCENE / AWAITING FOOTAGE<span>${escapeHtml(cutscene.id)} · blank gameplay iframe</span>`;
      empty.hidden = Boolean(source);
      const iframe = document.createElement("iframe");
      iframe.title = `${cutscene.id}: ${cutscene.title} gameplay video slot`;
      iframe.dataset.cutsceneId = cutscene.id;
      iframe.src = source || "about:blank";
      iframe.loading = "lazy";
      iframe.allow = "autoplay; fullscreen; picture-in-picture";
      iframe.referrerPolicy = "no-referrer";
      iframe.setAttribute("sandbox", "allow-scripts allow-presentation");
      viewport.append(empty, iframe);

      const controls = document.createElement("div");
      controls.className = "cutscene-frame__controls";
      const label = document.createElement("label");
      const inputId = `video-source-${cutscene.id.toLowerCase()}`;
      label.htmlFor = inputId;
      label.textContent = "Video URL";
      const input = document.createElement("input");
      input.id = inputId;
      input.type = "url";
      input.placeholder = "https://… or leave blank";
      input.value = source;
      input.autocomplete = "off";
      const apply = document.createElement("button");
      apply.type = "button";
      apply.className = "cutscene-apply";
      apply.dataset.action = "apply-cutscene";
      apply.dataset.cutsceneId = cutscene.id;
      apply.textContent = source ? "Update" : "Attach";
      controls.append(label, input, apply);

      article.append(meta, viewport, controls);
      container.append(article);
    });
  }

  function syncCutsceneActiveStates(container, sceneId) {
    const activeIds = sceneId === "p3"
      ? [state.accusation === "aaron" ? "CS-17A" : state.accusation === "ivy" ? "CS-17B" : state.accusation === "priya" ? "CS-17C" : ""]
      : sceneId === "p4"
        ? [(ENDINGS[state.ending] || ENDINGS.E_DAVID).id]
        : [];
    container.querySelectorAll(".cutscene-frame").forEach((frame) => {
      frame.dataset.active = String(activeIds.includes(frame.dataset.cutsceneId));
    });
  }

  function renderNextScene(nextIndex, buttonLabel) {
    const target = elements.sceneContent.querySelector("#next-scene");
    const next = SCENES[nextIndex];
    if (!target || !next) return;
    target.innerHTML = `
      <section class="next-scene-card" aria-label="Next scene">
        <span class="next-scene-card__index">${String(nextIndex + 1).padStart(2, "0")}</span>
        <div><small>Next scene</small><strong>${escapeHtml(next.phase)} · ${escapeHtml(next.time)}</strong></div>
        <button class="primary-button" type="button" data-action="advance" data-next-index="${nextIndex}">${escapeHtml(buttonLabel)}</button>
      </section>
    `;
  }

  function renderSceneError() {
    const target = elements.sceneContent.querySelector("#scene-error");
    if (!target) return;
    target.innerHTML = "";
    if (!currentError) return;
    target.innerHTML = `
      <section class="error-card" role="alert">
        <strong>The scene could not continue</strong>
        <p>${escapeHtml(currentError)}</p>
      </section>
    `;
  }

  function renderNotebook() {
    const evidenceFound = EVIDENCE.filter((item) => state.evidence[item.id]).length;
    elements.evidenceCount.textContent = `${evidenceFound} / ${EVIDENCE.length}`;
    elements.evidenceList.innerHTML = "";
    EVIDENCE.forEach((item) => {
      const li = document.createElement("li");
      li.dataset.found = String(Boolean(state.evidence[item.id]));
      li.textContent = state.evidence[item.id] ? item.label : "Observation not recorded";
      elements.evidenceList.append(li);
    });

    const marksFound = STORY_MARKS.filter((mark) => state.marks[mark.id]).length;
    elements.marksCount.textContent = `${marksFound} / ${STORY_MARKS.length}`;
    elements.marksList.innerHTML = "";
    STORY_MARKS.forEach((mark) => {
      const li = document.createElement("li");
      li.dataset.found = String(Boolean(state.marks[mark.id]));
      li.textContent = mark.label;
      elements.marksList.append(li);
    });

    const davidTurns = state.transcripts.filter((turn) => turn.speaker === "David").length;
    elements.turnCount.textContent = `${davidTurns} turn${davidTurns === 1 ? "" : "s"}`;
    elements.transcriptList.innerHTML = "";
    if (!state.transcripts.length) {
      const empty = document.createElement("li");
      empty.className = "transcript-empty";
      empty.textContent = "No statement has been entered into the record.";
      elements.transcriptList.append(empty);
      return;
    }
    state.transcripts.slice(-16).forEach((turn, index) => {
      const li = document.createElement("li");
      li.className = "transcript-entry";
      li.dataset.speaker = turn.speaker;
      const meta = document.createElement("small");
      meta.textContent = `${turn.speaker} · ${turn.phase || "statement"} · ${String(index + 1).padStart(2, "0")}`;
      const text = document.createElement("p");
      text.textContent = turn.text;
      li.append(meta, text);
      if (turn.speaker === "Spassky") {
        const replay = document.createElement("button");
        replay.type = "button";
        replay.className = "transcript-replay";
        replay.dataset.action = "replay-officer";
        replay.dataset.replyText = turn.text;
        replay.textContent = "Replay voice";
        replay.setAttribute("aria-label", `Replay Spassky: ${turn.text}`);
        li.append(replay);
      }
      elements.transcriptList.append(li);
    });
  }

  function observeEvidence(action) {
    switch (action) {
      case "radio":
        state.evidence.storm_warning = true;
        announce("The storm warning has been recorded.");
        break;
      case "cups":
        state.evidence.five_cups = true;
        break;
      case "clock":
        state.evidence.clock = true;
        break;
      case "coat":
        state.evidence.thin_jacket = true;
        break;
      case "morning-door":
        state.evidence.door_locked = true;
        announce("The front door was locked from inside.");
        break;
      case "morning-key":
        state.evidence.key_inside = true;
        break;
      case "morning-window":
        state.evidence.grille_intact = true;
        break;
      case "morning-carry":
        state.evidence.ivy_alibi = true;
        state.evidence.aaron_move = true;
        state.evidence.affair = true;
        state.phaseFlags.bodyMoved = true;
        announce("Ivy's alibi and Aaron's instruction have been recorded.");
        break;
      default:
        return;
    }
    saveState();
    renderAll();
  }

  function applyCutsceneSource(cutsceneId, button) {
    const article = button.closest(".cutscene-frame");
    const input = article?.querySelector("input[type='url']");
    if (!input) return;
    const raw = input.value.trim();
    const validated = validateCutsceneUrl(raw);
    if (raw && !validated) {
      setError("That cutscene URL is not valid. Use HTTPS, localhost HTTP, or leave the field blank.");
      renderSceneError();
      return;
    }
    if (validated) state.cutsceneSources[cutsceneId] = validated;
    else delete state.cutsceneSources[cutsceneId];
    setError("");
    saveState();
    const iframe = article.querySelector("iframe");
    const empty = article.querySelector(".cutscene-frame__empty");
    if (iframe) iframe.src = validated || "about:blank";
    if (empty) empty.hidden = Boolean(validated);
    input.value = validated;
    button.textContent = validated ? "Update" : "Attach";
    renderSceneError();
    announce(validated ? `${cutsceneId} video attached.` : `${cutsceneId} returned to a blank iframe.`);
  }

  function validateCutsceneUrl(value) {
    if (!value) return "";
    try {
      const url = new URL(value, window.location.href);
      const localHttp = url.protocol === "http:" && ["localhost", "127.0.0.1", "[::1]"].includes(url.hostname);
      if (url.protocol !== "https:" && !localHttp) return "";
      return url.href;
    } catch (_error) {
      return "";
    }
  }

  async function ensureMicrophone() {
    if (micStream && micStream.active) {
      micReady = true;
      return micStream;
    }
    if (!navigator.mediaDevices?.getUserMedia) {
      throw new Error("This browser does not expose microphone capture. Open the page on HTTPS or localhost in a current browser.");
    }
    const stream = await navigator.mediaDevices.getUserMedia({
      audio: {
        channelCount: 1,
        echoCancellation: true,
        noiseSuppression: true,
        autoGainControl: false,
      },
      video: false,
    });
    micStream = stream;
    micReady = true;
    state.micConsent = true;
    setupLevelMeter(stream);
    saveState();
    return stream;
  }

  function getAudioContext() {
    if (!audioContext || audioContext.state === "closed") {
      const AudioContextClass = window.AudioContext || window.webkitAudioContext;
      if (!AudioContextClass) throw new Error("This browser cannot process microphone audio.");
      audioContext = new AudioContextClass();
    }
    if (audioContext.state === "suspended") audioContext.resume().catch(() => {});
    return audioContext;
  }

  function setupLevelMeter(stream) {
    const context = getAudioContext();
    if (micSource) {
      try { micSource.disconnect(); } catch (_error) { /* already disconnected */ }
    }
    analyser = context.createAnalyser();
    analyser.fftSize = 1024;
    analyser.smoothingTimeConstant = 0.5;
    meterBuffer = new Float32Array(analyser.fftSize);
    micSource = context.createMediaStreamSource(stream);
    micSource.connect(analyser);
    if (meterFrame) cancelAnimationFrame(meterFrame);
    updateLevelMeter();
  }

  function updateLevelMeter() {
    if (!analyser) return;
    if (!meterBuffer || meterBuffer.length !== analyser.fftSize) meterBuffer = new Float32Array(analyser.fftSize);
    analyser.getFloatTimeDomainData(meterBuffer);
    let sum = 0;
    for (const sample of meterBuffer) sum += sample * sample;
    const rms = Math.sqrt(sum / meterBuffer.length);
    const normalized = Math.min(1, rms * 8.5);
    const fill = document.querySelector("#level-fill");
    if (fill) fill.style.width = `${Math.max(1.5, normalized * 100)}%`;
    if (Array.isArray(calibrationSamples)) calibrationSamples.push(rms);
    if (mediaRecorder?.state === "recording") recordingPeak = Math.max(recordingPeak, rms);
    meterFrame = requestAnimationFrame(updateLevelMeter);
  }

  async function calibrateMicrophone() {
    const calibrationGeneration = experienceGeneration;
    isProcessing = true;
    setError("");
    renderAll();
    try {
      await ensureMicrophone();
      calibrationSamples = [];
      await new Promise((resolve) => window.setTimeout(resolve, 1800));
      if (calibrationGeneration !== experienceGeneration) return;
      const peak = Math.max(0.035, ...calibrationSamples.slice(-240));
      state.calibrationPeak = peak;
      state.micConsent = true;
      saveState();
      announce("Microphone calibrated. The officer can hear you.");
    } catch (error) {
      micReady = false;
      setError(describeMicrophoneError(error));
    } finally {
      calibrationSamples = null;
      isProcessing = false;
      renderAll();
    }
  }

  function describeMicrophoneError(error) {
    if (error?.name === "NotAllowedError") {
      return "Microphone permission was denied. Allow microphone access in the browser's site controls, then choose Enable microphone again.";
    }
    if (error?.name === "NotFoundError") {
      return "No microphone was found. Connect or enable an input device, then retry calibration.";
    }
    if (error?.name === "NotSupportedError" || /not supported/i.test(error?.message || "")) {
      return "Microphone capture is unavailable in this page context. Open the fallback through HTTPS or localhost in a current browser, then retry.";
    }
    return error?.message || "The microphone could not start. Check browser permissions and retry.";
  }

  function getSpeechRecognitionConstructor() {
    return window.SpeechRecognition || window.webkitSpeechRecognition || null;
  }

  async function toggleRecording(purpose) {
    if (mediaRecorder?.state === "recording") {
      await stopRecording();
      return;
    }
    await startRecording(purpose);
  }

  async function startRecording(purpose) {
    setError("");
    pendingTurn = null;
    try {
      await ensureMicrophone();
      if (typeof MediaRecorder === "undefined") {
        throw new Error("This browser cannot create a voice recording. Use a current Chrome, Edge, Firefox, or Safari release.");
      }
      const options = chooseRecorderOptions();
      mediaRecorder = options ? new MediaRecorder(micStream, options) : new MediaRecorder(micStream);
      const recorder = mediaRecorder;
      recordingChunks = [];
      const recorderChunks = recordingChunks;
      let recorderTimeout = 0;
      recordingStartedAt = performance.now();
      recordingPeak = 0;
      recordingPurpose = purpose;
      recognizedFinal = "";
      recognizedInterim = "";

      recorder.addEventListener("dataavailable", (event) => {
        if (event.data && event.data.size) recorderChunks.push(event.data);
      });
      recorder.addEventListener("error", () => {
        if (mediaRecorder !== recorder) return;
        failedRecorders.add(recorder);
        if (recorderTimeout) window.clearTimeout(recorderTimeout);
        if (recordingTimeout === recorderTimeout) recordingTimeout = 0;
        try { if (recorder.state === "recording") recorder.stop(); } catch (_error) { /* recorder already stopped */ }
        if (recognition) {
          try { recognition.stop(); } catch (_error) { /* recognition already stopped */ }
        }
        mediaRecorder = null;
        recognition = null;
        recordingPurpose = "";
        recordingChunks = [];
        setError("The browser stopped the recording unexpectedly. Check the microphone and try this answer again.");
        renderAll();
      });
      startSpeechRecognition();
      recorder.start(200);
      recorderTimeout = window.setTimeout(() => {
        if (mediaRecorder === recorder && recorder.state === "recording") {
          recordingTimeout = 0;
          announce("The 19 second answer limit was reached. Processing your recording.");
          stopRecording();
        }
      }, MAX_RECORDING_MS);
      recordingTimeout = recorderTimeout;
      announce("Recording started.");
      updateRecordingUi(true);
    } catch (error) {
      setError(describeMicrophoneError(error));
      renderAll();
    }
  }

  function chooseRecorderOptions() {
    const candidates = [
      "audio/webm;codecs=opus",
      "audio/ogg;codecs=opus",
      "audio/mp4",
      "audio/webm",
    ];
    const mimeType = candidates.find((candidate) => MediaRecorder.isTypeSupported?.(candidate));
    return mimeType ? { mimeType } : null;
  }

  function startSpeechRecognition() {
    const Recognition = getSpeechRecognitionConstructor();
    if (!Recognition) return;
    try {
      const activeRecognition = new Recognition();
      recognition = activeRecognition;
      activeRecognition.lang = "en-US";
      activeRecognition.continuous = true;
      activeRecognition.interimResults = true;
      activeRecognition.maxAlternatives = 1;
      activeRecognition.addEventListener("result", (event) => {
        if (recognition !== activeRecognition) return;
        recognizedInterim = "";
        for (let index = event.resultIndex; index < event.results.length; index += 1) {
          const result = event.results[index];
          const text = result[0]?.transcript || "";
          if (result.isFinal) recognizedFinal += `${text} `;
          else recognizedInterim += text;
        }
        const preview = document.querySelector("#recognition-preview");
        if (preview) preview.textContent = (recognizedFinal + recognizedInterim).trim() || "Listening…";
      });
      activeRecognition.addEventListener("error", () => {
        if (recognition === activeRecognition) recognition = null;
      });
      activeRecognition.start();
    } catch (_error) {
      recognition = null;
    }
  }

  function stopRecording() {
    if (!mediaRecorder || mediaRecorder.state !== "recording") return Promise.resolve();
    const recorder = mediaRecorder;
    const recorderChunks = recordingChunks;
    const purpose = recordingPurpose;
    if (recordingTimeout) {
      window.clearTimeout(recordingTimeout);
      recordingTimeout = 0;
    }
    isProcessing = true;
    updateRecordingUi(false, true);
    const duration = performance.now() - recordingStartedAt;
    const recordingGeneration = experienceGeneration;
    const recordingSessionId = state.sessionId;
    const recognitionFinished = finishSpeechRecognition();
    return new Promise((resolve) => {
      recorder.addEventListener("stop", async () => {
        const mimeType = recorder.mimeType || recorderChunks[0]?.type || "audio/webm";
        const blob = new Blob(recorderChunks, { type: mimeType });
        try {
          await recognitionFinished;
          if (failedRecorders.has(recorder)) return;
          if (duration < MIN_RECORDING_MS || !blob.size) {
            throw new Error("That answer was too short to hear. Speak for at least one second, then finish the recording.");
          }
          const pcm = await recordedBlobToPcm16(blob);
          const repair = document.querySelector("#transcript-repair")?.value?.trim() || "";
          const browserTranscript = repair || recognizedFinal.trim() || recognizedInterim.trim();
          if (recordingGeneration !== experienceGeneration || recordingSessionId !== state.sessionId) return;
          await processVoiceTurn({ pcm, purpose, browserTranscript, duration, peak: recordingPeak });
        } catch (error) {
          setError(error?.message || "The recording could not be processed. Try the answer again.");
        } finally {
          isProcessing = false;
          if (mediaRecorder === recorder) mediaRecorder = null;
          recognition = null;
          recordingPurpose = "";
          recordingChunks = [];
          renderAll();
          resolve();
        }
      }, { once: true });
      recorder.stop();
    });
  }

  function finishSpeechRecognition() {
    const activeRecognition = recognition;
    if (!activeRecognition) return Promise.resolve();
    return new Promise((resolve) => {
      let finished = false;
      const finish = () => {
        if (finished) return;
        finished = true;
        resolve();
      };
      activeRecognition.addEventListener("end", finish, { once: true });
      window.setTimeout(finish, 400);
      try { activeRecognition.stop(); } catch (_error) { finish(); }
    });
  }

  function updateRecordingUi(recording, processing = false) {
    const button = document.querySelector("[data-action='record']");
    const micState = document.querySelector("#mic-state");
    if (button) {
      button.dataset.recording = String(recording);
      button.textContent = processing ? "Processing…" : recording ? "Finish answer" : "Begin speaking";
      button.disabled = processing;
    }
    if (micState) {
      micState.dataset.state = recording ? "recording" : micReady ? "ready" : "idle";
      micState.textContent = recording ? "Listening" : processing ? "Processing" : "Microphone ready";
    }
  }

  async function recordedBlobToPcm16(blob) {
    const context = getAudioContext();
    let decoded;
    try {
      decoded = await context.decodeAudioData(await blob.arrayBuffer());
    } catch (_error) {
      throw new Error("The browser recorded audio in a format it could not decode. Try a current Chromium or Firefox browser.");
    }
    const mono = mixToMono(decoded);
    const resampled = resampleLinear(mono, decoded.sampleRate, TARGET_SAMPLE_RATE);
    const pcm = new ArrayBuffer(resampled.length * 2);
    const view = new DataView(pcm);
    for (let index = 0; index < resampled.length; index += 1) {
      const sample = Math.max(-1, Math.min(1, resampled[index]));
      view.setInt16(index * 2, sample < 0 ? sample * 32768 : sample * 32767, true);
    }
    return pcm;
  }

  function mixToMono(audioBuffer) {
    if (audioBuffer.numberOfChannels === 1) return audioBuffer.getChannelData(0).slice();
    const mono = new Float32Array(audioBuffer.length);
    for (let channel = 0; channel < audioBuffer.numberOfChannels; channel += 1) {
      const data = audioBuffer.getChannelData(channel);
      for (let index = 0; index < data.length; index += 1) mono[index] += data[index] / audioBuffer.numberOfChannels;
    }
    return mono;
  }

  function resampleLinear(samples, sourceRate, targetRate) {
    if (sourceRate === targetRate) return samples;
    const targetLength = Math.max(1, Math.round(samples.length * targetRate / sourceRate));
    const output = new Float32Array(targetLength);
    const ratio = sourceRate / targetRate;
    for (let index = 0; index < targetLength; index += 1) {
      const position = index * ratio;
      const left = Math.floor(position);
      const right = Math.min(samples.length - 1, left + 1);
      const blend = position - left;
      output[index] = samples[left] * (1 - blend) + samples[right] * blend;
    }
    return output;
  }

  async function processVoiceTurn(turn) {
    setError("");
    if (turn.purpose === "p1") {
      const text = turn.browserTranscript || "[Voice captured; browser transcript unavailable]";
      addTranscript("David", text, "p1");
      addTranscript("Spassky", "I'm Officer Spassky. Nick is dead, and right now you're one of the suspects. I've already spoken to the others. Take your time and tell me everything you remember from last night.", "p1");
      state.phaseFlags.p1Answered = true;
      saveState();
      announce("The officer heard you.");
      return;
    }

    if (turn.purpose === "m1-call") {
      const threshold = Math.max(0.045, state.calibrationPeak * 1.18);
      if (turn.peak < threshold) {
        throw new Error("The storm took that call. Speak louder than your calibration level and try again.");
      }
      const text = turn.browserTranscript || "[David calls for Nick; transcript unavailable]";
      addTranscript("David", text, "m1");
      state.phaseFlags.doorCalled = true;
      saveState();
      announce("Your call disappears into the storm. Nothing answers.");
      return;
    }

    if (!turn.browserTranscript && state.mode === "offline") {
      throw new Error("Offline mode needs a transcript to choose the scripted reply. Type a correction in the transcript box, then speak the answer again.");
    }

    pendingTurn = turn;
    if (state.mode === "live") {
      await processLiveTurn(turn);
    } else {
      processOfflineTurn(turn);
    }
  }

  async function processLiveTurn(turn) {
    const turnGeneration = experienceGeneration;
    const turnSessionId = state.sessionId;
    const controller = new AbortController();
    activeTurnController = controller;
    try {
      const response = await sendSidecarTurn(turn, controller.signal);
      if (turnGeneration !== experienceGeneration || turnSessionId !== state.sessionId) return;
      const transcript = (response.transcript || turn.browserTranscript || "").trim();
      if (response.session_ended) {
        if (transcript) addTranscript("David", transcript, turn.purpose);
        const closingReply = response.reply_text || "We're done for tonight. The station will follow up.";
        addTranscript("Spassky", closingReply, turn.purpose);
        completeInterrogationTurn(turn.purpose, transcript);
        state.mode = "offline";
        officerSpeech?.speak(closingReply);
        pendingTurn = null;
        saveState();
        setError("The live AI session reached its turn limit. Spassky's closing line is in the interview log, and Offline scripted mode is now active so the story can continue.");
        announce("The live session ended. Offline scripted mode is active.");
        return;
      }
      if (!transcript) {
        throw new Error("The backend received the recording but could not transcribe speech. Check the mic level and try again.");
      }
      addTranscript("David", transcript, turn.purpose);
      const reply = response.reply_text || "[No officer reply text returned]";
      addTranscript("Spassky", reply, turn.purpose);
      const pcmPlayed = response.audio_b64
        ? await playPcmReply(response.audio_b64, response.audio_sample_rate || 24000)
        : false;
      if (!pcmPlayed) officerSpeech?.speak(reply);
      completeInterrogationTurn(turn.purpose, transcript);
      pendingTurn = null;
      announce(`Spassky replied in ${response.total_ms || "an unknown number of"} milliseconds.`);
    } catch (error) {
      if (error?.name === "AbortError") return;
      const message = describeBackendError(error);
      setError(message);
      throw new Error(message);
    } finally {
      if (activeTurnController === controller) activeTurnController = null;
    }
  }

  function processOfflineTurn(turn) {
    const transcript = turn.browserTranscript.trim();
    addTranscript("David", transcript, turn.purpose);
    if (turn.purpose === "p2") {
      const index = state.transcripts.filter((entry) => entry.scene === "p2" && entry.speaker === "Spassky").length;
      addTranscript("Spassky", OFFLINE_REPLIES[index % OFFLINE_REPLIES.length], "p2");
    } else if (turn.purpose === "p3") {
      const accusation = detectAccusation(transcript);
      if (!accusation) {
        addTranscript("Spassky", window.FalsePositiveSpeech?.getOfflineVerdictReply("") || "Who, David?", "p3");
        setError("No single accusation was heard. Name exactly one of Aaron, Ivy, or Priya in your next answer.");
        pendingTurn = null;
        saveState();
        return;
      }
      state.accusation = accusation;
      addTranscript("Spassky", window.FalsePositiveSpeech?.getOfflineVerdictReply(accusation) || `So you think it's ${capitalize(accusation)}.`, "p3");
    }
    completeInterrogationTurn(turn.purpose, transcript);
    pendingTurn = null;
  }

  function completeInterrogationTurn(purpose, transcript) {
    updateStoryMarks(transcript);
    if (purpose === "p3") {
      const accusation = state.accusation || detectAccusation(transcript);
      if (accusation) {
        state.accusation = accusation;
        state.ending = selectEnding(accusation, transcript);
      }
    }
    saveState();
  }

  function addTranscript(speaker, text, scene) {
    const clean = String(text || "").trim().slice(0, 1400);
    if (!clean) return;
    const entry = { speaker, text: clean, scene, phase: SCENES.find((item) => item.id === scene)?.phase || scene };
    if (speaker === "David") {
      entry.turnNumber = state.transcripts.reduce((highest, turn) => {
        if (turn.speaker !== "David") return highest;
        return Math.max(highest, Number(turn.turnNumber) || highest + 1);
      }, 0) + 1;
    }
    state.transcripts.push(entry);
    state.transcripts = state.transcripts.slice(-80);
    if (speaker === "Spassky" && state.mode === "offline") officerSpeech?.speak(clean);
  }

  function updateStoryMarks(transcript) {
    const normalized = transcript.toLowerCase();
    STORY_MARKS.forEach((mark) => {
      if (mark.words.some((word) => normalized.includes(word))) state.marks[mark.id] = true;
    });
  }

  function detectAccusation(transcript) {
    const normalized = transcript.toLowerCase();
    const suspect = "(?:aaron|ivy|priya)";
    const names = ["aaron", "ivy", "priya"].filter((name) => new RegExp(`\\b${name}\\b`, "i").test(normalized));
    if (names.length > 1 && /\b(?:or|maybe|possibly|perhaps)\b/i.test(normalized)) return "";
    const ambiguousPair = new RegExp(`\\b${suspect}\\b\\s*(?:,\\s*|(?:and|and\\/or|or)(?:\\s+maybe)?\\s+|(?:maybe|possibly|perhaps)\\s+)\\b${suspect}\\b`, "i");
    if (ambiguousPair.test(normalized)) return "";
    const accusationPatterns = [
      /\b(?:it was|it is|it's|i accuse|i blame|i suspect|i think it was|i believe it was)\s+(aaron|ivy|priya)\b/i,
      new RegExp(`\\b(${suspect})\\b(?:(?!\\b${suspect}\\b)[^.!?]){0,80}\\b(?:did it|killed|murdered|is guilty|was responsible)\\b`, "i"),
    ];
    for (const pattern of accusationPatterns) {
      const match = normalized.match(pattern);
      if (match) return match[1];
    }
    return names.length === 1 ? names[0] : "";
  }

  function selectEnding(accusation, transcript) {
    const marks = STORY_MARKS.filter((mark) => state.marks[mark.id]).length;
    const caughtReasoning = ["door", "key", "window", "grille", "jacket", "coat", "affair", "alibi", "body", "locked"]
      .filter((word) => transcript.toLowerCase().includes(word)).length;
    if (accusation === "aaron") {
      return marks >= 3 && caughtReasoning >= 2 ? "E_AARON" : "E_DAVID";
    }
    if (marks < 2) return "E_DAVID";
    if (accusation === "ivy") return "E_IVY";
    if (accusation === "priya") return "E_PRIYA";
    return "E_DAVID";
  }

  async function sendSidecarTurn(turn, signal) {
    const base = resolveBackendBase();
    const key = sessionStorage.getItem(SESSION_KEY) || "";
    if (!key) throw new Error("unauthorized: no client key is configured for this tab");
    const form = new FormData();
    form.append("session_id", state.sessionId);
    form.append("sample_rate", String(TARGET_SAMPLE_RATE));
    form.append("onset_delay_ms", "0");
    form.append("scene_instruction", SCENE_INSTRUCTIONS[turn.purpose] || "");
    form.append("audio", new Blob([turn.pcm], { type: "application/octet-stream" }), "utterance.pcm");

    let response;
    try {
      response = await fetch(`${base}/turn`, {
        method: "POST",
        headers: { "X-FP-Client-Key": key },
        body: form,
        signal,
      });
    } catch (error) {
      if (error?.name === "AbortError") throw error;
      throw new Error(`network: ${error?.message || "request failed"}`);
    }
    let payload;
    try {
      payload = await response.json();
    } catch (_error) {
      throw new Error(`http ${response.status}: the backend returned an unreadable response`);
    }
    if (payload.session_ended) return payload;
    if (!response.ok || !payload.ok) {
      throw new Error(payload.error || `http ${response.status}`);
    }
    return payload;
  }

  function resolveBackendBase() {
    const configured = (state.backendUrl || "").trim().replace(/\/$/, "");
    return configured || window.location.origin;
  }

  function describeBackendError(error) {
    const message = String(error?.message || error || "");
    if (/unauthorized|401/i.test(message)) {
      return "The backend rejected the client key. Open Setup, enter the current X-FP-Client-Key, and retry the saved recording.";
    }
    if (/timed out|timeout|504/i.test(message)) {
      return "The interrogation turn timed out. The saved recording can be retried, or switch to Offline scripted mode to keep the story moving.";
    }
    if (/network|failed to fetch|cors/i.test(message)) {
      return "The browser could not reach the Sidecar. Check the backend URL and HTTPS/CORS setup, then retry—or switch to Offline scripted mode.";
    }
    return `The Sidecar could not finish the turn: ${message || "unknown error"}. Retry the saved recording or use Offline scripted mode.`;
  }

  async function playPcmReply(audioB64, sampleRate) {
    try {
      const binary = atob(audioB64);
      const samples = Math.floor(binary.length / 2);
      const pcm = new Float32Array(samples);
      for (let index = 0; index < samples; index += 1) {
        const low = binary.charCodeAt(index * 2);
        const high = binary.charCodeAt(index * 2 + 1);
        let value = (high << 8) | low;
        if (value >= 0x8000) value -= 0x10000;
        pcm[index] = value / 32768;
      }
      const context = getAudioContext();
      const buffer = context.createBuffer(1, pcm.length, sampleRate);
      buffer.copyToChannel(pcm, 0);
      const source = context.createBufferSource();
      source.buffer = buffer;
      source.connect(context.destination);
      officerSpeech?.stop();
      source.start();
      return true;
    } catch (_error) {
      return false;
    }
  }

  async function retryPendingTurn() {
    if (!pendingTurn) return;
    isProcessing = true;
    setError("");
    renderAll();
    try {
      if (state.mode === "live") await processLiveTurn(pendingTurn);
      else processOfflineTurn(pendingTurn);
    } catch (_error) {
      // The detailed recoverable error is already shown by processLiveTurn.
    } finally {
      isProcessing = false;
      renderAll();
    }
  }

  async function testBackendConnection() {
    elements.connectionResult.textContent = "Checking…";
    try {
      const configured = elements.backendUrl.value.trim().replace(/\/$/, "");
      const response = await fetch(`${configured || window.location.origin}/health`, { headers: { Accept: "application/json" } });
      if (!response.ok) throw new Error(`HTTP ${response.status}`);
      const body = await response.json();
      elements.connectionResult.textContent = body.models_loaded
        ? "Ready. Models are loaded."
        : "Reachable. Models are still loading.";
    } catch (error) {
      elements.connectionResult.textContent = `Not reachable: ${error?.message || "network error"}.`;
    }
  }

  function switchOffline() {
    cancelActiveTurn();
    state.mode = "offline";
    pendingTurn = null;
    setError("");
    saveState();
    renderAll();
    announce("Offline scripted interrogation enabled.");
  }

  function advanceTo(index) {
    const nextIndex = Number(index);
    if (!Number.isInteger(nextIndex) || nextIndex < 0 || nextIndex >= SCENES.length) return;
    if (nextIndex > state.maxUnlocked + 1) return;
    if (nextIndex !== state.sceneIndex) cancelActiveTurn();
    state.sceneIndex = nextIndex;
    state.maxUnlocked = Math.max(state.maxUnlocked, nextIndex);
    setError("");
    pendingTurn = null;
    const nextScene = SCENES[nextIndex];
    const opening = state.mode === "offline"
      ? window.FalsePositiveSpeech?.getOfflineSceneOpening(nextScene.id, state.ending)
      : "";
    const openingAlreadyLogged = state.transcripts.some((turn) => turn.speaker === "Spassky" && turn.scene === nextScene.id);
    if (opening && !openingAlreadyLogged) addTranscript("Spassky", opening, nextScene.id);
    saveState();
    renderAll({ focusScene: true });
    announce(`${SCENES[nextIndex].phase} scene opened.`);
  }

  async function resetExperience() {
    const oldSession = state.sessionId;
    const backendBase = resolveBackendBase();
    const clientKey = sessionStorage.getItem(SESSION_KEY) || "";
    cancelActiveTurn();
    stopMicrophone();
    localStorage.removeItem(STORAGE_KEY);
    sessionStorage.removeItem(TRANSCRIPT_SESSION_KEY);
    state = { ...DEFAULT_STATE, sessionId: createSessionId(), evidence: {}, marks: {}, transcripts: [], cutsceneSources: {}, phaseFlags: {} };
    pendingTurn = null;
    setError("");
    saveState();
    renderAll({ focusScene: true });
    announce("Local case progress cleared.");

    if (clientKey && oldSession) {
      const form = new FormData();
      form.append("session_id", oldSession);
      fetch(`${backendBase}/session/reset`, {
        method: "POST",
        headers: { "X-FP-Client-Key": clientKey },
        body: form,
      }).catch(() => {});
    }
  }

  function stopMicrophone() {
    if (recordingTimeout) {
      window.clearTimeout(recordingTimeout);
      recordingTimeout = 0;
    }
    if (mediaRecorder?.state === "recording") {
      try { mediaRecorder.stop(); } catch (_error) { /* already stopping */ }
    }
    if (recognition) {
      try { recognition.stop(); } catch (_error) { /* already stopped */ }
    }
    if (meterFrame) cancelAnimationFrame(meterFrame);
    micStream?.getTracks().forEach((track) => track.stop());
    micStream = null;
    micReady = false;
    analyser = null;
    meterBuffer = null;
    micSource = null;
    mediaRecorder = null;
    recognition = null;
    recordingPurpose = "";
    recordingChunks = [];
  }

  function cancelActiveTurn() {
    experienceGeneration += 1;
    activeTurnController?.abort();
    activeTurnController = null;
    if (recordingTimeout) {
      window.clearTimeout(recordingTimeout);
      recordingTimeout = 0;
    }
    if (mediaRecorder?.state === "recording") {
      try { mediaRecorder.stop(); } catch (_error) { /* already stopping */ }
    }
    if (recognition) {
      try { recognition.stop(); } catch (_error) { /* already stopped */ }
    }
    mediaRecorder = null;
    recognition = null;
    recordingPurpose = "";
    recordingChunks = [];
  }

  function capitalize(value) {
    return value ? value[0].toUpperCase() + value.slice(1) : "";
  }

  function openSettings() {
    elements.backendUrl.value = state.backendUrl || "";
    elements.backendKey.value = sessionStorage.getItem(SESSION_KEY) || "";
    elements.captionsToggle.checked = state.captions;
    const radio = elements.settingsForm.querySelector(`input[name='experience-mode'][value='${state.mode}']`);
    if (radio) radio.checked = true;
    updateLiveSettingsVisibility();
    elements.connectionResult.textContent = "";
    elements.settingsDialog.showModal();
  }

  function saveSettings() {
    cancelActiveTurn();
    const mode = elements.settingsForm.querySelector("input[name='experience-mode']:checked")?.value;
    state.mode = mode === "live" ? "live" : "offline";
    state.backendUrl = elements.backendUrl.value.trim().replace(/\/$/, "");
    state.captions = elements.captionsToggle.checked;
    const key = elements.backendKey.value.trim();
    if (key) sessionStorage.setItem(SESSION_KEY, key);
    else sessionStorage.removeItem(SESSION_KEY);
    pendingTurn = null;
    setError("");
    saveState();
    elements.settingsDialog.close();
    renderAll();
    announce(`${state.mode === "live" ? "Live AI" : "Offline scripted"} mode selected.`);
  }

  function updateLiveSettingsVisibility() {
    const mode = elements.settingsForm.querySelector("input[name='experience-mode']:checked")?.value;
    elements.liveSettings.dataset.enabled = String(mode === "live");
  }

  elements.sceneContent.addEventListener("click", async (event) => {
    const button = event.target.closest("button[data-action]");
    if (!button) return;
    const action = button.dataset.action;
    if (action === "enable-mic") await calibrateMicrophone();
    else if (action === "enter-story") advanceTo(1);
    else if (action === "advance") advanceTo(button.dataset.nextIndex);
    else if (action === "open-phase") advanceTo(button.dataset.sceneIndex);
    else if (action === "observe-evidence") observeEvidence(button.dataset.evidenceAction);
    else if (action === "record") await toggleRecording(button.dataset.purpose);
    else if (action === "apply-cutscene") applyCutsceneSource(button.dataset.cutsceneId, button);
    else if (action === "finish-recall") continueIntoMorning();
    else if (action === "continue-ending") advanceTo(6);
    else if (action === "retry-pending") await retryPendingTurn();
    else if (action === "switch-offline") switchOffline();
    else if (action === "request-reset") elements.resetDialog.showModal();
  });

  elements.phaseList.addEventListener("click", (event) => {
    const button = event.target.closest("button[data-action='open-phase']");
    if (button) advanceTo(button.dataset.sceneIndex);
  });

  elements.transcriptList.addEventListener("click", (event) => {
    const button = event.target.closest("button[data-action='replay-officer']");
    if (!button) return;
    if (!officerSpeech?.speak(button.dataset.replyText || "")) {
      announce("Browser voice output is unavailable. The officer's reply remains visible.");
    }
  });

  elements.settingsButton.addEventListener("click", openSettings);
  elements.settingsForm.addEventListener("change", (event) => {
    if (event.target.name === "experience-mode") updateLiveSettingsVisibility();
  });
  elements.settingsForm.addEventListener("submit", (event) => {
    event.preventDefault();
    if (event.submitter?.value === "cancel") elements.settingsDialog.close();
    else saveSettings();
  });
  elements.connectionTest.addEventListener("click", testBackendConnection);
  elements.resetButton.addEventListener("click", () => elements.resetDialog.showModal());
  elements.confirmReset.addEventListener("click", resetExperience);
  elements.notebookToggle.addEventListener("click", () => {
    const expanded = elements.notebookToggle.getAttribute("aria-expanded") === "true";
    elements.notebookToggle.setAttribute("aria-expanded", String(!expanded));
    elements.notebookToggle.textContent = expanded ? "Case notes" : "Close";
    elements.notebookToggle.setAttribute("aria-label", expanded ? "Open case notes" : "Close case notes");
    elements.notebookBody.hidden = expanded;
  });
  window.addEventListener("beforeunload", () => {
    officerSpeech?.stop();
    stopMicrophone();
  });

  renderAll();
})();
