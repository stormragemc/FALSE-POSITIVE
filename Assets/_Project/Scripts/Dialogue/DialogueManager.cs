using System;
using FalsePositive.Audio;
using FalsePositive.Cop;
using FalsePositive.Net;
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
        public event Action<string> TurnFailed;

        public DialogueState State { get; private set; } = DialogueState.Idle;
        public bool IsBound { get; private set; }
        public bool IsSuspended { get; private set; }
        public string SessionId { get; private set; }

        private InterrogationSidecarClient _sidecarClient;
        private VoiceActivityDetector _vad;
        private UtteranceRecorder _recorder;

        private string _pendingSceneInstruction;
        private float _listeningStartedAt;
        private int _lastSpeechOnsetDelayMs;

        /// <summary>Called once by InterrogationSceneBinder, on first load only.
        /// Re-binding an already-bound manager (e.g. a defensive re-call) is
        /// safe — event subscriptions are re-established cleanly.</summary>
        public void BindServices(
            InterrogationSidecarClient sidecarClient,
            VoiceActivityDetector vad,
            UtteranceRecorder recorder,
            string sessionId)
        {
            if (IsBound) UnbindServicesInternal();

            _sidecarClient = sidecarClient;
            _vad = vad;
            _recorder = recorder;
            SessionId = sessionId;
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

            SetState(DialogueState.Uploading);
            _vad.SetGated(true);
            string instruction = _pendingSceneInstruction;
            _pendingSceneInstruction = null;
            _sidecarClient.PostTurn(SessionId, null, 16000, 0, instruction, OnTurnSuccess, OnTurnError);
        }

        private void OnUtteranceCaptured(float[] samples, int sampleRate)
        {
            if (!IsBound || IsSuspended || State != DialogueState.Listening) return;

            PlayFiller();
            SetState(DialogueState.Uploading);
            _vad.SetGated(true);
            string instruction = _pendingSceneInstruction;
            _pendingSceneInstruction = null;
            _sidecarClient.PostTurn(
                SessionId,
                samples,
                sampleRate,
                _lastSpeechOnsetDelayMs,
                instruction,
                OnTurnSuccess,
                OnTurnError);
        }

        private void OnTurnSuccess(SidecarTurnResponse response)
        {
            // The filler clip plays to cover upload/inference latency —
            // stop it here so it doesn't keep overlapping the reply once
            // the reply itself starts (it was previously never stopped on
            // either exit path from Uploading).
            StopFiller();

            byte[] pcmBytes = Convert.FromBase64String(response.audio_b64);
            int channels = Mathf.Max(response.audio_channels, 1);
            AudioClip clip = PcmUtility.ToAudioClip(pcmBytes, response.audio_sample_rate, channels, "CopReply");

            SetState(DialogueState.Speaking);
            copMouth.Begin(copVoice.Source);
            copVoice.Play(clip);

            TurnCompleted?.Invoke(response);
        }

        private void OnTurnError(string error)
        {
            StopFiller();

            Debug.LogWarning($"[Dialogue] Turn failed: {error}");
            TurnFailed?.Invoke(error);

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
