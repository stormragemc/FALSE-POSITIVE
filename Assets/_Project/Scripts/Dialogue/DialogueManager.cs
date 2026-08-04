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
    /// State machine (see plan section 5):
    ///   Idle --(opening line)--> Speaking
    ///   Speaking --(clip ends + tail)--> Listening [VAD armed]
    ///   Listening --(UtteranceCaptured)--> Uploading [VAD disarmed, filler plays]
    ///   Uploading --(response ok)--> Speaking
    ///   Uploading --(response error)--> Listening [+ TurnFailed for UI]
    /// </summary>
    public sealed class DialogueManager : MonoBehaviour
    {
        [SerializeField] private InterrogationSidecarClient sidecarClient;
        [SerializeField] private VoiceActivityDetector vad;
        [SerializeField] private UtteranceRecorder recorder;
        [SerializeField] private CopVoicePlayback copVoice;
        [SerializeField] private CopMouthController copMouth;
        [SerializeField] private AudioSource fillerSource;
        [SerializeField] private AudioClip[] fillerClips;

        public event Action<DialogueState> StateChanged;
        public event Action<SidecarTurnResponse> TurnCompleted;
        public event Action<string> TurnFailed;

        public DialogueState State { get; private set; } = DialogueState.Idle;

        private string _sessionId;
        private float _listeningStartedAt;
        private int _lastSpeechOnsetDelayMs;

        private void Awake()
        {
            _sessionId = Guid.NewGuid().ToString("N");
        }

        private void OnEnable()
        {
            recorder.UtteranceCaptured += OnUtteranceCaptured;
            copVoice.Stopped += OnCopFinishedSpeaking;
            vad.SpeakingStateChanged += OnSpeakingStateChanged;
        }

        private void OnDisable()
        {
            recorder.UtteranceCaptured -= OnUtteranceCaptured;
            copVoice.Stopped -= OnCopFinishedSpeaking;
            vad.SpeakingStateChanged -= OnSpeakingStateChanged;
        }

        /// <summary>Called once by GameBootstrap once the sidecar is confirmed healthy — requests the officer's scripted opening line (no audio part = opening trigger, see Sidecar/app.py).</summary>
        public void BeginConversation()
        {
            SetState(DialogueState.Uploading);
            vad.SetGated(true);
            sidecarClient.PostTurn(_sessionId, null, 16000, 0, OnTurnSuccess, OnTurnError);
        }

        private void OnUtteranceCaptured(float[] samples, int sampleRate)
        {
            if (State != DialogueState.Listening) return;

            PlayFiller();
            SetState(DialogueState.Uploading);
            vad.SetGated(true);
            sidecarClient.PostTurn(
                _sessionId,
                samples,
                sampleRate,
                _lastSpeechOnsetDelayMs,
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
            // the player can try again.
            BeginListening();
        }

        private void OnCopFinishedSpeaking()
        {
            if (State != DialogueState.Speaking) return;
            copMouth.Stop();
            BeginListening();
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
            vad.SetGated(false);
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
