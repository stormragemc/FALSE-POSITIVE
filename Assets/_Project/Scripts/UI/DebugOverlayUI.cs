using FalsePositive.Audio;
using FalsePositive.Dialogue;
using FalsePositive.Net;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace FalsePositive.UI
{
    /// <summary>
    /// Debug HUD: per-stage sidecar timings, VAD state, live RMS, the last
    /// transcript/emotion/error, the boot status line while the sidecar is
    /// starting, and (A8) story-mark coverage. Toggle with F1. Uses the new
    /// Input System's Keyboard API directly — no legacy Input calls anywhere
    /// in this project.
    ///
    /// Lives in _Persistent; its VoiceActivityDetector and DialogueManager
    /// are scene-local objects it does not own (the VAD lives in
    /// _Persistent too, but DialogueManager lives in Interrogation), so both
    /// are supplied at runtime via Bind rather than serialized in the
    /// inspector — see docs/GAME_COMPLETION_PLAN.md A0.
    /// </summary>
    public sealed class DebugOverlayUI : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private Text bootStatusText;
        [SerializeField] private Text stateText;
        [SerializeField] private Text vadText;
        [SerializeField] private Text lastTurnText;
        [SerializeField] private Text marksText;

        private VoiceActivityDetector _vad;
        private DialogueManager _dialogueManager;

        /// <summary>Called by InterrogationSceneBinder once both dependencies exist.
        /// Safe to call again (e.g. a future re-bind) — re-subscribes cleanly.</summary>
        public void Bind(VoiceActivityDetector vad, DialogueManager dialogueManager)
        {
            Unbind();
            _vad = vad;
            _dialogueManager = dialogueManager;
            if (_dialogueManager != null)
            {
                _dialogueManager.StateChanged += OnDialogueStateChanged;
                _dialogueManager.TurnCompleted += OnTurnCompleted;
                _dialogueManager.SessionEnded += OnSessionEnded;
                _dialogueManager.TurnFailed += OnTurnFailed;
            }
        }

        public void Unbind()
        {
            if (_dialogueManager != null)
            {
                _dialogueManager.StateChanged -= OnDialogueStateChanged;
                _dialogueManager.TurnCompleted -= OnTurnCompleted;
                _dialogueManager.SessionEnded -= OnSessionEnded;
                _dialogueManager.TurnFailed -= OnTurnFailed;
            }
            _dialogueManager = null;
            _vad = null;
        }

        private void OnDisable() => Unbind();

        private void Update()
        {
            if (panel != null && Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame)
            {
                panel.SetActive(!panel.activeSelf);
            }

            if (vadText != null && _vad != null)
            {
                string gated = _vad.Gated ? " [gated]" : "";
                string calibrated = _vad.IsCalibrated ? "" : " [uncalibrated]";
                vadText.text = $"VAD: {(_vad.IsSpeaking ? "speaking" : "idle")}{gated}{calibrated}  rms={_vad.DisplayRms:F4}";
            }
        }

        public void SetBootStatus(string text)
        {
            if (bootStatusText != null) bootStatusText.text = text;
        }

        /// <summary>Renders the seven story marks and the turn count/cap for the
        /// current phase — see A8's StoryMarkTracker, the only caller.</summary>
        public void SetMarksStatus(string text)
        {
            if (marksText != null) marksText.text = text;
        }

        private void OnDialogueStateChanged(DialogueState state)
        {
            if (stateText != null) stateText.text = $"Dialogue: {state}";
        }

        private void OnTurnCompleted(SidecarTurnResponse r)
        {
            if (lastTurnText == null) return;
            SidecarProsodySignal p = r.prosody;
            string flags = p?.flags != null && p.flags.Length > 0
                ? string.Join(",", p.flags)
                : "-";
            bool hasProsody = p != null && !string.IsNullOrEmpty(p.version);
            string affect = !hasProsody
                ? "prosody: legacy response"
                : $"prosody: {(p.reliable ? "usable" : p.reliability_reason)} " +
                  $"signal={p.confidence_in_signal:F2} tension={p.tension:F2} " +
                  $"referenceΔ={(p.reference_comparison_available ? p.hubert_baseline_distance.ToString("F2") : "n/a")} " +
                  $"relative={(p.reference_comparison_available ? p.hubert_reference_change.ToString("F2") + "x" : "n/a")} trend={p.trend}\n" +
                  $"onset={p.onset_delay_ms}ms calibration={p.calibration_state} " +
                  $"flags={flags}";
            string emotion = hasProsody && !p.reliable
                ? $"emotion: raw {r.emotion} ({r.emotion_confidence:F2}) [suppressed]"
                : $"emotion: {r.emotion} ({r.emotion_confidence:F2})";
            lastTurnText.text =
                $"transcript: {r.transcript}\n" +
                $"{emotion}\n" +
                $"{affect}\n" +
                $"reply: {r.reply_text}\n" +
                $"stt={r.stt_ms}ms ser={r.ser_ms}ms llm={r.llm_ms}ms tts={r.tts_ms}ms total={r.total_ms}ms";
        }

        private void OnTurnFailed(string error)
        {
            if (lastTurnText != null) lastTurnText.text = $"ERROR: {error}";
        }

        private void OnSessionEnded(string replyText)
        {
            if (lastTurnText != null) lastTurnText.text = $"SESSION ENDED: {replyText}";
        }
    }
}
