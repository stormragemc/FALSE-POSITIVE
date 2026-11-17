/** Browser audio boundary. Audio remains in memory; credentials never enter this module. */
const SAMPLE_RATE = 16000;
const MAX_RECORDING_MS = 19000;
const RECORDINGS = new Map([
  ["I'm Officer Spassky. Nick is dead, and right now you're one of the suspects. I've already spoken to the others. Take your time and tell me everything you remember from last night.", 'SPASSKY-005'],
  ["So. What's the last thing you remember?", 'SPASSKY-006'],
  ['Who else was drinking with you?', 'SPASSKY-007'],
  ['How much did you have?', 'SPASSKY-008'],
  ['Tell me about the argument with Nick. What was it really about?', 'SPASSKY-009'],
  ['What did Nick do after the argument?', 'SPASSKY-011'],
  ['What did you do next?', 'SPASSKY-013'],
  ['Was the door locked when you opened it?', 'SPASSKY-015'],
  ["Walk me through the morning. Start with Priya's scream.", 'SPASSKY-020'],
  ['What happened to Nick?', 'SPASSKY-024'],
  ['Tell me why I should spare your life.', 'SPASSKY-037'],
  ["So you think it's Aaron, huh? Tell me why.", 'SPASSKY-040'],
  ["So you think it's Ivy, huh? Tell me why.", 'SPASSKY-043'],
  ["So you think it's Priya, huh? Tell me why.", 'SPASSKY-046'],
  ["You were the only one who couldn't tell me where you were.", 'SPASSKY-053'],
  ['He locked it. You unlocked it. Only one of those was a decision.', 'SPASSKY-054'],
  ["She agreed with you. That's not the same as it being true.", 'SPASSKY-055'],
  ["She's the one who called us. Sit with that.", 'SPASSKY-056'],
  ['Who, David?', 'SPASSKY-062'],
]);

export function monoSamples(buffer) {
  const output = new Float32Array(buffer.length);
  for (let channel = 0; channel < buffer.numberOfChannels; channel++) {
    const samples = buffer.getChannelData(channel);
    for (let i = 0; i < output.length; i++) output[i] += samples[i] / buffer.numberOfChannels;
  }
  return output;
}

export function encodePcm16(samples, sourceRate, targetRate = SAMPLE_RATE) {
  if (!samples?.length || !Number.isFinite(sourceRate) || sourceRate <= 0) throw new Error('The recording contains no decodable audio.');
  const length = Math.max(1, Math.round(samples.length * targetRate / sourceRate));
  const pcm = new ArrayBuffer(length * 2);
  const view = new DataView(pcm);
  const ratio = sourceRate / targetRate;
  for (let i = 0; i < length; i++) {
    const position = i * ratio;
    const left = Math.min(samples.length - 1, Math.floor(position));
    const right = Math.min(samples.length - 1, left + 1);
    const blend = position - left;
    const value = Math.max(-1, Math.min(1, samples[left] * (1 - blend) + samples[right] * blend));
    view.setInt16(i * 2, value < 0 ? value * 32768 : value * 32767, true);
  }
  return pcm;
}

export function createVoiceEngine({
  audioBase = './audio/spassky',
  onStatus = () => {}, onLevel = () => {}, onSpeaking = () => {},
  onRecordingReady = () => {}, browserTranscription = false,
  fetchImpl = (...args) => fetch(...args),
} = {}) {
  let context;
  let stream;
  let recorder;
  let recognition;
  let recordingPromise;
  let lastRecording;
  let recordingTimer;
  let meterFrame;
  let activeAudio;
  let activeSource;
  let playbackGeneration = 0;
  let recordingGeneration = 0;
  let destroyed = false;
  const requests = new Set();

  async function audioContext() {
    if (!context) {
      const Constructor = window.AudioContext || window.webkitAudioContext;
      if (!Constructor) throw new Error('Audio playback is unavailable. Captions and typed statements still work.');
      context = new Constructor();
    }
    if (context.state === 'suspended') await context.resume();
    return context;
  }

  function stopSpeaking() {
    playbackGeneration++;
    if (activeAudio) { activeAudio.pause(); activeAudio = undefined; }
    if (activeSource) { try { activeSource.stop(); } catch { /* already ended */ } activeSource = undefined; }
    window.speechSynthesis?.cancel();
    onSpeaking(false);
  }

  function browserSpeak(text, generation) {
    if (generation !== playbackGeneration) return false;
    if (!window.speechSynthesis || !window.SpeechSynthesisUtterance) { onSpeaking(false); return false; }
    const speech = new window.SpeechSynthesisUtterance(text);
    const voices = window.speechSynthesis.getVoices();
    speech.voice = voices.find(v => /^en/.test(v.lang) && /daniel|alex|george|arthur/i.test(v.name)) || voices.find(v => /^en/.test(v.lang)) || null;
    speech.lang = speech.voice?.lang || 'en-GB';
    speech.rate = 0.88;
    speech.pitch = 0.8;
    speech.onend = () => { if (generation === playbackGeneration) onSpeaking(false); };
    speech.onerror = speech.onend;
    onSpeaking(true);
    window.speechSynthesis.speak(speech);
    return true;
  }

  async function speak(text, payload = {}) {
    const clean = String(text || '').trim();
    stopSpeaking();
    if (!clean || destroyed) return false;
    const generation = playbackGeneration;
    if (payload.audio_b64) {
      try {
        const ctx = await audioContext();
        const bytes = Uint8Array.from(atob(payload.audio_b64), c => c.charCodeAt(0));
        const channels = Math.max(1, Number(payload.audio_channels) || 1);
        const count = Math.floor(bytes.length / (2 * channels));
        const buffer = ctx.createBuffer(channels, count, Number(payload.audio_sample_rate) || 24000);
        const view = new DataView(bytes.buffer, bytes.byteOffset, bytes.byteLength);
        for (let channel = 0; channel < channels; channel++) {
          const data = buffer.getChannelData(channel);
          for (let i = 0; i < count; i++) data[i] = view.getInt16((i * channels + channel) * 2, true) / 32768;
        }
        if (generation !== playbackGeneration) return false;
        const source = ctx.createBufferSource();
        source.buffer = buffer;
        source.connect(ctx.destination);
        source.onended = () => { if (generation === playbackGeneration) { activeSource = undefined; onSpeaking(false); } };
        activeSource = source;
        onSpeaking(true);
        source.start();
        return true;
      } catch { /* Keep the returned text available when live PCM cannot play. */ }
    }
    const recording = RECORDINGS.get(clean);
    if (!recording) return browserSpeak(clean, generation);
    const player = new Audio(`${audioBase.replace(/\/$/, '')}/${recording}.wav`);
    activeAudio = player;
    player.preload = 'auto';
    let fellBack = false;
    const fallback = () => {
      if (fellBack || generation !== playbackGeneration) return;
      fellBack = true;
      activeAudio = undefined;
      browserSpeak(clean, generation);
    };
    player.onended = () => { if (generation === playbackGeneration) { activeAudio = undefined; onSpeaking(false); } };
    player.onerror = fallback;
    onSpeaking(true);
    try { await player.play(); return true; } catch { fallback(); return false; }
  }

  function releaseMicrophone() {
    clearTimeout(recordingTimer);
    cancelAnimationFrame(meterFrame);
    stream?.getTracks().forEach(track => track.stop());
    stream = undefined;
    onLevel(0);
  }

  async function startRecording() {
    if (destroyed) throw new Error('Start a new case before recording.');
    if (recorder?.state === 'recording') return;
    if (!navigator.mediaDevices?.getUserMedia || !window.MediaRecorder) throw new Error('Microphone recording is unavailable in this browser. Use the statement field instead.');
    stopSpeaking();
    lastRecording = undefined;
    const generation = ++recordingGeneration;
    try {
      stream = await navigator.mediaDevices.getUserMedia({ audio: { channelCount: 1, echoCancellation: true, noiseSuppression: true }, video: false });
      if (destroyed || generation !== recordingGeneration) { releaseMicrophone(); return; }
      const ctx = await audioContext();
      const input = ctx.createMediaStreamSource(stream);
      const analyser = ctx.createAnalyser();
      analyser.fftSize = 512;
      input.connect(analyser);
      const samples = new Float32Array(analyser.fftSize);
      let peak = 0;
      const measure = () => {
        if (generation !== recordingGeneration || recorder?.state !== 'recording') return;
        analyser.getFloatTimeDomainData(samples);
        let squared = 0;
        for (const value of samples) squared += value * value;
        const rms = Math.sqrt(squared / samples.length);
        peak = Math.max(peak, rms);
        onLevel(rms);
        meterFrame = requestAnimationFrame(measure);
      };
      let transcript = '';
      let interim = '';
      // Browser transcription is opt-in: browser providers may process that audio.
      if (browserTranscription) {
        const Recognition = window.SpeechRecognition || window.webkitSpeechRecognition;
        if (Recognition) {
          try {
            recognition = new Recognition();
            recognition.continuous = true;
            recognition.interimResults = true;
            recognition.lang = 'en-US';
            recognition.onresult = event => {
              interim = '';
              for (let i = event.resultIndex; i < event.results.length; i++) {
                if (event.results[i].isFinal) transcript += `${event.results[i][0].transcript} `;
                else interim += event.results[i][0].transcript;
              }
            };
            recognition.start();
          } catch { recognition = undefined; }
        }
      }
      const types = ['audio/webm;codecs=opus', 'audio/ogg;codecs=opus', 'audio/mp4', 'audio/webm'];
      const mimeType = types.find(type => window.MediaRecorder.isTypeSupported?.(type));
      const current = mimeType ? new window.MediaRecorder(stream, { mimeType }) : new window.MediaRecorder(stream);
      recorder = current;
      const chunks = [];
      const started = performance.now();
      current.ondataavailable = event => { if (event.data?.size) chunks.push(event.data); };
      recordingPromise = new Promise((resolve, reject) => {
        current.onerror = () => { releaseMicrophone(); onStatus('idle'); reject(new Error('The microphone stopped unexpectedly. Try again or write your statement.')); };
        current.onstop = () => {
          releaseMicrophone();
          try { recognition?.stop(); } catch { /* already ended */ }
          recognition = undefined;
          const result = { blob: new Blob(chunks, { type: current.mimeType || mimeType || 'audio/webm' }), transcript: (transcript + interim).trim(), peak, durationMs: performance.now() - started };
          if (generation !== recordingGeneration) { resolve(null); return; }
          lastRecording = result;
          onStatus('idle');
          onRecordingReady(result);
          resolve(result);
        };
      });
      // The UI may await the same promise later; prevent an early recorder error being unhandled.
      recordingPromise.catch(() => {});
      current.start(200);
      onStatus('recording');
      measure();
      recordingTimer = setTimeout(() => { if (current.state === 'recording') current.stop(); }, MAX_RECORDING_MS);
    } catch (error) {
      releaseMicrophone();
      onStatus('idle');
      if (error.name === 'NotAllowedError' || error.name === 'SecurityError') throw new Error('Microphone permission was denied. You can complete the case with typed statements.');
      if (error.name === 'NotFoundError') throw new Error('No microphone was found. Use the statement field instead.');
      throw error;
    }
  }

  async function stopRecording() {
    if (recorder?.state === 'recording') recorder.stop();
    return recordingPromise ? await recordingPromise : lastRecording || null;
  }

  async function request(path, form, { signal } = {}) {
    const controller = new AbortController();
    const abort = () => controller.abort();
    signal?.addEventListener('abort', abort, { once: true });
    if (signal?.aborted) controller.abort();
    requests.add(controller);
    const timeout = setTimeout(abort, 60000);
    onStatus('processing');
    try {
      const response = await fetchImpl(path, { method: 'POST', body: form, credentials: 'same-origin', signal: controller.signal });
      let payload;
      try { payload = await response.json(); } catch { throw new Error('The AI gateway returned an unreadable reply. Retry or use the labelled scripted mode.'); }
      if (!response.ok || payload.ok === false) throw new Error(payload.error || `The AI gateway is unavailable (HTTP ${response.status}).`);
      return payload;
    } catch (error) {
      if (error.name === 'AbortError') throw new Error('The AI request was cancelled or timed out. Your statement was not committed locally.');
      throw error;
    } finally {
      clearTimeout(timeout);
      signal?.removeEventListener('abort', abort);
      requests.delete(controller);
      onStatus('idle');
    }
  }

  function purposeForm(purpose) {
    if (!['p2', 'p3'].includes(purpose)) throw new Error('The officer is not accepting a statement in this phase.');
    const form = new FormData();
    form.append('purpose', purpose);
    return form;
  }

  async function submitAudio(recording, purpose, options) {
    const blob = recording?.blob || recording;
    if (!(blob instanceof Blob) || !blob.size) throw new Error('Record an answer before sending it.');
    if (recording.durationMs !== undefined && recording.durationMs < 600) throw new Error('That answer was too short to hear. Speak for at least a second, or write it.');
    const ctx = await audioContext();
    let decoded;
    try { decoded = await ctx.decodeAudioData(await blob.arrayBuffer()); } catch { throw new Error('This browser could not decode its recording. Please write your statement instead.'); }
    if (decoded.duration > 20.1) throw new Error('Keep each spoken answer under 20 seconds.');
    const pcm = encodePcm16(monoSamples(decoded), decoded.sampleRate);
    const form = purposeForm(purpose);
    form.append('audio', new Blob([pcm], { type: 'application/octet-stream' }), 'statement.pcm');
    return request('/api/turn', form, options);
  }

  async function submitText(text, purpose, options) {
    const statement = String(text || '').trim();
    if (!statement) throw new Error('Write your statement first.');
    if (statement.length > 2000) throw new Error('Keep each statement under 2000 characters.');
    const form = purposeForm(purpose);
    form.append('statement', statement);
    return request('/api/statement', form, options);
  }

  async function resetSession() {
    recordingGeneration++;
    requests.forEach(controller => controller.abort());
    stopSpeaking();
    if (recorder?.state === 'recording') recorder.stop();
    releaseMicrophone();
    lastRecording = undefined;
    recordingPromise = undefined;
    await fetchImpl('/api/session/reset', { method: 'POST', credentials: 'same-origin' }).catch(() => {});
  }

  function dispose() {
    destroyed = true;
    recordingGeneration++;
    requests.forEach(controller => controller.abort());
    stopSpeaking();
    if (recorder?.state === 'recording') recorder.stop();
    releaseMicrophone();
    try { recognition?.abort(); } catch { /* already ended */ }
    context?.close().catch(() => {});
  }

  return { startRecording, stopRecording, submitAudio, submitText, speak, stopSpeaking, resetSession, dispose,
    get recording() { return recorder?.state === 'recording'; },
    get lastRecording() { return lastRecording; },
    get supported() { return Boolean(typeof navigator !== 'undefined' && navigator.mediaDevices?.getUserMedia && typeof window !== 'undefined' && window.MediaRecorder); },
  };
}
