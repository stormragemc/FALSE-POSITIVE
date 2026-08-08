using System;
using FalsePositive.Audio;
using UnityEngine;

namespace FalsePositive.Voice
{
    /// <summary>
    /// The client-side loudness gate behind the M1_Night "call for Nick" beat
    /// (docs/STORY_SCRIPT.md §4) — 100% local, zero backend dependency.
    /// Subscribes to UtteranceRecorder directly rather than going through
    /// DialogueManager, because this moment isn't a conversational turn at
    /// all: nothing is transcribed or sent anywhere, only evaluated for
    /// loudness. GameFlowDirector.RequestSpokenPrompt(requireLoud: true) is
    /// the only intended caller.
    /// </summary>
    public sealed class LoudnessGate : MonoBehaviour
    {
        [SerializeField] private UtteranceRecorder recorder;
        [SerializeField] private int evaluationWindowSamples = 800; // 50ms at 16kHz

        public bool IsArmed { get; private set; }
        public int Attempts { get; private set; }

        /// <summary>Fires once a captured utterance clears the armed threshold.</summary>
        public event Action Satisfied;
        /// <summary>Fires on every attempt that does NOT clear the threshold —
        /// stays armed, no cap on attempts.</summary>
        public event Action TooQuiet;

        private float _thresholdRms;

        public void Arm(float loudReferenceRms, float factor)
        {
            _thresholdRms = Mathf.Max(0f, loudReferenceRms) * Mathf.Max(0f, factor);
            Attempts = 0;
            IsArmed = true;
            // Idempotent: unsubscribe first so a re-arm (e.g. two RequestSpokenPrompt
            // calls in a row without a Disarm between them) can never double-subscribe
            // and fire OnUtteranceCaptured twice per utterance. -= on a handler that
            // was never added is a legal no-op.
            recorder.UtteranceCaptured -= OnUtteranceCaptured;
            recorder.UtteranceCaptured += OnUtteranceCaptured;
        }

        public void Disarm()
        {
            if (!IsArmed) return;
            IsArmed = false;
            recorder.UtteranceCaptured -= OnUtteranceCaptured;
        }

        private void OnDisable() => Disarm();

        private void OnUtteranceCaptured(float[] samples, int sampleRate)
        {
            if (!IsArmed) return;
            Attempts++;

            if (LoudnessEvaluator.ClearsGate(samples, evaluationWindowSamples, _thresholdRms))
            {
                Disarm();
                Satisfied?.Invoke();
            }
            else
            {
                TooQuiet?.Invoke();
            }
        }
    }
}
