using System;
using System.Collections;
using FalsePositive.Audio;
using FalsePositive.Cop;
using FalsePositive.Net;
using FalsePositive.UI;
using UnityEngine;
using Random = UnityEngine.Random;

namespace FalsePositive.Dialogue
{
    public enum DialogueState
    {
        Idle,
        Speaking,
        Listening,
        Uploading,
    }

    /// <summary>
    /// Orchestrates one conversational turn end to end: encode the captured
    /// utterance, POST it to the sidecar, play the reply audio, drive lip
    /// sync, and re-arm listening. Never throws into the game loop — every
    /// sidecar failure recovers by re-arming the VAD and returning to
    /// Listening rather than crashing the turn; TurnFailed exists purely for
    /// UI to show a toast.
    ///
    /// Lives in Interrogation.unity, which is loaded once (behind the
    /// consent card) and additively deactivated/reactivated for the rest of
    /// the playthrough — never unloaded. The voice services and the session
    /// id itself live in _Persistent and are handed in by
    /// InterrogationSceneBinder via BindServices, once, at first load. This
    /// class no longer mints its own session id: doing so on every scene
    /// reactivate was the bug that reset the backend's conversation history
    /// AND the HuBERT affect baseline on every trip back from a memory
    /// scene, silently breaking the interrogation's central mechanic. See
    /// docs/GAME_COMPLETION_PLAN.md A0.
    ///
    /// State machine (see plan section 5):
    ///   Idle --(officer turn)--> Speaking
    ///   Speaking --(clip ends + tail)--> Listening [VAD armed]
    ///   Listening --(UtteranceCaptured)--> Uploading [VAD disarmed, filler plays]
    ///   Uploading --(response ok)--> Speaking
    ///   Uploading --(session cap)--> Idle [+ SessionEnded for UI]
    ///   Uploading --(response error)--> Listening [+ TurnFailed for UI]
    /// Suspend/Resume gate the whole machine for memory scenes and
    /// cutscenes, where no turn may start regardless of what triggers it.
    /// </summary>
    public sealed class DialogueManager : MonoBehaviour
    {
        [SerializeField] private CopVoicePlayback copVoice;
        [SerializeField] private CopMouthController copMouth;
        [SerializeField] private AudioSource fillerSource;
        [SerializeField] private AudioClip[] fillerClips;

        public event Action<DialogueState> StateChanged;
        public event Action<SidecarTurnResponse> TurnCompleted;
        public event Action<string> SessionEnded;
        public event Action<string> TurnFailed;

        public DialogueState State { get; private set; } = DialogueState.Idle;
        public bool IsBound { get; private set; }
        public bool IsSuspended { get; private set; }
        public string SessionId { get; private set; }

        /// <summary>Set true for the whole of a phase by
        /// PhaseDialogueController when GameFlowDirector.OfflineMode is on —
        /// see docs/GAME_COMPLETION_PLAN.md §10 and the offline-mode plan:
        /// every officer turn plays a fixed authored line instead of
        /// POSTing to the sidecar, so the full flow stays walkable with no
        /// backend running.</summary>
        public bool OfflineMode { get; set; }

        private InterrogationSidecarClient _sidecarClient;
        private VoiceActivityDetector _vad;
        private UtteranceRecorder _recorder;
        private SubtitleUI _subtitles;

        private string _pendingSceneInstruction;
        private float _listeningStartedAt;
        private int _lastSpeechOnsetDelayMs;
        private int _consecutiveTurnFailures;

        // Turn-latency marks. _turnCapturedAt is the instant the recorder handed
        // over a finished utterance, which is already _turnVadWaitMs after the
        // player actually stopped talking — both are needed to report the span
        // the player experiences rather than the one the server can see.
        private float _turnCapturedAt;
        private int _turnVadWaitMs;

        /// <summary>Breakdown of the last completed turn. Also published to
        /// <see cref="TurnLatency.Last"/> so the debug overlay can read it
        /// without a reference to this component.</summary>
        public TurnLatency LastTurnLatency { get; private set; }

        private OfflineOfficerLine[] _offlineLines;
        private int _offlineIndex;

        /// <summary>Called once by InterrogationSceneBinder, on first load only.
        /// Re-binding an already-bound manager (e.g. a defensive re-call) is
        /// safe — event subscriptions are re-established cleanly.</summary>
        public void BindServices(
            InterrogationSidecarClient sidecarClient,
            VoiceActivityDetector vad,
            UtteranceRecorder recorder,
            string sessionId,
            SubtitleUI subtitles = null)
        {
            if (IsBound) UnbindServicesInternal();

            _sidecarClient = sidecarClient;
            _vad = vad;
            _recorder = recorder;
            SessionId = sessionId;
            _subtitles = subtitles;
            IsBound = true;

            if (isActiveAndEnabled) SubscribeToServices();
        }

        public void UnbindServices()
        {
            UnbindServicesInternal();
            IsBound = false;
            SessionId = null;
        }

        private void UnbindServicesInternal()
        {
            if (_recorder != null) _recorder.UtteranceCaptured -= OnUtteranceCaptured;
            if (_vad != null) _vad.SpeakingStateChanged -= OnSpeakingStateChanged;
            _sidecarClient = null;
            _vad = null;
            _recorder = null;
        }

        private void OnEnable()
        {
            if (copVoice != null) copVoice.Stopped += OnCopFinishedSpeaking;
            if (IsBound) SubscribeToServices();
        }

        private void OnDisable()
        {
            if (copVoice != null) copVoice.Stopped -= OnCopFinishedSpeaking;
            if (_recorder != null) _recorder.UtteranceCaptured -= OnUtteranceCaptured;
            if (_vad != null) _vad.SpeakingStateChanged -= OnSpeakingStateChanged;
        }

        private void SubscribeToServices()
        {
            _recorder.UtteranceCaptured += OnUtteranceCaptured;
            _vad.SpeakingStateChanged += OnSpeakingStateChanged;
        }

        /// <summary>Queues a phase briefing to ride on the NEXT PostTurn call only.
        /// The sidecar remembers it server-side and re-applies it to every turn
        /// after that automatically, so the client sends it exactly once per
        /// phase — see docs/GAME_COMPLETION_PLAN.md A7.</summary>
        public void QueueSceneInstruction(string text)
        {
            _pendingSceneInstruction = text;
        }

        /// <summary>Hard stop for memory scenes and cutscenes: gates the VAD and
        /// refuses to start a new turn until Resume(). Does not abort a turn
        /// already in flight — its callback checks IsSuspended before re-arming.</summary>
        public void Suspend()
        {
            IsSuspended = true;
            if (_vad != null) _vad.SetGated(true);
        }

        public void Resume()
        {
            IsSuspended = false;
        }

        /// <summary>Loads the fixed script for this phase and resets its
        /// cursor. Called once per phase entry by PhaseDialogueController,
        /// before OfflineMode's first RequestOfficerTurn — has no effect
        /// unless OfflineMode is also true.</summary>
        public void BeginOfflinePhase(OfflineOfficerLine[] lines)
        {
            _offlineLines = lines;
            _offlineIndex = 0;
        }

        /// <summary>Requests an audio-less turn — the officer speaks next with no
        /// witness utterance to react to. Used both for the true session opener
        /// (no prior history) and for every phase transition — the sidecar
        /// tells the two apart server-side (see Sidecar/app.py's
        /// is_session_opening) so this call looks identical from the client
        /// either way.</summary>
        public void RequestOfficerTurn(string sceneInstruction)
        {
            if (!IsBound || IsSuspended) return;
            if (!string.IsNullOrEmpty(sceneInstruction)) QueueSceneInstruction(sceneInstruction);

            // No utterance and therefore no VAD wait, but the marks still have
            // to be re-stamped: OnTurnSuccess is shared with the spoken path,
            // and a stale _turnCapturedAt would report this turn's latency as
            // however long ago the player last finished a sentence.
            _turnCapturedAt = Time.realtimeSinceStartup;
            _turnVadWaitMs = 0;

            SetState(DialogueState.Uploading);
            _vad.SetGated(true);
            string instruction = _pendingSceneInstruction;
            _pendingSceneInstruction = null;

            if (OfflineMode)
            {
                PlayOfflineTurn();
                return;
            }

            _sidecarClient.PostTurn(
                SessionId,
                null,
                16000,
                0,
                instruction,
                OnTurnSuccess,
                OnSessionEnded,
                OnTurnError);
        }

        private void OnUtteranceCaptured(float[] samples, int sampleRate)
        {
            if (!IsBound || IsSuspended || State != DialogueState.Listening) return;

            _turnCapturedAt = Time.realtimeSinceStartup;
            _turnVadWaitMs = _vad != null
                ? Mathf.RoundToInt(_vad.LastSilenceHeldSeconds * 1000f)
                : 0;

            PlayFiller();
            SetState(DialogueState.Uploading);
            _vad.SetGated(true);

            if (OfflineMode)
            {
                StopFiller();
                _pendingSceneInstruction = null;
                PlayOfflineTurn();
                return;
            }

            string instruction = _pendingSceneInstruction;
            _pendingSceneInstruction = null;
            _sidecarClient.PostTurn(
                SessionId,
                samples,
                sampleRate,
                _lastSpeechOnsetDelayMs,
                instruction,
                OnTurnSuccess,
                OnSessionEnded,
                OnTurnError);
        }

        /// <summary>Offline-mode equivalent of a real turn: plays the next
        /// authored line instead of asking the sidecar for one. Never
        /// fabricates prosody — SessionScore.RecordTurn only counts
        /// Composure from reliable turns, and marking these unreliable is
        /// what keeps that honest (docs/GAME_COMPLETION_PLAN.md §10).</summary>
        private void PlayOfflineTurn()
        {
            OfflineOfficerLine offlineLine = null;
            if (_offlineLines != null && _offlineLines.Length > 0)
            {
                int index = Mathf.Min(_offlineIndex, _offlineLines.Length - 1);
                offlineLine = _offlineLines[index];
                _offlineIndex = index + 1;
            }

            string line = offlineLine != null ? offlineLine.line : string.Empty;
            AudioClip clip = offlineLine != null ? offlineLine.voClip : null;
            float holdSeconds = offlineLine != null ? offlineLine.holdSecondsIfNoClip : 3f;

            var response = new SidecarTurnResponse
            {
                ok = true,
                transcript = string.Empty,
                reply_text = line,
                prosody = new SidecarProsodySignal
                {
                    available = false,
                    reliable = false,
                    reliability_reason = "offline mode — no prosody analysis",
                },
            };

            SetState(DialogueState.Speaking);
            if (_subtitles != null) _subtitles.Show("SPASSKY", line, clip != null ? clip.length : holdSeconds);

            if (clip != null)
            {
                copMouth.Begin(copVoice.Source);
                copVoice.Play(clip);
            }
            else
            {
                StartCoroutine(HoldThenFinishOfflineTurn(holdSeconds));
            }

            TurnCompleted?.Invoke(response);
        }

        private IEnumerator HoldThenFinishOfflineTurn(float holdSeconds)
        {
            yield return new WaitForSeconds(Mathf.Max(0.1f, holdSeconds));
            OnCopFinishedSpeaking();
        }

        /// <summary>Called once the officer's voice has actually started, which
        /// is the only moment the wait is genuinely over from the player's side.
        /// The VAD silence is added back in because it elapsed before the turn
        /// began: leaving it out would report a number the player never felt.
        /// </summary>
        private void RecordTurnLatency(int decodeMs)
        {
            var latency = new TurnLatency
            {
                vadWaitMs = _turnVadWaitMs,
                wireMs = _sidecarClient != null ? _sidecarClient.LastWireMs : 0,
                serverMs = _sidecarClient != null ? _sidecarClient.LastServerMs : 0,
                decodeMs = decodeMs,
                totalMs = _turnVadWaitMs
                    + Mathf.RoundToInt((Time.realtimeSinceStartup - _turnCapturedAt) * 1000f),
                wireBytes = _sidecarClient != null ? _sidecarClient.LastDownloadBytes : 0,
            };

            LastTurnLatency = latency;
            TurnLatency.Publish(latency);
        }

        private void OnTurnSuccess(SidecarTurnResponse response)
        {
            // The filler clip plays to cover upload/inference latency —
            // stop it here so it doesn't keep overlapping the reply once
            // the reply itself starts (it was previously never stopped on
            // either exit path from Uploading).
            StopFiller();
            _consecutiveTurnFailures = 0;

            // A backend response with no audio (empty/malformed audio_b64)
            // must not throw inside this class — it is documented as never
            // throwing into the game loop, but Convert.FromBase64String on
            // an empty/null string previously did exactly that. Fall back
            // to subtitling the reply text and holding, same shape as the
            // offline no-clip path.
            if (string.IsNullOrEmpty(response.audio_b64))
            {
                Debug.LogWarning("[Dialogue] Turn succeeded with no audio_b64 — subtitling reply_text instead.");
                SetState(DialogueState.Speaking);
                if (_subtitles != null) _subtitles.Show("SPASSKY", response.reply_text, 3.5f);
                StartCoroutine(HoldThenFinishOfflineTurn(3.5f));
                TurnCompleted?.Invoke(response);
                return;
            }

            float decodeStart = Time.realtimeSinceStartup;
            byte[] pcmBytes = Convert.FromBase64String(response.audio_b64);
            int channels = Mathf.Max(response.audio_channels, 1);
            AudioClip clip = PcmUtility.ToAudioClip(pcmBytes, response.audio_sample_rate, channels, "CopReply");
            int decodeMs = Mathf.RoundToInt((Time.realtimeSinceStartup - decodeStart) * 1000f);

            SetState(DialogueState.Speaking);
            copMouth.Begin(copVoice.Source);
            copVoice.Play(clip);

            RecordTurnLatency(decodeMs);
            TurnCompleted?.Invoke(response);
        }

        private void OnTurnError(string error)
        {
            StopFiller();

            Debug.LogWarning($"[Dialogue] Turn failed: {error}");

            _consecutiveTurnFailures++;
            if (_consecutiveTurnFailures == 3)
            {
                // This used to be console-only (Debug.LogError), invisible in
                // a shipped build. Fold it into the same TurnFailed event the
                // debug overlay already renders, so a misconfigured key or a
                // dead backend is visible without opening the console.
                const string escalation = "The interrogation service appears unreachable after three " +
                    "failed turns. Return to the main menu and use \"Offline demo\" to play the full " +
                    "story without it.";
                Debug.LogError($"[Dialogue] Three consecutive turn failures: {escalation}");
                TurnFailed?.Invoke($"{error}\n{escalation}");
            }
            else
            {
                TurnFailed?.Invoke(error);
            }

            // Never hard-fail the conversation — just re-arm listening so
            // the player can try again, unless we've been suspended in the
            // meantime (e.g. a phase change fired while this turn was in
            // flight), in which case leave the VAD gated.
            if (IsSuspended)
            {
                SetState(DialogueState.Idle);
            }
            else
            {
                BeginListening();
            }
        }

        private void OnSessionEnded(SidecarTurnResponse response)
        {
            StopFiller();
            if (_vad != null) _vad.SetGated(true);
            SetState(DialogueState.Idle);
            SessionEnded?.Invoke(response.reply_text);
        }

        private void OnCopFinishedSpeaking()
        {
            if (State != DialogueState.Speaking) return;
            copMouth.Stop();
            if (IsSuspended)
            {
                SetState(DialogueState.Idle);
            }
            else
            {
                BeginListening();
            }
        }

        private void OnSpeakingStateChanged(bool speaking)
        {
            if (!speaking || State != DialogueState.Listening) return;
            _lastSpeechOnsetDelayMs = Mathf.Max(
                0,
                Mathf.RoundToInt((Time.realtimeSinceStartup - _listeningStartedAt) * 1000f));
        }

        private void BeginListening()
        {
            _lastSpeechOnsetDelayMs = 0;
            _listeningStartedAt = Time.realtimeSinceStartup;
            SetState(DialogueState.Listening);
            if (_vad != null) _vad.SetGated(false);
        }

        private void PlayFiller()
        {
            if (fillerSource == null || fillerClips == null || fillerClips.Length == 0) return;
            AudioClip clip = fillerClips[Random.Range(0, fillerClips.Length)];
            fillerSource.PlayOneShot(clip);
        }

        private void StopFiller()
        {
            if (fillerSource != null && fillerSource.isPlaying) fillerSource.Stop();
        }

        private void SetState(DialogueState newState)
        {
            State = newState;
            StateChanged?.Invoke(newState);
        }
    }
}
