(function installFalsePositiveSpeech(root, factory) {
  "use strict";

  const api = factory();
  if (typeof module === "object" && module.exports) module.exports = api;
  if (root) root.FalsePositiveSpeech = api;
})(typeof window === "object" ? window : globalThis, function createSpeechModule() {
  "use strict";

  const PREFERRED_VOICE = /\b(?:alex|aaron|arthur|daniel|eddy|fred|reed|rishi|rocko)\b/i;
  const OFFICER_RECORDINGS = new Map([
    ["I'm Officer Spassky. Nick is dead, and right now you're one of the suspects. I've already spoken to the others. Take your time and tell me everything you remember from last night.", "SPASSKY-005"],
    ["So. What's the last thing you remember?", "SPASSKY-006"],
    ["Who else was drinking with you?", "SPASSKY-007"],
    ["How much did you have?", "SPASSKY-008"],
    ["Tell me about the argument with Nick. What was it really about?", "SPASSKY-009"],
    ["What did Nick do after the argument?", "SPASSKY-011"],
    ["What did you do next?", "SPASSKY-013"],
    ["Was the door locked when you opened it?", "SPASSKY-015"],
    ["Walk me through the morning. Start with Priya's scream.", "SPASSKY-020"],
    ["What happened to Nick?", "SPASSKY-024"],
    ["Tell me why I should spare your life.", "SPASSKY-037"],
    ["So you think it's Aaron, huh? Tell me why.", "SPASSKY-040"],
    ["So you think it's Ivy, huh? Tell me why.", "SPASSKY-043"],
    ["So you think it's Priya, huh? Tell me why.", "SPASSKY-046"],
    ["You were the only one who couldn't tell me where you were.", "SPASSKY-053"],
    ["He locked it. You unlocked it. Only one of those was a decision.", "SPASSKY-054"],
    ["She agreed with you. That's not the same as it being true.", "SPASSKY-055"],
    ["She's the one who called us. Sit with that.", "SPASSKY-056"],
    ["Who, David?", "SPASSKY-062"],
  ]);
  const offlineRecallLines = Object.freeze([
    "So. What's the last thing you remember?",
    "Who else was drinking with you?",
    "How much did you have?",
    "Tell me about the argument with Nick. What was it really about?",
    "What did Nick do after the argument?",
    "What did you do next?",
    "Was the door locked when you opened it?",
    "Walk me through the morning. Start with Priya's scream.",
    "What happened to Nick?",
  ]);

  function getOfflineVerdictReply(accusation) {
    const suspect = String(accusation || "").trim().toLowerCase();
    const names = { aaron: "Aaron", ivy: "Ivy", priya: "Priya" };
    return names[suspect]
      ? `So you think it's ${names[suspect]}, huh? Tell me why.`
      : "Who, David?";
  }

  function getOfflineSceneOpening(scene, ending = "") {
    if (scene === "p2") return offlineRecallLines[0];
    if (scene === "p3") return "Tell me why I should spare your life.";
    if (scene !== "p4") return "";
    return {
      E_DAVID: "You were the only one who couldn't tell me where you were.",
      E_AARON: "He locked it. You unlocked it. Only one of those was a decision.",
      E_IVY: "She agreed with you. That's not the same as it being true.",
      E_PRIYA: "She's the one who called us. Sit with that.",
    }[ending] || "";
  }

  function resolveOfficerRecording(text) {
    const id = OFFICER_RECORDINGS.get(String(text || "").trim());
    return id ? `./audio/spassky/${id}.wav` : "";
  }

  function createOfficerSpeech(browser) {
    const synthesis = browser?.speechSynthesis;
    const Utterance = browser?.SpeechSynthesisUtterance;
    const AudioPlayer = browser?.Audio;
    let activeAudio = null;

    function stop() {
      synthesis?.cancel?.();
      if (activeAudio) {
        activeAudio.pause?.();
        activeAudio = null;
      }
    }

    function speakWithBrowserVoice(text) {
      const clean = String(text || "").trim();
      if (!clean || !synthesis?.speak || !Utterance) return false;

      try {
        const utterance = new Utterance(clean);
        const voices = synthesis.getVoices?.() || [];
        const englishVoices = voices.filter((voice) => /^en(?:-|$)/i.test(voice.lang || ""));
        const voice = englishVoices.find((candidate) => PREFERRED_VOICE.test(candidate.name || ""))
          || englishVoices[0]
          || null;
        if (voice) utterance.voice = voice;
        utterance.lang = voice?.lang || "en-GB";
        utterance.rate = 0.86;
        utterance.pitch = 0.78;
        utterance.volume = 1;
        stop();
        synthesis.speak(utterance);
        return true;
      } catch (_error) {
        return false;
      }
    }

    function speak(text, recordingSource = "") {
      const clean = String(text || "").trim();
      const source = String(recordingSource || resolveOfficerRecording(clean)).trim();
      if (!clean) return false;
      if (!source || !AudioPlayer) return speakWithBrowserVoice(clean);

      try {
        stop();
        const audio = new AudioPlayer(source);
        activeAudio = audio;
        audio.preload = "auto";
        let fallbackStarted = false;
        const fallbackToBrowserVoice = () => {
          if (fallbackStarted) return;
          fallbackStarted = true;
          if (activeAudio === audio) activeAudio = null;
          speakWithBrowserVoice(clean);
        };
        audio.onerror = fallbackToBrowserVoice;
        audio.onended = () => {
          if (activeAudio === audio) activeAudio = null;
        };
        const playback = audio.play();
        playback?.catch?.(fallbackToBrowserVoice);
        return true;
      } catch (_error) {
        activeAudio = null;
        return speakWithBrowserVoice(clean);
      }
    }

    return {
      speak,
      stop,
      supported: Boolean(AudioPlayer || (synthesis?.speak && Utterance)),
    };
  }

  return {
    createOfficerSpeech,
    getOfflineSceneOpening,
    getOfflineVerdictReply,
    offlineRecallLines,
    resolveOfficerRecording,
  };
});
