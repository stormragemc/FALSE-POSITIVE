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
    /// transcript/emotion/error, and the boot status line while the sidecar
    /// is starting. Toggle with F1. Uses the new Input System's Keyboard API
    /// directly — no legacy Input calls anywhere in this project.
    /// </summary>
    public sealed class DebugOverlayUI : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private Text bootStatusText;
        [SerializeField] private Text stateText;
        [SerializeField] private Text vadText;
        [SerializeField] private Text lastTurnText;

        [SerializeField] private VoiceActivityDetector vad;
        [SerializeField] private DialogueManager dialogueManager;

        private void OnEnable()
        {
            if (dialogueManager == null) return;
            dialogueManager.StateChanged += OnDialogueStateChanged;
            dialogueManager.TurnCompleted += OnTurnCompleted;
            dialogueManager.SessionEnded += OnSessionEnded;
            dialogueManager.TurnFailed += OnTurnFailed;
        }

        private void OnDisable()
        {
            if (dialogueManager == null) return;
            dialogueManager.StateChanged -= OnDialogueStateChanged;
            dialogueManager.TurnCompleted -= OnTurnCompleted;
            dialogueManager.SessionEnded -= OnSessionEnded;
            dialogueManager.TurnFailed -= OnTurnFailed;
        }

        private void Update()
        {
            if (panel != null && Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame)
            {
                panel.SetActive(!panel.activeSelf);
            }

            if (vadText != null && vad != null)
            {
                string gated = vad.Gated ? " [gated]" : "";
                vadText.text = $"VAD: {(vad.IsSpeaking ? "speaking" : "idle")}{gated}  rms={vad.DisplayRms:F4}";
            }
        }

        public void SetBootStatus(string text)
        {
            if (bootStatusText != null) bootStatusText.text = text;
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
