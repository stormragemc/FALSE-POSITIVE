import assert from "node:assert/strict";
import { createRequire } from "node:module";
import { test } from "node:test";
import { fileURLToPath } from "node:url";
import fs from "node:fs";
import path from "node:path";

const require = createRequire(import.meta.url);
const speechPath = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "../web/speech.js");

let speechModule = {};
try {
  speechModule = require(speechPath);
} catch (error) {
  if (error?.code !== "MODULE_NOT_FOUND") throw error;
}
if (typeof speechModule.createOfficerSpeech !== "function" && globalThis.FalsePositiveSpeech) {
  speechModule = globalThis.FalsePositiveSpeech;
}

test("an officer reply is sent to browser speech without overlapping the previous reply", () => {
  assert.equal(
    typeof speechModule.createOfficerSpeech,
    "function",
    "the browser speech controller is missing",
  );

  const calls = [];
  class FakeUtterance {
    constructor(text) {
      this.text = text;
    }
  }
  const englishVoice = { name: "Daniel", lang: "en-GB" };
  const browser = {
    SpeechSynthesisUtterance: FakeUtterance,
    speechSynthesis: {
      cancel() { calls.push(["cancel"]); },
      getVoices() { return [{ name: "Amelie", lang: "fr-FR" }, englishVoice]; },
      speak(utterance) { calls.push(["speak", utterance]); },
    },
  };

  const officerSpeech = speechModule.createOfficerSpeech(browser);
  assert.equal(officerSpeech.speak("  The door, David.  "), true);
  assert.equal(calls[0][0], "cancel");
  assert.equal(calls[1][0], "speak");
  assert.equal(calls[1][1].text, "The door, David.");
  assert.equal(calls[1][1].voice, englishVoice);
  assert.equal(calls[1][1].lang, "en-GB");
  assert.equal(calls[1][1].volume, 1);
});

test("a generated officer recording is preferred over browser speech", () => {
  const calls = [];
  class FakeAudio {
    constructor(source) {
      this.src = source;
      calls.push(["audio", source]);
    }

    play() {
      calls.push(["play", this.src]);
      return Promise.resolve();
    }

    pause() {
      calls.push(["pause", this.src]);
    }
  }
  class FakeUtterance {
    constructor(text) {
      this.text = text;
    }
  }
  const browser = {
    Audio: FakeAudio,
    SpeechSynthesisUtterance: FakeUtterance,
    speechSynthesis: {
      cancel() { calls.push(["cancel-synthesis"]); },
      getVoices() { return []; },
      speak(utterance) { calls.push(["speak", utterance.text]); },
    },
  };

  const officerSpeech = speechModule.createOfficerSpeech(browser);
  assert.equal(
    officerSpeech.speak("So. What's the last thing you remember?", "./audio/spassky/SPASSKY-006.wav"),
    true,
  );
  assert.deepEqual(calls, [
    ["cancel-synthesis"],
    ["audio", "./audio/spassky/SPASSKY-006.wav"],
    ["play", "./audio/spassky/SPASSKY-006.wav"],
  ]);
});

test("canonical offline lines resolve to their reviewed Spassky recordings", () => {
  assert.equal(
    speechModule.resolveOfficerRecording("So. What's the last thing you remember?"),
    "./audio/spassky/SPASSKY-006.wav",
  );
  assert.equal(
    speechModule.resolveOfficerRecording("So you think it's Priya, huh? Tell me why."),
    "./audio/spassky/SPASSKY-046.wav",
  );
  assert.equal(speechModule.resolveOfficerRecording("An unscripted live reply."), "");
});

test("the offline interrogation only chooses lines with generated recordings", () => {
  assert.ok(speechModule.offlineRecallLines.length >= 6);
  speechModule.offlineRecallLines.forEach((line) => {
    assert.match(speechModule.resolveOfficerRecording(line), /SPASSKY-\d{3}\.wav$/);
  });

  const priyaReply = speechModule.getOfflineVerdictReply("priya");
  assert.equal(priyaReply, "So you think it's Priya, huh? Tell me why.");
  assert.match(speechModule.resolveOfficerRecording(priyaReply), /SPASSKY-046\.wav$/);
  assert.equal(speechModule.getOfflineVerdictReply(""), "Who, David?");
});

test("offline scene openings resolve to the generated voice used by the game", () => {
  const recallOpening = speechModule.getOfflineSceneOpening("p2");
  assert.equal(recallOpening, "So. What's the last thing you remember?");
  assert.match(speechModule.resolveOfficerRecording(recallOpening), /SPASSKY-006\.wav$/);

  const ending = speechModule.getOfflineSceneOpening("p4", "E_IVY");
  assert.equal(ending, "She agreed with you. That's not the same as it being true.");
  assert.match(speechModule.resolveOfficerRecording(ending), /SPASSKY-055\.wav$/);
  assert.equal(speechModule.getOfflineSceneOpening("m2"), "");
});

test("every offline generated recording ships with the fallback site", () => {
  const lines = [
    "I'm Officer Spassky. Nick is dead, and right now you're one of the suspects. I've already spoken to the others. Take your time and tell me everything you remember from last night.",
    ...speechModule.offlineRecallLines,
    speechModule.getOfflineVerdictReply(""),
    speechModule.getOfflineVerdictReply("aaron"),
    speechModule.getOfflineVerdictReply("ivy"),
    speechModule.getOfflineVerdictReply("priya"),
    speechModule.getOfflineSceneOpening("p3"),
    speechModule.getOfflineSceneOpening("p4", "E_DAVID"),
    speechModule.getOfflineSceneOpening("p4", "E_AARON"),
    speechModule.getOfflineSceneOpening("p4", "E_IVY"),
    speechModule.getOfflineSceneOpening("p4", "E_PRIYA"),
  ];

  lines.forEach((line) => {
    const source = speechModule.resolveOfficerRecording(line);
    assert.ok(source, `no recording maps to: ${line}`);
    assert.equal(
      fs.existsSync(path.resolve(path.dirname(speechPath), source)),
      true,
      `missing shipped recording: ${source}`,
    );
  });
});

test("a failed generated recording falls back to browser speech only once", async () => {
  const spoken = [];
  let audio = null;
  class FailingAudio {
    constructor() {
      audio = this;
    }

    play() {
      return Promise.reject(new Error("decode failed"));
    }

    pause() {}
  }
  class FakeUtterance {
    constructor(text) {
      this.text = text;
    }
  }
  const browser = {
    Audio: FailingAudio,
    SpeechSynthesisUtterance: FakeUtterance,
    speechSynthesis: {
      cancel() {},
      getVoices() { return []; },
      speak(utterance) { spoken.push(utterance.text); },
    },
  };

  const officerSpeech = speechModule.createOfficerSpeech(browser);
  officerSpeech.speak("What happened to Nick?", "./audio/spassky/SPASSKY-024.wav");
  audio.onerror();
  await Promise.resolve();
  await Promise.resolve();

  assert.deepEqual(spoken, ["What happened to Nick?"]);
});
